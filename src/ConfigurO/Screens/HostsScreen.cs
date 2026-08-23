using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace ConfigurO
{
    /// <summary>
    /// Hosts editor: the entry table on the left, a 320px rail on the right
    /// with the add form, the pre-made block lists, and the read-only lock.
    /// </summary>
    internal sealed class HostsScreen : NScreen
    {
        internal const string ScreenId = "hosts";

        const string BlockedIp = "0.0.0.0";

        readonly NTable _table = new NTable();
        readonly NCard _add = new NCard();
        readonly NCard _lists = new NCard();
        readonly NCard _lock = new NCard();

        readonly NTextBox _ip = new NTextBox();
        readonly NTextBox _domain = new NTextBox();
        readonly NButton _addButton = new NButton();
        readonly NButton _blockButton = new NButton();
        readonly NButton _basic = new NButton();
        readonly NButton _social = new NButton();
        readonly NButton _restore = new NButton();
        readonly NButton _openFile = new NButton();
        readonly NButton _removeAll = new NButton();
        readonly NButton _refresh = new NButton();
        readonly MoonToggle _readOnly = new MoonToggle();

        List<string> _entries = new List<string>();
        bool _suppressLock;
        bool _hostsPresent = true;

        internal override string Id { get { return ScreenId; } }
        internal override string Icon { get { return NocturneIcons.Hosts; } }
        internal override string NavLabel { get { return I18n.Get("navHosts", "Hosts"); } }

        protected override void Build()
        {
            TitleText = I18n.Get("hostsTitle", "Hosts file");

            _openFile.Style = NButtonStyle.Ghost;
            _openFile.Text = I18n.Get("linkAdvancedEdit", "Advanced editor");
            _openFile.Icon = NocturneIcons.ExternalLink;
            _openFile.Click += (s, e) =>
            {
                using (HostsEditorForm f = new HostsEditorForm()) f.ShowDialog(FindForm());
                Load();
            };
            AddAction(_openFile);

            _removeAll.Style = NButtonStyle.Ghost;
            _removeAll.Text = I18n.Get("removeAllHostsB", "Delete all");
            _removeAll.Icon = NocturneIcons.Trash;
            _removeAll.Click += (s, e) => RemoveAll();
            AddAction(_removeAll);

            _refresh.Style = NButtonStyle.Icon;
            _refresh.Icon = NocturneIcons.Refresh;
            _refresh.Click += (s, e) => Load();
            AddAction(_refresh);

            _table.SetColumns(
                new NColumn { Header = "IP", Width = 140, Kind = NCellKind.Mono },
                new NColumn { Header = I18n.Get("lblDomain", "Domain"), Weight = 1f },
                new NColumn { Header = string.Empty, Width = 86, Kind = NCellKind.Tag },
                new NColumn { Header = string.Empty, Width = 44, Kind = NCellKind.Action, Icon = NocturneIcons.Trash });
            _table.ActionClicked += (s, e) => Remove(e.RowIndex);
            Body.Controls.Add(_table);

            // ── add entry ──
            _add.Title = I18n.Get("hostsAddTitle", "Add entry");
            _ip.Placeholder = I18n.Get("lblIP", "IP address");
            _ip.Monospace = true;
            _domain.Placeholder = I18n.Get("lblDomain", "Domain");
            _add.Body.Controls.Add(_ip);
            _add.Body.Controls.Add(_domain);

            _addButton.Style = NButtonStyle.Primary;
            _addButton.Text = I18n.Get("addHostB", "Add");
            _addButton.Click += (s, e) => Add(false);
            _add.Body.Controls.Add(_addButton);

            _blockButton.Style = NButtonStyle.Ghost;
            _blockButton.AutoWidth = true;
            _blockButton.Text = I18n.Get("btnBlock", "Block");
            _blockButton.Click += (s, e) => Add(true);
            _add.Body.Controls.Add(_blockButton);
            Body.Controls.Add(_add);

            // ── pre-made lists ──
            _lists.Title = I18n.Get("lblAdblock", "Pre-made adblocks");
            _lists.Note = I18n.Get("hostsListsNote", "Replaces your current configuration");
            _basic.Style = NButtonStyle.Secondary;
            _basic.Text = I18n.Get("hostsAdblockBasic", "AdBlock basic");
            _basic.Click += (s, e) => ApplyList(AdBlockLists.Basic, _basic.Text);
            _lists.Body.Controls.Add(_basic);

            _social.Style = NButtonStyle.Secondary;
            _social.Text = I18n.Get("hostsAdblockSocial", "AdBlock + Social");
            _social.Click += (s, e) => ApplyList(AdBlockLists.Social, _social.Text);
            _lists.Body.Controls.Add(_social);

            _restore.Style = NButtonStyle.Ghost;
            _restore.Text = I18n.Get("linkRestoreDefault", "Restore default");
            _restore.Click += (s, e) => RestoreDefault();
            _lists.Body.Controls.Add(_restore);
            Body.Controls.Add(_lists);

            // ── lock ──
            _lock.Title = I18n.Get("hostsLockTitle", "Lock hosts file");
            _lock.Icon = NocturneIcons.Lock;
            _lock.Note = I18n.Get("hostsLockNote", "Read-only protection");
            _readOnly.CheckedChanged += (s, e) =>
            {
                if (_suppressLock) return;
                HostsHelper.ReadOnly(_readOnly.Checked);
                ApplyLockState();
                Toast(_readOnly.Checked
                    ? I18n.Get("hostsLocked", "Hosts file locked")
                    : I18n.Get("hostsUnlocked", "Hosts file unlocked"));
            };
            _lock.Body.Controls.Add(_readOnly);
            Body.Controls.Add(_lock);
        }

        internal override void Activate() { Load(); }

        void Load()
        {
            _table.Clear();
            try { _entries = HostsHelper.GetHostsEntries(); }
            catch (Exception ex)
            {
                Logger.LogError("HostsScreen.Load", ex.Message, ex.StackTrace);
                _entries = new List<string>();
            }

            foreach (string entry in _entries)
            {
                string[] parts = entry.Split(new[] { " : " }, 2, StringSplitOptions.None);
                string ip = parts.Length > 0 ? parts[0].Trim() : string.Empty;
                string domain = parts.Length > 1 ? parts[1].Trim() : string.Empty;
                _table.AddRow(new[]
                {
                    ip, domain,
                    ip == BlockedIp ? I18n.Get("hostsBlocked", "Blocked") : string.Empty,
                    string.Empty
                });
            }

            SubtitleText = string.Format("{0} · {1}", HostsHelper.HostsFile,
                string.Format(I18n.Get("hostsCount", "{0} entries"), _entries.Count));

            // GetReadOnly() reports a missing file as read-only, which would
            // grey the whole screen out with no explanation. Say what is wrong
            // instead.
            _hostsPresent = System.IO.File.Exists(HostsHelper.HostsFile);
            _suppressLock = true;
            _readOnly.Checked = _hostsPresent && HostsHelper.GetReadOnly();
            _suppressLock = false;
            ApplyLockState();

            if (!_hostsPresent)
                SetEmpty(I18n.Get("hostsMissing", "The hosts file could not be found."), NocturneIcons.Warning);
            else if (_entries.Count == 0)
                SetEmpty(I18n.Get("hostsEmpty", "No entries yet."), NocturneIcons.Hosts);
            else
                SetEmpty(null);

            RefreshHeader();
            PerformLayout();
        }

        void ApplyLockState()
        {
            bool locked = _readOnly.Checked || !_hostsPresent;
            _readOnly.Enabled = _hostsPresent;
            _ip.Enabled = _domain.Enabled = !locked;
            _addButton.Enabled = _blockButton.Enabled = !locked;
            _basic.Enabled = _social.Enabled = _restore.Enabled = !locked;
            _removeAll.Enabled = !locked;
            _table.Enabled = !locked;
        }

        void Add(bool block)
        {
            string ip = (block ? BlockedIp : _ip.Text).Trim();
            string domain = _domain.Text.Trim();

            if (string.IsNullOrEmpty(domain))
            {
                Toast(I18n.Get("hostsNeedDomain", "Enter a domain first"));
                return;
            }
            if (string.IsNullOrEmpty(ip))
            {
                Toast(I18n.Get("hostsNeedIp", "Enter an IP, or use Block"));
                return;
            }

            HostsHelper.AddEntry(HostsHelper.SanitizeEntry(ip) + " " + HostsHelper.SanitizeEntry(domain));
            _ip.Text = string.Empty;
            _domain.Text = string.Empty;
            Toast(string.Format(I18n.Get("hostsAdded", "{0} added"), domain));
            Load();
        }

        void Remove(int row)
        {
            if (row < 0 || row >= _entries.Count) return;
            string domain = _entries[row];
            HostsHelper.RemoveEntry(domain.Replace(" : ", " "));
            Toast(I18n.Get("hostsRemoved", "Entry removed"));
            Load();
        }

        void RemoveAll()
        {
            if (_entries.Count == 0) return;
            if (!HelperForm.Confirm(FindForm(),
                    I18n.Get("removeAllHosts", "Are you sure you want to delete all hosts entries?"))) return;

            List<string> plain = new List<string>();
            foreach (string e in _entries) plain.Add(e.Replace(" : ", " "));
            HostsHelper.RemoveAllEntries(plain);
            Toast(string.Format(I18n.Get("hostsRemovedAll", "{0} entries removed"), plain.Count));
            Load();
        }

        void ApplyList(string url, string label)
        {
            string prompt = I18n.Get("hostsReplaceConfirm",
                "This replaces your current hosts configuration. Continue?");
            if (!HelperForm.Confirm(FindForm(), prompt)) return;

            _basic.Enabled = _social.Enabled = false;
            Toast(I18n.Get("hostsDownloading", "Downloading block list…"));

            Task.Run(() => AdBlockLists.Fetch(url)).ContinueWith(t =>
            {
                string[] lines = t.Status == TaskStatus.RanToCompletion ? t.Result : null;
                OnUi(() => ListReady(lines, label));
            });
        }

        void ListReady(string[] lines, string label)
        {
            _basic.Enabled = _social.Enabled = !_readOnly.Checked;

            if (lines == null || lines.Length == 0)
            {
                Toast(I18n.Get("hostsListFailed", "Could not download that block list"));
                return;
            }

            HostsHelper.SaveHosts(lines);
            Load();
            Toast(string.Format(I18n.Get("hostsListApplied", "{0} · {1} domains blocked"),
                                label, _entries.Count));
        }

        void RestoreDefault()
        {
            if (!HelperForm.Confirm(FindForm(), I18n.Get("hostsRestoreConfirm", "Restore the default hosts file?"))) return;
            HostsHelper.RestoreDefaultHosts();
            Load();
            Toast(I18n.Get("hostsRestored", "Default hosts file restored"));
        }

        protected override void Relayout()
        {
            int railW = NocturneScale.S(320);
            int gap = NocturneScale.S(16);
            int tableW = Math.Max(NocturneScale.S(240), Width - Pad * 2 - railW - gap);
            int railX = Pad + tableW + gap;

            _table.SetBounds(Pad, 0, tableW, Math.Max(_table.ContentHeight, NocturneScale.S(120)));

            int y = 0;
            int fieldH = NocturneScale.S(NocturneTheme.InputHeight);
            int addH = NocturneScale.S(34) + fieldH * 2 + NocturneScale.S(8) * 2 + NocturneScale.S(34) + NocturneScale.S(26);
            _add.SetBounds(railX, y, railW, addH);
            _ip.SetBounds(0, 0, _add.Body.Width, fieldH);
            _domain.SetBounds(0, fieldH + NocturneScale.S(8), _add.Body.Width, fieldH);

            int by = fieldH * 2 + NocturneScale.S(16);
            // Half the row at most, so a long translation of "Block" cannot
            // squeeze "Add" out of existence.
            int blockW = Math.Min(_add.Body.Width / 2,
                                  Math.Max(NocturneScale.S(84), _blockButton.Width));
            _addButton.SetBounds(0, by, _add.Body.Width - blockW - NocturneScale.S(8), NocturneScale.S(34));
            _blockButton.SetBounds(_add.Body.Width - blockW, by, blockW, NocturneScale.S(34));

            y += addH + gap;
            int listsH = NocturneScale.S(52) + NocturneScale.S(34) * 3 + NocturneScale.S(8) * 2 + NocturneScale.S(26);
            _lists.SetBounds(railX, y, railW, listsH);
            _basic.SetBounds(0, 0, _lists.Body.Width, NocturneScale.S(34));
            _social.SetBounds(0, NocturneScale.S(42), _lists.Body.Width, NocturneScale.S(34));
            _restore.SetBounds(0, NocturneScale.S(84), _lists.Body.Width, NocturneScale.S(34));

            y += listsH + gap;
            int lockH = NocturneScale.S(52) + NocturneScale.S(30) + NocturneScale.S(26);
            _lock.SetBounds(railX, y, railW, lockH);
            _readOnly.Location = new Point(_lock.Body.Width - _readOnly.Width, 0);

            y += lockH;
            Body.Height = Math.Max(_table.Height, y) + NocturneScale.S(20);
        }
    }

    /// <summary>
    /// The curated block lists offered on the Hosts screen.
    ///
    /// These are Steven Black's consolidated hosts files -- the same source
    /// most host-based blockers ship with -- fetched on demand rather than
    /// bundled, so they are current whenever the button is pressed.
    /// </summary>
    internal static class AdBlockLists
    {
        internal const string Basic =
            "https://raw.githubusercontent.com/StevenBlack/hosts/master/hosts";
        internal const string Social =
            "https://raw.githubusercontent.com/StevenBlack/hosts/master/alternates/social/hosts";

        internal static string[] Fetch(string url)
        {
            try
            {
                ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
                using (WebClient c = new WebClient { Encoding = Encoding.UTF8 })
                    return c.DownloadString(url)
                            .Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
            }
            catch (Exception ex)
            {
                Logger.LogError("AdBlockLists.Fetch", ex.Message, ex.StackTrace);
                return null;
            }
        }
    }
}
