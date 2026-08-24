#!/usr/bin/env python3
"""
Generate feed/feed.json -- the ConfigurO app-downloader catalogue.

CATALOG below is the catalogue: 15 categories, ~130 entries. This script emits
it and fills in download links from three sources:

  1. documented vendor endpoints for the runtime families that `appDefs`
     enumerates by version (.NET via aka.ms, VC++ 2015+ via aka.ms,
     Adoptium via its v3 installer API, Corretto via corretto.aws/latest);
  2. RESOLVERS -- publishers that expose a machine-readable index (the GitHub
     releases API, python.org's ftp listing, KDE's stable tree, Cursor's
     download API, AIMP's download page, and the plain directory listings VLC,
     Node, GIMP, Blender and Opera publish). These are re-resolved on every
     run, so regenerating the feed picks up new versions;
  3. VENDOR -- publishers with a stable, versionless download endpoint, or a
     pinned installer where that is the only thing they publish;
  4. nothing -- entries with no trustworthy link are emitted with empty
     Link/Link64 and the UI marks them unavailable rather than guessing.

Nothing is inherited from the feed this project was forked from. That feed was
archived in January 2026 and its links had been pinned to 2021 builds ever
since; 38 of these entries were still taking their only link from it, two of
which had rotted to a 404 and an HTML page. Every link is now resolved here.

Every emitted link is expected to end in .exe or .msi: the app names the
downloaded file from the URL it was given and then runs it, so a .zip would be
saved as an .exe and executed. `--check` probes every link and reports.

Usage: tools/build_feed.py [--check]
"""
import json, os, re, sys, urllib.request

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
ICON_BASE = "https://raw.githubusercontent.com/wrstt/ConfigurO/main/feed/icons/"

NET_V = ['x64 8', 'arm64 8', '8', 'x64 9', 'arm64 9', '9', 'x64 10', 'arm64 10', '10']
JAVA_V = ['x64 8', '8', 'x64 11', 'x64 17', 'x64 21', 'x64 25']
VC_V = ['x64 2015+', 'x86 2015+', 'arm64 2015+', 'x64 2013', 'x86 2013', 'x64 2012',
        'x86 2012', 'x64 2010', 'x86 2010', 'x64 2008', 'x86 2008', 'x64 2005', 'x86 2005']

# Microsoft keeps the pre-2015 redistributables on the Download Center under
# permanent per-release GUIDs; there is no aka.ms alias for them.
VC_LEGACY = {
    '2013': {'x86': 'https://download.microsoft.com/download/2/E/6/2E61CFA4-993B-4DD4-91DA-3737CD5CD6E3/vcredist_x86.exe',
             'x64': 'https://download.microsoft.com/download/2/E/6/2E61CFA4-993B-4DD4-91DA-3737CD5CD6E3/vcredist_x64.exe'},
    '2012': {'x86': 'https://download.microsoft.com/download/1/6/B/16B06F60-3B20-4FF2-B699-5E9B7962F9AE/VSU_4/vcredist_x86.exe',
             'x64': 'https://download.microsoft.com/download/1/6/B/16B06F60-3B20-4FF2-B699-5E9B7962F9AE/VSU_4/vcredist_x64.exe'},
    '2010': {'x86': 'https://download.microsoft.com/download/1/6/5/165255E7-1014-4D0A-B094-B6A430A6BFFC/vcredist_x86.exe',
             'x64': 'https://download.microsoft.com/download/3/2/2/3224B87F-CFA0-4E70-BDA3-3DE650EFEBA5/vcredist_x64.exe'},
    '2008': {'x86': 'https://download.microsoft.com/download/5/D/8/5D8C65CB-C849-4025-8E95-C3966CAFD8AE/vcredist_x86.exe',
             'x64': 'https://download.microsoft.com/download/5/D/8/5D8C65CB-C849-4025-8E95-C3966CAFD8AE/vcredist_x64.exe'},
    '2005': {'x86': 'https://download.microsoft.com/download/8/B/4/8B42259F-5D70-43F4-AC2E-4B208FD8D66A/vcredist_x86.EXE',
             'x64': 'https://download.microsoft.com/download/8/B/4/8B42259F-5D70-43F4-AC2E-4B208FD8D66A/vcredist_x64.EXE'},
}

