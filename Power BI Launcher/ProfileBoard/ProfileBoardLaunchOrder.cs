using System;
using System.Collections.Generic;
using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Windows.Foundation;
using Windows.System;

namespace PowerBILauncher;

/// <summary>
/// Board-local Launch Order reorder chrome: six-dot grip, whole-card drag, gap
/// insertion indicator, and nudge. Drag runs on pointer capture, never WinUI OLE
/// drag - releasing an <c>IDragOperation</c> after focus loss native-crashes
/// WASDK 1.7 (fail-fast 0xc0000409). Does not reflow the Grid mid-drag.
/// </summary>
internal static class ProfileBoardLaunchOrder
{
    private const double GapWidenDip = 12;
    private const double GapBarThickness = 3;

    // Fluent System Icons reOrderDotsVertical 20×20 centers (radius 1).
    private static readonly Point[] ReOrderDotsCenters =
    [
        new(7, 4), new(7, 10), new(7, 16),
        new(13, 4), new(13, 10), new(13, 16)
    ];

    private static Grid? _board;
    private static Canvas? _insertionLayer;
    private static Func<IReadOnlyList<Button>>? _cardButtons;
    private static Border? _gapSlot;
    private static Border? _gapBar;
    private static Guid? _draggingId;
    private static Button? _dragSourceCard;
    private static bool _dragSessionActive;
    /// <summary>
    /// True from drag start until pending apply/relayout finishes - blocks overlapping fast drags.
    /// </summary>
    private static bool _boardBusy;
    private static Action? _onDragSessionEnded;
    private static Action? _onFlyoutOpenChanged;
    private static Point _lastBoardPoint;
    private static UIElement? _pointerRoot;
    private static PointerEventHandler? _rootMoved;
    private static PointerEventHandler? _rootReleased;

    /// <summary>
    /// True while a Launch Order drag session is active or a drop apply is still settling.
    /// </summary>
    public static bool IsDragSessionActive =>
        _dragSessionActive || _draggingId is not null || _boardBusy;

    /// <summary>
    /// Clears the post-drop busy gate after model apply + deferred relayout have finished.
    /// </summary>
    public static void NotifyApplyFinished()
    {
        Microsoft.UI.Dispatching.DispatcherQueue? queue = _board?.DispatcherQueue;
        if (queue is null)
        {
            _boardBusy = false;
            return;
        }

        // Extra Low tick after relayout so PointerExited / More chrome do not mutate
        // the tree in the same turn as Grid.SetRow (native fail-fast 0xc0000409).
        _ = queue.TryEnqueue(
            Microsoft.UI.Dispatching.DispatcherQueuePriority.Low,
            () =>
            {
                _boardBusy = false;
                try
                {
                    _onDragSessionEnded?.Invoke();
                }
                catch
                {
                    // Host catch-up must not break settle.
                }

            });
    }

    /// <summary>
    /// Binds the board surface used for gap targeting and the insertion overlay.
    /// </summary>
    public static void BindBoard(
        Grid board,
        Canvas insertionLayer,
        Func<IReadOnlyList<Button>> cardButtons,
        Action? onDragSessionEnded = null,
        Action? onFlyoutOpenChanged = null)
    {
        ArgumentNullException.ThrowIfNull(board);
        ArgumentNullException.ThrowIfNull(insertionLayer);
        ArgumentNullException.ThrowIfNull(cardButtons);

        _board = board;
        _insertionLayer = insertionLayer;
        _cardButtons = cardButtons;
        _onDragSessionEnded = onDragSessionEnded;
        _onFlyoutOpenChanged = onFlyoutOpenChanged;
        EnsureGapVisuals();
        ClearGapIndicator();
    }

    /// <summary>
    /// Shows the gap overlay only while Reorder mode is active. Collapsing it outside
    /// Reorder mode prevents a leftover 3-epx accent bar from painting on the board.
    /// </summary>
    public static void SetGapOverlayVisible(bool visible)
    {
        if (_insertionLayer is null)
        {
            return;
        }

        if (!visible)
        {
            ClearGapIndicator();
        }

        _insertionLayer.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
    }

