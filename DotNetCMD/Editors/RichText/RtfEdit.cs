using System;
using System.ComponentModel;
using System.Drawing;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Forms;
using DotNetCommander.Properties;

namespace View
{
    public partial class RtfEdit : Form
    {
        private const string EmptyDocumentRtf = "{\\rtf1\\ansi\\deff0{\\fonttbl{\\f0 Segoe UI;}}\\uc1\\fs22\\par}";
        private RichTextBox richTextBox;
        private StatusStrip statusStrip;
        private ToolStripStatusLabel statusMessageLabel;
        private ToolStripStatusLabel statusStatsLabel;
        private ContextMenuStrip editorContextMenu;
        private ToolStripMenuItem styleMenuItem;
        private string currentFilePath;
        private bool previewMode;
        private string markdownPreviewSource;
        private bool markdownPreviewHasTables;
        private Timer markdownResizeTimer;
        private int lastMarkdownRenderWidth;
        private int markdownRenderGeneration;
        private static Point _savedLocation = new Point(-1, -1);
        private static Size _savedSize = new Size(0, 0);

        public RtfEdit()
        {
            InitializeComponent();
            InitializeStatusBar();
            InitializeEditor();
        }

        private void InitializeStatusBar()
        {
            statusMessageLabel = new ToolStripStatusLabel
            {
                AutoSize = false,
                Width = 260,
                TextAlign = ContentAlignment.MiddleLeft
            };
            statusStatsLabel = new ToolStripStatusLabel
            {
                Spring = true,
                TextAlign = ContentAlignment.MiddleRight
            };

            statusStrip = new StatusStrip();
            statusStrip.SizingGrip = false;
            statusStrip.Items.Add(statusMessageLabel);
            statusStrip.Items.Add(statusStatsLabel);
            Controls.Add(statusStrip);
        }

        private void InitializeEditor()
        {
            richTextBox = new RichTextBox();
            richTextBox.Dock = DockStyle.Fill;
            richTextBox.HideSelection = false;
            richTextBox.DetectUrls = true;
            richTextBox.AcceptsTab = true;
            richTextBox.EnableAutoDragDrop = true;
            richTextBox.Font = CreateEditorFont();
            richTextBox.KeyDown += RichTextBox_KeyDown;
            richTextBox.MouseWheel += RichTextBox_MouseWheel;
            richTextBox.SelectionChanged += (_, __) => UpdateStatusBar();
            richTextBox.TextChanged += (_, __) => UpdateStatusBar();
            richTextBox.ContextMenuStrip = CreateEditorContextMenu();

            markdownResizeTimer = new Timer { Interval = 250 };
            markdownResizeTimer.Tick += MarkdownResizeTimer_Tick;
            Resize += RtfEdit_Resize;

            Controls.Add(richTextBox);
            ApplyStatusBarSettings();
            UpdateStatusBar();
        }

        private void InitializeComponent()
        {
            SuspendLayout();
            AutoScaleDimensions = new System.Drawing.SizeF(12F, 16F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new System.Drawing.Size(900, 650);
            Name = "RtfEdit";
            Text = "RTF Editor";
            KeyPreview = true;
            ResumeLayout(false);
        }

        private void RichTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Control && e.Shift && e.KeyCode == Keys.S)
            {
                SaveFileAs();
                e.Handled = true;
            }
            else if (e.Control && e.KeyCode == Keys.S)
            {
                SaveFile();
                e.Handled = true;
            }
            else if (e.Control && e.KeyCode == Keys.B)
            {
                ToggleFontStyle(FontStyle.Bold);
                e.Handled = true;
            }
            else if (e.Control && e.KeyCode == Keys.I)
            {
                ToggleFontStyle(FontStyle.Italic);
                e.Handled = true;
            }
            else if (e.Control && e.KeyCode == Keys.U)
            {
                ToggleFontStyle(FontStyle.Underline);
                e.Handled = true;
            }
            else if (e.Control && e.KeyCode == Keys.T)
            {
                ShowFontDialog();
                e.Handled = true;
            }
            else if (e.Control && e.KeyCode == Keys.Add)
            {
                ChangeFontSize(1f);
                e.Handled = true;
            }
            else if (e.Control && e.KeyCode == Keys.Subtract)
            {
                ChangeFontSize(-1f);
                e.Handled = true;
            }
        }

        private void RichTextBox_MouseWheel(object sender, MouseEventArgs e)
        {
            if (ModifierKeys == Keys.Control)
            {
                if (e.Delta > 0)
                    ChangeFontSize(1f);
                else
                    ChangeFontSize(-1f);
            }
        }

        private void ToggleFontStyle(FontStyle style)
        {
            Font currentFont = richTextBox.SelectionFont ?? richTextBox.Font;
            FontStyle newStyle;

            if ((currentFont.Style & style) == style)
            {
                newStyle = currentFont.Style & ~style;
            }
            else
            {
                newStyle = currentFont.Style | style;
            }

            ApplyFontToSelection(new Font(currentFont, newStyle));
        }

        private void ChangeFontSize(float delta)
        {
            Font currentFont = richTextBox.SelectionFont ?? richTextBox.Font;
            float newSize = Math.Max(6f, Math.Min(96f, currentFont.Size + delta));

            if (Math.Abs(newSize - currentFont.Size) < 0.1f)
                return;

            Font resizedFont = new Font(currentFont.FontFamily, newSize, currentFont.Style);
            ApplyFontToSelection(resizedFont);
            UpdateStatusBar();
        }

