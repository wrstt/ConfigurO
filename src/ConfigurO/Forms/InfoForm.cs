using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;

namespace ConfigurO
{
    public sealed partial class InfoForm : Form
    {
        public InfoForm(string info)
        {
            InitializeComponent();
            CheckForIllegalCrossThreadCalls = false;

            OptionsHelper.ApplyTheme(this);

            txtInfo.Text = info;

            // translate UI elements
            if (OptionsHelper.CurrentOptions.LanguageCode != LanguageCode.EN) Translate();
        }

        private void Translate()
        {
            Dictionary<string, string> translationList = I18n.Map();

            Control element;

            foreach (var x in translationList)
            {
                if (x.Key == null || x.Key == string.Empty) continue;
                element = this.Controls.Find(x.Key, true).FirstOrDefault();

                if (element == null) continue;

                element.Text = x.Value;
            }
        }

        private void btnOK_Click(object sender, EventArgs e)
        {
            this.Close();
        }

        private void Info_Load(object sender, EventArgs e)
        {

        }

        private void copyIPB_Click(object sender, EventArgs e)
        {
            try
            {
                Clipboard.SetText(txtInfo.Text);
            }
            catch { }
        }
    }
}
