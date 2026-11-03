namespace PowerBILauncher
{
    public enum ProfileLaunchRemediation
    {
        None,
        Settings,
        EditProfile
    }

    public enum ProfileLaunchErrorKind
    {
        MissingFile,
        NotPowerBiDesktop,
        NoDefaultVersion,
        DanglingVersion,
        MicrosoftStoreVersion,
        PasswordUnavailable,
        ProcessStart,
        ProfileFolderMigration,
        ProfileFolderMigrationLeftovers,
        PathTooLong,
        ProfileFolderUnavailable
    }

    public sealed class ProfileLaunchResult
    {
        public bool Succeeded { get; init; }
        public ProfileLaunchErrorKind? ErrorKind { get; init; }
        public ProfileLaunchRemediation Remediation { get; init; }
        public string? OsDetail { get; init; }
        public PathBudgetMiss? PathBudget { get; init; }

        public static ProfileLaunchResult Success() =>
            new() { Succeeded = true, Remediation = ProfileLaunchRemediation.None };

        public static ProfileLaunchResult Fail(
            ProfileLaunchErrorKind kind,
            ProfileLaunchRemediation remediation,
            string? osDetail = null,
            PathBudgetMiss? pathBudget = null) =>
            new()
            {
                Succeeded = false,
                ErrorKind = kind,
                Remediation = remediation,
                OsDetail = osDetail,
                PathBudget = pathBudget
            };
    }
}
