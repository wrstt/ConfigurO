using System;
using System.Drawing;
using System.Drawing.Text;
using System.Runtime.InteropServices;

namespace ConfigurO
{
    /// <summary>
    /// The ConfigurO type system: Inter for UI, IBM Plex Mono for paths, IPs
    /// and console output. Both are bundled (SIL OFL 1.1) and loaded from
    /// embedded resources, so nothing has to be installed.
    ///
    /// Inter is what the Nocturne handoff was drawn against, and it holds its
    /// colour at 12-14px on any display, which is the whole job here.
    ///
    /// Hierarchy is size and space -- never weight above Medium (500).
    /// If loading fails for any reason we fall back through the best
    /// available system UI faces rather than dropping to the WinForms default.
    /// </summary>
    internal static class NocturneFonts
    {
        const uint FR_PRIVATE = 0x10;

        [DllImport("gdi32.dll", CharSet = CharSet.Unicode)]
        static extern int AddFontResourceEx(string file, uint flags, IntPtr reserved);

        static PrivateFontCollection _private;
        static FontFamily _sans, _sansMedium, _mono;

        // The memory handed to AddFontMemResourceEx must stay alive for as long
        // as the fonts are in use, so these are deliberately never freed.
        static readonly System.Collections.Generic.List<IntPtr> _pinned = new System.Collections.Generic.List<IntPtr>();

        static readonly string[] SansFallback =
        {
            "Inter", "Segoe UI Variable Text", "Segoe UI", "Tahoma"
        };

        /// <summary>
        /// Inter covers Latin, Greek and Cyrillic and nothing else. Nine of the
        /// 28 languages are written in scripts it has no glyphs for, and GDI+
        /// does not font-link a PrivateFontCollection face, so those would draw
        /// as rows of .notdef boxes. Each maps to the system UI face Windows
        /// ships for that script instead; the bundled face is skipped entirely
        /// rather than half-used.
        /// </summary>
        static readonly string[] ArabicFallback   = { "Segoe UI", "Tahoma", "Arial" };
        static readonly string[] ChineseFallback  = { "Microsoft YaHei UI", "Microsoft YaHei", "SimSun" };
        static readonly string[] TaiwanFallback   = { "Microsoft JhengHei UI", "Microsoft JhengHei", "MingLiU" };
        static readonly string[] JapaneseFallback = { "Yu Gothic UI", "Meiryo UI", "Meiryo", "MS UI Gothic" };
        static readonly string[] KoreanFallback   = { "Malgun Gothic", "Gulim" };
        static readonly string[] IndicFallback    = { "Nirmala UI", "Mangal" };

        /// <summary>
        /// The fallback chain for the language in use, or null when Inter can
        /// render it. Read per call so switching language takes effect without
        /// a restart.
        /// </summary>
        static string[] ScriptFallback()
        {
            Options o = OptionsHelper.CurrentOptions;
            return o == null ? null : ScriptFallback(o.LanguageCode);
        }

        /// <summary>
        /// The chain for one specific language, whatever the app is currently
        /// set to. The first-run picker lists all 28 names in their own scripts
        /// at once, so it needs this per row rather than per app.
        /// </summary>
        internal static string[] ScriptFallback(LanguageCode code)
        {
            switch (code)
            {
                case LanguageCode.AR:
                case LanguageCode.FA:
                case LanguageCode.UR:
                case LanguageCode.KU: return ArabicFallback;
                case LanguageCode.CN: return ChineseFallback;
                case LanguageCode.TW: return TaiwanFallback;
                case LanguageCode.JA: return JapaneseFallback;
                case LanguageCode.KO: return KoreanFallback;
                case LanguageCode.NE: return IndicFallback;
                default: return null;
            }
        }
        static readonly string[] MonoFallback =
        {
            "IBM Plex Mono", "Cascadia Mono", "Consolas", "Courier New"
        };

        internal static bool Bundled { get; private set; }

