namespace PowerBILauncher.Tests;

public class LaunchCoordinatorSeamTests
{
    [Fact]
    public void Window_and_pages_do_not_call_VERSIONINFO_or_process_start_for_launch()
    {
        string repo = FindRepoRoot();
        string[] files =
        [
            Path.Combine(repo, "Power BI Launcher", "Shell", "MainWindow.xaml.cs"),
            Path.Combine(repo, "Power BI Launcher", "EditProfile", "EditProfilePage.xaml.cs"),
            Path.Combine(repo, "Power BI Launcher", "ProfileBoard", "ProfileBoardPage.xaml.cs")
        ];

        foreach (string file in files)
        {
            string source = File.ReadAllText(file);
            Assert.DoesNotContain("FileVersionInfo", source, StringComparison.Ordinal);
            Assert.DoesNotContain("CreateLaunchStartInfo", source, StringComparison.Ordinal);
            Assert.DoesNotContain("CredRead", source, StringComparison.Ordinal);
            Assert.DoesNotContain("CredWrite", source, StringComparison.Ordinal);
            Assert.DoesNotContain("WindowsCredentialStore", source, StringComparison.Ordinal);
        }

        string mainWindow = File.ReadAllText(files[0]);
        Assert.Contains("_lifecycle.TryLaunch", mainWindow, StringComparison.Ordinal);
    }

    private static string FindRepoRoot()
    {
        string? directory = AppContext.BaseDirectory;
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory, "Power BI Launcher.sln")))
            {
                return directory;
            }

            directory = Directory.GetParent(directory)?.FullName;
        }

        throw new DirectoryNotFoundException("Could not find the repository root from the test output directory.");
    }
}
