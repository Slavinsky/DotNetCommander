using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace DotNetCommander
{
    internal sealed class FormAiOrganizer : Form
    {
        private readonly string rootPath;
        private readonly AiOrganizationService organizationService = new AiOrganizationService();
        private readonly TextBox textInstruction;
        private readonly TextBox textEndpoint;
        private readonly ComboBox comboModel;
        private readonly ComboBox comboContext;
        private readonly ComboBox comboTaskMode;
        private readonly Label labelModelInfo;
        private readonly DataGridView gridPlan;
        private readonly TextBox textSummary;
        private readonly Label labelStatus;
        private readonly ProgressBar progressBar;
        private readonly Button buttonAnalyze;
        private readonly Button buttonExecute;
        private readonly Button buttonCancel;
        private readonly System.Windows.Forms.Timer progressTimer;
        private IReadOnlyList<OllamaModelDescriptor> availableModels = Array.Empty<OllamaModelDescriptor>();
        private CancellationTokenSource cancellation;
        private CancellationTokenSource modelLoadingCancellation;
        private DateTime analysisStarted;
        private DateTime progressReceived;
        private AiOrganizationProgress latestProgress;
        private bool busy;

        public FormAiOrganizer(string rootPath)
        {
            this.rootPath = rootPath;
            Text = Language.getString("aiOrganizerTitle");
            StartPosition = FormStartPosition.CenterParent;
            MinimumSize = new Size(780, 560);
            Size = new Size(980, 690);
            ShowIcon = false;
            KeyPreview = true;

            var root = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(18),
                ColumnCount = 1,
                RowCount = 8
            };
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            var labelTitle = new Label
            {
                AutoSize = true,
                Font = DialogStyleService.CreateHeaderFont(),
                Text = Language.getString("aiOrganizerHeading"),
                Margin = new Padding(0, 0, 0, 8)
            };
            var labelPath = new Label
            {
                AutoEllipsis = true,
                Dock = DockStyle.Fill,
                Text = rootPath,
                ForeColor = Color.FromArgb(75, 85, 95),
                Margin = new Padding(0, 0, 0, 12)
            };

            textInstruction = new TextBox
            {
                Dock = DockStyle.Top,
                Multiline = true,
                Height = 58,
                Text = Language.getString("aiOrganizerDefaultInstruction"),
                ScrollBars = ScrollBars.Vertical
            };

            var settingsPanel = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                ColumnCount = 4,
                RowCount = 3,
                Margin = new Padding(0, 10, 0, 10)
            };
            settingsPanel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            settingsPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 55));
            settingsPanel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            settingsPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 45));
            settingsPanel.Controls.Add(new Label { AutoSize = true, Anchor = AnchorStyles.Left, Text = Language.getString("aiOrganizerEndpoint") }, 0, 0);
            textEndpoint = new TextBox
            {
                Dock = DockStyle.Fill,
                Text = Properties.Settings.Default.AiOrganizerEndpoint,
                Margin = new Padding(8, 3, 18, 3)
            };
            settingsPanel.Controls.Add(textEndpoint, 1, 0);
            settingsPanel.Controls.Add(new Label { AutoSize = true, Anchor = AnchorStyles.Left, Text = Language.getString("aiOrganizerModel") }, 2, 0);
            comboModel = new ComboBox
            {
                Dock = DockStyle.Fill,
                DropDownStyle = ComboBoxStyle.DropDown,
                Text = Properties.Settings.Default.AiOrganizerModel,
                Margin = new Padding(8, 3, 0, 3)
            };
            if (!string.IsNullOrWhiteSpace(comboModel.Text))
                comboModel.Items.Add(comboModel.Text);
            comboModel.SelectedIndexChanged += (_, __) => UpdateModelInfo();
            comboModel.TextChanged += (_, __) => UpdateModelInfo();
            settingsPanel.Controls.Add(comboModel, 3, 0);
            settingsPanel.Controls.Add(new Label { AutoSize = true, Anchor = AnchorStyles.Left, Text = Language.getString("aiOrganizerContext") }, 0, 1);
            comboContext = CreateContextModeComboBox();
            comboContext.Dock = DockStyle.Fill;
            comboContext.Margin = new Padding(8, 3, 18, 3);
            comboContext.SelectedIndex = ContextModeToIndex(AiOrganizerContextModeStorage.Parse(Properties.Settings.Default.AiOrganizerContextMode));
            settingsPanel.Controls.Add(comboContext, 1, 1);
            settingsPanel.Controls.Add(new Label { AutoSize = true, Anchor = AnchorStyles.Left, Text = Language.getString("aiOrganizerTaskMode") }, 2, 1);
            comboTaskMode = new ComboBox
            {
                Dock = DockStyle.Fill,
                DropDownStyle = ComboBoxStyle.DropDownList,
                Margin = new Padding(8, 3, 0, 3)
            };
            comboTaskMode.Items.Add(Language.getString("aiOrganizerTaskFilePlan"));
            comboTaskMode.Items.Add(Language.getString("aiOrganizerTaskSummary"));
            comboTaskMode.SelectedIndex = 0;
            settingsPanel.Controls.Add(comboTaskMode, 3, 1);
            labelModelInfo = new Label
            {
                AutoEllipsis = true,
                Dock = DockStyle.Fill,
                ForeColor = SystemColors.GrayText,
                Height = 24,
                Margin = new Padding(0, 3, 0, 0)
            };
            settingsPanel.Controls.Add(labelModelInfo, 0, 2);
            settingsPanel.SetColumnSpan(labelModelInfo, 4);

            gridPlan = CreatePlanGrid();
            textSummary = new TextBox
            {
                Dock = DockStyle.Fill,
                Height = 62,
                Multiline = true,
                ReadOnly = true,
                ScrollBars = ScrollBars.Vertical,
                Margin = new Padding(0, 8, 0, 2)
            };

            var progressPanel = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 1, RowCount = 2 };
            progressPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            progressPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 20));
            labelStatus = new Label { AutoSize = true, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, Margin = new Padding(0, 2, 0, 4) };
            progressBar = new ProgressBar { Dock = DockStyle.Fill, Style = ProgressBarStyle.Marquee, Visible = false, Margin = new Padding(0) };
            progressPanel.Controls.Add(labelStatus, 0, 0);
            progressPanel.Controls.Add(progressBar, 0, 1);

            var buttons = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.RightToLeft,
                AutoSize = true,
                WrapContents = false,
                Margin = new Padding(0, 12, 0, 0)
            };
            buttonCancel = CreateButton(Language.getString("close"));
            buttonCancel.Click += ButtonCancel_Click;
            buttonExecute = CreateButton(Language.getString("aiOrganizerExecute"));
            buttonExecute.Enabled = false;
            buttonExecute.Click += async (_, __) => await ExecuteSelectedAsync();
            buttonAnalyze = CreateButton(Language.getString("aiOrganizerAnalyze"));
            buttonAnalyze.Click += async (_, __) => await AnalyzeAsync();
            buttons.Controls.Add(buttonCancel);
            buttons.Controls.Add(buttonExecute);
            buttons.Controls.Add(buttonAnalyze);

            root.Controls.Add(labelTitle, 0, 0);
            root.Controls.Add(labelPath, 0, 1);
            root.Controls.Add(textInstruction, 0, 2);
            root.Controls.Add(settingsPanel, 0, 3);
            root.Controls.Add(gridPlan, 0, 4);
            root.Controls.Add(textSummary, 0, 5);
            root.Controls.Add(progressPanel, 0, 6);
            root.Controls.Add(buttons, 0, 7);
            Controls.Add(root);

            progressTimer = new System.Windows.Forms.Timer { Interval = 1000 };
            progressTimer.Tick += (_, __) => UpdateProgressStatus();

            DialogStyleService.ApplyDialogFont(this);
            AcceptButton = buttonAnalyze;
            FormClosing += FormAiOrganizer_FormClosing;
            Shown += async (_, __) => await RefreshModelsAsync();
            textEndpoint.Leave += async (_, __) => await RefreshModelsAsync();
        }

        public bool AppliedChanges { get; private set; }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (keyData == Keys.Escape)
            {
                ButtonCancel_Click(this, EventArgs.Empty);
                return true;
            }

            return base.ProcessCmdKey(ref msg, keyData);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                cancellation?.Dispose();
                modelLoadingCancellation?.Cancel();
                modelLoadingCancellation?.Dispose();
                progressTimer?.Dispose();
                organizationService.Dispose();
            }
            base.Dispose(disposing);
        }

        private DataGridView CreatePlanGrid()
        {
            var grid = new DataGridView
            {
                Dock = DockStyle.Fill,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                AllowUserToResizeRows = false,
                AutoGenerateColumns = false,
                BackgroundColor = SystemColors.Window,
                BorderStyle = BorderStyle.FixedSingle,
                CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal,
                ColumnHeadersHeight = 34,
                ClipboardCopyMode = DataGridViewClipboardCopyMode.EnableAlwaysIncludeHeaderText,
                EnableHeadersVisualStyles = false,
                MultiSelect = false,
                RowHeadersVisible = false,
                RowTemplate = { Height = 31 },
                SelectionMode = DataGridViewSelectionMode.FullRowSelect
            };
            grid.Columns.Add(new DataGridViewCheckBoxColumn
            {
                HeaderText = string.Empty,
                Width = 42,
                TrueValue = true,
                FalseValue = false
            });
            grid.Columns.Add(new DataGridViewTextBoxColumn
            {
                HeaderText = Language.getString("aiOrganizerSource"),
                ReadOnly = true,
                Width = 220,
                SortMode = DataGridViewColumnSortMode.NotSortable
            });
            grid.Columns.Add(new DataGridViewTextBoxColumn
            {
                HeaderText = Language.getString("aiOrganizerDestination"),
                ReadOnly = true,
                Width = 270,
                SortMode = DataGridViewColumnSortMode.NotSortable
            });
            grid.Columns.Add(new DataGridViewTextBoxColumn
            {
                AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill,
                HeaderText = Language.getString("aiOrganizerReason"),
                ReadOnly = true,
                MinimumWidth = 180,
                SortMode = DataGridViewColumnSortMode.NotSortable
            });
            return grid;
        }

        private static ComboBox CreateContextModeComboBox()
        {
            var combo = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList };
            combo.Items.Add(Language.getString("aiOrganizerContextAuto"));
            combo.Items.Add(Language.getString("aiOrganizerContextMetadata"));
            combo.Items.Add(Language.getString("aiOrganizerContextContent"));
            return combo;
        }

        private static int ContextModeToIndex(AiOrganizerContextMode mode)
        {
            return mode == AiOrganizerContextMode.MetadataOnly ? 1
                : mode == AiOrganizerContextMode.MetadataAndContent ? 2
                : 0;
        }

        private AiOrganizerContextMode SelectedContextMode => comboContext.SelectedIndex == 1
            ? AiOrganizerContextMode.MetadataOnly
            : comboContext.SelectedIndex == 2
                ? AiOrganizerContextMode.MetadataAndContent
                : AiOrganizerContextMode.Auto;

        private AiOrganizerTaskMode SelectedTaskMode => comboTaskMode.SelectedIndex == 1
            ? AiOrganizerTaskMode.FolderSummary
            : AiOrganizerTaskMode.FilePlan;

        private static Button CreateButton(string text)
        {
            return new Button
            {
                AutoSize = true,
                MinimumSize = new Size(124, 38),
                Text = text,
                UseVisualStyleBackColor = true,
                Margin = new Padding(8, 0, 0, 0)
            };
        }

        private async Task AnalyzeAsync()
        {
            if (busy)
                return;

            OllamaModelDescriptor selectedDescriptor = FindSelectedModel();
            if (selectedDescriptor != null
                && selectedDescriptor.Capabilities.Count > 0
                && !selectedDescriptor.HasCapability("completion"))
            {
                MessageBox.Show(this, Language.getString("aiOrganizerModelNotCompletion"), Language.getString("Info"), MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            modelLoadingCancellation?.Cancel();
            gridPlan.Rows.Clear();
            textSummary.Text = string.Empty;
            analysisStarted = DateTime.Now;
            progressReceived = analysisStarted;
            latestProgress = null;
            SetBusy(true, Language.getString("aiOrganizerAnalyzing"), true);
            progressTimer.Start();
            cancellation = new CancellationTokenSource();
            try
            {
                var progress = new Progress<AiOrganizationProgress>(OnAnalysisProgress);
                AiOrganizationPlan plan = await organizationService.AnalyzeAsync(
                    rootPath,
                    textInstruction.Text,
                    textEndpoint.Text,
                    comboModel.Text,
                    SelectedContextMode,
                    SelectedTaskMode,
                    progress,
                    cancellation.Token);

                foreach (AiOrganizationAction action in plan.Actions)
                {
                    string explanation = action.Reason;
                    if (!string.IsNullOrWhiteSpace(action.Evidence))
                    {
                        explanation += "  " + string.Format(Language.getString("aiOrganizerEvidenceFormat"), action.Evidence);
                    }
                    int index = gridPlan.Rows.Add(true, action.SourceDisplay, action.DestinationDisplay, explanation);
                    gridPlan.Rows[index].Tag = action;
                }

                foreach (AiSkippedOrganizationAction skipped in plan.SkippedActions)
                {
                    int index = gridPlan.Rows.Add(
                        false,
                        skipped.SourceDisplay,
                        string.Empty,
                        string.Format(Language.getString("aiOrganizerSkippedFormat"), skipped.Reason));
                    DataGridViewRow row = gridPlan.Rows[index];
                    row.Cells[0].ReadOnly = true;
                    row.DefaultCellStyle.ForeColor = SystemColors.GrayText;
                    row.DefaultCellStyle.BackColor = Color.LemonChiffon;
                }

                foreach (AiRejectedOrganizationAction rejected in plan.RejectedActions)
                {
                    int index = gridPlan.Rows.Add(false, rejected.SourceDisplay, rejected.DestinationDisplay, rejected.RejectionReason);
                    DataGridViewRow row = gridPlan.Rows[index];
                    row.Cells[0].ReadOnly = true;
                    row.DefaultCellStyle.ForeColor = SystemColors.GrayText;
                    row.DefaultCellStyle.BackColor = Color.MistyRose;
                }

                if (plan.TaskMode == AiOrganizerTaskMode.FolderSummary)
                {
                    textSummary.Text = plan.Summary;
                    labelStatus.Text = Language.getString("aiOrganizerSummaryReady");
                }
                else
                {
                    textSummary.Text = string.Format(
                        Language.getString("aiOrganizerDecisionSummaryFormat"),
                        plan.Actions.Count,
                        plan.SkippedFiles,
                        plan.RejectedSuggestions,
                        plan.ProcessedBatches);
                    if (!string.IsNullOrWhiteSpace(plan.Summary))
                        textSummary.Text += Environment.NewLine + string.Format(Language.getString("aiOrganizerModelNoteFormat"), plan.Summary);
                    labelStatus.Text = plan.Actions.Count == 0
                        ? Language.getString("aiOrganizerNoActions")
                        : string.Format(Language.getString("aiOrganizerReadyFormat"), plan.Actions.Count);
                }
                if (plan.ContentFilesIncluded > 0)
                {
                    textSummary.Text += Environment.NewLine + string.Format(
                        Language.getString("aiOrganizerContentIncludedFormat"),
                        plan.ContentFilesIncluded,
                        plan.ContentCharactersIncluded);
                }
                if (plan.EvidenceRejectedSuggestions > 0)
                    textSummary.Text += Environment.NewLine + string.Format(Language.getString("aiOrganizerEvidenceRejectedFormat"), plan.EvidenceRejectedSuggestions);
                buttonExecute.Enabled = plan.TaskMode == AiOrganizerTaskMode.FilePlan && plan.Actions.Count > 0;
            }
            catch (OperationCanceledException)
            {
                labelStatus.Text = Language.getString("aiOrganizerCancelled");
            }
            catch (Exception ex)
            {
                LogService.LogException("FormAiOrganizer.Analyze", ex);
                labelStatus.Text = Language.getString("aiOrganizerFailed");
                MessageBox.Show(this, ex.Message, Language.getString("error"), MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                progressTimer.Stop();
                cancellation.Dispose();
                cancellation = null;
                SetBusy(false, labelStatus.Text);
            }
        }

        private async Task RefreshModelsAsync()
        {
            if (busy || IsDisposed)
                return;

            modelLoadingCancellation?.Cancel();
            modelLoadingCancellation?.Dispose();
            var loadingCancellation = new CancellationTokenSource();
            modelLoadingCancellation = loadingCancellation;
            string selectedModel = comboModel.Text.Trim();
            labelStatus.Text = Language.getString("aiOrganizerLoadingModels");

            try
            {
                IReadOnlyList<OllamaModelDescriptor> models = await organizationService.GetModelsAsync(
                    textEndpoint.Text,
                    loadingCancellation.Token);
                if (loadingCancellation.IsCancellationRequested || IsDisposed)
                    return;

                availableModels = models;
                List<OllamaModelDescriptor> suitableModels = models
                    .Where(item => item.Capabilities.Count == 0 || item.HasCapability("completion"))
                    .ToList();

                comboModel.BeginUpdate();
                try
                {
                    comboModel.Items.Clear();
                    foreach (OllamaModelDescriptor modelInfo in suitableModels)
                        comboModel.Items.Add(modelInfo);
                    if (!string.IsNullOrWhiteSpace(selectedModel)
                        && !suitableModels.Any(item => string.Equals(item.Name, selectedModel, StringComparison.OrdinalIgnoreCase)))
                    {
                        comboModel.Items.Insert(0, selectedModel);
                    }
                    comboModel.Text = selectedModel.Length > 0
                        ? selectedModel
                        : suitableModels.FirstOrDefault()?.Name ?? string.Empty;
                    int widestItem = comboModel.Items.Cast<object>()
                        .Select(item => TextRenderer.MeasureText(item?.ToString() ?? string.Empty, comboModel.Font).Width)
                        .DefaultIfEmpty(comboModel.Width)
                        .Max();
                    comboModel.DropDownWidth = Math.Max(
                        comboModel.Width,
                        Math.Min(widestItem + SystemInformation.VerticalScrollBarWidth + 18, Screen.FromControl(this).WorkingArea.Width - 40));
                }
                finally
                {
                    comboModel.EndUpdate();
                }

                UpdateModelInfo();
                labelStatus.Text = string.Format(Language.getString("aiOrganizerModelsLoadedFormat"), suitableModels.Count);
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex)
            {
                LogService.LogException("FormAiOrganizer.RefreshModels", ex);
                if (!IsDisposed)
                    labelStatus.Text = Language.getString("aiOrganizerModelsUnavailable");
            }
            finally
            {
                if (ReferenceEquals(modelLoadingCancellation, loadingCancellation))
                    modelLoadingCancellation = null;
                loadingCancellation.Dispose();
            }
        }

        private OllamaModelDescriptor FindSelectedModel()
        {
            string selectedName = comboModel.Text.Trim();
            return availableModels.FirstOrDefault(item => string.Equals(item.Name, selectedName, StringComparison.OrdinalIgnoreCase));
        }

        private void UpdateModelInfo()
        {
            if (labelModelInfo == null)
                return;

            OllamaModelDescriptor model = FindSelectedModel();
            if (model == null)
            {
                labelModelInfo.Text = Language.getString("aiOrganizerModelInfoUnknown");
                return;
            }

            string context = model.Details.ContextLength > 0
                ? FormatCompactNumber(model.Details.ContextLength)
                : "—";
            string capabilities = model.Capabilities.Count > 0
                ? string.Join(" • ", model.Capabilities.Select(ToDisplayCapability))
                : Language.getString("aiOrganizerCapabilitiesUnknown");
            labelModelInfo.Text = string.Join("  •  ", new[]
            {
                string.IsNullOrWhiteSpace(model.Details.ParameterSize) ? "—" : model.Details.ParameterSize,
                string.IsNullOrWhiteSpace(model.Details.QuantizationLevel) ? "—" : model.Details.QuantizationLevel,
                string.Format(Language.getString("aiOrganizerContextLengthFormat"), context),
                capabilities
            });
        }

        private void OnAnalysisProgress(AiOrganizationProgress progress)
        {
            latestProgress = progress;
            progressReceived = DateTime.Now;
            progressBar.Maximum = Math.Max(1, progress.TotalFiles);
            progressBar.Value = Math.Min(progressBar.Maximum, Math.Max(0, progress.CompletedFiles));
            UpdateProgressStatus();
        }

        private void UpdateProgressStatus()
        {
            if (!busy || latestProgress == null)
                return;

            TimeSpan elapsed = DateTime.Now - analysisStarted;
            if (latestProgress.IsFinalizing)
            {
                labelStatus.Text = string.Format(Language.getString("aiOrganizerFinalizingSummaryFormat"), FormatDuration(elapsed));
                return;
            }

            int currentBatch = latestProgress.CompletedBatches >= latestProgress.TotalBatches
                ? latestProgress.TotalBatches
                : latestProgress.CompletedBatches + 1;
            string remaining;
            string completion;
            if (latestProgress.EstimatedRemaining.HasValue && latestProgress.CompletedBatches > 0)
            {
                TimeSpan sinceProgress = DateTime.Now - progressReceived;
                TimeSpan adjustedRemaining = latestProgress.EstimatedRemaining.Value - sinceProgress;
                if (adjustedRemaining < TimeSpan.Zero)
                    adjustedRemaining = TimeSpan.Zero;
                remaining = FormatDuration(adjustedRemaining);
                completion = DateTime.Now.Add(adjustedRemaining).ToString("HH:mm");
            }
            else
            {
                remaining = Language.getString("aiOrganizerCalculating");
                completion = "—";
            }

            labelStatus.Text = string.Format(
                Language.getString("aiOrganizerProgressFormat"),
                currentBatch,
                latestProgress.TotalBatches,
                latestProgress.CompletedFiles,
                latestProgress.TotalFiles,
                FormatDuration(elapsed),
                remaining,
                completion);
        }

        private static string FormatDuration(TimeSpan value)
        {
            if (value.TotalHours >= 1)
                return string.Format("{0}:{1:00}:{2:00}", (int)value.TotalHours, value.Minutes, value.Seconds);
            return string.Format("{0:00}:{1:00}", (int)value.TotalMinutes, value.Seconds);
        }

        private static string FormatCompactNumber(long value)
        {
            if (value >= 1024 * 1024)
                return (value / (1024d * 1024d)).ToString("0.#") + "M";
            if (value >= 1024)
                return (value / 1024d).ToString("0.#") + "K";
            return value.ToString();
        }

        private static string ToDisplayCapability(string capability)
        {
            return string.IsNullOrWhiteSpace(capability)
                ? string.Empty
                : char.ToUpperInvariant(capability[0]) + capability.Substring(1);
        }

        private async Task ExecuteSelectedAsync()
        {
            if (busy)
                return;

            gridPlan.EndEdit();
            List<AiOrganizationAction> selected = gridPlan.Rows.Cast<DataGridViewRow>()
                .Where(row => Convert.ToBoolean(row.Cells[0].Value))
                .Select(row => row.Tag as AiOrganizationAction)
                .Where(action => action != null)
                .ToList();
            if (selected.Count == 0)
            {
                MessageBox.Show(this, Language.getString("aiOrganizerSelectActions"), Language.getString("Info"), MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            DialogResult confirmation = MessageBox.Show(
                this,
                string.Format(Language.getString("aiOrganizerConfirmFormat"), selected.Count),
                Language.getString("aiOrganizerTitle"),
                MessageBoxButtons.OKCancel,
                MessageBoxIcon.Warning,
                MessageBoxDefaultButton.Button2);
            if (confirmation != DialogResult.OK)
                return;

            SetBusy(true, Language.getString("aiOrganizerExecuting"), false);
            cancellation = new CancellationTokenSource();
            int completed = 0;
            int failed = 0;
            bool closeOnSuccess = false;
            try
            {
                for (int i = 0; i < selected.Count; i++)
                {
                    cancellation.Token.ThrowIfCancellationRequested();
                    AiOrganizationAction action = selected[i];
                    labelStatus.Text = string.Format(Language.getString("aiOrganizerExecutingFormat"), i + 1, selected.Count, action.SourceDisplay);

                    FileOperationResult result = await FileOperationService.ExecuteCopyOrMoveAsync(
                        new[] { action.SourcePath },
                        action.DestinationPath,
                        FormCopy.Type.Move,
                        false,
                        new Dictionary<string, FileConflictResolution>(StringComparer.OrdinalIgnoreCase),
                        null,
                        _ => FileOperationFailureAction.Skip,
                        cancellation.Token);

                    if (result.RunResult == FileOperationRunResult.Completed
                        && result.CompletedEntries > 0
                        && result.FailedEntries == 0)
                    {
                        completed++;
                    }
                    else
                    {
                        failed++;
                    }
                }

                AppliedChanges = completed > 0;
                labelStatus.Text = string.Format(Language.getString("aiOrganizerCompletedFormat"), completed, failed);
                MessageBox.Show(this, labelStatus.Text, Language.getString("aiOrganizerTitle"), MessageBoxButtons.OK,
                    failed == 0 ? MessageBoxIcon.Information : MessageBoxIcon.Warning);
                if (failed == 0)
                {
                    closeOnSuccess = true;
                }
            }
            catch (OperationCanceledException)
            {
                AppliedChanges = completed > 0;
                labelStatus.Text = Language.getString("aiOrganizerCancelled");
            }
            catch (Exception ex)
            {
                AppliedChanges = completed > 0;
                LogService.LogException("FormAiOrganizer.Execute", ex);
                labelStatus.Text = Language.getString("aiOrganizerFailed");
                MessageBox.Show(this, ex.Message, Language.getString("error"), MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                cancellation.Dispose();
                cancellation = null;
                SetBusy(false, labelStatus.Text);
            }

            if (closeOnSuccess)
            {
                DialogResult = DialogResult.OK;
                Close();
            }
        }

        private void SetBusy(bool value, string status, bool determinateProgress = false)
        {
            busy = value;
            textInstruction.Enabled = !value;
            textEndpoint.Enabled = !value;
            comboModel.Enabled = !value;
            comboContext.Enabled = !value;
            comboTaskMode.Enabled = !value;
            gridPlan.Enabled = !value;
            buttonAnalyze.Enabled = !value;
            buttonExecute.Enabled = !value && gridPlan.Rows.Cast<DataGridViewRow>().Any(row => row.Tag is AiOrganizationAction);
            buttonCancel.Text = value ? Language.getString("cancel") : Language.getString("close");
            progressBar.Visible = value;
            if (value)
            {
                progressBar.Style = determinateProgress ? ProgressBarStyle.Continuous : ProgressBarStyle.Marquee;
                if (determinateProgress)
                {
                    progressBar.Minimum = 0;
                    progressBar.Maximum = 1;
                    progressBar.Value = 0;
                }
            }
            labelStatus.Text = status ?? string.Empty;
        }

        private void ButtonCancel_Click(object sender, EventArgs e)
        {
            if (busy)
            {
                cancellation?.Cancel();
                labelStatus.Text = Language.getString("aiOrganizerCancelling");
                return;
            }

            Close();
        }

        private void FormAiOrganizer_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (!busy)
                return;

            cancellation?.Cancel();
            modelLoadingCancellation?.Cancel();
            labelStatus.Text = Language.getString("aiOrganizerCancelling");
            e.Cancel = true;
        }
    }
}
