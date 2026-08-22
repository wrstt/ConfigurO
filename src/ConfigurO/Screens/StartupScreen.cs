using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using Newtonsoft.Json;

namespace ConfigurO
{
    /// <summary>
    /// Startup: everything Windows launches at sign-in, with a per-entry
    /// Enabled switch (via <see cref="StartupApproval"/>) and removal.
    ///
    /// Backups are JSON snapshots in the ConfigurO data folder, restored
    /// through the existing StartupRestoreForm.
    /// </summary>
    internal sealed class StartupScreen : NScreen
    {
        internal const string ScreenId = "startup";

        readonly NTable _table = new NTable();
        readonly NButton _backup = new NButton();
        readonly NButton _restore = new NButton();
        readonly NButton _refresh = new NButton();
        readonly NButton _removeAll = new NButton();
        ContextMenuStrip _rowMenu;
        List<StartupItem> _items = new List<StartupItem>();

        internal override string Id { get { return ScreenId; } }
        internal override string Icon { get { return NocturneIcons.Startup; } }
        internal override string NavLabel { get { return I18n.Get("navStartup", "Startup"); } }

        protected override void Build()
        {
            TitleText = I18n.Get("startupTitle", "Startup");

            _backup.Style = NButtonStyle.Secondary;
            _backup.Text = I18n.Get("backupStartupB", "Backup");
            _backup.Icon = NocturneIcons.Save;
            _backup.Click += (s, e) => Backup();
            AddAction(_backup);

            _restore.Style = NButtonStyle.Ghost;
            _restore.Text = I18n.Get("restoreStartupB", "Restore");
            _restore.Icon = NocturneIcons.History;
            _restore.Click += (s, e) => Restore();
            AddAction(_restore);

            _removeAll.Style = NButtonStyle.Ghost;
            _removeAll.Text = I18n.Get("removeAllIIB", "Delete all");
            _removeAll.Icon = NocturneIcons.Trash;
            _removeAll.Click += (s, e) => RemoveAll();
            AddAction(_removeAll);

            _refresh.Style = NButtonStyle.Icon;
            _refresh.Icon = NocturneIcons.Refresh;
            _refresh.Click += (s, e) => Load();
            AddAction(_refresh);

            _table.SetColumns(
                new NColumn { Header = I18n.Get("startupItemName", "Name"), Weight = 1.4f },
                new NColumn { Header = I18n.Get("startupPublisher", "Publisher"), Weight = 1f, Kind = NCellKind.Muted },
                new NColumn { Header = I18n.Get("startupItemLocation", "Location"), Weight = 1.2f, Kind = NCellKind.Mono },
                new NColumn { Header = I18n.Get("startupEnabled", "Enabled"), Width = 70, Kind = NCellKind.Toggle },
                new NColumn { Header = string.Empty, Width = 44, Kind = NCellKind.Action, Icon = NocturneIcons.Trash });

            _table.ToggleChanged += (s, e) => SetEnabled(e.RowIndex);
            _table.ActionClicked += (s, e) => Remove(e.RowIndex);
            _table.RowContextMenu += (s, e) => ShowRowMenu(e.RowIndex);
            Body.Controls.Add(_table);
        }

        internal override void Activate() { Load(); }

        void Load()
        {
            _table.Clear();
            try { _items = StartupHelper.GetStartupItems(); }
            catch (Exception ex)
            {
                Logger.LogError("StartupScreen.Load", ex.Message, ex.StackTrace);
                _items = new List<StartupItem>();
            }

            foreach (StartupItem i in _items)
            {
                _table.AddRow(new[]
                {
                    i.Name,
                    StartupApproval.Publisher(i),
                    i.ToString(),
                    string.Empty,
                    string.Empty
                }, StartupApproval.IsEnabled(i));
            }

            SetEmpty(_items.Count == 0
                ? I18n.Get("startupEmpty", "Nothing launches at sign-in.")
                : null, NocturneIcons.Startup);

            SubtitleText = string.Format(I18n.Get("startupCount", "{0} items launch with Windows"), _items.Count);
            RefreshHeader();
            PerformLayout();
        }

