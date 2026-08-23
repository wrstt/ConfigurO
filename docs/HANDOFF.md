# Handoff — 23 August 2026

`docs/DEVELOPING.md` is the map for working in the code. This is what is *true
right now* and what to do next.

## Where things stand

- Repo **https://github.com/wrstt/ConfigurO**, private, default branch `main`.
- Latest release **2.9**. `main` and the tag agree.
- Every release ships two assets: `ConfigurO-<v>.exe` (portable) and
  `ConfigurO-Setup-<v>.exe` (installs the .NET Framework first if absent).
- `build/check.sh` reports 0 errors **and 0 warnings**, and now fails on either,
  because MSBuild builds with `/warnaserror+`.

## The big change this session: the app can be run here

Wine runs ConfigurO on this machine. That reaches screens no harness can — the
shell, the dialogs, and the first-run picker, which needs a real window and had
only ever been seen on a user's PC. Recipe and limits are in
`docs/DEVELOPING.md`. It found the Korean font bug within a minute of working.

The loop is now: push → CI → download artifact → run under Wine → screenshot →
fix, in about two minutes, without asking the user for anything.

**Drive it without input simulation** by writing `LastScreen` into
`~/.wine-configuro/drive_c/ProgramData/ConfigurO/ConfigurO.json` before each
launch. `xdotool` is not installed.

Two things Wine cannot answer: which fonts a Windows machine has (it resolves a
missing name to Tahoma rather than failing), and anything DWM — no title bar
chrome, no Mica, no rounded corners. It reports itself as **Windows 7** by
default, but that is only a registry value: set `ProductName` and `CurrentBuild`
and the UWP screen and the Win11-only tweaks come back. The prefix here is
already set to Windows 10 22H2.

It can also be built for here without CI: `mcs` against Mono's 4.8 reference
assemblies produces something real .NET Framework 4.8 will load. Recipe in
`docs/DEVELOPING.md`.

## Open, and what is known about each

1. ~~UWP and Apps header actions overlap each other and the title.~~ **Fixed in
   2.8, reproduced and verified under Wine.** It was not a layout fault at all:
   `ButtonBase` sets `ControlStyles.Opaque`, which suppresses
   `OnPaintBackground`, and `MoonCheck.OnPaint` fills nothing — so the
   checkbox's client area kept whatever WinForms' shared double buffer last
   held, which is the sibling that painted just before it. "Select all" and the
   Uninstall button's outline were being drawn *through* "Include system apps",
   and the same on the Apps footer with "Refresh links" under "Install after
   downloading". `MoonRadio` and `MoonToggle` had it too. Both checkboxes also
   now measure themselves rather than using a hardcoded width, which was
   clipping every translation longer than the English string.

   Two things made it reproducible that the last session had ruled out: Wine's
   Windows version is a registry value, so the UWP screen can be opened here;
   and `mcs` can build a Wine-runnable exe locally, so the loop is seconds, not
   a CI round trip. Both are written up in `docs/DEVELOPING.md`.
2. **Nepali draws as boxes under Wine.** Wine-only: it answers
   `new Font("Noto Sans Devanagari")` with Tahoma, so the name check correctly
   rejects it. Nirmala UI is present on Windows 8+. Worth confirming on a
   Windows machine without Indic language support.
3. ~~The setup bootstrapper's download-verify-install path has never run.~~
   **Run for the first time in 2.9**, in a second Wine prefix built with no .NET
   Framework and no Wine-Mono (`WINEDLLOVERRIDES="mscoree,mshtml=d"`, then
   `wineboot -u`). Detection, the prompt, the download of Microsoft's real
   1.4 MB web installer, the signature check and the launch all ran. It found a
   real fault: the installer exited 0 without installing anything, setup believed
   it, and ConfigurO was written to Program Files and launched on a machine with
   no framework. Setup now re-reads the registry and refuses. Fixed and verified.

   Two caveats on what that run proves. Wine's `wintrust` is not Windows'
   — `TrustedMicrosoftBinary` returning true here says the code path executes,
   not that the verdict is right, so signature rejection is still unexercised.
   And the .NET installer will not actually install under Wine, so the success
   path (framework genuinely arrives, setup continues) has still never run.
