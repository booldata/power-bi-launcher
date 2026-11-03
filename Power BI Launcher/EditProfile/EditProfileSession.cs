using System;
using System.Threading.Tasks;
using Microsoft.UI.Xaml.Media;
using Windows.UI;

namespace PowerBILauncher;

/// <summary>
/// Shell callbacks for Edit Profile session operations that need window/UI ownership.
/// </summary>
public sealed class EditProfileSessionHost
{
    public required Action<string, int> ShowMessage { get; init; }

    public required Func<string, Task> ShowErrorAsync { get; init; }

    public required Func<Task<string?>> PickAvatarImagePathAsync { get; init; }

    public required Func<Task<bool>> ConfirmDiscardAsync { get; init; }

    public required Func<string, Task<bool>> ConfirmDeleteAsync { get; init; }
}

/// <summary>
/// Raised when an Edit Profile session ends (save, discard, or delete).
/// </summary>
public sealed class EditProfileSessionEndedEventArgs : EventArgs
{
    public required bool NavigateToBoard { get; init; }
}

/// <summary>
/// Edit Profile session coordinator: open/bind/snapshot, attach/detach page events,
/// live preview, dirty/save chrome, Save, Discard, and Delete lifecycle.
/// Shell keeps Drill navigation and platform dialogs / HWND pickers.
/// </summary>
public sealed class EditProfileSession
{
    private readonly ProfileLifecycle _lifecycle;
    private readonly EditProfileSessionHost _host;
    private EditProfilePage? _page;
    private Profile? _profile;
    private Profile? _snapshot;
    private string? _draftAvatarColor;
    private bool _bindingForm;

    public EditProfileSession(ProfileLifecycle lifecycle, EditProfileSessionHost host)
    {
        _lifecycle = lifecycle ?? throw new ArgumentNullException(nameof(lifecycle));
        _host = host ?? throw new ArgumentNullException(nameof(host));
    }

    /// <summary>True while a Profile is open for editing (before or after page attach).</summary>
    public bool IsEditing => _profile is not null;

    /// <summary>True while Add Profile opened an unsaved in-memory draft.</summary>
    public bool IsDraft => _profile is not null && _snapshot is null;

    public Profile? Profile => _profile;

    public EditProfilePage? Page => _page;

    public event EventHandler? OpenFolderRequested;

    public event EventHandler? AvatarColorRequested;

    public event EventHandler? PickCardColorRequested;

    public event EventHandler? OpenSettingsRequested;

    public event EventHandler<EditProfileSessionEndedEventArgs>? SessionEnded;