        /// <summary>
        /// Skips the bundled faces and uses the system fallback chain instead.
        /// Set by the headless render harness only: libgdiplus can register a
        /// memory font it cannot then rasterise, which turns every glyph into
        /// a box. Must be set before <see cref="Load"/>.
        /// </summary>
        internal static bool ForceSystemFonts { get; set; }

        /// <summary>The families GDI+ actually registered. Diagnostics only.</summary>
        internal static FontFamily[] LoadedFamilies
        {
            get { return _private == null ? new FontFamily[0] : _private.Families; }
        }

        static readonly object _loadGate = new object();

        internal static void Load()
        {
            // The splash screen runs on its own STA thread and also asks for
            // the fonts, so this has to be safe to call from both.
            lock (_loadGate)
            {
                LoadCore();
            }
        }

        static readonly string FontFolder = System.IO.Path.Combine(CoreHelper.CoreFolder, "Fonts");

        static void LoadCore()
        {
            if (_private != null) return;
            _private = new PrivateFontCollection();

            // Identify each face by the family it adds, not by its name: GDI+
            // reports different family names on different platforms (Windows
            // uses the font's GDI family, "Inter Medium"; libgdiplus
            // uses the typographic one), so both Sans faces can arrive with
            // identical names. Load order is the only thing true everywhere.
            _sans       = Add("Inter-Regular.ttf", Properties.Resources.Inter_Regular);
            _sansMedium = Add("Inter-Medium.ttf", Properties.Resources.Inter_Medium);
            _mono       = Add("IBMPlexMono-Regular.ttf", Properties.Resources.IBMPlexMono_Regular);

            if (_sans == null) _sans = _sansMedium;
            if (_sansMedium == null) _sansMedium = _sans;
            Bundled = _sans != null;
        }

        /// <summary>
        /// Registers one bundled face and returns the family it produced.
        ///
        /// The face is extracted next to the app's other deployed files and
        /// registered from there. AddFontFile is the dependable path -- an
        /// in-memory font can register successfully and still fail to
        /// rasterise -- with AddMemoryFont kept as the fallback for the case
        /// where the data folder is not writable. Failures are contained per
        /// step so a problem with one face cannot drop the others.
        /// </summary>
        static FontFamily Add(string fileName, byte[] data)
        {
            if (data == null || data.Length == 0) return null;
            if (ForceSystemFonts) return null;

            int before = _private.Families.Length;
            string path = Extract(fileName, data);
            bool added = false;

            if (path != null)
            {
                try { _private.AddFontFile(path); added = true; }
                catch (Exception ex) { Logger.LogError("NocturneFonts.AddFontFile", ex.Message, ex.StackTrace); }
            }

            if (!added && !AddFromMemory(data)) return null;

            // Also hand it to GDI so TextRenderer-based controls can see it.
            // Purely a bonus; a failure here must not lose the face.
            try
            {
                if (path != null)
                {
                    uint installed = 0;
                    AddFontResourceEx(path, FR_PRIVATE, IntPtr.Zero);
                    GC.KeepAlive(installed);
                }
            }
            catch (DllNotFoundException) { }          // not Windows
            catch (EntryPointNotFoundException) { }
            catch (Exception ex) { Logger.LogError("NocturneFonts.AddFontResourceEx", ex.Message, ex.StackTrace); }

            FontFamily[] after = _private.Families;
            // Nothing new means this face merged into a family already present.
            return after.Length > before ? after[after.Length - 1] : null;
        }

        static bool AddFromMemory(byte[] data)
        {
            try
            {
                IntPtr p = Marshal.AllocCoTaskMem(data.Length);
                Marshal.Copy(data, 0, p, data.Length);
                _pinned.Add(p);                        // must outlive the fonts
                _private.AddMemoryFont(p, data.Length);
                return true;
            }
            catch (Exception ex)
            {
                Logger.LogError("NocturneFonts.AddMemoryFont", ex.Message, ex.StackTrace);
                return false;
            }
        }

