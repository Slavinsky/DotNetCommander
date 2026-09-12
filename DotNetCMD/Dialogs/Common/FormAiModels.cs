using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace DotNetCommander
{
    internal sealed class FormAiModels : Form
    {
        private readonly string endpoint;
        private readonly string initialModelName;
        private readonly AiOrganizationService service = new AiOrganizationService();
        private readonly ComboBox filterComboBox;
        private readonly DataGridView modelsGrid;
        private readonly TextBox detailsTextBox;
        private readonly Label statusLabel;
        private readonly ProgressBar progressBar;
        private readonly Button useButton;
        private readonly Button refreshButton;
        private CancellationTokenSource cancellation;
        private IReadOnlyList<OllamaModelDescriptor> models = Array.Empty<OllamaModelDescriptor>();

        public FormAiModels(string endpoint, string initialModelName)
        {
            this.endpoint = endpoint;
            this.initialModelName = initialModelName ?? string.Empty;
            Text = Language.getString("aiModelsTitle");
            StartPosition = FormStartPosition.CenterParent;
            MinimumSize = new Size(820, 560);
            Size = new Size(1040, 700);
            ShowIcon = false;
            KeyPreview = true;

            var root = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(16),
                ColumnCount = 1,
                RowCount = 6
            };
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 62));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 38));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            var header = new Label
            {
                AutoSize = true,
                Font = DialogStyleService.CreateHeaderFont(),
                Text = Language.getString("aiModelsHeading"),
                Margin = new Padding(0, 0, 0, 8)
            };

            var filterPanel = new FlowLayoutPanel
            {
                AutoSize = true,
                Dock = DockStyle.Fill,
                WrapContents = false,
                Margin = new Padding(0, 0, 0, 8)
            };
            filterPanel.Controls.Add(new Label
            {
                AutoSize = true,
                Text = Language.getString("aiModelsFilter"),
                Margin = new Padding(0, 7, 8, 0)
            });
            filterComboBox = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 230 };
            filterComboBox.Items.Add(Language.getString("aiModelsFilterSuitable"));
            filterComboBox.Items.Add(Language.getString("aiModelsFilterVision"));
            filterComboBox.Items.Add(Language.getString("aiModelsFilterTools"));
            filterComboBox.Items.Add(Language.getString("aiModelsFilterThinking"));
            filterComboBox.Items.Add(Language.getString("aiModelsFilterEmbedding"));
            filterComboBox.Items.Add(Language.getString("aiModelsFilterAll"));
            filterComboBox.SelectedIndex = 0;
            filterComboBox.SelectedIndexChanged += (_, __) => ApplyFilter();
            filterPanel.Controls.Add(filterComboBox);

            modelsGrid = CreateModelsGrid();
            modelsGrid.SelectionChanged += (_, __) => ShowSelectedDetails();
            modelsGrid.CellDoubleClick += (_, e) =>
            {
                if (e.RowIndex >= 0)
                    UseSelectedModel();
            };

            detailsTextBox = new TextBox
            {
                Dock = DockStyle.Fill,
                Multiline = true,
                ReadOnly = true,
                ScrollBars = ScrollBars.Both,
                WordWrap = false
            };

            var statusPanel = new TableLayoutPanel { AutoSize = true, Dock = DockStyle.Fill, ColumnCount = 2 };
            statusPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            statusPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 180));
            statusLabel = new Label { AutoEllipsis = true, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft };
            progressBar = new ProgressBar { Dock = DockStyle.Fill, Style = ProgressBarStyle.Marquee, Visible = false };
            statusPanel.Controls.Add(statusLabel, 0, 0);
            statusPanel.Controls.Add(progressBar, 1, 0);

            var buttons = new FlowLayoutPanel
            {
                AutoSize = true,
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.RightToLeft,
                WrapContents = false,
                Margin = new Padding(0, 12, 0, 0)
            };
            var closeButton = CreateButton(Language.getString("close"));
            closeButton.DialogResult = DialogResult.Cancel;
            useButton = CreateButton(Language.getString("aiModelsUse"));
            useButton.Enabled = false;
            useButton.Click += (_, __) => UseSelectedModel();
            refreshButton = CreateButton(Language.getString("aiModelsRefresh"));
            refreshButton.Click += async (_, __) => await LoadModelsAsync();
            buttons.Controls.Add(closeButton);
            buttons.Controls.Add(useButton);
            buttons.Controls.Add(refreshButton);

            root.Controls.Add(header, 0, 0);
            root.Controls.Add(filterPanel, 0, 1);
            root.Controls.Add(modelsGrid, 0, 2);
            root.Controls.Add(detailsTextBox, 0, 3);
            root.Controls.Add(statusPanel, 0, 4);
            root.Controls.Add(buttons, 0, 5);
            Controls.Add(root);

            DialogStyleService.ApplyDialogFont(this);
            CancelButton = closeButton;
            Shown += async (_, __) => await LoadModelsAsync();
            FormClosing += (_, __) => cancellation?.Cancel();
        }

        public string SelectedModelName { get; private set; }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (keyData == Keys.Escape)
            {
                Close();
                return true;
            }
            return base.ProcessCmdKey(ref msg, keyData);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                cancellation?.Cancel();
                cancellation?.Dispose();
                service.Dispose();
            }
            base.Dispose(disposing);
        }

        private async Task LoadModelsAsync()
        {
            cancellation?.Cancel();
            cancellation?.Dispose();
            cancellation = new CancellationTokenSource();
            SetBusy(true, Language.getString("aiOrganizerLoadingModels"));
            try
            {
                models = await service.GetModelsAsync(endpoint, cancellation.Token);
                ApplyFilter();
                statusLabel.Text = string.Format(Language.getString("aiOrganizerModelsLoadedFormat"), models.Count);
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex)
            {
                LogService.LogException("FormAiModels.Load", ex);
                statusLabel.Text = Language.getString("aiOrganizerModelsUnavailable");
                MessageBox.Show(this, ex.Message, Language.getString("error"), MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                if (!IsDisposed)
                    SetBusy(false, statusLabel.Text);
            }
        }

        private void ApplyFilter()
        {
            if (modelsGrid == null)
                return;

            IEnumerable<OllamaModelDescriptor> filtered = filterComboBox.SelectedIndex switch
            {
                0 => models.Where(IsSuitablePlannerModel),
                1 => models.Where(model => model.HasCapability("vision")),
                2 => models.Where(model => model.HasCapability("tools")),
                3 => models.Where(model => model.HasCapability("thinking")),
                4 => models.Where(model => model.HasCapability("embedding")),
                _ => models
            };

            modelsGrid.Rows.Clear();
            int selectedIndex = -1;
            foreach (OllamaModelDescriptor model in filtered)
            {
                int rowIndex = modelsGrid.Rows.Add(
                    model.Name,
                    model.Details.ParameterSize,
                    model.Details.QuantizationLevel,
                    FormatCompactNumber(model.Details.ContextLength),
                    FormatBytes(model.Size),
                    string.Join(", ", model.Capabilities ?? new List<string>()));
                modelsGrid.Rows[rowIndex].Tag = model;
                if (string.Equals(model.Name, initialModelName, StringComparison.OrdinalIgnoreCase))
                    selectedIndex = rowIndex;
            }

            if (modelsGrid.Rows.Count > 0)
            {
                int rowIndex = selectedIndex >= 0 ? selectedIndex : 0;
                modelsGrid.Rows[rowIndex].Selected = true;
                modelsGrid.CurrentCell = modelsGrid.Rows[rowIndex].Cells[0];
            }
            else
            {
                detailsTextBox.Clear();
                useButton.Enabled = false;
            }
        }

        private void ShowSelectedDetails()
        {
            OllamaModelDescriptor model = SelectedModel();
            useButton.Enabled = model != null && IsSuitablePlannerModel(model);
            if (model == null)
            {
                detailsTextBox.Clear();
                return;
            }

            var text = new StringBuilder();
            AddDetail(text, Language.getString("aiModelsName"), model.Name);
            AddDetail(text, Language.getString("aiModelsFamily"), model.Details.Family);
            AddDetail(text, Language.getString("aiModelsParent"), model.Details.ParentModel);
            AddDetail(text, Language.getString("aiModelsFormat"), model.Details.Format);
            AddDetail(text, Language.getString("aiModelsParameters"), model.Details.ParameterSize);
            AddDetail(text, Language.getString("aiModelsQuantization"), model.Details.QuantizationLevel);
            AddDetail(text, Language.getString("aiModelsContext"), model.Details.ContextLength > 0 ? model.Details.ContextLength.ToString("N0") : string.Empty);
            AddDetail(text, Language.getString("aiModelsEmbedding"), model.Details.EmbeddingLength > 0 ? model.Details.EmbeddingLength.ToString("N0") : string.Empty);
            AddDetail(text, Language.getString("aiModelsCapabilities"), string.Join(", ", model.Capabilities ?? new List<string>()));
            AddDetail(text, Language.getString("aiModelsDiskSize"), FormatBytes(model.Size));
            AddDetail(text, Language.getString("aiModelsModified"), model.ModifiedAt);
            AddDetail(text, Language.getString("aiModelsDigest"), model.Digest);
            detailsTextBox.Text = text.ToString();
        }

        private void UseSelectedModel()
        {
            OllamaModelDescriptor model = SelectedModel();
            if (model == null || !IsSuitablePlannerModel(model))
                return;
            SelectedModelName = model.Name;
            DialogResult = DialogResult.OK;
            Close();
        }

        private OllamaModelDescriptor SelectedModel()
        {
            return modelsGrid.CurrentRow?.Tag as OllamaModelDescriptor;
        }

        private static bool IsSuitablePlannerModel(OllamaModelDescriptor model)
        {
            return model != null && (model.Capabilities.Count == 0 || model.HasCapability("completion"));
        }

        private void SetBusy(bool value, string status)
        {
            filterComboBox.Enabled = !value;
            modelsGrid.Enabled = !value;
            refreshButton.Enabled = !value;
            useButton.Enabled = !value && SelectedModel() != null && IsSuitablePlannerModel(SelectedModel());
            progressBar.Visible = value;
            statusLabel.Text = status ?? string.Empty;
        }

        private static DataGridView CreateModelsGrid()
        {
            var grid = new DataGridView
            {
                Dock = DockStyle.Fill,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                AllowUserToResizeRows = false,
                AutoGenerateColumns = false,
                BackgroundColor = SystemColors.Window,
                ReadOnly = true,
                RowHeadersVisible = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                MultiSelect = false,
                ClipboardCopyMode = DataGridViewClipboardCopyMode.EnableAlwaysIncludeHeaderText
            };
            AddColumn(grid, Language.getString("aiModelsName"), 245);
            AddColumn(grid, Language.getString("aiModelsParameters"), 85);
            AddColumn(grid, Language.getString("aiModelsQuantization"), 95);
            AddColumn(grid, Language.getString("aiModelsContext"), 80);
            AddColumn(grid, Language.getString("aiModelsDiskSize"), 85);
            var capabilities = AddColumn(grid, Language.getString("aiModelsCapabilities"), 220);
            capabilities.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            return grid;
        }

        private static DataGridViewTextBoxColumn AddColumn(DataGridView grid, string header, int width)
        {
            var column = new DataGridViewTextBoxColumn
            {
                HeaderText = header,
                Width = width,
                SortMode = DataGridViewColumnSortMode.Automatic
            };
            grid.Columns.Add(column);
            return column;
        }

        private static Button CreateButton(string text)
        {
            return new Button
            {
                AutoSize = true,
                MinimumSize = new Size(120, 36),
                Text = text,
                UseVisualStyleBackColor = true,
                Margin = new Padding(8, 0, 0, 0)
            };
        }

        private static void AddDetail(StringBuilder text, string label, string value)
        {
            if (!string.IsNullOrWhiteSpace(value))
                text.Append(label).Append(": ").AppendLine(value);
        }

        private static string FormatBytes(long bytes)
        {
            return bytes <= 0 ? "—" : (bytes / (1024d * 1024d * 1024d)).ToString("0.0", CultureInfo.CurrentCulture) + " GB";
        }

        private static string FormatCompactNumber(long value)
        {
            if (value <= 0)
                return "—";
            if (value >= 1024 * 1024)
                return (value / (1024d * 1024d)).ToString("0.#", CultureInfo.CurrentCulture) + "M";
            if (value >= 1024)
                return (value / 1024d).ToString("0.#", CultureInfo.CurrentCulture) + "K";
            return value.ToString(CultureInfo.CurrentCulture);
        }
    }
}
