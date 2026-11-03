namespace PowerBILauncher.Tests;

public class PbiExecutablePolicyTests
{
    [Fact]
    public void Accepts_existing_PBIDesktop_exe_whose_product_name_mentions_Power_BI()
    {
        var facts = new PbiExecutableFacts(
            Exists: true,
            FileName: "PBIDesktop.exe",
            ProductName: "Microsoft Power BI Desktop",
            FileDescription: "PBIDesktop");

        Assert.True(PbiExecutablePolicy.IsPowerBiExecutable(facts));
    }

    [Fact]
    public void Accepts_existing_PBIDesktop_exe_whose_file_description_mentions_Power_BI()
    {
        var facts = new PbiExecutableFacts(
            Exists: true,
            FileName: "PBIDesktop.exe",
            ProductName: "Microsoft Corporation",
            FileDescription: "Power BI Desktop");

        Assert.True(PbiExecutablePolicy.IsPowerBiExecutable(facts));
    }

    [Fact]
    public void Accepts_PBIDesktop_exe_regardless_of_leaf_casing()
    {
        var facts = new PbiExecutableFacts(
            Exists: true,
            FileName: "pbidesktop.EXE",
            ProductName: "Power BI Desktop",
            FileDescription: null);

        Assert.True(PbiExecutablePolicy.IsPowerBiExecutable(facts));
    }

    [Fact]
    public void Rejects_missing_file()
    {
        var facts = new PbiExecutableFacts(
            Exists: false,
            FileName: "PBIDesktop.exe",
            ProductName: "Power BI Desktop",
            FileDescription: "Power BI Desktop");

        Assert.False(PbiExecutablePolicy.IsPowerBiExecutable(facts));
    }

    [Fact]
    public void Rejects_notepad_even_when_the_file_exists()
    {
        var facts = new PbiExecutableFacts(
            Exists: true,
            FileName: "notepad.exe",
            ProductName: "Microsoft® Windows® Operating System",
            FileDescription: "Notepad");

        Assert.False(PbiExecutablePolicy.IsPowerBiExecutable(facts));
    }

    [Fact]
    public void Rejects_PBIDesktopRS_exe()
    {
        var facts = new PbiExecutableFacts(
            Exists: true,
            FileName: "PBIDesktopRS.exe",
            ProductName: "Power BI Report Server",
            FileDescription: "Power BI Report Server");

        Assert.False(PbiExecutablePolicy.IsPowerBiExecutable(facts));
    }

    [Fact]
    public void Rejects_PBIDesktop_exe_whose_version_info_does_not_mention_Power_BI()
    {
        var facts = new PbiExecutableFacts(
            Exists: true,
            FileName: "PBIDesktop.exe",
            ProductName: "Widget",
            FileDescription: "Widget Launcher");

        Assert.False(PbiExecutablePolicy.IsPowerBiExecutable(facts));
    }
}

public class PbiVersionKindPolicyTests
{
    [Fact]
    public void Infers_ReportServer_from_Desktop_RS_folder()
    {
        PbiVersionKind kind = PbiVersionKindPolicy.Infer(
            @"C:\Program Files\Microsoft Power BI Desktop RS\bin\PBIDesktop.exe",
            productName: "Microsoft Power BI Desktop",
            fileDescription: null);

        Assert.Equal(PbiVersionKind.ReportServer, kind);
        Assert.Equal("Report Server", PbiVersionKindPolicy.ScanNameHead(kind));
    }

    [Fact]
    public void Infers_ReportServer_from_version_info()
    {
        PbiVersionKind kind = PbiVersionKindPolicy.Infer(
            @"D:\Tools\PBIDesktop\bin\PBIDesktop.exe",
            productName: "Power BI Report Server",
            fileDescription: null);

        Assert.Equal(PbiVersionKind.ReportServer, kind);
    }

    [Fact]
    public void Infers_Desktop_otherwise()
    {
        PbiVersionKind kind = PbiVersionKindPolicy.Infer(
            @"C:\Program Files\Microsoft Power BI Desktop\bin\PBIDesktop.exe",
            productName: "Microsoft Power BI Desktop",
            fileDescription: "Power BI Desktop");

        Assert.Equal(PbiVersionKind.Desktop, kind);
        Assert.Equal("Desktop", PbiVersionKindPolicy.ScanNameHead(kind));
    }

    [Fact]
    public void Microsoft_Store_is_a_WindowsApps_path_or_a_store_package_flag()
    {
        Assert.True(PbiVersionKindPolicy.IsMicrosoftStore(
            @"C:\Program Files\WindowsApps\Microsoft.MicrosoftPowerBIDesktop_2.140.0.0_x64__8wekyb3d8bbwe\bin\PBIDesktop.exe"));
        Assert.True(PbiVersionKindPolicy.IsMicrosoftStore(
            @"D:\Redirected\PBIDesktop.exe",
            isStorePackage: true));
        Assert.False(PbiVersionKindPolicy.IsMicrosoftStore(
            @"C:\Program Files\Microsoft Power BI Desktop\bin\PBIDesktop.exe"));
    }
}
