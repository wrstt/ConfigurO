using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Windows.Forms;

namespace ConfigurO
{
    static class Program
    {
        /// <summary>
        /// Product version. Also the tag the updater compares against
        /// version.txt, so keep it in step with CHANGELOG.md.
        /// </summary>
        internal const int Major = 1;
        internal const int Minor = 3;
        internal const bool EXPERIMENTAL_BUILD = false;

        internal static string GetCurrentVersionTostring()
        {
            return Major + "." + Minor;
        }

        /// <summary>
        /// Parsed with the invariant culture on purpose: the UI ships in 28
        /// languages, and on a comma-decimal locale the ambient culture makes
        /// this throw and the update check die silently.
        /// </summary>
        internal static float GetCurrentVersionToFloat()
        {
            return float.Parse(GetCurrentVersionTostring(),
                               System.Globalization.CultureInfo.InvariantCulture);
        }

        internal static bool SILENT_MODE = false;

        // Enables the corresponding Windows tab for Windows Server machines,
        // as well as the Advanced tweaks tab
        internal static bool UNSAFE_MODE = false;

        const string _jsonAssembly = @"ConfigurO.Newtonsoft.Json.dll";

        internal static MainForm _MainForm;
        internal static SplashForm _SplashForm;

        static string _adminMissingMessage = "ConfigurO needs to be run as administrator!\nApp will now close...";
        static string _unsupportedMessage = "ConfigurO works with Windows 7 and higher!\nApp will now close...";

        static string _confInvalidVersionMsg = "Windows version does not match!";
        static string _confInvalidFormatMsg = "Config file is in invalid format!";
        static string _confNotFoundMsg = "Config file does not exist!";
        static string _argInvalidMsg = "Invalid argument! Example: ConfigurO.exe /config=win10.json";
        static string _alreadyRunningMsg = "ConfigurO is already running in the background!";

        const string MUTEX_GUID = @"{DEADMOON-0EFC7B8A-D1FC-467F-B4B1-0117C643FE19-CONFIGURO}";
        internal static Mutex MUTEX;
        static bool _notRunning;

        // DPI awareness normally comes from app.manifest. These are the
        // runtime fallbacks for the case where the manifest was stripped
        // (some packagers do) or the OS predates Per-Monitor-V2.
        static readonly IntPtr DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2 = new IntPtr(-4);

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern bool SetProcessDpiAwarenessContext(IntPtr value);

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern bool SetProcessDPIAware();

        private static void EnsureDpiAwareness()
        {
            try
            {
                // Windows 10 1703 and newer.
                if (SetProcessDpiAwarenessContext(DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2)) return;
            }
            catch (EntryPointNotFoundException) { }
            catch (DllNotFoundException) { }

            try { if (Environment.OSVersion.Version.Major >= 6) SetProcessDPIAware(); }
            catch (EntryPointNotFoundException) { }
        }

