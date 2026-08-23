using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Reflection;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace ConfigurO
{
    /// <summary>
    /// Borderless window base with a hand-drawn title bar.
    ///
    /// Dropping the system frame means re-implementing the things it gave us
    /// for free, and this is where that happens:
    ///  - WM_NCHITTEST returns the resize borders, the caption band and --
    ///    critically -- HTMAXBUTTON over our maximise button, which is what
    ///    makes the Windows 11 Snap Layouts flyout appear on hover;
    ///  - WM_NCCALCSIZE keeps the client area covering the whole window so no
    ///    system caption is drawn over our own;
    ///  - WM_GETMINMAXINFO clamps a maximised window to the *work area* of the
    ///    monitor it is on, so it does not swallow the taskbar;
    ///  - WM_DPICHANGED re-scales the layout when the window crosses displays.
    ///
    /// DWM styling (dark mode, rounded corners, Mica) lives in
    /// <see cref="DwmChrome"/>.
    /// </summary>
    internal class NocturneShell : Form
    {
        // ── Win32 ───────────────────────────────────────────────────────
        const int WM_NCCALCSIZE = 0x0083;
        const int WM_NCHITTEST = 0x0084;
        const int WM_GETMINMAXINFO = 0x0024;
        const int WM_DPICHANGED = 0x02E0;
        const int WM_NCLBUTTONDOWN = 0x00A1;
        const int WM_NCLBUTTONUP = 0x00A2;
        const int WM_SYSCOMMAND = 0x0112;

        const int HTCLIENT = 1, HTCAPTION = 2;
        const int HTLEFT = 10, HTRIGHT = 11, HTTOP = 12, HTTOPLEFT = 13, HTTOPRIGHT = 14;
        const int HTBOTTOM = 15, HTBOTTOMLEFT = 16, HTBOTTOMRIGHT = 17;
        const int HTMINBUTTON = 8, HTMAXBUTTON = 9, HTCLOSE = 20;

        const int MONITOR_DEFAULTTONEAREST = 2;

        [StructLayout(LayoutKind.Sequential)]
        struct RECT { public int Left, Top, Right, Bottom; }

        [StructLayout(LayoutKind.Sequential)]
        struct MONITORINFO
        {
            public int cbSize;
            public RECT rcMonitor;
            public RECT rcWork;
            public int dwFlags;
        }

        [StructLayout(LayoutKind.Sequential)]
        struct POINT { public int x, y; }

        [StructLayout(LayoutKind.Sequential)]
        struct MINMAXINFO
        {
            public POINT ptReserved, ptMaxSize, ptMaxPosition, ptMinTrackSize, ptMaxTrackSize;
        }

        [DllImport("user32.dll")]
        static extern IntPtr MonitorFromWindow(IntPtr hwnd, int flags);

        [DllImport("user32.dll")]
        static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFO lpmi);

        [DllImport("user32.dll")]
        static extern bool ReleaseCapture();

        [DllImport("user32.dll")]
        static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

        // ── State ───────────────────────────────────────────────────────
        protected NTitleBar TitleBar;

        int Border { get { return Math.Max(4, NocturneScale.S(6)); } }

        internal NocturneShell()
        {
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.CenterScreen;
            DoubleBuffered = true;
            SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
            BackColor = NocturneTheme.Bg;
            MinimumSize = NocturneScale.S(NocturneTheme.WindowMinimumSize);
            KeyPreview = true;
            Icon = AppIcon();

            NocturneTheme.Changed += OnThemeChanged;
            NocturneWheelRouter.Install();
        }

        /// <summary>
        /// The application icon, for the taskbar button, Alt-Tab and the window
        /// menu.
        ///
        /// ApplicationIcon in the project file only sets the icon Explorer
        /// shows on the .exe; a Form still shows the generic WinForms icon
        /// unless it is given one. The shell was never given one, so the app
        /// looked unbranded everywhere Windows represents a running window --
        /// which is most of the places a user sees it.
        /// </summary>
        static Icon AppIcon()
        {
            try
            {
                Assembly asm = Assembly.GetExecutingAssembly();
                string name = asm.GetManifestResourceNames()
                                 .FirstOrDefault(n => n.EndsWith(".ico", StringComparison.OrdinalIgnoreCase));
                if (name != null)
                    using (Stream s = asm.GetManifestResourceStream(name))
                        if (s != null) return new Icon(s);
            }
            catch (Exception ex) { Logger.LogError("NocturneShell.AppIcon", ex.Message, ex.StackTrace); }

            // Falls back to the icon on the running executable, which is the
            // same artwork by another route.
            try { return Icon.ExtractAssociatedIcon(Application.ExecutablePath); }
            catch { return null; }
        }

        protected void InstallTitleBar(NTitleBar bar)
        {
            TitleBar = bar;
            // Positioned by hand in OnLayout. Docking several controls in one
            // container makes the result depend on z-order, which is exactly
            // the kind of thing that silently breaks when a control is added
            // somewhere new.
            TitleBar.Dock = DockStyle.None;
            TitleBar.MinimizeClicked += (s, e) => WindowState = FormWindowState.Minimized;
            TitleBar.MaximizeClicked += (s, e) => ToggleMaximize();
            TitleBar.CloseClicked += (s, e) => Close();
            Controls.Add(TitleBar);
        }

        protected void ToggleMaximize()
        {
            WindowState = WindowState == FormWindowState.Maximized
                        ? FormWindowState.Normal
                        : FormWindowState.Maximized;
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);

            // The window is built before it has a handle, so every size so far
            // was computed at 96 DPI. Now that the target display is known,
            // rescale before the first paint.
            int previous = (int)Math.Round(NocturneScale.Factor * 96f);
            NocturneScale.SetDpi(DeviceDpi);
            if (DeviceDpi != previous)
            {
                MinimumSize = NocturneScale.S(NocturneTheme.WindowMinimumSize);
                if (WindowState == FormWindowState.Normal)
                    ClientSize = NocturneScale.S(NocturneTheme.WindowDefaultSize);
            }

            ApplyChrome();
        }

        protected virtual void ApplyChrome()
        {
            DwmChrome.Apply(this, OptionsHelper.CurrentOptions != null && OptionsHelper.CurrentOptions.UseMica);
        }

        void OnThemeChanged(object sender, EventArgs e)
        {
            if (IsDisposed) return;
            BackColor = NocturneTheme.Bg;
            ApplyChrome();
            Invalidate(true);
        }

        /// <summary>
        /// Repaints the title bar once the window is actually on screen.
        ///
        /// The bar was reported blank on first show -- the app name, mark and
        /// window buttons only appeared once the pointer crossed it, which is
        /// the hover handler invalidating a region that never received its
        /// first paint. Render() fills its whole client rectangle opaquely, so
        /// there is nothing wrong with what it draws; it simply had not been
        /// asked to. Forcing one paint here is cheap and cannot make a
        /// correctly-painted bar wrong.
        /// </summary>
        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            if (TitleBar == null) return;
            TitleBar.Invalidate();
            TitleBar.Update();
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            // The moment someone reaches for the keyboard to move around, focus
            // rings become useful and are switched on for the rest of the run.
            Keys key = keyData & Keys.KeyCode;
            if (key == Keys.Tab || key == Keys.Up || key == Keys.Down ||
                key == Keys.Left || key == Keys.Right)
            {
                if (!NocturneDraw.ShowFocusRings)
                {
                    NocturneDraw.ShowFocusRings = true;
                    Invalidate(true);
                }
            }
            return base.ProcessCmdKey(ref msg, keyData);
        }

        protected override void OnActivated(EventArgs e)
        {
            base.OnActivated(e);
            if (TitleBar != null) TitleBar.Invalidate();
        }

        /// <summary>
        /// Deactivating repaints the bar too, and this is the half that was
        /// missing.
        ///
        /// Losing focus left the bar blank -- no brand mark, no version, no OS
        /// string, no window buttons -- and it stayed that way until the window
        /// was clicked again, because only OnActivated ever asked for a repaint.
        /// It looked like the bar had never drawn at all, and it is why every
        /// screenshot of it came back empty: taking a screenshot moves focus to
        /// the capture tool, so the bar was blank in the picture and correct on
        /// the screen the moment the picture was taken.
        /// </summary>
        protected override void OnDeactivate(EventArgs e)
        {
            base.OnDeactivate(e);
            if (TitleBar == null) return;
            TitleBar.Invalidate();
            TitleBar.Update();      // paint now, not whenever focus comes back
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            if (TitleBar != null)
            {
                TitleBar.IsMaximized = WindowState == FormWindowState.Maximized;
                TitleBar.Invalidate();
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                NocturneTheme.Changed -= OnThemeChanged;
                NocturneWheelRouter.Uninstall();
            }
            base.Dispose(disposing);
        }

        protected override void WndProc(ref Message m)
        {
            switch (m.Msg)
            {
                case WM_NCCALCSIZE:
                    // Client area == window area: no system caption, no frame.
                    if (m.WParam != IntPtr.Zero) { m.Result = IntPtr.Zero; return; }
                    break;

                case WM_NCHITTEST:
                    m.Result = (IntPtr)HitTest(m.LParam);
                    return;

                case WM_NCLBUTTONDOWN:
                {
                    int hit = m.WParam.ToInt32();
                    if (hit == HTMINBUTTON || hit == HTMAXBUTTON || hit == HTCLOSE)
                    {
                        // Swallow the press so Windows does not draw its own
                        // pressed state over our button; we act on the release.
                        m.Result = IntPtr.Zero;
                        return;
                    }
                    break;
                }

                case WM_NCLBUTTONUP:
                {
                    int hit = m.WParam.ToInt32();
                    if (hit == HTMINBUTTON) { WindowState = FormWindowState.Minimized; m.Result = IntPtr.Zero; return; }
                    if (hit == HTMAXBUTTON) { ToggleMaximize(); m.Result = IntPtr.Zero; return; }
                    if (hit == HTCLOSE) { Close(); m.Result = IntPtr.Zero; return; }
                    break;
                }

                case WM_GETMINMAXINFO:
                    ClampToWorkArea(ref m);
                    return;

                case WM_DPICHANGED:
                {
                    int dpi = m.WParam.ToInt32() & 0xFFFF;
                    NocturneScale.SetDpi(dpi);
                    RECT suggested = (RECT)Marshal.PtrToStructure(m.LParam, typeof(RECT));
                    SetBounds(suggested.Left, suggested.Top,
                              suggested.Right - suggested.Left,
                              suggested.Bottom - suggested.Top);
                    MinimumSize = NocturneScale.S(NocturneTheme.WindowMinimumSize);
                    PerformLayout();
                    Invalidate(true);
                    m.Result = IntPtr.Zero;
                    return;
                }
            }
            base.WndProc(ref m);
        }

        /// <summary>
        /// Maps a screen point to a window region. Order matters: resize edges
        /// win over the caption, and the caption's own buttons win over drag.
        /// </summary>
        int HitTest(IntPtr lParam)
        {
            Point screen = new Point(unchecked((short)(long)lParam), unchecked((short)((long)lParam >> 16)));
            Point p = PointToClient(screen);

            if (WindowState != FormWindowState.Maximized)
            {
                int b = Border;
                bool left = p.X <= b, right = p.X >= ClientSize.Width - b;
                bool top = p.Y <= b, bottom = p.Y >= ClientSize.Height - b;

                if (top && left) return HTTOPLEFT;
                if (top && right) return HTTOPRIGHT;
                if (bottom && left) return HTBOTTOMLEFT;
                if (bottom && right) return HTBOTTOMRIGHT;
                if (left) return HTLEFT;
                if (right) return HTRIGHT;
                if (top) return HTTOP;
                if (bottom) return HTBOTTOM;
            }

            if (TitleBar == null || p.Y >= TitleBar.Height) return HTCLIENT;

            switch (TitleBar.HitTest(TitleBar.PointToClient(screen)))
            {
                case NTitleBar.Hit.Close: return HTCLOSE;
                // Reporting HTMAXBUTTON is what summons the Snap Layouts flyout.
                case NTitleBar.Hit.Maximize: return HTMAXBUTTON;
                case NTitleBar.Hit.Minimize: return HTMINBUTTON;
                // The theme switch is ours; keep it a client-area click.
                case NTitleBar.Hit.Theme: return HTCLIENT;
                default: return HTCAPTION;
            }
        }

        /// <summary>
        /// Without this a borderless maximised window covers the taskbar,
        /// because Windows sizes it to the whole monitor rather than the work
        /// area once the frame is gone.
        /// </summary>
        void ClampToWorkArea(ref Message m)
        {
            MINMAXINFO info = (MINMAXINFO)Marshal.PtrToStructure(m.LParam, typeof(MINMAXINFO));
            IntPtr monitor = MonitorFromWindow(Handle, MONITOR_DEFAULTTONEAREST);
            if (monitor != IntPtr.Zero)
            {
                MONITORINFO mi = new MONITORINFO();
                mi.cbSize = Marshal.SizeOf(typeof(MONITORINFO));
                if (GetMonitorInfo(monitor, ref mi))
                {
                    info.ptMaxPosition.x = mi.rcWork.Left - mi.rcMonitor.Left;
                    info.ptMaxPosition.y = mi.rcWork.Top - mi.rcMonitor.Top;
                    info.ptMaxSize.x = mi.rcWork.Right - mi.rcWork.Left;
                    info.ptMaxSize.y = mi.rcWork.Bottom - mi.rcWork.Top;
                }
            }
            info.ptMinTrackSize.x = MinimumSize.Width;
            info.ptMinTrackSize.y = MinimumSize.Height;
            Marshal.StructureToPtr(info, m.LParam, false);
            m.Result = IntPtr.Zero;
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
            using (SolidBrush b = new SolidBrush(NocturneTheme.Bg))
                e.Graphics.FillRectangle(b, ClientRectangle);

            // Windows 11 rounds and outlines the window for us; older versions
            // get a hairline so the app still reads as a distinct surface.
            if (WindowsRelease.SupportsRoundedCorners || WindowState == FormWindowState.Maximized) return;
            using (Pen p = new Pen(NocturneTheme.Neutral500))
                e.Graphics.DrawRectangle(p, 0, 0, Width - 1, Height - 1);
        }

        /// <summary>Shows a transient confirmation. Overridden by the main shell.</summary>
        internal virtual void Toast(string message) { }
    }
}
