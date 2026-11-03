using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.Windows.AppLifecycle;

namespace PowerBILauncher
{
    /// <summary>
    /// Custom entry point (<c>DISABLE_XAML_GENERATED_MAIN</c>). A second launch of the same copy
    /// redirects to the running instance, which shows its window, so two launchers never
    /// overwrite each other's JSON or add a second tray icon.
    /// </summary>
    public static class Program
    {
        private static DispatcherQueue? _uiQueue;

        [STAThread]
        private static int Main(string[] args)
        {
            WinRT.ComWrappersSupport.InitializeComWrappers();
            if (RedirectedToRunningInstance())
            {
                return 0;
            }

            Application.Start(callbackParams =>
            {
                _uiQueue = DispatcherQueue.GetForCurrentThread();
                SynchronizationContext.SetSynchronizationContext(
                    new DispatcherQueueSynchronizationContext(_uiQueue));
                _ = new App();
            });
            return 0;
        }

        private static bool RedirectedToRunningInstance()
        {
            AppInstance primary = AppInstance.FindOrRegisterForKey(
                SingleInstanceKey.ForBaseDirectory(AppContext.BaseDirectory));
            if (primary.IsCurrent)
            {
                primary.Activated += OnRedirectedActivation;
                return false;
            }

            AppActivationArguments activation = AppInstance.GetCurrent().GetActivatedEventArgs();

            // The running instance is in the background; this process owns the foreground right.
            AllowSetForegroundWindow(primary.ProcessId);

            // Redirect off the STA thread and pump COM while waiting, or the call can deadlock.
            using var redirected = new ManualResetEvent(false);
            Task.Run(() =>
            {
                try
                {
                    primary.RedirectActivationToAsync(activation).AsTask().Wait();
                }
                finally
                {
                    redirected.Set();
                }
            });
            _ = CoWaitForMultipleObjects(
                CwmoDefault,
                Infinite,
                1,
                [redirected.SafeWaitHandle.DangerousGetHandle()],
                out _);
            return true;
        }

        private static void OnRedirectedActivation(object? sender, AppActivationArguments args) =>
            _uiQueue?.TryEnqueue(() => (Application.Current as App)?.ShowMainWindow());

        private const uint CwmoDefault = 0;
        private const uint Infinite = 0xFFFFFFFF;

        [DllImport("ole32.dll")]
        private static extern int CoWaitForMultipleObjects(
            uint dwFlags,
            uint dwTimeout,
            uint cHandles,
            IntPtr[] pHandles,
            out uint lpdwindex);

        [DllImport("user32.dll")]
        private static extern bool AllowSetForegroundWindow(uint dwProcessId);
    }
}
