namespace PowerBILauncher.Tests;

public class ScanPolicyTests
{
    private static readonly string DesktopPath =
        @"C:\Program Files\Microsoft Power BI Desktop\bin\PBIDesktop.exe";
    private static readonly string ReportServerPath =
        @"C:\Program Files\Microsoft Power BI Desktop RS\bin\PBIDesktop.exe";
    private static readonly string StorePath =
        @"C:\Program Files\WindowsApps\Microsoft.MicrosoftPowerBIDesktop_2.140.0.0_x64__8wekyb3d8bbwe\bin\PBIDesktop.exe";
    private static readonly string JunkPath =
        @"C:\Windows\System32\notepad.exe";

    [Fact]
    public void Accept_maps_kind_and_generated_display_name_and_drops_junk()
    {
        var inspector = new StubExecutableInspector();
        inspector.Pass(DesktopPath);
        inspector.PassReportServer(ReportServerPath);
        inspector.NotPowerBi(JunkPath, "notepad.exe");

        IReadOnlyList<ScanAcceptedVersion> accepted = ScanPolicy.Accept(
            [
                new PbiDiscoveryCandidate
                {
                    Path = DesktopPath,
                    ProductName = "Microsoft Power BI Desktop",
                    DisplayHint = "Microsoft Power BI Desktop (May 2026) (x64)"
                },
                new PbiDiscoveryCandidate
                {
                    Path = ReportServerPath,
                    ProductName = "Microsoft Power BI Report Server",
                    DisplayHint = "Microsoft Power BI Desktop (September 2025) (x64)"
                },
                new PbiDiscoveryCandidate
                {
                    Path = JunkPath,
                    ProductName = "Notepad"
                }
            ],
            inspector.Inspect);

        Assert.Equal(2, accepted.Count);
        Assert.Equal(DesktopPath, accepted[0].Path);
        Assert.Equal(PbiVersionKind.Desktop, accepted[0].Kind);
        Assert.Equal("Desktop · Downloaded · May 2026", accepted[0].DisplayName);
        Assert.Equal(ReportServerPath, accepted[1].Path);
        Assert.Equal(PbiVersionKind.ReportServer, accepted[1].Kind);
        Assert.Equal("Report Server · September 2025", accepted[1].DisplayName);
    }

    [Fact]
    public void Accept_names_microsoft_store_desktop_even_without_a_month_year()
    {
        var inspector = new StubExecutableInspector();
        inspector.Pass(StorePath);

        IReadOnlyList<ScanAcceptedVersion> accepted = ScanPolicy.Accept(
            [
                new PbiDiscoveryCandidate
                {
                    Path = StorePath,
                    DisplayHint = "Power BI Desktop",
                    IsStorePackage = true
                }
            ],
            inspector.Inspect);

        Assert.Single(accepted);
        Assert.Equal(PbiVersionKind.Desktop, accepted[0].Kind);
        Assert.Equal("Desktop · Microsoft Store", accepted[0].DisplayName);
        Assert.True(accepted[0].IsMicrosoftStore);
    }

    [Fact]
    public void Accept_names_downloaded_desktop_when_no_month_year_is_known()
    {
        var inspector = new StubExecutableInspector();
        inspector.Pass(DesktopPath);

        IReadOnlyList<ScanAcceptedVersion> accepted = ScanPolicy.Accept(
            [new PbiDiscoveryCandidate { Path = DesktopPath }],
            inspector.Inspect);

        Assert.Single(accepted);
        Assert.Equal("Desktop · Downloaded", accepted[0].DisplayName);
    }

    [Fact]
    public void Accept_keeps_arp_month_year_when_the_same_path_was_found_in_a_default_folder_first()
    {
        var inspector = new StubExecutableInspector();
        inspector.PassReportServer(ReportServerPath);

        IReadOnlyList<ScanAcceptedVersion> accepted = ScanPolicy.Accept(
            [
                new PbiDiscoveryCandidate { Path = ReportServerPath },
                new PbiDiscoveryCandidate
                {
                    Path = ReportServerPath,
                    DisplayHint = "Microsoft Power BI Desktop (September 2025) (x64)"
                }
            ],
            inspector.Inspect);

        Assert.Single(accepted);
        Assert.Equal(PbiVersionKind.ReportServer, accepted[0].Kind);
        Assert.Equal("Report Server · September 2025", accepted[0].DisplayName);
    }

