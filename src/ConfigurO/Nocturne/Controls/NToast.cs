using System;
using System.Drawing;
using System.Windows.Forms;

namespace ConfigurO
{
    /// <summary>
    /// The confirmation toast: a bottom-centred panel with an accent-700 edge,
    /// a check-circle in accent and 13px text. Auto-dismisses after 2.6s.
    ///
    /// One instance lives on the shell; <see cref="Show(string)"/> restarts the
    /// timer rather than stacking, so a burst of actions leaves one message.
    /// </summary>
    internal sealed class NToast : NControl
    {
        readonly Timer _timer = new Timer { Interval = 2600 };

        internal NToast()
        {
            Visible = false;
            Height = NocturneScale.S(38);
            _timer.Tick += (s, e) => { _timer.Stop(); Visible = false; };
        }

        internal void Show(string message)
        {
            if (string.IsNullOrEmpty(message)) return;
            Text = message;
            Measure();
            Reposition();
            Visible = true;
            BringToFront();
            Invalidate();
            _timer.Stop();
            _timer.Start();
        }

        void Measure()
        {
            using (Graphics g = NocturneDraw.CreateMeasureGraphics())
            using (Font f = NocturneFonts.Row())
            {
                int w = (int)Math.Ceiling(NocturneDraw.Width(g, Text, f));
                Width = w + NocturneScale.S(18) * 2 + NocturneScale.S(17) + NocturneScale.S(9);
                Height = NocturneScale.S(38);
            }
        }

        /// <summary>Centres the toast near the bottom of its parent.</summary>
        internal void Reposition()
        {
            if (Parent == null || Width <= 0) return;
            Left = Math.Max(0, (Parent.ClientSize.Width - Width) / 2);
            Top = Math.Max(0, Parent.ClientSize.Height - Height - NocturneScale.S(28));
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
            NocturneDraw.Card(g, r, NocturneTheme.SurfaceAlt, NocturneTheme.Accent700, NocturneTheme.RadiusMd);

            int pad = NocturneScale.S(18);
            int s = NocturneScale.S(17);
            NocturneIcons.Draw(g, NocturneIcons.CheckCircle, pad, (Height - s) / 2, s, NocturneTheme.AccentText);

            using (Font f = NocturneFonts.Row())
                NocturneDraw.Text(g, Text, f, NocturneTheme.Text,
                    new RectangleF(pad + s + NocturneScale.S(9), 0,
                                   Math.Max(0, Width - pad * 2 - s - NocturneScale.S(9)), Height),
                    NocturneDraw.Left);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) _timer.Dispose();
            base.Dispose(disposing);
        }
    }
}
