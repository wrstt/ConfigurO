using System;
using System.Drawing;
using System.Windows.Forms;

namespace ConfigurO
{
    /// <summary>
    /// A selectable card -- cleaner items, app tiles, UWP rows.
    ///
    /// Idle is a neutral hairline; hover raises the border to accent-700;
    /// selected keeps that border and adds an accent@6% wash, plus a tick in
    /// the corner where the layout calls for one.
    /// </summary>
    internal sealed class NSelectCard : NControl
    {
        internal enum CardLayout
        {
            /// <summary>Icon, name, meta on one row with a checkbox at the right.</summary>
            Row,
            /// <summary>Large icon over a centred name and status line.</summary>
            Tile
        }

        bool _selected;

        internal NSelectCard()
        {
            Cursor = Cursors.Hand;
            TabStop = true;
            Height = NocturneScale.S(52);
        }

        internal CardLayout Kind = CardLayout.Row;

        /// <summary>Icon name, or null when <see cref="Image"/> is supplied.</summary>
        internal string Icon;

        /// <summary>Bitmap for app tiles; takes precedence over <see cref="Icon"/>.</summary>
        internal Image Image;

        /// <summary>Right-aligned metadata on a Row card, e.g. "1.2 GB".</summary>
        internal string Meta;

        /// <summary>Accent status line under a Tile card, e.g. "42%" or "Installed".</summary>
        internal string Status;

        /// <summary>Draw the checkbox affordance (Row cards only).</summary>
        internal bool ShowCheck = true;

        internal bool Selected
        {
            get { return _selected; }
            set { if (_selected == value) return; _selected = value; Invalidate(); OnSelectedChanged(); }
        }

        internal event EventHandler SelectedChanged;

        void OnSelectedChanged()
        {
            EventHandler h = SelectedChanged;
            if (h != null) h(this, EventArgs.Empty);
        }

        // Hover easing is NControl's; nothing to override.
        protected override void OnClick(EventArgs e) { Focus(); Selected = !Selected; base.OnClick(e); }

        protected override bool IsInputKey(Keys keyData)
        {
            return keyData == Keys.Space || base.IsInputKey(keyData);
        }

        protected override void OnKeyUp(KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Space) { Selected = !Selected; e.Handled = true; }
            base.OnKeyUp(e);
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
            // Selection is a state and lands at once; hover is a response and
            // fades. Mixing rather than swapping is what stops the card edge
            // flicking between two colours as the pointer crosses a grid.
            Color border = _selected
                         ? NocturneTheme.Accent700
                         : NocturneTheme.Mix(NocturneTheme.Accent700, NocturneTheme.Border, HoverAmount);
            Color fill = _selected ? NocturneTheme.SelectedFill : Color.Empty;

            NocturneDraw.Card(g, r, fill, border, NocturneTheme.RadiusMd);
            if (Focused) NocturneDraw.FocusRing(g, new Rectangle(0, 0, Width - 1, Height - 1), NocturneTheme.RadiusMd);

            if (Kind == CardLayout.Tile) PaintTile(g);
            else PaintRow(g);
        }

