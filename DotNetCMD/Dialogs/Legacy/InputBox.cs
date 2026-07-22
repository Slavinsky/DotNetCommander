using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;

/// <summary>
/// Small text input dialog used by legacy call sites.
/// </summary>
public class InputBox : Form
{
    private readonly Container components = new Container();
    private Button _OKButton;
    private Button _CancelButton;
    private Label _Label;
    private TextBox _TextBox;
    private Panel _headerPanel;

    private string output;

    [Browsable(true)]
    [Category("InputBox")]
    public string Output
    {
        get => output;
        set => output = value;
    }

    [Browsable(true)]
    [Category("InputBox")]
    public bool Password
    {
        get => _TextBox.UseSystemPasswordChar;
        set => _TextBox.UseSystemPasswordChar = value;
    }

#if DEBUG
    [Browsable(true)]
    [Category("InputBox")]
    [DebuggerDisplay("{Label.Text}")]
    public Label Label
    {
        get => _Label;
        set => _Label = value;
    }

    [Browsable(true)]
    [Category("InputBox")]
    public TextBox TextBox
    {
        get => _TextBox;
        set => _TextBox = value;
    }
#endif

    public static string Show(
        string prompt,
        string title = "",
        string defaultResponse = "",
        int xPos = -1,
        int yPos = -1)
    {
        using InputBox input = new InputBox(prompt, title, defaultResponse, xPos, yPos);
        input.ShowDialog();
        return input.DialogResult == DialogResult.OK ? input.Output : defaultResponse;
    }

    private InputBox()
    {
        Output = string.Empty;
        InitializeComponent();
    }

    internal InputBox(
        string prompt,
        string title = "",
        string defaultResponse = "",
        int xPos = -1,
        int yPos = -1)
    {
        Output = string.Empty;
        InitializeComponent();
        InitializeInputBox(prompt, title, defaultResponse, xPos, yPos);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            components.Dispose();
        }

        base.Dispose(disposing);
    }

    private void InitializeComponent()
    {
        var resources = new ComponentResourceManager(typeof(InputBox));

        _OKButton = new Button();
        _CancelButton = new Button();
        _TextBox = new TextBox();
        _Label = new Label();

        resources.ApplyResources(_OKButton, "_OKButton");
        resources.ApplyResources(_CancelButton, "_CancelButton");

        _OKButton.Text = string.IsNullOrWhiteSpace(_OKButton.Text)
            ? DotNetCommander.Language.getString("ok")
            : _OKButton.Text;
        _OKButton.BackColor = Color.FromArgb(55, 93, 129);
        _OKButton.DialogResult = DialogResult.OK;
        _OKButton.FlatStyle = FlatStyle.Flat;
        _OKButton.ForeColor = Color.White;
        _OKButton.Size = new Size(110, 36);
        _OKButton.UseVisualStyleBackColor = false;
        _OKButton.FlatAppearance.BorderSize = 0;
        _OKButton.Click += OKButton_Click;

        _CancelButton.Text = string.IsNullOrWhiteSpace(_CancelButton.Text)
            ? DotNetCommander.Language.getString("cancel")
            : _CancelButton.Text;
        _CancelButton.DialogResult = DialogResult.Cancel;
        _CancelButton.FlatStyle = FlatStyle.Flat;
        _CancelButton.Size = new Size(110, 36);
        _CancelButton.UseVisualStyleBackColor = true;
        _CancelButton.Click += CancelButton_Click;

        _Label.Dock = DockStyle.Fill;
        _Label.Font = DotNetCommander.DialogStyleService.CreateBodyFont();
        _Label.ForeColor = Color.White;
        _Label.Padding = new Padding(18, 14, 18, 12);
        _Label.TextAlign = ContentAlignment.MiddleLeft;

        _TextBox.BorderStyle = BorderStyle.FixedSingle;
        _TextBox.Dock = DockStyle.Top;
        _TextBox.Font = DotNetCommander.DialogStyleService.CreateBodyFont();
        _TextBox.Margin = new Padding(0);

        _headerPanel = new Panel
        {
            BackColor = Color.FromArgb(55, 93, 129),
            Dock = DockStyle.Top,
            Height = 82
        };
        _headerPanel.Controls.Add(_Label);

        var contentPanel = new Panel
        {
            BackColor = Color.White,
            Dock = DockStyle.Fill,
            Padding = new Padding(18, 18, 18, 12)
        };
        contentPanel.Controls.Add(_TextBox);

        var buttonPanel = new Panel
        {
            BackColor = Color.FromArgb(245, 247, 250),
            Dock = DockStyle.Bottom,
            Height = 64,
            Padding = new Padding(18, 13, 18, 13)
        };
        buttonPanel.Controls.Add(_OKButton);
        buttonPanel.Controls.Add(_CancelButton);
        buttonPanel.Resize += (_, __) =>
        {
            _CancelButton.Location = new Point(buttonPanel.ClientSize.Width - 128, 13);
            _OKButton.Location = new Point(_CancelButton.Left - 118, 13);
        };

        AcceptButton = _OKButton;
        AutoScaleMode = AutoScaleMode.Font;
        BackColor = Color.White;
        CancelButton = _CancelButton;
        ClientSize = new Size(480, 208);
        Controls.Add(contentPanel);
        Controls.Add(buttonPanel);
        Controls.Add(_headerPanel);
        Font = DotNetCommander.DialogStyleService.CreateDialogFont();
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        Name = "InputBox";
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.CenterParent;
        Shown += (_, __) =>
        {
            _TextBox.Focus();
            _TextBox.SelectAll();
        };
    }

    private void InitializeInputBox(string prompt, string title, string defaultResponse, int xPos, int yPos)
    {
        Text = title ?? string.Empty;
        _Label.Text = prompt ?? string.Empty;
        _TextBox.Text = defaultResponse ?? string.Empty;

        Size preferredPromptSize = TextRenderer.MeasureText(
            _Label.Text,
            _Label.Font,
            new Size(ClientSize.Width - 36, 0),
            TextFormatFlags.WordBreak | TextFormatFlags.TextBoxControl);
        _headerPanel.Height = Math.Max(82, preferredPromptSize.Height + 32);
        ClientSize = new Size(ClientSize.Width, _headerPanel.Height + 126);

        if (xPos < 0 && yPos < 0)
        {
            StartPosition = FormStartPosition.CenterScreen;
            return;
        }

        StartPosition = FormStartPosition.Manual;
        DesktopLocation = new Point(xPos < 0 ? 600 : xPos, yPos < 0 ? 350 : yPos);
    }

    private void OKButton_Click(object sender, EventArgs e)
    {
        Output = _TextBox.Text;
        DialogResult = DialogResult.OK;
        Close();
    }

    private void CancelButton_Click(object sender, EventArgs e)
    {
        DialogResult = DialogResult.Cancel;
        Close();
    }
}
