using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace ConfigurO
{
    /// <summary>
    /// Nocturne checked list: the <see cref="MoonCheck"/> language rendered
    /// inline per row. Used by the file-unlock dialog.
    /// </summary>
    public sealed class MoonCheckList : CheckedListBox
    {
        public MoonCheckList()
        {
            SetStyle(ControlStyles.OptimizedDoubleBuffer, true);
            BorderStyle = BorderStyle.None;
            BackColor = NocturneTheme.Surface;
            ForeColor = NocturneTheme.Text;
            Font = NocturneFonts.Row();
            CheckOnClick = true;
            IntegralHeight = false;
        }

        protected override void OnDrawItem(DrawItemEventArgs e)
        {
            if (e.Index < 0) return;
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            bool selected = (e.State & DrawItemState.Selected) == DrawItemState.Selected;
            bool checkedItem = GetItemChecked(e.Index);

            using (SolidBrush b = new SolidBrush(selected ? NocturneTheme.SelectedFillOnSurface : NocturneTheme.Surface))
                g.FillRectangle(b, e.Bounds);

            int s = NocturneScale.S(16);
            Rectangle box = new Rectangle(e.Bounds.X + NocturneScale.S(8),
                                          e.Bounds.Y + (e.Bounds.Height - s) / 2, s - 1, s - 1);
            using (GraphicsPath p = NocturneTheme.RoundedRect(box, NocturneScale.S(4)))
            {
                if (checkedItem)
                {
                    using (SolidBrush b = new SolidBrush(NocturneTheme.Accent)) g.FillPath(b, p);
                }
                else
                {
                    using (Pen pen = new Pen(NocturneTheme.Neutral600)) g.DrawPath(pen, p);
                }
            }

            if (checkedItem)
            {
                using (Pen pen = new Pen(NocturneTheme.CheckMark, NocturneScale.Sf(1.6f))
                       { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round })
                    g.DrawLines(pen, new[]
                    {
                        new PointF(box.X + box.Width * 0.24f, box.Y + box.Height * 0.52f),
                        new PointF(box.X + box.Width * 0.43f, box.Y + box.Height * 0.71f),
                        new PointF(box.X + box.Width * 0.77f, box.Y + box.Height * 0.31f)
                    });
            }

            using (SolidBrush b = new SolidBrush(NocturneTheme.Text))
            using (StringFormat sf = new StringFormat { LineAlignment = StringAlignment.Center, FormatFlags = StringFormatFlags.NoWrap })
                g.DrawString(GetItemText(Items[e.Index]), Font, b,
                    new RectangleF(box.Right + NocturneScale.S(8), e.Bounds.Y,
                                   e.Bounds.Width - box.Right - NocturneScale.S(12), e.Bounds.Height), sf);
        }
    }
}
