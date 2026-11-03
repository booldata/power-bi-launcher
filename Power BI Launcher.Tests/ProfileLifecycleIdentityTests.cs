using System.Diagnostics;

namespace PowerBILauncher.Tests;

public class ProfileLifecycleIdentityTests : IDisposable
{
    private static readonly string StubPath =
        @"D:\Tools\PBIDesktop\bin\PBIDesktop.exe";

    private readonly string _baseDir;
    private readonly string _cwdDir;
    private readonly string _profilesRoot;

    public ProfileLifecycleIdentityTests()
    {
        // Short names: the Profiles root must stay within PowerBiPathBudget for launches to pass.
        string run = Guid.NewGuid().ToString("N").Substring(0, 8);
        _baseDir = Path.Combine(Path.GetTempPath(), "pbi-id-" + run);
        _cwdDir = Path.Combine(Path.GetTempPath(), "pbi-id-cwd-" + run);
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

    private ProfileLifecycle Ready(
        MemoryCredentialStore store,
        RecordingProcessStarter? starter = null)
    {
        var inspector = new StubExecutableInspector();
        inspector.Pass(StubPath);
        var lifecycle = new ProfileLifecycle(_baseDir, _cwdDir, inspector, starter, store);
        lifecycle.Load();
        lifecycle.Settings.UserProfilePath = _profilesRoot;
        Assert.True(lifecycle.TryRegisterVersion(StubPath, out _));
        return lifecycle;
    }

    private static Profile AddProfile(ProfileLifecycle lifecycle, string name = "SIXT")
    {
        var profile = new Profile
        {
            Id = Guid.Parse("66666666-6666-6666-6666-666666666666"),
            Name = name,
            Order = 1,
            AvatarColor = "#4F6BED"
        };
        lifecycle.Profiles.Add(profile);
        return profile;
    }

    [Fact]
    public void Current_user_launch_has_no_alternate_credentials_and_keeps_isolation()
    {
        var store = new MemoryCredentialStore();
        var starter = new RecordingProcessStarter();
        ProfileLifecycle lifecycle = Ready(store, starter);
        Profile profile = AddProfile(lifecycle);
        var paths = ProfilePaths.ForProfile(_profilesRoot, profile.Id);

        ProfileLaunchResult result = lifecycle.TryLaunch(profile);

        Assert.True(result.Succeeded);
        Assert.True(string.IsNullOrEmpty(starter.Last!.UserName));
        Assert.True(string.IsNullOrEmpty(starter.Last.Domain));
        Assert.False(starter.Last.UseCredentialsForNetworkingOnly);
        Assert.Equal(paths.Root, starter.Last.EnvironmentVariables["USERPROFILE"]);
        Assert.Equal(paths.LocalAppData, starter.Last.EnvironmentVariables["LOCALAPPDATA"]);
    }

    [Fact]
    public void Missing_saved_password_blocks_launch_without_current_user_fallback()
    {
        var store = new MemoryCredentialStore();
        var starter = new RecordingProcessStarter();
        ProfileLifecycle lifecycle = Ready(store, starter);
        Profile profile = AddProfile(lifecycle);
        profile.WindowsNetworkIdentity = @"CONTOSO\JSmith";

        ProfileLaunchResult result = lifecycle.TryLaunch(profile);

        Assert.False(result.Succeeded);
        Assert.Equal(0, starter.StartCount);
        Assert.Equal(ProfileLaunchErrorKind.PasswordUnavailable, result.ErrorKind);
        Assert.Equal(ProfileLaunchRemediation.EditProfile, result.Remediation);
    }

    [Fact]
    public void Unreadable_saved_password_blocks_launch_and_does_not_delete_the_entry()
    {
        var store = new MemoryCredentialStore();
        var starter = new RecordingProcessStarter();
        ProfileLifecycle lifecycle = Ready(store, starter);
        Profile profile = AddProfile(lifecycle);
        profile.WindowsNetworkIdentity = @"CONTOSO\JSmith";
        store.Passwords[profile.Id] = "secret";
        store.Unreadable.Add(profile.Id);

        ProfileLaunchResult result = lifecycle.TryLaunch(profile);

        Assert.False(result.Succeeded);
        Assert.Equal(0, starter.StartCount);
        Assert.Equal(ProfileLaunchErrorKind.PasswordUnavailable, result.ErrorKind);
        Assert.True(store.Passwords.ContainsKey(profile.Id));
    }

    [Fact]
    public void Two_profiles_with_the_same_identity_text_own_independent_passwords()
    {
        var store = new MemoryCredentialStore();
        ProfileLifecycle lifecycle = Ready(store);
        Profile first = AddProfile(lifecycle, "One");
        first.Id = Guid.Parse("11111111-1111-1111-1111-111111111111");
        Profile second = new()
        {
            Id = Guid.Parse("22222222-2222-2222-2222-222222222222"),
            Name = "Two",
            Order = 2,
            AvatarColor = "#4F6BED"
        };
        lifecycle.Profiles.Add(second);

        Assert.True(lifecycle.TryPersistWindowsNetworkIdentity(first, true, @"CONTOSO\Shared", "alpha", out _, out _));
        Assert.True(lifecycle.TryPersistWindowsNetworkIdentity(second, true, @"CONTOSO\Shared", "beta", out _, out _));
        Assert.Equal("alpha", store.Passwords[first.Id]);
        Assert.Equal("beta", store.Passwords[second.Id]);

        ProfileRemoveResult removal = lifecycle.TryRemoveProfile(first);
        Assert.True(removal.Removed);
        Assert.Null(removal.CredentialError);
        Assert.False(store.Passwords.ContainsKey(first.Id));
        Assert.Equal("beta", store.Passwords[second.Id]);
        Assert.Equal(@"CONTOSO\Shared", second.WindowsNetworkIdentity);
    }

    [Fact]
    public void Failed_credential_write_preserves_prior_profile_state()
    {
        var store = new MemoryCredentialStore();
        ProfileLifecycle lifecycle = Ready(store);
        Profile profile = AddProfile(lifecycle);
        profile.WindowsNetworkIdentity = null;
        store.WriteErrorKind = CredentialStoreErrorKind.SaveFailed;
        store.WriteOsDetail = "Credential Manager is unavailable.";

        bool saved = lifecycle.TryPersistWindowsNetworkIdentity(
            profile,
            enabled: true,
            identityText: @"CONTOSO\JSmith",
            password: "secret",
            out IdentityPersistErrorKind? errorKind,
            out string? osDetail);

        Assert.False(saved);
        Assert.Equal(IdentityPersistErrorKind.CredentialSaveFailed, errorKind);
        Assert.Equal("Credential Manager is unavailable.", osDetail);
        Assert.Null(profile.WindowsNetworkIdentity);
        Assert.False(store.Passwords.ContainsKey(profile.Id));
    }

    [Fact]
    public void Blank_password_on_unchanged_identity_preserves_the_saved_password()
    {
        var store = new MemoryCredentialStore();
        ProfileLifecycle lifecycle = Ready(store);
        Profile profile = AddProfile(lifecycle);
        Assert.True(lifecycle.TryPersistWindowsNetworkIdentity(profile, true, @"CONTOSO\JSmith", "secret", out _, out _));

        Assert.True(lifecycle.TryPersistWindowsNetworkIdentity(profile, true, @"CONTOSO\JSmith", password: "", out _, out _));

        Assert.Equal("secret", store.Passwords[profile.Id]);
        Assert.Equal(@"CONTOSO\JSmith", profile.WindowsNetworkIdentity);
    }

    [Fact]
    public void Changing_identity_text_requires_a_password()
    {
        var store = new MemoryCredentialStore();
        ProfileLifecycle lifecycle = Ready(store);
        Profile profile = AddProfile(lifecycle);
        Assert.True(lifecycle.TryPersistWindowsNetworkIdentity(profile, true, @"CONTOSO\Old", "secret", out _, out _));

        bool saved = lifecycle.TryPersistWindowsNetworkIdentity(profile, true, @"CONTOSO\New", password: "", out IdentityPersistErrorKind? errorKind, out _);

        Assert.False(saved);
        Assert.Equal(IdentityPersistErrorKind.PasswordRequired, errorKind);
        Assert.Equal(@"CONTOSO\Old", profile.WindowsNetworkIdentity);
        Assert.Equal("secret", store.Passwords[profile.Id]);
    }

    [Fact]
    public void Clear_disables_identity_and_deletes_the_password()
    {
        var store = new MemoryCredentialStore();
        ProfileLifecycle lifecycle = Ready(store);
        Profile profile = AddProfile(lifecycle);
        Assert.True(lifecycle.TryPersistWindowsNetworkIdentity(profile, true, @"CONTOSO\JSmith", "secret", out _, out _));

        Assert.True(lifecycle.TryPersistWindowsNetworkIdentity(profile, enabled: false, identityText: "", password: "", out _, out _));

        Assert.Null(profile.WindowsNetworkIdentity);
        Assert.False(store.Passwords.ContainsKey(profile.Id));
    }

    [Fact]
    public void Failed_credential_delete_after_removing_the_profile_is_reported_not_rolled_back()
    {
        var store = new MemoryCredentialStore();
        ProfileLifecycle lifecycle = Ready(store);
        Profile profile = AddProfile(lifecycle);
        Assert.True(lifecycle.TryPersistWindowsNetworkIdentity(profile, true, @"CONTOSO\JSmith", "secret", out _, out _));
        store.DeleteErrorKind = CredentialStoreErrorKind.RemoveFailed;
        store.DeleteOsDetail = "Access denied.";
        string root = ProfilePaths.ForProfile(_profilesRoot, profile.Id).Root;
        Directory.CreateDirectory(root);

        ProfileRemoveResult result = lifecycle.TryRemoveProfile(profile);

        Assert.True(result.Removed);
        Assert.Equal("Access denied.", result.CredentialError);
        Assert.Null(result.DataError);
        Assert.DoesNotContain(profile, lifecycle.Profiles);
        Assert.DoesNotContain(profile.Id.ToString(), File.ReadAllText(lifecycle.ProfilesFilePath));
        Assert.False(Directory.Exists(root));
        Assert.Equal("secret", store.Passwords[profile.Id]);
    }

    private static void BlockProfilesFile(ProfileLifecycle lifecycle) =>
        Directory.CreateDirectory(lifecycle.ProfilesFilePath);

    [Fact]
    public void Save_writes_the_identity_and_profiles_json_together()
    {
        var store = new MemoryCredentialStore();
        ProfileLifecycle lifecycle = Ready(store);
        var draft = new Profile { Name = "Draft", AvatarColor = "#4F6BED" };

        ProfileSaveResult result = lifecycle.TrySaveProfile(draft, isNew: true, true, @"CONTOSO\JSmith", "secret");

        Assert.True(result.Succeeded);
        Assert.Contains(draft, lifecycle.Profiles);
        Assert.Equal("secret", store.Passwords[draft.Id]);
        Assert.Equal(@"CONTOSO\JSmith", store.UserNames[draft.Id]);
        Assert.Contains(@"CONTOSO\\JSmith", File.ReadAllText(lifecycle.ProfilesFilePath));
    }

    [Fact]
    public void Failed_profiles_write_after_clearing_identity_puts_the_password_back()
    {
        var store = new MemoryCredentialStore();
        ProfileLifecycle lifecycle = Ready(store);
        Profile profile = AddProfile(lifecycle);
        Assert.True(lifecycle.TryPersistWindowsNetworkIdentity(profile, true, @"CONTOSO\JSmith", "secret", out _, out _));
        BlockProfilesFile(lifecycle);

        ProfileSaveResult result = lifecycle.TrySaveProfile(profile, isNew: false, identityEnabled: false, "", "");

        Assert.False(result.Succeeded);
        Assert.Null(result.IdentityError);
        Assert.False(string.IsNullOrEmpty(result.OsDetail));
        Assert.False(result.CredentialNotRestored);
        Assert.Equal("secret", store.Passwords[profile.Id]);
        Assert.Equal(@"CONTOSO\JSmith", store.UserNames[profile.Id]);
    }

    [Fact]
    public void Failed_profiles_write_after_changing_identity_puts_the_previous_credential_back()
    {
        var store = new MemoryCredentialStore();
        ProfileLifecycle lifecycle = Ready(store);
        Profile profile = AddProfile(lifecycle);
        Assert.True(lifecycle.TryPersistWindowsNetworkIdentity(profile, true, @"CONTOSO\Old", "old-secret", out _, out _));
        BlockProfilesFile(lifecycle);

        ProfileSaveResult result = lifecycle.TrySaveProfile(profile, isNew: false, true, @"CONTOSO\New", "new-secret");

        Assert.False(result.Succeeded);
        Assert.False(result.CredentialNotRestored);
        Assert.Equal("old-secret", store.Passwords[profile.Id]);
        Assert.Equal(@"CONTOSO\Old", store.UserNames[profile.Id]);
    }

    [Fact]
    public void Failed_profiles_write_for_a_new_identity_removes_the_new_password()
    {
        var store = new MemoryCredentialStore();
        ProfileLifecycle lifecycle = Ready(store);
        Profile profile = AddProfile(lifecycle);
        BlockProfilesFile(lifecycle);

        ProfileSaveResult result = lifecycle.TrySaveProfile(profile, isNew: false, true, @"CONTOSO\JSmith", "secret");

        Assert.False(result.Succeeded);
        Assert.False(result.CredentialNotRestored);
        Assert.False(store.Passwords.ContainsKey(profile.Id));
    }

    [Fact]
    public void Failed_profiles_write_for_a_new_draft_leaves_no_password_and_no_profile()
    {
        var store = new MemoryCredentialStore();
        ProfileLifecycle lifecycle = Ready(store);
        BlockProfilesFile(lifecycle);
        var draft = new Profile { Name = "Draft", AvatarColor = "#4F6BED" };

        ProfileSaveResult result = lifecycle.TrySaveProfile(draft, isNew: true, true, @"CONTOSO\JSmith", "secret");

        Assert.False(result.Succeeded);
        Assert.Empty(lifecycle.Profiles);
        Assert.False(store.Passwords.ContainsKey(draft.Id));
    }

    [Fact]
    public void Failed_profiles_write_reports_a_password_that_could_not_be_put_back()
    {
        var store = new MemoryCredentialStore();
        ProfileLifecycle lifecycle = Ready(store);
        Profile profile = AddProfile(lifecycle);
        Assert.True(lifecycle.TryPersistWindowsNetworkIdentity(profile, true, @"CONTOSO\JSmith", "secret", out _, out _));
        BlockProfilesFile(lifecycle);
        store.WriteErrorKind = CredentialStoreErrorKind.SaveFailed;

        ProfileSaveResult result = lifecycle.TrySaveProfile(profile, isNew: false, identityEnabled: false, "", "");

        Assert.False(result.Succeeded);
        Assert.True(result.CredentialNotRestored);
        Assert.True(result.CredentialRemoved);
        Assert.False(store.Passwords.ContainsKey(profile.Id));
    }

    [Fact]
    public void Failed_profiles_write_removes_the_new_password_when_the_previous_one_cannot_be_put_back()
    {
        var store = new MemoryCredentialStore();
        var starter = new RecordingProcessStarter();
        ProfileLifecycle lifecycle = Ready(store, starter);
        Profile profile = AddProfile(lifecycle);
        Assert.True(lifecycle.TryPersistWindowsNetworkIdentity(profile, true, @"CONTOSO\Old", "old-secret", out _, out _));
        BlockProfilesFile(lifecycle);
        store.WriteErrorKind = CredentialStoreErrorKind.SaveFailed;
        store.FailWritesAfter = store.WriteCount + 1;

        ProfileSaveResult result = lifecycle.TrySaveProfile(profile, isNew: false, true, @"CONTOSO\New", "new-secret");
        profile.WindowsNetworkIdentity = @"CONTOSO\Old";

        Assert.False(result.Succeeded);
        Assert.True(result.CredentialNotRestored);
        Assert.True(result.CredentialRemoved);
        Assert.False(store.Passwords.ContainsKey(profile.Id));
        Assert.Equal(ProfileLaunchErrorKind.PasswordUnavailable, lifecycle.TryLaunch(profile).ErrorKind);
        Assert.Equal(0, starter.StartCount);
    }

    [Fact]
    public void Failed_profiles_write_reports_a_new_password_it_could_neither_put_back_nor_remove()
    {
        var store = new MemoryCredentialStore();
        ProfileLifecycle lifecycle = Ready(store);
        Profile profile = AddProfile(lifecycle);
        Assert.True(lifecycle.TryPersistWindowsNetworkIdentity(profile, true, @"CONTOSO\Old", "old-secret", out _, out _));
        BlockProfilesFile(lifecycle);
        store.WriteErrorKind = CredentialStoreErrorKind.SaveFailed;
        store.FailWritesAfter = store.WriteCount + 1;
        store.DeleteErrorKind = CredentialStoreErrorKind.RemoveFailed;
        store.FailDeletesAfter = 0;

        ProfileSaveResult result = lifecycle.TrySaveProfile(profile, isNew: false, true, @"CONTOSO\New", "new-secret");

        Assert.False(result.Succeeded);
        Assert.True(result.CredentialNotRestored);
        Assert.False(result.CredentialRemoved);
    }

    [Fact]
    public void Failed_profiles_write_cannot_put_back_an_unreadable_entry_so_removes_the_replacement()
    {
        var store = new MemoryCredentialStore();
        ProfileLifecycle lifecycle = Ready(store);
        Profile profile = AddProfile(lifecycle);
        profile.WindowsNetworkIdentity = @"CONTOSO\JSmith";
        store.Passwords[profile.Id] = "secret";
        store.Unreadable.Add(profile.Id);
        BlockProfilesFile(lifecycle);

        ProfileSaveResult result = lifecycle.TrySaveProfile(profile, isNew: false, true, @"CONTOSO\JSmith", "replacement");

        Assert.False(result.Succeeded);
        Assert.True(result.CredentialNotRestored);
        Assert.True(result.CredentialRemoved);
        Assert.False(store.Passwords.ContainsKey(profile.Id));
    }

    [Fact]
    public void Failed_profiles_write_with_an_unchanged_identity_does_not_touch_credential_manager()
    {
        var store = new MemoryCredentialStore();
        ProfileLifecycle lifecycle = Ready(store);
        Profile profile = AddProfile(lifecycle);
        Assert.True(lifecycle.TryPersistWindowsNetworkIdentity(profile, true, @"CONTOSO\JSmith", "secret", out _, out _));
        int writesBefore = store.WriteCount;
        BlockProfilesFile(lifecycle);

        ProfileSaveResult result = lifecycle.TrySaveProfile(profile, isNew: false, true, @"CONTOSO\JSmith", "");

        Assert.False(result.Succeeded);
        Assert.False(result.CredentialNotRestored);
        Assert.Equal(writesBefore, store.WriteCount);
        Assert.Equal(0, store.DeleteCount);
        Assert.Equal("secret", store.Passwords[profile.Id]);
    }

    [Fact]
    public void Failed_identity_step_writes_neither_credential_manager_nor_profiles_json()
    {
        var store = new MemoryCredentialStore { WriteErrorKind = CredentialStoreErrorKind.SaveFailed, WriteOsDetail = "Vault down." };
        ProfileLifecycle lifecycle = Ready(store);
        var draft = new Profile { Name = "Draft", AvatarColor = "#4F6BED" };

        ProfileSaveResult result = lifecycle.TrySaveProfile(draft, isNew: true, true, @"CONTOSO\JSmith", "secret");

        Assert.False(result.Succeeded);
        Assert.Equal(IdentityPersistErrorKind.CredentialSaveFailed, result.IdentityError);
        Assert.Equal("Vault down.", result.OsDetail);
        Assert.Empty(lifecycle.Profiles);
        Assert.False(File.Exists(lifecycle.ProfilesFilePath));
    }

    [Fact]
    public void Identity_launch_uses_network_only_credentials_and_keeps_isolation()
    {
        var store = new MemoryCredentialStore();
        var starter = new RecordingProcessStarter();
        ProfileLifecycle lifecycle = Ready(store, starter);
        Profile profile = AddProfile(lifecycle);
        Assert.True(lifecycle.TryPersistWindowsNetworkIdentity(profile, true, @"CONTOSO\JSmith", "secret", out _, out _));
        var paths = ProfilePaths.ForProfile(_profilesRoot, profile.Id);

        ProfileLaunchResult result = lifecycle.TryLaunch(profile);

        Assert.True(result.Succeeded);
        Assert.True(starter.Last!.UseCredentialsForNetworkingOnly);
        Assert.Equal("JSmith", starter.Last.UserName);
        Assert.Equal("CONTOSO", starter.Last.Domain);
        Assert.Equal(paths.Root, starter.Last.EnvironmentVariables["USERPROFILE"]);
        Assert.Equal("", starter.Last.Arguments);
    }

    [Fact]
    public void Changing_version_does_not_change_identity()
    {
        var store = new MemoryCredentialStore();
        ProfileLifecycle lifecycle = Ready(store);
        Profile profile = AddProfile(lifecycle);
        Assert.True(lifecycle.TryPersistWindowsNetworkIdentity(profile, true, @"CONTOSO\JSmith", "secret", out _, out _));
        string? identity = profile.WindowsNetworkIdentity;

        profile.VersionId = lifecycle.Settings.DefaultVersionId;

        Assert.Equal(identity, profile.WindowsNetworkIdentity);
        Assert.Equal("secret", store.Passwords[profile.Id]);
    }
}
