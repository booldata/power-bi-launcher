# Architecture - Power BI Launcher

## Overview
A single-project WinUI 3 (Windows App SDK) desktop application with intentionally minimal layering. One main window, two models, JSON config next to the executable, and a UI-free Profile lifecycle + launch isolation module. The app can live in the system tray and launches Power BI Desktop with isolated user environment variables.

## Tech Stack
| Layer | Technology | Notes |
|---|---|---|
| UI | WinUI 3 / Windows App SDK (.NET 10, Windows) | Top-pivot shell (Power BI \| Settings); Edit Profile via nested Profiles `Frame` |
| Data / Persistence | System.Text.Json + `settings.json` / `profiles.json` | Stored next to the executable (`AppContext.BaseDirectory`) |
| Domain / launch | `ProfileLifecycle` | Load/save, Version registry, Isolated Environment folders, Windows network identity, child-process start info |
| Process Management | `System.Diagnostics.Process` | Launches PBI Desktop with custom env vars on `ProcessStartInfo` only |
| Tray | Win32 `Shell_NotifyIcon` (best-effort) | Unpackaged/full-trust; launches → separator → Show/Hide → Exit; left-click show/focus/hide |

## Project Structure
Folders are presentation only (`RootNamespace` stays `PowerBILauncher`). Filenames stay unique, so the key-components table below keeps bare names.