CATALOG = {
 'Web Browsers': [('Chrome','chrome'),('Opera','opera'),('Firefox','firefox'),('Edge','edge'),('Brave','brave'),('Vivaldi','vivaldi')],
 'Messaging': [('Zoom','zoom'),('Discord','discord'),('Teams','teams'),('Telegram','telegram'),('Pidgin','pidgin'),('Thunderbird','thunderbird'),('Trillian','trillian')],
 'Media': [('iTunes','itunes'),('VLC','vlc'),('AIMP','aimp'),('foobar2000','foobar'),('Winamp','winamp'),('MusicBee','musicbee'),('Audacity','audacity'),('K-Lite Codecs','klite'),('GOM',None),('Spotify','spotify'),('CCCP','cccp'),('MediaMonkey','mediamonkey'),('HandBrake','handbrake'),('OBS Studio','obs')],
 '.NET': [('.NET 4.8.1','netfw')] + [('.NET Desktop Runtime '+v,'netfw') for v in NET_V] + [('ASP.NET Core Runtime '+v,'netfw') for v in NET_V],
 'Java': [('Java (AdoptOpenJDK) '+v,'java') for v in JAVA_V] + [('JDK (AdoptOpenJDK) '+v,'java') for v in JAVA_V] + [('JDK (Amazon Corretto) '+v,'java') for v in JAVA_V] + [('JRE (Amazon Corretto) x64 8','java'),('JRE (Amazon Corretto) 8','java')],
 'Imaging': [('Krita','krita'),('Blender','blender'),('Paint.NET','paintnet'),('GIMP','gimp'),('IrfanView','irfanview'),('XnView','xnview'),('Inkscape','inkscape'),('FastStone','faststone'),('Greenshot','greenshot'),('ShareX','sharex')],
 'Documents': [('Foxit Reader','foxit'),('LibreOffice','libreoffice'),('SumatraPDF','sumatrapdf'),('CutePDF','cutepdf'),('OpenOffice','openoffice')],
 'Security': [('Malwarebytes','malwarebytes'),('Avast','avast'),('AVG','avg'),('Spybot 2','spybot'),('Avira','avira'),('SUPERAntiSpyware','superantispyware')],
 'Compression': [('7-Zip','7zip'),('PeaZip','peazip'),('WinRAR','winrar')],
 'File Sharing': [('qBittorrent','qbittorrent')],
 'Other': [('Evernote','evernote'),('Google Earth','googleearth'),('Steam','steam'),('Epic Games Launcher','epic'),('KeePass 2','keepass'),('Everything','everything'),('NV Access','nvaccess')],
 'Online Storage': [('Dropbox','dropbox'),('Google Drive for Desktop','googledrive'),('OneDrive','onedrive'),('SugarSync',None)],
 'VC++ Redistributables': [('VC Redist '+v,'visualcpp') for v in VC_V],
 'Developer Tools': [('Python x64 3','python'),('Python arm64 3','python'),('Python 3','python'),('Git','git'),('FileZilla','filezilla'),('Notepad++','notepadpp'),('WinSCP','winscp'),('PuTTY','putty'),('WinMerge','winmerge'),('Eclipse','eclipse'),('Visual Studio Code','vscode'),('Cursor','cursor'),('GitHub Desktop','github'),('Node.js','nodejs'),('Sublime Text','sublimetext')],
 'Utilities': [('AnyDesk','anydesk'),('TeamViewer 15','teamviewer'),('ImgBurn',None),('RealVNC Server',None),('RealVNC Viewer',None),('TightVNC','tightvnc'),('TeraCopy','teracopy'),('CDBurnerXP','cdburnerxp'),('Revo Uninstaller','revo'),('Launchy',None),('WinDirStat','windirstat'),('WizTree','wiztree'),('Glary','glary'),('InfraRecorder',None),('Open-Shell','openshell'),('CCleaner','ccleaner'),('Rufus','rufus')],
}


def norm(t):
    """Loose title key so 'VLC' matches 'VLC Media Player'."""
    return re.sub(r'[^a-z0-9]', '', t.lower())



