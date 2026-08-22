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

        internal NControl()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw |
                     ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.Transparent;
            ForeColor = NocturneTheme.Text;
            NocturneTheme.Changed += OnThemeChangedInternal;
            NocturneScale.Changed += OnScaleChangedInternal;
            _subscribed = true;
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
                _subscribed = false;
            }
            base.Dispose(disposing);
        }
    }

    /// <summary>Container flavour of <see cref="NControl"/> for panels that host children.</summary>
    internal class NPanel : Panel
    {
        bool _subscribed;

        internal NPanel()
        {
            SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
            NocturneTheme.Changed += OnThemeChangedInternal;
            NocturneScale.Changed += OnScaleChangedInternal;
            _subscribed = true;
        }

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
                _subscribed = false;
            }
            base.Dispose(disposing);
        }
    }
}
