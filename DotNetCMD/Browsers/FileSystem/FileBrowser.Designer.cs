namespace DotNetCommander
{
    partial class FileBrowser
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
            if (disposing)
            {
                if (directoryWatcher != null)
                {
                    directoryWatcher.EnableRaisingEvents = false;
                    directoryWatcher.Dispose();
                    directoryWatcher = null;
                }

                if (directoryRefreshTimer != null)
                {
                    directoryRefreshTimer.Dispose();
                }

                if (components != null)
                {
                    components.Dispose();
                }
            }
            base.Dispose(disposing);
        }

    #region Vom Komponenten-Designer generierter Code

    /// <summary> 
    /// Erforderliche Methode für die Designerunterstützung. 
    /// Der Inhalt der Methode darf nicht mit dem Code-Editor geändert werden.
    /// </summary>
    private void InitializeComponent() {
      components = new System.ComponentModel.Container();
      fileImages = new System.Windows.Forms.ImageList(components);
      fileImagesLarge = new System.Windows.Forms.ImageList(components);
      browserView = new System.Windows.Forms.ListView();
      contextMenu = new System.Windows.Forms.ContextMenuStrip(components);
      toolStripMenuItem1 = new System.Windows.Forms.ToolStripMenuItem();
      helpProvider1 = new System.Windows.Forms.HelpProvider();
      addressBarCurrentPath = new AddressBar();
      contextMenu.SuspendLayout();
      SuspendLayout();
      // 
      // fileImages
      // 
      fileImages.ColorDepth = System.Windows.Forms.ColorDepth.Depth32Bit;
      fileImages.ImageSize = new System.Drawing.Size(16, 16);
      fileImages.TransparentColor = System.Drawing.Color.Transparent;
      // 
      // fileImagesLarge
      // 
      fileImagesLarge.ColorDepth = System.Windows.Forms.ColorDepth.Depth32Bit;
      fileImagesLarge.ImageSize = new System.Drawing.Size(32, 32);
      fileImagesLarge.TransparentColor = System.Drawing.Color.Transparent;
      // 
      // browserView
      // 
      browserView.AllowColumnReorder = true;
      browserView.AllowDrop = true;
      browserView.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
      browserView.FullRowSelect = true;
      browserView.LabelEdit = true;
      browserView.Location = new System.Drawing.Point(4, 44);
      browserView.Margin = new System.Windows.Forms.Padding(4);
      browserView.Name = "browserView";
      browserView.Size = new System.Drawing.Size(823, 535);
      browserView.SmallImageList = fileImages;
      browserView.LargeImageList = fileImagesLarge;
      browserView.TabIndex = 2;
      browserView.UseCompatibleStateImageBehavior = false;
      browserView.View = System.Windows.Forms.View.Details;
      browserView.BeforeLabelEdit += browserView_BeforeLabelEdit;
      browserView.ItemActivate += browserView_ItemActivate;
      browserView.SelectedIndexChanged += browserView_SelectedIndexChanged;
      browserView.KeyDown += browserView_KeyDown;
      // 
      // contextMenu
      // 
      contextMenu.Items.AddRange(new System.Windows.Forms.ToolStripItem[] { toolStripMenuItem1 });
      contextMenu.Name = "contextMenuStrip1";
      contextMenu.Size = new System.Drawing.Size(244, 34);
      contextMenu.Opening += contextMenu_Opening;
      contextMenu.ItemClicked += contextMenu_ItemClicked;
      // 
      // toolStripMenuItem1
      // 
      toolStripMenuItem1.Name = "toolStripMenuItem1";
      toolStripMenuItem1.Size = new System.Drawing.Size(243, 30);
      toolStripMenuItem1.Text = "toolStripMenuItem1";
      // 
      // addressBarCurrentPath
      // 
      addressBarCurrentPath.AutoSize = true;
      addressBarCurrentPath.Location = new System.Drawing.Point(4, 4);
      addressBarCurrentPath.Margin = new System.Windows.Forms.Padding(4);
      addressBarCurrentPath.Name = "addressBarCurrentPath";
      addressBarCurrentPath.Path = null;
      addressBarCurrentPath.Size = new System.Drawing.Size(823, 32);
      addressBarCurrentPath.TabIndex = 3;
      addressBarCurrentPath.PathChange += addressBarCurrentPath_PathChange;
      // 
      // FileBrowser
      // 
      AutoScaleDimensions = new System.Drawing.SizeF(9F, 18F);
      AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
      Controls.Add(addressBarCurrentPath);
      Controls.Add(browserView);
      Font = new System.Drawing.Font("Microsoft Sans Serif", 11.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
      Margin = new System.Windows.Forms.Padding(4);
      Name = "FileBrowser";
      Size = new System.Drawing.Size(861, 600);
      SizeChanged += FileBrowser_SizeChanged;
      contextMenu.ResumeLayout(false);
      ResumeLayout(false);
      PerformLayout();
    }

    #endregion

    private System.Windows.Forms.ImageList fileImages;
        private System.Windows.Forms.ImageList fileImagesLarge;
        private System.Windows.Forms.ListView browserView;
        private System.Windows.Forms.ContextMenuStrip contextMenu;
        private System.Windows.Forms.HelpProvider helpProvider1;
        private System.Windows.Forms.ToolStripMenuItem toolStripMenuItem1;
        private AddressBar addressBarCurrentPath;
    }
}
