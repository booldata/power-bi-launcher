# Branching and releases

`main` is the only long-lived branch. Work happens on short-lived branches (`feature/*`, `fix/*`, `ux/*`) and merges by pull request after CI. `main` must always be releasable.

Tags on `main` select the release channel:

- `vX.Y.Z` → GitHub Release marked latest. The in-app update check notifies users.
- `vX.Y.Z-beta.N` → GitHub pre-release for testers. The in-app update check ignores it.

Rejected: GitFlow or a permanent `develop` / `pre-release` branch. A pre-release tag already gives testers their own channel without a second history to maintain.
