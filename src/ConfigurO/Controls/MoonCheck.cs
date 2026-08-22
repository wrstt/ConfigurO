using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace ConfigurO
{
    /// <summary>
    /// The Nocturne checkbox: an 18px rounded square. Unchecked shows a
    /// neutral-600 edge on nothing; checked fills with the accent and draws a
    /// dark tick, so it reads against both themes.
    /// </summary>
    public sealed class MoonCheck : CheckBox
    {
        bool _hover;

        public MoonCheck()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.Transparent;
            Cursor = Cursors.Hand;
            AutoSize = false;
            Size = new Size(NocturneScale.S(NocturneTheme.CheckboxSize),
                            NocturneScale.S(NocturneTheme.CheckboxSize));
        }

        public override string Text
        {
            get { return base.Text; }
            set { base.Text = value; }
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
            Rectangle box = new Rectangle(0, top, s - 1, s - 1);
            int radius = NocturneScale.S(5);

            using (GraphicsPath p = NocturneTheme.RoundedRect(box, radius))
            {
                if (Checked)
                {
                    using (SolidBrush b = new SolidBrush(NocturneTheme.Accent)) g.FillPath(b, p);
                }
                else
                {
                    Color edge = _hover ? NocturneTheme.Accent : NocturneTheme.Neutral600;
                    using (Pen pen = new Pen(edge)) g.DrawPath(pen, p);
                }
            }

            if (Checked)
            {
                using (Pen pen = new Pen(NocturneTheme.CheckMark, Math.Max(1.6f, NocturneScale.Sf(1.8f)))
                       { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round })
                {
                    float x = box.X, y = box.Y, w = box.Width, h = box.Height;
                    g.DrawLines(pen, new[]
                    {
                        new PointF(x + w * 0.24f, y + h * 0.52f),
                        new PointF(x + w * 0.43f, y + h * 0.71f),
                        new PointF(x + w * 0.77f, y + h * 0.31f)
                    });
                }
            }

            if (Focused)
            {
                Rectangle ring = Rectangle.Inflate(box, NocturneScale.S(2), NocturneScale.S(2));
                using (GraphicsPath p = NocturneTheme.RoundedRect(ring, radius + NocturneScale.S(2)))
                using (Pen pen = new Pen(NocturneTheme.Accent, NocturneScale.S(2)))
                    g.DrawPath(pen, p);
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
