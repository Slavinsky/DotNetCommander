using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace DotNetCommander
{
    internal sealed class FormNewFile : Form
    {
        private const int MinimumClientHeight = 360;
        private readonly string currentPath;
        private readonly NewFileEditorOption[] editorOptions;
        private readonly TextBox textFileName;
        private readonly ComboBox comboEditorType;
        private readonly TextBox textLocation;
        private readonly TextBox textPreview;
        private readonly Label labelDescription;
        private readonly Label labelFileName;
        private readonly Label labelEditorType;
        private readonly Label labelLocation;
        private readonly Label labelPreview;
        private readonly Label labelTitle;
        private readonly Panel panelButtons;
        private readonly Panel panelContent;
        private readonly Panel panelHeader;
        private readonly Button buttonCreate;
        private readonly Button buttonCancel;
        private NewFileEditorOption previousSelectedOption;

        public FormNewFile(string currentPath, string defaultFileName, NewFileEditorOption[] editorOptions)
        {
            this.currentPath = currentPath ?? string.Empty;
            this.editorOptions = editorOptions ?? Array.Empty<NewFileEditorOption>();
            Font = DialogStyleService.CreateDialogFont();

            SuspendLayout();

            panelHeader = new Panel
            {
                BackColor = Color.FromArgb(38, 84, 124),
                Dock = DockStyle.Top,
                Location = new Point(0, 0),
                Name = "panelHeader",
                Padding = new Padding(18, 16, 18, 12),
                Size = new Size(524, 84)
            };

            labelTitle = new Label
            {
                AutoSize = true,
                ForeColor = Color.White,
                Location = new Point(18, 16),
                Name = "labelTitle",
                Text = Language.getString("newFileInlineTitle")
            };

            labelDescription = new Label
            {
                ForeColor = Color.FromArgb(224, 236, 247),
                Location = new Point(21, 46),
                Name = "labelDescription",
                Text = Language.getString("newFileInlineDescription")
            };

            panelHeader.Controls.Add(labelDescription);
            panelHeader.Controls.Add(labelTitle);

            panelContent = new Panel
            {
                BackColor = Color.White,
                Dock = DockStyle.Fill,
                Location = new Point(0, 84),
                Name = "panelContent",
                Padding = new Padding(18, 16, 18, 8),
                Size = new Size(524, 218)
            };

            labelFileName = new Label
            {
                AutoSize = true,
                ForeColor = Color.FromArgb(88, 96, 105),
                Location = new Point(21, 16),
                Name = "labelFileName",
                Text = Language.getString("newFileName")
            };

            textFileName = new TextBox
            {
                BorderStyle = BorderStyle.FixedSingle,
                Location = new Point(24, 32),
                Name = "textFileName",
                Size = new Size(479, 20),
                Text = defaultFileName ?? string.Empty
            };
            textFileName.TextChanged += InputChanged;

            labelEditorType = new Label
            {
                AutoSize = true,
                ForeColor = Color.FromArgb(88, 96, 105),
                Location = new Point(21, 67),
                Name = "labelEditorType",
                Text = Language.getString("newFileType")
            };

            comboEditorType = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                FormattingEnabled = true,
                Location = new Point(24, 83),
                Name = "comboEditorType",
                Size = new Size(479, 21)
            };
            foreach (NewFileEditorOption option in this.editorOptions.Where(option => option != null))
            {
                comboEditorType.Items.Add(option);
            }

            labelLocation = new Label
            {
                AutoSize = true,
                ForeColor = Color.FromArgb(88, 96, 105),
                Location = new Point(21, 118),
                Name = "labelLocation",
                Text = Language.getString("newFileLocation")
            };

            textLocation = new TextBox
            {
                BackColor = Color.FromArgb(248, 250, 252),
                BorderStyle = BorderStyle.FixedSingle,
                Location = new Point(24, 134),
                Name = "textLocation",
                ReadOnly = true,
                Size = new Size(479, 20),
                Text = this.currentPath
            };

            labelPreview = new Label
            {
                AutoSize = true,
                ForeColor = Color.FromArgb(88, 96, 105),
                Location = new Point(21, 169),
                Name = "labelPreview",
                Text = Language.getString("newFileTarget")
            };

            textPreview = new TextBox
            {
                BackColor = Color.FromArgb(248, 250, 252),
                BorderStyle = BorderStyle.FixedSingle,
                Location = new Point(24, 185),
                Name = "textPreview",
                ReadOnly = true,
                Size = new Size(479, 20)
            };

            panelContent.Controls.Add(labelFileName);
            panelContent.Controls.Add(textFileName);
            panelContent.Controls.Add(labelEditorType);
            panelContent.Controls.Add(comboEditorType);
            panelContent.Controls.Add(labelLocation);
            panelContent.Controls.Add(textLocation);
            panelContent.Controls.Add(labelPreview);
            panelContent.Controls.Add(textPreview);

            panelButtons = new Panel
            {
                BackColor = Color.FromArgb(245, 247, 250),
                Dock = DockStyle.Bottom,
                Location = new Point(0, 302),
                Name = "panelButtons",
                Padding = new Padding(18, 10, 18, 10),
                Size = new Size(524, 58)
            };

            buttonCreate = new Button
            {
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
                BackColor = Color.FromArgb(38, 84, 124),
                FlatStyle = FlatStyle.Flat,
                ForeColor = Color.White,
                Location = new Point(293, 13),
                Name = "buttonCreate",
                Size = new Size(118, 38),
                TabIndex = 0,
                Text = Language.getString("newFileCreate"),
                UseVisualStyleBackColor = false
            };
            buttonCreate.FlatAppearance.BorderSize = 0;
            buttonCreate.Click += ButtonCreate_Click;

            buttonCancel = new Button
            {
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
                DialogResult = DialogResult.Cancel,
                FlatStyle = FlatStyle.Flat,
                Location = new Point(385, 13),
                Name = "buttonCancel",
                Size = new Size(118, 38),
                TabIndex = 1,
                Text = Language.getString("cancel"),
                UseVisualStyleBackColor = true
            };

            panelButtons.Controls.Add(buttonCancel);
            panelButtons.Controls.Add(buttonCreate);

            AcceptButton = buttonCreate;
            AutoScaleDimensions = new SizeF(6F, 13F);
            AutoScaleMode = AutoScaleMode.Font;
            BackColor = Color.White;
            CancelButton = buttonCancel;
            ClientSize = new Size(524, 360);
            Controls.Add(panelContent);
            Controls.Add(panelButtons);
            Controls.Add(panelHeader);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            Name = "FormNewFile";
            StartPosition = FormStartPosition.CenterParent;
            Text = Language.getString("newFileInlineTitle");
            ApplyDialogAppearance();
            panelContent.Resize += (_, __) => UpdateLayout();

            comboEditorType.SelectedIndexChanged += InputChanged;
            if (comboEditorType.Items.Count > 0)
            {
                comboEditorType.SelectedIndex = 0;
            }

            ResumeLayout(false);

            UpdatePreview();
            UpdateLayout();
            Shown += (_, __) =>
            {
                textFileName.Focus();
                SelectFileNamePart();
            };
        }

        public NewFileEditorOption SelectedOption => comboEditorType.SelectedItem as NewFileEditorOption;

        public string TargetPath { get; private set; }

        private void InputChanged(object sender, EventArgs e)
        {
            if (ReferenceEquals(sender, comboEditorType))
            {
                UpdateFileNameExtension();
            }

            UpdatePreview();
        }

        private void ButtonCreate_Click(object sender, EventArgs e)
        {
            string fileName = BuildFileName(out bool hasInvalidCharacters);
            if (string.IsNullOrWhiteSpace(fileName))
            {
                MessageBox.Show(this, Language.getString("newFileNameRequired"), Language.getString("Info"), MessageBoxButtons.OK, MessageBoxIcon.Information);
                textFileName.Focus();
                return;
            }

            if (hasInvalidCharacters)
            {
                MessageBox.Show(this, Language.getString("newFileNameInvalid"), Language.getString("Info"), MessageBoxButtons.OK, MessageBoxIcon.Information);
                textFileName.Focus();
                textFileName.SelectAll();
                return;
            }

            string filePath = Path.Combine(currentPath, fileName);

            try
            {
                string fullPath = Path.GetFullPath(filePath);
                TargetPath = fullPath;
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
            string fileName = BuildFileName(out _);
            textPreview.Text = string.IsNullOrWhiteSpace(fileName)
                ? currentPath
                : Path.Combine(currentPath, fileName);
        }

        private void ApplyDialogAppearance()
        {
            labelTitle.Font = DialogStyleService.CreateHeaderFont();
            labelDescription.Font = DialogStyleService.CreateBodyFont();
            labelFileName.Font = DialogStyleService.CreateCaptionFont();
            labelEditorType.Font = DialogStyleService.CreateCaptionFont();
            labelLocation.Font = DialogStyleService.CreateCaptionFont();
            labelPreview.Font = DialogStyleService.CreateCaptionFont();
            textFileName.Font = DialogStyleService.CreateBodyFont();
            comboEditorType.Font = DialogStyleService.CreateBodyFont();
            textLocation.Font = DialogStyleService.CreateBodyFont();
            textPreview.Font = DialogStyleService.CreateBodyFont();
            buttonCreate.Font = DialogStyleService.CreateBodyFont();
            buttonCancel.Font = DialogStyleService.CreateBodyFont();
        }

        private void UpdateLayout()
        {
            int left = 24;
            int width = Math.Max(320, panelContent.ClientSize.Width - 42);
            int top = 16;
            int labelGap = 6;
            int sectionGap = 14;
            int textBoxHeight = TextRenderer.MeasureText("Ag", textFileName.Font).Height + 10;
            int comboHeight = Math.Max(textBoxHeight, comboEditorType.PreferredSize.Height);

            labelTitle.Location = new Point(18, 16);
            labelDescription.Location = new Point(21, labelTitle.Bottom + 8);
            labelDescription.AutoSize = true;

            labelFileName.Location = new Point(21, top);
            top = labelFileName.Bottom + labelGap;
            textFileName.Location = new Point(left, top);
            textFileName.Size = new Size(width, textBoxHeight);

            top = textFileName.Bottom + sectionGap;
            labelEditorType.Location = new Point(21, top);
            top = labelEditorType.Bottom + labelGap;
            comboEditorType.Location = new Point(left, top);
            comboEditorType.Size = new Size(width, comboHeight);

            top = comboEditorType.Bottom + sectionGap;
            labelLocation.Location = new Point(21, top);
            top = labelLocation.Bottom + labelGap;
            textLocation.Location = new Point(left, top);
            textLocation.Size = new Size(width, textBoxHeight);

            top = textLocation.Bottom + sectionGap;
            labelPreview.Location = new Point(21, top);
            top = labelPreview.Bottom + labelGap;
            textPreview.Location = new Point(left, top);
            textPreview.Size = new Size(width, textBoxHeight);

            int contentBottom = textPreview.Bottom + 16;
            int desiredHeight = Math.Max(MinimumClientHeight, panelHeader.Height + panelButtons.Height + contentBottom + 12);
            if (ClientSize.Height != desiredHeight)
            {
                ClientSize = new Size(ClientSize.Width, desiredHeight);
            }
        }

        private string BuildFileName(out bool hasInvalidCharacters)
        {
            hasInvalidCharacters = false;
            string fileName = (textFileName.Text ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(fileName))
            {
                return string.Empty;
            }

            foreach (char invalidChar in Path.GetInvalidFileNameChars())
            {
                if (fileName.IndexOf(invalidChar) >= 0)
                {
                    hasInvalidCharacters = true;
                    return fileName;
                }
            }

            if (Path.HasExtension(fileName))
            {
                return fileName;
            }

            string defaultExtension = SelectedOption?.DefaultExtension;
            if (string.IsNullOrWhiteSpace(defaultExtension))
            {
                return fileName;
            }

            return fileName + defaultExtension;
        }

        private void UpdateFileNameExtension()
        {
            NewFileEditorOption currentOption = SelectedOption;
            if (currentOption == null)
            {
                previousSelectedOption = null;
                return;
            }

            string fileName = (textFileName.Text ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(fileName))
            {
                previousSelectedOption = currentOption;
                return;
            }

            string extension = Path.GetExtension(fileName);
            if (string.IsNullOrWhiteSpace(extension))
            {
                textFileName.Text = fileName + currentOption.DefaultExtension;
                textFileName.SelectionStart = textFileName.TextLength;
                previousSelectedOption = currentOption;
                return;
            }

            if (ShouldReplaceExtension(extension))
            {
                string fileNameWithoutExtension = Path.GetFileNameWithoutExtension(fileName);
                textFileName.Text = fileNameWithoutExtension + currentOption.DefaultExtension;
                textFileName.SelectionStart = textFileName.TextLength;
            }

            previousSelectedOption = currentOption;
        }

        private bool ShouldReplaceExtension(string extension)
        {
            if (previousSelectedOption != null &&
                string.Equals(extension, previousSelectedOption.DefaultExtension, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            foreach (NewFileEditorOption option in editorOptions)
            {
                if (option != null &&
                    string.Equals(extension, option.DefaultExtension, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private void SelectFileNamePart()
        {
            string fileName = textFileName.Text ?? string.Empty;
            string extension = Path.GetExtension(fileName);
            if (string.IsNullOrEmpty(extension))
            {
                textFileName.SelectAll();
                return;
            }

            int selectionLength = Math.Max(0, fileName.Length - extension.Length);
            textFileName.SelectionStart = 0;
            textFileName.SelectionLength = selectionLength;
        }
    }
}
