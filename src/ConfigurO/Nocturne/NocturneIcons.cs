using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;

namespace ConfigurO
{
    /// <summary>
    /// Renders the Remix Icon outlines compiled into <see cref="NocturneIconData"/>.
    ///
    /// The outlines are filled paths on a 24x24 grid (not strokes), so they scale
    /// to any DPI without hinting artefacts and take their colour from the caller.
    /// Scaled paths are cached per (name, size) because the sidebar, tables and
    /// card grids redraw them constantly.
    /// </summary>
    internal static class NocturneIcons
    {
        // ── Names used by the UI. Kept as constants so a typo is a build error. ──
        internal const string Tweaks     = "equalizer-3-line";
        internal const string Cleaner    = "brush-2-line";
        internal const string Startup    = "rocket-line";
        internal const string Hosts      = "file-list-3-line";
        internal const string Apps       = "download-cloud-2-line";
        internal const string Network    = "wifi-line";
        internal const string Uwp        = "apps-2-line";
        internal const string Hardware   = "cpu-line";
        internal const string Integrator = "plug-line";
        internal const string Settings   = "settings-3-line";

        internal const string Sun = "sun-line", Moon = "moon-line";
        internal const string Minimize = "subtract-line", Maximize = "checkbox-blank-line";
        internal const string Restore = "checkbox-multiple-blank-line", Close = "close-line";

        internal const string Search = "search-line", Refresh = "refresh-line", Add = "add-line";
        internal const string Trash = "delete-bin-line", Check = "check-line", CheckCircle = "checkbox-circle-line";
        internal const string Lock = "lock-line", Unlock = "lock-unlock-line", Save = "save-line";
        internal const string Copy = "file-copy-line", ExternalLink = "external-link-line";
        internal const string Info = "information-line", Warning = "error-warning-line";
        internal const string Folder = "folder-line", Terminal = "terminal-line", Restart = "restart-line";
        internal const string Shield = "shield-check-line", Caret = "arrow-down-s-line";
        internal const string Cursor = "cursor-line", Upload = "upload-line", Download = "download-line";
        internal const string History = "history-line", Clock = "time-line", Filter = "filter-line";
        internal const string Eye = "eye-line", Bug = "bug-line", Question = "question-line";
        internal const string Key = "key-2-line", Windows = "windows-line", Edit = "edit-box-line";
        internal const string More = "more-2-line", Play = "play-line";

        static readonly Dictionary<string, GraphicsPath> _cache = new Dictionary<string, GraphicsPath>();
        static readonly object _gate = new object();

        internal static bool Exists(string name)
        {
            return name != null && NocturneIconData.Paths.ContainsKey(name);
        }

        /// <summary>
        /// A cached path for <paramref name="name"/> scaled to a
        /// <paramref name="size"/>-pixel box. Never returns null; an unknown
        /// name yields an empty path so a missing glyph degrades to blank
        /// rather than throwing mid-paint.
        /// </summary>
        internal static GraphicsPath Path(string name, int size)
        {
            string key = name + "@" + size;
            lock (_gate)
            {
                GraphicsPath cached;
                if (_cache.TryGetValue(key, out cached)) return cached;

                GraphicsPath p = new GraphicsPath(FillMode.Winding);
                string data;
                if (name != null && NocturneIconData.Paths.TryGetValue(name, out data))
                    Build(p, data, size / NocturneIconData.Grid);

                _cache[key] = p;
                return p;
            }
        }

        static void Build(GraphicsPath p, string data, float scale)
        {
            string[] t = data.Split(' ');
            PointF cur = PointF.Empty, start = PointF.Empty;
            bool open = false;
            int i = 0;
            while (i < t.Length)
            {
                string c = t[i++];
                switch (c)
                {
                    case "M":
                        if (open) p.CloseFigure();
                        cur = start = new PointF(F(t[i++]) * scale, F(t[i++]) * scale);
                        p.StartFigure();
                        open = true;
                        break;
                    case "L":
                    {
                        PointF to = new PointF(F(t[i++]) * scale, F(t[i++]) * scale);
                        if (to != cur) p.AddLine(cur, to);
                        cur = to;
                        break;
                    }
                    case "C":
                    {
                        PointF c1 = new PointF(F(t[i++]) * scale, F(t[i++]) * scale);
                        PointF c2 = new PointF(F(t[i++]) * scale, F(t[i++]) * scale);
                        PointF to = new PointF(F(t[i++]) * scale, F(t[i++]) * scale);
                        p.AddBezier(cur, c1, c2, to);
                        cur = to;
                        break;
                    }
                    case "Z":
                        if (open) p.CloseFigure();
                        open = false;
                        cur = start;
                        break;
                }
            }
            if (open) p.CloseFigure();
        }

        static float F(string s)
        {
            return float.Parse(s, System.Globalization.CultureInfo.InvariantCulture);
        }

        /// <summary>Draws an icon with its top-left at <paramref name="x"/>,<paramref name="y"/>.</summary>
        internal static void Draw(Graphics g, string name, int x, int y, int size, Color color)
        {
            GraphicsPath p = Path(name, size);
            if (p.PointCount == 0) return;

            SmoothingMode old = g.SmoothingMode;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            GraphicsState st = g.Save();
            g.TranslateTransform(x, y);
            using (SolidBrush b = new SolidBrush(color)) g.FillPath(b, p);
            g.Restore(st);
            g.SmoothingMode = old;
        }

        /// <summary>Draws an icon centred inside <paramref name="bounds"/>.</summary>
        internal static void DrawCentered(Graphics g, string name, Rectangle bounds, int size, Color color)
        {
            Draw(g, name,
                 bounds.X + (bounds.Width - size) / 2,
                 bounds.Y + (bounds.Height - size) / 2,
                 size, color);
        }
    }
}
