# Handoff — 23 August 2026

`docs/DEVELOPING.md` is the map for working in the code. This is what is *true
right now* and what to do next.

## Where things stand

- Repo **https://github.com/wrstt/ConfigurO**, private, default branch `main`.
- Latest release **2.7**. `main` and the tag agree.
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
chrome, no Mica, no rounded corners. It also reports itself as **Windows 7**, so
the UWP screen is gated off and Win11-only tweaks are hidden.

## Open, and what is known about each

1. **UWP and Apps header actions overlap each other and the title** on a
   narrower window — reported with a screenshot. Cannot be reproduced under
   Wine, because Wine reports Windows 7 and the UWP screen is correctly hidden.
   `NScreen.OnLayout` lays actions right-to-left from `Width - Pad` and records
   `_actionsLeft` for the title to truncate against, so the title should not be
   overrun; the likely cause is a control drawing wider than its own bounds —
   the "Include system apps" checkbox is the suspect. **Do not guess at this;
   reproduce it on Windows at the reported size first.**
2. **Nepali draws as boxes under Wine.** Wine-only: it answers
   `new Font("Noto Sans Devanagari")` with Tahoma, so the name check correctly
   rejects it. Nirmala UI is present on Windows 8+. Worth confirming on a
   Windows machine without Indic language support.
3. **The setup bootstrapper's download-verify-install path has never run.** It
   compiles, links, embeds the app and imports wintrust/crypt32 — all verified —
   but needs a clean VM with no .NET Framework to exercise for real.
4. **SmartScreen.** No code fix exists. Sectigo EV ≈ $280/yr, DigiCert ≈ $560/yr,
   one-year maximum from Feb 2026. EV needs a registered entity; SSL.com does a
   sole-proprietor EV with notary verification. Azure Trusted Signing ≈ $10/mo
   but builds reputation rather than granting it. Deferred deliberately.
5. `docs/screenshots/` still shows the legacy interface.

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
