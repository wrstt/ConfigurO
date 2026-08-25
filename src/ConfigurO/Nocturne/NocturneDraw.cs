using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Globalization;
using System.Text;

namespace ConfigurO
{
    /// <summary>
    /// Text and surface drawing helpers shared by every Nocturne control.
    ///
    /// Everything goes through GDI+ (<c>Graphics.DrawString</c>) rather than
    /// <c>TextRenderer</c>, because the bundled Inter and Plex Mono faces live in a
    /// PrivateFontCollection that GDI cannot see.
    /// </summary>
    internal static class NocturneDraw
    {
        internal static readonly StringFormat Left = Make(StringAlignment.Near, StringAlignment.Center);
        internal static readonly StringFormat Center = Make(StringAlignment.Center, StringAlignment.Center);
        internal static readonly StringFormat Right = Make(StringAlignment.Far, StringAlignment.Center);
        internal static readonly StringFormat TopLeft = Make(StringAlignment.Near, StringAlignment.Near);

        /// <summary>
        /// Built on GenericTypographic, and that is load-bearing rather than a
        /// detail. Widths are measured with GenericTypographic (see
        /// <see cref="Width"/>); a plain `new StringFormat()` is GenericDefault,
        /// which reserves roughly a sixth of an em of side bearing that
        /// typographic measurement does not report. Drawing GenericDefault into
        /// a box measured GenericTypographic leaves the string a little wider
        /// than its box, and because these formats trim with an ellipsis, GDI+
        /// resolves that by eating characters -- a button sized to its own
        /// label still renders "Reinforce polic...". Measuring and drawing with
        /// the same metrics is what stops that.
        ///
        /// It cannot be caught on the Linux harness: libgdiplus returns
        /// identical widths for both generic formats, so the mismatch only
        /// exists on real GDI+.
        /// </summary>
        static StringFormat Make(StringAlignment h, StringAlignment v)
        {
            StringFormat f = new StringFormat(StringFormat.GenericTypographic);
            // Take the typographic metrics but not LineLimit, which
            // GenericTypographic sets and which drops any line taller than its
            // box instead of drawing it. Rows lay text into boxes a little
            // shorter than the line height on purpose, so leaving it on erases
            // every name and tip in the list.
            f.FormatFlags = (f.FormatFlags | StringFormatFlags.NoWrap)
                            & ~StringFormatFlags.LineLimit;
            f.Alignment = h;
            f.LineAlignment = v;
            f.Trimming = StringTrimming.EllipsisCharacter;
            f.HotkeyPrefix = HotkeyPrefix.None;
            return f;
        }

