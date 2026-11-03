# System tray

## Win32 tray icon

WinUI 3 and the Windows App SDK have no tray icon API (the WinRT `SystemTray` class is phone-only and non-functional on desktop), so the tray uses Win32 `Shell_NotifyIcon` and a native popup menu. The app keeps running while its window is hidden in the tray.

Rejected: third-party tray wrappers (same Win32 calls plus a dependency) and WinUI flyouts near the tray (they do not behave like tray menus).

## Menu

- One item per Profile with **Show on tray** on, in Launch Order, then **Exit**. No Profiles appear until first-run setup is complete.
- Items show the Profile name only. Version and Windows network identity are not shown, since the screen may be shared.
- The menu is rebuilt each time it opens, so changes appear without restarting.
- Launching from the tray uses the same path and error dialogs as the board.
- Left-click shows, focuses, or hides the window. Right-click opens the menu.

## Dark menu

Win32 popup menus have no documented dark mode. The app uses the same undocumented `uxtheme.dll` calls as PowerToys to follow the Windows app theme. If they are unavailable, the menu falls back to light; it never crashes.

## Sources

- https://learn.microsoft.com/en-us/windows/win32/shell/notification-area
- https://github.com/microsoft/microsoft-ui-xaml/issues/2020 (no WinUI tray icon API)
- https://github.com/microsoft/PowerToys/blob/main/src/modules/ZoomIt/ZoomIt/Utility.cpp (dark menu pattern)
