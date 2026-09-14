using System;
using System.Drawing;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace DotNetCommander
{
    internal sealed class FormVectorIndex : Form
    {
        private readonly string rootPath;
        private readonly Label statusLabel;
        private readonly Label fileLabel;
        private readonly ProgressBar progressBar;
        private readonly Button cancelButton;
        private readonly VectorIndexService service = new VectorIndexService();
        private CancellationTokenSource cancellation;
        private bool busy;

        public FormVectorIndex(string rootPath)
        {
            this.rootPath = rootPath;
            Text = Language.getString("vectorIndexTitle");
            StartPosition = FormStartPosition.CenterParent;
            MinimumSize = new Size(620, 260);
            Size = new Size(760, 300);
            ShowIcon = false;
            KeyPreview = true;

            var root = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(18), ColumnCount = 1, RowCount = 6 };
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.Controls.Add(new Label { AutoSize = true, Font = DialogStyleService.CreateHeaderFont(), Text = Language.getString("vectorIndexHeading") }, 0, 0);
            root.Controls.Add(new Label { AutoEllipsis = true, Dock = DockStyle.Fill, Text = rootPath, ForeColor = SystemColors.GrayText, Margin = new Padding(0, 8, 0, 12) }, 0, 1);
            statusLabel = new Label { AutoSize = true, Dock = DockStyle.Fill, Text = Language.getString("vectorIndexPreparing") };
            fileLabel = new Label { AutoEllipsis = true, Dock = DockStyle.Fill, ForeColor = SystemColors.GrayText, Height = 24 };
            progressBar = new ProgressBar { Dock = DockStyle.Top, Height = 20, Style = ProgressBarStyle.Marquee };
            cancelButton = new Button { AutoSize = true, MinimumSize = new Size(120, 36), Text = Language.getString("cancel"), Anchor = AnchorStyles.Right };
            cancelButton.Click += (_, __) => { if (busy) cancellation?.Cancel(); else Close(); };
            root.Controls.Add(statusLabel, 0, 2);
            root.Controls.Add(fileLabel, 0, 3);
            root.Controls.Add(progressBar, 0, 4);
            root.Controls.Add(cancelButton, 0, 5);
            Controls.Add(root);
            DialogStyleService.ApplyDialogFont(this);
            Shown += async (_, __) => await RunAsync();
            FormClosing += OnFormClosing;
        }

        private async Task RunAsync()
        {
            busy = true;
            cancellation = new CancellationTokenSource();
            try
            {
                var progress = new Progress<VectorIndexProgress>(value =>
                {
                    statusLabel.Text = string.Format(Language.getString("vectorIndexProgressFormat"), value.ProcessedFiles, value.IndexedFiles, value.ErrorFiles);
                    fileLabel.Text = value.CurrentFile;
                });
                VectorIndexResult result = await service.IndexFolderAsync(rootPath, progress, cancellation.Token);
                statusLabel.Text = string.Format(Language.getString("vectorIndexCompleteFormat"), result.ProcessedFiles, result.IndexedFiles, result.VectorRecords, result.ErrorFiles);
                fileLabel.Text = result.StoreFilePath;
            }
            catch (OperationCanceledException)
            {
                statusLabel.Text = Language.getString("vectorIndexCancelled");
            }
            catch (Exception ex)
            {
                LogService.LogException("FormVectorIndex.Run", ex);
                statusLabel.Text = Language.getString("vectorIndexFailed");
                MessageBox.Show(this, ex.Message, Language.getString("error"), MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                busy = false;
                progressBar.Style = ProgressBarStyle.Blocks;
                progressBar.Value = 100;
                cancelButton.Text = Language.getString("close");
                cancellation.Dispose();
                cancellation = null;
            }
        }

        private void OnFormClosing(object sender, FormClosingEventArgs e)
        {
            if (!busy) return;
            cancellation?.Cancel();
            statusLabel.Text = Language.getString("vectorIndexCancelling");
            e.Cancel = true;
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (keyData == Keys.Escape) { if (busy) cancellation?.Cancel(); else Close(); return true; }
            return base.ProcessCmdKey(ref msg, keyData);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) { cancellation?.Cancel(); cancellation?.Dispose(); service.Dispose(); }
            base.Dispose(disposing);
        }
    }
}
