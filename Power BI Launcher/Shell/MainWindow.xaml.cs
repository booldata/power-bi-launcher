using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.UI;
using Microsoft.UI.Input;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.Foundation;
using Windows.Graphics;
using Windows.Storage.Pickers;
using Windows.System;
using Windows.UI;
using Windows.UI.ViewManagement;
using WinRT.Interop;
using IOPath = System.IO.Path;

namespace PowerBILauncher
{
    public sealed partial class MainWindow : Window
    {
        // Reorder / Done header chrome (Segoe Fluent Icons).
        private const string ReorderGlyph = "\uE8CB";
        private const string DoneGlyph = "\uE73E";
        // VK_OEM_COMMA - Windows.System.VirtualKey has no named comma member.
        private const VirtualKey CommaKey = (VirtualKey)0xBC;
        private const double DefaultWindowHeightDip = 640;

        private readonly ProfileLifecycle _lifecycle;
        private readonly AppWindow _appWindow;
        private readonly ProfileBoardHost _boardHost;
        private readonly EditProfileSession _editSession;
        private readonly WindowCloseGate _closeGate;
        private readonly SettingsPanelSession _settingsSession;
        private TrayIcon? _trayIcon;
        private CancellationTokenSource? _messageCts;
        private CancellationTokenSource? _updateCheckCts;
        private GitHubReleaseChecker? _updateChecker;
        private KeyboardAccelerator? _addProfileAccelerator;
        private KeyboardAccelerator? _settingsAccelerator;
        private KeyboardAccelerator? _leaveReorderAccelerator;
        private int _openDialogCount;
        private bool _reorderModeAnnounced;
        private bool _resourcesCleanedUp;
        private bool _windowIsActive;
        private DateTimeOffset _lastDeactivatedAt = DateTimeOffset.MinValue;

        private string LauncherVersionText => AppChrome.Format("LauncherVersion_Label", GetLauncherVersion());

        public MainWindow()
        {
            InitializeComponent();
            // WinUI 3 Window is not a DependencyObject, so x:Uid cannot set Title.
            Title = AppChrome.Get("MainWindow/Title");

            ExtendsContentIntoTitleBar = true;
            SystemBackdrop = new MicaBackdrop();

            _lifecycle = new ProfileLifecycle(AppContext.BaseDirectory);
            _boardHost = new ProfileBoardHost(_lifecycle, new ProfileBoardShell
            {
                SaveProfiles = SaveProfilesOrReport,
                OpenEditProfile = OpenEditProfile,
                LaunchProfile = LaunchFromBoard,
                DeleteProfile = DeleteProfileFromBoard,
                RequestEmptyBoardFocus = RequestEmptyBoardFocus,
                AddProfile = AddProfileFromBoard,
                IsDialogOpen = () => _openDialogCount > 0,
                SyncBoardCommanding = () => UpdateBoardCommanding(onProfilesBoard: true),
                SyncShortcutAvailability = SyncAppShortcutAvailability
            });
            _editSession = new EditProfileSession(_lifecycle, new EditProfileSessionHost
            {
                ShowMessage = ShowMessage,
                ShowErrorAsync = ShowErrorDialogAsync,
                PickAvatarImagePathAsync = PickAvatarImagePathAsync,
                ConfirmDiscardAsync = ConfirmDiscardEditProfileAsync,
                ConfirmDeleteAsync = ConfirmDeleteProfileAsync
            });
            _editSession.OpenFolderRequested += EditSession_OpenFolderRequested;
            _editSession.AvatarColorRequested += EditSession_AvatarColorRequested;
            _editSession.PickCardColorRequested += EditSession_PickCardColorRequested;
            _editSession.OpenSettingsRequested += EditSession_OpenSettingsRequested;
            _editSession.SessionEnded += EditSession_SessionEnded;
            _settingsSession = new SettingsPanelSession(_lifecycle, new SettingsPanelSessionHost
            {
                ShowMessage = ShowMessage,
                PickPowerBiExecutablePathAsync = PickPowerBiExecutablePathAsync,
                PickProfilesFolderAsync = PickProfilesFolderAsync,
                OpenProfilesFolder = OpenFolderInExplorer
            });
            _settingsSession.SettingsSaved += (_, _) => ApplyUiMode(UiMode.Profiles);
            _settingsSession.SettingsCancelled += (_, _) => ApplyUiMode(UiMode.Profiles);
            ConfigureAboutSection();
            _settingsSession.Attach(SettingsPanel);
            _closeGate = new WindowCloseGate(new WindowCloseGateHost
            {
                IsEditSessionDirty = () => _editSession.IsEditing && _editSession.IsDirty(),
                TryDiscardEditSessionAsync = () => _editSession.TryDiscardAsync(navigateToBoard: false),
                CleanupBeforeClose = CleanupResources,
                HideWindow = HideWindow,
                ForceCloseWindow = () => Close()
            });
            _appWindow = GetAppWindow();
            ApplyInitialWindowSize();
            ApplyWindowMinimumSize();
            ApplyAppIcon();
            _appWindow.Closing += AppWindow_Closing;
            _appWindow.Changed += AppWindow_Changed;
            Activated += MainWindow_Activated;

            ProfilesFrame.Navigated += ProfilesFrame_Navigated;
            EnsureProfileBoardPage();

            IReadOnlyList<UnreadableConfigFile> unreadable = _lifecycle.Load();
            if (unreadable.Count > 0)
            {
                ShowUnreadableConfigOnceLoaded(unreadable);
            }

            _settingsSession.Reset(!_lifecycle.Settings.HasCompletedSetup);
            _boardHost.RefreshCards();
            SetupTrayIcon();
            AttachAppShortcuts();
            ToolTipService.SetToolTip(BoardDoneButton, AppChrome.Get("Board_DoneTooltip"));
            UpdateBoardCommanding(onProfilesBoard: true);

            if (!_lifecycle.Settings.HasCompletedSetup)
            {
                ApplyUiMode(UiMode.Onboarding);
                ShowMessage(AppChrome.Get("Toast_ReviewSettings"), 45000);
            }
            else
            {
                ApplyUiMode(UiMode.Profiles);
            }

            // Card fills are SolidColorBrush snapshots (incl. "System" → AppTheme.CardSurfaceColor).
            // ThemeResources in XAML update on their own; recreate cards when the UI theme flips.
            RootGrid.ActualThemeChanged += (_, _) => OnUiThemeChanged();

            // OnExplicitShutdown keeps the process alive for tray hide; a real window close must Exit.
            Closed += (_, _) =>
            {
                CleanupResources();
                App.ExitApp();
            };

            if (_lifecycle.Settings.HasCompletedSetup)
            {
                StartLauncherUpdateCheck();
            }
        }

