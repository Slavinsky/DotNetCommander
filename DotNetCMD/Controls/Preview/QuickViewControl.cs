using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using View;

namespace DotNetCommander
{
    /// <summary>
    /// Displays a cancellable, read-only preview of the item selected in the active commander panel.
    /// </summary>
    public class QuickViewControl : UserControl
    {
        private const int DefaultTextPreviewBytes = 256 * 1024;
        private const int CsvPreviewDebounceMs = 120;

        private readonly InteractiveImageViewControl imageView;
        private readonly TextBox textBox;
        private readonly RichTextBox richTextBox;
        private readonly DataGridView dataGridView;
        private readonly GedcomQuickViewControl gedcomGraph;
        private readonly Label infoLabel;
        private readonly Label detailsLabel;
        private readonly CsvTableLoader csvTableLoader = new CsvTableLoader();
        private CancellationTokenSource previewCancellationTokenSource;
        private string requestedPath;
        private Font textPreviewFont;
        private Font richPreviewFont;
        private Font detailsFont;
        private string imageDescription;

        /// <summary>Initializes all mutually exclusive preview surfaces.</summary>
        public QuickViewControl()
        {
            BackColor = SystemColors.Window;

            imageView = new InteractiveImageViewControl();
            imageView.ZoomChanged += (_, __) => UpdateImageStatus();

            textBox = new TextBox
            {
                Dock = DockStyle.Fill,
                ReadOnly = true,
                Multiline = true,
                ScrollBars = ScrollBars.Both,
                Visible = false,
                BackColor = SystemColors.Window,
                ForeColor = SystemColors.WindowText
            };

            // Renders .rtf files and markdown-converted-to-RTF content with real formatting,
            // instead of showing raw markup in the plain TextBox.
            richTextBox = new RichTextBox
            {
                Dock = DockStyle.Fill,
                ReadOnly = true,
                Visible = false,
                BorderStyle = BorderStyle.None,
                DetectUrls = false,
                BackColor = SystemColors.Window,
                ForeColor = SystemColors.WindowText
            };

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
                Visible = false,
                BackgroundColor = SystemColors.Window,
                BorderStyle = BorderStyle.None,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None,
                EnableHeadersVisualStyles = false
            };

            infoLabel = new Label
            {
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleCenter,
                ForeColor = SystemColors.GrayText,
                Visible = true
            };

            detailsLabel = new Label
            {
                AutoEllipsis = true,
                BackColor = SystemColors.Control,
                Dock = DockStyle.Bottom,
                ForeColor = SystemColors.GrayText,
                Padding = new Padding(6, 2, 6, 2),
                TextAlign = ContentAlignment.MiddleLeft,
                Visible = false
            };

            gedcomGraph = new GedcomQuickViewControl();
            gedcomGraph.PersonActivated += (_, e) => GedcomPersonActivated?.Invoke(this, e);

            Controls.Add(imageView);
            Controls.Add(textBox);
            Controls.Add(richTextBox);
            Controls.Add(dataGridView);
            Controls.Add(gedcomGraph);
            Controls.Add(infoLabel);
            Controls.Add(detailsLabel);

            ApplyUserSettings();

            ShowInfo(Language.getString("quickViewSelectFile"));
        }

        /// <summary>Raised when a person is activated in the GEDCOM graph preview.</summary>
        internal event EventHandler<GedcomPersonActivatedEventArgs> GedcomPersonActivated;

        /// <summary>Starts a preview for a physical or materialized file path.</summary>
        public void DisplayFile(string path)
        {
            _ = DisplayFileAsync(path);
        }

        /// <summary>Starts plain-text extraction for a Word Binary CFBF document.</summary>
        internal void DisplayWordBinaryDocument(string path)
        {
            _ = DisplayWordBinaryDocumentAsync(path);
        }

        /// <summary>Displays the family graph for a GEDCOM person.</summary>
        internal void DisplayGedcomPerson(GedcomPersonEntry person)
        {
            requestedPath = null;
            CancelPreviewWork();
            dataGridView.DataSource = null;
            ClearImage();

            if (person == null)
            {
                ShowInfo(Language.getString("quickViewGedcomSelectPerson"));
                return;
            }

            gedcomGraph.DisplayPerson(person);
            SetActiveView(gedcomGraph);
        }