    [Fact]
    public void Accept_keeps_several_report_server_installs_distinct()
    {
        string olderRs =
            @"D:\Archive\Microsoft Power BI Desktop RS\bin\PBIDesktop.exe";
        var inspector = new StubExecutableInspector();
        inspector.PassReportServer(ReportServerPath);
        inspector.PassReportServer(olderRs);

        IReadOnlyList<ScanAcceptedVersion> accepted = ScanPolicy.Accept(
            [
                new PbiDiscoveryCandidate
                {
                    Path = ReportServerPath,
                    DisplayHint = "Microsoft Power BI Desktop (September 2025) (x64)"
                },
                new PbiDiscoveryCandidate
                {
                    Path = olderRs,
                    DisplayHint = "Microsoft Power BI Desktop (May 2024) (x64)"
                }
            ],
            inspector.Inspect);

        Assert.Equal(2, accepted.Count);
        Assert.Equal("Report Server · September 2025", accepted[0].DisplayName);
        Assert.Equal("Report Server · May 2024", accepted[1].DisplayName);
    }

    [Fact]
    public void Accept_disambiguates_report_server_installs_that_share_a_generated_name()
    {
        string olderRs = @"D:\Archive\RS-Old\bin\PBIDesktop.exe";
        var inspector = new StubExecutableInspector();
        inspector.PassReportServer(ReportServerPath);
        inspector.PassReportServer(olderRs);

        IReadOnlyList<ScanAcceptedVersion> accepted = ScanPolicy.Accept(
            [
                new PbiDiscoveryCandidate { Path = ReportServerPath },
                new PbiDiscoveryCandidate { Path = olderRs }
            ],
            inspector.Inspect);

        Assert.Equal(2, accepted.Count);
        Assert.Equal("Report Server · Microsoft Power BI Desktop RS", accepted[0].DisplayName);
        Assert.Equal("Report Server · RS-Old", accepted[1].DisplayName);
    }

    [Fact]
    public void Accept_uses_arp_product_name_for_kind_when_the_path_is_not_a_default_folder()
    {
        string archivePath = @"D:\Archive\RS\PBIDesktop.exe";
        var inspector = new StubExecutableInspector();
        inspector.Pass(archivePath);

        IReadOnlyList<ScanAcceptedVersion> accepted = ScanPolicy.Accept(
            [
                new PbiDiscoveryCandidate
                {
                    Path = archivePath,
                    ProductName = "Microsoft Power BI Report Server",
                    DisplayHint = "Microsoft Power BI Desktop (September 2025) (x64)"
                }
            ],
            inspector.Inspect);

        Assert.Single(accepted);
        Assert.Equal(PbiVersionKind.ReportServer, accepted[0].Kind);
        Assert.Equal("Report Server · September 2025", accepted[0].DisplayName);
    }

    [Fact]
    public void Accept_uses_start_menu_or_folder_hint_for_kind_when_versioninfo_is_desktop()
    {
        string archivePath = @"D:\Archive\September\PBIDesktop.exe";
        var inspector = new StubExecutableInspector();
        inspector.Pass(archivePath);

        IReadOnlyList<ScanAcceptedVersion> accepted = ScanPolicy.Accept(
            [
                new PbiDiscoveryCandidate
                {
                    Path = archivePath,
                    DisplayHint = "Microsoft Power BI Desktop RS"
                }
            ],
            inspector.Inspect);

        Assert.Single(accepted);
        Assert.Equal(PbiVersionKind.ReportServer, accepted[0].Kind);
        Assert.Equal("Report Server", accepted[0].DisplayName);
    }

