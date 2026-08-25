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

        /// <summary>How far the card travels as it arrives, in design pixels.</summary>
        const int Rise = 8;

        readonly NAnim _fade;
        bool _dismissing;

        internal NToast()
        {
            Visible = false;
            Height = NocturneScale.S(38 + Rise);
            _fade = new NAnim(OnFadeFrame, 180);
            _timer.Tick += (s, e) => { _timer.Stop(); Dismiss(); };
        }

        void OnFadeFrame()
        {
            Invalidate();
            // Hidden only once it has actually finished leaving, or the last
            // few frames of the fade are thrown away and it blinks out.
            if (_dismissing && !_fade.Running && _fade.Value <= 0.01f)
            {
                _dismissing = false;
                Visible = false;
            }
        }

        void Dismiss()
        {
            _dismissing = true;
            _fade.To(0f);
        }

        internal void Show(string message)
        {
            if (string.IsNullOrEmpty(message)) return;
            Text = message;
            Measure();
            Reposition();
            _dismissing = false;
            Visible = true;
            BringToFront();
            _fade.To(1f);
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
                // Taller than the card by the travel distance, so the rise has
                // somewhere to happen without clipping the bottom edge.
                Height = NocturneScale.S(38 + Rise);
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

            // Everything is drawn at the fade's alpha rather than the control
            // being hidden outright. A confirmation that appears and vanishes
            // between two frames reads as a glitch; one that arrives and
            // leaves reads as the app answering.
            float a = _fade.Value;
            if (a <= 0.004f) return;

            int travel = NocturneScale.S(Rise);
            int cardTop = (int)Math.Round((1f - a) * travel);
            int cardH = Height - travel;
            Rectangle r = new Rectangle(0, cardTop, Width, cardH);

            NocturneDraw.Card(g, r,
                NocturneTheme.Alpha(NocturneTheme.SurfaceAlt, a),
                NocturneTheme.Alpha(NocturneTheme.Accent700, a),
                NocturneTheme.RadiusMd);

            int pad = NocturneScale.S(18);
            int s = NocturneScale.S(17);
            NocturneIcons.Draw(g, NocturneIcons.CheckCircle, pad, cardTop + (cardH - s) / 2, s,
                NocturneTheme.Alpha(NocturneTheme.AccentText, a));

            using (Font f = NocturneFonts.Row())
                NocturneDraw.Text(g, Text, f, NocturneTheme.Alpha(NocturneTheme.Text, a),
                    new RectangleF(pad + s + NocturneScale.S(9), cardTop,
                                   Math.Max(0, Width - pad * 2 - s - NocturneScale.S(9)), cardH),
                    NocturneDraw.Left);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) { _timer.Dispose(); _fade.Dispose(); }
            base.Dispose(disposing);
        }
    }
}
