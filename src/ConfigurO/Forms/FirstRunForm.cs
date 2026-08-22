using System;
using System.Drawing;
using System.Windows.Forms;

namespace ConfigurO
{
    /// <summary>
    /// First-run language chooser.
    ///
    /// This was the one screen the Nocturne redesign never reached: a Designer
    /// form of 28 PictureBoxes and 28 RadioButtons at fixed coordinates, sized
    /// against a font the app no longer uses, with a hand-written Click handler
    /// per flag. It is now the picker control plus a button, and the layout is
    /// computed rather than typed.
    /// </summary>
    public sealed class FirstRunForm : Form
    {
        readonly NLanguagePicker _picker = new NLanguagePicker();
        readonly NButton _start = new NButton();

        public FirstRunForm()
        {
            SuspendLayout();

            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.CenterScreen;
            Text = "ConfigurO";
            DoubleBuffered = true;
            BackColor = NocturneTheme.Bg;
            ForeColor = NocturneTheme.Text;

            _picker.Selected = OptionsHelper.CurrentOptions != null
                ? OptionsHelper.CurrentOptions.LanguageCode
                : LanguageCode.EN;
            _picker.SelectionChanged += (s, e) => Apply(_picker.Selected);
            Controls.Add(_picker);

            _start.Style = NButtonStyle.Primary;
            _start.AutoWidth = true;
            _start.Text = "Start";
            _start.Click += (s, e) => Close();
            Controls.Add(_start);
            AcceptButton = _start;

            ClientSize = new Size(NocturneScale.S(560), NocturneScale.S(600));
            ResumeLayout(false);
            Relayout();
        }

        /// <summary>
        /// Writes the choice through immediately, the way the old form did:
        /// the dialog has no Cancel, so whatever is highlighted is the setting.
        /// </summary>
        static void Apply(LanguageCode code)
        {
            if (OptionsHelper.CurrentOptions == null) return;
            OptionsHelper.CurrentOptions.LanguageCode = code;
            OptionsHelper.SaveSettings();
            OptionsHelper.LoadTranslation();
        }

        // static so the render harness can lay the screen out exactly as the
        // form does, without constructing a Form -- which cannot be realised
        // headlessly under Mono.
        internal static int Pad { get { return NocturneScale.S(22); } }
        internal static int HeaderHeight { get { return NocturneScale.S(76); } }
        internal static int FooterHeight { get { return NocturneScale.S(64); } }

        void Relayout()
        {
            int footer = FooterHeight;
            _picker.SetBounds(Pad, HeaderHeight,
                              Math.Max(1, ClientSize.Width - Pad * 2),
                              Math.Max(1, ClientSize.Height - HeaderHeight - footer));
            _start.Location = new Point(ClientSize.Width - Pad - _start.Width,
                                        ClientSize.Height - footer + NocturneScale.S(14));
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            Relayout();
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            // We keep a system caption here -- it is a modal dialog, not the
            // shell -- so at least tell DWM to draw it dark.
            DwmChrome.SetDarkMode(Handle, NocturneTheme.IsDark);
            DwmChrome.SetCaptionColor(Handle, NocturneTheme.Bg);
            DwmChrome.SetCaptionTextColor(Handle, NocturneTheme.Text);
            DwmChrome.SetCorners(Handle, DwmChrome.CornerPreference.Round);
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            // Sized to fit every language without scrolling, when the display
            // allows it; the picker clips rather than overflows if it does not.
            int wanted = HeaderHeight + _picker.PreferredHeight + FooterHeight;
            Screen screen = Screen.FromControl(this);
            int cap = screen.WorkingArea.Height - NocturneScale.S(60);
            if (wanted > ClientSize.Height && wanted <= cap)
            {
                ClientSize = new Size(ClientSize.Width, wanted);
                CenterToScreen();
            }
            _picker.Focus();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            PaintChrome(e.Graphics, ClientSize);
        }

        /// <summary>Title, subtitle and footer rule. Shared with the harness.</summary>
        internal static void PaintChrome(Graphics g, Size client)
        {
            NocturneDraw.Prepare(g);
            int x = Pad;

            using (Font title = NocturneFonts.ScreenTitle())
                NocturneDraw.Text(g, "Choose your language", title, NocturneTheme.Text,
                    new RectangleF(x, NocturneScale.S(24), client.Width - x * 2,
                                   NocturneScale.S(26)), NocturneDraw.Left);

            using (Font sub = NocturneFonts.ScreenSubtitle())
                NocturneDraw.Text(g, "You can change this later in Settings.", sub,
                    NocturneTheme.TextFaint,
                    new RectangleF(x, NocturneScale.S(50), client.Width - x * 2,
                                   NocturneScale.S(18)), NocturneDraw.Left);

            NocturneTheme.DrawFadedRule(g, Pad, client.Height - FooterHeight,
                                        client.Width - Pad * 2, NocturneTheme.Border);
        }
    }
}
