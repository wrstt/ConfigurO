using System;

namespace ConfigurO
{
    /// <summary>The six groups the Tweaks screen renders, in display order.</summary>
    internal enum TweakGroup
    {
        Performance,
        Privacy,
        UpdatesDefender,
        Gaming,
        Interface,
        System
    }

    /// <summary>
    /// One row on the Tweaks screen.
    ///
    /// Everything the app needs to know about a tweak lives here: how to read
    /// and write its remembered state, how to apply and revert it, whether it
    /// needs a reboot or a confirmation, and which Windows builds it applies
    /// to. The screen, the silent-config runner and policy reinforcement all
    /// read the same table, so a tweak is added in exactly one place.
    /// </summary>
    internal sealed class TweakDef
    {
        /// <summary>Stable identifier; matches the design prototype where one exists.</summary>
        internal string Id;

        internal TweakGroup Group;

        /// <summary>
        /// i18n key for the row label. These are the legacy control names,
        /// which is what the translation files are keyed on.
        /// </summary>
        internal string LabelKey;

        /// <summary>i18n key for the tip line under the label.</summary>
        internal string TipKey;

        /// <summary>English text used when the translation files have no entry (new tweaks).</summary>
        internal string Label;
        internal string Tip;

        internal Func<Options, bool> Get;
        internal Action<Options, bool> Set;

        /// <summary>Turns the tweak on.</summary>
        internal Action Apply;

        /// <summary>Restores the Windows default.</summary>
        internal Action Revert;

        /// <summary>Surfaces the "restart to finish applying" banner.</summary>
        internal bool RestartRequired;

        /// <summary>
        /// i18n key for a confirmation prompt shown before switching on.
        /// Reserved for genuinely destructive tweaks (Defender, System
        /// Restore, uninstalling OneDrive).
        /// </summary>
        internal string ConfirmKey;

        /// <summary>Runs off the UI thread; the row shows no progress but stays responsive.</summary>
        internal bool RunAsync;

        /// <summary>Minimum Windows build, or 0 for "any supported version".</summary>
        internal int MinBuild;

        /// <summary>Windows 11 only.</summary>
        internal bool RequiresWindows11;

        /// <summary>
        /// Reads this tweak out of a silent-config file. Only set for tweaks
        /// that <see cref="SilentOps"/> does not already handle by hand.
        /// </summary>
        internal Func<Tweaks, bool?> SilentGet;

        /// <summary>Whether this tweak can run on the machine we are on.</summary>
        internal bool IsAvailable
        {
            get
            {
                if (RequiresWindows11 && !WindowsRelease.IsWindows11) return false;
                if (MinBuild > 0 && !WindowsRelease.IsAtLeastBuild(MinBuild)) return false;
                return true;
            }
        }

        internal string ResolvedLabel { get { return I18n.Get(LabelKey, Label); } }

        /// <summary>
        /// i18n key for the row's one-line summary.
        ///
        /// Deliberately a new namespace rather than a reuse of TipKey. The
        /// translation files key TipKey to Optimizer's long-form help -- several
        /// sentences, often with a bulleted list and hard newlines, written for a
        /// dialog that had room for it. A Nocturne row has one line, so that text
        /// arrives flattened and then ellipsised: 72% of the tips are longer than
        /// the summary written for this UI, and a third carry hard breaks. That is
        /// what made the screen read as clutter rather than as a product.
        ///
        /// No file defines these keys yet, so every language falls back to the
        /// English one-liner and reads correctly today, and a translator can add
        /// one later without touching the legacy key or losing the long-form text.
        /// </summary>
        internal string SummaryKey { get { return TipKey + "Short"; } }

        /// <summary>The one-line summary drawn in the row.</summary>
        internal string ResolvedSummary { get { return I18n.Get(SummaryKey, Tip); } }

        /// <summary>
        /// The long-form help, shown on hover. This is the already-translated
        /// legacy text; hard newlines in it are wanted here, where there is room.
        /// </summary>
        internal string ResolvedDetail { get { return I18n.Get(TipKey, Tip); } }

        /// <summary>Detail worth showing separately, i.e. not just the summary again.</summary>
        internal bool HasDetail
        {
            get
            {
                string d = ResolvedDetail;
                return !string.IsNullOrEmpty(d) && d != ResolvedSummary;
            }
        }
    }
}
