#!/usr/bin/env python3
"""
Compile Remix Icon SVGs into a C# icon table for ConfigurO.

Remix line icons are 24x24 filled outlines (not strokes), so they render
perfectly as a GDI+ GraphicsPath with FillMode.Winding. This script normalises
every path command down to just three (M / L / C / Z) so the C# side needs
only a trivial parser -- no arc maths at runtime.

Usage: tools/svg_to_cs_icons.py <svg-dir> <out.cs>
"""
import os, re, sys, math

NUM = re.compile(r'[-+]?(?:\d*\.\d+|\d+\.?)(?:[eE][-+]?\d+)?')
CMD = re.compile(r'([MmZzLlHhVvCcSsQqTtAa])')


def tokenize(d):
    out, i = [], 0
    for part in CMD.split(d):
        part = part.strip()
        if not part:
            continue
        if len(part) == 1 and part in 'MmZzLlHhVvCcSsQqTtAa':
            out.append(part)
        else:
            out.extend(float(n) for n in NUM.findall(part))
    return out


def arc_to_cubics(x0, y0, rx, ry, phi, large, sweep, x, y):
    """Endpoint-parameterised elliptical arc -> list of cubic segments."""
    if rx == 0 or ry == 0 or (x0 == x and y0 == y):
        return [('L', x, y)]
    rx, ry = abs(rx), abs(ry)
    rad = math.radians(phi)
    cosr, sinr = math.cos(rad), math.sin(rad)
    dx2, dy2 = (x0 - x) / 2.0, (y0 - y) / 2.0
    x1 = cosr * dx2 + sinr * dy2
    y1 = -sinr * dx2 + cosr * dy2
    # scale radii up if they cannot span the endpoints
    lam = (x1 * x1) / (rx * rx) + (y1 * y1) / (ry * ry)
    if lam > 1:
        s = math.sqrt(lam)
        rx, ry = rx * s, ry * s
    num = rx * rx * ry * ry - rx * rx * y1 * y1 - ry * ry * x1 * x1
    den = rx * rx * y1 * y1 + ry * ry * x1 * x1
    co = math.sqrt(max(0.0, num / den)) if den else 0.0
    if large == sweep:
        co = -co
    cx1 = co * rx * y1 / ry
    cy1 = -co * ry * x1 / rx
    cx = cosr * cx1 - sinr * cy1 + (x0 + x) / 2.0
    cy = sinr * cx1 + cosr * cy1 + (y0 + y) / 2.0

    def ang(ux, uy, vx, vy):
        d = math.hypot(ux, uy) * math.hypot(vx, vy)
        if d == 0:
            return 0.0
        c = max(-1.0, min(1.0, (ux * vx + uy * vy) / d))
        a = math.acos(c)
        return -a if ux * vy - uy * vx < 0 else a

    th0 = ang(1, 0, (x1 - cx1) / rx, (y1 - cy1) / ry)
    dth = ang((x1 - cx1) / rx, (y1 - cy1) / ry, (-x1 - cx1) / rx, (-y1 - cy1) / ry)
    if not sweep and dth > 0:
        dth -= 2 * math.pi
    elif sweep and dth < 0:
        dth += 2 * math.pi

    segs = max(1, int(math.ceil(abs(dth) / (math.pi / 2))))
    out, step = [], dth / segs
    t = (4.0 / 3.0) * math.tan(step / 4.0)
    th = th0
    for _ in range(segs):
        c0, s0 = math.cos(th), math.sin(th)
        th1 = th + step
        c1, s1 = math.cos(th1), math.sin(th1)
        # unit-circle control points, then map through the ellipse
        def M(px, py):
            return (cosr * rx * px - sinr * ry * py + cx,
                    sinr * rx * px + cosr * ry * py + cy)
        p1 = M(c0 - t * s0, s0 + t * c0)
        p2 = M(c1 + t * s1, s1 - t * c1)
        p3 = M(c1, s1)
        out.append(('C', p1[0], p1[1], p2[0], p2[1], p3[0], p3[1]))
        th = th1
    return out


