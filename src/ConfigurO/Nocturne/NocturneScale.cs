using System;
using System.Drawing;
using System.Windows.Forms;

namespace ConfigurO
{
    /// <summary>
    /// DPI scaling for the hand-laid-out Nocturne UI.
    ///
    /// Every size in the design tokens is expressed at 96 DPI; layout code runs
    /// it through <see cref="S(int)"/>. The app declares PerMonitorV2 awareness
    /// in its manifest, so <see cref="Factor"/> is refreshed from the shell
    /// window whenever it moves to a display with a different scale.
    /// </summary>
    internal static class NocturneScale
    {
        const float Base = 96f;

        static float _factor = 1f;

        /// <summary>Raised after <see cref="Factor"/> changes so the shell can re-layout.</summary>
        internal static event EventHandler Changed;

        internal static float Factor
        {
            get { return _factor; }
            private set
            {
                if (Math.Abs(_factor - value) < 0.001f) return;
                _factor = value;
                EventHandler h = Changed;
                if (h != null) h(null, EventArgs.Empty);
            }
        }

        internal static void SetDpi(int dpi)
        {
            if (dpi <= 0) dpi = 96;
            Factor = dpi / Base;
        }

        /// <summary>Scales a design-token pixel value to the current display.</summary>
        internal static int S(int v)
        {
            return (int)Math.Round(v * _factor, MidpointRounding.AwayFromZero);
        }

        internal static float Sf(float v) { return v * _factor; }

        internal static Size S(Size s) { return new Size(S(s.Width), S(s.Height)); }

        internal static Padding S(Padding p)
        {
            return new Padding(S(p.Left), S(p.Top), S(p.Right), S(p.Bottom));
        }

        /// <summary>
        /// Point sizes are already DPI-relative in GDI+, so type does NOT get
        /// multiplied by <see cref="Factor"/>; this exists so callers can be
        /// explicit about that and never accidentally double-scale text.
        /// </summary>
        internal static float Pt(float pt) { return pt; }
    }
}
