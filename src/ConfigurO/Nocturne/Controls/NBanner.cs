using System;
using System.Drawing;
using System.Windows.Forms;

namespace ConfigurO
{
    /// <summary>
    /// The "restart to finish applying" strip that appears under the screen
    /// header once a tweak needing a reboot has been switched. Clicking the
    /// action raises <see cref="ActionClicked"/>; the X dismisses it.
    /// </summary>
    internal sealed class NBanner : NControl
    {
        bool _hoverAction, _hoverClose;

        internal NBanner()
        {
            Visible = false;
            Height = NocturneScale.S(38);
            Cursor = Cursors.Default;
        }

        internal string ActionText = string.Empty;
        internal string Icon = NocturneIcons.Restart;

        internal event EventHandler ActionClicked;

        internal void Show(string message, string action)
        {
            Text = message;
            ActionText = action ?? string.Empty;
            Visible = true;
            Invalidate();
        }

        // Cached so hit-testing does not need a Graphics of its own; the
        // paint pass is the only thing that can measure text cheaply.
        Rectangle _actionRect = Rectangle.Empty;

        Rectangle ActionRect() { return _actionRect; }

        Rectangle MeasureAction(Graphics g)
        {
            if (string.IsNullOrEmpty(ActionText)) return Rectangle.Empty;
            using (Font f = NocturneFonts.Row())
            {
                int w = (int)Math.Ceiling(NocturneDraw.Width(g, ActionText, f)) + NocturneScale.S(24);
                int h = NocturneScale.S(26);
                return new Rectangle(Width - NocturneScale.S(12) - NocturneScale.S(28) - w,
                                     (Height - h) / 2, w, h);
            }
        }

        Rectangle CloseRect()
        {
            int s = NocturneScale.S(24);
            return new Rectangle(Width - NocturneScale.S(10) - s, (Height - s) / 2, s, s);
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            bool a = ActionRect().Contains(e.Location);
            bool c = CloseRect().Contains(e.Location);
            if (a != _hoverAction || c != _hoverClose)
            {
                _hoverAction = a; _hoverClose = c;
                Cursor = a || c ? Cursors.Hand : Cursors.Default;
                Invalidate();
            }
            base.OnMouseMove(e);
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            _hoverAction = _hoverClose = false;
            Cursor = Cursors.Default;
            Invalidate();
            base.OnMouseLeave(e);
        }

        protected override void OnMouseClick(MouseEventArgs e)
        {
            if (CloseRect().Contains(e.Location)) { Visible = false; }
            else if (ActionRect().Contains(e.Location))
            {
                EventHandler h = ActionClicked;
                if (h != null) h(this, EventArgs.Empty);
            }
            base.OnMouseClick(e);
        }

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

            Rectangle r = new Rectangle(0, 0, Width, Height);
            NocturneDraw.Card(g, r, NocturneTheme.SelectedFill, NocturneTheme.Accent700, NocturneTheme.RadiusMd);

            int pad = NocturneScale.S(12);
            int s = NocturneScale.S(16);
            NocturneIcons.Draw(g, Icon, pad, (Height - s) / 2, s, NocturneTheme.AccentText);

            Rectangle action = _actionRect = MeasureAction(g);
            int textRight = action == Rectangle.Empty ? Width - pad : action.X - NocturneScale.S(8);

            using (Font f = NocturneFonts.Row())
                NocturneDraw.Text(g, Text, f, NocturneTheme.Text,
                    new RectangleF(pad + s + NocturneScale.S(9), 0,
                                   Math.Max(0, textRight - pad - s - NocturneScale.S(9)), Height),
                    NocturneDraw.Left);

            if (action != Rectangle.Empty)
            {
                NocturneDraw.Card(g, action, _hoverAction ? NocturneTheme.HoverAccent : Color.Empty,
                                  NocturneTheme.Accent, NocturneTheme.RadiusMd);
                using (Font f = NocturneFonts.Row())
                    NocturneDraw.Text(g, ActionText, f, NocturneTheme.AccentText, action, NocturneDraw.Center);
            }

            Rectangle close = CloseRect();
            NocturneIcons.DrawCentered(g, NocturneIcons.Close, close, NocturneScale.S(14),
                _hoverClose ? NocturneTheme.Text : NocturneTheme.TextFaint);
        }
    }
}
