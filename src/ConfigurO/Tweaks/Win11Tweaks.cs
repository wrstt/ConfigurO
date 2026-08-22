using System;
using Microsoft.Win32;

namespace ConfigurO
{
    /// <summary>
    /// Windows 11-era tweaks that post-date the original helper set: Recall and
    /// Click to Do, the Start "Recommended" section, taskbar labels and End
    /// task, the Explorer Gallery, Sudo, memory integrity and friends.
    ///
    /// Kept separate from <see cref="OptimizeHelper"/> so the long-standing
    /// tweak code is untouched. Every pair is symmetric -- Disable/Enable
    /// restore the documented default -- and every write is guarded, because a
    /// policy hive may be absent or locked down on Home SKUs.
    /// </summary>
    internal static class Win11Tweaks
    {
        const string Advanced      = @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced";
        const string AdvancedSub   = @"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced";
        const string WindowsAI     = @"HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\Windows\WindowsAI";
        const string WindowsAISub  = @"SOFTWARE\Policies\Microsoft\Windows\WindowsAI";
        const string ExplorerPol   = @"HKEY_CURRENT_USER\Software\Policies\Microsoft\Windows\Explorer";
        const string ExplorerPolSub= @"Software\Policies\Microsoft\Windows\Explorer";
        const string StartPolicy   = @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\PolicyManager\current\device\Start";
        const string StartPolicySub= @"SOFTWARE\Microsoft\PolicyManager\current\device\Start";
        const string DevSettings   = Advanced + @"\TaskbarDeveloperSettings";
        const string DevSettingsSub= AdvancedSub + @"\TaskbarDeveloperSettings";
        const string SmartClipboard    = @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\SmartActionPlatform\SmartClipboard";
        const string SmartClipboardSub = @"Software\Microsoft\Windows\CurrentVersion\SmartActionPlatform\SmartClipboard";
        const string Engagement    = @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\UserProfileEngagement";
        const string SudoKey       = @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\Sudo";
        const string HvciKey       = @"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control\DeviceGuard\Scenarios\HypervisorEnforcedCodeIntegrity";

        // Explorer CLSIDs pinned into the navigation pane.
        const string GalleryClsid  = @"HKEY_CURRENT_USER\Software\Classes\CLSID\{e88865ea-0e1c-4e20-9aa6-edcd0212c87c}";
        const string OneDriveClsid = @"HKEY_CURRENT_USER\Software\Classes\CLSID\{018D5C66-4533-4307-9B53-224DE2ED1FE6}";
        const string PinnedValue   = "System.IsPinnedToNameSpaceTree";

        static void Set(string key, string name, int value)
        {
            try { Registry.SetValue(key, name, value, RegistryValueKind.DWord); }
            catch (Exception ex) { Logger.LogError("Win11Tweaks.Set:" + name, ex.Message, ex.StackTrace); }
        }

        static void Clear(bool localMachine, string subKey, string name)
        {
            Utilities.TryDeleteRegistryValue(localMachine, subKey, name);
        }

        // ── Recall: "Turn off saving snapshots for Windows" (24H2+) ─────
        internal static void DisableRecall()      { Set(WindowsAI, "DisableAIDataAnalysis", 1); }
        internal static void EnableRecall()       { Clear(true, WindowsAISub, "DisableAIDataAnalysis"); }

        // ── Click to Do (24H2+) ─────────────────────────────────────────
        internal static void DisableClickToDo()   { Set(WindowsAI, "DisableClickToDo", 1); }
        internal static void EnableClickToDo()    { Clear(true, WindowsAISub, "DisableClickToDo"); }

        // ── Bing / web results in Search ────────────────────────────────
        internal static void DisableWebSearch()   { Set(ExplorerPol, "DisableSearchBoxSuggestions", 1); }
        internal static void EnableWebSearch()    { Clear(false, ExplorerPolSub, "DisableSearchBoxSuggestions"); }

        // ── "Suggested actions" when copying dates/numbers (22H2+) ──────
        internal static void DisableSuggestedActions() { Set(SmartClipboard, "Disabled", 1); }
        internal static void EnableSuggestedActions()  { Clear(false, SmartClipboardSub, "Disabled"); }

