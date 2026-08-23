# Changelog

All notable changes to ConfigurO are recorded here. The updater reads the
heading for the running version to decide what to show, so keep the
`## [x.y]` format.

## [2.5]

### Fixes
- The title bar went blank when the window lost focus — brand, version, system
  details and window buttons all disappeared until it was clicked again. It
  repaints on losing focus now as well as on gaining it.
- Button labels are set slightly smaller when space is short instead of losing
  their last letters. "Reinforce policies" no longer becomes "Reinforce poli…"
  in a smaller window.
- The two lines in the sidebar footer were touching, and it said "1 tweaks
  applied".
- Buttons no longer show a focus outline just because the window opened. It
  appears when you start navigating with the keyboard and goes away when you
  click.

## [2.4]

### Interface
- More room between a tweak's name and its description. The two lines were a
  pixel apart, so a row read as one block of text rather than as a heading with
  a note under it. Cards are taller and further apart to match.
- The screen title and the count beneath it were overlapping.
- "Reinforce policies" no longer loses its last letters. The button was sized to
  hold the whole label but then drew the text into a box cut to the exact width
  it had just measured, so the slightest disagreement between measuring and
  drawing trimmed a word off the end.

## [2.3]

### Interface
- Each tweak now sits on its own card. A name on the left and its switch on the
  right had several hundred pixels of empty space between them on a wide window,
  with nothing connecting the two, so a row read as a label and an unrelated
  control rather than as one setting. The card gives that space something to
  belong to. Hovering lifts the card.

## [2.2]

### Interface
- Tweak names are set in medium weight at a slightly larger size, so a row reads
  as a name with a note under it rather than as two lines of similar text.
- Notes are a step brighter and slightly larger. They were the faintest grey in
  the palette, which made them hard to read at all.
- Rows are a little tighter, since the type now carries more of the structure.

## [2.1]

### Security
- Setup now verifies the .NET Framework installer it downloads before running
  it. It checks that the file carries a valid Authenticode signature and that
  the certificate names Microsoft Corporation; anything else is deleted and you
  are pointed at Microsoft's download page instead. HTTPS attests to who was
  contacted, not to what came back, and setup runs that file with administrator
  rights — a network that tampers with the download should not be able to
  choose what gets run.

## [2.0]

Numbered 2.0 rather than 1.10 because the updater compares versions as decimal
numbers, and 1.10 reads as 1.1 — older than 1.9.

### Added
- **ConfigurO-Setup.exe**, for PCs without the .NET Framework. It checks for
  version 4.8, offers to download it from Microsoft if it is missing, then
  installs ConfigurO into Program Files with a Start Menu shortcut and starts
  it. Setup needs nothing itself: it is a native program with no runtime of its
  own, so it works on a machine with nothing installed. The plain
  `ConfigurO-<version>.exe` is unchanged for anyone who already has the
  framework.

### Changed
- Now built against .NET Framework 4.8 rather than 4.8.1. 4.8.1 is not available
  for Windows 7, 8, 8.1 or Windows 10 before 21H2, so the app was requiring
  something those systems cannot install while claiming to support them. 4.8 is
  built into Windows 10 (May 2019) and later and installs on everything older
  that ConfigurO supports. Machines with 4.8.1 are unaffected.
- If the .NET Framework is older than 4.8, ConfigurO now says so and links the
  download instead of failing partway through with an unrelated error.

## [1.9]

### Fixes
- Any failure in the Hosts tool crashed the app instead of reporting itself. The
  five messages shown when a hosts operation fails were built in a way that
  always throws, so the error handler was worse than the error.
- Dialogs could crash rather than open if the translations had not loaded. The
  update prompt, file unlocker, hosts editor, about box and both startup dialogs
  now fall back to English instead.
- Restarting Windows Explorer could leave the desktop, taskbar and Start menu
  gone. Explorer is stopped and started again as part of applying some tweaks;
  if starting it back up failed, nothing retried and nothing said so. It now
  retries, and if it still cannot, explains how to restart it from Task Manager.
- Restarting the machine and revealing a file in Explorer no longer throw if
  Windows refuses the request.

## [1.8]

