using System;
using System.IO;

namespace PowerBILauncher;

/// <summary>
/// UI-free custom Avatar file set/clear under a Profile root from Settings Folder to store Profiles.
/// Edit Profile keeps a picked image as a pending source path; files change only on Save.
/// </summary>
public static class ProfileCustomAvatarFiles
{
    public static string CopyIntoProfileFolder(
        string userProfilePath,
        Guid profileId,
        string sourceFilePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceFilePath);

        if (LogoValidation.TryValidateLogoExtension(sourceFilePath, out string? extension) is LogoValidationKind kind)
        {
            throw new LogoValidationException(kind, extension);
        }

        ProfilePaths paths = ProfilePaths.ForProfile(userProfilePath, profileId);
        Directory.CreateDirectory(paths.Root);
        string destinationPath = FreeDestinationPath(paths.Root, Path.GetFileName(sourceFilePath));
        File.Copy(sourceFilePath, destinationPath, overwrite: false);
        return destinationPath;
    }

    /// <summary>
    /// Copies a pending (picked, not yet saved) custom image into the Profile root.
    /// Returns the new launcher-owned path, or null when <paramref name="iconPath"/> is empty,
    /// missing, or already under the Profile root.
    /// </summary>
    public static string? CopyPendingImage(string userProfilePath, Guid profileId, string? iconPath)
    {
        if (!LetterAvatar.HasCustomIcon(iconPath) || IsUnderProfileFolder(userProfilePath, profileId, iconPath))
        {
            return null;
        }

        return CopyIntoProfileFolder(userProfilePath, profileId, iconPath!);
    }

    /// <summary>
    /// Deletes a launcher-owned custom Avatar file under the Profile root when applicable.
    /// Returns a user-visible error message when delete fails; null on success or no-op.
    /// </summary>
    public static string? TryDeleteLauncherOwnedFile(
        string userProfilePath,
        Guid profileId,
        string? iconPath)
    {
        if (!LetterAvatar.HasCustomIcon(iconPath) || !IsUnderProfileFolder(userProfilePath, profileId, iconPath))
        {
            return null;
        }

        try
        {
            File.Delete(Path.GetFullPath(iconPath!));
        }
        catch (Exception ex)
        {
            return ex.Message;
        }

        return null;
    }

    /// <summary>
    /// After a successful Save: deletes the previously saved launcher-owned image when the
    /// Profile no longer uses it. Same return contract as <see cref="TryDeleteLauncherOwnedFile"/>.
    /// </summary>
    public static string? TryDeleteReplacedFile(
        string userProfilePath,
        Guid profileId,
        string? previousIconPath,
        string? savedIconPath)
    {
        if (string.IsNullOrWhiteSpace(previousIconPath) || IsSamePath(previousIconPath, savedIconPath))
        {
            return null;
        }

        return TryDeleteLauncherOwnedFile(userProfilePath, profileId, previousIconPath);
    }

    public static bool IsUnderProfileFolder(string userProfilePath, Guid profileId, string? iconPath)
    {
        if (string.IsNullOrWhiteSpace(iconPath))
        {
            return false;
        }

        try
        {
            string profileFolder = Path.GetFullPath(ProfilePaths.ForProfile(userProfilePath, profileId).Root)
                .TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            return Path.GetFullPath(iconPath).StartsWith(profileFolder, StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception ex) when (ex is ArgumentException or IOException or NotSupportedException)
        {
            return false;
        }
    }

    private static bool IsSamePath(string first, string? second)
    {
        if (string.IsNullOrWhiteSpace(second))
        {
            return false;
        }

        try
        {
            return string.Equals(Path.GetFullPath(first), Path.GetFullPath(second), StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception ex) when (ex is ArgumentException or IOException or NotSupportedException)
        {
            return string.Equals(first, second, StringComparison.OrdinalIgnoreCase);
        }
    }

    private static string FreeDestinationPath(string folder, string fileName)
    {
        string candidate = Path.Combine(folder, fileName);
        string stem = Path.GetFileNameWithoutExtension(fileName);
        string extension = Path.GetExtension(fileName);
        for (int suffix = 1; File.Exists(candidate); suffix++)
        {
            candidate = Path.Combine(folder, $"{stem}-{suffix}{extension}");
        }

        return candidate;
    }
}