4. **SmartScreen.** No code fix exists. Sectigo EV ≈ $280/yr, DigiCert ≈ $560/yr,
   one-year maximum from Feb 2026. EV needs a registered entity; SSL.com does a
   sole-proprietor EV with notary verification. Azure Trusted Signing ≈ $10/mo
   but builds reputation rather than granting it. Deferred deliberately.
5. `docs/screenshots/` still shows the legacy interface. Now straightforward:
   `sweep.sh`-style capture under Wine gets all ten screens in both themes. The
   one caveat is that Wine has no DWM, so the captures have no rounded corners,
   no shadow and no Mica — the window content is right, the frame is not.

## Traps, and the ones that cost the most

- **A logger must never throw.** `LogInfoSilent` appends to a buffer that only a
  silent run creates. A diagnostic line added to `NocturneFonts` put a caller on
  the ordinary startup path and killed every launch for five releases. Use
  `Logger.LogInfo`.
- **Failure has to be visible.** `LoadSettings` caught everything and called
  `Environment.Exit(0)`; the unhandled handler logged and returned, leaving a
  process alive holding the single-instance mutex. Both fixed, and the error
  dialog now carries the exception type, the site and six stack frames. That
  dialog is what finally located the crash.
- **A hand-painted `CheckBox`, `RadioButton` or `Button` that fills no
  background shows the previous control's pixels.** `ButtonBase` sets
  `ControlStyles.Opaque`, so `OnPaintBackground` never runs, and WinForms'
  double buffer is shared between controls. This shipped, visible in the
  header, for the whole 2.x line. Rule 5 in `docs/DEVELOPING.md`.
- **A control painted behind another control is not painted at all.** The input
  hint was drawn on the `NTextBox` frame, underneath the real `TextBox` child
  that sits exactly where the text goes. Eight fields, four screens, never once
  visible, and the only symptom was one glyph's descender poking out below the
  child. It is `EM_SETCUEBANNER` now. Before painting anything in a container,
  check what child is going to cover it.
- **A hardcoded control width is a bug in 27 languages.** "Flush DNS cache" at a
  fixed 100px rendered "Flush DNS cac" in *English*. Measure the label.
- **`build/check.sh`'s `-nowarn` list is not hiding CI failures.** It looks like
  it should be — the csproj sets `TreatWarningsAsErrors` and no `NoWarn` — but
  the real CI log shows csc reporting `0 Warning(s)` on the same tree where mcs
  reports three. The suppressions compensate for mcs being stricter than csc.
  Checked against the actual build log; do not "fix" it without doing the same.
- **Text laid out by adding the previous box's height.** Five instances of one
  mistake — screen header, tweak row, sidebar footer, card header, about card —
  each producing lines that touched or overlapped. A line box is never exactly
  its line height. Check any new pair.
- **The version parses as a float.** `1.0.1` is not a float and `1.10` reads as
  `1.1`, older than `1.9`. Minor runs 0–9, then bump major. Nothing enforces it.
- **Measure and draw with the same metrics**, and give text room to breathe:
  size a label down rather than trimming it.
- **libgdiplus here has no X11 backend**, so `Form` cannot be realised under
  Mono even with Xvfb. Wine is the way to see a real window.
- **`check.sh` suppressing a warning MSBuild treats as fatal** produced a
  confident green that meant nothing. It no longer suppresses 0649 and fails on
  any warning.

## Note on paths

The project is `~/Desktop/configuro-modern`. `~/Desktop/ConfigurO` beside it is
the old pre-rebrand tree — not the working copy, and not what CI builds.