        private void ShowUnreadableConfigOnceLoaded(IReadOnlyList<UnreadableConfigFile> unreadable)
        {
            string message = string.Join(
                Environment.NewLine + Environment.NewLine,
                unreadable.Select(AppChrome.UnreadableConfig));

            // ContentDialog needs a XamlRoot, which exists only after the content has loaded.
            void OnLoaded(object sender, RoutedEventArgs e)
            {
                RootGrid.Loaded -= OnLoaded;
                _ = ShowErrorDialogAsync(message);
            }

            RootGrid.Loaded += OnLoaded;
        }

        private static string GetLauncherVersion()
        {
            string? informationalVersion = typeof(MainWindow).Assembly
                .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
                ?.InformationalVersion;

            if (string.IsNullOrWhiteSpace(informationalVersion))
            {
                return AppChrome.Get("LauncherVersion_Unknown");
            }

            int metadataSeparator = informationalVersion.IndexOf('+');
            return metadataSeparator >= 0
                ? informationalVersion[..metadataSeparator]
                : informationalVersion;
        }

        private void ConfigureAboutSection()
        {
            SettingsPanel.SetLauncherVersionText(LauncherVersionText);

            DateTimeOffset? buildDate = LauncherBuildInfo.TryGetBuildDate(typeof(MainWindow).Assembly);
            if (buildDate is not null)
            {
                SettingsPanel.SetLauncherBuildDateText(
                    AppChrome.Format(
                        "LauncherBuildDate_Label",
                        LauncherBuildInfo.FormatBuildDate(buildDate.Value)));
            }

            bool canCheck = LauncherVersionPolicy.TryParseInstalled(GetLauncherVersion(), out _);
            SettingsPanel.SetCheckForUpdatesEnabled(
                canCheck,
                AppChrome.Get("LauncherVersion_CheckUnavailableTooltip"));
            SettingsPanel.CheckForUpdatesRequested += SettingsPanel_CheckForUpdatesRequested;
        }

        private async void SettingsPanel_CheckForUpdatesRequested(object? sender, EventArgs e)
        {
            SettingsPanel.SetCheckForUpdatesBusy(true);
            SettingsPanel.HideAboutUpdateCheck();

            try
            {
                using var checker = new GitHubReleaseChecker();
                LauncherUpdateCheckResult result = await checker
                    .CheckDetailedAsync(GetLauncherVersion())
                    .ConfigureAwait(false);
                if (_resourcesCleanedUp)
                {
                    return;
                }

                LauncherUpdateCheckResult captured = result;
                DispatcherQueue.TryEnqueue(() => ShowAboutUpdateCheckResult(captured));
            }
            finally
            {
                if (!_resourcesCleanedUp)
                {
                    DispatcherQueue.TryEnqueue(() => SettingsPanel.SetCheckForUpdatesBusy(false));
                }
            }
        }

        private void ShowAboutUpdateCheckResult(LauncherUpdateCheckResult result)
        {
            switch (result.Status)
            {
                case LauncherUpdateCheckStatus.UpToDate:
                    SettingsPanel.ShowAboutUpdateCheck(
                        InfoBarSeverity.Informational,
                        AppChrome.Get("LauncherVersion_UpToDate"),
                        releaseUrl: null);
                    break;
                case LauncherUpdateCheckStatus.UpdateAvailable:
                    LauncherUpdate update = result.Update!.Value;
                    SettingsPanel.ShowAboutUpdateCheck(
                        InfoBarSeverity.Success,
                        AppChrome.Format("LauncherVersion_Available", update.VersionText),
                        update.ReleaseUrl);
                    break;
                case LauncherUpdateCheckStatus.Failed:
                    SettingsPanel.ShowAboutUpdateCheck(
                        InfoBarSeverity.Warning,
                        AppChrome.Get("LauncherVersion_CheckFailed"),
                        releaseUrl: null);
                    break;
                case LauncherUpdateCheckStatus.Ineligible:
                    break;
            }
        }

        private void StartLauncherUpdateCheck()
        {
            _updateCheckCts = new CancellationTokenSource();
            _updateChecker = new GitHubReleaseChecker();
            _ = CheckForLauncherUpdateAsync(_updateCheckCts.Token);
        }

        private async Task CheckForLauncherUpdateAsync(CancellationToken cancellationToken)
        {
            GitHubReleaseChecker? checker = _updateChecker;
            if (checker is null)
            {
                return;
            }

            LauncherUpdate? update;
            try
            {
                update = await checker.CheckAsync(GetLauncherVersion(), cancellationToken).ConfigureAwait(false);
            }
            catch
            {
                return;
            }

            if (update is null || cancellationToken.IsCancellationRequested)
            {
                return;
            }

            LauncherUpdate found = update.Value;
            DispatcherQueue.TryEnqueue(() => ShowLauncherUpdate(found));
        }

