# Glossary

Desktop launcher that isolates Power BI Desktop workspaces per client **Profile**.

## Language

**Profile**:
A named, isolated Power BI Desktop environment the user can launch or edit.
_Avoid_: account, workspace (ambiguous with Fabric/Power BI service), client (sometimes means the person, not the environment)

**Isolated Environment**:
The separate user profile, AppData, local AppData, and temp folders a Profile launches with, so Power BI sign-ins and caches never mix between Profiles. They live under **Folder to store profiles**.
_Avoid_: sandbox, container

**Power BI executable**:
The on-disk Desktop program the launcher starts for a Profile. Not every existing file - it has to actually be Power BI Desktop.
_Avoid_: Power BI Path, exe, binary

**Version**:
A registered Power BI Desktop installation a Profile can launch. It has a stable identity, a display name, a path to a Power BI executable, and a Version kind.
_Avoid_: install, build, edition, Power BI Path

**Launcher version**:
The semantic identifier of a Power BI Launcher release. It is distinct from Version, which identifies a registered Power BI Desktop installation.
_Avoid_: Version, app version, release version

**Version registry**:
The list of Versions the launcher knows about, plus exactly one Default Version.
_Avoid_: installs list, catalog, Power BI Path

**Version kind**:
Which product a Version is: Power BI Desktop (Desktop) or Power BI Desktop for Report Server (Report Server). Scan-generated display names distinguish Microsoft Store Desktop, downloaded Desktop, and Report Server (with release month when known).
_Avoid_: edition, product, type

**Default Version**:
The Version a Profile launches when it has not picked one.
_Avoid_: current version, system version, fallback version

**Scan**:
The Settings action that discovers Power BI Desktop installations so the user can register them as Versions.
_Avoid_: search, detect, find

**Windows network identity**:
An optional per-Profile identity whose saved credentials Power BI uses only for Windows-authenticated network access, such as Report Server or on-premises data sources. It does not change the process's local Windows identity or sign in to the Power BI service through Entra ID or MFA.
_Avoid_: run-as identity, account, login, Power BI sign-in

**Avatar**:
The circular identity visual for a Profile (letter + color, or custom image). It appears on the Profile board card and again on the Edit Profile page.
_Avoid_: icon (ambiguous with app/tray icon), logo

**Profile board**:
The main grid of Profile cards where the user launches a Profile or opens Edit Profile (via the card overflow). Title: **Power BI**. Each card shows the resolved Version as a caption and a small Desktop or Report Server product icon. An empty board shows **No profiles yet** with **Add profile**.
_Avoid_: gallery, home (unless referring to the Power BI pivot)

**Edit Profile**:
The child page of the Profile board where the user changes one Profile, or names a new one. **Add profile** opens it as an unsaved draft: nothing is written and nothing appears on the board or in the tray until **Save changes**. It is not app-wide Settings, and Launch Order is not edited here (see **Reorder mode**). Page layout and save rules: [`decisions/edit-profile.md`](decisions/edit-profile.md).
_Avoid_: detail page, profile settings (Settings is the app-wide panel), Actions section, System (Behavior) section

**Launch Order**:
The sequence Profiles appear on the Profile board and in tray launches (among Profiles with show on tray). Changed in **Reorder mode** (whole-card drag-and-drop to a gap, or grab-handle Move up/down); not a numeric field on Edit Profile.
_Avoid_: priority, rank, sort index (unless discussing persistence)

**Reorder mode**:
Temporary Profile board mode for changing Launch Order. Entered via header **Reorder** or card overflow **Reorder**; exited via **Done** or Esc. An accent-outlined **Reorder mode** banner holds Done. Six-dot grip affordance on; whole card draggable; gap insertion indicator (accent bar + slight widen); overflow hidden; card body inert for Launch / Edit Profile (drag still works). Persist Launch Order on drop (and on handle nudge). When fewer than two Profiles, Reorder is hidden and overflow Reorder is omitted or disabled.
_Avoid_: board edit mode, Edit Order mode, sort mode

**Show on tray**:
Whether a Profile appears as a launch item in the system tray menu. The row is the Profile name; Version and Windows network identity stay off the menu. Edit Profile UI copy: **Show profile in system tray** (in Launch).
_Avoid_: favourite, pin, shortcut, tray favourite

**Drill navigation (Edit Profile)**:
Board ↔ Edit Profile uses WinUI Drill page transitions both ways (honor reduced motion with plain navigate). A Connected avatar transition is rejected for this surface.
_Avoid_: hero animation (generic), shared element (framework jargon unless discussing WinUI APIs)

**Settings**:
The app-wide panel for the Version registry, **Folder to store profiles**, minimize and close behavior, and About (launcher version, **Built** date, **Check for updates**). Changing the folder does not move existing Profile folders. First-run setup uses the same panel (see **Onboarding**). Distinct from **Edit Profile**, which changes one Profile.
_Avoid_: profile settings, preferences, options (unless quoting Windows UI)

**Onboarding**:
The first-run variant of **Settings**, shown until `HasCompletedSetup`. Welcome title, **Cancel** hidden, setup InfoBar while a Default Version is missing, accented **Scan**. It is a flag on the Settings panel, not a second surface.
_Avoid_: setup wizard, first-run wizard, welcome page

**Copy casing**:
Fluent sentence case for commands, labels, descriptions, dialog titles, and menu items. Capitalize the first word of a sentence or label, plus product names (Power BI, Power BI Desktop, Report Server, Credential Manager). Domain terms (profile, version, default version, scan, settings) stay lowercase unless they start the string.