        /// <summary>Writes a bundled face to the data folder, once. Null if that is not possible.</summary>
        static string Extract(string fileName, byte[] data)
        {
            try
            {
                System.IO.Directory.CreateDirectory(FontFolder);
                string path = System.IO.Path.Combine(FontFolder, fileName);
                System.IO.FileInfo existing = new System.IO.FileInfo(path);
                if (!existing.Exists || existing.Length != data.Length)
                    System.IO.File.WriteAllBytes(path, data);
                return path;
            }
            catch (Exception ex)
            {
                Logger.LogError("NocturneFonts.Extract", ex.Message, ex.StackTrace);
                return null;
            }
        }

        static Font Make(FontFamily bundled, string[] fallback, float size, FontStyle style)
        {
            if (bundled != null)
            {
                try { return new Font(bundled, size, style, GraphicsUnit.Point); }
                catch (ArgumentException) { /* family lacks that style -- fall through */ }
            }
            foreach (string name in fallback)
            {
                try
                {
                    Font f = new Font(name, size, style, GraphicsUnit.Point);
                    if (f.Name.Equals(name, StringComparison.OrdinalIgnoreCase)) return f;
                    f.Dispose();
                }
                catch (ArgumentException) { }
            }
            return new Font(System.Drawing.SystemFonts.MessageBoxFont.FontFamily, size, style, GraphicsUnit.Point);
        }

        /// <summary>Body / control text (weight 400).</summary>
        internal static Font Sans(float pt)
        {
            string[] script = ScriptFallback();
            if (script != null) return Make(null, script, pt, FontStyle.Regular);
            return Make(_sans, SansFallback, pt, FontStyle.Regular);
        }

        /// <summary>Titles and emphasis (weight 500) -- never heavier.</summary>
        internal static Font Medium(float pt)
        {
            // A script face has one weight here; asking for Medium would only
            // synthesise bold, which the type rules forbid.
            string[] script = ScriptFallback();
            if (script != null) return Make(null, script, pt, FontStyle.Regular);

            if (_sansMedium != null && _sansMedium != _sans) return Make(_sansMedium, SansFallback, pt, FontStyle.Regular);
            // No dedicated Medium face: synthesising bold would break the
            // "never above 500" rule, so stay at Regular.
            return Make(_sans, SansFallback, pt, FontStyle.Regular);
        }

        internal static Font Mono(float pt) { return Make(_mono, MonoFallback, pt, FontStyle.Regular); }

        /// <summary>Body text that must render <paramref name="code"/>'s script.</summary>
        internal static Font SansFor(LanguageCode code, float pt)
        {
            string[] script = ScriptFallback(code);
            if (script != null) return Make(null, script, pt, FontStyle.Regular);
            return Make(_sans, SansFallback, pt, FontStyle.Regular);
        }

        // ── The type ramp from the handoff, in points (px * 0.75) ───────
        // These are methods, not properties: each call allocates a Font that
        // the caller owns and disposes, which a property would hide.
        internal static Font ScreenTitle()    { return Medium(15.75f); }  // 21px / 500
        internal static Font ScreenSubtitle() { return Sans(9.75f); }     // 13px
        internal static Font SectionLabel()   { return Medium(8.25f); }   // 11px uppercase
        internal static Font Row()            { return Sans(10.125f); }   // 13.5px
        internal static Font RowMedium()      { return Medium(10.125f); }
        internal static Font Tip()            { return Sans(9f); }        // 12px
        internal static Font Meta()           { return Sans(9.375f); }    // 12.5px
        internal static Font Small()          { return Sans(7.875f); }    // 10.5px
        internal static Font Tag()            { return Medium(7.5f); }    // 10px
        internal static Font Nav()            { return Sans(10.125f); }   // 13.5px
        internal static Font Brand()          { return Medium(10.5f); }   // 14px
        internal static Font Chrome()         { return Sans(9f); }        // 12px title-bar meta
        internal static Font Big()            { return Medium(15f); }     // 20px totals
        internal static Font Code()           { return Mono(9f); }        // 12px
        internal static Font CodeSmall()      { return Mono(8.625f); }    // 11.5px
        internal static Font TableHeader()    { return Medium(8.25f); }   // 11px uppercase
    }
}
