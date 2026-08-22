using System;
using System.Drawing;
using System.Windows.Forms;

namespace ConfigurO
{
    /// <summary>
    /// The 46px custom title bar: brand mark, wordmark plus version chip on the
    /// left; OS string, theme switch and the window buttons on the right.
    ///
    /// The window buttons are painted here rather than hosted as controls so
    /// the shell's WM_NCHITTEST can report HTMAXBUTTON over the maximise
    /// button -- that is what makes Windows 11 Snap Layouts appear on hover.
    /// </summary>
    internal sealed class NTitleBar : NControl
    {
        internal enum Hit { None, Drag, Theme, Minimize, Maximize, Close }

        Hit _hover = Hit.None;

        internal NTitleBar()
        {
            Height = NocturneScale.S(NocturneTheme.TitleBarHeight);
        }

        protected override void OnScaleChanged()
        {
            Height = NocturneScale.S(NocturneTheme.TitleBarHeight);
        }

        internal string VersionTag = string.Empty;
        internal string OsSummary = string.Empty;
        internal bool IsMaximized;

        internal event EventHandler ThemeClicked;
        internal event EventHandler MinimizeClicked;
        internal event EventHandler MaximizeClicked;
        internal event EventHandler CloseClicked;

        int ButtonW { get { return NocturneScale.S(36); } }
        int ButtonH { get { return NocturneScale.S(30); } }

        /// <summary>Which affordance sits under a point in this bar's own coordinates.</summary>
        internal Hit HitTest(Point p)
        {
            int right = Width;
            int by = (Height - ButtonH) / 2;
            for (int i = 0; i < 4; i++)
            {
                Rectangle r = new Rectangle(right - ButtonW * (i + 1), by, ButtonW, ButtonH);
                if (!r.Contains(p)) continue;
                switch (i)
                {
                    case 0: return Hit.Close;
                    case 1: return Hit.Maximize;
                    case 2: return Hit.Minimize;
                    default: return Hit.Theme;
                }
            }
            return Hit.Drag;
        }

        Rectangle ButtonRect(Hit h)
        {
            int index;
            switch (h)
            {
                case Hit.Close: index = 0; break;
                case Hit.Maximize: index = 1; break;
                case Hit.Minimize: index = 2; break;
                case Hit.Theme: index = 3; break;
                default: return Rectangle.Empty;
            }
            return new Rectangle(Width - ButtonW * (index + 1), (Height - ButtonH) / 2, ButtonW, ButtonH);
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            Hit h = HitTest(e.Location);
            if (h != _hover) { _hover = h; Invalidate(); }
            base.OnMouseMove(e);
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            _hover = Hit.None;
            Invalidate();
            base.OnMouseLeave(e);
        }

        protected override void OnMouseClick(MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left) { base.OnMouseClick(e); return; }
            switch (HitTest(e.Location))
            {
                case Hit.Theme: Raise(ThemeClicked); break;
                case Hit.Minimize: Raise(MinimizeClicked); break;
                case Hit.Maximize: Raise(MaximizeClicked); break;
                case Hit.Close: Raise(CloseClicked); break;
            }
            base.OnMouseClick(e);
        }

        protected override void OnMouseDoubleClick(MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left && HitTest(e.Location) == Hit.Drag) Raise(MaximizeClicked);
            base.OnMouseDoubleClick(e);
        }

        static void Raise(EventHandler h) { if (h != null) h(null, EventArgs.Empty); }

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

            using (SolidBrush b = new SolidBrush(NocturneTheme.Bg))
                g.FillRectangle(b, ClientRectangle);

            int pad = NocturneScale.S(14);
            int x = pad;

            // ── brand ──
            int mark = NocturneScale.S(22);
            NocturneBrand.Draw(g, x, (Height - mark) / 2, mark);
            x += mark + NocturneScale.S(10);

            using (Font f = NocturneFonts.Brand())
            {
                float w = NocturneDraw.Width(g, "ConfigurO", f);
                NocturneDraw.Text(g, "ConfigurO", f, NocturneTheme.Text,
                    new RectangleF(x, 0, w, Height), NocturneDraw.Left);
                x += (int)Math.Ceiling(w) + NocturneScale.S(8);
            }

            if (!string.IsNullOrEmpty(VersionTag))
            {
                using (Font f = NocturneFonts.Tag())
                {
                    int tw = (int)Math.Ceiling(NocturneDraw.Width(g, VersionTag, f)) + NocturneScale.S(12);
                    int th = NocturneScale.S(17);
                    Rectangle tag = new Rectangle(x, (Height - th) / 2, tw, th);
                    NocturneDraw.Card(g, tag, NocturneTheme.TagBg, Color.Empty, NocturneTheme.RadiusSm);
                    NocturneDraw.Text(g, VersionTag, f, NocturneTheme.AccentStrong, tag, NocturneDraw.Center);
                }
            }

            // ── window buttons ──
            PaintButton(g, Hit.Close, NocturneIcons.Close, NocturneTheme.SidebarText, true);
            PaintButton(g, Hit.Maximize, IsMaximized ? NocturneIcons.Restore : NocturneIcons.Maximize,
                        NocturneTheme.SidebarText, false);
            PaintButton(g, Hit.Minimize, NocturneIcons.Minimize, NocturneTheme.SidebarText, false);
            PaintButton(g, Hit.Theme, NocturneTheme.IsDark ? NocturneIcons.Sun : NocturneIcons.Moon,
                        NocturneTheme.AccentText, false);

            // ── OS string, to the left of the buttons ──
            if (string.IsNullOrEmpty(OsSummary)) return;
            int buttonsLeft = ButtonRect(Hit.Theme).X;
            using (Font f = NocturneFonts.Chrome())
            {
                int w = (int)Math.Ceiling(NocturneDraw.Width(g, OsSummary, f));
                int left = Math.Max(x + NocturneScale.S(16), buttonsLeft - NocturneScale.S(12) - w);
                NocturneDraw.Text(g, OsSummary, f, NocturneTheme.TextMuted,
                    new RectangleF(left, 0, buttonsLeft - NocturneScale.S(12) - left, Height),
                    NocturneDraw.Right);
            }
        }

        void PaintButton(Graphics g, Hit which, string icon, Color color, bool danger)
        {
            Rectangle r = ButtonRect(which);
            if (_hover == which)
            {
                using (SolidBrush b = new SolidBrush(danger ? NocturneTheme.Alpha(Color.FromArgb(232, 68, 68), 0.85)
                                                            : NocturneTheme.HoverFill))
                    g.FillRectangle(b, r);
                if (danger) color = Color.White;
            }
            NocturneIcons.DrawCentered(g, icon, r, NocturneScale.S(15), color);
        }
    }
}
