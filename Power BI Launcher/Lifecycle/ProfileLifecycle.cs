using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;

namespace PowerBILauncher
{
    public enum VersionRegistryErrorKind
    {
        NotPowerBiDesktop,
        FileMissing,
        NotRegistered,
        NameEmpty,
        MicrosoftStoreVersion
    }

    public enum IdentityPersistErrorKind
    {
        IdentityEmpty,
        PasswordRequired,
        CredentialSaveFailed,
        CredentialDeleteFailed
    }

    public enum UnreadableConfigReason
    {
        /// <summary>The file was read but is not valid JSON.</summary>
        Unparsable,

        /// <summary>The file exists but could not be opened, even after retrying.</summary>
        Unavailable
    }

    /// <summary>
    /// A config file that could not be loaded. The launcher started without it;
    /// <see cref="BackupPath"/> is where an unparsable copy was kept (null when it could not be moved).
    /// </summary>
    public sealed class UnreadableConfigFile
    {
        public required string FilePath { get; init; }
        public UnreadableConfigReason Reason { get; init; }
        public string? BackupPath { get; init; }

        /// <summary>
        /// The original file is still in place, so saves to it are refused for this session.
        /// </summary>
        public bool SavesBlocked => Reason == UnreadableConfigReason.Unavailable || BackupPath is null;
    }

    /// <summary>
    /// Outcome of <see cref="ProfileLifecycle.TryRemoveProfile"/>. When <see cref="Removed"/> is false,
    /// <see cref="SaveError"/> says why profiles.json could not be written and nothing was deleted.
    /// </summary>
    public sealed class ProfileRemoveResult
    {
        public bool Removed { get; init; }
        public string? SaveError { get; init; }

        /// <summary>The Profile is gone, but its password is still in Credential Manager.</summary>
        public string? CredentialError { get; init; }

        /// <summary>The Profile is gone, but its folder could not be deleted.</summary>
        public string? DataError { get; init; }
    }

    /// <summary>
    /// Outcome of <see cref="ProfileLifecycle.TrySaveProfile"/>. <see cref="IdentityError"/> is set when the
    /// Windows network identity step failed (nothing was written); otherwise a failure means profiles.json
    /// could not be written.
    /// </summary>
    public sealed class ProfileSaveResult
    {
        public bool Succeeded { get; private init; }
        public IdentityPersistErrorKind? IdentityError { get; private init; }
        public string? OsDetail { get; private init; }

        /// <summary>
        /// profiles.json failed after Credential Manager changed, and the previous entry could not be put back.
        /// </summary>
        public bool CredentialNotRestored { get; private init; }

        /// <summary>
        /// With <see cref="CredentialNotRestored"/>: the entry was deleted instead, so launch asks for the
        /// password rather than pairing the saved identity with the password that was just written.
        /// </summary>
        public bool CredentialRemoved { get; private init; }

        public static ProfileSaveResult Success() => new() { Succeeded = true };

        public static ProfileSaveResult IdentityFailed(IdentityPersistErrorKind errorKind, string? osDetail) =>
            new() { IdentityError = errorKind, OsDetail = osDetail };

        public static ProfileSaveResult ProfilesFileFailed(string? osDetail, bool credentialRestored, bool credentialRemoved = false) =>
            new() { OsDetail = osDetail, CredentialNotRestored = !credentialRestored, CredentialRemoved = credentialRemoved };
    }

    /// <summary>
    /// UI-free Profile lifecycle and launch isolation: JSON beside the app base directory,
    /// Isolated Environment folders, Version registry, and child-process start.
    /// </summary>
    public sealed class ProfileLifecycle
    {
        public static readonly JsonSerializerOptions SettingsJsonOptions = new()
        {
            Converters = { new JsonStringEnumConverter() }
        };

        private readonly string _baseDirectory;
        private readonly string _workingDirectory;
        private readonly IPbiExecutableInspector _executableInspector;
        private readonly IProcessStarter _processStarter;
        private readonly IProfileCredentialStore _credentialStore;
        private readonly IPbiDiscovery _discovery;
        private readonly Action<TimeSpan> _lockRetryWait;
        private bool _settingsSavesBlocked;
        private bool _profilesSavesBlocked;

        private static readonly TimeSpan[] LockRetryDelays =
        {
            TimeSpan.FromMilliseconds(50),
            TimeSpan.FromMilliseconds(100),
            TimeSpan.FromMilliseconds(200),
            TimeSpan.FromMilliseconds(400)
        };

        public ProfileLifecycle(
            string baseDirectory,
            string? workingDirectory = null,
            IPbiExecutableInspector? executableInspector = null,
            IProcessStarter? processStarter = null,
            IProfileCredentialStore? credentialStore = null,
            IPbiDiscovery? discovery = null,
            Action<TimeSpan>? lockRetryWait = null)
        {
            _baseDirectory = baseDirectory ?? throw new ArgumentNullException(nameof(baseDirectory));
            _workingDirectory = workingDirectory ?? Directory.GetCurrentDirectory();
            _executableInspector = executableInspector ?? new FilePbiExecutableInspector();
            _processStarter = processStarter ?? new ProcessStarter();
            _credentialStore = credentialStore ?? new WindowsCredentialStore();
            _discovery = discovery ?? new WindowsPbiDiscovery();
            _lockRetryWait = lockRetryWait ?? Thread.Sleep;
            ProfilesFilePath = Path.Combine(_baseDirectory, "profiles.json");
            SettingsFilePath = Path.Combine(_baseDirectory, "settings.json");
            Settings = CreateDefaultSettings();
            Profiles = new List<Profile>();
        }