        /// <summary>
        /// A Graphics suitable for measuring text before any control has a
        /// window. Control.CreateGraphics needs a handle, and layout runs
        /// while screens are still being built, so measuring goes through a
        /// throwaway bitmap instead. Dispose what this returns.
        /// </summary>
        /// <summary>
        /// A scratch surface for measuring text, at the DPI the caller will
        /// actually draw at.
        ///
        /// This matters more than it looks. A Bitmap defaults to 96 DPI, and
        /// GDI+ converts a Font's point size to pixels using the Graphics DPI
        /// -- so measuring on a default bitmap while painting on a screen
        /// surface at 120 or 144 DPI understates every width by the scale
        /// factor. Buttons sized that way truncate their own labels, and text
        /// laid out beside a measured element overlaps it. At 100% the two
        /// agree, which is why it only shows up on a scaled display.
        /// </summary>
        internal static Graphics CreateMeasureGraphics()
        {
            float dpi = 96f * NocturneScale.Factor;
            Bitmap bmp = new Bitmap(1, 1);
            bmp.SetResolution(dpi, dpi);
            Graphics g = Graphics.FromImage(bmp);
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

        /// <summary>
        /// Collapses hard line breaks to single spaces.
        ///
        /// 23 of the tweak tips carry the newlines the legacy dialogs wanted.
        /// GDI+ honours those breaks even under <see cref="StringFormatFlags.NoWrap"/>,
        /// so a one-line row painted only the text up to the first break and
        /// silently dropped the rest -- which reads as a truncation bug rather
        /// than as source text, and without an ellipsis to admit it.
        /// </summary>
        static string Flatten(string s)
        {
            if (s.IndexOf('\n') < 0 && s.IndexOf('\r') < 0 && s.IndexOf('\t') < 0) return s;

            StringBuilder sb = new StringBuilder(s.Length);
            bool pendingSpace = false;
            for (int i = 0; i < s.Length; i++)
            {
                char ch = s[i];
                if (ch == '\n' || ch == '\r' || ch == '\t') { pendingSpace = true; continue; }
                if (pendingSpace)
                {
                    if (sb.Length > 0 && sb[sb.Length - 1] != ' ') sb.Append(' ');
                    pendingSpace = false;
                }
                sb.Append(ch);
            }
            return sb.ToString();
        }

        /// <summary>Unescaped, and flattened when the caller asked for one line.</summary>
        static string Prepare(string s, StringFormat sf)
        {
            s = Unescape(s);
            return (sf.FormatFlags & StringFormatFlags.NoWrap) != 0 ? Flatten(s) : s;
        }

        internal static void Text(Graphics g, string s, Font f, Color c, RectangleF r, StringFormat sf = null)
        {
            if (string.IsNullOrEmpty(s)) return;
            StringFormat fmt = sf ?? Left;
            using (SolidBrush b = new SolidBrush(c))
                g.DrawString(Prepare(s, fmt), f, b, r, fmt);
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
            // Measured as it is drawn: every caller lays the result out on one
            // line, so a break in the source must not widen the box either.
            return g.MeasureString(Flatten(Unescape(s)), f, int.MaxValue,
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
        /// <summary>
        /// Letter-spacing, which GDI+ has no notion of: DrawString lays glyphs
        /// out on the font's own advances and there is no tracking parameter.
        /// Drawn a glyph at a time with the extra advance added by hand.
        ///
        /// Only worth it on short strings -- per-glyph drawing loses kerning
        /// across the pairs it splits, which is invisible on an eight-character
        /// uppercase label and would not be on a sentence.
        /// </summary>
        internal static void TrackedText(Graphics g, string s, Font f, Color c,
                                         float x, float y, float height, float tracking)
        {
            if (string.IsNullOrEmpty(s)) return;
            using (SolidBrush b = new SolidBrush(c))
            {
                StringFormat fmt = TopLeft;
                float baseline = y + (height - f.GetHeight(g)) / 2f;

                // Tracking means placing each unit yourself, and placing each
                // unit yourself means GDI+ never sees the run. Devanagari came
                // out as its own letters with the halants left standing --
                // "का र ् य" where it should read "कार्य" -- because every
                // conjunct was handed over one code unit at a time. The
                // Arabic-derived scripts lose their joining the same way, and
                // any surrogate pair anywhere is split down the middle. So:
                // one call, no tracking, whenever the run has to be shaped.
                if (NocturneFonts.UsingScriptFallback)
                {
                    g.DrawString(s, f, b, x, baseline, fmt);
                    return;
                }

                foreach (string unit in TextElements(s))
                {
                    g.DrawString(unit, f, b, x, baseline, fmt);
                    x += g.MeasureString(unit, f, int.MaxValue,
                                         StringFormat.GenericTypographic).Width + tracking;
                }
            }
        }

        /// <summary>
        /// Splits into user-perceived characters rather than UTF-16 units, so
        /// a surrogate pair or a base plus its combining marks stays in one
        /// piece. <see cref="TrackedText"/> and <see cref="TrackedWidth"/> both
        /// walk this, which is what keeps them agreeing.
        /// </summary>
        static IEnumerable<string> TextElements(string s)
        {
            TextElementEnumerator e = StringInfo.GetTextElementEnumerator(s);
            while (e.MoveNext()) yield return (string)e.Current;
        }

        /// <summary>
        /// One em in pixels. Font.Size is in points, while every x we advance
        /// is a world-space pixel, so an em-relative measure has to be
        /// converted through the surface DPI or it comes out ~25% short at
        /// 96 DPI and drifts further as the display scales.
        /// </summary>
        internal static float Em(Graphics g, Font f)
        {
            return f.SizeInPoints * g.DpiY / 72f;
        }

        /// <summary>Width of a tracked run, so callers can lay out beside it.</summary>
        internal static float TrackedWidth(Graphics g, string s, Font f, float tracking)
        {
            if (string.IsNullOrEmpty(s)) return 0f;

            // Measured the way TrackedText draws it, or the caller lays the
            // next thing out against a width that was never used.
            if (NocturneFonts.UsingScriptFallback)
                return g.MeasureString(s, f, int.MaxValue, StringFormat.GenericTypographic).Width;

            float w = 0f;
            foreach (string unit in TextElements(s))
                w += g.MeasureString(unit, f, int.MaxValue,
                                     StringFormat.GenericTypographic).Width + tracking;
            return w;
        }

        internal static void SectionLabel(Graphics g, string text, Font f, int x, int y, int height)
        {
            int dashW = NocturneScale.S(14), dashH = Math.Max(2, NocturneScale.S(2));
            using (SolidBrush b = new SolidBrush(NocturneTheme.Accent))
                g.FillRectangle(b, x, y + (height - dashH) / 2, dashW, dashH);

            // 0.12em of tracking, per the handoff's type ramp. An uppercase
            // caption set solid reads as a cramped word rather than a label;
            // the spacing is most of what separates the two.
            string label = (text ?? string.Empty).ToUpperInvariant();
            TrackedText(g, label, f, NocturneTheme.TextMuted,
                        x + dashW + NocturneScale.S(8), y, height, Em(g, f) * 0.12f);
        }

        /// <summary>Card chrome: optional fill plus a 1px hairline at radius 8.</summary>
        internal static void Card(Graphics g, Rectangle r, Color fill, Color border, int radius)
        {
            if (fill != Color.Empty) NocturneTheme.FillRounded(g, r, radius, fill);
            if (border != Color.Empty) NocturneTheme.DrawRounded(g, r, radius, border);
        }

        /// <summary>
        /// Whether focus rings should be drawn at all.
        ///
        /// A ring is an answer to "where does the keyboard go next", and it is
        /// noise to anyone who is not asking. WinForms gives a control focus
        /// when it is clicked and when a window opens, so the first button on a
        /// screen wore a 2px accent ring from launch -- which on the dark theme
        /// reads as a purple halo round an edge for no reason the user can see.
        /// Turned on by the first Tab and off by the next click.
        /// </summary>
        internal static bool ShowFocusRings;

        /// <summary>A 2px accent focus ring, offset 2px outside the control.</summary>
        internal static void FocusRing(Graphics g, Rectangle r, int radius)
        {
            // Through DrawRounded so the ring gets the same half-pen inset as
            // every other outline. It matters here only at scales where S(2)
            // comes out odd -- an even pen width centred on an integer
            // coordinate already lands on whole pixels -- but a focus ring
            // that sharpens and softens as the display scale changes is worse
            // than one that is simply always right.
            int w = NocturneScale.S(2);
            NocturneTheme.DrawRounded(g, Rectangle.Inflate(r, w, w),
                                      radius + w, NocturneTheme.Accent, w);
        }
    }
}
