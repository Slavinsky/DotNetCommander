using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;

/// <summary>
/// Compact status window for legacy synchronous operations.
/// </summary>
public class frmWait : Form
{
    private readonly Container components = new Container();
    private readonly Label lblMessage;

    public frmWait(string message = "Wait...")
    {
        Font = DotNetCommander.DialogStyleService.CreateDialogFont();

        var accentPanel = new Panel
        {
            BackColor = Color.FromArgb(55, 93, 129),
            Dock = DockStyle.Left,
            Width = 8
        };

        lblMessage = new Label
        {
            Dock = DockStyle.Fill,
            Font = DotNetCommander.DialogStyleService.CreateEmphasisFont(),
            ForeColor = Color.FromArgb(45, 52, 60),
            Padding = new Padding(20, 0, 20, 0),
            Text = message ?? string.Empty,
            TextAlign = ContentAlignment.MiddleLeft
        };

        AutoScaleMode = AutoScaleMode.Font;
        BackColor = Color.White;
        ClientSize = new Size(360, 92);
        ControlBox = false;
        Controls.Add(lblMessage);
        Controls.Add(accentPanel);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        Name = "frmWait";
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.CenterParent;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            components.Dispose();
        }

        base.Dispose(disposing);
    }
}
