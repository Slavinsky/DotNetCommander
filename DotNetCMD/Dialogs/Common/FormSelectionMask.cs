using System.Drawing;
using System.Windows.Forms;

namespace DotNetCommander
{
    internal sealed class FormSelectionMask : Form
    {
        private readonly TextBox maskTextBox;
        private readonly Button applyButton;

        public FormSelectionMask(bool selectMatches)
        {
            Text = Language.getString(selectMatches ? "selectMaskTitle" : "unselectMaskTitle");
            Font = DialogStyleService.CreateDialogFont();
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.CenterParent;
            ClientSize = new Size(480, 154);

            var layout = new TableLayoutPanel
            {
                ColumnCount = 1,
                Dock = DockStyle.Fill,
                Padding = new Padding(16),
                RowCount = 3
            };
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            var prompt = new Label
            {
                AutoSize = false,
                Dock = DockStyle.Fill,
                Height = 38,
                Text = Language.getString("selectionMaskPrompt")
            };
            maskTextBox = new TextBox
            {
                Dock = DockStyle.Top,
                MaxLength = 256,
                Text = "*"
            };
            maskTextBox.TextChanged += (_, __) => applyButton.Enabled = !string.IsNullOrWhiteSpace(maskTextBox.Text);

            var buttons = new FlowLayoutPanel
            {
                AutoSize = true,
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.RightToLeft,
                WrapContents = false
            };
            applyButton = new Button
            {
                AutoSize = true,
                MinimumSize = new Size(110, 34),
                Text = Language.getString(selectMatches ? "selectMaskApply" : "unselectMaskApply")
            };
            applyButton.Click += (_, __) => DialogResult = DialogResult.OK;
            var cancelButton = new Button
            {
                AutoSize = true,
                DialogResult = DialogResult.Cancel,
                MinimumSize = new Size(100, 34),
                Text = Language.getString("cancel")
            };
            buttons.Controls.Add(applyButton);
            buttons.Controls.Add(cancelButton);

            layout.Controls.Add(prompt, 0, 0);
            layout.Controls.Add(maskTextBox, 0, 1);
            layout.Controls.Add(buttons, 0, 2);
            Controls.Add(layout);
            AcceptButton = applyButton;
            CancelButton = cancelButton;
            Shown += (_, __) =>
            {
                maskTextBox.Focus();
                maskTextBox.SelectAll();
            };
        }

        public string Mask => maskTextBox.Text.Trim();
    }
}
