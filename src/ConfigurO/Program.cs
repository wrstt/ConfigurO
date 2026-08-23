using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
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
        internal const int Major = 2;
        internal const int Minor = 8;
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

        /// <summary>Registry release number for .NET Framework 4.8.</summary>
        const int NET_48_RELEASE = 528040;

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

            // Anything thrown on the UI thread reaches Fatal rather than
            // WinForms' own dialog. Left to itself WinForms catches the
            // exception, shows a dialog and *keeps the message loop running* --
            // so a failure during startup left a process alive with no window,
            // still holding the single-instance mutex. Every later launch then
            // reported the app was already running, and nothing would open
            // again until the stale process was killed by hand.
            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
            Application.ThreadException += (s, e) => Fatal("Application.ThreadException", e.Exception);

            // The .NET Framework cannot be carried inside the executable --
            // it is a machine-wide Windows component, not a library that can be
            // embedded the way Newtonsoft.Json and the fonts are. All that can
            // be done is to notice it is missing and say so, rather than let the
            // app fail later with a MissingMethodException from somewhere
            // arbitrary, which reads as the app being broken.
            int release = Utilities.GetNETFrameworkRelease();
            if (release > 0 && release < NET_48_RELEASE)
            {
                MessageBox.Show(
                    "ConfigurO needs the Microsoft .NET Framework 4.8 or newer.\n\n" +
                    "This PC has an older version installed. It is a free download from Microsoft:\n" +
                    "https://dotnet.microsoft.com/download/dotnet-framework/net48",
                    "ConfigurO", MessageBoxButtons.OK, MessageBoxIcon.Information);
                Environment.Exit(0);
                return;
            }

            // single-instance mechanism
            MUTEX = new Mutex(true, MUTEX_GUID, out _notRunning);

            if (!_notRunning)
            {
                if (AlreadyRunning()) return;
                Environment.Exit(0);
                return;
            }

            if (!Utilities.IsAdmin())
            {
                string file = Process.GetCurrentProcess().MainModule.FileName;
                ProcessStartInfo p = new ProcessStartInfo(file);
                p.Verb = "runas";
                p.Arguments = string.Join(" ", switches);

                // Hand the mutex back before the elevated copy starts, not
                // after. This process acquired it a moment ago and is about to
                // exit; the elevated child races it for the same name, and if
                // it wins that race it sees the mutex held by its own parent
                // and reports the app is already running. Nothing then opens.
                ReleaseMutex();

                try { Process.Start(p); }
                catch (System.ComponentModel.Win32Exception)
                {
                    // The user dismissed the elevation prompt. That is a
                    // decision, not a fault: leave without a stack trace.
                }
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
            Fatal("Program.Main-UnhandledException", e.ExceptionObject as Exception);
        }

        /// <summary>
        /// Reports a startup or runtime failure and ends the process.
        ///
        /// The logging used to be the whole of it: the handler wrote a line and
        /// returned, which left the process running with no window and the
        /// single-instance mutex still held, so the app could not be started
        /// again. Failing has to be visible and it has to release the mutex.
        /// </summary>
        internal static void Fatal(string where, Exception ex)
        {
            try { Logger.LogError(where, ex == null ? "(none)" : ex.Message,
                                  ex == null ? string.Empty : ex.StackTrace); }
            catch { }

            try
            {
                MessageBox.Show(
                    "ConfigurO could not start.\n\n" + Describe(where, ex) +
                    "\n\nPress Ctrl+C to copy this message.\nFull details: " +
                    CoreHelper.CoreFolder + "ConfigurO.log",
                    "ConfigurO", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            catch { }

            ReleaseMutex();
            Environment.Exit(1);
        }

        /// <summary>
        /// A failure description someone can act on.
        ///
        /// Showing only Exception.Message meant the dialog said "Object
        /// reference not set to an instance of an object" and nothing else --
        /// true, and worth exactly nothing, since it does not say which
        /// reference or where. The type, the site and the first few frames make
        /// the difference between a report that can be diagnosed and one that
        /// can only be guessed at.
        /// </summary>
        static string Describe(string where, Exception ex)
        {
            if (ex == null) return "Unknown error in " + where + ".";

            StringBuilder sb = new StringBuilder();
            sb.Append(ex.GetType().Name).Append(": ").Append(ex.Message);
            sb.Append("\n\nWhere: ").Append(where);

            for (Exception inner = ex.InnerException; inner != null; inner = inner.InnerException)
                sb.Append("\nCaused by: ").Append(inner.GetType().Name)
                  .Append(": ").Append(inner.Message);

            Exception deepest = ex;
            while (deepest.InnerException != null) deepest = deepest.InnerException;

            if (!string.IsNullOrEmpty(deepest.StackTrace))
            {
                sb.Append("\n");
                string[] frames = deepest.StackTrace.Split(new[] { "\r\n", "\n" },
                                                           StringSplitOptions.RemoveEmptyEntries);
                for (int i = 0; i < frames.Length && i < 6; i++)
                    sb.Append('\n').Append(frames[i].Trim());
            }
            return sb.ToString();
        }

        /// <summary>Gives up the single-instance mutex, if this process holds it.</summary>
        internal static void ReleaseMutex()
        {
            try { if (MUTEX != null && _notRunning) MUTEX.ReleaseMutex(); }
            catch { }
            try { if (MUTEX != null) MUTEX.Close(); }
            catch { }
            _notRunning = false;
        }

        /// <summary>
        /// The other instance may not be a working one. A crash during startup
        /// can leave a process alive with no window, and before this the only
        /// way out was Task Manager: the dialog said the app was already
        /// running and there was nothing to do about it. Offering to restart
        /// makes that recoverable from the app itself.
        /// </summary>
        /// <returns>true when a fresh copy was started.</returns>
        private static bool AlreadyRunning()
        {
            DialogResult answer = MessageBox.Show(
                _alreadyRunningMsg + "\n\n" +
                "If ConfigurO is not responding, or no window appeared, choose Restart to close it and start again.",
                "ConfigurO", MessageBoxButtons.YesNo, MessageBoxIcon.Information,
                MessageBoxDefaultButton.Button2);

            if (answer != DialogResult.Yes) return false;

            Process me = Process.GetCurrentProcess();
            foreach (Process other in Process.GetProcessesByName(me.ProcessName))
            {
                if (other.Id == me.Id) continue;
                try { other.Kill(); other.WaitForExit(5000); }
                catch (Exception ex) { Logger.LogError("Program.AlreadyRunning-Kill", ex.Message, ex.StackTrace); }
                finally { other.Dispose(); }
            }

            // This process never owned the mutex -- it failed to acquire it --
            // so there is nothing to release here. The name frees as the stale
            // process dies, and the fresh copy takes it normally.
            try { Process.Start(new ProcessStartInfo(Application.ExecutablePath)); }
            catch (Exception ex) { Logger.LogError("Program.AlreadyRunning-Restart", ex.Message, ex.StackTrace); }

            Environment.Exit(0);
            return true;
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
                        // Contained. Choosing a language is not a prerequisite
                        // for running the app -- English is already loaded by
                        // this point -- so a fault in the chooser must not be
                        // the reason the app will not start. It is also the one
                        // screen no harness can exercise: a Form cannot be
                        // realised headlessly, so this dialog reaches a user
                        // without ever having been run anywhere else.
                        try
                        {
                            using (FirstRunForm frf = new FirstRunForm())
                                frf.ShowDialog();
                        }
                        catch (Exception ex)
                        {
                            Logger.LogError("Program.FirstRunForm", ex.Message, ex.StackTrace);
                            OptionsHelper.SaveSettings();
                        }
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
                // Through I18n.Get rather than indexing TranslationList.
                // TranslationList is dynamic, so indexing it while it is null
                // throws -- and these seven lines ran before the app had shown
                // anything, so a translation that failed to load took the whole
                // launch with it. I18n.Get returns the English already in these
                // fields instead, which is worth far more than the message.
                _adminMissingMessage = I18n.Get("adminMissingMsg", _adminMissingMessage);
                _unsupportedMessage = I18n.Get("unsupportedMsg", _unsupportedMessage);
                _confInvalidFormatMsg = I18n.Get("confInvalidFormatMsg", _confInvalidFormatMsg);
                _confInvalidVersionMsg = I18n.Get("confInvalidVersionMsg", _confInvalidVersionMsg);
                _confNotFoundMsg = I18n.Get("confNotFoundMsg", _confNotFoundMsg);
                _argInvalidMsg = I18n.Get("argInvalidMsg", _argInvalidMsg);
                _alreadyRunningMsg = I18n.Get("alreadyRunningMsg", _alreadyRunningMsg);
            }
            catch (Exception ex)
            {
                // Was: log it and Environment.Exit(0). Exiting zero, with
                // nothing shown, is indistinguishable from the app declining to
                // start for no reason -- double-click, nothing happens, no
                // window, no message, and the only trace a line in a log file
                // nobody knows to look in. Anything that stops the app getting
                // as far as its first window has to say so.
                Fatal("Program.Main-LoadSettings", ex);
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
            // Background, so it cannot hold the process open by itself. As a
            // foreground thread it kept a failed launch alive -- and with it
            // the mutex -- long after the main window had given up.
            splashThread.IsBackground = true;
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