        /// <summary>
        /// Per-row actions that do not earn a column of their own: revealing
        /// the executable and jumping to the registry key behind the entry.
        /// </summary>
        void ShowRowMenu(int row)
        {
            if (row < 0 || row >= _items.Count) return;
            StartupItem item = _items[row];

            if (_rowMenu != null) _rowMenu.Dispose();
            _rowMenu = NocturneMenu.Create();
            _rowMenu.Add(I18n.Get("linkLocate", "Locate file"), () => item.LocateFile());
            if (item is RegistryStartupItem)
                _rowMenu.Add(I18n.Get("findInRegB", "Find in Registry"), () => item.LocateKey());
            _rowMenu.Separator();
            _rowMenu.Add(I18n.Get("removeStartupItemB", "Delete"), () => Remove(row));
            _rowMenu.Show(Cursor.Position);
        }

        void RemoveAll()
        {
            if (_items.Count == 0) return;
            if (!HelperForm.Confirm(FindForm(),
                    I18n.Get("removeAllStartup", "Are you sure you want to delete all startup items?"))) return;

            foreach (StartupItem i in _items) i.Remove();
            Toast(string.Format(I18n.Get("startupRemovedAll", "{0} startup items removed"), _items.Count));
            Load();
        }

        void SetEnabled(int row)
        {
            if (row < 0 || row >= _items.Count) return;
            bool on = _table.Flag(row);
            if (StartupApproval.SetEnabled(_items[row], on))
            {
                Toast(string.Format(on ? I18n.Get("startupEnabledToast", "{0} enabled")
                                       : I18n.Get("startupDisabledToast", "{0} disabled"), _items[row].Name));
            }
            else
            {
                _table.SetFlag(row, !on);   // the write failed; do not lie about the state
                Toast(I18n.Get("startupChangeFailed", "Could not change that startup entry"));
            }
        }

        void Remove(int row)
        {
            if (row < 0 || row >= _items.Count) return;
            StartupItem item = _items[row];

            string prompt = string.Format(
                I18n.Get("removeStartupOne", "Remove \"{0}\" from startup?"), item.Name);
            if (!HelperForm.Confirm(FindForm(), prompt)) return;

            item.Remove();
            Toast(string.Format(I18n.Get("startupRemoved", "{0} removed"), item.Name));
            Load();
        }

        void Backup()
        {
            List<BackupStartupItem> backup = new List<BackupStartupItem>();
            foreach (StartupItem x in _items)
                backup.Add(new BackupStartupItem(x.Name, x.FileLocation,
                    x.RegistryLocation.ToString(), x.StartupType.ToString()));

            if (backup.Count == 0)
            {
                Toast(I18n.Get("startupNothingToBackup", "Nothing to back up"));
                return;
            }

            try
            {
                string name = Utilities.SanitizeFileFolderName(
                    string.Format("Startup - [{0}-{1}]",
                        DateTime.Now.ToShortDateString(), DateTime.Now.ToShortTimeString()));
                File.WriteAllText(Path.Combine(CoreHelper.StartupItemsBackupFolder, name + ".json"),
                                  JsonConvert.SerializeObject(backup, Formatting.Indented));
                Toast(string.Format(I18n.Get("startupBackedUp", "{0} items backed up"), backup.Count));
            }
            catch (Exception ex)
            {
                Logger.LogError("StartupScreen.Backup", ex.Message, ex.StackTrace);
                Toast(I18n.Get("startupBackupFailed", "Backup failed"));
            }
        }

        void Restore()
        {
            using (StartupRestoreForm f = new StartupRestoreForm())
                f.ShowDialog(FindForm());
            Load();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && _rowMenu != null) _rowMenu.Dispose();
            base.Dispose(disposing);
        }

        protected override void Relayout()
        {
            int w = Math.Max(0, Width - Pad * 2);
            _table.SetBounds(Pad, 0, w, Math.Max(_table.ContentHeight, NocturneScale.S(120)));
            Body.Height = _table.Height + NocturneScale.S(20);
        }
    }
}