```
Power BI Launcher/
├── App.xaml / App.xaml.cs         - WinUI application (explicit shutdown for tray, last-resort unhandled-exception report)
├── Program.cs                     - Custom Main: single instance per copy (AppInstance redirect → show the running window)
├── settings.json                  - Source template only (not copied to output)
├── (link) ..\LICENSE.md           - Repo-root license, copied to output so the release zip carries it
├── Assets/pbilaunchericon.ico     - Application / tray icon (copied to output)
├── Assets/pbilaunchericon.af      - Affinity source for the app icon (design-only, not copied)
├── Assets/pbidesktoplogo.png      - Microsoft Power BI Desktop product artwork (not MIT-licensed; copied to output; Profile cards)
├── Assets/pbidesktoprslogo.png    - Microsoft Power BI Desktop for Report Server product artwork (not MIT-licensed; copied to output; Profile cards)
├── Strings/en-US/Resources.resw   - English chrome (x:Uid + mapper keys); runtime artifact is resources.pri
├── ProfileBoard/                  - board page, host, layout, cards, Launch Order
│   ├── ProfileBoardPage.xaml / .cs - Profile board page (inside Profiles Frame)
│   ├── ProfileBoardHost.cs        - Profile board coordinator (refresh / Reorder-mode sync / activate / Launching / empty board)
│   ├── ProfileBoardLayout.cs      - Equal-width card grid (max 5 columns) inside the board ScrollViewer
│   ├── ProfileCardInteraction.cs  - UI-free Profile board commanding policy (activate, Reorder offer, shortcuts, caption, product icon, empty board)
│   ├── ProfileCardFactory.cs      - Builds board card visuals (Avatar via AvatarPaint); F2 / Delete accelerators scoped to the card
│   ├── ProfileCardModel.cs        - Per-card board state (Reorder, More, Launching, Launch Order chrome)
│   ├── ProfileBoardLaunchOrder.cs - Board-local Launch Order grip / whole-card drag / gap-insert chrome
│   ├── LaunchOrder.cs             - UI-free Launch Order renumber / move helper
│   └── LaunchOrderGapTargeting.cs - UI-free nearest-gap insertion from card bounds
├── EditProfile/                   - Edit Profile page, session, dirty-state helper
│   ├── EditProfilePage.xaml / .cs - Edit Profile UI surface (controls, local affordances, raise events)
│   ├── EditProfileSession.cs      - Edit Profile session coordinator (open/draft/bind/dirty/save/discard/delete/avatar files)
│   └── EditProfileDirtyState.cs   - UI-free Edit Profile dirty check vs open snapshot or draft defaults
├── Settings/                      - Settings / onboarding surface, session, Version registry draft
│   ├── SettingsPanel.xaml / .cs   - Settings / onboarding UI surface (named elements + events)
│   ├── SettingsPanelSession.cs    - Settings session coordinator (reset / Scan staging / Browse / Save)
│   ├── SettingsDirtyState.cs      - UI-free Settings dirty check vs saved snapshot
│   └── VersionRegistryDraft.cs    - UI-free uncommitted Version registry + Scan staging
├── Versions/                      - Version model, Scan, Version selection, version-facing adapters
│   ├── PbiVersion.cs / PbiVersionKind.cs - Version registry model (C# type avoids System.Version)
│   ├── PbiExecutablePolicy.cs     - Pure Power BI executable bar over supplied facts
│   ├── FileVersionInfoReader.cs   - Thin VERSIONINFO adapter
│   ├── FilePbiExecutableInspector.cs - Maps a path to executable facts
│   ├── WindowsPbiDiscovery.cs     - Scan candidate adapter (registry, default folders, Store, Start Menu)
│   ├── ScanPolicy.cs              - Accepts discovery candidates; drops junk; generates display names
│   ├── ScanCandidateRow.cs        - Settings Scan staging row (tick + display)
│   ├── VersionRegistryRow.cs      - Settings Version registry row projection (Default marker, kind badge, truncated path, overflow actions)
│   └── VersionSelection.cs        - UI-free Edit Profile Version ComboBox options
├── Lifecycle/                     - ProfileLifecycle seam, profile model, paths, migrations
│   ├── ProfileLifecycle.cs        - UI-free persistence + launch isolation seam
│   ├── ProfileLaunchResult.cs     - Launch success / error kind + remediation (board + tray)
│   ├── Profile.cs                 - Profile model (name, Avatar, order, card color, ShowOnTray, VersionId, WindowsNetworkIdentity)
│   ├── ProfileCustomAvatarFiles.cs - UI-free custom Avatar copy/delete under Profile root
│   ├── ProfileDirectoryDelete.cs  - Deletes a Profile folder without following junctions such as Content.IE5
│   ├── ProfilePaths.cs            - Profile directory path bundle (short layout `<root>\<12-hex>\AppData\{Local,Roaming}`)
│   ├── ProfileFolderMigration.cs  - Resumable move of `<root>\<guid>\AppData\…` folders to the short layout
│   ├── PowerBiPathBudget.cs       - MAX_PATH budget PBIDesktop.exe (.NET Framework) imposes on LOCALAPPDATA
│   ├── ProfileMigration.cs        - One-shot in-place profile list migrations
│   └── ProfileValidation.cs       - Profile name validation (empty, uniqueness)
├── Tray/                          - notify icon, menu composition, menu theme
│   ├── TrayMenuComposition.cs     - UI-free tray menu entries (launches, separator, Show/Hide, Exit)
│   ├── TrayMenuTheme.cs           - Approach A fail-soft dark/light for Win32 popup
│   └── TrayIcon.cs                - Win32 notification-area icon (renders composition result)
├── Shell/                         - main window, chrome, settings model, update check
│   ├── MainWindow.xaml / .cs      - Top-pivot shell; Drill + dialogs/pickers; Profiles chrome + nested Frame; Settings panel host
│   ├── AppChrome.cs               - UI-adjacent ResourceLoader mapper (C# chrome only)
│   ├── WindowCloseGate.cs         - Sibling close-gate + pure WindowCloseDecision (hide / Exit / dirty discard)
│   ├── SingleInstanceKey.cs       - UI-free AppInstance key from the exe folder (one launcher per copy)
│   ├── Setting.cs                 - App settings model (Versions, DefaultVersionId, HasCompletedSetup)
│   ├── SettingsValidation.cs      - Settings path validation + behavior enum parsing
│   ├── WindowBehavior.cs          - Minimize/close behavior enums
│   ├── UiMode.cs                  - Panel mode enum (Profiles, Settings, Onboarding)
│   ├── WindowLayoutConstraints.cs - UI-free main-window MinWidth / MinHeight / DefaultWidth (DIP) plus page gutter and form max width
│   ├── LauncherVersionPolicy.cs   - Pure stable X.Y.Z Launcher version parse / compare
│   ├── LauncherBuildInfo.cs       - Compile-date metadata for About
│   ├── GitHubReleaseChecker.cs    - Fail-silent startup check plus on-demand detailed result
│   └── AppTheme.cs                - Windows light/dark detection + card surface colors
├── Adapters/                      - remaining thin Windows adapters
│   ├── IProfileCredentialStore.cs - Credential Manager contract keyed by Profile ID
│   ├── WindowsCredentialStore.cs  - Credential Manager adapter keyed by Profile ID
│   ├── WindowsNetworkIdentityText.cs - Normalize / parse DOMAIN\user or UPN
│   └── ProcessStarter.cs          - Thin process-start adapter
└── Avatar/                        - Avatar presentation shared by board and Edit Profile
    ├── LogoValidation.cs          - Logo file extension validation
    ├── LetterAvatar.cs            - Letter avatar helpers (letter + palette)
    ├── AvatarPaint.cs             - Shared Avatar presentation (letter vs custom image) for board + Edit Profile preview
    └── ColorContrast.cs           - Card contrast / color parse helpers

Power BI Launcher.Tests/                - Unit tests for lifecycle, Version registry draft, Scan policy, identity, validation, close-gate, avatar-file, and GitHub update-check helpers (`Fakes/` holds in-memory doubles; sources stay flat)
Power BI Launcher.Tests.PbiDesktopFixture/ - VERSIONINFO stub (Product/Description = Power BI Desktop) for FileVersionInfoReader tests
```

