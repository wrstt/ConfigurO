using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace ConfigurO
{
    /// <summary>
    /// Self-update: compares the published version marker with this build,
    /// shows the changelog, and swaps the executable in place.
    ///
    /// Ported out of the legacy MainForm so the Settings screen and the launch
    /// check share one implementation. Version numbers are parsed with the
    /// invariant culture -- the previous code used the ambient one, which threw
    /// on any comma-decimal locale.
    /// </summary>
    internal static class UpdateHelper
    {
        internal const string Repository = "https://github.com/wrstt/ConfigurO";
        const string VersionUrl = "https://raw.githubusercontent.com/wrstt/ConfigurO/main/version.txt";
        const string ChangelogUrl = "https://raw.githubusercontent.com/wrstt/ConfigurO/main/CHANGELOG.md";

        /// <summary>Release asset for a given version tag.</summary>
        internal static string DownloadLink(string version)
        {
            return string.Format("{0}/releases/download/{1}/ConfigurO-{1}.exe", Repository, version);
        }

        static WebClient NewClient()
        {
            return new WebClient { Encoding = Encoding.UTF8 };
        }

        /// <summary>
        /// Checks in the background and calls back on the UI thread. Used for
        /// the silent check at launch so a slow network never blocks startup.
        /// </summary>
        internal static void CheckAsync(Form owner, bool silent)
        {
            Task.Run(() =>
            {
                string latest = FetchLatest();
                if (owner == null || owner.IsDisposed || !owner.IsHandleCreated) return;
                try
                {
                    owner.BeginInvoke(new Action(() => Present(owner, latest, silent)));
                }
                catch (ObjectDisposedException) { /* window closed mid-check */ }
            });
        }

        static string FetchLatest()
        {
            try
            {
                using (WebClient c = NewClient()) return c.DownloadString(VersionUrl).Trim();
            }
            catch (Exception ex)
            {
                Logger.LogError("UpdateHelper.FetchLatest", ex.Message, ex.StackTrace);
                return null;
            }
        }

        /// <summary>Runs the check on the UI thread, reporting failures.</summary>
        internal static void CheckInteractive(Form owner)
        {
            string latest = FetchLatest();
            if (latest == null)
            {
                MessageBox.Show(I18n.Get("updateFailed", "Could not reach the update server."),
                                "ConfigurO", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            Present(owner, latest, false);
        }

        static void Present(Form owner, string latest, bool silent)
        {
            if (string.IsNullOrEmpty(latest)) return;

            float latestVersion;
            if (!float.TryParse(latest, NumberStyles.Float, CultureInfo.InvariantCulture, out latestVersion)) return;

            float current = Program.GetCurrentVersionToFloat();

            if (latestVersion > current)
            {
                if (silent)
                {
                    MainForm shell = owner as MainForm;
                    if (shell != null) shell.Toast(string.Format(
                        I18n.Get("updateAvailable", "Version {0} is available"), latest));
                    return;
                }
                using (UpdateForm f = new UpdateForm(I18n.Get("newVersion", "A new version is available!"),
                                                     true, Changelog(), latest))
                {
                    if (f.ShowDialog(owner) == DialogResult.Yes) Install(owner, latest);
                }
                return;
            }

            if (silent) return;

            string message = latestVersion == current
                ? I18n.Get("noNewVersion", "You already have the latest version!")
                : I18n.Get("betaVersion", "You are using an experimental version!");
            using (UpdateForm f = new UpdateForm(message, false, string.Empty, latest))
                f.ShowDialog(owner);
        }

        /// <summary>
        /// Everything in CHANGELOG.md above this build's own heading, i.e. what
        /// the user would gain by updating.
        /// </summary>
        internal static string Changelog()
        {
            List<string> lines;
            try
            {
                using (WebClient c = NewClient())
                    lines = c.DownloadString(ChangelogUrl).Trim()
                             .Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None).ToList();
            }
            catch (Exception ex)
            {
                Logger.LogError("UpdateHelper.Changelog", ex.Message, ex.StackTrace);
                return string.Empty;
            }

            if (lines.Count == 0) return string.Empty;

            string marker = string.Format("## [{0}]", Program.GetCurrentVersionTostring());
            int cut = lines.FindIndex(l => l.Contains(marker));
            if (cut > 0) lines.RemoveRange(cut, lines.Count - cut);

            return string.Join(Environment.NewLine, lines).Replace("##", "➤");
        }

        /// <summary>
        /// Downloads the new build next to the running one, keeps the old
        /// executable as a backup, then restarts. The single-instance mutex has
        /// to be released first or the restarted process sees itself.
        /// </summary>
        static void Install(Form owner, string version)
        {
            try
            {
                Assembly assembly = Assembly.GetEntryAssembly() ?? Assembly.GetCallingAssembly();
                string folder = Path.GetDirectoryName(assembly.Location);
                string name = Path.GetFileNameWithoutExtension(assembly.Location);
                string ext = Path.GetExtension(assembly.Location);

                string backup = Path.Combine(folder, "ConfigurO_old" + ext);
                string current = Path.Combine(folder, name + ext);
                string temp = Path.Combine(folder, "ConfigurO_tmp" + ext);

                using (WebClient c = NewClient()) c.DownloadFile(DownloadLink(version), temp);

                if (File.Exists(backup)) File.Delete(backup);
                File.Move(current, backup);
                File.Move(temp, current);

                if (Program.MUTEX != null)
                {
                    Program.MUTEX.ReleaseMutex();
                    Program.MUTEX.Dispose();
                    Program.MUTEX = null;
                }

                Application.Restart();
            }
            catch (Exception ex)
            {
                Logger.LogError("UpdateHelper.Install", ex.Message, ex.StackTrace);
                MessageBox.Show(ex.Message, "ConfigurO", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }
}
