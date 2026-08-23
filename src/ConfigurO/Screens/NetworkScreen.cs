using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Net.NetworkInformation;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace ConfigurO
{
    /// <summary>
    /// Network: the pinger on the left with its console output, and a right
    /// rail holding the DNS server picker and the resolver-cache card.
    /// </summary>
    internal sealed class NetworkScreen : NScreen
    {
        internal const string ScreenId = "network";

        sealed class DnsChoice
        {
            internal string Name;
            internal string Display;
            internal string[] V4;
            internal string[] V6;
            public override string ToString() { return Display; }
        }

        readonly NCard _pingerCard = new NCard();
        readonly NTextBox _host = new NTextBox();
        readonly NButton _ping = new NButton();
        readonly NButton _shodan = new NButton();
        readonly NButton _copyIp = new NButton();
        readonly NButton _export = new NButton();
        readonly NConsole _console = new NConsole();
        readonly NButton _openAdapters = new NButton();

        readonly NCard _dnsCard = new NCard();
        readonly MoonSelect _adapters = new MoonSelect();
        readonly MoonSelect _dns = new MoonSelect();
        readonly MoonCheck _allNics = new MoonCheck();
        readonly NButton _setDns = new NButton();

        readonly NCard _cacheCard = new NCard();
        readonly NButton _flush = new NButton();

        readonly List<DnsChoice> _choices = new List<DnsChoice>();
        readonly List<long> _latencies = new List<long>();

        string _currentDns = "—";
        string _cacheState;

        /// <summary>Height of the cache state line that sits above the button.</summary>
        static int StateLineHeight { get { return NocturneScale.S(20); } }
        string _lastAddress;
        bool _pinging;

        internal override string Id { get { return ScreenId; } }
        internal override string Icon { get { return NocturneIcons.Network; } }
        internal override string NavLabel { get { return I18n.Get("navNetwork", "Network"); } }

        protected override void Build()
        {
            TitleText = I18n.Get("networkTitle", "Network");
            SubtitleText = I18n.Get("networkSubtitle", "Reachability and name resolution");
            _cacheState = I18n.Get("dnsCacheUnknown", "Resolver cache active");

            // ── pinger ──
            _pingerCard.Title = I18n.Get("pingerTitle", "Pinger");
            _pingerCard.Icon = NocturneIcons.Network;
            _host.Placeholder = I18n.Get("lblPinger", "IP / Domain name");
            _host.Monospace = true;
            _host.Text = OptionsHelper.CurrentOptions.InternalDNS ?? Constants.INTERNAL_DNS;
            _host.KeyDownInner += (s, e) => { if (e.KeyCode == Keys.Enter) { e.Handled = true; StartPing(); } };
            _pingerCard.Body.Controls.Add(_host);

            _ping.Style = NButtonStyle.Primary;
            _ping.Text = I18n.Get("btnPing", "Ping");
            _ping.Click += (s, e) => StartPing();
            _pingerCard.Body.Controls.Add(_ping);

            _shodan.Style = NButtonStyle.Ghost;
            _shodan.Text = I18n.Get("shodanShort", "Shodan");
            _shodan.Icon = NocturneIcons.ExternalLink;
            _shodan.Enabled = false;
            _shodan.Click += (s, e) => OpenShodan();
            _pingerCard.Body.Controls.Add(_shodan);

            _copyIp.Style = NButtonStyle.Icon;
            _copyIp.Icon = NocturneIcons.Copy;
            _copyIp.Enabled = false;
            _copyIp.Click += (s, e) => CopyAddress();
            _pingerCard.Body.Controls.Add(_copyIp);

            _export.Style = NButtonStyle.Icon;
            _export.Icon = NocturneIcons.Save;
            _export.Enabled = false;
            _export.Click += (s, e) => ExportResults();
            _pingerCard.Body.Controls.Add(_export);

            _console.Set(I18n.Get("pingIdle", "Idle — enter an IP or domain and press Ping."));
            _pingerCard.Body.Controls.Add(_console);
            _pingerCard.Body.Paint += PaintSummary;
            Body.Controls.Add(_pingerCard);

            // ── DNS ──
            _dnsCard.Title = I18n.Get("dnsTitle", "DNS server");
            _dnsCard.Icon = "server-line";
            BuildDnsChoices();
            foreach (DnsChoice c in _choices) _dns.Items.Add(c);
            _dns.SelectedIndex = 0;
            _dnsCard.Body.Controls.Add(_adapters);
            _dnsCard.Body.Controls.Add(_dns);

            _adapters.SelectedIndexChanged += (s, e) => ShowCurrentDns();

            _allNics.Text = I18n.Get("dnsAllAdapters", "Set for all network adapters");
            _dnsCard.Body.Controls.Add(_allNics);

            _setDns.Style = NButtonStyle.Primary;
            _setDns.Text = I18n.Get("btnSetDns", "Set DNS");
            _setDns.Click += (s, e) => ApplyDns();
            _dnsCard.Body.Controls.Add(_setDns);
            _openAdapters.Style = NButtonStyle.Ghost;
            _openAdapters.Text = I18n.Get("btnOpenNetwork", "Network Connections");
            _openAdapters.Icon = NocturneIcons.ExternalLink;
            _openAdapters.Click += (s, e) => OpenAdapters();
            _dnsCard.Body.Controls.Add(_openAdapters);

            _dnsCard.Body.Paint += PaintCurrentDns;
            Body.Controls.Add(_dnsCard);

            // ── cache ──
            _cacheCard.Title = I18n.Get("dnsCacheTitle", "DNS cache");
            _cacheCard.Icon = NocturneIcons.History;
            _flush.Style = NButtonStyle.Secondary;
            // AutoWidth before Text, so the first measurement happens on the
            // resolved string rather than on the English default.
            _flush.AutoWidth = true;
            _flush.Text = I18n.Get("flushCacheB", "Flush");
            _flush.Icon = NocturneIcons.Refresh;
            _flush.Click += (s, e) => Flush();
            _cacheCard.Body.Controls.Add(_flush);
            _cacheCard.Body.Paint += PaintCacheState;
            Body.Controls.Add(_cacheCard);
        }

        void BuildDnsChoices()
        {
            _choices.Clear();
            _choices.Add(new DnsChoice { Name = Constants.AutomaticDNS, Display = Constants.AutomaticDNS + " — " + I18n.Get("dnsDhcp", "DHCP assigned") });
            Add(Constants.CloudflareDNS, PingerHelper.CloudflareDNSv4, PingerHelper.CloudflareDNSv6);
            Add(Constants.Quad9DNS, PingerHelper.Quad9DNSv4, PingerHelper.Quad9DNSv6);
            Add(Constants.GoogleDNS, PingerHelper.GoogleDNSv4, PingerHelper.GoogleDNSv6);
            Add(Constants.OpenDNS, PingerHelper.OpenDNSv4, PingerHelper.OpenDNSv6);
            Add(Constants.AdguardDNS, PingerHelper.AdguardDNSv4, PingerHelper.AdguardDNSv6);
            Add(Constants.CleanBrowsingDNS, PingerHelper.CleanBrowsingDNSv4, PingerHelper.CleanBrowsingDNSv6);
            Add(Constants.CleanBrowsingAdultFilterDNS, PingerHelper.CleanBrowsingAdultDNSv4, PingerHelper.CleanBrowsingAdultDNSv6);
            Add(Constants.AlternateDNS, PingerHelper.AlternateDNSv4, PingerHelper.AlternateDNSv6);
        }

        void Add(string name, string[] v4, string[] v6)
        {
            _choices.Add(new DnsChoice { Name = name, Display = name + " — " + v4[0], V4 = v4, V6 = v6 });
        }

        internal override void Activate()
        {
            PingerHelper.NetworkAdapters = PingerHelper.GetActiveNetworkAdapters();
            _adapters.Items.Clear();
            foreach (NetworkInterface n in PingerHelper.NetworkAdapters) _adapters.Items.Add(n.Name);
            if (_adapters.Items.Count > 0) _adapters.SelectedIndex = 0;
            ShowCurrentDns();
        }

        // ── ping ────────────────────────────────────────────────────────
        void StartPing()
        {
            if (_pinging) return;
            string host = _host.Text.Trim();
            if (string.IsNullOrEmpty(host)) host = Constants.INTERNAL_DNS;

            _pinging = true;
            _ping.Enabled = false;
            _shodan.Enabled = false;
            _latencies.Clear();
            _lastAddress = null;
            _copyIp.Enabled = _export.Enabled = false;
            _console.Set(string.Format(
                I18n.Get("pingStart", "Pinging {0} with 32 bytes of data — 9 times…"), host));
            _pingerCard.Body.Invalidate();

            Task.Run(() =>
            {
                for (int i = 0; i < 9; i++)
                {
                    PingReply reply = PingerHelper.PingHost(host);
                    string line;
                    if (reply == null)
                    {
                        line = string.Format(I18n.Get("hostNotFound", "Host not found: {0}"), host);
                    }
                    else if (reply.Status != IPStatus.Success || reply.Address == null)
                    {
                        line = reply.Status.ToString();
                    }
                    else
                    {
                        _latencies.Add(reply.RoundtripTime);
                        _lastAddress = reply.Address.ToString();
                        int ttl = reply.Options != null ? reply.Options.Ttl : 0;
                        line = string.Format("Reply from {0}: bytes=32 time={1}ms TTL={2}",
                                             reply.Address, reply.RoundtripTime, ttl);
                    }
                    Emit(line);
                    if (reply == null) break;
                    Thread.Sleep(260);
                }
                Finish();
            });
        }

        void Emit(string line)
        {
            OnUi(() => _console.Write(line));
        }

        void Finish()
        {
            OnUi(() =>
            {
                _pinging = false;
                _ping.Enabled = true;
                _shodan.Enabled = _copyIp.Enabled = !string.IsNullOrEmpty(_lastAddress);
                _export.Enabled = _latencies.Count > 0;
                if (_latencies.Count == 0) _console.Write(I18n.Get("timeout", "Request timed out"));
                _pingerCard.Body.Invalidate();
            });
        }

        void CopyAddress()
        {
            if (string.IsNullOrEmpty(_lastAddress)) return;
            try
            {
                Clipboard.SetText(_lastAddress);
                Toast(string.Format(I18n.Get("pingCopied", "{0} copied"), _lastAddress));
            }
            catch (Exception ex)
            {
                Logger.LogError("NetworkScreen.CopyAddress", ex.Message, ex.StackTrace);
                Toast(I18n.Get("copyFailed", "Could not copy to the clipboard"));
            }
        }

        /// <summary>Writes the console transcript and the summary to a text file.</summary>
        void ExportResults()
        {
            if (_latencies.Count == 0) return;
            using (SaveFileDialog d = new SaveFileDialog())
            {
                d.Filter = "Text file|*.txt";
                d.FileName = "ConfigurO-ping.txt";
                if (d.ShowDialog(FindForm()) != DialogResult.OK) return;
                try
                {
                    System.Text.StringBuilder sb = new System.Text.StringBuilder();
                    foreach (string line in _console.Lines) sb.AppendLine(line);
                    sb.AppendLine();
                    sb.AppendLine(string.Format("{0} = {1} ms, {2} = {3} ms, {4} = {5} ms",
                        I18n.Get("min", "Min"), _latencies.Min(),
                        I18n.Get("avg", "Avg"), (int)Math.Round(_latencies.Average()),
                        I18n.Get("max", "Max"), _latencies.Max()));
                    System.IO.File.WriteAllText(d.FileName, sb.ToString());
                    Toast(I18n.Get("pingExported", "Results saved"));
                }
                catch (Exception ex)
                {
                    Logger.LogError("NetworkScreen.Export", ex.Message, ex.StackTrace);
                    Toast(I18n.Get("pingExportFailed", "Could not save the results"));
                }
            }
        }

        void OpenAdapters()
        {
            try { Process.Start("NCPA.cpl"); }
            catch (Exception ex)
            {
                Logger.LogError("NetworkScreen.OpenAdapters", ex.Message, ex.StackTrace);
                Toast(I18n.Get("adaptersFailed", "Could not open Network Connections"));
            }
        }

        void OpenShodan()
        {
            if (string.IsNullOrEmpty(_lastAddress)) return;
            try { Process.Start(string.Format("https://www.shodan.io/host/{0}", _lastAddress)); }
            catch (Exception ex) { Logger.LogError("NetworkScreen.Shodan", ex.Message, ex.StackTrace); }
        }

        void PaintSummary(object sender, PaintEventArgs e)
        {
            if (_latencies.Count == 0) return;
            Graphics g = e.Graphics;
            NocturneDraw.Prepare(g);

            int y = _console.Bottom + NocturneScale.S(10);
            int x = 0;
            x = SummaryTag(g, x, y, I18n.Get("min", "Min"), _latencies.Min() + " ms", false);
            x = SummaryTag(g, x, y, I18n.Get("avg", "Avg"), (int)Math.Round(_latencies.Average()) + " ms", true);
            SummaryTag(g, x, y, I18n.Get("max", "Max"), _latencies.Max() + " ms", false);
        }

        int SummaryTag(Graphics g, int x, int y, string label, string value, bool accent)
        {
            string text = label + " " + value;
            using (Font f = NocturneFonts.Tag())
            {
                int w = (int)Math.Ceiling(NocturneDraw.Width(g, text, f)) + NocturneScale.S(16);
                int h = NocturneScale.S(20);
                Rectangle r = new Rectangle(x, y, w, h);
                NocturneDraw.Card(g, r, accent ? NocturneTheme.TagBg : Color.Empty,
                                  accent ? Color.Empty : NocturneTheme.Border, NocturneTheme.RadiusSm);
                NocturneDraw.Text(g, text, f,
                    accent ? NocturneTheme.AccentStrong : NocturneTheme.TextMuted, r, NocturneDraw.Center);
                return x + w + NocturneScale.S(8);
            }
        }

        // ── DNS ─────────────────────────────────────────────────────────
        void ShowCurrentDns()
        {
            _currentDns = "—";
            try
            {
                if (_adapters.SelectedIndex >= 0 && _adapters.SelectedIndex < PingerHelper.NetworkAdapters.Length)
                {
                    string[] servers = PingerHelper
                        .GetDNSFromNetworkAdapter(PingerHelper.NetworkAdapters[_adapters.SelectedIndex])
                        .ToArray();
                    if (servers.Length > 0) _currentDns = string.Join(", ", servers);
                }
            }
            catch (Exception ex) { Logger.LogError("NetworkScreen.ShowCurrentDns", ex.Message, ex.StackTrace); }
            _dnsCard.Body.Invalidate();
        }

        void ApplyDns()
        {
            DnsChoice choice = _dns.SelectedItem as DnsChoice;
            if (choice == null) return;

            try
            {
                if (choice.V4 == null)
                {
                    if (_allNics.Checked) PingerHelper.ResetDefaultDNSForAllNICs();
                    else if (_adapters.SelectedIndex >= 0)
                        PingerHelper.ResetDefaultDNS(PingerHelper.NetworkAdapters[_adapters.SelectedIndex].Name);
                }
                else if (_allNics.Checked)
                {
                    PingerHelper.SetDNSForAllNICs(choice.V4, choice.V6);
                }
                else if (_adapters.SelectedIndex >= 0)
                {
                    PingerHelper.SetDNS(PingerHelper.NetworkAdapters[_adapters.SelectedIndex].Name,
                                        choice.V4, choice.V6);
                }
                Toast(string.Format(I18n.Get("dnsSet", "DNS set to {0}"), choice.Name));
            }
            catch (Exception ex)
            {
                Logger.LogError("NetworkScreen.ApplyDns", ex.Message, ex.StackTrace);
                Toast(I18n.Get("dnsSetFailed", "Could not change DNS"));
            }
            ShowCurrentDns();
        }

        void PaintCurrentDns(object sender, PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            NocturneDraw.Prepare(g);
            using (Font f = NocturneFonts.CodeSmall())
                NocturneDraw.Text(g, string.Format(I18n.Get("dnsCurrent", "Current: {0}"), _currentDns), f,
                    NocturneTheme.TextFaint,
                    new RectangleF(0, _dnsCard.Body.Height - NocturneScale.S(18),
                                   _dnsCard.Body.Width, NocturneScale.S(16)), NocturneDraw.Left);
        }

        void Flush()
        {
            try
            {
                PingerHelper.FlushDNSCache();
                _cacheState = I18n.Get("dnsCacheEmpty", "Cache empty — flushed just now");
                Toast(I18n.Get("dnsCacheFlushed", "DNS cache flushed"));
            }
            catch (Exception ex)
            {
                Logger.LogError("NetworkScreen.Flush", ex.Message, ex.StackTrace);
                Toast(I18n.Get("dnsFlushFailed", "Could not flush the DNS cache"));
            }
            _cacheCard.Body.Invalidate();
        }

        void PaintCacheState(object sender, PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            NocturneDraw.Prepare(g);
            // Its own row above the button, so it gets the card's full width
            // whatever the button's label costs.
            using (Font f = NocturneFonts.Tip())
                NocturneDraw.Text(g, _cacheState, f, NocturneTheme.TextFaint,
                    new RectangleF(0, 0, _cacheCard.Body.Width, StateLineHeight),
                    NocturneDraw.Left);
        }

        protected override void Relayout()
        {
            int railW = NocturneScale.S(320);
            int gap = NocturneScale.S(16);
            int leftW = Math.Max(NocturneScale.S(280), Width - Pad * 2 - railW - gap);
            int railX = Pad + leftW + gap;

            int fieldH = NocturneScale.S(NocturneTheme.InputHeight);
            int consoleH = NocturneScale.S(240);
            int pingerH = NocturneScale.S(34) + fieldH + NocturneScale.S(10) + consoleH + NocturneScale.S(40) + NocturneScale.S(26);
            _pingerCard.SetBounds(Pad, 0, leftW, pingerH);

            int pingW = NocturneScale.S(90), shodanW = NocturneScale.S(96);
            _host.SetBounds(0, 0, Math.Max(0, _pingerCard.Body.Width - pingW - shodanW - NocturneScale.S(16)), fieldH);
            _ping.SetBounds(_host.Right + NocturneScale.S(8), 0, pingW, fieldH);
            _shodan.SetBounds(_ping.Right + NocturneScale.S(8), 0, shodanW, fieldH);
            _console.SetBounds(0, fieldH + NocturneScale.S(10), _pingerCard.Body.Width, consoleH);
            int iconY = _console.Bottom + NocturneScale.S(8);
            int icon = NocturneScale.S(30);
            _export.SetBounds(_pingerCard.Body.Width - icon, iconY, icon, icon);
            _copyIp.SetBounds(_pingerCard.Body.Width - icon * 2 - NocturneScale.S(6), iconY, icon, icon);

            int y = 0;
            int dnsH = NocturneScale.S(34) + fieldH * 2 + NocturneScale.S(8)
                     + NocturneScale.S(26) + NocturneScale.S(34) + NocturneScale.S(34)
                     + NocturneScale.S(24) + NocturneScale.S(26);
            _dnsCard.SetBounds(railX, y, railW, dnsH);
            _adapters.SetBounds(0, 0, _dnsCard.Body.Width, fieldH);
            _dns.SetBounds(0, fieldH + NocturneScale.S(8), _dnsCard.Body.Width, fieldH);
            _allNics.SetBounds(0, fieldH * 2 + NocturneScale.S(16), _dnsCard.Body.Width, NocturneScale.S(20));
            _setDns.SetBounds(0, fieldH * 2 + NocturneScale.S(42), _dnsCard.Body.Width, NocturneScale.S(34));
            _openAdapters.SetBounds(0, fieldH * 2 + NocturneScale.S(80), _dnsCard.Body.Width, NocturneScale.S(30));

            y += dnsH + gap;
            // The state line and the button had to share one 320px-wide row.
            // The button's label is "Flush DNS cache" -- not "Flush", which is
            // only the code-level default -- so at a fixed 100px it rendered as
            // "Flush DNS cac", and at its true width it covered the state line.
            // Neither fits beside the other in any language, so they stack.
            int cacheH = NocturneScale.S(34) + StateLineHeight + NocturneScale.S(8)
                       + NocturneScale.S(34) + NocturneScale.S(26);
            _cacheCard.SetBounds(railX, y, railW, cacheH);
            int flushW = Math.Min(_cacheCard.Body.Width,
                                  Math.Max(NocturneScale.S(100), _flush.Width));
            _flush.SetBounds(_cacheCard.Body.Width - flushW, StateLineHeight + NocturneScale.S(8),
                             flushW, NocturneScale.S(34));

            y += cacheH;
            Body.Height = Math.Max(pingerH, y) + NocturneScale.S(20);
        }
    }
}