        private void ShowLauncherUpdate(LauncherUpdate update)
        {
            if (_resourcesCleanedUp)
            {
                return;
            }

            UpdateAvailableInfoBar.Message = AppChrome.Format("LauncherVersion_Available", update.VersionText);
            ViewReleaseButton.NavigateUri = update.ReleaseUrl;
            UpdateAvailableInfoBar.IsOpen = true;
        }

        private void ApplyInitialWindowSize()
        {
            double scale = GetWindowDpiScale();
            _appWindow.Resize(new SizeInt32(
                (int)(WindowLayoutConstraints.DefaultWidth * scale),
                (int)(DefaultWindowHeightDip * scale)));
        }

        private double GetWindowDpiScale()
        {
            IntPtr hwnd = WindowNative.GetWindowHandle(this);
            uint dpi = GetDpiForWindow(hwnd);
            return dpi == 0 ? 1.0 : dpi / 96.0;
        }

        private void ApplyWindowMinimumSize()
        {
            if (_appWindow.Presenter is not OverlappedPresenter presenter)
            {
                return;
            }

            presenter.PreferredMinimumWidth = (int)Math.Ceiling(WindowLayoutConstraints.MinWidth);
            presenter.PreferredMinimumHeight = (int)Math.Ceiling(WindowLayoutConstraints.MinHeight);
        }

        private void EnsureProfileBoardPage()
        {
            if (_boardHost.HasPage)
            {
                return;
            }

            ProfilesFrame.Navigate(typeof(ProfileBoardPage));
        }

        private void ProfilesFrame_Navigated(object sender, Microsoft.UI.Xaml.Navigation.NavigationEventArgs e)
        {
            if (e.Content is ProfileBoardPage boardPage)
            {
                AttachBoardPage(boardPage);
                return;
            }

            if (e.Content is EditProfilePage editPage)
            {
                AttachEditPage(editPage);
            }
        }

        private void AttachBoardPage(ProfileBoardPage boardPage)
        {
            _editSession.DetachPage();
            _boardHost.AttachPage(boardPage);
            SyncEditChrome();
        }

        private void AttachEditPage(EditProfilePage editPage)
        {
            _editSession.AttachPage(editPage);
            SyncEditChrome();
        }

        private void DetachEditPage() =>
            _editSession.DetachPage();

        private void EditSession_SessionEnded(object? sender, EditProfileSessionEndedEventArgs e)
        {
            if (e.NavigateToBoard)
            {
                NavigateToProfileBoard();
            }

            SyncEditChrome();
        }

        private async Task<bool> ConfirmDiscardEditProfileAsync()
        {
            var dialog = new ContentDialog
            {
                Title = AppChrome.Get("Dialog_DiscardTitle"),
                Content = AppChrome.Get("Dialog_DiscardBody"),
                PrimaryButtonText = AppChrome.Get("Dialog_Discard"),
                CloseButtonText = AppChrome.Get("Dialog_Stay"),
                DefaultButton = ContentDialogButton.Close,
                XamlRoot = Content.XamlRoot
            };

            ContentDialogResult result = await ShowModalDialogAsync(dialog);
            return result == ContentDialogResult.Primary;
        }

        private async Task<bool> ConfirmDeleteProfileAsync(string profileName)
        {
            var dialog = new ContentDialog
            {
                Title = AppChrome.Get("Dialog_DeleteProfileTitle"),
                Content = AppChrome.Format("Dialog_DeleteProfileBody", profileName),
                PrimaryButtonText = AppChrome.Get("Dialog_Delete"),
                CloseButtonText = AppChrome.Get("Dialog_Cancel"),
                DefaultButton = ContentDialogButton.Close,
                XamlRoot = Content.XamlRoot
            };

            ContentDialogResult result = await ShowModalDialogAsync(dialog);
            return result == ContentDialogResult.Primary;
        }

        private async void ShellBackButton_Click(object sender, RoutedEventArgs e) =>
            await _editSession.TryDiscardAsync(navigateToBoard: true);

        private void SyncEditChrome()
        {
            bool onProfiles = ProfilesPanel.Visibility == Visibility.Visible;
            bool editing = _editSession.IsEditing && onProfiles;

            ShellNav.Visibility = editing ? Visibility.Collapsed : Visibility.Visible;
            EditShellChrome.Visibility = editing ? Visibility.Visible : Visibility.Collapsed;
            ProfilesPageTitle.Visibility = onProfiles && !editing ? Visibility.Visible : Visibility.Collapsed;
            UpdateBoardCommanding(onProfilesBoard: onProfiles && !editing);
            UpdateTitleBarChromeLayout();
        }

        private void ShellTopChrome_SizeChanged(object sender, SizeChangedEventArgs e) =>
            UpdateTitleBarChromeLayout();

        /// <summary>
        /// Keep edit Back clear of the drag region and clickable under ExtendsContentIntoTitleBar.
        /// </summary>
        private void UpdateTitleBarChromeLayout()
        {
            double scale = RootGrid.XamlRoot?.RasterizationScale ?? 1.0;
            if (scale <= 0)
            {
                scale = 1.0;
            }

            double leftInsetDip = _appWindow.TitleBar.LeftInset / scale;

            ShellTopChrome.Padding = new Thickness(
                20 + leftInsetDip,
                12,
                20,
                0);

            UpdateTitleBarPassthroughRegions(scale);
        }

