using System;
using System.Drawing;
using System.Windows.Forms;

namespace ConfigurO
{
    /// <summary>
    /// Buttons in Nocturne are outlined, never filled.
    /// </summary>
    internal enum NButtonStyle
    {
        /// <summary>1px accent border, accent text, transparent fill.</summary>
        Primary,
        /// <summary>Divider border, body-coloured text.</summary>
        Secondary,
        /// <summary>Accent text, no border.</summary>
        Ghost,
        /// <summary>Pill-shaped filter chip; the active one uses Primary styling.</summary>
        Pill,
        /// <summary>36x36 square with an icon and no label.</summary>
        Icon
    }

    /// <summary>
    /// The Nocturne button. Hover tints the fill with accent@12%, press with
    /// accent@22%; keyboard focus draws the 2px accent ring. An optional icon
    /// sits before the label.
    /// </summary>
    internal sealed class NButton : NControl, IButtonControl
    {
        bool _hover, _pressed;
        string _icon;
        NButtonStyle _style = NButtonStyle.Secondary;
        bool _active;

        internal NButton()
        {
            Cursor = Cursors.Hand;
            TabStop = true;
            Size = DefaultSize;
        }

        /// <summary>
        /// Control's own default is 0x0, which would make an unsized button
        /// invisible rather than merely wrong.
        /// </summary>
        protected override Size DefaultSize
        {
            get { return new Size(NocturneScale.S(110), NocturneScale.S(32)); }
        }

        /// <summary>
        /// Re-measures whenever the label changes. Set by
        /// <see cref="NScreen.AddAction"/> for header buttons, which nothing
        /// else lays out; buttons positioned by a screen's Relayout leave this
        /// off so their explicit bounds are not fought over.
        /// </summary>
        internal bool AutoWidth { get; set; }

        protected override void OnTextChanged(EventArgs e)
        {
            base.OnTextChanged(e);
            if (AutoWidth) AutoFit();
        }

        internal NButtonStyle Style
        {
            get { return _style; }
            set
            {
                _style = value;
                if (value == NButtonStyle.Icon) Size = new Size(NocturneScale.S(36), NocturneScale.S(36));
                else if (AutoWidth) AutoFit();
                Invalidate();
            }
        }

        /// <summary>Icon name from <see cref="NocturneIcons"/>, or null.</summary>
        internal string Icon
        {
            get { return _icon; }
            set { _icon = value; if (AutoWidth) AutoFit(); Invalidate(); }
        }

        internal int IconSize { get; set; }

        /// <summary>For <see cref="NButtonStyle.Pill"/>: renders as the selected filter.</summary>
        internal bool Active
        {
            get { return _active; }
            set { if (_active == value) return; _active = value; Invalidate(); }
        }

        /// <summary>Sizes the button to its content plus the standard padding.</summary>
        internal void AutoFit(int horizontalPadding = 14)
        {
            if (_style == NButtonStyle.Icon) { Size = new Size(NocturneScale.S(36), NocturneScale.S(36)); return; }
            using (Graphics g = NocturneDraw.CreateMeasureGraphics())
            using (Font f = NocturneFonts.Row())
            {
                // Two pixels of slack: measuring happens on a scratch surface
                // and drawing happens on the screen, and grid-fitting does not
                // have to agree between them to the pixel.
                int w = (int)Math.Ceiling(NocturneDraw.Width(g, Text, f)) + NocturneScale.S(2)
                      + NocturneScale.S(horizontalPadding) * 2;
                if (!string.IsNullOrEmpty(_icon)) w += EffectiveIconSize + NocturneScale.S(7);
                Width = w;
            }
        }

        int EffectiveIconSize
        {
            get { return IconSize > 0 ? NocturneScale.S(IconSize) : NocturneScale.S(15); }
        }

