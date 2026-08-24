# Adding an app to the Apps screen

`feed/feed.json` is **generated**. Editing it by hand works until the next time
anyone runs the generator, which overwrites the file. Add the app to
[`tools/build_feed.py`](../tools/build_feed.py) instead.

## 1. Add it to the catalogue

`CATALOG` maps each group to a list of `(title, icon)` pairs. The icon name is
the base name of a PNG in `feed/icons/`, or `None` if there isn't one — the tile
still renders, just without artwork.

```python
'Utilities': [..., ('Everything', 'everything'), ('YourApp', 'yourapp')],
```

The groups are the ones the Apps screen shows:

| | | |
|---|---|---|
| Web Browsers | Messaging | Media |
| .NET | Java | Imaging |
| Documents | Security | Compression |
| File Sharing | Other | Online Storage |
| VC++ Redistributables | Developer Tools | Utilities |

## 2. Give it a link

Pick whichever of these fits, in order of preference:

- **`RESOLVERS`** — the publisher exposes something machine-readable (a GitHub
  releases API, a version index, a download API). Write a small function
  returning `(link32, link64)`. It re-runs on every regeneration, so the feed
  tracks new versions instead of freezing on whatever was current the day it
  was added. Prefer this.
- **`VENDOR`** — the publisher serves a stable, versionless URL
  (`https://download.ccleaner.com/ccsetup.exe`), or a version-pinned file is the
  only thing they publish. A lone string is used for both architectures; a
  `(x86, x64)` tuple sets them separately.
- **`NO_LINK`** — there is nothing worth linking. Record *why*. The tile shows
  "No link yet" and cannot be selected, and the reason keeps a deliberate blank
  from being mistaken for a regression later.

The link **must** end in `.exe` or `.msi`, or be a redirector that serves one.
The Apps screen names the downloaded file from the URL it was given and then
runs it, so a `.zip` gets saved as `.exe` and executed. That is why Paint.NET,
which ships only archives, is in `NO_LINK`.

## 3. Regenerate and verify

```
tools/build_feed.py --check
```

This rewrites `feed/feed.json`, then fetches the first kilobyte of every link
and fails on anything that is not a Windows installer — a 404, an HTML
click-through page, an archive, or an installer for another platform. All three
have been shipped by real publishers in this catalogue, so do not skip it.

## 4. Icons

Icons live in `feed/icons/` as PNGs with a transparent background, up to
256×256 and under 50 KB, and are served from
`raw.githubusercontent.com/wrstt/ConfigurO/main/feed/icons/`. `feed/icons.zip`
is the offline cache the app falls back to; regenerate it after adding one.

## The upstream feed, and why there isn't one any more

`build_feed.py` no longer reads anything from the project this was forked from.
Until 3.1 it did, and the note that used to sit here said dropping it "would
change no output" because `Tag` was the only field taken from it. That was
wrong, and wrong in the direction that matters.

`Tag` was indeed the only field read *deliberately*. But the link-resolution
loop ended in `elif up:` — a silent last resort that took the upstream `Link`
whenever our own resolvers and vendor endpoints came up empty. **38 of the 147
entries were reaching that branch**, so more than a quarter of the catalogue was
still being served links from a repository archived on 2026-01-20 and pinned to
2021 builds: Opera 81, Blender 2.93, OBS 27, Rufus 3.18, Node 16 (32-bit),
Sublime Text build 3211. Two had rotted outright — Epic's installer 404s, and
VLC's `.exe` URL answers 200 with an HTML page, which the app would have saved
as `vlc-3.0.16-win32.exe` and executed.

All 38 were replaced with endpoints resolved here: 20 stable publisher URLs in
`VENDOR`, 12 in `RESOLVERS` (seven GitHub releases, plus directory listings for
VLC, Node, GIMP, Blender and Opera), and five moved to `NO_LINK` with a reason.
`--check` now reports 141 links probed, 0 bad.

The catalogue therefore has no upstream. `RESOLVERS` and `VENDOR` are not a
stopgap covering for a lagging source — they are the only thing keeping it
current, and nothing else will ever fix a link that rots. Run `--check` after
regenerating, always: the app names the downloaded file from its URL and then
runs it, so a wrong file is executed rather than rejected.

Provenance stays recorded in [THIRD-PARTY-NOTICES.md](../THIRD-PARTY-NOTICES.md),
which is where it belongs. That is an attribution obligation under GPL-3.0 and
is not affected by any of this.
