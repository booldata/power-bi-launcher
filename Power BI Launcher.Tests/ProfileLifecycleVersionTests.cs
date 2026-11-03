using System.Text.Json;

namespace PowerBILauncher.Tests;

public class ProfileLifecycleVersionTests : IDisposable
{
    private static readonly string StubPath =
        @"D:\Tools\PBIDesktop\bin\PBIDesktop.exe";
    private static readonly string ReportServerPath =
        @"C:\Program Files\Microsoft Power BI Desktop RS\bin\PBIDesktop.exe";
    private static readonly string StorePath =
        @"C:\Program Files\WindowsApps\Microsoft.MicrosoftPowerBIDesktop_2.157.1354.0_x64__8wekyb3d8bbwe\bin\PBIDesktop.exe";

    private readonly string _baseDir;
    private readonly string _cwdDir;
    private readonly string _profilesRoot;

    public ProfileLifecycleVersionTests()
    {
        // Short names: the Profiles root must stay within PowerBiPathBudget for launches to pass.
        string run = Guid.NewGuid().ToString("N").Substring(0, 8);
        _baseDir = Path.Combine(Path.GetTempPath(), "pbi-ver-" + run);
        _cwdDir = Path.Combine(Path.GetTempPath(), "pbi-ver-cwd-" + run);
        _profilesRoot = Path.Combine(_baseDir, "Profiles");
        Directory.CreateDirectory(_baseDir);
        Directory.CreateDirectory(_cwdDir);
        Directory.CreateDirectory(_profilesRoot);
    }