        private void UpdateTitleBarPassthroughRegions(double scale)
        {
            InputNonClientPointerSource source =
                InputNonClientPointerSource.GetForWindowId(_appWindow.Id);

            if (EditShellChrome.Visibility != Visibility.Visible)
            {
                source.SetRegionRects(NonClientRegionKind.Passthrough, Array.Empty<RectInt32>());
                return;
            }

            var rects = new List<RectInt32>();
            AddPassthroughRect(rects, ShellBackButton, scale);
            source.SetRegionRects(NonClientRegionKind.Passthrough, rects.ToArray());
        }

        private static void AddPassthroughRect(
            List<RectInt32> rects,
            FrameworkElement element,
            double scale)
        {
            if (element.Visibility != Visibility.Visible
                || element.ActualWidth <= 0
                || element.ActualHeight <= 0)
            {
                return;
            }

            GeneralTransform transform = element.TransformToVisual(null);
            Rect bounds = transform.TransformBounds(
                new Rect(0, 0, element.ActualWidth, element.ActualHeight));

            rects.Add(new RectInt32(
                (int)Math.Round(bounds.X * scale),
                (int)Math.Round(bounds.Y * scale),
                (int)Math.Round(bounds.Width * scale),
                (int)Math.Round(bounds.Height * scale)));
        }

        private void OnUiThemeChanged()
        {
            _boardHost.RefreshCards();

            _editSession.RefreshSystemCardColorPreview();
        }

        private AppWindow GetAppWindow()
        {
            IntPtr hwnd = WindowNative.GetWindowHandle(this);
            WindowId id = Win32Interop.GetWindowIdFromWindow(hwnd);
            return AppWindow.GetFromWindowId(id);
        }

        private void OpenSettings()
        {
            bool onboarding = !_lifecycle.Settings.HasCompletedSetup;
            _settingsSession.Reset(onboarding);
            ApplyUiMode(onboarding ? UiMode.Onboarding : UiMode.Settings);
        }

        private void ApplyUiMode(UiMode mode)
        {
            bool settings = mode is UiMode.Settings or UiMode.Onboarding;

            ProfilesPanel.Visibility = mode == UiMode.Profiles ? Visibility.Visible : Visibility.Collapsed;
            SettingsPanel.Visibility = settings ? Visibility.Visible : Visibility.Collapsed;

            if (settings)
            {
                // Leaving the board always exits Reorder mode.
                _boardHost.SetBoardEditMode(false, refreshCards: _boardHost.BoardEditMode);

                SelectNavTag("settings");
            }
            else if (mode == UiMode.Profiles)
            {
                EnsureProfileBoardPage();
                if (!_editSession.IsEditing && ProfilesFrame.Content is not ProfileBoardPage)
                {
                    ProfilesFrame.Navigate(typeof(ProfileBoardPage));
                }

                SelectNavTag("profiles");
            }

            SyncEditChrome();
        }

        /// <summary>
        /// Header chrome for the Profile board: normal → Add profile + Reorder
        /// (Reorder hidden when &lt; 2 Profiles); Reorder mode → mode banner with Done
        /// (Add hidden).
        /// </summary>
        private void UpdateBoardCommanding(bool onProfilesBoard)
        {
            if (!onProfilesBoard)
            {
                AnnounceReorderMode(false);
                AddProfileButton.Visibility = Visibility.Collapsed;
                ReorderModeBanner.Visibility = Visibility.Collapsed;
                BoardEditToggleButton.Visibility = Visibility.Collapsed;
                SyncAppShortcutAvailability();
                return;
            }

            bool reorderMode = _boardHost.BoardEditMode;
            AddProfileButton.Visibility = reorderMode ? Visibility.Collapsed : Visibility.Visible;
            if (reorderMode)
            {
                ReorderModeBanner.Visibility = Visibility.Visible;
                AnnounceReorderMode(true);
                BoardEditToggleButton.Visibility = Visibility.Collapsed;
                BoardDoneButton.Content = CreateIconLabel(DoneGlyph, AppChrome.Get("Board_Done"));
                SyncAppShortcutAvailability();
                return;
            }

            AnnounceReorderMode(false);
            ReorderModeBanner.Visibility = Visibility.Collapsed;

            if (_boardHost.IsReorderOffered)
            {
                BoardEditToggleButton.Visibility = Visibility.Visible;
                BoardEditToggleButton.Content = CreateIconLabel(ReorderGlyph, AppChrome.Get("Board_Reorder"));
                SyncAppShortcutAvailability();
                return;
            }

            BoardEditToggleButton.Visibility = Visibility.Collapsed;
            SyncAppShortcutAvailability();
        }

        private void AnnounceReorderMode(bool reorderMode)
        {
            if (_reorderModeAnnounced == reorderMode)
            {
                return;
            }

            _reorderModeAnnounced = reorderMode;
            AutomationProperties.SetName(
                ReorderModeBanner,
                AppChrome.Get(reorderMode ? "Board_ReorderModeOn" : "Board_ReorderModeOff"));
        }

