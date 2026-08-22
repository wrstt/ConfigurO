using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;

namespace ConfigurO
{
    public sealed partial class HelperForm : System.Windows.Forms.Form
    {
        readonly Action _onConfirm;
        readonly MessageType _type;

        private void Confirm()
        {
            if (_type == MessageType.Error)
            {
                this.Close();
            }
            if (_type == MessageType.Restart)
            {
                OptionsHelper.SaveSettings();
                Utilities.Reboot();
                return;
            }
            // Startup / Hosts / Integrator all mean "the caller knows what to do".
            if (_onConfirm != null) _onConfirm();
        }

        /// <summary>
        /// A yes/no confirmation. <paramref name="onConfirm"/> runs when the
        /// user accepts; pass null for informational messages.
        /// </summary>
        internal HelperForm(Action onConfirm, MessageType m, string text)
        {
            InitializeComponent();
            OptionsHelper.ApplyTheme(this);

            _onConfirm = onConfirm;
            _type = m;

            lblMessage.Text = text;
            // The designer sets Segoe UI Semibold Bold here; Nocturne never
            // goes above weight 500, so the prompt uses the 20px title face.
            lblMessage.Font = NocturneFonts.Big();

            if (_type == MessageType.Error)
            {
                btnNo.Visible = false;
                // Program shows this before the translations are loaded, so
                // indexing TranslationList directly used to throw here and
                // take out the "unsupported Windows version" message with it.
                btnYes.Text = I18n.Get("btnOk", "OK");

                this.AcceptButton = btnNo;
                this.AcceptButton = btnYes;
                this.CancelButton = btnNo;
                this.CancelButton = btnYes;
            }

            // translate UI elements
            if (OptionsHelper.TranslationList != null &&
                OptionsHelper.CurrentOptions.LanguageCode != LanguageCode.EN) Translate();
        }

        private void btnNo_Click(object sender, EventArgs e)
        {
            this.Close();
        }

        private void btnYes_Click(object sender, EventArgs e)
        {
            Confirm();
            this.Close();
        }

        /// <summary>
        /// A themed yes/no prompt. Used instead of MessageBox wherever the
        /// question belongs to ConfigurO rather than to Windows, so
        /// confirmations follow the Nocturne dialog spec.
        /// </summary>
        internal static bool Confirm(IWin32Window owner, string text)
        {
            using (HelperForm f = new HelperForm(null, MessageType.Error, text))
            {
                // MessageType.Error hides the No button, which is wrong for a
                // question -- restore it and let ShowDialog report the answer.
                f.btnNo.Visible = true;
                f.btnYes.Text = I18n.Get("btnYes", "Yes");
                f.btnNo.Text = I18n.Get("btnNo", "No");
                f.AcceptButton = f.btnYes;
                f.CancelButton = f.btnNo;
                return f.ShowDialog(owner) == DialogResult.Yes;
            }
        }

        private void Messager_Load(object sender, EventArgs e)
        {
            CheckForIllegalCrossThreadCalls = false;
            this.BringToFront();
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

    }
}
