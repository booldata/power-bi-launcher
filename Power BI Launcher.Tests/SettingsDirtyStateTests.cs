namespace PowerBILauncher.Tests;

public class SettingsDirtyStateTests
{
    private static readonly Guid DesktopId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    private static PbiVersion DesktopVersion() =>
        new()
        {
            Id = DesktopId,
            DisplayName = "Desktop · Downloaded",
            Path = @"C:\PBIDesktop.exe",
            Kind = PbiVersionKind.Desktop
        };

    private static Setting Saved(
        string folder = @"C:\Profiles",
        MinimizeButtonBehavior minimize = MinimizeButtonBehavior.Minimize,
        CloseButtonBehavior close = CloseButtonBehavior.Close) =>
        new()
        {
            UserProfilePath = folder,
            Versions = [DesktopVersion()],
            DefaultVersionId = DesktopId,
            MinimizeBehavior = minimize,
            CloseBehavior = close,
            HasCompletedSetup = true
        };

    [Fact]
    public void IsDirty_false_when_matches_saved()
    {
        Setting saved = Saved();
        Assert.False(SettingsDirtyState.IsDirty(
            saved,
            draftVersions: [DesktopVersion()],
            draftDefaultVersionId: DesktopId,
            currentFolder: @"C:\Profiles",
            currentMinimize: MinimizeButtonBehavior.Minimize,
            currentClose: CloseButtonBehavior.Close));
    }

    [Fact]
    public void FooterSave_disabled_when_setup_complete_and_snapshot_equals_current()
    {
        Setting saved = Saved();
        bool dirty = SettingsDirtyState.IsDirty(
            saved,
            draftVersions: [DesktopVersion()],
            draftDefaultVersionId: DesktopId,
            currentFolder: @"C:\Profiles",
            currentMinimize: MinimizeButtonBehavior.Minimize,
            currentClose: CloseButtonBehavior.Close);

        Assert.False(SettingsDirtyState.CanEnableFooterSave(dirty, hasCompletedSetup: true, pathIssue: null));
    }

    [Fact]
    public void FooterSave_enabled_during_onboarding_even_when_form_matches_defaults()
    {
        Setting saved = Saved();
        bool dirty = SettingsDirtyState.IsDirty(
            saved,
            draftVersions: [DesktopVersion()],
            draftDefaultVersionId: DesktopId,
            currentFolder: @"C:\Profiles",
            currentMinimize: MinimizeButtonBehavior.Minimize,
            currentClose: CloseButtonBehavior.Close);

        Assert.True(SettingsDirtyState.CanEnableFooterSave(dirty, hasCompletedSetup: false, pathIssue: null));
    }

    [Fact]
    public void FooterSave_disabled_when_path_issue_even_during_onboarding()
    {
        Assert.False(SettingsDirtyState.CanEnableFooterSave(
            dirty: true,
            hasCompletedSetup: false,
            pathIssue: new SettingsIssue { Kind = SettingsIssueKind.FolderEmpty }));
    }

    [Fact]
    public void FooterSave_disabled_when_path_issue_and_form_is_dirty()
    {
        Assert.False(SettingsDirtyState.CanEnableFooterSave(
            dirty: true,
            hasCompletedSetup: true,
            pathIssue: new SettingsIssue { Kind = SettingsIssueKind.FolderRelative }));
    }

    [Fact]
    public void RootChangeNote_shown_when_edited_root_differs_and_profiles_exist()
    {
        Assert.True(SettingsDirtyState.ShouldShowProfilesRootChangeNote(
            savedRoot: @"C:\Profiles",
            editedRoot: @"D:\Other",
            profileCount: 1));
    }

    [Fact]
    public void RootChangeNote_hidden_when_no_profiles_exist()
    {
        Assert.False(SettingsDirtyState.ShouldShowProfilesRootChangeNote(
            savedRoot: @"C:\Profiles",
            editedRoot: @"D:\Other",
            profileCount: 0));
    }

    [Fact]
    public void RootChangeNote_hidden_when_root_matches_saved()
    {
        Assert.False(SettingsDirtyState.ShouldShowProfilesRootChangeNote(
            savedRoot: @"C:\Profiles",
            editedRoot: "  C:\\Profiles  ",
            profileCount: 2));
    }

    [Fact]
    public void IsDirty_when_folder_changes()
    {
        Assert.True(SettingsDirtyState.IsDirty(
            Saved(),
            draftVersions: [DesktopVersion()],
            draftDefaultVersionId: DesktopId,
            currentFolder: @"D:\Other",
            currentMinimize: MinimizeButtonBehavior.Minimize,
            currentClose: CloseButtonBehavior.Close));
    }

    [Fact]
    public void IsDirty_false_when_folder_differs_only_by_whitespace()
    {
        Assert.False(SettingsDirtyState.IsDirty(
            Saved(folder: @"C:\Profiles"),
            draftVersions: [DesktopVersion()],
            draftDefaultVersionId: DesktopId,
            currentFolder: "  C:\\Profiles  ",
            currentMinimize: MinimizeButtonBehavior.Minimize,
            currentClose: CloseButtonBehavior.Close));
    }

    [Fact]
    public void IsDirty_when_minimize_behavior_changes()
    {
        Assert.True(SettingsDirtyState.IsDirty(
            Saved(),
            draftVersions: [DesktopVersion()],
            draftDefaultVersionId: DesktopId,
            currentFolder: @"C:\Profiles",
            currentMinimize: MinimizeButtonBehavior.SendToTray,
            currentClose: CloseButtonBehavior.Close));
    }

    [Fact]
    public void IsDirty_when_close_behavior_changes()
    {
        Assert.True(SettingsDirtyState.IsDirty(
            Saved(),
            draftVersions: [DesktopVersion()],
            draftDefaultVersionId: DesktopId,
            currentFolder: @"C:\Profiles",
            currentMinimize: MinimizeButtonBehavior.Minimize,
            currentClose: CloseButtonBehavior.SendToTray));
    }

    [Fact]
    public void IsDirty_when_default_version_changes()
    {
        Guid otherId = Guid.Parse("22222222-2222-2222-2222-222222222222");
        Assert.True(SettingsDirtyState.IsDirty(
            Saved(),
            draftVersions: [DesktopVersion()],
            draftDefaultVersionId: otherId,
            currentFolder: @"C:\Profiles",
            currentMinimize: MinimizeButtonBehavior.Minimize,
            currentClose: CloseButtonBehavior.Close));
    }

    [Fact]
    public void IsDirty_when_version_display_name_changes()
    {
        PbiVersion renamed = DesktopVersion();
        renamed.DisplayName = "Renamed Desktop";
        Assert.True(SettingsDirtyState.IsDirty(
            Saved(),
            draftVersions: [renamed],
            draftDefaultVersionId: DesktopId,
            currentFolder: @"C:\Profiles",
            currentMinimize: MinimizeButtonBehavior.Minimize,
            currentClose: CloseButtonBehavior.Close));
    }
}