        private void AttachAppShortcuts()
        {
            _addProfileAccelerator = new KeyboardAccelerator
            {
                Key = VirtualKey.N,
                Modifiers = VirtualKeyModifiers.Control
            };
            _addProfileAccelerator.Invoked += (_, e) =>
            {
                if (!AreAppShortcutsAllowed())
                {
                    return;
                }

                AddProfileFromBoard();
                e.Handled = true;
            };

            _settingsAccelerator = new KeyboardAccelerator
            {
                Key = CommaKey,
                Modifiers = VirtualKeyModifiers.Control
            };
            _settingsAccelerator.Invoked += (_, e) =>
            {
                if (!AreAppShortcutsAllowed())
                {
                    return;
                }

                if (SettingsPanel.Visibility != Visibility.Visible)
                {
                    OpenSettings();
                }

                e.Handled = true;
            };

            _leaveReorderAccelerator = new KeyboardAccelerator { Key = VirtualKey.Escape };
            _leaveReorderAccelerator.Invoked += (_, e) =>
            {
                if (ProfilesPanel.Visibility != Visibility.Visible || _editSession.IsEditing)
                {
                    return;
                }

                if (!ProfileCardInteraction.ShouldLeaveReorderOnEscape(
                        _boardHost.BoardEditMode,
                        IsDialogOrFlyoutOpen))
                {
                    return;
                }

                _boardHost.SetBoardEditMode(false, refreshCards: true);
                UpdateBoardCommanding(onProfilesBoard: true);
                e.Handled = true;
            };

            RootGrid.KeyboardAccelerators.Add(_addProfileAccelerator);
            RootGrid.KeyboardAccelerators.Add(_settingsAccelerator);
            RootGrid.KeyboardAccelerators.Add(_leaveReorderAccelerator);
            SyncAppShortcutAvailability();
        }

        private bool IsDialogOrFlyoutOpen =>
            _openDialogCount > 0 || _boardHost.IsAnyFlyoutOpen;

        private bool AreAppShortcutsAllowed() =>
            ProfileCardInteraction.AreAppShortcutsAllowed(
                _lifecycle.Settings.HasCompletedSetup,
                _editSession.IsEditing,
                IsDialogOrFlyoutOpen);

        private void SyncAppShortcutAvailability()
        {
            bool allowed = AreAppShortcutsAllowed();
            if (_addProfileAccelerator is not null)
            {
                _addProfileAccelerator.IsEnabled = allowed;
            }

            if (_settingsAccelerator is not null)
            {
                _settingsAccelerator.IsEnabled = allowed;
            }

            if (_leaveReorderAccelerator is not null)
            {
                _leaveReorderAccelerator.IsEnabled =
                    ProfilesPanel.Visibility == Visibility.Visible
                    && !_editSession.IsEditing
                    && ProfileCardInteraction.ShouldLeaveReorderOnEscape(
                        _boardHost.BoardEditMode,
                        IsDialogOrFlyoutOpen);
            }

            _boardHost.SetCardShortcutsEnabled(
                AreAppShortcutsAllowed()
                && ProfilesPanel.Visibility == Visibility.Visible
                && !_boardHost.BoardEditMode);
        }

        private async Task<ContentDialogResult> ShowModalDialogAsync(ContentDialog dialog)
        {
            _openDialogCount++;
            SyncAppShortcutAvailability();
            try
            {
                return await dialog.ShowAsync();
            }
            finally
            {
                _openDialogCount--;
                SyncAppShortcutAvailability();
            }
        }

        private static StackPanel CreateIconLabel(string glyph, string label) =>
            new()
            {
                Orientation = Orientation.Horizontal,
                Spacing = 8,
                Children =
                {
                    new FontIcon
                    {
                        Glyph = glyph,
                        FontSize = 16,
                        VerticalAlignment = VerticalAlignment.Center
                    },
                    new TextBlock
                    {
                        Text = label,
                        VerticalAlignment = VerticalAlignment.Center
                    }
                }
            };

        private void BoardEditToggleButton_Click(object sender, RoutedEventArgs e)
        {

            _boardHost.SetBoardEditMode(!_boardHost.BoardEditMode, refreshCards: true);
            UpdateBoardCommanding(onProfilesBoard: true);
        }

        private void SelectNavTag(string tag)
        {
            foreach (var item in ShellNav.MenuItems.OfType<NavigationViewItem>())
            {
                if (string.Equals(item.Tag?.ToString(), tag, StringComparison.OrdinalIgnoreCase))
                {
                    ShellNav.SelectedItem = item;
                    return;
                }
            }
        }

        private async void ShellNav_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
        {
            if (args.SelectedItem is not NavigationViewItem item)
            {
                return;
            }

            string? tag = item.Tag?.ToString();
            if (tag == "settings")
            {
                if (_editSession.IsEditing)
                {
                    bool left = await _editSession.TryDiscardAsync(navigateToBoard: true);
                    if (!left)
                    {
                        SelectNavTag("profiles");
                        return;
                    }
                }

                OpenSettings();
            }
            else if (tag == "profiles")
            {
                if (!_lifecycle.Settings.HasCompletedSetup)
                {
                    ApplyUiMode(UiMode.Onboarding);
                    SelectNavTag("settings");
                    return;
                }

                ApplyUiMode(UiMode.Profiles);
            }
        }

        private void RequestEmptyBoardFocus()
        {
            if (BoardEditToggleButton.Visibility == Visibility.Visible)
            {
                BoardEditToggleButton.Focus(FocusState.Programmatic);
            }
        }

        private void AddProfileButton_Click(object sender, RoutedEventArgs e) =>
            AddProfileFromBoard();

        private void AddProfileFromBoard()
        {
            _editSession.BeginDraft();
            NavigateToEditProfile();
        }

        private bool SaveProfilesOrReport()
        {
            if (_lifecycle.TrySaveProfiles(out string? osDetail))
            {
                return true;
            }

            ShowMessage(AppChrome.ProfilesSaveFailed(osDetail, _lifecycle.ProfileSavesBlockedBySettings), 10000);
            return false;
        }

        /// <summary>
        /// Last-resort report for an exception nothing else handled (see <see cref="App"/>).
        /// </summary>
        public void ShowUnexpectedError(string detail) =>
            ShowMessage(AppChrome.Format("Toast_UnexpectedError", detail), 10000);

        private ProfileLaunchResult LaunchFromBoard(Profile profile)
        {
            ProfileLaunchResult result = _lifecycle.TryLaunch(profile);
            if (result.Succeeded)
            {
                return result;
            }

            _ = ShowLaunchErrorDialogAsync(result, profile);
            return result;
        }

