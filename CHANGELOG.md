# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

Release sections must use `## [X.Y.Z] - YYYY-MM-DD` for stable tags or
`## [X.Y.Z-beta.N] - YYYY-MM-DD` for beta tags. The release workflow
publishes the matching section body for the tag.

## [Unreleased]

### Changed

- The portable zip now bundles Windows App SDK 2.6 instead of 1.7.

## [1.0.0] - 2026-11-03

### Added

- Portable zip `Power-BI-Launcher-v1.0.0-win-x64.zip` with `Power BI Launcher.exe`. If an older private build left `PBI Launcher.exe` in the same folder, delete that leftover file after unzipping.
- Profiles: each Profile starts Power BI Desktop with its own user profile, AppData, local AppData, and temp folders, so sign-ins and caches never mix between clients or tenants.
- First-run setup in Settings: **Scan** (or **Browse**) for Power BI Desktop, choose a Default Version, review **Folder to store profiles**, and **Save settings**. Save creates the folder when it is missing.
- Version registry in Settings for the downloaded Power BI Desktop and Power BI Desktop for Report Server. Exactly one Default Version; a Profile follows it unless it picks its own Version. Rows show the Default Version, a kind badge, and **Rename**, **Set as default**, and **Remove** in one menu.
- Profile board: click a card to launch its Profile. Cards show the Version they launch and a short **Launching…** state so a second click does not start two Power BI windows. With no Profiles, the board offers **Add profile**.
- **Add profile** opens Edit Profile as an unsaved draft; nothing is written until **Save changes**. Edit Profile sets the name, Avatar (letter and color, or a custom image), card color, Version, Windows network identity, and **Show profile in system tray**. **Discard** undoes every change, including Avatar images.
- **Reorder mode** changes Launch Order by dragging a card or with **Move up** / **Move down** (Alt+Up / Alt+Down). The tray uses the same order.
- Windows network identity: optional per-Profile credentials for Windows-authenticated Report Servers and data sources. The password is kept in Windows Credential Manager, never in the launcher's files.
- System tray: launch any Profile with **Show profile in system tray** on, show or hide the window, and exit. Minimize and Close can send the window to the tray.
- Keyboard shortcuts: Ctrl+N adds a Profile, Ctrl+, opens Settings, Enter or Space launches the focused card, F2 opens Edit Profile, and Delete asks to delete it.
- Settings **About** shows the launcher version, the **Built** date, and **Check for updates**. After setup, startup shows a dismissible notice when a newer stable release is on GitHub. The launcher never downloads or installs updates itself.
- Scan lists the Microsoft Store version of Power BI Desktop greyed out as *Not supported*, because it signs every Profile in to the same account. Launching a Profile set to it explains why and offers **Open settings** or **Edit profile**.
- Opening the launcher while it is already running shows the running window instead of starting a second launcher.
- Profiles and settings are saved so that a crash or power loss cannot damage them. A file that cannot be read at startup is kept under a dated name and reported instead of being replaced.

[unreleased]: https://github.com/booldata/power-bi-launcher/compare/v1.0.0...HEAD
[1.0.0]: https://github.com/booldata/power-bi-launcher/releases/tag/v1.0.0
