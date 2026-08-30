using System;
using System.Collections.Generic;
using System.Linq;

namespace ConfigurO
{
    /// <summary>
    /// The complete tweak catalogue -- the single source of truth shared by the
    /// Tweaks screen, silent configs and policy reinforcement.
    ///
    /// Tweaks are grouped into six categories; ids are stable and are what the
    /// silent-configuration templates in templates/ refer to, so renaming one
    /// breaks saved configurations.
    /// </summary>
    internal static class TweakRegistry
    {
        static List<TweakDef> _all;

        internal static IReadOnlyList<TweakDef> All
        {
            get { return _all ?? (_all = Build()); }
        }

        /// <summary>Tweaks that apply to the running version of Windows, grouped for display.</summary>
        internal static IEnumerable<IGrouping<TweakGroup, TweakDef>> Available()
        {
            return All.Where(t => t.IsAvailable).GroupBy(t => t.Group).OrderBy(g => (int)g.Key);
        }

        internal static TweakDef ById(string id)
        {
            return All.FirstOrDefault(t => t.Id == id);
        }

        internal static string GroupTitle(TweakGroup g)
        {
            switch (g)
            {
                case TweakGroup.Performance:     return I18n.Get("subPerformance", "Performance");
                case TweakGroup.Privacy:         return I18n.Get("subPrivacy", "Privacy");
                case TweakGroup.UpdatesDefender: return I18n.Get("subUpdates", "Updates & Defender");
                case TweakGroup.Gaming:          return I18n.Get("subGaming", "Gaming");
                case TweakGroup.Interface:       return I18n.Get("subInterface", "Interface");
                default:                         return I18n.Get("subSystem", "System");
            }
        }

        /// <summary>How many available tweaks are currently switched on.</summary>
        internal static int AppliedCount(Options o)
        {
            return All.Count(t => t.IsAvailable && t.Get(o));
        }

        // ── Builder shorthand ───────────────────────────────────────────
        static TweakDef T(string id, TweakGroup group, string labelKey, string tipKey,
                          string label, string tip,
                          Func<Options, bool> get, Action<Options, bool> set,
                          Action apply, Action revert,
                          bool restart = false, string confirmKey = null, bool async = false,
                          int minBuild = 0, bool win11 = false, Func<Tweaks, bool?> silent = null)
        {
            return new TweakDef
            {
                Id = id, Group = group, LabelKey = labelKey, TipKey = tipKey,
                Label = label, Tip = tip, Get = get, Set = set, Apply = apply, Revert = revert,
                RestartRequired = restart, ConfirmKey = confirmKey, RunAsync = async,
                MinBuild = minBuild, RequiresWindows11 = win11, SilentGet = silent
            };
        }

