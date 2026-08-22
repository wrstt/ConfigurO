using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace ConfigurO
{
    /// <summary>
    /// Integrator: desktop right-click menu entries on the left, custom Run
    /// commands on the right, each a form over its existing-items list.
    ///
    /// The lower card carries the ready-made menu packs and the two shell
    /// extensions the legacy tool shipped, so nothing is lost by the move.
    /// </summary>
    internal sealed class IntegratorScreen : NScreen
    {
        internal const string ScreenId = "integrator";

        readonly NCard _menuCard = new NCard();
        readonly NTextBox _menuName = new NTextBox();
        readonly NTextBox _menuPath = new NTextBox();
        readonly NButton _menuBrowse = new NButton();
        readonly MoonCheck _shiftOnly = new MoonCheck();
        readonly NButton _menuAdd = new NButton();
        readonly NSectionLabel _menuListLabel = new NSectionLabel();
        readonly NTable _menuList = new NTable();

        readonly NCard _commandCard = new NCard();
        readonly NTextBox _keyword = new NTextBox();
        readonly NTextBox _commandPath = new NTextBox();
        readonly NButton _commandBrowse = new NButton();
        readonly NButton _commandAdd = new NButton();
        readonly NSectionLabel _commandListLabel = new NSectionLabel();
        readonly NTable _commandList = new NTable();

        readonly NCard _packsCard = new NCard();
        readonly List<MoonToggle> _packToggles = new List<MoonToggle>();

        readonly NButton _refresh = new NButton();
        readonly NButton _removeAllMenu = new NButton();
        readonly NButton _removeAllCommands = new NButton();
        List<string> _menuItems = new List<string>();
        List<string> _commands = new List<string>();
        bool _suppressPacks;

        internal override string Id { get { return ScreenId; } }
        internal override string Icon { get { return NocturneIcons.Integrator; } }
        internal override string NavLabel { get { return I18n.Get("navIntegrator", "Integrator"); } }

        // The ready-made menu packs: display name, registry item name, .reg file.
        static readonly string[][] Packs =
        {
            new[] { "Power Menu", "Power Menu", "PowerMenu.reg" },
            new[] { "Desktop shortcuts", "DesktopShortcuts", "DesktopShortcuts.reg" },
            new[] { "System shortcuts", "SystemShortcuts", "SystemShortcuts.reg" },
            new[] { "System tools", "SystemTools", "SystemTools.reg" },
            new[] { "Windows apps", "WindowsApps", "WindowsApps.reg" }
        };

        protected override void Build()
        {
            TitleText = I18n.Get("integratorTitle", "Integrator");
            SubtitleText = I18n.Get("integratorSubtitle", "Desktop menu entries and Run commands");

            // ── desktop menu ──
            _menuCard.Title = I18n.Get("integratorMenuTitle", "Desktop menu item");
            _menuCard.Icon = NocturneIcons.Cursor;
            _menuName.Placeholder = I18n.Get("integratorName", "Name");
            _menuPath.Placeholder = I18n.Get("integratorLocation", "Program location");
            _menuPath.Monospace = true;
            _menuCard.Body.Controls.Add(_menuName);
            _menuCard.Body.Controls.Add(_menuPath);

            _menuBrowse.Style = NButtonStyle.Icon;
            _menuBrowse.Icon = NocturneIcons.Folder;
            _menuBrowse.Click += (s, e) => Browse(_menuPath);
            _menuCard.Body.Controls.Add(_menuBrowse);

            _shiftOnly.Text = I18n.Get("integratorShift", "Show only when SHIFT is pressed");
            _menuCard.Body.Controls.Add(_shiftOnly);

            _menuAdd.Style = NButtonStyle.Primary;
            _menuAdd.Text = I18n.Get("btnAddItem", "Add to menu");
            _menuAdd.Click += (s, e) => AddMenuItem();
            _menuCard.Body.Controls.Add(_menuAdd);

            _menuListLabel.Text = I18n.Get("removeIntegratorItemsL", "Existing items");
            _menuCard.Body.Controls.Add(_menuListLabel);

            _removeAllMenu.Style = NButtonStyle.Ghost;
            _removeAllMenu.Text = I18n.Get("removeAllIIB", "Delete all");
            _removeAllMenu.Click += (s, e) => RemoveAllMenuItems();
            _menuCard.Body.Controls.Add(_removeAllMenu);

            _menuList.RowHeight = 30;
            _menuList.HeaderHeight = 0;
            _menuList.SetColumns(
                new NColumn { Header = string.Empty, Weight = 1f },
                new NColumn { Header = string.Empty, Width = 40, Kind = NCellKind.Action, Icon = NocturneIcons.Trash });
            _menuList.ActionClicked += (s, e) => RemoveMenuItem(e.RowIndex);
            _menuCard.Body.Controls.Add(_menuList);
            Body.Controls.Add(_menuCard);

            // ── run commands ──
            _commandCard.Title = I18n.Get("integratorCommandTitle", "Custom Run commands");
            _commandCard.Icon = NocturneIcons.Terminal;
            _keyword.Placeholder = I18n.Get("integratorKeyword", "Keyword");
            _commandPath.Placeholder = I18n.Get("integratorFile", "File location");
            _commandPath.Monospace = true;
            _commandCard.Body.Controls.Add(_keyword);
            _commandCard.Body.Controls.Add(_commandPath);

            _commandBrowse.Style = NButtonStyle.Icon;
            _commandBrowse.Icon = NocturneIcons.Folder;
            _commandBrowse.Click += (s, e) => Browse(_commandPath);
            _commandCard.Body.Controls.Add(_commandBrowse);

            _commandAdd.Style = NButtonStyle.Primary;
            _commandAdd.Text = I18n.Get("btnCreateCustomCommand", "Create");
            _commandAdd.Click += (s, e) => AddCommand();
            _commandCard.Body.Controls.Add(_commandAdd);

            _commandListLabel.Text = I18n.Get("removeCCL", "Existing commands");
            _commandCard.Body.Controls.Add(_commandListLabel);

            _removeAllCommands.Style = NButtonStyle.Ghost;
            _removeAllCommands.Text = I18n.Get("removeAllIIB", "Delete all");
            _removeAllCommands.Click += (s, e) => RemoveAllCommands();
            _commandCard.Body.Controls.Add(_removeAllCommands);

            _commandList.RowHeight = 30;
            _commandList.HeaderHeight = 0;
            _commandList.SetColumns(
                new NColumn { Header = string.Empty, Width = 110, Kind = NCellKind.Tag },
                new NColumn { Header = string.Empty, Weight = 1f, Kind = NCellKind.Mono },
                new NColumn { Header = string.Empty, Width = 40, Kind = NCellKind.Action, Icon = NocturneIcons.Trash });
            _commandList.ActionClicked += (s, e) => RemoveCommand(e.RowIndex);
            _commandCard.Body.Controls.Add(_commandList);
            Body.Controls.Add(_commandCard);

            // ── ready-made packs and shell extensions ──
            _packsCard.Title = I18n.Get("integratorPacksTitle", "Ready-made menus");
            _packsCard.Icon = NocturneIcons.Add;
            foreach (string[] pack in Packs) AddPackToggle(pack[0]);
            AddPackToggle(I18n.Get("integratorOpenCmd", "\"Open with CMD\" on folders"));
            AddPackToggle(I18n.Get("integratorTakeOwnership", "\"Take ownership\" on files"));
            _packsCard.Body.Paint += PaintPackLabels;
            Body.Controls.Add(_packsCard);

            _refresh.Style = NButtonStyle.Icon;
            _refresh.Icon = NocturneIcons.Refresh;
            _refresh.Click += (s, e) => Load();
            AddAction(_refresh);
        }

        void AddPackToggle(string label)
        {
            MoonToggle t = new MoonToggle { Tag = label };
            t.CheckedChanged += (s, e) => TogglePack((MoonToggle)s);
            _packToggles.Add(t);
            _packsCard.Body.Controls.Add(t);
        }

        internal override void Activate() { Load(); }

        void Load()
        {
            _menuList.Clear();
            try { _menuItems = IntegratorHelper.GetDesktopItems(); }
            catch (Exception ex)
            {
                Logger.LogError("IntegratorScreen.GetDesktopItems", ex.Message, ex.StackTrace);
                _menuItems = new List<string>();
            }
            foreach (string i in _menuItems) _menuList.AddRow(new[] { i, string.Empty });

            _commandList.Clear();
            try { _commands = IntegratorHelper.GetCustomCommands(); }
            catch (Exception ex)
            {
                Logger.LogError("IntegratorScreen.GetCustomCommands", ex.Message, ex.StackTrace);
                _commands = new List<string>();
            }
            foreach (string c in _commands)
                _commandList.AddRow(new[] { c.Replace(".exe", string.Empty), CommandPath(c), string.Empty });

            RefreshPackStates();
            PerformLayout();
        }

        void RemoveAllMenuItems()
        {
            if (_menuItems.Count == 0) return;
            if (!HelperForm.Confirm(FindForm(),
                    I18n.Get("removeAllItems", "Are you sure you want to delete all desktop items?"))) return;
            IntegratorHelper.RemoveAllItems(_menuItems);
            Toast(I18n.Get("integratorRemovedAll", "Desktop menu items removed"));
            Load();
        }

        void RemoveAllCommands()
        {
            if (_commands.Count == 0) return;
            if (!HelperForm.Confirm(FindForm(),
                    I18n.Get("removeAllCommands", "Are you sure you want to delete all custom commands?"))) return;
            foreach (string c in _commands) IntegratorHelper.DeleteCustomCommand(c);
            Toast(I18n.Get("integratorCommandsRemovedAll", "Custom commands removed"));
            Load();
        }

        static string CommandPath(string command)
        {
            try
            {
                object v = Microsoft.Win32.Registry.GetValue(
                    @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\App Paths\" + command,
                    "", string.Empty);
                return v == null ? string.Empty : v.ToString();
            }
            catch (Exception ex)
            {
                Logger.LogError("IntegratorScreen.CommandPath", ex.Message, ex.StackTrace);
                return string.Empty;
            }
        }

        void RefreshPackStates()
        {
            _suppressPacks = true;
            try
            {
                for (int i = 0; i < Packs.Length; i++)
                    _packToggles[i].Checked = IntegratorHelper.DesktopItemExists(Packs[i][1]);
                _packToggles[Packs.Length].Checked = IntegratorHelper.OpenWithCMDExists();
                _packToggles[Packs.Length + 1].Checked = IntegratorHelper.TakeOwnershipExists();
            }
            catch (Exception ex) { Logger.LogError("IntegratorScreen.RefreshPackStates", ex.Message, ex.StackTrace); }
            finally { _suppressPacks = false; }
        }

        void TogglePack(MoonToggle toggle)
        {
            if (_suppressPacks) return;
            int index = _packToggles.IndexOf(toggle);
            if (index < 0) return;

            try
            {
                if (index < Packs.Length)
                {
                    if (toggle.Checked)
                        Utilities.ImportRegistryScript(CoreHelper.ReadyMadeMenusFolder + Packs[index][2]);
                    else
                        IntegratorHelper.RemoveItem(Packs[index][1]);
                }
                else if (index == Packs.Length)
                {
                    if (toggle.Checked) IntegratorHelper.InstallOpenWithCMD();
                    else IntegratorHelper.DeleteOpenWithCMD();
                }
                else
                {
                    IntegratorHelper.InstallTakeOwnership(!toggle.Checked);
                }
                Toast(I18n.Get("integratorMenuUpdated", "Right-click menu updated"));
            }
            catch (Exception ex)
            {
                Logger.LogError("IntegratorScreen.TogglePack", ex.Message, ex.StackTrace);
                Toast(I18n.Get("integratorPackFailed", "Could not change that menu"));
            }
            Load();
        }

        static void Browse(NTextBox target)
        {
            using (OpenFileDialog d = new OpenFileDialog())
            {
                d.Filter = "Programs|*.exe;*.bat;*.cmd|All files|*.*";
                if (d.ShowDialog() == DialogResult.OK) target.Text = d.FileName;
            }
        }

        void AddMenuItem()
        {
            string name = _menuName.Text.Trim();
            string path = _menuPath.Text.Trim();

            if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(path))
            {
                Toast(I18n.Get("integratorNeedBoth", "Enter a name and a program location"));
                return;
            }
            if (IntegratorHelper.DesktopItemExists(name))
            {
                Toast(I18n.Get("integratorExists", "A menu item with that name already exists"));
                return;
            }

            try
            {
                string icon = IntegratorHelper.ExtractIconFromExecutable(name, path);
                IntegratorHelper.AddItem(name, path, icon, DesktopTypePosition.Top,
                                         _shiftOnly.Checked, DesktopItemType.Program);
                _menuName.Text = string.Empty;
                _menuPath.Text = string.Empty;
                Toast(string.Format(I18n.Get("integratorAdded", "{0} added to the desktop menu"), name));
            }
            catch (Exception ex)
            {
                Logger.LogError("IntegratorScreen.AddMenuItem", ex.Message, ex.StackTrace);
                Toast(I18n.Get("integratorAddFailed", "Could not add that menu item"));
            }
            Load();
        }

        void RemoveMenuItem(int row)
        {
            if (row < 0 || row >= _menuItems.Count) return;
            string name = _menuItems[row];
            IntegratorHelper.RemoveItem(name);
            Toast(string.Format(I18n.Get("integratorRemoved", "{0} removed"), name));
            Load();
        }

        void AddCommand()
        {
            string keyword = _keyword.Text.Trim();
            string path = _commandPath.Text.Trim();

            if (string.IsNullOrEmpty(keyword) || string.IsNullOrEmpty(path))
            {
                Toast(I18n.Get("integratorNeedKeyword", "Enter a keyword and a file location"));
                return;
            }
            if (!File.Exists(path) && !Directory.Exists(path))
            {
                Toast(I18n.Get("integratorNoSuchFile", "That file does not exist"));
                return;
            }

            try
            {
                IntegratorHelper.CreateCustomCommand(path, keyword);
                _keyword.Text = string.Empty;
                _commandPath.Text = string.Empty;
                Toast(string.Format(I18n.Get("integratorCommandAdded", "Run \"{0}\" created"), keyword));
            }
            catch (Exception ex)
            {
                Logger.LogError("IntegratorScreen.AddCommand", ex.Message, ex.StackTrace);
                Toast(I18n.Get("integratorCommandFailed", "Could not create that command"));
            }
            Load();
        }

        void RemoveCommand(int row)
        {
            if (row < 0 || row >= _commands.Count) return;
            IntegratorHelper.DeleteCustomCommand(_commands[row]);
            Toast(I18n.Get("integratorCommandRemoved", "Command removed"));
            Load();
        }

        void PaintPackLabels(object sender, PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            NocturneDraw.Prepare(g);
            int rowH = NocturneScale.S(30);
            using (Font f = NocturneFonts.Row())
            {
                for (int i = 0; i < _packToggles.Count; i++)
                {
                    NocturneDraw.Text(g, (string)_packToggles[i].Tag, f, NocturneTheme.Text,
                        new RectangleF(0, i * rowH,
                                       Math.Max(0, _packsCard.Body.Width - NocturneScale.S(56)), rowH),
                        NocturneDraw.Left);
                    if (i < _packToggles.Count - 1)
                        NocturneTheme.DrawFadedRule(g, 0, i * rowH + rowH - 1,
                                                    _packsCard.Body.Width, NocturneTheme.Border);
                }
            }
        }

        protected override void Relayout()
        {
            int gap = NocturneScale.S(16);
            int w = Math.Max(0, Width - Pad * 2);
            int colW = (w - gap) / 2;
            int fieldH = NocturneScale.S(NocturneTheme.InputHeight);
            int browse = NocturneScale.S(36);

            // ── desktop menu card ──
            int listH = Math.Max(NocturneScale.S(60), _menuList.ContentHeight);
            int menuH = NocturneScale.S(34) + fieldH * 2 + NocturneScale.S(8)
                      + NocturneScale.S(28) + NocturneScale.S(34) + NocturneScale.S(30)
                      + listH + NocturneScale.S(26);
            _menuCard.SetBounds(Pad, 0, colW, menuH);
            int bw = _menuCard.Body.Width;
            _menuName.SetBounds(0, 0, bw, fieldH);
            _menuPath.SetBounds(0, fieldH + NocturneScale.S(8), bw - browse - NocturneScale.S(8), fieldH);
            _menuBrowse.SetBounds(bw - browse, fieldH + NocturneScale.S(8), browse, fieldH);
            _shiftOnly.SetBounds(0, fieldH * 2 + NocturneScale.S(18), bw, NocturneScale.S(20));
            _menuAdd.SetBounds(0, fieldH * 2 + NocturneScale.S(44), bw, NocturneScale.S(34));
            int delW = NocturneScale.S(78);
            _menuListLabel.SetBounds(0, fieldH * 2 + NocturneScale.S(88), bw - delW, NocturneScale.S(18));
            _removeAllMenu.SetBounds(bw - delW, fieldH * 2 + NocturneScale.S(84), delW, NocturneScale.S(26));
            _menuList.SetBounds(0, fieldH * 2 + NocturneScale.S(110), bw, listH);

            // ── run commands card ──
            int cmdListH = Math.Max(NocturneScale.S(60), _commandList.ContentHeight);
            int cmdH = NocturneScale.S(34) + fieldH * 2 + NocturneScale.S(8)
                     + NocturneScale.S(34) + NocturneScale.S(30) + cmdListH + NocturneScale.S(52);
            _commandCard.SetBounds(Pad + colW + gap, 0, colW, cmdH);
            int cw = _commandCard.Body.Width;
            _keyword.SetBounds(0, 0, cw, fieldH);
            _commandPath.SetBounds(0, fieldH + NocturneScale.S(8), cw - browse - NocturneScale.S(8), fieldH);
            _commandBrowse.SetBounds(cw - browse, fieldH + NocturneScale.S(8), browse, fieldH);
            _commandAdd.SetBounds(0, fieldH * 2 + NocturneScale.S(18), cw, NocturneScale.S(34));
            _commandListLabel.SetBounds(0, fieldH * 2 + NocturneScale.S(62), cw - delW, NocturneScale.S(18));
            _removeAllCommands.SetBounds(cw - delW, fieldH * 2 + NocturneScale.S(58), delW, NocturneScale.S(26));
            _commandList.SetBounds(0, fieldH * 2 + NocturneScale.S(84), cw, cmdListH);

            // ── packs ──
            int y = Math.Max(menuH, cmdH) + gap;
            int rowH = NocturneScale.S(30);
            int packsH = NocturneScale.S(34) + _packToggles.Count * rowH + NocturneScale.S(26);
            _packsCard.SetBounds(Pad, y, w, packsH);
            for (int i = 0; i < _packToggles.Count; i++)
            {
                _packToggles[i].Location = new Point(
                    _packsCard.Body.Width - _packToggles[i].Width,
                    i * rowH + (rowH - _packToggles[i].Height) / 2);
            }

            Body.Height = y + packsH + NocturneScale.S(20);
        }
    }
}
