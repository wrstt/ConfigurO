# Third-party notices

ConfigurO bundles or derives from the work below. Each item keeps its own
licence; this file records what is included and where it came from.

## Optimizer

ConfigurO began as a fork of **Optimizer**, a Windows configuration utility
released under the GNU General Public License v3.0. The helper layer —
`OptimizeHelper`, `CleanHelper`, `HostsHelper`, `PingerHelper`,
`IndiciumHelper`, `IntegratorHelper`, `UWPHelper`, `StartupHelper`, the silent
configuration runner and the 28 translation files — descends from that project.
ConfigurO is likewise GPL-3.0; see [LICENSE](LICENSE).

- Upstream: https://github.com/hellzerg/optimizer
- Licence: GPL-3.0

## Inter

The interface is set in **Inter**, bundled as an embedded resource in
`src/ConfigurO/Resources/Fonts/` and registered at runtime. Nothing is
installed system-wide.

- Copyright © 2016 The Inter Project Authors
- Source: https://github.com/rsms/inter (v4.1)
- Licence: SIL Open Font License 1.1 — https://scripts.sil.org/OFL

## IBM Plex Mono

Paths, IP addresses and console output are set in **IBM Plex Mono**, bundled
the same way.

- Copyright © 2017 IBM Corp., with Reserved Font Name "Plex"
- Source: https://github.com/IBM/plex (v6.4.0)
- Licence: SIL Open Font License 1.1 — https://scripts.sil.org/OFL

## Remix Icon

The interface icons are the **Remix Icon** line set. The SVG outlines are
compiled into `src/ConfigurO/Nocturne/NocturneIconData.cs` by
`tools/svg_to_cs_icons.py` and drawn as vector paths, so no icon font ships.

- Source: https://github.com/Remix-Design/RemixIcon
- Licence: Apache License 2.0

## Json.NET

JSON handling for settings, silent configurations and the app catalogue.

- Copyright © James Newton-King
- Source: https://github.com/JamesNK/Newtonsoft.Json (13.0.3)
- Licence: MIT

## ByteSize

Byte-size formatting, vendored under `src/ConfigurO/ByteSize/`.

- Copyright © Omar Bahareth
- Source: https://github.com/omar/ByteSize
- Licence: MIT

## Steven Black hosts

The Hosts screen offers two curated block lists. They are fetched on demand and
are not redistributed with ConfigurO.

- Source: https://github.com/StevenBlack/hosts
- Licence: MIT

## App catalogue

`feed/feed.json` lists publicly available Windows applications and links to
their vendors' own download endpoints. ConfigurO redistributes no installers.
The icons in `feed/icons/` are the applications' own marks, used to identify
them.
