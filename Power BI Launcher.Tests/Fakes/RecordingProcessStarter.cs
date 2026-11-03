using System.Diagnostics;

namespace PowerBILauncher.Tests;

internal sealed class RecordingProcessStarter : IProcessStarter
{
    public ProcessStartInfo? Last { get; private set; }
    public int StartCount { get; private set; }
    public Exception? ThrowOnStart { get; set; }

    public void Start(ProcessStartInfo startInfo)
    {
        if (ThrowOnStart is not null)
        {
            throw ThrowOnStart;
        }

        Last = startInfo;
        StartCount++;
    }
}
