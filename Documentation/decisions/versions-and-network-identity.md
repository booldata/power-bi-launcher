# Power BI Versions and Windows network identity

Consultants and external contributors need Power BI Desktop and Power BI Desktop for Report Server side by side, picked per Profile. Some on-premises sources also need the Windows identity people already use with `RUNAS /netonly`.

## Version registry

- Settings keeps a list of registered Versions and exactly one **Default Version**. Profiles follow the default unless they pick a specific Version.
- A Version has its own id, so moving or renaming the executable does not break the Profiles that use it.
- Versions are added by **Scan** (registry, default install folders, Start Menu) or **Browse**.

## What counts as Power BI

A file is accepted only if it is named `PBIDesktop.exe` and its file properties say "Power BI". This is checked when registering and again before every launch, so a moved or replaced file fails loudly instead of starting something else.

## Launch

- If the Profile's Version is missing, unregistered, or fails the check, the launch stops with an error. **It never falls back to the Default Version**, because opening the wrong product silently is worse than an error.
- Profile isolation (`USERPROFILE`, `LOCALAPPDATA`, `APPDATA`, `TEMP`, `TMP`) is set only on the Power BI process, never on the launcher.
- Report Server Desktop launches exactly like Desktop: the same `PBIDesktop.exe` with no arguments, from its own install folder. There are no special command-line flags.

## Microsoft Store Versions are blocked

Profiles exist so each one signs in to its own account. The Microsoft Store version of Power BI Desktop cannot do that from a launcher, so it is never registered or launched from a Profile. A Profile that silently opens on the wrong account (and could publish to the wrong tenant) is worse than one that refuses to start.

Why it cannot be isolated (tested October 2026, Store Desktop 2.157):

- Store Desktop is a packaged app, so it ignores the Profile's folder settings and stores its files inside its own package folder.
- Even swapping that package folder per Profile does not help: sign-in is stored in the shared Windows account store (the same one Teams, Outlook, and OneDrive use), so every Profile ends up on the same account.

The downloaded installer and Report Server Desktop are not packaged and stay fully isolated per Profile.

Scan still lists a Store install, greyed out and marked *Not supported*, so users understand why it cannot be used rather than thinking Scan is broken. Store Desktop opened from the Start menu keeps working normally.

Rejected: linking or copying the package folder per Profile (sign-in stays shared), swapping the shared Windows sign-in folders per launch (would sign Teams, Outlook, and OneDrive in and out), and warning but launching anyway (still lands on the wrong account).

## Windows network identity

- Optional and off by default. Stores a `DOMAIN\user` or UPN on the Profile.
- The password is stored **only** in Windows Credential Manager, keyed by Profile ID, so two Profiles with the same username keep separate passwords. It is never written to `profiles.json`.
- The password is not validated when saved. If it is missing or unreadable at launch, the launch stops and offers **Edit Profile**; it never falls back to the current Windows user.
- Launch uses the network-only credentials option (same as `RUNAS /netonly`) and keeps Profile isolation.
- This is **not** Power BI service, Entra ID, MFA, or SSO sign-in.

Rejected: cloud or shared password vaults, MFA/SSO-aware sign-in.

## Sources

- https://learn.microsoft.com/en-us/power-bi/report-server/install-powerbi-desktop
- https://learn.microsoft.com/en-us/power-bi/fundamentals/desktop-get-the-desktop
- https://learn.microsoft.com/en-us/windows/msix/desktop/desktop-to-uwp-behind-the-scenes