### Fixes
- The app would not start at all: it failed with "Object reference not set to an
  instance of an object" before showing a window. A line added in 1.3 to record
  whether the bundled fonts had loaded used a logging method that only works
  during a silent configuration run, and throws on an ordinary launch. The
  diagnostic meant to make a font problem visible was stopping the app instead.
- Logging can no longer bring the app down. The silent-run methods return
  quietly when there is nothing to write to, rather than throwing at exactly the
  moment something was worth recording.

## [1.7]

### Fixes
- When the app cannot start, the message now says what failed and where instead
  of only "Object reference not set to an instance of an object". Ctrl+C copies
  it.
- A fault in the first-run language chooser can no longer stop the app starting.
  It opens with English already loaded, so the app continues without it.

## [1.6]

### Fixes
- The app could fail to start with "Object reference not set to an instance of
  an object", on every launch, and stay that way. Settings were written straight
  over the existing file, so a process that was killed mid-write left an empty
  one behind; an empty settings file loaded as nothing rather than failing, and
  the app read from it and died. Settings are now written alongside and swapped
  in, so the file on disk is always complete, and a file that cannot be read is
  replaced with defaults instead of stopping the app.
- The messages shown before the main window appeared were read from the
  translations in a way that could itself fail if the translations had not
  loaded. They fall back to English now.

## [1.5]

### Fixes
- The app could be started and simply not appear -- no splash, no window, no
  message -- with a second attempt reporting it was already running. A failure
  while loading settings exited silently, and an unhandled error left a process
  alive with no window still holding the single-instance lock, so nothing would
  open again until that process was ended by hand. Failures now say what went
  wrong and where the log is, and always release the lock.
- Starting the app relaunches it with administrator rights, and the first
  process was holding the single-instance lock while the second started, so the
  two raced each other. Losing that race is what produced "already running" on a
  first launch. The lock is handed over before the elevated copy starts.
- The splash screen could keep a failed launch alive on its own.
- Declining the administrator prompt is treated as a decision rather than an
  error.

### Interface
- "ConfigurO is already running" now offers to restart: it closes the other
  instance and starts a fresh copy, for when that instance is not responding.
- The window, taskbar button and Alt-Tab now show the ConfigurO icon. Only the
  .exe carried it before.
- The notification-area icon is removed properly when the app closes instead of
  leaving its slot behind.

## [1.4]

### Interface
- The tweak list has been re-spaced. Rows were tighter inside themselves than
  they were from one another, so the list read as a wall of text; there is now
  more air between rows than between a name and its note, which is what lets the
  pair read as one item.
- Removed the rule under every row. Eighty-four of them stacked made the list
  look like a spreadsheet, and the spacing already separates one row from the
  next.
- Each group of tweaks now sits in a panel. A label on the left with its toggle
  against the far right edge left a wide empty span between the two that grew
  with the window; the panel gives that span something to belong to, and
  separates one section from the next without reintroducing any rules.
- Hovering a row lifts it as an inset rounded card rather than washing the full
  width of the pane.
- Labels are inset further from the edge of the content area.

## [1.3]

### Fixes
- The title bar could show as a light grey strip across the top of the window,
  or stay blank until the pointer crossed it. It is a child window, so it clips
  the shell and the strip it covers is the one part of the client area the
  form's own background can never fill; with a transparent background of its
  own, the bar's colour depended entirely on its custom paint landing, and when
  that was missed the strip showed the raw window-class brush. The bar now
  carries an opaque background that tracks the theme, so the fallback is the
  right colour whatever happens to the paint.
- The interface could be drawn in the system font instead of the bundled Inter.
  Font loading gave up permanently after a single failed attempt -- the splash
  screen loads fonts from its own thread before the main window asks, and a
  first failure there left every later request falling back for the rest of the
  session. It now retries, and records in the log whether the bundled faces were
  registered.

## [1.2]

Fixes for what the first Windows run showed: text truncating in controls sized
to fit it, and a Tweaks screen that read as clutter.

### Fixes
- Text was measured with one set of metrics and drawn with another. Measurement
  used `GenericTypographic`; drawing used a `GenericDefault`-based format, which
  reserves about a sixth of an em of side bearing that typographic measurement
  does not report. Every string was therefore drawn into a box slightly narrower
  than it needed, and because those formats trim with an ellipsis, GDI+ resolved
  it by dropping characters -- a button sized by AutoFit to its own label still
  rendered "Reinforce polic...". The shortfall is a fraction of the em, so it
  grew with the font's pixel size and looked like a DPI fault; the 1.1 DPI fix
  was correct but unrelated. Both paths measure and draw the same way now.

