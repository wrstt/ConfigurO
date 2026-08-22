// Headless render harness (Linux dev only -- never part of the shipping build).
//
// Instantiates the Nocturne controls, drives their protected OnPaint with a
// Graphics backed by a Bitmap, and writes PNGs. That makes the redesign
// reviewable on a machine that cannot run WinForms, and catches paint-time
// exceptions the type-checker cannot.
#if MONO_LINUX_CHECK
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Reflection;
using System.Windows.Forms;

namespace ConfigurO
{
    internal static class RenderHarness
    {
        static string _out;

        internal static int Main(string[] args)
        {
            _out = args.Length > 0 ? args[0] : "build/render/out";
            Directory.CreateDirectory(_out);

            // libgdiplus registers a PrivateFontCollection face but cannot
            // rasterise it -- every glyph comes out as a box. The shipping
            // Windows build uses bundled Inter; these renders use the
            // system fallback chain so the layout is actually reviewable.
            NocturneFonts.ForceSystemFonts = true;
            NocturneFonts.Load();
            Console.WriteLine("bundled fonts: " + NocturneFonts.Bundled + " (forced off for headless rendering)");

            OptionsHelper.CurrentOptions = new Options
            {
                ThemeMode = NocturneTheme.Mode.Dark,
                ShowHelpMessages = true,
                InternalDNS = "1.1.1.1",
                LanguageCode = LanguageCode.EN
            };

            foreach (NocturneTheme.Mode mode in new[] { NocturneTheme.Mode.Dark, NocturneTheme.Mode.Light })
            {
                NocturneTheme.Current = mode;
                string suffix = mode == NocturneTheme.Mode.Dark ? "dark" : "light";
                Shell(suffix);
                Controls(suffix);
                FirstRun(suffix);
                if (mode == NocturneTheme.Mode.Dark)
                {
                    _narrow = false; Screens();
                    // Minimum supported window: catches header actions running
                    // under the title and cards overflowing their column.
                    _narrow = true; Screens();
                }
            }

            // Everything above renders at 96 DPI, the one scale at which a
            // measurement taken on a default Bitmap agrees with what gets
            // painted. This pass catches layout that only adds up at 100%:
            // boxes, padding and column math.
            //
            // It does NOT catch font-metric bugs. libgdiplus draws and
            // measures a point-sized font at the same pixel size whatever the
            // Graphics DPI says, so type never grows here the way it does on
            // Windows. That blind spot is how an undersized-measurement bug
            // reached 1.0 -- invisible on this harness by construction, and
            // visible immediately on a real scaled Windows display.
            NocturneTheme.Current = NocturneTheme.Mode.Dark;
            NocturneScale.SetDpi(120);
            _narrow = false;
            _suffix = "-125";
            Screens();
            NocturneScale.SetDpi(96);
            _suffix = "";

            Console.WriteLine("wrote PNGs to " + _out);
            return 0;
        }

        // ── canvas helpers ──────────────────────────────────────────────
        static Bitmap Canvas(int w, int h, out Graphics g)
        {
            Bitmap b = new Bitmap(w, h);
            // Match the DPI a real window would paint at, so a point-sized font
            // grows here the way it does on a scaled display. Without this the
            // canvas stays at 96 DPI, type never scales, and the scaled pass
            // measures nothing the 100% pass did not already cover.
            float dpi = 96f * NocturneScale.Factor;
            b.SetResolution(dpi, dpi);
            g = Graphics.FromImage(b);
            using (SolidBrush s = new SolidBrush(NocturneTheme.Bg)) g.FillRectangle(s, 0, 0, w, h);
            return b;
        }

        static bool _painted;

        static readonly MethodInfo OnPaintMethod =
            typeof(Control).GetMethod("OnPaint", BindingFlags.Instance | BindingFlags.NonPublic);

        /// <summary>Paints a control at (x,y) without it ever owning a window handle.</summary>
        static void Paint(Graphics g, Control c, int x, int y)
        {
            _painted = true;
            using (Bitmap layer = new Bitmap(Math.Max(1, c.Width), Math.Max(1, c.Height)))
            {
                using (Graphics lg = Graphics.FromImage(layer))
                {
                    MethodInfo m = c.GetType().GetMethod("OnPaint",
                        BindingFlags.Instance | BindingFlags.NonPublic) ?? OnPaintMethod;
                    try
                    {
                        m.Invoke(c, new object[]
                        {
                            new PaintEventArgs(lg, new Rectangle(0, 0, c.Width, c.Height))
                        });
                    }
                    catch (TargetInvocationException ex)
                    {
                        Console.WriteLine("  !! paint failed: " + c.GetType().Name + " -> " + ex.InnerException);
                    }
                }
                g.DrawImage(layer, x, y);
            }
        }

