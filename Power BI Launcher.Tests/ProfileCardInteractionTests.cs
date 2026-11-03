namespace PowerBILauncher.Tests;

public class ProfileCardInteractionTests
{
    [Fact]
    public void ActivateKind_is_Launch_outside_Reorder_mode()
    {
        Assert.Equal(
            ProfileCardActivateKind.Launch,
            ProfileCardInteraction.ActivateKind(reorderMode: false));
    }

    [Fact]
    public void ActivateKind_is_Inert_in_Reorder_mode()
    {
        Assert.Equal(
            ProfileCardActivateKind.Inert,
            ProfileCardInteraction.ActivateKind(reorderMode: true));
    }

    [Fact]
    public void ActivateKind_is_Inert_while_launching()
    {
        Assert.Equal(
            ProfileCardActivateKind.Inert,
            ProfileCardInteraction.ActivateKind(reorderMode: false, isLaunching: true));
    }

    [Fact]
    public void AutomationKind_is_Launch_outside_Reorder_mode()
    {
        Assert.Equal(ProfileCardAutomationKind.Launch, ProfileCardInteraction.AutomationKind(false));
    }

    [Fact]
    public void AutomationKind_is_Profile_in_Reorder_mode()
    {
        Assert.Equal(ProfileCardAutomationKind.Profile, ProfileCardInteraction.AutomationKind(true));
    }

    [Fact]
    public void IsReorderOffered_is_false_when_profile_count_is_below_two()
    {
        Assert.False(ProfileCardInteraction.IsReorderOffered(0));
        Assert.False(ProfileCardInteraction.IsReorderOffered(1));
    }

    [Fact]
    public void IsReorderOffered_is_true_when_profile_count_is_at_least_two()
    {
        Assert.True(ProfileCardInteraction.IsReorderOffered(2));
        Assert.True(ProfileCardInteraction.IsReorderOffered(5));
    }

    [Fact]
    public void Launch_shortcut_is_allowed_only_when_the_card_would_Launch()
    {
        Assert.True(
            ProfileCardInteraction.IsShortcutAllowed(
                ProfileCardShortcutKind.Launch,
                reorderMode: false,
                isLaunching: false,
                dialogOrFlyoutOpen: false));
        Assert.False(
            ProfileCardInteraction.IsShortcutAllowed(
                ProfileCardShortcutKind.Launch,
                reorderMode: true,
                isLaunching: false,
                dialogOrFlyoutOpen: false));
        Assert.False(
            ProfileCardInteraction.IsShortcutAllowed(
                ProfileCardShortcutKind.Launch,
                reorderMode: false,
                isLaunching: true,
                dialogOrFlyoutOpen: false));
        Assert.False(
            ProfileCardInteraction.IsShortcutAllowed(
                ProfileCardShortcutKind.Launch,
                reorderMode: false,
                isLaunching: false,
                dialogOrFlyoutOpen: true));
    }

    [Fact]
    public void Edit_and_Delete_shortcuts_are_allowed_outside_Reorder_mode_when_no_dialog_is_open()
    {
        Assert.True(
            ProfileCardInteraction.IsShortcutAllowed(
                ProfileCardShortcutKind.Edit,
                reorderMode: false,
                isLaunching: true,
                dialogOrFlyoutOpen: false));
        Assert.True(
            ProfileCardInteraction.IsShortcutAllowed(
                ProfileCardShortcutKind.Delete,
                reorderMode: false,
                isLaunching: false,
                dialogOrFlyoutOpen: false));
        Assert.False(
            ProfileCardInteraction.IsShortcutAllowed(
                ProfileCardShortcutKind.Edit,
                reorderMode: true,
                isLaunching: false,
                dialogOrFlyoutOpen: false));
        Assert.False(
            ProfileCardInteraction.IsShortcutAllowed(
                ProfileCardShortcutKind.Delete,
                reorderMode: false,
                isLaunching: false,
                dialogOrFlyoutOpen: true));
    }

