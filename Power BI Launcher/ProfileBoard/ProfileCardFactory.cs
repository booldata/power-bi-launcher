using System;
using System.Collections.Generic;
using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Automation.Peers;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Microsoft.UI.Xaml.Shapes;
using Windows.System;
using Windows.UI;
using PointerDeviceType = Microsoft.UI.Input.PointerDeviceType;

namespace PowerBILauncher;

/// <summary>
/// Shell/host callbacks for per-card overflow commands.
/// </summary>
public sealed class ProfileCardOverflowCommands
{
    public required Action<Profile> OpenEditProfile { get; init; }

    public required Action EnterReorderMode { get; init; }

    public required Func<bool> IsReorderOffered { get; init; }

    public required Func<bool> IsDialogOrFlyoutOpen { get; init; }

    public required Action FlyoutOpenChanged { get; init; }

    public required Func<Profile, Task> DeleteProfile { get; init; }
}

/// <summary>
/// Builds Profile board cards. Avatar presentation is owned by <see cref="AvatarPaint"/>.
/// Launch Order grab/drag/nudge chrome is attached by <see cref="ProfileBoardLaunchOrder"/>.
/// </summary>
public static class ProfileCardFactory
{
    // More / Edit / Reorder / Delete / Play / warning / identity glyphs (Segoe Fluent Icons).
    private const string MoreGlyph = "\uE712";
    private const string EditGlyph = "\uE70F";
    private const string ReorderGlyph = "\uE8CB";
    private const string DeleteGlyph = "\uE74D";
    private const string PlayGlyph = "\uE768";
    private const string WarningGlyph = "\uE814";
    private const string IdentityGlyph = "\uE77B";
    private const string ReorderMenuTag = "ProfileCardReorder";
    private const string DesktopLogoUri = "ms-appx:///Assets/pbidesktoplogo.png";
    private const string ReportServerLogoUri = "ms-appx:///Assets/pbidesktoprslogo.png";

    public static Button Create(
        Profile profile,
        bool boardEditMode,
        bool isLaunching,
        IReadOnlyList<PbiVersion> versions,
        Guid? defaultVersionId,
        RoutedEventHandler clickHandler,
        ProfileCardOverflowCommands overflow)
    {
        ArgumentNullException.ThrowIfNull(overflow);
        ArgumentNullException.ThrowIfNull(versions);

        var avatarEllipse = new Ellipse
        {
            Width = 48,
            Height = 48,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
        var avatarLetter = new TextBlock
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Style = (Style)Application.Current.Resources["SubtitleTextBlockStyle"]
        };
        AutomationProperties.SetAutomationId(avatarLetter, $"ProfileAvatarLetter_{profile.Id:N}");

        AvatarPaint.Apply(profile.IconPath, profile.Name, profile.AvatarColor, avatarEllipse, avatarLetter);

        SolidColorBrush contrastBrush = new(ColorContrast.ContrastForeground(profile.CardColor));
        var nameBlock = new TextBlock
        {
            Text = profile.Name,
            Style = (Style)Application.Current.Resources["BodyStrongTextBlockStyle"],
            TextWrapping = TextWrapping.NoWrap,
            TextTrimming = TextTrimming.CharacterEllipsis,
            MaxLines = 1,
            Foreground = contrastBrush
        };

        ProfileCardCaption caption = ProfileCardInteraction.Caption(
            profile.VersionId,
            profile.WindowsNetworkIdentity,
            versions,
            defaultVersionId);
        ProfileCardProductIconKind iconKind = ProfileCardInteraction.ProductIconKind(
            profile.VersionId,
            versions,
            defaultVersionId);

        var cardModel = new ProfileCardModel
        {
            Profile = profile,
            BoardEditMode = boardEditMode,
            IsLaunching = isLaunching
        };

        FrameworkElement captionRow = CreateCaptionRow(cardModel, caption, iconKind, contrastBrush);

        // Column 0 reserved for Launch Order grab handle (board attaches reorder chrome).
        var top = new Grid();
        top.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        top.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        top.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var avatarHost = new Grid
        {
            Width = 48,
            Height = 48,
            Margin = new Thickness(0, 0, 12, 0),
            VerticalAlignment = VerticalAlignment.Center
        };
        avatarHost.Children.Add(avatarEllipse);
        avatarHost.Children.Add(avatarLetter);
        Grid.SetColumn(avatarHost, 1);
        top.Children.Add(avatarHost);

        // Leave room for the More control on the top-right.
        var nameHost = new StackPanel
        {
            Spacing = 4,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 40, 0)
        };
        nameHost.Children.Add(nameBlock);
        nameHost.Children.Add(captionRow);
        Grid.SetColumn(nameHost, 2);
        top.Children.Add(nameHost);

