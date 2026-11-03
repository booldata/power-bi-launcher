using System;
using System.IO;

namespace PowerBILauncher
{
    public enum ProfileFolderMigrationOutcome
    {
        NotNeeded,
        Migrated,
        Failed
    }

    public sealed class ProfileFolderMigrationResult
    {
        public ProfileFolderMigrationOutcome Outcome { get; init; }
        public string? Error { get; init; }

        /// <summary>True when <see cref="Profile.IconPath"/> was rewritten to the new folder.</summary>
        public bool ProfileChanged { get; init; }

        public static ProfileFolderMigrationResult NotNeeded() =>
            new() { Outcome = ProfileFolderMigrationOutcome.NotNeeded };

        public static ProfileFolderMigrationResult Migrated(bool profileChanged) =>
            new() { Outcome = ProfileFolderMigrationOutcome.Migrated, ProfileChanged = profileChanged };

        public static ProfileFolderMigrationResult Failed(string? error, bool profileChanged) =>
            new() { Outcome = ProfileFolderMigrationOutcome.Failed, Error = error, ProfileChanged = profileChanged };
    }

    /// <summary>
    /// Moves a Profile's Isolated Environment from the legacy layout
    /// (<c>&lt;root&gt;\&lt;guid&gt;\AppData\{Local,Roaming}</c>) to the short layout
    /// (<c>&lt;root&gt;\&lt;12-hex&gt;\AppData\{Local,Roaming}</c>). Also folds in the short-lived
    /// interim layout <c>&lt;root&gt;\&lt;12-hex&gt;\{Local,Roaming}</c> that lacked the
    /// <c>AppData</c> level (see <see cref="ProfilePaths"/> for why that level is required).
    /// UI-free, best-effort and resumable: a folder that is still in use (Power BI open from the
    /// old layout) fails this attempt and is retried on the next call.
    /// </summary>
    public static class ProfileFolderMigration
    {
        public static ProfileFolderMigrationResult TryMigrate(string settingsRoot, Profile profile)
        {
            ArgumentNullException.ThrowIfNull(profile);
            if (string.IsNullOrWhiteSpace(settingsRoot))
            {
                return ProfileFolderMigrationResult.NotNeeded();
            }

            ProfilePaths legacy = ProfilePaths.LegacyForProfile(settingsRoot, profile.Id);
            ProfilePaths current = ProfilePaths.ForProfile(settingsRoot, profile.Id);
            string interimLocal = Path.Combine(current.Root, ProfilePaths.LocalFolderName);
            string interimRoaming = Path.Combine(current.Root, ProfilePaths.RoamingFolderName);

            bool legacyPresent = Directory.Exists(legacy.Root);
            bool interimPresent = Directory.Exists(interimLocal) || Directory.Exists(interimRoaming);
            if (!legacyPresent && !interimPresent)
            {
                return ProfileFolderMigrationResult.NotNeeded();
            }

            string? error = null;
            try
            {
                MoveInto(interimLocal, current.LocalAppData);
                MoveInto(interimRoaming, current.RoamingAppData);
                MoveInto(legacy.Root, current.Root);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                error = ex.Message;
            }

            bool profileChanged = TryRewriteIconPath(profile, legacy.Root, current.Root);

            bool leftovers = Directory.Exists(legacy.Root)
                || Directory.Exists(interimLocal)
                || Directory.Exists(interimRoaming);
            if (error is not null || leftovers)
            {
                return ProfileFolderMigrationResult.Failed(error, profileChanged);
            }

            return ProfileFolderMigrationResult.Migrated(profileChanged);
        }

        /// <summary>
        /// Moves everything under <paramref name="source"/> into <paramref name="destination"/>.
        /// Renames when the destination does not exist yet; merges otherwise. On a name clash the
        /// destination file wins (it was written by the new layout, so it is the newer one).
        /// </summary>
        private static void MoveInto(string source, string destination)
        {
            if (!Directory.Exists(source))
            {
                return;
            }

            if (!Directory.Exists(destination))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
                Directory.Move(source, destination);
                return;
            }

            foreach (string file in Directory.GetFiles(source))
            {
                string target = Path.Combine(destination, Path.GetFileName(file));
                if (File.Exists(target))
                {
                    File.Delete(file);
                }
                else
                {
                    File.Move(file, target);
                }
            }

            foreach (string directory in Directory.GetDirectories(source))
            {
                MoveInto(directory, Path.Combine(destination, Path.GetFileName(directory)));
            }

            DeleteIfEmpty(source);
        }

        private static void DeleteIfEmpty(string directory)
        {
            if (!Directory.Exists(directory))
            {
                return;
            }

            if (Directory.EnumerateFileSystemEntries(directory).GetEnumerator().MoveNext())
            {
                return;
            }

            Directory.Delete(directory);
        }

        private static bool TryRewriteIconPath(Profile profile, string legacyRoot, string currentRoot)
        {
            // Not LetterAvatar.HasCustomIcon: that checks File.Exists, and the file has just moved.
            if (string.IsNullOrWhiteSpace(profile.IconPath))
            {
                return false;
            }

            string legacyPrefix = Path.GetFullPath(legacyRoot).TrimEnd(Path.DirectorySeparatorChar)
                + Path.DirectorySeparatorChar;
            string fullIconPath;
            try
            {
                fullIconPath = Path.GetFullPath(profile.IconPath);
            }
            catch (Exception ex) when (ex is ArgumentException or IOException or NotSupportedException)
            {
                return false;
            }

            if (!fullIconPath.StartsWith(legacyPrefix, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            string relative = fullIconPath.Substring(legacyPrefix.Length);
            string rewritten = Path.Combine(Path.GetFullPath(currentRoot), relative);
            if (!File.Exists(rewritten))
            {
                return false;
            }

            profile.IconPath = rewritten;
            return true;
        }
    }
}
