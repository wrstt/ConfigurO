using System;
using System.Windows.Forms;

namespace ConfigurO
{
    /// <summary>
    /// Builds themed context menus.
    ///
    /// WinForms menus are system-drawn, so every one has to be handed the
    /// Nocturne renderer or it arrives in default grey. This keeps that in one
    /// place and re-themes open menus when the mode flips.
    /// </summary>
    internal static class NocturneMenu
    {
        internal static ContextMenuStrip Create()
        {
            ContextMenuStrip menu = new ContextMenuStrip
            {
                Renderer = new MoonMenuRenderer(),
                BackColor = NocturneTheme.Surface,
                ForeColor = NocturneTheme.Text,
                Font = NocturneFonts.Row(),
                ShowImageMargin = false
            };

            EventHandler retheme = (s, e) =>
            {
                if (menu.IsDisposed) return;
                menu.BackColor = NocturneTheme.Surface;
                menu.ForeColor = NocturneTheme.Text;
            };
            NocturneTheme.Changed += retheme;
            menu.Disposed += (s, e) => NocturneTheme.Changed -= retheme;

            return menu;
        }

        /// <summary>Adds an item and returns the menu, so calls can be chained.</summary>
        internal static ContextMenuStrip Add(this ContextMenuStrip menu, string text, Action action)
        {
            ToolStripMenuItem item = new ToolStripMenuItem(text);
            item.Click += (s, e) => { if (action != null) action(); };
            menu.Items.Add(item);
            return menu;
        }

        internal static ContextMenuStrip Separator(this ContextMenuStrip menu)
        {
            menu.Items.Add(new ToolStripSeparator());
            return menu;
        }
    }
}
