using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Microsoft.UI.Xaml;

namespace PowerBILauncher
{
    /// <summary>
    /// Best-effort Win32 notification-area icon for unpackaged WinUI.
    /// Menu entries come from <see cref="TrayMenuComposition"/>; this type only renders and dispatches.
    /// </summary>
    public sealed class TrayIcon : IDisposable
    {
        private const uint NimAdd = 0x00000000;
        private const uint NimModify = 0x00000001;
        private const uint NimDelete = 0x00000002;
        private const uint NifMessage = 0x00000001;
        private const uint NifIcon = 0x00000002;
        private const uint NifTip = 0x00000004;
        private const uint WmLButtonUp = 0x0202;
        private const uint WmRButtonUp = 0x0205;
        private const uint WmApp = 0x8000;
        private const uint CallbackMessage = WmApp + 1;
        private const uint WmCommand = 0x0111;
        private const uint MfString = 0x00000000;
        private const uint MfSeparator = 0x00000800;
        private const uint TpmRightButton = 0x0002;
        private const int IdiApplication = 32512;
        private const int ImageIcon = 1;
        private const uint LrDefaultSize = 0x00000040;
        private const uint LrLoadFromFile = 0x00000010;
        private const ushort ShowHideCommandId = 1001;
        private const ushort ExitCommandId = 1002;
        private const ushort LaunchCommandIdBase = 2000;

        private readonly Window _window;
        private readonly Func<IReadOnlyList<TrayMenuEntry>> _getMenuEntries;
        private readonly Action<Guid> _onLaunchProfile;
        private readonly Action _onLeftClick;
        private readonly Action _onShowHideWindow;
        private readonly Action _onExit;
        private readonly NativeWindow _nativeWindow;
        private readonly string _tip;
        private readonly uint _taskbarCreatedMessage;
        private readonly Dictionary<ushort, TrayMenuEntry> _commandMap = new();
        private bool _added;
        private bool _disposed;
        private bool _ownsIcon;
        private IntPtr _iconHandle;

        public TrayIcon(
            Window window,
            string tip,
            string? iconPath,
            Func<IReadOnlyList<TrayMenuEntry>> getMenuEntries,
            Action<Guid> onLaunchProfile,
            Action onLeftClick,
            Action onShowHideWindow,
            Action onExit)
        {
            ArgumentNullException.ThrowIfNull(getMenuEntries);
            ArgumentNullException.ThrowIfNull(onLaunchProfile);
            ArgumentNullException.ThrowIfNull(onLeftClick);
            ArgumentNullException.ThrowIfNull(onShowHideWindow);
            ArgumentNullException.ThrowIfNull(onExit);

            _window = window;
            _getMenuEntries = getMenuEntries;
            _onLaunchProfile = onLaunchProfile;
            _onLeftClick = onLeftClick;
            _onShowHideWindow = onShowHideWindow;
            _onExit = onExit;
            _tip = tip;
            _taskbarCreatedMessage = RegisterWindowMessage("TaskbarCreated");
            _nativeWindow = new NativeWindow(OnMessage);
            _iconHandle = LoadIconHandle(iconPath);
            AddOrModify(tip, isAdd: true);
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;

            if (_added)
            {
                var data = CreateData();
                Shell_NotifyIcon(NimDelete, ref data);
                _added = false;
            }

            if (_ownsIcon && _iconHandle != IntPtr.Zero)
            {
                DestroyIcon(_iconHandle);
                _iconHandle = IntPtr.Zero;
            }

            _nativeWindow.Dispose();
        }

        private void AddOrModify(string tip, bool isAdd)
        {
            var data = CreateData();
            data.uFlags = NifMessage | NifIcon | NifTip;
            data.uCallbackMessage = CallbackMessage;
            data.hIcon = _iconHandle != IntPtr.Zero
                ? _iconHandle
                : LoadIcon(IntPtr.Zero, (IntPtr)IdiApplication);
            data.szTip = tip.Length > 127 ? tip[..127] : tip;

            uint message = isAdd ? NimAdd : NimModify;
            if (Shell_NotifyIcon(message, ref data))
            {
                _added = true;
            }
        }

