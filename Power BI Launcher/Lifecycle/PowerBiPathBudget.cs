using System;

namespace PowerBILauncher
{
    public enum PathBudgetScope
    {
        LocalAppData,
        ProfilesRoot
    }

    public sealed class PathBudgetMiss
    {
        public required PathBudgetScope Scope { get; init; }
        public required PbiVersionKind Kind { get; init; }
        public required int ActualLength { get; init; }
        public required int MaxLength { get; init; }
    }

    /// <summary>
    /// Path-length budget Power BI Desktop imposes on the redirected LOCALAPPDATA.
    /// PBIDesktop.exe targets .NET Framework without <c>longPathAware</c>, so it cannot see
    /// files whose full path reaches MAX_PATH. It discovers its Analysis Services engine by
    /// polling <c>msmdsrv.port.txt</c> inside
    /// <c>%LOCALAPPDATA%\Microsoft\Power BI Desktop[ SSRS]\AnalysisServicesWorkspaces\AnalysisServicesWorkspace_{guid}\Data</c>;
    /// when that folder is too long the poll never succeeds and every new report fails with
    /// "We weren't able to connect to the model". Measured limit: the Data folder must stay at
    /// or below 235 characters.
    /// </summary>
    public static class PowerBiPathBudget
    {
        public const int MaxWorkspaceDataDirLength = 235;

        private const int DesktopWorkspaceSuffixLength = 122;
        private const int ReportServerWorkspaceSuffixLength = 127;

        public static int MaxLocalAppDataLength(PbiVersionKind kind) =>
            MaxWorkspaceDataDirLength - WorkspaceSuffixLength(kind);

        /// <summary>
        /// Longest Profiles root that still fits every Profile for <paramref name="kind"/>,
        /// given the fixed <see cref="ProfilePaths"/> layout below the root.
        /// </summary>
        public static int MaxProfilesRootLength(PbiVersionKind kind) =>
            MaxLocalAppDataLength(kind) - ProfileLayoutSuffixLength;

        /// <summary>
        /// Returns a structured miss when <paramref name="localAppData"/> is too long for
        /// <paramref name="kind"/>; null when it fits.
        /// </summary>
        public static PathBudgetMiss? CheckLocalAppData(string localAppData, PbiVersionKind kind)
        {
            ArgumentNullException.ThrowIfNull(localAppData);
            int max = MaxLocalAppDataLength(kind);
            if (localAppData.Length <= max)
            {
                return null;
            }

            return new PathBudgetMiss
            {
                Scope = PathBudgetScope.LocalAppData,
                Kind = kind,
                ActualLength = localAppData.Length,
                MaxLength = max
            };
        }

        /// <summary>
        /// Returns a structured miss when <paramref name="profilesRoot"/> is too long for
        /// <paramref name="kind"/>; null when it fits.
        /// </summary>
        public static PathBudgetMiss? CheckProfilesRoot(string profilesRoot, PbiVersionKind kind)
        {
            ArgumentNullException.ThrowIfNull(profilesRoot);
            int max = MaxProfilesRootLength(kind);
            if (profilesRoot.TrimEnd('\\', '/').Length <= max)
            {
                return null;
            }

            return new PathBudgetMiss
            {
                Scope = PathBudgetScope.ProfilesRoot,
                Kind = kind,
                ActualLength = profilesRoot.Length,
                MaxLength = max
            };
        }

        // "\" + 12-char profile folder + "\AppData\Local"
        private static int ProfileLayoutSuffixLength =>
            1 + ProfilePaths.FolderNameLength
            + 1 + ProfilePaths.AppDataFolderName.Length
            + 1 + ProfilePaths.LocalFolderName.Length;

        private static int WorkspaceSuffixLength(PbiVersionKind kind) =>
            kind switch
            {
                PbiVersionKind.Desktop => DesktopWorkspaceSuffixLength,
                PbiVersionKind.ReportServer => ReportServerWorkspaceSuffixLength,
                _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null)
            };
    }
}
