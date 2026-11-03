using System;
using System.Linq;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Windows.System;

namespace PowerBILauncher;

/// <summary>
/// Raised when an in-place Version rename is committed from the Settings surface.
/// </summary>
public sealed class VersionRenameCommitEventArgs : EventArgs
{
    public required Guid VersionId { get; init; }

    public required string DisplayName { get; init; }
}

/// <summary>
/// Settings / onboarding UI surface. Named elements and events only - no lifecycle or policy.
/// </summary>
public sealed partial class SettingsPanel : UserControl
{
    private bool _checkForUpdatesEligible;
    private string? _checkForUpdatesDisabledTooltip;

    public SettingsPanel()
    {
        InitializeComponent();
    }

    public TextBlock SettingsTitleElement => SettingsTitle;
    public InfoBar SetupVersionsInfoBarElement => SetupVersionsInfoBar;
    public Button ScanVersionsButtonElement => ScanVersionsButton;
    public Button BrowseVersionButtonElement => BrowseVersionButton;
    public ListView VersionsListViewElement => VersionsListView;
    public TextBlock VersionsEmptyTextElement => VersionsEmptyText;
    public TextBox UserProfilePathTextBoxElement => UserProfilePathTextBox;
    public ComboBox MinimizeBehaviorComboBoxElement => MinimizeBehaviorComboBox;
    public ComboBox CloseBehaviorComboBoxElement => CloseBehaviorComboBox;
    public Button CancelSettingsButtonElement => CancelSettingsButton;
    public Border SettingsFooterElement => SettingsFooter;
    public StackPanel SettingsFormPanelElement => SettingsFormPanel;
    public StackPanel ScanStagingPanelElement => ScanStagingPanel;
    public ListView ScanResultsListViewElement => ScanResultsListView;
    public TextBlock ScanEmptyTextElement => ScanEmptyText;
    public InfoBar ScanStoreWarningInfoBarElement => ScanStoreWarningInfoBar;
    public Button ConfirmScanButtonElement => ConfirmScanButton;

    public event EventHandler? SaveRequested;
    public event EventHandler? CancelRequested;
    public event EventHandler? ScanRequested;
    public event EventHandler? BrowseRequested;
    public event EventHandler? FolderBrowseRequested;
    public event EventHandler? FolderOpenRequested;
    public event EventHandler? ConfirmScanRequested;
    public event EventHandler? CancelScanRequested;
    public event EventHandler<Guid>? RenameRequested;
    public event EventHandler<VersionRenameCommitEventArgs>? RenameCommitRequested;
    public event EventHandler<Guid>? RenameCancelRequested;
    public event EventHandler<Guid>? SetDefaultRequested;
    public event EventHandler<Guid>? RemoveRequested;
    public event EventHandler? FormChanged;
    public event EventHandler? CheckForUpdatesRequested;

    public void SetLauncherVersionText(string text) =>
        LauncherVersionTextBlock.Text = text ?? "";

    public void SetLauncherBuildDateText(string? text)
    {
        bool hasText = !string.IsNullOrWhiteSpace(text);
        LauncherBuildDateTextBlock.Text = text ?? "";
        LauncherBuildDateTextBlock.Visibility = hasText ? Visibility.Visible : Visibility.Collapsed;
    }

    public void SetCheckForUpdatesEnabled(bool enabled, string? disabledTooltip)
    {
        _checkForUpdatesEligible = enabled;
        _checkForUpdatesDisabledTooltip = disabledTooltip;
        ApplyCheckForUpdatesButtonState(busy: false);
    }

    public void SetCheckForUpdatesBusy(bool busy) =>
        ApplyCheckForUpdatesButtonState(busy);

    private void ApplyCheckForUpdatesButtonState(bool busy)
    {
        CheckForUpdatesButton.IsEnabled = _checkForUpdatesEligible && !busy;
        ToolTipService.SetToolTip(
            CheckForUpdatesButton,
            _checkForUpdatesEligible ? null : _checkForUpdatesDisabledTooltip);
    }

    public void ShowAboutUpdateCheck(
        InfoBarSeverity severity,
        string message,
        Uri? releaseUrl)
    {
        AboutUpdateCheckInfoBar.Severity = severity;
        AboutUpdateCheckInfoBar.Message = message;
        bool showAction = releaseUrl is not null;
        AboutViewReleaseButton.NavigateUri = releaseUrl;
        AboutUpdateCheckInfoBar.ActionButton = showAction ? AboutViewReleaseButton : null;
        AboutUpdateCheckInfoBar.IsOpen = true;
        AboutUpdateCheckInfoBar.Visibility = Visibility.Visible;
    }

    public void HideAboutUpdateCheck()
    {
        AboutUpdateCheckInfoBar.IsOpen = false;
        AboutUpdateCheckInfoBar.Visibility = Visibility.Collapsed;
        AboutUpdateCheckInfoBar.ActionButton = null;
    }

    public void SetSaveChrome(bool canSave) =>
        SaveSettingsButton.IsEnabled = canSave;

