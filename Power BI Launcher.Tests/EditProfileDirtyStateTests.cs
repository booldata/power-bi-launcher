namespace PowerBILauncher.Tests;

public class EditProfileDirtyStateTests
{
    private static Profile Snapshot(
        string name = "Acme",
        bool? showOnTray = true,
        string iconPath = "",
        string avatarColor = "#4F6BED",
        string cardColor = "",
        Guid? versionId = null) =>
        new()
        {
            Name = name,
            ShowOnTray = showOnTray,
            IconPath = iconPath,
            AvatarColor = avatarColor,
            CardColor = cardColor,
            VersionId = versionId
        };

    [Fact]
    public void IsDirty_false_when_matches_snapshot()
    {
        Profile snap = Snapshot();
        Assert.False(EditProfileDirtyState.IsDirty(
            snap,
            currentName: "Acme",
            currentShowOnTray: true,
            currentIconPath: "",
            currentAvatarColor: "#4F6BED",
            currentCardColor: "System",
            currentVersionId: null));
    }

    [Fact]
    public void IsDirty_true_for_a_pending_picked_image_or_a_pending_remove()
    {
        Profile snap = Snapshot(iconPath: @"C:\Profiles\aaaaaaaaaaaa\logo.png");

        Assert.True(EditProfileDirtyState.IsDirty(
            snap,
            currentName: "Acme",
            currentShowOnTray: true,
            currentIconPath: @"C:\Users\me\Pictures\logo.png",
            currentAvatarColor: "#4F6BED",
            currentCardColor: "",
            currentVersionId: null));
        Assert.True(EditProfileDirtyState.IsDirty(
            snap,
            currentName: "Acme",
            currentShowOnTray: true,
            currentIconPath: "",
            currentAvatarColor: "#4F6BED",
            currentCardColor: "",
            currentVersionId: null));
    }

    [Fact]
    public void FooterSave_disabled_when_snapshot_equals_current()
    {
        Profile snap = Snapshot();
        bool dirty = EditProfileDirtyState.IsDirty(
            snap,
            currentName: "Acme",
            currentShowOnTray: true,
            currentIconPath: "",
            currentAvatarColor: "#4F6BED",
            currentCardColor: "System",
            currentVersionId: null);
        bool nameValid = true;
        bool footerSaveEnabled = dirty && nameValid;

        Assert.False(dirty);
        Assert.False(footerSaveEnabled);
    }

    [Fact]
    public void IsDirty_false_when_draft_has_defaults()
    {
        Assert.False(EditProfileDirtyState.IsDirty(
            null,
            currentName: "",
            currentShowOnTray: true,
            currentIconPath: "",
            currentAvatarColor: "",
            currentCardColor: "",
            currentVersionId: null));
    }

    [Fact]
    public void IsDirty_when_draft_has_typed_name()
    {
        Assert.True(EditProfileDirtyState.IsDirty(
            null,
            currentName: "Acme",
            currentShowOnTray: true,
            currentIconPath: "",
            currentAvatarColor: "",
            currentCardColor: "",
            currentVersionId: null));
    }

    [Fact]
    public void IsDirty_when_draft_avatar_color_changes()
    {
        Assert.True(EditProfileDirtyState.IsDirty(
            null,
            currentName: "",
            currentShowOnTray: true,
            currentIconPath: "",
            currentAvatarColor: "#112233",
            currentCardColor: "",
            currentVersionId: null));
    }

    [Fact]
    public void IsDirty_when_draft_icon_path_changes()
    {
        Assert.True(EditProfileDirtyState.IsDirty(
            null,
            currentName: "",
            currentShowOnTray: true,
            currentIconPath: @"C:\logo.png",
            currentAvatarColor: "",
            currentCardColor: "",
            currentVersionId: null));
    }

    [Fact]
    public void FooterSave_enabled_when_draft_has_valid_name()
    {
        bool dirty = EditProfileDirtyState.IsDirty(
            null,
            currentName: "Acme",
            currentShowOnTray: true,
            currentIconPath: "",
            currentAvatarColor: "",
            currentCardColor: "",
            currentVersionId: null);
        bool nameValid = true;
        bool footerSaveEnabled = dirty && nameValid;

        Assert.True(dirty);
        Assert.True(footerSaveEnabled);
    }

    [Fact]
    public void IsDirty_when_name_changes()
    {
        Assert.True(EditProfileDirtyState.IsDirty(
            Snapshot(),
            currentName: "Other",
            currentShowOnTray: true,
            currentIconPath: "",
            currentAvatarColor: "#4F6BED",
            currentCardColor: "System",
            currentVersionId: null));
    }

