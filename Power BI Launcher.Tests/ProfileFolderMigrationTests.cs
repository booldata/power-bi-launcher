using System;
using System.IO;

namespace PowerBILauncher.Tests;

public class ProfileFolderMigrationTests : IDisposable
{
    private readonly string _root;

    public ProfileFolderMigrationTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "pbi-folder-migration-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_root))
            {
                Directory.Delete(_root, recursive: true);
            }
        }
        catch
        {
        }
    }

    [Fact]
    public void TryMigrate_is_not_needed_when_no_legacy_folder_exists()
    {
        var profile = new Profile();

        var result = ProfileFolderMigration.TryMigrate(_root, profile);

        Assert.Equal(ProfileFolderMigrationOutcome.NotNeeded, result.Outcome);
        Assert.False(result.ProfileChanged);
        Assert.False(Directory.Exists(ProfilePaths.ForProfile(_root, profile.Id).Root));
    }

    [Fact]
    public void TryMigrate_moves_legacy_layout_into_short_layout()
    {
        var profile = new Profile();
        ProfilePaths legacy = ProfilePaths.LegacyForProfile(_root, profile.Id);
        ProfilePaths current = ProfilePaths.ForProfile(_root, profile.Id);
        WriteFile(Path.Combine(legacy.LocalAppData, "Microsoft", "Power BI Desktop", "user.json"), "local");
        WriteFile(Path.Combine(legacy.RoamingAppData, "Microsoft", "settings.xml"), "roaming");
        WriteFile(Path.Combine(legacy.Temp, "scratch.tmp"), "tmp");
        WriteFile(Path.Combine(legacy.Root, "Desktop", "note.txt"), "desktop");

        var result = ProfileFolderMigration.TryMigrate(_root, profile);

        Assert.Equal(ProfileFolderMigrationOutcome.Migrated, result.Outcome);
        Assert.False(Directory.Exists(legacy.Root));
        Assert.Equal("local", File.ReadAllText(Path.Combine(current.LocalAppData, "Microsoft", "Power BI Desktop", "user.json")));
        Assert.Equal("roaming", File.ReadAllText(Path.Combine(current.RoamingAppData, "Microsoft", "settings.xml")));
        Assert.Equal("tmp", File.ReadAllText(Path.Combine(current.Temp, "scratch.tmp")));
        Assert.Equal("desktop", File.ReadAllText(Path.Combine(current.Root, "Desktop", "note.txt")));
    }

    [Fact]
    public void TryMigrate_folds_interim_layout_without_appdata_level_into_current_layout()
    {
        var profile = new Profile();
        ProfilePaths current = ProfilePaths.ForProfile(_root, profile.Id);
        string interimLocal = Path.Combine(current.Root, "Local");
        string interimRoaming = Path.Combine(current.Root, "Roaming");
        WriteFile(Path.Combine(interimLocal, "Microsoft", "Power BI Desktop SSRS", "user.json"), "local");
        WriteFile(Path.Combine(interimRoaming, "Microsoft", "settings.xml"), "roaming");
        // The shell already created %USERPROFILE%\AppData\Roaming while the interim layout was live.
        WriteFile(Path.Combine(current.RoamingAppData, "shell.txt"), "shell");

        var result = ProfileFolderMigration.TryMigrate(_root, profile);

        Assert.Equal(ProfileFolderMigrationOutcome.Migrated, result.Outcome);
        Assert.False(Directory.Exists(interimLocal));
        Assert.False(Directory.Exists(interimRoaming));
        Assert.Equal("local", File.ReadAllText(Path.Combine(current.LocalAppData, "Microsoft", "Power BI Desktop SSRS", "user.json")));
        Assert.Equal("roaming", File.ReadAllText(Path.Combine(current.RoamingAppData, "Microsoft", "settings.xml")));
        Assert.Equal("shell", File.ReadAllText(Path.Combine(current.RoamingAppData, "shell.txt")));
    }

    [Fact]
    public void TryMigrate_merges_when_short_layout_already_has_content_and_keeps_newer_file()
    {
        var profile = new Profile();
        ProfilePaths legacy = ProfilePaths.LegacyForProfile(_root, profile.Id);
        ProfilePaths current = ProfilePaths.ForProfile(_root, profile.Id);
        WriteFile(Path.Combine(legacy.LocalAppData, "a.txt"), "legacy-a");
        WriteFile(Path.Combine(legacy.LocalAppData, "sub", "b.txt"), "legacy-b");
        WriteFile(Path.Combine(legacy.Root, "avatar.png"), "old-avatar");
        WriteFile(Path.Combine(current.LocalAppData, "a.txt"), "new-a");
        WriteFile(Path.Combine(current.Root, "avatar.png"), "new-avatar");

        var result = ProfileFolderMigration.TryMigrate(_root, profile);

        Assert.Equal(ProfileFolderMigrationOutcome.Migrated, result.Outcome);
        Assert.False(Directory.Exists(legacy.Root));
        Assert.Equal("new-a", File.ReadAllText(Path.Combine(current.LocalAppData, "a.txt")));
        Assert.Equal("legacy-b", File.ReadAllText(Path.Combine(current.LocalAppData, "sub", "b.txt")));
        Assert.Equal("new-avatar", File.ReadAllText(Path.Combine(current.Root, "avatar.png")));
    }

    [Fact]
    public void TryMigrate_rewrites_custom_icon_path_into_short_layout()
    {
        var profile = new Profile();
        ProfilePaths legacy = ProfilePaths.LegacyForProfile(_root, profile.Id);
        ProfilePaths current = ProfilePaths.ForProfile(_root, profile.Id);
        string legacyIcon = Path.Combine(legacy.Root, "me.png");
        WriteFile(legacyIcon, "png");
        profile.IconPath = legacyIcon;

        var result = ProfileFolderMigration.TryMigrate(_root, profile);

        Assert.Equal(ProfileFolderMigrationOutcome.Migrated, result.Outcome);
        Assert.True(result.ProfileChanged);
        Assert.Equal(Path.Combine(Path.GetFullPath(current.Root), "me.png"), profile.IconPath);
        Assert.True(File.Exists(profile.IconPath));
    }

    [Fact]
    public void TryMigrate_leaves_icon_path_outside_profile_folder_alone()
    {
        var profile = new Profile();
        ProfilePaths legacy = ProfilePaths.LegacyForProfile(_root, profile.Id);
        Directory.CreateDirectory(legacy.LocalAppData);
        string external = Path.Combine(_root, "elsewhere.png");
        WriteFile(external, "png");
        profile.IconPath = external;

        var result = ProfileFolderMigration.TryMigrate(_root, profile);

        Assert.Equal(ProfileFolderMigrationOutcome.Migrated, result.Outcome);
        Assert.False(result.ProfileChanged);
        Assert.Equal(external, profile.IconPath);
    }

    [Fact]
    public void TryMigrate_reports_failure_and_leaves_legacy_root_when_a_file_is_locked()
    {
        var profile = new Profile();
        ProfilePaths legacy = ProfilePaths.LegacyForProfile(_root, profile.Id);
        ProfilePaths current = ProfilePaths.ForProfile(_root, profile.Id);
        string locked = Path.Combine(legacy.LocalAppData, "locked.bin");
        WriteFile(locked, "busy");
        WriteFile(Path.Combine(legacy.RoamingAppData, "free.txt"), "free");

        ProfileFolderMigrationResult result;
        using (new FileStream(locked, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
        {
            result = ProfileFolderMigration.TryMigrate(_root, profile);
        }

        Assert.Equal(ProfileFolderMigrationOutcome.Failed, result.Outcome);
        Assert.NotNull(result.Error);
        Assert.True(Directory.Exists(legacy.Root));
        Assert.True(File.Exists(locked));

        // Second attempt after the lock is gone completes the move.
        var retry = ProfileFolderMigration.TryMigrate(_root, profile);

        Assert.Equal(ProfileFolderMigrationOutcome.Migrated, retry.Outcome);
        Assert.False(Directory.Exists(legacy.Root));
        Assert.Equal("busy", File.ReadAllText(Path.Combine(current.LocalAppData, "locked.bin")));
        Assert.Equal("free", File.ReadAllText(Path.Combine(current.RoamingAppData, "free.txt")));
    }

    [Fact]
    public void TryMigrate_is_not_needed_for_blank_root()
    {
        var result = ProfileFolderMigration.TryMigrate("  ", new Profile());

        Assert.Equal(ProfileFolderMigrationOutcome.NotNeeded, result.Outcome);
    }

    private static void WriteFile(string path, string contents)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, contents);
    }
}
