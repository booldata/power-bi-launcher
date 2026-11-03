namespace PowerBILauncher;

/// <summary>
/// UI-free dirty check for Edit Profile vs the open-time snapshot, or vs draft defaults
/// when the snapshot is null (unsaved Add Profile draft).
/// </summary>
public static class EditProfileDirtyState
{
    public static bool IsDirty(
        Profile? snapshot,
        string currentName,
        bool currentShowOnTray,
        string? currentIconPath,
        string? currentAvatarColor,
        string? currentCardColor,
        Guid? currentVersionId,
        bool currentIdentityEnabled = false,
        string? currentIdentityText = null,
        bool pendingPasswordAction = false)
    {
        // Null snapshot is a new draft: compare against draft defaults
        // (empty name, ShowOnTray on, no icon, empty Avatar/card, Default Version).
        string snapName = snapshot is null ? "" : Normalize(snapshot.Name);
        bool snapShowOnTray = snapshot is null || snapshot.ShowOnTray != false;
        string snapIcon = snapshot is null ? "" : Normalize(snapshot.IconPath);
        string snapAvatar = snapshot is null ? "" : Normalize(snapshot.AvatarColor);
        string snapCard = snapshot is null ? "" : NormalizeCardColor(snapshot.CardColor);
        Guid? snapVersionId = snapshot?.VersionId;
        string? snapIdentity = snapshot is null
            ? null
            : WindowsNetworkIdentityText.Normalize(snapshot.WindowsNetworkIdentity);
        bool snapIdentityEnabled = snapIdentity is not null;

        return !string.Equals(snapName, Normalize(currentName), StringComparison.Ordinal)
            || snapShowOnTray != currentShowOnTray
            || !string.Equals(snapIcon, Normalize(currentIconPath), StringComparison.OrdinalIgnoreCase)
            || !string.Equals(snapAvatar, Normalize(currentAvatarColor), StringComparison.OrdinalIgnoreCase)
            || !string.Equals(snapCard, NormalizeCardColor(currentCardColor), StringComparison.OrdinalIgnoreCase)
            || snapVersionId != currentVersionId
            || snapIdentityEnabled != currentIdentityEnabled
            || !string.Equals(
                snapIdentity,
                WindowsNetworkIdentityText.Normalize(currentIdentityText),
                StringComparison.Ordinal)
            || pendingPasswordAction;
    }

    private static string Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value) ? "" : value.Trim();

    /// <summary>
    /// True when the card uses the default untinted fill. UI sentinels "System" and
    /// "No Accent" (and whitespace) match an empty stored CardColor.
    /// </summary>
    public static bool IsDefaultCardColor(string? value)
    {
        string normalized = Normalize(value);
        return normalized.Length == 0
            || string.Equals(normalized, "System", StringComparison.OrdinalIgnoreCase)
            || string.Equals(normalized, "No Accent", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>UI shows "System" / "No Accent" for empty card color; treat those as equal.</summary>
    private static string NormalizeCardColor(string? value) =>
        IsDefaultCardColor(value) ? "" : Normalize(value);
}
