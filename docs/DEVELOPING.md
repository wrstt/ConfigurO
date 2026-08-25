# Developing ConfigurO

ConfigurO is a WinForms application on .NET Framework 4.8, built with MSBuild
on Windows. The interface is hand-painted against a design system called
Nocturne; almost nothing on screen is a stock WinForms control.

## Building

You need MSBuild with the .NET Framework 4.8 targeting pack. Visual Studio 2022
has it, or Build Tools alone:

```
winget install --id Microsoft.VisualStudio.2022.BuildTools --override "--quiet --wait --add Microsoft.VisualStudio.Workload.ManagedDesktopBuildTools --add Microsoft.Net.Component.4.8.SDK --add Microsoft.Net.Component.4.8.TargetingPack --add Microsoft.VisualStudio.Component.NuGet"
```

Then restore and build:

```
nuget restore ConfigurO.sln
msbuild ConfigurO.sln /p:Configuration=Release
```

The executable lands in `bin/Release/ConfigurO.exe`. It requires administrator
rights — the manifest asks for them outright rather than failing halfway
through a registry write.

**Warnings fail the build.** Both configurations set
`TreatWarningsAsErrors`, and CI builds the same way, so an unused field stops
the build rather than reaching a tag.

**`ConfigurO.exe.config` is not shipped.** The release is a single executable,
so anything that only works with a config file beside the binary does not work
for users. See rule 2.

## Layout

```
ConfigurO.sln
src/ConfigurO/
  Nocturne/               design system — tokens, fonts, drawing, icon data
  Nocturne/Controls/      the N* control set (buttons, cards, tables, chrome)
  Screens/                one NScreen subclass per tool
  Tweaks/                 TweakRegistry (the catalogue) + TweakRunner (applies it)
  Controls/               the Moon* suite, restyled onto the Nocturne tokens
  Forms/                  MainForm (the shell) and the remaining dialogs
  Resources/              i18n, scripts, flags, fonts — reached via Resources.resx
feed/                     app catalogue (feed.json) + icon pack (icons.zip)
tools/                    generators for the icon table and the app feed
templates/                silent-configuration templates
docs/                     guides and screenshots
assets/logo/              the ConfigurO mark
```

## The rules that matter

1. **No hex literals in the UI.** Every colour comes from `NocturneTheme`.
   Two modes, one accent; `NocturneTheme.Current` drives both, and controls
   repaint from the `NocturneTheme.Changed` event.

2. **Scale comes from `NocturneScale`, and DPI comes from Win32.** Design
   tokens are 96-DPI values that must go through `NocturneScale.S()` at use.
   Never read `Control.DeviceDpi`: WinForms only reports a real DPI when the
   Per-Monitor-V2 switches in `App.config` are present, and the release ships
   no config file, so `DeviceDpi` answers 96 on every machine. Ask
   `NocturneScale.DpiOf(handle)` instead. The whole interface rendered at 1.0
   on scaled displays for eleven releases because nothing did.

3. **Hairlines are drawn through `NocturneTheme.DrawRounded`.** Surfaces paint
   under `PixelOffsetMode.HighQuality`, which puts sample points on pixel
   corners — a 1px pen on an integer coordinate straddles two rows and each
   gets about two thirds of the colour. `DrawRounded` insets the path by half
   the pen width so the line lands on one row. Fills are the opposite and want
   integer coordinates, which is why `FillRounded` does not inset.

4. **Add a tweak in exactly one place** — `TweakRegistry.Build()`. The Tweaks
   screen, silent configurations and policy reinforcement all read that table.
   Windows-version gating is `MinBuild` / `RequiresWindows11` on the entry.

5. **A hand-painted control must paint its own background, or say who does.**
   `ButtonBase` — and so `CheckBox`, `RadioButton` and everything derived from
   them — sets `ControlStyles.Opaque` in its constructor, which suppresses
   `OnPaintBackground` altogether. Override `OnPaint` without filling and the
   client area keeps whatever WinForms' shared double buffer last held, which
   is the sibling that painted just before. `SetStyle(ControlStyles.Opaque,
   false)` restores the pull-through from the parent. The same trap catches a
   `Label` set to `Color.Transparent` on a `UserPaint` form: the parent never
   runs `OnPaintBackground`, so give it a real colour.

6. **Text is drawn with GDI+, never `TextRenderer`.** The bundled Inter faces
   live in a `PrivateFontCollection`; use `NocturneFonts` and dispose what it
   returns. Inter covers Latin, Greek and Cyrillic only, and GDI+ does not
   font-link a privately-registered face, so `NocturneFonts` swaps to a system
   UI face for the nine languages in other scripts — never bypass it by
   constructing a `Font` directly. Measure through `NocturneDraw`, whose
   scratch surface carries the DPI you will draw at; a plain `Bitmap` is 96 DPI
   and understates every width on a scaled display.

7. **Strings go through `I18n.Get(key, englishFallback)`.** It never throws on
   a missing key, which matters because new strings land before translations.
   When you add one, add it to `Resources/i18n/EN.json` and to the other 27, or
   the fallback silently turns that language back into English for that string.

8. **Interaction eases; state switches.** `NControl` and `NPanel` expose
   `HoverAmount` and `PressAmount`, driven by `NAnim`. Paint from those floats
   rather than a bool, so a surface arrives and leaves instead of blinking.
   Selection and focus are state and should land at once. `NAnim` honours the
   Windows animation setting, so never animate around it.

9. **The window draws its own chrome.** `NocturneShell` is borderless with
   `WM_NCCALCSIZE` collapsed, and `CreateParams` puts back the frame style bits
   `FormBorderStyle.None` strips — without them there is no Aero Snap, no Snap
   Layouts, no system menu and no minimise animation. Because the client fills
   the whole window, a maximised window with a thick frame would push about
   eleven pixels of interface off every edge, so `PinMaximisedClient` holds the
   client rectangle to the monitor work area. Rounded corners, shadow and the
   DWM border come from Windows 11 itself via `DwmChrome`.

## Regenerating data

```
python tools/build_feed.py        # rebuilds feed/feed.json and feed/icons.zip
python tools/svg_to_cs_icons.py <svg-dir> src/ConfigurO/Nocturne/NocturneIconData.cs
```

App icons must be transparent PNGs. Artwork flattened onto an opaque
background shows as a hard box against the `#232532` cards.

## Cutting a release

Six things carry the version and the release workflow refuses to build if they
disagree:

| | |
|---|---|
| `version.txt` | what the updater reads |
| `Program.Major` / `Program.Minor` | title-bar badge, update comparison |
| `AssemblyVersion` / `AssemblyFileVersion` | assembly metadata |
| `CHANGELOG.md` | needs a `## [x.y]` heading |
| the git tag | must equal `version.txt` exactly |

Bump all of them, commit, then tag and push:

```
git tag -a 3.4 -m "ConfigurO 3.4"
git push origin main 3.4
```

The tag triggers `.github/workflows/release.yml`, which builds, names the
assets `ConfigurO-<v>.exe` and `ConfigurO-Setup-<v>.exe`, extracts that
version's changelog section as the release notes, and publishes.

The updater builds its download URL from `version.txt` on `main`, so pushing a
version bump before the release exists leaves every running copy offering a
download that 404s. Tag promptly.
