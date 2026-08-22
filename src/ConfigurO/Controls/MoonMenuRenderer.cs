using System.Drawing;
using System.Windows.Forms;

namespace ConfigurO
{
    /// <summary>
    /// Nocturne styling for the tray context menu -- surface fill, hairline
    /// border, accent-tinted hover. Reads live from <see cref="NocturneTheme"/>
    /// so the menu follows the Dark/Light switch without being rebuilt.
    /// </summary>
    internal sealed class MoonMenuRenderer : ToolStripProfessionalRenderer
    {
        internal MoonMenuRenderer() : base(new MoonColors()) { RoundedEdges = false; }

        protected override void OnRenderArrow(ToolStripArrowRenderEventArgs e)
        {
            e.ArrowColor = NocturneTheme.AccentText;
            base.OnRenderArrow(e);
        }

        protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
        {
            e.TextColor = e.Item.Selected ? NocturneTheme.AccentText : NocturneTheme.Text;
            base.OnRenderItemText(e);
        }
    }

    internal sealed class MoonColors : ProfessionalColorTable
    {
        public override Color SeparatorLight { get { return NocturneTheme.Border; } }
        public override Color SeparatorDark { get { return NocturneTheme.Border; } }
        public override Color ToolStripDropDownBackground { get { return NocturneTheme.Surface; } }
        public override Color ImageMarginGradientBegin { get { return NocturneTheme.Surface; } }
        public override Color ImageMarginGradientMiddle { get { return NocturneTheme.Surface; } }
        public override Color ImageMarginGradientEnd { get { return NocturneTheme.Surface; } }
        public override Color ToolStripBorder { get { return NocturneTheme.Border; } }
        public override Color MenuBorder { get { return NocturneTheme.Border; } }
        public override Color MenuItemSelected { get { return NocturneTheme.SelectedFillOnSurface; } }
        public override Color MenuItemSelectedGradientBegin { get { return NocturneTheme.SelectedFillOnSurface; } }
        public override Color MenuItemSelectedGradientEnd { get { return NocturneTheme.SelectedFillOnSurface; } }
        public override Color MenuItemBorder { get { return NocturneTheme.Accent700; } }
        public override Color MenuItemPressedGradientBegin { get { return NocturneTheme.Surface; } }
        public override Color MenuItemPressedGradientEnd { get { return NocturneTheme.Surface; } }
    }
}
