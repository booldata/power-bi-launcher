namespace PowerBILauncher.Tests;

public class VersionRegistryRowTests
{
    [Fact]
    public void PathDisplay_keeps_a_short_path_and_the_full_path_for_the_tooltip()
    {
        const string path = @"C:\PBI\PBIDesktop.exe";
        VersionRegistryRow row = Row(path);

        Assert.Equal(path, row.PathDisplay);
        Assert.Equal(path, row.PathTooltip);
    }

    [Fact]
    public void PathDisplay_truncates_only_above_the_length_threshold()
    {
        string path = new string('p', VersionRegistryRow.PathDisplayMaxLength + 1);
        VersionRegistryRow row = Row(path);

        Assert.Equal(VersionRegistryRow.PathDisplayMaxLength, row.PathDisplay.Length);
        Assert.EndsWith("…", row.PathDisplay);
        Assert.Equal(path, row.PathTooltip);
        Assert.NotEqual(path, row.PathDisplay);
    }

    [Fact]
    public void KindBadgeText_is_Desktop_or_Report_Server()
    {
        VersionRegistryRow desktop = Row(@"C:\PBI\PBIDesktop.exe", kind: PbiVersionKind.Desktop);
        VersionRegistryRow reportServer = Row(
            @"C:\PBI RS\PBIDesktop.exe",
            kind: PbiVersionKind.ReportServer);

        Assert.Equal("Desktop", desktop.KindBadgeText);
        Assert.Equal("Report Server", reportServer.KindBadgeText);
    }

    [Fact]
    public void AutomationName_includes_Default_Version_only_when_default()
    {
        VersionRegistryRow fallback = Row(
            @"C:\PBI\PBIDesktop.exe",
            displayName: "Desktop · Downloaded",
            isDefault: false);
        VersionRegistryRow defaultRow = Row(
            @"C:\PBI\PBIDesktop.exe",
            displayName: "Desktop · Downloaded",
            isDefault: true);

        Assert.Equal("Desktop · Downloaded", fallback.AutomationName);
        Assert.Equal("Desktop · Downloaded, Default Version", defaultRow.AutomationName);
        Assert.Equal(defaultRow.AutomationName, defaultRow.ToString());
    }

    [Fact]
    public void CanSetDefault_is_false_only_for_the_default_row()
    {
        Assert.False(Row(@"C:\PBI\PBIDesktop.exe", isDefault: true).CanSetDefault);
        Assert.True(Row(@"C:\PBI\PBIDesktop.exe", isDefault: false).CanSetDefault);
    }

    [Fact]
    public void RemoveTooltip_explains_why_the_default_cannot_be_removed()
    {
        Assert.Null(Row(@"C:\PBI\PBIDesktop.exe", isDefault: false).RemoveTooltip);
        Assert.Null(Row(@"C:\PBI\PBIDesktop.exe", isDefault: true, versionCount: 1).RemoveTooltip);
        Assert.Equal(
            "Set another version as default before removing this one.",
            Row(@"C:\PBI\PBIDesktop.exe", isDefault: true, versionCount: 2).RemoveTooltip);
    }

    private static VersionRegistryRow Row(
        string path,
        bool isDefault = false,
        int versionCount = 2,
        PbiVersionKind kind = PbiVersionKind.Desktop,
        string displayName = "Desktop · Downloaded")
    {
        var version = new PbiVersion
        {
            DisplayName = displayName,
            Path = path,
            Kind = kind
        };
        return new VersionRegistryRow(
            version,
            isDefault: isDefault,
            isRenaming: false,
            versionCount: versionCount);
    }
}
