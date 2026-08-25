using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace ConfigurO
{
    /// <summary>
    /// DPI scaling for the hand-laid-out Nocturne UI.
    ///
    /// Every size in the design tokens is expressed at 96 DPI; layout code runs
    /// it through <see cref="S(int)"/>. The app declares PerMonitorV2 awareness
    /// in its manifest, so <see cref="Factor"/> is refreshed from the shell
    /// window whenever it moves to a display with a different scale.
    /// </summary>
    internal static class NocturneScale
    {
        const float Base = 96f;

        static float _factor = 1f;

        /// <summary>Raised after <see cref="Factor"/> changes so the shell can re-layout.</summary>
        internal static event EventHandler Changed;

        internal static float Factor
        {
            get { return _factor; }
            private set
            {
                if (Math.Abs(_factor - value) < 0.001f) return;
                _factor = value;
                EventHandler h = Changed;
                if (h != null) h(null, EventArgs.Empty);
            }
        }

        internal static void SetDpi(int dpi)
        {
            if (dpi <= 0) dpi = 96;
            Factor = dpi / Base;
        }

        // ── Asking the system, not the framework ────────────────────────
        const int LOGPIXELSX = 88;
        const int MDT_EFFECTIVE_DPI = 0;
        const int MONITOR_DEFAULTTONEAREST = 2;

        [DllImport("user32.dll")] static extern uint GetDpiForWindow(IntPtr hwnd);
        [DllImport("user32.dll")] static extern IntPtr MonitorFromWindow(IntPtr hwnd, int flags);
        [DllImport("user32.dll")] static extern IntPtr GetDC(IntPtr hwnd);
        [DllImport("user32.dll")] static extern int ReleaseDC(IntPtr hwnd, IntPtr hdc);
        [DllImport("gdi32.dll")] static extern int GetDeviceCaps(IntPtr hdc, int index);
        [DllImport("shcore.dll")] static extern int GetDpiForMonitor(IntPtr monitor, int type, out uint x, out uint y);

        /// <summary>
        /// The DPI of the display <paramref name="hwnd"/> is on, straight from
        /// Win32.
        ///
        /// Deliberately not Control.DeviceDpi. WinForms on .NET Framework only
        /// reports a real DPI when the DpiAwareness switches in App.config are
        /// present, and those live in ConfigurO.exe.config -- a file the
        /// release does not ship, because the app is distributed as one
        /// executable. Run that way, DeviceDpi answers 96 on every machine,
        /// the whole interface lays out at 1.0, and on a 150% display it comes
        /// out two-thirds of the size it was drawn at. It is crisp, because
        /// the manifest still makes the process Per-Monitor-V2 aware at the
        /// Win32 level -- it is simply small, which reads as an older, denser
        /// build of the app rather than as a bug.
        ///
        /// Win32 has always known the right answer; nothing was asking it.
        /// Every version since 1.0 shipped this way.
        ///
        /// Falls back the way the API history goes: GetDpiForWindow is
        /// Windows 10 1607, GetDpiForMonitor is 8.1, and GetDeviceCaps is the
        /// system-wide value that works everywhere else.
        /// </summary>
        internal static int DpiOf(IntPtr hwnd)
        {
            if (hwnd != IntPtr.Zero)
            {
                try
                {
                    uint d = GetDpiForWindow(hwnd);
                    if (d > 0) return (int)d;
                }
                catch (EntryPointNotFoundException) { }   // pre-1607
                catch (DllNotFoundException) { }

                try
                {
                    IntPtr monitor = MonitorFromWindow(hwnd, MONITOR_DEFAULTTONEAREST);
                    uint x, y;
                    if (monitor != IntPtr.Zero &&
                        GetDpiForMonitor(monitor, MDT_EFFECTIVE_DPI, out x, out y) == 0 && x > 0)
                        return (int)x;
                }
                catch (EntryPointNotFoundException) { }   // pre-8.1
                catch (DllNotFoundException) { }
            }

            try
            {
                IntPtr dc = GetDC(IntPtr.Zero);
                if (dc != IntPtr.Zero)
                {
                    int d = GetDeviceCaps(dc, LOGPIXELSX);
                    ReleaseDC(IntPtr.Zero, dc);
                    if (d > 0) return d;
                }
            }
            catch (DllNotFoundException) { }

            return 96;
        }

        /// <summary>Scales a design-token pixel value to the current display.</summary>
        internal static int S(int v)
        {
            return (int)Math.Round(v * _factor, MidpointRounding.AwayFromZero);
        }

        internal static float Sf(float v) { return v * _factor; }

        internal static Size S(Size s) { return new Size(S(s.Width), S(s.Height)); }

        internal static Padding S(Padding p)
        {
            return new Padding(S(p.Left), S(p.Top), S(p.Right), S(p.Bottom));
        }

        /// <summary>
        /// Point sizes are already DPI-relative in GDI+, so type does NOT get
        /// multiplied by <see cref="Factor"/>; this exists so callers can be
        /// explicit about that and never accidentally double-scale text.
        /// </summary>
        internal static float Pt(float pt) { return pt; }
    }
}
