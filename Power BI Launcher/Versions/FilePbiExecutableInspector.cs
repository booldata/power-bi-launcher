using System.IO;

namespace PowerBILauncher
{
    public interface IPbiExecutableInspector
    {
        PbiExecutableFacts Inspect(string path);
    }

    public sealed class FilePbiExecutableInspector : IPbiExecutableInspector
    {
        private readonly IFileVersionInfoReader _versionInfo;

        public FilePbiExecutableInspector(IFileVersionInfoReader? versionInfo = null)
        {
            _versionInfo = versionInfo ?? new FileVersionInfoReader();
        }

        public PbiExecutableFacts Inspect(string path)
        {
            string fileName = string.IsNullOrWhiteSpace(path) ? "" : Path.GetFileName(path);
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            {
                return new PbiExecutableFacts(false, fileName, null, null);
            }

            FileVersionInfoStrings versionInfo = _versionInfo.Read(path);
            return new PbiExecutableFacts(true, fileName, versionInfo.ProductName, versionInfo.FileDescription);
        }
    }
}
