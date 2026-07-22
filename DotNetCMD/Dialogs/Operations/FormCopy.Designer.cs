namespace DotNetCommander
{
    partial class FormCopy
    {
        private System.ComponentModel.IContainer components = null;

        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        private void InitializeComponent()
        {
            this.panelHeader = new System.Windows.Forms.Panel();
            this.labelDescription = new System.Windows.Forms.Label();
            this.labelTitle = new System.Windows.Forms.Label();
            this.panelContent = new System.Windows.Forms.Panel();
            this.labelSummaryValue = new System.Windows.Forms.Label();
            this.labelSummaryCaption = new System.Windows.Forms.Label();
            this.textPathSource = new System.Windows.Forms.TextBox();
            this.labelSource = new System.Windows.Forms.Label();
            this.textPathDest = new System.Windows.Forms.TextBox();
            this.labelDestination = new System.Windows.Forms.Label();
            this.checkBoxOverwriteExisting = new System.Windows.Forms.CheckBox();
            this.panelButtons = new System.Windows.Forms.Panel();
            this.buttonCancel = new System.Windows.Forms.Button();
            this.buttonOK = new System.Windows.Forms.Button();
            this.panelHeader.SuspendLayout();
            this.panelContent.SuspendLayout();
            this.panelButtons.SuspendLayout();
            this.SuspendLayout();
            // 
            // panelHeader
            // 
            this.panelHeader.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(38)))), ((int)(((byte)(84)))), ((int)(((byte)(124)))));
            this.panelHeader.Controls.Add(this.labelDescription);
            this.panelHeader.Controls.Add(this.labelTitle);
            this.panelHeader.Dock = System.Windows.Forms.DockStyle.Top;
            this.panelHeader.Location = new System.Drawing.Point(0, 0);
            this.panelHeader.Name = "panelHeader";
            this.panelHeader.Padding = new System.Windows.Forms.Padding(18, 16, 18, 12);
            this.panelHeader.Size = new System.Drawing.Size(524, 84);
            this.panelHeader.TabIndex = 0;
            // 
            // labelDescription
            // 
            this.labelDescription.AutoSize = true;
            this.labelDescription.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(224)))), ((int)(((byte)(236)))), ((int)(((byte)(247)))));
            this.labelDescription.Location = new System.Drawing.Point(21, 46);
            this.labelDescription.Name = "labelDescription";
            this.labelDescription.Size = new System.Drawing.Size(112, 13);
            this.labelDescription.TabIndex = 1;
            this.labelDescription.Text = "Choose destination path";
            // 
            // labelTitle
            // 
            this.labelTitle.AutoSize = true;
            this.labelTitle.Font = new System.Drawing.Font("Segoe UI", 14.25F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(204)));
            this.labelTitle.ForeColor = System.Drawing.Color.White;
            this.labelTitle.Location = new System.Drawing.Point(18, 16);
            this.labelTitle.Name = "labelTitle";
            this.labelTitle.Size = new System.Drawing.Size(56, 25);
            this.labelTitle.TabIndex = 0;
            this.labelTitle.Text = "Copy";
            // 
            // panelContent
            // 
            this.panelContent.BackColor = System.Drawing.Color.White;
            this.panelContent.Controls.Add(this.labelSummaryValue);
            this.panelContent.Controls.Add(this.labelSummaryCaption);
            this.panelContent.Controls.Add(this.textPathSource);
            this.panelContent.Controls.Add(this.labelSource);
            this.panelContent.Controls.Add(this.textPathDest);
            this.panelContent.Controls.Add(this.labelDestination);
            this.panelContent.Controls.Add(this.checkBoxOverwriteExisting);
            this.panelContent.Dock = System.Windows.Forms.DockStyle.Fill;
            this.panelContent.Location = new System.Drawing.Point(0, 84);
            this.panelContent.Name = "panelContent";
            this.panelContent.Padding = new System.Windows.Forms.Padding(18, 16, 18, 8);
            this.panelContent.Size = new System.Drawing.Size(524, 266);
            this.panelContent.TabIndex = 1;
            // 
            // labelSummaryValue
            // 
            this.labelSummaryValue.AutoEllipsis = true;
            this.labelSummaryValue.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(204)));
            this.labelSummaryValue.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(51)))), ((int)(((byte)(61)))), ((int)(((byte)(71)))));
            this.labelSummaryValue.Location = new System.Drawing.Point(21, 16);
            this.labelSummaryValue.Name = "labelSummaryValue";
            this.labelSummaryValue.Size = new System.Drawing.Size(482, 17);
            this.labelSummaryValue.TabIndex = 5;
            this.labelSummaryValue.Text = "Selected item";
            // 
            // labelSummaryCaption
            // 
            this.labelSummaryCaption.AutoSize = true;
            this.labelSummaryCaption.ForeColor = System.Drawing.Color.DimGray;
            this.labelSummaryCaption.Location = new System.Drawing.Point(21, 43);
            this.labelSummaryCaption.Name = "labelSummaryCaption";
            this.labelSummaryCaption.Size = new System.Drawing.Size(73, 13);
            this.labelSummaryCaption.TabIndex = 4;
            this.labelSummaryCaption.Text = "Selected items";
            // 
            // textPathSource
            // 
            this.textPathSource.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(248)))), ((int)(((byte)(250)))), ((int)(((byte)(252)))));
            this.textPathSource.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.textPathSource.Location = new System.Drawing.Point(24, 87);
            this.textPathSource.Name = "textPathSource";
            this.textPathSource.ReadOnly = true;
            this.textPathSource.Size = new System.Drawing.Size(479, 20);
            this.textPathSource.TabIndex = 1;
            // 
            // labelSource
            // 
            this.labelSource.AutoSize = true;
            this.labelSource.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(88)))), ((int)(((byte)(96)))), ((int)(((byte)(105)))));
            this.labelSource.Location = new System.Drawing.Point(21, 71);
            this.labelSource.Name = "labelSource";
            this.labelSource.Size = new System.Drawing.Size(41, 13);
            this.labelSource.TabIndex = 0;
            this.labelSource.Text = "Source";
            // 
            // textPathDest
            // 
            this.textPathDest.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.textPathDest.Location = new System.Drawing.Point(24, 140);
            this.textPathDest.Name = "textPathDest";
            this.textPathDest.Size = new System.Drawing.Size(479, 20);
            this.textPathDest.TabIndex = 3;
            // 
            // labelDestination
            // 
            this.labelDestination.AutoSize = true;
            this.labelDestination.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(88)))), ((int)(((byte)(96)))), ((int)(((byte)(105)))));
            this.labelDestination.Location = new System.Drawing.Point(21, 124);
            this.labelDestination.Name = "labelDestination";
            this.labelDestination.Size = new System.Drawing.Size(60, 13);
            this.labelDestination.TabIndex = 2;
            this.labelDestination.Text = "Destination";
            // 
            // checkBoxOverwriteExisting
            // 
            this.checkBoxOverwriteExisting.AutoSize = true;
            this.checkBoxOverwriteExisting.Location = new System.Drawing.Point(24, 176);
            this.checkBoxOverwriteExisting.Name = "checkBoxOverwriteExisting";
            this.checkBoxOverwriteExisting.Size = new System.Drawing.Size(149, 17);
            this.checkBoxOverwriteExisting.TabIndex = 6;
            this.checkBoxOverwriteExisting.Text = "Overwrite existing files";
            this.checkBoxOverwriteExisting.UseVisualStyleBackColor = true;
            // 
            // panelButtons
            // 
            this.panelButtons.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(245)))), ((int)(((byte)(247)))), ((int)(((byte)(250)))));
            this.panelButtons.Controls.Add(this.buttonCancel);
            this.panelButtons.Controls.Add(this.buttonOK);
            this.panelButtons.Dock = System.Windows.Forms.DockStyle.Bottom;
            this.panelButtons.Location = new System.Drawing.Point(0, 350);
            this.panelButtons.Name = "panelButtons";
            this.panelButtons.Padding = new System.Windows.Forms.Padding(18, 10, 18, 10);
            this.panelButtons.Size = new System.Drawing.Size(524, 58);
            this.panelButtons.TabIndex = 2;
            // 
            // buttonCancel
            // 
            this.buttonCancel.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.buttonCancel.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.buttonCancel.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.buttonCancel.Location = new System.Drawing.Point(401, 13);
            this.buttonCancel.Name = "buttonCancel";
            this.buttonCancel.Size = new System.Drawing.Size(102, 32);
            this.buttonCancel.TabIndex = 1;
            this.buttonCancel.Text = "Cancel";
            this.buttonCancel.UseVisualStyleBackColor = true;
            this.buttonCancel.Click += new System.EventHandler(this.buttonCancel_Click);
            // 
            // buttonOK
            // 
            this.buttonOK.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.buttonOK.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(38)))), ((int)(((byte)(84)))), ((int)(((byte)(124)))));
            this.buttonOK.FlatAppearance.BorderSize = 0;
            this.buttonOK.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.buttonOK.ForeColor = System.Drawing.Color.White;
            this.buttonOK.Location = new System.Drawing.Point(293, 13);
            this.buttonOK.Name = "buttonOK";
            this.buttonOK.Size = new System.Drawing.Size(102, 32);
            this.buttonOK.TabIndex = 0;
            this.buttonOK.Text = "Copy";
            this.buttonOK.UseVisualStyleBackColor = false;
            this.buttonOK.Click += new System.EventHandler(this.buttonOK_Click);
            // 
            // FormCopy
            // 
            this.AcceptButton = this.buttonOK;
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.BackColor = System.Drawing.Color.White;
            this.CancelButton = this.buttonCancel;
            this.ClientSize = new System.Drawing.Size(524, 408);
            this.Controls.Add(this.panelContent);
            this.Controls.Add(this.panelButtons);
            this.Controls.Add(this.panelHeader);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "FormCopy";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "Copy";
            this.panelHeader.ResumeLayout(false);
            this.panelHeader.PerformLayout();
            this.panelContent.ResumeLayout(false);
            this.panelContent.PerformLayout();
            this.panelButtons.ResumeLayout(false);
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.Panel panelHeader;
        private System.Windows.Forms.Label labelDescription;
        private System.Windows.Forms.Label labelTitle;
        private System.Windows.Forms.Panel panelContent;
        private System.Windows.Forms.Label labelSummaryValue;
        private System.Windows.Forms.Label labelSummaryCaption;
        private System.Windows.Forms.TextBox textPathSource;
        private System.Windows.Forms.Label labelSource;
        private System.Windows.Forms.TextBox textPathDest;
        private System.Windows.Forms.Label labelDestination;
        private System.Windows.Forms.CheckBox checkBoxOverwriteExisting;
        private System.Windows.Forms.Panel panelButtons;
        private System.Windows.Forms.Button buttonCancel;
        private System.Windows.Forms.Button buttonOK;
    }
}