    /// <summary>
    /// Installs grab-handle reorder chrome and wires whole-card drag/drop + nudge.
    /// </summary>
    public static void Attach(
        Button card,
        Action<Guid, int> onMoveToIndex,
        Action<Guid, int> onNudge)
    {
        ArgumentNullException.ThrowIfNull(card);
        ArgumentNullException.ThrowIfNull(onMoveToIndex);
        ArgumentNullException.ThrowIfNull(onNudge);

        if (card.Tag is not ProfileCardModel model ||
            FindCardTopRow(card) is not Grid top)
        {
            return;
        }

        LaunchOrderGrabHandle grabHandle = CreateGrabHandle(model.Profile.Id);
        model.GrabHandle = grabHandle;
        top.Children.Insert(0, grabHandle);

        WireDrag(card, model, onMoveToIndex, onNudge);
        ApplyChrome(card, model);
    }

    /// <summary>
    /// Syncs grab-handle / whole-card drag affordances after Reorder mode flips in place.
    /// </summary>
    public static void SyncChrome(Button card)
    {
        if (card.Tag is not ProfileCardModel model)
        {
            return;
        }

        ApplyChrome(card, model);
    }

    private static void EnsureGapVisuals()
    {
        if (_insertionLayer is null || _gapSlot is not null)
        {
            return;
        }

        var accent = (Brush)Application.Current.Resources["AccentFillColorDefaultBrush"];
        var slotFill = (Brush)Application.Current.Resources["AccentFillColorSecondaryBrush"];

        _gapSlot = new Border
        {
            Width = GapWidenDip,
            Background = slotFill,
            CornerRadius = new CornerRadius(2),
            Visibility = Visibility.Collapsed,
            IsHitTestVisible = false
        };
        _gapBar = new Border
        {
            Width = GapBarThickness,
            Background = accent,
            CornerRadius = new CornerRadius(1.5),
            Visibility = Visibility.Collapsed,
            IsHitTestVisible = false
        };
        _insertionLayer.Children.Add(_gapSlot);
        _insertionLayer.Children.Add(_gapBar);
    }

    private static Grid? FindCardTopRow(Button card)
    {
        if (card.Content is Grid root &&
            root.Children.Count > 0 &&
            root.Children[0] is Grid top)
        {
            return top;
        }

        return null;
    }

    private static LaunchOrderGrabHandle CreateGrabHandle(Guid profileId)
    {
        var icon = new PathIcon
        {
            Data = CreateReOrderDotsGeometry(),
            Width = 16,
            Height = 16,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"]
        };

        var handle = new LaunchOrderGrabHandle
        {
            Width = 28,
            VerticalAlignment = VerticalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Center,
            VerticalContentAlignment = VerticalAlignment.Center,
            Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent),
            IsTabStop = true,
            Content = icon
        };

