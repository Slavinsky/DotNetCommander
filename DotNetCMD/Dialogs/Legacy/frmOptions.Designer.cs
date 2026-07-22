partial class frmOptions
{
    private System.ComponentModel.IContainer components = null;
    private System.Windows.Forms.PropertyGrid pg;
    private System.Windows.Forms.Label labelTitle;
    private System.Windows.Forms.Button buttonClose;

    protected override void Dispose(bool disposing)
    {
        if (disposing && components != null)
        {
            components.Dispose();
        }

        base.Dispose(disposing);
    }

    private void InitializeComponent()
    {
        var resources = new System.ComponentModel.ComponentResourceManager(typeof(frmOptions));
        var headerPanel = new System.Windows.Forms.Panel();
        var contentPanel = new System.Windows.Forms.Panel();
        var buttonPanel = new System.Windows.Forms.Panel();

        pg = new System.Windows.Forms.PropertyGrid();
        labelTitle = new System.Windows.Forms.Label();
        buttonClose = new System.Windows.Forms.Button();

        SuspendLayout();

        headerPanel.BackColor = System.Drawing.Color.FromArgb(55, 93, 129);
        headerPanel.Controls.Add(labelTitle);
        headerPanel.Dock = System.Windows.Forms.DockStyle.Top;
        headerPanel.Height = 76;

        labelTitle.Dock = System.Windows.Forms.DockStyle.Fill;
        labelTitle.ForeColor = System.Drawing.Color.White;
        labelTitle.Padding = new System.Windows.Forms.Padding(18, 0, 18, 0);
        labelTitle.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;

        contentPanel.BackColor = System.Drawing.Color.White;
        contentPanel.Controls.Add(pg);
        contentPanel.Dock = System.Windows.Forms.DockStyle.Fill;
        contentPanel.Padding = new System.Windows.Forms.Padding(18);

        pg.Dock = System.Windows.Forms.DockStyle.Fill;
        pg.Location = new System.Drawing.Point(18, 18);
        pg.Name = "pg";
        pg.TabIndex = 0;
        pg.ToolbarVisible = true;

        buttonPanel.BackColor = System.Drawing.Color.FromArgb(245, 247, 250);
        buttonPanel.Controls.Add(buttonClose);
        buttonPanel.Dock = System.Windows.Forms.DockStyle.Bottom;
        buttonPanel.Height = 64;

        buttonClose.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right;
        buttonClose.DialogResult = System.Windows.Forms.DialogResult.OK;
        buttonClose.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
        buttonClose.Location = new System.Drawing.Point(359, 13);
        buttonClose.Size = new System.Drawing.Size(110, 36);
        buttonClose.TabIndex = 1;
        buttonClose.UseVisualStyleBackColor = true;

        AcceptButton = buttonClose;
        AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
        BackColor = System.Drawing.Color.White;
        CancelButton = buttonClose;
        ClientSize = new System.Drawing.Size(487, 520);
        Controls.Add(contentPanel);
        Controls.Add(buttonPanel);
        Controls.Add(headerPanel);
        FormBorderStyle = System.Windows.Forms.FormBorderStyle.Sizable;
        Icon = (System.Drawing.Icon)resources.GetObject("$this.Icon");
        MaximizeBox = false;
        MinimizeBox = false;
        MinimumSize = new System.Drawing.Size(520, 480);
        Name = "frmOptions";
        ShowInTaskbar = false;
        StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
        Text = "Options";

        ResumeLayout(false);
    }
}
