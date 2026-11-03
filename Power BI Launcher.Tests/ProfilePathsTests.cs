using System;
using System.IO;

namespace PowerBILauncher.Tests;

public class ProfilePathsTests
{
    [Fact]
    public void ForProfile_builds_short_root_and_appdata_paths()
    {
        var profileId = Guid.Parse("1234abcd-5678-4ef0-9abc-def012345678");
        const string settingsRoot = @"C:\LauncherData\Profiles";

        var paths = ProfilePaths.ForProfile(settingsRoot, profileId);

        Assert.Equal(Path.Combine(settingsRoot, "1234abcd5678"), paths.Root);
        Assert.Equal(Path.Combine(paths.Root, @"AppData\Local"), paths.LocalAppData);
        Assert.Equal(Path.Combine(paths.Root, @"AppData\Roaming"), paths.RoamingAppData);
        Assert.Equal(Path.Combine(paths.LocalAppData, "Temp"), paths.Temp);
    }

    [Fact]
    public void ForProfile_keeps_known_folder_layout_under_user_profile()
    {
        // Windows resolves LocalApplicationData as %USERPROFILE%\AppData\Local regardless of the
        // LOCALAPPDATA variable; .NET Framework returns "" when it is missing and Power BI then
        // fails with "Access to the path 'Microsoft\Power BI Desktop SSRS' is denied".
        var paths = ProfilePaths.ForProfile(@"C:\P", Guid.NewGuid());

        Assert.Equal(Path.Combine(paths.Root, "AppData", "Local"), paths.LocalAppData);
        Assert.Equal(Path.Combine(paths.Root, "AppData", "Roaming"), paths.RoamingAppData);
    }

    [Fact]
    public void FolderName_is_twelve_lowercase_hex_digits()
    {
        string name = ProfilePaths.FolderName(Guid.Parse("ABCDEF01-2345-6789-ABCD-EF0123456789"));

        Assert.Equal("abcdef012345", name);
        Assert.Equal(ProfilePaths.FolderNameLength, name.Length);
    }

    [Fact]
    public void LegacyForProfile_builds_guid_root_and_nested_appdata_paths()
    {
        var profileId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        const string settingsRoot = @"C:\LauncherData\Profiles";

        var paths = ProfilePaths.LegacyForProfile(settingsRoot, profileId);

        Assert.Equal(Path.Combine(settingsRoot, profileId.ToString("D")), paths.Root);
        Assert.Equal(Path.Combine(paths.Root, @"AppData\Local"), paths.LocalAppData);
        Assert.Equal(Path.Combine(paths.Root, @"AppData\Roaming"), paths.RoamingAppData);
        Assert.Equal(Path.Combine(paths.LocalAppData, "Temp"), paths.Temp);
    }

    [Fact]
    public void Default_profiles_root_fits_report_server_budget_for_long_user_names()
    {
        // Real-world shape: C:\Users\<name>\AppData\Local\Power BI Launcher\Profiles.
        // Windows caps SAM account names at 20 characters; the legacy layout broke at 12.
        string root = Path.Combine(@"C:\Users", new string('u', 30), @"AppData\Local\Power BI Launcher\Profiles");

        var paths = ProfilePaths.ForProfile(root, Guid.NewGuid());

        Assert.Null(PowerBiPathBudget.CheckLocalAppData(paths.LocalAppData, PbiVersionKind.ReportServer));
    }

    [Fact]
    public void Legacy_layout_overflowed_report_server_budget_for_the_reported_user()
    {
        // Regression anchor for the "We weren't able to connect to the model" report.
        const string root = @"C:\Users\PedroRoseira\AppData\Local\Power BI Launcher\Profiles";
        var profileId = Guid.Parse("2284b569-cddb-459f-914b-d3d9369e051b");

        ProfilePaths legacy = ProfilePaths.LegacyForProfile(root, profileId);
        ProfilePaths current = ProfilePaths.ForProfile(root, profileId);

        Assert.Equal(113, legacy.LocalAppData.Length);
        Assert.NotNull(PowerBiPathBudget.CheckLocalAppData(legacy.LocalAppData, PbiVersionKind.ReportServer));
        Assert.Null(PowerBiPathBudget.CheckLocalAppData(legacy.LocalAppData, PbiVersionKind.Desktop));
        Assert.Null(PowerBiPathBudget.CheckLocalAppData(current.LocalAppData, PbiVersionKind.ReportServer));
    }
}
