using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace ConfigurO
{
    internal sealed class NNavItem
    {
        internal string Id;
        internal string Icon;
        internal string Label;
        /// <summary>Hidden when the tool is switched off in Options or by a CLI flag.</summary>
        internal bool Enabled = true;
    }

    /// <summary>
    /// The 208px navigation rail that replaces the old top tab strip.
    ///
    /// Idle rows are neutral-300; hover fills with neutral-800; the active row
    /// takes an accent@12% fill, accent-200 text and a 2px accent left edge.
    /// The footer keeps a running count of applied tweaks.
    /// </summary>
    internal sealed class NSidebar : NControl
    {
        readonly List<NNavItem> _items = new List<NNavItem>();
        int _hover = -1;
        string _selected;

        internal NSidebar()
        {
            Width = NocturneScale.S(NocturneTheme.SidebarWidth);
        }

        protected override void OnScaleChanged()
        {
            Width = NocturneScale.S(NocturneTheme.SidebarWidth);
        }

        internal string FooterPrimary = string.Empty;
        internal string FooterSecondary = string.Empty;

        internal event EventHandler<string> Navigated;

        internal void SetItems(IEnumerable<NNavItem> items)
        {
            _items.Clear();
            _items.AddRange(items);
            Invalidate();
        }

        internal string Selected
        {
            get { return _selected; }
            set { if (_selected == value) return; _selected = value; Invalidate(); }
        }

        internal IEnumerable<NNavItem> Items { get { return _items; } }

        int ItemHeight { get { return NocturneScale.S(34); } }
        int TopPad { get { return NocturneScale.S(10); } }
        int SidePad { get { return NocturneScale.S(10); } }
        int FooterHeight { get { return NocturneScale.S(62); } }

        List<NNavItem> VisibleItems()
        {
            List<NNavItem> v = new List<NNavItem>();
            foreach (NNavItem i in _items) if (i.Enabled) v.Add(i);
            return v;
        }

        int IndexAt(Point p)
        {
            List<NNavItem> v = VisibleItems();
            int y = TopPad;
            for (int i = 0; i < v.Count; i++)
            {
                if (p.Y >= y && p.Y < y + ItemHeight) return i;
                y += ItemHeight + NocturneScale.S(2);
            }
            return -1;
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            int i = IndexAt(e.Location);
            if (i != _hover)
            {
                _hover = i;
                Cursor = i >= 0 ? Cursors.Hand : Cursors.Default;
                Invalidate();
            }
            base.OnMouseMove(e);
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            _hover = -1;
            Cursor = Cursors.Default;
            Invalidate();
            base.OnMouseLeave(e);
        }

        protected override void OnMouseClick(MouseEventArgs e)
        {
            int i = IndexAt(e.Location);
            if (i >= 0)
            {
                List<NNavItem> v = VisibleItems();
                Selected = v[i].Id;
                EventHandler<string> h = Navigated;
                if (h != null) h(this, v[i].Id);
            }
            base.OnMouseClick(e);
        }

        /// <summary>Moves the selection by <paramref name="delta"/> rows (Ctrl+Tab and arrows).</summary>
        internal void Step(int delta)
        {
            List<NNavItem> v = VisibleItems();
            if (v.Count == 0) return;
            int current = v.FindIndex(x => x.Id == _selected);
            int next = ((current < 0 ? 0 : current) + delta) % v.Count;
            if (next < 0) next += v.Count;
            Selected = v[next].Id;
            EventHandler<string> h = Navigated;
            if (h != null) h(this, v[next].Id);
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

            using (SolidBrush b = new SolidBrush(NocturneTheme.Bg))
                g.FillRectangle(b, ClientRectangle);

            using (Pen p = new Pen(NocturneTheme.Border))
                g.DrawLine(p, Width - 1, 0, Width - 1, Height);

            List<NNavItem> v = VisibleItems();
            int y = TopPad;
            using (Font f = NocturneFonts.Nav())
            {
                for (int i = 0; i < v.Count; i++)
                {
                    bool active = v[i].Id == _selected;
                    Rectangle row = new Rectangle(SidePad, y, Width - SidePad * 2 - NocturneScale.S(1), ItemHeight);

                    if (active)
                    {
                        NocturneTheme.FillRounded(g, row, NocturneTheme.RadiusMd, NocturneTheme.ActiveNavFill);
                        using (SolidBrush b = new SolidBrush(NocturneTheme.Accent))
                            g.FillRectangle(b, row.X, row.Y + NocturneScale.S(4),
                                            Math.Max(2, NocturneScale.S(2)), row.Height - NocturneScale.S(8));
                    }
                    else if (i == _hover)
                    {
                        NocturneTheme.FillRounded(g, row, NocturneTheme.RadiusMd, NocturneTheme.HoverFill);
                    }

                    Color fg = active ? NocturneTheme.AccentStrong : NocturneTheme.SidebarText;
                    int iconSize = NocturneScale.S(17);
                    int ix = row.X + NocturneScale.S(12);
                    NocturneIcons.Draw(g, v[i].Icon, ix, row.Y + (row.Height - iconSize) / 2, iconSize, fg);

                    NocturneDraw.Text(g, v[i].Label, f, fg,
                        new RectangleF(ix + iconSize + NocturneScale.S(10), row.Y,
                                       row.Right - ix - iconSize - NocturneScale.S(14), row.Height),
                        NocturneDraw.Left);

                    y += ItemHeight + NocturneScale.S(2);
                }
            }

            // ── footer ──
            int fy = Height - FooterHeight;
            NocturneTheme.DrawFadedRule(g, SidePad, fy, Width - SidePad * 2, NocturneTheme.Border);
            using (Font f = NocturneFonts.Small())
            {
                NocturneDraw.Text(g, FooterPrimary, f, NocturneTheme.TextFaint,
                    new RectangleF(SidePad + NocturneScale.S(2), fy + NocturneScale.S(12),
                                   Width - SidePad * 2, NocturneScale.S(16)), NocturneDraw.Left);
                NocturneDraw.Text(g, FooterSecondary, f, NocturneTheme.TextDim,
                    // 25 sat this line's box directly against the one above,
                    // whose box ran to exactly 25. Two lines with no gap read as
                    // one wrapped sentence rather than as a count and a state.
                    new RectangleF(SidePad + NocturneScale.S(2), fy + NocturneScale.S(31),
                                   Width - SidePad * 2, NocturneScale.S(16)), NocturneDraw.Left);
            }
        }
    }
}
