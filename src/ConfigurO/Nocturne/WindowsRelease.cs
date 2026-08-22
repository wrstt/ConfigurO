using System;
using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace ConfigurO
{
    /// <summary>
    /// Authoritative Windows version information.
    ///
    /// <c>Environment.OSVersion</c> and <c>GetVersionEx</c> are shimmed and lie
    /// about anything past Windows 8 unless the app manifest opts in, so this
    /// reads <c>RtlGetVersion</c> (never shimmed) and fills in the marketing
    /// details from the registry. Windows 11 reports itself as major 10 with a
    /// build of 22000 or higher, which is the only reliable way to tell the two
    /// apart.
    /// </summary>
    internal static class WindowsRelease
    {
        const string CurrentVersionKey = @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows NT\CurrentVersion";

        // Windows 11 feature-update build floors.
        internal const int Build11_21H2 = 22000;
        internal const int Build11_22H2 = 22621;
        internal const int Build11_23H2 = 22631;
        internal const int Build11_24H2 = 26100;
        internal const int Build11_25H2 = 26200;

        [StructLayout(LayoutKind.Sequential)]
        struct OSVERSIONINFOEXW
        {
            public int dwOSVersionInfoSize;
            public int dwMajorVersion;
            public int dwMinorVersion;
            public int dwBuildNumber;
            public int dwPlatformId;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string szCSDVersion;
            public ushort wServicePackMajor;
            public ushort wServicePackMinor;
            public ushort wSuiteMask;
            public byte wProductType;
            public byte wReserved;
        }

        [DllImport("ntdll.dll", CharSet = CharSet.Unicode)]
        static extern int RtlGetVersion(ref OSVERSIONINFOEXW v);

        static bool _probed;
        static int _major, _minor, _build, _ubr;
        static string _productName, _displayVersion, _edition;

        static void Probe()
        {
            if (_probed) return;
            _probed = true;

            OSVERSIONINFOEXW v = new OSVERSIONINFOEXW();
            v.dwOSVersionInfoSize = Marshal.SizeOf(typeof(OSVERSIONINFOEXW));
            bool ok = false;
            try { ok = RtlGetVersion(ref v) == 0; }
            catch (Exception ex) { Logger.LogError("WindowsRelease.Probe", ex.Message, ex.StackTrace); }

            if (ok)
            {
                _major = v.dwMajorVersion;
                _minor = v.dwMinorVersion;
                _build = v.dwBuildNumber;
            }
            else
            {
                Version os = Environment.OSVersion.Version;
                _major = os.Major; _minor = os.Minor; _build = os.Build;
            }

            _productName   = Reg("ProductName", string.Empty);
            _displayVersion = Reg("DisplayVersion", string.Empty);
            if (string.IsNullOrEmpty(_displayVersion)) _displayVersion = Reg("ReleaseId", string.Empty);
            _edition       = Reg("EditionID", string.Empty);
            _ubr           = RegInt("UBR", 0);

            // The registry still says "Windows 10 ..." on Windows 11.
            if (_build >= Build11_21H2 && _productName.IndexOf("Windows 10", StringComparison.OrdinalIgnoreCase) >= 0)
                _productName = _productName.Replace("Windows 10", "Windows 11");
        }

        static string Reg(string name, string fallback)
        {
            try { return (Registry.GetValue(CurrentVersionKey, name, fallback) as string) ?? fallback; }
            catch { return fallback; }
        }

        static int RegInt(string name, int fallback)
        {
            try
            {
                object o = Registry.GetValue(CurrentVersionKey, name, fallback);
                return o == null ? fallback : Convert.ToInt32(o);
            }
            catch { return fallback; }
        }

        internal static int Major        { get { Probe(); return _major; } }
        internal static int Build        { get { Probe(); return _build; } }
        internal static int Revision     { get { Probe(); return _ubr; } }
        /// <summary>Marketing name, e.g. "Windows 11 Pro".</summary>
        internal static string ProductName { get { Probe(); return _productName; } }
        /// <summary>Feature update, e.g. "24H2". Empty before Windows 10.</summary>
        internal static string DisplayVersion { get { Probe(); return _displayVersion; } }
        internal static string EditionId { get { Probe(); return _edition; } }

        internal static bool IsWindows11 { get { Probe(); return _major >= 10 && _build >= Build11_21H2; } }
        internal static bool IsWindows10 { get { Probe(); return _major == 10 && _build < Build11_21H2; } }
        internal static bool IsAtLeastBuild(int build) { Probe(); return _build >= build; }

        internal static bool Is11_22H2OrLater { get { return IsAtLeastBuild(Build11_22H2); } }
        internal static bool Is11_23H2OrLater { get { return IsAtLeastBuild(Build11_23H2); } }
        internal static bool Is11_24H2OrLater { get { return IsAtLeastBuild(Build11_24H2); } }
        internal static bool Is11_25H2OrLater { get { return IsAtLeastBuild(Build11_25H2); } }

        // ── Chrome capability gates ─────────────────────────────────────
        /// <summary>DWMWA_USE_IMMERSIVE_DARK_MODE, stable from Windows 10 20H1.</summary>
        internal static bool SupportsImmersiveDarkMode { get { return IsAtLeastBuild(18985); } }
        /// <summary>DWMWA_WINDOW_CORNER_PREFERENCE -- Windows 11 only.</summary>
        internal static bool SupportsRoundedCorners { get { return IsWindows11; } }
        /// <summary>DWMWA_SYSTEMBACKDROP_TYPE (Mica / Acrylic) -- Windows 11 22H2+.</summary>
        internal static bool SupportsBackdrop { get { return Is11_22H2OrLater; } }
        /// <summary>Snap Layouts flyout on hover over the maximise button.</summary>
        internal static bool SupportsSnapLayouts { get { return IsWindows11; } }

        internal static string Architecture
        {
            get { return Environment.Is64BitOperatingSystem ? "x64" : "x86"; }
        }

        /// <summary>
        /// The title-bar string, e.g. "Windows 11 Pro · 24H2 · x64".
        /// </summary>
        internal static string ChromeSummary()
        {
            Probe();
            string name = string.IsNullOrEmpty(_productName) ? "Windows" : _productName;
            return string.IsNullOrEmpty(_displayVersion)
                ? string.Format("{0} · {1}", name, Architecture)
                : string.Format("{0} · {1} · {2}", name, _displayVersion, Architecture);
        }

        /// <summary>Full build string for logs and the Hardware screen.</summary>
        internal static string FullBuild()
        {
            Probe();
            return _ubr > 0
                ? string.Format("{0}.{1}.{2}", _major, _build, _ubr)
                : string.Format("{0}.{1}", _major, _build);
        }
    }
}