        void PaintRow(Graphics g)
        {
            int pad = NocturneScale.S(12);
            int x = pad;
            Color iconColor = _selected ? NocturneTheme.AccentText : NocturneTheme.TextFaint;

            if (Image != null)
            {
                int s = NocturneScale.S(22);
                g.DrawImage(Image, x, (Height - s) / 2, s, s);
                x += s + NocturneScale.S(10);
            }
            else if (!string.IsNullOrEmpty(Icon) && NocturneIcons.Exists(Icon))
            {
                int s = NocturneScale.S(19);
                NocturneIcons.Draw(g, Icon, x, (Height - s) / 2, s, iconColor);
                x += s + NocturneScale.S(10);
            }

            // The checkbox sits at `pad` from the right edge; reserve its
            // width plus a gap so right-aligned metadata cannot run into it.
            int checkSpace = ShowCheck
                ? pad + NocturneScale.S(NocturneTheme.CheckboxSize) + NocturneScale.S(10)
                : pad;
            int metaW = 0;
            if (!string.IsNullOrEmpty(Meta))
            {
                using (Font f = NocturneFonts.Meta())
                    metaW = (int)Math.Ceiling(NocturneDraw.Width(g, Meta, f)) + NocturneScale.S(10);
            }

            int textRight = Width - checkSpace - metaW;
            using (Font f = NocturneFonts.Row())
                NocturneDraw.Text(g, Text, f, NocturneTheme.Text,
                    new RectangleF(x, 0, Math.Max(0, textRight - x), Height), NocturneDraw.Left);

            if (metaW > 0)
            {
                using (Font f = NocturneFonts.Meta())
                    NocturneDraw.Text(g, Meta, f, NocturneTheme.TextFaint,
                        new RectangleF(textRight, 0, metaW, Height), NocturneDraw.Right);
            }

            if (ShowCheck)
            {
                int s = NocturneScale.S(NocturneTheme.CheckboxSize);
                NocturneCheckGlyph.Draw(g, new Rectangle(Width - pad - s, (Height - s) / 2, s, s), _selected);
            }
        }

        void PaintTile(Graphics g)
        {
            int iconBox = NocturneScale.S(38);
            int top = NocturneScale.S(14);
            int cx = (Width - iconBox) / 2;

            if (Image != null)
            {
                g.DrawImage(Image, cx, top, iconBox, iconBox);
            }
            else if (!string.IsNullOrEmpty(Icon) && NocturneIcons.Exists(Icon))
            {
                NocturneIcons.Draw(g, Icon, cx, top, iconBox,
                    _selected ? NocturneTheme.AccentText : NocturneTheme.TextFaint);
            }

            int y = top + iconBox + NocturneScale.S(8);
            using (Font f = NocturneFonts.Meta())
                NocturneDraw.Text(g, Text, f, NocturneTheme.Text,
                    new RectangleF(NocturneScale.S(6), y, Math.Max(0, Width - NocturneScale.S(12)),
                                   NocturneScale.S(16)), NocturneDraw.Center);

            if (!string.IsNullOrEmpty(Status))
            {
                using (Font f = NocturneFonts.Small())
                    NocturneDraw.Text(g, Status, f, NocturneTheme.AccentText,
                        new RectangleF(NocturneScale.S(6), y + NocturneScale.S(16),
                                       Math.Max(0, Width - NocturneScale.S(12)),
                                       NocturneScale.S(14)), NocturneDraw.Center);
            }
        }
    }

    /// <summary>
    /// The checkbox mark drawn inline by cards and table cells, sharing
    /// <see cref="MoonCheck"/>'s geometry without needing a child control.
    /// </summary>
    internal static class NocturneCheckGlyph
    {
        internal static void Draw(Graphics g, Rectangle box, bool on)
        {
            Rectangle r = new Rectangle(box.X, box.Y, box.Width - 1, box.Height - 1);
            int radius = NocturneScale.S(5);

            using (System.Drawing.Drawing2D.GraphicsPath p = NocturneTheme.RoundedRect(r, radius))
            {
                if (on)
                {
                    using (SolidBrush b = new SolidBrush(NocturneTheme.Accent)) g.FillPath(b, p);
                }
                else
                {
                    NocturneTheme.DrawRounded(g, r, radius, NocturneTheme.Neutral600);
                }
            }

            if (!on) return;
            using (Pen pen = new Pen(NocturneTheme.CheckMark, Math.Max(1.6f, NocturneScale.Sf(1.8f)))
                   {
                       StartCap = System.Drawing.Drawing2D.LineCap.Round,
                       EndCap = System.Drawing.Drawing2D.LineCap.Round,
                       LineJoin = System.Drawing.Drawing2D.LineJoin.Round
                   })
                g.DrawLines(pen, new[]
                {
                    new PointF(r.X + r.Width * 0.24f, r.Y + r.Height * 0.52f),
                    new PointF(r.X + r.Width * 0.43f, r.Y + r.Height * 0.71f),
                    new PointF(r.X + r.Width * 0.77f, r.Y + r.Height * 0.31f)
                });
        }
    }
}
