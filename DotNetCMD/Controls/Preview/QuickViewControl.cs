using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace DotNetCommander
{
    public class QuickViewControl : UserControl
    {
        private const int MaxTextPreviewBytes = 256 * 1024; // 256 KB
        private const int CsvPreviewDebounceMs = 120;

        private readonly InteractiveImageViewControl imageView;
        private readonly TextBox textBox;
        private readonly DataGridView dataGridView;
        private readonly GedcomQuickViewControl gedcomGraph;
        private readonly Label infoLabel;
        private readonly CsvTableLoader csvTableLoader = new CsvTableLoader();
        private CancellationTokenSource previewCancellationTokenSource;
        private string requestedPath;

        public QuickViewControl()
        {
            BackColor = SystemColors.Window;

            imageView = new InteractiveImageViewControl();

            textBox = new TextBox
            {
                Dock = DockStyle.Fill,
                ReadOnly = true,
                Multiline = true,
                ScrollBars = ScrollBars.Both,
                Visible = false,
                Font = new Font("Consolas", 12f),
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
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.DisplayedCells,
                EnableHeadersVisualStyles = false
            };

            infoLabel = new Label
            {
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleCenter,
                ForeColor = SystemColors.GrayText,
                Visible = true
            };

            gedcomGraph = new GedcomQuickViewControl();
            gedcomGraph.PersonActivated += (_, e) => GedcomPersonActivated?.Invoke(this, e);

            Controls.Add(imageView);
            Controls.Add(textBox);
            Controls.Add(dataGridView);
            Controls.Add(gedcomGraph);
            Controls.Add(infoLabel);

            ShowInfo(Language.getString("quickViewSelectFile"));
        }

        internal event EventHandler<GedcomPersonActivatedEventArgs> GedcomPersonActivated;

        public void DisplayFile(string path)
        {
            _ = DisplayFileAsync(path);
        }

        internal void DisplayGedcomPerson(GedcomPersonEntry person)
        {
            requestedPath = null;
            CancelPreviewWork();
            ClearImage();
            dataGridView.DataSource = null;
            if (person == null)
            {
                ShowInfo(Language.getString("quickViewGedcomSelectPerson"));
                return;
            }

            gedcomGraph.DisplayPerson(person);
            gedcomGraph.Visible = true;
            gedcomGraph.BringToFront();
            imageView.Visible = false;
            textBox.Visible = false;
            dataGridView.Visible = false;
            infoLabel.Visible = false;
        }

        private void ShowImage(string path)
        {
            try
            {
                using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                {
                    using (var image = Image.FromStream(stream))
                    {
                        imageView.SetImage(new Bitmap(image));
                    }
                }

                imageView.Visible = true;
                imageView.BringToFront();
                gedcomGraph.Visible = false;
                textBox.Visible = false;
                dataGridView.Visible = false;
                infoLabel.Visible = false;
            }
            catch
            {
                ShowInfo(Language.getString("quickViewError"));
            }
        }

        private void ShowText(string path)
        {
            try
            {
                FileInfo info = new FileInfo(path);
                if (info.Length > MaxTextPreviewBytes)
                {
                    ShowInfo(Language.getString("quickViewLargeFile"));
                    return;
                }

                using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                using (var reader = new StreamReader(stream, Encoding.UTF8, true))
                {
                  // Обрабатываем разные типы переносов строк (Windows, Unix, Mac)
                  //textBox.Text = reader.ReadToEnd();
                  // Читаем содержимое файла с корректной обработкой переносов строк
                  string content = reader.ReadToEnd();
                  // Обрабатываем разные типы переносов строк (Windows, Unix, Mac)
                  content = content.Replace("\r\n", "\n").Replace("\r", "\n").Replace("\n", Environment.NewLine);
                  textBox.Text = content;
                }

                textBox.Visible = true;
                gedcomGraph.Visible = false;
                imageView.Visible = false;
                dataGridView.Visible = false;
                infoLabel.Visible = false;
                ClearImage();
            }
            catch
            {
                ShowInfo(Language.getString("quickViewError"));
            }
        }

        private async Task DisplayFileAsync(string path)
        {
            requestedPath = path;
            CancelPreviewWork();

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

            switch (FileTypeClassifier.Classify(path))
            {
                case FileContentKind.Image:
                    ShowImage(path);
                    return;
                case FileContentKind.Text:
                case FileContentKind.Markdown:
                case FileContentKind.RichText:
                    ShowText(path);
                    return;
                case FileContentKind.Csv:
                    await ShowCsvAsync(path);
                    return;
                default:
                    ShowInfo(Language.getString("quickViewUnsupported"));
                    return;
            }
        }

        private async Task ShowCsvAsync(string path)
        {
            if (!Properties.Settings.Default.QuickViewCsvEnabled)
            {
                ShowInfo(Language.getString("quickViewCsvDisabled"));
                return;
            }

            FileInfo info = new FileInfo(path);
            if (info.Length > Properties.Settings.Default.QuickViewCsvMaxBytes)
            {
                ShowInfo(string.Format(
                    Language.getString("quickViewCsvTooLargeFormat"),
                    Math.Max(1, Properties.Settings.Default.QuickViewCsvMaxBytes / (1024 * 1024))));
                return;
            }

            var localCts = new CancellationTokenSource();
            previewCancellationTokenSource = localCts;
            ShowInfo(Language.getString("quickViewCsvLoading"));

            try
            {
                await Task.Delay(CsvPreviewDebounceMs, localCts.Token);
                DataTable table = await csvTableLoader.LoadAsync(path, null, localCts.Token);
                if (localCts.IsCancellationRequested || requestedPath != path || IsDisposed)
                {
                    return;
                }

                dataGridView.DataSource = table;
                AlignNumericColumns(table);
                dataGridView.Visible = true;
                gedcomGraph.Visible = false;
                    imageView.Visible = false;
                textBox.Visible = false;
                infoLabel.Visible = false;
                ClearImage();
            }
            catch (OperationCanceledException)
            {
            }
            catch
            {
                if (!localCts.IsCancellationRequested)
                {
                    ShowInfo(Language.getString("quickViewError"));
                }
            }
            finally
            {
                if (ReferenceEquals(previewCancellationTokenSource, localCts))
                {
                    previewCancellationTokenSource = null;
                }

                localCts.Dispose();
            }
        }

        private void ShowInfo(string message)
        {
            infoLabel.Text = message;
            infoLabel.Visible = true;
            imageView.Visible = false;
            textBox.Visible = false;
            dataGridView.Visible = false;
            gedcomGraph.Visible = false;
            dataGridView.DataSource = null;
            ClearImage();
        }

        private void ClearImage()
        {
            imageView.ClearImage();
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
    }
}