        private void OpenEditProfile(Profile profile)
        {
            _editSession.Begin(profile);
            NavigateToEditProfile();
        }

        private void NavigateToEditProfile()
        {
            ApplyUiMode(UiMode.Profiles);

            bool animationsEnabled = new UISettings().AnimationsEnabled;
            NavigationTransitionInfo transition = animationsEnabled
                ? new DrillInNavigationTransitionInfo()
                : new SuppressNavigationTransitionInfo();
            ProfilesFrame.Navigate(typeof(EditProfilePage), null, transition);
        }

        /// <summary>
        /// Board overflow / ContextFlyout Delete - same confirm + lifecycle as Danger Zone.
        /// Stays on the Profile board unless that Profile was open in Edit Profile.
        /// </summary>
        private async Task DeleteProfileFromBoard(Profile profile)
        {
            bool editingThis =
                _editSession.IsEditing
                && _editSession.Profile is not null
                && _editSession.Profile.Id == profile.Id;

            // Navigate back to the board only when leaving an Edit Profile session for
            // the deleted Profile; board-side delete should not Drill into Edit Profile.
            if (!await _editSession.TryDeleteAsync(profile, navigateToBoard: editingThis))
            {
                return;
            }

            if (editingThis)
            {
                return;
            }

            _boardHost.RefreshCards();
            UpdateBoardCommanding(onProfilesBoard: true);
        }

        private void NavigateToProfileBoard()
        {
            bool animationsEnabled = new UISettings().AnimationsEnabled;
            if (ProfilesFrame.CanGoBack)
            {
                if (animationsEnabled)
                {
                    ProfilesFrame.GoBack();
                }
                else
                {
                    ProfilesFrame.GoBack(new SuppressNavigationTransitionInfo());
                }

                return;
            }

            NavigationTransitionInfo transition = animationsEnabled
                ? new DrillInNavigationTransitionInfo()
                : new SuppressNavigationTransitionInfo();
            ProfilesFrame.Navigate(typeof(ProfileBoardPage), null, transition);
        }

        private async void EditSession_OpenSettingsRequested(object? sender, EventArgs e)
        {
            if (!await _editSession.TryDiscardAsync(navigateToBoard: false))
            {
                return;
            }

            OpenSettings();
        }

        private void EditSession_OpenFolderRequested(object? sender, EventArgs e)
        {
            Profile? profile = _editSession.Profile;
            if (profile is null)
            {
                return;
            }

            try
            {
                ProfilePaths paths = _lifecycle.EnsureProfileDirectories(profile);
                OpenFolderInExplorer(paths.Root);
            }
            catch (Exception ex)
            {
                ShowMessage(AppChrome.Format("Toast_OpenFolderFailed", ex.Message), 5000);
            }
        }

        private async void LaunchProfile(Profile profile, bool showStatusMessage, bool ensureWindowVisibleOnError = false)
        {
            ProfileLaunchResult result = _lifecycle.TryLaunch(profile);
            if (result.Succeeded)
            {
                if (showStatusMessage)
                {
                    ShowMessage(AppChrome.Format("Toast_Starting", profile.Name));
                }

                return;
            }

            if (ensureWindowVisibleOnError)
            {
                ShowWindow();
            }

            if (showStatusMessage)
            {
                ShowMessage(AppChrome.LaunchError(result), 5000);
            }

            await ShowLaunchErrorDialogAsync(result, profile);
        }

        private async Task ShowLaunchErrorDialogAsync(ProfileLaunchResult result, Profile profile)
        {
            var dialog = new ContentDialog
            {
                Title = AppChrome.LaunchErrorTitle(result),
                Content = AppChrome.LaunchError(result),
                CloseButtonText = result.Remediation == ProfileLaunchRemediation.None
                    ? AppChrome.Get("Dialog_Ok")
                    : AppChrome.Get("Dialog_Cancel"),
                XamlRoot = Content.XamlRoot
            };

            if (result.Remediation == ProfileLaunchRemediation.Settings)
            {
                dialog.PrimaryButtonText = AppChrome.Get("Dialog_OpenSettings");
                dialog.DefaultButton = ContentDialogButton.Primary;
            }
            else if (result.Remediation == ProfileLaunchRemediation.EditProfile)
            {
                dialog.PrimaryButtonText = AppChrome.Get("Dialog_EditProfile");
                dialog.DefaultButton = ContentDialogButton.Primary;
            }

            ContentDialogResult dialogResult = await ShowModalDialogAsync(dialog);
            if (dialogResult != ContentDialogResult.Primary)
            {
                return;
            }

            if (result.Remediation == ProfileLaunchRemediation.Settings)
            {
                OpenSettings();
            }
            else if (result.Remediation == ProfileLaunchRemediation.EditProfile)
            {
                OpenEditProfile(profile);
            }
        }

        private async Task ShowErrorDialogAsync(string message)
        {
            var dialog = new ContentDialog
            {
                Title = AppChrome.Get("Dialog_ErrorTitle"),
                Content = message,
                CloseButtonText = AppChrome.Get("Dialog_Ok"),
                XamlRoot = Content.XamlRoot
            };
            await ShowModalDialogAsync(dialog);
        }

        private async Task<string?> PickAvatarImagePathAsync()
        {
            var picker = new FileOpenPicker();
            InitializeWithWindow.Initialize(picker, WindowNative.GetWindowHandle(this));
            picker.ViewMode = PickerViewMode.Thumbnail;
            picker.SuggestedStartLocation = PickerLocationId.PicturesLibrary;
            picker.FileTypeFilter.Add(".png");
            picker.FileTypeFilter.Add(".jpg");
            picker.FileTypeFilter.Add(".jpeg");
            picker.FileTypeFilter.Add(".ico");
            picker.FileTypeFilter.Add(".bmp");

            var file = await picker.PickSingleFileAsync();
            return file?.Path;
        }

