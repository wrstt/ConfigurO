using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Windows.Forms;

namespace ConfigurO
{
    /// <summary>
    /// The ConfigurO shell.
    ///
    /// Replaces the legacy tabbed window: a borderless frame with a custom
    /// title bar, a 208px navigation rail, and one <see cref="NScreen"/> per
    /// tool swapped into the content pane. Screens are built lazily the first
    /// time they are shown, so startup only pays for the default screen.
    /// </summary>
    internal sealed class MainForm : NocturneShell
    {
        readonly NSidebar _sidebar = new NSidebar();
        readonly NPanel _content = new NPanel();
        readonly NToast _toast = new NToast();
        readonly Dictionary<string, NScreen> _screens = new Dictionary<string, NScreen>();

        NotifyIcon _tray;
        ContextMenuStrip _trayMenu;
        NScreen _active;

        readonly bool _disableIndicium, _disableHostsEditor, _disableAppsTool, _disableUWPApps;
        readonly bool _disableStartupTool, _disableCleaner, _disableIntegrator, _disablePinger;

        internal MainForm(SplashForm splashForm,
                          bool? disableIndicium = null, bool? disableHostsEditor = null,
                          bool? disableCommonApps = null, bool? disableUWPApps = null,
                          bool? disableStartups = null, bool? disableCleaner = null,
                          bool? disableIntegrator = null, bool? disablePinger = null)
        {
            // The legacy code parsed version numbers and sizes with the ambient
            // culture, which breaks on comma-decimal locales. The UI is
            // translated; the parsing is not.
            CultureInfo.DefaultThreadCurrentCulture = new CultureInfo("en-US");
            CultureInfo.DefaultThreadCurrentUICulture = new CultureInfo("en-US");

            Options o = OptionsHelper.CurrentOptions;
            _disableStartupTool = disableStartups ?? o.DisableStartupTool;
            _disableUWPApps = disableUWPApps ?? o.DisableUWPApps;
            _disableAppsTool = disableCommonApps ?? o.DisableAppsTool;
            _disablePinger = disablePinger ?? o.DisablePinger;
            _disableCleaner = disableCleaner ?? o.DisableCleaner;
            _disableHostsEditor = disableHostsEditor ?? o.DisableHostsEditor;
            _disableIndicium = disableIndicium ?? o.DisableIndicium;
            _disableIntegrator = disableIntegrator ?? o.DisableIntegrator;

            Status(splashForm, "preparing interface");

            NocturneTheme.Current = o.ThemeMode;
            Text = "ConfigurO";
            Icon = LoadAppIcon();
            ClientSize = NocturneScale.S(NocturneTheme.WindowDefaultSize);

            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;

            BuildChrome();
            BuildNavigation();

            Status(splashForm, "loading tools");

            TweakRunner.RestartNeeded += OnRestartNeeded;

            Load += OnLoaded;
            FormClosing += OnClosing;
        }

        static void Status(SplashForm splash, string message)
        {
            if (splash == null || splash.IsDisposed) return;
            try { Utilities.SetControlPropertyThreadSafe(splash.LoadingStatus, "Text", message); }
            catch (Exception ex) { Logger.LogError("MainForm.Status", ex.Message, ex.StackTrace); }
        }

        /// <summary>
        /// The window icon, taken from the embedded multi-resolution .ico so
        /// Windows can pick a sharp size for the taskbar and Alt-Tab.
        /// ExtractAssociatedIcon only ever yields 32x32, which is soft on a
        /// scaled display.
        /// </summary>
        static Icon LoadAppIcon()
        {
            try
            {
                using (System.IO.Stream s = typeof(MainForm).Assembly
                           .GetManifestResourceStream("ConfigurO.ConfigurO.ico"))
                {
                    if (s != null) return new Icon(s);
                }
            }
            catch (Exception ex) { Logger.LogError("MainForm.LoadAppIcon", ex.Message, ex.StackTrace); }

            try { return Icon.ExtractAssociatedIcon(Application.ExecutablePath); }
            catch (Exception ex)
            {
                Logger.LogError("MainForm.LoadAppIcon-fallback", ex.Message, ex.StackTrace);
                return null;
            }
        }

        // ── Chrome ──────────────────────────────────────────────────────
        void BuildChrome()
        {
            NTitleBar bar = new NTitleBar
            {
                VersionTag = Program.GetCurrentVersionTostring(),
                OsSummary = WindowsRelease.ChromeSummary()
            };
            bar.ThemeClicked += (s, e) => ToggleTheme();
            InstallTitleBar(bar);

            _content.BackColor = Color.Transparent;
            _sidebar.Navigated += (s, id) => Navigate(id);

            Controls.Add(_content);
            Controls.Add(_sidebar);
            Controls.Add(_toast);
            _toast.BringToFront();
        }

