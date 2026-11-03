using Microsoft.Win32;
using Windows.UI;

namespace PowerBILauncher
{
    public static class AppTheme
    {
        public static bool IsDark()
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
                if (key?.GetValue("AppsUseLightTheme") is int useLightTheme)
                {
                    return useLightTheme == 0;
                }
            }
            catch
            {
            }

            return false;
        }

        public static Color CardSurfaceColor => IsDark()
            ? Color.FromArgb(255, 45, 45, 45)
            : Color.FromArgb(255, 255, 255, 255);

        public static string CardSurfaceHex => IsDark() ? "#2D2D2D" : "#FFFFFF";
    }
}
