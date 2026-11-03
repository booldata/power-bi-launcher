using System;
using System.Runtime.InteropServices;

namespace PowerBILauncher
{
    /// <summary>
    /// Approach A: best-effort Win32 popup dark/light theming via undocumented uxtheme ordinals.
    /// Fail soft - callers must still show the menu if theming is unavailable.
    /// </summary>
    internal static class TrayMenuTheme
    {
        private const int PreferredAppModeAllowDark = 1;

        private static readonly object Sync = new();
        private static bool _resolved;
        private static SetPreferredAppModeDelegate? _setPreferredAppMode;
        private static AllowDarkModeForWindowDelegate? _allowDarkModeForWindow;
        private static ShouldAppsUseDarkModeDelegate? _shouldAppsUseDarkMode;
        private static FlushMenuThemesDelegate? _flushMenuThemes;
        private static IntPtr _darkMenuBrush;

        public static void ApplyBeforePopup(IntPtr hwnd, IntPtr menu)
        {
            try
            {
                EnsureResolved();

                bool preferDark = _shouldAppsUseDarkMode?.Invoke() ?? false;

                _setPreferredAppMode?.Invoke(PreferredAppModeAllowDark);
                if (hwnd != IntPtr.Zero)
                {
                    _allowDarkModeForWindow?.Invoke(hwnd, preferDark);
                }

                _flushMenuThemes?.Invoke();

                if (preferDark && menu != IntPtr.Zero)
                {
                    ApplyDarkMenuBackground(menu);
                }
            }
            catch
            {
                // Fail soft: untinted / light Win32 menu.
            }
        }

        private static void EnsureResolved()
        {
            if (_resolved)
            {
                return;
            }

            lock (Sync)
            {
                if (_resolved)
                {
                    return;
                }

                IntPtr uxtheme = LoadLibrary("uxtheme.dll");
                if (uxtheme != IntPtr.Zero)
                {
                    // Undocumented ordinals (PowerToys / ZoomIt pattern).
                    _shouldAppsUseDarkMode = Resolve<ShouldAppsUseDarkModeDelegate>(uxtheme, 132);
                    _allowDarkModeForWindow = Resolve<AllowDarkModeForWindowDelegate>(uxtheme, 133);
                    _setPreferredAppMode = Resolve<SetPreferredAppModeDelegate>(uxtheme, 135);
                    _flushMenuThemes = Resolve<FlushMenuThemesDelegate>(uxtheme, 136);
                }

                _resolved = true;
            }
        }

        private static T? Resolve<T>(IntPtr module, ushort ordinal) where T : Delegate
        {
            IntPtr proc = GetProcAddress(module, (IntPtr)ordinal);
            if (proc == IntPtr.Zero)
            {
                return null;
            }

            return Marshal.GetDelegateForFunctionPointer<T>(proc);
        }

        private static void ApplyDarkMenuBackground(IntPtr menu)
        {
            if (_darkMenuBrush == IntPtr.Zero)
            {
                // BGR dark gray; PowerToys-style MIM_BACKGROUND when apps prefer dark.
                _darkMenuBrush = CreateSolidBrush(0x00202020);
            }

            if (_darkMenuBrush == IntPtr.Zero)
            {
                return;
            }

            var info = new MENUINFO
            {
                cbSize = (uint)Marshal.SizeOf<MENUINFO>(),
                fMask = 0x00000002, // MIM_BACKGROUND
                hbrBack = _darkMenuBrush
            };

            SetMenuInfo(menu, ref info);
        }

        private delegate int SetPreferredAppModeDelegate(int appMode);
        private delegate bool AllowDarkModeForWindowDelegate(IntPtr hwnd, bool allow);
        private delegate bool ShouldAppsUseDarkModeDelegate();
        private delegate void FlushMenuThemesDelegate();

        [StructLayout(LayoutKind.Sequential)]
        private struct MENUINFO
        {
            public uint cbSize;
            public uint fMask;
            public uint dwStyle;
            public uint cyMax;
            public IntPtr hbrBack;
            public uint dwContextHelpID;
            public UIntPtr dwMenuData;
        }

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern IntPtr LoadLibrary(string lpFileName);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr GetProcAddress(IntPtr hModule, IntPtr procNameOrOrdinal);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool SetMenuInfo(IntPtr hmenu, ref MENUINFO lpcmi);

        [DllImport("gdi32.dll")]
        private static extern IntPtr CreateSolidBrush(uint crColor);
    }
}
