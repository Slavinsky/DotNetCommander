using DotNetCommander;
using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace View
{
    public sealed class FileCompareForm : Form
    {
        private const long MaxTextCompareBytes = 1024L * 1024L;
        private const int BinaryPreviewBytes = 256;
        private static readonly Size DefaultWindowSize = new Size(1280, 820);
        private static Point savedLocation = new Point(-1, -1);
        private static Size savedSize = Size.Empty;

        private readonly ToolStrip toolStrip;
        private readonly ToolStripLabel leftLabel;
        private readonly ToolStripLabel modeLabel;
        private readonly ToolStripLabel rightLabel;
        private readonly StatusStrip statusStrip;
        private readonly ToolStripStatusLabel summaryStatusLabel;
        private readonly ToolStripStatusLabel detailStatusLabel;
        private readonly Panel contentHost;

        private string leftPath;
        private string rightPath;

        public FileCompareForm()
        {
            Text = Language.getString("compareWindowTitle");
            MinimumSize = new Size(900, 560);
            StartPosition = FormStartPosition.Manual;
            KeyPreview = true;

            toolStrip = new ToolStrip
            {
                Dock = DockStyle.Top,
                GripStyle = ToolStripGripStyle.Hidden,
                Padding = new Padding(8, 6, 8, 6),
                BackColor = Color.FromArgb(245, 247, 250),
                RenderMode = ToolStripRenderMode.System
            };

            leftLabel = new ToolStripLabel
            {
                AutoSize = false,
                Width = 370,
                TextAlign = ContentAlignment.MiddleLeft
            };

            modeLabel = new ToolStripLabel
            {
                AutoSize = false,
                Width = 220,
                TextAlign = ContentAlignment.MiddleCenter,
                Font = new Font("Segoe UI", 9f, FontStyle.Bold)
            };

            rightLabel = new ToolStripLabel
            {
                AutoSize = false,
                Width = 370,
                TextAlign = ContentAlignment.MiddleRight
            };

            toolStrip.Items.Add(leftLabel);
            toolStrip.Items.Add(new ToolStripSeparator());
            toolStrip.Items.Add(modeLabel);
            toolStrip.Items.Add(new ToolStripSeparator());
            toolStrip.Items.Add(rightLabel);

            statusStrip = new StatusStrip();
            summaryStatusLabel = new ToolStripStatusLabel
            {
                Spring = true,
                TextAlign = ContentAlignment.MiddleLeft
            };
            detailStatusLabel = new ToolStripStatusLabel();
            statusStrip.Items.Add(summaryStatusLabel);
            statusStrip.Items.Add(detailStatusLabel);

            contentHost = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = SystemColors.Window
            };

            Controls.Add(contentHost);
            Controls.Add(statusStrip);
            Controls.Add(toolStrip);
        }

        public async void LoadFiles(string leftFilePath, string rightFilePath)
        {
            leftPath = leftFilePath;
            rightPath = rightFilePath;

            leftLabel.Text = leftFilePath;
            rightLabel.Text = rightFilePath;
            Text = string.Format(
                CultureInfo.CurrentCulture,
                Language.getString("compareWindowTitleFormat"),
                Path.GetFileName(leftFilePath),
                Path.GetFileName(rightFilePath));

            await LoadComparisonAsync();
        }

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);

            Size = savedSize.IsEmpty ? DefaultWindowSize : savedSize;
            if (savedLocation.X >= 0 && savedLocation.Y >= 0)
            {
                Location = savedLocation;
            }
            else
            {
                StartPosition = FormStartPosition.CenterParent;
            }
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            savedLocation = Location;
            savedSize = Size;
            base.OnFormClosing(e);
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (keyData == Keys.Escape)
            {
                Close();
                return true;
            }

            return base.ProcessCmdKey(ref msg, keyData);
        }

        private async Task LoadComparisonAsync()
        {
            contentHost.Controls.Clear();
            ShowPlaceholder(Language.getString("compareLoading"));

            try
            {
                FileComparisonMode mode = FileTypeClassifier.GetComparisonMode(leftPath, rightPath);
                switch (mode)
                {
                    case FileComparisonMode.Text:
                        await ShowTextComparisonAsync();
                        break;
                    case FileComparisonMode.Csv:
                        await ShowCsvComparisonAsync();
                        break;
                    case FileComparisonMode.Image:
                        await ShowImageComparisonAsync();
                        break;
                    default:
                        await ShowBinaryComparisonAsync(null);
                        break;
                }
            }
            catch (Exception ex)
            {
                LogService.LogException("FileCompareForm.LoadComparisonAsync", ex);
                ShowPlaceholder(ex.Message);
                summaryStatusLabel.Text = Language.getString("compareLoadFailed");
                detailStatusLabel.Text = string.Empty;
            }
        }

        private async Task ShowTextComparisonAsync()
        {
            long leftSize = new FileInfo(leftPath).Length;
            long rightSize = new FileInfo(rightPath).Length;
            if (leftSize > MaxTextCompareBytes || rightSize > MaxTextCompareBytes)
            {
                await ShowBinaryComparisonAsync(Language.getString("compareTextTooLarge"));
                return;
            }

            TextDiffResult diffResult = await Task.Run(() =>
            {
                string leftText = ReadTextFile(leftPath);
                string rightText = ReadTextFile(rightPath);
                return TextDiffBuilder.Build(leftText, rightText);
            });

            var grid = CreateTextGrid();
            foreach (TextDiffRow row in diffResult.Rows)
            {
                int index = grid.Rows.Add(row.LeftLineNumberText, row.LeftText, row.RightLineNumberText, row.RightText);
                ApplyTextRowStyle(grid.Rows[index], row.State);
            }

            contentHost.Controls.Clear();
            contentHost.Controls.Add(grid);

            modeLabel.Text = Language.getString("compareModeText");
            summaryStatusLabel.Text = diffResult.AreEqual
                ? Language.getString("compareSummaryIdentical")
                : string.Format(
                    CultureInfo.CurrentCulture,
                    Language.getString("compareSummaryTextFormat"),
                    diffResult.ModifiedCount,
                    diffResult.LeftOnlyCount,
                    diffResult.RightOnlyCount);
            detailStatusLabel.Text = string.Format(
                CultureInfo.CurrentCulture,
                Language.getString("compareLinesFormat"),
                diffResult.Rows.Count);
        }

        private async Task ShowCsvComparisonAsync()
        {
            CsvTableLoader leftLoader = new CsvTableLoader();
            CsvTableLoader rightLoader = new CsvTableLoader();
            DataTable leftTable = await leftLoader.LoadAsync(leftPath, null, default);
            DataTable rightTable = await rightLoader.LoadAsync(rightPath, null, default);

            var split = CreateSplitContainer();
            DataGridView leftGrid = CreateCsvGrid();
            DataGridView rightGrid = CreateCsvGrid();
            leftGrid.DataSource = leftTable;
            rightGrid.DataSource = rightTable;
            split.Panel1.Controls.Add(leftGrid);
            split.Panel2.Controls.Add(rightGrid);
            contentHost.Controls.Clear();
            contentHost.Controls.Add(split);

            int differenceCount = HighlightCsvDifferences(leftGrid, rightGrid, leftTable, rightTable);
            modeLabel.Text = Language.getString("compareModeCsv");
            summaryStatusLabel.Text = differenceCount == 0
                ? Language.getString("compareSummaryIdentical")
                : string.Format(CultureInfo.CurrentCulture, Language.getString("compareSummaryCsvFormat"), differenceCount);
            detailStatusLabel.Text = string.Format(
                CultureInfo.CurrentCulture,
                Language.getString("compareCsvShapeFormat"),
                leftTable.Rows.Count,
                leftTable.Columns.Count,
                rightTable.Rows.Count,
                rightTable.Columns.Count);
        }

        private async Task ShowImageComparisonAsync()
        {
            ImageCompareSnapshot snapshot = await Task.Run(() => BuildImageSnapshot(leftPath, rightPath));
            var split = CreateSplitContainer();
            ConfigureImageSplitLayout(split, snapshot.LeftMetadata.ImageSize, snapshot.RightMetadata.ImageSize);
            split.Panel1.Controls.Add(CreateImagePanel(snapshot.LeftPreview, snapshot.LeftMetadata));
            split.Panel2.Controls.Add(CreateImagePanel(snapshot.RightPreview, snapshot.RightMetadata));

            contentHost.Controls.Clear();
            contentHost.Controls.Add(split);

            modeLabel.Text = Language.getString("compareModeImage");
            summaryStatusLabel.Text = snapshot.HashesEqual
                ? Language.getString("compareSummaryIdentical")
                : string.Format(
                    CultureInfo.CurrentCulture,
                    Language.getString("compareSummaryImageFormat"),
                    snapshot.LeftMetadata.ImageStatusText,
                    snapshot.RightMetadata.ImageStatusText);
            detailStatusLabel.Text = string.Format(
                CultureInfo.CurrentCulture,
                Language.getString("compareHashesFormat"),
                snapshot.LeftMetadata.HashShort,
                snapshot.RightMetadata.HashShort);
        }

        private async Task ShowBinaryComparisonAsync(string detailOverride)
        {
            BinaryCompareSnapshot snapshot = await Task.Run(() => BuildBinarySnapshot(leftPath, rightPath));
            var split = CreateSplitContainer();
            split.Panel1.Controls.Add(CreateBinaryPanel(snapshot.LeftMetadata, snapshot.LeftHexPreview));
            split.Panel2.Controls.Add(CreateBinaryPanel(snapshot.RightMetadata, snapshot.RightHexPreview));

            contentHost.Controls.Clear();
            contentHost.Controls.Add(split);

            modeLabel.Text = Language.getString("compareModeBinary");
            summaryStatusLabel.Text = snapshot.HashesEqual
                ? Language.getString("compareSummaryIdentical")
                : Language.getString("compareSummaryBinaryDifferent");
            detailStatusLabel.Text = string.IsNullOrWhiteSpace(detailOverride)
                ? string.Format(
                    CultureInfo.CurrentCulture,
                    Language.getString("compareHashesFormat"),
                    snapshot.LeftMetadata.HashShort,
                    snapshot.RightMetadata.HashShort)
                : detailOverride;
        }

        private void ShowPlaceholder(string message)
        {
            var label = new Label
            {
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleCenter,
                ForeColor = SystemColors.GrayText,
                Text = message
            };

            contentHost.Controls.Clear();
            contentHost.Controls.Add(label);
        }

        private static string ReadTextFile(string path)
        {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using var reader = new StreamReader(stream, Encoding.UTF8, true);
            string content = reader.ReadToEnd();
            return content.Replace("\r\n", "\n").Replace("\r", "\n");
        }

        private static DataGridView CreateTextGrid()
        {
            var grid = new DataGridView
            {
                Dock = DockStyle.Fill,
                ReadOnly = true,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                AllowUserToResizeRows = false,
                RowHeadersVisible = false,
                AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.AllCells,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                MultiSelect = false,
                ClipboardCopyMode = DataGridViewClipboardCopyMode.EnableAlwaysIncludeHeaderText,
                BackgroundColor = Color.White,
                BorderStyle = BorderStyle.None,
                EnableHeadersVisualStyles = false,
                ColumnHeadersDefaultCellStyle = new DataGridViewCellStyle
                {
                    BackColor = Color.FromArgb(240, 244, 248),
                    Font = new Font("Segoe UI", 9.5f, FontStyle.Bold)
                },
                DefaultCellStyle = new DataGridViewCellStyle
                {
                    Font = new Font("Consolas", 10f),
                    WrapMode = DataGridViewTriState.True
                }
            };

            grid.Columns.Add(CreateTextColumn(Language.getString("compareLeftLine"), 70));
            grid.Columns.Add(CreateTextColumn(Language.getString("compareLeftText"), 430));
            grid.Columns.Add(CreateTextColumn(Language.getString("compareRightLine"), 70));
            grid.Columns.Add(CreateTextColumn(Language.getString("compareRightText"), 430));
            return grid;
        }

        private static DataGridViewTextBoxColumn CreateTextColumn(string headerText, int width)
        {
            return new DataGridViewTextBoxColumn
            {
                HeaderText = headerText,
                SortMode = DataGridViewColumnSortMode.NotSortable,
                Width = width,
                AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill
            };
        }

        private static void ApplyTextRowStyle(DataGridViewRow row, TextDiffState state)
        {
            Color leftBackColor = Color.White;
            Color rightBackColor = Color.White;

            switch (state)
            {
                case TextDiffState.Modified:
                    leftBackColor = Color.FromArgb(255, 245, 204);
                    rightBackColor = Color.FromArgb(255, 245, 204);
                    break;
                case TextDiffState.LeftOnly:
                    leftBackColor = Color.FromArgb(255, 231, 231);
                    break;
                case TextDiffState.RightOnly:
                    rightBackColor = Color.FromArgb(230, 247, 230);
                    break;
            }

            row.Cells[0].Style.BackColor = leftBackColor;
            row.Cells[1].Style.BackColor = leftBackColor;
            row.Cells[2].Style.BackColor = rightBackColor;
            row.Cells[3].Style.BackColor = rightBackColor;
        }

        private static SplitContainer CreateSplitContainer()
        {
            return new SplitContainer
            {
                Dock = DockStyle.Fill,
                Orientation = Orientation.Vertical,
                Panel1MinSize = 120,
                Panel2MinSize = 120,
                SplitterDistance = 620
            };
        }

        private static void ConfigureImageSplitLayout(SplitContainer split, Size leftImageSize, Size rightImageSize)
        {
            if (leftImageSize.Width <= 0 || leftImageSize.Height <= 0 ||
                rightImageSize.Width <= 0 || rightImageSize.Height <= 0)
            {
                return;
            }

            void UpdateSplitter()
            {
                int availableWidth = split.ClientSize.Width - split.SplitterWidth;
                if (availableWidth <= 0)
                {
                    return;
                }

                double leftAspect = (double)leftImageSize.Width / leftImageSize.Height;
                double rightAspect = (double)rightImageSize.Width / rightImageSize.Height;
                double aspectSum = leftAspect + rightAspect;
                if (aspectSum <= 0d)
                {
                    return;
                }

                int leftWidth = (int)Math.Round(availableWidth * (leftAspect / aspectSum), MidpointRounding.AwayFromZero);
                int minLeft = split.Panel1MinSize;
                int maxLeft = availableWidth - split.Panel2MinSize;
                if (maxLeft < minLeft)
                {
                    return;
                }

                split.SplitterDistance = Math.Max(minLeft, Math.Min(maxLeft, leftWidth));
            }

            split.SizeChanged += (_, __) => UpdateSplitter();
            split.HandleCreated += (_, __) => UpdateSplitter();
            UpdateSplitter();
        }

        private static DataGridView CreateCsvGrid()
        {
            return new DataGridView
            {
                Dock = DockStyle.Fill,
                ReadOnly = true,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                AllowUserToResizeRows = false,
                RowHeadersVisible = false,
                MultiSelect = false,
                SelectionMode = DataGridViewSelectionMode.CellSelect,
                ClipboardCopyMode = DataGridViewClipboardCopyMode.EnableAlwaysIncludeHeaderText,
                BackgroundColor = Color.White,
                BorderStyle = BorderStyle.None,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.DisplayedCells,
                EnableHeadersVisualStyles = false,
                ColumnHeadersDefaultCellStyle = new DataGridViewCellStyle
                {
                    BackColor = Color.FromArgb(240, 244, 248),
                    Font = new Font("Segoe UI", 9.5f, FontStyle.Bold)
                },
                DefaultCellStyle = new DataGridViewCellStyle
                {
                    Font = new Font("Segoe UI", 9.5f)
                }
            };
        }

        private static int HighlightCsvDifferences(DataGridView leftGrid, DataGridView rightGrid, DataTable leftTable, DataTable rightTable)
        {
            int differenceCount = 0;
            int maxRows = Math.Max(leftTable.Rows.Count, rightTable.Rows.Count);
            int maxColumns = Math.Max(leftTable.Columns.Count, rightTable.Columns.Count);

            for (int rowIndex = 0; rowIndex < maxRows; rowIndex++)
            {
                for (int columnIndex = 0; columnIndex < maxColumns; columnIndex++)
                {
                    string leftValue = GetTableValue(leftTable, rowIndex, columnIndex);
                    string rightValue = GetTableValue(rightTable, rowIndex, columnIndex);
                    if (string.Equals(leftValue, rightValue, StringComparison.Ordinal))
                    {
                        continue;
                    }

                    differenceCount++;
                    ApplyCsvCellStyle(leftGrid, rowIndex, columnIndex, Color.FromArgb(255, 245, 204));
                    ApplyCsvCellStyle(rightGrid, rowIndex, columnIndex, Color.FromArgb(255, 245, 204));
                }
            }

            return differenceCount;
        }

        private static string GetTableValue(DataTable table, int rowIndex, int columnIndex)
        {
            if (rowIndex < 0 || rowIndex >= table.Rows.Count || columnIndex < 0 || columnIndex >= table.Columns.Count)
            {
                return string.Empty;
            }

            return table.Rows[rowIndex][columnIndex]?.ToString() ?? string.Empty;
        }

        private static void ApplyCsvCellStyle(DataGridView grid, int rowIndex, int columnIndex, Color backColor)
        {
            if (rowIndex < 0 || rowIndex >= grid.Rows.Count || columnIndex < 0 || columnIndex >= grid.Columns.Count)
            {
                return;
            }

            grid.Rows[rowIndex].Cells[columnIndex].Style.BackColor = backColor;
        }

        private static Panel CreateImagePanel(Image previewImage, FileMetadata metadata)
        {
            var layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                RowCount = 2,
                ColumnCount = 1
            };
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 72));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 28));

            var pictureBox = new PictureBox
            {
                Dock = DockStyle.Fill,
                SizeMode = PictureBoxSizeMode.Zoom,
                BackColor = Color.Black,
                Image = previewImage
            };

            var metaBox = CreateReadOnlyTextBox(metadata.ToDisplayString(includeDimensions: true));
            layout.Controls.Add(pictureBox, 0, 0);
            layout.Controls.Add(metaBox, 0, 1);

            var panel = new Panel { Dock = DockStyle.Fill };
            panel.Controls.Add(layout);
            return panel;
        }

        private static Panel CreateBinaryPanel(FileMetadata metadata, string hexPreview)
        {
            var layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                RowCount = 2,
                ColumnCount = 1
            };
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 34));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 66));

            layout.Controls.Add(CreateReadOnlyTextBox(metadata.ToDisplayString(includeDimensions: false)), 0, 0);
            layout.Controls.Add(CreateReadOnlyTextBox(hexPreview, new Font("Consolas", 9.5f)), 0, 1);

            var panel = new Panel { Dock = DockStyle.Fill };
            panel.Controls.Add(layout);
            return panel;
        }

        private static TextBox CreateReadOnlyTextBox(string text, Font font = null)
        {
            return new TextBox
            {
                Dock = DockStyle.Fill,
                Multiline = true,
                ReadOnly = true,
                ScrollBars = ScrollBars.Both,
                Font = font ?? new Font("Segoe UI", 9.5f),
                Text = text,
                BackColor = Color.White,
                BorderStyle = BorderStyle.None
            };
        }

        private static ImageCompareSnapshot BuildImageSnapshot(string leftFilePath, string rightFilePath)
        {
            FileMetadata leftMetadata = FileMetadata.FromFile(leftFilePath, includeImageDetails: true);
            FileMetadata rightMetadata = FileMetadata.FromFile(rightFilePath, includeImageDetails: true);

            return new ImageCompareSnapshot(
                LoadPreviewImage(leftFilePath),
                LoadPreviewImage(rightFilePath),
                leftMetadata,
                rightMetadata,
                string.Equals(leftMetadata.Hash, rightMetadata.Hash, StringComparison.OrdinalIgnoreCase));
        }

        private static BinaryCompareSnapshot BuildBinarySnapshot(string leftFilePath, string rightFilePath)
        {
            FileMetadata leftMetadata = FileMetadata.FromFile(leftFilePath, includeImageDetails: false);
            FileMetadata rightMetadata = FileMetadata.FromFile(rightFilePath, includeImageDetails: false);

            return new BinaryCompareSnapshot(
                leftMetadata,
                rightMetadata,
                BuildHexPreview(leftFilePath),
                BuildHexPreview(rightFilePath),
                string.Equals(leftMetadata.Hash, rightMetadata.Hash, StringComparison.OrdinalIgnoreCase));
        }

        private static Image LoadPreviewImage(string path)
        {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using var image = Image.FromStream(stream);
            return new Bitmap(image);
        }

        private static string BuildHexPreview(string path)
        {
            byte[] buffer = new byte[BinaryPreviewBytes];
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            int bytesRead = stream.Read(buffer, 0, buffer.Length);

            var builder = new StringBuilder(bytesRead * 4);
            for (int index = 0; index < bytesRead; index += 16)
            {
                int count = Math.Min(16, bytesRead - index);
                builder.Append(index.ToString("X4", CultureInfo.InvariantCulture));
                builder.Append(": ");
                for (int offset = 0; offset < count; offset++)
                {
                    builder.Append(buffer[index + offset].ToString("X2", CultureInfo.InvariantCulture));
                    builder.Append(' ');
                }

                builder.AppendLine();
            }

            return builder.ToString();
        }

        private sealed class ImageCompareSnapshot
        {
            public ImageCompareSnapshot(Image leftPreview, Image rightPreview, FileMetadata leftMetadata, FileMetadata rightMetadata, bool hashesEqual)
            {
                LeftPreview = leftPreview;
                RightPreview = rightPreview;
                LeftMetadata = leftMetadata;
                RightMetadata = rightMetadata;
                HashesEqual = hashesEqual;
            }

            public Image LeftPreview { get; }
            public Image RightPreview { get; }
            public FileMetadata LeftMetadata { get; }
            public FileMetadata RightMetadata { get; }
            public bool HashesEqual { get; }
        }

        private sealed class BinaryCompareSnapshot
        {
            public BinaryCompareSnapshot(FileMetadata leftMetadata, FileMetadata rightMetadata, string leftHexPreview, string rightHexPreview, bool hashesEqual)
            {
                LeftMetadata = leftMetadata;
                RightMetadata = rightMetadata;
                LeftHexPreview = leftHexPreview;
                RightHexPreview = rightHexPreview;
                HashesEqual = hashesEqual;
            }

            public FileMetadata LeftMetadata { get; }
            public FileMetadata RightMetadata { get; }
            public string LeftHexPreview { get; }
            public string RightHexPreview { get; }
            public bool HashesEqual { get; }
        }

        private sealed class FileMetadata
        {
            private FileMetadata(
                string filePath,
                long length,
                DateTime lastWriteTime,
                string hash,
                string imageFormat,
                Size imageSize,
                float horizontalDpi,
                float verticalDpi)
            {
                FilePath = filePath;
                Length = length;
                LastWriteTime = lastWriteTime;
                Hash = hash;
                ImageFormat = imageFormat;
                ImageSize = imageSize;
                HorizontalDpi = horizontalDpi;
                VerticalDpi = verticalDpi;
            }

            public string FilePath { get; }
            public long Length { get; }
            public DateTime LastWriteTime { get; }
            public string Hash { get; }
            public string ImageFormat { get; }
            public Size ImageSize { get; }
            public float HorizontalDpi { get; }
            public float VerticalDpi { get; }
            public string HashShort => string.IsNullOrEmpty(Hash) || Hash.Length < 12 ? Hash : Hash.Substring(0, 12);
            public string DimensionText => ImageSize.IsEmpty ? "-" : $"{ImageSize.Width}x{ImageSize.Height}";
            public string AspectRatioText => GetAspectRatioText(ImageSize);
            public string DpiText => GetDpiText(HorizontalDpi, VerticalDpi);
            public string ImageStatusText => BuildImageStatusText();

            public static FileMetadata FromFile(string filePath, bool includeImageDetails)
            {
                var fileInfo = new FileInfo(filePath);
                string imageFormat = null;
                Size imageSize = Size.Empty;
                float horizontalDpi = 0f;
                float verticalDpi = 0f;

                if (includeImageDetails) {
          try {
            using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using var image = Image.FromStream(stream, false, false);
            imageFormat = image.RawFormat.ToString();
            imageSize = image.Size;
            horizontalDpi = image.HorizontalResolution;
            verticalDpi = image.VerticalResolution;
          }


          catch (Exception) {

            throw;
          }
        }
        return new FileMetadata(
                    filePath,
                    fileInfo.Length,
                    fileInfo.LastWriteTime,
                    ComputeSha256(filePath),
                    imageFormat,
                    imageSize,
                    horizontalDpi,
                    verticalDpi);
            }

            public string ToDisplayString(bool includeDimensions)
            {
                var builder = new StringBuilder();
                builder.AppendLine(Path.GetFileName(FilePath));
                builder.AppendLine(FilePath);
                builder.AppendLine();
                builder.AppendLine($"{Language.getString("compareMetaSize")}: {Length:N0} bytes");
                builder.AppendLine($"{Language.getString("compareMetaModified")}: {LastWriteTime:G}");
                if (includeDimensions)
                {
                    builder.AppendLine($"{Language.getString("compareMetaDimensions")}: {DimensionText} ({AspectRatioText})");
                    builder.AppendLine($"{Language.getString("compareMetaDpi")}: {DpiText}");
                    builder.AppendLine($"{Language.getString("compareMetaFormat")}: {ImageFormat}");
                }

                builder.AppendLine($"{Language.getString("compareMetaHash")}:");
                builder.AppendLine(Hash);
                return builder.ToString();
            }

            private static string ComputeSha256(string filePath)
            {
                using var sha256 = SHA256.Create();
                using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                byte[] hash = sha256.ComputeHash(stream);
                return Convert.ToHexString(hash);
            }

            private string BuildImageStatusText()
            {
                if (ImageSize.IsEmpty)
                {
                    return "-";
                }

                return string.Format(
                    CultureInfo.CurrentCulture,
                    "{0} ({1}, {2})",
                    DimensionText,
                    AspectRatioText,
                    DpiText);
            }

            private static string GetAspectRatioText(Size imageSize)
            {
                if (imageSize.Width <= 0 || imageSize.Height <= 0)
                {
                    return "-";
                }

                int divisor = GreatestCommonDivisor(imageSize.Width, imageSize.Height);
                return divisor <= 0
                    ? $"{imageSize.Width}x{imageSize.Height}"
                    : $"{imageSize.Width / divisor}x{imageSize.Height / divisor}";
            }

            private static string GetDpiText(float horizontalDpi, float verticalDpi)
            {
                if (horizontalDpi <= 0f || verticalDpi <= 0f)
                {
                    return "-";
                }

                string horizontalText = horizontalDpi.ToString("0.##", CultureInfo.CurrentCulture);
                string verticalText = verticalDpi.ToString("0.##", CultureInfo.CurrentCulture);
                return Math.Abs(horizontalDpi - verticalDpi) < 0.01f
                    ? $"{horizontalText} DPI"
                    : $"{horizontalText}x{verticalText} DPI";
            }

            private static int GreatestCommonDivisor(int left, int right)
            {
                left = Math.Abs(left);
                right = Math.Abs(right);

                while (right != 0)
                {
                    int remainder = left % right;
                    left = right;
                    right = remainder;
                }

                return left;
            }
        }

        private enum TextDiffState
        {
            Equal = 0,
            Modified,
            LeftOnly,
            RightOnly
        }

        private sealed class TextDiffRow
        {
            public TextDiffRow(int? leftLineNumber, string leftText, int? rightLineNumber, string rightText, TextDiffState state)
            {
                LeftLineNumber = leftLineNumber;
                LeftText = leftText ?? string.Empty;
                RightLineNumber = rightLineNumber;
                RightText = rightText ?? string.Empty;
                State = state;
            }

            public int? LeftLineNumber { get; }
            public string LeftText { get; }
            public int? RightLineNumber { get; }
            public string RightText { get; }
            public TextDiffState State { get; }
            public string LeftLineNumberText => LeftLineNumber?.ToString(CultureInfo.InvariantCulture) ?? string.Empty;
            public string RightLineNumberText => RightLineNumber?.ToString(CultureInfo.InvariantCulture) ?? string.Empty;
        }

        private sealed class TextDiffResult
        {
            public List<TextDiffRow> Rows { get; } = new List<TextDiffRow>();
            public int ModifiedCount { get; set; }
            public int LeftOnlyCount { get; set; }
            public int RightOnlyCount { get; set; }
            public bool AreEqual => ModifiedCount == 0 && LeftOnlyCount == 0 && RightOnlyCount == 0;
        }

        private static class TextDiffBuilder
        {
            private const int MaxLcsCellCount = 2_000_000;

            public static TextDiffResult Build(string leftText, string rightText)
            {
                string[] leftLines = SplitLines(leftText);
                string[] rightLines = SplitLines(rightText);

                return leftLines.Length * rightLines.Length <= MaxLcsCellCount
                    ? BuildUsingLcs(leftLines, rightLines)
                    : BuildByIndex(leftLines, rightLines);
            }

            private static string[] SplitLines(string text)
            {
                return (text ?? string.Empty).Split(new[] { '\n' }, StringSplitOptions.None);
            }

            private static TextDiffResult BuildByIndex(string[] leftLines, string[] rightLines)
            {
                var result = new TextDiffResult();
                int max = Math.Max(leftLines.Length, rightLines.Length);
                for (int index = 0; index < max; index++)
                {
                    string left = index < leftLines.Length ? leftLines[index] : string.Empty;
                    string right = index < rightLines.Length ? rightLines[index] : string.Empty;
                    if (index >= leftLines.Length)
                    {
                        result.Rows.Add(new TextDiffRow(null, string.Empty, index + 1, right, TextDiffState.RightOnly));
                        result.RightOnlyCount++;
                    }
                    else if (index >= rightLines.Length)
                    {
                        result.Rows.Add(new TextDiffRow(index + 1, left, null, string.Empty, TextDiffState.LeftOnly));
                        result.LeftOnlyCount++;
                    }
                    else if (string.Equals(left, right, StringComparison.Ordinal))
                    {
                        result.Rows.Add(new TextDiffRow(index + 1, left, index + 1, right, TextDiffState.Equal));
                    }
                    else
                    {
                        result.Rows.Add(new TextDiffRow(index + 1, left, index + 1, right, TextDiffState.Modified));
                        result.ModifiedCount++;
                    }
                }

                return result;
            }

            private static TextDiffResult BuildUsingLcs(string[] leftLines, string[] rightLines)
            {
                int[,] lcs = BuildLcsMatrix(leftLines, rightLines);
                var raw = new List<TextDiffRow>();
                int leftIndex = 0;
                int rightIndex = 0;

                while (leftIndex < leftLines.Length && rightIndex < rightLines.Length)
                {
                    if (string.Equals(leftLines[leftIndex], rightLines[rightIndex], StringComparison.Ordinal))
                    {
                        raw.Add(new TextDiffRow(leftIndex + 1, leftLines[leftIndex], rightIndex + 1, rightLines[rightIndex], TextDiffState.Equal));
                        leftIndex++;
                        rightIndex++;
                        continue;
                    }

                    if (lcs[leftIndex + 1, rightIndex] >= lcs[leftIndex, rightIndex + 1])
                    {
                        raw.Add(new TextDiffRow(leftIndex + 1, leftLines[leftIndex], null, string.Empty, TextDiffState.LeftOnly));
                        leftIndex++;
                    }
                    else
                    {
                        raw.Add(new TextDiffRow(null, string.Empty, rightIndex + 1, rightLines[rightIndex], TextDiffState.RightOnly));
                        rightIndex++;
                    }
                }

                while (leftIndex < leftLines.Length)
                {
                    raw.Add(new TextDiffRow(leftIndex + 1, leftLines[leftIndex], null, string.Empty, TextDiffState.LeftOnly));
                    leftIndex++;
                }

                while (rightIndex < rightLines.Length)
                {
                    raw.Add(new TextDiffRow(null, string.Empty, rightIndex + 1, rightLines[rightIndex], TextDiffState.RightOnly));
                    rightIndex++;
                }

                return CoalesceRows(raw);
            }

            private static int[,] BuildLcsMatrix(string[] leftLines, string[] rightLines)
            {
                int[,] matrix = new int[leftLines.Length + 1, rightLines.Length + 1];
                for (int leftIndex = leftLines.Length - 1; leftIndex >= 0; leftIndex--)
                {
                    for (int rightIndex = rightLines.Length - 1; rightIndex >= 0; rightIndex--)
                    {
                        matrix[leftIndex, rightIndex] = string.Equals(leftLines[leftIndex], rightLines[rightIndex], StringComparison.Ordinal)
                            ? matrix[leftIndex + 1, rightIndex + 1] + 1
                            : Math.Max(matrix[leftIndex + 1, rightIndex], matrix[leftIndex, rightIndex + 1]);
                    }
                }

                return matrix;
            }

            private static TextDiffResult CoalesceRows(List<TextDiffRow> rawRows)
            {
                var result = new TextDiffResult();
                int index = 0;
                while (index < rawRows.Count)
                {
                    TextDiffRow current = rawRows[index];
                    if (current.State == TextDiffState.LeftOnly &&
                        index + 1 < rawRows.Count &&
                        rawRows[index + 1].State == TextDiffState.RightOnly)
                    {
                        TextDiffRow right = rawRows[index + 1];
                        result.Rows.Add(new TextDiffRow(current.LeftLineNumber, current.LeftText, right.RightLineNumber, right.RightText, TextDiffState.Modified));
                        result.ModifiedCount++;
                        index += 2;
                        continue;
                    }

                    result.Rows.Add(current);
                    if (current.State == TextDiffState.LeftOnly)
                    {
                        result.LeftOnlyCount++;
                    }
                    else if (current.State == TextDiffState.RightOnly)
                    {
                        result.RightOnlyCount++;
                    }

                    index++;
                }

                return result;
            }
        }
    }
}