        /// <summary>
        /// Runs one control's construction and paint, reporting rather than
        /// aborting. Mono's TextBoxBase, for instance, needs a real window to
        /// measure itself, which a headless render will never have.
        /// </summary>
        static void Safe(string what, Action a)
        {
            try { a(); }
            catch (Exception ex)
            {
                Exception e = ex.InnerException ?? ex;
                Console.WriteLine("  !! skipped " + what + ": " + e.GetType().Name +
                                  ": " + e.Message);
            }
        }

        /// <summary>Stand-in for an input that could not be constructed headless.</summary>
        static void FakeInput(Graphics g, Rectangle r, string placeholder, string icon)
        {
            NocturneDraw.Card(g, r, NocturneTheme.Surface, NocturneTheme.Divider, NocturneTheme.RadiusMd);
            int x = r.X + 10;
            if (icon != null)
            {
                NocturneIcons.Draw(g, icon, x, r.Y + (r.Height - 16) / 2, 16, NocturneTheme.TextFaint);
                x += 23;
            }
            using (Font f = NocturneFonts.Row())
                NocturneDraw.Text(g, placeholder, f, NocturneTheme.TextDim,
                    new RectangleF(x, r.Y, r.Width - (x - r.X) - 10, r.Height), NocturneDraw.Left);
        }

        static void Save(Bitmap b, Graphics g, string name)
        {
            g.Dispose();
            b.Save(Path.Combine(_out, name + ".png"), ImageFormat.Png);
            b.Dispose();
            Console.WriteLine("  " + name + ".png");
        }

        // ── the shell: title bar + sidebar + a screen header ─────────────
        static void Shell(string suffix)
        {
            _painted = false;
            Graphics g;
            Bitmap b = Canvas(1340, 860, out g);

            NTitleBar bar = new NTitleBar
            {
                Width = 1340,
                VersionTag = "1.0",
                OsSummary = "Windows 11 Pro · 24H2 · x64"
            };
            Paint(g, bar, 0, 0);

            NSidebar side = new NSidebar { Height = 860 - 46 };
            side.SetItems(NavItems());
            side.Selected = "tweaks";
            side.FooterPrimary = "12 tweaks applied";
            side.FooterSecondary = "Policies reinforced today";
            Paint(g, side, 0, 46);

            // screen header, drawn the way NScreen paints it
            NocturneDraw.Prepare(g);
            int px = 208 + 32;
            using (Font f = NocturneFonts.ScreenTitle())
                NocturneDraw.Text(g, "Universal tweaks", f, NocturneTheme.Text,
                    new RectangleF(px, 46 + 26, 600, 28), NocturneDraw.Left);
            using (Font f = NocturneFonts.ScreenSubtitle())
                NocturneDraw.Text(g, "84 tweaks · 12 applied", f, NocturneTheme.TextMuted,
                    new RectangleF(px, 46 + 53, 600, 18), NocturneDraw.Left);

            Rectangle searchBox = new Rectangle(1340 - 32 - 260 - 8 - 170, 46 + 24, 260, 36);
            _painted = false;
            Safe("search box", () =>
            {
                NTextBox search = new NTextBox
                {
                    Width = searchBox.Width, Height = searchBox.Height,
                    Placeholder = "Search tweaks…",
                    Icon = NocturneIcons.Search
                };
                Paint(g, search, searchBox.X, searchBox.Y);
            });
            if (!_painted) FakeInput(g, searchBox, "Search tweaks…", NocturneIcons.Search);

            NButton reinforce = new NButton
            {
                Style = NButtonStyle.Secondary,
                Text = "Reinforce policies",
                Icon = NocturneIcons.Refresh,
                Width = 170, Height = 34
            };
            Paint(g, reinforce, 1340 - 32 - 170, 46 + 25);

            NTweakList list = new NTweakList { Width = 1340 - 208 - 64 };
            list.Load();
            Paint(g, list, px, 46 + 84);

            NToast toast = new NToast { Width = 240, Height = 38, Text = "Policies reinforced" };
            Paint(g, toast, (1340 - 240) / 2, 860 - 38 - 28);

            Save(b, g, "shell-" + suffix);
        }

        static List<NNavItem> NavItems()
        {
            return new List<NNavItem>
            {
                new NNavItem { Id = "tweaks", Icon = NocturneIcons.Tweaks, Label = "Tweaks" },
                new NNavItem { Id = "cleaner", Icon = NocturneIcons.Cleaner, Label = "Cleaner" },
                new NNavItem { Id = "startup", Icon = NocturneIcons.Startup, Label = "Startup" },
                new NNavItem { Id = "hosts", Icon = NocturneIcons.Hosts, Label = "Hosts" },
                new NNavItem { Id = "apps", Icon = NocturneIcons.Apps, Label = "Apps" },
                new NNavItem { Id = "network", Icon = NocturneIcons.Network, Label = "Network" },
                new NNavItem { Id = "uwp", Icon = NocturneIcons.Uwp, Label = "UWP Apps" },
                new NNavItem { Id = "hardware", Icon = NocturneIcons.Hardware, Label = "Hardware" },
                new NNavItem { Id = "integrator", Icon = NocturneIcons.Integrator, Label = "Integrator" },
                new NNavItem { Id = "settings", Icon = NocturneIcons.Settings, Label = "Settings" }
            };
        }

