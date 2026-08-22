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
        internal string ResolvedTip { get { return I18n.Get(TipKey, Tip); } }
    }
}