Runtime also creates `profiles.json` and a user `settings.json` beside the exe after first save. Paths are resolved with `AppContext.BaseDirectory` so a shortcut’s “Start in” folder cannot hide the files.

## Configuration files

Both files sit next to `Power BI Launcher.exe`. Relative paths follow the process working directory and break when a shortcut’s Start in folder is not the exe folder.

Saves write `<file>.tmp` beside the target and then rename it over the file, so an interrupted write leaves the previous file intact. Load retries a file another program holds (antivirus, backup, sync) for under a second. A file that cannot be parsed is moved to `<file>.<yyyyMMdd-HHmmss>.bak` (never replacing an earlier backup), the launcher starts without it, and the window reports which file was set aside. A file that stays unopenable, or an unparsable one that could not be moved, is left in place and every save to it is refused for that session (the user is told to restart), so an empty in-memory state never overwrites it. An unloaded `settings.json` of that kind does not restart onboarding.

### `settings.json`

```json
{
  "UserProfilePath": "C:\\Users\\<user>\\AppData\\Local\\Power BI Launcher\\Profiles",
  "Versions": [
    {
      "Id": "11111111-1111-1111-1111-111111111111",
      "DisplayName": "Desktop · Downloaded",
      "Path": "C:\\Program Files\\Microsoft Power BI Desktop\\bin\\PBIDesktop.exe",
      "Kind": "Desktop"
    }
  ],
  "DefaultVersionId": "11111111-1111-1111-1111-111111111111",
  "MinimizeBehavior": "Minimize",
  "CloseBehavior": "Close",
  "HasCompletedSetup": true
}
```

- **UserProfilePath**: Defaults to `%LOCALAPPDATA%\Power BI Launcher\Profiles` when empty; can be changed in Settings
- **Versions** / **DefaultVersionId**: Registered Power BI executables and the Default Version. A `PowerBIPath` value with no Versions list is read into one Default Version when it is a real Power BI executable; otherwise the registry stays empty. Do not add `PowerBIPath` as a second launch path
- **MinimizeBehavior**: `Minimize` (taskbar) or `SendToTray` (hide to tray)
- **CloseBehavior**: `Close` (exit) or `SendToTray` (hide to tray)
- **HasCompletedSetup**: Set to `true` after the user saves settings once; first-run onboarding runs while this is `false`

### `profiles.json`

