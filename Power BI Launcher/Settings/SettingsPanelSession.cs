using System.IO;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace PowerBILauncher;

/// <summary>
/// Shell callbacks for Settings operations that need window/UI ownership.
/// </summary>
public sealed class SettingsPanelSessionHost
{
    public required Action<string, int> ShowMessage { get; init; }

    public required Func<Task<string?>> PickPowerBiExecutablePathAsync { get; init; }

    public required Func<Task<string?>> PickProfilesFolderAsync { get; init; }

    public required Action<string> OpenProfilesFolder { get; init; }
}

/// <summary>
/// Settings / onboarding session coordinator: attach/detach surface events, reset from saved
/// settings, Version registry list projection, Scan staging, Version Browse, folder Browse / Open,
/// and Save / Cancel. Shell keeps <see cref="UiMode"/> visibility, HWND pickers, and dialogs.
/// </summary>
public sealed class SettingsPanelSession
{
    private readonly ProfileLifecycle _lifecycle;
    private readonly SettingsPanelSessionHost _host;
    private readonly VersionRegistryDraft _draft;
    private SettingsPanel? _panel;
    private bool _suppressFormEvents;
    private List<ScanCandidateRow> _scanRows = new();

    public SettingsPanelSession(ProfileLifecycle lifecycle, SettingsPanelSessionHost host)
    {
        _lifecycle = lifecycle ?? throw new ArgumentNullException(nameof(lifecycle));
        _host = host ?? throw new ArgumentNullException(nameof(host));
        _draft = new VersionRegistryDraft(lifecycle);
    }

    public event EventHandler? SettingsSaved;

    public event EventHandler? SettingsCancelled;

    public void Attach(SettingsPanel panel)
    {
        ArgumentNullException.ThrowIfNull(panel);
        Detach();
        _panel = panel;
        panel.SaveRequested += OnSaveRequested;
        panel.CancelRequested += OnCancelRequested;
        panel.ScanRequested += OnScanRequested;
        panel.BrowseRequested += OnBrowseRequested;
        panel.FolderBrowseRequested += OnFolderBrowseRequested;
        panel.FolderOpenRequested += OnFolderOpenRequested;
        panel.ConfirmScanRequested += OnConfirmScanRequested;
        panel.CancelScanRequested += OnCancelScanRequested;
        panel.RenameRequested += OnRenameRequested;
        panel.RenameCommitRequested += OnRenameCommitRequested;
        panel.RenameCancelRequested += OnRenameCancelRequested;
        panel.SetDefaultRequested += OnSetDefaultRequested;
        panel.RemoveRequested += OnRemoveRequested;
        panel.FormChanged += OnFormChanged;
    }

    public void Detach()
    {
        if (_panel is null)
        {
            return;
        }

        _panel.SaveRequested -= OnSaveRequested;
        _panel.CancelRequested -= OnCancelRequested;
        _panel.ScanRequested -= OnScanRequested;
        _panel.BrowseRequested -= OnBrowseRequested;
        _panel.FolderBrowseRequested -= OnFolderBrowseRequested;
        _panel.FolderOpenRequested -= OnFolderOpenRequested;
        _panel.ConfirmScanRequested -= OnConfirmScanRequested;
        _panel.CancelScanRequested -= OnCancelScanRequested;
        _panel.RenameRequested -= OnRenameRequested;
        _panel.RenameCommitRequested -= OnRenameCommitRequested;
        _panel.RenameCancelRequested -= OnRenameCancelRequested;
        _panel.SetDefaultRequested -= OnSetDefaultRequested;
        _panel.RemoveRequested -= OnRemoveRequested;
        _panel.FormChanged -= OnFormChanged;
        _panel = null;
    }

    public void Reset(bool onboarding)
    {
        _draft.Reset();
        if (_panel is null)
        {
            return;
        }

        _suppressFormEvents = true;
        try
        {
            CloseScanStaging();
            _panel.UserProfilePathTextBoxElement.Text = _lifecycle.Settings.UserProfilePath;
            SelectComboTag(_panel.MinimizeBehaviorComboBoxElement, _lifecycle.Settings.MinimizeBehavior.ToString());
            SelectComboTag(_panel.CloseBehaviorComboBoxElement, _lifecycle.Settings.CloseBehavior.ToString());
            _panel.SettingsTitleElement.Text = AppChrome.SettingsTitle(onboarding);
            _panel.CancelSettingsButtonElement.Visibility =
                onboarding || !_lifecycle.Settings.HasCompletedSetup
                    ? Visibility.Collapsed
                    : Visibility.Visible;
            RefreshVersionList();
        }
        finally
        {
            _suppressFormEvents = false;
        }

        UpdateSaveChrome();
    }

