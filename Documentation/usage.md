# Usage

Power BI Launcher keeps a separate Power BI Desktop environment for each **Profile**, then launches that Profile from the board or the system tray.

## Install

1. Download the latest `Power-BI-Launcher-vX.Y.Z-win-x64.zip` from [GitHub Releases](https://github.com/booldata/power-bi-launcher/releases/latest).
2. Unzip it into a folder you can write to, for example `%LOCALAPPDATA%\Programs\Power BI Launcher`. No admin install. Avoid `Program Files` and other read-only folders: the launcher saves `settings.json` and `profiles.json` next to `Power BI Launcher.exe`.
3. Run `Power BI Launcher.exe`. Opening it again while it is already running shows that window instead of starting a second launcher.

Portable releases are currently unsigned, so Windows may show **Unknown publisher** or SmartScreen. You can still run the app after that prompt.

To confirm the zip matches the published checksum:

```powershell
$zip = "Power-BI-Launcher-v1.0.0-win-x64.zip"
$expected = (Get-Content "$zip.sha256").Split()[0]
$actual = (Get-FileHash $zip -Algorithm SHA256).Hash.ToLowerInvariant()
$actual -eq $expected
```

Building from source, publishing a portable folder, and cutting a release are documented in [`conventions.md`](conventions.md).

## Upgrade

1. Choose **Exit** from the tray icon so the launcher is not running.
2. Download the new zip and unzip it **into the same folder**, replacing existing files.
3. If the folder still has `PBI Launcher.exe` from an older build, delete that leftover file.
4. Run `Power BI Launcher.exe`. Your Profiles, Versions, and settings are still there.

The zip never contains `settings.json` or `profiles.json`, so unzipping over the folder keeps them. Settings and Profile data stay next to the exe and in `%LOCALAPPDATA%\Power BI Launcher\Profiles`. If you unzip into a new folder instead, copy those two files from the old folder before the first run, then delete the old folder. Otherwise the new copy starts at first-run setup with no Profiles. Recreating a Profile by hand gives it a new, empty folder, so you would have to sign in to Power BI again.

## Uninstall

1. Optional: delete each Profile in the launcher first. That removes its folder and its saved Windows network identity password in one step.
2. Exit the launcher from the tray and delete its folder (with `Power BI Launcher.exe`, `settings.json`, and `profiles.json`).
3. Delete the **Folder to store profiles** (default `%LOCALAPPDATA%\Power BI Launcher\Profiles`). It holds each Profile's Power BI data and custom Avatar images.
4. If you skipped step 1, open **Credential Manager** > **Windows Credentials** and remove any generic credentials whose name starts with `booldata.PowerBILauncher/run-as/`.

## Your data

| What | Where |
|---|---|
| Settings and the Version registry | `settings.json` next to `Power BI Launcher.exe` |
| Profile list (names, colors, order, Version, Windows network identity user name) | `profiles.json` next to `Power BI Launcher.exe` |
| Each Profile's Power BI sign-in, cache, temp files, and custom Avatar image | A subfolder of **Folder to store profiles** |
| Windows network identity passwords | Windows Credential Manager, one entry per Profile |

Nothing is sent anywhere. The only network request the launcher makes is to the GitHub Releases API, to check for a newer launcher version.

## First run

Until settings have been saved once, the app opens **Welcome - configure settings**.

1. **Scan** finds installed Power BI Desktop products (downloaded installer and Report Server). Tick the **Versions** to register, then confirm. **Browse** registers one `PBIDesktop.exe` immediately. A Microsoft Store install still appears in the scan results, greyed out and marked *Not supported*, with a warning; it cannot be ticked or browsed to (see [Microsoft Store version](#microsoft-store-version)).
2. Choose exactly one **Default Version**. Setup cannot finish without it.
3. Review **Folder to store profiles** (default: `%LOCALAPPDATA%\Power BI Launcher\Profiles`). Save creates that folder when it is missing.
4. Click **Save settings**. The **Power BI** board opens so you can add Profiles. **Cancel** stays hidden until setup is complete.

## Profiles

The **Power BI** board is a grid of Profile cards. Click a card to launch Power BI Desktop in that isolated environment.

1. Click **Add profile**. Edit Profile opens as an unsaved draft - nothing appears on the board or in the tray until **Save changes**.
2. Name the Profile. Optionally pick a Version (otherwise it follows the Default Version), a Windows network identity for Windows-authenticated data sources, an Avatar or letter color, a card color, and whether it shows on the tray.
3. Open **More** (`...`) or the card context menu for **Edit profile**, **Reorder**, and **Delete**. Delete is also on Edit Profile under **Danger zone**.
4. Enter **Reorder mode** with header **Reorder** (or overflow **Reorder**) when there are at least two Profiles. Drag a card, or use the grab handle **Move up** / **Move down** (Alt+Up / Alt+Down). Click **Done** or press Esc to return to launching.

Keyboard, when Edit Profile, a dialog, or a menu is not open: Ctrl+N adds a Profile, Ctrl+, opens Settings, Enter or Space launches the focused card, F2 opens Edit Profile, and Delete asks to delete it.

Windows network identity is only for Windows-authenticated Report Servers or data sources. It does not sign in to the Power BI service, and it does not replace Entra ID or MFA.

## System tray

The tray icon lists Profiles that have **Show profile in system tray**, in Launch Order, then **Show window** / **Hide window** and **Exit**. Each launch row is the Profile name only.

- Left-click the icon to show, focus, or hide the window.
- Minimize and Close can send the window to the tray; choose that in Settings.
- A failed tray launch shows and focuses the app with the same message as a board launch.
- Profile launches are omitted until setup is complete, when none qualify, or when Show on tray is off.

## Settings

After setup, **Settings** is the other top pivot. It holds the Version registry, **Folder to store profiles**, minimize and close behavior, and About. About shows the launcher version, the **Built** date, and **Check for updates** (**You have the latest version.**, an available version with **View release**, or **Could not check for updates.**). Changing the profiles folder does not move existing Profile folders.

After setup, startup may check GitHub Releases for a newer stable launcher and show a dismissible notice with **View release**. Failures stay silent. The app does not download or install updates itself.

## Isolated environments

Each Profile uses its own folders for Power BI Desktop data (sign-in cache, local app data, temp files). That keeps tenants and clients from sharing credentials. Folder layout and the JSON files next to the exe are documented in [`architecture.md`](architecture.md).

## Microsoft Store version

Profiles do not launch the Microsoft Store version of Power BI Desktop, and it cannot be registered. **Scan** lists it greyed out as *Not supported*, and **Browse** rejects it. Confirming a scan removes a Store Version already in the registry. Launching a Profile whose Version is still the Store version shows *The Microsoft Store version signs every profile in to the same account, so profiles cannot use it* and offers **Open settings** (when the Profile follows the Default Version) or **Edit profile** (when the Profile picked the Store version).

Why: the Store version is a packaged Windows app. It ignores the Profile's folders and signs in through the account store Windows shares with Teams, Outlook, and OneDrive. Every Profile would open on whichever account signed in last, which is the problem Profiles exist to prevent.

What to do:

1. Install the [downloaded version of Power BI Desktop](https://www.microsoft.com/download/details.aspx?id=58494) (or Power BI Desktop for Report Server). It can be installed alongside the Store version.
2. In **Settings**, **Scan**, register the downloaded Version, and make it the **Default Version**.
3. In Edit Profile, change any Profile that picked the Store version.

The Store version still appears (greyed out) in Scan, and still works when you open it from the Start menu, outside the launcher. Why it cannot be isolated, and what was tried, is in [`decisions/versions-and-network-identity.md`](decisions/versions-and-network-identity.md#microsoft-store-versions-are-blocked).
