using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace ConfigurO
{
    /// <summary>
    /// Hardware: one card per subsystem, each an accent icon and title over
    /// key/value rows separated by hairlines.
    ///
    /// The WMI queries in <see cref="IndiciumHelper"/> are slow enough to
    /// stall the window, so the whole sweep runs on a worker thread and the
    /// cards fill in when it returns.
    /// </summary>
    internal sealed class HardwareScreen : NScreen
    {
        internal const string ScreenId = "hardware";

        sealed class Section
        {
            internal string Title;
            internal string Icon;
            internal readonly List<KeyValuePair<string, string>> Lines =
                new List<KeyValuePair<string, string>>();
        }

        readonly List<Section> _sections = new List<Section>();
        readonly List<NCard> _cards = new List<NCard>();
        readonly NButton _copy = new NButton();
        readonly NButton _save = new NButton();
        readonly NButton _refresh = new NButton();
        ContextMenuStrip _lineMenu;
        bool _loading;

        internal override string Id { get { return ScreenId; } }
        internal override string Icon { get { return NocturneIcons.Hardware; } }
        internal override string NavLabel { get { return I18n.Get("navHardware", "Hardware"); } }

        protected override void Build()
        {
            TitleText = I18n.Get("hardwareTitle", "Hardware");
            SubtitleText = I18n.Get("hardwareLoading", "Reading system information…");

            _copy.Style = NButtonStyle.Secondary;
            _copy.Text = I18n.Get("btnCopyHW", "Copy");
            _copy.Icon = NocturneIcons.Copy;
            _copy.Click += (s, e) => CopyReport();
            AddAction(_copy);

            _save.Style = NButtonStyle.Ghost;
            _save.Text = I18n.Get("btnSaveHW", "Save");
            _save.Icon = NocturneIcons.Save;
            _save.Click += (s, e) => SaveReport();
            AddAction(_save);

            _refresh.Style = NButtonStyle.Icon;
            _refresh.Icon = NocturneIcons.Refresh;
            _refresh.Click += (s, e) => { _sections.Clear(); Load(); };
            AddAction(_refresh);
        }

        internal override void Activate()
        {
            if (_sections.Count == 0) Load();
        }

        void Load()
        {
            if (_loading) return;
            _loading = true;
            SetEmpty(I18n.Get("hardwareLoading", "Reading system information…"), NocturneIcons.Hardware);

            Task.Run(() => Collect()).ContinueWith(t =>
            {
                List<Section> sections = t.Status == TaskStatus.RanToCompletion ? t.Result : new List<Section>();
                OnUi(() => Populate(sections));
            });
        }

        static List<Section> Collect()
        {
            List<Section> all = new List<Section>();
            try
            {
                all.Add(Processor());
                all.Add(Memory());
                all.Add(Graphics());
                all.Add(Storage());
                all.Add(Motherboard());
                all.Add(Network());
                all.Add(OperatingSystem());
            }
            catch (Exception ex)
            {
                Logger.LogError("HardwareScreen.Collect", ex.Message, ex.StackTrace);
            }
            return all.Where(s => s != null && s.Lines.Count > 0).ToList();
        }

        static Section Processor()
        {
            Section s = new Section { Title = I18n.Get("hwProcessor", "Processor"), Icon = NocturneIcons.Hardware };
            foreach (CPU c in IndiciumHelper.GetCPUs())
            {
                s.Lines.Add(new KeyValuePair<string, string>(I18n.Get("hwModel", "Model"), c.Name));
                s.Lines.Add(new KeyValuePair<string, string>(I18n.Get("hwCores", "Cores / Threads"),
                    string.Format("{0} / {1}", c.Cores, c.LogicalCpus)));
                s.Lines.Add(new KeyValuePair<string, string>(I18n.Get("hwL3", "L3 cache"), c.L3CacheSize.ToString()));
                s.Lines.Add(new KeyValuePair<string, string>(I18n.Get("hwVirtualization", "Virtualization"), c.Virtualization));
            }
            return s;
        }

        static Section Memory()
        {
            Section s = new Section { Title = I18n.Get("hwMemory", "Memory"), Icon = "ram-line" };
            List<RAM> modules = IndiciumHelper.GetRAM();
            if (modules.Count > 0)
            {
                ByteSize total = new ByteSize(0);
                foreach (RAM m in modules) total += m.Capacity;
                s.Lines.Add(new KeyValuePair<string, string>(I18n.Get("hwInstalled", "Installed"),
                    string.Format("{0} {1}", total.ToString("GB"), modules[0].MemoryType)));
                s.Lines.Add(new KeyValuePair<string, string>(I18n.Get("hwSpeed", "Speed"), modules[0].Speed + " MT/s"));
                s.Lines.Add(new KeyValuePair<string, string>(I18n.Get("hwSlots", "Modules"), modules.Count.ToString()));
                s.Lines.Add(new KeyValuePair<string, string>(I18n.Get("hwFormFactor", "Form factor"), modules[0].FormFactor));
            }
            VirtualMemory vm = IndiciumHelper.GetVM();
            if (vm.TotalVirtualMemory.Bytes > 0)
                s.Lines.Add(new KeyValuePair<string, string>(I18n.Get("hwVirtual", "Virtual memory"),
                    string.Format("{0} / {1}", vm.UsedVirtualMemory.ToString("GB"), vm.TotalVirtualMemory.ToString("GB"))));
            return s;
        }

        static Section Graphics()
        {
            Section s = new Section { Title = I18n.Get("hwGraphics", "Graphics"), Icon = "tv-2-line" };
            foreach (GPU g in IndiciumHelper.GetGPUs())
            {
                s.Lines.Add(new KeyValuePair<string, string>("GPU", g.Name));
                s.Lines.Add(new KeyValuePair<string, string>("VRAM", g.Memory.ToString("GB")));
                s.Lines.Add(new KeyValuePair<string, string>(I18n.Get("hwResolution", "Resolution"),
                    string.Format("{0} × {1} @ {2} Hz", g.ResolutionX, g.ResolutionY, g.RefreshRate)));
            }
            return s;
        }

        static Section Storage()
        {
            Section s = new Section { Title = I18n.Get("hwStorage", "Storage"), Icon = "hard-drive-2-line" };
            foreach (Disk d in IndiciumHelper.GetDisks())
                s.Lines.Add(new KeyValuePair<string, string>(d.MediaType ?? "Disk",
                    string.Format("{0} — {1}", d.Model, d.Capacity.ToString("GB"))));

            IndiciumHelper.GetVolumes();
            foreach (Volume v in IndiciumHelper.Volumes)
                s.Lines.Add(new KeyValuePair<string, string>(v.DriveLetter,
                    string.Format("{0} {1} of {2}", v.FreeSpace.ToString("GB"),
                                  I18n.Get("hwFree", "free"), v.Capacity.ToString("GB"))));
            return s;
        }

        static Section Motherboard()
        {
            Section s = new Section { Title = I18n.Get("hwMotherboard", "Motherboard"), Icon = "server-line" };
            foreach (Motherboard m in IndiciumHelper.GetMotherboards())
            {
                s.Lines.Add(new KeyValuePair<string, string>(I18n.Get("hwBoard", "Board"),
                    string.Format("{0} {1}", m.Manufacturer, m.Model)));
                s.Lines.Add(new KeyValuePair<string, string>("BIOS",
                    string.Format("{0} ({1})", m.BIOSVersion, m.BIOSName)));
                s.Lines.Add(new KeyValuePair<string, string>(I18n.Get("hwChipset", "Chipset"), m.Chipset));
            }
            return s;
        }

        static Section Network()
        {
            Section s = new Section { Title = I18n.Get("hwNetwork", "Network"), Icon = NocturneIcons.Network };
            IndiciumHelper.GetNetworkAdapters();
            foreach (NetworkDevice n in IndiciumHelper.PhysicalAdapters)
            {
                s.Lines.Add(new KeyValuePair<string, string>(I18n.Get("hwAdapter", "Adapter"), n.ProductName));
                s.Lines.Add(new KeyValuePair<string, string>("MAC", n.MacAddress));
            }
            return s;
        }

        static Section OperatingSystem()
        {
            Section s = new Section { Title = I18n.Get("hwOS", "Operating system"), Icon = NocturneIcons.Windows };
            s.Lines.Add(new KeyValuePair<string, string>(I18n.Get("hwEdition", "Edition"), WindowsRelease.ProductName));
            s.Lines.Add(new KeyValuePair<string, string>(I18n.Get("hwRelease", "Release"),
                string.IsNullOrEmpty(WindowsRelease.DisplayVersion) ? "—" : WindowsRelease.DisplayVersion));
            s.Lines.Add(new KeyValuePair<string, string>(I18n.Get("hwBuild", "Build"), WindowsRelease.FullBuild()));
            s.Lines.Add(new KeyValuePair<string, string>(I18n.Get("hwArch", "Architecture"), WindowsRelease.Architecture));
            s.Lines.Add(new KeyValuePair<string, string>(".NET", Utilities.GetNETFramework()));
            return s;
        }

        void Populate(List<Section> sections)
        {
            _loading = false;
            _sections.Clear();
            _sections.AddRange(sections);

            foreach (NCard c in _cards) { Body.Controls.Remove(c); c.Dispose(); }
            _cards.Clear();

            foreach (Section s in _sections)
            {
                NCard card = new NCard { Title = s.Title, Icon = s.Icon, Tag = s };
                card.Body.Paint += PaintLines;
                card.Body.MouseDown += LineMouseDown;
                _cards.Add(card);
                Body.Controls.Add(card);
            }

            SetEmpty(_sections.Count == 0
                ? I18n.Get("hardwareEmpty", "No hardware information could be read.")
                : null, NocturneIcons.Warning);

            _copy.Enabled = _save.Enabled = _sections.Count > 0;
            SubtitleText = WindowsRelease.ChromeSummary();
            RefreshHeader();
            PerformLayout();
        }

        void PaintLines(object sender, PaintEventArgs e)
        {
            NPanel body = (NPanel)sender;
            Section s = (Section)((NCard)body.Parent).Tag;

            Graphics g = e.Graphics;
            NocturneDraw.Prepare(g);

            int rowH = NocturneScale.S(24);
            int keyW = NocturneScale.S(120);

            using (Font f = NocturneFonts.Meta())
            {
                for (int i = 0; i < s.Lines.Count; i++)
                {
                    int y = i * rowH;
                    NocturneDraw.Text(g, s.Lines[i].Key, f, NocturneTheme.TextFaint,
                        new RectangleF(0, y, keyW, rowH), NocturneDraw.Left);
                    NocturneDraw.Text(g, s.Lines[i].Value, f, NocturneTheme.Text,
                        new RectangleF(keyW, y, Math.Max(0, body.Width - keyW), rowH), NocturneDraw.Left);

                    if (i < s.Lines.Count - 1)
                        NocturneTheme.DrawFadedRule(g, 0, y + rowH - 1, body.Width, NocturneTheme.Border);
                }
            }
        }

        /// <summary>
        /// Right-clicking a value offers to copy it or look it up -- the
        /// legacy tool's hardware context menu, kept because searching an
        /// unfamiliar device string is the common next step.
        /// </summary>
        void LineMouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Right) return;

            NPanel body = (NPanel)sender;
            Section section = (Section)((NCard)body.Parent).Tag;
            int index = e.Y / NocturneScale.S(24);
            if (index < 0 || index >= section.Lines.Count) return;

            KeyValuePair<string, string> line = section.Lines[index];
            string value = line.Value ?? string.Empty;
            if (string.IsNullOrWhiteSpace(value)) return;

            if (_lineMenu != null) _lineMenu.Dispose();
            _lineMenu = NocturneMenu.Create();
            _lineMenu.Add(I18n.Get("toolHWCopy", "Copy"), () => CopyText(value));
            _lineMenu.Add(I18n.Get("toolHWGoogle", "Search with Google"), () => Utilities.SearchWith(value, false));
            _lineMenu.Add(I18n.Get("toolHWDuck", "Search with DuckDuckGo"), () => Utilities.SearchWith(value, true));
            _lineMenu.Show(Cursor.Position);
        }

        void CopyText(string text)
        {
            try
            {
                Clipboard.SetText(text);
                Toast(I18n.Get("hwLineCopied", "Copied"));
            }
            catch (Exception ex)
            {
                Logger.LogError("HardwareScreen.CopyText", ex.Message, ex.StackTrace);
                Toast(I18n.Get("copyFailed", "Could not copy to the clipboard"));
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && _lineMenu != null) _lineMenu.Dispose();
            base.Dispose(disposing);
        }

        string Report()
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("ConfigurO " + Program.GetCurrentVersionTostring() + " — " + I18n.Get("hardwareTitle", "Hardware"));
            sb.AppendLine(WindowsRelease.ChromeSummary());
            sb.AppendLine();
            foreach (Section s in _sections)
            {
                sb.AppendLine("[" + s.Title + "]");
                foreach (KeyValuePair<string, string> l in s.Lines)
                    sb.AppendLine("  " + l.Key + ": " + l.Value);
                sb.AppendLine();
            }
            return sb.ToString();
        }

        void CopyReport()
        {
            if (_sections.Count == 0) return;
            try
            {
                Clipboard.SetText(Report());
                Toast(I18n.Get("hwCopied", "Report copied to the clipboard"));
            }
            catch (Exception ex)
            {
                Logger.LogError("HardwareScreen.Copy", ex.Message, ex.StackTrace);
                Toast(I18n.Get("hwCopyFailed", "Could not copy the report"));
            }
        }

        void SaveReport()
        {
            if (_sections.Count == 0) return;
            using (SaveFileDialog d = new SaveFileDialog())
            {
                d.Filter = "Text file|*.txt";
                d.FileName = "ConfigurO-hardware.txt";
                if (d.ShowDialog(FindForm()) != DialogResult.OK) return;
                try
                {
                    System.IO.File.WriteAllText(d.FileName, Report());
                    Toast(I18n.Get("hwSaved", "Report saved"));
                }
                catch (Exception ex)
                {
                    Logger.LogError("HardwareScreen.Save", ex.Message, ex.StackTrace);
                    Toast(I18n.Get("hwSaveFailed", "Could not save the report"));
                }
            }
        }

        protected override void Relayout()
        {
            int w = Math.Max(0, Width - Pad * 2);
            int gap = NocturneScale.S(14);
            int cols = w > NocturneScale.S(760) ? 2 : 1;
            int colW = (w - gap * (cols - 1)) / cols;

            int[] columnY = new int[cols];
            for (int i = 0; i < _cards.Count; i++)
            {
                Section s = (Section)_cards[i].Tag;
                int h = NocturneScale.S(34) + s.Lines.Count * NocturneScale.S(24) + NocturneScale.S(24);

                // Shortest-column packing keeps the two columns even when the
                // subsystems have very different line counts.
                int col = 0;
                for (int c = 1; c < cols; c++) if (columnY[c] < columnY[col]) col = c;

                _cards[i].SetBounds(Pad + col * (colW + gap), columnY[col], colW, h);
                columnY[col] += h + gap;
            }

            Body.Height = columnY.Max() + NocturneScale.S(20);
        }
    }
}
