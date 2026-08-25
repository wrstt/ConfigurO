using System;
using System.Drawing;
using System.Windows.Forms;

namespace ConfigurO
{
    /// <summary>
    /// Base for every hand-painted Nocturne control.
    ///
    /// Handles the three things all of them need: flicker-free painting, a
    /// repaint when the theme flips, and a re-layout when the window moves to a
    /// display with a different DPI. Both subscriptions are released on dispose
    /// -- the theme events are static, so a control that forgot would keep its
    /// whole screen alive.
    /// </summary>
    internal class NControl : Control
    {
        bool _subscribed;

        readonly NAnim _hoverAnim;
        readonly NAnim _pressAnim;

        internal NControl()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw |
                     ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.Transparent;
            ForeColor = NocturneTheme.Text;

            // Press settles faster than hover: a click wants to feel immediate,
            // where a pointer crossing a surface wants to feel unhurried.
            _hoverAnim = new NAnim(Invalidate, 130);
            _pressAnim = new NAnim(Invalidate, 90);

            NocturneTheme.Changed += OnThemeChangedInternal;
            NocturneScale.Changed += OnScaleChangedInternal;
            _subscribed = true;
        }

        // ── Eased interaction state ─────────────────────────────────────
        // Painters read these instead of a bool. At 0 the control is at rest,
        // at 1 it is fully hovered or fully pressed, and in between it is on
        // its way -- which is the whole difference between a surface that
        // responds and one that switches.

        /// <summary>0 at rest, 1 hovered, eased in between.</summary>
        protected float HoverAmount { get { return _hoverAnim.Value; } }

        /// <summary>0 at rest, 1 pressed, eased in between.</summary>
        protected float PressAmount { get { return _pressAnim.Value; } }

        /// <summary>Drives the hover easing. Called for you on enter and leave.</summary>
        protected void SetHover(bool on) { _hoverAnim.To(on ? 1f : 0f); }

        /// <summary>
        /// Drives the press easing. The mouse path calls this for you; keyboard
        /// activation has to say so itself, since Space and Enter produce no
        /// mouse events.
        /// </summary>
        protected void SetPressed(bool on) { _pressAnim.To(on ? 1f : 0f); }

        protected override void OnMouseEnter(EventArgs e) { SetHover(true); base.OnMouseEnter(e); }

        protected override void OnMouseLeave(EventArgs e)
        {
            // A pointer that leaves mid-press must not leave the press behind:
            // the mouse-up will land somewhere else and never come back here.
            SetHover(false);
            SetPressed(false);
            base.OnMouseLeave(e);
        }

        protected override void OnMouseDown(MouseEventArgs e) { SetPressed(true); base.OnMouseDown(e); }
        protected override void OnMouseUp(MouseEventArgs e) { SetPressed(false); base.OnMouseUp(e); }

        protected override void OnEnabledChanged(EventArgs e)
        {
            // A control disabled under the pointer keeps its hover for as long
            // as the pointer stays, and comes back wearing it.
            if (!Enabled) { _hoverAnim.Set(0f); _pressAnim.Set(0f); }
            base.OnEnabledChanged(e);
        }

        void OnThemeChangedInternal(object sender, EventArgs e)
        {
            if (IsDisposed) return;
            OnThemeChanged();
            Invalidate();
        }

        void OnScaleChangedInternal(object sender, EventArgs e)
        {
            if (IsDisposed) return;
            OnScaleChanged();
            Invalidate();
        }

        /// <summary>Called before the repaint that follows a Dark/Light switch.</summary>
        protected virtual void OnThemeChanged() { }

        /// <summary>Called when the display scale changes; re-measure here.</summary>
        protected virtual void OnScaleChanged() { }

        protected override void Dispose(bool disposing)
        {
            if (disposing && _subscribed)
            {
                NocturneTheme.Changed -= OnThemeChangedInternal;
                NocturneScale.Changed -= OnScaleChangedInternal;
                _hoverAnim.Dispose();
                _pressAnim.Dispose();
                _subscribed = false;
            }
            base.Dispose(disposing);
        }
    }

    /// <summary>Container flavour of <see cref="NControl"/> for panels that host children.</summary>
    internal class NPanel : Panel
    {
        bool _subscribed;

        readonly NAnim _hoverAnim;
        readonly NAnim _pressAnim;

        internal NPanel()
        {
            SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);

            // Mirrors NControl. Most panels are containers that never drive
            // these, and an NAnim that is never told to move never starts its
            // timer, so the pair costs a couple of managed objects and nothing
            // else -- a WinForms Timer does not touch the OS until Start.
            _hoverAnim = new NAnim(Invalidate, 130);
            _pressAnim = new NAnim(Invalidate, 90);

            NocturneTheme.Changed += OnThemeChangedInternal;
            NocturneScale.Changed += OnScaleChangedInternal;
            _subscribed = true;
        }

        /// <summary>0 at rest, 1 hovered, eased in between.</summary>
        protected float HoverAmount { get { return _hoverAnim.Value; } }

        /// <summary>0 at rest, 1 pressed, eased in between.</summary>
        protected float PressAmount { get { return _pressAnim.Value; } }

        protected void SetHover(bool on) { _hoverAnim.To(on ? 1f : 0f); }
        protected void SetPressed(bool on) { _pressAnim.To(on ? 1f : 0f); }

        protected override void OnMouseEnter(EventArgs e) { SetHover(true); base.OnMouseEnter(e); }

        protected override void OnMouseLeave(EventArgs e)
        {
            SetHover(false);
            SetPressed(false);
            base.OnMouseLeave(e);
        }

        protected override void OnMouseDown(MouseEventArgs e) { SetPressed(true); base.OnMouseDown(e); }
        protected override void OnMouseUp(MouseEventArgs e) { SetPressed(false); base.OnMouseUp(e); }

        void OnThemeChangedInternal(object sender, EventArgs e)
        {
            if (IsDisposed) return;
            OnThemeChanged();
            Invalidate(true);
        }

        void OnScaleChangedInternal(object sender, EventArgs e)
        {
            if (IsDisposed) return;
            OnScaleChanged();
            Invalidate(true);
        }

        protected virtual void OnThemeChanged() { }
        protected virtual void OnScaleChanged() { }

        protected override void Dispose(bool disposing)
        {
            if (disposing && _subscribed)
            {
                NocturneTheme.Changed -= OnThemeChangedInternal;
                NocturneScale.Changed -= OnScaleChangedInternal;
                _hoverAnim.Dispose();
                _pressAnim.Dispose();
                _subscribed = false;
            }
            base.Dispose(disposing);
        }
    }
}
