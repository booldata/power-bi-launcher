using System;
using System.IO;

namespace PowerBILauncher.Tests;

public class ProfileCustomAvatarFilesTests : IDisposable
{
    private readonly string _profilesRoot;
    private readonly Guid _profileId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");

    public ProfileCustomAvatarFilesTests()
    {
        _profilesRoot = Path.Combine(Path.GetTempPath(), "pbi-avatar-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_profilesRoot);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_profilesRoot))
            {
                Directory.Delete(_profilesRoot, recursive: true);
            }
        }
        catch
        {
        }
    }

    [Fact]
    public void CopyIntoProfileFolder_copies_valid_image_under_profile_root()
    {
        string source = Path.Combine(_profilesRoot, "source.png");
        File.WriteAllBytes(source, [0x89, 0x50, 0x4E, 0x47]);

        string destination = ProfileCustomAvatarFiles.CopyIntoProfileFolder(
            _profilesRoot,
            _profileId,
            source);

        string expectedFolder = ProfilePaths.ForProfile(_profilesRoot, _profileId).Root;
        Assert.Equal(Path.Combine(expectedFolder, "source.png"), destination);
        Assert.True(File.Exists(destination));
    }

    [Fact]
    public void CopyIntoProfileFolder_rejects_unsupported_extension()
    {
        string source = Path.Combine(_profilesRoot, "source.gif");
        File.WriteAllText(source, "gif");

        LogoValidationException ex = Assert.Throws<LogoValidationException>(() =>
            ProfileCustomAvatarFiles.CopyIntoProfileFolder(_profilesRoot, _profileId, source));

        Assert.Equal(LogoValidationKind.UnsupportedFormat, ex.Kind);
        Assert.Equal(".gif", ex.Extension);
    }

    [Fact]
    public void TryDeleteLauncherOwnedFile_deletes_file_under_profile_root()
    {
        ProfilePaths paths = ProfilePaths.ForProfile(_profilesRoot, _profileId);
        Directory.CreateDirectory(paths.Root);
        string iconPath = Path.Combine(paths.Root, "logo.png");
        File.WriteAllText(iconPath, "png");

        string? error = ProfileCustomAvatarFiles.TryDeleteLauncherOwnedFile(
            _profilesRoot,
            _profileId,
            iconPath);

        Assert.Null(error);
        Assert.False(File.Exists(iconPath));
    }

    [Fact]
    public void TryDeleteLauncherOwnedFile_skips_paths_outside_profile_root()
    {
        string outside = Path.Combine(_profilesRoot, "outside.png");
        File.WriteAllText(outside, "png");

        string? error = ProfileCustomAvatarFiles.TryDeleteLauncherOwnedFile(
            _profilesRoot,
            _profileId,
            outside);

        Assert.Null(error);
        Assert.True(File.Exists(outside));
    }

    [Fact]
    public void CopyIntoProfileFolder_keeps_an_existing_avatar_with_the_same_name()
    {
        ProfilePaths paths = ProfilePaths.ForProfile(_profilesRoot, _profileId);
        Directory.CreateDirectory(paths.Root);
        string current = Path.Combine(paths.Root, "logo.png");
        File.WriteAllText(current, "current");
        string sourceFolder = Path.Combine(_profilesRoot, "picked");
        Directory.CreateDirectory(sourceFolder);
        string source = Path.Combine(sourceFolder, "logo.png");
        File.WriteAllText(source, "new");

        string destination = ProfileCustomAvatarFiles.CopyIntoProfileFolder(_profilesRoot, _profileId, source);

        Assert.NotEqual(current, destination);
        Assert.Equal(Path.Combine(paths.Root, "logo-1.png"), destination);
        Assert.Equal("current", File.ReadAllText(current));
        Assert.Equal("new", File.ReadAllText(destination));
    }

    [Fact]
    public void CopyPendingImage_copies_a_picked_file_from_outside_the_profile_folder()
    {
        string source = Path.Combine(_profilesRoot, "picked.png");
        File.WriteAllText(source, "png");

        string? copied = ProfileCustomAvatarFiles.CopyPendingImage(_profilesRoot, _profileId, source);

        Assert.NotNull(copied);
        Assert.True(ProfileCustomAvatarFiles.IsUnderProfileFolder(_profilesRoot, _profileId, copied));
        Assert.True(File.Exists(source));
    }

