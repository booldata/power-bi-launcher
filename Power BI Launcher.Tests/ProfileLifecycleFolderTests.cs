using System.Text.Json;

namespace PowerBILauncher.Tests;

public class ProfileLifecycleFolderTests : IDisposable
{
    private static readonly string DesktopPath = @"D:\Tools\PBIDesktop\bin\PBIDesktop.exe";
    private static readonly string ReportServerPath = @"C:\Program Files\Microsoft Power BI Desktop RS\bin\PBIDesktop.exe";

    private readonly string _baseDir;
    private readonly string _profilesRoot;

    public ProfileLifecycleFolderTests()
    {
        // Short name: the Profiles root must stay within PowerBiPathBudget for launches to pass.
        _baseDir = Path.Combine(Path.GetTempPath(), "pbi-fld-" + Guid.NewGuid().ToString("N").Substring(0, 8));
        _profilesRoot = Path.Combine(_baseDir, "Profiles");
        Directory.CreateDirectory(_profilesRoot);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_baseDir))
            {
                Directory.Delete(_baseDir, recursive: true);
            }
        }
        catch
        {
        }
    }

    private ProfileLifecycle ReadyLifecycle(
        StubExecutableInspector inspector,
        RecordingProcessStarter starter,
        string? profilesRoot = null)
    {
        inspector.Pass(DesktopPath);
        var lifecycle = new ProfileLifecycle(_baseDir, _baseDir, inspector, starter);
        lifecycle.Load();
        lifecycle.Settings.UserProfilePath = profilesRoot ?? _profilesRoot;
        Assert.True(lifecycle.TryRegisterVersion(DesktopPath, out _));
        return lifecycle;
    }

    private static Profile AddProfile(ProfileLifecycle lifecycle)
    {
        var profile = new Profile
        {
            Id = Guid.Parse("55555555-5555-5555-5555-555555555555"),
            Name = "Folders",
            Order = 1,
            AvatarColor = "#4F6BED"
        };
        lifecycle.Profiles.Add(profile);
        return profile;
    }

    private static void WriteFile(string path, string contents)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, contents);
    }

    [Fact]
    public void TryLaunch_uses_short_layout_environment()
    {
        var inspector = new StubExecutableInspector();
        var starter = new RecordingProcessStarter();
        ProfileLifecycle lifecycle = ReadyLifecycle(inspector, starter);
        Profile profile = AddProfile(lifecycle);

        ProfileLaunchResult result = lifecycle.TryLaunch(profile);

        Assert.True(result.Succeeded);
        string expectedRoot = Path.Combine(_profilesRoot, "555555555555");
        Assert.Equal(expectedRoot, starter.Last!.EnvironmentVariables["USERPROFILE"]);
        Assert.Equal(Path.Combine(expectedRoot, "AppData", "Local"), starter.Last.EnvironmentVariables["LOCALAPPDATA"]);
        Assert.Equal(Path.Combine(expectedRoot, "AppData", "Roaming"), starter.Last.EnvironmentVariables["APPDATA"]);
        Assert.Equal(Path.Combine(expectedRoot, "AppData", "Local", "Temp"), starter.Last.EnvironmentVariables["TEMP"]);
        Assert.True(Directory.Exists(Path.Combine(expectedRoot, "AppData", "Local", "Microsoft", "EdgeWebView")));
    }

    [Fact]
    public void TryLaunch_migrates_legacy_folder_before_starting()
    {
        var inspector = new StubExecutableInspector();
        var starter = new RecordingProcessStarter();
        ProfileLifecycle lifecycle = ReadyLifecycle(inspector, starter);
        Profile profile = AddProfile(lifecycle);
        ProfilePaths legacy = ProfilePaths.LegacyForProfile(_profilesRoot, profile.Id);
        ProfilePaths current = ProfilePaths.ForProfile(_profilesRoot, profile.Id);
        WriteFile(Path.Combine(legacy.LocalAppData, "Microsoft", "Power BI Desktop", "User.zip"), "state");

        ProfileLaunchResult result = lifecycle.TryLaunch(profile);

        Assert.True(result.Succeeded);
        Assert.False(Directory.Exists(legacy.Root));
        Assert.Equal("state", File.ReadAllText(Path.Combine(current.LocalAppData, "Microsoft", "Power BI Desktop", "User.zip")));
        Assert.Equal(current.LocalAppData, starter.Last!.EnvironmentVariables["LOCALAPPDATA"]);
    }

    [Fact]
    public void TryLaunch_blocks_when_legacy_folder_cannot_be_moved()
    {
        var inspector = new StubExecutableInspector();
        var starter = new RecordingProcessStarter();
        ProfileLifecycle lifecycle = ReadyLifecycle(inspector, starter);
        Profile profile = AddProfile(lifecycle);
        ProfilePaths legacy = ProfilePaths.LegacyForProfile(_profilesRoot, profile.Id);
        string locked = Path.Combine(legacy.LocalAppData, "locked.bin");
        WriteFile(locked, "busy");

        ProfileLaunchResult result;
        using (new FileStream(locked, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
        {
            result = lifecycle.TryLaunch(profile);
        }

        Assert.False(result.Succeeded);
        Assert.Equal(0, starter.StartCount);
        Assert.Equal(ProfileLaunchRemediation.None, result.Remediation);
        Assert.Equal(ProfileLaunchErrorKind.ProfileFolderMigration, result.ErrorKind);
        Assert.False(string.IsNullOrEmpty(result.OsDetail));
        Assert.True(Directory.Exists(legacy.Root));

        ProfileLaunchResult retry = lifecycle.TryLaunch(profile);

        Assert.True(retry.Succeeded);
        Assert.False(Directory.Exists(legacy.Root));
    }

    [Fact]
    public void Load_migrates_legacy_folders_and_rewrites_icon_path()
    {
        var profileId = Guid.Parse("66666666-6666-6666-6666-666666666666");
        ProfilePaths legacy = ProfilePaths.LegacyForProfile(_profilesRoot, profileId);
        ProfilePaths current = ProfilePaths.ForProfile(_profilesRoot, profileId);
        string legacyIcon = Path.Combine(legacy.Root, "me.png");
        WriteFile(legacyIcon, "png");
        WriteFile(Path.Combine(legacy.RoamingAppData, "x.txt"), "roam");
        var settings = new Setting { UserProfilePath = _profilesRoot, HasCompletedSetup = true };
        File.WriteAllText(
            Path.Combine(_baseDir, "settings.json"),
            JsonSerializer.Serialize(settings, ProfileLifecycle.SettingsJsonOptions));
        File.WriteAllText(
            Path.Combine(_baseDir, "profiles.json"),
            JsonSerializer.Serialize(new List<Profile>
            {
                new() { Id = profileId, Name = "Old", Order = 1, AvatarColor = "#4F6BED", IconPath = legacyIcon, ShowOnTray = true }
            }));

        var lifecycle = new ProfileLifecycle(_baseDir, _baseDir, new StubExecutableInspector(), new RecordingProcessStarter());
        lifecycle.Load();

        Assert.False(Directory.Exists(legacy.Root));
        Assert.Equal("roam", File.ReadAllText(Path.Combine(current.RoamingAppData, "x.txt")));
        string expectedIcon = Path.Combine(Path.GetFullPath(current.Root), "me.png");
        Assert.Equal(expectedIcon, lifecycle.Profiles[0].IconPath);
        string persisted = File.ReadAllText(Path.Combine(_baseDir, "profiles.json"));
        Assert.Contains(JsonEncodedText.Encode(expectedIcon).ToString(), persisted);
    }

    [Fact]
    public void TryLaunch_blocks_report_server_when_profile_path_is_too_long()
    {
        var inspector = new StubExecutableInspector();
        var starter = new RecordingProcessStarter();
        int maxRoot = PowerBiPathBudget.MaxProfilesRootLength(PbiVersionKind.ReportServer);
        string longRoot = Path.Combine(_profilesRoot, new string('p', maxRoot + 1 - _profilesRoot.Length - 1));
        Assert.Equal(maxRoot + 1, longRoot.Length);
        Directory.CreateDirectory(longRoot);
        ProfileLifecycle lifecycle = ReadyLifecycle(inspector, starter, longRoot);
        inspector.PassReportServer(ReportServerPath);
        Assert.True(lifecycle.TryRegisterVersion(ReportServerPath, out _));
        PbiVersion reportServer = lifecycle.Settings.Versions.Single(version => version.Kind == PbiVersionKind.ReportServer);
        Profile profile = AddProfile(lifecycle);
        profile.VersionId = reportServer.Id;

        ProfileLaunchResult result = lifecycle.TryLaunch(profile);

        Assert.False(result.Succeeded);
        Assert.Equal(0, starter.StartCount);
        Assert.Equal(ProfileLaunchRemediation.Settings, result.Remediation);
        Assert.Equal(ProfileLaunchErrorKind.PathTooLong, result.ErrorKind);
        Assert.NotNull(result.PathBudget);
        Assert.Equal(PbiVersionKind.ReportServer, result.PathBudget.Kind);
        Assert.Equal(PathBudgetScope.LocalAppData, result.PathBudget.Scope);
        Assert.False(Directory.Exists(ProfilePaths.ForProfile(longRoot, profile.Id).Root));

        // Same root still fits regular Desktop, which has a slightly larger budget.
        profile.VersionId = null;
        Assert.True(lifecycle.TryLaunch(profile).Succeeded);
    }

    [Fact]
    public void TryLaunch_reports_an_unavailable_profiles_folder_instead_of_throwing()
    {
        var inspector = new StubExecutableInspector();
        var starter = new RecordingProcessStarter();
        string blocker = Path.Combine(_baseDir, "blocker");
        File.WriteAllText(blocker, "not a folder");
        ProfileLifecycle lifecycle = ReadyLifecycle(inspector, starter, Path.Combine(blocker, "Profiles"));
        Profile profile = AddProfile(lifecycle);

        ProfileLaunchResult result = lifecycle.TryLaunch(profile);

        Assert.False(result.Succeeded);
        Assert.Equal(0, starter.StartCount);
        Assert.Equal(ProfileLaunchErrorKind.ProfileFolderUnavailable, result.ErrorKind);
        Assert.Equal(ProfileLaunchRemediation.Settings, result.Remediation);
        Assert.False(string.IsNullOrEmpty(result.OsDetail));
    }

    [Fact]
    public void TryEnsureProfilesRoot_creates_missing_folder()
    {
        var inspector = new StubExecutableInspector();
        ProfileLifecycle lifecycle = ReadyLifecycle(inspector, new RecordingProcessStarter());
        string missing = Path.Combine(_baseDir, "new-root");
        Assert.False(Directory.Exists(missing));

        bool created = lifecycle.TryEnsureProfilesRoot(missing, out SettingsIssue? issue);

        Assert.True(created);
        Assert.Null(issue);
        Assert.True(Directory.Exists(missing));
        Assert.Empty(lifecycle.CollectSettingsSaveErrors(missing));
    }

    [Fact]
    public void TryEnsureProfilesRoot_returns_folder_missing_when_path_is_a_file()
    {
        var inspector = new StubExecutableInspector();
        ProfileLifecycle lifecycle = ReadyLifecycle(inspector, new RecordingProcessStarter());
        string filePath = Path.Combine(_baseDir, "not-a-folder");
        File.WriteAllText(filePath, "nope");

        bool created = lifecycle.TryEnsureProfilesRoot(filePath, out SettingsIssue? issue);

        Assert.False(created);
        Assert.NotNull(issue);
        Assert.Equal(SettingsIssueKind.FolderMissing, issue.Kind);
        Assert.True(File.Exists(filePath));
    }

    [Fact]
    public void CollectSettingsSaveErrors_flags_root_too_long_for_registered_kinds()
    {
        var inspector = new StubExecutableInspector();
        ProfileLifecycle lifecycle = ReadyLifecycle(inspector, new RecordingProcessStarter());
        int maxDesktopRoot = PowerBiPathBudget.MaxProfilesRootLength(PbiVersionKind.Desktop);
        string longRoot = Path.Combine(_profilesRoot, new string('p', maxDesktopRoot + 1 - _profilesRoot.Length - 1));
        Directory.CreateDirectory(longRoot);

        IReadOnlyList<SettingsIssue> errors = lifecycle.CollectSettingsSaveErrors(longRoot);

        Assert.Single(errors);
        Assert.Equal(SettingsIssueKind.PathTooLong, errors[0].Kind);
        PathBudgetMiss? budget = errors[0].Budget;
        Assert.NotNull(budget);
        Assert.Equal(PbiVersionKind.Desktop, budget.Kind);
        Assert.Empty(lifecycle.CollectSettingsSaveErrors(_profilesRoot));
    }

    [Fact]
    public void TryRemoveProfile_deletes_both_layouts()
    {
        var inspector = new StubExecutableInspector();
        ProfileLifecycle lifecycle = ReadyLifecycle(inspector, new RecordingProcessStarter());
        Profile profile = AddProfile(lifecycle);
        ProfilePaths legacy = ProfilePaths.LegacyForProfile(_profilesRoot, profile.Id);
        ProfilePaths current = ProfilePaths.ForProfile(_profilesRoot, profile.Id);
        WriteFile(Path.Combine(legacy.LocalAppData, "a.txt"), "a");
        WriteFile(Path.Combine(current.LocalAppData, "b.txt"), "b");

        Assert.True(lifecycle.TryDeleteProfileData(profile, out string? error));

        Assert.Null(error);
        Assert.False(Directory.Exists(legacy.Root));
        Assert.False(Directory.Exists(current.Root));
    }
}
