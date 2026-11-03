# Scope

## Purpose
A Windows desktop utility that allows Power BI Desktop users to maintain multiple isolated user profiles and launch Power BI Desktop under a selected profile. Solves the problem of profile/credential conflicts when working with multiple Power BI environments or tenants.

## Goals
- Allow creation and management of named Power BI Desktop profiles
- Launch Power BI Desktop with fully isolated AppData, LocalAppData, and TEMP paths per profile
- Persist profile configuration including custom icons and launch order
- Provide a clean tray-accessible UI for quick profile switching

## Non-Goals (Out of Scope)
- Managing Power BI service (cloud) accounts or workspaces
- Treating Windows network identity as Power BI service, Entra ID, MFA, or SSO sign-in
- Modifying Power BI Desktop installer or binaries
- Automating report publishing or refresh

## Target Users / Personas
- Power BI developers or consultants working across multiple client tenants
- Users who need to maintain separate credential stores for different environments

## Constraints
- **Platform:** Windows only
- **Tech Stack:** WinUI 3, Windows App SDK, C# 14, .NET 10, Windows-only APIs
- **Dependencies:** Power BI Desktop (downloaded installer) or Power BI Desktop for Report Server must be installed; Windows App SDK shipped self-contained with the app
- **Microsoft Store Power BI Desktop:** out of scope for launch. It is packaged and signs in through the shared Windows account store, so Profiles cannot isolate it ([why](decisions/versions-and-network-identity.md#microsoft-store-versions-are-blocked))
- **Deployment Target:** Portable unpackaged folder copy; no admin / no separate runtime install required for v1
- **Packaging:** MSIX / Store optional later; not required for v1
- **Code signing:** Portable releases are currently unsigned and can show Unknown publisher / SmartScreen warnings

## Key Concepts / Domain Terms
Profile, Version, Default Version, Scan, Isolated Environment, Windows network identity, Launch Order, and the other domain terms are defined once in [`glossary.md`](glossary.md).

## References
- [Windows App SDK](https://learn.microsoft.com/windows/apps/windows-app-sdk/)
- [Distribute an unpackaged WinUI 3 app](https://learn.microsoft.com/windows/apps/package-and-deploy/unpackage-winui-app)
- UI stack and packaging decision: [`Documentation/decisions/ui-and-packaging.md`](decisions/ui-and-packaging.md)
- Shell, board, and tray locks: [`Documentation/decisions/shell-and-navigation.md`](decisions/shell-and-navigation.md), [`profile-board.md`](decisions/profile-board.md), [`tray.md`](decisions/tray.md)
