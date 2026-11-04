using System.IO;

namespace PowerBILauncher
{
    /// <summary>
    /// Deletes a Profile folder. <see cref="Directory.Delete(string, bool)"/> stops on the
    /// hidden system junction Windows creates at <c>INetCache\Content.IE5</c> and reports
    /// "Access to the path 'Content.IE5' is denied.", leaving the folder in place.
    /// Reparse points are unlinked and not followed, so a junction that points outside
    /// the Profile folder is not traversed.
    /// </summary>
    public static class ProfileDirectoryDelete
    {
        private static readonly EnumerationOptions IncludeHiddenAndSystem = new()
        {
            AttributesToSkip = 0,
            RecurseSubdirectories = false
        };

        public static void Delete(string directory)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(directory);
            var info = new DirectoryInfo(directory);
            if (!info.Exists)
            {
                return;
            }

            DeleteDirectory(info);
        }

        private static void DeleteDirectory(DirectoryInfo directory)
        {
            if (IsReparsePoint(directory))
            {
                directory.Delete(recursive: false);
                return;
            }

            foreach (FileSystemInfo entry in directory.EnumerateFileSystemInfos("*", IncludeHiddenAndSystem))
            {
                if (IsReparsePoint(entry))
                {
                    entry.Delete();
                    continue;
                }

                if (entry is DirectoryInfo child)
                {
                    DeleteDirectory(child);
                    continue;
                }

                entry.Delete();
            }

            directory.Delete(recursive: false);
        }

        private static bool IsReparsePoint(FileSystemInfo entry) =>
            (entry.Attributes & FileAttributes.ReparsePoint) != 0;
    }
}
