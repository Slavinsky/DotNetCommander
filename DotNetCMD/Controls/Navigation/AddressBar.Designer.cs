namespace DotNetCommander
{
    partial class AddressBar
    {
        /// <summary> 
        /// Erforderliche Designervariable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary> 
        /// Verwendete Ressourcen bereinigen.
        /// </summary>
        /// <param name="disposing">True, wenn verwaltete Ressourcen gelöscht werden sollen; andernfalls False.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Vom Komponenten-Designer generierter Code

        /// <summary> 
        /// Erforderliche Methode für die Designerunterstützung. 
        /// Der Inhalt der Methode darf nicht mit dem Code-Editor geändert werden.
        /// </summary>
        private void InitializeComponent()
        {
            this.flowLayoutAddressBar = new System.Windows.Forms.FlowLayoutPanel();
            this.SuspendLayout();
            // 
            // flowLayoutAddressBar
            // 
            this.flowLayoutAddressBar.BackColor = System.Drawing.SystemColors.InactiveBorder;
            this.flowLayoutAddressBar.BorderStyle = System.Windows.Forms.BorderStyle.Fixed3D;
            this.flowLayoutAddressBar.Dock = System.Windows.Forms.DockStyle.Fill;
            this.flowLayoutAddressBar.Location = new System.Drawing.Point(0, 0);
            this.flowLayoutAddressBar.Margin = new System.Windows.Forms.Padding(0);
            this.flowLayoutAddressBar.Name = "flowLayoutAddressBar";
            this.flowLayoutAddressBar.Size = new System.Drawing.Size(295, 31);
            this.flowLayoutAddressBar.TabIndex = 0;
            this.flowLayoutAddressBar.WrapContents = false;
            this.flowLayoutAddressBar.MouseClick += new System.Windows.Forms.MouseEventHandler(this.flowLayoutAddressBar_MouseClick);
            // 
            // AddressBar
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.Controls.Add(this.flowLayoutAddressBar);
            this.Name = "AddressBar";
            this.Size = new System.Drawing.Size(295, 31);
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.FlowLayoutPanel flowLayoutAddressBar;
    }
}
