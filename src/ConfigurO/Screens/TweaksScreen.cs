using System;
using System.Drawing;
using System.Windows.Forms;

namespace ConfigurO
{
    /// <summary>
    /// Universal tweaks -- the default screen.
    ///
    /// One grouped list driven by <see cref="TweakRegistry"/>, a live search
    /// over names and tips, and the "Reinforce policies" action that re-applies
    /// everything currently switched on (Windows quietly reverts a number of
    /// these across feature updates).
    /// </summary>
    internal sealed class TweaksScreen : NScreen
    {
        internal const string ScreenId = "tweaks";

        readonly NTweakList _list = new NTweakList();
        readonly NTextBox _search = new NTextBox();
        readonly NButton _reinforce = new NButton();

        internal override string Id { get { return ScreenId; } }
        internal override string Icon { get { return NocturneIcons.Tweaks; } }
        internal override string NavLabel { get { return I18n.Get("navTweaks", "Tweaks"); } }

        protected override void Build()
        {
            TitleText = I18n.Get("tweaksTitle", "Universal tweaks");

            _search.Placeholder = I18n.Get("tweaksSearch", "Search tweaks…");
            _search.Icon = NocturneIcons.Search;
            _search.Width = NocturneScale.S(260);
            _search.TextChanged += (s, e) =>
            {
                _list.Query = _search.Text;
                UpdateSubtitle();
                PerformLayout();          // the filtered list has a new height
                ScrollHost.ScrollToTop();
            };
            AddAction(_search);

            _reinforce.Style = NButtonStyle.Secondary;
            _reinforce.Text = I18n.Get("btnReinforce", "Reinforce policies");
            _reinforce.Icon = NocturneIcons.Refresh;
            _reinforce.Click += (s, e) => Reinforce();
            AddAction(_reinforce);

            _list.Toggled += (s, def) =>
            {
                UpdateSubtitle();
                MainForm shell = FindForm() as MainForm;
                if (shell != null) shell.RefreshFooter();
                OptionsHelper.SaveSettings();
            };
            Body.Controls.Add(_list);

            _list.Load();
        }

        protected override void OnBannerAction()
        {
            if (HelperForm.Confirm(FindForm(), I18n.Get("restartConfirm", "Restart the computer now?")))
            {
                OptionsHelper.SaveSettings();
                Utilities.Reboot();
            }
        }

        internal override void Activate()
        {
            _list.ShowTips = OptionsHelper.CurrentOptions.ShowHelpMessages;
            _list.Load();
            UpdateSubtitle();
        }

        void UpdateSubtitle()
        {
            SubtitleText = string.Format(
                I18n.Get("tweaksCount", "{0} tweaks · {1} applied"),
                _list.VisibleCount, _list.AppliedCount);
            RefreshHeader();
        }

        void Reinforce()
        {
            // msgReinforce is the confirmation question, not the result.
            if (!HelperForm.Confirm(FindForm(), I18n.Get("msgReinforce",
                    "Re-apply every policy you currently have switched on?"))) return;

            _reinforce.Enabled = false;
            try
            {
                Utilities.ReinforceCurrentTweaks();
                OptionsHelper.CurrentOptions.LastReinforced = DateTime.Now;
                OptionsHelper.SaveSettings();

                MainForm shell = FindForm() as MainForm;
                if (shell != null) shell.RefreshFooter();

                Toast(I18n.Get("msgReinforceDone", "Policies reinforced"));
            }
            catch (Exception ex)
            {
                Logger.LogError("TweaksScreen.Reinforce", ex.Message, ex.StackTrace);
                Toast(I18n.Get("msgReinforceFailed", "Could not reinforce every policy"));
            }
            finally { _reinforce.Enabled = true; }
        }

        protected override void Relayout()
        {
            int w = Math.Max(0, Width - Pad * 2);
            _list.SetBounds(Pad, 0, w, _list.Height);
            Body.Height = _list.Height + NocturneScale.S(20);
        }
    }
}
