using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using Microsoft.UI.Xaml.Shapes;
using Windows.System;

namespace PowerBILauncher;

public sealed partial class EditProfilePage : Page
{
    private bool _editingName;
    private string _nameEditBaseline = "";
    private string _selectedCardColor = "";
    private bool _removePhotoPointerDanger;
    private bool _removePhotoKeyboardFocused;
    private bool _suppressVersionEvents;
    private bool _suppressIdentityEvents;
    private bool _passwordSaved;
    private bool _replacingPassword;
    private bool _clearedIdentity;

    public EditProfilePage()
    {
        InitializeComponent();
        // Match TitleTextBlockStyle on NameDisplay (avoid raw FontSize on the TextBox).
        NameEditBox.FontSize = NameDisplay.FontSize;
        NameEditBox.FontWeight = NameDisplay.FontWeight;
        UpdateNoAccentVisual();
    }

    public Grid AvatarPreviewHostElement => AvatarPreviewHost;
    public Ellipse IconPreviewEllipseElement => IconPreviewEllipse;
    public TextBlock IconPreviewLetterElement => IconPreviewLetter;
    public ToggleSwitch ShowOnTrayToggleElement => ShowOnTrayToggle;
    public Border CardColorPreviewBorderElement => CardColorPreviewBorder;

    /// <summary>Hex card color, or empty for the default untinted card.</summary>
    public string SelectedCardColor => _selectedCardColor;

    /// <summary>Current Profile name (display or in-progress edit).</summary>
    public string ProfileName =>
        _editingName ? NameEditBox.Text : NameDisplay.Text;

    public event EventHandler? SaveRequested;
    public event EventHandler? DiscardRequested;
    public event EventHandler? DeleteRequested;
    public event EventHandler? ChooseLogoRequested;
    public event EventHandler? ClearLogoRequested;
    public event EventHandler? OpenFolderRequested;
    public event EventHandler? AvatarColorRequested;
    public event EventHandler? PickCardColorRequested;
    public event EventHandler? ResetCardColorRequested;
    public event EventHandler? ProfileNameChanged;
    public event EventHandler? EditStateChanged;
    public event EventHandler? OpenSettingsRequested;
    public event EventHandler? VersionSelectionChanged;

    /// <summary>Null follows the Default Version.</summary>
    public Guid? SelectedVersionId =>
        VersionComboBox.SelectedItem is VersionChoiceOption option && !option.IsFollowDefault
            ? option.VersionId
            : null;

    public bool IdentityEnabled => IdentityToggle.IsOn;

    public string IdentityText => IdentityTextBox.Text ?? "";

    public string IdentityPassword => IdentityPasswordBox.Password ?? "";

    public bool PendingPasswordAction =>
        _clearedIdentity
        || _replacingPassword
        || IdentityPassword.Length > 0;

    protected override void OnNavigatedFrom(NavigationEventArgs e)
    {
        EndNameEdit(commit: false);
        base.OnNavigatedFrom(e);
    }

    public void SetProfileName(string name)
    {
        EndNameEdit(commit: false);
        NameDisplay.Text = name ?? "";
        NameEditBox.Text = NameDisplay.Text;
    }

    public void SetPhotoAffordances(bool hasCustomPhoto)
    {
        RemovePhotoButton.Visibility = hasCustomPhoto ? Visibility.Visible : Visibility.Collapsed;
        AvatarColorPanel.Visibility = hasCustomPhoto ? Visibility.Collapsed : Visibility.Visible;
        ChangePhotoLabel.Text = AppChrome.PhotoAction(hasCustomPhoto);
    }

