using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace ConfigurO
{
    /// <summary>
    /// The Nocturne progress bar: a 4px accent fill on a neutral-800 track,
    /// with a soft accent glow along the filled portion.
    /// </summary>
    internal sealed class MoonProgress : ProgressBar
    {
        public MoonProgress()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer, true);
            Height = NocturneScale.S(4);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            int h = Math.Max(NocturneScale.S(4), 3);
            int top = (Height - h) / 2;
            Rectangle track = new Rectangle(0, top, Width, h);

            NocturneTheme.FillRounded(g, track, h / 2, NocturneTheme.Border);

            int span = Maximum - Minimum;
            if (span <= 0) return;
            int w = (int)Math.Round((double)(Value - Minimum) / span * Width);
            if (w <= 0) return;

            Rectangle fill = new Rectangle(0, top, Math.Min(w, Width), h);

            // The glow is a wider, translucent pass under the solid bar.
            using (GraphicsPath glow = NocturneTheme.RoundedRect(
                       Rectangle.Inflate(fill, 0, NocturneScale.S(2)), h))
            using (SolidBrush b = new SolidBrush(NocturneTheme.Alpha(NocturneTheme.Accent, 0.25)))
                g.FillPath(b, glow);

            NocturneTheme.FillRounded(g, fill, h / 2, NocturneTheme.Accent);
        }
    }
}
