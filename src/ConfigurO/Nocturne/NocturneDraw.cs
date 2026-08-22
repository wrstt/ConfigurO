using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;

namespace ConfigurO
{
    /// <summary>
    /// Text and surface drawing helpers shared by every Nocturne control.
    ///
    /// Everything goes through GDI+ (<c>Graphics.DrawString</c>) rather than
    /// <c>TextRenderer</c>, because the bundled IBM Plex faces live in a
    /// PrivateFontCollection that GDI cannot see.
    /// </summary>
    internal static class NocturneDraw
    {
        internal static readonly StringFormat Left = Make(StringAlignment.Near, StringAlignment.Center);
        internal static readonly StringFormat Center = Make(StringAlignment.Center, StringAlignment.Center);
        internal static readonly StringFormat Right = Make(StringAlignment.Far, StringAlignment.Center);
        internal static readonly StringFormat TopLeft = Make(StringAlignment.Near, StringAlignment.Near);

        static StringFormat Make(StringAlignment h, StringAlignment v)
        {
            return new StringFormat(StringFormatFlags.NoWrap)
            {
                Alignment = h,
                LineAlignment = v,
                Trimming = StringTrimming.EllipsisCharacter,
                HotkeyPrefix = HotkeyPrefix.None
            };
        }

        /// <summary>
        /// A Graphics suitable for measuring text before any control has a
        /// window. Control.CreateGraphics needs a handle, and layout runs
        /// while screens are still being built, so measuring goes through a
        /// throwaway bitmap instead. Dispose what this returns.
        /// </summary>
        internal static Graphics CreateMeasureGraphics()
        {
            Graphics g = Graphics.FromImage(new Bitmap(1, 1));
            Prepare(g);
            return g;
        }

        /// <summary>Turns on the rendering modes every Nocturne surface expects.</summary>
        internal static void Prepare(Graphics g)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
        }

        /// <summary>
        /// Undoes the ampersand doubling the translation files carry.
        ///
        /// Those strings were written for WinForms Buttons, which treat "&amp;"
        /// as a mnemonic marker and so need "&amp;&amp;" to show one. Nothing here
        /// draws mnemonics, so the doubled form would appear literally.
        /// </summary>
        static string Unescape(string s)
        {
            return s.IndexOf("&&", StringComparison.Ordinal) >= 0 ? s.Replace("&&", "&") : s;
        }

        internal static void Text(Graphics g, string s, Font f, Color c, RectangleF r, StringFormat sf = null)
        {
            if (string.IsNullOrEmpty(s)) return;
            using (SolidBrush b = new SolidBrush(c))
                g.DrawString(Unescape(s), f, b, r, sf ?? Left);
        }

        internal static void Text(Graphics g, string s, Font f, Color c, float x, float y)
        {
            if (string.IsNullOrEmpty(s)) return;
            using (SolidBrush b = new SolidBrush(c))
                g.DrawString(Unescape(s), f, b, x, y);
        }

        internal static float Width(Graphics g, string s, Font f)
        {
            if (string.IsNullOrEmpty(s)) return 0f;
            return g.MeasureString(Unescape(s), f, int.MaxValue,
                                   StringFormat.GenericTypographic).Width + 2f;
        }

        internal static SizeF Measure(Graphics g, string s, Font f)
        {
            if (string.IsNullOrEmpty(s)) return SizeF.Empty;
            return g.MeasureString(s, f);
        }

        /// <summary>
        /// A section label: an 11px uppercase caption preceded by a solid
        /// 14x2px accent dash, per the handoff type ramp.
        /// </summary>
        internal static void SectionLabel(Graphics g, string text, Font f, int x, int y, int height)
        {
            int dashW = NocturneScale.S(14), dashH = Math.Max(2, NocturneScale.S(2));
            using (SolidBrush b = new SolidBrush(NocturneTheme.Accent))
                g.FillRectangle(b, x, y + (height - dashH) / 2, dashW, dashH);

            Text(g, (text ?? string.Empty).ToUpperInvariant(), f, NocturneTheme.TextMuted,
                 new RectangleF(x + dashW + NocturneScale.S(8), y, 600, height), Left);
        }

        /// <summary>Card chrome: optional fill plus a 1px hairline at radius 8.</summary>
        internal static void Card(Graphics g, Rectangle r, Color fill, Color border, int radius)
        {
            if (fill != Color.Empty) NocturneTheme.FillRounded(g, r, radius, fill);
            if (border != Color.Empty) NocturneTheme.DrawRounded(g, r, radius, border);
        }

        /// <summary>A 2px accent focus ring, offset 2px outside the control.</summary>
        internal static void FocusRing(Graphics g, Rectangle r, int radius)
        {
            Rectangle ring = Rectangle.Inflate(r, NocturneScale.S(2), NocturneScale.S(2));
            using (GraphicsPath p = NocturneTheme.RoundedRect(
                       new Rectangle(ring.X, ring.Y, ring.Width - 1, ring.Height - 1),
                       radius + NocturneScale.S(2)))
            using (Pen pen = new Pen(NocturneTheme.Accent, NocturneScale.S(2)))
                g.DrawPath(pen, p);
        }
    }
}
