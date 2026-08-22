using System.Drawing;
using System.Windows.Forms;

namespace ConfigurO
{
    /// <summary>
    /// An 11px uppercase caption with a solid 14x2px accent dash in front of
    /// it, used to head each group on the Tweaks screen and each list in the
    /// Integrator.
    /// </summary>
    internal sealed class NSectionLabel : NControl
    {
        internal NSectionLabel()
        {
            Height = NocturneScale.S(18);
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
            NocturneDraw.Prepare(e.Graphics);
            using (Font f = NocturneFonts.SectionLabel())
                NocturneDraw.SectionLabel(e.Graphics, Text, f, 0, 0, Height);
        }
    }
}
