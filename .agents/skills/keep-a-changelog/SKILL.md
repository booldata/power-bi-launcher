---
name: keep-a-changelog
description: >-
  CHANGELOG.md Keep a Changelog 1.1.0 writer. Use when adding Unreleased
  notes, cutting a version section, or editing CHANGELOG.md / release notes.
---

# Keep a Changelog

Read [`Documentation/keep-a-changelog.md`](../../../Documentation/keep-a-changelog.md) before any write. Then read root `CHANGELOG.md`.

## Unreleased

1. Collect the **notable** user-facing differences in the work (not the git log).
2. Classify each as Added, Changed, Deprecated, Removed, Fixed, or Security.
3. Append each entry under `## [Unreleased]` in the matching `###` group; create the group heading if it is missing.
4. Omit empty groups. Do not add a version heading until a release is being cut.

**Done when** every notable change from the work sits under `## [Unreleased]` in the right group, wording is for launcher users (glossary terms, quoted chrome in sentence case), and no empty `###` headings were introduced.

## Cut a release

1. Confirm heading shape in [`Documentation/conventions.md`](../../../Documentation/conventions.md) (Release Workflow): `## [X.Y.Z] - YYYY-MM-DD` or `## [X.Y.Z-beta.N] - YYYY-MM-DD`.
2. Move the `Unreleased` groups into that new section (today's date). Leave `## [Unreleased]` in place with no groups.
3. Insert the section above older releases. Refresh footer compare links so `[unreleased]` points at the new tag `...HEAD` and the new tag points at the previous tag.

**Done when** the version heading is unique and non-empty, `Unreleased` is empty but present, and footer links match the new latest tag.

## Entry shape

```markdown
### Added
- **Add profile** opens Edit Profile as an unsaved draft.
```

One notable difference per bullet. Not a commit subject (`Fix AvatarPaint letter offset`).