```json
[
  {
    "Id": "a212d81c-cc62-4e9e-990a-b7721300f1f2",
    "Order": 1,
    "Name": "Client A",
    "IconPath": "",
    "AvatarColor": "#4F6BED",
    "CardColor": "#2D2D2D",
    "VersionId": null,
    "WindowsNetworkIdentity": null,
    "ShowOnTray": true
  }
]
```

- **IconPath**: Path to a custom image, or empty to use the letter Avatar
- **AvatarColor**: Hex color for the letter-Avatar circle (kept when the name changes)
- **CardColor**: Hex color (e.g. `#2D2D2D`) or empty string for the system default
- **VersionId**: Registered Version to launch, or `null` to follow the Default Version
- **WindowsNetworkIdentity**: Optional `DOMAIN\user` or UPN. The password is stored in Windows Credential Manager under `booldata.PowerBILauncher/run-as/{profile-guid}`, never in JSON

## Key Components
| Component | Responsibility |
|---|---|
| `MainWindow` | Shell edge: Fluent top-pivot UI, Drill / reduced-motion navigation, ContentDialog / file / folder / color pickers, tray host wiring, session-only GitHub update InfoBar, Settings About (Built date and Check for updates), Reorder/Done + Add Profile chrome (Add Profile opens an unsaved Edit Profile draft), save-Profiles-when-asked, Settings panel host |
| `SettingsPanel` | Settings / onboarding UI surface: named elements and events only; no lifecycle or policy. Folder to store Profiles raises Browse / Open requests and shows inline path chrome. About shows the launcher version, Built date, and Check for updates |
| `SettingsPanelSession` | Owns Settings attach/detach, reset-from-saved, Version registry list via `VersionRegistryDraft.ProjectRows`, Scan staging, Version Browse, folder Browse / Open, Save / Cancel; raises `SettingsSaved` / `SettingsCancelled` |
| `SettingsDirtyState` | Pure helper: Settings dirty vs saved snapshot; footer Save enablement (onboarding stays savable unless the folder path has an issue); root-change note when Profiles exist |
| `VersionRegistryDraft` | UI-free uncommitted Version registry (draft Versions, Default Version, rename, Scan staging, row projection) that commits through `ProfileLifecycle` |
| `ProfileBoardHost` | Owns Profile board refresh cards, empty-board CTA, sync Reorder mode on cards, activate (Launch vs inert via commanding policy, including Launching dwell), overflow More, Launch Order reorder chrome (via `ProfileBoardLaunchOrder`), shortcut enablement, and drop/nudge → apply → refresh |
| `ProfileCardInteraction` | UI-free commanding policy: card activate kind (Reorder / Launching → inert), automation name, whether header / overflow Reorder is offered (`count >= 2`), card and app shortcut availability, empty-board decision, caption facts, product-icon kind, adaptive More reveal |
| `ProfileBoardLaunchOrder` | Board-local six-dot grip / whole-card drag / gap indicator / nudge wiring; card factory builds visuals only |
| `LaunchOrderGapTargeting` | Pure helper: nearest reading-order insertion gap from card rects + pointer |
| `EditProfileSession` | Owns Edit Profile open → bind → dirty/save chrome → Save / Discard / Delete → custom Avatar file set/clear (staged until Save, so Discard touches no files); Add Profile draft persists only on Save; queries `EditProfileDirtyState`; paints preview via `AvatarPaint` |
| `AvatarPaint` | Shared Avatar presentation for board cards and Edit Profile preview (letter vs Ellipse+ImageBrush, contrast, async load + stale-path guard) |
| `ProfileCardFactory` | Builds board card visuals; Avatar hosts painted via `AvatarPaint`; Launch Order chrome attached by `ProfileBoardLaunchOrder` |
| `WindowCloseGate` / `WindowCloseDecision` | Sibling of the session: title-bar Send-to-tray hide, explicit Exit, dirty discard prompt, force-close, re-entrancy; queries session for dirty / TryDiscard |
| `ProfileCustomAvatarFiles` | UI-free copy/delete of launcher-owned custom Avatar files under Settings **Folder to store Profiles**: copy a pending image on Save (free name, never overwrites), delete the replaced file after the write succeeds |
| `ProfileBoardPage` / `EditProfilePage` | Nested Profiles Frame pages; board page is the Grid/ScrollViewer surface the host attaches to; Edit Profile page is UI surface only (controls, local affordances, raise events); while editing, nav → thin Back and board commanding hide |
| `WindowLayoutConstraints` | Pure helper: preferred window minimum and first-open default width from board + shared page gutter and form max width |
| `EditProfileDirtyState` | Pure helper: dirty vs open-time snapshot, or vs draft defaults when the snapshot is null (name, tray, icon, Avatar/card color, Version, Windows network identity) |
| `ProfileLifecycle` | JSON beside base directory, Version registry, credential persist, Isolated Environment folders, launch start info |
| `IPbiExecutableInspector` / `IPbiDiscovery` / `IProfileCredentialStore` / `IProcessStarter` | Thin Windows adapters; window and pages consume coordinator results only |
| `VersionSelection` | Default-first Version ComboBox options; empty and dangling evaluation |
| `TrayMenuComposition` | Pure helper: tray menu shape from Profiles (Show on tray) + setup-complete |
| `LaunchOrder` | Pure helper: contiguous Order renumber after board reorder (insert-at / move-to-index / nudge) |
| `TrayIcon` | Win32 notify icon; themes popup (fail soft); left-click toggle; dispatches launch / exit; re-adds the icon on `TaskbarCreated` |
| `Program` / `SingleInstanceKey` | Custom `Main` (`DISABLE_XAML_GENERATED_MAIN`): `AppInstance.FindOrRegisterForKey` per exe folder; a second launch redirects and exits, the running instance shows and focuses its window |
| `Profile` | Model: isolated Power BI environment (`ShowOnTray`, `VersionId`, `WindowsNetworkIdentity`) |
| `Setting` | Model: app-level preferences, Version registry, Default Version, setup completion |
| `ProfilePaths` | Resolves profile data directories under the configured profiles root. The profile folder name is short (12 hex) because Power BI's Analysis Services workspace adds ~125 chars below LOCALAPPDATA and PBIDesktop.exe cannot see paths past MAX_PATH. The `AppData\Local` / `AppData\Roaming` nesting under the redirected USERPROFILE is mandatory: Windows resolves the known folders from `%USERPROFILE%\AppData\…`, not from LOCALAPPDATA / APPDATA, and .NET Framework returns "" for a missing known folder (Power BI then fails with "Access to the path 'Microsoft\Power BI Desktop…' is denied") |
| `ProfileFolderMigration` / `PowerBiPathBudget` | Startup + pre-launch move of long `<root>\<guid>\AppData\…` folders to the short layout; launch/settings preflight that blocks roots too long for Desktop (≤113) or Report Server (≤108) LOCALAPPDATA |
| `ProfileValidation` / `SettingsValidation` / `LogoValidation` | Pure validation helpers. `SettingsValidation.TryValidateProfilesFolder` returns empty / relative / invalid-character / path-budget issues; missing folders are allowed because Save creates them |
| `LauncherVersionPolicy` | Pure stable `X.Y.Z` parse/compare; local, prerelease, malformed, and unknown installed versions are ineligible |
| `LauncherBuildInfo` | Reads compile-day `AssemblyMetadata` `BuildDate` for Settings About; missing or invalid metadata hides the Built line |
| `GitHubReleaseChecker` | `CheckAsync` for the startup InfoBar (fail-silent; only a strictly newer `vX.Y.Z` with an HTTPS matching GitHub release URL). `CheckDetailedAsync` for Settings **Check for updates** (latest / available / failed; ineligible skips the request) |

