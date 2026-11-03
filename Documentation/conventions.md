# Conventions

Always-loaded Forbidden Actions live in root [`AGENTS.md`](../AGENTS.md).

## Approach
The codebase is intentionally minimal: a single WinUI 3 / Windows App SDK project with a UI-free Profile lifecycle seam. Keep changes focused and avoid over-engineering.

## Documentation files
Root files under `Documentation/` use kebab-case and are named for their job.

| File | Job |
|---|---|
| [`glossary.md`](glossary.md) | Domain language |
| [`architecture.md`](architecture.md) | Architecture, file tree, JSON next to the exe |
| [`conventions.md`](conventions.md) | Coding, build, publish, tests, releases |
| [`scope.md`](scope.md) | Purpose, goals, non-goals |
| [`keep-a-changelog.md`](keep-a-changelog.md) | Changelog conventions |
| [`usage.md`](usage.md) | How to use the launcher |

Decision records live under [`decisions/`](decisions/) as one kebab-case file per surface, indexed by [`decisions/README.md`](decisions/README.md). A record keeps only decisions that shape the product: the rule, why (when not obvious), what was rejected, and primary sources. Layout, styling, and AutomationIds stay in code. When a decision changes, edit the surface file; do not add a dated sibling.

## Code layout
- **Naming:** PascalCase for classes and methods, camelCase for locals and parameters
- **File Structure (app project only):** Put each new source file in the folder that owns that surface. `RootNamespace` stays `PowerBILauncher` - folders are presentation only; do not introduce folder-matched namespaces.
  - `ProfileBoard/` - board page, host, layout, cards, Launch Order chrome
  - `EditProfile/` - Edit Profile page, session coordinator, dirty-state helper
  - `Settings/` - Settings / onboarding UserControl, session coordinator, Version registry draft
  - `Versions/` - Version model, Scan policy and rows, Version selection, version-facing adapters (discovery, VERSIONINFO, executable inspector)
  - `Lifecycle/` - `ProfileLifecycle` seam, profile model, paths, migrations, path budget, custom Avatar files, profile validation
  - `Tray/` - notify icon, menu composition, menu theme
  - `Shell/` - main window, window behavior / close gate / layout, UI mode, theme, chrome mapper, settings model and validation, launcher version policy, GitHub release check
  - `Adapters/` - Credential Manager, process starter, Windows network identity text
  - `Avatar/` - Avatar paint, letter avatar, colour contrast, logo validation (shared by board and Edit Profile)
  - Root keeps `App.xaml` / `App.xaml.cs`, `Program.cs` (custom `Main`), `app.manifest`, the project file, `settings.json`, `Strings/<BCP-47>/Resources.resw` (WinUI PRI layout), `Properties/PublishProfiles/`, and `Assets/`
- **File Structure (test projects):** Stay flat at each test-project root, plus the existing `Fakes/` subdirectory. Do not mirror the app folders.
- **Patterns to Follow:** WinUI code-behind + `ProfileLifecycle` for persistence/launch; MVVM is acceptable if complexity grows but not required
- **Patterns to Avoid:** Do not introduce unnecessary abstractions or service layers

## Coding Standards
- Target .NET 10, C# 14 (SDK default), WinUI 3 / Windows App SDK
- Use `System.Text.Json` for any new JSON serialization (not Newtonsoft)
- Prefer `async/await` for any I/O operations
- The root `Directory.Build.props` defaults every build to x64; do not repeat `-p:Platform=x64`

## How to Run / Build
```
# Build (x64)
dotnet build "Power BI Launcher/Power BI Launcher.csproj"

# Run
dotnet run --project "Power BI Launcher/Power BI Launcher.csproj"

# Portable self-contained publish
dotnet publish "Power BI Launcher/Power BI Launcher.csproj" -p:PublishProfile=PortableProfile

# Or in Visual Studio: Properties/PublishProfiles/PortableProfile.pubxml (MSIX/Store optional later, not v1)

# Tests
dotnet test "Power BI Launcher.Tests/Power BI Launcher.Tests.csproj"

# Restore gitignored WinUI agent skills (winui-search.exe lives here)
npx skills experimental_install
```

## Release Workflow
- All releases are tagged from `main`. Short-lived topic branches (`feature/*`, `fix/*`, `ux/*`) merge by PR; tags (`vX.Y.Z` or `vX.Y.Z-beta.N`), not branches, select the channel. See [`Documentation/decisions/trunk-based-releases.md`](decisions/trunk-based-releases.md).
- Changelog entries follow [`keep-a-changelog.md`](keep-a-changelog.md) (Keep a Changelog 1.1.0). Day-to-day notes go under `## [Unreleased]`. When cutting a release, move those groups into a non-empty `## [X.Y.Z] - YYYY-MM-DD` or `## [X.Y.Z-beta.N] - YYYY-MM-DD` section and merge it to `main`
- A pushed `vX.Y.Z` tag reruns tests, publishes PortableProfile with Launcher version `X.Y.Z`, and creates the GitHub Release marked latest
- A pushed `vX.Y.Z-beta.N` tag does the same and creates a GitHub pre-release. In-app update checks ignore pre-releases; a beta build still notices a newer stable release
- The portable zip excludes PDBs, includes `LICENSE.md`, and ships with a `.sha256` checksum. It never contains `settings.json` or `profiles.json`, so unzipping a new release over an existing folder keeps the user's data
- Releases are currently unsigned and can show Unknown publisher / SmartScreen warnings
- Do not create or push a release tag unless the user explicitly requests it

## Useful Entry Points
Start from the file tree and the Key Components table in [`architecture.md`](architecture.md). `Program.cs` (custom `Main`, single instance), `MainWindow.xaml.cs` (shell), and `ProfileLifecycle.cs` (persistence and launch) are the usual first reads.
