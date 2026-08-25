using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace ConfigurO
{
    /// <summary>
    /// The Nocturne toggle: a 38x21 pill with a 15px knob that slides in 150ms.
    /// Off is a neutral fill with a neutral-700 edge; on is accent-700 filled
    /// with an accent edge. An optional "On" label sits to its left in accent.
    /// </summary>
    public sealed class MoonToggle : CheckBox
    {
        const int AnimationMs = 150;

        readonly Timer _animation = new Timer { Interval = 15 };
        float _position;          // 0 = off, 1 = on
        DateTime _animationStart;
        float _animationFrom;
        bool _hover;

        public MoonToggle()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.SupportsTransparentBackColor, true);
            // ButtonBase turns Opaque on, which suppresses OnPaintBackground
            // and leaves the client area holding the shared double buffer's
            // last contents -- see MoonCheck.
            SetStyle(ControlStyles.Opaque, false);
            BackColor = Color.Transparent;
            Cursor = Cursors.Hand;
            AutoSize = false;
            Size = new Size(NocturneScale.S(NocturneTheme.ToggleWidth),
                            NocturneScale.S(NocturneTheme.ToggleHeight));
            _position = Checked ? 1f : 0f;
            _animation.Tick += Animate;
        }

        /// <summary>Shows the accent "On" caption to the left of the pill when checked.</summary>
        public bool ShowOnLabel { get; set; }

        public override string Text
        {
            get { return string.Empty; }
            set { base.Text = string.Empty; }
        }

        protected override void OnCheckedChanged(EventArgs e)
        {
            base.OnCheckedChanged(e);
            _animationFrom = _position;
            _animationStart = DateTime.UtcNow;
            _animation.Start();
        }

        void Animate(object sender, EventArgs e)
        {
            float t = (float)(DateTime.UtcNow - _animationStart).TotalMilliseconds / AnimationMs;
            if (t >= 1f)
            {
                t = 1f;
                _animation.Stop();
            }
            // ease-out so the knob settles rather than stopping dead
            float eased = 1f - (1f - t) * (1f - t);
            float target = Checked ? 1f : 0f;
            _position = _animationFrom + (target - _animationFrom) * eased;
            Invalidate();
        }

        protected override void OnMouseEnter(EventArgs e) { _hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { _hover = false; Invalidate(); base.OnMouseLeave(e); }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            int h = NocturneScale.S(NocturneTheme.ToggleHeight);
            int w = NocturneScale.S(NocturneTheme.ToggleWidth);
            int knob = NocturneScale.S(NocturneTheme.ToggleKnobSize);
            int pad = (h - knob) / 2;
            int top = (Height - h) / 2;
            int left = Width - w;      // the pill is right-aligned; the label sits before it

            if (ShowOnLabel && _position > 0.5f)
            {
                using (Font f = NocturneFonts.Tip())
                using (SolidBrush b = new SolidBrush(NocturneTheme.AccentText))
                {
                    string on = I18n.Get("toggleOn", "On");
                    SizeF sz = g.MeasureString(on, f);
                    g.DrawString(on, f, b, left - sz.Width - NocturneScale.S(8),
                                 top + (h - sz.Height) / 2f);
                }
            }

            Rectangle pill = new Rectangle(left, top, w - 1, h - 1);
            Color fill = Blend(NocturneTheme.ToggleOff, NocturneTheme.ToggleOn, _position);
            Color edge = Blend(NocturneTheme.ToggleOffEdge, NocturneTheme.Accent, _position);
            if (_hover && _position < 0.5f) edge = NocturneTheme.BorderStrong;

            using (GraphicsPath p = NocturneTheme.RoundedRect(pill, h / 2))
            {
                using (SolidBrush b = new SolidBrush(fill)) g.FillPath(b, p);
                NocturneTheme.DrawRounded(g, pill, h / 2, edge);
            }

            if (Focused)
            {
                NocturneDraw.FocusRing(g, pill, pill.Height / 2);
            }

            float knobX = left + pad + _position * (w - knob - pad * 2);
            using (SolidBrush b = new SolidBrush(NocturneTheme.ToggleKnob))
                g.FillEllipse(b, knobX, top + pad, knob, knob);

            // See NocturneTogglePill: the knob needs an edge in light mode.
            if (!NocturneTheme.IsDark)
            {
                using (Pen pen = new Pen(NocturneTheme.Alpha(NocturneTheme.Neutral900, 0.16)))
                    g.DrawEllipse(pen, knobX, top + pad, knob, knob);
            }
        }

        static Color Blend(Color from, Color to, float t)
        {
            return NocturneTheme.Mix(to, from, t);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) _animation.Dispose();
            base.Dispose(disposing);
        }
    }
}