# ---------------------------------------------------------------------------
# Publishers with a stable endpoint. Verified with --check on 2026-08-22.
# (32-bit, 64-bit); a lone value means the publisher ships one build for both.
# ---------------------------------------------------------------------------
VENDOR = {
    # ── Stable publisher endpoints that replaced links inherited from the
    # upstream feed. Each was fetched and confirmed to serve a PE or MSI.
    'Chrome':             'https://dl.google.com/chrome/install/latest/chrome_installer.exe',
    'Firefox':            'https://download.mozilla.org/?product=firefox-latest-ssl&os=win64&lang=en-US',
    'Thunderbird':        'https://download.mozilla.org/?product=thunderbird-latest-ssl&os=win64&lang=en-US',
    'Brave':              'https://laptop-updates.brave.com/latest/winx64',
    'Zoom':               'https://zoom.us/client/latest/ZoomInstallerFull.exe?archType=x64',
    'Discord':            'https://discord.com/api/download?platform=win',
    'Telegram':           'https://telegram.org/dl/desktop/win64',
    'Spotify':            'https://download.scdn.co/SpotifySetup.exe',
    'Steam':              'https://cdn.cloudflare.steamstatic.com/client/installer/SteamSetup.exe',
    'AnyDesk':            'https://download.anydesk.com/AnyDesk.exe',
    'TeamViewer 15':      'https://download.teamviewer.com/download/TeamViewer_Setup_x64.exe',
    'Revo Uninstaller':   'https://download.revouninstaller.com/download/revosetup.exe',
    'Malwarebytes':       'https://data-cdn.mbamupdates.com/web/mb4-setup-consumer/MBSetup.exe',
    'OneDrive':           'https://go.microsoft.com/fwlink/p/?LinkID=2182910',
    'Epic Games Launcher':'https://launcher-public-service-prod06.ol.epicgames.com/launcher/api/installer/download/EpicGamesLauncherInstaller.msi',
    'Everything':         'https://www.voidtools.com/Everything-1.4.1.1026.x64-Setup.exe',
    'iTunes':             'https://www.apple.com/itunes/download/win64',
    'Foxit Reader':       'https://www.foxit.com/downloads/latest.html?product=Foxit-Reader&platform=Windows',
    'WinRAR':             'https://www.win-rar.com/fileadmin/winrar-versions/winrar/winrar-x64-711.exe',
    'Evernote':           'https://win.desktop.evernote.com/builds/Evernote-latest.exe',
    # Messaging
    'Pidgin':                   'https://downloads.sourceforge.net/project/pidgin/Pidgin/2.14.14/pidgin-2.14.14-offline.exe',
    # Media
    'MediaMonkey':              'https://www.mediamonkey.com/MediaMonkey-2024_Setup.exe',
    # Imaging
    'XnView':                   'https://download.xnview.com/XnView-win-full.exe',
    'FastStone':                'https://www.faststone.org/DN/FSViewerSetup85.exe',
    # Documents
    'CutePDF':                  'https://www.cutepdf.com/download/CuteWriter.exe',
    'OpenOffice':               'https://downloads.sourceforge.net/project/openofficeorg.mirror/4.1.15/binaries/en-US/Apache_OpenOffice_4.1.15_Win_x86_install_en-US.exe',
    # Security
    'Avast':                    'https://bits.avcdn.net/productfamily_ANTIVIRUS/insttype_FREE/platform_WIN/installertype_ONLINE/build_RELEASE/cookie_mmm_avn_998_999_a1t_04/avast_free_antivirus_setup_online.exe',
    'AVG':                      'https://bits.avcdn.net/productfamily_ANTIVIRUS/insttype_FREE/platform_WIN_AVG/installertype_ONLINE/build_RELEASE/avg_antivirus_free_setup.exe',
    'Spybot 2':                 'https://updates2.safer-networking.org/spybot1/spybotsd-2.9.85.5.exe',
    'Avira':                    'https://package.avira.com/package/oeavira/win/int/avira_en_av.exe',
    'SUPERAntiSpyware':         'https://secure.superantispyware.com/SUPERAntiSpyware.exe',
    # Other
    'Google Earth':             'https://dl.google.com/earth/client/advanced/current/GoogleEarthProWin-x64.exe',
    'KeePass 2':                'https://downloads.sourceforge.net/keepass/KeePass-2.59-Setup.exe',
    # Online Storage
    'Dropbox':                  'https://www.dropbox.com/download?plat=win',
    'Google Drive for Desktop': 'https://dl.google.com/drive-file-stream/GoogleDriveSetup.exe',
    # Utilities
    'ImgBurn':                  'https://download.imgburn.com/SetupImgBurn_2.5.8.0.exe',
    'TightVNC':                 ('https://www.tightvnc.com/download/2.8.85/tightvnc-2.8.85-gpl-setup-32bit.msi',
                                 'https://www.tightvnc.com/download/2.8.85/tightvnc-2.8.85-gpl-setup-64bit.msi'),
    'TeraCopy':                 'https://codesector.com/files/teracopy.exe',
    'Launchy':                  'https://www.launchy.net/downloads/win/Launchy2.5.exe',
    'Glary':                    'https://download.glarysoft.com/gu5setup.exe',
    'InfraRecorder':            'https://downloads.sourceforge.net/project/infrarecorder/InfraRecorder/0.53/ir053.exe',
    'CCleaner':                 'https://download.ccleaner.com/ccsetup.exe',
    # Developer Tools
    'NV Access':                'https://www.nvaccess.org/files/nvda/releases/2026.1.1/nvda_2026.1.1.exe',

    # Publishers whose upstream-feed link has since rotted. Everything below
    # replaces a link inherited from Optimizer that now 404s or serves HTML.
    # fwlink 2243204 is teamsbootstrapper.exe; the older Teams fwlinks now
    # answer with an .msix or a support article, which the app would run as
    # though it were an installer.
    'Teams':                    'https://go.microsoft.com/fwlink/?linkid=2243204',
    'Visual Studio Code':       'https://code.visualstudio.com/sha/download?build=stable&os=win32-x64-user',
    'GitHub Desktop':           'https://central.github.com/deployments/desktop/desktop/latest/win32',
    'WinSCP':                   'https://downloads.sourceforge.net/project/winscp/WinSCP/6.5.6/WinSCP-6.5.6-Setup.exe',
    'SumatraPDF':               'https://www.sumatrapdfreader.org/dl/rel/3.5.2/SumatraPDF-3.5.2-64-install.exe',
    'qBittorrent':              'https://downloads.sourceforge.net/project/qbittorrent/qbittorrent-win32/qbittorrent-5.1.2/qbittorrent_5.1.2_x64_setup.exe',
}

