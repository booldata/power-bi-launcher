using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace PowerBILauncher.Tests;

public class ProfileLifecycleTests : IDisposable
{
    private readonly string _baseDir;
    private readonly string _cwdDir;

    public ProfileLifecycleTests()
    {
        _baseDir = Path.Combine(Path.GetTempPath(), "pbi-lifecycle-" + Guid.NewGuid().ToString("N"));
        _cwdDir = Path.Combine(Path.GetTempPath(), "pbi-cwd-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_baseDir);
        Directory.CreateDirectory(_cwdDir);
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

    private ProfileLifecycle CreateLifecycle() => new(_baseDir, _cwdDir);

    [Fact]
    public void Load_migrates_json_from_cwd_when_base_missing()
    {
        var settings = new Setting
        {
            HasCompletedSetup = true,
            UserProfilePath = Path.Combine(_baseDir, "Profiles")
        };
        File.WriteAllText(
            Path.Combine(_cwdDir, "settings.json"),
            JsonSerializer.Serialize(settings, ProfileLifecycle.SettingsJsonOptions));

        var profiles = new List<Profile>
        {
            new()
            {
                Id = Guid.Parse("11111111-1111-1111-1111-111111111111"),
                Name = "Client A",
                Order = 1,
                AvatarColor = "#4F6BED"
            }
        };
        File.WriteAllText(
            Path.Combine(_cwdDir, "profiles.json"),
            JsonSerializer.Serialize(profiles));

        var lifecycle = CreateLifecycle();
        lifecycle.Load();

        Assert.True(File.Exists(Path.Combine(_baseDir, "settings.json")));
        Assert.True(File.Exists(Path.Combine(_baseDir, "profiles.json")));
        Assert.True(lifecycle.Settings.HasCompletedSetup);
        Assert.Single(lifecycle.Profiles);
        Assert.Equal("Client A", lifecycle.Profiles[0].Name);
    }

    [Fact]
    public void SaveSettings_and_SaveProfiles_round_trip_beside_base_directory()
    {
        string profilesRoot = Path.Combine(_baseDir, "Profiles");
        Directory.CreateDirectory(profilesRoot);

        var lifecycle = CreateLifecycle();
        lifecycle.Load();
        lifecycle.Settings.HasCompletedSetup = true;
        lifecycle.Settings.UserProfilePath = profilesRoot;
        lifecycle.Settings.MinimizeBehavior = MinimizeButtonBehavior.SendToTray;
        lifecycle.Settings.CloseBehavior = CloseButtonBehavior.SendToTray;
        lifecycle.SaveSettings();

        lifecycle.Profiles.Add(new Profile
        {
            Id = Guid.Parse("22222222-2222-2222-2222-222222222222"),
            Name = "Work",
            Order = 1,
            AvatarColor = "#107C10"
        });
        lifecycle.SaveProfiles();

        var reloaded = CreateLifecycle();
        reloaded.Load();

        Assert.True(reloaded.Settings.HasCompletedSetup);
        Assert.Equal(profilesRoot, reloaded.Settings.UserProfilePath);
        Assert.Equal(MinimizeButtonBehavior.SendToTray, reloaded.Settings.MinimizeBehavior);
        Assert.Equal(CloseButtonBehavior.SendToTray, reloaded.Settings.CloseBehavior);
        Assert.Single(reloaded.Profiles);
        Assert.Equal("Work", reloaded.Profiles[0].Name);
        Assert.Equal("#107C10", reloaded.Profiles[0].AvatarColor);
    }

    [Fact]
    public void SaveSettings_sets_HasCompletedSetup_on_first_save()
    {
        string profilesRoot = Path.Combine(_baseDir, "Profiles");
        Directory.CreateDirectory(profilesRoot);

        var lifecycle = CreateLifecycle();
        lifecycle.Load();

        Assert.False(lifecycle.Settings.HasCompletedSetup);

        lifecycle.Settings.UserProfilePath = profilesRoot;
        lifecycle.SaveSettings();

        Assert.True(lifecycle.Settings.HasCompletedSetup);

        var reloaded = CreateLifecycle();
        reloaded.Load();

        Assert.True(reloaded.Settings.HasCompletedSetup);
    }

    [Fact]
    public void EnsureProfileDirectories_creates_isolated_environment_folders()
    {
        string profilesRoot = Path.Combine(_baseDir, "Profiles");
        Directory.CreateDirectory(profilesRoot);

        var lifecycle = CreateLifecycle();
        lifecycle.Load();
        lifecycle.Settings.UserProfilePath = profilesRoot;

        var profile = new Profile
        {
            Id = Guid.Parse("33333333-3333-3333-3333-333333333333"),
            Name = "Env",
            Order = 1,
            AvatarColor = "#4F6BED"
        };
        lifecycle.Profiles.Add(profile);

        var paths = lifecycle.EnsureProfileDirectories(profile);

        Assert.True(Directory.Exists(paths.Root));
        Assert.True(Directory.Exists(paths.LocalAppData));
        Assert.True(Directory.Exists(paths.RoamingAppData));
        Assert.True(Directory.Exists(paths.Temp));
        Assert.True(Directory.Exists(Path.Combine(paths.LocalAppData, "Microsoft", "EdgeWebView")));
        Assert.True(Directory.Exists(Path.Combine(paths.Root, "Desktop")));
    }

    [Fact]
    public void TryDeleteProfileData_removes_isolated_environment_folder()
    {
        string profilesRoot = Path.Combine(_baseDir, "Profiles");
        Directory.CreateDirectory(profilesRoot);

        var lifecycle = CreateLifecycle();
        lifecycle.Load();
        lifecycle.Settings.UserProfilePath = profilesRoot;

        var profile = new Profile
        {
            Id = Guid.Parse("44444444-4444-4444-4444-444444444444"),
            Name = "DeleteMe",
            Order = 1,
            AvatarColor = "#4F6BED"
        };
        lifecycle.Profiles.Add(profile);
        var paths = lifecycle.EnsureProfileDirectories(profile);
        File.WriteAllText(Path.Combine(paths.Root, "marker.txt"), "x");

        bool deleted = lifecycle.TryDeleteProfileData(profile, out string? error);

        Assert.True(deleted);
        Assert.Null(error);
        Assert.False(Directory.Exists(paths.Root));
    }

    [Fact]
    public void TryLaunch_with_notepad_as_target_fails_before_start()
    {
        string profilesRoot = Path.Combine(_baseDir, "Profiles");
        Directory.CreateDirectory(profilesRoot);
        string notepadPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.System),
            "notepad.exe");

        var inspector = new StubExecutableInspector();
        inspector.NotPowerBi(notepadPath, "notepad.exe");
        var starter = new RecordingProcessStarter();
        var lifecycle = new ProfileLifecycle(_baseDir, _cwdDir, inspector, starter);
        lifecycle.Load();
        lifecycle.Settings.UserProfilePath = profilesRoot;
        lifecycle.Settings.Versions.Add(new PbiVersion
        {
            Id = Guid.Parse("88888888-8888-8888-8888-888888888888"),
            DisplayName = "Notepad",
            Path = notepadPath,
            Kind = PbiVersionKind.Desktop
        });
        lifecycle.Settings.DefaultVersionId = lifecycle.Settings.Versions[0].Id;

        var profile = new Profile
        {
            Id = Guid.Parse("55555555-5555-5555-5555-555555555555"),
            Name = "Launch",
            Order = 1,
            AvatarColor = "#4F6BED"
        };
        lifecycle.Profiles.Add(profile);

        ProfileLaunchResult result = lifecycle.TryLaunch(profile);

        Assert.False(result.Succeeded);
        Assert.Equal(0, starter.StartCount);
        Assert.Equal(ProfileLaunchErrorKind.NotPowerBiDesktop, result.ErrorKind);
    }

