using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace ConfigurO
{
    /// <summary>
    /// Cleaner: a two-column grid of selectable cards over a footer that totals
    /// the selection and runs the clean.
    ///
    /// Sizes are measured on entry rather than guessed, so the totals are real;
    /// the scan runs off the UI thread because a cold temp folder takes a while
    /// to walk.
    /// </summary>
    internal sealed class CleanerScreen : NScreen
    {
        internal const string ScreenId = "cleaner";

        readonly List<CleanTarget> _targets = CleanTargets.Build();
        readonly List<NSelectCard> _cards = new List<NSelectCard>();
        readonly NCard _footer = new NCard();
        readonly NButton _clean = new NButton();
        readonly MoonProgress _progress = new MoonProgress();
        readonly NButton _selectAll = new NButton();
        readonly NButton _diskCleanup = new NButton();

        bool _scanning, _cleaning;
        string _resultLine = string.Empty;
        Timer _progressTick;

        internal override string Id { get { return ScreenId; } }
        internal override string Icon { get { return NocturneIcons.Cleaner; } }
        internal override string NavLabel { get { return I18n.Get("navCleaner", "Cleaner"); } }

        protected override void Build()
        {
            TitleText = I18n.Get("cleanerTitle", "Cleaner");
            SubtitleText = I18n.Get("cleanerScanning", "Measuring…");

            _selectAll.Style = NButtonStyle.Ghost;
            _selectAll.Text = I18n.Get("checkSelectAll", "Select all");
            _selectAll.Click += (s, e) => ToggleAll();
            AddAction(_selectAll);

            _diskCleanup.Style = NButtonStyle.Ghost;
            _diskCleanup.Text = I18n.Get("btnWinClean", "Disk Cleanup");
            _diskCleanup.Icon = NocturneIcons.ExternalLink;
            _diskCleanup.Click += (s, e) => LaunchDiskCleanup();
            AddAction(_diskCleanup);

            foreach (CleanTarget t in _targets)
            {
                NSelectCard card = new NSelectCard
                {
                    Text = t.Label,
                    Icon = t.Icon,
                    Meta = "—",
                    Selected = t.Selected,
                    Tag = t
                };
                card.SelectedChanged += (s, e) =>
                {
                    ((CleanTarget)((NSelectCard)s).Tag).Selected = ((NSelectCard)s).Selected;
                    UpdateTotal();
                };
                _cards.Add(card);
                Body.Controls.Add(card);
            }

            _footer.Title = null;
            _footer.CardPadding = new Padding(16, 14, 16, 14);
            Body.Controls.Add(_footer);

            _clean.Style = NButtonStyle.Primary;
            _clean.Text = I18n.Get("cleanDriveB", "Clean");
            _clean.Icon = NocturneIcons.Cleaner;
            _clean.Click += (s, e) => StartClean();
            _footer.Body.Controls.Add(_clean);

            _progress.Visible = false;
            _progress.Maximum = 100;
            _footer.Body.Controls.Add(_progress);
        }

        internal override void Activate()
        {
            if (_scanning || _cleaning) return;
            StartScan();
        }

        void StartScan()
        {
            _scanning = true;
            SubtitleText = I18n.Get("cleanerScanning", "Measuring…");
            RefreshHeader();
            foreach (NSelectCard c in _cards) c.Meta = "—";
            Invalidate(true);

            List<CleanTarget> targets = _targets;
            Task.Run(() => CleanTargets.Scan(targets)).ContinueWith(t =>
            {
                OnUi(ScanFinished);
            });
        }

        void ScanFinished()
        {
            _scanning = false;
            for (int i = 0; i < _cards.Count; i++) _cards[i].Meta = Format(_targets[i].Size);
            SubtitleText = string.Format(I18n.Get("cleanerSubtitle", "{0} across {1} locations"),
                Format(Total(_targets)), _targets.Count);
            RefreshHeader();
            UpdateTotal();
            Invalidate(true);
        }

        static ByteSize Total(IEnumerable<CleanTarget> targets)
        {
            ByteSize total = new ByteSize(0);
            foreach (CleanTarget t in targets) total += t.Size;
            return total;
        }

        static string Format(ByteSize size)
        {
            return size.Bytes <= 0 ? "0 MB" : size.ToString();
        }

        void UpdateTotal()
        {
            int selected = _cards.Count(c => c.Selected);
            _selectAll.Text = selected == _cards.Count && _cards.Count > 0
                ? I18n.Get("checkClearSelection", "Clear selection")
                : I18n.Get("checkSelectAll", "Select all");
            _selectAll.AutoFit();
            _footer.Invalidate();
            PerformLayout();
        }

        void ToggleAll()
        {
            bool select = _cards.Count(c => c.Selected) < _cards.Count;
            foreach (NSelectCard c in _cards) c.Selected = select;
            UpdateTotal();
        }

        /// <summary>Hands off to Windows' own cleanup for the areas we do not touch.</summary>
        void LaunchDiskCleanup()
        {
            try { System.Diagnostics.Process.Start("cleanmgr.exe"); }
            catch (Exception ex)
            {
                Logger.LogError("CleanerScreen.DiskCleanup", ex.Message, ex.StackTrace);
                Toast(I18n.Get("cleanmgrFailed", "Could not start Disk Cleanup"));
            }
        }

        void StartClean()
        {
            if (_cleaning || _scanning) return;

            List<CleanTarget> selected = _targets.Where(t => t.Selected).ToList();
            if (selected.Count == 0)
            {
                Toast(I18n.Get("cleanNothing", "Nothing selected to clean"));
                return;
            }

            _cleaning = true;
            _resultLine = string.Empty;
            _clean.Text = I18n.Get("btnCleaning", "Cleaning…");
            _clean.Enabled = false;
            _progress.Value = 0;
            _progress.Visible = true;
            PerformLayout();

            Task.Run(() => CleanTargets.Clean(selected)).ContinueWith(t =>
            {
                ByteSize freed = t.Status == TaskStatus.RanToCompletion ? t.Result : new ByteSize(0);
                OnUi(() => CleanFinished(freed));
            });

            // The delete pass gives no progress signal, so the bar reports
            // elapsed work rather than pretending to know the file count.
            StopProgressTick();
            _progressTick = new Timer { Interval = 90 };
            _progressTick.Tick += (s, e) =>
            {
                if (!_cleaning || IsDisposed || _progress.IsDisposed) { StopProgressTick(); return; }
                _progress.Value = Math.Min(95, _progress.Value + 4);
            };
            _progressTick.Start();
        }

        void StopProgressTick()
        {
            if (_progressTick == null) return;
            _progressTick.Stop();
            _progressTick.Dispose();
            _progressTick = null;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) StopProgressTick();
            base.Dispose(disposing);
        }

        void CleanFinished(ByteSize freed)
        {
            _cleaning = false;
            StopProgressTick();
            _progress.Value = 100;
            _clean.Text = I18n.Get("btnClean", "Clean");
            _clean.Enabled = true;

            foreach (NSelectCard c in _cards) c.Selected = false;
            foreach (CleanTarget t in _targets) { t.Selected = false; t.Size = new ByteSize(0); t.Files.Clear(); }
            foreach (NSelectCard c in _cards) c.Meta = "0 MB";

            _resultLine = string.Format(I18n.Get("cleanFreed", "{0} freed successfully"), Format(freed));
            Toast(string.Format(I18n.Get("cleanToast", "{0} freed"), Format(freed)));

            _progress.Visible = false;
            UpdateTotal();
            PerformLayout();
        }

        protected override void Relayout()
        {
            int w = Math.Max(0, Width - Pad * 2);
            int gap = NocturneScale.S(10);
            int colW = (w - gap) / 2;
            int cardH = NocturneScale.S(52);

            for (int i = 0; i < _cards.Count; i++)
            {
                int row = i / 2, col = i % 2;
                _cards[i].SetBounds(Pad + col * (colW + gap), row * (cardH + gap), colW, cardH);
            }

            int rows = (_cards.Count + 1) / 2;
            int y = rows * (cardH + gap) + NocturneScale.S(8);

            int footerH = NocturneScale.S(84);
            _footer.SetBounds(Pad, y, w, footerH);

            int bw = Math.Max(NocturneScale.S(120), _clean.Width);
            _clean.SetBounds(_footer.Body.Width - bw, (_footer.Body.Height - NocturneScale.S(34)) / 2,
                             bw, NocturneScale.S(34));
            _progress.SetBounds(0, _footer.Body.Height - NocturneScale.S(6),
                                Math.Max(0, _footer.Body.Width - bw - NocturneScale.S(20)), NocturneScale.S(4));

            Body.Height = y + footerH + NocturneScale.S(20);

            _footer.Paint -= PaintFooter;
            _footer.Paint += PaintFooter;
        }

        void PaintFooter(object sender, PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            NocturneDraw.Prepare(g);

            Padding p = NocturneScale.S(_footer.CardPadding);
            ByteSize selected = Total(_targets.Where(t => t.Selected));

            using (Font f = NocturneFonts.ScreenSubtitle())
                NocturneDraw.Text(g, I18n.Get("lblPretext", "Maximum size to be freed"), f,
                    NocturneTheme.TextMuted,
                    new RectangleF(p.Left, p.Top, _footer.Width - p.Horizontal, NocturneScale.S(18)),
                    NocturneDraw.Left);

            using (Font f = NocturneFonts.Big())
                NocturneDraw.Text(g, Format(selected), f, NocturneTheme.AccentStrong,
                    new RectangleF(p.Left, p.Top + NocturneScale.S(20),
                                   _footer.Width - p.Horizontal, NocturneScale.S(26)),
                    NocturneDraw.Left);

            if (string.IsNullOrEmpty(_resultLine)) return;
            using (Font f = NocturneFonts.Tip())
                NocturneDraw.Text(g, _resultLine, f, NocturneTheme.AccentText,
                    new RectangleF(p.Left, _footer.Height - p.Bottom - NocturneScale.S(16),
                                   _footer.Width - p.Horizontal, NocturneScale.S(16)),
                    NocturneDraw.Left);
        }
    }
}
