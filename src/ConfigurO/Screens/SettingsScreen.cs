using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace ConfigurO
{
    /// <summary>
    /// Settings: language and behaviour on the left; updates, troubleshooting
    /// and About on the right.
    /// </summary>
    internal sealed class SettingsScreen : NScreen
    {
        internal const string ScreenId = "settings";

        sealed class BehaviourToggle
        {
            internal string Label;
            internal MoonToggle Toggle;
            internal Func<Options, bool> Get;
            internal Action<Options, bool> Set;
            internal Action<bool> Apply;
        }

        readonly NCard _languageCard = new NCard();
        readonly MoonSelect _language = new MoonSelect();

        readonly NCard _behaviourCard = new NCard();
        readonly List<BehaviourToggle> _behaviour = new List<BehaviourToggle>();

        readonly NCard _updatesCard = new NCard();
        readonly NButton _checkUpdate = new NButton();
        readonly NButton _viewChanges = new NButton();

        readonly NCard _troubleCard = new NCard();
        readonly NButton _viewErrors = new NButton();
        readonly NButton _openFolder = new NButton();
        readonly NButton _systemFont = new NButton();
        readonly NButton _repair = new NButton();

        readonly NCard _accessCard = new NCard();
        readonly List<KeyValuePair<string, Action>> _accessFixes = new List<KeyValuePair<string, Action>>();
        readonly List<MoonCheck> _accessChecks = new List<MoonCheck>();
        readonly NButton _accessFix = new NButton();

        readonly NCard _restartCard = new NCard();
        readonly NButton _restartNormal = new NButton();
        readonly NButton _restartSafe = new NButton();
        readonly NButton _restartDefender = new NButton();

        readonly NCard _aboutCard = new NCard();
        readonly NButton _linkSource = new NButton();
        readonly NButton _linkBug = new NButton();
        readonly NButton _linkFeature = new NButton();
        readonly NButton _linkFaq = new NButton();
        readonly NButton _linkLicense = new NButton();

        readonly NButton _setFont = new NButton();

        bool _suppress;

        internal override string Id { get { return ScreenId; } }
        internal override string Icon { get { return NocturneIcons.Settings; } }
        internal override string NavLabel { get { return I18n.Get("navSettings", "Settings"); } }

        // Native language names, paired with their code, in the legacy order.
        static readonly KeyValuePair<string, LanguageCode>[] Languages =
        {
            new KeyValuePair<string, LanguageCode>(Constants.ENGLISH, LanguageCode.EN),
            new KeyValuePair<string, LanguageCode>(Constants.RUSSIAN, LanguageCode.RU),
            new KeyValuePair<string, LanguageCode>(Constants.TURKISH, LanguageCode.TR),
            new KeyValuePair<string, LanguageCode>(Constants.HELLENIC, LanguageCode.EL),
            new KeyValuePair<string, LanguageCode>(Constants.GERMAN, LanguageCode.DE),
            new KeyValuePair<string, LanguageCode>(Constants.PORTUGUESE, LanguageCode.PT),
            new KeyValuePair<string, LanguageCode>(Constants.FRENCH, LanguageCode.FR),
            new KeyValuePair<string, LanguageCode>(Constants.SPANISH, LanguageCode.ES),
            new KeyValuePair<string, LanguageCode>(Constants.ITALIAN, LanguageCode.IT),
            new KeyValuePair<string, LanguageCode>(Constants.CHINESE, LanguageCode.CN),
            new KeyValuePair<string, LanguageCode>(Constants.TAIWANESE, LanguageCode.TW),
            new KeyValuePair<string, LanguageCode>(Constants.CZECH, LanguageCode.CZ),
            new KeyValuePair<string, LanguageCode>(Constants.KOREAN, LanguageCode.KO),
            new KeyValuePair<string, LanguageCode>(Constants.POLISH, LanguageCode.PL),
            new KeyValuePair<string, LanguageCode>(Constants.ARABIC, LanguageCode.AR),
            new KeyValuePair<string, LanguageCode>(Constants.KURDISH, LanguageCode.KU),
            new KeyValuePair<string, LanguageCode>(Constants.HUNGARIAN, LanguageCode.HU),
            new KeyValuePair<string, LanguageCode>(Constants.ROMANIAN, LanguageCode.RO),
            new KeyValuePair<string, LanguageCode>(Constants.DUTCH, LanguageCode.NL),
            new KeyValuePair<string, LanguageCode>(Constants.UKRAINIAN, LanguageCode.UA),
            new KeyValuePair<string, LanguageCode>(Constants.JAPANESE, LanguageCode.JA),
            new KeyValuePair<string, LanguageCode>(Constants.PERSIAN, LanguageCode.FA),
            new KeyValuePair<string, LanguageCode>(Constants.NEPALI, LanguageCode.NE),
            new KeyValuePair<string, LanguageCode>(Constants.BULGARIAN, LanguageCode.BG),
            new KeyValuePair<string, LanguageCode>(Constants.VIETNAMESE, LanguageCode.VN),
            new KeyValuePair<string, LanguageCode>(Constants.URDU, LanguageCode.UR),
            new KeyValuePair<string, LanguageCode>(Constants.INDONESIAN, LanguageCode.ID),
            new KeyValuePair<string, LanguageCode>(Constants.CROATIAN, LanguageCode.HR)
        };

        protected override void Build()
        {
            TitleText = I18n.Get("settingsTitle", "Settings");
            SubtitleText = string.Format(I18n.Get("settingsSubtitle", "ConfigurO {0} · {1}"),
                Program.GetCurrentVersionTostring(), WindowsRelease.ChromeSummary());

            // ── language ──
            _languageCard.Title = I18n.Get("settingsLanguage", "Language");
            _languageCard.Icon = "global-line";
            foreach (KeyValuePair<string, LanguageCode> l in Languages) _language.Items.Add(l.Key);
            _language.SelectedIndexChanged += (s, e) => ChangeLanguage();
            _languageCard.Body.Controls.Add(_language);
            Body.Controls.Add(_languageCard);

            // ── behaviour ──
            _behaviourCard.Title = I18n.Get("settingsBehaviour", "Behavior");
            _behaviourCard.Icon = NocturneIcons.Settings;

            AddBehaviour(I18n.Get("settingsQuickAccess", "Show Quick Access menu"),
                o => o.EnableTray, (o, v) => o.EnableTray = v,
                v => { MainForm shell = FindForm() as MainForm; if (shell != null) shell.ApplyTraySetting(v); });

            AddBehaviour(I18n.Get("settingsHelpMessages", "Show help messages"),
                o => o.ShowHelpMessages, (o, v) => o.ShowHelpMessages = v, null);

            AddBehaviour(I18n.Get("settingsAutoStart", "Start with Windows"),
                o => o.AutoStart, (o, v) => o.AutoStart = v,
                v => { if (v) Utilities.RegisterAutoStart(); else Utilities.UnregisterAutoStart(); });

            AddBehaviour(I18n.Get("settingsUpdateOnLaunch", "Check for updates on launch"),
                o => o.UpdateOnLaunch, (o, v) => o.UpdateOnLaunch = v, null);

            if (WindowsRelease.SupportsBackdrop)
            {
                AddBehaviour(I18n.Get("settingsMica", "Use the Windows 11 Mica material"),
                    o => o.UseMica, (o, v) => o.UseMica = v,
                    v =>
                    {
                        NocturneShell shell = FindForm() as NocturneShell;
                        if (shell != null) DwmChrome.Apply(shell, v);
                    });
            }

            _behaviourCard.Body.Paint += PaintBehaviourLabels;
            Body.Controls.Add(_behaviourCard);

            // ── updates ──
            _updatesCard.Title = I18n.Get("settingsUpdates", "Updates");
            _updatesCard.Icon = NocturneIcons.Refresh;
            _updatesCard.Note = string.Format(I18n.Get("settingsVersion", "You are running version {0}"),
                                              Program.GetCurrentVersionTostring());

            _checkUpdate.Style = NButtonStyle.Secondary;
            _checkUpdate.Text = I18n.Get("btnUpdate", "Check for update");
            _checkUpdate.Click += (s, e) => UpdateHelper.CheckInteractive(FindForm());
            _updatesCard.Body.Controls.Add(_checkUpdate);

            _viewChanges.Style = NButtonStyle.Ghost;
            _viewChanges.Text = I18n.Get("btnChangelog", "View changes");
            _viewChanges.Click += (s, e) => ShowChangelog();
            _updatesCard.Body.Controls.Add(_viewChanges);
            Body.Controls.Add(_updatesCard);

            // ── troubleshooting ──
            _troubleCard.Title = I18n.Get("lblTroubleshoot", "Troubleshooting");
            _troubleCard.Icon = NocturneIcons.Bug;

            _viewErrors.Style = NButtonStyle.Ghost;
            _viewErrors.Text = I18n.Get("btnViewLog", "View errors");
            _viewErrors.Click += (s, e) => ShowErrors();
            _troubleCard.Body.Controls.Add(_viewErrors);

            _openFolder.Style = NButtonStyle.Ghost;
            _openFolder.Text = I18n.Get("btnOpenConf", "Configuration folder");
            _openFolder.Click += (s, e) => OpenFolder();
            _troubleCard.Body.Controls.Add(_openFolder);

            _setFont.Style = NButtonStyle.Ghost;
            _setFont.Text = I18n.Get("btnSetGlobalFont", "Set system font");
            _setFont.Click += (s, e) => SetSystemFont();
            _troubleCard.Body.Controls.Add(_setFont);

            _systemFont.Style = NButtonStyle.Ghost;
            _systemFont.Text = I18n.Get("btnRestoreFont", "Restore system font");
            _systemFont.Click += (s, e) => RestoreSystemFont();
            _troubleCard.Body.Controls.Add(_systemFont);

            _repair.Style = NButtonStyle.Secondary;
            _repair.Text = I18n.Get("btnResetConfig", "Repair");
            _repair.Click += (s, e) => Repair();
            _troubleCard.Body.Controls.Add(_repair);
            Body.Controls.Add(_troubleCard);

            // ── restore Windows access ──
            // The registry-repair tool: undoes the lockdowns malware and
            // over-zealous group policy leave behind.
            _accessCard.Title = I18n.Get("settingsAccess", "Restore Windows access");
            _accessCard.Icon = NocturneIcons.Key;
            _accessCard.Note = I18n.Get("settingsAccessNote", "Re-enables tools a policy or infection has blocked");

            AddAccessFix(I18n.Get("checkTaskManager", "Task Manager"), Utilities.EnableTaskManager);
            AddAccessFix(I18n.Get("checkRegistryEditor", "Registry Editor"), Utilities.EnableRegistryEditor);
            AddAccessFix(I18n.Get("checkCommandPrompt", "Command Prompt"), Utilities.EnableCommandPrompt);
            AddAccessFix(I18n.Get("checkControlPanel", "Control Panel"), Utilities.EnableControlPanel);
            AddAccessFix(I18n.Get("checkFolderOptions", "Folder Options"), Utilities.EnableFolderOptions);
            AddAccessFix(I18n.Get("checkRunDialog", "Run dialog"), Utilities.EnableRunDialog);
            AddAccessFix(I18n.Get("checkContextMenu", "Right-click menu"), Utilities.EnableContextMenu);
            AddAccessFix(I18n.Get("checkFirewall", "Windows Firewall"), Utilities.EnableFirewall);

            _accessFix.Style = NButtonStyle.Primary;
            _accessFix.Text = I18n.Get("regFixB", "Fix");
            _accessFix.Click += (s, e) => ApplyAccessFixes();
            _accessCard.Body.Controls.Add(_accessFix);
            _accessCard.Body.Paint += PaintAccessLabels;
            Body.Controls.Add(_accessCard);

            // ── restart ──
            _restartCard.Title = I18n.Get("settingsRestart", "Restart");
            _restartCard.Icon = NocturneIcons.Restart;
            _restartCard.Note = I18n.Get("settingsRestartNote", "Disabling Defender completes in safe mode");

            _restartNormal.Style = NButtonStyle.Secondary;
            _restartNormal.Text = I18n.Get("btnRestart", "Restart in Normal Mode");
            _restartNormal.Click += (s, e) => Restart(RestartType.Normal);
            _restartCard.Body.Controls.Add(_restartNormal);

            _restartSafe.Style = NButtonStyle.Ghost;
            _restartSafe.Text = I18n.Get("btnRestartSafe", "Restart in Safe Mode");
            _restartSafe.Click += (s, e) => Restart(RestartType.SafeMode);
            _restartCard.Body.Controls.Add(_restartSafe);

            _restartDefender.Style = NButtonStyle.Ghost;
            _restartDefender.Text = I18n.Get("btnRestartDisableDefender", "Restart && disable Defender");
            _restartDefender.Click += (s, e) => Restart(RestartType.DisableDefender);
            _restartCard.Body.Controls.Add(_restartDefender);
            Body.Controls.Add(_restartCard);

            // ── about ──
            _linkSource.Style = NButtonStyle.Ghost;
            _linkSource.Text = I18n.Get("linkLabel2", "Source code");
            _linkSource.Click += (s, e) => Open(UpdateHelper.Repository);
            _aboutCard.Controls.Add(_linkSource);

            _linkBug.Style = NButtonStyle.Ghost;
            _linkBug.Text = I18n.Get("linkLabel4", "Report a bug");
            _linkBug.Click += (s, e) => Open(UpdateHelper.Repository + "/issues/new?template=bug_report.md");
            _aboutCard.Controls.Add(_linkBug);

            _linkFeature.Style = NButtonStyle.Ghost;
            _linkFeature.Text = I18n.Get("linkLabel6", "Request a feature");
            _linkFeature.Click += (s, e) => Open(UpdateHelper.Repository + "/issues/new?template=feature_request.md");
            _aboutCard.Controls.Add(_linkFeature);

            _linkFaq.Style = NButtonStyle.Ghost;
            _linkFaq.Text = I18n.Get("linkLabel7", "FAQ && help");
            _linkFaq.Click += (s, e) => Open(UpdateHelper.Repository + "/blob/main/docs/FAQ.md");
            _aboutCard.Controls.Add(_linkFaq);

            _linkLicense.Style = NButtonStyle.Ghost;
            _linkLicense.Text = I18n.Get("linkLabel5", "GNU GPL 3.0");
            _linkLicense.Click += (s, e) => Open("https://www.gnu.org/licenses/gpl-3.0.en.html");
            _aboutCard.Controls.Add(_linkLicense);

            _aboutCard.Paint += PaintAbout;
            Body.Controls.Add(_aboutCard);
        }

        void AddBehaviour(string label, Func<Options, bool> get, Action<Options, bool> set, Action<bool> apply)
        {
            MoonToggle t = new MoonToggle();
            BehaviourToggle b = new BehaviourToggle { Label = label, Toggle = t, Get = get, Set = set, Apply = apply };
            t.CheckedChanged += (s, e) =>
            {
                if (_suppress) return;
                b.Set(OptionsHelper.CurrentOptions, t.Checked);
                if (b.Apply != null) b.Apply(t.Checked);
                OptionsHelper.SaveSettings();
            };
            _behaviour.Add(b);
            _behaviourCard.Body.Controls.Add(t);
        }

        internal override void Activate()
        {
            _suppress = true;
            try
            {
                Options o = OptionsHelper.CurrentOptions;
                foreach (BehaviourToggle b in _behaviour) b.Toggle.Checked = b.Get(o);

                for (int i = 0; i < Languages.Length; i++)
                    if (Languages[i].Value == o.LanguageCode) { _language.SelectedIndex = i; break; }
            }
            finally { _suppress = false; }
        }

        void ChangeLanguage()
        {
            if (_suppress || _language.SelectedIndex < 0) return;
            LanguageCode code = Languages[_language.SelectedIndex].Value;
            if (code == OptionsHelper.CurrentOptions.LanguageCode) return;

            OptionsHelper.CurrentOptions.LanguageCode = code;
            OptionsHelper.LoadTranslation();
            OptionsHelper.SaveSettings();

            Toast(I18n.Get("settingsLanguageChanged", "Language changed — restart to translate everything"));
        }

        void ShowChangelog()
        {
            string text = UpdateHelper.Changelog();
            if (string.IsNullOrEmpty(text)) text = I18n.Get("changelogUnavailable", "The changelog is not available right now.");
            using (InfoForm f = new InfoForm(text)) f.ShowDialog(FindForm());
        }

        void ShowErrors()
        {
            try
            {
                string text = File.Exists(Logger.ErrorLogFile)
                    ? File.ReadAllText(Logger.ErrorLogFile)
                    : I18n.Get("noErrorsM", "There are no errors to show!");
                using (InfoForm f = new InfoForm(text)) f.ShowDialog(FindForm());
            }
            catch (Exception ex)
            {
                Logger.LogError("SettingsScreen.ShowErrors", ex.Message, ex.StackTrace);
                Toast(I18n.Get("errorsUnavailable", "Could not read the error log"));
            }
        }

        void OpenFolder()
        {
            try { Process.Start(CoreHelper.CoreFolder); }
            catch (Exception ex)
            {
                Logger.LogError("SettingsScreen.OpenFolder", ex.Message, ex.StackTrace);
                Toast(I18n.Get("folderFailed", "Could not open the configuration folder"));
            }
        }

        void RestoreSystemFont()
        {
            if (!HelperForm.Confirm(FindForm(), I18n.Get("restoreFontConfirm",
                    "Restore the default Windows system font? You will need to sign out for it to take effect."))) return;
            FontHelper.RestoreDefaultGlobalFont();
            Toast(I18n.Get("fontRestored", "System font restored"));
        }

        void Repair()
        {
            if (!HelperForm.Confirm(FindForm(), I18n.Get("resetMessage",
                    "Reset ConfigurO's settings and re-deploy its support files?"))) return;
            // Repair() deletes the whole data folder, so the app has to come
            // back up afterwards; Repair(true) would leave it running against
            // a directory that no longer exists.
            Utilities.Repair();
        }

        void AddAccessFix(string label, Action fix)
        {
            _accessFixes.Add(new KeyValuePair<string, Action>(label, fix));
            MoonCheck check = new MoonCheck { Checked = true };
            _accessChecks.Add(check);
            _accessCard.Body.Controls.Add(check);
        }

        void ApplyAccessFixes()
        {
            int applied = 0;
            for (int i = 0; i < _accessFixes.Count; i++)
            {
                if (!_accessChecks[i].Checked) continue;
                try { _accessFixes[i].Value(); applied++; }
                catch (Exception ex)
                {
                    Logger.LogError("SettingsScreen.AccessFix:" + _accessFixes[i].Key,
                                    ex.Message, ex.StackTrace);
                }
            }
            Toast(applied == 0
                ? I18n.Get("accessNothing", "Nothing selected to fix")
                : string.Format(I18n.Get("accessFixed", "{0} restored"), applied));
        }

        void Restart(RestartType type)
        {
            string prompt;
            switch (type)
            {
                case RestartType.SafeMode:
                    prompt = I18n.Get("restartSafeConfirm", "Restart into safe mode now?"); break;
                case RestartType.DisableDefender:
                    prompt = I18n.Get("restartDefenderConfirm",
                        "Restart into safe mode and disable Windows Defender?"); break;
                default:
                    prompt = I18n.Get("restartConfirm", "Restart the computer now?"); break;
            }
            if (!HelperForm.Confirm(FindForm(), prompt)) return;

            OptionsHelper.SaveSettings();
            switch (type)
            {
                case RestartType.SafeMode: Program.RestartInSafeMode(); break;
                case RestartType.DisableDefender: Program.SetRunOnceDisableDefender(); break;
                default: Utilities.Reboot(); break;
            }
        }

        /// <summary>
        /// Substitutes the system UI font. Kept from the legacy tool -- it is a
        /// registry-level change that needs a sign-out, so it confirms first.
        /// </summary>
        void SetSystemFont()
        {
            using (FontDialog d = new FontDialog { FontMustExist = true, ShowEffects = false })
            {
                if (d.ShowDialog(FindForm()) != DialogResult.OK) return;
                if (!HelperForm.Confirm(FindForm(), string.Format(
                        I18n.Get("setFontConfirm",
                            "Set \"{0}\" as the system font? You will need to sign out for it to take effect."),
                        d.Font.Name))) return;
                FontHelper.ChangeGlobalFont(d.Font.Name);
                Toast(string.Format(I18n.Get("fontSet", "System font set to {0}"), d.Font.Name));
            }
        }

        static void Open(string url)
        {
            try { Process.Start(url); }
            catch (Exception ex) { Logger.LogError("SettingsScreen.Open", ex.Message, ex.StackTrace); }
        }

        void PaintAccessLabels(object sender, PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            NocturneDraw.Prepare(g);
            int rowH = NocturneScale.S(26);
            int colW = _accessCard.Body.Width / 2;
            using (Font f = NocturneFonts.Row())
            {
                for (int i = 0; i < _accessFixes.Count; i++)
                {
                    int col = i % 2, row = i / 2;
                    int x = col * colW + NocturneScale.S(NocturneTheme.CheckboxSize) + NocturneScale.S(8);
                    NocturneDraw.Text(g, _accessFixes[i].Key, f, NocturneTheme.Text,
                        new RectangleF(x, row * rowH, colW - (x - col * colW) - NocturneScale.S(8), rowH),
                        NocturneDraw.Left);
                }
            }
        }

        void PaintBehaviourLabels(object sender, PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            NocturneDraw.Prepare(g);
            int rowH = NocturneScale.S(30);
            using (Font f = NocturneFonts.Row())
            {
                for (int i = 0; i < _behaviour.Count; i++)
                {
                    NocturneDraw.Text(g, _behaviour[i].Label, f, NocturneTheme.Text,
                        new RectangleF(0, i * rowH,
                                       Math.Max(0, _behaviourCard.Body.Width - NocturneScale.S(56)), rowH),
                        NocturneDraw.Left);
                    if (i < _behaviour.Count - 1)
                        NocturneTheme.DrawFadedRule(g, 0, i * rowH + rowH - 1,
                                                    _behaviourCard.Body.Width, NocturneTheme.Border);
                }
            }
        }

        void PaintAbout(object sender, PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            NocturneDraw.Prepare(g);

            NocturneDraw.Card(g, new Rectangle(0, 0, _aboutCard.Width, _aboutCard.Height),
                              NocturneTheme.Surface, NocturneTheme.Border, NocturneTheme.RadiusMd);

            int pad = NocturneScale.S(16);
            int mark = NocturneScale.S(44);
            NocturneBrand.DrawFull(g, new Rectangle(pad, pad, mark, mark));

            int x = pad + mark + NocturneScale.S(14);
            int w = Math.Max(0, _aboutCard.Width - x - pad);

            using (Font f = NocturneFonts.Big())
                NocturneDraw.Text(g, "ConfigurO " + Program.GetCurrentVersionTostring(), f,
                    NocturneTheme.Text, new RectangleF(x, pad, w, NocturneScale.S(24)), NocturneDraw.Left);

            using (Font f = NocturneFonts.Tip())
            {
                NocturneDraw.Text(g, I18n.Get("aboutTagline",
                        "Windows configuration, privacy and cleanup, in one window."),
                    f, NocturneTheme.TextMuted,
                    new RectangleF(x, pad + NocturneScale.S(24), w, NocturneScale.S(18)), NocturneDraw.Left);

                NocturneDraw.Text(g, "WRSTT · GPL-3.0 · " + UpdateHelper.Repository,
                    f, NocturneTheme.TextDim,
                    new RectangleF(x, pad + NocturneScale.S(42), w, NocturneScale.S(18)), NocturneDraw.Left);
            }
        }

        protected override void Relayout()
        {
            int gap = NocturneScale.S(16);
            int w = Math.Max(0, Width - Pad * 2);
            int colW = (w - gap) / 2;
            int rightX = Pad + colW + gap;
            int fieldH = NocturneScale.S(NocturneTheme.InputHeight);
            int buttonH = NocturneScale.S(34);
            int rowH = NocturneScale.S(30);

            // ── left column ──
            int y = 0;
            int langH = NocturneScale.S(34) + fieldH + NocturneScale.S(26);
            _languageCard.SetBounds(Pad, y, colW, langH);
            _language.SetBounds(0, 0, _languageCard.Body.Width, fieldH);

            y += langH + gap;
            int behaviourH = NocturneScale.S(34) + _behaviour.Count * rowH + NocturneScale.S(26);
            _behaviourCard.SetBounds(Pad, y, colW, behaviourH);
            for (int i = 0; i < _behaviour.Count; i++)
            {
                MoonToggle t = _behaviour[i].Toggle;
                t.Location = new Point(_behaviourCard.Body.Width - t.Width,
                                       i * rowH + (rowH - t.Height) / 2);
            }

            y += behaviourH + gap;
            int accessRows = (_accessFixes.Count + 1) / 2;
            int accessRowH = NocturneScale.S(26);
            int accessH = NocturneScale.S(52) + accessRows * accessRowH
                        + NocturneScale.S(10) + buttonH + NocturneScale.S(26);
            _accessCard.SetBounds(Pad, y, colW, accessH);
            int accessColW = _accessCard.Body.Width / 2;
            for (int i = 0; i < _accessChecks.Count; i++)
            {
                MoonCheck c = _accessChecks[i];
                c.SetBounds((i % 2) * accessColW,
                            (i / 2) * accessRowH + (accessRowH - c.Height) / 2,
                            NocturneScale.S(NocturneTheme.CheckboxSize),
                            NocturneScale.S(NocturneTheme.CheckboxSize));
            }
            _accessFix.SetBounds(0, accessRows * accessRowH + NocturneScale.S(10),
                                 _accessCard.Body.Width, buttonH);
            int leftBottom = y + accessH;

            // ── right column ──
            y = 0;
            int updatesH = NocturneScale.S(52) + buttonH + NocturneScale.S(26);
            _updatesCard.SetBounds(rightX, y, colW, updatesH);
            int half = (_updatesCard.Body.Width - NocturneScale.S(8)) / 2;
            _checkUpdate.SetBounds(0, 0, half, buttonH);
            _viewChanges.SetBounds(half + NocturneScale.S(8), 0, half, buttonH);

            y += updatesH + gap;
            int troubleH = NocturneScale.S(34) + buttonH * 3
                         + NocturneScale.S(8) * 2 + NocturneScale.S(26);
            _troubleCard.SetBounds(rightX, y, colW, troubleH);
            int tw = (_troubleCard.Body.Width - NocturneScale.S(8)) / 2;
            int step = buttonH + NocturneScale.S(8);
            _viewErrors.SetBounds(0, 0, tw, buttonH);
            _openFolder.SetBounds(tw + NocturneScale.S(8), 0, tw, buttonH);
            _setFont.SetBounds(0, step, tw, buttonH);
            _systemFont.SetBounds(tw + NocturneScale.S(8), step, tw, buttonH);
            _repair.SetBounds(0, step * 2, _troubleCard.Body.Width, buttonH);

            y += troubleH + gap;
            int restartH = NocturneScale.S(52) + buttonH * 2 + NocturneScale.S(8) + NocturneScale.S(26);
            _restartCard.SetBounds(rightX, y, colW, restartH);
            int rw = (_restartCard.Body.Width - NocturneScale.S(8)) / 2;
            _restartNormal.SetBounds(0, 0, rw, buttonH);
            _restartSafe.SetBounds(rw + NocturneScale.S(8), 0, rw, buttonH);
            _restartDefender.SetBounds(0, step, _restartCard.Body.Width, buttonH);

            y += restartH + gap;
            int linkH = NocturneScale.S(28);
            int aboutH = NocturneScale.S(92) + linkH * 2 + NocturneScale.S(8);
            _aboutCard.SetBounds(rightX, y, colW, aboutH);
            LayoutLinks(linkH);

            Body.Height = Math.Max(leftBottom, y + aboutH) + NocturneScale.S(20);
        }

        /// <summary>Wraps the project links across the bottom of the About card.</summary>
        void LayoutLinks(int linkH)
        {
            NButton[] links = { _linkSource, _linkBug, _linkFeature, _linkFaq, _linkLicense };
            int pad = NocturneScale.S(12);
            int gap = NocturneScale.S(4);
            int top = NocturneScale.S(84);
            int x = pad, row = 0;

            using (Graphics g = NocturneDraw.CreateMeasureGraphics())
            using (Font f = NocturneFonts.Row())
            {
                foreach (NButton b in links)
                {
                    int bw = (int)Math.Ceiling(NocturneDraw.Width(g, b.Text, f)) + NocturneScale.S(18);
                    if (x + bw > _aboutCard.Width - pad) { x = pad; row++; }
                    b.SetBounds(x, top + row * linkH, bw, linkH);
                    x += bw + gap;
                }
            }
        }
    }
}