    [Fact]
    public void Confirm_adds_selected_and_removes_unticked_discoveries_but_keeps_browse_only()
    {
        var inspector = new StubExecutableInspector();
        inspector.Pass(DesktopPath);
        inspector.PassReportServer(ReportServerPath);
        string browsePath = @"D:\Archive\PBIDesktop.exe";
        inspector.Pass(browsePath);

        string baseDir = Path.Combine(Path.GetTempPath(), "pbi-scan-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(baseDir);
        var lifecycle = new ProfileLifecycle(baseDir, executableInspector: inspector);
        try
        {
        lifecycle.Load();
        Assert.True(lifecycle.TryRegisterVersion(DesktopPath, out _));
        Assert.True(lifecycle.TryRegisterVersion(browsePath, out _));
        Guid browseId = lifecycle.Settings.Versions.Single(v =>
            string.Equals(v.Path, browsePath, StringComparison.OrdinalIgnoreCase)).Id;

        var found = ScanPolicy.Accept(
            [
                new PbiDiscoveryCandidate { Path = DesktopPath, DisplayHint = "May 2026" },
                new PbiDiscoveryCandidate { Path = ReportServerPath, DisplayHint = "September 2025" }
            ],
            inspector.Inspect);

        var selected = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ReportServerPath };
        List<PbiVersion> versions = lifecycle.Settings.Versions;
        Guid? defaultId = lifecycle.Settings.DefaultVersionId;
        lifecycle.ConfirmScan(found, selected, versions, ref defaultId);
        lifecycle.Settings.DefaultVersionId = defaultId;

        Assert.DoesNotContain(versions, v => string.Equals(v.Path, DesktopPath, StringComparison.OrdinalIgnoreCase));
        Assert.Contains(versions, v => string.Equals(v.Path, ReportServerPath, StringComparison.OrdinalIgnoreCase));
        Assert.Contains(versions, v => v.Id == browseId);
        Assert.Equal("Report Server · September 2025", versions.Single(v =>
            string.Equals(v.Path, ReportServerPath, StringComparison.OrdinalIgnoreCase)).DisplayName);
        }
        finally
        {
            try
            {
                if (Directory.Exists(baseDir))
                {
                    Directory.Delete(baseDir, recursive: true);
                }
            }
            catch
            {
            }
        }
    }

    [Fact]
    public void Confirm_replaces_a_placeholder_display_name_from_an_earlier_scan()
    {
        var inspector = new StubExecutableInspector();
        inspector.Pass(DesktopPath);

        string baseDir = Path.Combine(Path.GetTempPath(), "pbi-scan-rename-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(baseDir);
        var lifecycle = new ProfileLifecycle(baseDir, executableInspector: inspector);
        try
        {
            lifecycle.Load();
            Assert.True(lifecycle.TryRegisterVersion(DesktopPath, out _));
            lifecycle.Settings.Versions[0].DisplayName = "Desktop";

            var found = ScanPolicy.Accept(
                [new PbiDiscoveryCandidate { Path = DesktopPath, DisplayHint = "May 2026" }],
                inspector.Inspect);
            List<PbiVersion> versions = lifecycle.Settings.Versions;
            Guid? defaultId = lifecycle.Settings.DefaultVersionId;
            lifecycle.ConfirmScan(
                found,
                new HashSet<string>(StringComparer.OrdinalIgnoreCase) { DesktopPath },
                versions,
                ref defaultId);

            Assert.Equal("Desktop · Downloaded · May 2026", versions[0].DisplayName);
        }
        finally
        {
            try
            {
                if (Directory.Exists(baseDir))
                {
                    Directory.Delete(baseDir, recursive: true);
                }
            }
            catch
            {
            }
        }
    }

    [Fact]
    public void Confirm_keeps_a_user_rename()
    {
        var inspector = new StubExecutableInspector();
        inspector.Pass(DesktopPath);

        string baseDir = Path.Combine(Path.GetTempPath(), "pbi-scan-keep-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(baseDir);
        var lifecycle = new ProfileLifecycle(baseDir, executableInspector: inspector);
        try
        {
            lifecycle.Load();
            Assert.True(lifecycle.TryRegisterVersion(DesktopPath, out _));
            lifecycle.Settings.Versions[0].DisplayName = "Client Desktop";

            var found = ScanPolicy.Accept(
                [new PbiDiscoveryCandidate { Path = DesktopPath, DisplayHint = "May 2026" }],
                inspector.Inspect);
            List<PbiVersion> versions = lifecycle.Settings.Versions;
            Guid? defaultId = lifecycle.Settings.DefaultVersionId;
            lifecycle.ConfirmScan(
                found,
                new HashSet<string>(StringComparer.OrdinalIgnoreCase) { DesktopPath },
                versions,
                ref defaultId);

            Assert.Equal("Client Desktop", versions[0].DisplayName);
        }
        finally
        {
            try
            {
                if (Directory.Exists(baseDir))
                {
                    Directory.Delete(baseDir, recursive: true);
                }
            }
            catch
            {
            }
        }
    }
}
