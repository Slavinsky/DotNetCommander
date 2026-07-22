using System;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Windows.Forms;

namespace DotNetCommander
{
    internal enum FileConflictDialogChoice
    {
        Cancel,
        Overwrite,
        Skip,
        Rename,
        OverwriteAll,
        SkipAll
    }

    internal sealed class FormFileConflict : Form
    {
        public FormFileConflict(FormCopy.Type operationType, string sourcePath, string destinationPath)
        {
            Text = Language.getString("conflictDialogTitle");
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MinimizeBox = false;
            MaximizeBox = false;
            ShowInTaskbar = false;
            Font = DialogStyleService.CreateDialogFont();
            ClientSize = new Size(760, 396);

            Panel headerPanel = new Panel
            {
                Dock = DockStyle.Top,
                Height = 92,
                BackColor = Color.FromArgb(138, 61, 52)
            };

            Label titleLabel = new Label
            {
                Dock = DockStyle.Top,
                Height = 38,
                Padding = new Padding(18, 16, 18, 0),
                Font = DialogStyleService.CreateHeaderFont(),
                ForeColor = Color.White,
                Text = Language.getString("conflictDialogTitle")
            };

            Label descriptionLabel = new Label
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(18, 2, 18, 12),
                Font = DialogStyleService.CreateBodyFont(),
                ForeColor = Color.FromArgb(244, 229, 226),
                Text = operationType == FormCopy.Type.Copy
                    ? Language.getString("conflictDialogCopyDescription")
                    : Language.getString("conflictDialogMoveDescription")
            };

            headerPanel.Controls.Add(descriptionLabel);
            headerPanel.Controls.Add(titleLabel);

            Panel buttonPanel = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 112,
                Padding = new Padding(12),
                BackColor = Color.FromArgb(245, 247, 250)
            };