# Publishers with no link we would trust. Listed so `--check` can tell a
# deliberate blank from a regression, and so the reason survives in the repo.
NO_LINK = {
    'Vivaldi':       'the stable listing answers a direct request with 403',
    'Sublime Text':  'the download endpoint is a template with the build number substituted in',
    'Eclipse':       'the installer is published only through a mirror-redirect page',
    'K-Lite Codecs': 'codecguide.com serves its download page from script, with no file URL in the markup',
    'Winamp':        'no installer is published at a fixed URL since the 5.9 relaunch',
    'Trillian':      'downloads are gated behind a JS form; no stable file URL',
    'MusicBee':      'distributed through mega.nz and the Microsoft Store only',
    'GOM':           'only the Korean installer is published at a fixed URL',
    'CCCP':          'cccp-project.net has been unreachable since the project ended',
    'Paint.NET':     'ships .zip archives only; the app runs what it downloads',
    'SugarSync':     'the published installer URLs 404',
    'RealVNC Viewer':'no file exists under the viewer.files path RealVNC documents',
    'CDBurnerXP':    'discontinued; the download host no longer serves a valid certificate',
    'IrfanView':     'irfanview.info answers a direct request with a click-through page',
    'FileZilla':     'the download host answers any direct request with its home page',
}


def _get(url, timeout=30):
    req = urllib.request.Request(url, headers={'User-Agent': 'ConfigurO-feed'})
    with urllib.request.urlopen(req, timeout=timeout) as r:
        return r.read().decode('utf-8', 'replace')


def _github_latest(repo, rx32, rx64):
    """Newest release of `repo`, matched to one asset per architecture."""
    rel = json.loads(_get('https://api.github.com/repos/%s/releases/latest' % repo))
    names = {a['name']: a['browser_download_url'] for a in rel.get('assets', [])}

    def pick(rx):
        for name in sorted(names):
            if re.match(rx, name):
                return names[name]
        return None

    return (pick(rx32), pick(rx64))


def _head(url, timeout=20):
    req = urllib.request.Request(url, method='HEAD',
                                 headers={'User-Agent': 'ConfigurO-feed'})
    try:
        with urllib.request.urlopen(req, timeout=timeout):
            return True
    except Exception:
        return False


def _python_latest():
    """Newest 3.x on python.org's ftp listing. The listing includes directories
    for versions that only ever shipped pre-releases, so walk down from the top
    until one actually has an installer."""
    index = _get('https://www.python.org/ftp/python/')
    versions = sorted(set(re.findall(r'"(3\.\d+\.\d+)/"', index)),
                      key=lambda v: [int(n) for n in v.split('.')], reverse=True)
    for v in versions[:12]:
        base = 'https://www.python.org/ftp/python/%s/python-%s' % (v, v)
        if _head(base + '-amd64.exe'):
            return {'Python 3': (base + '.exe', base + '.exe'),
                    'Python x64 3': (base + '-amd64.exe', base + '-amd64.exe'),
                    'Python arm64 3': (base + '-arm64.exe', base + '-arm64.exe')}
    raise RuntimeError('no python.org version with a windows installer')


def _krita_latest():
    index = _get('https://download.kde.org/stable/krita/')
    versions = sorted(set(re.findall(r'"(\d+\.\d+\.\d+)/"', index)),
                      key=lambda v: [int(n) for n in v.split('.')])
    v = versions[-1]
    u = 'https://download.kde.org/stable/krita/%s/krita-x64-%s-setup.exe' % (v, v)
    return (u, u)