        [STAThread]
        static void Main(string[] switches)
        {
            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
            AppDomain.CurrentDomain.AssemblyResolve += CurrentDomain_AssemblyResolve;
            EmbeddedAssembly.Load(_jsonAssembly, _jsonAssembly.Replace("ConfigurO.", string.Empty));

            EnsureDpiAwareness();
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            // single-instance mechanism
            MUTEX = new Mutex(true, MUTEX_GUID, out _notRunning);

            if (!_notRunning)
            {
                MessageBox.Show(_alreadyRunningMsg, "ConfigurO", MessageBoxButtons.OK, MessageBoxIcon.Information);
                Environment.Exit(0);
                return;
            }

            if (!Utilities.IsAdmin())
            {
                string file = Process.GetCurrentProcess().MainModule.FileName;
                ProcessStartInfo p = new ProcessStartInfo(file);
                p.Verb = "runas";
                p.Arguments = string.Join(" ", switches);
                Process.Start(p);
                Environment.Exit(0);
                return;
            }

            // Deploy and load the fonts before anything can show a window --
            // the compatibility dialog below is a window.
            CoreHelper.Deploy();
            NocturneFonts.Load();

            if (!Utilities.IsCompatible())
            {
                using (HelperForm f = new HelperForm(null, MessageType.Error, _unsupportedMessage))
                    f.ShowDialog();
                Environment.Exit(0);
                return;
            }

            if (switches.Length == 1)
            {
                string arg = switches[0].Trim().ToLowerInvariant();

                // UNSAFE mode switch (allows running on Windows Server 2008+)
                if (arg == "/unsafe")
                {
                    UNSAFE_MODE = true;
                    StartMainForm();
                    return;
                }

                if (arg == "/repair")
                {
                    Utilities.Repair(true);
                    return;
                }

                if (arg == "/disablehpet")
                {
                    Utilities.DisableHPET();
                    Environment.Exit(0);
                    return;
                }
                if (arg == "/enablehpet")
                {
                    Utilities.EnableHPET();
                    Environment.Exit(0);
                    return;
                }

                // [!!!] unlock all cores instruction 
                if (arg == "/unlockcores")
                {
                    Utilities.UnlockAllCores();
                    Environment.Exit(0);
                    return;
                }

                if (arg.StartsWith("/svchostsplit="))
                {
                    string x = arg.Replace("/svchostsplit=", string.Empty);
                    bool isValid = !x.Any(c => !char.IsDigit(c));
                    if (isValid && int.TryParse(x, out int result)) Utilities.DisableSvcHostProcessSplitting(result);
                    Environment.Exit(0);
                    return;
                }

                if (arg == "/resetsvchostsplit")
                {
                    Utilities.EnableSvcHostProcessSplitting();
                    Environment.Exit(0);
                    return;
                }

                if (arg == "/version")
                {

                    Environment.Exit(0);
                    return;
                }
                // instruct to restart in safe-mode
                if (arg == "/restart=safemode")
                {
                    RestartInSafeMode();
                }

                // instruct to restart normally
                if (arg == "/restart=normal")
                {
                    RestartInNormalMode();
                }

                // disable defender automatically
                if (arg == "/restart=disabledefender")
                {
                    SetRunOnceDisableDefender();
                }

                // enable defender automatically
                if (arg == "/restart=enabledefender")
                {
                    SetRunOnceEnableDefender();
                }

                // return from safe-mode automatically
                if (arg == "/silentdisabledefender")
                {
                    DisableDefenderInSafeMode();
                    RestartInNormalMode();
                }

                if (arg == "/silentenabledefender")
                {
                    EnableDefenderInSafeMode();
                    RestartInNormalMode();
                }

                // disables Defender in SAFE MODE (for Windows 10 1903+ / works in Windows 11 as well)
                if (arg == "/disabledefender")
                {
                    DisableDefenderInSafeMode();

                    MessageBox.Show("Windows Defender has been completely disabled successfully.", "ConfigurO", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    Environment.Exit(0);
                    return;
                }


                // other options for disabling specific tools
                if (arg.StartsWith("/disable="))
                {
                    string x = arg.Replace("/disable=", string.Empty);
                    string[] opts = x.Split(',');

                    bool? o1, o2, o3, o4, o5, o6, o7, o8;
                    if (opts.Contains(Constants.INDICIUM_TOOL)) o1 = true; else o1 = null;
                    if (opts.Contains(Constants.UWP_TOOL)) o2 = true; else o2 = null;
                    if (opts.Contains(Constants.APPS_TOOL)) o3 = true; else o3 = null;
                    if (opts.Contains(Constants.HOSTS_EDITOR)) o4 = true; else o4 = null;
                    if (opts.Contains(Constants.STARTUP_TOOL)) o5 = true; else o5 = null;
                    if (opts.Contains(Constants.CLEANER_TOOL)) o6 = true; else o6 = null;
                    if (opts.Contains(Constants.INTEGRATOR_TOOL)) o7 = true; else o7 = null;
                    if (opts.Contains(Constants.PINGER_TOOL)) o8 = true; else o8 = null;

                    StartMainForm(new bool?[] { o1, o2, o3, o4, o5, o6, o7, o8 });
                    return;
                }

                if (arg.StartsWith("/config="))
                {
                    UNSAFE_MODE = true;
                    string fileName = arg.Replace("/config=", string.Empty);

                    if (!File.Exists(fileName))
                    {
                        MessageBox.Show(_confNotFoundMsg, "ConfigurO", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        Environment.Exit(0);
                        return;
                    }

                    SilentOps.GetSilentConfig(fileName);

                    if (SilentOps.CurrentSilentConfig == null)
                    {
                        MessageBox.Show(_confInvalidFormatMsg, "ConfigurO", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        Environment.Exit(0);
                        return;
                    }
                    if (!SilentOps.ProcessWindowsVersionCompatibility())
                    {
                        MessageBox.Show(_confInvalidVersionMsg, "ConfigurO", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        Environment.Exit(0);
                        return;
                    }
                    SILENT_MODE = true;
                    LoadSettings();
                    SilentOps.ProcessAllActions();
                    OptionsHelper.SaveSettings();
                }
            }
            else
            {
                StartMainForm();
            }
        }

        private static void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            Exception error = (Exception)e.ExceptionObject;
            Logger.LogError("Program.Main-UnhandledException", error.Message, error.StackTrace);
        }

        private static void LoadSettings()
        {
            // for backward compatibility
            OptionsHelper.LegacyCheck();

            // load settings, if there is no settings, load defaults
            try
            {
                // show FirstRunForm/Language Selector if app is running first time
                if (!File.Exists(OptionsHelper.SettingsFile))
                {
                    OptionsHelper.LoadSettings();
                    if (!SILENT_MODE)
                    {
                        FirstRunForm frf = new FirstRunForm();
                        frf.ShowDialog();
                    }
                }
                else
                {
                    OptionsHelper.LoadSettings();
                }

                //if (!Options.CurrentOptions.DisableConfigurOTelemetry)
                //{
                //    TelemetryHelper.EnableTelemetryService();
                //}

                // ideal place to replace internal messages from translation list
                _adminMissingMessage = OptionsHelper.TranslationList["adminMissingMsg"];
                _unsupportedMessage = OptionsHelper.TranslationList["unsupportedMsg"];
                _confInvalidFormatMsg = OptionsHelper.TranslationList["confInvalidFormatMsg"];
                _confInvalidVersionMsg = OptionsHelper.TranslationList["confInvalidVersionMsg"];
                _confNotFoundMsg = OptionsHelper.TranslationList["confNotFoundMsg"];
                _argInvalidMsg = OptionsHelper.TranslationList["argInvalidMsg"];
                _alreadyRunningMsg = OptionsHelper.TranslationList["alreadyRunningMsg"];
            }
            catch (Exception ex)
            {
                Logger.LogError("Program.Main-LoadSettings", ex.Message, ex.StackTrace);
                Environment.Exit(0);
            }
        }

        internal static void RestartInSafeMode()
        {
            Utilities.RunCommand("bcdedit /set {current} safeboot Minimal");
            Thread.Sleep(500);
            Utilities.Reboot();

            Environment.Exit(0);
        }

        internal static void RestartInNormalMode()
        {
            Utilities.RunCommand("bcdedit /deletevalue {current} safeboot");
            Thread.Sleep(500);
            Utilities.Reboot();

            Environment.Exit(0);
        }

        private static void DisableDefenderInSafeMode()
        {
            File.WriteAllText("DisableDefenderSafeMode.bat", Properties.Resources.DisableDefenderSafeMode1903Plus);

            Utilities.RunBatchFile("DisableDefenderSafeMode.bat");
            Thread.Sleep(1000);
            Utilities.RunBatchFile("DisableDefenderSafeMode.bat");
            Thread.Sleep(1000);

            File.Delete("DisableDefenderSafeMode.bat");
        }

        private static void EnableDefenderInSafeMode()
        {
            File.WriteAllText("EnableDefenderSafeMode.bat", Properties.Resources.EnableDefenderSafeMode1903Plus);

            Utilities.RunBatchFile("EnableDefenderSafeMode.bat");
            Thread.Sleep(1000);
            Utilities.RunBatchFile("EnableDefenderSafeMode.bat");
            Thread.Sleep(1000);

            File.Delete("EnableDefenderSafeMode.bat");
        }

        internal static void SetRunOnceDisableDefender()
        {
            // set RunOnce instruction
            Microsoft.Win32.Registry.SetValue(@"HKEY_LOCAL_MACHINE\Software\Microsoft\Windows\CurrentVersion\RunOnce", "*ConfigurODisableDefender", Assembly.GetExecutingAssembly().Location + " /silentdisabledefender", Microsoft.Win32.RegistryValueKind.String);
            RestartInSafeMode();
        }

        internal static void SetRunOnceEnableDefender()
        {
            // set RunOnce instruction
            Microsoft.Win32.Registry.SetValue(@"HKEY_LOCAL_MACHINE\Software\Microsoft\Windows\CurrentVersion\RunOnce", "*ConfigurOEnableDefender", Assembly.GetExecutingAssembly().Location + " /silentenabledefender", Microsoft.Win32.RegistryValueKind.String);
            RestartInSafeMode();
        }

        private static void StartMainForm()
        {
            LoadSettings();
            StartSplashForm();

            _MainForm = new MainForm(_SplashForm);
            _MainForm.Load += MainForm_Load;
            Application.Run(_MainForm);
        }

        private static void StartMainForm(bool?[] codes)
        {
            LoadSettings();
            StartSplashForm();

            _MainForm = new MainForm(_SplashForm, codes[0], codes[3], codes[2], codes[1], codes[4], codes[5], codes[6], codes[7]);
            _MainForm.Load += MainForm_Load;
            Application.Run(_MainForm);
        }

        private static void StartSplashForm()
        {
            _SplashForm = new SplashForm();
            var splashThread = new Thread(new ThreadStart(
                () => Application.Run(_SplashForm)));

            splashThread.SetApartmentState(ApartmentState.STA);
            splashThread.Start();
        }

        private static void MainForm_Load(object sender, EventArgs e)
        {
            if (_SplashForm != null && !_SplashForm.Disposing && !_SplashForm.IsDisposed)
                _SplashForm.Invoke(new Action(() => _SplashForm.Close()));

            _MainForm.TopMost = true;
            _MainForm.Activate();
            _MainForm.TopMost = false;
        }

        private static Assembly CurrentDomain_AssemblyResolve(object sender, ResolveEventArgs args)
        {
            return EmbeddedAssembly.Get(args.Name);
        }
    }
}