### Interface
- Tweak rows show the one-line summary written for them. They had been showing
  the legacy long-form help instead: across 66 tweaks that text averages 78
  characters against 41, 72% of it is longer than the summary, and a third of it
  carries hard line breaks, because it was written for a dialog with room for
  paragraphs and bullet lists. Flattened onto a single row it truncated.
- That long-form help is now the hover tooltip, where the paragraphs belong.
  Nothing translated was discarded, and the tooltip is drawn in the app's own
  palette rather than left as a light system rectangle on a dark window.
- Section labels are set with 0.12em of letter-spacing, per the design's type
  ramp. Set solid they read as a cramped word rather than as a label.
- Searching a tweak matches its name, its summary and its long-form text.

### Fixes to the rebrand
- Two tweaks were named after the application because a find-and-replace had
  rewritten the product name where it formed part of a tweak's name. "Optimizer
  Network" had become "ConfigurO Network"; it is the tweak that disables network
  throttling, and is now named that. Croatian carried the same fault, and Urdu
  still carried the previous product name transliterated.
- Removed a leftover switch offering to disable "ConfigurO Insights". Nothing
  read it -- it was the previous project's opt-out for its own analytics, which
  this application does not collect -- and in 15 languages it implied otherwise.

## [1.1]

`ConfigurO-1.0.exe` was built seven commits before the 1.0 notes below were
finished, so a number of things those notes describe were never in a released
build. This is the first one that carries them, together with two faults found
on the first run on a scaled Windows display.

### Fixes
- Text was measured on a scratch `Bitmap`, which is 96 DPI, and then painted on
  a screen surface at the monitor's DPI. GDI+ converts a point size through the
  Graphics DPI, so at 125% every measurement came back around 20% short.
  `NButton.AutoFit` sized itself from that, which is why "Reinforce policies"
  drew as "Reinforce polic...", and anything laid out beside a measured element
  reserved too little room and collided with it. Measurement now happens at the
  DPI the caller will draw at.
- 23 tweak descriptions stopped mid-sentence with no ellipsis. Those strings
  carry hard newlines from the legacy dialogs, and GDI+ honours a break even
  under `NoWrap`, so a single-line row painted as far as the first one and
  dropped the rest. Single-line draws now flatten breaks to spaces, and the
  width is measured from the same flattened string so the ellipsis agrees.
- The custom title bar could stay blank until the pointer first crossed it --
  the hover handler invalidated a region that had never had its first paint. It
  now paints on show and on activate.

### Shipped for the first time
Described under 1.0, but not present in the build 1.0 was cut from.
- The first-run language chooser, rebuilt as one table of language, flag and
  native name -- and with it the three faults the old wiring had drifted into:
  clicking the Korea flag selected Chinese, the Ukraine and Bulgaria flags were
  connected to nothing, and Taiwan's flag was never shown because China's was
  used for both entries.
- The interface set in **Inter** instead of IBM Plex Sans, which read thin and
  wide at 12-14px. IBM Plex Mono stays for paths, IPs and console output.
- The nine languages written in scripts neither face covers -- Arabic, Persian,
  Urdu, Kurdish, Nepali, Chinese, Taiwanese, Japanese and Korean -- now draw in
  the system UI font Windows ships for the script rather than as rows of
  .notdef boxes. GDI+ will not font-link a privately registered face, so this
  has to be resolved per language.
- The picker's 28 flags reshipped at 160x95, so the form's DPI scaling
  downscales them instead of magnifying a 32x19 bitmap into a smear.
- Icons for the 34 app-catalogue tiles that drew without artwork.
- Tweak rows at 54px with 19px between a label and its tip, replacing 46px and
  16px, which read as one crowded block.

### Repository
- `docs/FEED.md` and `docs/DEVELOPING.md` now record that the upstream Optimizer
  feed was archived on 20 January 2026 and is read-only. It still serves, and
  nothing in the app reads the one field taken from it.

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
