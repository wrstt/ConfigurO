using System;
using System.Drawing;
using System.Windows.Forms;

namespace ConfigurO
{
    /// <summary>
    /// A vertically scrolling content host with a Nocturne scrollbar.
    ///
    /// The WinForms scrollbars are system-drawn and cannot be themed, so the
    /// native ones are suppressed and a 6px accent-neutral thumb is painted on
    /// top of the content instead.
    ///
    /// The wheel does not move the content directly. It moves a target, and a
    /// timer eases the content towards it -- see <see cref="ScrollByNotches"/>.
    /// </summary>
    internal sealed class NScrollPanel : NPanel
    {
        const int ThumbWidth = 6;

        /// <summary>Design pixels travelled per wheel notch.</summary>
        const int WheelStep = 54;

        /// <summary>Glide frame interval, ~66fps.</summary>
        const int FrameMs = 15;

        /// <summary>
        /// Fraction of the remaining distance covered each frame. Exponential
        /// rather than linear: it leaves quickly and arrives softly, which is
        /// what every other scrolling surface on the platform does.
        /// </summary>
        const float Ease = 0.28f;

        readonly NPanel _content = new NPanel();
        readonly Timer _glide = new Timer();
        int _target;
        bool _dragging;
        int _dragOffset;
        bool _thumbHover;

        internal NScrollPanel()
        {
            BackColor = Color.Transparent;
            _content.BackColor = Color.Transparent;
            _content.Location = Point.Empty;
            Controls.Add(_content);

            _glide.Interval = FrameMs;
            _glide.Tick += OnGlide;
        }

        /// <summary>
        /// Composites children into one buffer before the panel is painted.
        ///
        /// Moving the content panel is what scrolling is, and moving a control
        /// with children repaints each of them separately. At one step per
        /// notch that reads as a jump; at 66 frames a second it reads as
        /// tearing. This is the flag that makes the difference.
        /// </summary>
        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams cp = base.CreateParams;
                cp.ExStyle |= 0x02000000;   // WS_EX_COMPOSITED
                return cp;
            }
        }

        /// <summary>Put children in here; set its Height to the full content height.</summary>
        internal NPanel Content { get { return _content; } }

        int Overflow { get { return Math.Max(0, _content.Height - Height); } }

        int Clamp(int v) { return Math.Max(0, Math.Min(Overflow, v)); }

        int ScrollY
        {
            get { return -_content.Top; }
            set
            {
                int v = Clamp(value);
                if (-_content.Top == v) return;
                _content.Top = -v;
                Invalidate();
            }
        }

        /// <summary>True when the content is taller than the viewport.</summary>
        internal bool CanScroll { get { return Overflow > 0; } }

        /// <summary>
        /// Moves the scroll target by a number of wheel notches and lets the
        /// glide catch up. Fractional notches are honoured, so a precision
        /// touchpad reports the small deltas it means rather than having them
        /// rounded up to a full step.
        /// </summary>
        internal void ScrollByNotches(float notches)
        {
            if (!CanScroll) return;

            // Accumulated against the target, not the current position, so a
            // fast flick compounds into one long glide instead of each notch
            // restarting from wherever the last one had reached.
            _target = Clamp(_target + (int)Math.Round(notches * NocturneScale.S(WheelStep)));
            if (_target == ScrollY) { _glide.Stop(); return; }
            _glide.Start();
        }

        /// <summary>Jumps without animating. For direct manipulation and screen changes.</summary>
        void ScrollTo(int v)
        {
            _glide.Stop();
            ScrollY = v;
            _target = ScrollY;
        }

        internal void ScrollToTop() { ScrollTo(0); }

        void OnGlide(object sender, EventArgs e)
        {
            int from = ScrollY;
            int gap = _target - from;

            // Below a pixel a proportional step rounds to zero and the glide
            // would never finish, so the last stretch is walked one pixel at a
            // time.
            int step = (int)Math.Round(gap * Ease);
            if (step == 0) step = Math.Sign(gap);

            ScrollY = from + step;

            // Stopping on "did not move" as well as on "reached the target"
            // catches the case where the content shrank under us and the
            // target is no longer reachable.
            if (ScrollY == _target || ScrollY == from)
            {
                _target = ScrollY;
                _glide.Stop();
            }
        }

        protected override void OnLayout(LayoutEventArgs e)
        {
            base.OnLayout(e);
            _content.Width = Width;
            if (Overflow == 0 && _content.Top != 0) ScrollTo(0);
            else _target = Clamp(_target);
        }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            ScrollByNotches(-e.Delta / 120f);
            base.OnMouseWheel(e);
        }

        Rectangle ThumbRect()
        {
            if (Overflow <= 0) return Rectangle.Empty;
            int w = NocturneScale.S(ThumbWidth);
            int track = Height;
            int h = Math.Max(NocturneScale.S(30), (int)((float)Height / _content.Height * track));
            int y = (int)((float)ScrollY / Overflow * (track - h));
            return new Rectangle(Width - w - NocturneScale.S(2), y, w, h);
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            Rectangle t = ThumbRect();
            if (t != Rectangle.Empty && t.Contains(e.Location))
            {
                _dragging = true;
                _dragOffset = e.Y - t.Y;
                Capture = true;
                _glide.Stop();
            }
            base.OnMouseDown(e);
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            Rectangle t = ThumbRect();
            bool over = t != Rectangle.Empty && t.Contains(e.Location);
            if (over != _thumbHover) { _thumbHover = over; Invalidate(); }

            if (_dragging && Overflow > 0)
            {
                int trackFree = Height - t.Height;
                // Dragging is direct manipulation: the thumb belongs under the
                // pointer, not easing towards it.
                if (trackFree > 0)
                    ScrollTo((int)((float)(e.Y - _dragOffset) / trackFree * Overflow));
            }
            base.OnMouseMove(e);
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            _dragging = false;
            Capture = false;
            base.OnMouseUp(e);
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            _thumbHover = false;
            Invalidate();
            base.OnMouseLeave(e);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            Rectangle t = ThumbRect();
            if (t == Rectangle.Empty) return;

            NocturneDraw.Prepare(e.Graphics);
            NocturneTheme.FillRounded(e.Graphics, t, t.Width / 2,
                _dragging || _thumbHover ? NocturneTheme.Accent700 : NocturneTheme.ScrollThumb);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _glide.Tick -= OnGlide;
                _glide.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
