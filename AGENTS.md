# Power BI Launcher

C# WinUI 3 utility: isolated Power BI Desktop **Profiles**, Version registry, tray launch. Minimal layering - code-behind + `ProfileLifecycle`.

## Forbidden Actions

- Confirm with the user before adding a NuGet package.
- Update the matching model class whenever the `settings.json` schema changes.
- Isolate profile paths only on `ProcessStartInfo.EnvironmentVariables` when starting Power BI (through `ProfileLifecycle`). Never call `Environment.SetEnvironmentVariable` on the launcher (`USERPROFILE` / `LOCALAPPDATA` / `APPDATA` / `TEMP` / `TMP`). Save/edit/delete may create profile directories; they must not mutate the launcher’s environment.
- Call VERSIONINFO, discovery, Credential Manager, and process-start only through `ProfileLifecycle` and its adapters (`FilePbiExecutableInspector`, `WindowsPbiDiscovery`, `WindowsCredentialStore`, `ProcessStarter`) - not from `MainWindow` or UI pages.
- Persist a Version registry plus one Default Version. Profiles follow that default unless `VersionId` is set. Do not restore a single Power BI Path.
- Keep Windows network identity as Profile text plus a Credential Manager password keyed by Profile ID. It is not Power BI service, Entra ID, MFA, or SSO.
- Resolve `settings.json` / `profiles.json` with `AppContext.BaseDirectory` (via `ProfileLifecycle`). Relative paths follow the process cwd and break when a shortcut’s “Start in” folder is not the exe folder.
- Gate first-run on `Setting.HasCompletedSetup`, not “settings file missing.” Do not add `settings.json` back to the csproj as a copied output/resource unless the setup-flag logic stays.
- Do not track `.scratch/`. It is local temporary work. Distilled decision records (one file per surface) live under `Documentation/decisions/`.
- Do not track restored copies under `.agents/skills/` (WinUI skills from [`skills-lock.json`](skills-lock.json); `npx skills experimental_install`, add `--copy` on Windows if the CLI cannot symlink). Restored skills: `winui-design`, `winui-dev-workflow`, `winui-code-review`, `winui-packaging`. Track first-party skills such as [`keep-a-changelog`](.agents/skills/keep-a-changelog/).

## Docs

- **Glossary** - [`Documentation/glossary.md`](Documentation/glossary.md). Reach when naming UI, writing copy, adding a domain type, or choosing Profile / Version / Scan / Avatar / Reorder wording.
- **Architecture** - [`Documentation/architecture.md`](Documentation/architecture.md). Reach when locating a file, adding a type, or changing a seam (lifecycle, board, tray, adapters). The hand-maintained file tree lives here.
- **Conventions** - [`Documentation/conventions.md`](Documentation/conventions.md). Reach when adding a source file, choosing a pattern, building, publishing, running tests, or adding a `Documentation/` file.
- **Scope** - [`Documentation/scope.md`](Documentation/scope.md). Reach when proposing a feature or deciding in vs out of scope.
- **Decisions** - [`Documentation/decisions/README.md`](Documentation/decisions/README.md). Reach before changing a surface (shell, board, Edit Profile, tray, Versions, packaging, releases) to see what is locked, why, and what was already rejected.
- **Keep a Changelog** - [`Documentation/keep-a-changelog.md`](Documentation/keep-a-changelog.md). Reach when writing `CHANGELOG.md`, Unreleased notes, or release notes. Load the `keep-a-changelog` skill.
