using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;

namespace PowerBILauncher;

/// <summary>
/// Shell callbacks for Profile board operations that stay on the window edge.
/// </summary>
public sealed class ProfileBoardShell
{
    /// <summary>
    /// Persists Launch Order; false when the write failed (the shell has already told the user).
    /// </summary>
    public required Func<bool> SaveProfiles { get; init; }

    public required Action<Profile> OpenEditProfile { get; init; }

    public required Func<Profile, ProfileLaunchResult> LaunchProfile { get; init; }

    /// <summary>
    /// Confirm + delete lifecycle shared with Edit Profile Danger Zone.
    /// </summary>
    public required Func<Profile, Task> DeleteProfile { get; init; }

    public required Action RequestEmptyBoardFocus { get; init; }

    /// <summary>
    /// Create an unsaved Edit Profile draft - shared by header Add and the empty-board CTA.
    /// </summary>
    public required Action AddProfile { get; init; }

    /// <summary>
    /// True while a ContentDialog is showing (card and app shortcuts stay inert).
    /// </summary>
    public required Func<bool> IsDialogOpen { get; init; }

    /// <summary>
    /// Refresh header Add / Reorder·Done chrome after Reorder mode changes from the board.
    /// </summary>
    public required Action SyncBoardCommanding { get; init; }

    /// <summary>
    /// Recalculate keyboard-accelerator enablement after a flyout opens or closes.
    /// </summary>
    public required Action SyncShortcutAvailability { get; init; }
}

/// <summary>
/// Profile board coordinator: refresh cards, sync Reorder mode on cards,
/// activate via <see cref="ProfileCardInteraction"/> (Launch vs inert), overflow More,
/// Launch Order reorder chrome (via <see cref="ProfileBoardLaunchOrder"/>), and drop/nudge → apply → refresh.
/// Shell keeps Reorder/Done chrome, Add Profile (unsaved draft), and save-Profiles-when-asked.
/// </summary>
public sealed class ProfileBoardHost
{
    private readonly ProfileLifecycle _lifecycle;
    private readonly ProfileBoardShell _shell;
    private readonly List<Button> _profileCardButtons = new();
    private readonly HashSet<Guid> _launchingProfileIds = new();
    private readonly Dictionary<Guid, DispatcherQueueTimer> _launchDwellTimers = new();
    private ProfileBoardPage? _page;
    private bool _boardEditMode;
    private bool _refreshingProfileCards;
    private bool _sizeLayoutQueued;
    private double _pendingSizeLayoutWidth = double.NaN;
    private static readonly TimeSpan LaunchingDwell = TimeSpan.FromSeconds(3);

    public ProfileBoardHost(ProfileLifecycle lifecycle, ProfileBoardShell shell)
    {
        _lifecycle = lifecycle ?? throw new ArgumentNullException(nameof(lifecycle));
        _shell = shell ?? throw new ArgumentNullException(nameof(shell));
    }

    public bool HasPage => _page is not null;

    public bool BoardEditMode => _boardEditMode;

    /// <summary>
    /// Whether header Reorder / overflow Reorder should be offered (Profile count ≥ 2).
    /// </summary>
    public bool IsReorderOffered =>
        ProfileCardInteraction.IsReorderOffered(_lifecycle.Profiles.Count);

    /// <summary>
    /// True while a card overflow, context, or grip flyout is open.
    /// </summary>
    public bool IsAnyFlyoutOpen
    {
        get
        {
            foreach (Button card in _profileCardButtons)
            {
                if (card.Tag is ProfileCardModel model && model.IsOverflowFlyoutOpen)
                {
                    return true;
                }
            }

            return false;
        }
    }

    public void SetCardShortcutsEnabled(bool enabled)
    {
        foreach (Button card in _profileCardButtons)
        {
            foreach (KeyboardAccelerator accelerator in card.KeyboardAccelerators)
            {
                if (accelerator.Key is Windows.System.VirtualKey.F2 or Windows.System.VirtualKey.Delete)
                {
                    accelerator.IsEnabled = enabled;
                }
            }
        }
    }

