using System;
using System.Collections.Specialized;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace DotNetCommander
{
    public partial class FormCopy : Form
    {
        private const int ProgressRevealDelayMs = 220;
        private const int MinimumCompactClientHeight = 372;
        private const int ProgressSectionHeight = 132;

        private readonly System.Windows.Forms.Timer progressRevealTimer;
        private Panel panelProgress;
        private Label labelProgressCaption;
        private Label labelProgressValue;
        private ProgressBar progressOperation;
        private Label labelCurrentItemCaption;
        private Label labelCurrentItemValue;
        private Label labelTransferCaption;
        private Label labelTransferValue;

        private CancellationTokenSource operationCancellation;
        private bool isRunning;
        private bool isCancelling;
        private bool allowClose;

        public enum Type
        {
            Copy,
            Move
        }

        public string[] Items = null;
        public string destPath = null;
        public Type type = Type.Copy;

        public delegate void ActionCompleteHandler(int result);
        public event ActionCompleteHandler ActionComplete;

        public FormCopy(string[] sources, string destination, Type type = Type.Copy)
        {
            InitializeComponent();
            panelProgress = CreateProgressPanel();
            progressRevealTimer = new System.Windows.Forms.Timer { Interval = ProgressRevealDelayMs };
            progressRevealTimer.Tick += ProgressRevealTimer_Tick;
            ApplyDialogAppearance();
            panelContent.Resize += (_, __) => UpdateLayout();

            InitializeFormState(sources, destination, type);
        }

        public FormCopy(StringCollection sources, string destination, Type type = Type.Copy)
        {
            InitializeComponent();
            panelProgress = CreateProgressPanel();
            progressRevealTimer = new System.Windows.Forms.Timer { Interval = ProgressRevealDelayMs };
            progressRevealTimer.Tick += ProgressRevealTimer_Tick;
            ApplyDialogAppearance();
            panelContent.Resize += (_, __) => UpdateLayout();

            string[] items = new string[sources.Count];
            sources.CopyTo(items, 0);
            InitializeFormState(items, destination, type);
        }

        private Panel CreateProgressPanel()
        {
            Panel progressPanel = new Panel
            {
                Location = new System.Drawing.Point(24, 208),
                Size = new System.Drawing.Size(479, 132),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
                Visible = false
            };

            labelProgressCaption = CreateCaptionLabel(Language.getString("operationProgress"), 0);
            labelProgressValue = CreateValueLabel(string.Empty, 18);
            progressOperation = new ProgressBar
            {
                Location = new System.Drawing.Point(0, 42),
                Size = new System.Drawing.Size(479, 20),
                Style = ProgressBarStyle.Marquee,
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };
            labelCurrentItemCaption = CreateCaptionLabel(Language.getString("operationCurrentItem"), 72);
            labelCurrentItemValue = CreateValueLabel(string.Empty, 92);
            labelTransferCaption = CreateCaptionLabel(Language.getString("operationTransferred"), 114);
            labelTransferValue = CreateValueLabel(string.Empty, 134);

            progressPanel.Controls.Add(labelProgressCaption);
            progressPanel.Controls.Add(labelProgressValue);
            progressPanel.Controls.Add(progressOperation);
            progressPanel.Controls.Add(labelCurrentItemCaption);
            progressPanel.Controls.Add(labelCurrentItemValue);
            progressPanel.Controls.Add(labelTransferCaption);
            progressPanel.Controls.Add(labelTransferValue);
            panelContent.Controls.Add(progressPanel);

            return progressPanel;
        }

        private Label CreateCaptionLabel(string text, int top)
        {
            return new Label
            {
                AutoSize = true,
                Font = DialogStyleService.CreateCaptionFont(),
                ForeColor = System.Drawing.Color.FromArgb(88, 96, 105),
                Location = new System.Drawing.Point(0, top),
                Text = text
            };
        }

        private Label CreateValueLabel(string text, int top)
        {
            return new Label
            {
                AutoEllipsis = true,
                Location = new System.Drawing.Point(0, top),
                Size = new System.Drawing.Size(479, 18),
                ForeColor = System.Drawing.Color.FromArgb(51, 61, 71),
                Font = DialogStyleService.CreateEmphasisFont(),
                Text = text
            };
        }

        private void InitializeFormState(string[] sources, string destination, Type operationType)
        {
            type = operationType;
            Items = sources ?? Array.Empty<string>();
            destPath = destination ?? string.Empty;
            checkBoxOverwriteExisting.Checked = Properties.Settings.Default.OverwriteExistingFiles;

            ApplyLocalization();

            textPathDest.Text = destPath;
            if (Items.Length == 0)
            {
                textPathSource.Text = string.Empty;
                buttonOK.Enabled = false;
                return;
            }

            if (Items.Length > 1)
            {
                textPathSource.Text = Path.GetPathRoot(Items[0]) ?? Items[0];
            }
            else
            {
                textPathSource.Text = Items[0];
                textPathDest.Text = Path.Combine(textPathDest.Text, Path.GetFileName(Items[0]));
            }

            UpdateSummary();
        }

        private void ApplyLocalization()
        {
            bool isCopy = type == Type.Copy;
            Text = isCopy ? Language.getString("copyDialogTitle") : Language.getString("moveDialogTitle");
            labelTitle.Text = Text;
            labelDescription.Text = isCopy
                ? Language.getString("copyDialogDescription")
                : Language.getString("moveDialogDescription");
            labelSource.Text = Language.getString("sourcePath");
            labelDestination.Text = Language.getString("destinationPath");
            labelSummaryCaption.Text = Language.getString("selectedItems");
            buttonOK.Text = isCopy ? Language.getString("copy") : Language.getString("move");
            buttonCancel.Text = Language.getString("cancel");
            checkBoxOverwriteExisting.Text = Language.getString("overwriteExistingFiles");

            if (panelProgress != null)
            {
                labelProgressCaption.Text = Language.getString("operationProgress");
                labelCurrentItemCaption.Text = Language.getString("operationCurrentItem");
                labelTransferCaption.Text = Language.getString("operationTransferred");
            }

            UpdateLayout();
        }

        private void ApplyDialogAppearance()
        {
            DialogStyleService.ApplyDialogFont(this);
            labelTitle.Font = DialogStyleService.CreateHeaderFont();
            labelSummaryValue.Font = DialogStyleService.CreateEmphasisFont();
            labelDescription.Font = DialogStyleService.CreateBodyFont();
            labelSummaryCaption.Font = DialogStyleService.CreateCaptionFont();
            labelSource.Font = DialogStyleService.CreateCaptionFont();
            labelDestination.Font = DialogStyleService.CreateCaptionFont();
            textPathSource.Font = DialogStyleService.CreateBodyFont();
            textPathDest.Font = DialogStyleService.CreateBodyFont();
            checkBoxOverwriteExisting.Font = DialogStyleService.CreateBodyFont();
            buttonOK.Font = DialogStyleService.CreateBodyFont();
            buttonCancel.Font = DialogStyleService.CreateBodyFont();
            buttonOK.Height = 40;
            buttonCancel.Height = 40;
            ClientSize = new System.Drawing.Size(ClientSize.Width, MinimumCompactClientHeight);
        }

        private void UpdateLayout()
        {
            int left = 24;
            int width = Math.Max(320, panelContent.ClientSize.Width - 42);
            int top = 16;
            int labelGap = 6;
            int sectionGap = 14;
            int textBoxHeight = TextRenderer.MeasureText("Ag", textPathSource.Font).Height + 10;

            labelSummaryValue.Location = new System.Drawing.Point(21, top);
            labelSummaryValue.Size = new System.Drawing.Size(width, labelSummaryValue.Height);

            top = labelSummaryValue.Bottom + 8;
            labelSummaryCaption.Location = new System.Drawing.Point(21, top);

            top = labelSummaryCaption.Bottom + sectionGap;
            labelSource.Location = new System.Drawing.Point(21, top);

            top = labelSource.Bottom + labelGap;
            textPathSource.Location = new System.Drawing.Point(left, top);
            textPathSource.Size = new System.Drawing.Size(width, textBoxHeight);

            top = textPathSource.Bottom + sectionGap;
            labelDestination.Location = new System.Drawing.Point(21, top);

            top = labelDestination.Bottom + labelGap;
            textPathDest.Location = new System.Drawing.Point(left, top);
            textPathDest.Size = new System.Drawing.Size(width, textBoxHeight);

            top = textPathDest.Bottom + 12;
            checkBoxOverwriteExisting.Location = new System.Drawing.Point(left, top);

            int contentBottom = checkBoxOverwriteExisting.Bottom + 16;
            top = contentBottom;
            panelProgress.Location = new System.Drawing.Point(left, top);
            panelProgress.Size = new System.Drawing.Size(width, ProgressSectionHeight);
            progressOperation.Width = width;
            labelProgressValue.Width = width;
            labelCurrentItemValue.Width = width;
            labelTransferValue.Width = width;

            int compactHeight = Math.Max(MinimumCompactClientHeight, panelHeader.Height + panelButtons.Height + contentBottom + 12);
            int expandedHeight = Math.Max(compactHeight + ProgressSectionHeight + 16, panelHeader.Height + panelButtons.Height + panelProgress.Bottom + 12);
            int desiredHeight = panelProgress.Visible ? expandedHeight : compactHeight;
            if (ClientSize.Height != desiredHeight)
            {
                ClientSize = new System.Drawing.Size(ClientSize.Width, desiredHeight);
            }
        }

        private void UpdateSummary()
        {
            if (Items == null || Items.Length == 0)
            {
                labelSummaryValue.Text = Language.getString("noItemsSelected");
                return;
            }

            if (Items.Length == 1)
            {
                labelSummaryValue.Text = Path.GetFileName(Items[0]);
                return;
            }

            labelSummaryValue.Text = string.Format(
                CultureInfo.CurrentUICulture,
                Language.getString("itemsSelectedFormat"),
                Items.Length);
        }

        public void Start()
        {
            _ = BeginOperationAsync();
        }

        private async Task BeginOperationAsync()
        {
            destPath = textPathDest.Text.Trim();
            if (string.IsNullOrWhiteSpace(destPath))
            {
                MessageBox.Show(
                    Language.getString("destinationRequired"),
                    Language.getString("error"),
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                return;
            }

            if (isRunning)
                return;

            Dictionary<string, FileConflictResolution> conflictResolutions = ResolveConflicts(destPath);
            if (conflictResolutions == null)
            {
                return;
            }

            isRunning = true;
            isCancelling = false;
            allowClose = false;
            operationCancellation = new CancellationTokenSource();
            Properties.Settings.Default.OverwriteExistingFiles = checkBoxOverwriteExisting.Checked;
            Properties.Settings.Default.Save();

            SetIdleControlsEnabled(false);
            ResetProgressState();
            progressRevealTimer.Start();

            Progress<FileOperationProgressInfo> progress = new Progress<FileOperationProgressInfo>(UpdateProgress);

            try
            {
                FileOperationRunResult result = await FileOperationService.ExecuteCopyOrMoveAsync(
                    Items,
                    destPath,
                    type,
                    checkBoxOverwriteExisting.Checked,
                    conflictResolutions,
                    progress,
                    operationCancellation.Token);
                progressRevealTimer.Stop();
                ActionComplete?.Invoke(result == FileOperationRunResult.Cancelled ? -2 : 0);
                allowClose = true;
                Close();
            }
            catch (Exception ex)
            {
                LogService.LogException("FormCopy.BeginOperationAsync", ex);
                progressRevealTimer.Stop();
                panelProgress.Visible = true;
                SetIdleControlsEnabled(true);
                labelDescription.Text = type == Type.Copy
                    ? Language.getString("copyDialogDescription")
                    : Language.getString("moveDialogDescription");
                MessageBox.Show(
                    (type == Type.Copy ? Language.getString("copyOperationFailed") : Language.getString("moveOperationFailed"))
                    + Environment.NewLine + ex.Message,
                    Language.getString("error"),
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
                ActionComplete?.Invoke(-1);
            }
            finally
            {
                isRunning = false;
                isCancelling = false;
                operationCancellation?.Dispose();
                operationCancellation = null;
            }
        }

        private void ResetProgressState()
        {
            panelProgress.Visible = false;
            progressOperation.Style = ProgressBarStyle.Marquee;
            progressOperation.Value = 0;
            labelProgressValue.Text = Language.getString("operationPreparing");
            labelCurrentItemValue.Text = string.Empty;
            labelTransferValue.Text = string.Empty;
            UpdateLayout();
        }

        private void UpdateProgress(FileOperationProgressInfo info)
        {
            if (info == null)
                return;

            if (info.Phase == FileOperationPhase.Preparing)
            {
                labelDescription.Text = Language.getString("operationPreparing");
                labelProgressValue.Text = Language.getString("operationPreparing");
                progressOperation.Style = ProgressBarStyle.Marquee;
                return;
            }

            if (isCancelling)
            {
                labelDescription.Text = Language.getString("operationCancelling");
            }

            panelProgress.Visible = panelProgress.Visible || !progressRevealTimer.Enabled;
            UpdateLayout();
            labelProgressValue.Text = string.Format(
                CultureInfo.CurrentUICulture,
                Language.getString("operationItemsFormat"),
                info.CompletedEntries,
                info.TotalEntries);

            labelCurrentItemValue.Text = string.IsNullOrWhiteSpace(info.CurrentItem)
                ? Language.getString("operationWaiting")
                : info.CurrentItem;

            if (info.TotalBytes > 0)
            {
                progressOperation.Style = ProgressBarStyle.Continuous;
                int percent = (int)Math.Max(0, Math.Min(100, (info.CompletedBytes * 100L) / Math.Max(1L, info.TotalBytes)));
                progressOperation.Value = percent;

                labelTransferValue.Text = BuildTransferStatus(info);
            }
            else
            {
                progressOperation.Style = ProgressBarStyle.Marquee;
                labelTransferValue.Text = BuildElapsedStatus(info.Elapsed);
            }
        }

        private string BuildTransferStatus(FileOperationProgressInfo info)
        {
            string completed = FormatBytes(info.CompletedBytes);
            string total = FormatBytes(info.TotalBytes);
            double bytesPerSecond = info.Elapsed.TotalSeconds > 0
                ? info.CompletedBytes / Math.Max(0.1, info.Elapsed.TotalSeconds)
                : 0;

            string speed = bytesPerSecond > 0
                ? FormatBytes((long)bytesPerSecond) + "/s"
                : Language.getString("operationCalculating");

            if (bytesPerSecond <= 0 || info.CompletedBytes <= 0 || info.CompletedBytes >= info.TotalBytes)
            {
                return string.Format(
                    CultureInfo.CurrentUICulture,
                    Language.getString("operationTransferStatusNoEta"),
                    completed,
                    total,
                    speed);
            }

            long remainingBytes = Math.Max(0, info.TotalBytes - info.CompletedBytes);
            TimeSpan remaining = TimeSpan.FromSeconds(remainingBytes / bytesPerSecond);
            return string.Format(
                CultureInfo.CurrentUICulture,
                Language.getString("operationTransferStatus"),
                completed,
                total,
                speed,
                FormatDuration(remaining));
        }

        private string BuildElapsedStatus(TimeSpan elapsed)
        {
            return string.Format(
                CultureInfo.CurrentUICulture,
                Language.getString("operationElapsedStatus"),
                FormatDuration(elapsed));
        }

        private void SetIdleControlsEnabled(bool enabled)
        {
            textPathDest.Enabled = enabled;
            checkBoxOverwriteExisting.Enabled = enabled;
            buttonOK.Enabled = enabled && Items != null && Items.Length > 0;
            buttonCancel.Enabled = true;
        }

        private Dictionary<string, FileConflictResolution> ResolveConflicts(string destinationPath)
        {
            if (checkBoxOverwriteExisting.Checked)
            {
                return new Dictionary<string, FileConflictResolution>(StringComparer.OrdinalIgnoreCase);
            }

            IReadOnlyList<FileOperationConflict> conflicts = FileOperationService.CollectCopyOrMoveConflicts(
                Items,
                destinationPath,
                type,
                CancellationToken.None);

            if (conflicts == null || conflicts.Count == 0)
            {
                return new Dictionary<string, FileConflictResolution>(StringComparer.OrdinalIgnoreCase);
            }

            Dictionary<string, FileConflictResolution> resolutions = new Dictionary<string, FileConflictResolution>(StringComparer.OrdinalIgnoreCase);
            bool overwriteAll = false;
            bool skipAll = false;

            foreach (FileOperationConflict conflict in conflicts)
            {
                if (overwriteAll)
                {
                    resolutions[conflict.DestinationPath] = new FileConflictResolution(FileConflictResolutionAction.Overwrite);
                    continue;
                }

                if (skipAll)
                {
                    resolutions[conflict.DestinationPath] = new FileConflictResolution(FileConflictResolutionAction.Skip);
                    continue;
                }

                while (true)
                {
                    using FormFileConflict dialog = new FormFileConflict(type, conflict.SourcePath, conflict.DestinationPath);
                    DialogResult dialogResult = dialog.ShowDialog(this);
                    FileConflictDialogChoice choice = dialogResult == DialogResult.OK
                        ? dialog.SelectedChoice
                        : FileConflictDialogChoice.Cancel;

                    if (choice == FileConflictDialogChoice.Cancel)
                    {
                        return null;
                    }

                    if (choice == FileConflictDialogChoice.Overwrite)
                    {
                        resolutions[conflict.DestinationPath] = new FileConflictResolution(FileConflictResolutionAction.Overwrite);
                        break;
                    }

                    if (choice == FileConflictDialogChoice.Skip)
                    {
                        resolutions[conflict.DestinationPath] = new FileConflictResolution(FileConflictResolutionAction.Skip);
                        break;
                    }

                    if (choice == FileConflictDialogChoice.OverwriteAll)
                    {
                        overwriteAll = true;
                        resolutions[conflict.DestinationPath] = new FileConflictResolution(FileConflictResolutionAction.Overwrite);
                        break;
                    }

                    if (choice == FileConflictDialogChoice.SkipAll)
                    {
                        skipAll = true;
                        resolutions[conflict.DestinationPath] = new FileConflictResolution(FileConflictResolutionAction.Skip);
                        break;
                    }

                    string renamedPath = PromptForConflictRename(conflict);
                    if (!string.IsNullOrWhiteSpace(renamedPath))
                    {
                        resolutions[conflict.DestinationPath] = new FileConflictResolution(FileConflictResolutionAction.Rename, renamedPath);
                        break;
                    }
                }
            }

            return resolutions;
        }

        private string PromptForConflictRename(FileOperationConflict conflict)
        {
            string destinationDirectory = Path.GetDirectoryName(conflict.DestinationPath);
            string suggestedFileName = BuildUniqueConflictFileName(conflict.DestinationPath);

            using SaveFileDialog saveDialog = new SaveFileDialog();
            saveDialog.Title = Language.getString("conflictRenameDialogTitle");
            saveDialog.InitialDirectory = destinationDirectory;
            saveDialog.FileName = suggestedFileName;
            saveDialog.OverwritePrompt = false;
            saveDialog.CheckFileExists = false;
            saveDialog.RestoreDirectory = true;

            if (saveDialog.ShowDialog(this) != DialogResult.OK)
            {
                return null;
            }

            string renamedPath = saveDialog.FileName;
            if (string.IsNullOrWhiteSpace(renamedPath))
            {
                return null;
            }

            if (FileSystemService.FileExists(renamedPath))
            {
                MessageBox.Show(
                    this,
                    string.Format(CultureInfo.CurrentUICulture, Language.getString("conflictRenameExistsFormat"), renamedPath),
                    Language.getString("error"),
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                return null;
            }

            return renamedPath;
        }

        private static string BuildUniqueConflictFileName(string destinationPath)
        {
            string directory = Path.GetDirectoryName(destinationPath) ?? string.Empty;
            string extension = Path.GetExtension(destinationPath) ?? string.Empty;
            string fileNameWithoutExtension = Path.GetFileNameWithoutExtension(destinationPath);
            int suffix = 1;
            string candidateName = Path.GetFileName(destinationPath);
            string candidatePath = destinationPath;

            while (FileSystemService.FileExists(candidatePath))
            {
                suffix++;
                candidateName = string.Format(
                    CultureInfo.CurrentUICulture,
                    "{0} ({1}){2}",
                    fileNameWithoutExtension,
                    suffix,
                    extension);
                candidatePath = Path.Combine(directory, candidateName);
            }

            return candidateName;
        }

        private string FormatBytes(long value)
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

        private string FormatDuration(TimeSpan duration)
        {
            if (duration.TotalHours >= 1)
                return duration.ToString(@"h\:mm\:ss", CultureInfo.InvariantCulture);

            return duration.ToString(@"m\:ss", CultureInfo.InvariantCulture);
        }

        private void ProgressRevealTimer_Tick(object sender, EventArgs e)
        {
            progressRevealTimer.Stop();
            if (isRunning)
            {
                panelProgress.Visible = true;
                UpdateLayout();
            }
        }

        private async void buttonOK_Click(object sender, EventArgs e)
        {
            await BeginOperationAsync();
        }

        private void buttonCancel_Click(object sender, EventArgs e)
        {
            if (!isRunning)
            {
                allowClose = true;
                Close();
                return;
            }

            if (isCancelling)
                return;

            isCancelling = true;
            buttonCancel.Enabled = false;
            labelDescription.Text = Language.getString("operationCancelling");
            panelProgress.Visible = true;
            operationCancellation?.Cancel();
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (isRunning && !allowClose)
            {
                e.Cancel = true;
                buttonCancel_Click(this, EventArgs.Empty);
                return;
            }

            base.OnFormClosing(e);
        }
    }
}