        // ── the control language, on one sheet ──────────────────────────
        static void Controls(string suffix)
        {
            _painted = false;
            Graphics g;
            Bitmap b = Canvas(980, 620, out g);
            NocturneDraw.Prepare(g);

            int y = 24;
            Label(g, "BUTTONS", 24, y); y += 26;

            Paint(g, Btn(NButtonStyle.Primary, "Add", NocturneIcons.Add, 110), 24, y);
            Paint(g, Btn(NButtonStyle.Secondary, "Backup", NocturneIcons.Save, 130), 146, y);
            Paint(g, Btn(NButtonStyle.Ghost, "Restore", NocturneIcons.History, 120), 288, y);
            NButton pill = Btn(NButtonStyle.Pill, "Web Browsers", null, 140); pill.Height = 28; pill.Active = true;
            Paint(g, pill, 420, y + 3);
            NButton pill2 = Btn(NButtonStyle.Pill, "Messaging", null, 110); pill2.Height = 28;
            Paint(g, pill2, 572, y + 3);
            Paint(g, Btn(NButtonStyle.Icon, "", NocturneIcons.Folder, 36), 694, y);
            y += 60;

            Label(g, "TOGGLE · CHECKBOX · TAG · PROGRESS", 24, y); y += 26;
            NocturneTogglePill.DrawAnimated(g, new Rectangle(24, y + 7, 37, 20), 1f);
            NocturneTogglePill.DrawAnimated(g, new Rectangle(74, y + 7, 37, 20), 0f);
            NocturneCheckGlyph.Draw(g, new Rectangle(128, y + 8, 18, 18), true);
            NocturneCheckGlyph.Draw(g, new Rectangle(156, y + 8, 18, 18), false);

            NTag tag = new NTag { Text = "Blocked", Height = 18, Width = 66 };
            Paint(g, tag, 196, y + 8);
            NTag tag2 = new NTag { Text = "dl", Height = 18, Width = 40, Outline = true };
            Paint(g, tag2, 272, y + 8);

            MoonProgress bar = new MoonProgress { Width = 220, Height = 8, Maximum = 100, Value = 62 };
            Paint(g, bar, 330, y + 13);
            y += 60;

            Label(g, "INPUT · SELECTABLE CARDS", 24, y); y += 26;
            Rectangle inputBox = new Rectangle(24, y, 260, 36);
            _painted = false;
            Safe("input", () =>
            {
                NTextBox input = new NTextBox
                { Width = inputBox.Width, Height = inputBox.Height, Placeholder = "example.com" };
                Paint(g, input, inputBox.X, inputBox.Y);
            });
            if (!_painted) FakeInput(g, inputBox, "example.com", null);

            NSelectCard card = new NSelectCard
            { Width = 300, Height = 52, Text = "Temporary files", Meta = "1.2 GB",
              Icon = NocturneIcons.Windows, Selected = true };
            Paint(g, card, 300, y);

            NSelectCard card2 = new NSelectCard
            { Width = 300, Height = 52, Text = "Recycle Bin", Meta = "2.3 GB", Icon = NocturneIcons.Trash };
            Paint(g, card2, 616, y);
            y += 72;

            Label(g, "TILE · CARD · BANNER", 24, y); y += 26;
            NSelectCard tile = new NSelectCard
            { Width = 150, Height = 104, Kind = NSelectCard.CardLayout.Tile,
              Text = "Visual Studio Code", Icon = "code-s-slash-line", Status = "42%", Selected = true };
            Paint(g, tile, 24, y);

            NCard cardBox = new NCard { Width = 300, Height = 104, Title = "Lock hosts file",
                                        Icon = NocturneIcons.Lock, Note = "Read-only protection" };
            Paint(g, cardBox, 190, y);

            NBanner banner = new NBanner
            { Width = 440, Height = 38, Text = "Restart to finish applying these changes",
              ActionText = "Restart now", Visible = true };
            Paint(g, banner, 506, y + 8);
            y += 124;

            Label(g, "TABLE · CONSOLE", 24, y); y += 26;
            NTable table = new NTable { Width = 460, Height = 130 };
            table.SetColumns(
                new NColumn { Header = "Name", Weight = 1.3f },
                new NColumn { Header = "Location", Weight = 1.2f, Kind = NCellKind.Mono },
                new NColumn { Header = "Enabled", Width = 70, Kind = NCellKind.Toggle },
                new NColumn { Header = "", Width = 40, Kind = NCellKind.Action, Icon = NocturneIcons.Trash });
            table.AddRow(new[] { "OneDrive", @"HKCU\..\Run", "", "" }, true);
            table.AddRow(new[] { "Spotify", @"HKCU\..\Run", "", "" }, true);
            table.AddRow(new[] { "Discord", @"HKCU\..\Run", "", "" }, false);
            Paint(g, table, 24, y);

            NConsole console = new NConsole { Width = 440, Height = 130 };
            console.Set("Pinging 1.1.1.1 with 32 bytes of data — 9 times…",
                        "Reply from 1.1.1.1: bytes=32 time=12ms TTL=57",
                        "Reply from 1.1.1.1: bytes=32 time=11ms TTL=57",
                        "Reply from 1.1.1.1: bytes=32 time=14ms TTL=57");
            Paint(g, console, 506, y);

            Save(b, g, "controls-" + suffix);
        }

