using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace DotNetCommander
{
    internal sealed class FormNewFolder : Form
    {
        private const int MinimumClientHeight = 352;
        private readonly string currentPath;
        private readonly TextBox textFolderPath;
        private readonly TextBox textLocation;
        private readonly TextBox textPreview;
        private readonly CheckBox checkOpenCreatedFolder;
        private readonly Label labelDescription;
        private readonly Label labelTitle;
        private readonly Label labelFolderPath;
        private readonly Label labelLocation;
        private readonly Label labelPreview;
        private readonly Panel panelHeader;
        private readonly Panel panelContent;
        private readonly Panel panelButtons;
        private readonly Button buttonCreate;
        private readonly Button buttonCancel;

        public FormNewFolder(string currentPath, string defaultRelativePath)
        {
            this.currentPath = currentPath ?? string.Empty;
            Font = DialogStyleService.CreateDialogFont();

            SuspendLayout();

            panelHeader = new Panel
            {
                BackColor = Color.FromArgb(55, 93, 129),
                Dock = DockStyle.Top,
                Padding = new Padding(18, 16, 18, 12),
                Size = new Size(560, 88)
            };

            labelTitle = new Label
            {
                AutoSize = true,
                ForeColor = Color.White,
                Location = new Point(18, 16),
                Text = Language.getString("newFolderDialogTitle")
            };

            labelDescription = new Label
            {
                ForeColor = Color.FromArgb(224, 236, 247),
                Location = new Point(21, 49),
                Text = Language.getString("newFolderDialogDescription")
            };

            panelHeader.Controls.Add(labelDescription);
            panelHeader.Controls.Add(labelTitle);

            panelContent = new Panel
            {
                BackColor = Color.White,
                Dock = DockStyle.Fill,
                Padding = new Padding(18, 16, 18, 8)
            };

            labelFolderPath = new Label
            {
                AutoSize = true,
                ForeColor = Color.FromArgb(88, 96, 105),
                Location = new Point(21, 16),
                Text = Language.getString("newFolderRelativePath")
            };

            textFolderPath = new TextBox
            {
                BorderStyle = BorderStyle.FixedSingle,
                Location = new Point(24, 34),
                Size = new Size(512, 25),
                Text = defaultRelativePath ?? string.Empty
            };
            textFolderPath.TextChanged += InputChanged;

            labelLocation = new Label
            {
                AutoSize = true,
                ForeColor = Color.FromArgb(88, 96, 105),
                Location = new Point(21, 72),
                Text = Language.getString("newFolderLocation")
            };

            textLocation = new TextBox
            {
                BackColor = Color.FromArgb(248, 250, 252),
                BorderStyle = BorderStyle.FixedSingle,
                Location = new Point(24, 90),
                ReadOnly = true,
                Size = new Size(512, 25),
                Text = this.currentPath
            };

            labelPreview = new Label
            {
                AutoSize = true,
                ForeColor = Color.FromArgb(88, 96, 105),
                Location = new Point(21, 128),
                Text = Language.getString("newFolderTarget")
            };

            textPreview = new TextBox
            {
                BackColor = Color.FromArgb(248, 250, 252),
                BorderStyle = BorderStyle.FixedSingle,
                Location = new Point(24, 146),
                ReadOnly = true,
                Size = new Size(512, 25)
            };

            checkOpenCreatedFolder = new CheckBox
            {
                AutoSize = true,
                Location = new Point(24, 188),
                Text = Language.getString("newFolderOpenCreated")
            };

            panelContent.Controls.Add(labelFolderPath);
            panelContent.Controls.Add(textFolderPath);
            panelContent.Controls.Add(labelLocation);
            panelContent.Controls.Add(textLocation);
            panelContent.Controls.Add(labelPreview);
            panelContent.Controls.Add(textPreview);
            panelContent.Controls.Add(checkOpenCreatedFolder);

            panelButtons = new Panel
            {
                BackColor = Color.FromArgb(245, 247, 250),
                Dock = DockStyle.Bottom,
                Padding = new Padding(18, 10, 18, 10),
                Size = new Size(560, 64)
            };

            buttonCreate = new Button
            {
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
                BackColor = Color.FromArgb(55, 93, 129),
                FlatStyle = FlatStyle.Flat,
                ForeColor = Color.White,
                Location = new Point(300, 13),
                Size = new Size(118, 38),
                Text = Language.getString("newFolderCreate"),
                UseVisualStyleBackColor = false
            };
            buttonCreate.FlatAppearance.BorderSize = 0;
            buttonCreate.Click += ButtonCreate_Click;

            buttonCancel = new Button
            {
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
                DialogResult = DialogResult.Cancel,
                FlatStyle = FlatStyle.Flat,
                Location = new Point(424, 13),
                Size = new Size(118, 38),
                Text = Language.getString("cancel"),
                UseVisualStyleBackColor = true
            };

            panelButtons.Controls.Add(buttonCancel);
            panelButtons.Controls.Add(buttonCreate);

            AcceptButton = buttonCreate;
            CancelButton = buttonCancel;
            AutoScaleMode = AutoScaleMode.Font;
            BackColor = Color.White;
            ClientSize = new Size(560, 352);
            Controls.Add(panelContent);
            Controls.Add(panelButtons);
            Controls.Add(panelHeader);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            StartPosition = FormStartPosition.CenterParent;
            Text = Language.getString("newFolderDialogTitle");
            ApplyDialogAppearance();
            panelContent.Resize += (_, __) => UpdateLayout();

            ResumeLayout(false);

            UpdatePreview();
            UpdateLayout();
            Shown += (_, __) =>
            {
                textFolderPath.Focus();
                textFolderPath.SelectAll();
            };
        }

        public string TargetPath { get; private set; }

        public bool OpenCreatedFolder => checkOpenCreatedFolder.Checked;

        public string RelativePath => (textFolderPath.Text ?? string.Empty).Trim();

        private void InputChanged(object sender, EventArgs e)
        {
            UpdatePreview();
        }

        private void ButtonCreate_Click(object sender, EventArgs e)
        {
            string relativePath = NormalizeRelativePath(RelativePath, out bool hasInvalidCharacters);
            if (string.IsNullOrWhiteSpace(relativePath))
            {
                MessageBox.Show(this, Language.getString("newFolderNameRequired"), Language.getString("Info"), MessageBoxButtons.OK, MessageBoxIcon.Information);
                textFolderPath.Focus();
                return;
            }

            if (hasInvalidCharacters)
            {
                MessageBox.Show(this, Language.getString("newFolderNameInvalid"), Language.getString("Info"), MessageBoxButtons.OK, MessageBoxIcon.Information);
                textFolderPath.Focus();
                textFolderPath.SelectAll();
                return;
            }

            string previewPath = Path.Combine(currentPath, relativePath);
            try
            {
                TargetPath = Path.GetFullPath(previewPath);
                DialogResult = DialogResult.OK;
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, Language.getString("error"), MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void UpdatePreview()
        {
            string relativePath = NormalizeRelativePath(RelativePath, out _);
            textPreview.Text = string.IsNullOrWhiteSpace(relativePath)
                ? currentPath
                : Path.Combine(currentPath, relativePath);
        }

        private void ApplyDialogAppearance()
        {
            labelTitle.Font = DialogStyleService.CreateHeaderFont();
            labelDescription.Font = DialogStyleService.CreateBodyFont();
            labelFolderPath.Font = DialogStyleService.CreateCaptionFont();
            labelLocation.Font = DialogStyleService.CreateCaptionFont();
            labelPreview.Font = DialogStyleService.CreateCaptionFont();
            textFolderPath.Font = DialogStyleService.CreateBodyFont();
            textLocation.Font = DialogStyleService.CreateBodyFont();
            textPreview.Font = DialogStyleService.CreateBodyFont();
            checkOpenCreatedFolder.Font = DialogStyleService.CreateBodyFont();
            buttonCreate.Font = DialogStyleService.CreateBodyFont();
            buttonCancel.Font = DialogStyleService.CreateBodyFont();
        }

        private void UpdateLayout()
        {
            int left = 24;
            int width = Math.Max(340, panelContent.ClientSize.Width - 48);
            int top = 16;
            int labelGap = 6;
            int sectionGap = 14;
            int textBoxHeight = TextRenderer.MeasureText("Ag", textFolderPath.Font).Height + 10;

            labelTitle.Location = new Point(18, 16);
            labelDescription.Location = new Point(21, labelTitle.Bottom + 8);
            labelDescription.AutoSize = true;

            labelFolderPath.Location = new Point(21, top);
            top = labelFolderPath.Bottom + labelGap;
            textFolderPath.Location = new Point(left, top);
            textFolderPath.Size = new Size(width, textBoxHeight);

            top = textFolderPath.Bottom + sectionGap;
            labelLocation.Location = new Point(21, top);
            top = labelLocation.Bottom + labelGap;
            textLocation.Location = new Point(left, top);
            textLocation.Size = new Size(width, textBoxHeight);

            top = textLocation.Bottom + sectionGap;
            labelPreview.Location = new Point(21, top);
            top = labelPreview.Bottom + labelGap;
            textPreview.Location = new Point(left, top);
            textPreview.Size = new Size(width, textBoxHeight);

            top = textPreview.Bottom + 16;
            checkOpenCreatedFolder.Location = new Point(left, top);

            int contentBottom = checkOpenCreatedFolder.Bottom + 16;
            int desiredHeight = Math.Max(MinimumClientHeight, panelHeader.Height + panelButtons.Height + contentBottom + 12);
            if (ClientSize.Height != desiredHeight)
            {
                ClientSize = new Size(ClientSize.Width, desiredHeight);
            }
        }

        private static string NormalizeRelativePath(string input, out bool hasInvalidCharacters)
        {
            hasInvalidCharacters = false;
            string[] segments = (input ?? string.Empty)
                .Split(new[] { '\\', '/' }, StringSplitOptions.RemoveEmptyEntries);

            if (segments.Length == 0)
            {
                return string.Empty;
            }

            foreach (string segment in segments)
            {
                if (segment == "." || segment == "..")
                {
                    hasInvalidCharacters = true;
                    return string.Empty;
                }

                if (segment.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
                {
                    hasInvalidCharacters = true;
                    return string.Empty;
                }
            }

            return Path.Combine(segments);
        }
    }
}