        void ToggleTheme()
        {
            NocturneTheme.Toggle();
            OptionsHelper.CurrentOptions.ThemeMode = NocturneTheme.Current;
            OptionsHelper.SaveSettings();
            TitleBar.Invalidate();
            Invalidate(true);
        }

        // ── Navigation ──────────────────────────────────────────────────
        void BuildNavigation()
        {
            Register(new TweaksScreen(), true);
            Register(new CleanerScreen(), !_disableCleaner);
            Register(new StartupScreen(), !_disableStartupTool);
            Register(new HostsScreen(), !_disableHostsEditor);
            Register(new AppsScreen(), !_disableAppsTool);
            Register(new NetworkScreen(), !_disablePinger);
            Register(new UwpScreen(), !_disableUWPApps && Utilities.CurrentWindowsVersion != WindowsVersion.Windows7);
            Register(new HardwareScreen(), !_disableIndicium);
            Register(new IntegratorScreen(), !_disableIntegrator);
            Register(new SettingsScreen(), true);

            _sidebar.SetItems(_navItems);
        }

        readonly List<NNavItem> _navItems = new List<NNavItem>();

        void Register(NScreen screen, bool enabled)
        {
            _screens[screen.Id] = screen;
            _navItems.Add(new NNavItem
            {
                Id = screen.Id,
                Icon = screen.Icon,
                Label = screen.NavLabel,
                Enabled = enabled
            });
        }

        internal void Navigate(string id)
        {
            NScreen next;
            if (!_screens.TryGetValue(id, out next)) return;

            NNavItem item = _navItems.FirstOrDefault(i => i.Id == id);
            if (item == null || !item.Enabled) return;

            if (_active == next) { next.Activate(); return; }

            SuspendLayout();
            try
            {
                if (_active != null)
                {
                    _active.Deactivate();
                    _active.Visible = false;
                }

                next.EnsureBuilt();
                if (!_content.Controls.Contains(next)) _content.Controls.Add(next);
                next.Visible = true;
                next.BringToFront();
                _active = next;
            }
            finally { ResumeLayout(true); }

            next.Activate();
            _sidebar.Selected = id;
            RefreshFooter();
            OptionsHelper.CurrentOptions.LastScreen = id;
        }

        /// <summary>Keeps the sidebar footer's applied-tweak count current.</summary>
        internal void RefreshFooter()
        {
            int applied = TweakRegistry.AppliedCount(OptionsHelper.CurrentOptions);
            _sidebar.FooterPrimary = string.Format(
                I18n.Get("sidebarApplied", "{0} tweaks applied"), applied);

            // Report when policies were actually last reinforced rather than
            // always claiming "today" the way the design mock does.
            DateTime last = OptionsHelper.CurrentOptions.LastReinforced;
            if (last == default(DateTime))
                _sidebar.FooterSecondary = I18n.Get("sidebarNeverReinforced", "Policies never reinforced");
            else if (last.Date == DateTime.Now.Date)
                _sidebar.FooterSecondary = I18n.Get("sidebarPolicies", "Policies reinforced today");
            else
                _sidebar.FooterSecondary = string.Format(
                    I18n.Get("sidebarReinforcedOn", "Policies reinforced {0}"), last.ToShortDateString());

            _sidebar.Invalidate();
        }

        // ── Lifecycle ───────────────────────────────────────────────────
        void OnLoaded(object sender, EventArgs e)
        {
            string start = OptionsHelper.CurrentOptions.LastScreen;
            if (string.IsNullOrEmpty(start) || !_screens.ContainsKey(start)) start = TweaksScreen.ScreenId;
            NNavItem item = _navItems.FirstOrDefault(i => i.Id == start);
            if (item == null || !item.Enabled) start = TweaksScreen.ScreenId;
            Navigate(start);

            ApplyTraySetting(OptionsHelper.CurrentOptions.EnableTray);

            if (OptionsHelper.CurrentOptions.UpdateOnLaunch)
                UpdateHelper.CheckAsync(this, silent: true);
        }

        void OnClosing(object sender, FormClosingEventArgs e)
        {
            OptionsHelper.SaveSettings();

            // Disposed, not just hidden. A NotifyIcon that is only made
            // invisible can leave its slot in the notification area behind
            // until something makes Windows repaint it -- the icon is gone but
            // the gap it occupied answers the mouse. Disposing removes it now.
            if (_tray != null) { _tray.Visible = false; _tray.Dispose(); _tray = null; }
            if (_trayMenu != null) { _trayMenu.Dispose(); _trayMenu = null; }
        }

