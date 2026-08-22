# Changelog

All notable changes to ConfigurO are recorded here. The updater reads the
heading for the running version to decide what to show, so keep the
`## [x.y]` format.

## [1.0]

First release under the ConfigurO name, rebuilt around the **Nocturne** design.

### Interface
- Rebuilt the first-run language chooser, the one screen the redesign had not
  reached. It was 28 PictureBoxes and 28 RadioButtons at fixed coordinates,
  sized against a font the app no longer uses, with a hand-written click
  handler per flag. It is now one table: language, flag, native name. That
  table also fixes three faults the old wiring had drifted into -- clicking the
  Korea flag selected Chinese, the Ukraine and Bulgaria flags were connected to
  nothing, and the Taiwan flag was never shown at all because China's was used
  for both. Each name is drawn in a face that can render its own script, and
  the list is keyboard-navigable.
- The picker's 28 flags were 32x19-pixel bitmaps, upscaled by the form's DPI
  scaling into a smear. Reshipped at 160x95 with the letterboxing baked in, so
  the PictureBox stretch is a pure downscale.
- 34 app-catalogue tiles that drew without artwork now have icons, sourced from
  each publisher and normalised to 128x128. `tools/build_feed.py` rebuilds
  `feed/icons.zip` and fails `--check` if an entry references a file that is
  not in it.
- Replaced the tabbed window with a borderless shell: a 46px custom title bar
  and a 208px navigation rail, one screen per tool.
- New design system (`src/ConfigurO/Nocturne/`): a single accent, Dark and Light
  modes, one control language, all tokens defined in one place.
- Bundled Inter for the interface and IBM Plex Mono for paths, IPs and console
  output; typographic hierarchy is size and space rather than weight. Inter is
  what the Nocturne handoff was drawn against and holds its colour at 12-14px
  on any display.
- Nine of the 28 languages -- Arabic, Persian, Urdu, Kurdish, Nepali, Chinese,
  Taiwanese, Japanese and Korean -- are written in scripts Inter has no glyphs
  for, and GDI+ does not font-link a privately-registered face. Those languages
  now draw in the system UI face Windows ships for the script instead of a row
  of .notdef boxes.
- Icons are compiled vector outlines, so they stay sharp at any display scale.
- Rewrote the Moon* control suite — toggle, checkbox, radio, select, list,
  checked list, progress, menu renderer — against the new tokens.
- Retired the Ocean/Magma/Zerg/Caramel/Lime/Minimal themes and the accent colour
  picker. Appearance is now Dark or Light, remembered between runs.

### Windows 11
- Rounded window corners, immersive dark mode, and matching caption and border
  colours through DWM.
- Snap Layouts: hovering the maximise button raises the Windows 11 flyout.
- Optional Mica backdrop (Windows 11 22H2 and newer).
- Per-Monitor-V2 DPI awareness, with the layout rescaling when the window moves
  between displays.
- Accurate release detection via `RtlGetVersion`, including 21H2 through 25H2.
- New tweaks: disable Recall, disable Click to Do, disable Bing and web results
  in Search, disable suggested actions, disable setup reminders, hide the Task
  View button, hide the Start "Recommended" section, never combine taskbar
  buttons, add "End task" to the taskbar, open File Explorer to This PC, remove
  Gallery and OneDrive from the Explorer navigation pane, show file extensions,
  enable Sudo for Windows, disable memory integrity (HVCI), unlock all CPU cores.

### Tools
- Tweaks: all 84 switches come from one catalogue shared by the screen, silent
  configurations and policy reinforcement. Live search over names and tips.
- Cleaner: sizes are measured per location before anything is deleted.
- Startup: entries can now be disabled without being removed, via the same
  StartupApproved mechanism Task Manager uses.
- Hosts: curated block lists, and the read-only lock moved onto the screen.
- Apps: 15 categories, 147 entries, concurrent downloads with per-tile
  progress. The catalogue and its icons are cached for offline use.
  `tools/build_feed.py` now resolves download links itself rather than
  inheriting them from the upstream Optimizer feed, whose links are years
  stale: publishers with a machine-readable index (GitHub releases, the Edge
  enterprise API, python.org, KDE, Cursor) are re-resolved on every run, and
  the rest come from a table of stable vendor endpoints. `--check` downloads
  the first kilobyte of every link and fails on anything that is not a Windows
  installer. 137 of the 147 entries now carry a verified link, up from 93.
- Hardware: the WMI sweep runs off the UI thread and the report can be copied
  or saved.

### Fixes
- Version and size parsing now uses the invariant culture; on a comma-decimal
  locale the update check used to throw and fail silently.
- Bundled fonts are registered from disk rather than memory, and a failure in
  one step no longer discards every face. A `gdi32` call that could not be
  resolved used to take the whole font load down with it.
- `SaveSettings` used to return silently when the settings file was missing,
  which left the app unable to persist anything at all after a repair.
- Repair now restarts, instead of leaving the app running against a data
  folder it had just deleted.
- The unsupported-Windows message read the translation table before it was
  loaded and crashed instead of showing.
- The mouse wheel scrolls whatever is under the pointer. Windows sends it to
  the focused control, so panes only scrolled once clicked.
- The hosts file being missing is no longer reported as "read-only", which
  silently disabled the entire screen with no explanation.
- Downloads fall back to the profile's Downloads folder when the shell lookup
  comes back empty, rather than writing to the working directory.
- Long-running work marshals back to the UI safely when its screen has closed,
  and the cleaner's progress timer cannot outlive its screen.
- Screens that were never opened are disposed with the window.
- Ampersands from the translation files render as one character; they were
  doubled for WinForms buttons, which do not draw the new UI.

### Translations
- The 245 strings the redesign introduces are translated into all 27 languages.
  Every key in `EN.json` now resolves in every language file; the English
  fallback in `I18n.Get` remains, but nothing currently depends on it.

### Known gaps
- 10 of the 147 app-catalogue entries still have no download link; those tiles
  are shown but cannot be selected. Each is listed in `NO_LINK` in
  `tools/build_feed.py` with the reason — the publisher ships only a .zip, gates
  downloads behind a form, hotlink-protects the file, or has shut down.
- 7 catalogue entries have no icon, so their tiles draw without artwork: GOM,
  SugarSync, ImgBurn, RealVNC Server, RealVNC Viewer, Launchy and
  InfraRecorder. Nothing legible is published for these at a usable size, and a
  16px favicon blown up to a tile is a smear -- which reads as broken rather
  than as absent.
- `docs/screenshots/` still shows the legacy interface.
