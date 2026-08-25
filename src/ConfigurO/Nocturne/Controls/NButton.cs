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
        bool _pressed;
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

        // Enter is handled entirely by NControl, which eases HoverAmount.
        protected override void OnMouseLeave(EventArgs e)
        {
            // NControl unwinds the easing; this clears the logical flag the
            // keyboard path guards on.
            _pressed = false;
            base.OnMouseLeave(e);
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            // Clicking is not navigating: a ring here answers a question nobody
            // asked, and it is the one that appears on launch.
            if (NocturneDraw.ShowFocusRings)
            {
                NocturneDraw.ShowFocusRings = false;
                Parent?.Invalidate(true);
            }
            _pressed = true; Focus(); Invalidate(); base.OnMouseDown(e);
        }
        protected override void OnMouseUp(MouseEventArgs e) { _pressed = false; base.OnMouseUp(e); }
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
                SetPressed(true);
                e.Handled = true;
            }
            base.OnKeyDown(e);
        }

        protected override void OnKeyUp(KeyEventArgs e)
        {
            if (_pressed && (e.KeyCode == Keys.Space || e.KeyCode == Keys.Enter))
            {
                _pressed = false;
                SetPressed(false);
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
            else
            {
                // Hover and press used to be two discrete fills swapped in by
                // an if/else. They are now one colour faded up from nothing,
                // so the button arrives and leaves instead of blinking.
                //
                // Alpha-blended rather than mixed against a ground colour
                // because these controls are transparent-backed: the parent
                // has already painted whatever sits behind, and at amount 0
                // there must be nothing here at all.
                Color hoverFill = _style == NButtonStyle.Secondary || _style == NButtonStyle.Icon
                                ? NocturneTheme.HoverFill : NocturneTheme.HoverAccent;
                Color pressFill = _style == NButtonStyle.Secondary
                                ? NocturneTheme.Border : NocturneTheme.PressedAccent;

                float h = HoverAmount, p = PressAmount;
                float amount = Math.Max(h, p);
                if (amount > 0.004f)
                {
                    // Press wins as it comes in, so a click deepens the hover
                    // tint rather than replacing it and jumping.
                    Color blended = p > 0f ? NocturneTheme.Mix(pressFill, hoverFill, p) : hoverFill;
                    fill = NocturneTheme.Alpha(blended, amount);
                }
            }

            NocturneDraw.Card(g, r, fill, border, radius);
            if (Focused && Enabled && NocturneDraw.ShowFocusRings)
                NocturneDraw.FocusRing(g, new Rectangle(0, 0, Width - 1, Height - 1), radius);

            int iconSize = EffectiveIconSize;
            bool hasIcon = !string.IsNullOrEmpty(_icon) && NocturneIcons.Exists(_icon);

            if (_style == NButtonStyle.Icon || string.IsNullOrEmpty(Text))
            {
                if (hasIcon) NocturneIcons.DrawCentered(g, _icon, r, iconSize, text);
                return;
            }

            using (Font baseFont = NocturneFonts.Row())
            {
                float pad = NocturneScale.S(4);
                float iconSpace = hasIcon ? iconSize + NocturneScale.S(7) : 0;
                float room = Math.Max(0, Width - pad * 2 - iconSpace);

                // Set smaller until it fits, rather than trimmed until it fits.
                //
                // AutoFit sizes the button from a measurement taken on a scratch
                // surface, and the paint happens on the screen; the two need not
                // agree to the pixel, and when they disagree the label lost its
                // last letters -- "Reinforce policies" became "Reinforce poli…".
                // A word cut off is a defect. A word set a quarter-point smaller
                // is not, and it is still true at any DPI, in any language, and
                // whatever the window has been resized to.
                Font f = baseFont;
                Font shrunk = null;
                float size = baseFont.SizeInPoints;
                while (size > 7.5f && NocturneDraw.Width(g, Text, f) > room)
                {
                    size -= 0.25f;
                    if (shrunk != null) shrunk.Dispose();
                    shrunk = new Font(baseFont.FontFamily, size, baseFont.Style, GraphicsUnit.Point);
                    f = shrunk;
                }

                float textW = NocturneDraw.Width(g, Text, f);
                float x = Math.Max(pad, (Width - (textW + iconSpace)) / 2f);

                if (hasIcon)
                {
                    NocturneIcons.Draw(g, _icon, (int)Math.Round(x), (Height - iconSize) / 2, iconSize, text);
                    x += iconSpace;
                }
                NocturneDraw.Text(g, Text, f, text,
                    new RectangleF(x, 0, Math.Max(0, Width - x - pad), Height), NocturneDraw.Left);

                if (shrunk != null) shrunk.Dispose();
            }
        }
    }
}
