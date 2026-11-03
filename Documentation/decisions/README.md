# Decision records

One file per surface. Each record keeps the decisions that shape the product, why they were made when it is not obvious, and the alternatives that were rejected. Layout and styling details live in the code.

Product vocabulary is in [`glossary.md`](../glossary.md). File tree and seams are in [`architecture.md`](../architecture.md). If a record and `glossary.md` disagree, the glossary wins.

| Record | Covers |
|---|---|
| [`ui-and-packaging.md`](ui-and-packaging.md) | UI stack, unpackaged + self-contained deployment, MSIX |
| [`shell-and-navigation.md`](shell-and-navigation.md) | Single instance, top pivot, Edit Profile navigation, pinned Save |
| [`profile-board.md`](profile-board.md) | Card actions, Reorder mode, Launch Order |
| [`edit-profile.md`](edit-profile.md) | Draft until Save, page layout, staged Avatar files, unsaved-changes handling |
| [`tray.md`](tray.md) | Win32 tray icon, menu, dark theme |
| [`versions-and-network-identity.md`](versions-and-network-identity.md) | Version registry, Scan, launch rules, Microsoft Store Desktop, Credential Manager |
| [`trunk-based-releases.md`](trunk-based-releases.md) | Branching, tags, release channels |
