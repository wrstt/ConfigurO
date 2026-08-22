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

## The upstream feed

`build_feed.py` still reads the Optimizer feed, but **only** for its `Tag`
values. Its download links are years out of date — a check in August 2026 found
17 of them dead, including one that served a macOS `.pkg` — so nothing it says
about links is trusted any more.

That feed is now **frozen**. `hellzerg/optimizer` was archived on 2026-01-20
and is read-only. It still serves — 106 entries, fetched and checked in August
2026 — so `build_feed.py` keeps working and nothing needs changing today. But it
will never gain an entry again, which means `RESOLVERS`/`VENDOR` are no longer a
stopgap covering for a lagging upstream. They are the only thing keeping the
catalogue current, and nothing else will ever fix a link that rots.

Worth knowing before spending any effort on it: `Tag` is the sole field taken
from upstream, and **nothing reads it**. It is deserialised into `AppInfo.Tag`
and never consumed — it was Optimizer's silent-install switch, and this app
downloads and runs installers interactively instead. An entry that matches
nothing upstream simply gets `''`. So the upstream fetch is already inert, and
dropping `UPSTREAM` altogether would change no output; it is kept only so the
provenance of the catalogue stays visible.