        private async void EditSession_AvatarColorRequested(object? sender, EventArgs e) =>
            await PickAvatarColorAsync();

        private async void EditSession_PickCardColorRequested(object? sender, EventArgs e) =>
            await PickCardColorAsync();

        private async Task PickCardColorAsync()
        {
            EditProfilePage? page = _editSession.Page;
            if (page is null)
            {
                return;
            }

            string baseline = page.SelectedCardColor;
            Color? picked = await PickColorAsync(
                AppChrome.Get("Dialog_PickCardColor"),
                ColorContrast.ResolveCardColor(
                    string.IsNullOrWhiteSpace(baseline) ? null : baseline),
                onColorChanged: color =>
                    _editSession.PreviewCardColor(ColorContrast.ToHex(color)));
            if (picked is null)
            {
                _editSession.PreviewCardColor(baseline);
            }

            _editSession.ApplyPickedCardColor(picked);
        }

        private async Task PickAvatarColorAsync()
        {
            Profile? profile = _editSession.Profile;
            if (profile is null)
            {
                return;
            }

            Color initial = ColorContrast.TryParseHex(profile.AvatarColor, out Color parsed)
                ? parsed
                : ColorContrast.TryParseHex(LetterAvatar.PickRandomColor(), out Color fallback)
                    ? fallback
                    : Color.FromArgb(255, 79, 107, 237);

            Color? picked = await PickColorAsync(
                AppChrome.Get("Dialog_PickAvatarColor"),
                initial,
                onColorChanged: color =>
                    _editSession.PreviewAvatarColor(ColorContrast.ToHex(color)));
            if (picked is null)
            {
                _editSession.UpdateIconEditorUi();
                return;
            }

            _editSession.ApplyPickedAvatarColor(ColorContrast.ToHex(picked.Value));
        }

        private void ShowMessage(string message, int delay = 10000) =>
            _ = ShowMessageAsync(message, delay);

        private async Task ShowMessageAsync(string message, int delay = 10000)
        {
            _messageCts?.Cancel();
            _messageCts?.Dispose();
            _messageCts = new CancellationTokenSource();
            var token = _messageCts.Token;

            MessageTextBlock.Text = message;
            try
            {
                await Task.Delay(delay, token);
                if (!token.IsCancellationRequested)
                {
                    MessageTextBlock.Text = string.Empty;
                }
            }
            catch (OperationCanceledException)
            {
            }
        }

        private async Task<string?> PickPowerBiExecutablePathAsync()
        {
            var picker = new FileOpenPicker();
            InitializeWithWindow.Initialize(picker, WindowNative.GetWindowHandle(this));
            picker.ViewMode = PickerViewMode.List;
            picker.SuggestedStartLocation = PickerLocationId.ComputerFolder;
            picker.FileTypeFilter.Add(".exe");

            var file = await picker.PickSingleFileAsync();
            return file?.Path;
        }

        private async Task<string?> PickProfilesFolderAsync()
        {
            var picker = new FolderPicker();
            InitializeWithWindow.Initialize(picker, WindowNative.GetWindowHandle(this));
            picker.ViewMode = PickerViewMode.List;
            picker.SuggestedStartLocation = PickerLocationId.ComputerFolder;
            picker.FileTypeFilter.Add("*");

            var folder = await picker.PickSingleFolderAsync();
            return folder?.Path;
        }

