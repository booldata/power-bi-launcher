using System.Diagnostics;

namespace PowerBILauncher
{
    public interface IProcessStarter
    {
        void Start(ProcessStartInfo startInfo);
    }

    public sealed class ProcessStarter : IProcessStarter
    {
        public void Start(ProcessStartInfo startInfo) =>
            Process.Start(startInfo);
    }
}