            TableLayoutPanel buttonLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 3,
                RowCount = 2
            };
            buttonLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.33f));
            buttonLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.33f));
            buttonLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.34f));
            buttonLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 50f));
            buttonLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 50f));

            buttonLayout.Controls.Add(CreateButton(Language.getString("conflictOverwrite"), FileConflictDialogChoice.Overwrite), 0, 0);
            buttonLayout.Controls.Add(CreateButton(Language.getString("conflictSkip"), FileConflictDialogChoice.Skip), 1, 0);
            buttonLayout.Controls.Add(CreateButton(Language.getString("conflictRename"), FileConflictDialogChoice.Rename), 2, 0);
            buttonLayout.Controls.Add(CreateButton(Language.getString("conflictOverwriteAll"), FileConflictDialogChoice.OverwriteAll), 0, 1);
            buttonLayout.Controls.Add(CreateButton(Language.getString("conflictSkipAll"), FileConflictDialogChoice.SkipAll), 1, 1);
            buttonLayout.Controls.Add(CreateButton(Language.getString("cancel"), FileConflictDialogChoice.Cancel), 2, 1);
            buttonPanel.Controls.Add(buttonLayout);

            TableLayoutPanel contentLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 2,
                Padding = new Padding(18, 16, 18, 12),
                BackColor = Color.White
            };
            contentLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 50f));
            contentLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 50f));

            FileInfo sourceInfo = TryGetFileInfo(sourcePath);
            FileInfo destinationInfo = TryGetFileInfo(destinationPath);

            contentLayout.Controls.Add(CreateFileSection(
                Language.getString("sourcePath"),
                sourcePath,
                BuildFileDetails(sourceInfo, destinationInfo)),
                0,
                0);

            contentLayout.Controls.Add(CreateFileSection(
                Language.getString("destinationPath"),
                destinationPath,
                BuildFileDetails(destinationInfo, sourceInfo)),
                0,
                1);

            Controls.Add(contentLayout);
            Controls.Add(buttonPanel);
            Controls.Add(headerPanel);

            SelectedChoice = FileConflictDialogChoice.Cancel;
        }

        public FileConflictDialogChoice SelectedChoice { get; private set; }

        private Control CreateFileSection(string caption, string path, string details)
        {
            Panel sectionPanel = new Panel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(0, 0, 0, 12)
            };

            Label captionLabel = new Label
            {
                Dock = DockStyle.Top,
                Height = 22,
                ForeColor = Color.FromArgb(88, 96, 105),
                Text = caption
            };

            TextBox pathTextBox = new TextBox
            {
                Dock = DockStyle.Top,
                Height = 58,
                Margin = new Padding(0),
                Multiline = true,
                ReadOnly = true,
                ScrollBars = ScrollBars.Vertical,
                BorderStyle = BorderStyle.FixedSingle,
                BackColor = Color.FromArgb(248, 250, 252),
                Text = string.IsNullOrWhiteSpace(path) ? string.Empty : path
            };

            Label detailsLabel = new Label
            {
                Dock = DockStyle.Top,
                Height = 46,
                Padding = new Padding(0, 8, 0, 0),
                ForeColor = Color.FromArgb(88, 96, 105),
                Text = details
            };

            sectionPanel.Controls.Add(detailsLabel);
            sectionPanel.Controls.Add(pathTextBox);
            sectionPanel.Controls.Add(captionLabel);
            return sectionPanel;
        }

        private string BuildFileDetails(FileInfo fileInfo, FileInfo otherFileInfo)
        {
            if (fileInfo == null)
            {
                return Language.getString("conflictFileDetailsUnavailable");
            }

            string details = string.Format(
                CultureInfo.CurrentUICulture,
                Language.getString("conflictFileDetailsFormat"),
                FormatBytes(fileInfo.Length),
                fileInfo.LastWriteTime.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.CurrentUICulture));

            string comparison = BuildComparisonText(fileInfo, otherFileInfo);
            return string.IsNullOrWhiteSpace(comparison)
                ? details
                : details + Environment.NewLine + comparison;
        }

        private string BuildComparisonText(FileInfo fileInfo, FileInfo otherFileInfo)
        {
            if (fileInfo == null || otherFileInfo == null)
            {
                return string.Empty;
            }

            string sizeComparison = string.Empty;
            if (fileInfo.Length > otherFileInfo.Length)
            {
                sizeComparison = string.Format(CultureInfo.CurrentUICulture, Language.getString("conflictComparedWithFormat"), Language.getString("conflictSizeLargerSuffix"));
            }
            else if (fileInfo.Length < otherFileInfo.Length)
            {
                sizeComparison = string.Format(CultureInfo.CurrentUICulture, Language.getString("conflictComparedWithFormat"), Language.getString("conflictSizeSmallerSuffix"));
            }

            string timeComparison = string.Empty;
            if (fileInfo.LastWriteTime > otherFileInfo.LastWriteTime)
            {
                timeComparison = string.Format(CultureInfo.CurrentUICulture, Language.getString("conflictComparedWithFormat"), Language.getString("conflictSizeNewerSuffix"));
            }
            else if (fileInfo.LastWriteTime < otherFileInfo.LastWriteTime)
            {
                timeComparison = string.Format(CultureInfo.CurrentUICulture, Language.getString("conflictComparedWithFormat"), Language.getString("conflictSizeOlderSuffix"));
            }

            if (!string.IsNullOrWhiteSpace(sizeComparison) && !string.IsNullOrWhiteSpace(timeComparison))
            {
                return sizeComparison + " | " + timeComparison;
            }

            return sizeComparison + timeComparison;
        }

        private static FileInfo TryGetFileInfo(string path)
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            {
                return null;
            }

            try
            {
                return new FileInfo(path);
            }
            catch
            {
                return null;
            }
        }

        private static string FormatBytes(long value)
        {
            string[] units = { "B", "KB", "MB", "GB", "TB" };
            double size = value;
            int unitIndex = 0;
            while (size >= 1024 && unitIndex < units.Length - 1)
            {
                size /= 1024;
                unitIndex++;
            }

            return string.Format(CultureInfo.CurrentUICulture, "{0:0.##} {1}", size, units[unitIndex]);
        }

        private Button CreateButton(string text, FileConflictDialogChoice choice)
        {
            Button button = new Button
            {
                Dock = DockStyle.Fill,
                Margin = new Padding(6),
                MinimumSize = new Size(140, 40),
                Text = text,
                Tag = choice
            };
            button.Click += Button_Click;
            return button;
        }

        private void Button_Click(object sender, EventArgs e)
        {
            if (sender is Button button && button.Tag is FileConflictDialogChoice choice)
            {
                SelectedChoice = choice;
                DialogResult = choice == FileConflictDialogChoice.Cancel ? DialogResult.Cancel : DialogResult.OK;
                Close();
            }
        }
    }
}
