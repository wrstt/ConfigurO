using System;
using Microsoft.Win32;

namespace ConfigurO
{
    /// <summary>
    /// Reads and writes the StartupApproved flags -- the mechanism Task
    /// Manager and Settings use to disable a startup entry without deleting it.
    ///
    /// The legacy tool could only remove entries. The redesigned Startup screen
    /// has an Enabled toggle, which needs this: each entry gets a 12-byte value
    /// under StartupApproved whose first byte is 0x02 when enabled and 0x03
    /// when disabled, followed by the FILETIME of the change.
    /// </summary>
    internal static class StartupApproval
    {
        const string Base = @"Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved";

        const byte Enabled = 0x02;
        const byte Disabled = 0x03;

        static void Locate(StartupItem item, out RegistryKey hive, out string subKey)
        {
            switch (item.RegistryLocation)
            {
                case StartupItemLocation.HKLM:
                    hive = Registry.LocalMachine; subKey = Base + @"\Run"; break;
                case StartupItemLocation.HKLMWoW:
                    hive = Registry.LocalMachine; subKey = Base + @"\Run32"; break;
                case StartupItemLocation.HKCU:
                    hive = Registry.CurrentUser; subKey = Base + @"\Run"; break;
                case StartupItemLocation.LMStartupFolder:
                    hive = Registry.LocalMachine; subKey = Base + @"\StartupFolder"; break;
                default:
                    hive = Registry.CurrentUser; subKey = Base + @"\StartupFolder"; break;
            }
        }

        /// <summary>
        /// Folder entries are keyed by shortcut file name; registry entries by
        /// their value name.
        /// </summary>
        static string ValueName(StartupItem item)
        {
            FolderStartupItem folder = item as FolderStartupItem;
            if (folder != null && !string.IsNullOrEmpty(folder.Shortcut))
                return System.IO.Path.GetFileName(folder.Shortcut);
            return item.Name;
        }

        internal static bool IsEnabled(StartupItem item)
        {
            RegistryKey hive; string subKey;
            Locate(item, out hive, out subKey);
            try
            {
                using (RegistryKey k = hive.OpenSubKey(subKey, false))
                {
                    if (k == null) return true;   // no record means it has never been disabled
                    byte[] value = k.GetValue(ValueName(item)) as byte[];
                    if (value == null || value.Length == 0) return true;
                    // Any odd first byte marks a disabled entry.
                    return (value[0] & 1) == 0;
                }
            }
            catch (Exception ex)
            {
                Logger.LogError("StartupApproval.IsEnabled", ex.Message, ex.StackTrace);
                return true;
            }
        }

        internal static bool SetEnabled(StartupItem item, bool enabled)
        {
            RegistryKey hive; string subKey;
            Locate(item, out hive, out subKey);
            try
            {
                using (RegistryKey k = hive.CreateSubKey(subKey))
                {
                    if (k == null) return false;
                    byte[] value = new byte[12];
                    value[0] = enabled ? Enabled : Disabled;
                    if (!enabled)
                    {
                        // Bytes 4..11 hold the FILETIME of the change.
                        byte[] stamp = BitConverter.GetBytes(DateTime.Now.ToFileTime());
                        Array.Copy(stamp, 0, value, 4, 8);
                    }
                    k.SetValue(ValueName(item), value, RegistryValueKind.Binary);
                    return true;
                }
            }
            catch (Exception ex)
            {
                Logger.LogError("StartupApproval.SetEnabled", ex.Message, ex.StackTrace);
                return false;
            }
        }

        /// <summary>A friendly publisher string for the Startup table.</summary>
        internal static string Publisher(StartupItem item)
        {
            try
            {
                string path = item.FileLocation;
                if (string.IsNullOrEmpty(path)) return string.Empty;
                path = path.Replace("\"", string.Empty).Trim();
                int exe = path.IndexOf(".exe", StringComparison.OrdinalIgnoreCase);
                if (exe > 0) path = path.Substring(0, exe + 4);
                if (!System.IO.File.Exists(path)) return string.Empty;
                return System.Diagnostics.FileVersionInfo.GetVersionInfo(path).CompanyName ?? string.Empty;
            }
            catch (Exception ex)
            {
                Logger.LogError("StartupApproval.Publisher", ex.Message, ex.StackTrace);
                return string.Empty;
            }
        }
    }
}