    public void SetVersionChoices(
        IReadOnlyList<VersionChoiceOption> options,
        Guid? selectedVersionId,
        VersionSelectionKind kind)
    {
        ArgumentNullException.ThrowIfNull(options);
        _suppressVersionEvents = true;
        try
        {
            IReadOnlyList<VersionChoiceOption> display = options
                .Select(option => new VersionChoiceOption
                {
                    VersionId = option.VersionId,
                    Label = AppChrome.VersionChoiceLabel(option),
                    IsFollowDefault = option.IsFollowDefault,
                    IsPlaceholder = option.IsPlaceholder,
                    Kind = option.Kind
                })
                .ToList();
            VersionComboBox.ItemsSource = display;
            VersionComboBox.IsEnabled = kind != VersionSelectionKind.EmptyRegistry;

            VersionChoiceOption? selected = kind == VersionSelectionKind.FollowDefault
                ? display.FirstOrDefault(option => option.IsFollowDefault)
                : display.FirstOrDefault(option => option.VersionId == selectedVersionId)
                    ?? display.FirstOrDefault(option => option.IsFollowDefault);
            VersionComboBox.SelectedItem = selected;

            bool empty = kind == VersionSelectionKind.EmptyRegistry;
            EmptyVersionInfoBar.IsOpen = empty;
            EmptyVersionInfoBar.Visibility = empty ? Visibility.Visible : Visibility.Collapsed;

            bool dangling = kind == VersionSelectionKind.Dangling;
            DanglingVersionInfoBar.IsOpen = dangling;
            DanglingVersionInfoBar.Visibility = dangling ? Visibility.Visible : Visibility.Collapsed;
        }
        finally
        {
            _suppressVersionEvents = false;
        }
    }

    public void SetIdentity(bool enabled, string? identityText, bool passwordSaved, bool passwordUnavailable)
    {
        _suppressIdentityEvents = true;
        try
        {
            IdentityToggle.IsOn = enabled;
            IdentityTextBox.Text = identityText ?? "";
            IdentityPasswordBox.Password = "";
            _passwordSaved = passwordSaved;
            _replacingPassword = false;
            _clearedIdentity = false;
            RefreshIdentityChrome(passwordUnavailable);
        }
        finally
        {
            _suppressIdentityEvents = false;
        }
    }

    public void SetCardColorMode(string? cardColor)
    {
        _selectedCardColor = EditProfileDirtyState.IsDefaultCardColor(cardColor)
            ? ""
            : cardColor!.Trim();
        UpdateNoAccentVisual();
    }