def _inkscape_latest():
    """inkscape.org's /dl/ page carries a meta-refresh to the real file."""
    for release in ('inkscape-1.4.4', 'inkscape-1.4.3', 'inkscape-1.4.2'):
        try:
            page = _get('https://inkscape.org/release/%s/windows/64-bit/exe/dl/' % release)
        except Exception:
            continue
        m = re.search(r'href="(/gallery/item/\d+/[^"]+\.exe)"', page)
        if m:
            u = 'https://inkscape.org' + m.group(1)
            return (u, u)
    return (None, None)


def _cursor_latest():
    api = 'https://www.cursor.com/api/download?platform=win32-x64-user&releaseTrack=stable'
    u = json.loads(_get(api)).get('downloadUrl')
    return (u, u)


def _aimp_latest():
    """AIMP serves builds through opaque ids. The page lists the stable release
    first and the beta below it, each as a 32-bit then a 64-bit row, so the id
    for an architecture is the last one appearing before that row's label."""
    page = _get('https://www.aimp.ru/?do=download&os=windows')
    ids = [(m.start(), m.group(1))
           for m in re.finditer(r'do=download\.file&(?:amp;)?id=(\d+)', page)]

    def nearest(label):
        at = page.find(label)
        before = [(pos, i) for pos, i in ids if pos < at]
        return max(before)[1] if at >= 0 and before else None

    fmt = 'https://www.aimp.ru/?do=download.file&id=%s'
    a, b = nearest('32-bit'), nearest('64-bit')
    return (fmt % a if a else None, fmt % b if b else None)


def _realvnc_server_latest():
    """RealVNC publishes no index; walk the documented path for the newest 7.x."""
    fmt = 'https://downloads.realvnc.com/download/file/vnc.files/VNC-Server-7.%d.0-Windows.exe'
    found = None
    for minor in range(10, 40):
        u = fmt % minor
        try:
            req = urllib.request.Request(u, method='HEAD',
                                         headers={'User-Agent': 'ConfigurO-feed'})
            with urllib.request.urlopen(req, timeout=20):
                found = u
        except Exception:
            pass
    return (found, found)


def _sevenzip_latest():
    """7-zip.org/download.html lists every release ever; take the highest."""
    page = _get('https://www.7-zip.org/download.html')
    builds = sorted(set(re.findall(r'"a/7z(\d+)(-x64)?\.exe"', page)),
                    key=lambda t: int(t[0]))
    v = builds[-1][0]
    return ('https://www.7-zip.org/a/7z%s.exe' % v,
            'https://www.7-zip.org/a/7z%s-x64.exe' % v)


def _putty_latest():
    page = _get('https://www.chiark.greenend.org.uk/~sgtatham/putty/latest.html')
    m = re.search(r'putty-(\d+\.\d+)-installer\.msi', page)
    v = m.group(1)
    base = 'https://the.earth.li/~sgtatham/putty/latest'
    return ('%s/w32/putty-%s-installer.msi' % (base, v),
            '%s/w64/putty-64bit-%s-installer.msi' % (base, v))


def _foobar_latest():
    page = _get('https://www.foobar2000.org/download')
    vs = sorted(set(re.findall(r'/downloads/foobar2000_v([\d.]+)\.exe', page)),
                key=lambda v: [int(n) for n in v.split('.')])
    v = vs[-1]
    return ('https://www.foobar2000.org/downloads/foobar2000_v%s.exe' % v,
            'https://www.foobar2000.org/downloads/foobar2000-x64_v%s.exe' % v)


def _edge_latest():
    """Edge publishes an enterprise release index; the consumer fwlink has
    started answering with a macOS .pkg."""
    products = json.loads(_get('https://edgeupdates.microsoft.com/api/products?view=enterprise'))
    stable = next(p for p in products if p.get('Product') == 'Stable')
    best = {}
    for rel in stable['Releases']:
        if rel.get('Platform') != 'Windows':
            continue
        arch = rel.get('Architecture')
        if arch not in ('x86', 'x64'):
            continue
        key = [int(n) for n in rel['ProductVersion'].split('.')]
        if arch not in best or key > best[arch][0]:
            best[arch] = (key, rel['Artifacts'][0]['Location'])
    return (best['x86'][1], best['x64'][1])


