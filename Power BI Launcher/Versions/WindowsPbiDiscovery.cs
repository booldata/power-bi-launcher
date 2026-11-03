using System.Reflection;
using Microsoft.Win32;
using Windows.Management.Deployment;

namespace PowerBILauncher;

/// <summary>
/// Discovers installed Power BI Desktop products. Unit tests never call this adapter.
/// </summary>
public sealed class WindowsPbiDiscovery : IPbiDiscovery
{
    public IReadOnlyList<PbiDiscoveryCandidate> FindCandidates()
    {
        var candidates = new List<PbiDiscoveryCandidate>();
        AddDefaultFolders(candidates);
        AddUninstallKeys(candidates, RegistryHive.LocalMachine, RegistryView.Registry64);
        AddUninstallKeys(candidates, RegistryHive.LocalMachine, RegistryView.Registry32);
        AddUninstallKeys(candidates, RegistryHive.CurrentUser, RegistryView.Registry64);
        AddUninstallKeys(candidates, RegistryHive.CurrentUser, RegistryView.Registry32);
        AddStorePackages(candidates);
        AddStartMenuShortcuts(candidates);
        return candidates;
    }

    private static void AddDefaultFolders(List<PbiDiscoveryCandidate> candidates)
    {
        string programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        string programFilesX86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
        AddExisting(candidates, Path.Combine(programFiles, "Microsoft Power BI Desktop", "bin", "PBIDesktop.exe"), null);
        AddExisting(candidates, Path.Combine(programFiles, "Microsoft Power BI Desktop RS", "bin", "PBIDesktop.exe"), null);
        if (!string.Equals(programFiles, programFilesX86, StringComparison.OrdinalIgnoreCase))
        {
            AddExisting(candidates, Path.Combine(programFilesX86, "Microsoft Power BI Desktop", "bin", "PBIDesktop.exe"), null);
            AddExisting(candidates, Path.Combine(programFilesX86, "Microsoft Power BI Desktop RS", "bin", "PBIDesktop.exe"), null);
        }
    }

