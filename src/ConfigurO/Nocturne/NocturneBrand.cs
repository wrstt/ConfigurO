using System;
using System.Drawing;
using System.Drawing.Drawing2D;

namespace ConfigurO
{
    /// <summary>
    /// The ConfigurO mark, drawn as vectors so it stays crisp at every DPI and
    /// picks up the theme.
    ///
    /// Geometry is transcribed from assets/logo/configuro-glyph.svg (a 120-unit
    /// grid): an accent ring with a 2x2 cluster of rounded tiles rotated 11
    /// degrees inside it. <see cref="DrawOrbits"/> adds the two elliptical
    /// orbits from the full lockup for the larger About/splash sizes.
    /// </summary>
    internal static class NocturneBrand
    {
        // Source grid, and the glyph's own bounds within it (ring outer edge).
        const float Grid = 120f;
        const float Cx = 60f, Cy = 60f;
        const float RingR = 26f, RingW = 7f;
        const float Extent = (RingR + RingW / 2f) * 2f;   // 59 units, the trimmed box
        const float TileSize = 10.4f, TileR = 2f, TileRotation = 11f;
        const float TileA = 48.5f, TileB = 61.1f;

        /// <summary>
        /// Draws the glyph filling a <paramref name="size"/>-pixel box at
        /// <paramref name="x"/>,<paramref name="y"/>.
        /// </summary>
        internal static void Draw(Graphics g, int x, int y, int size, bool glow = true)
        {
            if (size <= 0) return;

            SmoothingMode oldSmooth = g.SmoothingMode;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            GraphicsState st = g.Save();

            // Map the trimmed glyph box onto the requested pixel box.
            float scale = size / Extent;
            g.TranslateTransform(x + size / 2f, y + size / 2f);
            g.ScaleTransform(scale, scale);
            g.TranslateTransform(-Cx, -Cy);

            if (glow) DrawGlow(g);
            DrawRing(g);
            DrawTiles(g);

            g.Restore(st);
            g.SmoothingMode = oldSmooth;
        }

        /// <summary>
        /// The full lockup: orbit ellipses behind the glyph. Needs roughly
        /// 2x the glyph's own box, so pass the outer bounds you want to fill.
        /// </summary>
        internal static void DrawFull(Graphics g, Rectangle bounds)
        {
            int size = Math.Min(bounds.Width, bounds.Height);
            if (size <= 0) return;

            SmoothingMode oldSmooth = g.SmoothingMode;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            GraphicsState st = g.Save();

            float scale = size / Grid;   // full grid this time -- orbits reach the edges
            g.TranslateTransform(bounds.X + bounds.Width / 2f, bounds.Y + bounds.Height / 2f);
            g.ScaleTransform(scale, scale);
            g.TranslateTransform(-Cx, -Cy);

            DrawOrbits(g);
            DrawGlow(g);
            DrawRing(g);
            DrawTiles(g);

            g.Restore(st);
            g.SmoothingMode = oldSmooth;
        }

        static void DrawOrbits(Graphics g)
        {
            DrawOrbit(g, -24f, 52f, 18.5f, NocturneTheme.Accent700, 1.4f);
            DrawOrbit(g, 28f, 44f, 14f, NocturneTheme.Accent800, 1.2f);
        }

        static void DrawOrbit(Graphics g, float angle, float rx, float ry, Color c, float width)
        {
            GraphicsState st = g.Save();
            g.TranslateTransform(Cx, Cy);
            g.RotateTransform(angle);
            using (Pen p = new Pen(c, width))
                g.DrawEllipse(p, -rx, -ry, rx * 2f, ry * 2f);
            g.Restore(st);
        }

        static void DrawGlow(Graphics g)
        {
            // Approximates the SVG's feGaussianBlur halo with a few soft rings.
            for (int i = 3; i >= 1; i--)
            {
                using (Pen p = new Pen(NocturneTheme.Alpha(NocturneTheme.Accent, 0.10 * i / 3.0), RingW + i * 3f))
                    g.DrawEllipse(p, Cx - RingR, Cy - RingR, RingR * 2f, RingR * 2f);
            }
        }

        static void DrawRing(Graphics g)
        {
            // The lockup uses a diagonal accent-300 -> accent -> accent-700 ramp.
            RectangleF box = new RectangleF(Cx - RingR - RingW, Cy - RingR - RingW,
                                            (RingR + RingW) * 2f, (RingR + RingW) * 2f);
            using (LinearGradientBrush lg = new LinearGradientBrush(
                       box, NocturneTheme.Accent300, NocturneTheme.Accent700, 45f))
            {
                ColorBlend cb = new ColorBlend(3);
                cb.Colors = new[] { NocturneTheme.Accent300, NocturneTheme.Accent, NocturneTheme.Accent700 };
                cb.Positions = new[] { 0f, 0.55f, 1f };
                lg.InterpolationColors = cb;
                using (Pen p = new Pen(lg, RingW))
                    g.DrawEllipse(p, Cx - RingR, Cy - RingR, RingR * 2f, RingR * 2f);
            }
        }

        static void DrawTiles(Graphics g)
        {
            // accent-200 is invisible on a light ground, so the brightest tile
            // drops to accent-600 in light mode.
            Color bright = NocturneTheme.IsDark ? NocturneTheme.Accent200 : NocturneTheme.Accent600;

            GraphicsState st = g.Save();
            g.TranslateTransform(Cx, Cy);
            g.RotateTransform(TileRotation);
            g.TranslateTransform(-Cx, -Cy);

            Tile(g, TileA, TileA, bright);
            Tile(g, TileB, TileA, NocturneTheme.Accent);
            Tile(g, TileA, TileB, NocturneTheme.Accent);
            Tile(g, TileB, TileB, NocturneTheme.Accent700);

            g.Restore(st);
        }

        static void Tile(Graphics g, float x, float y, Color c)
        {
            using (GraphicsPath p = RoundedF(x, y, TileSize, TileSize, TileR))
            using (SolidBrush b = new SolidBrush(c))
                g.FillPath(b, p);
        }

        static GraphicsPath RoundedF(float x, float y, float w, float h, float r)
        {
            GraphicsPath p = new GraphicsPath();
            float d = Math.Min(r * 2f, Math.Min(w, h));
            p.AddArc(x, y, d, d, 180, 90);
            p.AddArc(x + w - d, y, d, d, 270, 90);
            p.AddArc(x + w - d, y + h - d, d, d, 0, 90);
            p.AddArc(x, y + h - d, d, d, 90, 90);
            p.CloseFigure();
            return p;
        }
    }
}