def _libreoffice_latest():
    index = _get('https://download.documentfoundation.org/libreoffice/stable/')
    versions = sorted(set(re.findall(r'"(\d+\.\d+\.\d+)/"', index)),
                      key=lambda v: [int(n) for n in v.split('.')])
    v = versions[-1]
    base = 'https://download.documentfoundation.org/libreoffice/stable/%s/win' % v
    return ('%s/x86/LibreOffice_%s_Win_x86.msi' % (base, v),
            '%s/x86_64/LibreOffice_%s_Win_x86-64.msi' % (base, v))


# title -> callable returning (link32, link64), or a dict of several titles.
def _dir_latest(index_url, file_rx, file_base=None):
    """Newest file matching `file_rx` in a directory listing. `file_base`
    defaults to `index_url`, and is separate for listings whose hrefs are
    absolute paths rather than bare names."""
    page = _get(index_url)
    names = sorted(set(re.findall(file_rx, page)))
    if not names:
        return ('', '')
    url = (file_base or index_url) + names[-1].rsplit('/', 1)[-1]
    return (url, url)


def _versioned_dir_latest(root, dir_rx, file_rx):
    """Two-hop listing: pick the highest-numbered directory under `root`, then
    the newest matching file inside it. Opera and Blender both publish this
    way, with one directory per release and no 'latest' alias."""
    def key(v):
        parts = [int(n) for n in re.findall(r'\d+', v)]
        return parts or [0]

    dirs = sorted(set(re.findall(dir_rx, _get(root))), key=key)
    for d in reversed(dirs[-5:]):          # newest first, a few back if empty
        files = sorted(set(re.findall(file_rx, _get(root + d + '/'))))
        if files:
            url = root + d + '/' + files[-1]
            return (url, url)
    return ('', '')


def _opera_latest():
    def key(v):
        return [int(n) for n in re.findall(r'\d+', v)] or [0]
    root = 'https://get.geo.opera.com/pub/opera/desktop/'
    dirs = sorted({v for v in re.findall(r'href="([\d.]+)/"', _get(root)) if key(v) != [0]}, key=key)
    for d in reversed(dirs[-5:]):
        files = re.findall(r'href="(Opera_[^"]*_Setup\.exe)"', _get(root + d + '/win/'))
        if files:
            url = root + d + '/win/' + files[0]
            return (url, url)
    return ('', '')


RESOLVERS = {
    # Entries below the divider replaced links inherited from the upstream feed.
    'Audacity':   lambda: _github_latest('audacity/audacity',
                                         r'audacity-win-[\d.]+-64bit\.exe$',
                                         r'audacity-win-[\d.]+-64bit\.exe$'),
    'OBS Studio': lambda: _github_latest('obsproject/obs-studio',
                                         r'OBS-Studio-[\d.]+-Windows-x64-Installer\.exe$',
                                         r'OBS-Studio-[\d.]+-Windows-x64-Installer\.exe$'),
    'ShareX':     lambda: _github_latest('ShareX/ShareX',
                                         r'ShareX-[\d.]+-setup-x64\.exe$',
                                         r'ShareX-[\d.]+-setup-x64\.exe$'),
    'PeaZip':     lambda: _github_latest('peazip/PeaZip',
                                         r'peazip-[\d.]+\.WIN64\.exe$',
                                         r'peazip-[\d.]+\.WIN64\.exe$'),
    'Notepad++':  lambda: _github_latest('notepad-plus-plus/notepad-plus-plus',
                                         r'npp\.[\d.]+\.Installer\.x64\.exe$',
                                         r'npp\.[\d.]+\.Installer\.x64\.exe$'),
    'Open-Shell': lambda: _github_latest('Open-Shell/Open-Shell-Menu',
                                         r'OpenShellSetup_[\d_]+\.exe$',
                                         r'OpenShellSetup_[\d_]+\.exe$'),
    'Rufus':      lambda: _github_latest('pbatard/rufus',
                                         r'rufus-[\d.]+\.exe$',
                                         r'rufus-[\d.]+\.exe$'),
    'VLC':        lambda: _dir_latest('https://get.videolan.org/vlc/last/win64/',
                                      r'href="(vlc-[\d.]+-win64\.exe)"'),
    'Node.js':    lambda: _dir_latest('https://nodejs.org/dist/latest/',
                                      r'href="([^"]*node-v[\d.]+-x64\.msi)"',
                                      'https://nodejs.org/dist/latest/'),
    'GIMP':       lambda: _dir_latest('https://download.gimp.org/gimp/v3.0/windows/',
                                      r'href="(gimp-[\d.]+-setup\.exe)"'),
    'Blender':    lambda: _versioned_dir_latest('https://download.blender.org/release/',
                                                r'href="(Blender[\d.]+)/"',
                                                r'href="(blender-[\d.]+-windows-x64\.msi)"'),
    'Opera':      _opera_latest,
    'HandBrake':  lambda: _github_latest('HandBrake/HandBrake',
                                         r'HandBrake-[\d.]+-x86_64-Win_GUI\.exe$',
                                         r'HandBrake-[\d.]+-x86_64-Win_GUI\.exe$'),
    'Greenshot':  lambda: _github_latest('greenshot/greenshot',
                                         r'Greenshot-INSTALLER-[\d.]+-RELEASE\.exe$',
                                         r'Greenshot-INSTALLER-[\d.]+-RELEASE\.exe$'),
    'WinMerge':   lambda: _github_latest('WinMerge/winmerge',
                                         r'WinMerge-[\d.]+-Setup\.exe$',
                                         r'WinMerge-[\d.]+-x64-Setup\.exe$'),
    'Git':        lambda: _github_latest('git-for-windows/git',
                                         r'Git-[\d.]+-64-bit\.exe$',
                                         r'Git-[\d.]+-64-bit\.exe$'),
    'WinDirStat': lambda: _github_latest('windirstat/windirstat',
                                         r'WinDirStat-x86\.msi$',
                                         r'WinDirStat-x64\.msi$'),
    'Krita':      _krita_latest,
    'Inkscape':   _inkscape_latest,
    'Cursor':     _cursor_latest,
    'AIMP':       _aimp_latest,
    'RealVNC Server': _realvnc_server_latest,
    '7-Zip':      _sevenzip_latest,
    'Edge':       _edge_latest,
    'PuTTY':      _putty_latest,
    'foobar2000': _foobar_latest,
    'LibreOffice': _libreoffice_latest,
    'WizTree':    lambda: (lambda u: (u, u))(
        'https://diskanalyzer.com/' + re.search(
            r'href="(files/wiztree_[\d_]+_setup\.exe)"',
            _get('https://diskanalyzer.com/download')).group(1)),
}


