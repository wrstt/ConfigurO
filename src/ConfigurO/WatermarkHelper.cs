using System;
using Microsoft.Win32;

namespace ConfigurO
{
    /// <summary>
    /// The desktop corner notices -- the build stamp, the Windows 11 "System
    /// requirements not met" nag and the activation reminders.
    ///
    /// This is the ground Universal Watermark Disabler covers, reached the way
    /// a configuration tool should reach it. UWD and its successors get at the
    /// remaining watermarks by force: the original swaps modified copies of
    /// basebrd.dll.mui and shell32.dll.mui over the shipped ones, uwd2 walks
    /// Microsoft's symbols to find CDesktopWatermark::s_DesktopBuildPaint and
    /// writes a ret over it in the running explorer.exe, and pr701's build
    /// COM-hijacks ExplorerFrame. None of those survive a servicing update, an
    /// sfc /scannow or a reboot, and all of them fail loudly on the machines
    /// where they go wrong.
    ///
    /// Everything here is a documented value, reverts cleanly and is what the
    /// corresponding Settings page or Group Policy object would write. The
    /// trade is honest and worth stating plainly: the Test Mode and Secure
    /// Boot watermarks have no such value behind them and are therefore not
    /// covered -- <see cref="TestSigningEnabled"/> exists so the Test Mode
    /// case can at least be named and fixed at the source instead.
    /// </summary>
    internal static class WatermarkHelper
    {
        const string Desktop        = @"HKEY_CURRENT_USER\Control Panel\Desktop";
        const string DesktopSub     = @"Control Panel\Desktop";

        const string HwNotice       = @"HKEY_CURRENT_USER\Control Panel\UnsupportedHardwareNotificationCache";
        const string HwNoticeSub    = @"Control Panel\UnsupportedHardwareNotificationCache";

        const string Activation     = @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows NT\CurrentVersion\SoftwareProtectionPlatform\Activation";
        const string ActivationSub  = @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\SoftwareProtectionPlatform\Activation";

        const string LicenseTask    = @"\Microsoft\Windows\Subscription\LicenseAcquisition";

        static void Set(string key, string name, int value)
        {
            try { Registry.SetValue(key, name, value, RegistryValueKind.DWord); }
            catch (Exception ex) { Logger.LogError("WatermarkHelper.Set:" + name, ex.Message, ex.StackTrace); }
        }

        // ── Build / version stamp above the clock ───────────────────────
        //
        // PaintDesktopVersion is the switch the shell has honoured since NT 4
        // and is what "Show Windows version on desktop" toggles. It clears the
        // plain build stamp; pre-release and evaluation strings are painted by
        // a different path and are not affected.

        internal static void HideBuildWatermark()
        {
            Set(Desktop, "PaintDesktopVersion", 0);
        }

        internal static void ShowBuildWatermark()
        {
            Utilities.TryDeleteRegistryValue(false, DesktopSub, "PaintDesktopVersion");
        }

        /// <summary>Whether the build stamp is currently suppressed.</summary>
        internal static bool BuildWatermarkHidden
        {
            get
            {
                object v = Registry.GetValue(Desktop, "PaintDesktopVersion", null);
                return v != null && Convert.ToInt32(v) == 0;
            }
        }

        // ── "System requirements not met" (Windows 11 24H2+) ────────────
        //
        // Windows caches its verdict in SV1/SV2 and repaints from the cache,
        // so zeroing both is what the notice actually reads. It says nothing
        // about whether the machine is supported -- upgrades still evaluate
        // the hardware on their own.

        internal static void HideUnsupportedHardwareNotice()
        {
            Set(HwNotice, "SV1", 0);
            Set(HwNotice, "SV2", 0);
        }

        internal static void ShowUnsupportedHardwareNotice()
        {
            Utilities.TryDeleteRegistryValue(false, HwNoticeSub, "SV1");
            Utilities.TryDeleteRegistryValue(false, HwNoticeSub, "SV2");
        }

        // ── Activation reminders ────────────────────────────────────────
        //
        // NotificationDisabled is Microsoft's own volume-activation switch for
        // sites that do not want the reminders on screen, and Manual stops the
        // platform reaching out on its own schedule. This suppresses the
        // notices only: the licensing state is untouched, an unlicensed
        // machine stays unlicensed and keeps every restriction that comes with
        // that. It is not an activation bypass and does not pretend to be one.

        internal static void HideActivationNotices()
        {
            Set(Activation, "NotificationDisabled", 1);
            Set(Activation, "Manual", 1);
            Utilities.RunCommand("schtasks.exe /change /disable /tn \"" + LicenseTask + "\"");
        }

        internal static void ShowActivationNotices()
        {
            Utilities.TryDeleteRegistryValue(true, ActivationSub, "NotificationDisabled");
            Utilities.TryDeleteRegistryValue(true, ActivationSub, "Manual");
            Utilities.RunCommand("schtasks.exe /change /enable /tn \"" + LicenseTask + "\"");
        }

        // ── Test Mode ───────────────────────────────────────────────────
        //
        // The Test Mode watermark is painted because the boot configuration
        // has test signing on, so the fix is to turn test signing off rather
        // than to hide the evidence that it is on. That is a boot-policy
        // change and needs a restart; on a Secure Boot machine bcdedit will
        // refuse it outright, which is the correct outcome.

        /// <summary>Whether the running boot entry has test signing enabled.</summary>
        internal static bool TestSigningEnabled
        {
            get
            {
                try
                {
                    string output = Utilities.RunCommandCapture("bcdedit /enum {current}");
                    if (string.IsNullOrEmpty(output)) return false;
                    foreach (string line in output.Split('\n'))
                    {
                        string l = line.Trim();
                        if (l.StartsWith("testsigning", StringComparison.OrdinalIgnoreCase))
                            return l.IndexOf("Yes", StringComparison.OrdinalIgnoreCase) >= 0;
                    }
                }
                catch (Exception ex)
                {
                    Logger.LogError("WatermarkHelper.TestSigningEnabled", ex.Message, ex.StackTrace);
                }
                return false;
            }
        }

        internal static void DisableTestSigning()
        {
            Utilities.RunCommand("bcdedit /set testsigning off");
        }

        internal static void EnableTestSigning()
        {
            Utilities.RunCommand("bcdedit /set testsigning on");
        }
    }
}