    /// <summary>Commit in-progress name edit and raise <see cref="SaveRequested"/>.</summary>
    public void RequestSave()
    {
        EndNameEdit(commit: true);
        SaveRequested?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Sync Save enablement and unsaved indicator on the pinned footer.</summary>
    public void SetSaveChrome(bool dirty, bool canSave)
    {
        SaveButton.IsEnabled = canSave;
        UnsavedPanel.Visibility = dirty ? Visibility.Visible : Visibility.Collapsed;
    }

    public void SetDraftMode(bool isDraft) =>
        DraftExtrasPanel.Visibility = isDraft ? Visibility.Collapsed : Visibility.Visible;

    public void SetNameIssue(string? message)
    {
        bool invalid = !string.IsNullOrEmpty(message);
        ProfileNameIssueInfoBar.Message = message ?? "";
        ProfileNameIssueInfoBar.IsOpen = invalid;
        ProfileNameIssueInfoBar.Visibility = invalid ? Visibility.Visible : Visibility.Collapsed;
    }

    /// <summary>Switch the name field into edit mode and focus it.</summary>
    public void StartNameEdit() =>
        BeginNameEdit();

    public void NotifyEditStateChanged() =>
        EditStateChanged?.Invoke(this, EventArgs.Empty);

    private void SaveButton_Click(object sender, RoutedEventArgs e) =>
        RequestSave();

    private void DiscardButton_Click(object sender, RoutedEventArgs e) =>
        DiscardRequested?.Invoke(this, EventArgs.Empty);

    private void RenameButton_Click(object sender, RoutedEventArgs e) =>
        BeginNameEdit();

    private void ConfirmNameButton_Click(object sender, RoutedEventArgs e) =>
        EndNameEdit(commit: true);

    private void CancelNameButton_Click(object sender, RoutedEventArgs e) =>
        EndNameEdit(commit: false);

    private void BeginNameEdit()
    {
        if (_editingName)
        {
            return;
        }

        _editingName = true;
        _nameEditBaseline = NameDisplay.Text;
        NameEditBox.Text = NameDisplay.Text;
        NameHitButton.Visibility = Visibility.Collapsed;
        RenameButton.Visibility = Visibility.Collapsed;
        NameEditActions.Visibility = Visibility.Visible;
        NameEditBox.Visibility = Visibility.Visible;
        NameEditBox.Focus(FocusState.Programmatic);
        NameEditBox.SelectAll();
    }

    private void EndNameEdit(bool commit)
    {
        if (!_editingName)
        {
            return;
        }

        _editingName = false;
        if (commit)
        {
            NameDisplay.Text = NameEditBox.Text;
            ProfileNameChanged?.Invoke(this, EventArgs.Empty);
            EditStateChanged?.Invoke(this, EventArgs.Empty);
        }
        else
        {
            NameEditBox.Text = _nameEditBaseline;
            NameDisplay.Text = _nameEditBaseline;
        }

        NameEditBox.Visibility = Visibility.Collapsed;
        NameEditActions.Visibility = Visibility.Collapsed;
        NameHitButton.Visibility = Visibility.Visible;
        RenameButton.Visibility = Visibility.Visible;
    }

    private void NameEditBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_editingName)
        {
            EditStateChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    private void NameEditBox_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == VirtualKey.Enter)
        {
            EndNameEdit(commit: true);
            e.Handled = true;
        }
        else if (e.Key == VirtualKey.Escape)
        {
            EndNameEdit(commit: false);
            e.Handled = true;
        }
    }

    private void NameEditBox_LostFocus(object sender, RoutedEventArgs e)
    {
        if (!_editingName)
        {
            return;
        }

        // Defer: clicking Done/Discard focuses those buttons after LostFocus; commit only if focus left the edit chrome.
        DispatcherQueue.TryEnqueue(() =>
        {
            if (!_editingName)
            {
                return;
            }

            object? focused = FocusManager.GetFocusedElement(XamlRoot);
            if (ReferenceEquals(focused, ConfirmNameButton)
                || ReferenceEquals(focused, CancelNameButton)
                || ReferenceEquals(focused, NameEditBox))
            {
                return;
            }

            EndNameEdit(commit: true);
        });
    }

    private void ChoosePhotoButton_Click(object sender, RoutedEventArgs e) =>
        ChooseLogoRequested?.Invoke(this, EventArgs.Empty);

    private void RemovePhotoButton_Click(object sender, RoutedEventArgs e) =>
        ClearLogoRequested?.Invoke(this, EventArgs.Empty);

    private void OpenFolderButton_Click(object sender, RoutedEventArgs e) =>
        OpenFolderRequested?.Invoke(this, EventArgs.Empty);

    private void DeleteProfileButton_Click(object sender, RoutedEventArgs e) =>
        DeleteRequested?.Invoke(this, EventArgs.Empty);

    private void PickAvatarColorButton_Click(object sender, RoutedEventArgs e) =>
        AvatarColorRequested?.Invoke(this, EventArgs.Empty);

    private void PickCardColorButton_Click(object sender, RoutedEventArgs e) =>
        PickCardColorRequested?.Invoke(this, EventArgs.Empty);

    private void NoAccentButton_Click(object sender, RoutedEventArgs e)
    {
        SetCardColorMode("");
        ResetCardColorRequested?.Invoke(this, EventArgs.Empty);
        EditStateChanged?.Invoke(this, EventArgs.Empty);
    }

    private void VersionComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_suppressVersionEvents)
        {
            return;
        }

        EditStateChanged?.Invoke(this, EventArgs.Empty);
        VersionSelectionChanged?.Invoke(this, EventArgs.Empty);
    }

    private void OpenSettingsButton_Click(object sender, RoutedEventArgs e) =>
        OpenSettingsRequested?.Invoke(this, EventArgs.Empty);

    private void IdentityToggle_Toggled(object sender, RoutedEventArgs e)
    {
        if (!_suppressIdentityEvents && !IdentityToggle.IsOn)
        {
            _clearedIdentity = _passwordSaved || _clearedIdentity;
            _passwordSaved = false;
            _replacingPassword = false;
        }

        RefreshIdentityChrome(passwordUnavailable: false);
        if (!_suppressIdentityEvents)
        {
            EditStateChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    private void IdentityFields_Changed(object sender, RoutedEventArgs e)
    {
        if (_suppressIdentityEvents)
        {
            return;
        }

        EditStateChanged?.Invoke(this, EventArgs.Empty);
    }

    private void IdentityTextBox_TextChanged(object sender, TextChangedEventArgs e) =>
        IdentityFields_Changed(sender, e);

    private void ReplacePasswordButton_Click(object sender, RoutedEventArgs e)
    {
        _replacingPassword = true;
        RefreshIdentityChrome(passwordUnavailable: false);
        EditStateChanged?.Invoke(this, EventArgs.Empty);
    }

    private void CancelReplaceButton_Click(object sender, RoutedEventArgs e)
    {
        _replacingPassword = false;
        IdentityPasswordBox.Password = "";
        RefreshIdentityChrome(passwordUnavailable: false);
        EditStateChanged?.Invoke(this, EventArgs.Empty);
    }

    private void ClearIdentityButton_Click(object sender, RoutedEventArgs e)
    {
        IdentityToggle.IsOn = false;
    }

    private void RefreshIdentityChrome(bool passwordUnavailable)
    {
        bool on = IdentityToggle.IsOn;
        IdentityFieldsPanel.Visibility = on ? Visibility.Visible : Visibility.Collapsed;
        bool showSaved = on && _passwordSaved && !_replacingPassword && !passwordUnavailable;
        PasswordSavedPanel.Visibility = showSaved ? Visibility.Visible : Visibility.Collapsed;
        IdentityPasswordBox.Visibility = on && !showSaved ? Visibility.Visible : Visibility.Collapsed;
        IdentityPasswordHelp.Visibility = IdentityPasswordBox.Visibility;
        IdentityPasswordBox.Header = AppChrome.IdentityPasswordHeader(_replacingPassword);
        CancelReplaceButton.Visibility = on && _replacingPassword ? Visibility.Visible : Visibility.Collapsed;
        PasswordUnavailableInfoBar.IsOpen = passwordUnavailable;
        PasswordUnavailableInfoBar.Visibility = passwordUnavailable ? Visibility.Visible : Visibility.Collapsed;
        if (passwordUnavailable)
        {
            IdentityPasswordHelp.Text = AppChrome.IdentityPasswordHelp(passwordUnavailable: true);
        }
        else
        {
            IdentityPasswordHelp.Text = AppChrome.IdentityPasswordHelp(passwordUnavailable: false);
        }
    }

    private void ShowOnTrayToggle_Toggled(object sender, RoutedEventArgs e)
    {
        EditStateChanged?.Invoke(this, EventArgs.Empty);
    }

    private void RemovePhotoButton_PointerEntered(object sender, PointerRoutedEventArgs e)
    {
        _removePhotoPointerDanger = true;
        UpdateRemovePhotoDangerForeground();
    }

    private void RemovePhotoButton_PointerExited(object sender, PointerRoutedEventArgs e)
    {
        _removePhotoPointerDanger = false;
        UpdateRemovePhotoDangerForeground();
    }

    private void RemovePhotoButton_PointerCaptureLost(object sender, PointerRoutedEventArgs e)
    {
        _removePhotoPointerDanger = false;
        UpdateRemovePhotoDangerForeground();
    }

    private void RemovePhotoButton_GotFocus(object sender, RoutedEventArgs e)
    {
        _removePhotoKeyboardFocused = true;
        UpdateRemovePhotoDangerForeground();
    }

    private void RemovePhotoButton_LostFocus(object sender, RoutedEventArgs e)
    {
        _removePhotoKeyboardFocused = false;
        UpdateRemovePhotoDangerForeground();
    }

    private void UpdateRemovePhotoDangerForeground() =>
        SetRemovePhotoDangerForeground(_removePhotoPointerDanger || _removePhotoKeyboardFocused);

    /// <summary>
    /// Tint icon + label only - leave Button Background to the default VSM so hover exit does not flicker.
    /// </summary>
    private void SetRemovePhotoDangerForeground(bool danger)
    {
        if (danger)
        {
            var brush = (Brush)Application.Current.Resources["SystemFillColorCriticalBrush"];
            RemovePhotoIcon.Foreground = brush;
            RemovePhotoLabel.Foreground = brush;
        }
        else
        {
            RemovePhotoIcon.ClearValue(IconElement.ForegroundProperty);
            RemovePhotoLabel.ClearValue(TextBlock.ForegroundProperty);
        }
    }

    private void UpdateNoAccentVisual()
    {
        NoAccentButton.Visibility = string.IsNullOrWhiteSpace(_selectedCardColor)
            ? Visibility.Collapsed
            : Visibility.Visible;
    }
}