def vendor_links(title):
    """Documented vendor endpoints for the version-enumerated runtime families."""
    m = re.match(r'^\.NET Desktop Runtime (?:(x64|arm64) )?(\d+)$', title)
    if m:
        arch = m.group(1) or 'x86'
        return ('https://aka.ms/dotnet/%s.0/windowsdesktop-runtime-win-%s.exe' % (m.group(2), arch),
                'https://aka.ms/dotnet/%s.0/windowsdesktop-runtime-win-x64.exe' % m.group(2))
    m = re.match(r'^ASP\.NET Core Runtime (?:(x64|arm64) )?(\d+)$', title)
    if m:
        arch = m.group(1) or 'x86'
        return ('https://aka.ms/dotnet/%s.0/aspnetcore-runtime-win-%s.exe' % (m.group(2), arch),
                'https://aka.ms/dotnet/%s.0/aspnetcore-runtime-win-x64.exe' % m.group(2))
    if title == '.NET 4.8.1':
        u = 'https://go.microsoft.com/fwlink/?linkid=2203304'   # .NET Framework 4.8.1 web installer
        return (u, u)

    m = re.match(r'^VC Redist (x64|x86|arm64) 2015\+$', title)
    if m:
        a = {'x64': 'x64', 'x86': 'x86', 'arm64': 'arm64'}[m.group(1)]
        u = 'https://aka.ms/vs/17/release/vc_redist.%s.exe' % a
        return (u, u)

    m = re.match(r'^VC Redist (x64|x86) (2013|2012|2010|2008|2005)$', title)
    if m:
        u = VC_LEGACY[m.group(2)][m.group(1)]
        return (u, u)

    m = re.match(r'^(?:Java|JDK) \(AdoptOpenJDK\) (?:(x64) )?(\d+)$', title)
    if m:
        arch = 'x64' if m.group(1) else 'x86'
        kind = 'jdk' if title.startswith('JDK') else 'jre'
        u = ('https://api.adoptium.net/v3/installer/latest/%s/ga/windows/%s/%s/hotspot/normal/eclipse'
             % (m.group(2), arch, kind))
        return (u, u.replace('/%s/' % arch, '/x64/'))

    m = re.match(r'^(JDK|JRE) \(Amazon Corretto\) (?:(x64) )?(\d+)$', title)
    if m:
        arch = 'x64' if m.group(2) else 'x86'
        kind = m.group(1).lower()
        u = ('https://corretto.aws/downloads/latest/amazon-corretto-%s-%s-windows-%s.msi'
             % (m.group(3), arch, kind))
        return (u, u.replace('-%s-windows' % arch, '-x64-windows'))

    return (None, None)


