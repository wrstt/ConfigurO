using System.Windows.Forms;

namespace ConfigurO
{
    public sealed partial class SplashForm : Form
    {
        public SplashForm()
        {
            InitializeComponent();

            this.DoubleBuffered = true;
            CheckForIllegalCrossThreadCalls = false;

            NocturneFonts.Load();
            LoadingStatus.Font = NocturneFonts.Row();
            OptionsHelper.ApplyTheme(this);
            pictureBox2.BackColor = NocturneTheme.Accent;
        }
    }
}