    private void OnSaveRequested(object? sender, EventArgs e)
    {
        if (_panel is null)
        {
            return;
        }

        string userProfilePath = _panel.UserProfilePathTextBoxElement.Text?.Trim() ?? "";
        ReadBehaviors(out MinimizeButtonBehavior minimizeBehavior, out CloseButtonBehavior closeBehavior);

        IReadOnlyList<SettingsIssue> issues = _draft.Commit(userProfilePath, minimizeBehavior, closeBehavior);
        if (issues.Count > 0)
        {
            _host.ShowMessage(string.Join(" ", issues.Select(AppChrome.SettingsIssue)), 10000);
            return;
        }

        _host.ShowMessage(AppChrome.Get("Toast_Saved"), 2000);
        SettingsSaved?.Invoke(this, EventArgs.Empty);
    }

    private void OnCancelRequested(object? sender, EventArgs e)
    {
        Reset(onboarding: false);
        SettingsCancelled?.Invoke(this, EventArgs.Empty);
    }

    private void OnScanRequested(object? sender, EventArgs e)
    {
        if (_panel is null)
        {
            return;
        }

        IReadOnlyList<ScanAcceptedVersion> found = _draft.BeginScan();
        _scanRows = found
            .Select(item => new ScanCandidateRow(item, isSelected: true))
            .ToList();
        _panel.ScanResultsListViewElement.ItemsSource = _scanRows;
        _panel.ScanEmptyTextElement.Visibility = _scanRows.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        _panel.ScanStoreWarningInfoBarElement.Visibility =
            _scanRows.Any(row => !row.IsSupported) ? Visibility.Visible : Visibility.Collapsed;
        _panel.ConfirmScanButtonElement.IsEnabled = _scanRows.Count > 0;
        _panel.SettingsFormPanelElement.Visibility = Visibility.Collapsed;
        _panel.ScanStagingPanelElement.Visibility = Visibility.Visible;
        _panel.SettingsFooterElement.Visibility = Visibility.Collapsed;
    }

    private void OnConfirmScanRequested(object? sender, EventArgs e)
    {
        var selected = _scanRows
            .Where(row => row.IsSelected)
            .Select(row => row.Path)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        _draft.ConfirmScan(selected);
        CloseScanStaging();
        RefreshVersionList();
    }

    private void OnCancelScanRequested(object? sender, EventArgs e)
    {
        _draft.CancelScan();
        CloseScanStaging();
        RefreshVersionList();
    }

    private async void OnBrowseRequested(object? sender, EventArgs e)
    {
        string? path = await _host.PickPowerBiExecutablePathAsync();
        if (path is null)
        {
            return;
        }

        if (!_draft.TryRegister(path, out VersionRegistryErrorKind? errorKind))
        {
            _host.ShowMessage(
                AppChrome.VersionRegistry(errorKind ?? VersionRegistryErrorKind.NotPowerBiDesktop),
                10000);
            return;
        }

        RefreshVersionList();
    }

    private async void OnFolderBrowseRequested(object? sender, EventArgs e)
    {
        string? path = await _host.PickProfilesFolderAsync();
        if (path is null || _panel is null)
        {
            return;
        }

        _panel.UserProfilePathTextBoxElement.Text = path;
    }

    private void OnFolderOpenRequested(object? sender, EventArgs e)
    {
        if (_panel is null)
        {
            return;
        }

        string path = _panel.UserProfilePathTextBoxElement.Text?.Trim() ?? "";
        if (!Directory.Exists(path))
        {
            return;
        }

        _host.OpenProfilesFolder(path);
    }

    private void OnRenameRequested(object? sender, Guid id)
    {
        _draft.BeginRename(id);
        RefreshVersionList();
    }

    private void OnRenameCommitRequested(object? sender, VersionRenameCommitEventArgs e)
    {
        if (!_draft.TryCommitRename(e.VersionId, e.DisplayName, out VersionRegistryErrorKind? errorKind))
        {
            _host.ShowMessage(
                AppChrome.VersionRegistry(errorKind ?? VersionRegistryErrorKind.NameEmpty),
                10000);
            return;
        }

        RefreshVersionList();
    }

    private void OnRenameCancelRequested(object? sender, Guid _)
    {
        _draft.CancelRename();
        RefreshVersionList();
    }

    private void OnSetDefaultRequested(object? sender, Guid id)
    {
        if (!_draft.TrySetDefault(id, out VersionRegistryErrorKind? errorKind))
        {
            _host.ShowMessage(
                AppChrome.VersionRegistry(errorKind ?? VersionRegistryErrorKind.NotRegistered),
                10000);
            return;
        }

        RefreshVersionList();
    }