    public void Dispose()
    {
        TryDelete(_baseDir);
        TryDelete(_cwdDir);
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, recursive: true);
            }
        }
        catch
        {
        }
    }

    private ProfileLifecycle CreateLifecycle(
        IPbiExecutableInspector? inspector = null,
        IProcessStarter? starter = null) =>
        new(_baseDir, _cwdDir, inspector, starter);

    private ProfileLifecycle ReadyLifecycle(
        StubExecutableInspector inspector,
        RecordingProcessStarter? starter = null)
    {
        inspector.Pass(StubPath);
        var lifecycle = CreateLifecycle(inspector, starter);
        lifecycle.Load();
        lifecycle.Settings.UserProfilePath = _profilesRoot;
        Assert.True(lifecycle.TryRegisterVersion(StubPath, out VersionRegistryErrorKind? error));
        Assert.Null(error);
        return lifecycle;
    }

    private static Profile AddProfile(ProfileLifecycle lifecycle, string name = "Launch")
    {
        var profile = new Profile
        {
            Id = Guid.Parse("55555555-5555-5555-5555-555555555555"),
            Name = name,
            Order = 1,
            AvatarColor = "#4F6BED"
        };
        lifecycle.Profiles.Add(profile);
        return profile;
    }

    [Fact]
    public void SaveSettings_persists_version_registry_and_default_without_power_bi_path()
    {
        var inspector = new StubExecutableInspector();
        inspector.Pass(StubPath);
        var lifecycle = CreateLifecycle(inspector);
        lifecycle.Load();
        lifecycle.Settings.UserProfilePath = _profilesRoot;
        Assert.True(lifecycle.TryRegisterVersion(StubPath, out _));
        lifecycle.SaveSettings();

        string json = File.ReadAllText(lifecycle.SettingsFilePath);
        Assert.DoesNotContain("PowerBIPath", json, StringComparison.Ordinal);

        var reloaded = CreateLifecycle(inspector);
        reloaded.Load();

        Assert.Single(reloaded.Settings.Versions);
        Assert.Equal(StubPath, reloaded.Settings.Versions[0].Path);
        Assert.Equal(reloaded.Settings.Versions[0].Id, reloaded.Settings.DefaultVersionId);
        Assert.Equal(PbiVersionKind.Desktop, reloaded.Settings.Versions[0].Kind);
        Assert.Equal("Desktop · Downloaded", reloaded.Settings.Versions[0].DisplayName);
        Assert.True(reloaded.Settings.HasCompletedSetup);
    }

    [Fact]
    public void Registering_the_same_path_updates_the_existing_version_instead_of_duplicating()
    {
        var inspector = new StubExecutableInspector();
        inspector.Pass(StubPath);
        var lifecycle = CreateLifecycle(inspector);
        lifecycle.Load();

        Assert.True(lifecycle.TryRegisterVersion(StubPath, out _));
        Guid originalId = lifecycle.Settings.Versions[0].Id;
        lifecycle.Settings.Versions[0].DisplayName = "My Desktop";

        Assert.True(lifecycle.TryRegisterVersion(StubPath, out _));

        Assert.Single(lifecycle.Settings.Versions);
        Assert.Equal(originalId, lifecycle.Settings.Versions[0].Id);
        Assert.Equal("My Desktop", lifecycle.Settings.Versions[0].DisplayName);
        Assert.Equal(StubPath, lifecycle.Settings.Versions[0].Path);
    }

    [Fact]
    public void Browse_rejects_a_file_that_fails_the_executable_bar()
    {
        var inspector = new StubExecutableInspector();
        inspector.NotPowerBi(StubPath, "notepad.exe");
        var lifecycle = CreateLifecycle(inspector);
        lifecycle.Load();

        bool registered = lifecycle.TryRegisterVersion(StubPath, out VersionRegistryErrorKind? error);

        Assert.False(registered);
        Assert.Equal(VersionRegistryErrorKind.NotPowerBiDesktop, error);
        Assert.Empty(lifecycle.Settings.Versions);
    }

    [Fact]
    public void User_can_rename_set_default_and_remove_a_registered_version()
    {
        var inspector = new StubExecutableInspector();
        inspector.Pass(StubPath);
        inspector.PassReportServer(ReportServerPath);
        var lifecycle = CreateLifecycle(inspector);
        lifecycle.Load();

        Assert.True(lifecycle.TryRegisterVersion(StubPath, out _));
        Assert.True(lifecycle.TryRegisterVersion(ReportServerPath, out _));
        PbiVersion desktop = lifecycle.Settings.Versions[0];
        PbiVersion reportServer = lifecycle.Settings.Versions[1];
        Assert.Equal(desktop.Id, lifecycle.Settings.DefaultVersionId);

        Assert.True(lifecycle.TryRenameVersion(reportServer.Id, "  SIXT RS  ", out VersionRegistryErrorKind? renameError));
        Assert.Null(renameError);
        Assert.Equal("SIXT RS", reportServer.DisplayName);

        Assert.True(lifecycle.TrySetDefaultVersion(reportServer.Id, out VersionRegistryErrorKind? defaultError));
        Assert.Null(defaultError);
        Assert.Equal(reportServer.Id, lifecycle.Settings.DefaultVersionId);

        Assert.True(lifecycle.TryRemoveVersion(reportServer.Id, out VersionRegistryErrorKind? removeError));
        Assert.Null(removeError);
        Assert.Single(lifecycle.Settings.Versions);
        Assert.Equal(desktop.Id, lifecycle.Settings.Versions[0].Id);
        Assert.Equal(desktop.Id, lifecycle.Settings.DefaultVersionId);
    }

    [Fact]
    public void Removing_the_last_version_clears_the_default()
    {
        var inspector = new StubExecutableInspector();
        inspector.Pass(StubPath);
        var lifecycle = CreateLifecycle(inspector);
        lifecycle.Load();
        Assert.True(lifecycle.TryRegisterVersion(StubPath, out _));

        Assert.True(lifecycle.TryRemoveVersion(lifecycle.Settings.Versions[0].Id, out _));

        Assert.Empty(lifecycle.Settings.Versions);
        Assert.Null(lifecycle.Settings.DefaultVersionId);
    }

    [Fact]
    public void Load_migrates_a_valid_legacy_power_bi_path_to_one_default_version()
    {
        var inspector = new StubExecutableInspector();
        inspector.Pass(StubPath);
        File.WriteAllText(
            Path.Combine(_baseDir, "settings.json"),
            JsonSerializer.Serialize(new
            {
                UserProfilePath = _profilesRoot,
                PowerBIPath = StubPath,
                MinimizeBehavior = "Minimize",
                CloseBehavior = "Close",
                HasCompletedSetup = true
            }));

        var lifecycle = CreateLifecycle(inspector);
        lifecycle.Load();

        Assert.Single(lifecycle.Settings.Versions);
        Assert.Equal(StubPath, lifecycle.Settings.Versions[0].Path);
        Assert.Equal(lifecycle.Settings.Versions[0].Id, lifecycle.Settings.DefaultVersionId);
        Assert.Null(lifecycle.Settings.PowerBIPath);

        string rewritten = File.ReadAllText(lifecycle.SettingsFilePath);
        Assert.DoesNotContain("PowerBIPath", rewritten, StringComparison.Ordinal);
        Assert.Contains("Versions", rewritten, StringComparison.Ordinal);
        Assert.True(lifecycle.Settings.HasCompletedSetup);
    }

    [Fact]
    public void Load_migrates_an_invalid_legacy_power_bi_path_to_an_empty_registry()
    {
        var inspector = new StubExecutableInspector();
        inspector.NotPowerBi(@"C:\Windows\System32\where.exe", "where.exe");
        File.WriteAllText(
            Path.Combine(_baseDir, "settings.json"),
            JsonSerializer.Serialize(new
            {
                UserProfilePath = _profilesRoot,
                PowerBIPath = @"C:\Windows\System32\where.exe",
                HasCompletedSetup = true
            }));

        var lifecycle = CreateLifecycle(inspector);
        lifecycle.Load();

        Assert.Empty(lifecycle.Settings.Versions);
        Assert.Null(lifecycle.Settings.DefaultVersionId);
        Assert.Null(lifecycle.Settings.PowerBIPath);
        Assert.True(lifecycle.Settings.HasCompletedSetup);

        var starter = new RecordingProcessStarter();
        var launchLifecycle = CreateLifecycle(inspector, starter);
        launchLifecycle.Load();
        Profile profile = AddProfile(launchLifecycle);

        ProfileLaunchResult result = launchLifecycle.TryLaunch(profile);

        Assert.False(result.Succeeded);
        Assert.Equal(0, starter.StartCount);
        Assert.Equal(ProfileLaunchErrorKind.NoDefaultVersion, result.ErrorKind);
        Assert.Equal(ProfileLaunchRemediation.Settings, result.Remediation);
    }

    [Fact]
    public void CollectSettingsSaveErrors_requires_a_default_version()
    {
        var lifecycle = CreateLifecycle(new StubExecutableInspector());
        lifecycle.Load();

        IReadOnlyList<SettingsIssue> errors = lifecycle.CollectSettingsSaveErrors(_profilesRoot);

        Assert.Contains(errors, issue => issue.Kind == SettingsIssueKind.DefaultVersionRequired);
    }

    [Fact]
    public void CollectSettingsSaveErrors_accepts_a_default_version_and_existing_profiles_folder()
    {
        var inspector = new StubExecutableInspector();
        ProfileLifecycle lifecycle = ReadyLifecycle(inspector);

        IReadOnlyList<SettingsIssue> errors = lifecycle.CollectSettingsSaveErrors(_profilesRoot);

        Assert.Empty(errors);
    }

    [Fact]
    public void Profile_with_no_explicit_version_launches_the_default_version()
    {
        var inspector = new StubExecutableInspector();
        var starter = new RecordingProcessStarter();
        ProfileLifecycle lifecycle = ReadyLifecycle(inspector, starter);
        Profile profile = AddProfile(lifecycle);
        var paths = ProfilePaths.ForProfile(_profilesRoot, profile.Id);

        string beforeUserProfile = Environment.GetEnvironmentVariable("USERPROFILE") ?? "";
        ProfileLaunchResult result = lifecycle.TryLaunch(profile);

        Assert.True(result.Succeeded);
        Assert.Equal(1, starter.StartCount);
        Assert.NotNull(starter.Last);
        Assert.Equal(StubPath, starter.Last!.FileName);
        Assert.Equal("", starter.Last.Arguments);
        Assert.False(starter.Last.UseShellExecute);
        Assert.Equal(Path.GetDirectoryName(StubPath), starter.Last.WorkingDirectory);
        Assert.Equal(paths.Root, starter.Last.EnvironmentVariables["USERPROFILE"]);
        Assert.Equal(paths.LocalAppData, starter.Last.EnvironmentVariables["LOCALAPPDATA"]);
        Assert.Equal(paths.RoamingAppData, starter.Last.EnvironmentVariables["APPDATA"]);
        Assert.Equal(paths.Temp, starter.Last.EnvironmentVariables["TEMP"]);
        Assert.Equal(paths.Temp, starter.Last.EnvironmentVariables["TMP"]);
        Assert.Equal(beforeUserProfile, Environment.GetEnvironmentVariable("USERPROFILE"));
        Assert.True(Directory.Exists(paths.Root));
    }

    [Fact]
    public void Launch_fails_before_start_when_the_default_version_file_is_missing()
    {
        var inspector = new StubExecutableInspector();
        var starter = new RecordingProcessStarter();
        ProfileLifecycle lifecycle = ReadyLifecycle(inspector, starter);
        inspector.Missing(StubPath);
        Profile profile = AddProfile(lifecycle);

        ProfileLaunchResult result = lifecycle.TryLaunch(profile);

        Assert.False(result.Succeeded);
        Assert.Equal(0, starter.StartCount);
        Assert.Equal(ProfileLaunchErrorKind.MissingFile, result.ErrorKind);
        Assert.Equal(ProfileLaunchRemediation.Settings, result.Remediation);
    }

    [Fact]
    public void Launch_fails_before_start_when_the_file_fails_the_executable_bar()
    {
        var inspector = new StubExecutableInspector();
        var starter = new RecordingProcessStarter();
        ProfileLifecycle lifecycle = ReadyLifecycle(inspector, starter);
        inspector.NotPowerBi(StubPath);
        Profile profile = AddProfile(lifecycle);

        ProfileLaunchResult result = lifecycle.TryLaunch(profile);

        Assert.False(result.Succeeded);
        Assert.Equal(0, starter.StartCount);
        Assert.Equal(ProfileLaunchErrorKind.NotPowerBiDesktop, result.ErrorKind);
        Assert.Equal(ProfileLaunchRemediation.Settings, result.Remediation);
    }

    [Fact]
    public void Launch_fails_when_the_registry_is_empty()
    {
        var starter = new RecordingProcessStarter();
        var lifecycle = CreateLifecycle(new StubExecutableInspector(), starter);
        lifecycle.Load();
        lifecycle.Settings.UserProfilePath = _profilesRoot;
        Profile profile = AddProfile(lifecycle);

        ProfileLaunchResult result = lifecycle.TryLaunch(profile);

        Assert.False(result.Succeeded);
        Assert.Equal(0, starter.StartCount);
        Assert.Equal(ProfileLaunchErrorKind.NoDefaultVersion, result.ErrorKind);
        Assert.Equal(ProfileLaunchRemediation.Settings, result.Remediation);
    }

    [Fact]
    public void Launch_fails_when_the_default_version_id_is_missing()
    {
        var inspector = new StubExecutableInspector();
        var starter = new RecordingProcessStarter();
        ProfileLifecycle lifecycle = ReadyLifecycle(inspector, starter);
        lifecycle.Settings.DefaultVersionId = null;
        Profile profile = AddProfile(lifecycle);

        ProfileLaunchResult result = lifecycle.TryLaunch(profile);

        Assert.False(result.Succeeded);
        Assert.Equal(0, starter.StartCount);
        Assert.Equal(ProfileLaunchErrorKind.NoDefaultVersion, result.ErrorKind);
    }

    [Fact]
    public void Launch_fails_when_an_explicit_version_id_is_dangling()
    {
        var inspector = new StubExecutableInspector();
        var starter = new RecordingProcessStarter();
        ProfileLifecycle lifecycle = ReadyLifecycle(inspector, starter);
        Profile profile = AddProfile(lifecycle);
        profile.VersionId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");

        ProfileLaunchResult result = lifecycle.TryLaunch(profile);

        Assert.False(result.Succeeded);
        Assert.Equal(0, starter.StartCount);
        Assert.Equal(ProfileLaunchErrorKind.DanglingVersion, result.ErrorKind);
        Assert.Equal(ProfileLaunchRemediation.EditProfile, result.Remediation);
    }

    [Fact]
    public void Launch_blocks_a_default_microsoft_store_version_with_settings_remediation()
    {
        var inspector = new StubExecutableInspector();
        inspector.Pass(StorePath);
        var starter = new RecordingProcessStarter();
        var lifecycle = CreateLifecycle(inspector, starter);
        lifecycle.Load();
        lifecycle.Settings.UserProfilePath = _profilesRoot;
        PbiVersion store = SeedStoreVersion(lifecycle);
        lifecycle.Settings.DefaultVersionId = store.Id;
        Profile profile = AddProfile(lifecycle);

        ProfileLaunchResult result = lifecycle.TryLaunch(profile);

        Assert.False(result.Succeeded);
        Assert.Equal(0, starter.StartCount);
        Assert.Equal(ProfileLaunchErrorKind.MicrosoftStoreVersion, result.ErrorKind);
        Assert.Equal(ProfileLaunchRemediation.Settings, result.Remediation);
    }

    [Fact]
    public void Launch_blocks_a_pinned_microsoft_store_version_with_edit_profile_remediation()
    {
        var inspector = new StubExecutableInspector();
        inspector.Pass(StubPath);
        inspector.Pass(StorePath);
        var starter = new RecordingProcessStarter();
        var lifecycle = CreateLifecycle(inspector, starter);
        lifecycle.Load();
        lifecycle.Settings.UserProfilePath = _profilesRoot;
        Assert.True(lifecycle.TryRegisterVersion(StubPath, out _));
        PbiVersion store = SeedStoreVersion(lifecycle);
        Profile profile = AddProfile(lifecycle);
        profile.VersionId = store.Id;

        ProfileLaunchResult result = lifecycle.TryLaunch(profile);

        Assert.False(result.Succeeded);
        Assert.Equal(0, starter.StartCount);
        Assert.Equal(ProfileLaunchErrorKind.MicrosoftStoreVersion, result.ErrorKind);
        Assert.Equal(ProfileLaunchRemediation.EditProfile, result.Remediation);
    }

    [Fact]
    public void Browse_rejects_a_microsoft_store_version()
    {
        var inspector = new StubExecutableInspector();
        inspector.Pass(StorePath);
        var lifecycle = CreateLifecycle(inspector);
        lifecycle.Load();

        bool registered = lifecycle.TryRegisterVersion(StorePath, out VersionRegistryErrorKind? error);

        Assert.False(registered);
        Assert.Equal(VersionRegistryErrorKind.MicrosoftStoreVersion, error);
        Assert.Empty(lifecycle.Settings.Versions);
    }

    [Fact]
    public void ConfirmScan_never_registers_and_removes_a_microsoft_store_version()
    {
        var inspector = new StubExecutableInspector();
        inspector.Pass(StubPath);
        inspector.Pass(StorePath);
        var lifecycle = CreateLifecycle(inspector);
        lifecycle.Load();
        PbiVersion store = SeedStoreVersion(lifecycle);
        Guid? defaultVersionId = store.Id;
        var found = new[]
        {
            new ScanAcceptedVersion { Path = StubPath, DisplayName = "Desktop · Downloaded" },
            new ScanAcceptedVersion { Path = StorePath, DisplayName = "Desktop · Microsoft Store", IsMicrosoftStore = true }
        };

        lifecycle.ConfirmScan(
            found,
            new HashSet<string>(StringComparer.OrdinalIgnoreCase) { StubPath, StorePath },
            lifecycle.Settings.Versions,
            ref defaultVersionId);

        PbiVersion only = Assert.Single(lifecycle.Settings.Versions);
        Assert.Equal(StubPath, only.Path);
        Assert.Equal(only.Id, defaultVersionId);
    }

    private static PbiVersion SeedStoreVersion(ProfileLifecycle lifecycle)
    {
        var store = new PbiVersion
        {
            Id = Guid.NewGuid(),
            DisplayName = "Desktop · Microsoft Store",
            Path = StorePath,
            Kind = PbiVersionKind.Desktop
        };
        lifecycle.Settings.Versions.Add(store);
        return store;
    }

    [Fact]
    public void Launch_reports_process_start_errors_with_settings_remediation()
    {
        var inspector = new StubExecutableInspector();
        var starter = new RecordingProcessStarter
        {
            ThrowOnStart = new InvalidOperationException("access denied")
        };
        ProfileLifecycle lifecycle = ReadyLifecycle(inspector, starter);
        Profile profile = AddProfile(lifecycle);

        ProfileLaunchResult result = lifecycle.TryLaunch(profile);

        Assert.False(result.Succeeded);
        Assert.Equal(ProfileLaunchErrorKind.ProcessStart, result.ErrorKind);
        Assert.Equal("access denied", result.OsDetail);
        Assert.Equal(ProfileLaunchRemediation.Settings, result.Remediation);
    }

    [Fact]
    public void New_profiles_follow_the_default_version()
    {
        var inspector = new StubExecutableInspector();
        ProfileLifecycle lifecycle = ReadyLifecycle(inspector);

        Profile profile = new()
        {
            Name = "Launch",
            AvatarColor = "#111111",
            ShowOnTray = true
        };
        Assert.True(lifecycle.TryAddProfile(profile, out _));

        Assert.Null(profile.VersionId);
    }

    [Fact]
    public void SaveProfiles_persists_an_explicit_version_id()
    {
        var inspector = new StubExecutableInspector();
        ProfileLifecycle lifecycle = ReadyLifecycle(inspector);
        Profile profile = AddProfile(lifecycle);
        profile.VersionId = lifecycle.Settings.Versions[0].Id;
        lifecycle.SaveProfiles();

        var reloaded = CreateLifecycle(inspector);
        reloaded.Load();

        Assert.Equal(profile.VersionId, reloaded.Profiles[0].VersionId);
    }

    [Fact]
    public void Explicit_version_launches_that_version_not_the_default()
    {
        var inspector = new StubExecutableInspector();
        inspector.Pass(StubPath);
        inspector.PassReportServer(ReportServerPath);
        var starter = new RecordingProcessStarter();
        var lifecycle = CreateLifecycle(inspector, starter);
        lifecycle.Load();
        lifecycle.Settings.UserProfilePath = _profilesRoot;
        Assert.True(lifecycle.TryRegisterVersion(StubPath, out _));
        Assert.True(lifecycle.TryRegisterVersion(ReportServerPath, out _));
        Assert.Equal(lifecycle.Settings.Versions[0].Id, lifecycle.Settings.DefaultVersionId);

        Profile profile = AddProfile(lifecycle);
        profile.VersionId = lifecycle.Settings.Versions[1].Id;

        ProfileLaunchResult result = lifecycle.TryLaunch(profile);

        Assert.True(result.Succeeded);
        Assert.Equal(ReportServerPath, starter.Last!.FileName);
        Assert.Equal("", starter.Last.Arguments);
        Assert.Equal(Path.GetDirectoryName(ReportServerPath), starter.Last.WorkingDirectory);
    }

    [Fact]
    public void Changing_the_default_version_affects_an_unpinned_profile()
    {
        var inspector = new StubExecutableInspector();
        inspector.Pass(StubPath);
        inspector.PassReportServer(ReportServerPath);
        var starter = new RecordingProcessStarter();
        var lifecycle = CreateLifecycle(inspector, starter);
        lifecycle.Load();
        lifecycle.Settings.UserProfilePath = _profilesRoot;
        Assert.True(lifecycle.TryRegisterVersion(StubPath, out _));
        Assert.True(lifecycle.TryRegisterVersion(ReportServerPath, out _));
        Profile profile = AddProfile(lifecycle);
        Assert.Null(profile.VersionId);

        Assert.True(lifecycle.TrySetDefaultVersion(lifecycle.Settings.Versions[1].Id, out _));
        ProfileLaunchResult result = lifecycle.TryLaunch(profile);

        Assert.True(result.Succeeded);
        Assert.Equal(ReportServerPath, starter.Last!.FileName);
    }
}
