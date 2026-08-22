using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace ConfigurO
{
    /// <summary>
    /// Desktop Window Manager integration -- the parts of a modern Windows 11
    /// window a borderless WinForms app has to opt into by hand.
    ///
    /// Every call is capability-gated on <see cref="WindowsRelease"/> and
    /// swallows failures: an unsupported attribute simply returns an HRESULT we
    /// ignore, so the same binary still runs on Windows 7/8/10.
    /// </summary>
    internal static class DwmChrome
    {
        // DWMWINDOWATTRIBUTE
        const int DWMWA_USE_IMMERSIVE_DARK_MODE_PRE20H1 = 19;
        const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;
        const int DWMWA_WINDOW_CORNER_PREFERENCE = 33;
        const int DWMWA_BORDER_COLOR = 34;
        const int DWMWA_CAPTION_COLOR = 35;
        const int DWMWA_TEXT_COLOR = 36;
        const int DWMWA_SYSTEMBACKDROP_TYPE = 38;

        internal enum CornerPreference
        {
            Default = 0,
            DoNotRound = 1,
            Round = 2,
            RoundSmall = 3
        }

        internal enum Backdrop
        {
            Auto = 0,
            None = 1,
            Mica = 2,
            Acrylic = 3,
            MicaAlt = 4
        }

        /// <summary>DWM's "use the system default" sentinel for colour attributes.</summary>
        const uint DWMWA_COLOR_DEFAULT = 0xFFFFFFFF;

        [DllImport("dwmapi.dll", PreserveSig = true)]
        static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);

        [DllImport("dwmapi.dll", PreserveSig = true)]
        static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref uint value, int size);

        [StructLayout(LayoutKind.Sequential)]
        struct MARGINS { public int cxLeftWidth, cxRightWidth, cyTopHeight, cyBottomHeight; }

        [DllImport("dwmapi.dll", PreserveSig = true)]
        static extern int DwmExtendFrameIntoClientArea(IntPtr hwnd, ref MARGINS m);

        static bool Set(IntPtr hwnd, int attr, int value)
        {
            if (hwnd == IntPtr.Zero) return false;
            try { return DwmSetWindowAttribute(hwnd, attr, ref value, sizeof(int)) == 0; }
            catch (DllNotFoundException) { return false; }   // pre-Vista / Server Core
            catch (EntryPointNotFoundException) { return false; }
        }

        static bool Set(IntPtr hwnd, int attr, uint value)
        {
            if (hwnd == IntPtr.Zero) return false;
            try { return DwmSetWindowAttribute(hwnd, attr, ref value, sizeof(uint)) == 0; }
            catch (DllNotFoundException) { return false; }
            catch (EntryPointNotFoundException) { return false; }
        }

        /// <summary>
        /// Tells DWM the window is dark so system-drawn affordances (the resize
        /// shadow, scrollbar edges, context menus) match the theme.
        /// </summary>
        internal static void SetDarkMode(IntPtr hwnd, bool dark)
        {
            if (!WindowsRelease.SupportsImmersiveDarkMode) return;
            int on = dark ? 1 : 0;
            if (!Set(hwnd, DWMWA_USE_IMMERSIVE_DARK_MODE, on))
                Set(hwnd, DWMWA_USE_IMMERSIVE_DARK_MODE_PRE20H1, on);
        }

        /// <summary>Windows 11 rounded window corners.</summary>
        internal static void SetCorners(IntPtr hwnd, CornerPreference pref)
        {
            if (!WindowsRelease.SupportsRoundedCorners) return;
            Set(hwnd, DWMWA_WINDOW_CORNER_PREFERENCE, (int)pref);
        }

        /// <summary>Mica / Acrylic material behind the window (Windows 11 22H2+).</summary>
        internal static void SetBackdrop(IntPtr hwnd, Backdrop type)
        {
            if (!WindowsRelease.SupportsBackdrop) return;
            Set(hwnd, DWMWA_SYSTEMBACKDROP_TYPE, (int)type);
        }

        /// <summary>
        /// Colours the 1px DWM border. Windows 11 only; pass
        /// <c>Color.Empty</c> to restore the system default.
        /// </summary>
        internal static void SetBorderColor(IntPtr hwnd, Color c)
        {
            if (!WindowsRelease.IsWindows11) return;
            Set(hwnd, DWMWA_BORDER_COLOR, c.IsEmpty ? DWMWA_COLOR_DEFAULT : ToColorRef(c));
        }

        /// <summary>
        /// Colours the system caption. We draw our own title bar, but the
        /// caption colour still shows during the open/close animation and in
        /// Alt-Tab / Task View previews, so keeping it in sync avoids a flash
        /// of white when the app launches in dark mode.
        /// </summary>
        internal static void SetCaptionColor(IntPtr hwnd, Color c)
        {
            if (!WindowsRelease.IsWindows11) return;
            Set(hwnd, DWMWA_CAPTION_COLOR, c.IsEmpty ? DWMWA_COLOR_DEFAULT : ToColorRef(c));
        }

        internal static void SetCaptionTextColor(IntPtr hwnd, Color c)
        {
            if (!WindowsRelease.IsWindows11) return;
            Set(hwnd, DWMWA_TEXT_COLOR, c.IsEmpty ? DWMWA_COLOR_DEFAULT : ToColorRef(c));
        }

        static uint ToColorRef(Color c)
        {
            return (uint)(c.R | (c.G << 8) | (c.B << 16));   // COLORREF is 0x00BBGGRR
        }

        /// <summary>
        /// Applies the whole Nocturne chrome treatment to a window. Safe to call
        /// repeatedly -- the shell re-runs it whenever the theme flips.
        /// </summary>
        internal static void Apply(Form form, bool mica)
        {
            if (form == null || !form.IsHandleCreated) return;
            IntPtr h = form.Handle;
            bool dark = NocturneTheme.IsDark;

            SetDarkMode(h, dark);
            SetCorners(h, CornerPreference.Round);
            SetCaptionColor(h, NocturneTheme.Bg);
            SetCaptionTextColor(h, NocturneTheme.Text);
            SetBorderColor(h, dark ? NocturneTheme.Neutral500 : NocturneTheme.BorderStrong);

            // Mica needs a sheet of glass to show through, which only works if
            // the window paints a transparent ground. Our screens are opaque, so
            // this is opt-in and off by default.
            SetBackdrop(h, mica ? Backdrop.MicaAlt : Backdrop.None);
            if (mica)
            {
                MARGINS m = new MARGINS { cxLeftWidth = -1, cxRightWidth = -1, cyTopHeight = -1, cyBottomHeight = -1 };
                try { DwmExtendFrameIntoClientArea(h, ref m); } catch (DllNotFoundException) { }
            }
        }
    }
}
