using System;
using System.Drawing;
using System.Drawing.Drawing2D;

namespace ConfigurO
{
    /// <summary>
    /// Nocturne -- the ConfigurO design tokens.
    ///
    /// Single source of truth for colour, geometry and type. Nothing in the UI
    /// layer may hard-code a hex literal; everything routes through the
    /// mode-aware accessors below so the Dark/Light switch is a one-liner.
    /// </summary>
    public static class NocturneTheme
    {
        public enum Mode { Dark, Light }

        static Mode _current = Mode.Dark;

        /// <summary>Raised after <see cref="Current"/> changes so live controls can repaint.</summary>
        internal static event EventHandler Changed;

        internal static Mode Current
        {
            get { return _current; }
            set
            {
                if (_current == value) return;
                _current = value;
                EventHandler h = Changed;
                if (h != null) h(null, EventArgs.Empty);
            }
        }

        internal static bool IsDark { get { return _current == Mode.Dark; } }

        internal static void Toggle()
        {
            Current = IsDark ? Mode.Light : Mode.Dark;
        }

        // ── Palette ─────────────────────────────────────────────────────
        // The ramp is fixed; only the *roles* below flip between modes.
        internal static readonly Color DarkBg      = FromHex("#161826");
        internal static readonly Color DarkSurface = FromHex("#232532");
        internal static readonly Color DarkText    = FromHex("#e9e9ed");

        internal static readonly Color Accent      = FromHex("#9184d9"); // outlines, active edge, toggle-on border, focus
        internal static readonly Color Accent200   = FromHex("#e7e5fe"); // active nav text
        internal static readonly Color Accent300   = FromHex("#d2cefd"); // accent-tinted text/icons
        internal static readonly Color Accent600   = FromHex("#796cbf"); // accent text on a light ground
        internal static readonly Color Accent700   = FromHex("#5d5294"); // toggle-on fill, selected-card border
        internal static readonly Color Accent800   = FromHex("#423a6a"); // accent tag background

        internal static readonly Color Neutral100  = FromHex("#f3f5fe");
        internal static readonly Color Neutral300  = FromHex("#cfd3e5");
        internal static readonly Color Neutral400  = FromHex("#b2b6ca");
        internal static readonly Color Neutral500  = FromHex("#9397ab");
        internal static readonly Color Neutral600  = FromHex("#75798c");
        internal static readonly Color Neutral700  = FromHex("#595d6c");
        internal static readonly Color Neutral800  = FromHex("#3f424d");
        internal static readonly Color Neutral900  = FromHex("#292b31");

        // ── Role accessors ──────────────────────────────────────────────
        // Light mode inverts the neutral scale by mixing neutral-900 into
        // neutral-100 at the ratios given in the handoff README.
        internal static Color Bg           { get { return IsDark ? DarkBg      : Neutral100; } }
        internal static Color Surface      { get { return IsDark ? DarkSurface : Mix(Neutral900, Neutral100, 0.04); } }
        internal static Color SurfaceAlt   { get { return IsDark ? Mix(Neutral800, DarkSurface, 0.35) : Mix(Neutral900, Neutral100, 0.07); } }
        internal static Color Text         { get { return IsDark ? DarkText    : Neutral900; } }
        internal static Color TextMuted    { get { return IsDark ? Neutral400  : Mix(Neutral900, Neutral100, 0.62); } }
        internal static Color TextFaint    { get { return IsDark ? Neutral500  : Mix(Neutral900, Neutral100, 0.50); } }
        internal static Color TextDim      { get { return IsDark ? Neutral600  : Mix(Neutral900, Neutral100, 0.36); } }
        internal static Color SidebarText  { get { return IsDark ? Neutral300  : Mix(Neutral900, Neutral100, 0.76); } }
        internal static Color Border       { get { return IsDark ? Neutral800  : Mix(Neutral900, Neutral100, 0.11); } }
        internal static Color BorderStrong { get { return IsDark ? Neutral700  : Mix(Neutral900, Neutral100, 0.24); } }
        internal static Color Divider      { get { return IsDark ? Color.FromArgb(41, DarkText) : Mix(Neutral900, Neutral100, 0.14); } }
        internal static Color HoverFill    { get { return Border; } }
        internal static Color AccentText   { get { return IsDark ? Accent300 : Accent600; } }
        internal static Color AccentStrong { get { return IsDark ? Accent200 : Accent600; } }
        internal static Color ToggleOn     { get { return IsDark ? Accent700 : Mix(Accent, Neutral100, 0.32); } }
        internal static Color ToggleOff    { get { return IsDark ? Neutral800 : Mix(Neutral900, Neutral100, 0.11); } }
        internal static Color ToggleOffEdge{ get { return IsDark ? Neutral700 : Mix(Neutral900, Neutral100, 0.24); } }
        internal static Color ToggleKnob   { get { return Neutral100; } }
        internal static Color CheckMark    { get { return Neutral900; } }
        internal static Color TagBg        { get { return IsDark ? Accent800 : Mix(Accent, Neutral100, 0.22); } }
        internal static Color ScrollThumb  { get { return IsDark ? Neutral700 : Mix(Neutral900, Neutral100, 0.24); } }