        static List<TweakDef> Build()
        {
            List<TweakDef> t = new List<TweakDef>();

            // ══ Performance ════════════════════════════════════════════
            t.Add(T("perf", TweakGroup.Performance, "performanceSw", "performanceTip",
                "Enable performance tweaks", "Applies a bundle of registry optimizations for responsiveness.",
                o => o.EnablePerformanceTweaks, (o, v) => o.EnablePerformanceTweaks = v,
                OptimizeHelper.EnablePerformanceTweaks, OptimizeHelper.DisablePerformanceTweaks, restart: true));

            t.Add(T("throttle", TweakGroup.Performance, "networkSw", "networkTip",
                "Disable network throttling", "Removes the multimedia network throttling index.",
                o => o.DisableNetworkThrottling, (o, v) => o.DisableNetworkThrottling = v,
                OptimizeHelper.DisableNetworkThrottling, OptimizeHelper.EnableNetworkThrottling));

            t.Add(T("superfetch", TweakGroup.Performance, "superfetchSw", "superfetchTip",
                "Disable Superfetch (SysMain)", "Recommended on SSD systems.",
                o => o.DisableSuperfetch, (o, v) => o.DisableSuperfetch = v,
                OptimizeHelper.DisableSuperfetch, OptimizeHelper.EnableSuperfetch));

            t.Add(T("hibernate", TweakGroup.Performance, "hibernateSw", "hibernateTip",
                "Disable hibernation", "Deletes hiberfil.sys and frees disk space.",
                o => o.DisableHibernation, (o, v) => o.DisableHibernation = v,
                Utilities.DisableHibernation, Utilities.EnableHibernation, restart: true));

            t.Add(T("ntfs", TweakGroup.Performance, "ntfsStampSw", "ntfsStampTip",
                "Disable NTFS timestamps", "Stops last-access time updates on files.",
                o => o.DisableNTFSTimeStamp, (o, v) => o.DisableNTFSTimeStamp = v,
                OptimizeHelper.DisableNTFSTimeStamp, OptimizeHelper.EnableNTFSTimeStamp, restart: true));

            t.Add(T("cores", TweakGroup.Performance, null, null,
                "Unlock all CPU cores", "Removes the boot core limit if one is set.",
                o => o.UnlockAllCores, (o, v) => o.UnlockAllCores = v,
                // Silent configs reach this through AdvancedTweaks.UnlockAllCores,
                // which SilentOps already handles -- no binding here.
                Win11Tweaks.UnlockAllCores, Win11Tweaks.RestoreCoreLimit, restart: true));

            t.Add(T("hpet", TweakGroup.Performance, "hpetSw", "hpetSw",
                "Disable HPET", "Turns off the high precision event timer.",
                o => o.DisableHPET, (o, v) => o.DisableHPET = v,
                Utilities.DisableHPET, Utilities.EnableHPET, restart: true));

            t.Add(T("standby", TweakGroup.Performance, "modernStandbySw", "modernStandbySw",
                "Disable modern standby", "Uses classic sleep instead of S0 low-power idle.",
                o => o.DisableModernStandby, (o, v) => o.DisableModernStandby = v,
                OptimizeHelper.DisableModernStandby, OptimizeHelper.EnableModernStandby, restart: true));

            t.Add(T("winsearch", TweakGroup.Performance, "winSearchSw", "winSearchTip",
                "Disable Windows Search indexing", "Stops the background indexer and its disk activity.",
                o => o.DisableSearch, (o, v) => o.DisableSearch = v,
                OptimizeHelper.DisableSearch, OptimizeHelper.EnableSearch));

            t.Add(T("vbs", TweakGroup.Performance, "vbsSw", "vbsTip",
                "Disable virtualization-based security", "Recovers the VBS performance cost. Lowers isolation.",
                o => o.DisableVBS, (o, v) => o.DisableVBS = v,
                OptimizeHelper.DisableVirtualizationBasedSecurity, OptimizeHelper.EnableVirtualizationBasedSecurity,
                restart: true));

            t.Add(T("hvci", TweakGroup.Performance, null, null,
                "Disable memory integrity (HVCI)", "Removes the hypervisor code-integrity overhead. Lowers protection.",
                o => o.DisableMemoryIntegrity, (o, v) => o.DisableMemoryIntegrity = v,
                Win11Tweaks.DisableMemoryIntegrity, Win11Tweaks.EnableMemoryIntegrity,
                restart: true, silent: s => s.DisableMemoryIntegrity));

            // ══ Privacy ════════════════════════════════════════════════
            t.Add(T("telsvc", TweakGroup.Privacy, "telemetryServicesSw", "telemetryServicesTip",
                "Disable telemetry services", "Stops DiagTrack and connected user experiences.",
                o => o.DisableTelemetryServices, (o, v) => o.DisableTelemetryServices = v,
                OptimizeHelper.DisableTelemetryServices, OptimizeHelper.EnableTelemetryServices, restart: true));

            t.Add(T("teltasks", TweakGroup.Privacy, "telemetryTasksSw", "telemetryTasksTip",
                "Disable telemetry tasks", "Removes scheduled telemetry collection tasks.",
                o => o.DisableTelemetryTasks, (o, v) => o.DisableTelemetryTasks = v,
                OptimizeHelper.DisableTelemetryTasks, OptimizeHelper.EnableTelemetryTasks));

            t.Add(T("cortana", TweakGroup.Privacy, "cortanaSw", "cortanaTip",
                "Disable Cortana", "Turns the assistant off system-wide.",
                o => o.DisableCortana, (o, v) => o.DisableCortana = v,
                OptimizeHelper.DisableCortana, OptimizeHelper.EnableCortana));

            t.Add(T("copilot", TweakGroup.Privacy, "copilotSw", "copilotTip",
                "Disable Copilot AI", "Completely turns off the Copilot feature in Windows and Edge.",
                o => o.DisableCoPilotAI, (o, v) => o.DisableCoPilotAI = v,
                OptimizeHelper.DisableCoPilotAI, OptimizeHelper.EnableCoPilotAI));

            t.Add(T("recall", TweakGroup.Privacy, null, null,
                "Disable Windows Recall", "Stops Windows saving periodic snapshots of your screen.",
                o => o.DisableRecall, (o, v) => o.DisableRecall = v,
                Win11Tweaks.DisableRecall, Win11Tweaks.EnableRecall,
                minBuild: WindowsRelease.Build11_24H2, win11: true, silent: s => s.DisableRecall));

            t.Add(T("clicktodo", TweakGroup.Privacy, null, null,
                "Disable Click to Do", "Turns off the on-screen AI actions overlay.",
                o => o.DisableClickToDo, (o, v) => o.DisableClickToDo = v,
                Win11Tweaks.DisableClickToDo, Win11Tweaks.EnableClickToDo,
                minBuild: WindowsRelease.Build11_24H2, win11: true, silent: s => s.DisableClickToDo));

            t.Add(T("privacy", TweakGroup.Privacy, "privacySw", "privacyTip",
                "Disable privacy options", "Opts out of advertising ID, feedback and tailored experiences.",
                o => o.DisablePrivacyOptions, (o, v) => o.DisablePrivacyOptions = v,
                OptimizeHelper.EnhancePrivacy, OptimizeHelper.CompromisePrivacy, restart: true, async: true));

            t.Add(T("websearch", TweakGroup.Privacy, null, null,
                "Disable Bing & web results in Search", "Search returns local results only.",
                o => o.DisableWebSearch, (o, v) => o.DisableWebSearch = v,
                Win11Tweaks.DisableWebSearch, Win11Tweaks.EnableWebSearch, silent: s => s.DisableWebSearch));

            t.Add(T("office", TweakGroup.Privacy, "officeTelemetrySw", "officeTelemetryTip",
                "Disable Office telemetry", "Works with Office 2016 or newer.",
                o => o.DisableOffice2016Telemetry, (o, v) => o.DisableOffice2016Telemetry = v,
                OptimizeHelper.DisableOffice2016Telemetry, OptimizeHelper.EnableOffice2016Telemetry));

            t.Add(T("smartscreen", TweakGroup.Privacy, "smartScreenSw", "smartScreenTip",
                "Disable SmartScreen", "Stops sending app and file data to Microsoft.",
                o => o.DisableSmartScreen, (o, v) => o.DisableSmartScreen = v,
                OptimizeHelper.DisableSmartScreen, OptimizeHelper.EnableSmartScreen));

            t.Add(T("clipboard", TweakGroup.Privacy, "ccSw", "ccTip",
                "Disable cloud clipboard", "Keeps clipboard history off the cloud.",
                o => o.DisableCloudClipboard, (o, v) => o.DisableCloudClipboard = v,
                OptimizeHelper.DisableCloudClipboard, OptimizeHelper.EnableCloudClipboard));

            t.Add(T("ink", TweakGroup.Privacy, "inkSw", "inkTip",
                "Disable Windows Ink", "Turns off pen data collection.",
                o => o.DisableWindowsInk, (o, v) => o.DisableWindowsInk = v,
                OptimizeHelper.DisableWindowsInk, OptimizeHelper.EnableWindowsInk));

            t.Add(T("spelling", TweakGroup.Privacy, "spellSw", "spellTip",
                "Disable spelling & typing data", "Stops sending typing samples.",
                o => o.DisableSpellingTyping, (o, v) => o.DisableSpellingTyping = v,
                OptimizeHelper.DisableSpellingAndTypingFeatures, OptimizeHelper.EnableSpellingAndTypingFeatures));

            t.Add(T("suggested", TweakGroup.Privacy, null, null,
                "Disable suggested actions", "No pop-up suggestions when you copy dates or numbers.",
                o => o.DisableSuggestedActions, (o, v) => o.DisableSuggestedActions = v,
                Win11Tweaks.DisableSuggestedActions, Win11Tweaks.EnableSuggestedActions,
                minBuild: WindowsRelease.Build11_22H2, win11: true, silent: s => s.DisableSuggestedActions));

            t.Add(T("scoobe", TweakGroup.Privacy, null, null,
                "Disable setup reminders", "No more \"let's finish setting up your device\" screens.",
                o => o.DisableSetupPrompts, (o, v) => o.DisableSetupPrompts = v,
                Win11Tweaks.DisableSetupPrompts, Win11Tweaks.EnableSetupPrompts, silent: s => s.DisableSetupPrompts));

            t.Add(T("sensors", TweakGroup.Privacy, "sensorSw", "sensorTip",
                "Disable sensor services", "Stops location and orientation sensor collection.",
                o => o.DisableSensorServices, (o, v) => o.DisableSensorServices = v,
                OptimizeHelper.DisableSensorServices, OptimizeHelper.EnableSensorServices, restart: true));

            t.Add(T("edgeai", TweakGroup.Privacy, "edgeAiSw", "edgeAiTip",
                "Disable Edge Discover bar", "Removes the sidebar and its content feed.",
                o => o.DisableEdgeDiscoverBar, (o, v) => o.DisableEdgeDiscoverBar = v,
                OptimizeHelper.DisableEdgeDiscoverBar, OptimizeHelper.EnableEdgeDiscoverBar));

            t.Add(T("edgetel", TweakGroup.Privacy, "edgeTelemetrySw", "edgeTelemetryTip",
                "Disable Edge telemetry", "Stops browsing diagnostics leaving the machine.",
                o => o.DisableEdgeTelemetry, (o, v) => o.DisableEdgeTelemetry = v,
                OptimizeHelper.DisableEdgeTelemetry, OptimizeHelper.EnableEdgeTelemetry));

            t.Add(T("chrometel", TweakGroup.Privacy, "chromeTelemetrySw", "chromeTelemetryTip",
                "Disable Chrome telemetry", "Turns off metrics reporting in Chrome.",
                o => o.DisableChromeTelemetry, (o, v) => o.DisableChromeTelemetry = v,
                OptimizeHelper.DisableChromeTelemetry, OptimizeHelper.EnableChromeTelemetry));

            t.Add(T("mv2", TweakGroup.Privacy, null, null,
                "Allow Manifest V2 extensions", "Keeps older content blockers loadable in Edge and Chrome.",
                o => o.AllowManifestV2Extensions, (o, v) => o.AllowManifestV2Extensions = v,
                OptimizeHelper.AllowManifestV2Extensions, OptimizeHelper.RestoreManifestV2Default,
                silent: s => s.AllowManifestV2Extensions));

            t.Add(T("fftel", TweakGroup.Privacy, "ffTelemetrySw", "ffTelemetryTip",
                "Disable Firefox telemetry", "Turns off data reporting in Firefox.",
                o => o.DisableFirefoxTemeletry, (o, v) => o.DisableFirefoxTemeletry = v,
                OptimizeHelper.DisableFirefoxTelemetry, OptimizeHelper.EnableFirefoxTelemetry));

            t.Add(T("vstel", TweakGroup.Privacy, "vsSw", "vsTip",
                "Disable Visual Studio telemetry", "Opts out of VS customer experience data.",
                o => o.DisableVisualStudioTelemetry, (o, v) => o.DisableVisualStudioTelemetry = v,
                OptimizeHelper.DisableVisualStudioTelemetry, OptimizeHelper.EnableVisualStudioTelemetry));

            t.Add(T("nvidiatel", TweakGroup.Privacy, "nvidiaTelemetrySw", "nvidiaTelemetrySw",
                "Disable NVIDIA telemetry", "Stops the driver's telemetry tasks and services.",
                o => o.DisableNVIDIATelemetry, (o, v) => o.DisableNVIDIATelemetry = v,
                OptimizeHelper.DisableNvidiaTelemetry, OptimizeHelper.EnableNvidiaTelemetry, restart: true));

            // ══ Updates & Defender ═════════════════════════════════════
            t.Add(T("updates", TweakGroup.UpdatesDefender, "autoUpdatesSw", "autoUpdatesTip",
                "Disable automatic updates", "Updates only download with your consent.",
                o => o.DisableAutomaticUpdates, (o, v) => o.DisableAutomaticUpdates = v,
                OptimizeHelper.DisableAutomaticUpdates, OptimizeHelper.EnableAutomaticUpdates));

            t.Add(T("drivers", TweakGroup.UpdatesDefender, "driversSw", "driversTip",
                "Exclude drivers from updates", "Windows Update will skip driver packages.",
                o => o.ExcludeDrivers, (o, v) => o.ExcludeDrivers = v,
                OptimizeHelper.ExcludeDrivers, OptimizeHelper.IncludeDrivers));

            t.Add(T("defender", TweakGroup.UpdatesDefender, "defenderSw", "defenderTip",
                "Disable Windows Defender", "Requires a restart in safe mode to fully apply.",
                o => o.DisableWindowsDefender, (o, v) => o.DisableWindowsDefender = v,
                OptimizeHelper.DisableDefender, OptimizeHelper.EnableDefender,
                restart: true, confirmKey: "defenderM"));

            t.Add(T("insider", TweakGroup.UpdatesDefender, "insiderSw", "insiderTip",
                "Disable Insider service", "Blocks preview build enrollment.",
                o => o.DisableInsiderService, (o, v) => o.DisableInsiderService = v,
                OptimizeHelper.DisableInsiderService, OptimizeHelper.EnableInsiderService));

            t.Add(T("store", TweakGroup.UpdatesDefender, "storeUpdatesSw", "storeUpdatesTip",
                "Disable Store auto-updates", "Apps update manually from the Store.",
                o => o.DisableStoreUpdates, (o, v) => o.DisableStoreUpdates = v,
                OptimizeHelper.DisableStoreUpdates, OptimizeHelper.EnableStoreUpdates));

            // ══ Gaming ═════════════════════════════════════════════════
            t.Add(T("xbox", TweakGroup.Gaming, "xboxSw", "xboxTip",
                "Disable Xbox Live services", "Stops Xbox background services and tasks.",
                o => o.DisableXboxLive, (o, v) => o.DisableXboxLive = v,
                OptimizeHelper.DisableXboxLive, OptimizeHelper.EnableXboxLive, restart: true));

            t.Add(T("gamebar", TweakGroup.Gaming, "gameBarSw", "gameBarTip",
                "Disable Game Bar", "Removes the overlay and its background capture.",
                o => o.DisableGameBar, (o, v) => o.DisableGameBar = v,
                OptimizeHelper.DisableGameBar, OptimizeHelper.EnableGameBar, restart: true));

            t.Add(T("gamemode", TweakGroup.Gaming, "gameModeSw", "gameModeTip",
                "Enable gaming mode", "Prioritizes foreground games for CPU and GPU.",
                o => o.EnableGamingMode, (o, v) => o.EnableGamingMode = v,
                OptimizeHelper.EnableGamingMode, OptimizeHelper.DisableGamingMode, restart: true));

            // ══ Interface ══════════════════════════════════════════════
            t.Add(T("ads", TweakGroup.Interface, "adsSw", "adsTip",
                "Disable Start menu ads", "Removes suggestions and promoted apps.",
                o => o.DisableStartMenuAds, (o, v) => o.DisableStartMenuAds = v,
                OptimizeHelper.DisableStartMenuAds, OptimizeHelper.EnableStartMenuAds));

            t.Add(T("recommended", TweakGroup.Interface, null, null,
                "Hide Start \"Recommended\"", "Removes the recent files and apps section.",
                o => o.HideStartRecommended, (o, v) => o.HideStartRecommended = v,
                Win11Tweaks.HideStartRecommended, Win11Tweaks.ShowStartRecommended,
                minBuild: WindowsRelease.Build11_22H2, win11: true, silent: s => s.HideStartRecommended));

            t.Add(T("news", TweakGroup.Interface, "newsInterestsSw", "newsInterestsSw",
                "Disable News & Interests", "Clears the taskbar widget feed.",
                o => o.DisableNewsInterests, (o, v) => o.DisableNewsInterests = v,
                OptimizeHelper.DisableNewsInterests, OptimizeHelper.EnableNewsInterests));

            t.Add(T("widgets", TweakGroup.Interface, "widgetsSw", "widgetsTip",
                "Disable Widgets", "Removes the widgets board and its taskbar button.",
                o => o.DisableWidgets, (o, v) => o.DisableWidgets = v,
                OptimizeHelper.DisableWidgets, OptimizeHelper.EnableWidgets, win11: true));

            t.Add(T("chat", TweakGroup.Interface, "chatSw", "chatTip",
                "Disable Chat / Meet Now", "Removes the Teams chat button from the taskbar.",
                o => o.DisableChat, (o, v) => o.DisableChat = v,
                OptimizeHelper.DisableChat, OptimizeHelper.EnableChat, win11: true));

            t.Add(T("snap", TweakGroup.Interface, "snapAssistSw", "snapAssistTip",
                "Disable Snap Assist", "Turns off window snapping suggestions.",
                o => o.DisableSnapAssist, (o, v) => o.DisableSnapAssist = v,
                OptimizeHelper.DisableSnapAssist, OptimizeHelper.EnableSnapAssist, restart: true));

            t.Add(T("taskbarleft", TweakGroup.Interface, "leftTaskbarSw", "leftTaskbarTip",
                "Align taskbar to the left", "Restores the classic Windows alignment.",
                o => o.TaskbarToLeft, (o, v) => o.TaskbarToLeft = v,
                OptimizeHelper.AlignTaskbarToLeft, OptimizeHelper.AlignTaskbarToCenter, win11: true));

            t.Add(T("taskview", TweakGroup.Interface, null, null,
                "Hide Task View button", "Removes the Task View icon from the taskbar.",
                o => o.HideTaskViewButton, (o, v) => o.HideTaskViewButton = v,
                Win11Tweaks.HideTaskViewButton, Win11Tweaks.ShowTaskViewButton, silent: s => s.HideTaskViewButton));

            t.Add(T("taskbarlabels", TweakGroup.Interface, null, null,
                "Never combine taskbar buttons", "Shows window labels like the classic taskbar.",
                o => o.ShowTaskbarLabels, (o, v) => o.ShowTaskbarLabels = v,
                Win11Tweaks.ShowTaskbarLabels, Win11Tweaks.CombineTaskbarButtons,
                minBuild: WindowsRelease.Build11_23H2, win11: true, silent: s => s.ShowTaskbarLabels));

            t.Add(T("endtask", TweakGroup.Interface, null, null,
                "Add \"End task\" to the taskbar", "Kill a hung app straight from its right-click menu.",
                o => o.EnableEndTask, (o, v) => o.EnableEndTask = v,
                Win11Tweaks.EnableEndTask, Win11Tweaks.DisableEndTask,
                minBuild: WindowsRelease.Build11_23H2, win11: true, silent: s => s.EnableEndTask));

            t.Add(T("classicmenu", TweakGroup.Interface, "classicContextSw", "classicContextTip",
                "Restore the classic context menu", "Skips the \"Show more options\" step in Explorer.",
                o => o.ClassicMenu, (o, v) => o.ClassicMenu = v,
                OptimizeHelper.DisableShowMoreOptions, OptimizeHelper.EnableShowMoreOptions,
                restart: true, win11: true));

            t.Add(T("thispc", TweakGroup.Interface, null, null,
                "Open File Explorer to This PC", "Skips the Home view on every new window.",
                o => o.ExplorerOpenThisPC, (o, v) => o.ExplorerOpenThisPC = v,
                Win11Tweaks.ExplorerOpenThisPC, Win11Tweaks.ExplorerOpenHome, silent: s => s.ExplorerOpenThisPC));

            t.Add(T("gallery", TweakGroup.Interface, null, null,
                "Remove Gallery from Explorer", "Hides Gallery from the navigation pane.",
                o => o.HideExplorerGallery, (o, v) => o.HideExplorerGallery = v,
                Win11Tweaks.HideExplorerGallery, Win11Tweaks.ShowExplorerGallery,
                minBuild: WindowsRelease.Build11_23H2, win11: true, silent: s => s.HideExplorerGallery));

            t.Add(T("onedrivenav", TweakGroup.Interface, null, null,
                "Remove OneDrive from Explorer", "Hides OneDrive from the navigation pane.",
                o => o.HideOneDriveInExplorer, (o, v) => o.HideOneDriveInExplorer = v,
                Win11Tweaks.HideOneDriveInExplorer, Win11Tweaks.ShowOneDriveInExplorer,
                silent: s => s.HideOneDriveInExplorer));

            t.Add(T("fileext", TweakGroup.Interface, null, null,
                "Show file extensions", "Explorer stops hiding known file types.",
                o => o.ShowFileExtensions, (o, v) => o.ShowFileExtensions = v,
                Win11Tweaks.ShowFileExtensions, Win11Tweaks.HideFileExtensions, silent: s => s.ShowFileExtensions));

            t.Add(T("quickaccess", TweakGroup.Interface, "oldExplorerSw", "oldExplorerTip",
                "Disable Quick Access history", "File Explorer stops tracking recent files.",
                o => o.DisableQuickAccessHistory, (o, v) => o.DisableQuickAccessHistory = v,
                () => OptimizeHelper.DisableQuickAccessHistory(), OptimizeHelper.EnableQuickAccessHistory,
                restart: true));

            t.Add(T("compact", TweakGroup.Interface, "compactModeSw", "compactModeTip",
                "Enable Explorer compact mode", "Tightens row spacing in File Explorer.",
                o => o.CompactMode, (o, v) => o.CompactMode = v,
                OptimizeHelper.EnableFilesCompactMode, OptimizeHelper.DisableFilesCompactMode,
                restart: true, win11: true));

            t.Add(T("stickers", TweakGroup.Interface, "stickersSw", "stickersTip",
                "Disable desktop stickers", "Removes the sticker editor from the desktop menu.",
                o => o.DisableStickers, (o, v) => o.DisableStickers = v,
                OptimizeHelper.DisableStickers, OptimizeHelper.EnableStickers, win11: true));

            t.Add(T("volume", TweakGroup.Interface, "oldMixerSw", "oldMixerTip",
                "Enable legacy volume slider", "Restores the classic volume flyout.",
                o => o.EnableLegacyVolumeSlider, (o, v) => o.EnableLegacyVolumeSlider = v,
                OptimizeHelper.EnableLegacyVolumeSlider, OptimizeHelper.DisableLegacyVolumeSlider));

            t.Add(T("cast", TweakGroup.Interface, "castSw", "castTip",
                "Remove \"Cast to Device\"", "Cleans the right-click menu.",
                o => o.RemoveCastToDevice, (o, v) => o.RemoveCastToDevice = v,
                OptimizeHelper.RemoveCastToDevice, OptimizeHelper.AddCastToDevice));

            t.Add(T("photoviewer", TweakGroup.Interface, "classicPhotoViewerSw", "classicPhotoViewerSw",
                "Restore classic Photo Viewer", "Re-registers the Windows 7 viewer.",
                o => o.RestoreClassicPhotoViewer, (o, v) => o.RestoreClassicPhotoViewer = v,
                OptimizeHelper.RestoreClassicPhotoViewer, OptimizeHelper.DisableClassicPhotoViewer));

            t.Add(T("hidesearch", TweakGroup.Interface, "hideSearchSw", "hideSearchSw",
                "Hide the taskbar search box", "Frees the space next to Start.",
                o => o.HideTaskbarSearch, (o, v) => o.HideTaskbarSearch = v,
                OptimizeHelper.HideTaskbarSearch, OptimizeHelper.ShowTaskbarSearch));

            t.Add(T("hideweather", TweakGroup.Interface, "hideWeatherSw", "hideWeatherSw",
                "Hide the taskbar weather widget", "Removes the weather and news button.",
                o => o.HideTaskbarWeather, (o, v) => o.HideTaskbarWeather = v,
                OptimizeHelper.HideTaskbarWeather, OptimizeHelper.ShowTaskbarWeather));

            t.Add(T("people", TweakGroup.Interface, "peopleSw", "peopleTip",
                "Disable My People", "Removes the contacts bar from the taskbar.",
                o => o.DisableMyPeople, (o, v) => o.DisableMyPeople = v,
                OptimizeHelper.DisableMyPeople, OptimizeHelper.EnableMyPeople));

            t.Add(T("traymenu", TweakGroup.Interface, "allTrayIconsSw", "allTrayIconsSw",
                "Show all tray icons", "Nothing hides in the overflow area.",
                o => o.ShowAllTrayIcons, (o, v) => o.ShowAllTrayIcons = v,
                OptimizeHelper.ShowAllTrayIcons, OptimizeHelper.HideTrayIcons));

            t.Add(T("menudelay", TweakGroup.Interface, "noMenuDelaySw", "noMenuDelaySw",
                "Remove menu show delay", "Menus open instantly instead of after 400ms.",
                o => o.RemoveMenusDelay, (o, v) => o.RemoveMenusDelay = v,
                OptimizeHelper.RemoveMenusDelay, OptimizeHelper.RestoreMenusDelay, restart: true));

            // Desktop corner notices. See WatermarkHelper for why these are
            // the three that are covered and what is deliberately left out.
            t.Add(T("watermark", TweakGroup.Interface, null, null,
                "Hide desktop build watermark", "Removes the Windows version stamp above the clock.",
                o => o.HideBuildWatermark, (o, v) => o.HideBuildWatermark = v,
                WatermarkHelper.HideBuildWatermark, WatermarkHelper.ShowBuildWatermark,
                silent: s => s.HideBuildWatermark));

            t.Add(T("hwnotice", TweakGroup.Interface, null, null,
                "Hide \"System requirements not met\"", "Clears the unsupported-hardware notice on the desktop.",
                o => o.HideUnsupportedHardwareNotice, (o, v) => o.HideUnsupportedHardwareNotice = v,
                WatermarkHelper.HideUnsupportedHardwareNotice, WatermarkHelper.ShowUnsupportedHardwareNotice,
                win11: true, silent: s => s.HideUnsupportedHardwareNotice));

            t.Add(T("actnotice", TweakGroup.Interface, null, null,
                "Hide activation reminders", "Silences the activation notices. Does not activate Windows.",
                o => o.HideActivationNotices, (o, v) => o.HideActivationNotices = v,
                WatermarkHelper.HideActivationNotices, WatermarkHelper.ShowActivationNotices,
                silent: s => s.HideActivationNotices));

            // ══ System ═════════════════════════════════════════════════
            t.Add(T("onedrive", TweakGroup.System, "uODSw", "uODTip",
                "Uninstall OneDrive", "Removes the client and Explorer integration.",
                o => o.UninstallOneDrive, (o, v) => o.UninstallOneDrive = v,
                OptimizeHelper.UninstallOneDrive, OptimizeHelper.InstallOneDrive,
                confirmKey: "onedriveM", async: true));

            t.Add(T("onedrivesync", TweakGroup.System, "disableOneDriveSw", "disableOneDriveTip",
                "Disable OneDrive sync", "Blocks file sync without uninstalling the client.",
                o => o.DisableOneDrive, (o, v) => o.DisableOneDrive = v,
                OptimizeHelper.DisableOneDrive, OptimizeHelper.EnableOneDrive));

            t.Add(T("errors", TweakGroup.System, "reportingSw", "reportingTip",
                "Disable error reporting", "Stops the Windows Error Reporting service.",
                o => o.DisableErrorReporting, (o, v) => o.DisableErrorReporting = v,
                OptimizeHelper.DisableErrorReporting, OptimizeHelper.EnableErrorReporting));

            t.Add(T("smb1", TweakGroup.System, "smb1Sw", null,
                "Disable SMB 1.0", "Closes a legacy and insecure file-sharing protocol.",
                o => o.DisableSMB1, (o, v) => o.DisableSMB1 = v,
                () => OptimizeHelper.DisableSMB("1"), () => OptimizeHelper.EnableSMB("1"), restart: true));

            t.Add(T("smb2", TweakGroup.System, "smb2Sw", null,
                "Disable SMB 2.0", "Only do this if nothing on your network needs SMB2.",
                o => o.DisableSMB2, (o, v) => o.DisableSMB2 = v,
                () => OptimizeHelper.DisableSMB("2"), () => OptimizeHelper.EnableSMB("2"), restart: true));

            t.Add(T("fax", TweakGroup.System, "faxSw", "faxTip",
                "Disable Fax service", "Nobody has faxed you since 2009.",
                o => o.DisableFaxService, (o, v) => o.DisableFaxService = v,
                OptimizeHelper.DisableFaxService, OptimizeHelper.EnableFaxService));

            t.Add(T("print", TweakGroup.System, "printSw", "printTip",
                "Set Print Spooler to manual", "Starts only when you actually print.",
                o => o.DisablePrintService, (o, v) => o.DisablePrintService = v,
                OptimizeHelper.DisablePrintService, OptimizeHelper.EnablePrintService));

            t.Add(T("sticky", TweakGroup.System, "stickySw", "stickyTip",
                "Disable Sticky Keys prompt", "No more accidental shift-key popups.",
                o => o.DisableStickyKeys, (o, v) => o.DisableStickyKeys = v,
                OptimizeHelper.DisableStickyKeys, OptimizeHelper.EnableStickyKeys));

            t.Add(T("longpaths", TweakGroup.System, "longPathsSw", "longPathsTip",
                "Enable long paths", "Lifts the 260-character path limit.",
                o => o.EnableLongPaths, (o, v) => o.EnableLongPaths = v,
                OptimizeHelper.EnableLongPaths, OptimizeHelper.DisableLongPaths, restart: true));

            t.Add(T("verbose", TweakGroup.System, "loginVerboseSw", "loginVerboseSw",
                "Enable detailed login screen", "Shows service status during boot and login.",
                o => o.EnableLoginVerbose, (o, v) => o.EnableLoginVerbose = v,
                Utilities.EnableLoginVerbose, Utilities.DisableLoginVerbose));

            t.Add(T("sudo", TweakGroup.System, null, null,
                "Enable Sudo for Windows", "Run a single command elevated from an ordinary prompt.",
                o => o.EnableSudo, (o, v) => o.EnableSudo = v,
                Win11Tweaks.EnableSudo, Win11Tweaks.DisableSudo,
                minBuild: WindowsRelease.Build11_24H2, win11: true, silent: s => s.EnableSudo));

            t.Add(T("tpm", TweakGroup.System, "tpmSw", "tpmTip",
                "Bypass TPM & Secure Boot checks", "Lets unsupported hardware take Windows 11 updates.",
                o => o.DisableTPMCheck, (o, v) => o.DisableTPMCheck = v,
                OptimizeHelper.DisableTPMCheck, OptimizeHelper.EnableTPMCheck));

            t.Add(T("restore", TweakGroup.System, "systemRestoreSw", "systemRestoreTip",
                "Disable System Restore", "Frees shadow-copy space. You lose restore points.",
                o => o.DisableSystemRestore, (o, v) => o.DisableSystemRestore = v,
                OptimizeHelper.DisableSystemRestore, OptimizeHelper.EnableSystemRestore,
                confirmKey: "systemRestoreM"));

            t.Add(T("compat", TweakGroup.System, "compatSw", "compatTip",
                "Disable Program Compatibility Assistant", "Stops the \"this app may not work\" prompts.",
                o => o.DisableCompatibilityAssistant, (o, v) => o.DisableCompatibilityAssistant = v,
                OptimizeHelper.DisableCompatibilityAssistant, OptimizeHelper.EnableCompatibilityAssistant));

            t.Add(T("homegroup", TweakGroup.System, "homegroupSw", "homegroupTip",
                "Disable HomeGroup", "Removes the legacy sharing services.",
                o => o.DisableHomeGroup, (o, v) => o.DisableHomeGroup = v,
                OptimizeHelper.DisableHomeGroup, OptimizeHelper.EnableHomeGroup));

            t.Add(T("mediasharing", TweakGroup.System, "mediaSharingSw", "mediaSharingTip",
                "Disable Media Player sharing", "Stops the network media streaming service.",
                o => o.DisableMediaPlayerSharing, (o, v) => o.DisableMediaPlayerSharing = v,
                OptimizeHelper.DisableMediaPlayerSharing, OptimizeHelper.EnableMediaPlayerSharing));

            // The root cause behind the "Test Mode" watermark. Turning test
            // signing off removes the watermark by removing the reason for it;
            // bcdedit refuses the change outright under Secure Boot, which is
            // the right answer rather than something to work around.
            t.Add(T("testsigning", TweakGroup.System, null, null,
                "Turn off test signing", "Leaves Test Mode and removes its desktop watermark.",
                o => o.DisableTestSigning, (o, v) => o.DisableTestSigning = v,
                WatermarkHelper.DisableTestSigning, WatermarkHelper.EnableTestSigning,
                restart: true, silent: s => s.DisableTestSigning));

            t.Add(T("utc", TweakGroup.System, "enableUtcSw", "enableUtcSw",
                "Store the hardware clock as UTC", "Keeps time correct when dual-booting Linux.",
                o => o.EnableUtcTime, (o, v) => o.EnableUtcTime = v,
                OptimizeHelper.EnableUTCTime, OptimizeHelper.DisableUTCTime, restart: true));

            return t;
        }
    }
}
