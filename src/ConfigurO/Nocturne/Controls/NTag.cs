using System;
using System.Drawing;
using System.Windows.Forms;

namespace ConfigurO
{
    /// <summary>
    /// A small pill of metadata: accent-tinted by default (the "Blocked" host
    /// marker, the version chip, the ping average) or outlined for neutral
    /// values like the Integrator's keyword chips.
    /// </summary>
    internal sealed class NTag : NControl
    {
        internal NTag()
        {
            Height = NocturneScale.S(18);
        }

        /// <summary>Outlined rather than filled -- used for keyword chips.</summary>
        internal bool Outline { get; set; }

        internal void AutoFit()
        {
            using (Graphics g = NocturneDraw.CreateMeasureGraphics())
            using (Font f = NocturneFonts.Tag())
                Width = (int)Math.Ceiling(NocturneDraw.Width(g, Text, f)) + NocturneScale.S(14);
        }

        /// <summary>Measures a tag without needing an instance on screen.</summary>
        internal static int MeasureWidth(Graphics g, string text)
        {
            using (Font f = NocturneFonts.Tag())
                return (int)Math.Ceiling(NocturneDraw.Width(g, text, f)) + NocturneScale.S(14);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Render(e);
            // Raise Paint last so anything attached to it draws on top.
            // Skipping this silently disables every `x.Paint += ...` handler.
            base.OnPaint(e);
        }

        void Render(PaintEventArgs e)
{
            Graphics g = e.Graphics;
            NocturneDraw.Prepare(g);

            Rectangle r = new Rectangle(0, 0, Width, Height);
            if (Outline)
                NocturneDraw.Card(g, r, Color.Empty, NocturneTheme.Divider, NocturneTheme.RadiusSm);
            else
                NocturneDraw.Card(g, r, NocturneTheme.TagBg, Color.Empty, NocturneTheme.RadiusSm);

            using (Font f = NocturneFonts.Tag())
                NocturneDraw.Text(g, Text, f,
                    Outline ? NocturneTheme.TextMuted : NocturneTheme.AccentStrong,
                    new RectangleF(0, 0, Width, Height), NocturneDraw.Center);
        }
    }
}