    public void Begin(Profile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);
        _profile = profile;
        _snapshot = CloneProfile(profile);
        _draftAvatarColor = null;
    }

    /// <summary>
    /// Opens Edit Profile as an unsaved draft. Nothing is persisted until Save.
    /// </summary>
    public void BeginDraft(Func<string>? pickAvatarColor = null)
    {
        pickAvatarColor ??= LetterAvatar.PickRandomColor;
        _profile = new Profile
        {
            Name = "",
            IconPath = "",
            AvatarColor = pickAvatarColor(),
            ShowOnTray = true,
            VersionId = null
        };
        _snapshot = null;
        _draftAvatarColor = _profile.AvatarColor;
    }

    public void AttachPage(EditProfilePage page)
    {
        ArgumentNullException.ThrowIfNull(page);
        DetachPage();
        _page = page;

        page.SaveRequested += OnSaveRequested;
        page.DiscardRequested += OnDiscardRequested;
        page.DeleteRequested += OnDeleteRequested;
        page.ChooseLogoRequested += OnChooseLogoRequested;
        page.ClearLogoRequested += OnClearLogoRequested;
        page.OpenFolderRequested += ForwardOpenFolderRequested;
        page.AvatarColorRequested += ForwardAvatarColorRequested;
        page.PickCardColorRequested += ForwardPickCardColorRequested;
        page.ResetCardColorRequested += OnResetCardColorRequested;
        page.ProfileNameChanged += OnProfileNameChanged;
        page.EditStateChanged += OnEditStateChanged;
        page.VersionSelectionChanged += OnVersionSelectionChanged;
        page.OpenSettingsRequested += ForwardOpenSettingsRequested;

        if (_profile is not null)
        {
            BindForm(_profile);
            if (IsDraft)
            {
                page.DispatcherQueue.TryEnqueue(() => page.StartNameEdit());
            }
        }

        UpdateDirtyUi();
    }

    public void DetachPage()
    {
        if (_page is null)
        {
            return;
        }

        _page.SaveRequested -= OnSaveRequested;
        _page.DiscardRequested -= OnDiscardRequested;
        _page.DeleteRequested -= OnDeleteRequested;
        _page.ChooseLogoRequested -= OnChooseLogoRequested;
        _page.ClearLogoRequested -= OnClearLogoRequested;
        _page.OpenFolderRequested -= ForwardOpenFolderRequested;
        _page.AvatarColorRequested -= ForwardAvatarColorRequested;
        _page.PickCardColorRequested -= ForwardPickCardColorRequested;
        _page.ResetCardColorRequested -= OnResetCardColorRequested;
        _page.ProfileNameChanged -= OnProfileNameChanged;
        _page.EditStateChanged -= OnEditStateChanged;
        _page.VersionSelectionChanged -= OnVersionSelectionChanged;
        _page.OpenSettingsRequested -= ForwardOpenSettingsRequested;
        _page = null;
    }

    public bool IsDirty()
    {
        if (_page is null || _profile is null)
        {
            return false;
        }

        string avatarForDirty = _profile.AvatarColor;
        if (_snapshot is null
            && _draftAvatarColor is not null
            && string.Equals(avatarForDirty, _draftAvatarColor, StringComparison.OrdinalIgnoreCase))
        {
            avatarForDirty = "";
        }

        return EditProfileDirtyState.IsDirty(
            _snapshot,
            _page.ProfileName.Trim(),
            _page.ShowOnTrayToggleElement.IsOn,
            _profile.IconPath,
            avatarForDirty,
            _page.SelectedCardColor,
            VersionSelection.VersionIdToSave(_page.SelectedVersionId, _snapshot?.VersionId, _lifecycle.Settings.Versions),
            _page.IdentityEnabled,
            _page.IdentityText,
            _page.PendingPasswordAction);
    }

    public void UpdateDirtyUi()
    {
        if (_page is null || _profile is null)
        {
            return;
        }

        bool dirty = IsDirty();
        ProfileValidationKind? nameError = ProfileValidation.TryValidateProfileName(
            _lifecycle.Profiles,
            _page.ProfileName,
            _profile.Id);
        _page.SetNameIssue(nameError is ProfileValidationKind kind
            ? AppChrome.ProfileValidation(kind)
            : null);
        _page.SetSaveChrome(dirty, canSave: dirty && nameError is null);
    }

    /// <summary>
    /// Leaves Edit Profile discarding unsaved work when confirmed.
    /// Returns false if the user chose Stay on a dirty form.
    /// </summary>
    public async Task<bool> TryDiscardAsync(bool navigateToBoard = true)
    {
        if (!IsEditing)
        {
            return true;
        }

        if (IsDirty() && !await _host.ConfirmDiscardAsync())
        {
            return false;
        }

        End(cancel: true, navigateToBoard);
        return true;
    }

    private async void OnDiscardRequested(object? sender, EventArgs e) =>
        await TryDiscardAsync(navigateToBoard: true);

    public void ApplyPickedCardColor(Color? picked)
    {
        if (_page is null)
        {
            return;
        }

        if (picked is null)
        {
            UpdateCardColorPreview();
            UpdateDirtyUi();
            return;
        }

        _page.SetCardColorMode(ColorContrast.ToHex(picked.Value));
        UpdateCardColorPreview();
        UpdateDirtyUi();
    }

    public void PreviewCardColor(string? cardColorSelection)
    {
        if (_page is null || _profile is null)
        {
            return;
        }

        PaintCardColorPreview(cardColorSelection);
    }

    public void ApplyPickedAvatarColor(string hex)
    {
        if (_profile is null || _page is null)
        {
            return;
        }

        _profile.AvatarColor = hex;
        UpdateIconEditorUi();
        UpdateDirtyUi();
    }

    public void PreviewAvatarColor(string hex)
    {
        if (_page is null || _profile is null)
        {
            return;
        }

        string previewName = string.IsNullOrWhiteSpace(_page.ProfileName)
            ? _profile.Name
            : _page.ProfileName;
        AvatarPaint.Apply(
            _profile.IconPath,
            previewName,
            hex,
            _page.IconPreviewEllipseElement,
            _page.IconPreviewLetterElement);
    }

    public void UpdateIconEditorUi()
    {
        if (_page is null || _profile is null)
        {
            return;
        }

        bool hasCustomPhoto = LetterAvatar.HasCustomIcon(_profile.IconPath);
        _page.SetPhotoAffordances(hasCustomPhoto);

        string previewName = string.IsNullOrWhiteSpace(_page.ProfileName)
            ? _profile.Name
            : _page.ProfileName;
        AvatarPaint.Apply(
            _profile.IconPath,
            previewName,
            _profile.AvatarColor,
            _page.IconPreviewEllipseElement,
            _page.IconPreviewLetterElement);
        UpdateCardColorPreview();
    }

    public void RefreshSystemCardColorPreview()
    {
        if (_page is null || !string.IsNullOrWhiteSpace(_page.SelectedCardColor))
        {
            return;
        }

        UpdateCardColorPreview();
    }

    private void UpdateCardColorPreview()
    {
        if (_page is null || _profile is null)
        {
            return;
        }

        PaintCardColorPreview(_page.SelectedCardColor);
    }

    private void PaintCardColorPreview(string? cardColorSelection)
    {
        if (_page is null || _profile is null)
        {
            return;
        }

        string? cardColor = EditProfileDirtyState.IsDefaultCardColor(cardColorSelection)
            ? null
            : cardColorSelection!.Trim();
        _page.CardColorPreviewBorderElement.Background =
            new SolidColorBrush(ColorContrast.ResolveCardColor(cardColor));
    }

    private void BindForm(Profile profile)
    {
        if (_page is null)
        {
            return;
        }

        _bindingForm = true;
        try
        {
            _page.SetProfileName(profile.Name);
            _page.ShowOnTrayToggleElement.IsOn = profile.ShowOnTray != false;
            _page.SetDraftMode(IsDraft);
            UpdateIconEditorUi();

            string cardColor = profile.CardColor ?? "";
            _page.SetCardColorMode(cardColor);
            UpdateCardColorPreview();
            BindVersionChoices(profile.VersionId);
            bool identityOn = !string.IsNullOrWhiteSpace(profile.WindowsNetworkIdentity);
            bool passwordUnavailable = identityOn
                && _lifecycle.GetPasswordStatus(profile.Id) != ProfileCredentialReadKind.Found;
            _page.SetIdentity(
                identityOn,
                profile.WindowsNetworkIdentity,
                passwordSaved: identityOn && !passwordUnavailable,
                passwordUnavailable);
        }
        finally
        {
            _bindingForm = false;
        }

        UpdateDirtyUi();
    }

    private void BindVersionChoices(Guid? selectedVersionId)
    {
        if (_page is null)
        {
            return;
        }

        IReadOnlyList<PbiVersion> versions = _lifecycle.Settings.Versions;
        Guid? defaultVersionId = _lifecycle.Settings.DefaultVersionId;
        VersionSelectionKind kind = VersionSelection.Evaluate(selectedVersionId, versions, defaultVersionId);
        IReadOnlyList<VersionChoiceOption> options = VersionSelection.BuildOptions(
            versions,
            defaultVersionId,
            selectedVersionId);
        _page.SetVersionChoices(options, selectedVersionId, kind);
    }

    private void End(bool cancel, bool navigateToBoard)
    {
        if (cancel && _profile is not null && _snapshot is not null)
        {
            RestoreSnapshot(_profile, _snapshot);
        }

        _profile = null;
        _snapshot = null;
        _draftAvatarColor = null;
        SessionEnded?.Invoke(this, new EditProfileSessionEndedEventArgs
        {
            NavigateToBoard = navigateToBoard
        });
    }

    private static void RestoreSnapshot(Profile profile, Profile snapshot)
    {
        profile.Name = snapshot.Name;
        profile.Order = snapshot.Order;
        profile.ShowOnTray = snapshot.ShowOnTray;
        profile.IconPath = snapshot.IconPath;
        profile.AvatarColor = snapshot.AvatarColor;
        profile.CardColor = snapshot.CardColor;
        profile.VersionId = snapshot.VersionId;
        profile.WindowsNetworkIdentity = snapshot.WindowsNetworkIdentity;
    }

    private static Profile CloneProfile(Profile profile) =>
        new()
        {
            Id = profile.Id,
            Order = profile.Order,
            Name = profile.Name,
            IconPath = profile.IconPath,
            AvatarColor = profile.AvatarColor,
            CardColor = profile.CardColor,
            ShowOnTray = profile.ShowOnTray,
            VersionId = profile.VersionId,
            WindowsNetworkIdentity = profile.WindowsNetworkIdentity
        };

    private async void OnSaveRequested(object? sender, EventArgs e)
    {
        if (_profile is null || _page is null)
        {
            return;
        }

        Profile beforeSave = CloneProfile(_profile);
        ProfileValidationKind? profileNameError = ProfileValidation.TryValidateProfileName(
            _lifecycle.Profiles,
            _page.ProfileName,
            _profile.Id);
        if (profileNameError is ProfileValidationKind nameKind)
        {
            _host.ShowMessage(AppChrome.ProfileValidation(nameKind), 10000);
            return;
        }

        if (_lifecycle.ProfileSavesBlocked)
        {
            await _host.ShowErrorAsync(AppChrome.ProfilesSaveFailed(null, _lifecycle.ProfileSavesBlockedBySettings));
            return;
        }

        bool wasDraft = IsDraft;
        string? savedIconPath = _snapshot?.IconPath;
        string userProfilePath = _lifecycle.Settings.UserProfilePath;
        if (!LetterAvatar.HasCustomIcon(_profile.IconPath))
        {
            _profile.IconPath = "";
        }

        string? copiedAvatar;
        try
        {
            copiedAvatar = ProfileCustomAvatarFiles.CopyPendingImage(userProfilePath, _profile.Id, _profile.IconPath);
        }
        catch (LogoValidationException ex)
        {
            _host.ShowMessage(AppChrome.LogoValidation(ex.Kind, ex.Extension), 10000);
            return;
        }
        catch (Exception ex)
        {
            await _host.ShowErrorAsync(AppChrome.Format("Toast_LogoCopyFailed", ex.Message));
            return;
        }

        if (copiedAvatar is not null)
        {
            _profile.IconPath = copiedAvatar;
        }

        Profile profile = _profile;
        void RollBack()
        {
            RestoreSnapshot(profile, beforeSave);
            ProfileCustomAvatarFiles.TryDeleteLauncherOwnedFile(userProfilePath, profile.Id, copiedAvatar);
        }

        _profile.Name = _page.ProfileName.Trim();
        _profile.ShowOnTray = _page.ShowOnTrayToggleElement.IsOn;
        _profile.VersionId = VersionSelection.VersionIdToSave(
            _page.SelectedVersionId,
            _profile.VersionId,
            _lifecycle.Settings.Versions);
        _profile.CardColor = _page.SelectedCardColor;

        if (string.IsNullOrWhiteSpace(_profile.AvatarColor))
        {
            _profile.AvatarColor = LetterAvatar.PickRandomColor();
        }

        ProfileSaveResult result = _lifecycle.TrySaveProfile(
            _profile,
            isNew: wasDraft,
            _page.IdentityEnabled,
            _page.IdentityText,
            _page.IdentityPassword);
        if (result.IdentityError is IdentityPersistErrorKind persistKind)
        {
            RollBack();
            _host.ShowMessage(AppChrome.IdentityPersist(persistKind, result.OsDetail), 10000);
            return;
        }

        if (!result.Succeeded)
        {
            RollBack();
            UpdateIconEditorUi();
            UpdateDirtyUi();
            string message = AppChrome.ProfilesSaveFailed(result.OsDetail, _lifecycle.ProfileSavesBlockedBySettings);
            if (result.CredentialNotRestored)
            {
                message += " " + AppChrome.Get(result.CredentialRemoved ? "Toast_PasswordRemoved" : "Toast_PasswordNotRestored");
            }

            await _host.ShowErrorAsync(message);
            return;
        }

        string? deleteError = wasDraft
            ? null
            : ProfileCustomAvatarFiles.TryDeleteReplacedFile(
                userProfilePath,
                _profile.Id,
                savedIconPath,
                _profile.IconPath);

        _snapshot = null;
        End(cancel: false, navigateToBoard: true);
        _host.ShowMessage(
            deleteError is null
                ? AppChrome.Get("Toast_Saved")
                : AppChrome.Format("Toast_LogoDeleteFailed", deleteError),
            deleteError is null ? 2000 : 4000);
    }

    private async void OnDeleteRequested(object? sender, EventArgs e)
    {
        if (_profile is null)
        {
            return;
        }

        await TryDeleteAsync(_profile, navigateToBoard: true);
    }

    /// <summary>
    /// Shared Delete lifecycle for Danger Zone and board overflow: confirm, remove Profile
    /// data + list entry, then clear the edit session when the deleted Profile was open.
    /// Returns false when the user cancels the confirmation dialog.
    /// </summary>
    public async Task<bool> TryDeleteAsync(Profile profile, bool navigateToBoard)
    {
        ArgumentNullException.ThrowIfNull(profile);

        if (!await _host.ConfirmDeleteAsync(profile.Name))
        {
            return false;
        }

        ProfileRemoveResult removal = _lifecycle.TryRemoveProfile(profile);
        if (!removal.Removed)
        {
            _host.ShowMessage(
                AppChrome.ProfilesSaveFailed(removal.SaveError, _lifecycle.ProfileSavesBlockedBySettings),
                5000);
        }
        else if (removal.CredentialError is not null || removal.DataError is not null)
        {
            string[] problems =
            [
                removal.CredentialError is null
                    ? ""
                    : AppChrome.Format("Toast_ProfileDeletedPasswordLeft", removal.CredentialError),
                removal.DataError ?? ""
            ];
            _host.ShowMessage(string.Join(" ", problems.Where(text => text.Length > 0)), 10000);
        }

        if (_profile is not null && _profile.Id == profile.Id)
        {
            bool stillListed = _lifecycle.Profiles.Contains(profile);
            if (!stillListed)
            {
                _snapshot = null;
            }

            End(cancel: stillListed, navigateToBoard);
        }

        return true;
    }

    private void OnProfileNameChanged(object? sender, EventArgs e)
    {
        if (_profile is null || _page is null)
        {
            UpdateDirtyUi();
            return;
        }

        if (LetterAvatar.HasCustomIcon(_profile.IconPath))
        {
            UpdateDirtyUi();
            return;
        }

        AvatarPaint.Apply(
            null,
            _page.ProfileName,
            _profile.AvatarColor,
            _page.IconPreviewEllipseElement,
            _page.IconPreviewLetterElement);
        UpdateCardColorPreview();
        UpdateDirtyUi();
    }

    private void OnEditStateChanged(object? sender, EventArgs e)
    {
        if (_bindingForm)
        {
            return;
        }

        UpdateDirtyUi();
    }

    private void OnVersionSelectionChanged(object? sender, EventArgs e)
    {
        if (_bindingForm || _page is null)
        {
            return;
        }

        BindVersionChoices(_page.SelectedVersionId);
        UpdateDirtyUi();
    }

    private void ForwardOpenSettingsRequested(object? sender, EventArgs e) =>
        OpenSettingsRequested?.Invoke(this, EventArgs.Empty);

    private void OnResetCardColorRequested(object? sender, EventArgs e)
    {
        if (_page is null)
        {
            return;
        }

        _page.SetCardColorMode("");
        UpdateCardColorPreview();
        UpdateDirtyUi();
    }

    private async void OnChooseLogoRequested(object? sender, EventArgs e)
    {
        if (_profile is null)
        {
            await _host.ShowErrorAsync(AppChrome.Get("Toast_NoProfileSelected"));
            return;
        }

        string? sourcePath = await _host.PickAvatarImagePathAsync();
        if (sourcePath is null)
        {
            return;
        }

        if (LogoValidation.TryValidateLogoExtension(sourcePath, out string? extension) is LogoValidationKind kind)
        {
            _host.ShowMessage(AppChrome.LogoValidation(kind, extension), 10000);
            return;
        }

        // Pending until Save copies it into the Profile folder; Discard leaves files untouched.
        _profile.IconPath = sourcePath;
        UpdateIconEditorUi();
        UpdateDirtyUi();
    }

    private void OnClearLogoRequested(object? sender, EventArgs e)
    {
        if (_profile is null)
        {
            return;
        }

        // Save deletes the replaced launcher-owned file once the Profile no longer references it.
        _profile.IconPath = "";
        if (string.IsNullOrWhiteSpace(_profile.AvatarColor))
        {
            _profile.AvatarColor = LetterAvatar.PickRandomColor();
        }

        UpdateIconEditorUi();
        UpdateDirtyUi();
    }

    private void ForwardOpenFolderRequested(object? sender, EventArgs e) =>
        OpenFolderRequested?.Invoke(this, EventArgs.Empty);

    private void ForwardAvatarColorRequested(object? sender, EventArgs e) =>
        AvatarColorRequested?.Invoke(this, EventArgs.Empty);

    private void ForwardPickCardColorRequested(object? sender, EventArgs e) =>
        PickCardColorRequested?.Invoke(this, EventArgs.Empty);
}