        /// <summary>Backdrop behind the window and the console/log panels -- always dark.</summary>
        internal static Color Backdrop     { get { return Neutral900; } }
        internal static Color ConsoleText  { get { return Neutral300; } }

        // Accent tints, expressed against whatever the current ground is.
        internal static Color SelectedFill  { get { return Mix(Accent, Bg, 0.06); } }
        internal static Color SelectedFillOnSurface { get { return Mix(Accent, Surface, 0.06); } }
        internal static Color ActiveNavFill { get { return Mix(Accent, Bg, 0.12); } }
        internal static Color HoverAccent   { get { return Mix(Accent, Bg, 0.12); } }
        internal static Color PressedAccent { get { return Mix(Accent, Bg, 0.22); } }

        // ── Geometry ────────────────────────────────────────────────────
        // Compact 0.7x density scale: 2.8 / 5.6 / 8.4 / 11.2 / 16.8 / 22.4
        internal const int Space1 = 3;
        internal const int Space2 = 6;
        internal const int Space3 = 8;
        internal const int Space4 = 11;
        internal const int Space5 = 17;
        internal const int Space6 = 22;

        internal const int RadiusSm = 4;
        internal const int RadiusMd = 8;
        internal const int RadiusLg = 14;
        internal const int WindowRadius = 12;

        internal const int TitleBarHeight = 46;
        internal const int SidebarWidth = 208;
        internal const int ToggleWidth = 38, ToggleHeight = 21, ToggleKnobSize = 15;
        internal const int CheckboxSize = 18;
        internal const int IconButtonSize = 36;
        internal const int InputHeight = 36;

        internal static readonly Size WindowDefaultSize = new Size(1340, 860);
        internal static readonly Size WindowMinimumSize = new Size(1040, 660);

        // ── Helpers ─────────────────────────────────────────────────────
        internal static Color FromHex(string hex)
        {
            hex = hex.TrimStart('#');
            return Color.FromArgb(
                int.Parse(hex.Substring(0, 2), System.Globalization.NumberStyles.HexNumber),
                int.Parse(hex.Substring(2, 2), System.Globalization.NumberStyles.HexNumber),
                int.Parse(hex.Substring(4, 2), System.Globalization.NumberStyles.HexNumber));
        }

        /// <summary>Opaque linear mix of <paramref name="a"/> into <paramref name="b"/>.</summary>
        internal static Color Mix(Color a, Color b, double amountOfA)
        {
            if (amountOfA < 0) amountOfA = 0;
            if (amountOfA > 1) amountOfA = 1;
            return Color.FromArgb(
                (int)Math.Round(a.R * amountOfA + b.R * (1 - amountOfA)),
                (int)Math.Round(a.G * amountOfA + b.G * (1 - amountOfA)),
                (int)Math.Round(a.B * amountOfA + b.B * (1 - amountOfA)));
        }

        internal static Color Alpha(Color c, double a)
        {
            return Color.FromArgb((int)Math.Round(Math.Max(0, Math.Min(1, a)) * 255), c);
        }