        private NOTIFYICONDATA CreateData() =>
            new()
            {
                cbSize = (uint)Marshal.SizeOf<NOTIFYICONDATA>(),
                hWnd = _nativeWindow.Handle,
                uID = 1
            };

        private void OnMessage(uint msg, IntPtr wParam, IntPtr lParam)
        {
            if (msg == CallbackMessage)
            {
                uint mouseMsg = (uint)lParam.ToInt64() & 0xFFFF;
                if (mouseMsg == WmLButtonUp)
                {
                    _window.DispatcherQueue.TryEnqueue(() => _onLeftClick());
                }
                else if (mouseMsg == WmRButtonUp)
                {
                    _window.DispatcherQueue.TryEnqueue(ShowContextMenu);
                }
            }
            else if (msg == WmCommand)
            {
                ushort commandId = (ushort)(wParam.ToInt32() & 0xFFFF);
                if (_commandMap.TryGetValue(commandId, out TrayMenuEntry? entry))
                {
                    _window.DispatcherQueue.TryEnqueue(() => DispatchMenuEntry(entry));
                }
            }
            else if (msg == _taskbarCreatedMessage && _taskbarCreatedMessage != 0 && !_disposed)
            {
                // Explorer restarted and dropped every notification-area icon.
                _added = false;
                AddOrModify(_tip, isAdd: true);
            }
        }

        private void ShowContextMenu()
        {
            IReadOnlyList<TrayMenuEntry> entries = _getMenuEntries();
            _commandMap.Clear();

            IntPtr menu = CreatePopupMenu();
            ushort nextLaunchId = LaunchCommandIdBase;

            foreach (TrayMenuEntry entry in entries)
            {
                if (entry.Kind == TrayMenuEntryKind.Separator)
                {
                    AppendMenu(menu, MfSeparator, 0, string.Empty);
                    continue;
                }

                ushort commandId = entry.Kind switch
                {
                    TrayMenuEntryKind.LaunchProfile => nextLaunchId++,
                    TrayMenuEntryKind.ShowWindow or TrayMenuEntryKind.HideWindow => ShowHideCommandId,
                    TrayMenuEntryKind.Exit => ExitCommandId,
                    _ => throw new InvalidOperationException($"Unhandled tray menu kind: {entry.Kind}")
                };

                _commandMap[commandId] = entry;
                AppendMenu(menu, MfString, commandId, entry.Label);
            }

            TrayMenuTheme.ApplyBeforePopup(_nativeWindow.Handle, menu);

            GetCursorPos(out POINT point);
            SetForegroundWindow(_nativeWindow.Handle);
            TrackPopupMenu(menu, TpmRightButton, point.X, point.Y, 0, _nativeWindow.Handle, IntPtr.Zero);
            DestroyMenu(menu);
        }

        private void DispatchMenuEntry(TrayMenuEntry entry)
        {
            switch (entry.Kind)
            {
                case TrayMenuEntryKind.LaunchProfile:
                    if (entry.ProfileId is Guid profileId)
                    {
                        _onLaunchProfile(profileId);
                    }

                    break;
                case TrayMenuEntryKind.ShowWindow:
                case TrayMenuEntryKind.HideWindow:
                    _onShowHideWindow();
                    break;
                case TrayMenuEntryKind.Exit:
                    _onExit();
                    break;
                case TrayMenuEntryKind.Separator:
                    break;
                default:
                    throw new InvalidOperationException($"Unhandled tray menu kind: {entry.Kind}");
            }
        }

        private IntPtr LoadIconHandle(string? iconPath)
        {
            if (!string.IsNullOrWhiteSpace(iconPath) && System.IO.File.Exists(iconPath))
            {
                IntPtr loaded = LoadImage(
                    IntPtr.Zero,
                    iconPath,
                    ImageIcon,
                    0,
                    0,
                    LrLoadFromFile | LrDefaultSize);
                if (loaded != IntPtr.Zero)
                {
                    _ownsIcon = true;
                    return loaded;
                }
            }

            _ownsIcon = false;
            return LoadIcon(IntPtr.Zero, (IntPtr)IdiApplication);
        }