    public void AttachPage(ProfileBoardPage page)
    {
        ArgumentNullException.ThrowIfNull(page);

        if (_page is not null)
        {
            _page.BoardScroller.SizeChanged -= BoardScroller_SizeChanged;
            _page.BoardScroller.PointerPressed -= BoardScroller_PointerPressed;
            _page.EmptyAddButton.Click -= EmptyBoardAddButton_Click;
        }

        _page = page;
        page.BoardScroller.SizeChanged += BoardScroller_SizeChanged;
        page.BoardScroller.PointerPressed += BoardScroller_PointerPressed;
        page.EmptyAddButton.Click += EmptyBoardAddButton_Click;
        ProfileBoardLaunchOrder.BindBoard(
            page.Board,
            page.InsertionLayer,
            () => _profileCardButtons,
            onDragSessionEnded: FlushPendingSizeLayout,
            onFlyoutOpenChanged: _shell.SyncShortcutAvailability);
        RefreshCards();
    }

    public void RefreshCards()
    {
        if (_refreshingProfileCards || _page is null)
        {
            return;
        }

        _refreshingProfileCards = true;
        try
        {
            Grid board = _page.Board;
            ScrollViewer scroller = _page.BoardScroller;


            ProfileBoardLaunchOrder.ResetDragSession();
            board.Children.Clear();
            _profileCardButtons.Clear();

            bool empty = ProfileCardInteraction.ShowEmptyState(_lifecycle.Profiles.Count);
            _page.EmptyBoard.Visibility = empty ? Visibility.Visible : Visibility.Collapsed;
            ProfileBoardLaunchOrder.SetGapOverlayVisible(_boardEditMode && !empty);

            IReadOnlyList<PbiVersion> versions = _lifecycle.Settings.Versions;
            Guid? defaultVersionId = _lifecycle.Settings.DefaultVersionId;
            ProfileCardOverflowCommands overflow = CreateOverflowCommands();
            foreach (Profile profile in _lifecycle.Profiles.OrderBy(p => p.Order))
            {
                Button card = ProfileCardFactory.Create(
                    profile,
                    _boardEditMode,
                    _launchingProfileIds.Contains(profile.Id),
                    versions,
                    defaultVersionId,
                    ProfileCard_Click,
                    overflow);
                ProfileBoardLaunchOrder.Attach(
                    card,
                    ApplyLaunchOrderMoveToIndex,
                    ApplyLaunchOrderNudge);
                _profileCardButtons.Add(card);
                board.Children.Add(card);
            }

            ProfileBoardLayout.Apply(
                board,
                _profileCardButtons,
                WindowLayoutConstraints.BoardContentWidth(scroller.ActualWidth));
        }
        finally
        {
            _refreshingProfileCards = false;
        }

        _shell.SyncShortcutAvailability();
    }

    /// <summary>
    /// Updates Reorder-mode affordances on existing cards without rebuilding them.
    /// </summary>
    public void SetBoardEditMode(bool enabled, bool refreshCards)
    {
        if (_boardEditMode == enabled)
        {
            if (refreshCards)
            {
                RefreshCards();
            }

            return;
        }

        _boardEditMode = enabled;
        if (refreshCards)
        {
            SyncCardActions();
        }

        bool empty = ProfileCardInteraction.ShowEmptyState(_lifecycle.Profiles.Count);
        ProfileBoardLaunchOrder.SetGapOverlayVisible(_boardEditMode && !empty);
    }

    private ProfileCardOverflowCommands CreateOverflowCommands() =>
        new()
        {
            OpenEditProfile = _shell.OpenEditProfile,
            EnterReorderMode = EnterReorderFromOverflow,
            IsReorderOffered = () =>
                ProfileCardInteraction.IsReorderOffered(_lifecycle.Profiles.Count),
            IsDialogOrFlyoutOpen = () => _shell.IsDialogOpen() || IsAnyFlyoutOpen,
            FlyoutOpenChanged = _shell.SyncShortcutAvailability,
            DeleteProfile = _shell.DeleteProfile
        };