        Button moreButton = CreateMoreButton(profile.Id, contrastBrush);
        cardModel.MoreButton = moreButton;
        moreButton.Flyout = CreateOverflowMenu(cardModel, overflow, idSuffix: "More");

        FontIcon playIcon = CreatePlayIcon(contrastBrush);
        cardModel.PlayIcon = playIcon;

        var root = new Grid
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            // Hit-test empty space so PointerEntered fires for adaptive More (empty grids do not).
            Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent)
        };
        top.VerticalAlignment = VerticalAlignment.Center;
        top.HorizontalAlignment = HorizontalAlignment.Stretch;
        root.Children.Add(top);
        root.Children.Add(moreButton);
        root.Children.Add(playIcon);

        Color cardColor = ColorContrast.ResolveCardColor(profile.CardColor);
        var cardFill = new SolidColorBrush(cardColor);
        var cardFillPointerOver = new SolidColorBrush(ColorContrast.ShiftForInteraction(cardColor, 28));
        var cardFillPressed = new SolidColorBrush(ColorContrast.ShiftForInteraction(cardColor, 40));
        var cardStroke = (Brush)Application.Current.Resources["CardStrokeColorDefaultBrush"];
        cardModel.IdleBorderBrush = cardStroke;
        var card = new ProfileBoardLaunchOrder.ProfileCardButton
        {
            Tag = cardModel,
            Padding = new Thickness(16),
            BorderThickness = new Thickness(1),
            BorderBrush = cardStroke,
            Background = cardFill,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            VerticalContentAlignment = VerticalAlignment.Stretch,
            Content = root
        };
        card.CornerRadius = (CornerRadius)Application.Current.Resources["ControlCornerRadius"];

        card.Resources["ButtonBackground"] = cardFill;
        card.Resources["ButtonBackgroundPointerOver"] = cardFillPointerOver;
        card.Resources["ButtonBackgroundPressed"] = cardFillPressed;
        card.Resources["ButtonBackgroundDisabled"] = cardFill;
        card.Resources["ButtonBorderBrushPointerOver"] = cardStroke;
        card.Resources["ButtonBorderBrushPressed"] = cardStroke;

        ApplyOverflowChrome(card, cardModel, overflow);
        card.Click += clickHandler;
        WireAdaptiveMore(card, cardModel, moreButton);
        WireCardShortcuts(card, cardModel, overflow);
        ApplyCardChrome(card, cardModel);

        return card;
    }

    /// <summary>
    /// Updates Reorder-mode card policy, automation name, and overflow More / ContextFlyout chrome.
    /// </summary>
    public static void SyncBoardEditMode(
        Button card,
        bool boardEditMode,
        ProfileCardOverflowCommands overflow)
    {
        ArgumentNullException.ThrowIfNull(overflow);

        if (card.Tag is not ProfileCardModel model || model.MoreButton is null)
        {
            return;
        }

        model.BoardEditMode = boardEditMode;
        ApplyOverflowChrome(card, model, overflow);
        ApplyCardChrome(card, model);
    }

    /// <summary>
    /// Updates the transient Launching chrome without rebuilding the card.
    /// </summary>
    public static void SyncLaunching(Button card, bool isLaunching)
    {
        if (card.Tag is not ProfileCardModel model)
        {
            return;
        }

        model.IsLaunching = isLaunching;
        ApplyCardChrome(card, model);
    }

    private static FrameworkElement CreateCaptionRow(
        ProfileCardModel model,
        ProfileCardCaption caption,
        ProfileCardProductIconKind iconKind,
        Brush contrastBrush)
    {
        UIElement productIcon = CreateProductIcon(iconKind);
        AutomationProperties.SetAccessibilityView(
            productIcon,
            iconKind == ProfileCardProductIconKind.Warning
                ? AccessibilityView.Content
                : AccessibilityView.Raw);
        if (iconKind == ProfileCardProductIconKind.Warning)
        {
            AutomationProperties.SetName(productIcon, AppChrome.CardCaption(caption));
        }

        var captionBlock = new TextBlock
        {
            Text = AppChrome.CardCaption(caption),
            Style = (Style)Application.Current.Resources["CaptionTextBlockStyle"],
            TextWrapping = TextWrapping.NoWrap,
            TextTrimming = TextTrimming.CharacterEllipsis,
            MaxLines = 1,
            Foreground = contrastBrush,
            Opacity = 0.72
        };
        model.CaptionBlock = captionBlock;

        var launchingLabel = new TextBlock
        {
            Text = AppChrome.Get("ProfileCard_Launching"),
            Style = (Style)Application.Current.Resources["CaptionTextBlockStyle"],
            Foreground = contrastBrush,
            VerticalAlignment = VerticalAlignment.Center
        };
        model.LaunchingLabel = launchingLabel;

        var progress = new ProgressRing
        {
            Width = 16,
            Height = 16,
            MinWidth = 16,
            MinHeight = 16,
            IsActive = false,
            VerticalAlignment = VerticalAlignment.Center
        };
        model.LaunchProgress = progress;

        var identity = new FontIcon
        {
            Glyph = IdentityGlyph,
            FontSize = 12,
            Foreground = contrastBrush,
            Margin = new Thickness(6, 0, 0, 0),
            VerticalAlignment = VerticalAlignment.Center,
            Visibility = caption.HasWindowsNetworkIdentity ? Visibility.Visible : Visibility.Collapsed
        };
        AutomationProperties.SetName(identity, AppChrome.Get("ProfileCard_WindowsNetworkIdentity"));
        ToolTipService.SetToolTip(identity, AppChrome.Get("ProfileCard_WindowsNetworkIdentity"));
        model.IdentityIcon = identity;

        var row = new Grid { VerticalAlignment = VerticalAlignment.Center };
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var iconHost = new Grid
        {
            Width = 16,
            Height = 16,
            Margin = new Thickness(0, 0, 6, 0),
            VerticalAlignment = VerticalAlignment.Center
        };
        iconHost.Children.Add(productIcon);
        iconHost.Children.Add(progress);
        Grid.SetColumn(iconHost, 0);
        row.Children.Add(iconHost);

        var textHost = new Grid { VerticalAlignment = VerticalAlignment.Center };
        textHost.Children.Add(captionBlock);
        textHost.Children.Add(launchingLabel);
        Grid.SetColumn(textHost, 1);
        row.Children.Add(textHost);

        Grid.SetColumn(identity, 2);
        row.Children.Add(identity);

        model.ProductIconHost = iconHost;
        return row;
    }

    private static UIElement CreateProductIcon(ProfileCardProductIconKind kind)
    {
        if (kind == ProfileCardProductIconKind.Warning)
        {
            return new FontIcon
            {
                Glyph = WarningGlyph,
                FontSize = 16,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Foreground = (Brush)Application.Current.Resources["SystemFillColorCautionBrush"]
            };
        }

        string uri = kind == ProfileCardProductIconKind.ReportServer
            ? ReportServerLogoUri
            : DesktopLogoUri;
        return new Image
        {
            Width = 16,
            Height = 16,
            Stretch = Stretch.Uniform,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Source = new BitmapImage(new Uri(uri))
        };
    }

    private static FontIcon CreatePlayIcon(Brush contrastBrush)
    {
        var play = new FontIcon
        {
            Glyph = PlayGlyph,
            FontSize = 16,
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Bottom,
            Foreground = contrastBrush,
            IsHitTestVisible = false,
            Opacity = 0
        };
        AutomationProperties.SetAccessibilityView(play, AccessibilityView.Raw);
        return play;
    }

    private static void ApplyOverflowChrome(
        Button card,
        ProfileCardModel model,
        ProfileCardOverflowCommands overflow)
    {
        if (model.MoreButton is null)
        {
            return;
        }

        if (model.BoardEditMode)
        {
            card.ContextFlyout = null;
            return;
        }

        bool offered = overflow.IsReorderOffered();
        if (model.MoreButton.Flyout is MenuFlyout moreFlyout)
        {
            SyncReorderMenuItem(moreFlyout, offered);
        }
        else
        {
            model.MoreButton.Flyout = CreateOverflowMenu(model, overflow, idSuffix: "More");
        }

        if (card.ContextFlyout is MenuFlyout contextFlyout)
        {
            SyncReorderMenuItem(contextFlyout, offered);
        }
        else
        {
            card.ContextFlyout = CreateOverflowMenu(model, overflow, idSuffix: "Context");
        }
    }

    private static Button CreateMoreButton(Guid profileId, Brush contrastBrush)
    {
        var icon = new FontIcon
        {
            Glyph = MoreGlyph,
            FontSize = 16,
            Foreground = contrastBrush,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };

        var transparent = new SolidColorBrush(Microsoft.UI.Colors.Transparent);
        var more = new Button
        {
            Content = icon,
            Width = 32,
            Height = 32,
            Padding = new Thickness(0),
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Top,
            Background = transparent,
            BorderThickness = new Thickness(0)
        };
        more.CornerRadius = (CornerRadius)Application.Current.Resources["ControlCornerRadius"];

        // Quiet default Button chrome so PointerOver does not flash a fill when
        // the adaptive More fades out on mouse leave.
        more.Resources["ButtonBackground"] = transparent;
        more.Resources["ButtonBackgroundPointerOver"] = transparent;
        more.Resources["ButtonBackgroundPressed"] =
            (Brush)Application.Current.Resources["ControlFillColorTertiaryBrush"];
        more.Resources["ButtonBackgroundDisabled"] = transparent;
        more.Resources["ButtonBorderBrush"] = transparent;
        more.Resources["ButtonBorderBrushPointerOver"] = transparent;
        more.Resources["ButtonBorderBrushPressed"] = transparent;
        more.Resources["ButtonBorderBrushDisabled"] = transparent;

        AutomationProperties.SetName(more, AppChrome.Get("ProfileCard_MoreOptions"));
        AutomationProperties.SetAutomationId(more, $"ProfileCardMore_{profileId:N}");
        return more;
    }

    private static MenuFlyout CreateOverflowMenu(
        ProfileCardModel model,
        ProfileCardOverflowCommands overflow,
        string idSuffix)
    {
        string compactId = model.Profile.Id.ToString("N");

        var editItem = new MenuFlyoutItem
        {
            Text = AppChrome.Get("ProfileCard_EditProfile"),
            Icon = new FontIcon { Glyph = EditGlyph }
        };
        AutomationProperties.SetAutomationId(editItem, $"ProfileCardEdit_{idSuffix}_{compactId}");
        editItem.KeyboardAcceleratorTextOverride = "F2";
        editItem.Click += (_, _) => overflow.OpenEditProfile(model.Profile);

        var reorderItem = new MenuFlyoutItem
        {
            Text = AppChrome.Get("ProfileCard_Reorder"),
            Icon = new FontIcon { Glyph = ReorderGlyph },
            Tag = ReorderMenuTag
        };
        AutomationProperties.SetAutomationId(reorderItem, $"ProfileCardReorder_{idSuffix}_{compactId}");
        reorderItem.Click += (_, _) => overflow.EnterReorderMode();
        ApplyReorderOffered(reorderItem, overflow.IsReorderOffered());

        var deleteItem = new MenuFlyoutItem
        {
            Text = AppChrome.Get("ProfileCard_Delete"),
            Icon = new FontIcon { Glyph = DeleteGlyph },
            Foreground = (Brush)Application.Current.Resources["SystemFillColorCriticalBrush"]
        };
        AutomationProperties.SetAutomationId(deleteItem, $"ProfileCardDelete_{idSuffix}_{compactId}");
        deleteItem.KeyboardAcceleratorTextOverride = "Del";
        deleteItem.Click += (_, _) => _ = overflow.DeleteProfile(model.Profile);

        var flyout = new MenuFlyout
        {
            Items =
            {
                editItem,
                reorderItem,
                new MenuFlyoutSeparator(),
                deleteItem
            }
        };

        flyout.Opened += (_, _) =>
        {
            model.IsOverflowFlyoutOpen = true;
            ApplyMoreReveal(model);
            overflow.FlyoutOpenChanged();
        };
        flyout.Closed += (_, _) =>
        {
            model.IsOverflowFlyoutOpen = false;
            ApplyMoreReveal(model);
            overflow.FlyoutOpenChanged();
        };

        return flyout;
    }

    private static void SyncReorderMenuItem(MenuFlyout flyout, bool offered)
    {
        foreach (MenuFlyoutItemBase item in flyout.Items)
        {
            if (item is MenuFlyoutItem reorder &&
                Equals(reorder.Tag, ReorderMenuTag))
            {
                ApplyReorderOffered(reorder, offered);
                return;
            }
        }
    }

    private static void ApplyReorderOffered(MenuFlyoutItem reorderItem, bool offered)
    {
        // Omit when not offered so the menu does not promise an empty action.
        reorderItem.Visibility = offered ? Visibility.Visible : Visibility.Collapsed;
        reorderItem.IsEnabled = offered;
    }

    private static void WireAdaptiveMore(Button card, ProfileCardModel model, Button moreButton)
    {
        void OnPointerEntered(PointerRoutedEventArgs e)
        {
            PointerDeviceType device = e.Pointer.PointerDeviceType;
            model.TouchOrPenInteraction = device is PointerDeviceType.Touch or PointerDeviceType.Pen;
            model.IsPointerOver = true;
            ApplyMoreReveal(model);
            ApplyPlayReveal(model);
        }

        void OnPointerExited(PointerRoutedEventArgs e)
        {
            model.IsPointerOver = false;
            if (e.Pointer.PointerDeviceType == PointerDeviceType.Mouse)
            {
                model.TouchOrPenInteraction = false;
            }

            ApplyMoreReveal(model);
            ApplyPlayReveal(model);
        }

        card.PointerEntered += (_, e) => OnPointerEntered(e);
        card.PointerExited += (_, e) => OnPointerExited(e);

        void SyncKeyboardFocus()
        {
            bool cardKeyboard = card.FocusState == FocusState.Keyboard;
            bool moreKeyboard = moreButton.FocusState == FocusState.Keyboard;
            model.HasKeyboardFocusWithin = cardKeyboard || moreKeyboard;
            ApplyMoreReveal(model);
            ApplyPlayReveal(model);
        }

        card.GotFocus += (_, _) => SyncKeyboardFocus();
        card.LostFocus += (_, _) => SyncKeyboardFocus();
        moreButton.GotFocus += (_, _) => SyncKeyboardFocus();
        moreButton.LostFocus += (_, _) => SyncKeyboardFocus();
    }

    /// <summary>
    /// F2 opens Edit Profile and Delete confirms delete. Enter and Space Launch
    /// through the card Button, which already consults <see cref="ProfileCardInteraction.ActivateKind"/>.
    /// </summary>
    private static void WireCardShortcuts(
        Button card,
        ProfileCardModel model,
        ProfileCardOverflowCommands overflow)
    {
        var edit = new KeyboardAccelerator { Key = VirtualKey.F2, ScopeOwner = card };
        edit.Invoked += (_, e) =>
        {
            if (!IsCardShortcutAllowed(ProfileCardShortcutKind.Edit, model, overflow))
            {
                return;
            }

            overflow.OpenEditProfile(model.Profile);
            e.Handled = true;
        };

        var deleteAccelerator = new KeyboardAccelerator { Key = VirtualKey.Delete, ScopeOwner = card };
        deleteAccelerator.Invoked += (s, e) =>
        {
            if (!IsCardShortcutAllowed(ProfileCardShortcutKind.Delete, model, overflow))
            {
                return;
            }

            _ = overflow.DeleteProfile(model.Profile);
            e.Handled = true;
        };

        card.KeyboardAccelerators.Add(edit);
        card.KeyboardAccelerators.Add(deleteAccelerator);
    }

    private static bool IsCardShortcutAllowed(
        ProfileCardShortcutKind kind,
        ProfileCardModel model,
        ProfileCardOverflowCommands overflow) =>
        ProfileCardInteraction.IsShortcutAllowed(
            kind,
            model.BoardEditMode,
            model.IsLaunching,
            overflow.IsDialogOrFlyoutOpen());

    private static void ApplyCardChrome(Button card, ProfileCardModel model)
    {
        ApplyLaunchingChrome(card, model);
        ApplyMoreReveal(model);
        ApplyPlayReveal(model);
    }

    private static void ApplyLaunchingChrome(Button card, ProfileCardModel model)
    {
        bool launching = model.IsLaunching;
        if (model.LaunchProgress is not null)
        {
            model.LaunchProgress.IsActive = launching;
            model.LaunchProgress.Visibility = launching ? Visibility.Visible : Visibility.Collapsed;
        }

        if (model.LaunchingLabel is not null)
        {
            model.LaunchingLabel.Visibility = launching ? Visibility.Visible : Visibility.Collapsed;
        }

        if (model.CaptionBlock is not null)
        {
            model.CaptionBlock.Visibility = launching ? Visibility.Collapsed : Visibility.Visible;
        }

        if (model.ProductIconHost is not null)
        {
            // Product icon sits in the same cell as the progress ring; hide the bitmap/glyph while launching.
            foreach (UIElement child in model.ProductIconHost.Children)
            {
                if (child is ProgressRing)
                {
                    continue;
                }

                child.Visibility = launching ? Visibility.Collapsed : Visibility.Visible;
            }
        }

        if (model.IdentityIcon is not null &&
            !string.IsNullOrWhiteSpace(model.Profile.WindowsNetworkIdentity))
        {
            model.IdentityIcon.Visibility = launching ? Visibility.Collapsed : Visibility.Visible;
        }

        string automationName = launching
            ? AppChrome.Format("ProfileCard_LaunchingAnnouncement", model.Profile.Name)
            : AppChrome.CardAutomationName(
                ProfileCardInteraction.AutomationKind(model.BoardEditMode),
                model.Profile.Name);
        AutomationProperties.SetAutomationId(card, $"ProfileCard_{model.Profile.Id:N}");
        if (launching)
        {
            AutomationProperties.SetLiveSetting(card, AutomationLiveSetting.Assertive);
            AutomationProperties.SetName(card, automationName);
        }
        else
        {
            AutomationProperties.SetName(card, automationName);
            AutomationProperties.SetLiveSetting(card, AutomationLiveSetting.Off);
        }
    }

    private static void ApplyMoreReveal(ProfileCardModel model)
    {
        if (model.MoreButton is null)
        {
            return;
        }

        // Mutating More chrome while WinUI owns a Launch Order drag source can
        // native-crash (0xc0000409) - especially on outside-window cancel.
        if (ProfileBoardLaunchOrder.IsDragSessionActive)
        {
            return;
        }

        if (model.BoardEditMode)
        {
            // Avoid re-assigning Collapsed after post-drop Relayout - that pointer-exit
            // mutation has native-crashed WinUI (0xc0000409) once the busy gate clears.
            if (model.MoreButton.Visibility != Visibility.Collapsed)
            {
                model.MoreButton.Visibility = Visibility.Collapsed;
            }

            return;
        }

        // Interaction modality only - do not use digitizer capability. Touch-capable
        // laptops would otherwise keep More visible for mouse users on every card.
        bool revealed = ProfileCardInteraction.IsMoreRevealed(
            reorderMode: model.BoardEditMode,
            touchOrPenPreferred: model.TouchOrPenInteraction,
            isPointerOver: model.IsPointerOver,
            hasKeyboardFocusWithin: model.HasKeyboardFocusWithin,
            isFlyoutOpen: model.IsOverflowFlyoutOpen);

        model.MoreButton.Visibility = Visibility.Visible;
        model.MoreButton.Opacity = revealed ? 1 : 0;
        model.MoreButton.IsHitTestVisible = revealed;
        model.MoreButton.IsTabStop = true;

        if (!revealed)
        {
            // Drop PointerOver/Pressed fills before opacity hits 0 so leave does not flash.
            VisualStateManager.GoToState(model.MoreButton, "Normal", useTransitions: false);
        }
    }

    private static void ApplyPlayReveal(ProfileCardModel model)
    {
        if (model.PlayIcon is null)
        {
            return;
        }

        bool show = !model.BoardEditMode &&
            (model.IsPointerOver || model.HasKeyboardFocusWithin);
        model.PlayIcon.Opacity = show ? 0.9 : 0;
    }
}
