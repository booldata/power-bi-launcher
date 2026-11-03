# WinUI 3, unpackaged, self-contained

## Decision

- UI stack is **WinUI 3 / Windows App SDK** on .NET 10, Windows only.
- Deployment is **unpackaged + self-contained** (Windows App SDK and .NET), published as a portable folder. Locked-down client PCs need "copy a folder, no admin, no runtime install".
- Code-behind plus the UI-free `ProfileLifecycle` seam. No MVVM or DI framework; it is not needed at this size.

## Rejected

- **WPF.** SettingsCard, TeachingTip, and Mica are WinUI controls and materials.
- **Framework-dependent Windows App SDK.** It needs a separate runtime installer.
- **MSIX as the primary channel.** It changes install and file layout. The Win32 tray icon still works under MSIX, because packaged WinUI 3 apps run full trust (`runFullTrust`) by default; only opt-in partial trust (AppContainer) would block `Shell_NotifyIcon`. MSIX can be added later as an extra channel.

## If packaging is reopened

- Package identity is only needed for file/protocol associations, startup tasks, push, share target, or Store auto-update. None are used today.
- `PublishSingleFile` extracts to a temp folder on first run; it is not a true single exe.

## Sources

- https://learn.microsoft.com/en-us/windows/apps/package-and-deploy/choose-packaging-model
- https://learn.microsoft.com/en-us/windows/apps/package-and-deploy/self-contained-deploy/deploy-self-contained-apps
- https://learn.microsoft.com/en-us/windows/apps/windows-app-sdk/deploy-unpackaged-apps