        protected override void OnLayout(LayoutEventArgs e)
        {
            base.OnLayout(e);
            if (TitleBar == null) return;

            // Title bar across the top, rail down the left of what remains,
            // content in the rest. Explicit beats three competing DockStyles.
            TitleBar.SetBounds(0, 0, ClientSize.Width, TitleBar.Height);

            int top = TitleBar.Height;
            _sidebar.SetBounds(0, top, _sidebar.Width, Math.Max(0, ClientSize.Height - top));
            _content.SetBounds(_sidebar.Width, top,
                               Math.Max(0, ClientSize.Width - _sidebar.Width),
                               Math.Max(0, ClientSize.Height - top));
            _toast.Reposition();
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            _toast.Reposition();
        }

        internal override void Toast(string message)
        {
            _toast.Show(message);
        }

        void OnRestartNeeded(object sender, EventArgs e)
        {
            if (_active == null) return;
            _active.Banner.Show(I18n.Get("restartAndApply", "Restart to finish applying these changes"),
                                I18n.Get("restartNow", "Restart now"));
            _active.PerformLayout();
        }

        // ── Keyboard ────────────────────────────────────────────────────
        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (keyData == (Keys.Control | Keys.Tab)) { _sidebar.Step(1); return true; }
            if (keyData == (Keys.Control | Keys.Shift | Keys.Tab)) { _sidebar.Step(-1); return true; }
            if (keyData == (Keys.Control | Keys.D)) { ToggleTheme(); return true; }
            return base.ProcessCmdKey(ref msg, keyData);
        }

        // ── Tray / quick access ─────────────────────────────────────────
        internal void ApplyTraySetting(bool enabled)
        {
            if (!enabled)
            {
                if (_tray != null) { _tray.Visible = false; _tray.Dispose(); _tray = null; }
                if (_trayMenu != null) { _trayMenu.Dispose(); _trayMenu = null; }
                return;
            }
            if (_tray != null) { _tray.Visible = true; return; }

            _trayMenu = NocturneMenu.Create();

            AddTrayItem(I18n.Get("trayStartup", "Startup Manager"), StartupScreen.ScreenId);
            AddTrayItem(I18n.Get("trayCleaner", "Drive Cleaner"), CleanerScreen.ScreenId);
            AddTrayItem(I18n.Get("trayPinger", "Network"), NetworkScreen.ScreenId);
            AddTrayItem(I18n.Get("trayHosts", "HOSTS Editor"), HostsScreen.ScreenId);
            AddTrayItem(I18n.Get("trayAD", "Apps Downloader"), AppsScreen.ScreenId);
            AddTrayItem(I18n.Get("trayHW", "Hardware Information"), HardwareScreen.ScreenId);
            AddTrayItem(I18n.Get("trayRegistry", "Registry Repair"), SettingsScreen.ScreenId);
            AddTrayItem(I18n.Get("trayOptions", "Options"), SettingsScreen.ScreenId);
            _trayMenu.Items.Add(new ToolStripSeparator());

            ToolStripMenuItem unlocker = new ToolStripMenuItem(I18n.Get("trayUnlocker", "File unlocker"));
            unlocker.Click += (s, e) => new FileUnlockForm().ShowDialog(this);
            _trayMenu.Items.Add(unlocker);

            ToolStripMenuItem explorer = new ToolStripMenuItem(I18n.Get("trayRestartExplorer", "Restart Explorer"));
            explorer.Click += (s, e) => Utilities.RestartExplorer();
            _trayMenu.Items.Add(explorer);

            _trayMenu.Items.Add(new ToolStripSeparator());
            ToolStripMenuItem exit = new ToolStripMenuItem(I18n.Get("trayExit", "Exit"));
            exit.Click += (s, e) => Close();
            _trayMenu.Items.Add(exit);

            _tray = new NotifyIcon
            {
                Icon = Icon,
                Text = "ConfigurO",
                Visible = true,
                ContextMenuStrip = _trayMenu
            };
            _tray.MouseDoubleClick += (s, e) => RestoreWindow();
        }

        void AddTrayItem(string label, string screenId)
        {
            ToolStripMenuItem item = new ToolStripMenuItem(label);
            item.Click += (s, e) => { RestoreWindow(); Navigate(screenId); };
            _trayMenu.Items.Add(item);
        }

        void RestoreWindow()
        {
            Show();
            WindowState = FormWindowState.Normal;
            Activate();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                TweakRunner.RestartNeeded -= OnRestartNeeded;
                if (_tray != null) { _tray.Visible = false; _tray.Dispose(); }
                if (_trayMenu != null) _trayMenu.Dispose();

                // Screens are constructed up front but only parented once
                // navigated to, so the ones never opened are not in the
                // control tree and would keep their static theme and scale
                // subscriptions alive.
                foreach (NScreen screen in _screens.Values)
                    if (screen.Parent == null && !screen.IsDisposed) screen.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
