using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace PowerBILauncher
{
    public enum SettingsIssueKind
    {
        FolderEmpty,
        FolderMissing,
        FolderRelative,
        FolderInvalidCharacters,
        DefaultVersionRequired,
        VersionNotPowerBiDesktop,
        VersionFileMissing,
        PathTooLong,
        SaveFailed
    }

    public sealed class SettingsIssue
    {
        public required SettingsIssueKind Kind { get; init; }
        public string? VersionDisplayName { get; init; }
        public PathBudgetMiss? Budget { get; init; }
        public string? OsDetail { get; init; }
    }

    public static class SettingsValidation
    {
        private static readonly char[] InvalidProfilesFolderCharacters =
        [
            '|', '<', '>', '"', '*', '?'
        ];

        public static IReadOnlyList<SettingsIssue> TryValidateSettings(
            string profilesFolder,
            Guid? defaultVersionId,
            IReadOnlyList<PbiVersion> versions)
        {
            var issues = new List<SettingsIssue>();

            SettingsIssue? folderIssue = TryValidateProfilesFolder(profilesFolder, versions);
            if (folderIssue is not null)
            {
                issues.Add(folderIssue);
            }

            if (defaultVersionId is null ||
                versions.All(version => version.Id != defaultVersionId.Value))
            {
                issues.Add(new SettingsIssue { Kind = SettingsIssueKind.DefaultVersionRequired });
            }

            return issues;
        }

        /// <summary>
        /// Returns a structured folder-path issue, or null when the path is a usable absolute root.
        /// Missing folders are allowed: Save creates them. Path-budget uses registered Version kinds.
        /// </summary>
        public static SettingsIssue? TryValidateProfilesFolder(
            string? profilesFolder,
            IReadOnlyList<PbiVersion> versions)
        {
            ArgumentNullException.ThrowIfNull(versions);
            string trimmed = profilesFolder?.Trim() ?? "";
            if (trimmed.Length == 0)
            {
                return new SettingsIssue { Kind = SettingsIssueKind.FolderEmpty };
            }

            if (!Path.IsPathFullyQualified(trimmed))
            {
                return new SettingsIssue { Kind = SettingsIssueKind.FolderRelative };
            }

            if (HasInvalidProfilesFolderCharacters(trimmed))
            {
                return new SettingsIssue { Kind = SettingsIssueKind.FolderInvalidCharacters };
            }

            try
            {
                _ = Path.GetFullPath(trimmed);
            }
            catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
            {
                return new SettingsIssue { Kind = SettingsIssueKind.FolderInvalidCharacters };
            }

            PathBudgetMiss? worst = null;
            foreach (PbiVersionKind kind in versions.Select(version => version.Kind).Distinct())
            {
                PathBudgetMiss? miss = PowerBiPathBudget.CheckProfilesRoot(trimmed, kind);
                if (miss is not null && (worst is null || miss.MaxLength < worst.MaxLength))
                {
                    worst = miss;
                }
            }

            if (worst is not null)
            {
                return new SettingsIssue
                {
                    Kind = SettingsIssueKind.PathTooLong,
                    Budget = worst
                };
            }

            return null;
        }

        public static bool TryParseMinimizeBehavior(string? tag, out MinimizeButtonBehavior behavior)
        {
            if (string.IsNullOrWhiteSpace(tag))
            {
                behavior = MinimizeButtonBehavior.Minimize;
                return false;
            }

            return Enum.TryParse(tag, ignoreCase: true, out behavior);
        }

        public static bool TryParseCloseBehavior(string? tag, out CloseButtonBehavior behavior)
        {
            if (string.IsNullOrWhiteSpace(tag))
            {
                behavior = CloseButtonBehavior.Close;
                return false;
            }

            return Enum.TryParse(tag, ignoreCase: true, out behavior);
        }

        private static bool HasInvalidProfilesFolderCharacters(string path)
        {
            foreach (char c in path)
            {
                if (c < 32 || Array.IndexOf(InvalidProfilesFolderCharacters, c) >= 0)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
