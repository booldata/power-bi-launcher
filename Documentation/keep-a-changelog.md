# Keep a Changelog

Root [`CHANGELOG.md`](../CHANGELOG.md) is the curated list of notable launcher changes for humans. The format is [Keep a Changelog 1.1.0](https://keepachangelog.com/en/1.1.0/). Launcher versions follow [Semantic Versioning](https://semver.org/spec/v2.0.0.html). Release heading shape and tag channels live in [`conventions.md`](conventions.md) (Release Workflow).

## Guiding Principles

- Changelogs are for humans, not machines.
- There should be an entry for every single version.
- The same types of changes should be grouped.
- Versions and sections should be linkable.
- The latest version comes first.
- The release date of each version is displayed.
- Mention whether you follow Semantic Versioning.

## Types of changes

Group notable changes under these headings. Omit a heading when that type has no entries.

- **Added** for new features.
- **Changed** for changes in existing functionality.
- **Deprecated** for soon-to-be removed features.
- **Removed** for now removed features.
- **Fixed** for any bug fixes.
- **Security** in case of vulnerabilities.

## This repo

- Keep an `## [Unreleased]` section at the top. Day-to-day notes go there. Cutting a release moves those groups into a new version section and leaves `Unreleased` empty (heading stays).
- Record **notable** user-facing differences, not every commit. A changelog entry often spans several commits. If you do nothing else, list deprecations, removals, and breaking changes.
- Write for launcher users. Use glossary terms from [`glossary.md`](glossary.md) (Profile, Version, Scan, Avatar). Fluent sentence case for quoted UI chrome.
- Dates are ISO 8601 (`YYYY-MM-DD`).
- Link version headings with footer compare URLs (`[Unreleased]` → latest tag `...HEAD`; each release → previous tag). First release links to its tag.
- Yanked releases stay in the file: `## [X.Y.Z] - YYYY-MM-DD [YANKED]`.
- GitHub Releases are generated from the matching version section body; they are not a substitute for `CHANGELOG.md`.

## Credit

Guiding Principles and Types of changes are from [Keep a Changelog 1.1.0](https://keepachangelog.com/en/1.1.0/) by [Olivier Lacan](https://github.com/olivierlacan).

Copyright (c) 2014 Olivier Lacan. Licensed under the [MIT License](https://github.com/olivierlacan/keep-a-changelog/blob/main/LICENSE).
