using Microsoft.UI.Xaml;

namespace PowerBILauncher
{
    public partial class App : Application
    {
        private MainWindow? _window;

        public App()
        {
            InitializeComponent();
            UnhandledException += App_UnhandledException;
        }

        protected override void OnLaunched(LaunchActivatedEventArgs args)
        {
            // Keep the process alive when the main window is hidden to the tray.
            DispatcherShutdownMode = DispatcherShutdownMode.OnExplicitShutdown;

            _window = new MainWindow();
            _window.Activate();
        }

        public static void ExitApp()
        {
            Current.Exit();
        }

        /// <summary>
        /// Another launch of this copy was redirected here (see <see cref="Program"/>). UI thread only.
        /// </summary>
        internal void ShowMainWindow() =>
            _window?.ShowFromRedirectedActivation();

        private void App_UnhandledException(object sender, Microsoft.UI.Xaml.UnhandledExceptionEventArgs e)
        {
            // Before the window exists, OnExplicitShutdown would leave a windowless process behind.
            if (_window is null)
            {
                return;
            }

            e.Handled = true;
            _window.ShowUnexpectedError(e.Message);
        }
    }
}
