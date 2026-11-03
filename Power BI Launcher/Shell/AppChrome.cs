using System.Linq;
using Microsoft.Windows.ApplicationModel.Resources;

namespace PowerBILauncher;

/// <summary>
/// UI-adjacent chrome lookup. The only C# <see cref="ResourceLoader"/> wrapper.
/// XAML still uses <c>x:Uid</c> directly.
/// </summary>
public static class AppChrome
{
    private static ResourceLoader? _loader;

    private static ResourceLoader Loader => _loader ??= new ResourceLoader();

    public static string Get(string key) => Loader.GetString(key);

    public static string Format(string key, params object[] args) =>
        string.Format(Get(key), args);

    public static string KindLabel(PbiVersionKind kind) =>
        kind switch
        {
            PbiVersionKind.Desktop => Get("PbiVersionKind_Desktop"),
            PbiVersionKind.ReportServer => Get("PbiVersionKind_ReportServer"),
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null)
        };

    public static string LaunchError(ProfileLaunchResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        return result.ErrorKind switch
        {
            ProfileLaunchErrorKind.MissingFile => Get("ProfileLaunchError_MissingFile"),
            ProfileLaunchErrorKind.NotPowerBiDesktop => Get("ProfileLaunchError_NotPowerBiDesktop"),
            ProfileLaunchErrorKind.NoDefaultVersion => Get("ProfileLaunchError_NoDefaultVersion"),
            ProfileLaunchErrorKind.DanglingVersion => Get("ProfileLaunchError_DanglingVersion"),
            ProfileLaunchErrorKind.MicrosoftStoreVersion => Get("ProfileLaunchError_MicrosoftStoreVersion"),
            ProfileLaunchErrorKind.PasswordUnavailable => Get("ProfileLaunchError_PasswordUnavailable"),
            ProfileLaunchErrorKind.ProcessStart => Format("ProfileLaunchError_ProcessStart", result.OsDetail ?? ""),
            ProfileLaunchErrorKind.ProfileFolderMigration =>
                Format("ProfileLaunchError_ProfileFolderMigration", result.OsDetail ?? ""),
            ProfileLaunchErrorKind.ProfileFolderMigrationLeftovers =>
                Get("ProfileLaunchError_ProfileFolderMigrationLeftovers"),
            ProfileLaunchErrorKind.PathTooLong => PathBudget(result.PathBudget),
            ProfileLaunchErrorKind.ProfileFolderUnavailable =>
                Format("ProfileLaunchError_ProfileFolderUnavailable", result.OsDetail ?? ""),
            null => Get("ProfileLaunchError_Generic"),
            _ => throw new ArgumentOutOfRangeException(nameof(result), result.ErrorKind, null)
        };
    }

    public static string LaunchErrorTitle(ProfileLaunchResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        return result.ErrorKind switch
        {
            ProfileLaunchErrorKind.PasswordUnavailable => Get("Dialog_SavedPasswordUnavailableTitle"),
            ProfileLaunchErrorKind.DanglingVersion => Get("Dialog_VersionMissingTitle"),
            _ => Get("Dialog_ErrorTitle")
        };
    }

    public static string PathBudget(PathBudgetMiss? miss)
    {
        if (miss is null)
        {
            return Get("ProfileLaunchError_Generic");
        }

        string kindLabel = KindLabel(miss.Kind);
        return miss.Scope switch
        {
            PathBudgetScope.LocalAppData =>
                Format("PathBudget_LocalAppData", kindLabel, miss.ActualLength, miss.MaxLength),
            PathBudgetScope.ProfilesRoot =>
                Format("PathBudget_ProfilesRoot", kindLabel, miss.ActualLength, miss.MaxLength),
            _ => throw new ArgumentOutOfRangeException(nameof(miss), miss.Scope, null)
        };
    }

    public static string SettingsIssue(SettingsIssue issue)
    {
        ArgumentNullException.ThrowIfNull(issue);
        return issue.Kind switch
        {
            SettingsIssueKind.FolderEmpty => Get("SettingsIssue_FolderEmpty"),
            SettingsIssueKind.FolderMissing => Get("SettingsIssue_FolderMissing"),
            SettingsIssueKind.FolderRelative => Get("SettingsIssue_FolderRelative"),
            SettingsIssueKind.FolderInvalidCharacters => Get("SettingsIssue_FolderInvalidCharacters"),
            SettingsIssueKind.DefaultVersionRequired => Get("SettingsIssue_DefaultVersionRequired"),
            SettingsIssueKind.VersionNotPowerBiDesktop =>
                Format("SettingsIssue_VersionNotPowerBiDesktop", issue.VersionDisplayName ?? ""),
            SettingsIssueKind.VersionFileMissing =>
                Format("SettingsIssue_VersionFileMissing", issue.VersionDisplayName ?? ""),
            SettingsIssueKind.PathTooLong => Format(
                issue.Budget?.Kind == PbiVersionKind.ReportServer
                    ? "SettingsIssue_FolderTooLong_ReportServer"
                    : "SettingsIssue_FolderTooLong_Desktop",
                issue.Budget?.MaxLength ?? 0),
            SettingsIssueKind.SaveFailed => issue.OsDetail is null
                ? Get("SettingsIssue_SaveBlocked")
                : Format("SettingsIssue_SaveFailed", issue.OsDetail),
            _ => throw new ArgumentOutOfRangeException(nameof(issue), issue.Kind, null)
        };
    }

    public static string ProfileValidation(ProfileValidationKind kind) =>
        kind switch
        {
            ProfileValidationKind.NameEmpty => Get("ProfileValidation_NameEmpty"),
            ProfileValidationKind.NameDuplicate => Get("ProfileValidation_NameDuplicate"),
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null)
        };

    public static string LogoValidation(LogoValidationKind kind, string? extension) =>
        kind switch
        {
            LogoValidationKind.PathEmpty => Get("LogoValidation_PathEmpty"),
            LogoValidationKind.UnsupportedFormat when string.IsNullOrEmpty(extension) =>
                Get("LogoValidation_UnsupportedFormat"),
            LogoValidationKind.UnsupportedFormat =>
                Format("LogoValidation_UnsupportedFormatWithExtension", extension),
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null)
        };

    public static string VersionRegistry(VersionRegistryErrorKind kind) =>
        kind switch
        {
            VersionRegistryErrorKind.NotPowerBiDesktop => Get("VersionRegistry_NotPowerBiDesktop"),
            VersionRegistryErrorKind.FileMissing => Get("VersionRegistry_FileMissing"),
            VersionRegistryErrorKind.NotRegistered => Get("VersionRegistry_NotRegistered"),
            VersionRegistryErrorKind.NameEmpty => Get("VersionRegistry_NameEmpty"),
            VersionRegistryErrorKind.MicrosoftStoreVersion => Get("VersionRegistry_MicrosoftStoreVersion"),
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null)
        };

    public static string IdentityPersist(IdentityPersistErrorKind kind, string? osDetail) =>
        kind switch
        {
            IdentityPersistErrorKind.IdentityEmpty => Get("IdentityPersist_IdentityEmpty"),
            IdentityPersistErrorKind.PasswordRequired => Get("IdentityPersist_PasswordRequired"),
            IdentityPersistErrorKind.CredentialSaveFailed =>
                CredentialStore(CredentialStoreErrorKind.SaveFailed, osDetail),
            IdentityPersistErrorKind.CredentialDeleteFailed =>
                CredentialStore(CredentialStoreErrorKind.RemoveFailed, osDetail),
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null)
        };

    /// <summary>
    /// A null <paramref name="osDetail"/> means the lifecycle refused the save because profiles.json, or
    /// settings.json when <paramref name="blockedBySettings"/>, could not be loaded at startup.
    /// </summary>
    public static string ProfilesSaveFailed(string? osDetail, bool blockedBySettings = false) =>
        osDetail is not null
            ? Format("Toast_ProfilesSaveFailed", osDetail)
            : Get(blockedBySettings ? "Toast_ProfilesSaveBlockedBySettings" : "Toast_ProfilesSaveBlocked");

    public static string UnreadableConfig(UnreadableConfigFile file)
    {
        ArgumentNullException.ThrowIfNull(file);
        string fileName = System.IO.Path.GetFileName(file.FilePath);
        if (file.Reason == UnreadableConfigReason.Unavailable)
        {
            return Format("Dialog_ConfigUnavailable", fileName);
        }

        return file.BackupPath is null
            ? Format("Dialog_ConfigUnreadable", fileName)
            : Format("Dialog_ConfigUnreadableBackup", fileName, file.BackupPath);
    }

    public static string CredentialStore(CredentialStoreErrorKind kind, string? osDetail) =>
        kind switch
        {
            CredentialStoreErrorKind.SaveFailed => Format("CredentialStore_SaveFailed", osDetail ?? ""),
            CredentialStoreErrorKind.RemoveFailed => Format("CredentialStore_RemoveFailed", osDetail ?? ""),
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null)
        };

    public static string VersionChoiceLabel(VersionChoiceOption option)
    {
        ArgumentNullException.ThrowIfNull(option);
        if (option.IsFollowDefault)
        {
            return string.IsNullOrEmpty(option.Label)
                ? Get("VersionSelection_FollowDefault")
                : Format("VersionSelection_FollowDefaultNamed", option.Label);
        }

        return option.Kind switch
        {
            VersionSelectionKind.EmptyRegistry => Get("VersionSelection_EmptyRegistry"),
            VersionSelectionKind.Dangling => Get("VersionSelection_Dangling"),
            _ => option.Label
        };
    }

    public static string CardAutomationName(ProfileCardAutomationKind kind, string profileName) =>
        kind switch
        {
            ProfileCardAutomationKind.Launch => Format("ProfileCard_Launch", profileName),
            ProfileCardAutomationKind.Profile => Format("ProfileCard_Profile", profileName),
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null)
        };

    public static string CardCaption(ProfileCardCaption caption)
    {
        ArgumentNullException.ThrowIfNull(caption);
        if (caption.IsUnresolved)
        {
            return caption.FollowsDefault
                ? Get("VersionSelection_FollowDefault")
                : Get("ProfileCard_CaptionMissing");
        }

        return caption.VersionDisplayName;
    }

    public static string SettingsTitle(bool onboarding) =>
        onboarding ? Get("Settings_WelcomeTitle") : Get("Settings_Title");

    public static string PhotoAction(bool hasCustomPhoto) =>
        hasCustomPhoto ? Get("EditProfile_ChangeIcon") : Get("EditProfile_UploadIcon");

    public static string IdentityPasswordHeader(bool replacing) =>
        replacing ? Get("EditProfile_NewPassword") : Get("EditProfile_Password");

    public static string IdentityPasswordHelp(bool passwordUnavailable) =>
        passwordUnavailable
            ? Get("EditProfile_ReenterPassword")
            : Get("EditProfile_PasswordRequiredHelp");

    public static IReadOnlyList<TrayMenuEntry> WithTrayLabels(IReadOnlyList<TrayMenuEntry> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);
        return entries.Select(entry => entry.Kind switch
        {
            TrayMenuEntryKind.ShowWindow => new TrayMenuEntry
            {
                Kind = entry.Kind,
                Label = Get("TrayMenu_ShowWindow")
            },
            TrayMenuEntryKind.HideWindow => new TrayMenuEntry
            {
                Kind = entry.Kind,
                Label = Get("TrayMenu_HideWindow")
            },
            TrayMenuEntryKind.Exit => new TrayMenuEntry
            {
                Kind = entry.Kind,
                Label = Get("TrayMenu_Exit")
            },
            _ => entry
        }).ToList();
    }
}