        public string ProfilesFilePath { get; }
        public string SettingsFilePath { get; }
        public Setting Settings { get; private set; }
        public List<Profile> Profiles { get; private set; }

        /// <summary>
        /// Profile writes are refused for this session: profiles.json is still in place unloaded, or
        /// settings.json is, so the Folder to store Profiles is only the default and may be the wrong one.
        /// </summary>
        public bool ProfileSavesBlocked => _profilesSavesBlocked || _settingsSavesBlocked;

        public bool ProfileSavesBlockedBySettings => _settingsSavesBlocked && !_profilesSavesBlocked;

        /// <summary>
        /// Loads settings and Profiles. Returns the files that were unreadable (empty when both loaded).
        /// A file whose original is still in place (<see cref="UnreadableConfigFile.SavesBlocked"/>)
        /// is never written for the rest of the session.
        /// </summary>
        public IReadOnlyList<UnreadableConfigFile> Load(Func<string>? pickAvatarColor = null)
        {
            var unreadable = new List<UnreadableConfigFile>();
            _settingsSavesBlocked = false;
            _profilesSavesBlocked = false;
            MigrateConfigFromCwdIfNeeded();
            Settings = LoadSettingsFromDisk(unreadable);
            Profiles = LoadProfilesFromDisk(pickAvatarColor, unreadable);
            if (!_settingsSavesBlocked)
            {
                // Without settings.json the Profiles root is only the default, which may not be where the folders live.
                MigrateProfileFolders();
            }

            return unreadable;
        }

        /// <summary>
        /// Best-effort move of every Profile folder to the short layout. A folder still in use
        /// is skipped here and retried at launch, where the failure is reported.
        /// </summary>
        public void MigrateProfileFolders()
        {
            bool dirty = false;
            foreach (Profile profile in Profiles)
            {
                ProfileFolderMigrationResult result = ProfileFolderMigration.TryMigrate(Settings.UserProfilePath, profile);
                dirty |= result.ProfileChanged;
            }

            if (dirty)
            {
                TrySaveProfiles(out _);
            }
        }

        public void SaveSettings()
        {
            ThrowIfSavesBlocked(_settingsSavesBlocked, SettingsFilePath);
            Settings.HasCompletedSetup = true;
            PersistSettings();
        }

        /// <summary>
        /// <see cref="SaveSettings"/> that reports a failed write instead of throwing.
        /// On failure <see cref="Setting.HasCompletedSetup"/> keeps its previous value.
        /// <paramref name="osDetail"/> is null when the save was refused because settings.json
        /// could not be loaded at startup and is still in place.
        /// </summary>
        public bool TrySaveSettings(out string? osDetail)
        {
            if (_settingsSavesBlocked)
            {
                osDetail = null;
                return false;
            }

            bool hadCompletedSetup = Settings.HasCompletedSetup;
            if (TryWrite(SaveSettings, out osDetail))
            {
                return true;
            }

            Settings.HasCompletedSetup = hadCompletedSetup;
            return false;
        }

        public void SaveProfiles()
        {
            ThrowIfSavesBlocked(_profilesSavesBlocked, ProfilesFilePath);
            ThrowIfSavesBlocked(_settingsSavesBlocked, SettingsFilePath);
            Profiles.Sort((a, b) => a.Order.CompareTo(b.Order));
            string json = JsonSerializer.Serialize(Profiles);
            WriteAllTextAtomically(ProfilesFilePath, json);
        }

        /// <summary>
        /// <see cref="SaveProfiles"/> that reports a failed write (read-only or unavailable
        /// launcher folder) instead of throwing. <paramref name="osDetail"/> is null when the save
        /// was refused (<see cref="ProfileSavesBlocked"/>).
        /// </summary>
        public bool TrySaveProfiles(out string? osDetail)
        {
            if (ProfileSavesBlocked)
            {
                osDetail = null;
                return false;
            }

            return TryWrite(SaveProfiles, out osDetail);
        }

        private static void ThrowIfSavesBlocked(bool blocked, string path)
        {
            if (blocked)
            {
                throw new InvalidOperationException(
                    $"{Path.GetFileName(path)} could not be loaded at startup, so saves are refused until restart.");
            }
        }

        public IReadOnlyList<SettingsIssue> CollectSettingsSaveErrors(string profilesFolder) =>
            CollectSettingsSaveErrors(profilesFolder, Settings.Versions, Settings.DefaultVersionId);

        public IReadOnlyList<SettingsIssue> CollectSettingsSaveErrors(
            string profilesFolder,
            IReadOnlyList<PbiVersion> versions,
            Guid? defaultVersionId)
        {
            var issues = SettingsValidation.TryValidateSettings(
                profilesFolder,
                defaultVersionId,
                versions).ToList();

            foreach (PbiVersion version in versions)
            {
                PbiExecutableFacts facts = _executableInspector.Inspect(version.Path);
                if (PbiExecutablePolicy.IsPowerBiExecutable(facts))
                {
                    continue;
                }

                issues.Add(new SettingsIssue
                {
                    Kind = facts.Exists
                        ? SettingsIssueKind.VersionNotPowerBiDesktop
                        : SettingsIssueKind.VersionFileMissing,
                    VersionDisplayName = version.DisplayName
                });
            }

            return issues;
        }