def normalize(d):
    """Reduce any SVG path to absolute M / L / C / Z commands."""
    t = tokenize(d)
    i = 0
    cur = prev = None          # current point, previous cubic control point
    start = None
    out = []
    cmd = None
    while i < len(t):
        if isinstance(t[i], str):
            cmd = t[i]
            i += 1
            if cmd in 'Zz':
                out.append(('Z',))
                cur = start
                prev = None
                continue
        if cmd is None:
            break
        rel = cmd.islower()
        c = cmd.upper()
        cx, cy = cur if cur else (0.0, 0.0)

        def take(n):
            nonlocal i
            v = t[i:i + n]
            i += n
            return v

        if c == 'M':
            x, y = take(2)
            if rel: x, y = cx + x, cy + y
            out.append(('M', x, y))
            cur = start = (x, y); prev = None
            cmd = 'l' if rel else 'L'      # subsequent pairs are implicit lineto
        elif c == 'L':
            x, y = take(2)
            if rel: x, y = cx + x, cy + y
            out.append(('L', x, y)); cur = (x, y); prev = None
        elif c == 'H':
            x = take(1)[0]
            if rel: x = cx + x
            out.append(('L', x, cy)); cur = (x, cy); prev = None
        elif c == 'V':
            y = take(1)[0]
            if rel: y = cy + y
            out.append(('L', cx, y)); cur = (cx, y); prev = None
        elif c == 'C':
            x1, y1, x2, y2, x, y = take(6)
            if rel:
                x1, y1, x2, y2, x, y = cx+x1, cy+y1, cx+x2, cy+y2, cx+x, cy+y
            out.append(('C', x1, y1, x2, y2, x, y)); cur = (x, y); prev = (x2, y2)
        elif c == 'S':
            x2, y2, x, y = take(4)
            if rel:
                x2, y2, x, y = cx+x2, cy+y2, cx+x, cy+y
            x1, y1 = (2*cx - prev[0], 2*cy - prev[1]) if prev else (cx, cy)
            out.append(('C', x1, y1, x2, y2, x, y)); cur = (x, y); prev = (x2, y2)
        elif c == 'Q':
            qx, qy, x, y = take(4)
            if rel:
                qx, qy, x, y = cx+qx, cy+qy, cx+x, cy+y
            out.append(('C', cx + 2.0/3*(qx-cx), cy + 2.0/3*(qy-cy),
                             x + 2.0/3*(qx-x),  y + 2.0/3*(qy-y), x, y))
            cur = (x, y); prev = ('q', qx, qy)
        elif c == 'T':
            x, y = take(2)
            if rel: x, y = cx+x, cy+y
            if prev and prev[0] == 'q':
                qx, qy = 2*cx - prev[1], 2*cy - prev[2]
            else:
                qx, qy = cx, cy
            out.append(('C', cx + 2.0/3*(qx-cx), cy + 2.0/3*(qy-cy),
                             x + 2.0/3*(qx-x),  y + 2.0/3*(qy-y), x, y))
            cur = (x, y); prev = ('q', qx, qy)
        elif c == 'A':
            rx, ry, rot, large, sweep, x, y = take(7)
            if rel: x, y = cx+x, cy+y
            out.extend(arc_to_cubics(cx, cy, rx, ry, rot, int(large), int(sweep), x, y))
            cur = (x, y); prev = None
        else:
            break
    return out


def fmt(v):
    s = f'{v:.2f}'.rstrip('0').rstrip('.')
    return '0' if s in ('', '-0') else s


def encode(cmds):
    parts = []
    for c in cmds:
        parts.append(c[0] if c[0] != 'Z' else 'Z')
        parts.extend(fmt(v) for v in c[1:])
    return ' '.join(parts)


def main():
    svg_dir, out_cs = sys.argv[1], sys.argv[2]
    rows = []
    for f in sorted(os.listdir(svg_dir)):
        if not f.endswith('.svg'):
            continue
        name = f[:-4]
        text = open(os.path.join(svg_dir, f), encoding='utf-8').read()
        ds = re.findall(r'<path[^>]*\bd="([^"]+)"', text)
        if not ds:
            print('!! no path in', f); continue
        vb = re.search(r'viewBox="([^"]+)"', text)
        if vb and vb.group(1).split() != ['0', '0', '24', '24']:
            print('!! unexpected viewBox in', f, vb.group(1))
        cmds = []
        for d in ds:
            cmds.extend(normalize(d))
        rows.append((name, encode(cmds)))

    with open(out_cs, 'w', encoding='utf-8') as fh:
        fh.write('''using System.Collections.Generic;

namespace ConfigurO
{
    /// <summary>
    /// Icon outlines, generated from the Remix Icon "line" set (Apache-2.0).
    ///
    /// GENERATED FILE -- do not hand-edit. Regenerate with:
    ///     tools/svg_to_cs_icons.py &lt;svg-dir&gt; src/ConfigurO/Nocturne/NocturneIconData.cs
    ///
    /// Every outline is expressed on a 24x24 grid using only three commands
    /// (M x y / L x y / C x1 y1 x2 y2 x y / Z) so <see cref="NocturneIcons"/>
    /// can turn it into a GraphicsPath without any curve maths at runtime.
    /// Winding direction is preserved, so counter-wound subpaths punch holes
    /// under FillMode.Winding exactly as they do in the source SVG.
    /// </summary>
    internal static class NocturneIconData
    {
        internal const float Grid = 24f;

        internal static readonly Dictionary<string, string> Paths = new Dictionary<string, string>(''' + str(len(rows)) + ''')
        {
''')
        for name, data in rows:
            fh.write(f'            {{ "{name}", "{data}" }},\n')
        fh.write('''        };
    }
}
''')
    print(f'wrote {out_cs}: {len(rows)} icons, '
          f'{os.path.getsize(out_cs)/1024:.0f} KB')


if __name__ == '__main__':
    main()
