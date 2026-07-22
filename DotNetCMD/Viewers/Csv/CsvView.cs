using DotNetCommander;
using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace View
{
    public sealed class CsvView : Form
    {
        private static readonly Size SavedDefaultSize = new Size(1100, 700);
        private static Point savedLocation = new Point(-1, -1);
        private static Size savedSize = Size.Empty;

        private readonly DataGridView dataGridView;
        private readonly ToolStrip toolStrip;
        private readonly ToolStripLabel fileLabel;
        private readonly ToolStripButton reloadButton;
        private readonly ToolStripButton cancelButton;
        private readonly StatusStrip statusStrip;
        private readonly ToolStripStatusLabel infoStatusLabel;
        private readonly ToolStripStatusLabel countStatusLabel;
        private readonly ToolStripProgressBar progressBar;
        private readonly CsvTableLoader loadService = new CsvTableLoader();
        private readonly Dictionary<string, TimeSpan> knownDurations = new Dictionary<string, TimeSpan>(StringComparer.OrdinalIgnoreCase);

        private string currentFilePath;
        private CancellationTokenSource loadCancellationTokenSource;
        private bool isLoading;
        private DateTime lastUiProgressUpdateUtc = DateTime.MinValue;
        private string lastUiProgressPhase;

        private const long LargeFileThresholdBytes = 512L * 1024L;
        private const int SlowLoadThresholdMs = 250;
        private static readonly TimeSpan UiProgressUpdateInterval = TimeSpan.FromMilliseconds(100);

        public CsvView()
        {
            Text = Language.getString("csvViewerTitle");
            StartPosition = FormStartPosition.Manual;
            MinimumSize = new Size(720, 420);
            KeyPreview = true;

            toolStrip = new ToolStrip
            {
                GripStyle = ToolStripGripStyle.Hidden,
                Dock = DockStyle.Top,
                Padding = new Padding(8, 6, 8, 6),
                BackColor = Color.FromArgb(245, 247, 250),
                RenderMode = ToolStripRenderMode.System
            };

            fileLabel = new ToolStripLabel
            {
                TextAlign = ContentAlignment.MiddleLeft,
                AutoSize = false,
                Width = 760
            };

            reloadButton = new ToolStripButton(Language.getString("view"));
            reloadButton.Click += async (_, __) => await ReloadAsync();

            cancelButton = new ToolStripButton(Language.getString("cancel"))
            {
                Enabled = false
            };
            cancelButton.Click += (_, __) => loadCancellationTokenSource?.Cancel();

            toolStrip.Items.Add(fileLabel);
            toolStrip.Items.Add(reloadButton);
            toolStrip.Items.Add(cancelButton);

            dataGridView = new DataGridView
            {
                Dock = DockStyle.Fill,
                ReadOnly = true,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                AllowUserToResizeRows = false,
                RowHeadersVisible = false,
                MultiSelect = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                ClipboardCopyMode = DataGridViewClipboardCopyMode.EnableAlwaysIncludeHeaderText,
                BackgroundColor = Color.White,
                BorderStyle = BorderStyle.None,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.DisplayedCells,
                ColumnHeadersDefaultCellStyle = new DataGridViewCellStyle
                {
                    BackColor = Color.FromArgb(240, 244, 248),
                    Font = new Font("Segoe UI", 12f, FontStyle.Bold),
                    SelectionBackColor = Color.FromArgb(227, 239, 255)
                },
                DefaultCellStyle = new DataGridViewCellStyle
                {
                    Font = new Font("Segoe UI", 12f),
                    SelectionBackColor = Color.FromArgb(227, 239, 255),
                    SelectionForeColor = Color.Black
                },
                EnableHeadersVisualStyles = false
            };

            statusStrip = new StatusStrip();
            infoStatusLabel = new ToolStripStatusLabel
            {
                Spring = true,
                TextAlign = ContentAlignment.MiddleLeft
            };
            countStatusLabel = new ToolStripStatusLabel();
            progressBar = new ToolStripProgressBar
            {
                Visible = false,
                Minimum = 0,
                Maximum = 100,
                Size = new Size(140, 18)
            };

            statusStrip.Items.Add(infoStatusLabel);
            statusStrip.Items.Add(countStatusLabel);
            statusStrip.Items.Add(progressBar);

            Controls.Add(dataGridView);
            Controls.Add(statusStrip);
            Controls.Add(toolStrip);

            Load += CsvView_Load;
            FormClosing += CsvView_FormClosing;
        }

        public async void LoadFile(string filePath)
        {
            currentFilePath = filePath;
            fileLabel.Text = filePath;
            Text = $"{Path.GetFileName(filePath)} - {Language.getString("csvViewerTitle")}";
            await LoadFileAsync(filePath);
        }

        private void CsvView_Load(object sender, EventArgs e)
        {
            Size = savedSize.IsEmpty ? SavedDefaultSize : savedSize;
            if (savedLocation.X >= 0 && savedLocation.Y >= 0)
            {
                Location = savedLocation;
            }
            else
            {
                StartPosition = FormStartPosition.CenterScreen;
            }
        }

        private void CsvView_FormClosing(object sender, FormClosingEventArgs e)
        {
            savedLocation = Location;
            savedSize = Size;
            loadCancellationTokenSource?.Cancel();
            loadCancellationTokenSource?.Dispose();
            loadCancellationTokenSource = null;
        }

        private async Task ReloadAsync()
        {
            if (string.IsNullOrEmpty(currentFilePath) || !File.Exists(currentFilePath))
            {
                return;
            }

            await LoadFileAsync(currentFilePath);
        }

        private async Task LoadFileAsync(string filePath)
        {
            if (isLoading || string.IsNullOrEmpty(filePath))
            {
                return;
            }

            try
            {
                isLoading = true;
                loadCancellationTokenSource?.Dispose();
                loadCancellationTokenSource = new CancellationTokenSource();

                bool showProgress = ShouldUseBackgroundLoad(filePath);
                TimeSpan? knownDuration = GetKnownLoadDuration(filePath);
                var stopwatch = Stopwatch.StartNew();

                DataTable dataTable;
                if (showProgress)
                {
                    SetLoadingState(true, knownDuration);
                    var progress = new Progress<CsvLoadProgress>(UpdateLoadProgress);
                    dataTable = await loadService.LoadAsync(filePath, progress, loadCancellationTokenSource.Token);
                }
                else
                {
                    SetLoadingState(false, knownDuration);
                    dataTable = loadService.Load(filePath, null, CancellationToken.None);
                }

                stopwatch.Stop();
                knownDurations[filePath] = stopwatch.Elapsed;

                dataGridView.DataSource = dataTable;
                AlignNumericColumns(dataTable);

                infoStatusLabel.Text = string.Format(Language.getString("csvViewerLoadedFormat"), Path.GetFileName(filePath));
                countStatusLabel.Text = string.Format(
                    Language.getString("csvViewerRowsFormat"),
                    dataTable.Rows.Count,
                    dataTable.Columns.Count,
                    FormatDuration(stopwatch.Elapsed));
                progressBar.Visible = showProgress;
                progressBar.Value = showProgress ? 100 : 0;
            }
            catch (OperationCanceledException)
            {
                infoStatusLabel.Text = Language.getString("csvViewerCancelled");
                countStatusLabel.Text = string.Empty;
                progressBar.Value = 0;
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, Language.getString("error"), MessageBoxButtons.OK, MessageBoxIcon.Error);
                infoStatusLabel.Text = Language.getString("csvViewerLoadFailed");
                countStatusLabel.Text = string.Empty;
                progressBar.Value = 0;
            }
            finally
            {
                isLoading = false;
                SetLoadingState(false, null);
            }
        }

        private bool ShouldUseBackgroundLoad(string filePath)
        {
            long fileLength = new FileInfo(filePath).Length;
            if (fileLength >= LargeFileThresholdBytes)
            {
                return true;
            }

            return knownDurations.TryGetValue(filePath, out TimeSpan knownDuration)
                && knownDuration.TotalMilliseconds >= SlowLoadThresholdMs;
        }

        private TimeSpan? GetKnownLoadDuration(string filePath)
        {
            return knownDurations.TryGetValue(filePath, out TimeSpan knownDuration)
                ? knownDuration
                : null;
        }

        private void SetLoadingState(bool showProgress, TimeSpan? knownDuration)
        {
            reloadButton.Enabled = !isLoading;
            cancelButton.Enabled = isLoading && showProgress;
            dataGridView.Enabled = !isLoading;
            progressBar.Visible = isLoading && showProgress;

            if (!isLoading)
            {
                return;
            }

            lastUiProgressUpdateUtc = DateTime.MinValue;
            lastUiProgressPhase = null;
            progressBar.Value = 0;
            infoStatusLabel.Text = knownDuration.HasValue
                ? string.Format(Language.getString("csvViewerLoadingEstimatedFormat"), FormatDuration(knownDuration.Value))
                : Language.getString("csvViewerLoading");
            countStatusLabel.Text = string.Empty;
        }

        private void UpdateLoadProgress(CsvLoadProgress progress)
        {
            if (progress == null)
            {
                return;
            }

            DateTime nowUtc = DateTime.UtcNow;
            bool phaseChanged = !string.Equals(lastUiProgressPhase, progress.Phase, StringComparison.Ordinal);
            bool completed = progress.Percent >= 100 || string.Equals(progress.Phase, "Completed", StringComparison.Ordinal);
            bool intervalElapsed = nowUtc - lastUiProgressUpdateUtc >= UiProgressUpdateInterval;

            if (!completed && !phaseChanged && !intervalElapsed)
            {
                return;
            }

            lastUiProgressUpdateUtc = nowUtc;
            lastUiProgressPhase = progress.Phase;
            progressBar.Visible = true;
            progressBar.Value = Math.Max(progressBar.Minimum, Math.Min(progressBar.Maximum, progress.Percent));
            infoStatusLabel.Text = string.Format(Language.getString("csvViewerProgressFormat"), progress.Phase, progress.Percent);
            countStatusLabel.Text = string.Format(
                Language.getString("csvViewerBytesFormat"),
                progress.RowsRead,
                FormatBytes(progress.BytesRead),
                FormatBytes(progress.TotalBytes));
        }

        private void AlignNumericColumns(DataTable dataTable)
        {
            foreach (DataGridViewColumn column in dataGridView.Columns)
            {
                if (!IsNumericColumn(dataTable, column.Index))
                {
                    continue;
                }

                column.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
            }
        }

        private static bool IsNumericColumn(DataTable dataTable, int columnIndex)
        {
            int rowsToCheck = Math.Min(20, dataTable.Rows.Count);
            for (int rowIndex = 0; rowIndex < rowsToCheck; rowIndex++)
            {
                string value = dataTable.Rows[rowIndex][columnIndex]?.ToString();
                if (string.IsNullOrWhiteSpace(value))
                {
                    continue;
                }

                if (!double.TryParse(
                    value,
                    System.Globalization.NumberStyles.Any,
                    System.Globalization.CultureInfo.InvariantCulture,
                    out _))
                {
                    return false;
                }
            }

            return rowsToCheck > 0;
        }

        private static string FormatDuration(TimeSpan duration)
        {
            return duration.TotalSeconds >= 1
                ? $"{duration.TotalSeconds:F2}s"
                : $"{duration.TotalMilliseconds:F0}ms";
        }

        private static string FormatBytes(long bytes)
        {
            string[] units = { "B", "KB", "MB", "GB" };
            double value = bytes;
            int unitIndex = 0;

            while (value >= 1024 && unitIndex < units.Length - 1)
            {
                value /= 1024;
                unitIndex++;
            }

            return unitIndex == 0 ? $"{value:F0} {units[unitIndex]}" : $"{value:F1} {units[unitIndex]}";
        }
    }

}
