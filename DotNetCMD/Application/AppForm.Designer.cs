namespace DotNetCommander
{
    partial class AppForm
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
      this.components = new System.ComponentModel.Container();
      System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(AppForm));
      this.menuStrip1 = new System.Windows.Forms.MenuStrip();
      this.dateiToolStripMenuItem1 = new System.Windows.Forms.ToolStripMenuItem();
      this.bearbeitenToolStripMenuItem1 = new System.Windows.Forms.ToolStripMenuItem();
      this.ansichtToolStripMenuItem1 = new System.Windows.Forms.ToolStripMenuItem();
      this.toolsToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
      this.helpToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
      this.dateiToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
      this.bearbeitenToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
      this.ansichtToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
      this.toolStripDrivers = new System.Windows.Forms.ToolStrip();
      this.imageListDrivers = new System.Windows.Forms.ImageList(this.components);
      this.splitContainer1 = new System.Windows.Forms.SplitContainer();
      this.fileBrowserLeft = new DotNetCommander.FileBrowser();
      this.fileBrowserRight = new DotNetCommander.FileBrowser();
      this.toolStripButtons = new System.Windows.Forms.ToolStrip();
      this.statusStripMain = new System.Windows.Forms.StatusStrip();
      this.toolStripStatusLabelInfo = new System.Windows.Forms.ToolStripStatusLabel();
      this.menuStrip1.SuspendLayout();
      ((System.ComponentModel.ISupportInitialize)(this.splitContainer1)).BeginInit();
      this.splitContainer1.Panel1.SuspendLayout();
      this.splitContainer1.Panel2.SuspendLayout();
      this.splitContainer1.SuspendLayout();
      this.statusStripMain.SuspendLayout();
      this.SuspendLayout();
      // 
      // menuStrip1
      // 
      this.menuStrip1.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.dateiToolStripMenuItem1,
            this.bearbeitenToolStripMenuItem1,
            this.ansichtToolStripMenuItem1,
            this.toolsToolStripMenuItem,
            this.helpToolStripMenuItem});
      this.menuStrip1.Location = new System.Drawing.Point(0, 0);
      this.menuStrip1.Name = "menuStrip1";
      this.menuStrip1.Size = new System.Drawing.Size(756, 33);
      this.menuStrip1.TabIndex = 0;
      this.menuStrip1.Text = "menuStrip1";
      // 
      // dateiToolStripMenuItem1
      // 
      this.dateiToolStripMenuItem1.Name = "dateiToolStripMenuItem1";
      this.dateiToolStripMenuItem1.Size = new System.Drawing.Size(65, 29);
      this.dateiToolStripMenuItem1.Text = "File";
      // 
      // bearbeitenToolStripMenuItem1
      // 
      this.bearbeitenToolStripMenuItem1.Name = "bearbeitenToolStripMenuItem1";
      this.bearbeitenToolStripMenuItem1.Size = new System.Drawing.Size(107, 29);
      this.bearbeitenToolStripMenuItem1.Text = "Edit";
      // 
      // ansichtToolStripMenuItem1
      // 
      this.ansichtToolStripMenuItem1.Name = "ansichtToolStripMenuItem1";
      this.ansichtToolStripMenuItem1.Size = new System.Drawing.Size(82, 29);
      this.ansichtToolStripMenuItem1.Text = "View";
      // 
      // toolsToolStripMenuItem
      // 
      this.toolsToolStripMenuItem.Name = "toolsToolStripMenuItem";
      this.toolsToolStripMenuItem.Size = new System.Drawing.Size(68, 29);
      this.toolsToolStripMenuItem.Text = "Tools";
      // 
      // helpToolStripMenuItem
      // 
      this.helpToolStripMenuItem.Name = "helpToolStripMenuItem";
      this.helpToolStripMenuItem.Size = new System.Drawing.Size(65, 29);
      this.helpToolStripMenuItem.Text = "Help";
      // 
      // dateiToolStripMenuItem
      // 
      this.dateiToolStripMenuItem.Name = "dateiToolStripMenuItem";
      this.dateiToolStripMenuItem.Size = new System.Drawing.Size(46, 20);
      this.dateiToolStripMenuItem.Text = "File";
      // 
      // bearbeitenToolStripMenuItem
      // 
      this.bearbeitenToolStripMenuItem.Name = "bearbeitenToolStripMenuItem";
      this.bearbeitenToolStripMenuItem.Size = new System.Drawing.Size(75, 20);
      this.bearbeitenToolStripMenuItem.Text = "Edit";
      // 
      // ansichtToolStripMenuItem
      // 
      this.ansichtToolStripMenuItem.Name = "ansichtToolStripMenuItem";
      this.ansichtToolStripMenuItem.Size = new System.Drawing.Size(59, 20);
      this.ansichtToolStripMenuItem.Text = "View";
      // 
      // toolStripDrivers
      // 
      this.toolStripDrivers.AutoSize = false;
      this.toolStripDrivers.BackColor = System.Drawing.SystemColors.Control;
      this.toolStripDrivers.GripStyle = System.Windows.Forms.ToolStripGripStyle.Hidden;
      this.toolStripDrivers.Location = new System.Drawing.Point(0, 33);
      this.toolStripDrivers.Name = "toolStripDrivers";
      this.toolStripDrivers.Padding = new System.Windows.Forms.Padding(5, 0, 1, 0);
      this.toolStripDrivers.Size = new System.Drawing.Size(756, 25);
      this.toolStripDrivers.TabIndex = 2;
      this.toolStripDrivers.Text = "toolStrip2";
      // 
      // imageListDrivers
      // 
      this.imageListDrivers.ColorDepth = System.Windows.Forms.ColorDepth.Depth32Bit;
      this.imageListDrivers.ImageSize = new System.Drawing.Size(16, 16);
      this.imageListDrivers.TransparentColor = System.Drawing.Color.Transparent;
      // 
      // splitContainer1
      // 
      this.splitContainer1.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
      this.splitContainer1.BorderStyle = System.Windows.Forms.BorderStyle.Fixed3D;
      this.splitContainer1.Location = new System.Drawing.Point(0, 52);
      this.splitContainer1.Name = "splitContainer1";
      // 
      // splitContainer1.Panel1
      // 
      this.splitContainer1.Panel1.Controls.Add(this.fileBrowserLeft);
      // 
      // splitContainer1.Panel2
      // 
      this.splitContainer1.Panel2.Controls.Add(this.fileBrowserRight);
      this.splitContainer1.Size = new System.Drawing.Size(756, 426);
      this.splitContainer1.SplitterDistance = 375;
      this.splitContainer1.TabIndex = 5;
      // 
      // fileBrowserLeft
      // 
      this.fileBrowserLeft.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
      this.fileBrowserLeft.BackColor = System.Drawing.SystemColors.Control;
      this.fileBrowserLeft.Font = new System.Drawing.Font("Microsoft Sans Serif", 11.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(204)));
      this.fileBrowserLeft.Location = new System.Drawing.Point(3, 3);
      this.fileBrowserLeft.Margin = new System.Windows.Forms.Padding(4);
      this.fileBrowserLeft.Name = "fileBrowserLeft";
      this.fileBrowserLeft.Size = new System.Drawing.Size(365, 419);
      this.fileBrowserLeft.TabIndex = 4;
      this.fileBrowserLeft.PathChange += new DotNetCommander.FileBrowser.PathChangeHandler(this.fileBrowser_PathChange);
      this.fileBrowserLeft.Enter += new System.EventHandler(this.fileBrowser_Enter);
      // 
      // fileBrowserRight
      // 
      this.fileBrowserRight.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
      this.fileBrowserRight.Font = new System.Drawing.Font("Microsoft Sans Serif", 11.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(204)));
      this.fileBrowserRight.Location = new System.Drawing.Point(3, 3);
      this.fileBrowserRight.Margin = new System.Windows.Forms.Padding(4);
      this.fileBrowserRight.Name = "fileBrowserRight";
      this.fileBrowserRight.Size = new System.Drawing.Size(367, 419);
      this.fileBrowserRight.TabIndex = 4;
      this.fileBrowserRight.PathChange += new DotNetCommander.FileBrowser.PathChangeHandler(this.fileBrowser_PathChange);
      this.fileBrowserRight.Enter += new System.EventHandler(this.fileBrowser_Enter);
      // 
      // toolStripButtons
      // 
      this.toolStripButtons.AutoSize = false;
      this.toolStripButtons.CanOverflow = false;
      this.toolStripButtons.Dock = System.Windows.Forms.DockStyle.Bottom;
      this.toolStripButtons.GripStyle = System.Windows.Forms.ToolStripGripStyle.Hidden;
      this.toolStripButtons.Location = new System.Drawing.Point(0, 481);
      this.toolStripButtons.Name = "toolStripButtons";
      this.toolStripButtons.RenderMode = System.Windows.Forms.ToolStripRenderMode.System;
      this.toolStripButtons.Size = new System.Drawing.Size(756, 25);
      this.toolStripButtons.TabIndex = 6;
      this.toolStripButtons.Text = "toolStripCommands";
      // 
      // statusStripMain
      // 
      this.statusStripMain.ImageScalingSize = new System.Drawing.Size(24, 24);
      this.statusStripMain.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.toolStripStatusLabelInfo});
      this.statusStripMain.Location = new System.Drawing.Point(0, 459);
      this.statusStripMain.Name = "statusStripMain";
      this.statusStripMain.ShowItemToolTips = true;
      this.statusStripMain.SizingGrip = false;
      this.statusStripMain.Size = new System.Drawing.Size(756, 22);
      this.statusStripMain.TabIndex = 7;
      this.statusStripMain.Text = "statusStripMain";
      // 
      // toolStripStatusLabelInfo
      // 
      this.toolStripStatusLabelInfo.Name = "toolStripStatusLabelInfo";
      this.toolStripStatusLabelInfo.Size = new System.Drawing.Size(741, 17);
      this.toolStripStatusLabelInfo.Spring = true;
      this.toolStripStatusLabelInfo.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
      // 
      // AppForm
      // 
      this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
      this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
      this.ClientSize = new System.Drawing.Size(756, 506);
      this.Controls.Add(this.toolStripDrivers);
      this.Controls.Add(this.toolStripButtons);
      this.Controls.Add(this.statusStripMain);
      this.Controls.Add(this.splitContainer1);
      this.Controls.Add(this.menuStrip1);
      this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
      this.MainMenuStrip = this.menuStrip1;
      this.Name = "AppForm";
      this.StartPosition = System.Windows.Forms.FormStartPosition.Manual;
      this.Text = ".NetCommander";
      this.FormClosing += new System.Windows.Forms.FormClosingEventHandler(this.AppForm_FormClosing);
      this.Load += new System.EventHandler(this.AppForm_Load);
      this.Resize += new System.EventHandler(this.AppForm_Resize);
      this.menuStrip1.ResumeLayout(false);
      this.menuStrip1.PerformLayout();
      this.splitContainer1.Panel1.ResumeLayout(false);
      this.splitContainer1.Panel2.ResumeLayout(false);
      ((System.ComponentModel.ISupportInitialize)(this.splitContainer1)).EndInit();
      this.splitContainer1.ResumeLayout(false);
      this.statusStripMain.ResumeLayout(false);
      this.statusStripMain.PerformLayout();
      this.ResumeLayout(false);
      this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.MenuStrip menuStrip1;
        private System.Windows.Forms.ToolStripMenuItem dateiToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem bearbeitenToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem ansichtToolStripMenuItem;
        private System.Windows.Forms.ToolStrip toolStripDrivers;
        private System.Windows.Forms.ImageList imageListDrivers;
        private System.Windows.Forms.SplitContainer splitContainer1;
        private System.Windows.Forms.ToolStrip toolStripButtons;
        private FileBrowser fileBrowserLeft;
        private FileBrowser fileBrowserRight;
        private System.Windows.Forms.ToolStripMenuItem dateiToolStripMenuItem1;
        private System.Windows.Forms.ToolStripMenuItem bearbeitenToolStripMenuItem1;
        private System.Windows.Forms.ToolStripMenuItem ansichtToolStripMenuItem1;
        private System.Windows.Forms.ToolStripMenuItem toolsToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem helpToolStripMenuItem;
        private System.Windows.Forms.StatusStrip statusStripMain;
        private System.Windows.Forms.ToolStripStatusLabel toolStripStatusLabelInfo;
    }
}

