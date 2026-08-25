namespace ConfigurO
{
    partial class SplashForm
    {
        private System.ComponentModel.IContainer components = null;

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(SplashForm));
            this.LoadingStatus = new System.Windows.Forms.Label();
            this.SuspendLayout();
            //
            // LoadingStatus
            //
            // Kept as a Label because MainForm writes to it across threads
            // through Utilities.SetControlPropertyThreadSafe, which needs a
            // real control. Everything else on this form is painted.
            this.LoadingStatus.BackColor = System.Drawing.Color.Transparent;
            this.LoadingStatus.Dock = System.Windows.Forms.DockStyle.Bottom;
            this.LoadingStatus.Name = "LoadingStatus";
            this.LoadingStatus.Size = new System.Drawing.Size(460, 40);
            this.LoadingStatus.TabIndex = 0;
            this.LoadingStatus.Text = "";
            this.LoadingStatus.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            //
            // SplashForm
            //
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.None;
            this.ClientSize = new System.Drawing.Size(460, 300);
            this.Controls.Add(this.LoadingStatus);
            this.DoubleBuffered = true;
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.None;
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "SplashForm";
            this.ShowInTaskbar = false;
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.ResumeLayout(false);
        }

        #endregion

        internal System.Windows.Forms.Label LoadingStatus;
    }
}