        private void ApplyFontToSelection(Font font, bool persistAsDefault = true)
        {
            if (richTextBox.SelectionLength > 0)
            {
                richTextBox.SelectionFont = font;
            }
            else
            {
                richTextBox.SelectionFont = font;
                richTextBox.Font = new Font(font.FontFamily, font.Size, font.Style);
            }

            if (persistAsDefault && richTextBox.SelectionLength == 0)
            {
                SaveEditorAppearance(font);
            }

            UpdateStatusBar();
        }

        private static Font CreateEditorFont()
        {
            string fontName = string.IsNullOrWhiteSpace(Settings.Default.RtfEditorFontName)
                ? "Segoe UI"
                : Settings.Default.RtfEditorFontName;
            float fontSize = Math.Max(6f, Settings.Default.RtfEditorFontSize);

            try
            {
                return new Font(fontName, fontSize);
            }
            catch
            {
                return new Font("Segoe UI", 11f);
            }
        }

        private static void SaveEditorAppearance(Font font)
        {
            if (font == null)
                return;

            Settings.Default.RtfEditorFontName = font.FontFamily.Name;
            Settings.Default.RtfEditorFontSize = font.Size;
            Settings.Default.Save();
        }

        private void ShowFontDialog()
        {
            if (richTextBox.ReadOnly)
                return;

            using FontDialog dialog = new FontDialog();
            dialog.ShowColor = true;
            dialog.Font = richTextBox.SelectionFont ?? richTextBox.Font;
            dialog.Color = richTextBox.SelectionColor.IsEmpty ? richTextBox.ForeColor : richTextBox.SelectionColor;

            if (dialog.ShowDialog(this) != DialogResult.OK)
                return;

            richTextBox.SelectionFont = dialog.Font;
            richTextBox.SelectionColor = dialog.Color;
            if (richTextBox.SelectionLength == 0)
            {
                richTextBox.Font = dialog.Font;
                richTextBox.ForeColor = dialog.Color;
                SaveEditorAppearance(dialog.Font);
            }

            UpdateStatusBar();
        }

        private ContextMenuStrip CreateEditorContextMenu()
        {
            editorContextMenu = new ContextMenuStrip();
            editorContextMenu.Opening += EditorContextMenu_Opening;
            editorContextMenu.ItemClicked += EditorContextMenu_ItemClicked;

            editorContextMenu.Items.Add(CreateContextMenuItem("Undo"));
            editorContextMenu.Items.Add(CreateContextMenuItem("Redo"));
            editorContextMenu.Items.Add(new ToolStripSeparator());
            editorContextMenu.Items.Add(CreateContextMenuItem("Cut"));
            editorContextMenu.Items.Add(CreateContextMenuItem("Copy"));
            editorContextMenu.Items.Add(CreateContextMenuItem("Paste"));
            editorContextMenu.Items.Add(CreateContextMenuItem("Delete"));
            editorContextMenu.Items.Add(new ToolStripSeparator());
            editorContextMenu.Items.Add(CreateContextMenuItem("SelectAll"));
            editorContextMenu.Items.Add(new ToolStripSeparator());
            styleMenuItem = CreateMarkdownStyleMenu();
            editorContextMenu.Items.Add(styleMenuItem);
            editorContextMenu.Items.Add(CreateContextMenuItem("Bold"));
            editorContextMenu.Items.Add(CreateContextMenuItem("Italic"));
            editorContextMenu.Items.Add(CreateContextMenuItem("Underline"));
            editorContextMenu.Items.Add(CreateContextMenuItem("Font"));

            return editorContextMenu;
        }

        private static ToolStripMenuItem CreateContextMenuItem(string name)
        {
            return new ToolStripMenuItem
            {
                Name = name,
                Text = name switch
                {
                    "SelectAll" => "Select All",
                    "Style" => "Style",
                    "Font" => "Font...",
                    _ => name
                }
            };
        }

        private ToolStripMenuItem CreateMarkdownStyleMenu()
        {
            ToolStripMenuItem menu = CreateContextMenuItem("Style");
            menu.DropDownItems.Add(CreateMarkdownStyleMenuItem("Normal"));
            menu.DropDownItems.Add(new ToolStripSeparator());
            menu.DropDownItems.Add(CreateMarkdownStyleMenuItem("H1"));
            menu.DropDownItems.Add(CreateMarkdownStyleMenuItem("H2"));
            menu.DropDownItems.Add(CreateMarkdownStyleMenuItem("H3"));
            menu.DropDownItems.Add(CreateMarkdownStyleMenuItem("H4"));
            menu.DropDownItems.Add(CreateMarkdownStyleMenuItem("H5"));
            menu.DropDownItems.Add(CreateMarkdownStyleMenuItem("H6"));
            menu.DropDownItems.Add(new ToolStripSeparator());
            menu.DropDownItems.Add(CreateMarkdownStyleMenuItem("Code"));
            return menu;
        }

        private ToolStripMenuItem CreateMarkdownStyleMenuItem(string styleName)
        {
            ToolStripMenuItem item = new ToolStripMenuItem(styleName)
            {
                Tag = styleName
            };
            item.Click += MarkdownStyleMenuItem_Click;
            return item;
        }

