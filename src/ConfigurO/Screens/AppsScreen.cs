using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace ConfigurO
{
    /// <summary>
    /// Apps: a wrapping row of category pills over a four-column grid of
    /// selectable tiles, with a footer that runs the downloads.
    ///
    /// Each selected app downloads on its own task and reports progress into
    /// its own tile, so a slow mirror never blocks the rest. Entries the feed
    /// has no link for stay visible but cannot be selected.
    /// </summary>
    internal sealed class AppsScreen : NScreen
    {
        internal const string ScreenId = "apps";

        readonly List<NButton> _pills = new List<NButton>();
        readonly List<NSelectCard> _tiles = new List<NSelectCard>();
        readonly NButton _refresh = new NButton();
        readonly NCard _footer = new NCard();
        readonly NButton _download = new NButton();
        readonly MoonCheck _autoInstall = new MoonCheck();
        readonly NButton _chooseFolder = new NButton();
        readonly NButton _openFolder = new NButton();

        string _category;
        string _downloadFolder;
        bool _loading, _busy;

        /// <summary>Left edge of the footer's controls; the status line trims to it.</summary>
        int _controlsLeft = int.MaxValue;

        internal override string Id { get { return ScreenId; } }
        internal override string Icon { get { return NocturneIcons.Apps; } }
        internal override string NavLabel { get { return I18n.Get("navApps", "Apps"); } }

        protected override void Build()
        {
            TitleText = I18n.Get("appsTitle", "Apps");
            SubtitleText = I18n.Get("appsLoading", "Loading catalogue…");

            _downloadFolder = ResolveDownloadFolder();

            _refresh.Style = NButtonStyle.Ghost;
            _refresh.Text = I18n.Get("btnGetFeed", "Refresh links");
            _refresh.Icon = NocturneIcons.Refresh;
            _refresh.Click += (s, e) => LoadFeed(true);
            AddAction(_refresh);

            _footer.CardPadding = new Padding(16, 12, 16, 12);
            Body.Controls.Add(_footer);

            _autoInstall.Text = I18n.Get("appsAutoInstall", "Install after downloading");
            _autoInstall.Checked = true;
            _footer.Body.Controls.Add(_autoInstall);

            _download.Style = NButtonStyle.Primary;
            _download.AutoWidth = true;
            _download.Text = I18n.Get("btnDownloadApps", "Download");
            _download.Icon = NocturneIcons.Download;
            _download.Click += (s, e) => StartDownloads();
            _footer.Body.Controls.Add(_download);

            _chooseFolder.Style = NButtonStyle.Icon;
            _chooseFolder.Icon = NocturneIcons.Folder;
            _chooseFolder.Click += (s, e) => ChooseFolder();
            _footer.Body.Controls.Add(_chooseFolder);

            _openFolder.Style = NButtonStyle.Icon;
            _openFolder.Icon = NocturneIcons.ExternalLink;
            _openFolder.Click += (s, e) => OpenFolder();
            _footer.Body.Controls.Add(_openFolder);

            _footer.Paint += PaintFooter;
        }

        /// <summary>
        /// Where downloads go: the remembered folder, else the shell's
        /// Downloads location, else the user profile. The registry lookup can
        /// come back empty on a redirected profile, and an empty path would
        /// send every download to the working directory.
        /// </summary>
        static string ResolveDownloadFolder()
        {
            string saved = OptionsHelper.CurrentOptions.AppsFolder;
            if (!string.IsNullOrWhiteSpace(saved)) return saved;

            string downloads = Utilities.GetUserDownloadsFolder();
            if (!string.IsNullOrWhiteSpace(downloads)) return downloads;

            string profile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            if (!string.IsNullOrWhiteSpace(profile)) return Path.Combine(profile, "Downloads");

            return Path.GetTempPath();
        }

        internal override void Activate()
        {
            if (AppFeed.Loaded) { Rebuild(); return; }
            LoadFeed(false);
        }

        void LoadFeed(bool force)
        {
            if (_loading) return;
            _loading = true;
            SubtitleText = I18n.Get("appsLoading", "Loading catalogue…");
            SetEmpty(I18n.Get("appsLoading", "Loading catalogue…"), NocturneIcons.Apps);
            RefreshHeader();

            Task.Run(() => AppFeed.Load(force)).ContinueWith(t =>
            {
                OnUi(() => { _loading = false; Rebuild(); });
            });
        }

        void Rebuild()
        {
            foreach (NButton b in _pills) { Body.Controls.Remove(b); b.Dispose(); }
            _pills.Clear();

            if (!AppFeed.Loaded)
            {
                SubtitleText = I18n.Get("txtFeedError", "No internet connection, try refreshing links again");
                RefreshHeader();
                BuildTiles();
                return;
            }

            List<string> categories = AppFeed.Categories().ToList();
            if (_category == null || !categories.Contains(_category))
                _category = categories.FirstOrDefault();

            foreach (string c in categories)
            {
                NButton pill = new NButton { Style = NButtonStyle.Pill, Text = c, Tag = c };
                pill.Height = NocturneScale.S(28);
                pill.Active = c == _category;
                pill.Click += (s, e) =>
                {
                    _category = (string)((NButton)s).Tag;
                    foreach (NButton p in _pills) p.Active = (string)p.Tag == _category;
                    BuildTiles();
                    ScrollHost.ScrollToTop();
                };
                _pills.Add(pill);
                Body.Controls.Add(pill);
            }

            SubtitleText = string.Format(I18n.Get("appsCount", "{0} apps across {1} categories"),
                                         AppFeed.Apps.Count, categories.Count);
            RefreshHeader();
            BuildTiles();
        }

        void BuildTiles()
        {
            foreach (NSelectCard c in _tiles) { Body.Controls.Remove(c); c.Dispose(); }
            _tiles.Clear();

            if (_category == null) { PerformLayout(); return; }

            foreach (AppInfo a in AppFeed.InCategory(_category))
            {
                bool available = AppFeed.IsAvailable(a);
                NSelectCard tile = new NSelectCard
                {
                    Kind = NSelectCard.CardLayout.Tile,
                    Text = a.Title,
                    Image = AppFeed.Icon(a),
                    Icon = AppFeed.CategoryIcon(a.Group),
                    ShowCheck = false,
                    Tag = a,
                    Enabled = available,
                    Status = available ? string.Empty : I18n.Get("appsNoLink", "No link yet")
                };
                tile.SelectedChanged += (s, e) => UpdateFooter();
                _tiles.Add(tile);
                Body.Controls.Add(tile);
            }

            SetEmpty(_tiles.Count == 0
                ? (AppFeed.Loaded
                    ? I18n.Get("appsCategoryEmpty", "Nothing in this category.")
                    : I18n.Get("txtFeedError", "No internet connection, try refreshing links again"))
                : null, NocturneIcons.Apps);

            UpdateFooter();
            PerformLayout();
        }

        void UpdateFooter() { _footer.Invalidate(); }

        /// <summary>Where downloads land. Remembered in Options.AppsFolder.</summary>
        void ChooseFolder()
        {
            using (FolderBrowserDialog d = new FolderBrowserDialog())
            {
                d.Description = I18n.Get("appsChooseFolder", "Where should downloads be saved?");
                d.SelectedPath = _downloadFolder;
                if (d.ShowDialog(FindForm()) != DialogResult.OK) return;
                _downloadFolder = d.SelectedPath;
                OptionsHelper.CurrentOptions.AppsFolder = _downloadFolder;
                OptionsHelper.SaveSettings();
                UpdateFooter();
            }
        }

        void OpenFolder()
        {
            try
            {
                if (!Directory.Exists(_downloadFolder)) Directory.CreateDirectory(_downloadFolder);
                Process.Start(_downloadFolder);
            }
            catch (Exception ex)
            {
                Logger.LogError("AppsScreen.OpenFolder", ex.Message, ex.StackTrace);
                Toast(I18n.Get("appsFolderFailed", "Cannot open the download folder"));
            }
        }

        int SelectedCount { get { return _tiles.Count(t => t.Selected); } }

        void StartDownloads()
        {
            if (_busy) return;

            List<NSelectCard> selected = _tiles.Where(t => t.Selected).ToList();
            if (selected.Count == 0)
            {
                Toast(I18n.Get("appsSelectFirst", "Select at least one app first"));
                return;
            }

            if (!Directory.Exists(_downloadFolder))
            {
                try { Directory.CreateDirectory(_downloadFolder); }
                catch (Exception ex)
                {
                    Logger.LogError("AppsScreen.CreateFolder", ex.Message, ex.StackTrace);
                    Toast(I18n.Get("appsFolderFailed", "Cannot write to the download folder"));
                    return;
                }
            }

            _busy = true;
            _download.Enabled = false;
            Toast(string.Format(I18n.Get("appsDownloading", "Downloading {0} app(s)…"), selected.Count));

            bool install = _autoInstall.Checked;
            int remaining = selected.Count;

            foreach (NSelectCard tile in selected)
            {
                AppInfo app = (AppInfo)tile.Tag;
                NSelectCard captured = tile;
                Download(app, captured, install, () =>
                {
                    if (System.Threading.Interlocked.Decrement(ref remaining) > 0) return;
                    OnUi(() =>
                    {
                        _busy = false;
                        _download.Enabled = true;
                        Toast(I18n.Get("appsDone", "Downloads finished"));
                    });
                });
            }
        }

        void Download(AppInfo app, NSelectCard tile, bool install, Action done)
        {
            bool prefer64 = Environment.Is64BitOperatingSystem && !string.IsNullOrEmpty(app.Link64);
            string url = prefer64 ? app.Link64 : app.Link;
            if (string.IsNullOrEmpty(url)) url = app.Link64;
            if (string.IsNullOrEmpty(url)) { done(); return; }

            string extension = url.IndexOf(".msi", StringComparison.OrdinalIgnoreCase) >= 0 ? ".msi" : ".exe";
            string target = Path.Combine(_downloadFolder,
                Utilities.SanitizeFileFolderName(app.Title) + (prefer64 ? "-x64" : "-x86") + extension);

            SetStatus(tile, I18n.Get("appsQueued", "Queued"));

            WebClient client = new WebClient();
            client.Headers.Add("User-Agent: Other");

            client.DownloadProgressChanged += (s, e) =>
            {
                // Servers that omit Content-Length report -1; show a spinner
                // rather than a nonsensical percentage.
                SetStatus(tile, e.TotalBytesToReceive > 0
                    ? e.ProgressPercentage + "%"
                    : ByteSize.FromBytes(e.BytesReceived).ToString("MB"));
            };

            client.DownloadFileCompleted += (s, e) =>
            {
                client.Dispose();
                if (e.Error != null || e.Cancelled)
                {
                    Logger.LogError("AppsScreen.Download:" + app.Title,
                        e.Error != null ? e.Error.Message : "cancelled", string.Empty);
                    SetStatus(tile, I18n.Get("appsFailed", "Failed"));
                    TryDelete(target);
                    done();
                    return;
                }

                SetStatus(tile, install ? I18n.Get("appsInstalling", "Installing…")
                                        : I18n.Get("appsDownloaded", "Downloaded"));
                if (install) Install(target, tile);
                else SetStatus(tile, I18n.Get("appsDownloaded", "Downloaded"));
                done();
            };

            try { client.DownloadFileAsync(new Uri(url), target); }
            catch (Exception ex)
            {
                Logger.LogError("AppsScreen.Start:" + app.Title, ex.Message, ex.StackTrace);
                SetStatus(tile, I18n.Get("appsFailed", "Failed"));
                client.Dispose();
                done();
            }
        }

        void Install(string file, NSelectCard tile)
        {
            try
            {
                ProcessStartInfo psi = file.EndsWith(".msi", StringComparison.OrdinalIgnoreCase)
                    ? new ProcessStartInfo("msiexec", "/i \"" + file + "\"")
                    : new ProcessStartInfo(file);
                psi.UseShellExecute = true;
                Process.Start(psi);
                SetStatus(tile, I18n.Get("appsInstalled", "Installed"));
            }
            catch (Exception ex)
            {
                Logger.LogError("AppsScreen.Install", ex.Message, ex.StackTrace);
                SetStatus(tile, I18n.Get("appsDownloaded", "Downloaded"));
            }
        }

        static void TryDelete(string path)
        {
            try { if (File.Exists(path)) File.Delete(path); }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }

        void SetStatus(NSelectCard tile, string status)
        {
            if (IsDisposed || tile.IsDisposed) return;
            if (InvokeRequired) { OnUi(() => SetStatus(tile, status)); return; }
            tile.Status = status;
            tile.Invalidate();
        }

        void PaintFooter(object sender, PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            NocturneDraw.Prepare(g);
            Padding p = NocturneScale.S(_footer.CardPadding);

            string line = string.Format(I18n.Get("appsFooter", "{0} selected · saved to {1}"),
                                        SelectedCount, _downloadFolder);
            // Trimmed against where the controls actually start rather than a
            // fixed reservation: the checkbox is as wide as its translation.
            float right = Math.Min(_controlsLeft, _footer.Body.Width);
            using (Font f = NocturneFonts.Meta())
                NocturneDraw.Text(g, line, f, NocturneTheme.TextMuted,
                    new RectangleF(p.Left, 0, Math.Max(0, right - NocturneScale.S(12)),
                                   _footer.Height), NocturneDraw.Left);
        }

        protected override void Relayout()
        {
            int w = Math.Max(0, Width - Pad * 2);
            int gap = NocturneScale.S(8);
            int y = 0;

            // ── wrapping category pills ──
            int x = Pad;
            using (Graphics g = NocturneDraw.CreateMeasureGraphics())
            using (Font f = NocturneFonts.Row())
            {
                foreach (NButton pill in _pills)
                {
                    int pw = (int)Math.Ceiling(NocturneDraw.Width(g, pill.Text, f)) + NocturneScale.S(28);
                    if (x + pw > Pad + w) { x = Pad; y += pill.Height + gap; }
                    pill.SetBounds(x, y, pw, NocturneScale.S(28));
                    x += pw + gap;
                }
            }
            if (_pills.Count > 0) y += NocturneScale.S(28) + NocturneScale.S(18);

            // ── four-column tile grid ──
            int cols = Math.Max(2, Math.Min(4, w / NocturneScale.S(180)));
            int tileW = (w - gap * (cols - 1)) / cols;
            int tileH = NocturneScale.S(104);

            for (int i = 0; i < _tiles.Count; i++)
            {
                int row = i / cols, col = i % cols;
                _tiles[i].SetBounds(Pad + col * (tileW + gap), y + row * (tileH + gap), tileW, tileH);
            }
            if (_tiles.Count > 0)
                y += ((_tiles.Count + cols - 1) / cols) * (tileH + gap) + NocturneScale.S(8);

            int footerH = NocturneScale.S(62);
            _footer.SetBounds(Pad, y, w, footerH);

            int bw = Math.Max(NocturneScale.S(130), _download.Width);
            int icon = NocturneScale.S(32);
            int mid = (_footer.Body.Height - NocturneScale.S(34)) / 2;
            _download.SetBounds(_footer.Body.Width - bw, mid, bw, NocturneScale.S(34));
            _autoInstall.AutoFit();
            _autoInstall.SetBounds(_download.Left - NocturneScale.S(10) - _autoInstall.Width,
                                   (_footer.Body.Height - _autoInstall.Height) / 2,
                                   _autoInstall.Width, _autoInstall.Height);
            _openFolder.SetBounds(_autoInstall.Left - icon - NocturneScale.S(8), mid, icon, icon);
            _chooseFolder.SetBounds(_openFolder.Left - icon - NocturneScale.S(4), mid, icon, icon);
            _controlsLeft = _chooseFolder.Left;

            Body.Height = y + footerH + NocturneScale.S(20);
        }
    }
}
