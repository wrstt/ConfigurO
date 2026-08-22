using System;
using System.Drawing;
using System.Windows.Forms;

namespace ConfigurO
{
    /// <summary>
    /// Nocturne text input: surface fill, divider border, radius 8, 36px tall,
    /// accent caret, and an accent border plus 2px focus ring when active --
    /// the default WinForms focus visuals are never left in place.
    ///
    /// A real <see cref="TextBox"/> is hosted inside a painted frame; that
    /// keeps IME, selection, undo and clipboard behaviour intact instead of
    /// reimplementing an editor.
    /// </summary>
    internal sealed class NTextBox : NPanel
    {
        readonly TextBox _inner = new TextBox();
        string _placeholder = string.Empty;
        string _icon;
        bool _hover;

        internal NTextBox()
        {
            Height = NocturneScale.S(NocturneTheme.InputHeight);
            BackColor = Color.Transparent;

            _inner.BorderStyle = BorderStyle.None;
            _inner.BackColor = NocturneTheme.Surface;
            _inner.ForeColor = NocturneTheme.Text;
            _inner.Font = NocturneFonts.Row();
            _inner.GotFocus += (s, e) => Invalidate();
            _inner.LostFocus += (s, e) => Invalidate();
            _inner.TextChanged += (s, e) => { Invalidate(); OnTextChanged(EventArgs.Empty); };
            _inner.KeyDown += (s, e) => { EventHandler<KeyEventArgs> h = KeyDownInner; if (h != null) h(this, e); };
            Controls.Add(_inner);
        }

        internal event EventHandler<KeyEventArgs> KeyDownInner;

        internal TextBox Inner { get { return _inner; } }

        public override string Text
        {
            get { return _inner.Text; }
            set { _inner.Text = value; }
        }

        /// <summary>Hint shown in faint text while the field is empty.</summary>
        internal string Placeholder
        {
            get { return _placeholder; }
            set { _placeholder = value ?? string.Empty; Invalidate(); }
        }

        /// <summary>Optional leading icon name from <see cref="NocturneIcons"/>.</summary>
        internal string Icon
        {
            get { return _icon; }
            set { _icon = value; PerformLayout(); Invalidate(); }
        }

        internal bool Monospace
        {
            set { _inner.Font = value ? NocturneFonts.Code() : NocturneFonts.Row(); }
        }

        internal new void Focus() { _inner.Focus(); }
        internal void SelectAll() { _inner.SelectAll(); }

        protected override void OnThemeChanged()
        {
            _inner.BackColor = NocturneTheme.Surface;
            _inner.ForeColor = NocturneTheme.Text;
        }

        protected override void OnScaleChanged()
        {
            Height = NocturneScale.S(NocturneTheme.InputHeight);
            _inner.Font = NocturneFonts.Row();
        }

        protected override void OnLayout(LayoutEventArgs e)
        {
            base.OnLayout(e);
            int pad = NocturneScale.S(10);
            int iconSpace = string.IsNullOrEmpty(_icon) ? 0 : NocturneScale.S(16) + NocturneScale.S(7);
            int h = _inner.PreferredHeight;
            _inner.SetBounds(pad + iconSpace, Math.Max(0, (Height - h) / 2),
                             Math.Max(0, Width - pad * 2 - iconSpace), h);
        }

        protected override void OnMouseEnter(EventArgs e) { _hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { _hover = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnMouseDown(MouseEventArgs e) { _inner.Focus(); base.OnMouseDown(e); }

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

            bool focused = _inner.Focused;
            Rectangle r = new Rectangle(0, 0, Width, Height);
            Color border = focused ? NocturneTheme.Accent
                         : _hover ? NocturneTheme.BorderStrong
                         : NocturneTheme.Divider;

            NocturneDraw.Card(g, r, NocturneTheme.Surface, border, NocturneTheme.RadiusMd);
            if (focused) NocturneDraw.FocusRing(g, new Rectangle(0, 0, Width - 1, Height - 1), NocturneTheme.RadiusMd);

            if (!string.IsNullOrEmpty(_icon) && NocturneIcons.Exists(_icon))
            {
                int s = NocturneScale.S(16);
                NocturneIcons.Draw(g, _icon, NocturneScale.S(10), (Height - s) / 2, s, NocturneTheme.TextFaint);
            }

            if (string.IsNullOrEmpty(_inner.Text) && !string.IsNullOrEmpty(_placeholder))
            {
                using (Font f = NocturneFonts.Row())
                    NocturneDraw.Text(g, _placeholder, f, NocturneTheme.TextDim,
                        new RectangleF(_inner.Left, 0, Math.Max(0, _inner.Width), Height), NocturneDraw.Left);
            }
        }
    }
}