        private void EditorContextMenu_Opening(object sender, CancelEventArgs e)
        {
            if (editorContextMenu == null || richTextBox == null)
                return;

            foreach (ToolStripItem item in editorContextMenu.Items)
            {
                if (item is not ToolStripMenuItem menuItem)
                    continue;

                switch (menuItem.Name)
                {
                    case "Undo":
                        menuItem.Enabled = richTextBox.CanUndo && !richTextBox.ReadOnly;
                        break;
                    case "Redo":
                        menuItem.Enabled = richTextBox.CanRedo && !richTextBox.ReadOnly;
                        break;
                    case "Cut":
                        menuItem.Enabled = richTextBox.SelectionLength > 0 && !richTextBox.ReadOnly;
                        break;
                    case "Copy":
                        menuItem.Enabled = richTextBox.SelectionLength > 0;
                        break;
                    case "Paste":
                        menuItem.Enabled = Clipboard.ContainsText() && !richTextBox.ReadOnly;
                        break;
                    case "Delete":
                        menuItem.Enabled = richTextBox.SelectionLength > 0 && !richTextBox.ReadOnly;
                        break;
                    case "SelectAll":
                        menuItem.Enabled = richTextBox.TextLength > 0 && richTextBox.SelectionLength < richTextBox.TextLength;
                        break;
                    case "Bold":
                    case "Italic":
                    case "Underline":
                    case "Font":
                        menuItem.Enabled = !richTextBox.ReadOnly;
                        break;
                }
            }

            if (styleMenuItem != null)
            {
                styleMenuItem.Enabled = !richTextBox.ReadOnly;
            }
        }

        private void EditorContextMenu_ItemClicked(object sender, ToolStripItemClickedEventArgs e)
        {
            if (richTextBox == null || e.ClickedItem == null)
                return;

            switch (e.ClickedItem.Name)
            {
                case "Undo":
                    richTextBox.Undo();
                    break;
                case "Redo":
                    richTextBox.Redo();
                    break;
                case "Cut":
                    richTextBox.Cut();
                    break;
                case "Copy":
                    richTextBox.Copy();
                    break;
                case "Paste":
                    richTextBox.Paste();
                    break;
                case "Delete":
                    richTextBox.SelectedText = string.Empty;
                    break;
                case "SelectAll":
                    richTextBox.SelectAll();
                    break;
                case "Bold":
                    ToggleFontStyle(FontStyle.Bold);
                    break;
                case "Italic":
                    ToggleFontStyle(FontStyle.Italic);
                    break;
                case "Underline":
                    ToggleFontStyle(FontStyle.Underline);
                    break;
                case "Font":
                    ShowFontDialog();
                    break;
            }
        }

        private void MarkdownStyleMenuItem_Click(object sender, EventArgs e)
        {
            if (sender is ToolStripMenuItem menuItem && menuItem.Tag is string styleName)
            {
                ApplyMarkdownStyle(styleName);
            }
        }

        private void ApplyMarkdownStyle(string styleName)
        {
            if (richTextBox == null || richTextBox.ReadOnly)
                return;

            FontStyle fontStyle = FontStyle.Regular;
            string fontName = Settings.Default.MarkdownPreviewFontName;
            float fontSize = Math.Max(6f, Settings.Default.MarkdownPreviewBaseFontSize);

            switch (styleName)
            {
                case "H1":
                    fontStyle = FontStyle.Bold;
                    fontSize = Math.Max(6f, Settings.Default.MarkdownPreviewH1FontSize);
                    break;
                case "H2":
                    fontStyle = FontStyle.Bold;
                    fontSize = Math.Max(6f, Settings.Default.MarkdownPreviewH2FontSize);
                    break;
                case "H3":
                    fontStyle = FontStyle.Bold;
                    fontSize = Math.Max(6f, Settings.Default.MarkdownPreviewH3FontSize);
                    break;
                case "H4":
                    fontStyle = FontStyle.Bold;
                    fontSize = Math.Max(6f, Settings.Default.MarkdownPreviewH4FontSize);
                    break;
                case "H5":
                    fontStyle = FontStyle.Bold;
                    fontSize = Math.Max(6f, Settings.Default.MarkdownPreviewH5FontSize);
                    break;
                case "H6":
                    fontStyle = FontStyle.Bold;
                    fontSize = Math.Max(6f, Settings.Default.MarkdownPreviewH6FontSize);
                    break;
                case "Code":
                    fontName = Settings.Default.MarkdownPreviewCodeFontName;
                    fontSize = Math.Max(6f, Settings.Default.MarkdownPreviewBaseFontSize);
                    break;
            }

            if (string.IsNullOrWhiteSpace(fontName))
            {
                fontName = styleName == "Code" ? "Consolas" : "Segoe UI";
            }

            try
            {
                ApplyFontToSelection(new Font(fontName, fontSize, fontStyle), false);
            }
            catch
            {
                ApplyFontToSelection(new Font(styleName == "Code" ? "Consolas" : "Segoe UI", fontSize, fontStyle), false);
            }
        }

        private void ApplyStatusBarSettings()
        {
            if (statusStrip != null)
            {
                statusStrip.Visible = Settings.Default.ShowEditorStatusBar;
            }
        }

        private void ShowStatusMessage(string message)
        {
            if (statusMessageLabel == null)
                return;

            statusMessageLabel.Text = message ?? string.Empty;
        }

        private void UpdateStatusBar()
        {
            if (statusStatsLabel == null || richTextBox == null)
                return;

            int textLength = richTextBox.TextLength;
            int lineCount = richTextBox.Lines.Length;
            if (lineCount == 0)
                lineCount = 1;

            int selectionLength = richTextBox.SelectionLength;
            int caretIndex = richTextBox.SelectionStart;
            int caretLine = richTextBox.GetLineFromCharIndex(caretIndex) + 1;
            int lineStart = richTextBox.GetFirstCharIndexOfCurrentLine();
            int caretColumn = Math.Max(1, caretIndex - Math.Max(0, lineStart) + 1);

            statusStatsLabel.Text = $"Lines: {lineCount}   Chars: {textLength:0,0}   Sel: {selectionLength:0,0}   Ln {caretLine}, Col {caretColumn}";
        }

