using System;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace DotNetCommander
{
    internal enum ArchiveOperationMode
    {
        Create,
        Extract
    }

    internal sealed class FormArchiveOperation : Form
    {
        private readonly ArchiveOperationMode mode;
        private readonly string[] sources;
        private readonly string[] archiveEntries;
        private readonly string archiveEntryRoot;
        private readonly Label descriptionLabel;
        private readonly Label progressValueLabel;
        private readonly Label currentItemValueLabel;
        private readonly Label transferValueLabel;
        private readonly ProgressBar progressBar;
        private readonly CheckBox overwriteCheckBox;
        private readonly TextBox destinationTextBox;
        private readonly ComboBox formatComboBox;
        private readonly Button browseButton;
        private readonly Button actionButton;
        private readonly Button cancelButton;

        private CancellationTokenSource cancellation;
        private bool isRunning;
        private bool allowClose;

        public FormArchiveOperation(
            ArchiveOperationMode mode,
            string[] sources,
            string destination,
            string[] archiveEntries = null,
            string archiveEntryRoot = null)
        {
            this.mode = mode;
            this.sources = sources ?? Array.Empty<string>();
            this.archiveEntries = archiveEntries;
            this.archiveEntryRoot = archiveEntryRoot;

            string title = Language.getString(mode == ArchiveOperationMode.Create
                ? "archiveCreateTitle"
                : "archiveExtractTitle");

            Font = DialogStyleService.CreateDialogFont();
            Text = title;
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MinimizeBox = false;
            MaximizeBox = false;
            ShowInTaskbar = false;
            BackColor = Color.White;
            ClientSize = new Size(620, 540);

            var headerPanel = new Panel
            {
                BackColor = Color.FromArgb(55, 93, 129),
                Dock = DockStyle.Top,
                Height = 92
            };
            var titleLabel = new Label
            {
                Dock = DockStyle.Top,
                Font = DialogStyleService.CreateHeaderFont(),
                ForeColor = Color.White,
                Height = 44,
                Padding = new Padding(18, 14, 18, 0),
                Text = title
            };
            descriptionLabel = new Label
            {
                Dock = DockStyle.Fill,
                Font = DialogStyleService.CreateBodyFont(),
                ForeColor = Color.FromArgb(224, 236, 247),
                Padding = new Padding(18, 3, 18, 10),
                Text = Language.getString(mode == ArchiveOperationMode.Create
                    ? "archiveCreateDescription"
                    : "archiveExtractDescription")
            };
            headerPanel.Controls.Add(descriptionLabel);
            headerPanel.Controls.Add(titleLabel);

            var contentPanel = new Panel
            {
                BackColor = Color.White,
                Dock = DockStyle.Fill,
                Padding = new Padding(22, 16, 22, 12)
            };

            Label sourceCaption = CreateCaption(Language.getString("archiveSource"), 16);
            var sourceValue = new TextBox
            {
                BackColor = Color.FromArgb(248, 250, 252),
                BorderStyle = BorderStyle.FixedSingle,
                Location = new Point(24, 38),
                ReadOnly = true,
                Size = new Size(572, 25),
                Text = BuildSourceSummary(this.sources)
            };

            Label destinationCaption = CreateCaption(Language.getString("archiveDestination"), 76);
            destinationTextBox = new TextBox
            {
                BackColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle,
                Location = new Point(24, 98),
                Size = new Size(454, 25),
                Text = destination ?? string.Empty
            };

            browseButton = new Button
            {
                FlatStyle = FlatStyle.Flat,
                Location = new Point(486, 94),
                Size = new Size(110, 33),
                Text = Language.getString("browse"),
                UseVisualStyleBackColor = true
            };
            browseButton.Click += BrowseButton_Click;

            Label formatCaption = CreateCaption(Language.getString("archiveFormat"), 142);
            formatCaption.Visible = mode == ArchiveOperationMode.Create;
            formatComboBox = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                Location = new Point(24, 164),
                Size = new Size(260, 28),
                Visible = mode == ArchiveOperationMode.Create
            };
            formatComboBox.Items.Add(Language.getString("archiveFormatZip"));
            formatComboBox.Items.Add(Language.getString("archiveFormatTar"));
            formatComboBox.Items.Add(Language.getString("archiveFormatTarGZip"));
            formatComboBox.SelectedIndex = GetFormatIndex(destinationTextBox.Text);
            formatComboBox.SelectedIndexChanged += FormatComboBox_SelectedIndexChanged;

            overwriteCheckBox = new CheckBox
            {
                AutoSize = true,
                Location = new Point(24, 164),
                Text = Language.getString("archiveOverwriteExisting"),
                Visible = mode == ArchiveOperationMode.Extract
            };

            Label progressCaption = CreateCaption(Language.getString("operationProgress"), 208);
            progressValueLabel = CreateValue(Language.getString("operationWaiting"), 228);
            progressBar = new ProgressBar
            {
                Location = new Point(24, 252),
                Size = new Size(572, 20),
                Style = ProgressBarStyle.Continuous
            };

            Label currentCaption = CreateCaption(Language.getString("operationCurrentItem"), 286);
            currentItemValueLabel = CreateValue(string.Empty, 306);
            Label transferCaption = CreateCaption(Language.getString("operationTransferred"), 334);
            transferValueLabel = CreateValue(string.Empty, 354);

            contentPanel.Controls.Add(sourceCaption);
            contentPanel.Controls.Add(sourceValue);
            contentPanel.Controls.Add(destinationCaption);
            contentPanel.Controls.Add(destinationTextBox);
            contentPanel.Controls.Add(browseButton);
            contentPanel.Controls.Add(formatCaption);
            contentPanel.Controls.Add(formatComboBox);
            contentPanel.Controls.Add(overwriteCheckBox);
            contentPanel.Controls.Add(progressCaption);
            contentPanel.Controls.Add(progressValueLabel);
            contentPanel.Controls.Add(progressBar);
            contentPanel.Controls.Add(currentCaption);
            contentPanel.Controls.Add(currentItemValueLabel);
            contentPanel.Controls.Add(transferCaption);
            contentPanel.Controls.Add(transferValueLabel);

            var buttonPanel = new Panel
            {
                BackColor = Color.FromArgb(245, 247, 250),
                Dock = DockStyle.Bottom,
                Height = 64
            };
            actionButton = new Button
            {
                BackColor = Color.FromArgb(55, 93, 129),
                FlatStyle = FlatStyle.Flat,
                ForeColor = Color.White,
                Location = new Point(374, 13),
                Size = new Size(110, 38),
                Text = Language.getString(mode == ArchiveOperationMode.Create ? "archiveCreate" : "archiveExtract"),
                UseVisualStyleBackColor = false
            };
            actionButton.FlatAppearance.BorderSize = 0;
            actionButton.Click += async (_, __) => await StartOperationAsync();

            cancelButton = new Button
            {
                DialogResult = DialogResult.Cancel,
                FlatStyle = FlatStyle.Flat,
                Location = new Point(490, 13),
                Size = new Size(110, 38),
                Text = Language.getString("cancel"),
                UseVisualStyleBackColor = true
            };
            cancelButton.Click += CancelButton_Click;
            buttonPanel.Controls.Add(actionButton);
            buttonPanel.Controls.Add(cancelButton);

            AcceptButton = actionButton;
            CancelButton = cancelButton;
            Controls.Add(contentPanel);
            Controls.Add(buttonPanel);
            Controls.Add(headerPanel);
        }

        public ArchiveOperationResult OperationResult { get; private set; }
        public string DestinationPath => destinationTextBox.Text.Trim();

        private Label CreateCaption(string text, int top)
        {
            return new Label
            {
                AutoSize = true,
                Font = DialogStyleService.CreateCaptionFont(),
                ForeColor = Color.FromArgb(88, 96, 105),
                Location = new Point(21, top),
                Text = text
            };
        }

        private Label CreateValue(string text, int top)
        {
            return new Label
            {
                AutoEllipsis = true,
                Font = DialogStyleService.CreateEmphasisFont(),
                ForeColor = Color.FromArgb(51, 61, 71),
                Location = new Point(21, top),
                Size = new Size(575, 20),
                Text = text
            };
        }

        private async Task StartOperationAsync()
        {
            if (isRunning)
            {
                return;
            }

            string destinationPath = ResolveDestinationPath();
            if (string.IsNullOrWhiteSpace(destinationPath))
            {
                MessageBox.Show(
                    this,
                    Language.getString("archiveDestinationRequired"),
                    Language.getString("Info"),
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
                destinationTextBox.Focus();
                return;
            }

            destinationTextBox.Text = destinationPath;
            if (mode == ArchiveOperationMode.Create &&
                File.Exists(destinationPath) &&
                MessageBox.Show(
                    this,
                    Language.getString("archiveOverwriteConfirm"),
                    Language.getString("archiveCreateTitle"),
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Warning) != DialogResult.Yes)
            {
                return;
            }

            isRunning = true;
            allowClose = false;
            cancellation = new CancellationTokenSource();
            actionButton.Enabled = false;
            overwriteCheckBox.Enabled = false;
            destinationTextBox.Enabled = false;
            formatComboBox.Enabled = false;
            browseButton.Enabled = false;
            cancelButton.Enabled = true;
            progressBar.Style = ProgressBarStyle.Marquee;
            progressValueLabel.Text = Language.getString("operationPreparing");

            var progress = new Progress<FileOperationProgressInfo>(UpdateProgress);
            try
            {
                OperationResult = mode == ArchiveOperationMode.Create
                    ? await ArchiveService.CreateArchiveAsync(sources, destinationPath, progress, cancellation.Token)
                    : archiveEntries != null && archiveEntries.Length > 0
                        ? await ArchiveService.ExtractArchiveEntriesAsync(
                            sources[0],
                            destinationPath,
                            overwriteCheckBox.Checked,
                            archiveEntries,
                            archiveEntryRoot,
                            progress,
                            cancellation.Token)
                        : await ArchiveService.ExtractArchiveAsync(
                            sources[0],
                            destinationPath,
                            overwriteCheckBox.Checked,
                            progress,
                            cancellation.Token);

                allowClose = true;
                DialogResult = OperationResult.RunResult == FileOperationRunResult.Completed
                    ? DialogResult.OK
                    : DialogResult.Cancel;
                Close();
            }
            catch (OperationCanceledException)
            {
                allowClose = true;
                DialogResult = DialogResult.Cancel;
                Close();
            }
            catch (Exception ex)
            {
                LogService.LogException("FormArchiveOperation.StartOperationAsync", ex);
                progressBar.Style = ProgressBarStyle.Continuous;
                progressBar.Value = 0;
                actionButton.Enabled = true;
                overwriteCheckBox.Enabled = true;
                destinationTextBox.Enabled = true;
                formatComboBox.Enabled = true;
                browseButton.Enabled = true;
                cancelButton.Enabled = true;
                descriptionLabel.Text = Language.getString(mode == ArchiveOperationMode.Create
                    ? "archiveCreateFailed"
                    : "archiveExtractFailed");
                MessageBox.Show(this, ex.Message, Language.getString("error"), MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                isRunning = false;
                cancellation?.Dispose();
                cancellation = null;
            }
        }

        private void BrowseButton_Click(object sender, EventArgs e)
        {
            if (mode == ArchiveOperationMode.Create)
            {
                BrowseForArchivePath();
                return;
            }

            using var folderDialog = new FolderBrowserDialog
            {
                Description = Language.getString("archiveChooseDestination"),
                InitialDirectory = Directory.Exists(destinationTextBox.Text) ? destinationTextBox.Text : string.Empty,
                ShowNewFolderButton = true,
                UseDescriptionForTitle = true
            };
            if (folderDialog.ShowDialog(this) == DialogResult.OK)
            {
                destinationTextBox.Text = folderDialog.SelectedPath;
            }
        }

        private void BrowseForArchivePath()
        {
            string currentPath = destinationTextBox.Text.Trim();
            string initialDirectory = Path.GetDirectoryName(currentPath);
            using var saveDialog = new SaveFileDialog
            {
                AddExtension = true,
                CheckPathExists = true,
                DefaultExt = GetSelectedExtension().TrimStart('.'),
                FileName = Path.GetFileName(currentPath),
                Filter = Language.getString("archiveSaveFilter"),
                FilterIndex = formatComboBox.SelectedIndex + 1,
                InitialDirectory = Directory.Exists(initialDirectory) ? initialDirectory : string.Empty,
                OverwritePrompt = true,
                RestoreDirectory = true,
                Title = Language.getString("archiveCreateTitle")
            };

            if (saveDialog.ShowDialog(this) == DialogResult.OK)
            {
                destinationTextBox.Text = saveDialog.FileName;
                formatComboBox.SelectedIndex = GetFormatIndex(saveDialog.FileName);
            }
        }

        private void FormatComboBox_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (mode != ArchiveOperationMode.Create || string.IsNullOrWhiteSpace(destinationTextBox.Text))
            {
                return;
            }

            destinationTextBox.Text = ReplaceArchiveExtension(destinationTextBox.Text, GetSelectedExtension());
            destinationTextBox.SelectionStart = destinationTextBox.Text.Length;
        }

        private string ResolveDestinationPath()
        {
            string path = destinationTextBox.Text.Trim();
            if (string.IsNullOrWhiteSpace(path))
            {
                return string.Empty;
            }

            if (mode == ArchiveOperationMode.Create && !FileTypeClassifier.IsSupportedArchive(path))
            {
                path = ReplaceArchiveExtension(path, GetSelectedExtension());
            }

            return path;
        }

        private string GetSelectedExtension()
        {
            return formatComboBox.SelectedIndex == 1
                ? ".tar"
                : formatComboBox.SelectedIndex == 2
                    ? ".tar.gz"
                    : ".zip";
        }

        private static int GetFormatIndex(string path)
        {
            string normalized = (path ?? string.Empty).ToLowerInvariant();
            if (normalized.EndsWith(".tar.gz", StringComparison.Ordinal) || normalized.EndsWith(".tgz", StringComparison.Ordinal))
            {
                return 2;
            }

            return normalized.EndsWith(".tar", StringComparison.Ordinal) ? 1 : 0;
        }

        private static string ReplaceArchiveExtension(string path, string extension)
        {
            string value = (path ?? string.Empty).Trim();
            string normalized = value.ToLowerInvariant();
            foreach (string knownExtension in new[] { ".tar.gz", ".tgz", ".tar", ".zip" })
            {
                if (normalized.EndsWith(knownExtension, StringComparison.Ordinal))
                {
                    value = value.Substring(0, value.Length - knownExtension.Length);
                    break;
                }
            }

            return value + extension;
        }

        private void UpdateProgress(FileOperationProgressInfo info)
        {
            if (info == null)
            {
                return;
            }

            if (info.Phase == FileOperationPhase.Preparing)
            {
                progressBar.Style = ProgressBarStyle.Marquee;
                progressValueLabel.Text = Language.getString("operationPreparing");
                return;
            }

            if (info.TotalEntries > 0)
            {
                progressValueLabel.Text = string.Format(
                    CultureInfo.CurrentUICulture,
                    Language.getString("operationItemsFormat"),
                    info.CompletedEntries,
                    info.TotalEntries);
            }
            else
            {
                progressValueLabel.Text = string.Format(
                    CultureInfo.CurrentUICulture,
                    Language.getString("archiveEntriesProcessedFormat"),
                    info.CompletedEntries);
            }

            currentItemValueLabel.Text = info.CurrentItem ?? string.Empty;
            if (info.TotalBytes > 0)
            {
                progressBar.Style = ProgressBarStyle.Continuous;
                progressBar.Value = (int)Math.Max(0, Math.Min(100, info.CompletedBytes * 100L / Math.Max(1, info.TotalBytes)));
                transferValueLabel.Text = string.Format(
                    CultureInfo.CurrentUICulture,
                    Language.getString("archiveTransferFormat"),
                    FormatBytes(info.CompletedBytes),
                    FormatBytes(info.TotalBytes));
            }
            else
            {
                progressBar.Style = ProgressBarStyle.Marquee;
                transferValueLabel.Text = string.Format(
                    CultureInfo.CurrentUICulture,
                    Language.getString("operationElapsedStatus"),
                    FormatDuration(info.Elapsed));
            }
        }

        private void CancelButton_Click(object sender, EventArgs e)
        {
            if (!isRunning)
            {
                allowClose = true;
                return;
            }

            cancelButton.Enabled = false;
            descriptionLabel.Text = Language.getString("operationCancelling");
            cancellation?.Cancel();
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (isRunning && !allowClose)
            {
                e.Cancel = true;
                CancelButton_Click(this, EventArgs.Empty);
                return;
            }

            base.OnFormClosing(e);
        }

        private static string BuildSourceSummary(string[] sourcePaths)
        {
            if (sourcePaths == null || sourcePaths.Length == 0)
            {
                return string.Empty;
            }

            if (sourcePaths.Length == 1)
            {
                return sourcePaths[0];
            }

            return string.Format(
                CultureInfo.CurrentUICulture,
                Language.getString("itemsSelectedFormat"),
                sourcePaths.Length);
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

        private static string FormatDuration(TimeSpan duration)
        {
            return duration.TotalHours >= 1
                ? duration.ToString(@"h\:mm\:ss", CultureInfo.InvariantCulture)
                : duration.ToString(@"m\:ss", CultureInfo.InvariantCulture);
        }
    }
}