        public bool TryRegisterVersion(string path, out VersionRegistryErrorKind? errorKind)
        {
            Guid? defaultVersionId = Settings.DefaultVersionId;
            bool registered = TryRegisterVersion(Settings.Versions, ref defaultVersionId, path, out errorKind);
            if (registered)
            {
                Settings.DefaultVersionId = defaultVersionId;
            }

            return registered;
        }

        public bool TryRegisterVersion(
            List<PbiVersion> versions,
            ref Guid? defaultVersionId,
            string path,
            out VersionRegistryErrorKind? errorKind)
        {
            ArgumentNullException.ThrowIfNull(versions);
            string trimmed = path?.Trim() ?? "";
            if (trimmed.Length == 0)
            {
                errorKind = VersionRegistryErrorKind.NotPowerBiDesktop;
                return false;
            }

            PbiExecutableFacts facts = _executableInspector.Inspect(trimmed);
            if (!PbiExecutablePolicy.IsPowerBiExecutable(facts))
            {
                errorKind = facts.Exists
                    ? VersionRegistryErrorKind.NotPowerBiDesktop
                    : VersionRegistryErrorKind.FileMissing;
                return false;
            }

            if (PbiVersionKindPolicy.IsMicrosoftStore(trimmed))
            {
                errorKind = VersionRegistryErrorKind.MicrosoftStoreVersion;
                return false;
            }

            PbiVersion? existing = FindByPath(versions, trimmed);
            if (existing is not null)
            {
                existing.Path = trimmed;
                existing.Kind = PbiVersionKindPolicy.Infer(trimmed, facts.ProductName, facts.FileDescription);
                errorKind = null;
                return true;
            }

            PbiVersion version = CreateVersion(trimmed, facts);
            versions.Add(version);
            defaultVersionId ??= version.Id;
            errorKind = null;
            return true;
        }

        public bool TryRenameVersion(Guid id, string displayName, out VersionRegistryErrorKind? errorKind) =>
            TryRenameVersion(Settings.Versions, id, displayName, out errorKind);

        public static bool TryRenameVersion(
            List<PbiVersion> versions,
            Guid id,
            string displayName,
            out VersionRegistryErrorKind? errorKind)
        {
            PbiVersion? version = versions.FirstOrDefault(item => item.Id == id);
            if (version is null)
            {
                errorKind = VersionRegistryErrorKind.NotRegistered;
                return false;
            }

            string trimmed = displayName?.Trim() ?? "";
            if (trimmed.Length == 0)
            {
                errorKind = VersionRegistryErrorKind.NameEmpty;
                return false;
            }

            version.DisplayName = trimmed;
            errorKind = null;
            return true;
        }

        public bool TrySetDefaultVersion(Guid id, out VersionRegistryErrorKind? errorKind)
        {
            Guid? defaultVersionId = Settings.DefaultVersionId;
            bool updated = TrySetDefaultVersion(Settings.Versions, ref defaultVersionId, id, out errorKind);
            if (updated)
            {
                Settings.DefaultVersionId = defaultVersionId;
            }

            return updated;
        }

        public static bool TrySetDefaultVersion(
            List<PbiVersion> versions,
            ref Guid? defaultVersionId,
            Guid id,
            out VersionRegistryErrorKind? errorKind)
        {
            if (versions.All(version => version.Id != id))
            {
                errorKind = VersionRegistryErrorKind.NotRegistered;
                return false;
            }

            defaultVersionId = id;
            errorKind = null;
            return true;
        }

        public bool TryRemoveVersion(Guid id, out VersionRegistryErrorKind? errorKind)
        {
            Guid? defaultVersionId = Settings.DefaultVersionId;
            bool removed = TryRemoveVersion(Settings.Versions, ref defaultVersionId, id, out errorKind);
            if (removed)
            {
                Settings.DefaultVersionId = defaultVersionId;
            }

            return removed;
        }

        public static bool TryRemoveVersion(
            List<PbiVersion> versions,
            ref Guid? defaultVersionId,
            Guid id,
            out VersionRegistryErrorKind? errorKind)
        {
            PbiVersion? version = versions.FirstOrDefault(item => item.Id == id);
            if (version is null)
            {
                errorKind = VersionRegistryErrorKind.NotRegistered;
                return false;
            }

            versions.Remove(version);
            if (defaultVersionId == id)
            {
                defaultVersionId = versions.FirstOrDefault()?.Id;
            }

            errorKind = null;
            return true;
        }