    private static void AddUninstallKeys(List<PbiDiscoveryCandidate> candidates, RegistryHive hive, RegistryView view)
    {
        try
        {
            using RegistryKey? baseKey = RegistryKey.OpenBaseKey(hive, view);
            AddUninstallHive(candidates, baseKey?.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall"));
            AddUninstallHive(candidates, baseKey?.OpenSubKey(@"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall"));
        }
        catch
        {
            // Best-effort discovery.
        }
    }

    private static void AddUninstallHive(List<PbiDiscoveryCandidate> candidates, RegistryKey? uninstall)
    {
        if (uninstall is null)
        {
            return;
        }

        using (uninstall)
        {
            foreach (string name in uninstall.GetSubKeyNames())
            {
                using RegistryKey? key = uninstall.OpenSubKey(name);
                if (key is null)
                {
                    continue;
                }

                string? displayName = key.GetValue("DisplayName") as string;
                if (!LooksLikePowerBi(displayName))
                {
                    continue;
                }

                string? installLocation = key.GetValue("InstallLocation") as string;
                AddFromInstallLocation(candidates, installLocation, displayName, displayName);
                AddFromDisplayIcon(candidates, key.GetValue("DisplayIcon") as string, displayName, displayName);
            }
        }
    }

    private static void AddFromInstallLocation(
        List<PbiDiscoveryCandidate> candidates,
        string? installLocation,
        string? displayHint,
        string? productName)
    {
        if (string.IsNullOrWhiteSpace(installLocation))
        {
            return;
        }

        AddExisting(candidates, Path.Combine(installLocation, "bin", "PBIDesktop.exe"), displayHint, productName);
        AddExisting(candidates, Path.Combine(installLocation, "PBIDesktop.exe"), displayHint, productName);
    }

    private static void AddFromDisplayIcon(
        List<PbiDiscoveryCandidate> candidates,
        string? displayIcon,
        string? displayHint,
        string? productName)
    {
        if (string.IsNullOrWhiteSpace(displayIcon))
        {
            return;
        }

        string path = displayIcon.Trim().Trim('"');
        int comma = path.LastIndexOf(',');
        if (comma > 0)
        {
            path = path[..comma];
        }

        if (string.Equals(Path.GetFileName(path), PbiExecutablePolicy.RequiredFileName, StringComparison.OrdinalIgnoreCase))
        {
            AddExisting(candidates, path, displayHint, productName);
        }
    }

    private static void AddStorePackages(List<PbiDiscoveryCandidate> candidates)
    {
        try
        {
            var manager = new PackageManager();
            foreach (Windows.ApplicationModel.Package package in manager.FindPackagesForUser(string.Empty))
            {
                string name = package.Id.Name ?? "";
                string display = package.DisplayName ?? "";
                if (!name.Contains("PowerBI", StringComparison.OrdinalIgnoreCase) &&
                    !display.Contains("Power BI", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                string? root = package.InstalledLocation?.Path;
                if (string.IsNullOrWhiteSpace(root))
                {
                    continue;
                }

                AddExisting(
                    candidates,
                    Path.Combine(root, "bin", "PBIDesktop.exe"),
                    display,
                    isStorePackage: true);
                AddExisting(
                    candidates,
                    Path.Combine(root, "PBIDesktop.exe"),
                    display,
                    isStorePackage: true);
            }
        }
        catch
        {
            // Store APIs are unavailable in some hosts; skip.
        }
    }

    private static void AddStartMenuShortcuts(List<PbiDiscoveryCandidate> candidates)
    {
        AddStartMenuTree(
            candidates,
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.CommonStartMenu),
                "Programs"));
        AddStartMenuTree(
            candidates,
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.StartMenu),
                "Programs"));
    }

    private static void AddStartMenuTree(List<PbiDiscoveryCandidate> candidates, string root)
    {
        if (!Directory.Exists(root))
        {
            return;
        }

        try
        {
            foreach (string directory in Directory.EnumerateDirectories(root, "*Power BI*", SearchOption.AllDirectories))
            {
                string hint = Path.GetFileName(directory);
                AddExisting(candidates, Path.Combine(directory, "PBIDesktop.exe"), hint);
                AddExisting(candidates, Path.Combine(directory, "bin", "PBIDesktop.exe"), hint);
                foreach (string link in Directory.EnumerateFiles(directory, "*.lnk"))
                {
                    AddShortcutTarget(candidates, link, hint);
                }
            }
        }
        catch
        {
            // Start Menu walk is best-effort.
        }
    }

    private static bool LooksLikePowerBi(string? displayName) =>
        !string.IsNullOrEmpty(displayName) &&
        displayName.Contains("Power BI", StringComparison.OrdinalIgnoreCase);

    private static void AddShortcutTarget(
        List<PbiDiscoveryCandidate> candidates,
        string linkPath,
        string? displayHint)
    {
        string? target = TryResolveShortcut(linkPath);
        if (string.IsNullOrWhiteSpace(target))
        {
            return;
        }

        if (!string.Equals(
                Path.GetFileName(target),
                PbiExecutablePolicy.RequiredFileName,
                StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        AddExisting(candidates, target, displayHint);
    }

    private static string? TryResolveShortcut(string linkPath)
    {
        try
        {
            Type? shellType = Type.GetTypeFromProgID("WScript.Shell");
            if (shellType is null)
            {
                return null;
            }

            object? shell = Activator.CreateInstance(shellType);
            if (shell is null)
            {
                return null;
            }

            object? shortcut = shellType.InvokeMember(
                "CreateShortcut",
                BindingFlags.InvokeMethod,
                binder: null,
                target: shell,
                args: [linkPath]);
            if (shortcut is null)
            {
                return null;
            }

            return shortcut.GetType().InvokeMember(
                "TargetPath",
                BindingFlags.GetProperty,
                binder: null,
                target: shortcut,
                args: null) as string;
        }
        catch
        {
            return null;
        }
    }

    private static void AddExisting(
        List<PbiDiscoveryCandidate> candidates,
        string path,
        string? displayHint,
        string? productName = null,
        bool isStorePackage = false)
    {
        if (!File.Exists(path))
        {
            return;
        }

        candidates.Add(new PbiDiscoveryCandidate
        {
            Path = path,
            DisplayHint = displayHint,
            ProductName = productName,
            IsStorePackage = isStorePackage
        });
    }
}
