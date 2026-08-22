using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace ConfigurO
{
    /// <summary>
    /// Base for the ten tool screens.
    ///
    /// Provides the header the handoff specifies for every screen -- 21px title,
    /// 13px subtitle, right-aligned action controls -- plus an optional restart
    /// banner and a scrolling body. Subclasses fill <see cref="Body"/> in
    /// <see cref="Build"/> and re-layout in <see cref="Relayout"/>.
    /// </summary>
    internal abstract class NScreen : NPanel
    {
        readonly NScrollPanel _scroll = new NScrollPanel();
        readonly NBanner _banner = new NBanner();
        readonly List<Control> _actions = new List<Control>();

        bool _built;
        int _actionsLeft = int.MaxValue;

        protected NScreen()
        {
            Dock = DockStyle.Fill;
            BackColor = Color.Transparent;

            _banner.ActionClicked += (s, e) => OnBannerAction();
            Controls.Add(_scroll);
            Controls.Add(_banner);
        }

        /// <summary>Sidebar id this screen is reached by.</summary>
        internal abstract string Id { get; }

        internal abstract string Icon { get; }

        /// <summary>Sidebar label.</summary>
        internal abstract string NavLabel { get; }

        protected string TitleText = string.Empty;
        protected string SubtitleText = string.Empty;

        /// <summary>
        /// Shown centred in the body while a screen has nothing to display.
        /// Without it a slow WMI sweep or an unreachable feed just leaves a
        /// blank pane with no explanation.
        /// </summary>
        protected string EmptyMessage;

        /// <summary>Icon drawn above <see cref="EmptyMessage"/>.</summary>
        protected string EmptyIcon;

        /// <summary>Sets the empty state and repaints the body.</summary>
        protected void SetEmpty(string message, string icon = null)
        {
            if (EmptyMessage == message && EmptyIcon == icon) return;
            EmptyMessage = message;
            EmptyIcon = icon;
            Invalidate();          // the message is painted by the screen itself
        }

        /// <summary>Add children here. Its Height drives scrolling.</summary>
        protected NPanel Body { get { return _scroll.Content; } }

        protected NScrollPanel ScrollHost { get { return _scroll; } }

        internal NBanner Banner { get { return _banner; } }

        /// <summary>
        /// Marshals <paramref name="action"/> onto the UI thread, doing nothing
        /// if the screen has gone away first. Background work outliving its
        /// screen is normal here -- a WMI sweep or a download can finish long
        /// after the user has navigated on -- and BeginInvoke on a control
        /// with no handle throws rather than no-oping.
        /// </summary>
        protected void OnUi(Action action)
        {
            if (action == null || IsDisposed || !IsHandleCreated) return;
            try { BeginInvoke(action); }
            catch (ObjectDisposedException) { }
            catch (InvalidOperationException) { }   // handle destroyed mid-call
        }

        /// <summary>Shows a toast on the shell.</summary>
        protected void Toast(string message)
        {
            NocturneShell shell = FindForm() as NocturneShell;
            if (shell != null) shell.Toast(message);
        }

        protected int Pad { get { return NocturneScale.S(32); } }
        protected int TopPad { get { return NocturneScale.S(26); } }
        int HeaderHeight { get { return NocturneScale.S(58); } }

        /// <summary>Adds a control to the right side of the header row.</summary>
        protected void AddAction(Control c)
        {
            // Header controls are positioned right-to-left from the edge, so
            // they must know their own width; nothing else sizes them.
            NButton b = c as NButton;
            if (b != null)
            {
                b.AutoWidth = true;
                b.AutoFit();
                b.Height = NocturneScale.S(34);
            }
            _actions.Add(c);
            Controls.Add(c);
            c.BringToFront();
        }

        /// <summary>Builds the screen the first time it is shown.</summary>
        internal void EnsureBuilt()
        {
            if (_built) return;
            _built = true;
            Build();
            PerformLayout();
        }

        /// <summary>Create child controls here; called once, lazily.</summary>
        protected abstract void Build();

        /// <summary>Position children inside <see cref="Body"/>; called on every resize.</summary>
        protected abstract void Relayout();

        /// <summary>Refresh data from the helpers; called each time the screen is shown.</summary>
        internal virtual void Activate() { }

        /// <summary>Called when the screen is navigated away from.</summary>
        internal virtual void Deactivate() { }

        protected virtual void OnBannerAction() { }

        protected override void OnLayout(LayoutEventArgs e)
        {
            base.OnLayout(e);
            if (!_built) return;

            int y = TopPad;
            int right = Width - Pad;

            // Header actions, laid out right-to-left in reverse add order.
            for (int i = _actions.Count - 1; i >= 0; i--)
            {
                Control c = _actions[i];
                if (!c.Visible) continue;
                c.Left = right - c.Width;
                c.Top = y + (NocturneScale.S(30) - c.Height) / 2;
                right = c.Left - NocturneScale.S(8);
            }
            // Remember where they end so the title can be trimmed to fit
            // rather than sliding underneath them on a narrow window.
            _actionsLeft = right;

            y += HeaderHeight;

            if (_banner.Visible)
            {
                _banner.SetBounds(Pad, y, Math.Max(0, Width - Pad * 2), NocturneScale.S(38));
                y += _banner.Height + NocturneScale.S(14);
            }

            _scroll.SetBounds(0, y, Width, Math.Max(0, Height - y - NocturneScale.S(20)));
            _scroll.Content.Width = Width;
            Relayout();
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

            using (SolidBrush b = new SolidBrush(NocturneTheme.Bg))
                g.FillRectangle(b, ClientRectangle);

            int titleRight = Math.Min(_actionsLeft, Width - Pad);
            int maxWidth = Math.Max(0, titleRight - Pad - NocturneScale.S(12));

            using (Font f = NocturneFonts.ScreenTitle())
                NocturneDraw.Text(g, TitleText, f, NocturneTheme.Text,
                    new RectangleF(Pad, TopPad, maxWidth, NocturneScale.S(28)), NocturneDraw.Left);

            using (Font f = NocturneFonts.ScreenSubtitle())
                NocturneDraw.Text(g, SubtitleText, f, NocturneTheme.TextMuted,
                    new RectangleF(Pad, TopPad + NocturneScale.S(27), maxWidth, NocturneScale.S(18)),
                    NocturneDraw.Left);

            if (string.IsNullOrEmpty(EmptyMessage)) return;

            Rectangle area = _scroll.Bounds;
            int cy = area.Y + area.Height / 3;
            if (!string.IsNullOrEmpty(EmptyIcon) && NocturneIcons.Exists(EmptyIcon))
            {
                int size = NocturneScale.S(34);
                NocturneIcons.Draw(g, EmptyIcon, (Width - size) / 2, cy - size - NocturneScale.S(12),
                                   size, NocturneTheme.TextDim);
            }
            using (Font f = NocturneFonts.ScreenSubtitle())
                NocturneDraw.Text(g, EmptyMessage, f, NocturneTheme.TextFaint,
                    new RectangleF(Pad, cy, Math.Max(0, Width - Pad * 2), NocturneScale.S(22)),
                    NocturneDraw.Center);
        }

        /// <summary>Repaints the header after Title/Subtitle change.</summary>
        protected void RefreshHeader()
        {
            Invalidate(new Rectangle(0, 0, Width, TopPad + HeaderHeight));
        }
    }
}
