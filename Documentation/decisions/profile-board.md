# Profile board

## Cards

- Clicking a card **launches** the Profile. A short **Launching…** state ignores further clicks so a double-click does not start two Power BI windows.
- Other actions (**Edit profile**, **Reorder**, **Delete**) are in the card's **More** (`…`) menu, also available by right-click and keyboard. Hover is never the only way to reach an action.
- Delete from the board and from Edit Profile use the same confirmation and delete path.
- Keyboard: Ctrl+N add Profile, Ctrl+, Settings, Enter/Space launch, F2 edit, Delete delete.
- The board is a plain non-virtualized grid (up to 5 columns). Profile counts are small, and this avoids cards swapping content through item recycling.

## Reorder mode

- Launch Order is changed in an explicit **Reorder mode** (enter from the header or the More menu, exit with **Done** or Esc). Cards do not launch while reordering.
- Drag a card to a gap between cards, or use Move up / Move down (Alt+Up / Alt+Down) for keyboard users.
- Each drop or move **saves immediately**; Done only exits the mode.
- The tray menu uses the same Launch Order.

## Rejected

- Permanent Launch / Edit buttons on every card.
- Built-in `GridView` reordering and board virtualization.
