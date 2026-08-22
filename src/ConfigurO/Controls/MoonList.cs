using System;
using System.Drawing;
using System.Windows.Forms;

namespace ConfigurO
{
    /// <summary>
    /// Nocturne list box: surface ground, accent-tinted selection, no system
    /// highlight blue. Used by the startup backup/preview dialogs.
    /// </summary>
    public sealed class MoonList : ListBox
    {
        public MoonList()
        {
            SetStyle(ControlStyles.OptimizedDoubleBuffer, true);
            DrawMode = DrawMode.OwnerDrawFixed;
            BorderStyle = BorderStyle.None;
            BackColor = NocturneTheme.Surface;
            ForeColor = NocturneTheme.Text;
            Font = NocturneFonts.Row();
            ItemHeight = NocturneScale.S(24);
            IntegralHeight = false;
        }

        protected override void OnDrawItem(DrawItemEventArgs e)
        {
            if (e.Index < 0) return;
            bool selected = (e.State & DrawItemState.Selected) == DrawItemState.Selected;

            using (SolidBrush b = new SolidBrush(selected ? NocturneTheme.SelectedFillOnSurface : NocturneTheme.Surface))
                e.Graphics.FillRectangle(b, e.Bounds);

            if (selected)
            {
                using (SolidBrush b = new SolidBrush(NocturneTheme.Accent))
                    e.Graphics.FillRectangle(b, new Rectangle(e.Bounds.X, e.Bounds.Y, NocturneScale.S(2), e.Bounds.Height));
            }

            using (SolidBrush b = new SolidBrush(selected ? NocturneTheme.AccentText : NocturneTheme.Text))
            using (StringFormat sf = new StringFormat { LineAlignment = StringAlignment.Center, FormatFlags = StringFormatFlags.NoWrap })
                e.Graphics.DrawString(GetItemText(Items[e.Index]), e.Font ?? Font, b,
                    new RectangleF(e.Bounds.X + NocturneScale.S(10), e.Bounds.Y,
                                   e.Bounds.Width - NocturneScale.S(14), e.Bounds.Height), sf);
        }
    }
}