        private void OpenFolderInExplorer(string path)
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = path,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                ShowMessage(AppChrome.Format("Toast_OpenFolderFailed", ex.Message), 5000);
            }
        }

        private async Task<Color?> PickColorAsync(string title, Color initial, Action<Color>? onColorChanged = null)
        {
            var picker = new ColorPicker
            {
                ColorSpectrumShape = ColorSpectrumShape.Box,
                IsMoreButtonVisible = true,
                IsColorSliderVisible = true,
                IsColorChannelTextInputVisible = true,
                IsHexInputVisible = true,
                IsAlphaEnabled = false,
                Color = initial
            };
            if (onColorChanged is not null)
            {
                picker.ColorChanged += (_, _) => onColorChanged(picker.Color);
            }

            var dialog = new ContentDialog
            {
                Title = title,
                Content = picker,
                PrimaryButtonText = AppChrome.Get("Dialog_Ok"),
                CloseButtonText = AppChrome.Get("Dialog_Cancel"),
                DefaultButton = ContentDialogButton.Primary,
                XamlRoot = Content.XamlRoot
            };

            ContentDialogResult result = await ShowModalDialogAsync(dialog);
            return result == ContentDialogResult.Primary ? picker.Color : null;
        }

        private static string AppIconFilePath =>
            IOPath.Combine(AppContext.BaseDirectory, "Assets", "pbilaunchericon.ico");

        private void ApplyAppIcon()
        {
            string iconPath = AppIconFilePath;
            if (!System.IO.File.Exists(iconPath))
            {
                return;
            }

            AppIconImage.Source = new BitmapImage(new Uri(iconPath, UriKind.Absolute));
            _appWindow.SetIcon(iconPath);
        }

        private void SetupTrayIcon()
        {
            string iconPath = AppIconFilePath;

            try
            {
                _trayIcon = new TrayIcon(
                    this,
                    AppChrome.Get("MainWindow/Title"),
                    iconPath,
                    GetTrayMenuEntries,
                    LaunchProfileFromTray,
                    ToggleWindowFromTray,
                    ShowOrHideWindowFromTrayMenu,
                    ExitApplication);
            }
            catch
            {
                // Best-effort tray presence.
            }
        }

        private IReadOnlyList<TrayMenuEntry> GetTrayMenuEntries() =>
            AppChrome.WithTrayLabels(
                TrayMenuComposition.Compose(
                    _lifecycle.Profiles.OrderBy(p => p.Order),
                    _lifecycle.Settings.HasCompletedSetup,
                    isWindowVisible: _appWindow.IsVisible));

        private void LaunchProfileFromTray(Guid profileId)
        {
            Profile? profile = _lifecycle.Profiles.FirstOrDefault(p => p.Id == profileId);
            if (profile is null)
            {
                return;
            }

            LaunchProfile(profile, showStatusMessage: false, ensureWindowVisibleOnError: true);
        }

        private void AppWindow_Changed(AppWindow sender, AppWindowChangedEventArgs args)
        {
            if (args.DidSizeChange)
            {
                UpdateTitleBarChromeLayout();
            }

            if (!args.DidPresenterChange)
            {
                return;
            }

            UpdateTitleBarChromeLayout();

            if (sender.Presenter is OverlappedPresenter presenter &&
                presenter.State == OverlappedPresenterState.Minimized &&
                _lifecycle.Settings.MinimizeBehavior == MinimizeButtonBehavior.SendToTray)
            {
                HideWindow();
            }
        }

        private async void AppWindow_Closing(AppWindow sender, AppWindowClosingEventArgs args)
        {
            await _closeGate.HandleClosingAsync(args, _lifecycle.Settings.CloseBehavior);
        }

        private void HideWindow()
        {
            _appWindow.Hide();
        }

        /// <summary>
        /// A second launch of this copy (for example the shortcut while hidden in the tray)
        /// shows and focuses this window instead of starting another launcher.
        /// </summary>
        public void ShowFromRedirectedActivation()
        {
            if (_resourcesCleanedUp)
            {
                return;
            }

            ShowWindow();
        }

        private void ShowWindow()
        {
            _appWindow.Show();
            if (_appWindow.Presenter is OverlappedPresenter presenter)
            {
                presenter.Restore();
            }

            BringWindowToFocus();
        }

        private void BringWindowToFocus()
        {
            _appWindow.MoveInZOrderAtTop();
            Activate();

            IntPtr hwnd = WindowNative.GetWindowHandle(this);
            if (hwnd == IntPtr.Zero)
            {
                return;
            }

            // Tray callbacks are not foreground; AttachThreadInput is the usual Win32 allow-list bypass.
            IntPtr foreground = GetForegroundWindow();
            uint foregroundThread = GetWindowThreadProcessId(foreground, out _);
            uint thisThread = GetCurrentThreadId();
            bool attached = false;
            if (foreground != IntPtr.Zero && foregroundThread != 0 && foregroundThread != thisThread)
            {
                attached = AttachThreadInput(thisThread, foregroundThread, true);
            }

            ShowWindowApi(hwnd, SwRestore);
            BringWindowToTop(hwnd);
            SetForegroundWindow(hwnd);

            if (attached)
            {
                AttachThreadInput(thisThread, foregroundThread, false);
            }
        }

        private void MainWindow_Activated(object sender, WindowActivatedEventArgs args)
        {

            if (args.WindowActivationState == WindowActivationState.Deactivated)
            {
                _windowIsActive = false;
                _lastDeactivatedAt = DateTimeOffset.UtcNow;
                return;
            }

            _windowIsActive = true;
        }

        /// <summary>
        /// Tray left-click: hidden → show; visible but not focused → bring forward; focused → hide.
        /// Tray clicks deactivate the window first, so a short grace period still counts as focused.
        /// </summary>
        private void ToggleWindowFromTray()
        {
            if (!_appWindow.IsVisible)
            {
                ShowWindow();
                return;
            }

            // Only treat as focused when we were active (tray click deactivates us first).
            bool treatAsFocused = _windowIsActive ||
                DateTimeOffset.UtcNow - _lastDeactivatedAt < TimeSpan.FromMilliseconds(400);

            if (treatAsFocused)
            {
                HideWindow();
                return;
            }

            BringWindowToFocus();
        }

        private const int SwRestore = 9;

        [DllImport("user32.dll")]
        private static extern uint GetDpiForWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool BringWindowToTop(IntPtr hWnd);

        [DllImport("user32.dll", EntryPoint = "ShowWindow")]
        private static extern bool ShowWindowApi(IntPtr hWnd, int nCmdShow);

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        [DllImport("user32.dll")]
        private static extern bool AttachThreadInput(uint idAttach, uint idAttachTo, bool fAttach);

        [DllImport("kernel32.dll")]
        private static extern uint GetCurrentThreadId();

        /// <summary>
        /// Tray menu Show/Hide: visibility toggle (label already reflects current state).
        /// </summary>
        private void ShowOrHideWindowFromTrayMenu()
        {
            if (_appWindow.IsVisible)
            {
                HideWindow();
                return;
            }

            ShowWindow();
        }

        private void CleanupResources()
        {
            if (_resourcesCleanedUp)
            {
                return;
            }

            _resourcesCleanedUp = true;
            _appWindow.Closing -= AppWindow_Closing;
            _appWindow.Changed -= AppWindow_Changed;

            _messageCts?.Cancel();
            _messageCts?.Dispose();
            _messageCts = null;

            _updateCheckCts?.Cancel();
            _updateCheckCts?.Dispose();
            _updateCheckCts = null;
            _updateChecker?.Dispose();
            _updateChecker = null;

            _trayIcon?.Dispose();
            _trayIcon = null;
        }

        private void ExitApplication()
        {
            _closeGate.RequestExplicitExit();
            Close();
        }
    }
}
