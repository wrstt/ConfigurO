using System;
using System.Drawing;
using System.Windows.Forms;

namespace ConfigurO
{
    /// <summary>
    /// A surface panel: 1px hairline, radius 8, optional title row with an
    /// accent icon. Hosts arbitrary children inside <see cref="Body"/>, which
    /// is already inset by the card's padding.
    /// </summary>
    internal sealed class NCard : NPanel
    {
        readonly NPanel _body = new NPanel();
        string _title, _icon, _note;

        internal NCard()
        {
            BackColor = Color.Transparent;
            _body.BackColor = Color.Transparent;
            Controls.Add(_body);
        }

        /// <summary>Add children here, not to the card itself.</summary>
        internal NPanel Body { get { return _body; } }

        internal string Title
        {
            get { return _title; }
            set { _title = value; PerformLayout(); Invalidate(); }
        }

        /// <summary>Icon name drawn in accent before the title.</summary>
        internal string Icon
        {
            get { return _icon; }
            set { _icon = value; Invalidate(); }
        }

        /// <summary>A muted line under the title, e.g. "Replaces your current configuration".</summary>
        internal string Note
        {
            get { return _note; }
            set { _note = value; PerformLayout(); Invalidate(); }
        }

        internal Padding CardPadding = new Padding(14, 12, 14, 12);

        /// <summary>Fill the card with the alt surface instead of the standard one.</summary>
        internal bool Raised { get; set; }

        int HeaderHeight
        {
            get
            {
                if (string.IsNullOrEmpty(_title)) return 0;
                int h = NocturneScale.S(22);
                if (!string.IsNullOrEmpty(_note)) h += NocturneScale.S(17);
                return h + NocturneScale.S(8);
            }
        }

        protected override void OnLayout(LayoutEventArgs e)
        {
            base.OnLayout(e);
            Padding p = NocturneScale.S(CardPadding);
            int top = p.Top + HeaderHeight;
            _body.SetBounds(p.Left, top,
                            Math.Max(0, Width - p.Left - p.Right),
                            Math.Max(0, Height - top - p.Bottom));
        }

        protected override void OnScaleChanged() { PerformLayout(); }

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

            NocturneDraw.Card(g, new Rectangle(0, 0, Width, Height),
                              Raised ? NocturneTheme.SurfaceAlt : NocturneTheme.Surface,
                              NocturneTheme.Border, NocturneTheme.RadiusMd);

            if (string.IsNullOrEmpty(_title)) return;

            Padding p = NocturneScale.S(CardPadding);
            int x = p.Left, y = p.Top;
            int line = NocturneScale.S(22);

            if (!string.IsNullOrEmpty(_icon) && NocturneIcons.Exists(_icon))
            {
                int s = NocturneScale.S(19);
                NocturneIcons.Draw(g, _icon, x, y + (line - s) / 2, s, NocturneTheme.AccentText);
                x += s + NocturneScale.S(8);
            }

            using (Font f = NocturneFonts.RowMedium())
                NocturneDraw.Text(g, _title, f, NocturneTheme.Text,
                    new RectangleF(x, y, Math.Max(0, Width - x - p.Right), line), NocturneDraw.Left);

            if (!string.IsNullOrEmpty(_note))
            {
                using (Font f = NocturneFonts.Tip())
                    NocturneDraw.Text(g, _note, f, NocturneTheme.TextFaint,
                        new RectangleF(p.Left, y + line, Math.Max(0, Width - p.Left - p.Right),
                                       NocturneScale.S(17)), NocturneDraw.Left);
            }
        }
    }
}