        /// <summary>Reapplies Quick View fonts after Options have been saved.</summary>
        internal void ApplyUserSettings()
        {
            Font nextTextFont = CreateConfiguredFont(
                Properties.Settings.Default.TextEditorFontName,
                Properties.Settings.Default.TextEditorFontSize,
                "Consolas",
                12f);
            Font nextRichFont = CreateConfiguredFont(
                Properties.Settings.Default.RtfEditorFontName,
                Properties.Settings.Default.RtfEditorFontSize,
                "Segoe UI",
                11f);
            Font nextDetailsFont = DialogStyleService.CreateCaptionFont();

            textBox.Font = nextTextFont;
            richTextBox.Font = nextRichFont;
            detailsLabel.Font = nextDetailsFont;
            textPreviewFont?.Dispose();
            richPreviewFont?.Dispose();
            detailsFont?.Dispose();
            textPreviewFont = nextTextFont;
            richPreviewFont = nextRichFont;
            detailsFont = nextDetailsFont;
            detailsLabel.Height = Math.Max(22, detailsLabel.Font.Height + 6);
            detailsLabel.BringToFront();
        }

        // ---------------------------------------------------------------
        // Dispatch
        // ---------------------------------------------------------------

        private async Task DisplayFileAsync(string path)
        {
            requestedPath = path;
            CancelPreviewWork();

            try
            {
                if (string.IsNullOrEmpty(path))
                {
                    ShowInfo(Language.getString("quickViewSelectFile"));
                    return;
                }

                if (Directory.Exists(path))
                {
                    ShowInfo(Language.getString("quickViewFolder"));
                    return;
                }

                if (FileTypeClassifier.IsCompoundFile(path))
                {
                    await DisplayWordBinaryDocumentAsync(path);
                    return;
                }

                FileContentKind kind = FileTypeClassifier.Classify(path);
                CancellationTokenSource cts = BeginPreviewOperation();
                try
                {
                    switch (kind)
                    {
                        case FileContentKind.Image:
                            await ShowImageAsync(path, cts.Token);
                            break;
                        case FileContentKind.Text:
                            await ShowTextAsync(path, cts.Token);
                            break;
                        case FileContentKind.RichText:
                            await ShowRichTextFileAsync(path, cts.Token);
                            break;
                        case FileContentKind.Markdown:
                            await ShowMarkdownAsync(path, cts.Token);
                            break;
                        case FileContentKind.Csv:
                            await ShowCsvAsync(path, cts.Token);
                            break;
                        default:
                            ShowInfo(Language.getString("quickViewUnsupported"));
                            break;
                    }
                }
                finally
                {
                    EndPreviewOperation(cts);
                }
            }
            catch (Exception ex)
            {
                LogService.LogException(nameof(DisplayFileAsync), ex);
                if (requestedPath == path && !IsDisposed)
                {
                    ShowInfo(Language.getString("quickViewError"));
                }
            }
        }

        // ---------------------------------------------------------------
        // Image
        // ---------------------------------------------------------------