    [Fact]
    public void Load_applies_legacy_profile_migrations_and_rewrites_when_dirty()
    {
        var profiles = new List<Profile>
        {
            new()
            {
                Id = Guid.Parse("66666666-6666-6666-6666-666666666666"),
                Name = "Legacy",
                Order = 1,
                CardColor = "#FFFFFF",
                AvatarColor = "",
                IconPath = Path.Combine(_baseDir, "pbi.png")
            }
        };
        File.WriteAllText(
            Path.Combine(_baseDir, "profiles.json"),
            JsonSerializer.Serialize(profiles));

        var lifecycle = CreateLifecycle();
        lifecycle.Load(pickAvatarColor: () => "#CAFE00");

        Assert.Equal("", lifecycle.Profiles[0].CardColor);
        Assert.Equal("#CAFE00", lifecycle.Profiles[0].AvatarColor);
        Assert.Equal("", lifecycle.Profiles[0].IconPath);

        string rewritten = File.ReadAllText(Path.Combine(_baseDir, "profiles.json"));
        var fromDisk = JsonSerializer.Deserialize<List<Profile>>(rewritten);
        Assert.NotNull(fromDisk);
        Assert.Equal("", fromDisk![0].CardColor);
        Assert.Equal("#CAFE00", fromDisk[0].AvatarColor);
        Assert.Equal("", fromDisk[0].IconPath);
    }

