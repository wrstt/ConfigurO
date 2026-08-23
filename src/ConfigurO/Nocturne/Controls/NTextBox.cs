using System;
using System.Drawing;
using System.Runtime.InteropServices;
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

        // The hint has to be set on the inner edit control, not painted by the
        // frame around it. The frame paints first and the edit control is an
        // opaque child sitting exactly where the text goes, so anything drawn
        // here was covered the moment the child painted -- for eight fields
        // across four screens the hint has never once been visible, and all
        // that ever showed was the descender of one glyph poking out below the
        // child's bottom edge. EM_SETCUEBANNER hands it to the edit control
        // itself, which is also what keeps it correct through focus, IME and
        // right-to-left without any of it being reimplemented here.
        const int EM_SETCUEBANNER = 0x1501;

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, string lParam);

        /// <summary>Hint shown in faint text while the field is empty.</summary>
        internal string Placeholder
        {
            get { return _placeholder; }
            set { _placeholder = value ?? string.Empty; ApplyPlaceholder(); Invalidate(); }
        }

        /// <summary>
        /// Pushes the hint into the edit control. Safe to call before the
        /// handle exists and safe to call twice; the handle is recreated
        /// whenever the font changes, which drops the banner, so every path
        /// that touches the font calls this again.
        /// </summary>
        void ApplyPlaceholder()
        {
            if (!_inner.IsHandleCreated) return;
            try
            {
                // wParam 1 keeps the hint up while the field has focus and is
                // still empty, rather than clearing it the moment it is clicked.
                SendMessage(_inner.Handle, EM_SETCUEBANNER, (IntPtr)1, _placeholder);
            }
            catch (Exception ex) { Logger.LogError("NTextBox.ApplyPlaceholder", ex.Message, ex.StackTrace); }
        }

        /// <summary>Optional leading icon name from <see cref="NocturneIcons"/>.</summary>
        internal string Icon
        {
            get { return _icon; }
            set { _icon = value; PerformLayout(); Invalidate(); }
        }

        internal bool Monospace
        {
            set { _inner.Font = value ? NocturneFonts.Code() : NocturneFonts.Row(); ApplyPlaceholder(); }
        }

        internal new void Focus() { _inner.Focus(); }
        internal void SelectAll() { _inner.SelectAll(); }

        protected override void OnCreateControl()
        {
            base.OnCreateControl();
            ApplyPlaceholder();
        }

        protected override void OnThemeChanged()
        {
            _inner.BackColor = NocturneTheme.Surface;
            _inner.ForeColor = NocturneTheme.Text;
        }

        protected override void OnScaleChanged()
        {
            Height = NocturneScale.S(NocturneTheme.InputHeight);
            _inner.Font = NocturneFonts.Row();
            ApplyPlaceholder();
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

        }
    }
}
