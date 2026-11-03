using System.Diagnostics;

namespace PowerBILauncher
{
    public readonly record struct FileVersionInfoStrings(string? ProductName, string? FileDescription);

    public interface IFileVersionInfoReader
    {
        FileVersionInfoStrings Read(string path);
    }

    public sealed class FileVersionInfoReader : IFileVersionInfoReader
    {
        public FileVersionInfoStrings Read(string path)
        {
            FileVersionInfo info = FileVersionInfo.GetVersionInfo(path);
            return new FileVersionInfoStrings(info.ProductName, info.FileDescription);
        }
    }
}