    private void OnRemoveRequested(object? sender, Guid id)
    {
        if (!_draft.TryRemove(id, out VersionRegistryErrorKind? errorKind))
        {
            _host.ShowMessage(
                AppChrome.VersionRegistry(errorKind ?? VersionRegistryErrorKind.NotRegistered),
                10000);
            return;
        }

        RefreshVersionList();
    }

    private void CloseScanStaging()
    {
        if (_panel is null)
        {
            return;
        }

        _panel.ScanStagingPanelElement.Visibility = Visibility.Collapsed;
        _panel.SettingsFormPanelElement.Visibility = Visibility.Visible;
        _panel.SettingsFooterElement.Visibility = Visibility.Visible;
        _scanRows = new List<ScanCandidateRow>();
        _panel.ScanResultsListViewElement.ItemsSource = null;
    }

    private void RefreshVersionList()
    {
        if (_panel is null)
        {
            return;
        }

        bool empty = _draft.Versions.Count == 0;
        _panel.VersionsEmptyTextElement.Visibility = empty ? Visibility.Visible : Visibility.Collapsed;
        _panel.VersionsListViewElement.Visibility = empty ? Visibility.Collapsed : Visibility.Visible;
        _panel.VersionsListViewElement.ItemsSource = _draft.ProjectRows();

        bool needsDefault = _draft.NeedsDefaultVersion;
        _panel.SetupVersionsInfoBarElement.Visibility =
            !_lifecycle.Settings.HasCompletedSetup && needsDefault
                ? Visibility.Visible
                : Visibility.Collapsed;
        _panel.ScanVersionsButtonElement.Style = needsDefault
            ? (Style)Application.Current.Resources["AccentButtonStyle"]
            : null;
        _panel.BrowseVersionButtonElement.Style = null;

        if (!_suppressFormEvents)
        {
            UpdateSaveChrome();
        }
    }

    private void OnFormChanged(object? sender, EventArgs e)
    {
        if (_suppressFormEvents)
        {
            return;
        }

        UpdateSaveChrome();
    }

    private void UpdateSaveChrome()
    {
        if (_panel is null)
        {
            return;
        }

        ReadBehaviors(out MinimizeButtonBehavior minimize, out CloseButtonBehavior close);
        string folder = _panel.UserProfilePathTextBoxElement.Text ?? "";
        SettingsIssue? pathIssue = SettingsValidation.TryValidateProfilesFolder(folder, _draft.Versions);
        bool dirty = SettingsDirtyState.IsDirty(
            _lifecycle.Settings,
            _draft.Versions,
            _draft.DefaultVersionId,
            folder,
            minimize,
            close);
        _panel.SetSaveChrome(
            SettingsDirtyState.CanEnableFooterSave(
                dirty,
                _lifecycle.Settings.HasCompletedSetup,
                pathIssue));
        _panel.SetProfilesFolderChrome(
            pathIssue is null ? null : AppChrome.SettingsIssue(pathIssue),
            canOpenFolder: Directory.Exists(folder.Trim()),
            openFolderDisabledTooltip: AppChrome.Get("ProfilesFolderOpenDisabledTooltip"),
            showRootChangeNote: SettingsDirtyState.ShouldShowProfilesRootChangeNote(
                _lifecycle.Settings.UserProfilePath,
                folder,
                _lifecycle.Profiles.Count));
    }

    private void ReadBehaviors(out MinimizeButtonBehavior minimizeBehavior, out CloseButtonBehavior closeBehavior)
    {
        minimizeBehavior = _lifecycle.Settings.MinimizeBehavior;
        closeBehavior = _lifecycle.Settings.CloseBehavior;
        if (_panel is null)
        {
            return;
        }

        if (_panel.MinimizeBehaviorComboBoxElement.SelectedItem is ComboBoxItem minimizeItem &&
            SettingsValidation.TryParseMinimizeBehavior(minimizeItem.Tag?.ToString(), out MinimizeButtonBehavior parsedMinimize))
        {
            minimizeBehavior = parsedMinimize;
        }

        if (_panel.CloseBehaviorComboBoxElement.SelectedItem is ComboBoxItem closeItem &&
            SettingsValidation.TryParseCloseBehavior(closeItem.Tag?.ToString(), out CloseButtonBehavior parsedClose))
        {
            closeBehavior = parsedClose;
        }
    }

    private static void SelectComboTag(ComboBox comboBox, string tag)
    {
        foreach (var item in comboBox.Items.OfType<ComboBoxItem>())
        {
            if (string.Equals(item.Tag?.ToString(), tag, StringComparison.OrdinalIgnoreCase))
            {
                comboBox.SelectedItem = item;
                return;
            }
        }
    }
}