def pack_icons(entries):
    """Rebuild feed/icons.zip from feed/icons/.

    The app downloads the zip once and reads tiles out of it, so it has to be
    regenerated whenever the icon folder changes or a new entry references a
    file that is only on disk. Flat layout -- AppFeed matches on entry name.
    """
    import zipfile

    folder = os.path.join(ROOT, 'feed', 'icons')
    names = sorted(f for f in os.listdir(folder) if f.lower().endswith('.png'))
    path = os.path.join(ROOT, 'feed', 'icons.zip')
    with zipfile.ZipFile(path, 'w', zipfile.ZIP_DEFLATED) as z:
        for n in names:
            z.write(os.path.join(folder, n), n)

    wanted = set(a['Image'].rsplit('/', 1)[-1] for a in entries if a['Image'])
    missing = sorted(wanted - set(names))
    print('  icons.zip: %d files, %d bytes' % (len(names), os.path.getsize(path)))
    if missing:
        print('  ! referenced but absent: ' + ', '.join(missing))
    return missing


def resolve(title, cache):
    """(link32, link64) from RESOLVERS then VENDOR. Resolvers are network
    calls, so each is run at most once and its failure is not fatal."""
    if title in RESOLVERS:
        if title not in cache:
            try:
                cache[title] = RESOLVERS[title]()
            except Exception as e:
                print('  ! %s: resolver failed (%s)' % (title, e))
                cache[title] = (None, None)
        a, b = cache[title]
        if a or b:
            return (a or b, b or a)

    v = VENDOR.get(title)
    if isinstance(v, tuple):
        return v
    if v:
        return (v, v)
    return (None, None)


def check(entries):
    """Probe every emitted link. The app names the downloaded file from the
    URL and then runs it, so anything that is not an installer is a defect."""
    seen, bad = {}, 0
    for a in entries:
        for url in (a['Link'], a['Link64']):
            if not url or url in seen:
                continue
            try:
                req = urllib.request.Request(
                    url, headers={'User-Agent': 'ConfigurO-feed', 'Range': 'bytes=0-1023'})
                with urllib.request.urlopen(req, timeout=45) as r:
                    ctype = r.headers.get('Content-Type', '')
                    disp = r.headers.get('Content-Disposition', '')
                    body = r.read(4)
                ok = body[:2] in (b'MZ', b'\xd0\xcf') and 'text/html' not in ctype
                seen[url] = (ok, '%s %s' % (ctype.split(';')[0], disp[:60]))
            except Exception as e:
                seen[url] = (False, str(e))
            if not seen[url][0]:
                bad += 1
                print('  BAD  %s\n       %s  %s' % (a['Title'], url, seen[url][1]))
    print('  checked %d links, %d bad' % (len(seen), bad))
    return bad


def main():
    do_check = '--check' in sys.argv[1:]

    cache = {}
    out, linked, unlinked = [], 0, []
    for group, items in CATALOG.items():
        for title, icon in items:
            link = link64 = ''
            if title not in NO_LINK:
                a, b = vendor_links(title)
                if not a:
                    a, b = resolve(title, cache)
                if a:
                    link, link64 = a, b
            if link or link64:
                linked += 1
            else:
                unlinked.append(title)
            out.append({
                'Title': title,
                'Group': group,
                'Image': ICON_BASE + icon + '.png' if icon else '',
                'Link': link,
                'Link64': link64 or link,
            })

    # python.org is one listing for three entries; resolve it once.
    try:
        for title, (a, b) in _python_latest().items():
            for e in out:
                if e['Title'] == title:
                    e['Link'], e['Link64'] = a, b
                    if title in unlinked:
                        unlinked.remove(title)
                        linked += 1
    except Exception as e:
        print('  ! Python: resolver failed (%s)' % e)

    path = os.path.join(ROOT, 'feed', 'feed.json')
    with open(path, 'w', encoding='utf-8') as f:
        json.dump(out, f, indent=2, ensure_ascii=False)
        f.write('\n')

    missing_icons = pack_icons(out)

    print('wrote %s' % path)
    print('  entries: %d   with links: %d   without: %d' % (len(out), linked, len(unlinked)))
    noicon = [a['Title'] for a in out if not a['Image']]
    if noicon:
        print('  no icon: %d  (%s)' % (len(noicon), ', '.join(noicon)))
    for title in unlinked:
        print('  no link: %-16s %s' % (title, NO_LINK.get(title, 'REGRESSION -- was linked')))

    if do_check:
        raise SystemExit(1 if (check(out) or missing_icons) else 0)


if __name__ == '__main__':
    main()
