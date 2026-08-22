<p align="center">
  <img src="assets/logo/configuro-logo-512.png" width="140" alt="ConfigurO">
</p>

<h1 align="center">ConfigurO</h1>

<p align="center">
  Windows configuration, privacy and cleanup — in one window.<br>
  <sub>by <b>WRSTT</b> · Windows 7 – 11 · .NET Framework 4.8.1</sub>
</p>

---

ConfigurO puts the settings Windows scatters across a dozen dialogs — telemetry,
Recall, the taskbar, startup entries, the hosts file, DNS, UWP packages — behind
one dark, dense, keyboard-friendly interface.

**Ten tools, one window:**

| | |
|---|---|
| **Tweaks** | 84 registry, service and policy switches, grouped and searchable |
| **Cleaner** | Measures then clears temp files, dumps, error reports and browser caches |
| **Startup** | Enable, disable or remove anything that launches at sign-in |
| **Hosts** | Edit entries, apply curated block lists, lock the file read-only |
| **Apps** | Download and install 147 common apps straight from their vendors |
| **Network** | Ping with live output, switch DNS provider, flush the resolver cache |
| **UWP Apps** | List installed packages with their size and uninstall in bulk |
| **Hardware** | CPU, memory, GPU, storage, board and network, copyable as a report |
| **Integrator** | Desktop right-click entries, custom Run commands, ready-made menus |
| **Settings** | 28 languages, behaviour, updates, troubleshooting |

## Install

Grab `ConfigurO.exe` from [Releases](https://github.com/wrstt/ConfigurO/releases).
It is a single file — no installer, nothing to unpack. It needs administrator
rights, because most of what it does writes to `HKLM`, services or system policy.

## Build

Requires Visual Studio 2022 (or MSBuild) with the .NET Framework 4.8.1
targeting pack.

```
nuget restore ConfigurO.sln
msbuild ConfigurO.sln /p:Configuration=Release
```

The executable lands in `bin/Release/`.

## Command line

ConfigurO can run without its window — see [docs/CONFS.md](docs/CONFS.md) for the
full list and [docs/AUTOMATION.md](docs/AUTOMATION.md) for applying a whole
configuration from a template.

```
ConfigurO.exe /config=template-windows11.json   apply a saved configuration
ConfigurO.exe /disable=uwp,apps                 hide individual tools
ConfigurO.exe /repair                           reset settings and support files
```

## Layout

```
ConfigurO.sln
src/ConfigurO/            the application
  Nocturne/               design system: tokens, type, icons, chrome, controls
  Screens/                one UserControl per tool
  Tweaks/                 the tweak catalogue and the runner that applies it
  Controls/               the restyled Moon* control suite
  Forms/                  the shell and the remaining dialogs
  Resources/              i18n (28 languages), scripts, flags, fonts
design_handoff/           the Nocturne spec and its interactive prototype
feed/                     app catalogue (feed.json) and icon pack
templates/                silent-configuration templates
docs/                     guides and screenshots
assets/logo/              the ConfigurO mark
build/                    Linux type-check and headless render harness
```

## Design

The interface is **Nocturne** — a single accent, two modes, one control
language. The specification and an interactive prototype live in
[`design_handoff/`](design_handoff/); the tokens are implemented once in
[`NocturneTheme.cs`](src/ConfigurO/Nocturne/NocturneTheme.cs) and nothing in the
UI hard-codes a colour.

## License

[GPL-3.0](LICENSE). ConfigurO began as a fork of the Optimizer project and keeps
its licence. Bundled third-party assets are listed in
[THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md).
