using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace ConfigurO
{
    /// <summary>
    /// UWP apps: a two-column grid of checkbox rows with the on-disk size of
    /// each package, plus select-all and a bulk uninstall.
    ///
    /// Enumerating packages goes through PowerShell, so both the listing and
    /// the uninstall run off the UI thread.
    /// </summary>
    internal sealed class UwpScreen : NScreen
    {
        internal const string ScreenId = "uwp";

        readonly List<NSelectCard> _cards = new List<NSelectCard>();
        readonly NButton _selectAll = new NButton();
        readonly NButton _uninstall = new NButton();
        readonly MoonCheck _showAll = new MoonCheck();
        readonly NButton _restoreAll = new NButton();
        readonly NButton _refresh = new NButton();

        List<KeyValuePair<string, string>> _apps = new List<KeyValuePair<string, string>>();
        bool _busy;

        internal override string Id { get { return ScreenId; } }
        internal override string Icon { get { return NocturneIcons.Uwp; } }
        internal override string NavLabel { get { return I18n.Get("navUwp", "UWP Apps"); } }

        protected override void Build()
        {
            TitleText = I18n.Get("txtModernAppsTitle", "UWP apps");
            SubtitleText = I18n.Get("uwpLoading", "Reading installed packages…");

            _showAll.Text = I18n.Get("uwpShowAll", "Include system apps");
            _showAll.CheckedChanged += (s, e) => Load();
            AddAction(_showAll);

            _selectAll.Style = NButtonStyle.Ghost;
            _selectAll.Text = I18n.Get("btnSelectAll", "Select all");
            _selectAll.Click += (s, e) => ToggleAll();
            AddAction(_selectAll);

            _uninstall.Style = NButtonStyle.Primary;
            _uninstall.Text = I18n.Get("uninstallModernAppsButton", "Uninstall");
            _uninstall.Icon = NocturneIcons.Trash;
            _uninstall.Click += (s, e) => Uninstall();
            AddAction(_uninstall);

            _restoreAll.Style = NButtonStyle.Ghost;
            _restoreAll.Text = I18n.Get("btnRestoreUwp", "Restore all UWP");
            _restoreAll.Icon = NocturneIcons.History;
            _restoreAll.Click += (s, e) => RestoreAll();
            AddAction(_restoreAll);

            _refresh.Style = NButtonStyle.Icon;
            _refresh.Icon = NocturneIcons.Refresh;
            _refresh.Click += (s, e) => Load();
            AddAction(_refresh);
        }

        internal override void Activate()
        {
            if (_cards.Count == 0) Load();
        }

        void Load()
        {
            if (_busy) return;
            _busy = true;
            SubtitleText = I18n.Get("uwpLoading", "Reading installed packages…");
            SetLoading(I18n.Get("uwpLoading", "Reading installed packages…"), NocturneIcons.Uwp);
            RefreshHeader();

            bool showAll = _showAll.Checked;
            Task.Run(() =>
            {
                try { return UWPHelper.GetUWPApps(showAll); }
                catch (Exception ex)
                {
                    Logger.LogError("UwpScreen.Load", ex.Message, ex.StackTrace);
                    return new List<KeyValuePair<string, string>>();
                }
            }).ContinueWith(t =>
            {
                List<KeyValuePair<string, string>> apps = t.Status == TaskStatus.RanToCompletion
                    ? t.Result : new List<KeyValuePair<string, string>>();
                OnUi(() => Populate(apps));
            });
        }

        void Populate(List<KeyValuePair<string, string>> apps)
        {
            _busy = false;
            _apps = apps;

            foreach (NSelectCard c in _cards) { Body.Controls.Remove(c); c.Dispose(); }
            _cards.Clear();

            foreach (KeyValuePair<string, string> a in _apps)
            {
                NSelectCard card = new NSelectCard
                {
                    Text = a.Key,
                    Meta = PackageSize(a.Value),
                    Icon = NocturneIcons.Uwp,
                    Tag = a.Key
                };
                card.SelectedChanged += (s, e) => UpdateHeader();
                _cards.Add(card);
                Body.Controls.Add(card);
            }

            SetEmpty(_cards.Count == 0
                ? I18n.Get("uwpEmpty", "No removable packages found.")
                : null, NocturneIcons.Uwp);

            UpdateHeader();
            PerformLayout();
        }

        static string PackageSize(string installLocation)
        {
            if (string.IsNullOrEmpty(installLocation) || !Directory.Exists(installLocation)) return string.Empty;
            try { return CleanHelper.CalculateSize(installLocation).ToString("MB"); }
            catch (Exception ex)
            {
                Logger.LogError("UwpScreen.PackageSize", ex.Message, ex.StackTrace);
                return string.Empty;
            }
        }

        void UpdateHeader()
        {
            int selected = _cards.Count(c => c.Selected);
            SubtitleText = string.Format(I18n.Get("uwpCount", "{0} packages installed"), _apps.Count);
            _uninstall.Text = selected > 0
                ? string.Format(I18n.Get("btnUninstallN", "Uninstall ({0})"), selected)
                : I18n.Get("btnUninstall", "Uninstall");
            _uninstall.AutoFit();
            _selectAll.Text = selected == _cards.Count && _cards.Count > 0
                ? I18n.Get("btnClearSelection", "Clear selection")
                : I18n.Get("btnSelectAll", "Select all");
            _selectAll.AutoFit();
            RefreshHeader();
            PerformLayout();
        }

        void ToggleAll()
        {
            bool select = _cards.Count(c => c.Selected) < _cards.Count;
            foreach (NSelectCard c in _cards) c.Selected = select;
            UpdateHeader();
        }

        void Uninstall()
        {
            if (_busy) return;
            List<NSelectCard> selected = _cards.Where(c => c.Selected).ToList();
            if (selected.Count == 0)
            {
                Toast(I18n.Get("uwpSelectFirst", "Select at least one app first"));
                return;
            }

            string prompt = I18n.Get("removeModernApps", "Uninstall the selected apps?") +
                            Environment.NewLine + Environment.NewLine +
                            string.Join(Environment.NewLine, selected.Select(c => c.Text));
            if (!HelperForm.Confirm(FindForm(), prompt)) return;

            _busy = true;
            _uninstall.Enabled = false;

            List<string> names = selected.Select(c => (string)c.Tag).ToList();
            Task.Run(() =>
            {
                List<string> failed = new List<string>();
                foreach (string n in names)
                {
                    try { if (UWPHelper.UninstallUWPApp(n)) failed.Add(n); }
                    catch (Exception ex)
                    {
                        Logger.LogError("UwpScreen.Uninstall:" + n, ex.Message, ex.StackTrace);
                        failed.Add(n);
                    }
                }
                return failed;
            }).ContinueWith(t =>
            {
                List<string> failed = t.Status == TaskStatus.RanToCompletion ? t.Result : names;
                OnUi(() => UninstallFinished(names.Count, failed));
            });
        }

        /// <summary>
        /// Re-registers every provisioned package for the current user. Slow
        /// and noisy, which is why it confirms first and runs off-thread.
        /// </summary>
        void RestoreAll()
        {
            if (_busy) return;
            if (!HelperForm.Confirm(FindForm(),
                    I18n.Get("restoreUwpMessage", "Are you sure you want to do this?"))) return;

            _busy = true;
            _restoreAll.Enabled = false;
            Toast(I18n.Get("uwpRestoring", "Restoring UWP apps…"));

            Task.Run(() =>
            {
                try { return UWPHelper.RestoreAllUWPApps(); }
                catch (Exception ex)
                {
                    Logger.LogError("UwpScreen.RestoreAll", ex.Message, ex.StackTrace);
                    return true;
                }
            }).ContinueWith(t =>
            {
                bool hadErrors = t.Status != TaskStatus.RanToCompletion || t.Result;
                OnUi(() =>
                {
                    _busy = false;
                    _restoreAll.Enabled = true;
                    Toast(hadErrors
                        ? I18n.Get("uwpRestorePartial", "Restore finished with some errors")
                        : I18n.Get("uwpRestored", "UWP apps restored"));
                    Load();
                });
            });
        }

        void UninstallFinished(int attempted, List<string> failed)
        {
            _busy = false;
            _uninstall.Enabled = true;

            int removed = attempted - failed.Count;
            Toast(failed.Count == 0
                ? string.Format(I18n.Get("uwpRemoved", "{0} apps uninstalled"), removed)
                : string.Format(I18n.Get("errorModernApps", "{0} removed, {1} could not be"), removed, failed.Count));

            Load();
        }

        protected override void Relayout()
        {
            int w = Math.Max(0, Width - Pad * 2);
            int gap = NocturneScale.S(10);
            int cols = w > NocturneScale.S(720) ? 2 : 1;
            int colW = (w - gap * (cols - 1)) / cols;
            int h = NocturneScale.S(46);

            for (int i = 0; i < _cards.Count; i++)
            {
                int row = i / cols, col = i % cols;
                _cards[i].SetBounds(Pad + col * (colW + gap), row * (h + gap), colW, h);
            }

            int rows = (_cards.Count + cols - 1) / cols;
            Body.Height = rows * (h + gap) + NocturneScale.S(20);
        }
    }
}
