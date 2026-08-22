using System;
using System.Drawing;
using System.Windows.Forms;

namespace ConfigurO
{
    /// <summary>
    /// A vertically scrolling content host with a Nocturne scrollbar.
    ///
    /// WinForms' own scrollbars are system-drawn and cannot be themed, so the
    /// native ones are suppressed and a 6px accent-neutral thumb is painted on
    /// top of the content instead.
    /// </summary>
    internal sealed class NScrollPanel : NPanel
    {
        const int ThumbWidth = 6;

        readonly NPanel _content = new NPanel();
        bool _dragging;
        int _dragOffset;
        bool _thumbHover;

        internal NScrollPanel()
        {
            BackColor = Color.Transparent;
            _content.BackColor = Color.Transparent;
            _content.Location = Point.Empty;
            Controls.Add(_content);
        }

        /// <summary>Put children in here; set its Height to the full content height.</summary>
        internal NPanel Content { get { return _content; } }

        int Overflow { get { return Math.Max(0, _content.Height - Height); } }

        int ScrollY
        {
            get { return -_content.Top; }
            set
            {
                int v = Math.Max(0, Math.Min(Overflow, value));
                if (-_content.Top == v) return;
                _content.Top = -v;
                Invalidate();
            }
        }

        internal void ScrollToTop() { ScrollY = 0; }

        /// <summary>True when the content is taller than the viewport.</summary>
        internal bool CanScroll { get { return Overflow > 0; } }

        /// <summary>Scrolls by a pixel delta. Used by the wheel router.</summary>
        internal void ScrollBy(int delta) { ScrollY += delta; }

        protected override void OnLayout(LayoutEventArgs e)
        {
            base.OnLayout(e);
            _content.Width = Width;
            if (Overflow == 0 && _content.Top != 0) _content.Top = 0;
        }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            ScrollY -= (int)(e.Delta / 120f * NocturneScale.S(54));
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
                if (trackFree > 0)
                    ScrollY = (int)((float)(e.Y - _dragOffset) / trackFree * Overflow);
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
    }
}