## Data Flow
```
User selects profile
  → ProfileLifecycle resolves Version (explicit ID or Default Version)
  → Microsoft Store Version → refuse (shared Windows sign-in cannot be isolated; see decisions/versions-and-network-identity.md)
  → Power BI executable bar runs on that path
  → Optional Windows network identity reads Credential Manager (no current-user fallback)
  → ProcessStartInfo: that exe, empty arguments, working directory = exe folder, child-only isolation env
  → Network-only credentials applied when identity is enabled
  → PBI Desktop launches; launcher does not watch the child after Start

Completed-setup window init
  → LauncherVersionPolicy rejects local / prerelease / malformed / unknown (no request)
  → GitHubReleaseChecker.CheckAsync GET repos/booldata/power-bi-launcher/releases/latest
  → Strictly newer vX.Y.Z + HTTPS matching release URL
  → Shell InfoBar (session-only close; View release opens that URL)

Settings About Check for updates
  → Ineligible builds disable the button (no request)
  → GitHubReleaseChecker.CheckDetailedAsync GET repos/booldata/power-bi-launcher/releases/latest
  → Three inline outcomes: You have the latest version; an available version with View release; Could not check for updates
```

## Key Decisions
| Decision | Rationale |
|---|---|
| WinUI 3 / WASDK | Recommended UI stack for new Windows-only desktop apps; Fluent / Windows 11 look |
| Unpackaged + self-contained | Portable copy-deploy without admin or separate WASDK runtime install |
| No MVVM mandate | Project is small; code-behind + lifecycle seam is enough |
| JSON flat files next to exe | Portable and easy to inspect; `AppContext.BaseDirectory` survives wrong Start-in folders |
| First-run via `HasCompletedSetup` | Onboarding until the user saves settings once |
| Process-only env isolation | Profile paths are set only on `ProcessStartInfo` - never via `Environment.SetEnvironmentVariable` |
| Single instance per copy | Two launchers on the same JSON overwrite each other and show two tray icons; see `decisions/shell-and-navigation.md` |
| Best-effort Win32 tray | No first-party WinUI NotifyIcon; composition drives launches → separator → Exit; left-click show/focus/hide |
| Isolated Environment folder layout | Profile folders stay on the short layout; `ShowOnTray` is a Profile field |
| Profile board is a small non-virtualized Grid | Profile counts are small and item recycling would swap card content; see `decisions/profile-board.md` |
| Nested Profiles Frame for Edit Profile | Board ↔ Edit Profile are pages; Settings and onboarding stay panels (`UiMode` is Profiles, Settings, or Onboarding); see `decisions/shell-and-navigation.md` |
| Edit Profile SettingsCard-style page | Page layout, draft, and save rules are in `decisions/edit-profile.md` |
| Version registry | Settings Scan (staging + Confirm) or Browse; exactly one Default Version; Profiles follow it unless `VersionId` is set |
| Windows network identity | Identity text on the Profile; password in Credential Manager (`booldata.PowerBILauncher/run-as/{profile-guid}`); `UseCredentialsForNetworkingOnly`; not Power BI service / Entra ID / MFA |
| Drill navigation (Edit Profile) | Profiles `Frame` stack on; `DrillInNavigationTransitionInfo` into Edit Profile; `GoBack` reverse Drill on leave; `SuppressNavigationTransitionInfo` when animations off |
| Window preferred minimum size | `OverlappedPresenter` mins from `WindowLayoutConstraints` (559×Profiles chrome + one 240-wide card; 32-epx gutters). First-open width is 815 (two profile columns); shrinking and widening still allowed. |
| Profiles surface Fluent icons | Reorder/Done, Add Profile, and overflow menu items use `FontIcon` + labels; no Launch/Edit badges; Launch Order uses a six-dot grab handle |

## Known Limitations / Technical Debt
- Scan discovery is best-effort (uninstall keys, default folders, Store packages, Start Menu). Browse covers copies Scan misses.
- MSIX / Store packaging is optional later, not the v1 path
- Profile isolation beyond USERPROFILE / AppData / TEMP (e.g. HOMEDRIVE / HOMEPATH) not verified yet
- Locked design records live under `Documentation/decisions/`. `.scratch/` is local temporary work and is not tracked.