        public ProfileLaunchResult TryLaunch(Profile profile)
        {
            ArgumentNullException.ThrowIfNull(profile);

            if (Settings.Versions.Count == 0)
            {
                return ProfileLaunchResult.Fail(
                    ProfileLaunchErrorKind.NoDefaultVersion,
                    ProfileLaunchRemediation.Settings);
            }

            Guid? resolvedId = profile.VersionId ?? Settings.DefaultVersionId;
            if (resolvedId is null)
            {
                return ProfileLaunchResult.Fail(
                    ProfileLaunchErrorKind.NoDefaultVersion,
                    ProfileLaunchRemediation.Settings);
            }

            PbiVersion? version = Settings.Versions.FirstOrDefault(item => item.Id == resolvedId.Value);
            if (version is null)
            {
                return profile.VersionId is null
                    ? ProfileLaunchResult.Fail(
                        ProfileLaunchErrorKind.NoDefaultVersion,
                        ProfileLaunchRemediation.Settings)
                    : ProfileLaunchResult.Fail(
                        ProfileLaunchErrorKind.DanglingVersion,
                        ProfileLaunchRemediation.EditProfile);
            }

            // Store Desktop signs in through the shared Windows account store, which Profile folders cannot isolate.
            if (PbiVersionKindPolicy.IsMicrosoftStore(version.Path))
            {
                return ProfileLaunchResult.Fail(
                    ProfileLaunchErrorKind.MicrosoftStoreVersion,
                    profile.VersionId is null
                        ? ProfileLaunchRemediation.Settings
                        : ProfileLaunchRemediation.EditProfile);
            }

            PbiExecutableFacts facts = _executableInspector.Inspect(version.Path);
            if (!facts.Exists)
            {
                return ProfileLaunchResult.Fail(
                    ProfileLaunchErrorKind.MissingFile,
                    ProfileLaunchRemediation.Settings);
            }

            if (!PbiExecutablePolicy.IsPowerBiExecutable(facts))
            {
                return ProfileLaunchResult.Fail(
                    ProfileLaunchErrorKind.NotPowerBiDesktop,
                    ProfileLaunchRemediation.Settings);
            }

            ProfileFolderMigrationResult migration = ProfileFolderMigration.TryMigrate(Settings.UserProfilePath, profile);
            if (migration.ProfileChanged)
            {
                TrySaveProfiles(out _);
            }

            if (migration.Outcome == ProfileFolderMigrationOutcome.Failed)
            {
                return migration.Error is null
                    ? ProfileLaunchResult.Fail(
                        ProfileLaunchErrorKind.ProfileFolderMigrationLeftovers,
                        ProfileLaunchRemediation.None)
                    : ProfileLaunchResult.Fail(
                        ProfileLaunchErrorKind.ProfileFolderMigration,
                        ProfileLaunchRemediation.None,
                        migration.Error);
            }

            var profilePaths = ProfilePaths.ForProfile(Settings.UserProfilePath, profile.Id);
            PathBudgetMiss? pathTooLong = PowerBiPathBudget.CheckLocalAppData(profilePaths.LocalAppData, version.Kind);
            if (pathTooLong is not null)
            {
                return ProfileLaunchResult.Fail(
                    ProfileLaunchErrorKind.PathTooLong,
                    ProfileLaunchRemediation.Settings,
                    pathBudget: pathTooLong);
            }

            string? password = null;
            string? identityUserName = null;
            string? identityDomain = null;
            if (!string.IsNullOrWhiteSpace(profile.WindowsNetworkIdentity))
            {
                ProfileCredentialRead read = _credentialStore.TryRead(profile.Id);
                if (read.Kind != ProfileCredentialReadKind.Found || read.Password is null)
                {
                    return ProfileLaunchResult.Fail(
                        ProfileLaunchErrorKind.PasswordUnavailable,
                        ProfileLaunchRemediation.EditProfile);
                }

                if (!WindowsNetworkIdentityText.TryParse(
                        profile.WindowsNetworkIdentity,
                        out identityUserName,
                        out identityDomain))
                {
                    return ProfileLaunchResult.Fail(
                        ProfileLaunchErrorKind.PasswordUnavailable,
                        ProfileLaunchRemediation.EditProfile);
                }

                password = read.Password;
            }

            ProcessStartInfo startInfo;
            try
            {
                startInfo = CreateLaunchStartInfo(profile, version.Path);
            }
            catch (Exception ex) when (IsFileSystemFailure(ex))
            {
                return ProfileLaunchResult.Fail(
                    ProfileLaunchErrorKind.ProfileFolderUnavailable,
                    ProfileLaunchRemediation.Settings,
                    ex.Message);
            }

            if (password is not null && identityUserName is not null)
            {
                ApplyNetworkCredentials(startInfo, identityUserName, identityDomain, password);
            }
            try
            {
                _processStarter.Start(startInfo);
                return ProfileLaunchResult.Success();
            }
            catch (Exception ex)
            {
                return ProfileLaunchResult.Fail(
                    ProfileLaunchErrorKind.ProcessStart,
                    ProfileLaunchRemediation.Settings,
                    ex.Message);
            }
        }

        public bool TryEnsureProfilesRoot(string profilesFolder, out SettingsIssue? issue)
        {
            if (string.IsNullOrWhiteSpace(profilesFolder))
            {
                issue = new SettingsIssue { Kind = SettingsIssueKind.FolderEmpty };
                return false;
            }

            try
            {
                Directory.CreateDirectory(profilesFolder);
            }
            catch (Exception)
            {
                issue = new SettingsIssue { Kind = SettingsIssueKind.FolderMissing };
                return false;
            }

            if (!Directory.Exists(profilesFolder))
            {
                issue = new SettingsIssue { Kind = SettingsIssueKind.FolderMissing };
                return false;
            }

            issue = null;
            return true;
        }