        // ── "Let's finish setting up your device" (SCOOBE) ──────────────
        internal static void DisableSetupPrompts() { Set(Engagement, "ScoobeSystemSettingEnabled", 0); }
        internal static void EnableSetupPrompts()  { Set(Engagement, "ScoobeSystemSettingEnabled", 1); }

        // ── Taskbar: Task View button ───────────────────────────────────
        internal static void HideTaskViewButton()  { Set(Advanced, "ShowTaskViewButton", 0); }
        internal static void ShowTaskViewButton()  { Set(Advanced, "ShowTaskViewButton", 1); }

        // ── Start menu "Recommended" section (22H2+) ────────────────────
        internal static void HideStartRecommended()
        {
            Set(StartPolicy, "HideRecommendedSection", 1);
            Set(Advanced, "Start_TrackDocs", 0);
            Set(Advanced, "Start_TrackProgs", 0);
        }

        internal static void ShowStartRecommended()
        {
            Clear(true, StartPolicySub, "HideRecommendedSection");
            Set(Advanced, "Start_TrackDocs", 1);
            Set(Advanced, "Start_TrackProgs", 1);
        }

        // ── Taskbar: never combine, show labels (23H2+) ─────────────────
        // TaskbarGlomLevel: 0 always combine, 1 when full, 2 never.
        internal static void ShowTaskbarLabels()
        {
            Set(Advanced, "TaskbarGlomLevel", 2);
            Set(Advanced, "MMTaskbarGlomLevel", 2);
        }

        internal static void CombineTaskbarButtons()
        {
            Set(Advanced, "TaskbarGlomLevel", 0);
            Set(Advanced, "MMTaskbarGlomLevel", 0);
        }

        // ── Taskbar right-click "End task" (23H2+) ──────────────────────
        internal static void EnableEndTask()  { Set(DevSettings, "TaskbarEndTask", 1); }
        internal static void DisableEndTask() { Clear(false, DevSettingsSub, "TaskbarEndTask"); }

        // ── File Explorer opens to This PC instead of Home ──────────────
        internal static void ExplorerOpenThisPC() { Set(Advanced, "LaunchTo", 1); }
        internal static void ExplorerOpenHome()   { Set(Advanced, "LaunchTo", 2); }

        // ── Gallery in the Explorer navigation pane (23H2+) ─────────────
        internal static void HideExplorerGallery() { Set(GalleryClsid, PinnedValue, 0); }
        internal static void ShowExplorerGallery() { Set(GalleryClsid, PinnedValue, 1); }

        // ── OneDrive in the Explorer navigation pane ────────────────────
        internal static void HideOneDriveInExplorer() { Set(OneDriveClsid, PinnedValue, 0); }
        internal static void ShowOneDriveInExplorer() { Set(OneDriveClsid, PinnedValue, 1); }

        // ── Show file extensions ────────────────────────────────────────
        internal static void ShowFileExtensions() { Set(Advanced, "HideFileExt", 0); }
        internal static void HideFileExtensions() { Set(Advanced, "HideFileExt", 1); }

        // ── Sudo for Windows (24H2+). 0 off, 1 new window, 2 input closed, 3 inline.
        internal static void EnableSudo()  { Set(SudoKey, "Enabled", 3); }
        internal static void DisableSudo() { Set(SudoKey, "Enabled", 0); }

        // ── Memory integrity / HVCI ─────────────────────────────────────
        internal static void DisableMemoryIntegrity() { Set(HvciKey, "Enabled", 0); }
        internal static void EnableMemoryIntegrity()  { Set(HvciKey, "Enabled", 1); }

        // ── CPU cores: lift the boot core limit ─────────────────────────
        internal static void UnlockAllCores() { Utilities.UnlockAllCores(); }

        /// <summary>
        /// There is no "re-limit" counterpart -- the tweak clears a boot cap
        /// that Windows only sets when msconfig has been used. Restoring the
        /// documented default (no cap) is what both states mean, so turning the
        /// toggle back off is a no-op beyond forgetting the preference.
        /// </summary>
        internal static void RestoreCoreLimit() { }
    }
}
