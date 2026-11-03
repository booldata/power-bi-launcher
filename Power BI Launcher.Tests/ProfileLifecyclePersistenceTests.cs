using System.Text.Json;

namespace PowerBILauncher.Tests;

public class ProfileLifecyclePersistenceTests : IDisposable
{
    private readonly string _baseDir;
    private readonly string _profilesRoot;

    public ProfileLifecyclePersistenceTests()
    {
        _baseDir = Path.Combine(Path.GetTempPath(), "pbi-persist-" + Guid.NewGuid().ToString("N").Substring(0, 8));
        _profilesRoot = Path.Combine(_baseDir, "Profiles");
        Directory.CreateDirectory(_profilesRoot);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_baseDir))
            {
                foreach (string file in Directory.GetFiles(_baseDir, "*", SearchOption.AllDirectories))
                {
                    File.SetAttributes(file, FileAttributes.Normal);
                }

                Directory.Delete(_baseDir, recursive: true);
            }
        }
        catch
        {
        }
    }

    private ProfileLifecycle LoadedLifecycle(Action<TimeSpan>? lockRetryWait = null)
    {
        var lifecycle = new ProfileLifecycle(
            _baseDir, _baseDir, new StubExecutableInspector(), new RecordingProcessStarter(), lockRetryWait: lockRetryWait);
        lifecycle.Load();
        lifecycle.Settings.UserProfilePath = _profilesRoot;
        return lifecycle;
    }

    private static Profile NewProfile(string name) =>
        new()
        {
            Name = name,
            AvatarColor = "#4F6BED",
            ShowOnTray = true
        };

    private void BlockProfilesFile(ProfileLifecycle lifecycle)
    {
        if (File.Exists(lifecycle.ProfilesFilePath))
        {
            File.Delete(lifecycle.ProfilesFilePath);
        }

        Directory.CreateDirectory(lifecycle.ProfilesFilePath);
    }

    [Fact]
    public void TrySaveProfiles_reports_a_failed_write_instead_of_throwing()
    {
        ProfileLifecycle lifecycle = LoadedLifecycle();
        lifecycle.Profiles.Add(NewProfile("Work"));
        BlockProfilesFile(lifecycle);

        bool saved = lifecycle.TrySaveProfiles(out string? osDetail);

        Assert.False(saved);
        Assert.False(string.IsNullOrEmpty(osDetail));
    }

    [Fact]
    public void TryAddProfile_leaves_the_list_unchanged_when_profiles_json_cannot_be_written()
    {
        ProfileLifecycle lifecycle = LoadedLifecycle();
        BlockProfilesFile(lifecycle);
        Profile draft = NewProfile("Draft");

        bool added = lifecycle.TryAddProfile(draft, out string? osDetail);

        Assert.False(added);
        Assert.False(string.IsNullOrEmpty(osDetail));
        Assert.Empty(lifecycle.Profiles);
        Assert.False(Directory.Exists(ProfilePaths.ForProfile(_profilesRoot, draft.Id).Root));
    }

    [Fact]
    public void TryRemoveProfile_keeps_the_profile_and_its_folder_when_profiles_json_cannot_be_written()
    {
        ProfileLifecycle lifecycle = LoadedLifecycle();
        Profile first = NewProfile("First");
        Profile second = NewProfile("Second");
        Assert.True(lifecycle.TryAddProfile(first, out _));
        Assert.True(lifecycle.TryAddProfile(second, out _));
        string root = ProfilePaths.ForProfile(_profilesRoot, first.Id).Root;
        BlockProfilesFile(lifecycle);

        ProfileRemoveResult result = lifecycle.TryRemoveProfile(first);

        Assert.False(result.Removed);
        Assert.False(string.IsNullOrEmpty(result.SaveError));
        Assert.Null(result.CredentialError);
        Assert.Equal(new[] { first, second }, lifecycle.Profiles);
        Assert.True(Directory.Exists(root));
    }

    [Fact]
    public void TryRemoveProfile_keeps_the_saved_password_when_profiles_json_cannot_be_written()
    {
        var store = new MemoryCredentialStore();
        var lifecycle = new ProfileLifecycle(_baseDir, _baseDir, new StubExecutableInspector(), new RecordingProcessStarter(), store);
        lifecycle.Load();
        lifecycle.Settings.UserProfilePath = _profilesRoot;
        Profile profile = NewProfile("Work");
        Assert.True(lifecycle.TryAddProfile(profile, out _));
        Assert.True(lifecycle.TryPersistWindowsNetworkIdentity(profile, true, @"CONTOSO\JSmith", "secret", out _, out _));
        BlockProfilesFile(lifecycle);

        ProfileRemoveResult result = lifecycle.TryRemoveProfile(profile);

        Assert.False(result.Removed);
        Assert.Contains(profile, lifecycle.Profiles);
        Assert.Equal("secret", store.Passwords[profile.Id]);
        Assert.Equal(0, store.DeleteCount);
    }

    [Fact]
    public void TryRemoveProfile_reports_a_folder_that_could_not_be_deleted_and_keeps_the_profile_removed()
    {
        ProfileLifecycle lifecycle = LoadedLifecycle();
        Profile profile = NewProfile("Work");
        Assert.True(lifecycle.TryAddProfile(profile, out _));
        string root = ProfilePaths.ForProfile(_profilesRoot, profile.Id).Root;
        string lockedFile = Path.Combine(root, "in-use.txt");
        File.WriteAllText(lockedFile, "busy");

        ProfileRemoveResult result;
        using (new FileStream(lockedFile, FileMode.Open, FileAccess.Read, FileShare.None))
        {
            result = lifecycle.TryRemoveProfile(profile);
        }

        Assert.True(result.Removed);
        Assert.False(string.IsNullOrEmpty(result.DataError));
        Assert.Empty(lifecycle.Profiles);
        Assert.DoesNotContain(profile.Id.ToString(), File.ReadAllText(lifecycle.ProfilesFilePath));
    }

    [Fact]
    public void TrySaveSettings_keeps_setup_incomplete_when_settings_json_cannot_be_written()
    {
        ProfileLifecycle lifecycle = LoadedLifecycle();
        Directory.CreateDirectory(lifecycle.SettingsFilePath);

        bool saved = lifecycle.TrySaveSettings(out string? osDetail);

        Assert.False(saved);
        Assert.False(string.IsNullOrEmpty(osDetail));
        Assert.False(lifecycle.Settings.HasCompletedSetup);
    }

    [Fact]
    public void SaveProfiles_and_SaveSettings_replace_the_files_without_leaving_a_temp_file()
    {
        ProfileLifecycle lifecycle = LoadedLifecycle();
        lifecycle.Profiles.Add(NewProfile("First"));
        lifecycle.SaveProfiles();
        lifecycle.SaveSettings();
        lifecycle.Profiles[0].Name = "Renamed";
        lifecycle.SaveProfiles();

        var fromDisk = JsonSerializer.Deserialize<List<Profile>>(File.ReadAllText(lifecycle.ProfilesFilePath));
        Assert.Equal("Renamed", Assert.Single(fromDisk!).Name);
        Assert.True(File.Exists(lifecycle.SettingsFilePath));
        Assert.Empty(Directory.GetFiles(_baseDir, "*.tmp"));
    }

    [Fact]
    public void Failed_profiles_write_keeps_the_previous_file_intact_and_cleans_up()
    {
        ProfileLifecycle lifecycle = LoadedLifecycle();
        lifecycle.Profiles.Add(NewProfile("Saved"));
        lifecycle.SaveProfiles();
        string before = File.ReadAllText(lifecycle.ProfilesFilePath);
        File.SetAttributes(lifecycle.ProfilesFilePath, FileAttributes.ReadOnly);
        lifecycle.Profiles[0].Name = "Unsaved";

        Assert.False(lifecycle.TrySaveProfiles(out _));

        Assert.Equal(before, File.ReadAllText(lifecycle.ProfilesFilePath));
        Assert.Empty(Directory.GetFiles(_baseDir, "*.tmp"));
    }

    [Fact]
    public void Save_retries_a_profiles_file_another_program_briefly_holds()
    {
        FileStream? holder = null;
        int waits = 0;
        ProfileLifecycle lifecycle = LoadedLifecycle(_ =>
        {
            waits++;
            holder?.Dispose();
        });
        lifecycle.Profiles.Add(NewProfile("Saved"));
        lifecycle.SaveProfiles();
        lifecycle.Profiles[0].Name = "Renamed";

        bool saved;
        string? osDetail;
        using (holder = new FileStream(lifecycle.ProfilesFilePath, FileMode.Open, FileAccess.Read, FileShare.None))
        {
            saved = lifecycle.TrySaveProfiles(out osDetail);
        }

        Assert.Equal(1, waits);
        Assert.True(saved, osDetail);
        var fromDisk = JsonSerializer.Deserialize<List<Profile>>(File.ReadAllText(lifecycle.ProfilesFilePath));
        Assert.Equal("Renamed", Assert.Single(fromDisk!).Name);
        Assert.Empty(Directory.GetFiles(_baseDir, "*.tmp"));
    }

    [Fact]
    public void Save_retries_a_settings_file_another_program_briefly_holds()
    {
        FileStream? holder = null;
        int waits = 0;
        ProfileLifecycle lifecycle = LoadedLifecycle(_ =>
        {
            waits++;
            holder?.Dispose();
        });
        lifecycle.SaveSettings();

        bool saved;
        string? osDetail;
        using (holder = new FileStream(lifecycle.SettingsFilePath, FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            saved = lifecycle.TrySaveSettings(out osDetail);
        }

        Assert.Equal(1, waits);
        Assert.True(saved, osDetail);
        Assert.Empty(Directory.GetFiles(_baseDir, "*.tmp"));
    }

    [Fact]
    public void Save_gives_up_on_a_profiles_file_that_stays_held_and_keeps_it_intact()
    {
        ProfileLifecycle lifecycle = LoadedLifecycle();
        lifecycle.Profiles.Add(NewProfile("Saved"));
        lifecycle.SaveProfiles();
        string before = File.ReadAllText(lifecycle.ProfilesFilePath);
        lifecycle.Profiles[0].Name = "Unsaved";

        bool saved;
        using (new FileStream(lifecycle.ProfilesFilePath, FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            saved = lifecycle.TrySaveProfiles(out _);
        }

        Assert.False(saved);
        Assert.Equal(before, File.ReadAllText(lifecycle.ProfilesFilePath));
        Assert.Empty(Directory.GetFiles(_baseDir, "*.tmp"));
    }

    [Fact]
    public void Load_reports_nothing_when_both_files_are_readable()
    {
        ProfileLifecycle lifecycle = LoadedLifecycle();
        lifecycle.Profiles.Add(NewProfile("Work"));
        lifecycle.SaveProfiles();
        lifecycle.SaveSettings();

        var reloaded = new ProfileLifecycle(_baseDir, _baseDir, new StubExecutableInspector(), new RecordingProcessStarter());

        Assert.Empty(reloaded.Load());
        Assert.Single(reloaded.Profiles);
    }

    [Fact]
    public void Load_reports_an_unreadable_profiles_file_and_keeps_it_as_a_backup()
    {
        string profilesFile = Path.Combine(_baseDir, "profiles.json");
        File.WriteAllText(profilesFile, "[{ not json");
        var lifecycle = new ProfileLifecycle(_baseDir, _baseDir, new StubExecutableInspector(), new RecordingProcessStarter());

        IReadOnlyList<UnreadableConfigFile> unreadable = lifecycle.Load();

        UnreadableConfigFile file = Assert.Single(unreadable);
        Assert.Equal(profilesFile, file.FilePath);
        Assert.NotNull(file.BackupPath);
        Assert.Equal("[{ not json", File.ReadAllText(file.BackupPath));
        Assert.False(File.Exists(profilesFile));
        Assert.Empty(lifecycle.Profiles);
    }

    [Fact]
    public void Load_reports_an_unreadable_settings_file_and_restarts_setup()
    {
        string settingsFile = Path.Combine(_baseDir, "settings.json");
        File.WriteAllText(settingsFile, "{ broken");
        var lifecycle = new ProfileLifecycle(_baseDir, _baseDir, new StubExecutableInspector(), new RecordingProcessStarter());

        IReadOnlyList<UnreadableConfigFile> unreadable = lifecycle.Load();

        UnreadableConfigFile file = Assert.Single(unreadable);
        Assert.Equal(settingsFile, file.FilePath);
        Assert.Equal("{ broken", File.ReadAllText(file.BackupPath!));
        Assert.False(lifecycle.Settings.HasCompletedSetup);
    }

    private ProfileLifecycle NewLifecycle(Action<TimeSpan>? lockRetryWait = null) =>
        new(_baseDir, _baseDir, new StubExecutableInspector(), new RecordingProcessStarter(), lockRetryWait: lockRetryWait);

    private static string SavedProfilesJson(string name) =>
        JsonSerializer.Serialize(new List<Profile> { new() { Name = name, Order = 1, AvatarColor = "#4F6BED" } });

    [Fact]
    public void Load_retries_a_profiles_file_another_program_briefly_holds()
    {
        string profilesFile = Path.Combine(_baseDir, "profiles.json");
        File.WriteAllText(profilesFile, SavedProfilesJson("Work"));
        FileStream? holder = null;
        int waits = 0;
        ProfileLifecycle lifecycle = NewLifecycle(_ =>
        {
            waits++;
            holder?.Dispose();
        });

        IReadOnlyList<UnreadableConfigFile> unreadable;
        using (holder = new FileStream(profilesFile, FileMode.Open, FileAccess.Read, FileShare.None))
        {
            unreadable = lifecycle.Load();
        }

        Assert.Equal(1, waits);
        Assert.Empty(unreadable);
        Assert.Equal("Work", Assert.Single(lifecycle.Profiles).Name);
        Assert.True(lifecycle.TrySaveProfiles(out _));
    }

    [Fact]
    public void Load_leaves_a_profiles_file_that_stays_locked_in_place_and_refuses_to_overwrite_it()
    {
        string profilesFile = Path.Combine(_baseDir, "profiles.json");
        string original = SavedProfilesJson("Work");
        File.WriteAllText(profilesFile, original);
        ProfileLifecycle lifecycle = NewLifecycle();

        IReadOnlyList<UnreadableConfigFile> unreadable;
        using (new FileStream(profilesFile, FileMode.Open, FileAccess.Read, FileShare.None))
        {
            unreadable = lifecycle.Load();
        }

        UnreadableConfigFile file = Assert.Single(unreadable);
        Assert.Equal(profilesFile, file.FilePath);
        Assert.Equal(UnreadableConfigReason.Unavailable, file.Reason);
        Assert.Null(file.BackupPath);
        Assert.True(file.SavesBlocked);
        Assert.Empty(lifecycle.Profiles);
        Assert.Empty(Directory.GetFiles(_baseDir, "*.bak"));

        lifecycle.Settings.UserProfilePath = _profilesRoot;
        Assert.False(lifecycle.TrySaveProfiles(out string? osDetail));
        Assert.Null(osDetail);
        Assert.False(lifecycle.TryAddProfile(NewProfile("New"), out string? addDetail));
        Assert.Null(addDetail);
        Assert.Empty(lifecycle.Profiles);
        Assert.Throws<InvalidOperationException>(lifecycle.SaveProfiles);
        Assert.Equal(original, File.ReadAllText(profilesFile));
    }

    [Fact]
    public void Load_leaves_a_settings_file_that_stays_locked_in_place_without_restarting_setup()
    {
        string settingsFile = Path.Combine(_baseDir, "settings.json");
        const string original = "{ \"HasCompletedSetup\": true }";
        File.WriteAllText(settingsFile, original);
        ProfileLifecycle lifecycle = NewLifecycle();

        IReadOnlyList<UnreadableConfigFile> unreadable;
        using (new FileStream(settingsFile, FileMode.Open, FileAccess.Read, FileShare.None))
        {
            unreadable = lifecycle.Load();
        }

        UnreadableConfigFile file = Assert.Single(unreadable);
        Assert.Equal(UnreadableConfigReason.Unavailable, file.Reason);
        Assert.True(file.SavesBlocked);
        Assert.True(lifecycle.Settings.HasCompletedSetup);
        Assert.False(lifecycle.TrySaveSettings(out string? osDetail));
        Assert.Null(osDetail);
        Assert.Throws<InvalidOperationException>(lifecycle.SaveSettings);
        Assert.Equal(original, File.ReadAllText(settingsFile));
        Assert.Empty(Directory.GetFiles(_baseDir, "*.bak"));
    }

    [Fact]
    public void Load_refuses_to_overwrite_an_unparsable_profiles_file_it_could_not_set_aside()
    {
        string profilesFile = Path.Combine(_baseDir, "profiles.json");
        File.WriteAllText(profilesFile, "[{ not json");
        ProfileLifecycle lifecycle = NewLifecycle();

        IReadOnlyList<UnreadableConfigFile> unreadable;
        using (new FileStream(profilesFile, FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            unreadable = lifecycle.Load();
        }

        UnreadableConfigFile file = Assert.Single(unreadable);
        Assert.Equal(UnreadableConfigReason.Unparsable, file.Reason);
        Assert.Null(file.BackupPath);
        Assert.True(file.SavesBlocked);
        Assert.False(lifecycle.TrySaveProfiles(out string? osDetail));
        Assert.Null(osDetail);
        Assert.Equal("[{ not json", File.ReadAllText(profilesFile));
    }

    [Fact]
    public void Load_refuses_to_overwrite_an_unparsable_settings_file_it_could_not_set_aside()
    {
        string settingsFile = Path.Combine(_baseDir, "settings.json");
        File.WriteAllText(settingsFile, "{ broken");
        ProfileLifecycle lifecycle = NewLifecycle();

        IReadOnlyList<UnreadableConfigFile> unreadable;
        using (new FileStream(settingsFile, FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            unreadable = lifecycle.Load();
        }

        UnreadableConfigFile file = Assert.Single(unreadable);
        Assert.Equal(UnreadableConfigReason.Unparsable, file.Reason);
        Assert.True(file.SavesBlocked);
        Assert.True(lifecycle.Settings.HasCompletedSetup);
        Assert.False(lifecycle.TrySaveSettings(out string? osDetail));
        Assert.Null(osDetail);
        Assert.Equal("{ broken", File.ReadAllText(settingsFile));
    }

    [Fact]
    public void Unparsable_settings_mentioning_the_legacy_path_are_not_rewritten_after_backup()
    {
        string settingsFile = Path.Combine(_baseDir, "settings.json");
        File.WriteAllText(settingsFile, "{ \"PowerBIPath\": broken");
        ProfileLifecycle lifecycle = NewLifecycle();

        UnreadableConfigFile file = Assert.Single(lifecycle.Load());

        Assert.NotNull(file.BackupPath);
        Assert.False(file.SavesBlocked);
        Assert.False(File.Exists(settingsFile));
        Assert.False(lifecycle.Settings.HasCompletedSetup);
        Assert.True(lifecycle.TrySaveSettings(out _));
    }

    [Fact]
    public void Repeated_unreadable_loads_never_overwrite_an_earlier_backup()
    {
        string profilesFile = Path.Combine(_baseDir, "profiles.json");
        string legacyBackup = profilesFile + ".bak";
        File.WriteAllText(legacyBackup, "older backup");
        var backups = new List<string>();

        foreach (string contents in new[] { "first broken", "second broken" })
        {
            File.WriteAllText(profilesFile, contents);
            var lifecycle = new ProfileLifecycle(_baseDir, _baseDir, new StubExecutableInspector(), new RecordingProcessStarter());
            backups.Add(Assert.Single(lifecycle.Load()).BackupPath!);
        }

        Assert.NotEqual(backups[0], backups[1]);
        Assert.Equal("first broken", File.ReadAllText(backups[0]));
        Assert.Equal("second broken", File.ReadAllText(backups[1]));
        Assert.Equal("older backup", File.ReadAllText(legacyBackup));
    }

    private static readonly Guid PinnedVersionId = Guid.Parse("77777777-7777-7777-7777-777777777777");

    private string WritePinnedProfile()
    {
        var profiles = new List<Profile>
        {
            new() { Name = "Pinned", Order = 1, AvatarColor = "#4F6BED", VersionId = PinnedVersionId }
        };
        string profilesFile = Path.Combine(_baseDir, "profiles.json");
        File.WriteAllText(profilesFile, JsonSerializer.Serialize(profiles));
        return profilesFile;
    }

    private static ProfileSaveResult SaveAsEditProfileWould(ProfileLifecycle lifecycle, Profile profile, Guid? selectedVersionId)
    {
        profile.VersionId = VersionSelection.VersionIdToSave(selectedVersionId, profile.VersionId, lifecycle.Settings.Versions);
        return lifecycle.TrySaveProfile(profile, isNew: false, identityEnabled: false, "", "");
    }

    private static Guid? SavedVersionId(string profilesFile) =>
        Assert.Single(JsonSerializer.Deserialize<List<Profile>>(File.ReadAllText(profilesFile))!).VersionId;

    [Fact]
    public void Saving_a_pinned_profile_while_settings_json_is_locked_keeps_its_version()
    {
        string profilesFile = WritePinnedProfile();
        string settingsFile = Path.Combine(_baseDir, "settings.json");
        File.WriteAllText(settingsFile, "{ \"HasCompletedSetup\": true }");
        var lifecycle = new ProfileLifecycle(_baseDir, _baseDir, new StubExecutableInspector(), new RecordingProcessStarter(), new MemoryCredentialStore());
        using (new FileStream(settingsFile, FileMode.Open, FileAccess.Read, FileShare.None))
        {
            lifecycle.Load();
        }

        Profile profile = Assert.Single(lifecycle.Profiles);
        SaveAsEditProfileWould(lifecycle, profile, selectedVersionId: null);

        Assert.Equal(PinnedVersionId, profile.VersionId);
        Assert.Equal(PinnedVersionId, SavedVersionId(profilesFile));
    }

    [Fact]
    public void Saving_a_pinned_profile_after_settings_json_was_set_aside_keeps_its_version()
    {
        string profilesFile = WritePinnedProfile();
        File.WriteAllText(Path.Combine(_baseDir, "settings.json"), "{ broken");
        var lifecycle = new ProfileLifecycle(_baseDir, _baseDir, new StubExecutableInspector(), new RecordingProcessStarter(), new MemoryCredentialStore());
        lifecycle.Load();
        Profile profile = Assert.Single(lifecycle.Profiles);
        profile.Name = "Renamed";

        ProfileSaveResult result = SaveAsEditProfileWould(lifecycle, profile, selectedVersionId: null);

        Assert.True(result.Succeeded);
        Assert.Equal(PinnedVersionId, SavedVersionId(profilesFile));
    }

    private ProfileLifecycle LoadWithSettingsLocked(MemoryCredentialStore store)
    {
        string settingsFile = Path.Combine(_baseDir, "settings.json");
        File.WriteAllText(settingsFile, $"{{ \"HasCompletedSetup\": true, \"UserProfilePath\": {JsonSerializer.Serialize(_profilesRoot)} }}");
        var lifecycle = new ProfileLifecycle(_baseDir, _baseDir, new StubExecutableInspector(), new RecordingProcessStarter(), store);
        using (new FileStream(settingsFile, FileMode.Open, FileAccess.Read, FileShare.None))
        {
            lifecycle.Load();
        }

        Assert.True(lifecycle.ProfileSavesBlocked);
        Assert.True(lifecycle.ProfileSavesBlockedBySettings);
        return lifecycle;
    }

    [Fact]
    public void Adding_a_profile_is_refused_while_settings_json_is_locked()
    {
        string profilesFile = WritePinnedProfile();
        string before = File.ReadAllText(profilesFile);
        ProfileLifecycle lifecycle = LoadWithSettingsLocked(new MemoryCredentialStore());
        Profile draft = NewProfile("New");

        bool added = lifecycle.TryAddProfile(draft, out string? osDetail);

        Assert.False(added);
        Assert.Null(osDetail);
        Assert.DoesNotContain(draft, lifecycle.Profiles);
        Assert.Equal(before, File.ReadAllText(profilesFile));
        Assert.False(Directory.Exists(ProfilePaths.ForProfile(lifecycle.Settings.UserProfilePath, draft.Id).Root));
        Assert.Throws<InvalidOperationException>(lifecycle.SaveProfiles);
    }

    [Fact]
    public void Deleting_a_profile_is_refused_while_settings_json_is_locked()
    {
        string profilesFile = WritePinnedProfile();
        string before = File.ReadAllText(profilesFile);
        var store = new MemoryCredentialStore();
        ProfileLifecycle lifecycle = LoadWithSettingsLocked(store);
        Profile profile = Assert.Single(lifecycle.Profiles);
        store.Passwords[profile.Id] = "secret";
        string root = ProfilePaths.ForProfile(_profilesRoot, profile.Id).Root;
        Directory.CreateDirectory(root);

        ProfileRemoveResult result = lifecycle.TryRemoveProfile(profile);

        Assert.False(result.Removed);
        Assert.Null(result.SaveError);
        Assert.Contains(profile, lifecycle.Profiles);
        Assert.Equal(before, File.ReadAllText(profilesFile));
        Assert.True(Directory.Exists(root));
        Assert.Equal("secret", store.Passwords[profile.Id]);
        Assert.Equal(0, store.DeleteCount);
    }

    [Fact]
    public void Saving_a_profile_is_refused_before_credential_manager_while_settings_json_is_locked()
    {
        string profilesFile = WritePinnedProfile();
        string before = File.ReadAllText(profilesFile);
        var store = new MemoryCredentialStore();
        ProfileLifecycle lifecycle = LoadWithSettingsLocked(store);
        Profile profile = Assert.Single(lifecycle.Profiles);

        ProfileSaveResult result = lifecycle.TrySaveProfile(profile, isNew: false, true, @"CONTOSO\JSmith", "secret");

        Assert.False(result.Succeeded);
        Assert.Null(result.IdentityError);
        Assert.Null(result.OsDetail);
        Assert.False(result.CredentialNotRestored);
        Assert.Equal(0, store.WriteCount);
        Assert.Null(profile.WindowsNetworkIdentity);
        Assert.Equal(before, File.ReadAllText(profilesFile));
        Assert.False(lifecycle.TrySaveProfiles(out string? osDetail));
        Assert.Null(osDetail);
    }

    [Fact]
    public void Load_keeps_migrated_profiles_in_memory_when_profiles_json_is_read_only()
    {
        var profiles = new List<Profile>
        {
            new()
            {
                Id = Guid.Parse("66666666-6666-6666-6666-666666666666"),
                Name = "Legacy",
                Order = 1,
                CardColor = "#FFFFFF",
                AvatarColor = ""
            }
        };
        string profilesFile = Path.Combine(_baseDir, "profiles.json");
        string original = JsonSerializer.Serialize(profiles);
        File.WriteAllText(profilesFile, original);
        File.SetAttributes(profilesFile, FileAttributes.ReadOnly);

        var lifecycle = new ProfileLifecycle(_baseDir, _baseDir, new StubExecutableInspector(), new RecordingProcessStarter());
        lifecycle.Load(pickAvatarColor: () => "#CAFE00");

        Profile loaded = Assert.Single(lifecycle.Profiles);
        Assert.Equal("Legacy", loaded.Name);
        Assert.Equal("#CAFE00", loaded.AvatarColor);
        Assert.Equal(original, File.ReadAllText(profilesFile));
        Assert.Empty(Directory.GetFiles(_baseDir, "*.bak"));
    }
}