    [Fact]
    public void App_shortcuts_are_allowed_after_setup_when_Edit_Profile_and_dialogs_are_closed()
    {
        Assert.True(
            ProfileCardInteraction.AreAppShortcutsAllowed(
                setupComplete: true,
                editProfileOpen: false,
                dialogOrFlyoutOpen: false));
        Assert.False(
            ProfileCardInteraction.AreAppShortcutsAllowed(
                setupComplete: false,
                editProfileOpen: false,
                dialogOrFlyoutOpen: false));
        Assert.False(
            ProfileCardInteraction.AreAppShortcutsAllowed(
                setupComplete: true,
                editProfileOpen: true,
                dialogOrFlyoutOpen: false));
        Assert.False(
            ProfileCardInteraction.AreAppShortcutsAllowed(
                setupComplete: true,
                editProfileOpen: false,
                dialogOrFlyoutOpen: true));
    }

    [Fact]
    public void Escape_leaves_Reorder_mode_only_when_the_mode_is_on_and_no_dialog_or_flyout_is_open()
    {
        Assert.True(
            ProfileCardInteraction.ShouldLeaveReorderOnEscape(
                reorderMode: true,
                dialogOrFlyoutOpen: false));
        Assert.False(
            ProfileCardInteraction.ShouldLeaveReorderOnEscape(
                reorderMode: false,
                dialogOrFlyoutOpen: false));
        Assert.False(
            ProfileCardInteraction.ShouldLeaveReorderOnEscape(
                reorderMode: true,
                dialogOrFlyoutOpen: true));
    }

    [Fact]
    public void IsMoreRevealed_is_false_in_Reorder_mode()
    {
        Assert.False(
            ProfileCardInteraction.IsMoreRevealed(
                reorderMode: true,
                touchOrPenPreferred: true,
                isPointerOver: true,
                hasKeyboardFocusWithin: true,
                isFlyoutOpen: true));
    }

    [Fact]
    public void IsMoreRevealed_is_true_for_touch_or_pen_outside_Reorder_mode()
    {
        Assert.True(
            ProfileCardInteraction.IsMoreRevealed(
                reorderMode: false,
                touchOrPenPreferred: true,
                isPointerOver: false,
                hasKeyboardFocusWithin: false,
                isFlyoutOpen: false));
    }

    [Fact]
    public void IsMoreRevealed_for_mouse_requires_hover_focus_or_flyout_latch()
    {
        Assert.False(
            ProfileCardInteraction.IsMoreRevealed(
                reorderMode: false,
                touchOrPenPreferred: false,
                isPointerOver: false,
                hasKeyboardFocusWithin: false,
                isFlyoutOpen: false));

        Assert.True(
            ProfileCardInteraction.IsMoreRevealed(
                reorderMode: false,
                touchOrPenPreferred: false,
                isPointerOver: true,
                hasKeyboardFocusWithin: false,
                isFlyoutOpen: false));

        Assert.True(
            ProfileCardInteraction.IsMoreRevealed(
                reorderMode: false,
                touchOrPenPreferred: false,
                isPointerOver: false,
                hasKeyboardFocusWithin: true,
                isFlyoutOpen: false));

        Assert.True(
            ProfileCardInteraction.IsMoreRevealed(
                reorderMode: false,
                touchOrPenPreferred: false,
                isPointerOver: false,
                hasKeyboardFocusWithin: false,
                isFlyoutOpen: true));
    }

    [Fact]
    public void Caption_uses_the_explicit_Version_display_name()
    {
        ProfileCardCaption caption = ProfileCardInteraction.Caption(
            profileVersionId: DesktopId,
            windowsNetworkIdentity: null,
            versions: [Desktop(), ReportServer()],
            defaultVersionId: DesktopId);

        Assert.Equal("Desktop · May 2026", caption.VersionDisplayName);
        Assert.False(caption.FollowsDefault);
        Assert.False(caption.IsUnresolved);
        Assert.False(caption.HasWindowsNetworkIdentity);
    }