        public async void LoadFile(string filePath, bool preview = false)
        {
            try
            {
                markdownRenderGeneration++;
                markdownResizeTimer.Stop();
                markdownPreviewSource = null;
                markdownPreviewHasTables = false;
                lastMarkdownRenderWidth = 0;
                currentFilePath = filePath;
                previewMode = preview;
                UpdateWindowTitle();

                string extension = Path.GetExtension(filePath)?.ToLowerInvariant() ?? string.Empty;
                if (previewMode && string.Equals(extension, ".md", StringComparison.OrdinalIgnoreCase))
                {
                    markdownPreviewSource = File.ReadAllText(filePath, Encoding.UTF8);
                    markdownPreviewHasTables = ContainsMarkdownTable(markdownPreviewSource);
                    richTextBox.ReadOnly = true;
                    await RenderMarkdownPreviewAsync(showWaitAfterDelay: true);
                }
                else if (string.Equals(extension, ".rtf", StringComparison.OrdinalIgnoreCase))
                {
                    LoadRtfDocument(filePath);
                }
                else
                {
                    string content = File.ReadAllText(filePath, Encoding.UTF8);
                    richTextBox.Text = content;
                }

                richTextBox.ReadOnly = previewMode;
                ShowStatusMessage(previewMode ? "Preview mode" : string.Empty);
                MoveCaretToDocumentStart();
                UpdateStatusBar();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading file: {ex.Message}", "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void RtfEdit_Resize(object sender, EventArgs e)
        {
            if (!Settings.Default.MarkdownPreviewRerenderOnResize ||
                string.IsNullOrEmpty(markdownPreviewSource) ||
                !markdownPreviewHasTables ||
                WindowState == FormWindowState.Minimized)
            {
                return;
            }

            markdownResizeTimer.Stop();
            markdownResizeTimer.Start();
        }

        private async void MarkdownResizeTimer_Tick(object sender, EventArgs e)
        {
            markdownResizeTimer.Stop();

            if (!Settings.Default.MarkdownPreviewRerenderOnResize ||
                string.IsNullOrEmpty(markdownPreviewSource) ||
                !markdownPreviewHasTables ||
                richTextBox.ClientSize.Width <= 0 ||
                richTextBox.ClientSize.Width == lastMarkdownRenderWidth)
            {
                return;
            }

            try
            {
                await RenderMarkdownPreviewAsync(preserveView: true);
            }
            catch (Exception ex)
            {
                DotNetCommander.LogService.LogException("RtfEdit.MarkdownResize", ex);
            }
        }

        private async Task RenderMarkdownPreviewAsync(bool preserveView = false, bool showWaitAfterDelay = false)
        {
            if (markdownPreviewSource == null)
                return;

            string markdown = markdownPreviewSource;
            int tableWidth = GetTableWidthTwips();
            lastMarkdownRenderWidth = richTextBox.ClientSize.Width;
            MarkdownRenderOptions options = CaptureMarkdownRenderOptions();
            int renderGeneration = ++markdownRenderGeneration;
            int selectionStart = richTextBox.SelectionStart;
            int selectionLength = richTextBox.SelectionLength;
            int firstVisibleCharacter = preserveView
                ? richTextBox.GetCharIndexFromPosition(new Point(1, 1))
                : 0;

            Task<string> renderTask = Task.Run(() => ConvertMarkdownToRtf(markdown, tableWidth, options));
            frmWait waitForm = null;

            try
            {
                if (showWaitAfterDelay && await Task.WhenAny(renderTask, Task.Delay(500)) != renderTask)
                {
                    waitForm = new frmWait(DotNetCommander.Language.getString("markdownRendering"));
                    waitForm.Show(this);
                    waitForm.Refresh();
                }

                string renderedRtf = await renderTask;
                if (renderGeneration != markdownRenderGeneration || IsDisposed || richTextBox.IsDisposed)
                    return;

                richTextBox.Rtf = renderedRtf;
            }
            finally
            {
                if (waitForm != null && !waitForm.IsDisposed)
                {
                    waitForm.Close();
                    waitForm.Dispose();
                }
            }

            lastMarkdownRenderWidth = richTextBox.ClientSize.Width;

            if (!preserveView)
                return;

            firstVisibleCharacter = Math.Min(firstVisibleCharacter, richTextBox.TextLength);
            richTextBox.Select(firstVisibleCharacter, 0);
            richTextBox.ScrollToCaret();

            selectionStart = Math.Min(selectionStart, richTextBox.TextLength);
            selectionLength = Math.Min(selectionLength, richTextBox.TextLength - selectionStart);
            richTextBox.Select(selectionStart, selectionLength);
        }

        private static MarkdownRenderOptions CaptureMarkdownRenderOptions()
        {
            return new MarkdownRenderOptions
            {
                BodyFont = string.IsNullOrWhiteSpace(Settings.Default.MarkdownPreviewFontName)
                    ? "Segoe UI"
                    : Settings.Default.MarkdownPreviewFontName,
                CodeFont = string.IsNullOrWhiteSpace(Settings.Default.MarkdownPreviewCodeFontName)
                    ? "Consolas"
                    : Settings.Default.MarkdownPreviewCodeFontName,
                BaseFontSize = Settings.Default.MarkdownPreviewBaseFontSize,
                H1FontSize = Settings.Default.MarkdownPreviewH1FontSize,
                H2FontSize = Settings.Default.MarkdownPreviewH2FontSize,
                H3FontSize = Settings.Default.MarkdownPreviewH3FontSize,
                H4FontSize = Settings.Default.MarkdownPreviewH4FontSize,
                H5FontSize = Settings.Default.MarkdownPreviewH5FontSize,
                H6FontSize = Settings.Default.MarkdownPreviewH6FontSize
            };
        }

        private sealed class MarkdownRenderOptions
        {
            public string BodyFont { get; init; }
            public string CodeFont { get; init; }
            public int BaseFontSize { get; init; }
            public int H1FontSize { get; init; }
            public int H2FontSize { get; init; }
            public int H3FontSize { get; init; }
            public int H4FontSize { get; init; }
            public int H5FontSize { get; init; }
            public int H6FontSize { get; init; }
        }

        private static bool ContainsMarkdownTable(string markdown)
        {
            if (string.IsNullOrEmpty(markdown))
                return false;

            string[] lines = markdown.Replace("\r\n", "\n").Replace("\r", "\n").Split('\n');
            for (int i = 0; i + 1 < lines.Length; i++)
            {
                if (IsTableRow(lines[i]) && IsTableSeparatorRow(lines[i + 1]))
                    return true;
            }

            return false;
        }

        private void MoveCaretToDocumentStart()
        {
            if (richTextBox == null)
                return;

            richTextBox.SelectionStart = 0;
            richTextBox.SelectionLength = 0;
            if (!IsHandleCreated)
                return;

            BeginInvoke(new Action(() =>
            {
                if (IsDisposed || richTextBox.IsDisposed)
                    return;

                richTextBox.SelectionStart = 0;
                richTextBox.SelectionLength = 0;
                richTextBox.ScrollToCaret();
                UpdateStatusBar();
            }));
        }

        public static string CreateEmptyDocumentRtf()
        {
            return EmptyDocumentRtf;
        }

        public void SaveFile()
        {
            if (previewMode)
            {
                NotifySaveBlocked();
                return;
            }

            if (string.IsNullOrEmpty(currentFilePath) && !PromptForSavePath())
            {
                return;
            }

            try
            {
                string extension = Path.GetExtension(currentFilePath)?.ToLowerInvariant() ?? string.Empty;
                if (extension == ".rtf")
                {
                    richTextBox.SaveFile(currentFilePath, RichTextBoxStreamType.RichText);
                }
                else
                {
                    File.WriteAllText(currentFilePath, richTextBox.Text, Encoding.UTF8);
                }

                UpdateWindowTitle();
                NotifySaveSuccess(currentFilePath);
            }
            catch (Exception ex)
            {
                ShowStatusMessage("Save failed");
                MessageBox.Show($"Error saving file: {ex.Message}", "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        public void SaveFileAs()
        {
            string previousPath = currentFilePath;
            if (!PromptForSavePath())
            {
                currentFilePath = previousPath;
                return;
            }

            string targetPath = currentFilePath;
            currentFilePath = previousPath;

            try
            {
                string extension = Path.GetExtension(targetPath)?.ToLowerInvariant() ?? string.Empty;
                if (extension == ".rtf")
                {
                    richTextBox.SaveFile(targetPath, RichTextBoxStreamType.RichText);
                }
                else
                {
                    File.WriteAllText(targetPath, richTextBox.Text, Encoding.UTF8);
                }

                if (!previewMode)
                {
                    currentFilePath = targetPath;
                    UpdateWindowTitle();
                }

                NotifySaveSuccess(targetPath);
            }
            catch (Exception ex)
            {
                ShowStatusMessage("Save failed");
                MessageBox.Show($"Error saving file: {ex.Message}", "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private bool PromptForSavePath()
        {
            using SaveFileDialog saveDialog = new SaveFileDialog();
            saveDialog.Filter = "Rich Text Format (*.rtf)|*.rtf|Text files (*.txt)|*.txt|All files (*.*)|*.*";
            saveDialog.FilterIndex = GetRtfSaveFilterIndex(currentFilePath);

            if (!string.IsNullOrWhiteSpace(currentFilePath))
            {
                saveDialog.FileName = Path.GetFileName(currentFilePath);
                string initialDirectory = Path.GetDirectoryName(currentFilePath);
                if (!string.IsNullOrWhiteSpace(initialDirectory) && Directory.Exists(initialDirectory))
                {
                    saveDialog.InitialDirectory = initialDirectory;
                }
            }

            if (saveDialog.ShowDialog(this) != DialogResult.OK)
                return false;

            currentFilePath = saveDialog.FileName;
            UpdateWindowTitle();
            return true;
        }

        private static int GetRtfSaveFilterIndex(string filePath)
        {
            string extension = Path.GetExtension(filePath)?.ToLowerInvariant();
            return extension == ".txt" ? 2 : 1;
        }

        private void NotifySaveSuccess(string path)
        {
            string message = $"Saved: {Path.GetFileName(path)}";
            if (Settings.Default.ShowEditorStatusBar)
            {
                ShowStatusMessage(message);
                UpdateStatusBar();
                return;
            }

            MessageBox.Show("File saved successfully!", "Success",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void NotifySaveBlocked()
        {
            const string message = "Preview is read-only. Use Ctrl+Shift+S to save a copy.";
            if (Settings.Default.ShowEditorStatusBar)
            {
                ShowStatusMessage(message);
                return;
            }

            MessageBox.Show(message, "Info", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void UpdateWindowTitle()
        {
            if (string.IsNullOrEmpty(currentFilePath))
                Text = "RTF Editor";
            else
                Text = Path.GetFileName(currentFilePath) + (previewMode ? " - Preview" : " - RTF Editor");
        }

        private void LoadRtfDocument(string filePath)
        {
            if (!File.Exists(filePath) || new FileInfo(filePath).Length == 0)
            {
                richTextBox.Rtf = EmptyDocumentRtf;
                return;
            }

            try
            {
                richTextBox.LoadFile(filePath, RichTextBoxStreamType.RichText);
            }
            catch (ArgumentException)
            {
                string content = File.ReadAllText(filePath, Encoding.UTF8);
                if (string.IsNullOrWhiteSpace(content))
                {
                    richTextBox.Rtf = EmptyDocumentRtf;
                    return;
                }

                throw;
            }
        }

        private string EscapeRtf(string text)
        {
            if (string.IsNullOrEmpty(text))
                return string.Empty;

            StringBuilder sb = new StringBuilder();
            foreach (char c in text)
            {
                if (c == '\\') sb.Append("\\\\");
                else if (c == '{') sb.Append("\\{");
                else if (c == '}') sb.Append("\\}");
                else if (c == '\n') sb.Append("\\par ");
                else if (c <= 0x7f)
                    sb.Append(c);
                else
                    sb.Append("\\u" + Convert.ToInt32(c) + "?");
            }

            return sb.ToString();
        }

        private string ConvertMarkdownToRtf(string markdown, int tableWidth, MarkdownRenderOptions options)
        {
            var sb = new StringBuilder();
            string bodyFont = options.BodyFont;
            string codeFont = options.CodeFont;
            int bodySize = ToRtfHalfPoints(options.BaseFontSize, 12);

            sb.Append("{\\rtf1\\ansi\\deff0{\\fonttbl{\\f0 ");
            sb.Append(EscapeRtf(bodyFont));
            sb.Append(";}{\\f1 ");
            sb.Append(EscapeRtf(codeFont));
            sb.Append(";}}\\uc1\\fs");
            sb.Append(bodySize);

            var lines = markdown.Replace("\r\n", "\n").Replace("\r", "\n").Split('\n');
            int lineIndex = 0;
            while (lineIndex < lines.Length)
            {
                string line = lines[lineIndex].TrimEnd();

                if (IsTableRow(line) && lineIndex + 1 < lines.Length && IsTableSeparatorRow(lines[lineIndex + 1]))
                {
                    lineIndex = AppendMarkdownTable(sb, lines, lineIndex, bodySize, tableWidth);
                    continue;
                }

                if (string.IsNullOrWhiteSpace(line))
                {
                    sb.Append("\\par ");
                    lineIndex++;
                    continue;
                }

                if (line.Trim() == "---")
                    AppendMarkdownHorizontalRule(sb, tableWidth, bodySize);
                else if (line.StartsWith("###### "))
                    sb.Append("\\fs" + ToRtfHalfPoints(options.H6FontSize, 9) + "\\b " + ApplyInlineMarkdown(line.Substring(7)) + "\\b0\\fs" + bodySize + "\\par ");
                else if (line.StartsWith("##### "))
                    sb.Append("\\fs" + ToRtfHalfPoints(options.H5FontSize, 10) + "\\b " + ApplyInlineMarkdown(line.Substring(6)) + "\\b0\\fs" + bodySize + "\\par ");
                else if (line.StartsWith("#### "))
                    sb.Append("\\fs" + ToRtfHalfPoints(options.H4FontSize, 11) + "\\b " + ApplyInlineMarkdown(line.Substring(5)) + "\\b0\\fs" + bodySize + "\\par ");
                else if (line.StartsWith("### "))
                    sb.Append("\\fs" + ToRtfHalfPoints(options.H3FontSize, 12) + "\\b " + ApplyInlineMarkdown(line.Substring(4)) + "\\b0\\fs" + bodySize + "\\par ");
                else if (line.StartsWith("## "))
                    sb.Append("\\fs" + ToRtfHalfPoints(options.H2FontSize, 13) + "\\b " + ApplyInlineMarkdown(line.Substring(3)) + "\\b0\\fs" + bodySize + "\\par ");
                else if (line.StartsWith("# "))
                    sb.Append("\\fs" + ToRtfHalfPoints(options.H1FontSize, 14) + "\\b " + ApplyInlineMarkdown(line.Substring(2)) + "\\b0\\fs" + bodySize + "\\par ");
                else if (line.StartsWith("- ") || line.StartsWith("* "))
                    sb.Append("\\par {\\pntext\\f1\\'B7\\tab} " + ApplyInlineMarkdown(line.Substring(2)) + "\\par ");
                else
                    sb.Append(ApplyInlineMarkdown(line) + "\\par ");

                lineIndex++;
            }

            sb.Append("}");
            return sb.ToString();
        }

        private static void AppendMarkdownHorizontalRule(StringBuilder sb, int tableWidth, int bodySize)
        {
            // RichEdit does not reliably paint a paragraph border on an empty paragraph.
            // A single-cell row uses the same border primitives as Markdown tables and
            // therefore produces a stable full-width horizontal rule.
            sb.Append("\\trowd\\trgaph0\\trleft0\\trrh120");
            sb.Append("\\clbrdrb\\brdrs\\brdrw15\\cellx");
            sb.Append(tableWidth);
            sb.Append("\\pard\\intbl\\fs2 \\~\\cell\\row");
            sb.Append("\\pard\\fs");
            sb.Append(bodySize);
            sb.Append(" ");
        }

        private static bool IsTableRow(string line)
        {
            if (string.IsNullOrWhiteSpace(line))
                return false;

            string trimmed = line.Trim();
            return trimmed.Contains('|');
        }

        private static readonly Regex TableSeparatorRowRegex = new Regex(
            @"^\s*\|?\s*:?-{1,}:?\s*(\|\s*:?-{1,}:?\s*)*\|?\s*$",
            RegexOptions.Compiled);

        private static bool IsTableSeparatorRow(string line)
        {
            if (string.IsNullOrWhiteSpace(line))
                return false;

            return TableSeparatorRowRegex.IsMatch(line.Trim());
        }

        private static readonly Regex UnescapedPipeSplitRegex = new Regex(@"(?<!\\)\|", RegexOptions.Compiled);

        private static System.Collections.Generic.List<string> SplitMarkdownTableRow(string line)
        {
            string trimmed = line.Trim();
            if (trimmed.StartsWith("|"))
                trimmed = trimmed.Substring(1);
            if (trimmed.EndsWith("|") && !trimmed.EndsWith("\\|"))
                trimmed = trimmed.Substring(0, trimmed.Length - 1);

            var parts = UnescapedPipeSplitRegex.Split(trimmed);
            var result = new System.Collections.Generic.List<string>(parts.Length);
            foreach (var part in parts)
            {
                result.Add(part.Trim().Replace("\\|", "|"));
            }

            return result;
        }

        private static System.Collections.Generic.List<char> ParseTableAlignments(string separatorLine, int expectedCount)
        {
            var cells = SplitMarkdownTableRow(separatorLine.Trim());
            var result = new System.Collections.Generic.List<char>(expectedCount);

            foreach (var cell in cells)
            {
                string trimmedCell = cell.Trim();
                bool leftColon = trimmedCell.StartsWith(":");
                bool rightColon = trimmedCell.EndsWith(":");

                if (leftColon && rightColon)
                    result.Add('c');
                else if (rightColon)
                    result.Add('r');
                else
                    result.Add('l');
            }

            while (result.Count < expectedCount)
                result.Add('l');

            return result;
        }

        private int AppendMarkdownTable(StringBuilder sb, string[] lines, int startIndex, int bodySize, int tableWidth)
        {
            var headerCells = SplitMarkdownTableRow(lines[startIndex].TrimEnd());
            var alignments = ParseTableAlignments(lines[startIndex + 1].TrimEnd(), headerCells.Count);

            var bodyRows = new System.Collections.Generic.List<System.Collections.Generic.List<string>>();
            int i = startIndex + 2;
            while (i < lines.Length)
            {
                string rowLine = lines[i].TrimEnd();
                if (string.IsNullOrWhiteSpace(rowLine) || !IsTableRow(rowLine) || IsTableSeparatorRow(rowLine))
                    break;

                bodyRows.Add(SplitMarkdownTableRow(rowLine));
                i++;
            }

            int columnCount = Math.Max(1, headerCells.Count);
            int[] colWidths = CalculateColumnWidths(headerCells, bodyRows, columnCount, tableWidth);

            AppendMarkdownTableRow(sb, headerCells, alignments, columnCount, colWidths, true);
            foreach (var row in bodyRows)
            {
                AppendMarkdownTableRow(sb, row, alignments, columnCount, colWidths, false);
            }

            sb.Append("\\pard\\fs" + bodySize + "\\par ");
            return i;
        }

        private int GetTableWidthTwips()
        {
            const int fallbackWidthPx = 800;
            const int minWidthPx = 200;
            const int rightPaddingPx = 24; // keep table clear of the scrollbar/edge

            int dpi = richTextBox != null && richTextBox.DeviceDpi > 0 ? richTextBox.DeviceDpi : 96;
            int clientWidthPx = richTextBox != null && richTextBox.ClientSize.Width > 0
                ? richTextBox.ClientSize.Width
                : fallbackWidthPx;

            int usableWidthPx = Math.Max(minWidthPx, clientWidthPx - rightPaddingPx);
            return usableWidthPx * 1440 / dpi;
        }

        private static int[] CalculateColumnWidths(System.Collections.Generic.List<string> headerCells,
            System.Collections.Generic.List<System.Collections.Generic.List<string>> bodyRows, int columnCount, int tableWidth)
        {
            const int minColWidthTwips = 900;

            var maxLens = new int[columnCount];
            for (int c = 0; c < columnCount; c++)
            {
                maxLens[c] = c < headerCells.Count ? headerCells[c].Length : 0;
            }

            foreach (var row in bodyRows)
            {
                for (int c = 0; c < columnCount; c++)
                {
                    int len = c < row.Count ? row[c].Length : 0;
                    if (len > maxLens[c])
                        maxLens[c] = len;
                }
            }

            long totalWeight = 0;
            foreach (var len in maxLens)
                totalWeight += Math.Max(1, len);

            var colWidths = new int[columnCount];
            int assignedWidth = 0;
            for (int c = 0; c < columnCount; c++)
            {
                long weight = Math.Max(1, maxLens[c]);
                int width = (int)(tableWidth * weight / totalWeight);
                colWidths[c] = Math.Max(minColWidthTwips, width);
                assignedWidth += colWidths[c];
            }

            // If proportional widths leave the table narrower than the available editor
            // width, stretch the widest column so the table still spans the full width.
            if (assignedWidth < tableWidth)
            {
                int widestIndex = 0;
                for (int c = 1; c < columnCount; c++)
                {
                    if (maxLens[c] > maxLens[widestIndex])
                        widestIndex = c;
                }

                colWidths[widestIndex] += tableWidth - assignedWidth;
            }

            return colWidths;
        }

        private void AppendMarkdownTableRow(StringBuilder sb, System.Collections.Generic.List<string> cells,
            System.Collections.Generic.List<char> alignments, int columnCount, int[] colWidths, bool isHeader)
        {
            sb.Append("\\trowd\\trgaph108\\trleft-108");

            int cellRight = 0;
            for (int c = 0; c < columnCount; c++)
            {
                cellRight += colWidths[c];
                sb.Append("\\clbrdrt\\brdrs\\brdrw10\\clbrdrl\\brdrs\\brdrw10\\clbrdrb\\brdrs\\brdrw10\\clbrdrr\\brdrs\\brdrw10\\cellx" + cellRight);
            }

            for (int c = 0; c < columnCount; c++)
            {
                string cellText = c < cells.Count ? cells[c] : string.Empty;
                char align = c < alignments.Count ? alignments[c] : 'l';
                string alignCode = align switch
                {
                    'c' => "\\qc",
                    'r' => "\\qr",
                    _ => "\\ql"
                };

                sb.Append("\\pard\\intbl" + alignCode + " ");
                if (isHeader)
                    sb.Append("\\b ");

                sb.Append(ApplyInlineMarkdown(cellText));

                if (isHeader)
                    sb.Append("\\b0 ");

                sb.Append("\\cell");
            }

            sb.Append("\\row\n");
        }

        private static int ToRtfHalfPoints(int configuredSize, int fallbackSize)
        {
            int pointSize = configuredSize > 0 ? configuredSize : fallbackSize;
            return Math.Max(12, pointSize * 2);
        }

        private string ApplyInlineMarkdown(string text)
        {
            if (string.IsNullOrEmpty(text))
                return string.Empty;

            // Inline markdown: code, bold, italic, underline.
            var rx = new Regex("`([^`]+?)`|<code>(.+?)</code>|\\*\\*(.+?)\\*\\*|__(.+?)__|\\*(.+?)\\*|_(.+?)_|<u>(.+?)</u>", RegexOptions.Compiled | RegexOptions.IgnoreCase);

            var sb = new StringBuilder();
            int lastIndex = 0;
            foreach (Match m in rx.Matches(text))
            {
                if (m.Index > lastIndex)
                {
                    sb.Append(EscapeRtf(text.Substring(lastIndex, m.Index - lastIndex)));
                }

                string content = null;
                string prefix = null;
                string suffix = null;

                if (m.Groups[1].Success)
                {
                    // `code`
                    content = m.Groups[1].Value;
                    prefix = "\\f1 "; suffix = "\\f0 ";
                }
                else if (m.Groups[2].Success)
                {
                    // <code>code</code>
                    content = m.Groups[2].Value;
                    prefix = "\\f1 "; suffix = "\\f0 ";
                }
                else if (m.Groups[3].Success)
                {
                    // **bold**
                    content = m.Groups[3].Value;
                    prefix = "\\b "; suffix = "\\b0 ";
                }
                else if (m.Groups[4].Success)
                {
                    // __bold__
                    content = m.Groups[4].Value;
                    prefix = "\\b "; suffix = "\\b0 ";
                }
                else if (m.Groups[5].Success)
                {
                    // *italic*
                    content = m.Groups[5].Value;
                    prefix = "\\i "; suffix = "\\i0 ";
                }
                else if (m.Groups[6].Success)
                {
                    // _italic_
                    content = m.Groups[6].Value;
                    prefix = "\\i "; suffix = "\\i0 ";
                }
                else if (m.Groups[7].Success)
                {
                    // <u>underline</u>
                    content = m.Groups[7].Value;
                    prefix = "\\ul "; suffix = "\\ul0 ";
                }

                if (content != null)
                {
                    sb.Append(prefix);
                    sb.Append(EscapeRtf(content));
                    sb.Append(suffix);
                }

                lastIndex = m.Index + m.Length;
            }

            if (lastIndex < text.Length)
                sb.Append(EscapeRtf(text.Substring(lastIndex)));

            return sb.ToString();
        }

        private string RegexReplace(string input, string pattern, string replacementPrefix, string replacementSuffix)
        {
            return Regex.Replace(input, pattern, match => replacementPrefix + EscapeRtf(match.Groups[1].Value) + replacementSuffix);
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (keyData == Keys.F1)
            {
                ShowKeyboardHelp();
                return true;
            }
            else if (keyData == Keys.F4)
            {
                using (OpenFileDialog openDialog = new OpenFileDialog())
                {
                    openDialog.Filter = "Rich Text Format (*.rtf)|*.rtf|Text files (*.txt)|*.txt|All files (*.*)|*.*";
                    openDialog.FilterIndex = 1;

                    if (openDialog.ShowDialog() == DialogResult.OK)
                    {
                        LoadFile(openDialog.FileName);
                    }
                }
                return true;
            }
            else if (keyData == Keys.Escape)
            {
                Close();
                return true;
            }

            return base.ProcessCmdKey(ref msg, keyData);
        }

        private void ShowKeyboardHelp()
        {
            StringBuilder builder = new StringBuilder();
            builder.AppendLine("RTF Editor");
            builder.AppendLine();
            builder.AppendLine("F1 - Show this help");
            builder.AppendLine("F4 - Open file");
            builder.AppendLine("Ctrl+S - Save file");
            builder.AppendLine("Ctrl+Shift+S - Save file as");
            builder.AppendLine("Ctrl+T - Font and color");
            builder.AppendLine("Ctrl+B - Bold");
            builder.AppendLine("Ctrl+I - Italic");
            builder.AppendLine("Ctrl+U - Underline");
            builder.AppendLine("Ctrl++ / Ctrl+Wheel Up - Increase font size");
            builder.AppendLine("Ctrl+- / Ctrl+Wheel Down - Decrease font size");
            builder.AppendLine("Context menu -> Style -> Normal / H1..H6 / Code");
            builder.AppendLine("Esc - Close editor");
            MessageBox.Show(this, builder.ToString(), Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            markdownResizeTimer?.Stop();
            _savedSize = Size;
            _savedLocation = Location;
            base.OnFormClosing(e);
        }

        protected override void OnLoad(EventArgs e)
        {
            if (_savedSize.Width > 0 && _savedSize.Height > 0)
            {
                Size = _savedSize;
            }

            if (_savedLocation.X >= 0 && _savedLocation.Y >= 0)
            {
                Location = _savedLocation;
            }

            ApplyStatusBarSettings();
            UpdateStatusBar();
            base.OnLoad(e);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                markdownResizeTimer?.Dispose();
            }

            base.Dispose(disposing);
        }
    }
}
