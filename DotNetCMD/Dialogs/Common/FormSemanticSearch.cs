using System;
using System.Collections.Generic;
using System.Drawing;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using View;

namespace DotNetCommander
{
    internal sealed class FormSemanticSearch : Form
    {
        private readonly string rootPath;
        private readonly TextBox queryTextBox;
        private readonly NumericUpDown topKNumeric;
        private readonly Button searchButton;
        private readonly Button askButton;
        private readonly Button cancelButton;
        private readonly DataGridView resultsGrid;
        private readonly TextBox contentTextBox;
        private readonly RichTextBox answerTextBox;
        private readonly Button openAnswerButton;
        private readonly TabControl outputTabs;
        private readonly Label statusLabel;
        private readonly ProgressBar progressBar;
        private readonly VectorIndexService service = new VectorIndexService();
        private CancellationTokenSource cancellation;
        private bool busy;
        private string answerMarkdown = string.Empty;

        public FormSemanticSearch(string rootPath, bool startInRagMode)
        {
            this.rootPath = rootPath;
            Text = startInRagMode ? Language.getString("vectorRagTitle") : Language.getString("semanticSearchTitle");
            StartPosition = FormStartPosition.CenterParent;
            MinimumSize = new Size(820, 580);
            Size = new Size(1040, 720);
            ShowIcon = false;
            KeyPreview = true;

            var root = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(16), ColumnCount = 1, RowCount = 6 };
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 58));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 42));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.Controls.Add(new Label { AutoSize = true, Font = DialogStyleService.CreateHeaderFont(), Text = Language.getString("semanticSearchHeading") }, 0, 0);
            root.Controls.Add(new Label { AutoEllipsis = true, Dock = DockStyle.Fill, Text = rootPath, ForeColor = SystemColors.GrayText }, 0, 1);

            var queryPanel = new TableLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, ColumnCount = 6, Margin = new Padding(0, 10, 0, 10) };
            queryPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            queryPanel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            queryPanel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            queryPanel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            queryPanel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            queryPanel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            queryTextBox = new TextBox { Dock = DockStyle.Fill, Margin = new Padding(0, 3, 8, 3) };
            topKNumeric = new NumericUpDown { Minimum = 1, Maximum = 100, Value = 10, Width = 60, Margin = new Padding(0, 3, 8, 3) };
            searchButton = CreateButton(Language.getString("semanticSearchButton"));
            askButton = CreateButton(Language.getString("vectorAskButton"));
            cancelButton = CreateButton(Language.getString("cancel"));
            cancelButton.Enabled = false;
            var closeButton = CreateButton(Language.getString("close"));
            searchButton.Click += async (_, __) => await SearchAsync(false);
            askButton.Click += async (_, __) => await SearchAsync(true);
            cancelButton.Click += (_, __) => cancellation?.Cancel();
            closeButton.Click += (_, __) => Close();
            queryPanel.Controls.Add(queryTextBox, 0, 0);
            queryPanel.Controls.Add(topKNumeric, 1, 0);
            queryPanel.Controls.Add(searchButton, 2, 0);
            queryPanel.Controls.Add(askButton, 3, 0);
            queryPanel.Controls.Add(cancelButton, 4, 0);
            queryPanel.Controls.Add(closeButton, 5, 0);
            root.Controls.Add(queryPanel, 0, 2);

            resultsGrid = new DataGridView { Dock = DockStyle.Fill, ReadOnly = true, AllowUserToAddRows = false, AllowUserToDeleteRows = false, RowHeadersVisible = false, AutoGenerateColumns = false, SelectionMode = DataGridViewSelectionMode.FullRowSelect, MultiSelect = false };
            resultsGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = Language.getString("semanticSearchScore"), Width = 85 });
            resultsGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = Language.getString("semanticSearchFile"), Width = 430 });
            resultsGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = Language.getString("semanticSearchChunk"), Width = 70 });
            resultsGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = Language.getString("semanticSearchPreview"), AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill });
            resultsGrid.SelectionChanged += (_, __) => ShowSelectedContent();
            root.Controls.Add(resultsGrid, 0, 3);

            outputTabs = new TabControl { Dock = DockStyle.Fill };
            var contentPage = new TabPage(Language.getString("semanticSearchContent"));
            var answerPage = new TabPage(Language.getString("vectorRagAnswer"));
            contentTextBox = CreateOutputTextBox();
            answerTextBox = new RichTextBox
            {
                Dock = DockStyle.Fill,
                ReadOnly = true,
                BorderStyle = BorderStyle.None,
                DetectUrls = true,
                BackColor = SystemColors.Window
            };
            openAnswerButton = CreateButton(Language.getString("vectorRagOpenAnswer"));
            openAnswerButton.Enabled = false;
            openAnswerButton.Click += (_, __) => OpenAnswerWindow();
            var answerLayout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2 };
            answerLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            answerLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            var answerToolbar = new FlowLayoutPanel
            {
                AutoSize = true,
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.RightToLeft,
                WrapContents = false,
                Padding = new Padding(0, 3, 0, 3)
            };
            answerToolbar.Controls.Add(openAnswerButton);
            answerLayout.Controls.Add(answerToolbar, 0, 0);
            answerLayout.Controls.Add(answerTextBox, 0, 1);
            contentPage.Controls.Add(contentTextBox);
            answerPage.Controls.Add(answerLayout);
            outputTabs.TabPages.Add(contentPage);
            outputTabs.TabPages.Add(answerPage);
            root.Controls.Add(outputTabs, 0, 4);

            var statusPanel = new TableLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, ColumnCount = 2 };
            statusPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            statusPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 180));
            statusLabel = new Label { Dock = DockStyle.Fill, AutoEllipsis = true };
            progressBar = new ProgressBar { Dock = DockStyle.Fill, Style = ProgressBarStyle.Marquee, Visible = false };
            statusPanel.Controls.Add(statusLabel, 0, 0);
            statusPanel.Controls.Add(progressBar, 1, 0);
            root.Controls.Add(statusPanel, 0, 5);
            Controls.Add(root);
            DialogStyleService.ApplyDialogFont(this);
            AcceptButton = startInRagMode ? askButton : searchButton;
            Shown += (_, __) => queryTextBox.Focus();
            FormClosing += (_, e) => { if (busy) { cancellation?.Cancel(); e.Cancel = true; } };
        }

        private async Task SearchAsync(bool ask)
        {
            if (busy || string.IsNullOrWhiteSpace(queryTextBox.Text)) return;
            SetBusy(true);
            cancellation = new CancellationTokenSource();
            try
            {
                if (ask)
                {
                    VectorRagResult result = await service.AskAsync(rootPath, queryTextBox.Text.Trim(), (int)topKNumeric.Value, cancellation.Token);
                    PopulateResults(result.Sources);
                    string answer = string.IsNullOrWhiteSpace(result.Answer) ? Language.getString("vectorRagNoContext") : result.Answer;
                    await ShowAnswerAsync(answer, cancellation.Token);
                    outputTabs.SelectedIndex = 1;
                }
                else
                {
                    List<VectorSearchResult> results = await service.SearchAsync(rootPath, queryTextBox.Text.Trim(), (int)topKNumeric.Value, cancellation.Token);
                    PopulateResults(results);
                    outputTabs.SelectedIndex = 0;
                }
            }
            catch (OperationCanceledException) { statusLabel.Text = Language.getString("vectorIndexCancelled"); }
            catch (Exception ex)
            {
                LogService.LogException("FormSemanticSearch.Search", ex);
                MessageBox.Show(this, ex.Message, Language.getString("error"), MessageBoxButtons.OK, MessageBoxIcon.Error);
                statusLabel.Text = Language.getString("semanticSearchFailed");
            }
            finally { cancellation.Dispose(); cancellation = null; SetBusy(false); }
        }

        private void PopulateResults(IReadOnlyList<VectorSearchResult> results)
        {
            resultsGrid.Rows.Clear();
            foreach (VectorSearchResult result in results)
            {
                int row = resultsGrid.Rows.Add(result.Score.ToString("0.000"), result.FilePath, result.ChunkIndex, MakePreview(result.Text));
                resultsGrid.Rows[row].Tag = result;
            }
            if (resultsGrid.Rows.Count > 0)
            {
                resultsGrid.Rows[0].Selected = true;
                resultsGrid.CurrentCell = resultsGrid.Rows[0].Cells[0];
            }
            statusLabel.Text = string.Format(Language.getString("semanticSearchResultsFormat"), results.Count);
        }

        private void ShowSelectedContent() => contentTextBox.Text = (resultsGrid.CurrentRow?.Tag as VectorSearchResult)?.Text ?? string.Empty;
        private async Task ShowAnswerAsync(string markdown, CancellationToken cancellationToken)
        {
            answerMarkdown = markdown ?? string.Empty;
            openAnswerButton.Enabled = !string.IsNullOrWhiteSpace(answerMarkdown);
            int dpi = answerTextBox.DeviceDpi > 0 ? answerTextBox.DeviceDpi : 96;
            int width = Math.Max(200, (answerTextBox.ClientSize.Width > 0 ? answerTextBox.ClientSize.Width : 800) - 24);
            int tableWidth = width * 1440 / dpi;
            RtfEdit.MarkdownRenderOptions options = RtfEdit.CaptureMarkdownRenderOptions(rootPath);
            try
            {
                string rtf = await Task.Run(() => RtfEdit.ConvertMarkdownToRtf(answerMarkdown, tableWidth, options), cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();
                if (!IsDisposed && !answerTextBox.IsDisposed)
                    answerTextBox.Rtf = rtf;
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex)
            {
                LogService.LogException("FormSemanticSearch.RenderAnswer", ex);
                answerTextBox.Text = answerMarkdown;
            }
        }

        private void OpenAnswerWindow()
        {
            if (string.IsNullOrWhiteSpace(answerMarkdown)) return;
            var editor = new RtfEdit();
            editor.Show();
            editor.LoadMarkdownContent(
                answerMarkdown,
                $"ai-answer-{DateTime.Now:yyyyMMdd-HHmmss}.md",
                Language.getString("vectorRagAnswerWindowTitle"));
        }

        private void SetBusy(bool value) { busy = value; queryTextBox.Enabled = !value; topKNumeric.Enabled = !value; searchButton.Enabled = !value; askButton.Enabled = !value; cancelButton.Enabled = value; progressBar.Visible = value; if (value) statusLabel.Text = Language.getString("semanticSearchWorking"); }
        private static string MakePreview(string text) { string value = (text ?? string.Empty).Replace('\r', ' ').Replace('\n', ' '); return value.Length <= 180 ? value : value.Substring(0, 180) + "…"; }
        private static TextBox CreateOutputTextBox() => new TextBox { Dock = DockStyle.Fill, Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Both, WordWrap = true };
        private static Button CreateButton(string text) => new Button { AutoSize = true, MinimumSize = new Size(100, 32), Text = text, Margin = new Padding(0, 0, 8, 0) };

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData) { if (keyData == Keys.Escape) { if (busy) cancellation?.Cancel(); else Close(); return true; } return base.ProcessCmdKey(ref msg, keyData); }
        protected override void Dispose(bool disposing) { if (disposing) { cancellation?.Cancel(); cancellation?.Dispose(); service.Dispose(); } base.Dispose(disposing); }
    }
}