    [Fact]
    public void Caption_uses_the_Default_Version_display_name()
    {
        ProfileCardCaption caption = ProfileCardInteraction.Caption(
            profileVersionId: null,
            windowsNetworkIdentity: null,
            versions: [Desktop(), ReportServer()],
            defaultVersionId: ReportServerId);

        Assert.Equal("Report Server · September 2025", caption.VersionDisplayName);
        Assert.True(caption.FollowsDefault);
        Assert.False(caption.IsUnresolved);
        Assert.False(caption.HasWindowsNetworkIdentity);
    }

    [Fact]
    public void Caption_marks_a_dangling_Version_unresolved()
    {
        ProfileCardCaption caption = ProfileCardInteraction.Caption(
            profileVersionId: DanglingId,
            windowsNetworkIdentity: null,
            versions: [Desktop()],
            defaultVersionId: DesktopId);

        Assert.Equal("", caption.VersionDisplayName);
        Assert.False(caption.FollowsDefault);
        Assert.True(caption.IsUnresolved);
        Assert.False(caption.HasWindowsNetworkIdentity);
    }

    [Fact]
    public void Caption_flags_a_Windows_network_identity()
    {
        ProfileCardCaption withIdentity = ProfileCardInteraction.Caption(
            profileVersionId: DesktopId,
            windowsNetworkIdentity: @"CLIENT\analyst",
            versions: [Desktop()],
            defaultVersionId: DesktopId);
        ProfileCardCaption withoutIdentity = ProfileCardInteraction.Caption(
            profileVersionId: DesktopId,
            windowsNetworkIdentity: "  ",
            versions: [Desktop()],
            defaultVersionId: DesktopId);

        Assert.True(withIdentity.HasWindowsNetworkIdentity);
        Assert.False(withoutIdentity.HasWindowsNetworkIdentity);
    }

    [Fact]
    public void ProductIconKind_follows_the_resolved_Version_kind()
    {
        Assert.Equal(
            ProfileCardProductIconKind.Desktop,
            ProfileCardInteraction.ProductIconKind(
                profileVersionId: DesktopId,
                versions: [Desktop(), ReportServer()],
                defaultVersionId: ReportServerId));
        Assert.Equal(
            ProfileCardProductIconKind.ReportServer,
            ProfileCardInteraction.ProductIconKind(
                profileVersionId: null,
                versions: [Desktop(), ReportServer()],
                defaultVersionId: ReportServerId));
    }

    [Fact]
    public void ProductIconKind_is_Warning_when_the_Version_cannot_be_resolved()
    {
        Assert.Equal(
            ProfileCardProductIconKind.Warning,
            ProfileCardInteraction.ProductIconKind(
                profileVersionId: DanglingId,
                versions: [Desktop()],
                defaultVersionId: DesktopId));
        Assert.Equal(
            ProfileCardProductIconKind.Warning,
            ProfileCardInteraction.ProductIconKind(
                profileVersionId: null,
                versions: [],
                defaultVersionId: null));
        Assert.Equal(
            ProfileCardProductIconKind.Warning,
            ProfileCardInteraction.ProductIconKind(
                profileVersionId: null,
                versions: [Desktop()],
                defaultVersionId: null));
    }

    [Fact]
    public void ShowEmptyState_is_true_only_when_there_are_no_Profiles()
    {
        Assert.True(ProfileCardInteraction.ShowEmptyState(0));
        Assert.False(ProfileCardInteraction.ShowEmptyState(1));
        Assert.False(ProfileCardInteraction.ShowEmptyState(2));
    }

    private static readonly Guid DesktopId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid ReportServerId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid DanglingId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");

    private static PbiVersion Desktop() => new()
    {
        Id = DesktopId,
        DisplayName = "Desktop · May 2026",
        Path = @"D:\PBI\PBIDesktop.exe",
        Kind = PbiVersionKind.Desktop
    };

    private static PbiVersion ReportServer() => new()
    {
        Id = ReportServerId,
        DisplayName = "Report Server · September 2025",
        Path = @"D:\PBIRS\PBIDesktop.exe",
        Kind = PbiVersionKind.ReportServer
    };
}