        public ProfilePaths EnsureProfileDirectories(Profile profile)
        {
            ArgumentNullException.ThrowIfNull(profile);
            ProfileFolderMigrationResult migration = ProfileFolderMigration.TryMigrate(Settings.UserProfilePath, profile);
            if (migration.ProfileChanged)
            {
                TrySaveProfiles(out _);
            }

            var paths = ProfilePaths.ForProfile(Settings.UserProfilePath, profile.Id);
            EnsureProfileDirectories(paths);
            return paths;
        }

        public static void EnsureProfileDirectories(ProfilePaths paths)
        {
            Directory.CreateDirectory(paths.Root);
            Directory.CreateDirectory(paths.LocalAppData);
            Directory.CreateDirectory(paths.RoamingAppData);
            Directory.CreateDirectory(paths.Temp);

            string edgeWebViewPath = Path.Combine(paths.LocalAppData, "Microsoft", "EdgeWebView");
            Directory.CreateDirectory(edgeWebViewPath);

            string desktopPath = Path.Combine(paths.Root, "Desktop");
            Directory.CreateDirectory(desktopPath);
        }

        public bool TryDeleteProfileData(Profile profile, out string? error)
        {
            ArgumentNullException.ThrowIfNull(profile);
            var paths = ProfilePaths.ForProfile(Settings.UserProfilePath, profile.Id);
            var legacyPaths = ProfilePaths.LegacyForProfile(Settings.UserProfilePath, profile.Id);

            try
            {
                if (Directory.Exists(paths.Root))
                {
                    Directory.Delete(paths.Root, recursive: true);
                }

                if (Directory.Exists(legacyPaths.Root))
                {
                    Directory.Delete(legacyPaths.Root, recursive: true);
                }

                error = null;
                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }

        /// <summary>
        /// Persists a Profile (assigning Launch Order = last + 1), writes profiles.json once,
        /// and creates its Isolated Environment folder. Call only from Save for a new draft.
        /// Returns false (and leaves the Profile list unchanged) when profiles.json cannot be written.
        /// </summary>
        public bool TryAddProfile(Profile profile, out string? osDetail)
        {
            ArgumentNullException.ThrowIfNull(profile);

            profile.Order = Profiles.Count == 0 ? 1 : Profiles.Max(p => p.Order) + 1;
            if (string.IsNullOrWhiteSpace(profile.AvatarColor))
            {
                profile.AvatarColor = LetterAvatar.PickRandomColor();
            }

            if (profile.ShowOnTray is null)
            {
                profile.ShowOnTray = true;
            }

            Profiles.Add(profile);
            if (!TrySaveProfiles(out osDetail))
            {
                Profiles.Remove(profile);
                return false;
            }

            try
            {
                EnsureProfileDirectories(profile);
            }
            catch (Exception ex) when (IsFileSystemFailure(ex))
            {
                // Launch recreates the folders and reports an unavailable Profiles folder.
            }

            return true;
        }

        /// <summary>
        /// Removes the Profile from profiles.json first, then deletes its saved password and folder.
        /// A failed profiles.json write keeps everything. Once the file no longer lists the Profile it
        /// stays deleted: a password or folder that could not be removed is reported, not rolled back.
        /// </summary>
        public ProfileRemoveResult TryRemoveProfile(Profile profile)
        {
            ArgumentNullException.ThrowIfNull(profile);

            int index = Profiles.IndexOf(profile);
            Profiles.Remove(profile);
            if (!TrySaveProfiles(out string? saveError))
            {
                if (index >= 0)
                {
                    Profiles.Insert(index, profile);
                }

                return new ProfileRemoveResult { SaveError = saveError };
            }

            string? credentialError = _credentialStore.TryDelete(profile.Id, out _, out string? credentialDetail)
                ? null
                : credentialDetail ?? "";
            string? dataError = TryDeleteProfileData(profile, out string? dataDetail)
                ? null
                : dataDetail ?? "";
            return new ProfileRemoveResult
            {
                Removed = true,
                CredentialError = credentialError,
                DataError = dataError
            };
        }

        public ProfileCredentialReadKind GetPasswordStatus(Guid profileId) =>
            _credentialStore.TryRead(profileId).Kind;

        /// <summary>
        /// Edit Profile Save: applies the Windows network identity, then writes profiles.json
        /// (<see cref="TryAddProfile"/> when <paramref name="isNew"/>). If profiles.json cannot be written,
        /// the Credential Manager entry is put back as it was so it matches the file on disk; when that fails
        /// the entry is deleted. The caller restores the in-memory Profile fields on failure.
        /// </summary>
        public ProfileSaveResult TrySaveProfile(
            Profile profile,
            bool isNew,
            bool identityEnabled,
            string? identityText,
            string? password)
        {
            ArgumentNullException.ThrowIfNull(profile);

            if (ProfileSavesBlocked)
            {
                return ProfileSaveResult.ProfilesFileFailed(null, credentialRestored: true);
            }

            bool changesCredential = !identityEnabled || !string.IsNullOrEmpty(password);
            ProfileCredentialRead? previousCredential = changesCredential
                ? _credentialStore.TryRead(profile.Id)
                : null;

            if (!TryPersistWindowsNetworkIdentity(
                    profile,
                    identityEnabled,
                    identityText,
                    password,
                    out IdentityPersistErrorKind? identityError,
                    out string? identityOsDetail))
            {
                return ProfileSaveResult.IdentityFailed(
                    identityError ?? IdentityPersistErrorKind.CredentialSaveFailed,
                    identityOsDetail);
            }

            string? saveError;
            bool saved = isNew
                ? TryAddProfile(profile, out saveError)
                : TrySaveProfiles(out saveError);
            if (saved)
            {
                return ProfileSaveResult.Success();
            }

            bool restored = previousCredential is null || TryRestoreCredential(profile.Id, previousCredential);
            bool removed = !restored && _credentialStore.TryDelete(profile.Id, out _, out _);
            return ProfileSaveResult.ProfilesFileFailed(saveError, restored, removed);
        }

        private bool TryRestoreCredential(Guid profileId, ProfileCredentialRead previous) =>
            previous.Kind switch
            {
                ProfileCredentialReadKind.Found => _credentialStore.TryWrite(
                    profileId,
                    previous.UserName ?? "",
                    previous.Password ?? "",
                    out _,
                    out _),
                ProfileCredentialReadKind.Missing => _credentialStore.TryDelete(profileId, out _, out _),
                _ => false
            };

        public bool TryPersistWindowsNetworkIdentity(
            Profile profile,
            bool enabled,
            string? identityText,
            string? password,
            out IdentityPersistErrorKind? errorKind,
            out string? osDetail)
        {
            ArgumentNullException.ThrowIfNull(profile);

            if (!enabled)
            {
                if (!_credentialStore.TryDelete(profile.Id, out _, out osDetail))
                {
                    errorKind = IdentityPersistErrorKind.CredentialDeleteFailed;
                    return false;
                }

                profile.WindowsNetworkIdentity = null;
                errorKind = null;
                osDetail = null;
                return true;
            }

            string? normalized = WindowsNetworkIdentityText.Normalize(identityText);
            if (normalized is null)
            {
                errorKind = IdentityPersistErrorKind.IdentityEmpty;
                osDetail = null;
                return false;
            }

            bool identityChanged = !string.Equals(
                profile.WindowsNetworkIdentity,
                normalized,
                StringComparison.Ordinal);
            bool hasPassword = !string.IsNullOrEmpty(password);
            if ((identityChanged || string.IsNullOrWhiteSpace(profile.WindowsNetworkIdentity)) && !hasPassword)
            {
                errorKind = IdentityPersistErrorKind.PasswordRequired;
                osDetail = null;
                return false;
            }

            if (hasPassword && !_credentialStore.TryWrite(profile.Id, normalized, password!, out _, out osDetail))
            {
                errorKind = IdentityPersistErrorKind.CredentialSaveFailed;
                return false;
            }

            profile.WindowsNetworkIdentity = normalized;
            errorKind = null;
            osDetail = null;
            return true;
        }

        public IReadOnlyList<ScanAcceptedVersion> ScanInstalledVersions() =>
            ScanPolicy.Accept(_discovery.FindCandidates(), _executableInspector.Inspect);

        public void ConfirmScan(
            IReadOnlyList<ScanAcceptedVersion> found,
            IReadOnlySet<string> selectedPaths,
            List<PbiVersion> versions,
            ref Guid? defaultVersionId)
        {
            ArgumentNullException.ThrowIfNull(found);
            ArgumentNullException.ThrowIfNull(selectedPaths);
            ArgumentNullException.ThrowIfNull(versions);

            var foundPaths = new HashSet<string>(
                found.Select(item => item.Path),
                StringComparer.OrdinalIgnoreCase);

            foreach (PbiVersion version in versions.ToList())
            {
                if (foundPaths.Contains(version.Path) &&
                    (PbiVersionKindPolicy.IsMicrosoftStore(version.Path) || !ContainsPath(selectedPaths, version.Path)))
                {
                    TryRemoveVersion(versions, ref defaultVersionId, version.Id, out _);
                }
            }

            foreach (ScanAcceptedVersion accepted in found)
            {
                if (accepted.IsMicrosoftStore || !ContainsPath(selectedPaths, accepted.Path))
                {
                    continue;
                }

                PbiVersion? existing = FindByPath(versions, accepted.Path);
                bool existed = existing is not null;
                TryRegisterVersion(versions, ref defaultVersionId, accepted.Path, out _);
                PbiVersion? created = FindByPath(versions, accepted.Path);
                if (created is null)
                {
                    continue;
                }

                created.Kind = accepted.Kind;
                if (!existed || ScanPolicy.IsPlaceholderDisplayName(created.DisplayName, created.Kind))
                {
                    created.DisplayName = accepted.DisplayName;
                }
            }
        }

        private static bool ContainsPath(IReadOnlySet<string> paths, string path) =>
            paths.Contains(path) ||
            paths.Any(item => string.Equals(item, path, StringComparison.OrdinalIgnoreCase));

        private ProcessStartInfo CreateLaunchStartInfo(Profile profile, string executablePath)
        {
            var paths = ProfilePaths.ForProfile(Settings.UserProfilePath, profile.Id);
            EnsureProfileDirectories(paths);

            var startInfo = new ProcessStartInfo
            {
                FileName = executablePath,
                Arguments = "",
                UseShellExecute = false,
                WorkingDirectory = Path.GetDirectoryName(executablePath) ?? ""
            };

            // Profile isolation only on the child process - never Environment.SetEnvironmentVariable.
            startInfo.EnvironmentVariables["USERPROFILE"] = paths.Root;
            startInfo.EnvironmentVariables["LOCALAPPDATA"] = paths.LocalAppData;
            startInfo.EnvironmentVariables["APPDATA"] = paths.RoamingAppData;
            startInfo.EnvironmentVariables["TEMP"] = paths.Temp;
            startInfo.EnvironmentVariables["TMP"] = paths.Temp;

            return startInfo;
        }

        private static void ApplyNetworkCredentials(
            ProcessStartInfo startInfo,
            string userName,
            string? domain,
            string password)
        {
            startInfo.UseCredentialsForNetworkingOnly = true;
            startInfo.UserName = userName;
            startInfo.Domain = domain ?? "";
            startInfo.Password = ToSecureString(password);
        }

        private static SecureString ToSecureString(string password)
        {
            var secure = new SecureString();
            foreach (char character in password)
            {
                secure.AppendChar(character);
            }

            secure.MakeReadOnly();
            return secure;
        }

        private static PbiVersion CreateVersion(string path, PbiExecutableFacts facts)
        {
            PbiVersionKind kind = PbiVersionKindPolicy.Infer(path, facts.ProductName, facts.FileDescription);
            return new PbiVersion
            {
                Id = Guid.NewGuid(),
                DisplayName = ScanPolicy.DisplayName(kind, path, facts.ProductName, facts.FileDescription),
                Path = path,
                Kind = kind
            };
        }

        private static PbiVersion? FindByPath(IReadOnlyList<PbiVersion> versions, string path) =>
            versions.FirstOrDefault(version =>
                string.Equals(version.Path, path, StringComparison.OrdinalIgnoreCase));

        private void PersistSettings()
        {
            Settings.Versions ??= new List<PbiVersion>();
            Settings.PowerBIPath = null;
            string json = JsonSerializer.Serialize(Settings, SettingsJsonOptions);
            WriteAllTextAtomically(SettingsFilePath, json);
        }

        /// <summary>
        /// Writes beside the target, then renames over it, so a crash or full disk mid-write
        /// leaves the previous file intact instead of a truncated one.
        /// </summary>
        private void WriteAllTextAtomically(string path, string contents)
        {
            string tempPath = path + ".tmp";
            try
            {
                using (var stream = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None))
                using (var writer = new StreamWriter(stream, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false)))
                {
                    writer.Write(contents);
                    writer.Flush();
                    stream.Flush(flushToDisk: true);
                }

                ReplaceWithRetry(tempPath, path);
            }
            catch
            {
                try
                {
                    File.Delete(tempPath);
                }
                catch
                {
                    // The next write recreates the temp file.
                }

                throw;
            }
        }