    [Fact]
    public void CopyPendingImage_skips_empty_missing_and_already_owned_paths()
    {
        ProfilePaths paths = ProfilePaths.ForProfile(_profilesRoot, _profileId);
        Directory.CreateDirectory(paths.Root);
        string owned = Path.Combine(paths.Root, "logo.png");
        File.WriteAllText(owned, "png");

        Assert.Null(ProfileCustomAvatarFiles.CopyPendingImage(_profilesRoot, _profileId, ""));
        Assert.Null(ProfileCustomAvatarFiles.CopyPendingImage(_profilesRoot, _profileId, Path.Combine(_profilesRoot, "gone.png")));
        Assert.Null(ProfileCustomAvatarFiles.CopyPendingImage(_profilesRoot, _profileId, owned));
        Assert.Single(Directory.GetFiles(paths.Root));
    }

    [Fact]
    public void IsUnderProfileFolder_rejects_a_sibling_folder_that_shares_the_prefix()
    {
        ProfilePaths paths = ProfilePaths.ForProfile(_profilesRoot, _profileId);
        string sibling = paths.Root + "-old";
        Directory.CreateDirectory(sibling);
        string iconPath = Path.Combine(sibling, "logo.png");
        File.WriteAllText(iconPath, "png");

        Assert.False(ProfileCustomAvatarFiles.IsUnderProfileFolder(_profilesRoot, _profileId, iconPath));
        Assert.True(ProfileCustomAvatarFiles.IsUnderProfileFolder(_profilesRoot, _profileId, Path.Combine(paths.Root, "logo.png")));

        Assert.Null(ProfileCustomAvatarFiles.TryDeleteLauncherOwnedFile(_profilesRoot, _profileId, iconPath));
        Assert.True(File.Exists(iconPath));
    }

    [Fact]
    public void TryDeleteReplacedFile_deletes_the_previous_owned_avatar_only_when_it_changed()
    {
        ProfilePaths paths = ProfilePaths.ForProfile(_profilesRoot, _profileId);
        Directory.CreateDirectory(paths.Root);
        string previous = Path.Combine(paths.Root, "logo.png");
        string replacement = Path.Combine(paths.Root, "logo-1.png");
        File.WriteAllText(previous, "old");
        File.WriteAllText(replacement, "new");

        Assert.Null(ProfileCustomAvatarFiles.TryDeleteReplacedFile(_profilesRoot, _profileId, previous, previous.ToUpperInvariant()));
        Assert.True(File.Exists(previous));

        Assert.Null(ProfileCustomAvatarFiles.TryDeleteReplacedFile(_profilesRoot, _profileId, previous, replacement));
        Assert.False(File.Exists(previous));
        Assert.True(File.Exists(replacement));
    }

    [Fact]
    public void TryDeleteReplacedFile_deletes_a_removed_avatar_but_never_files_outside_the_profile_folder()
    {
        ProfilePaths paths = ProfilePaths.ForProfile(_profilesRoot, _profileId);
        Directory.CreateDirectory(paths.Root);
        string owned = Path.Combine(paths.Root, "logo.png");
        string outside = Path.Combine(_profilesRoot, "outside.png");
        File.WriteAllText(owned, "png");
        File.WriteAllText(outside, "png");

        Assert.Null(ProfileCustomAvatarFiles.TryDeleteReplacedFile(_profilesRoot, _profileId, outside, ""));
        Assert.Null(ProfileCustomAvatarFiles.TryDeleteReplacedFile(_profilesRoot, _profileId, owned, ""));

        Assert.True(File.Exists(outside));
        Assert.False(File.Exists(owned));
    }

    [Fact]
    public void TryDeleteLauncherOwnedFile_no_op_for_empty_icon_path()
    {
        string? error = ProfileCustomAvatarFiles.TryDeleteLauncherOwnedFile(
            _profilesRoot,
            _profileId,
            "");

        Assert.Null(error);
    }
}
