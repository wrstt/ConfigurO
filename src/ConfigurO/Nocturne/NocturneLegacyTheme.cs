using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace ConfigurO
{
    /// <summary>
    /// Repaints the remaining designer-built dialogs (update, first run,
    /// splash, hosts editor, file unlocker, startup backup/restore, the text
    /// viewer and the confirmation prompt) in Nocturne colours.
    ///
    /// Those forms use stock WinForms controls laid out in the designer. Rather
    /// than rebuild each one, this walks the control tree once and maps every
    /// control type onto the token set -- which is also what replaced the old
    /// Ocean/Magma/... palette plumbing.
    /// </summary>
    internal static class NocturneLegacyTheme
    {
        internal static void Apply(Form f)
        {
            if (f == null) return;

            f.BackColor = NocturneTheme.Bg;
            f.ForeColor = NocturneTheme.Text;
            if (f.IsHandleCreated) DwmChrome.Apply(f, false);
            else f.HandleCreated += (s, e) => DwmChrome.Apply(f, false);

            foreach (Control c in Utilities.GetSelfAndChildrenRecursive(f)) Style(c);
        }

        static void Style(Control c)
        {
            // The hand-painted Nocturne controls own their appearance.
            if (c is NControl || c is NPanel || c is MoonToggle || c is MoonCheck ||
                c is MoonRadio || c is MoonSelect || c is MoonList || c is MoonCheckList ||
                c is MoonProgress)
            {
                c.Invalidate();
                return;
            }

            Button b = c as Button;
            if (b != null)
            {
                b.FlatStyle = FlatStyle.Flat;
                b.BackColor = Color.Transparent;
                b.ForeColor = NocturneTheme.AccentText;
                b.FlatAppearance.BorderSize = 1;
                b.FlatAppearance.BorderColor = NocturneTheme.Accent;
                b.FlatAppearance.MouseOverBackColor = NocturneTheme.HoverAccent;
                b.FlatAppearance.MouseDownBackColor = NocturneTheme.PressedAccent;
                b.Font = NocturneFonts.Row();
                b.UseVisualStyleBackColor = false;
                return;
            }

            LinkLabel link = c as LinkLabel;
            if (link != null)
            {
                link.BackColor = Color.Transparent;
                link.LinkColor = NocturneTheme.AccentText;
                link.VisitedLinkColor = NocturneTheme.AccentText;
                link.ActiveLinkColor = NocturneTheme.Accent;
                link.LinkBehavior = LinkBehavior.HoverUnderline;
                link.Font = NocturneFonts.Row();
                return;
            }

            TextBoxBase text = c as TextBoxBase;
            if (text != null)
            {
                text.BackColor = NocturneTheme.Surface;
                text.ForeColor = NocturneTheme.Text;
                text.BorderStyle = BorderStyle.FixedSingle;
                text.Font = text.Multiline ? NocturneFonts.Code() : NocturneFonts.Row();
                return;
            }

            ListView list = c as ListView;
            if (list != null)
            {
                list.BackColor = NocturneTheme.Surface;
                list.ForeColor = NocturneTheme.Text;
                list.BorderStyle = BorderStyle.None;
                list.Font = NocturneFonts.Row();
                return;
            }

            Label label = c as Label;
            if (label != null)
            {
                label.BackColor = Color.Transparent;
                // Tagged labels were the legacy accent-coloured captions.
                label.ForeColor = (string)label.Tag == Constants.THEME_FLAG
                                  ? NocturneTheme.AccentText : NocturneTheme.Text;
                label.Font = NocturneFonts.Row();
                return;
            }

            PictureBox picture = c as PictureBox;
            if (picture != null)
            {
                picture.BackColor = Color.Transparent;
                return;
            }

            Panel panel = c as Panel;
            if (panel != null)
            {
                panel.BackColor = NocturneTheme.Bg;
                return;
            }

            c.BackColor = NocturneTheme.Bg;
            c.ForeColor = NocturneTheme.Text;
        }
    }
}
