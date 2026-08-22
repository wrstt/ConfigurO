using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace ConfigurO
{
    internal static class OptionsHelper
    {
        internal readonly static string SettingsFile = CoreHelper.CoreFolder + "\\ConfigurO.json";

        internal static Options CurrentOptions = new Options();

        internal static dynamic TranslationList;

        /// <summary>
        /// Repaints a designer-built dialog in the current Nocturne mode.
        ///
        /// This used to build an ad-hoc palette from a user-chosen accent
        /// Colour (the Ocean/Magma/Zerg/... themes). The redesign has a single
        /// accent and two modes, so the whole per-theme colour plumbing is gone
        /// and everything routes through <see cref="NocturneTheme"/>.
        /// </summary>
        internal static void ApplyTheme(Form f)
        {
            NocturneLegacyTheme.Apply(f);
        }

        /// <summary>True when the settings file predates the Nocturne redesign.</summary>
        static bool LooksLegacy(string path)
        {
            try
            {
                string json = File.ReadAllText(path);
                return json.Contains("\"Color\":") || json.Contains("\"Theme\":");
            }
            catch (Exception ex)
            {
                Logger.LogError("OptionsHelper.LooksLegacy", ex.Message, ex.StackTrace);
                return false;
            }
        }

        internal static void LegacyCheck()
        {
            if (File.Exists(SettingsFile))
            {
                if (File.ReadAllText(SettingsFile).Contains("FirstRun"))
                {
                    File.Delete(SettingsFile);
                }
            }
        }

        internal static void SaveSettings()
        {
            try
            {
                string jsonMemory = JsonConvert.SerializeObject(CurrentOptions);

                if (File.Exists(SettingsFile))
                {
                    // Nothing to do if the file already says the same thing.
                    string jsonFile = File.ReadAllText(SettingsFile);
                    try
                    {
                        if (JToken.DeepEquals(JObject.Parse(jsonFile), JObject.Parse(jsonMemory))) return;
                    }
                    catch (JsonReaderException)
                    {
                        // The file on disk is corrupt; overwrite it.
                    }
                }
                else
                {
                    // This used to return silently when the file was missing,
                    // which meant a deleted or not-yet-created settings file
                    // left the app unable to persist anything at all.
                    Directory.CreateDirectory(Path.GetDirectoryName(SettingsFile));
                }

                // Written to one side and swapped in, never straight over the
                // top. File.WriteAllText truncates the target before it writes,
                // so a process that dies in that window -- killed because it had
                // hung, say -- leaves an empty settings file behind. That file
                // then deserialises to null on every subsequent launch, and the
                // app stops starting altogether until someone deletes it by
                // hand. The swap means the file on disk is always either the
                // old settings or the new ones.
                string tmp = SettingsFile + ".tmp";
                File.WriteAllText(tmp, JsonConvert.SerializeObject(CurrentOptions, Formatting.Indented));

                if (File.Exists(SettingsFile)) File.Replace(tmp, SettingsFile, null);
                else File.Move(tmp, SettingsFile);
            }
            catch (Exception ex)
            {
                Logger.LogError("OptionsHelper.SaveSettings", ex.Message, ex.StackTrace);
            }
        }

        internal static void LoadSettings()
        {
            if (!File.Exists(SettingsFile) || LooksLegacy(SettingsFile))
            {
                // settings migration for new color picker
                if (File.Exists(SettingsFile) && LooksLegacy(SettingsFile))
                {
                    // Settings written by a pre-Nocturne build carry a "Theme"
                    // colour and no mode. Keep everything else, and give the
                    // fields the redesign introduced their defaults rather
                    // than the false/null that deserialization leaves behind.
                    Options previous = ReadSettingsFile();
                    if (previous == null) { StartOver(); return; }
                    previous.ThemeMode = NocturneTheme.Mode.Dark;
                    previous.ShowHelpMessages = true;
                    previous.UseMica = false;
                    previous.LastScreen = string.Empty;
                    CurrentOptions = previous;
                }
                else
                {
                    // DEFAULT OPTIONS
                    CurrentOptions.ThemeMode = NocturneTheme.Mode.Dark;
                    CurrentOptions.AppsFolder = string.Empty;
                    CurrentOptions.EnableTray = false;
                    CurrentOptions.AutoStart = false;
                    CurrentOptions.InternalDNS = Constants.INTERNAL_DNS;
                    CurrentOptions.UpdateOnLaunch = true;
                    CurrentOptions.UseMica = false;
                    CurrentOptions.ShowHelpMessages = true;
                    CurrentOptions.LastScreen = string.Empty;

                    CurrentOptions.DisableIndicium = false;
                    CurrentOptions.DisableAppsTool = false;
                    CurrentOptions.DisableHostsEditor = false;
                    CurrentOptions.DisableUWPApps = false;
                    CurrentOptions.DisableStartupTool = false;
                    CurrentOptions.DisableCleaner = false;
                    CurrentOptions.DisableIntegrator = false;
                    CurrentOptions.DisablePinger = false;

                    //CurrentOptions.TelemetryClientID = Guid.NewGuid().ToString().ToUpperInvariant();
                    //CurrentOptions.DisableConfigurOTelemetry = false;

                    CurrentOptions.LanguageCode = LanguageCode.EN;

                    CurrentOptions.EnablePerformanceTweaks = false;
                    CurrentOptions.DisableNetworkThrottling = false;
                    CurrentOptions.DisableWindowsDefender = false;
                    CurrentOptions.DisableSystemRestore = false;
                    CurrentOptions.DisablePrintService = false;
                    CurrentOptions.DisableMediaPlayerSharing = false;
                    CurrentOptions.DisableErrorReporting = false;
                    CurrentOptions.DisableHomeGroup = false;
                    CurrentOptions.DisableSuperfetch = false;
                    CurrentOptions.DisableTelemetryTasks = false;
                    CurrentOptions.DisableOffice2016Telemetry = false;
                    CurrentOptions.DisableCompatibilityAssistant = false;
                    CurrentOptions.DisableFaxService = false;
                    CurrentOptions.DisableSmartScreen = false;
                    CurrentOptions.DisableStickyKeys = false;
                    CurrentOptions.EnableGamingMode = false;
                    CurrentOptions.EnableLegacyVolumeSlider = false;
                    CurrentOptions.DisableQuickAccessHistory = false;
                    CurrentOptions.DisableStartMenuAds = false;
                    CurrentOptions.UninstallOneDrive = false;
                    CurrentOptions.DisableMyPeople = false;
                    CurrentOptions.DisableAutomaticUpdates = false;
                    CurrentOptions.ExcludeDrivers = false;
                    CurrentOptions.DisableTelemetryServices = false;
                    CurrentOptions.DisablePrivacyOptions = false;
                    CurrentOptions.DisableCortana = false;
                    CurrentOptions.DisableSensorServices = false;
                    CurrentOptions.DisableWindowsInk = false;
                    CurrentOptions.DisableSpellingTyping = false;
                    CurrentOptions.DisableXboxLive = false;
                    CurrentOptions.DisableGameBar = false;
                    CurrentOptions.DisableInsiderService = false;
                    CurrentOptions.DisableStoreUpdates = false;
                    CurrentOptions.DisableCloudClipboard = false;
                    CurrentOptions.EnableLongPaths = false;
                    CurrentOptions.RemoveCastToDevice = false;
                    CurrentOptions.DisableHibernation = false;
                    CurrentOptions.DisableSMB1 = false;
                    CurrentOptions.DisableSMB2 = false;
                    CurrentOptions.DisableNTFSTimeStamp = false;
                    CurrentOptions.DisableSearch = false;
                    CurrentOptions.RestoreClassicPhotoViewer = false;

                    CurrentOptions.DisableVisualStudioTelemetry = false;
                    CurrentOptions.DisableFirefoxTemeletry = false;
                    CurrentOptions.DisableChromeTelemetry = false;
                    CurrentOptions.DisableNVIDIATelemetry = false;

                    CurrentOptions.DisableEdgeDiscoverBar = false;
                    CurrentOptions.DisableEdgeTelemetry = false;

                    CurrentOptions.DisableOneDrive = false;

                    CurrentOptions.TaskbarToLeft = false;
                    CurrentOptions.DisableSnapAssist = false;
                    CurrentOptions.DisableWidgets = false;
                    CurrentOptions.DisableChat = false;
                    CurrentOptions.ClassicMenu = false;
                    CurrentOptions.DisableTPMCheck = false;
                    CurrentOptions.CompactMode = false;
                    CurrentOptions.DisableStickers = false;
                    CurrentOptions.DisableVBS = false;
                    CurrentOptions.DisableCoPilotAI = false;

                    CurrentOptions.DisableHPET = false;
                    CurrentOptions.EnableLoginVerbose = false;

                    CurrentOptions.RemoveMenusDelay = false;
                    CurrentOptions.ShowAllTrayIcons = false;
                    CurrentOptions.DisableModernStandby = false;
                    CurrentOptions.EnableUtcTime = false;
                    CurrentOptions.DisableNewsInterests = false;
                    CurrentOptions.HideTaskbarSearch = false;
                    CurrentOptions.HideTaskbarWeather = false;

                    using (FileStream fs = File.Open(SettingsFile, FileMode.CreateNew))
                    using (StreamWriter sw = new StreamWriter(fs))
                    using (JsonWriter jw = new JsonTextWriter(sw))
                    {
                        jw.Formatting = Formatting.Indented;

                        JsonSerializer serializer = new JsonSerializer();
                        serializer.Serialize(jw, CurrentOptions);
                    }
                }
            }
            else
            {
                Options loaded = ReadSettingsFile();
                if (loaded == null) { StartOver(); return; }
                CurrentOptions = loaded;
            }

            NocturneTheme.Current = CurrentOptions.ThemeMode;
            // generate random telemetry ID if not present
            //if (string.IsNullOrEmpty(CurrentOptions.TelemetryClientID))
            //{
            //    CurrentOptions.TelemetryClientID = Guid.NewGuid().ToString().ToUpperInvariant();
            //    SaveSettings();
            //}

            LoadTranslation();
        }

        static bool _startingOver;

        /// <summary>
        /// Reads the settings file, or null if it cannot be used.
        ///
        /// Deserialising an empty or truncated file returns null rather than
        /// throwing, which is the trap: the caller went on to read a property
        /// off it and the app died with a NullReferenceException before it had
        /// drawn anything. Every caller has to treat null as "no settings".
        /// </summary>
        static Options ReadSettingsFile()
        {
            try
            {
                string json = File.ReadAllText(SettingsFile);
                if (string.IsNullOrWhiteSpace(json)) return null;
                return JsonConvert.DeserializeObject<Options>(json);
            }
            catch (Exception ex)
            {
                Logger.LogError("OptionsHelper.ReadSettingsFile", ex.Message, ex.StackTrace);
                return null;
            }
        }

        /// <summary>
        /// Drops an unusable settings file and loads defaults over the top.
        ///
        /// Losing preferences is a poor outcome; refusing to start is a worse
        /// one, and before this that is what happened -- permanently, since
        /// nothing ever rewrote the bad file.
        /// </summary>
        static void StartOver()
        {
            if (_startingOver) { CurrentOptions = new Options(); LoadTranslation(); return; }
            _startingOver = true;
            try
            {
                Logger.LogInfoSilent("OptionsHelper: settings file unusable, restoring defaults");
                if (File.Exists(SettingsFile)) File.Delete(SettingsFile);
            }
            catch (Exception ex) { Logger.LogError("OptionsHelper.StartOver", ex.Message, ex.StackTrace); }

            try { LoadSettings(); }
            finally { _startingOver = false; }
        }

        internal static void LoadTranslation()
        {
            // load proper translation list
            try
            {
                if (CurrentOptions.LanguageCode == LanguageCode.EN) TranslationList = JObject.Parse(Properties.Resources.EN);
                if (CurrentOptions.LanguageCode == LanguageCode.RU) TranslationList = JObject.Parse(Properties.Resources.RU);
                if (CurrentOptions.LanguageCode == LanguageCode.EL) TranslationList = JObject.Parse(Properties.Resources.EL);
                if (CurrentOptions.LanguageCode == LanguageCode.TR) TranslationList = JObject.Parse(Properties.Resources.TR);
                if (CurrentOptions.LanguageCode == LanguageCode.DE) TranslationList = JObject.Parse(Properties.Resources.DE);
                if (CurrentOptions.LanguageCode == LanguageCode.ES) TranslationList = JObject.Parse(Properties.Resources.ES);
                if (CurrentOptions.LanguageCode == LanguageCode.PT) TranslationList = JObject.Parse(Properties.Resources.PT);
                if (CurrentOptions.LanguageCode == LanguageCode.FR) TranslationList = JObject.Parse(Properties.Resources.FR);
                if (CurrentOptions.LanguageCode == LanguageCode.IT) TranslationList = JObject.Parse(Properties.Resources.IT);
                if (CurrentOptions.LanguageCode == LanguageCode.CN) TranslationList = JObject.Parse(Properties.Resources.CN);
                if (CurrentOptions.LanguageCode == LanguageCode.CZ) TranslationList = JObject.Parse(Properties.Resources.CZ);
                if (CurrentOptions.LanguageCode == LanguageCode.TW) TranslationList = JObject.Parse(Properties.Resources.TW);
                if (CurrentOptions.LanguageCode == LanguageCode.KO) TranslationList = JObject.Parse(Properties.Resources.KO);
                if (CurrentOptions.LanguageCode == LanguageCode.PL) TranslationList = JObject.Parse(Properties.Resources.PL);
                if (CurrentOptions.LanguageCode == LanguageCode.AR) TranslationList = JObject.Parse(Properties.Resources.AR);
                if (CurrentOptions.LanguageCode == LanguageCode.KU) TranslationList = JObject.Parse(Properties.Resources.KU);
                if (CurrentOptions.LanguageCode == LanguageCode.HU) TranslationList = JObject.Parse(Properties.Resources.HU);
                if (CurrentOptions.LanguageCode == LanguageCode.RO) TranslationList = JObject.Parse(Properties.Resources.RO);
                if (CurrentOptions.LanguageCode == LanguageCode.NL) TranslationList = JObject.Parse(Properties.Resources.NL);
                if (CurrentOptions.LanguageCode == LanguageCode.UA) TranslationList = JObject.Parse(Properties.Resources.UA);
                if (CurrentOptions.LanguageCode == LanguageCode.JA) TranslationList = JObject.Parse(Properties.Resources.JA);
                if (CurrentOptions.LanguageCode == LanguageCode.FA) TranslationList = JObject.Parse(Properties.Resources.FA);
                if (CurrentOptions.LanguageCode == LanguageCode.NE) TranslationList = JObject.Parse(Properties.Resources.NE);
                if (CurrentOptions.LanguageCode == LanguageCode.BG) TranslationList = JObject.Parse(Properties.Resources.BG);
                if (CurrentOptions.LanguageCode == LanguageCode.VN) TranslationList = JObject.Parse(Properties.Resources.VN);
                if (CurrentOptions.LanguageCode == LanguageCode.UR) TranslationList = JObject.Parse(Properties.Resources.UR);
                if (CurrentOptions.LanguageCode == LanguageCode.ID) TranslationList = JObject.Parse(Properties.Resources.ID);
                if (CurrentOptions.LanguageCode == LanguageCode.HR) TranslationList = JObject.Parse(Properties.Resources.HR);
            }
            catch (Exception ex)
            {
                Logger.LogError("Options.LoadTranslation", ex.Message, ex.StackTrace);
                TranslationList = JObject.Parse(Properties.Resources.EN);
            }
        }
    }
}
