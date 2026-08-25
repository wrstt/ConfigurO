using System;
using System.Drawing;
using System.Windows.Forms;

namespace ConfigurO
{
    /// <summary>
    /// The startup splash: the ConfigurO mark on the Nocturne ground, with the
    /// two orbits of the lockup turning while the app loads.
    ///
    /// Drawn rather than blitted. It used to be a PictureBox holding a raster
    /// banner inherited from the project this was forked from -- their artwork
    /// and their identity, stretched to whatever size the form happened to be.
    /// <see cref="NocturneBrand"/> is this app's own mark and is vector, so it
    /// is sharp at any scale and follows the theme.
    ///
    /// The turning orbits are the loading indicator. There is no progress to
    /// report here -- startup is a sequence of steps of unknown length -- so
    /// this says "working" and nothing more, which is the honest signal.
    /// </summary>
    public sealed partial class SplashForm : Form
    {
        readonly Timer _spin = new Timer { Interval = 16 };
        float _angle;

        public SplashForm()
        {
            InitializeComponent();

            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer, true);
            DoubleBuffered = true;

            // The splash runs on its own STA thread and paints before the shell
            // exists, so it loads the faces itself.
            NocturneFonts.Load();

            OptionsHelper.ApplyTheme(this);
            BackColor = NocturneTheme.Bg;

            // Opaque, not transparent, and set after ApplyTheme because
            // ApplyTheme forces every Label to Color.Transparent. A transparent
            // child asks its parent to paint the background behind it, and this
            // form is UserPaint -- OnPaintBackground never runs, so the label
            // kept whatever the buffer last held and came out white.
            LoadingStatus.BackColor = NocturneTheme.Bg;
            LoadingStatus.ForeColor = NocturneTheme.TextMuted;
            LoadingStatus.Font = NocturneFonts.Meta();

            _spin.Tick += (s, e) =>
            {
                _angle = (_angle + 1.1f) % 360f;
                Invalidate();
            };
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);

            // Scale from the display this actually opened on. Without it the
            // splash paints at whatever factor the last window left behind,
            // which on a scaled display is a mark half the size it should be.
            NocturneScale.SetDpi(NocturneScale.DpiOf(Handle));

            LoadingStatus.Height = NocturneScale.S(40);
            LoadingStatus.Font = NocturneFonts.Meta();
            ClientSize = NocturneScale.S(new Size(460, 300));
            Logger.LogInfo("Splash: scale=" + NocturneScale.Factor.ToString("0.00") +
                           " client=" + ClientSize.Width + "x" + ClientSize.Height);

            DwmChrome.SetDarkMode(Handle, NocturneTheme.IsDark);
            DwmChrome.SetCorners(Handle, DwmChrome.CornerPreference.Round);

            _spin.Start();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            NocturneDraw.Prepare(g);

            using (SolidBrush b = new SolidBrush(NocturneTheme.Bg))
                g.FillRectangle(b, ClientRectangle);

            // The mark, with the orbits turning. Sized off the shorter edge so
            // it stays centred whatever the scale rounds the form to.
            int mark = NocturneScale.S(150);
            int markTop = NocturneScale.S(46);
            NocturneBrand.DrawFull(g, new Rectangle((ClientSize.Width - mark) / 2, markTop, mark, mark), _angle);

            int textTop = markTop + mark + NocturneScale.S(14);
            using (Font f = NocturneFonts.Big())
                NocturneDraw.Text(g, "ConfigurO", f, NocturneTheme.Text,
                    new RectangleF(0, textTop, ClientSize.Width, NocturneScale.S(28)),
                    NocturneDraw.Center);

            using (Font f = NocturneFonts.Small())
                NocturneDraw.Text(g, Program.GetCurrentVersionTostring(), f, NocturneTheme.TextFaint,
                    new RectangleF(0, textTop + NocturneScale.S(26), ClientSize.Width, NocturneScale.S(18)),
                    NocturneDraw.Center);

            // A hairline so the window reads as a surface rather than a hole,
            // on the Windows versions that do not outline it themselves.
            NocturneTheme.DrawRounded(g, new Rectangle(0, 0, ClientSize.Width, ClientSize.Height),
                                      NocturneTheme.WindowRadius, NocturneTheme.Border);

            base.OnPaint(e);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _spin.Stop();
                _spin.Dispose();
                if (components != null) components.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
