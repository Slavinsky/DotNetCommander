using DotNetCommander;
using System;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace View
{
    public sealed class RawFileView : Form
    {
        private const int ChunkSize = 32 * 1024;
        private static readonly Size DefaultWindowSize = new Size(1120, 760);
        private static Point savedLocation = new Point(-1, -1);
        private static Size savedSize = Size.Empty;

        private readonly RichTextBox hexBox;
        private readonly ToolStripButton firstButton;
        private readonly ToolStripButton previousButton;
        private readonly ToolStripButton nextButton;
        private readonly ToolStripButton lastButton;
        private readonly ToolStripTextBox offsetBox;
        private readonly ToolStripButton goButton;
        private readonly ToolStripStatusLabel statusLabel;
        private CancellationTokenSource loadCancellation;
        private string filePath;
        private long fileLength;
        private long currentOffset;
        private int currentBytes;

        public RawFileView()
        {
            KeyPreview = true;
            StartPosition = FormStartPosition.CenterParent;
            MinimumSize = new Size(700, 420);
            Size = savedSize.IsEmpty ? DefaultWindowSize : savedSize;
            if (savedLocation.X >= 0 && savedLocation.Y >= 0)
            {
                StartPosition = FormStartPosition.Manual;
                Location = savedLocation;
            }

            var toolStrip = new ToolStrip
            {
                GripStyle = ToolStripGripStyle.Hidden,
                RenderMode = ToolStripRenderMode.System
            };
            firstButton = CreateButton("|◀", "rawFirst", (_, __) => _ = LoadOffsetAsync(0));
            previousButton = CreateButton("◀", "rawPrevious", (_, __) => _ = LoadOffsetAsync(currentOffset - ChunkSize));
            nextButton = CreateButton("▶", "rawNext", (_, __) => _ = LoadOffsetAsync(currentOffset + Math.Max(currentBytes, ChunkSize)));
            lastButton = CreateButton("▶|", "rawLast", (_, __) => _ = LoadOffsetAsync(GetLastChunkOffset()));
            offsetBox = new ToolStripTextBox
            {
                AutoSize = false,
                Width = 150,
                ToolTipText = Language.getString("rawOffsetHint")
            };
            offsetBox.KeyDown += OffsetBox_KeyDown;
            goButton = CreateButton(Language.getString("rawGo"), "rawOffsetHint", (_, __) => NavigateToEnteredOffset());
            toolStrip.Items.Add(firstButton);
            toolStrip.Items.Add(previousButton);
            toolStrip.Items.Add(nextButton);
            toolStrip.Items.Add(lastButton);
            toolStrip.Items.Add(new ToolStripSeparator());
            toolStrip.Items.Add(new ToolStripLabel(Language.getString("rawOffset") + " 0x"));
            toolStrip.Items.Add(offsetBox);
            toolStrip.Items.Add(goButton);

            hexBox = new RichTextBox
            {
                BackColor = Color.White,
                BorderStyle = BorderStyle.None,
                DetectUrls = false,
                Dock = DockStyle.Fill,
                Font = new Font("Consolas", 10F, FontStyle.Regular, GraphicsUnit.Point),
                HideSelection = false,
                ReadOnly = true,
                WordWrap = false
            };

            var statusStrip = new StatusStrip();
            statusLabel = new ToolStripStatusLabel
            {
                Spring = true,
                TextAlign = ContentAlignment.MiddleLeft
            };
            statusStrip.Items.Add(statusLabel);

            var layout = new TableLayoutPanel
            {
                ColumnCount = 1,
                Dock = DockStyle.Fill,
                RowCount = 3
            };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 28F));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 24F));
            toolStrip.Dock = DockStyle.Fill;
            statusStrip.Dock = DockStyle.Fill;
            layout.Controls.Add(toolStrip, 0, 0);
            layout.Controls.Add(hexBox, 0, 1);
            layout.Controls.Add(statusStrip, 0, 2);
            Controls.Add(layout);
        }

        public void OpenFile(string path)
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
                throw new FileNotFoundException(Language.getString("rawFileNotFound"), path);

            filePath = Path.GetFullPath(path);
            fileLength = new FileInfo(filePath).Length;
            currentOffset = 0;
            currentBytes = 0;
            Text = Language.getString("rawView") + " — " + Path.GetFileName(filePath);
        }

        protected override async void OnShown(EventArgs e)
        {
            base.OnShown(e);
            await LoadOffsetAsync(0);
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (keyData == Keys.Escape)
            {
                Close();
                return true;
            }
            if (keyData == Keys.F1)
            {
                MessageBox.Show(this, Language.getString("rawHelp"), Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
                return true;
            }
            if (keyData == Keys.F5)
            {
                RefreshFile();
                return true;
            }
            if (keyData == (Keys.Control | Keys.G))
            {
                offsetBox.Focus();
                offsetBox.SelectAll();
                return true;
            }
            if (keyData == Keys.PageUp)
            {
                _ = LoadOffsetAsync(currentOffset - ChunkSize);
                return true;
            }
            if (keyData == Keys.PageDown)
            {
                _ = LoadOffsetAsync(currentOffset + Math.Max(currentBytes, ChunkSize));
                return true;
            }
            if (keyData == (Keys.Control | Keys.Home))
            {
                _ = LoadOffsetAsync(0);
                return true;
            }
            if (keyData == (Keys.Control | Keys.End))
            {
                _ = LoadOffsetAsync(GetLastChunkOffset());
                return true;
            }

            return base.ProcessCmdKey(ref msg, keyData);
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            loadCancellation?.Cancel();
            loadCancellation?.Dispose();
            loadCancellation = null;
            if (WindowState == FormWindowState.Normal)
            {
                savedLocation = Location;
                savedSize = Size;
            }
            base.OnFormClosed(e);
        }

        private async Task LoadOffsetAsync(long requestedOffset)
        {
            if (string.IsNullOrWhiteSpace(filePath))
                return;

            long maxOffset = Math.Max(0, fileLength - 1);
            long targetOffset = Math.Max(0, Math.Min(requestedOffset, maxOffset));
            if (fileLength == 0)
                targetOffset = 0;

            loadCancellation?.Cancel();
            loadCancellation?.Dispose();
            loadCancellation = new CancellationTokenSource();
            CancellationToken cancellationToken = loadCancellation.Token;
            SetLoading(true);
            try
            {
                byte[] bytes = await ReadChunkAsync(filePath, targetOffset, cancellationToken);
                string dump = await Task.Run(
                    () => BuildHexDump(bytes, targetOffset, fileLength),
                    cancellationToken);
                if (cancellationToken.IsCancellationRequested || IsDisposed)
                    return;

                currentOffset = targetOffset;
                currentBytes = bytes.Length;
                hexBox.Text = dump;
                hexBox.SelectionStart = 0;
                hexBox.ScrollToCaret();
                offsetBox.Text = currentOffset.ToString("X", CultureInfo.InvariantCulture);
                UpdateStatus();
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex)
            {
                LogService.LogException("RawFileView.LoadOffsetAsync", ex);
                MessageBox.Show(this, ex.Message, Language.getString("error"), MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                if (!IsDisposed)
                    SetLoading(false);
            }
        }

        private static async Task<byte[]> ReadChunkAsync(string path, long offset, CancellationToken cancellationToken)
        {
            using var stream = new FileStream(
                path,
                FileMode.Open,
                FileAccess.Read,
                FileShare.ReadWrite | FileShare.Delete,
                4096,
                FileOptions.Asynchronous | FileOptions.RandomAccess);
            stream.Position = offset;
            int requested = (int)Math.Min(ChunkSize, Math.Max(0, stream.Length - offset));
            byte[] buffer = new byte[requested];
            int totalRead = 0;
            while (totalRead < buffer.Length)
            {
                int read = await stream.ReadAsync(
                    buffer.AsMemory(totalRead, buffer.Length - totalRead),
                    cancellationToken);
                if (read == 0)
                    break;
                totalRead += read;
            }
            if (totalRead == buffer.Length)
                return buffer;
            Array.Resize(ref buffer, totalRead);
            return buffer;
        }

        private static string BuildHexDump(byte[] bytes, long baseOffset, long totalLength)
        {
            int offsetWidth = totalLength > uint.MaxValue ? 16 : 8;
            var builder = new StringBuilder(Math.Max(256, bytes.Length * 5));
            builder.Append(' ', offsetWidth);
            builder.Append("   00 01 02 03 04 05 06 07  08 09 0A 0B 0C 0D 0E 0F   ASCII");
            builder.AppendLine();
            builder.AppendLine(new string('─', offsetWidth + 70));

            for (int index = 0; index < bytes.Length; index += 16)
            {
                int count = Math.Min(16, bytes.Length - index);
                builder.Append((baseOffset + index).ToString("X" + offsetWidth, CultureInfo.InvariantCulture));
                builder.Append("   ");
                for (int column = 0; column < 16; column++)
                {
                    if (column == 8)
                        builder.Append(' ');
                    if (column < count)
                    {
                        builder.Append(bytes[index + column].ToString("X2", CultureInfo.InvariantCulture));
                        builder.Append(' ');
                    }
                    else
                    {
                        builder.Append("   ");
                    }
                }
                builder.Append("  ");
                for (int column = 0; column < count; column++)
                {
                    byte value = bytes[index + column];
                    builder.Append(value >= 0x20 && value <= 0x7E ? (char)value : '·');
                }
                builder.AppendLine();
            }
            return builder.ToString();
        }

        private void NavigateToEnteredOffset()
        {
            string value = (offsetBox.Text ?? string.Empty).Trim();
            if (value.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
                value = value.Substring(2);
            if (long.TryParse(value, NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out long offset))
            {
                _ = LoadOffsetAsync(offset);
                return;
            }
            MessageBox.Show(this, Language.getString("rawInvalidOffset"), Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }

        private void OffsetBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode != Keys.Enter)
                return;
            NavigateToEnteredOffset();
            e.Handled = true;
            e.SuppressKeyPress = true;
        }

        private void RefreshFile()
        {
            if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
                return;
            fileLength = new FileInfo(filePath).Length;
            _ = LoadOffsetAsync(currentOffset);
        }

        private long GetLastChunkOffset()
        {
            return Math.Max(0, fileLength - Math.Min(fileLength, ChunkSize));
        }

        private void SetLoading(bool loading)
        {
            firstButton.Enabled = !loading && currentOffset > 0;
            previousButton.Enabled = !loading && currentOffset > 0;
            nextButton.Enabled = !loading && currentOffset + currentBytes < fileLength;
            lastButton.Enabled = !loading && currentOffset + currentBytes < fileLength;
            goButton.Enabled = !loading;
            UseWaitCursor = loading;
        }

        private void UpdateStatus()
        {
            statusLabel.Text = string.Format(
                CultureInfo.CurrentCulture,
                Language.getString("rawStatusFormat"),
                currentOffset,
                currentBytes > 0 ? currentOffset + currentBytes - 1 : currentOffset,
                fileLength,
                currentBytes);
        }

        private static ToolStripButton CreateButton(string text, string tooltipKey, EventHandler click)
        {
            var button = new ToolStripButton(text)
            {
                DisplayStyle = ToolStripItemDisplayStyle.Text,
                ToolTipText = Language.getString(tooltipKey)
            };
            button.Click += click;
            return button;
        }
    }
}