        protected override void OnMouseEnter(EventArgs e) { _hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { _hover = _pressed = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnMouseDown(MouseEventArgs e) { _pressed = true; Focus(); Invalidate(); base.OnMouseDown(e); }
        protected override void OnMouseUp(MouseEventArgs e) { _pressed = false; Invalidate(); base.OnMouseUp(e); }
        protected override void OnGotFocus(EventArgs e) { Invalidate(); base.OnGotFocus(e); }
        protected override void OnLostFocus(EventArgs e) { Invalidate(); base.OnLostFocus(e); }

        protected override bool IsInputKey(Keys keyData)
        {
            return keyData == Keys.Space || keyData == Keys.Enter || base.IsInputKey(keyData);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Space || e.KeyCode == Keys.Enter)
            {
                _pressed = true;
                Invalidate();
                e.Handled = true;
            }
            base.OnKeyDown(e);
        }

        protected override void OnKeyUp(KeyEventArgs e)
        {
            if (_pressed && (e.KeyCode == Keys.Space || e.KeyCode == Keys.Enter))
            {
                _pressed = false;
                Invalidate();
                PerformClick();
                e.Handled = true;
            }
            base.OnKeyUp(e);
        }

        internal void PerformClick() { OnClick(EventArgs.Empty); }

        // ── IButtonControl, so these work as dialog default/cancel buttons ──
        public DialogResult DialogResult { get; set; }
        public void NotifyDefault(bool value) { }
        void IButtonControl.PerformClick() { PerformClick(); }

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

            bool primaryLike = _style == NButtonStyle.Primary ||
                               (_style == NButtonStyle.Pill && _active);

            Rectangle r = new Rectangle(0, 0, Width, Height);
            int radius = _style == NButtonStyle.Pill ? Height / 2 : NocturneTheme.RadiusMd;

            Color border, text, fill = Color.Empty;
            if (primaryLike)      { border = NocturneTheme.Accent;  text = NocturneTheme.AccentText; }
            else if (_style == NButtonStyle.Ghost) { border = Color.Empty; text = NocturneTheme.AccentText; }
            else if (_style == NButtonStyle.Icon)  { border = Color.Empty; text = NocturneTheme.SidebarText; }
            else                  { border = NocturneTheme.Divider; text = NocturneTheme.Text; }

            if (!Enabled) { border = NocturneTheme.Border; text = NocturneTheme.TextDim; }
            else if (_pressed) fill = _style == NButtonStyle.Secondary ? NocturneTheme.Border : NocturneTheme.PressedAccent;
            else if (_hover)   fill = _style == NButtonStyle.Secondary || _style == NButtonStyle.Icon
                                      ? NocturneTheme.HoverFill : NocturneTheme.HoverAccent;

            NocturneDraw.Card(g, r, fill, border, radius);
            if (Focused && Enabled) NocturneDraw.FocusRing(g, new Rectangle(0, 0, Width - 1, Height - 1), radius);

            int iconSize = EffectiveIconSize;
            bool hasIcon = !string.IsNullOrEmpty(_icon) && NocturneIcons.Exists(_icon);

            if (_style == NButtonStyle.Icon || string.IsNullOrEmpty(Text))
            {
                if (hasIcon) NocturneIcons.DrawCentered(g, _icon, r, iconSize, text);
                return;
            }

            using (Font f = NocturneFonts.Row())
            {
                float textW = NocturneDraw.Width(g, Text, f);
                float total = textW + (hasIcon ? iconSize + NocturneScale.S(7) : 0);
                float x = (Width - total) / 2f;

                if (hasIcon)
                {
                    NocturneIcons.Draw(g, _icon, (int)Math.Round(x), (Height - iconSize) / 2, iconSize, text);
                    x += iconSize + NocturneScale.S(7);
                }
                // Given every pixel left in the button, not a box cut to the
                // width just measured. AutoFit already sized the button to hold
                // this string with padding either side, so the room is there --
                // but boxing the text to measured+2 means any disagreement
                // between measuring on a scratch surface and drawing on the
                // screen, however small, lands as a trimmed word. "Reinforce
                // policies" came out "Reinforce policie". Slack costs nothing;
                // the ellipsis is meant for text that genuinely does not fit.
                float room = Math.Max(0, Width - x - NocturneScale.S(4));
                NocturneDraw.Text(g, Text, f, text,
                    new RectangleF(x, 0, room, Height), NocturneDraw.Left);
            }
        }
    }
}