        private sealed class NativeWindow : IDisposable
        {
            private readonly WndProc _wndProc;
            private readonly string _className;
            private bool _disposed;

            public NativeWindow(Action<uint, IntPtr, IntPtr> handler)
            {
                _wndProc = (hWnd, msg, wParam, lParam) =>
                {
                    handler(msg, wParam, lParam);
                    return DefWindowProc(hWnd, msg, wParam, lParam);
                };

                _className = "PbiLauncherTray_" + Guid.NewGuid().ToString("N");
                var wc = new WNDCLASS
                {
                    lpszClassName = _className,
                    lpfnWndProc = Marshal.GetFunctionPointerForDelegate(_wndProc),
                    hInstance = GetModuleHandle(null)
                };

                RegisterClass(ref wc);
                Handle = CreateWindowEx(
                    0,
                    _className,
                    "Power BI Launcher Tray",
                    0,
                    0,
                    0,
                    0,
                    0,
                    IntPtr.Zero,
                    IntPtr.Zero,
                    wc.hInstance,
                    IntPtr.Zero);
            }

            public IntPtr Handle { get; }

            public void Dispose()
            {
                if (_disposed)
                {
                    return;
                }

                _disposed = true;
                if (Handle != IntPtr.Zero)
                {
                    DestroyWindow(Handle);
                }

                UnregisterClass(_className, GetModuleHandle(null));
            }
        }

        private delegate IntPtr WndProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct NOTIFYICONDATA
        {
            public uint cbSize;
            public IntPtr hWnd;
            public uint uID;
            public uint uFlags;
            public uint uCallbackMessage;
            public IntPtr hIcon;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
            public string szTip;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct WNDCLASS
        {
            public uint style;
            public IntPtr lpfnWndProc;
            public int cbClsExtra;
            public int cbWndExtra;
            public IntPtr hInstance;
            public IntPtr hIcon;
            public IntPtr hCursor;
            public IntPtr hbrBackground;
            public string? lpszMenuName;
            public string lpszClassName;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct POINT
        {
            public int X;
            public int Y;
        }

        [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
        private static extern bool Shell_NotifyIcon(uint dwMessage, ref NOTIFYICONDATA lpData);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern ushort RegisterClass(ref WNDCLASS lpWndClass);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern bool UnregisterClass(string lpClassName, IntPtr hInstance);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern IntPtr CreateWindowEx(
            int dwExStyle,
            string lpClassName,
            string lpWindowName,
            int dwStyle,
            int x,
            int y,
            int nWidth,
            int nHeight,
            IntPtr hWndParent,
            IntPtr hMenu,
            IntPtr hInstance,
            IntPtr lpParam);

        [DllImport("user32.dll")]
        private static extern bool DestroyWindow(IntPtr hWnd);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern uint RegisterWindowMessage(string lpString);

        [DllImport("user32.dll")]
        private static extern IntPtr DefWindowProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
        private static extern IntPtr GetModuleHandle(string? lpModuleName);

        [DllImport("user32.dll")]
        private static extern IntPtr CreatePopupMenu();

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern bool AppendMenu(IntPtr hMenu, uint uFlags, uint uIDNewItem, string lpNewItem);

        [DllImport("user32.dll")]
        private static extern bool DestroyMenu(IntPtr hMenu);

        [DllImport("user32.dll")]
        private static extern bool TrackPopupMenu(
            IntPtr hMenu,
            uint uFlags,
            int x,
            int y,
            int nReserved,
            IntPtr hWnd,
            IntPtr prcRect);

        [DllImport("user32.dll")]
        private static extern bool GetCursorPos(out POINT lpPoint);

        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern IntPtr LoadImage(
            IntPtr hInst,
            string name,
            uint type,
            int cx,
            int cy,
            uint fuLoad);

        [DllImport("user32.dll")]
        private static extern IntPtr LoadIcon(IntPtr hInstance, IntPtr lpIconName);

        [DllImport("user32.dll")]
        private static extern bool DestroyIcon(IntPtr hIcon);
    }
}
