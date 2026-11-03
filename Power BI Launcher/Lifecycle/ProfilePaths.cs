using System;
using System.IO;

namespace PowerBILauncher
{
    /// <summary>
    /// Isolated Environment folders for one Profile under the configured Profiles root:
    /// <c>&lt;root&gt;\&lt;12-hex id&gt;\AppData\{Local,Roaming}</c>.
    /// <para>
    /// The profile folder name is short because Power BI Desktop is a .NET Framework app without
    /// long-path support, and its Analysis Services workspace adds ~125 characters below
    /// LOCALAPPDATA (see <see cref="PowerBiPathBudget"/>).
    /// </para>
    /// <para>
    /// The <c>AppData\Local</c> and <c>AppData\Roaming</c> nesting is mandatory: Windows resolves
    /// the known folders as <c>%USERPROFILE%\AppData\Local|Roaming</c> (not from LOCALAPPDATA /
    /// APPDATA), and .NET Framework's <c>Environment.GetFolderPath</c> returns "" when that folder
    /// is missing. Power BI then works with the relative path <c>Microsoft\Power BI Desktop…</c>
    /// inside its Program Files working directory and fails with "Access to the path … is denied".
    /// </para>
    /// </summary>
    public readonly struct ProfilePaths
    {
        public const string AppDataFolderName = "AppData";
        public const string LocalFolderName = "Local";
        public const string RoamingFolderName = "Roaming";
        public const string TempFolderName = "Temp";
        public const int FolderNameLength = 12;

        public ProfilePaths(string root, string localAppData, string roamingAppData, string temp)
        {
            Root = root;
            LocalAppData = localAppData;
            RoamingAppData = roamingAppData;
            Temp = temp;
        }

        public string Root { get; }
        public string LocalAppData { get; }
        public string RoamingAppData { get; }
        public string Temp { get; }

        /// <summary>
        /// Short, stable folder name for a Profile: the first 12 hex digits of its id.
        /// </summary>
        public static string FolderName(Guid profileId) =>
            profileId.ToString("N").Substring(0, FolderNameLength);

        public static ProfilePaths ForProfile(string settingsRoot, Guid profileId) =>
            ForRoot(Path.Combine(settingsRoot, FolderName(profileId)));

        /// <summary>
        /// Layout used before the short layout: <c>&lt;root&gt;\&lt;D-format guid&gt;\AppData\{Local,Roaming}</c>.
        /// Only <see cref="ProfileFolderMigration"/> and delete should touch it.
        /// </summary>
        public static ProfilePaths LegacyForProfile(string settingsRoot, Guid profileId) =>
            ForRoot(Path.Combine(settingsRoot, profileId.ToString("D")));

        private static ProfilePaths ForRoot(string root)
        {
            string appData = Path.Combine(root, AppDataFolderName);
            string localAppData = Path.Combine(appData, LocalFolderName);
            string roamingAppData = Path.Combine(appData, RoamingFolderName);
            string temp = Path.Combine(localAppData, TempFolderName);
            return new ProfilePaths(root, localAppData, roamingAppData, temp);
        }
    }
}