    private void EnterReorderFromOverflow()
    {
        SetBoardEditMode(true, refreshCards: true);
        _shell.SyncBoardCommanding();
    }

    private void SyncCardActions()
    {
        ProfileCardOverflowCommands overflow = CreateOverflowCommands();
        foreach (Button card in _profileCardButtons)
        {
            ProfileCardFactory.SyncBoardEditMode(card, _boardEditMode, overflow);
            ProfileBoardLaunchOrder.SyncChrome(card);
        }

    }

    private void BoardScroller_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (_page is null || Math.Abs(e.PreviousSize.Width - e.NewSize.Width) < 0.5)
        {
            return;
        }

        // Coalesce rapid resize events onto one Low-priority layout pass.
        _pendingSizeLayoutWidth = e.NewSize.Width;
        if (_sizeLayoutQueued)
        {
            return;
        }

        _sizeLayoutQueued = true;
        Microsoft.UI.Dispatching.DispatcherQueue queue = _page.DispatcherQueue;
        _ = queue.TryEnqueue(
            Microsoft.UI.Dispatching.DispatcherQueuePriority.Low,
            FlushPendingSizeLayout);
    }

    private void FlushPendingSizeLayout()
    {
        _sizeLayoutQueued = false;
        if (_page is null || double.IsNaN(_pendingSizeLayoutWidth))
        {
            return;
        }

        double width = _pendingSizeLayoutWidth;
        _pendingSizeLayoutWidth = double.NaN;


        if (ProfileBoardLaunchOrder.IsDragSessionActive)
        {
            // Width-only: avoid Grid definition Clear while DnD owns a card.
            _page.Board.Width = WindowLayoutConstraints.BoardContentWidth(width);
            _pendingSizeLayoutWidth = width;
            return;
        }

        ProfileBoardLayout.Apply(
            _page.Board,
            _profileCardButtons,
            WindowLayoutConstraints.BoardContentWidth(width));
    }

    private void BoardScroller_PointerPressed(object sender, PointerRoutedEventArgs e)
    {
        if (e.OriginalSource is not DependencyObject source || IsUnderProfileCard(source))
        {
            return;
        }

        // Prefer stable chrome focus over the first card absorbing empty-area clicks.
        _shell.RequestEmptyBoardFocus();
    }

    private bool IsUnderProfileCard(DependencyObject? node)
    {
        while (node is not null)
        {
            if (node is Button button && _profileCardButtons.Contains(button))
            {
                return true;
            }

            node = VisualTreeHelper.GetParent(node);
        }

        return false;
    }

    private void ProfileCard_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: ProfileCardModel model })
        {
            return;
        }

        // Grab-handle presses must not activate the card (drag vs launch stay separate).
        if (model.BoardEditMode &&
            model.GrabHandle is not null &&
            e.OriginalSource is DependencyObject source &&
            IsDescendantOf(source, model.GrabHandle))
        {
            return;
        }

        // More opener must not Launch (nested control click backup).
        if (model.MoreButton is not null &&
            e.OriginalSource is DependencyObject moreSource &&
            IsDescendantOf(moreSource, model.MoreButton))
        {
            return;
        }

        ActivateProfileCard(model);
    }

    private static bool IsDescendantOf(DependencyObject? node, DependencyObject ancestor)
    {
        while (node is not null)
        {
            if (ReferenceEquals(node, ancestor))
            {
                return true;
            }

            node = VisualTreeHelper.GetParent(node);
        }

        return false;
    }

    private void ActivateProfileCard(ProfileCardModel model)
    {
        // Use the mode captured on this card, not the live toolbar flag, so a stale
        // card cannot launch after Reorder mode was already left (or the reverse).
        if (!ProfileCardInteraction.IsShortcutAllowed(
                ProfileCardShortcutKind.Launch,
                model.BoardEditMode,
                model.IsLaunching,
                _shell.IsDialogOpen() || IsAnyFlyoutOpen))
        {
            return;
        }

        SetCardLaunching(model.Profile.Id, launching: true);
        ProfileLaunchResult result = _shell.LaunchProfile(model.Profile);
        if (!result.Succeeded)
        {
            SetCardLaunching(model.Profile.Id, launching: false);
            return;
        }

        StartLaunchDwell(model.Profile.Id);
    }

    private void ApplyLaunchOrderMoveToIndex(Guid draggedId, int toIndex)
    {
        Dictionary<Guid, int> previousOrder = CaptureLaunchOrder();
        if (!LaunchOrder.TryApplyMoveToIndex(_lifecycle.Profiles, draggedId, toIndex))
        {
            ProfileBoardLaunchOrder.NotifyApplyFinished();
            return;
        }

        SaveLaunchOrderOrRevert(previousOrder);
        RelayoutCardsInPlace();
        ProfileBoardLaunchOrder.NotifyApplyFinished();
    }

    private Dictionary<Guid, int> CaptureLaunchOrder() =>
        _lifecycle.Profiles.ToDictionary(profile => profile.Id, profile => profile.Order);

    private void SaveLaunchOrderOrRevert(Dictionary<Guid, int> previousOrder)
    {
        if (_shell.SaveProfiles())
        {
            return;
        }

        foreach (Profile profile in _lifecycle.Profiles)
        {
            if (previousOrder.TryGetValue(profile.Id, out int order))
            {
                profile.Order = order;
            }
        }
    }

    /// <summary>
    /// Reorders existing card buttons to match Launch Order without rebuilding the visual tree.
    /// </summary>
    private void RelayoutCardsInPlace()
    {
        if (_page is null || _profileCardButtons.Count == 0)
        {
            return;
        }


        var byId = new Dictionary<Guid, Button>(_profileCardButtons.Count);
        foreach (Button card in _profileCardButtons)
        {
            if (card.Tag is ProfileCardModel model)
            {
                byId[model.Profile.Id] = card;
            }
        }

        _profileCardButtons.Clear();
        foreach (Profile profile in _lifecycle.Profiles.OrderBy(p => p.Order))
        {
            if (byId.TryGetValue(profile.Id, out Button? card))
            {
                _profileCardButtons.Add(card);
            }
        }

        ProfileBoardLayout.Apply(
            _page.Board,
            _profileCardButtons,
            WindowLayoutConstraints.BoardContentWidth(_page.BoardScroller.ActualWidth));

    }

    private void ApplyLaunchOrderNudge(Guid profileId, int delta)
    {
        Dictionary<Guid, int> previousOrder = CaptureLaunchOrder();
        if (!LaunchOrder.TryApplyNudgeById(_lifecycle.Profiles, profileId, delta))
        {
            return;
        }

        SaveLaunchOrderOrRevert(previousOrder);
        RelayoutCardsInPlace();
    }

    private void EmptyBoardAddButton_Click(object sender, RoutedEventArgs e) =>
        _shell.AddProfile();

    private void SetCardLaunching(Guid profileId, bool launching)
    {
        if (launching)
        {
            _launchingProfileIds.Add(profileId);
        }
        else
        {
            _launchingProfileIds.Remove(profileId);
            StopLaunchDwell(profileId);
        }

        Button? card = FindCard(profileId);
        if (card is not null)
        {
            ProfileCardFactory.SyncLaunching(card, launching);
        }
    }

    private void StartLaunchDwell(Guid profileId)
    {
        if (_page is null)
        {
            return;
        }

        StopLaunchDwell(profileId);
        DispatcherQueueTimer timer = _page.DispatcherQueue.CreateTimer();
        timer.Interval = LaunchingDwell;
        timer.IsRepeating = false;
        timer.Tick += (_, _) => SetCardLaunching(profileId, launching: false);
        _launchDwellTimers[profileId] = timer;
        timer.Start();
    }

    private void StopLaunchDwell(Guid profileId)
    {
        if (!_launchDwellTimers.Remove(profileId, out DispatcherQueueTimer? timer))
        {
            return;
        }

        timer.Stop();
    }

    private Button? FindCard(Guid profileId)
    {
        foreach (Button card in _profileCardButtons)
        {
            if (card.Tag is ProfileCardModel model && model.Profile.Id == profileId)
            {
                return card;
            }
        }

        return null;
    }
}