        // ── every real screen, built and laid out the way the shell does it ──
        static bool _narrow;
        static string _suffix = "";

        static void Screens()
        {
            NScreen[] screens =
            {
                new TweaksScreen(), new CleanerScreen(), new StartupScreen(), new HostsScreen(),
                new AppsScreen(), new NetworkScreen(), new UwpScreen(), new HardwareScreen(),
                new IntegratorScreen(), new SettingsScreen()
            };

            foreach (NScreen screen in screens)
            {
                string id = screen.Id;
                Safe("screen:" + id, () =>
                {
                    // Same box the shell gives a screen at the default size.
                    screen.Size = new Size(
                        NocturneScale.S(_narrow ? 1040 - 208 : 1340 - 208),
                        NocturneScale.S(_narrow ? 660 - 46 : 860 - 46));
                    screen.EnsureBuilt();
                    Safe("activate:" + id, () => screen.Activate());
                    screen.PerformLayout();

                    Graphics g;
                    Bitmap b = Canvas(screen.Width, screen.Height, out g);
                    PaintTree(g, screen, 0, 0);
                    Save(b, g, "screen-" + id + (_narrow ? "-narrow" : "") + _suffix);
                });
                screen.Dispose();
            }
        }

        /// <summary>Paints a container and every visible child, recursively.</summary>
        /// <summary>
        /// The first-run language chooser. Rebuilding it out of Nocturne
        /// controls is what makes it renderable at all -- the Designer form it
        /// replaced was 28 PictureBoxes and 28 RadioButtons, which libgdiplus
        /// will not paint, so nobody could look at this screen without Windows.
        /// </summary>
        static void FirstRun(string suffix)
        {
            Safe("firstrun", () =>
            {
                // A Form cannot be realised without X11, so build the same
                // layout the form does from the same shared constants.
                NLanguagePicker picker = new NLanguagePicker();
                int pad = FirstRunForm.Pad;
                int w = NocturneScale.S(560);
                picker.SetBounds(pad, FirstRunForm.HeaderHeight, w - pad * 2, 1);
                picker.Height = picker.PreferredHeight;
                int h = FirstRunForm.HeaderHeight + picker.Height + FirstRunForm.FooterHeight;

                NButton start = new NButton { Style = NButtonStyle.Primary, Text = "Start" };
                start.AutoWidth = true;
                start.AutoFit();
                start.Height = NocturneScale.S(34);
                start.Location = new Point(w - pad - start.Width,
                                           h - FirstRunForm.FooterHeight + NocturneScale.S(14));

                Graphics g;
                Bitmap b = Canvas(w, h, out g);
                FirstRunForm.PaintChrome(g, new Size(w, h));
                PaintTree(g, picker, picker.Left, picker.Top);
                PaintTree(g, start, start.Left, start.Top);
                Save(b, g, "firstrun-" + suffix);
                picker.Dispose();
                start.Dispose();
            });
        }

        static void PaintTree(Graphics g, Control c, int x, int y)
        {
            Paint(g, c, x, y);
            foreach (Control child in c.Controls)
            {
                if (!child.Visible || child.Width <= 0 || child.Height <= 0) continue;
                PaintTree(g, child, x + child.Left, y + child.Top);
            }
        }

        static NButton Btn(NButtonStyle style, string text, string icon, int width)
        {
            return new NButton { Style = style, Text = text, Icon = icon, Width = width, Height = 34 };
        }

        static void Label(Graphics g, string text, int x, int y)
        {
            using (Font f = NocturneFonts.SectionLabel())
                NocturneDraw.SectionLabel(g, text, f, x, y, 18);
        }
    }
}
#endif
