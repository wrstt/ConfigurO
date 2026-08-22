# Developing ConfigurO

ConfigurO is a Windows configuration, privacy and cleanup utility.
C#, WinForms, .NET Framework 4.8. Ships as a single elevated executable.

The interface is **Nocturne**: two modes, one accent, one control language.
`NocturneTheme` is the sole source of truth for colour, geometry and type — if
a value is in question, it is what the code says, not what any external mockup
said.

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
build/                    Linux type-check and headless render harness
tools/                    generators for the icon table and the app feed
templates/                silent-configuration templates
docs/                     guides and screenshots
assets/logo/              the ConfigurO mark
```

## The rules that matter

1. **No hex literals in the UI.** Every colour comes from `NocturneTheme`.
   Two modes, one accent; `NocturneTheme.Current` drives both, and controls
   repaint from the `NocturneTheme.Changed` event.
2. **Every size goes through `NocturneScale.S()`.** The app declares
   Per-Monitor-V2 DPI awareness, so design-token pixels are 96-DPI values that
   must be scaled at use.
3. **Add a tweak in exactly one place** — `TweakRegistry.Build()`. The Tweaks
   screen, silent configurations and policy reinforcement all read that table.
   Windows-version gating is `MinBuild` / `RequiresWindows11` on the entry.
4. **The helpers are load-bearing.** `OptimizeHelper`, `CleanHelper`,
   `HostsHelper`, `PingerHelper`, `IndiciumHelper`, `IntegratorHelper`,
   `UWPHelper`, `StartupHelper` and the i18n system predate the redesign and
   are widely depended on. Extend alongside them (as `Win11Tweaks` does)
   rather than restructuring them.
5. **Text is drawn with GDI+, never `TextRenderer`.** The bundled Inter
   faces live in a `PrivateFontCollection`; use `NocturneFonts` and dispose
   what it returns. Inter covers Latin, Greek and Cyrillic only, and GDI+ does
   not font-link a privately-registered face, so `NocturneFonts` swaps to a
   system UI face for the nine languages in other scripts — never bypass it by
   constructing a `Font` directly. Measure through `NocturneDraw`, whose
   scratch surface carries the DPI you will draw at; a plain `Bitmap` is 96 DPI
   and understates every width on a scaled display.
6. **Strings go through `I18n.Get(key, englishFallback)`.** It never throws on
   a missing key, which matters because new strings land before translations.
   All 658 keys currently resolve in all 28 languages; when you add one, add it
   to `Resources/i18n/EN.json` and to the other 27, or the fallback silently
   turns that language back into English for that string.

## Verifying without Windows

There is no .NET Framework toolchain on Linux, so two harnesses stand in.
Neither is part of the shipping build; both are guarded by `MONO_LINUX_CHECK`.

```
build/check.sh              type-check the whole project with Mono's mcs
build/render-resources.sh   compile Resources.resx (needed once by the renderer)
build/render.sh             paint the UI headlessly into build/render/out/*.png
```

`check.sh` must report **0 errors**. `render.sh` produces dark and light sheets
of the shell, the control language, every screen and the first-run picker — the
fastest way to see whether a change looks right. It forces the system font
fallback, because libgdiplus can register a private font it cannot then
rasterise.

**Know what these cannot tell you.** Both have hard blind spots, and each one
has already let a real bug reach a release:

- **libgdiplus is not GDI+.** It reports `Graphics.DpiX` correctly but measures
  and draws point-sized fonts at a fixed pixel size regardless. Any bug in the
  relationship between text size and DPI is invisible here *by construction*.
  The render's 125% pass covers layout that only adds up at 100% — boxes,
  padding, column math — and nothing about font metrics.
- **A `Form` cannot be realised headlessly** (Mono needs X11 for it). Anything
  that must be reviewable belongs in an `NControl`, with the surrounding chrome
  in a static method the harness can call — see `FirstRunForm.PaintChrome`.

A clean render is not a verified fix. Say which changes were observed and which
were only reasoned, and get the rest onto Windows before calling them done.

## Regenerating data

```
tools/svg_to_cs_icons.py <svg-dir> src/ConfigurO/Nocturne/NocturneIconData.cs
tools/build_feed.py [upstream-feed.json]      → feed/feed.json
tools/build_feed.py --check                   → regenerate, then probe every link
```

`build_feed.py` owns the download links. Publishers with a machine-readable
index are re-resolved on every run (`RESOLVERS`); the rest are pinned endpoints
(`VENDOR`); entries with nothing trustworthy are listed in `NO_LINK` with the
reason, so a blank tile is visibly deliberate rather than a regression. The
upstream feed it reads contributes only `Tag`, its links are years stale, the
repository behind it was archived on 2026-01-20 so it will never change again,
and nothing in the app reads `Tag` anyway. Treat it as inert: kept for
provenance, not for data, and never the basis of a new resolver. `--check`
fetches the first kilobyte of every emitted link and fails on anything that is
not a Windows installer; run it after regenerating, because the Apps screen
names the downloaded file from the URL and then runs it, so a wrong file is
executed rather than rejected.

It also rebuilds `feed/icons.zip` from `feed/icons/` on every run, so dropping
a PNG in that folder and naming it in `CATALOG` is the whole job; `--check`
fails if an entry names a file the pack does not contain. Tiles are square and
drawn at 38pt, so a wide wordmark is useless however high its resolution —
prefer the product mark, and check what you picked actually is that product's
(a vendor favicon is often the parent brand's: krita.org serves the KDE mark,
google.com the Google G).

## Building for real

Visual Studio 2022 or MSBuild with the .NET Framework 4.8 targeting pack:

```
nuget restore ConfigurO.sln
msbuild ConfigurO.sln /p:Configuration=Release
```

New files must be added to `src/ConfigurO/ConfigurO.csproj` — it is an
old-style project and does not glob.

## Cutting a release

The in-app updater reads `version.txt` and then downloads
`releases/download/<version>/ConfigurO-<version>.exe`. So:

1. bump `version.txt` **and `Program.Major`/`Program.Minor` together**, and
   add a matching `## [x.y]` section to `CHANGELOG.md`;
2. bump `AssemblyVersion`/`AssemblyFileVersion` in `Properties/AssemblyInfo.cs`
   to `x.y.0.0`, so the file properties agree with the release;
3. push a tag equal to that version — `1.1`, not `v1.1`.

There are two ways to get this wrong and both fail silently, in the field,
after the release has gone out.

**The version has to parse as a `float`.** `UpdateHelper.Present` runs
`float.TryParse` over the fetched `version.txt` and `return`s with no message
when it fails. `1.0.1` does not parse — two decimal points is not a float — so
publishing it stops every installed client from ever seeing an update again,
and there is no correcting it afterwards, because the parser is already inside
the binary they are running. Go `1.0` → `1.1` → `1.2`. If a patch component is
ever genuinely needed, `Program.GetCurrentVersionToFloat`, `Present` and
`Changelog` have to move to `System.Version` first, and every client older than
that change is still unreachable.

**`Program.Minor` is what the comparison actually reads.** `Present` compares
the server's `version.txt` against `Program.Major`/`Program.Minor` — not
against the `version.txt` sitting in the tree. Ship a build whose constants
still say the old version and it fetches the new one, installs it, restarts,
reports the old version and offers the same update again: a loop with no exit.
`release.yml` checks the tag against `version.txt`; nothing checks `Program`.

`.github/workflows/release.yml` builds on `windows-latest`, refuses to continue
if the tag and `version.txt` disagree, renames the exe to what the updater
expects, and publishes the release with that version's changelog section as the
notes. Nothing about this works while the repository is private: the updater,
the app catalogue and the icon pack are all fetched over anonymous HTTPS.

## Outstanding: not yet confirmed on Windows

Three changes shipped in 1.1 without a Windows run. They compile and they
render, but for the reasons in *Verifying without Windows* above, none of them
is observed. Check these against a real display before building on them:

1. **The DPI measurement fix.** Measurement now happens at the DPI the caller
   draws at. The arithmetic predicts the exact truncation that was reported
   (`Reinforce polic…`, ~20% short at 125%), but libgdiplus cannot reproduce
   it. Check that button labels fit and that text no longer collides with what
   sits beside it.
2. **The title-bar repaint.** The bar was reported blank until the pointer
   crossed it; it now forces a paint on show and on activate. That is a
   targeted guess at "never got a first paint", not a confirmed diagnosis. If
   it is still blank on first launch, the fix is wrong and needs a different
   approach.
3. **Inter, and the script fallback** for the nine non-Latin languages. Both
   compile; neither has been seen on a real display.

If any of the three is wrong, fix it and cut the next version — the updater
cannot express a patch release (see above).

## Known gaps, deliberately left

- 10 of the 147 catalogue entries have no download link and 7 have no icon.
  Each is listed in `NO_LINK` in `tools/build_feed.py` with the reason: the
  publisher ships only a `.zip`, gates downloads behind a form,
  hotlink-protects the file, or has shut down. A 16px favicon blown up to a
  tile reads as broken rather than as absent, which is why those stay blank.
- `docs/screenshots/` still shows the legacy interface. Regenerate once the app
  runs.
- The `## [1.0]` changelog section describes work that `ConfigurO-1.0.exe` does
  not contain, because it was written after that tag was cut. `## [1.1]` says
  which parts shipped for the first time in 1.1.
