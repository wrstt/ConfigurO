using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace ConfigurO
{
    /// <summary>
    /// Nocturne radio button. Same language as <see cref="MoonCheck"/> but
    /// round, with an accent ring and a filled dot when selected.
    /// </summary>
    public sealed class MoonRadio : RadioButton
    {
        bool _hover;

        public MoonRadio()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.Transparent;
            Cursor = Cursors.Hand;
            AutoSize = false;
        }

        protected override void OnCheckedChanged(EventArgs e) { base.OnCheckedChanged(e); Invalidate(); }
        protected override void OnMouseEnter(EventArgs e) { _hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { _hover = false; Invalidate(); base.OnMouseLeave(e); }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            int s = NocturneScale.S(NocturneTheme.CheckboxSize);
            int top = (Height - s) / 2;
            RectangleF box = new RectangleF(0, top, s - 1, s - 1);

            Color edge = Checked ? NocturneTheme.Accent
                                 : (_hover ? NocturneTheme.Accent : NocturneTheme.Neutral600);
            using (Pen pen = new Pen(edge, NocturneScale.Sf(1.4f))) g.DrawEllipse(pen, box);

            if (Checked)
            {
                float inset = s * 0.28f;
                using (SolidBrush b = new SolidBrush(NocturneTheme.Accent))
                    g.FillEllipse(b, box.X + inset, box.Y + inset,
                                  box.Width - inset * 2, box.Height - inset * 2);
            }

            if (Focused)
            {
                using (Pen pen = new Pen(NocturneTheme.Accent, NocturneScale.S(2)))
                    g.DrawEllipse(pen, RectangleF.Inflate(box, NocturneScale.S(3), NocturneScale.S(3)));
            }

            if (!string.IsNullOrEmpty(Text))
            {
                using (Font f = NocturneFonts.Row())
                using (SolidBrush b = new SolidBrush(Enabled ? NocturneTheme.Text : NocturneTheme.TextFaint))
                {
                    SizeF sz = g.MeasureString(Text, f);
                    g.DrawString(Text, f, b, s + NocturneScale.S(8), (Height - sz.Height) / 2f);
                }
            }
        }
    }
}
