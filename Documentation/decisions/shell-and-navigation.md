# Shell and navigation

## Shell

- Top pivot **Power BI | Settings**. Edit Profile is a child page of the board, not a third tab. First-run setup is the Settings page with a flag, not a separate screen.
- Mica window, system light/dark theme.

## Single instance

One launcher runs per copy (per exe folder). A second launch hands activation to the running instance, which shows and focuses its window, then exits.

Why: the launcher hides in the tray. Without this, clicking the shortcut again would start a second process with its own in-memory Profiles, both overwriting `profiles.json` / `settings.json`, and two tray icons.

The key is per exe folder, not global, so separate portable copies stay independent. A named mutex was rejected: it detects the second instance but cannot hand over activation to show the window.

## Edit Profile navigation

- Board and Edit Profile are pages in a nested `Frame` with Drill transitions (suppressed when Windows animations are off). While editing, the pivot is replaced by **Back**.
- **Save** / **Discard** sit in a pinned footer on Settings and Edit Profile so they stay visible while the form scrolls. Save is disabled until something changes.

## Sources

- https://learn.microsoft.com/en-us/windows/apps/windows-app-sdk/applifecycle/applifecycle-single-instance
- https://learn.microsoft.com/en-us/windows/apps/design/signature-experiences/materials