        /// <summary>Rounded rectangle path. A radius of 0 yields a plain rectangle.</summary>
        internal static GraphicsPath RoundedRect(Rectangle r, int radius)
        {
            GraphicsPath p = new GraphicsPath();
            if (radius <= 0 || r.Width <= 0 || r.Height <= 0)
            {
                p.AddRectangle(r);
                return p;
            }
            int d = Math.Min(radius * 2, Math.Min(r.Width, r.Height));
            p.AddArc(r.X, r.Y, d, d, 180, 90);
            p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            p.CloseFigure();
            return p;
        }

        /// <summary>
        /// Sub-pixel flavour of <see cref="RoundedRect(Rectangle,int)"/>, for
        /// stroked paths that have to sit on a half-pixel centreline.
        /// </summary>
        internal static GraphicsPath RoundedRect(RectangleF r, float radius)
        {
            GraphicsPath p = new GraphicsPath();
            if (radius <= 0f || r.Width <= 0f || r.Height <= 0f)
            {
                p.AddRectangle(r);
                return p;
            }
            float d = Math.Min(radius * 2f, Math.Min(r.Width, r.Height));
            p.AddArc(r.X, r.Y, d, d, 180, 90);
            p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            p.CloseFigure();
            return p;
        }

        internal static void FillRounded(Graphics g, Rectangle r, int radius, Color fill)
        {
            using (GraphicsPath p = RoundedRect(r, radius))
            using (SolidBrush b = new SolidBrush(fill))
                g.FillPath(b, p);
        }

        /// <summary>
        /// A hairline whose outer edge lands on <paramref name="r"/>.
        ///
        /// The centreline is inset by half the pen width, in floating point,
        /// and that half pixel is the whole point. NocturneDraw.Prepare sets
        /// PixelOffsetMode.HighQuality, which puts sample points on pixel
        /// *corners* -- so a 1px pen run along an integer coordinate straddles
        /// two pixel rows and GDI+ resolves it by painting both at roughly
        /// two-thirds strength. Measured on Windows: a #3F424D line on #161826
        /// came back as two rows of #2B2D3A instead of one row of #3F424D.
        /// Every card, input, button, tag and table outline in the app is
        /// drawn through here, so the whole interface read as soft.
        ///
        /// The integer -1 inset this replaces is the same idea done in whole
        /// pixels, which lands the centreline on the boundary rather than in
        /// the middle of a pixel and so does not avoid the split. It is
        /// invisible under libgdiplus, where the UI was reviewed.
        ///
        /// The corner radius is pulled in by the same half pixel so the
        /// stroke's outer edge follows the fill beneath it.
        /// </summary>
        internal static void DrawRounded(Graphics g, Rectangle r, int radius, Color stroke, float width = 1f)
        {
            if (r.Width <= 0 || r.Height <= 0) return;

            float half = width / 2f;
            RectangleF rr = new RectangleF(
                r.X + half, r.Y + half,
                Math.Max(0f, r.Width - width), Math.Max(0f, r.Height - width));

            using (GraphicsPath p = RoundedRect(rr, Math.Max(0f, radius - half)))
            using (Pen pen = new Pen(stroke, width))
                g.DrawPath(pen, p);
        }

        /// <summary>
        /// A 1px rule that fades to transparent at both ends, per the handoff
        /// ("rules longer than ~96px fade over 48px each side").
        /// </summary>
        internal static void DrawFadedRule(Graphics g, int x, int y, int width, Color c)
        {
            if (width <= 0) return;
            if (width < 96)
            {
                using (Pen p = new Pen(c)) g.DrawLine(p, x, y, x + width, y);
                return;
            }
            float fade = Math.Min(48f, width / 3f) / width;
            using (LinearGradientBrush b = new LinearGradientBrush(
                       new Rectangle(x, y, width, 1), Color.Black, Color.Black, 0f))
            {
                ColorBlend cb = new ColorBlend(4);
                cb.Colors = new[] { Color.FromArgb(0, c), c, c, Color.FromArgb(0, c) };
                cb.Positions = new[] { 0f, fade, 1f - fade, 1f };
                b.InterpolationColors = cb;
                g.FillRectangle(b, x, y, width, 1);
            }
        }
    }
}