        private async Task ShowImageAsync(string path, CancellationToken token)
        {
            Bitmap bitmap;
            string description;
            try
            {
                (bitmap, description) = await Task.Run(() =>
                {
                    using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                    using (var image = Image.FromStream(stream))
                    {
                        return (
                            new Bitmap(image),
                            DescriptionFileService.TryGetDescription(path));
                    }
                }, token);
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (Exception ex)
            {
                LogService.LogException(nameof(ShowImageAsync), ex);
                if (requestedPath == path && !IsDisposed)
                {
                    ShowInfo(Language.getString("quickViewError"));
                }
                return;
            }

            if (token.IsCancellationRequested || requestedPath != path || IsDisposed)
            {
                bitmap.Dispose();
                return;
            }

            imageDescription = description;
            imageView.SetImage(bitmap);
            SetActiveView(imageView, GetImageStatusText());
        }

        // ---------------------------------------------------------------
        // Plain text
        // ---------------------------------------------------------------

        private async Task ShowTextAsync(string path, CancellationToken token)
        {
            string content;
            Encoding detectedEncoding = null;
            try
            {
                var info = new FileInfo(path);
                if (info.Length > GetTextPreviewMaxBytes())
                {
                    if (requestedPath == path && !IsDisposed)
                    {
                        ShowInfo(Language.getString("quickViewLargeFile"));
                    }
                    return;
                }

                content = await Task.Run(() =>
                {
                    string value = TextFileEncodingService.ReadAllText(path, out Encoding encoding);
                    detectedEncoding = encoding;
                    return value;
                }, token);
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (Exception ex)
            {
                LogService.LogException(nameof(ShowTextAsync), ex);
                if (requestedPath == path && !IsDisposed)
                {
                    ShowInfo(Language.getString("quickViewError"));
                }
                return;
            }

            if (token.IsCancellationRequested || requestedPath != path || IsDisposed)
            {
                return;
            }

            string details = detectedEncoding == null
                ? null
                : string.Format(Language.getString("quickViewEncodingFormat"), detectedEncoding.EncodingName);
            ShowTextContent(content, details);
        }

        private void ShowTextContent(string content, string details = null)
        {
            content ??= string.Empty;
            textBox.Text = content.Replace("\r\n", "\n").Replace("\r", "\n").Replace("\n", Environment.NewLine);
            SetActiveView(textBox, details);
        }

        // ---------------------------------------------------------------
        // RTF files (.rtf)
        // ---------------------------------------------------------------

        private async Task ShowRichTextFileAsync(string path, CancellationToken token)
        {
            string rtf;
            try
            {
                var info = new FileInfo(path);
                if (info.Length > GetTextPreviewMaxBytes())
                {
                    if (requestedPath == path && !IsDisposed)
                    {
                        ShowInfo(Language.getString("quickViewLargeFile"));
                    }
                    return;
                }

                // RTF is spec'd as escaped ASCII, so a plain ASCII read is safe and
                // avoids the encoding-sniffing pass used for plain-text files.
                rtf = await Task.Run(() => File.ReadAllText(path, Encoding.ASCII), token);
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (Exception ex)
            {
                LogService.LogException(nameof(ShowRichTextFileAsync), ex);
                if (requestedPath == path && !IsDisposed)
                {
                    ShowInfo(Language.getString("quickViewError"));
                }
                return;
            }

            if (token.IsCancellationRequested || requestedPath != path || IsDisposed)
            {
                return;
            }

            ShowRtfContent(rtf);
        }

        // ---------------------------------------------------------------
        // Markdown (.md) — rendered via the existing RtfEdit converter
        // ---------------------------------------------------------------

        private async Task ShowMarkdownAsync(string path, CancellationToken token)
        {
            string markdown;
            try
            {
                var info = new FileInfo(path);
                if (info.Length > GetTextPreviewMaxBytes())
                {
                    if (requestedPath == path && !IsDisposed)
                    {
                        ShowInfo(Language.getString("quickViewLargeFile"));
                    }
                    return;
                }

                markdown = await Task.Run(() => TextFileEncodingService.ReadAllText(path, out _), token);
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (Exception ex)
            {
                LogService.LogException(nameof(ShowMarkdownAsync), ex);
                if (requestedPath == path && !IsDisposed)
                {
                    ShowInfo(Language.getString("quickViewError"));
                }
                return;
            }

            if (token.IsCancellationRequested || requestedPath != path || IsDisposed)
            {
                return;
            }

            // Measure our own RichTextBox and capture the editor's font/size settings
            // on the UI thread before handing the (pure, static) conversion off to a
            // worker thread.
            int tableWidth = GetMarkdownTableWidthTwips();
              RtfEdit.MarkdownRenderOptions options = RtfEdit.CaptureMarkdownRenderOptions(Path.GetDirectoryName(path));

            string rtf;
            try
            {
                rtf = await Task.Run(() => RtfEdit.ConvertMarkdownToRtf(markdown, tableWidth, options), token);
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (Exception ex)
            {
                LogService.LogException(nameof(ShowMarkdownAsync), ex);
                if (requestedPath == path && !IsDisposed)
                {
                    ShowInfo(Language.getString("quickViewError"));
                }
                return;
            }

            if (token.IsCancellationRequested || requestedPath != path || IsDisposed)
            {
                return;
            }

            ShowRtfContent(rtf);
        }

        /// <summary>
        /// Mirrors RtfEdit's own GetTableWidthTwips(), but measured against QuickView's
        /// RichTextBox instead of the editor's — that method is private/instance-bound
        /// to the editor Form, so it can't be reused directly here.
        /// </summary>
        private int GetMarkdownTableWidthTwips()
        {
            const int fallbackWidthPx = 800;
            const int minWidthPx = 200;
            const int rightPaddingPx = 24; // keep table clear of the scrollbar/edge

            int dpi = richTextBox.DeviceDpi > 0 ? richTextBox.DeviceDpi : 96;
            int clientWidthPx = richTextBox.ClientSize.Width > 0
                ? richTextBox.ClientSize.Width
                : fallbackWidthPx;

            int usableWidthPx = Math.Max(minWidthPx, clientWidthPx - rightPaddingPx);
            return usableWidthPx * 1440 / dpi;
        }

        private void ShowRtfContent(string rtf)
        {
            try
            {
                richTextBox.Rtf = rtf;
            }
            catch (ArgumentException ex)
            {
                // Malformed/unsupported RTF payload.
                LogService.LogException(nameof(ShowRtfContent), ex);
                ShowInfo(Language.getString("quickViewError"));
                return;
            }

            SetActiveView(richTextBox);
        }

        // ---------------------------------------------------------------
        // Compound (Word binary) documents
        // ---------------------------------------------------------------

        private async Task DisplayWordBinaryDocumentAsync(string path)
        {
            requestedPath = path;
            CancellationTokenSource cts = BeginPreviewOperation();

            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            {
                EndPreviewOperation(cts);
                ShowInfo(Language.getString("quickViewUnsupported"));
                return;
            }

            ShowInfo(Language.getString("compoundReading"));
            try
            {
                string content = await WordBinaryTextExtractor.TryExtractMainTextAsync(path, cts.Token);
                if (cts.Token.IsCancellationRequested || requestedPath != path || IsDisposed)
                {
                    return;
                }

                if (content == null)
                {
                    ShowInfo(Language.getString("quickViewUnsupported"));
                }
                else
                {
                    ShowTextContent(content);
                }
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex)
            {
                LogService.LogException(nameof(DisplayWordBinaryDocumentAsync), ex);
                if (requestedPath == path && !IsDisposed)
                {
                    ShowInfo(Language.getString("quickViewError"));
                }
            }
            finally
            {
                EndPreviewOperation(cts);
            }
        }

        // ---------------------------------------------------------------
        // CSV
        // ---------------------------------------------------------------

        private async Task ShowCsvAsync(string path, CancellationToken token)
        {
            if (!Properties.Settings.Default.QuickViewCsvEnabled)
            {
                ShowInfo(Language.getString("quickViewCsvDisabled"));
                return;
            }

            var info = new FileInfo(path);
            if (info.Length > Properties.Settings.Default.QuickViewCsvMaxBytes)
            {
                ShowInfo(string.Format(
                    Language.getString("quickViewCsvTooLargeFormat"),
                    Math.Max(1, Properties.Settings.Default.QuickViewCsvMaxBytes / (1024 * 1024))));
                return;
            }

            try
            {
                await Task.Delay(CsvPreviewDebounceMs, token);

                // Only show the loading message once the debounce window has actually
                // elapsed, so quickly scrolling through files doesn't flash it needlessly.
                if (requestedPath == path && !IsDisposed)
                {
                    ShowInfo(Language.getString("quickViewCsvLoading"));
                }

                DataTable table = await csvTableLoader.LoadAsync(path, null, token);
                if (token.IsCancellationRequested || requestedPath != path || IsDisposed)
                {
                    return;
                }

                dataGridView.DataSource = table;
                AlignNumericColumns(table);
                SizeCsvColumnsOnce();
                SetActiveView(dataGridView);
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex)
            {
                LogService.LogException(nameof(ShowCsvAsync), ex);
                if (!token.IsCancellationRequested && requestedPath == path && !IsDisposed)
                {
                    ShowInfo(Language.getString("quickViewError"));
                }
            }
        }

        // ---------------------------------------------------------------
        // Shared view / cancellation plumbing
        // ---------------------------------------------------------------

        private void ShowInfo(string message)
        {
            infoLabel.Text = message;
            dataGridView.DataSource = null;
            ClearImage();
            SetActiveView(infoLabel);
        }

        /// <summary>
        /// Shows exactly one of the preview surfaces and hides the rest, replacing the
        /// previous copy-pasted "set every Visible flag by hand" blocks in each Show* method.
        /// </summary>
        private void SetActiveView(Control activeView, string details = null)
        {
            imageView.Visible = ReferenceEquals(activeView, imageView);
            textBox.Visible = ReferenceEquals(activeView, textBox);
            richTextBox.Visible = ReferenceEquals(activeView, richTextBox);
            dataGridView.Visible = ReferenceEquals(activeView, dataGridView);
            gedcomGraph.Visible = ReferenceEquals(activeView, gedcomGraph);
            infoLabel.Visible = ReferenceEquals(activeView, infoLabel);
            detailsLabel.Text = details ?? string.Empty;
            detailsLabel.Visible = !string.IsNullOrWhiteSpace(details);

            activeView.BringToFront();
            if (detailsLabel.Visible)
                detailsLabel.BringToFront();
        }

        private void UpdateImageStatus()
        {
            if (!imageView.Visible || imageView.ImageSize.IsEmpty)
                return;

            detailsLabel.Text = GetImageStatusText();
            detailsLabel.Visible = true;
            detailsLabel.BringToFront();
        }

        private string GetImageStatusText()
        {
            Size size = imageView.ImageSize;
            string status = string.Format(
                Language.getString("imageZoomStatusFormat"),
                imageView.Zoom,
                size.Width,
                size.Height);
            return string.IsNullOrWhiteSpace(imageDescription)
                ? status
                : string.Format(
                    Language.getString("imageDescriptionStatusFormat"),
                    status,
                    imageDescription);
        }

        private static int GetTextPreviewMaxBytes()
        {
            int configured = Properties.Settings.Default.QuickViewTextMaxBytes;
            return configured > 0 ? configured : DefaultTextPreviewBytes;
        }

        private static Font CreateConfiguredFont(string configuredName, float configuredSize, string fallbackName, float fallbackSize)
        {
            string name = string.IsNullOrWhiteSpace(configuredName) ? fallbackName : configuredName;
            float size = configuredSize >= 6f ? configuredSize : fallbackSize;
            try
            {
                return new Font(name, size, FontStyle.Regular, GraphicsUnit.Point);
            }
            catch
            {
                return new Font(fallbackName, fallbackSize, FontStyle.Regular, GraphicsUnit.Point);
            }
        }

        private void ClearImage()
        {
            imageDescription = null;
            imageView.ClearImage();
        }

        private CancellationTokenSource BeginPreviewOperation()
        {
            CancelPreviewWork();
            var cts = new CancellationTokenSource();
            previewCancellationTokenSource = cts;
            return cts;
        }

        private void EndPreviewOperation(CancellationTokenSource cts)
        {
            if (ReferenceEquals(previewCancellationTokenSource, cts))
            {
                previewCancellationTokenSource = null;
            }

            cts.Dispose();
        }

        private void CancelPreviewWork()
        {
            if (previewCancellationTokenSource == null)
            {
                return;
            }

            previewCancellationTokenSource.Cancel();
            previewCancellationTokenSource.Dispose();
            previewCancellationTokenSource = null;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                CancelPreviewWork();
                textPreviewFont?.Dispose();
                richPreviewFont?.Dispose();
                detailsFont?.Dispose();
                textPreviewFont = null;
                richPreviewFont = null;
                detailsFont = null;
            }

            base.Dispose(disposing);
        }

        // ---------------------------------------------------------------
        // CSV column alignment
        // ---------------------------------------------------------------

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

        private void SizeCsvColumnsOnce()
        {
            dataGridView.AutoResizeColumns(DataGridViewAutoSizeColumnsMode.DisplayedCells);
            foreach (DataGridViewColumn column in dataGridView.Columns)
            {
                column.Width = Math.Max(40, Math.Min(600, column.Width));
                column.AutoSizeMode = DataGridViewAutoSizeColumnMode.None;
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
    }
}
