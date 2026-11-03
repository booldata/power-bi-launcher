using PowerBILauncher.Tests.PbiDesktopFixture;

namespace PowerBILauncher.Tests;

public class FileVersionInfoReaderTests : IDisposable
{
    private readonly string _directory;

    public FileVersionInfoReaderTests()
    {
        _directory = Path.Combine(Path.GetTempPath(), "pbi-versioninfo-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_directory);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_directory))
            {
                Directory.Delete(_directory, recursive: true);
            }
        }
        catch
        {
        }
    }

    [Fact]
    public void Reads_power_bi_and_non_power_bi_version_info_from_controlled_fixtures()
    {
        string positive = Path.Combine(_directory, "positive", "PBIDesktop.exe");
        string negative = Path.Combine(_directory, "negative", "PBIDesktop.exe");
        Directory.CreateDirectory(Path.GetDirectoryName(positive)!);
        Directory.CreateDirectory(Path.GetDirectoryName(negative)!);
        File.Copy(typeof(Marker).Assembly.Location, positive); // VERSIONINFO Product contains "Power BI"
        File.Copy(typeof(FileVersionInfoReaderTests).Assembly.Location, negative); // VERSIONINFO Product is "Launcher tests"

        var reader = new FileVersionInfoReader();
        FileVersionInfoStrings positiveInfo = reader.Read(positive);
        FileVersionInfoStrings negativeInfo = reader.Read(negative);

        Assert.Contains("Power BI", positiveInfo.ProductName ?? "", StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Power BI", negativeInfo.ProductName ?? "", StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Power BI", negativeInfo.FileDescription ?? "", StringComparison.OrdinalIgnoreCase);
    }
}