    public void SetProfilesFolderChrome(
        string? issueMessage,
        bool canOpenFolder,
        string openFolderDisabledTooltip,
        bool showRootChangeNote)
    {
        bool invalid = !string.IsNullOrEmpty(issueMessage);
        ProfilesFolderIssueInfoBar.Message = issueMessage ?? "";
        ProfilesFolderIssueInfoBar.IsOpen = invalid;
        ProfilesFolderIssueInfoBar.Visibility = invalid ? Visibility.Visible : Visibility.Collapsed;
        if (invalid)
        {
            UserProfilePathErrorBorder.BorderBrush =
                (Brush)Application.Current.Resources["SystemFillColorCriticalBrush"];
        }
        else
        {
            UserProfilePathErrorBorder.ClearValue(Border.BorderBrushProperty);
        }

        OpenProfilesFolderButton.IsEnabled = canOpenFolder;
        ToolTipService.SetToolTip(
            OpenProfilesFolderTooltipHost,
            canOpenFolder ? null : openFolderDisabledTooltip);

        ProfilesFolderRootChangeNote.Visibility =
            showRootChangeNote ? Visibility.Visible : Visibility.Collapsed;
    }

    private void SettingsForm_TextChanged(object sender, TextChangedEventArgs e) =>
        FormChanged?.Invoke(this, EventArgs.Empty);

    private void SettingsForm_SelectionChanged(object sender, SelectionChangedEventArgs e) =>
        FormChanged?.Invoke(this, EventArgs.Empty);

    private void SaveSettingsButton_Click(object sender, RoutedEventArgs e) =>
        SaveRequested?.Invoke(this, EventArgs.Empty);

    private void CancelSettingsButton_Click(object sender, RoutedEventArgs e) =>
        CancelRequested?.Invoke(this, EventArgs.Empty);

    private void ScanVersionsButton_Click(object sender, RoutedEventArgs e) =>
        ScanRequested?.Invoke(this, EventArgs.Empty);

    private void BrowseVersionButton_Click(object sender, RoutedEventArgs e) =>
        BrowseRequested?.Invoke(this, EventArgs.Empty);

    private void BrowseProfilesFolderButton_Click(object sender, RoutedEventArgs e) =>
        FolderBrowseRequested?.Invoke(this, EventArgs.Empty);

    private void OpenProfilesFolderButton_Click(object sender, RoutedEventArgs e) =>
        FolderOpenRequested?.Invoke(this, EventArgs.Empty);

    private void ConfirmScanButton_Click(object sender, RoutedEventArgs e) =>
        ConfirmScanRequested?.Invoke(this, EventArgs.Empty);

    private void CancelScanButton_Click(object sender, RoutedEventArgs e) =>
        CancelScanRequested?.Invoke(this, EventArgs.Empty);

    private void CheckForUpdatesButton_Click(object sender, RoutedEventArgs e) =>
        CheckForUpdatesRequested?.Invoke(this, EventArgs.Empty);

    private void VersionRenameButton_Click(object sender, RoutedEventArgs e)
    {
        if (TryGetVersionId(sender, out Guid id))
        {
            RenameRequested?.Invoke(this, id);
        }
    }

    private void VersionRenameDoneButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button button || !TryGetVersionId(button, out Guid id))
        {
            return;
        }

        if (button.Parent is Panel panel)
        {
            TextBox? box = panel.Children.OfType<TextBox>().FirstOrDefault();
            RaiseRenameCommit(id, box?.Text);
        }
    }

    private void VersionRenameBox_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (sender is not TextBox box || !TryGetVersionId(box, out Guid id))
        {
            return;
        }

        if (e.Key == VirtualKey.Enter)
        {
            RaiseRenameCommit(id, box.Text);
            e.Handled = true;
        }
        else if (e.Key == VirtualKey.Escape)
        {
            RenameCancelRequested?.Invoke(this, id);
            e.Handled = true;
        }
    }

    private void VersionRenameBox_Loaded(object sender, RoutedEventArgs e)
    {
        if (sender is TextBox { Visibility: Visibility.Visible } box)
        {
            box.Focus(FocusState.Programmatic);
            box.SelectAll();
        }
    }

    private void VersionsListView_ContainerContentChanging(ListViewBase sender, ContainerContentChangingEventArgs args)
    {
        if (args.Item is VersionRegistryRow row && args.ItemContainer is not null)
        {
            AutomationProperties.SetName(args.ItemContainer, row.AutomationName);
        }
    }

    private void VersionSetDefaultMenu_Click(object sender, RoutedEventArgs e)
    {
        if (TryGetVersionId(sender, out Guid id))
        {
            SetDefaultRequested?.Invoke(this, id);
        }
    }

    private void VersionRemoveMenu_Click(object sender, RoutedEventArgs e)
    {
        if (TryGetVersionId(sender, out Guid id))
        {
            RemoveRequested?.Invoke(this, id);
        }
    }

    private void RaiseRenameCommit(Guid id, string? displayName) =>
        RenameCommitRequested?.Invoke(this, new VersionRenameCommitEventArgs
        {
            VersionId = id,
            DisplayName = displayName ?? ""
        });

    private static bool TryGetVersionId(object sender, out Guid id)
    {
        if (sender is FrameworkElement element)
        {
            switch (element.Tag)
            {
                case Guid tagged:
                    id = tagged;
                    return true;
                case string text when Guid.TryParse(text, out id):
                    return true;
            }
        }

        id = default;
        return false;
    }
}