        /// <summary>
        /// Renames over the target, retrying briefly while another program (antivirus, backup, sync) holds it.
        /// A held target fails with access denied rather than a sharing violation, so both are retried.
        /// </summary>
        private void ReplaceWithRetry(string sourcePath, string destinationPath)
        {
            for (int attempt = 0; ; attempt++)
            {
                try
                {
                    File.Move(sourcePath, destinationPath, overwrite: true);
                    return;
                }
                catch (Exception ex) when (IsFileSystemFailure(ex) && attempt < LockRetryDelays.Length)
                {
                    _lockRetryWait(LockRetryDelays[attempt]);
                }
            }
        }

        private void MigrateConfigFromCwdIfNeeded()
        {
            TryMigrateFileFromCwd("settings.json", SettingsFilePath);
            TryMigrateFileFromCwd("profiles.json", ProfilesFilePath);
        }

        private void TryMigrateFileFromCwd(string fileName, string destinationPath)
        {
            if (File.Exists(destinationPath))
            {
                return;
            }

            string cwdPath = Path.GetFullPath(Path.Combine(_workingDirectory, fileName));
            string destFullPath = Path.GetFullPath(destinationPath);
            if (string.Equals(cwdPath, destFullPath, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            if (!File.Exists(cwdPath))
            {
                return;
            }

            try
            {
                File.Copy(cwdPath, destinationPath, overwrite: false);
            }
            catch
            {
                // Best-effort migration from a wrong Start-in folder.
            }
        }

        private Setting LoadSettingsFromDisk(List<UnreadableConfigFile> unreadable)
        {
            Setting settings;
            string? rawJson = null;
            if (!TryReadConfigFile(SettingsFilePath, out string? contents))
            {
                unreadable.Add(new UnreadableConfigFile
                {
                    FilePath = SettingsFilePath,
                    Reason = UnreadableConfigReason.Unavailable
                });
                settings = CreateBlockedSettings();
            }
            else if (contents is null)
            {
                settings = CreateDefaultSettings();
            }
            else
            {
                try
                {
                    settings = JsonSerializer.Deserialize<Setting>(contents, SettingsJsonOptions)
                        ?? CreateDefaultSettings();
                    rawJson = contents;

                    // Existing installs without the flag should not be re-onboarded.
                    if (!rawJson.Contains("HasCompletedSetup", StringComparison.Ordinal))
                    {
                        settings.HasCompletedSetup = true;
                    }
                }
                catch (JsonException)
                {
                    UnreadableConfigFile file = BackupUnreadableFile(SettingsFilePath);
                    unreadable.Add(file);
                    settings = file.SavesBlocked ? CreateBlockedSettings() : CreateDefaultSettings();
                }
            }

            settings.Versions ??= new List<PbiVersion>();

            if (string.IsNullOrWhiteSpace(settings.UserProfilePath))
            {
                settings.UserProfilePath = Setting.GetDefaultUserProfilePath();
            }

            // Close = Minimize was removed; treat legacy setting as Exit.
            if (settings.CloseBehavior == CloseButtonBehavior.Minimize)
            {
                settings.CloseBehavior = CloseButtonBehavior.Close;
            }

            if (TryMigrateLegacyPowerBiPath(settings, rawJson) && !_settingsSavesBlocked)
            {
                Settings = settings;
                TryWrite(PersistSettings, out _);
            }

            return settings;
        }

        /// <summary>
        /// Defaults for a session whose settings.json stays on disk untouched. Setup counts as complete:
        /// onboarding could only end in a refused save, and the real file already finished setup.
        /// </summary>
        private Setting CreateBlockedSettings()
        {
            _settingsSavesBlocked = true;
            return new Setting { HasCompletedSetup = true };
        }

        private bool TryMigrateLegacyPowerBiPath(Setting settings, string? rawJson)
        {
            bool jsonHasLegacyPath = rawJson is not null &&
                rawJson.Contains("PowerBIPath", StringComparison.Ordinal);

            if (settings.Versions.Count > 0)
            {
                bool hadLegacy = !string.IsNullOrWhiteSpace(settings.PowerBIPath) || jsonHasLegacyPath;
                settings.PowerBIPath = null;
                return hadLegacy;
            }

            if (string.IsNullOrWhiteSpace(settings.PowerBIPath))
            {
                settings.PowerBIPath = null;
                return jsonHasLegacyPath;
            }

            string legacyPath = settings.PowerBIPath;
            settings.PowerBIPath = null;

            PbiExecutableFacts facts = _executableInspector.Inspect(legacyPath);
            if (PbiExecutablePolicy.IsPowerBiExecutable(facts))
            {
                PbiVersion version = CreateVersion(legacyPath, facts);
                settings.Versions.Add(version);
                settings.DefaultVersionId = version.Id;
            }
            else
            {
                settings.Versions.Clear();
                settings.DefaultVersionId = null;
            }

            return true;
        }

        private List<Profile> LoadProfilesFromDisk(
            Func<string>? pickAvatarColor,
            List<UnreadableConfigFile> unreadable)
        {
            if (!TryReadConfigFile(ProfilesFilePath, out string? json))
            {
                _profilesSavesBlocked = true;
                unreadable.Add(new UnreadableConfigFile
                {
                    FilePath = ProfilesFilePath,
                    Reason = UnreadableConfigReason.Unavailable
                });
                return new List<Profile>();
            }

            if (json is null)
            {
                return new List<Profile>();
            }

            List<Profile> profiles;
            try
            {
                profiles = JsonSerializer.Deserialize<List<Profile>>(json) ?? new List<Profile>();
            }
            catch (JsonException)
            {
                UnreadableConfigFile file = BackupUnreadableFile(ProfilesFilePath);
                _profilesSavesBlocked = file.SavesBlocked;
                unreadable.Add(file);
                return new List<Profile>();
            }

            if (ProfileMigration.MigrateInPlace(profiles, pickAvatarColor))
            {
                Profiles = profiles;
                TrySaveProfiles(out _);
            }

            profiles.Sort((a, b) => a.Order.CompareTo(b.Order));
            return profiles;
        }

        /// <summary>
        /// Reads a config file, retrying briefly while another program (antivirus, backup, sync) holds it.
        /// <paramref name="contents"/> is null when the file does not exist. Returns false when it exists
        /// but stayed unreadable.
        /// </summary>
        private bool TryReadConfigFile(string path, out string? contents)
        {
            for (int attempt = 0; ; attempt++)
            {
                try
                {
                    contents = File.Exists(path) ? File.ReadAllText(path) : null;
                    return true;
                }
                catch (Exception ex) when (ex is FileNotFoundException or DirectoryNotFoundException)
                {
                    contents = null;
                    return true;
                }
                catch (IOException) when (attempt < LockRetryDelays.Length)
                {
                    _lockRetryWait(LockRetryDelays[attempt]);
                }
                catch (Exception ex) when (IsFileSystemFailure(ex))
                {
                    contents = null;
                    return false;
                }
            }
        }

        private static Setting CreateDefaultSettings() =>
            new Setting { HasCompletedSetup = false };

        private static bool IsFileSystemFailure(Exception ex) =>
            ex is IOException or UnauthorizedAccessException;

        private static bool TryWrite(Action write, out string? osDetail)
        {
            try
            {
                write();
                osDetail = null;
                return true;
            }
            catch (Exception ex) when (IsFileSystemFailure(ex))
            {
                osDetail = ex.Message;
                return false;
            }
        }

        /// <summary>
        /// Moves an unreadable file to a timestamped backup that never replaces an earlier one.
        /// </summary>
        private static UnreadableConfigFile BackupUnreadableFile(string path)
        {
            string? backupPath = null;
            try
            {
                if (File.Exists(path))
                {
                    string stamp = DateTime.Now.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);
                    string candidate = $"{path}.{stamp}.bak";
                    for (int suffix = 1; File.Exists(candidate); suffix++)
                    {
                        candidate = $"{path}.{stamp}-{suffix}.bak";
                    }

                    File.Move(path, candidate);
                    backupPath = candidate;
                }
            }
            catch
            {
                // Best-effort backup.
            }

            return new UnreadableConfigFile { FilePath = path, BackupPath = backupPath };
        }
    }
}
