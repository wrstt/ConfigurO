using System;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace ConfigurO
{
    /// <summary>
    /// Routes the mouse wheel to whatever is under the pointer.
    ///
    /// Windows sends WM_MOUSEWHEEL to the focused control, so a content pane
    /// only scrolls once it has been clicked -- which is not how anyone
    /// expects a scroll wheel to behave. This filter intercepts the message,
    /// finds the control under the cursor, walks up to the nearest
    /// <see cref="NScrollPanel"/> and scrolls that instead.
    /// </summary>
    internal sealed class NocturneWheelRouter : IMessageFilter
    {
        const int WM_MOUSEWHEEL = 0x020A;

        [DllImport("user32.dll")]
        static extern IntPtr WindowFromPoint(POINT point);

        [StructLayout(LayoutKind.Sequential)]
        struct POINT
        {
            public int X, Y;
            public POINT(int x, int y) { X = x; Y = y; }
        }

        static NocturneWheelRouter _installed;

        internal static void Install()
        {
            if (_installed != null) return;
            _installed = new NocturneWheelRouter();
            Application.AddMessageFilter(_installed);
        }

        internal static void Uninstall()
        {
            if (_installed == null) return;
            Application.RemoveMessageFilter(_installed);
            _installed = null;
        }

        public bool PreFilterMessage(ref Message m)
        {
            if (m.Msg != WM_MOUSEWHEEL) return false;

            try
            {
                // lParam carries screen coordinates for this message.
                int x = unchecked((short)(long)m.LParam);
                int y = unchecked((short)((long)m.LParam >> 16));

                Control target = Control.FromHandle(WindowFromPoint(new POINT(x, y)));
                if (target == null) return false;

                NScrollPanel panel = FindScrollPanel(target);
                if (panel == null || !panel.CanScroll) return false;

                int delta = unchecked((short)((long)m.WParam >> 16));
                // The step size lives in NScrollPanel; this only reports how
                // far the wheel turned. It used to compute the pixel distance
                // too, which meant two copies of the same constant and a
                // routed wheel that could scroll differently from a focused one.
                panel.ScrollByNotches(-delta / 120f);
                return true;   // handled; do not let it reach the focused control
            }
            catch (Exception ex)
            {
                Logger.LogError("NocturneWheelRouter", ex.Message, ex.StackTrace);
                return false;
            }
        }

        static NScrollPanel FindScrollPanel(Control c)
        {
            while (c != null)
            {
                NScrollPanel panel = c as NScrollPanel;
                if (panel != null) return panel;
                c = c.Parent;
            }
            return null;
        }
    }
}
