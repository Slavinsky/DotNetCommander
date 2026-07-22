using System;
using System.Collections.Specialized;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace DotNetCommander
{
    public partial class FormDelete : Form
    {
        private const int ProgressRevealDelayMs = 220;
        private const int MinimumCompactClientHeight = 320;
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

        public string[] Items { get; private set; }

        public delegate void ActionCompleteHandler(int result);
        public event ActionCompleteHandler ActionComplete;

        public FormDelete(string[] sources)
        {
            InitializeComponent();
            panelProgress = CreateProgressPanel();
            progressRevealTimer = new System.Windows.Forms.Timer { Interval = ProgressRevealDelayMs };
            progressRevealTimer.Tick += ProgressRevealTimer_Tick;
            ApplyDialogAppearance();
            panelContent.Resize += (_, __) => UpdateLayout();
            InitializeFormState(sources);
        }

        public FormDelete(StringCollection sources)
        {
            InitializeComponent();
            panelProgress = CreateProgressPanel();
            progressRevealTimer = new System.Windows.Forms.Timer { Interval = ProgressRevealDelayMs };
            progressRevealTimer.Tick += ProgressRevealTimer_Tick;
            ApplyDialogAppearance();
            panelContent.Resize += (_, __) => UpdateLayout();

            string[] items = new string[sources.Count];
            sources.CopyTo(items, 0);
            InitializeFormState(items);
        }

        private Panel CreateProgressPanel()
        {
            Panel progressPanel = new Panel
            {
                Location = new System.Drawing.Point(24, 120),
                Size = new System.Drawing.Size(479, ProgressSectionHeight),
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

        private void InitializeFormState(string[] sources)
        {
            Items = sources ?? Array.Empty<string>();
            ApplyLocalization();
            UpdateSummary();
        }

        private void ApplyLocalization()
        {
            Text = Language.getString("deleteDialogTitle");
            labelTitle.Text = Text;
            labelDescription.Text = Items != null && Items.Length > 1
                ? Language.getString("deleteDialogDescriptionMultiple")
                : Language.getString("deleteDialogDescriptionSingle");
            labelPath.Text = Language.getString("deleteDialogTarget");
            labelSummaryCaption.Text = Language.getString("selectedItems");
            buttonDelete.Text = Language.getString("delete");
            buttonCancel.Text = Language.getString("cancel");

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
            labelPath.Font = DialogStyleService.CreateCaptionFont();
            textPathTarget.Font = DialogStyleService.CreateBodyFont();
            buttonDelete.Font = DialogStyleService.CreateBodyFont();
            buttonCancel.Font = DialogStyleService.CreateBodyFont();
            buttonDelete.Height = 40;
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
            int textBoxHeight = TextRenderer.MeasureText("Ag", textPathTarget.Font).Height + 10;

            labelSummaryValue.Location = new System.Drawing.Point(21, top);
            labelSummaryValue.Size = new System.Drawing.Size(width, labelSummaryValue.Height);

            top = labelSummaryValue.Bottom + 8;
            labelSummaryCaption.Location = new System.Drawing.Point(21, top);

            top = labelSummaryCaption.Bottom + sectionGap;
            labelPath.Location = new System.Drawing.Point(21, top);

            top = labelPath.Bottom + labelGap;
            textPathTarget.Location = new System.Drawing.Point(left, top);
            textPathTarget.Size = new System.Drawing.Size(width, textBoxHeight);

            int contentBottom = textPathTarget.Bottom + 16;
            panelProgress.Location = new System.Drawing.Point(left, contentBottom);
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
                textPathTarget.Text = string.Empty;
                buttonDelete.Enabled = false;
                return;
            }

            if (Items.Length == 1)
            {
                labelSummaryValue.Text = Path.GetFileName(Items[0]);
                textPathTarget.Text = Items[0];
                return;
            }

            labelSummaryValue.Text = string.Format(
                CultureInfo.CurrentUICulture,
                Language.getString("itemsSelectedFormat"),
                Items.Length);
            textPathTarget.Text = Path.GetDirectoryName(Items[0]) ?? Items[0];
        }

        public void Start()
        {
            _ = BeginDeleteAsync();
        }

        private async Task BeginDeleteAsync()
        {
            if (isRunning)
                return;

            isRunning = true;
            isCancelling = false;
            allowClose = false;
            operationCancellation = new CancellationTokenSource();

            buttonDelete.Enabled = false;
            buttonCancel.Enabled = true;
            ResetProgressState();
            progressRevealTimer.Start();

            Progress<FileOperationProgressInfo> progress = new Progress<FileOperationProgressInfo>(UpdateProgress);

            try
            {
                FileOperationRunResult result = await FileOperationService.ExecuteDeleteAsync(Items, progress, operationCancellation.Token);
                progressRevealTimer.Stop();
                ActionComplete?.Invoke(result == FileOperationRunResult.Cancelled ? -2 : 0);
                allowClose = true;
                Close();
            }
            catch (Exception ex)
            {
                LogService.LogException("FormDelete.BeginDeleteAsync", ex);
                progressRevealTimer.Stop();
                panelProgress.Visible = true;
                buttonDelete.Enabled = Items != null && Items.Length > 0;
                buttonCancel.Enabled = true;
                labelDescription.Text = Items != null && Items.Length > 1
                    ? Language.getString("deleteDialogDescriptionMultiple")
                    : Language.getString("deleteDialogDescriptionSingle");
                MessageBox.Show(
                    Language.getString("deleteOperationFailed") + Environment.NewLine + ex.Message,
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

        private async void buttonDelete_Click(object sender, EventArgs e)
        {
            await BeginDeleteAsync();
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
