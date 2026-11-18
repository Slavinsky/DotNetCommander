using System;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace DotNetCommander
{
    internal sealed class FormCompoundIntegrity : Form
    {
        private readonly string path;
        private readonly Exception strictCatalogError;
        private readonly Func<string, IProgress<CompoundRecoveryProgress>, CancellationToken, Task<CompoundRecoveryResult>> recoveryAction;
        private readonly CancellationTokenSource cancellation = new CancellationTokenSource();
        private readonly Label statusLabel;
        private readonly Label strictLabel;
        private readonly Label lenientLabel;
        private readonly DataGridView issuesGrid;
        private readonly Button cancelButton;
        private readonly Button closeButton;
        private readonly Button recoveryButton;
        private CancellationTokenSource recoveryCancellation;
        private bool recoveryInProgress;
        private bool scanCompleted;

        public FormCompoundIntegrity(
            string path,
            Exception strictCatalogError = null,
            Func<string, IProgress<CompoundRecoveryProgress>, CancellationToken, Task<CompoundRecoveryResult>> recoveryAction = null)
        {
            this.path = path ?? string.Empty;
            this.strictCatalogError = strictCatalogError;
            this.recoveryAction = recoveryAction;
            Text = Language.getString("compoundIntegrityTitle");
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.Sizable;
            MinimizeBox = false;
            MaximizeBox = true;
            ShowInTaskbar = false;
            KeyPreview = true;
            MinimumSize = new Size(720, 460);
            ClientSize = new Size(980, 660);
            AutoScaleMode = AutoScaleMode.Font;
            DialogStyleService.ApplyDialogFont(this);
            KeyDown += IntegrityForm_KeyDown;
            FormClosing += (_, e) =>
            {
                if (recoveryInProgress)
                {
                    recoveryCancellation?.Cancel();
                    e.Cancel = true;
                }
                else if (!scanCompleted)
                {
                    cancellation.Cancel();
                }
            };

            var layout = new TableLayoutPanel
            {
                ColumnCount = 1,
                Dock = DockStyle.Fill,
                Padding = new Padding(16),
                RowCount = 4
            };
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            layout.Controls.Add(new Label
            {
                AutoEllipsis = true,
                AutoSize = false,
                Dock = DockStyle.Fill,
                Font = DialogStyleService.CreateHeaderFont(),
                Height = 30,
                Text = this.path
            }, 0, 0);

            var summary = new TableLayoutPanel { AutoSize = true, ColumnCount = 1, Dock = DockStyle.Top, RowCount = 3 };
            summary.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            summary.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            summary.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            strictLabel = CreateSummaryLabel();
            lenientLabel = CreateSummaryLabel();
            statusLabel = CreateSummaryLabel();
            summary.Controls.Add(strictLabel, 0, 0);
            summary.Controls.Add(lenientLabel, 0, 1);
            summary.Controls.Add(statusLabel, 0, 2);
            layout.Controls.Add(summary, 0, 1);

            issuesGrid = CreateIssuesGrid();
            layout.Controls.Add(issuesGrid, 0, 2);

            var buttons = new FlowLayoutPanel
            {
                AutoSize = true,
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.RightToLeft,
                WrapContents = false,
                Margin = new Padding(0, 10, 0, 0)
            };
            closeButton = new Button
            {
                AutoSize = true,
                MinimumSize = new Size(110, 34),
                Text = Language.getString("close"),
                Enabled = false
            };
            closeButton.Click += (_, __) => Close();
            cancelButton = new Button
            {
                AutoSize = true,
                MinimumSize = new Size(110, 34),
                Text = Language.getString("cancel")
            };
            cancelButton.Click += (_, __) =>
            {
                if (recoveryInProgress)
                    recoveryCancellation?.Cancel();
                else
                    cancellation.Cancel();
            };
            recoveryButton = new Button
            {
                AutoSize = true,
                MinimumSize = new Size(190, 34),
                Text = Language.getString("compoundRecoveryExportButton"),
                Enabled = false,
                Visible = recoveryAction != null
            };
            recoveryButton.Click += async (_, __) => await ExportReachableStreamsAsync();
            buttons.Controls.Add(recoveryButton);
            buttons.Controls.Add(closeButton);
            buttons.Controls.Add(cancelButton);
            layout.Controls.Add(buttons, 0, 3);
            Controls.Add(layout);
            Shown += async (_, __) => await RunInspectionAsync();
        }

        private async Task RunInspectionAsync()
        {
            var progress = new Progress<CompoundIntegrityProgress>(UpdateProgress);
            try
            {
                CompoundIntegrityReport report = await CompoundIntegrityService.InspectAsync(
                    path,
                    strictCatalogError,
                    progress,
                    cancellation.Token);
                if (IsDisposed)
                    return;
                ShowReport(report);
            }
            catch (OperationCanceledException)
            {
                if (!IsDisposed)
                    statusLabel.Text = Language.getString("compoundIntegrityStatusCancelled");
            }
            catch (Exception ex)
            {
                LogService.LogException("FormCompoundIntegrity.RunInspectionAsync", ex);
                if (!IsDisposed)
                {
                    statusLabel.Text = Language.getString("compoundIntegrityUnexpectedFailure") + " " + ex.Message;
                    strictLabel.Text = string.Empty;
                    lenientLabel.Text = string.Empty;
                }
            }
            finally
            {
                scanCompleted = true;
                if (!IsDisposed)
                {
                    cancelButton.Enabled = false;
                    closeButton.Enabled = true;
                }
                cancellation.Dispose();
            }
        }

        private void ShowReport(CompoundIntegrityReport report)
        {
            strictLabel.Text = FormatPass(Language.getString("compoundIntegrityStrict"), report.Strict);
            lenientLabel.Text = FormatPass(Language.getString("compoundIntegrityLenient"), report.Lenient);
            statusLabel.Text = Language.getString("compoundIntegrityDisclaimer");
            issuesGrid.Rows.Clear();

            AddPassIssues("compoundIntegrityStrict", report.Strict);
            AddPassIssues("compoundIntegrityLenient", report.Lenient);
            recoveryButton.Enabled = recoveryAction != null
                && report.Lenient.Status != CompoundIntegrityStatus.Failed
                && report.Lenient.Status != CompoundIntegrityStatus.NotRun
                && report.Lenient.Status != CompoundIntegrityStatus.Passed;
            if (issuesGrid.Rows.Count == 0)
                issuesGrid.Rows.Add(string.Empty, string.Empty, string.Empty, Language.getString("compoundIntegrityNoIssues"));
        }

        private async Task ExportReachableStreamsAsync()
        {
            if (!scanCompleted || recoveryInProgress || recoveryAction == null)
                return;

            using var folderDialog = new FolderBrowserDialog
            {
                Description = Language.getString("compoundRecoveryChooseDestination"),
                SelectedPath = Directory.Exists(Path.GetDirectoryName(path)) ? Path.GetDirectoryName(path) : string.Empty,
                ShowNewFolderButton = true,
                UseDescriptionForTitle = true
            };
            if (folderDialog.ShowDialog(this) != DialogResult.OK)
                return;

            string confirmation = string.Format(
                CultureInfo.CurrentCulture,
                Language.getString("compoundRecoveryConfirmFormat"),
                path,
                folderDialog.SelectedPath,
                Environment.NewLine);
            if (MessageBox.Show(this, confirmation, Text, MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes)
                return;

            recoveryInProgress = true;
            recoveryCancellation = new CancellationTokenSource();
            recoveryButton.Enabled = false;
            closeButton.Enabled = false;
            cancelButton.Enabled = true;
            cancelButton.Text = Language.getString("cancel");
            issuesGrid.Rows.Clear();
            statusLabel.Text = Language.getString("compoundRecoveryStarting");
            var progress = new Progress<CompoundRecoveryProgress>(UpdateRecoveryProgress);
            try
            {
                CompoundRecoveryResult result = await recoveryAction(
                    folderDialog.SelectedPath,
                    progress,
                    recoveryCancellation.Token);
                if (IsDisposed)
                    return;

                foreach (CompoundIntegrityIssue issue in result.Issues)
                    issuesGrid.Rows.Add(Language.getString("compoundRecoveryExportButton"), Language.getString(issue.StageKey), issue.Path, issue.Message);
                if (result.OmittedIssues > 0)
                {
                    issuesGrid.Rows.Add(
                        Language.getString("compoundRecoveryExportButton"),
                        Language.getString("compoundIntegrityLimit"),
                        string.Empty,
                        string.Format(Language.getString("compoundIntegrityOmittedIssues"), result.OmittedIssues));
                }

                string summary = result.Error != null
                    ? Language.getString("compoundRecoveryFailed") + " " + result.Error
                    : result.StreamsRecovered == 0
                        ? Language.getString("compoundRecoveryNoStreams")
                        : string.Format(
                            CultureInfo.CurrentCulture,
                            Language.getString("compoundRecoverySummaryFormat"),
                            result.StreamsRecovered,
                            result.StreamsSkipped,
                            result.BytesRead,
                            result.Incomplete ? Language.getString("compoundIntegrityStatusIncomplete") : Language.getString("compoundIntegrityStatusPassed"));
                statusLabel.Text = summary;
                if (result.DestinationCopyStarted)
                {
                    statusLabel.Height = 46;
                    string copyStatusKey = !result.DestinationCopyResultCode.HasValue
                        ? "compoundRecoveryDestinationCopyNotStarted"
                        : result.DestinationCopyResultCode.Value == 0
                            ? "compoundRecoveryDestinationCopySuccess"
                            : result.DestinationCopyResultCode.Value == -2
                                ? "compoundRecoveryDestinationCopyCancelled"
                                : "compoundRecoveryDestinationCopyPartial";
                    statusLabel.Text += Environment.NewLine + string.Format(
                        CultureInfo.CurrentCulture,
                        Language.getString("compoundRecoveryDestinationCopyFormat"),
                        Language.getString(copyStatusKey));
                }
                if (result.Error != null)
                    MessageBox.Show(this, summary, Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            catch (OperationCanceledException)
            {
                if (!IsDisposed)
                    statusLabel.Text = Language.getString("compoundIntegrityStatusCancelled");
            }
            catch (Exception ex)
            {
                LogService.LogException("FormCompoundIntegrity.ExportReachableStreamsAsync", ex);
                if (!IsDisposed)
                    statusLabel.Text = Language.getString("compoundRecoveryFailed") + " " + ex.Message;
            }
            finally
            {
                recoveryInProgress = false;
                recoveryCancellation?.Dispose();
                recoveryCancellation = null;
                if (!IsDisposed)
                {
                    cancelButton.Enabled = false;
                    cancelButton.Text = Language.getString("cancel");
                    closeButton.Enabled = true;
                    recoveryButton.Enabled = recoveryAction != null;
                }
            }
        }

        private void UpdateRecoveryProgress(CompoundRecoveryProgress progress)
        {
            if (IsDisposed)
                return;

            statusLabel.Text = string.Format(
                CultureInfo.CurrentCulture,
                Language.getString("compoundRecoveryProgress"),
                progress.EntriesVisited,
                progress.StreamsRecovered,
                progress.BytesRead,
                progress.CurrentPath);
        }

        private void AddPassIssues(string passKey, CompoundIntegrityPass pass)
        {
            foreach (CompoundIntegrityIssue issue in pass.Issues)
            {
                int row = issuesGrid.Rows.Add(
                    Language.getString(passKey),
                    Language.getString(issue.StageKey),
                    issue.Path,
                    issue.Message);
                issuesGrid.Rows[row].Cells[3].ToolTipText = issue.Message;
            }

            if (pass.OmittedIssues > 0)
            {
                issuesGrid.Rows.Add(
                    Language.getString(passKey),
                    Language.getString("compoundIntegrityLimit"),
                    string.Empty,
                    string.Format(Language.getString("compoundIntegrityOmittedIssues"), pass.OmittedIssues));
            }
        }

        private static string FormatPass(string label, CompoundIntegrityPass pass)
        {
            string status = Language.getString("compoundIntegrityStatus" + pass.Status);
            string counts = string.Format(
                CultureInfo.CurrentCulture,
                Language.getString("compoundIntegrityCounts"),
                pass.EntriesVisited,
                pass.StreamsFullyRead,
                pass.StreamsPartiallyRead,
                pass.BytesRead);
            string error = string.IsNullOrWhiteSpace(pass.Error) ? string.Empty : "  " + pass.Error;
            return label + ":  " + status + "  " + counts + error;
        }

        private void UpdateProgress(CompoundIntegrityProgress progress)
        {
            if (IsDisposed)
                return;
            statusLabel.Text = string.Format(
                CultureInfo.CurrentCulture,
                Language.getString("compoundIntegrityProgress"),
                Language.getString(progress.IsStrict ? "compoundIntegrityStrict" : "compoundIntegrityLenient"),
                progress.EntriesVisited,
                progress.StreamsFullyRead,
                progress.BytesRead);
        }

        private void IntegrityForm_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode != Keys.Escape)
                return;
            if (!scanCompleted)
                cancellation.Cancel();
            else
                Close();
            e.Handled = true;
        }

        private static Label CreateSummaryLabel()
        {
            return new Label
            {
                AutoEllipsis = true,
                AutoSize = false,
                Dock = DockStyle.Fill,
                Height = 28,
                Margin = new Padding(0, 2, 0, 2)
            };
        }

        private static DataGridView CreateIssuesGrid()
        {
            var grid = new DataGridView
            {
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                AllowUserToResizeRows = false,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None,
                BackgroundColor = SystemColors.Window,
                ClipboardCopyMode = DataGridViewClipboardCopyMode.EnableAlwaysIncludeHeaderText,
                Dock = DockStyle.Fill,
                MultiSelect = false,
                ReadOnly = true,
                RowHeadersVisible = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect
            };
            grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = Language.getString("compoundIntegrityPass"), Width = 90 });
            grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = Language.getString("compoundIntegrityStage"), Width = 150 });
            grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = Language.getString("compoundMetadataPath"), Width = 300 });
            grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = Language.getString("compoundMetadataValue"), AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill, MinimumWidth = 180 });
            return grid;
        }
    }
}
