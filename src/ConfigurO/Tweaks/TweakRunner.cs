using System;
using System.Threading.Tasks;

namespace ConfigurO
{
    /// <summary>
    /// Applies tweaks from <see cref="TweakRegistry"/>.
    ///
    /// The Tweaks screen, silent-config runs and policy reinforcement all go
    /// through here, so a tweak behaves identically however it is triggered.
    /// </summary>
    internal static class TweakRunner
    {
        /// <summary>
        /// Raised when a tweak that needs a reboot has been applied, so the
        /// shell can surface its "restart to finish" banner.
        /// </summary>
        internal static event EventHandler RestartNeeded;

        /// <summary>
        /// Applies or reverts <paramref name="def"/> and records the new state.
        /// Long-running tweaks are pushed onto the thread pool so the UI stays
        /// live; failures are logged rather than thrown, matching how the rest
        /// of the tool treats registry writes it may not be allowed to make.
        /// </summary>
        internal static void Set(TweakDef def, bool on, bool persist = true)
        {
            if (def == null) return;

            Action work = on ? def.Apply : def.Revert;
            if (work != null)
            {
                // A silent run exits as soon as the actions are queued, so
                // background work would be killed half-done. Only the
                // interactive UI can afford to hand these to the thread pool.
                if (def.RunAsync && !Program.SILENT_MODE) Task.Run(() => Guard(def, work));
                else Guard(def, work);
            }

            if (persist && def.Set != null) def.Set(OptionsHelper.CurrentOptions, on);

            if (def.RestartRequired)
            {
                EventHandler h = RestartNeeded;
                if (h != null) h(def, EventArgs.Empty);
            }
        }

        static void Guard(TweakDef def, Action work)
        {
            try { work(); }
            catch (Exception ex) { Logger.LogError("TweakRunner:" + def.Id, ex.Message, ex.StackTrace); }
        }

        /// <summary>
        /// Re-applies every switched-on tweak that the registry owns.
        ///
        /// Windows quietly resets a number of these on feature updates, which
        /// is what the "Reinforce policies" button is for. Only the tweaks with
        /// a <see cref="TweakDef.SilentGet"/> binding are handled here -- the
        /// rest are already covered by <see cref="Utilities.ReinforceCurrentTweaks"/>.
        /// </summary>
        internal static int Reinforce()
        {
            int n = 0;
            Options o = OptionsHelper.CurrentOptions;
            foreach (TweakDef def in TweakRegistry.All)
            {
                if (def.SilentGet == null || !def.IsAvailable) continue;
                if (!def.Get(o)) continue;
                Guard(def, def.Apply);
                n++;
            }
            return n;
        }

        /// <summary>
        /// Applies the registry-owned tweaks named in a silent-config file.
        /// Called from <see cref="SilentOps.ProcessAllActions"/> after the
        /// hand-written passes, so it only ever sees tweaks they do not know about.
        /// </summary>
        internal static void ApplySilent(Tweaks config)
        {
            if (config == null) return;
            foreach (TweakDef def in TweakRegistry.All)
            {
                if (def.SilentGet == null) continue;
                bool? v = def.SilentGet(config);
                if (!v.HasValue) continue;
                if (!def.IsAvailable)
                {
                    Logger.LogInfoSilent(string.Format("Tweaks | {0} | skipped, not supported on this build", def.Id));
                    continue;
                }
                Set(def, v.Value);
                Logger.LogInfoSilent(string.Format("Tweaks | {0} | {1}", def.Id, v.Value ? "applied" : "reverted"));
            }
        }
    }
}