    [Fact]
    public void SaveProfiles_does_not_reapply_legacy_migrations()
    {
        var lifecycle = CreateLifecycle();
        lifecycle.Load();
        lifecycle.Profiles.Add(new Profile
        {
            Id = Guid.Parse("77777777-7777-7777-7777-777777777777"),
            Name = "Clean",
            Order = 1,
            CardColor = "",
            AvatarColor = "#4F6BED",
            IconPath = ""
        });
        lifecycle.SaveProfiles();

        // Manually introduce a legacy-looking value after load; SaveProfiles must persist it as-is
        // (migrations run only on Load, not on every CRUD persist).
        lifecycle.Profiles[0].CardColor = "#FFFFFF";
        lifecycle.SaveProfiles();

        string json = File.ReadAllText(Path.Combine(_baseDir, "profiles.json"));
        var fromDisk = JsonSerializer.Deserialize<List<Profile>>(json);
        Assert.NotNull(fromDisk);
        Assert.Equal("#FFFFFF", fromDisk![0].CardColor);
    }

    [Fact]
    public void Load_marks_missing_HasCompletedSetup_as_completed_for_existing_installs()
    {
        var settings = new Setting
        {
            UserProfilePath = Path.Combine(_baseDir, "Profiles")
        };
        string json = JsonSerializer.Serialize(new
        {
            settings.UserProfilePath,
            MinimizeBehavior = "Minimize",
            CloseBehavior = "Close"
        });
        Assert.DoesNotContain("HasCompletedSetup", json, StringComparison.Ordinal);
        File.WriteAllText(Path.Combine(_baseDir, "settings.json"), json);

        var lifecycle = CreateLifecycle();
        lifecycle.Load();

        Assert.True(lifecycle.Settings.HasCompletedSetup);
    }

    [Fact]
    public void AddProfile_assigns_next_launch_order_persists_once_and_creates_folder()
    {
        string profilesRoot = Path.Combine(_baseDir, "Profiles");
        Directory.CreateDirectory(profilesRoot);

        var lifecycle = CreateLifecycle();
        lifecycle.Load();
        lifecycle.Settings.UserProfilePath = profilesRoot;

        Profile existing = new()
        {
            Name = "Acme",
            Order = 1,
            AvatarColor = "#111111",
            ShowOnTray = true
        };
        Assert.True(lifecycle.TryAddProfile(existing, out _));

        Profile added = new()
        {
            Name = "Contoso",
            AvatarColor = "#222222",
            ShowOnTray = true
        };
        Assert.True(lifecycle.TryAddProfile(added, out _));

        Assert.Equal(1, existing.Order);
        Assert.Equal(2, added.Order);
        Assert.Equal(2, lifecycle.Profiles.Count);
        Assert.True(Directory.Exists(ProfilePaths.ForProfile(profilesRoot, added.Id).Root));
        Assert.True(File.Exists(Path.Combine(_baseDir, "profiles.json")));

        var reloaded = CreateLifecycle();
        reloaded.Load();
        Assert.Equal(2, reloaded.Profiles.Count);
        Assert.Equal("Contoso", reloaded.Profiles.Single(profile => profile.Order == 2).Name);
    }

    [Fact]
    public void Unsaved_draft_does_not_create_profile_folder_or_profiles_json()
    {
        string profilesRoot = Path.Combine(_baseDir, "Profiles");
        Directory.CreateDirectory(profilesRoot);
        string[] before = Directory.GetFileSystemEntries(profilesRoot);

        var lifecycle = CreateLifecycle();
        lifecycle.Load();
        lifecycle.Settings.UserProfilePath = profilesRoot;

        var draft = new Profile
        {
            Name = "Draft",
            AvatarColor = "#4F6BED",
            ShowOnTray = true
        };

        Assert.Empty(lifecycle.Profiles);
        Assert.False(File.Exists(Path.Combine(_baseDir, "profiles.json")));
        Assert.Equal(before, Directory.GetFileSystemEntries(profilesRoot));
        Assert.False(Directory.Exists(ProfilePaths.ForProfile(profilesRoot, draft.Id).Root));
    }

    [Fact]
    public void TryRemoveProfile_removes_from_list_deletes_folders_and_persists()
    {
        string profilesRoot = Path.Combine(_baseDir, "Profiles");
        Directory.CreateDirectory(profilesRoot);

        var lifecycle = CreateLifecycle();
        lifecycle.Load();
        lifecycle.Settings.UserProfilePath = profilesRoot;

        Profile profile = new()
        {
            Name = "Remove me",
            AvatarColor = "#333333",
            ShowOnTray = true
        };
        Assert.True(lifecycle.TryAddProfile(profile, out _));
        string root = ProfilePaths.ForProfile(profilesRoot, profile.Id).Root;
        Assert.True(Directory.Exists(root));

        ProfileRemoveResult result = lifecycle.TryRemoveProfile(profile);

        Assert.True(result.Removed);
        Assert.Null(result.CredentialError);
        Assert.Null(result.DataError);
        Assert.Empty(lifecycle.Profiles);
        Assert.False(Directory.Exists(root));

        var reloaded = CreateLifecycle();
        reloaded.Load();
        Assert.Empty(reloaded.Profiles);
    }
}