        string compactId = profileId.ToString("N");
        AutomationProperties.SetName(handle, AppChrome.Get("LaunchOrder_Name"));
        AutomationProperties.SetAutomationId(handle, $"LaunchOrderGrabHandle_{compactId}");
        ToolTipService.SetToolTip(
            handle,
            AppChrome.Get("LaunchOrder_Help"));
        return handle;
    }

    /// <summary>
    /// Fluent <c>reOrderDotsVertical</c> (20×20) as six <see cref="EllipseGeometry"/> dots for PathIcon.
    /// </summary>
    private static Geometry CreateReOrderDotsGeometry()
    {
        var group = new GeometryGroup { FillRule = FillRule.Nonzero };
        foreach (Point center in ReOrderDotsCenters)
        {
            group.Children.Add(new EllipseGeometry
            {
                Center = center,
                RadiusX = 1,
                RadiusY = 1
            });
        }

        return group;
    }

    /// <summary>
    /// ContentControl subclass so we can set <see cref="UIElement.ProtectedCursor"/> (SizeAll) in Reorder mode.
    /// </summary>
    private sealed class LaunchOrderGrabHandle : ContentControl
    {
        public void SetReorderCursor(bool enabled)
        {
            ProtectedCursor = enabled
                ? InputSystemCursor.Create(InputSystemCursorShape.SizeAll)
                : null;
        }
    }

    /// <summary>
    /// Profile card Button that can show SizeAll cursor in Reorder mode.
    /// </summary>
    internal sealed class ProfileCardButton : Button
    {
        public void SetReorderCursor(bool enabled)
        {
            ProtectedCursor = enabled
                ? InputSystemCursor.Create(InputSystemCursorShape.SizeAll)
                : null;
        }
    }

    private static void WireDrag(
        Button card,
        ProfileCardModel model,
        Action<Guid, int> onMoveToIndex,
        Action<Guid, int> onNudge)
    {
        if (model.GrabHandle is null)
        {
            return;
        }

        FrameworkElement handle = model.GrabHandle;

        // Pointer capture - not WinUI OLE StartDragAsync. IDragOperation release after
        // focus loss native-crashes WASDK 1.7 (0xc0000409 hang then fail-fast).
        card.AddHandler(
            UIElement.PointerPressedEvent,
            new PointerEventHandler((_, e) =>
            {
                if (!model.BoardEditMode || IsDragSessionActive)
                {
                    return;
                }

                PointerPoint point = e.GetCurrentPoint(card);
                if (!point.Properties.IsLeftButtonPressed)
                {
                    return;
                }

                try
                {
                    if (_board is not null)
                    {
                        _lastBoardPoint = e.GetCurrentPoint(_board).Position;
                    }
                }
                catch
                {
                    // Board transform may be unavailable this frame.
                }

                _ = card.CapturePointer(e.Pointer);
                BeginPointerDrag(card, model, onMoveToIndex);
                e.Handled = true;
            }),
            handledEventsToo: true);

        card.AddHandler(
            UIElement.PointerMovedEvent,
            new PointerEventHandler((_, e) =>
            {
                if (!ReferenceEquals(_dragSourceCard, card) || _board is null)
                {
                    return;
                }

                try
                {
                    _lastBoardPoint = e.GetCurrentPoint(_board).Position;
                    UpdateGapFromBoardPoint(_lastBoardPoint);
                }
                catch
                {
                    // Pointer may not map into board space this frame.
                }
            }),
            handledEventsToo: true);

        card.AddHandler(
            UIElement.PointerReleasedEvent,
            new PointerEventHandler((_, e) =>
            {
                if (!ReferenceEquals(_dragSourceCard, card))
                {
                    if (_dragSourceCard is not null)
                    {
                    }

                    return;
                }

                Point boardPoint = _lastBoardPoint;
                try
                {
                    if (_board is not null)
                    {
                        boardPoint = e.GetCurrentPoint(_board).Position;
                    }
                }
                catch
                {
                    boardPoint = _lastBoardPoint;
                }


                CompletePointerDrag(card, e.Pointer, boardPoint, onMoveToIndex);
                e.Handled = true;
            }),
            handledEventsToo: true);

        card.AddHandler(
            UIElement.PointerCaptureLostEvent,
            new PointerEventHandler((_, e) =>
            {
                if (!ReferenceEquals(_dragSourceCard, card))
                {
                    return;
                }

                bool leftPressed = false;
                Point boardPoint = _lastBoardPoint;
                try
                {
                    PointerPoint lostPoint = _board is not null
                        ? e.GetCurrentPoint(_board)
                        : e.GetCurrentPoint(card);
                    leftPressed = lostPoint.Properties.IsLeftButtonPressed;
                    if (_board is not null)
                    {
                        boardPoint = lostPoint.Position;
                        _lastBoardPoint = boardPoint;
                    }
                }
                catch
                {
                    // Position may be unavailable after capture loss.
                }


                if (leftPressed)
                {
                    // Capture stolen mid-drag (ScrollViewer / Button). Keep the session;
                    // root pointer handlers continue tracking until release.
                    return;
                }

                CompletePointerDrag(card, e.Pointer, boardPoint, onMoveToIndex);
            }),
            handledEventsToo: true);

        WireNudge(card, handle, model, onNudge);
    }

    private static void BeginPointerDrag(
        Button card,
        ProfileCardModel model,
        Action<Guid, int> onMoveToIndex)
    {
        ClearGapIndicator();
        model.IsLaunchOrderDragging = true;
        _draggingId = model.Profile.Id;
        _dragSourceCard = card;
        _dragSessionActive = true;
        _boardBusy = true;
        card.Opacity = 0.55;
        HookRootPointerTracking(onMoveToIndex);

    }

    private static void CompletePointerDrag(
        Button card,
        Pointer pointer,
        Point boardPoint,
        Action<Guid, int> onMoveToIndex)
    {
        if (!ReferenceEquals(_dragSourceCard, card))
        {
            return;
        }

        Guid? draggedId = _draggingId;
        int? moveTo = _board is null ? null : ResolveMoveToFromBoardPoint(boardPoint);


        TeardownPointerDrag(card, pointer);

        if (draggedId is Guid id && moveTo is int toIndex)
        {
            onMoveToIndex(id, toIndex);
            return;
        }

        NotifyApplyFinished();
    }

    private static void CancelPointerDrag(Button card)
    {

        TeardownPointerDrag(card, pointer: null);
        _boardBusy = false;
    }

    private static void TeardownPointerDrag(Button card, Pointer? pointer)
    {
        UnhookRootPointerTracking();

        if (card.Tag is ProfileCardModel model)
        {
            model.IsLaunchOrderDragging = false;
        }

        card.Opacity = 1;
        _dragSourceCard = null;
        _draggingId = null;
        _dragSessionActive = false;
        ClearGapIndicator();

        if (pointer is not null)
        {
            try
            {
                card.ReleasePointerCapture(pointer);
            }
            catch
            {
                // Capture may already be gone (PointerReleased then CaptureLost).
            }
        }
    }

    private static void HookRootPointerTracking(Action<Guid, int> onMoveToIndex)
    {
        UnhookRootPointerTracking();
        if (_board?.XamlRoot?.Content is not UIElement root)
        {
            return;
        }

        _pointerRoot = root;
        _rootMoved = (_, e) =>
        {
            if (_dragSourceCard is null || _board is null)
            {
                return;
            }

            try
            {
                _lastBoardPoint = e.GetCurrentPoint(_board).Position;
                UpdateGapFromBoardPoint(_lastBoardPoint);
            }
            catch
            {
                // Pointer may not map into board space this frame.
            }
        };
        _rootReleased = (_, e) =>
        {
            Button? source = _dragSourceCard;
            if (source is null)
            {
                return;
            }

            Point boardPoint = _lastBoardPoint;
            try
            {
                if (_board is not null)
                {
                    boardPoint = e.GetCurrentPoint(_board).Position;
                }
            }
            catch
            {
                boardPoint = _lastBoardPoint;
            }


            CompletePointerDrag(source, e.Pointer, boardPoint, onMoveToIndex);
        };
        root.AddHandler(UIElement.PointerMovedEvent, _rootMoved, handledEventsToo: true);
        root.AddHandler(UIElement.PointerReleasedEvent, _rootReleased, handledEventsToo: true);
    }

    private static void UnhookRootPointerTracking()
    {
        if (_pointerRoot is null)
        {
            return;
        }

        if (_rootMoved is not null)
        {
            _pointerRoot.RemoveHandler(UIElement.PointerMovedEvent, _rootMoved);
        }

        if (_rootReleased is not null)
        {
            _pointerRoot.RemoveHandler(UIElement.PointerReleasedEvent, _rootReleased);
        }

        _pointerRoot = null;
        _rootMoved = null;
        _rootReleased = null;
    }


    /// <summary>
    /// Clears in-flight drag overlay / source dim state (e.g. before board refresh).
    /// </summary>
    public static void ResetDragSession()
    {
        if (_dragSourceCard is not null &&
            _dragSourceCard.Tag is ProfileCardModel model)
        {
            model.IsLaunchOrderDragging = false;
        }

        UnhookRootPointerTracking();
        _dragSourceCard = null;
        _draggingId = null;
        _dragSessionActive = false;
        _boardBusy = false;
        ClearGapIndicator();
    }


    private static void UpdateGapFromBoardPoint(Point boardPoint)
    {
        if (!TryCollectVisibleBounds(out List<LaunchOrderRect> bounds, out bool rtl))
        {
            ClearGapIndicator();
            return;
        }

        int insertion = LaunchOrderGapTargeting.ResolveInsertionIndex(
            bounds,
            new LaunchOrderPoint(boardPoint.X, boardPoint.Y),
            rtl);

        ShowGapIndicator(bounds, insertion, rtl);
    }

    private static int? ResolveMoveToFromBoardPoint(Point boardPoint)
    {
        if (!TryCollectVisibleBounds(out List<LaunchOrderRect> bounds, out bool rtl))
        {
            return null;
        }

        int insertion = LaunchOrderGapTargeting.ResolveInsertionIndex(
            bounds,
            new LaunchOrderPoint(boardPoint.X, boardPoint.Y),
            rtl);

        // Visible-only list: insertion index is ApplyMove toIndex.
        return insertion;
    }

    private static bool TryCollectVisibleBounds(out List<LaunchOrderRect> bounds, out bool rtl)
    {
        bounds = new List<LaunchOrderRect>();
        rtl = false;
        if (_board is null || _cardButtons is null)
        {
            return false;
        }

        foreach (Button button in _cardButtons())
        {
            if (button.Tag is not ProfileCardModel model ||
                button.Visibility != Visibility.Visible ||
                button.ActualWidth <= 0)
            {
                continue;
            }

            // Exclude the drag source so insertion indices match post-removal ApplyMove slots
            // without collapsing/reflowing the Grid mid-drag.
            if (_draggingId is Guid dragging && model.Profile.Id == dragging)
            {
                continue;
            }

            rtl = rtl || button.FlowDirection == FlowDirection.RightToLeft;
            try
            {
                GeneralTransform transform = button.TransformToVisual(_board);
                Point topLeft = transform.TransformPoint(new Point(0, 0));
                bounds.Add(new LaunchOrderRect(topLeft.X, topLeft.Y, button.ActualWidth, button.ActualHeight));
            }
            catch
            {
                // Skip cards that cannot map into board space this frame.
            }
        }

        return bounds.Count > 0;
    }

    private static void ShowGapIndicator(IReadOnlyList<LaunchOrderRect> bounds, int insertion, bool rtl)
    {
        EnsureGapVisuals();
        if (_gapSlot is null || _gapBar is null || _insertionLayer is null || bounds.Count == 0)
        {
            return;
        }

        (double x, double y, double height) = LaunchOrderGapTargeting.ResolveGapVisual(bounds, insertion, rtl);
        if (double.IsNaN(x) || double.IsNaN(y) || double.IsNaN(height) ||
            double.IsInfinity(x) || double.IsInfinity(y) || double.IsInfinity(height) ||
            height <= 0)
        {
            return;
        }

        _gapSlot.Height = height;
        _gapBar.Height = Math.Max(24, height - 8);
        Canvas.SetLeft(_gapSlot, x - (GapWidenDip / 2));
        Canvas.SetTop(_gapSlot, y);
        Canvas.SetLeft(_gapBar, x - (GapBarThickness / 2));
        Canvas.SetTop(_gapBar, y + ((height - _gapBar.Height) / 2));
        _gapSlot.Visibility = Visibility.Visible;
        _gapBar.Visibility = Visibility.Visible;
    }

    private static void ClearGapIndicator()
    {
        if (_gapSlot is not null)
        {
            _gapSlot.Visibility = Visibility.Collapsed;
        }

        if (_gapBar is not null)
        {
            _gapBar.Visibility = Visibility.Collapsed;
        }
    }

    private static void WireNudge(
        Button card,
        FrameworkElement handle,
        ProfileCardModel model,
        Action<Guid, int> onNudge)
    {
        string compactId = model.Profile.Id.ToString("N");
        var moveUp = new MenuFlyoutItem { Text = AppChrome.Get("LaunchOrder_MoveUp") };
        var moveDown = new MenuFlyoutItem { Text = AppChrome.Get("LaunchOrder_MoveDown") };
        AutomationProperties.SetAutomationId(moveUp, $"LaunchOrderMoveUp_{compactId}");
        AutomationProperties.SetAutomationId(moveDown, $"LaunchOrderMoveDown_{compactId}");
        moveUp.Click += (_, _) => onNudge(model.Profile.Id, -1);
        moveDown.Click += (_, _) => onNudge(model.Profile.Id, 1);
        var gripFlyout = new MenuFlyout { Items = { moveUp, moveDown } };
        gripFlyout.Opened += (_, _) =>
        {
            model.IsOverflowFlyoutOpen = true;
            _onFlyoutOpenChanged?.Invoke();
        };
        gripFlyout.Closed += (_, _) =>
        {
            model.IsOverflowFlyoutOpen = false;
            _onFlyoutOpenChanged?.Invoke();
        };
        handle.ContextFlyout = gripFlyout;

        void Nudge(int delta, KeyboardAcceleratorInvokedEventArgs e)
        {
            if (!model.BoardEditMode)
            {
                return;
            }

            onNudge(model.Profile.Id, delta);
            e.Handled = true;
        }

        var altUp = new KeyboardAccelerator
        {
            Key = VirtualKey.Up,
            Modifiers = VirtualKeyModifiers.Menu
        };
        altUp.Invoked += (_, e) => Nudge(-1, e);

        var altDown = new KeyboardAccelerator
        {
            Key = VirtualKey.Down,
            Modifiers = VirtualKeyModifiers.Menu
        };
        altDown.Invoked += (_, e) => Nudge(1, e);

        var handleUp = new KeyboardAccelerator { Key = VirtualKey.Up };
        handleUp.Invoked += (_, e) => Nudge(-1, e);
        var handleDown = new KeyboardAccelerator { Key = VirtualKey.Down };
        handleDown.Invoked += (_, e) => Nudge(1, e);

        card.KeyboardAccelerators.Add(altUp);
        card.KeyboardAccelerators.Add(altDown);
        handle.KeyboardAccelerators.Add(handleUp);
        handle.KeyboardAccelerators.Add(handleDown);
    }

    private static void ApplyChrome(Button card, ProfileCardModel model)
    {
        if (model.GrabHandle is null)
        {
            return;
        }

        bool edit = model.BoardEditMode;
        FrameworkElement handle = model.GrabHandle;
        handle.Visibility = edit ? Visibility.Visible : Visibility.Collapsed;
        handle.IsTabStop = edit;
        // Pointer-capture reorder - never enable OLE CanDrag (WASDK 1.7 IDragOperation crash).
        card.CanDrag = false;
        handle.CanDrag = false;
        card.AllowDrop = false;
        // Claim translation so BoardScroller does not steal capture mid-drag.
        card.ManipulationMode = edit
            ? ManipulationModes.TranslateX | ManipulationModes.TranslateY
            : ManipulationModes.System;
        if (_board is not null)
        {
            _board.AllowDrop = false;
        }

        bool rtl = card.FlowDirection == FlowDirection.RightToLeft
            || handle.FlowDirection == FlowDirection.RightToLeft;
        handle.Margin = rtl ? new Thickness(8, 0, 0, 0) : new Thickness(0, 0, 8, 0);

        if (handle is LaunchOrderGrabHandle grab)
        {
            grab.SetReorderCursor(edit);
        }

        if (card is ProfileCardButton profileCard)
        {
            profileCard.SetReorderCursor(edit);
        }


        if (!edit)
        {
            ClearGapIndicator();
            card.Opacity = 1;
        }

        if (handle.ContextFlyout is MenuFlyout flyout)
        {
            flyout.AreOpenCloseAnimationsEnabled = true;
            foreach (MenuFlyoutItemBase item in flyout.Items)
            {
                item.IsEnabled = edit;
            }
        }
    }
}
