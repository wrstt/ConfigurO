#!/usr/bin/env python3
"""
Generate feed/feed.json -- the ConfigurO app-downloader catalogue.

The design handoff names design_handoff/ConfigurO.dc.html's `appDefs` block as
the feed spec: 15 categories, ~130 entries. This script reproduces that
catalogue and fills in download links from three sources:

  1. the upstream Optimizer feed, matched on title (real, maintained links);
  2. documented vendor endpoints for the runtime families that `appDefs`
     enumerates by version (.NET via aka.ms, VC++ 2015+ via aka.ms,
     Adoptium via its v3 installer API, Corretto via corretto.aws/latest);
  3. nothing -- entries with no trustworthy link are emitted with empty
     Link/Link64 and the UI marks them unavailable rather than guessing.

Usage: tools/build_feed.py [upstream-feed.json]
"""
import json, os, re, sys, urllib.request

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
ICON_BASE = "https://raw.githubusercontent.com/wrstt/ConfigurO/main/feed/icons/"
UPSTREAM = "https://raw.githubusercontent.com/hellzerg/optimizer/master/feed.json"

NET_V = ['x64 8', 'arm64 8', '8', 'x64 9', 'arm64 9', '9', 'x64 10', 'arm64 10', '10']
JAVA_V = ['x64 8', '8', 'x64 11', 'x64 17', 'x64 21', 'x64 25']
VC_V = ['x64 2015+', 'x86 2015+', 'arm64 2015+', 'x64 2013', 'x86 2013', 'x64 2012',
        'x86 2012', 'x64 2010', 'x86 2010', 'x64 2008', 'x86 2008', 'x64 2005', 'x86 2005']

CATALOG = {
 'Web Browsers': [('Chrome','chrome'),('Opera','opera'),('Firefox','firefox'),('Edge','edge'),('Brave','brave'),('Vivaldi','vivaldi')],
 'Messaging': [('Zoom','zoom'),('Discord','discord'),('Teams','teams'),('Telegram','telegram'),('Pidgin',None),('Thunderbird','thunderbird'),('Trillian',None)],
 'Media': [('iTunes','itunes'),('VLC','vlc'),('AIMP',None),('foobar2000','foobar'),('Winamp','winamp'),('MusicBee',None),('Audacity','audacity'),('K-Lite Codecs','klite'),('GOM',None),('Spotify','spotify'),('CCCP',None),('MediaMonkey',None),('HandBrake',None),('OBS Studio','obs')],
 '.NET': [('.NET 4.8.1','netfw')] + [('.NET Desktop Runtime '+v,'netfw') for v in NET_V] + [('ASP.NET Core Runtime '+v,'netfw') for v in NET_V],
 'Java': [('Java (AdoptOpenJDK) '+v,'java') for v in JAVA_V] + [('JDK (AdoptOpenJDK) '+v,'java') for v in JAVA_V] + [('JDK (Amazon Corretto) '+v,'java') for v in JAVA_V] + [('JRE (Amazon Corretto) x64 8','java'),('JRE (Amazon Corretto) 8','java')],
 'Imaging': [('Krita',None),('Blender','blender'),('Paint.NET',None),('GIMP','gimp'),('IrfanView','irfanview'),('XnView',None),('Inkscape',None),('FastStone',None),('Greenshot',None),('ShareX','sharex')],
 'Documents': [('Foxit Reader','foxit'),('LibreOffice','libreoffice'),('SumatraPDF','sumatrapdf'),('CutePDF',None),('OpenOffice',None)],
 'Security': [('Malwarebytes','malwarebytes'),('Avast',None),('AVG',None),('Spybot 2',None),('Avira',None),('SUPERAntiSpyware',None)],
 'Compression': [('7-Zip','7zip'),('PeaZip','peazip'),('WinRAR','winrar')],
 'File Sharing': [('qBittorrent','qbittorrent')],
 'Other': [('Evernote','evernote'),('Google Earth',None),('Steam','steam'),('Epic Games Launcher','epic'),('KeePass 2',None),('Everything','everything'),('NV Access',None)],
 'Online Storage': [('Dropbox','dropbox'),('Google Drive for Desktop',None),('OneDrive','onedrive'),('SugarSync',None)],
 'VC++ Redistributables': [('VC Redist '+v,'visualcpp') for v in VC_V],
 'Developer Tools': [('Python x64 3','python'),('Python arm64 3','python'),('Python 3','python'),('Git',None),('FileZilla','filezilla'),('Notepad++','notepadpp'),('WinSCP','winscp'),('PuTTY','putty'),('WinMerge',None),('Eclipse','eclipse'),('Visual Studio Code','vscode'),('Cursor',None),('GitHub Desktop','github'),('Node.js','nodejs'),('Sublime Text','sublimetext')],
 'Utilities': [('AnyDesk','anydesk'),('TeamViewer 15','teamviewer'),('ImgBurn',None),('RealVNC Server',None),('RealVNC Viewer',None),('TightVNC',None),('TeraCopy',None),('CDBurnerXP',None),('Revo Uninstaller','revo'),('Launchy',None),('WinDirStat',None),('WizTree',None),('Glary',None),('InfraRecorder',None),('Open-Shell','openshell'),('CCleaner',None),('Rufus','rufus')],
}


def norm(t):
    """Loose title key so 'VLC' matches 'VLC Media Player'."""
    return re.sub(r'[^a-z0-9]', '', t.lower())


# The upstream feed uses full product names where appDefs uses short ones.
ALIASES = {
    'Chrome': 'Google Chrome',
    'Firefox': 'Mozilla Firefox',
    'Edge': 'Microsoft Edge',
    'Teams': 'Microsoft Teams',
    'Thunderbird': 'Mozilla Thunderbird',
    'Zoom': 'Google Zoom',
    'iTunes': 'Apple iTunes',
    'VLC': 'VLC Media Player',
    'foobar2000': 'Foobar2000',
    'K-Lite Codecs': 'K-Lite Codec Pack',
    'Epic Games Launcher': 'Epic Games',
    'Visual Studio Code': 'VS Code',
    'GitHub Desktop': 'GitHub',
    'TeamViewer 15': 'TeamViewer',
    'Python 3': 'Python 3',
    'Node.js': 'NodeJS',
    'Open-Shell': 'OpenShell',
    'qBittorrent': 'qBitTorrent',
    '7-Zip': '7-zip',
    'PuTTY': 'Putty',
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


def main():
    src = sys.argv[1] if len(sys.argv) > 1 else None
    if src:
        upstream = json.load(open(src))
    else:
        with urllib.request.urlopen(UPSTREAM, timeout=60) as r:
            upstream = json.loads(r.read().decode('utf-8'))
    by_title = {norm(a['Title']): a for a in upstream}

    out, linked, unlinked = [], 0, []
    for group, items in CATALOG.items():
        for title, icon in items:
            up = by_title.get(norm(ALIASES.get(title, title)))
            link = link64 = ''
            tag = ''
            if up:
                link, link64, tag = up.get('Link', ''), up.get('Link64', ''), up.get('Tag', '')
            else:
                a, b = vendor_links(title)
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
                'Tag': tag,
            })

    path = os.path.join(ROOT, 'feed', 'feed.json')
    with open(path, 'w', encoding='utf-8') as f:
        json.dump(out, f, indent=2, ensure_ascii=False)
        f.write('\n')

    print('wrote %s' % path)
    print('  entries: %d   with links: %d   without: %d' % (len(out), linked, len(unlinked)))
    if unlinked:
        print('  no link yet: ' + ', '.join(unlinked))


if __name__ == '__main__':
    main()