    [Fact]
    public void IsDirty_when_show_on_tray_changes()
    {
        Assert.True(EditProfileDirtyState.IsDirty(
            Snapshot(),
            currentName: "Acme",
            currentShowOnTray: false,
            currentIconPath: "",
            currentAvatarColor: "#4F6BED",
            currentCardColor: "System",
            currentVersionId: null));
    }

    [Fact]
    public void IsDirty_when_icon_path_changes()
    {
        Assert.True(EditProfileDirtyState.IsDirty(
            Snapshot(),
            currentName: "Acme",
            currentShowOnTray: true,
            currentIconPath: @"C:\logo.png",
            currentAvatarColor: "#4F6BED",
            currentCardColor: "System",
            currentVersionId: null));
    }

    [Fact]
    public void IsDirty_when_avatar_color_changes()
    {
        Assert.True(EditProfileDirtyState.IsDirty(
            Snapshot(),
            currentName: "Acme",
            currentShowOnTray: true,
            currentIconPath: "",
            currentAvatarColor: "#112233",
            currentCardColor: "System",
            currentVersionId: null));
    }

    [Fact]
    public void IsDirty_when_card_color_changes()
    {
        Assert.True(EditProfileDirtyState.IsDirty(
            Snapshot(),
            currentName: "Acme",
            currentShowOnTray: true,
            currentIconPath: "",
            currentAvatarColor: "#4F6BED",
            currentCardColor: "#AABBCC",
            currentVersionId: null));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("System")]
    [InlineData("system")]
    [InlineData("No Accent")]
    [InlineData("no accent")]
    [InlineData("  No Accent  ")]
    public void IsDefaultCardColor_true_for_empty_and_english_sentinels(string? value)
    {
        Assert.True(EditProfileDirtyState.IsDefaultCardColor(value));
    }

    [Fact]
    public void IsDefaultCardColor_false_for_a_custom_hex()
    {
        Assert.False(EditProfileDirtyState.IsDefaultCardColor("#AABBCC"));
    }

    [Fact]
    public void IsDirty_false_when_no_accent_matches_empty_snapshot()
    {
        Assert.False(EditProfileDirtyState.IsDirty(
            Snapshot(cardColor: ""),
            currentName: "Acme",
            currentShowOnTray: true,
            currentIconPath: "",
            currentAvatarColor: "#4F6BED",
            currentCardColor: "No Accent",
            currentVersionId: null));
    }

    [Fact]
    public void IsDirty_when_version_id_changes()
    {
        Guid explicitId = Guid.Parse("22222222-2222-2222-2222-222222222222");
        Assert.True(EditProfileDirtyState.IsDirty(
            Snapshot(versionId: null),
            currentName: "Acme",
            currentShowOnTray: true,
            currentIconPath: "",
            currentAvatarColor: "#4F6BED",
            currentCardColor: "System",
            currentVersionId: explicitId));
    }

    [Fact]
    public void IsDirty_false_when_version_id_matches_snapshot()
    {
        Guid explicitId = Guid.Parse("22222222-2222-2222-2222-222222222222");
        Assert.False(EditProfileDirtyState.IsDirty(
            Snapshot(versionId: explicitId),
            currentName: "Acme",
            currentShowOnTray: true,
            currentIconPath: "",
            currentAvatarColor: "#4F6BED",
            currentCardColor: "System",
            currentVersionId: explicitId));
    }

    [Fact]
    public void IsDirty_when_identity_is_enabled()
    {
        Assert.True(EditProfileDirtyState.IsDirty(
            Snapshot(),
            currentName: "Acme",
            currentShowOnTray: true,
            currentIconPath: "",
            currentAvatarColor: "#4F6BED",
            currentCardColor: "System",
            currentVersionId: null,
            currentIdentityEnabled: true,
            currentIdentityText: @"CONTOSO\JSmith"));
    }

    [Fact]
    public void IsDirty_when_pending_password_action()
    {
        Assert.True(EditProfileDirtyState.IsDirty(
            Snapshot(),
            currentName: "Acme",
            currentShowOnTray: true,
            currentIconPath: "",
            currentAvatarColor: "#4F6BED",
            currentCardColor: "System",
            currentVersionId: null,
            currentIdentityEnabled: false,
            currentIdentityText: null,
            pendingPasswordAction: true));
    }
}
