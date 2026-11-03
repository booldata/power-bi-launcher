# Edit Profile

## Draft until Save

**Add profile** opens Edit Profile as an unsaved draft. Nothing is written to `profiles.json`, no Profile folder is created, and nothing appears on the board or tray until **Save changes**. Leaving an untouched draft discards it silently; a changed draft asks Discard or Stay.

## Page layout

- While editing, the Power BI / Settings pivot is replaced by **Back**. **Save changes**, **Discard**, and the unsaved indicator sit in the pinned footer (see [`shell-and-navigation.md`](shell-and-navigation.md)).
- Top to bottom: an identity card (name, **Upload icon** or **Change icon** and **Remove icon**, circular Avatar on a tile tinted with **Card color**, **Avatar color** and **Card color** pickers), then **Launch** (Version, Windows network identity, **Show profile in system tray**), then a **Profile folder** row with **Open folder**, then a full-width **Danger zone**.
- A draft hides the Profile folder row and Danger zone, and opens with the name field focused. **Reset card color** appears only after a custom card color is chosen; a custom image hides the Avatar color picker.
- Save returns to the Profile board.

Rejected: **Copy path** on the folder row (**Open folder** covers it), and a numeric Launch Order field (Launch Order belongs to Reorder mode).

## Saving is all-or-nothing

- Save always writes the whole Profile, never part of it. Discard restores the state from when the page opened.
- **Avatar image changes are staged until Save.** Picking or removing an image only changes the preview. Save copies the new image under a fresh name, writes `profiles.json`, and only then deletes the old launcher-owned image. If the write fails, the new copy is deleted and the old Avatar is kept. Discard touches no files.
- Only image copies the launcher made (inside the Profile folder) are ever deleted, never the user's original file.

Rejected: copying or deleting image files on pick/remove, because Discard could not undo it.

## Unsaved changes on close

With the Close button set to **Send to tray**, closing the window only hides it, and unsaved edits stay open. Any action that actually exits the app (the Close button set to **Close**, or **Exit** from the tray) asks Discard or Stay first when there are unsaved changes; Stay cancels the exit.
