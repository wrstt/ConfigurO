using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace ConfigurO
{
    /// <summary>
    /// Nocturne select: surface fill, divider edge, radius 8, 36px tall, with
    /// an accent caret and an accent focus ring. The drop-down list is owner
    /// drawn so it matches the theme instead of falling back to system white.
    /// </summary>
    public sealed class MoonSelect : ComboBox
    {
        bool _hover;

        public MoonSelect()
        {
            SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            DropDownStyle = ComboBoxStyle.DropDownList;
            DrawMode = DrawMode.OwnerDrawFixed;
            FlatStyle = FlatStyle.Flat;
            BackColor = NocturneTheme.Surface;
            ForeColor = NocturneTheme.Text;
            ItemHeight = NocturneScale.S(26);
            Font = NocturneFonts.Row();
        }

        protected override void OnMouseEnter(EventArgs e) { _hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { _hover = false; Invalidate(); base.OnMouseLeave(e); }

        protected override void OnDrawItem(DrawItemEventArgs e)
        {
            if (e.Index < 0) return;
            bool selected = (e.State & DrawItemState.Selected) == DrawItemState.Selected;

            using (SolidBrush b = new SolidBrush(selected ? NocturneTheme.SelectedFillOnSurface : NocturneTheme.Surface))
                e.Graphics.FillRectangle(b, e.Bounds);

            using (SolidBrush b = new SolidBrush(selected ? NocturneTheme.AccentText : NocturneTheme.Text))
            using (StringFormat sf = new StringFormat { LineAlignment = StringAlignment.Center, FormatFlags = StringFormatFlags.NoWrap })
                e.Graphics.DrawString(GetItemText(Items[e.Index]), e.Font ?? Font, b,
                    new RectangleF(e.Bounds.X + NocturneScale.S(10), e.Bounds.Y,
                                   e.Bounds.Width - NocturneScale.S(14), e.Bounds.Height), sf);
        }

        protected override void WndProc(ref Message m)
        {
            const int WM_PAINT = 0x000F;
            base.WndProc(ref m);

            if (m.Msg != WM_PAINT) return;
            using (Graphics g = Graphics.FromHwnd(Handle))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                Rectangle r = new Rectangle(0, 0, Width - 1, Height - 1);

                // Repaint over the system-drawn frame and drop-down button.
                using (SolidBrush b = new SolidBrush(NocturneTheme.Surface))
                    g.FillRectangle(b, new Rectangle(Width - NocturneScale.S(26), 1, NocturneScale.S(25), Height - 2));

                Color edge = Focused ? NocturneTheme.Accent
                           : _hover ? NocturneTheme.BorderStrong
                           : NocturneTheme.Divider;
                using (GraphicsPath p = NocturneTheme.RoundedRect(r, NocturneTheme.RadiusMd))
                using (Pen pen = new Pen(edge)) g.DrawPath(pen, p);

                NocturneIcons.Draw(g, NocturneIcons.Caret,
                    Width - NocturneScale.S(24), (Height - NocturneScale.S(16)) / 2,
                    NocturneScale.S(16), NocturneTheme.AccentText);
            }
        }
    }
}
