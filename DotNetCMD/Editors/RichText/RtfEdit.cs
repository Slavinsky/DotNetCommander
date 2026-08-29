using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Forms;
using DotNetCommander.Properties;
using Timer = System.Windows.Forms.Timer;

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
        private Encoding currentTextEncoding = new UTF8Encoding(false);
        private Font ownedEditorFont;
        private static Point savedLocation = new Point(-1, -1);
        private static Size savedSize = new Size(0, 0);

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
            ownedEditorFont = CreateEditorFont();
            richTextBox.Font = ownedEditorFont;
            richTextBox.KeyDown += RichTextBox_KeyDown;
            richTextBox.MouseWheel += RichTextBox_MouseWheel;
            richTextBox.SelectionChanged += (_, __) => UpdateStatusBar();
            richTextBox.TextChanged += (_, __) => UpdateStatusBar();
            richTextBox.LinkClicked += RichTextBox_LinkClicked;
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
            Text = DotNetCommander.Language.getString("rtfEditorTitle");
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
            Font selectionFont = richTextBox.SelectionFont;
            Font currentFont = selectionFont ?? richTextBox.Font;

            try
            {
                FontStyle newStyle = (currentFont.Style & style) == style
                    ? currentFont.Style & ~style
                    : currentFont.Style | style;
                ApplyFontToSelection(new Font(currentFont, newStyle));
            }
            finally
            {
                selectionFont?.Dispose();
            }
        }

        private void ChangeFontSize(float delta)
        {
            Font selectionFont = richTextBox.SelectionFont;
            Font currentFont = selectionFont ?? richTextBox.Font;
            try
            {
                float newSize = Math.Max(6f, Math.Min(96f, currentFont.Size + delta));
                if (Math.Abs(newSize - currentFont.Size) < 0.1f)
                    return;

                ApplyFontToSelection(new Font(currentFont.FontFamily, newSize, currentFont.Style));
                UpdateStatusBar();
            }
            finally
            {
                selectionFont?.Dispose();
            }
        }

        private void ApplyFontToSelection(Font font, bool persistAsDefault = true)
        {
            try
            {
                richTextBox.SelectionFont = font;
                if (richTextBox.SelectionLength == 0)
                {
                    ReplaceEditorFont(new Font(font.FontFamily, font.Size, font.Style));
                }

                if (persistAsDefault && richTextBox.SelectionLength == 0)
                {
                    SaveEditorAppearance(font);
                }
            }
            finally
            {
                font.Dispose();
            }

            UpdateStatusBar();
        }

        private void ReplaceEditorFont(Font font)
        {
            Font previous = ownedEditorFont;
            ownedEditorFont = font;
            richTextBox.Font = font;
            previous?.Dispose();
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
            catch (Exception ex)
            {
                DotNetCommander.LogService.LogException("RtfEdit.CreateEditorFont", ex);
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
            Font selectionFont = richTextBox.SelectionFont;
            Font sourceFont = selectionFont ?? richTextBox.Font;
            dialog.Font = new Font(sourceFont, sourceFont.Style);
            dialog.Color = richTextBox.SelectionColor.IsEmpty ? richTextBox.ForeColor : richTextBox.SelectionColor;
            selectionFont?.Dispose();

            if (dialog.ShowDialog(this) != DialogResult.OK)
                return;

            richTextBox.SelectionFont = dialog.Font;
            richTextBox.SelectionColor = dialog.Color;
            if (richTextBox.SelectionLength == 0)
            {
                ReplaceEditorFont(new Font(dialog.Font, dialog.Font.Style));
                richTextBox.ForeColor = dialog.Color;
                SaveEditorAppearance(dialog.Font);
            }

            UpdateStatusBar();
        }

        private void RichTextBox_LinkClicked(object sender, LinkClickedEventArgs e)
        {
            try
            {
                Process.Start(new ProcessStartInfo(e.LinkText) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                DotNetCommander.LogService.LogException("RtfEdit.OpenLink", ex);
            }
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
                    "Undo" => DotNetCommander.Language.getString("rtfUndo"),
                    "Redo" => DotNetCommander.Language.getString("rtfRedo"),
                    "Cut" => DotNetCommander.Language.getString("rtfCut"),
                    "Copy" => DotNetCommander.Language.getString("rtfCopy"),
                    "Paste" => DotNetCommander.Language.getString("rtfPaste"),
                    "Delete" => DotNetCommander.Language.getString("rtfDelete"),
                    "SelectAll" => DotNetCommander.Language.getString("rtfSelectAll"),
                    "Style" => DotNetCommander.Language.getString("rtfStyle"),
                    "Bold" => DotNetCommander.Language.getString("rtfBold"),
                    "Italic" => DotNetCommander.Language.getString("rtfItalic"),
                    "Underline" => DotNetCommander.Language.getString("rtfUnderline"),
                    "Font" => DotNetCommander.Language.getString("rtfFont"),
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
            string displayName = styleName switch
            {
                "Normal" => DotNetCommander.Language.getString("rtfStyleNormal"),
                "Code" => DotNetCommander.Language.getString("rtfStyleCode"),
                _ => styleName
            };
            ToolStripMenuItem item = new ToolStripMenuItem(displayName)
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
            catch (Exception ex)
            {
                DotNetCommander.LogService.LogException("RtfEdit.ApplyMarkdownStyle", ex);
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

            statusStatsLabel.Text = string.Format(
                DotNetCommander.Language.getString("rtfStatusFormat"),
                lineCount,
                textLength,
                selectionLength,
                caretLine,
                caretColumn);
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
                currentTextEncoding = new UTF8Encoding(false);
                UpdateWindowTitle();

                string extension = Path.GetExtension(filePath)?.ToLowerInvariant() ?? string.Empty;
                if (previewMode && string.Equals(extension, ".md", StringComparison.OrdinalIgnoreCase))
                {
                    markdownPreviewSource = DotNetCommander.TextFileEncodingService.ReadAllText(filePath, out currentTextEncoding);
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
                    string content = DotNetCommander.TextFileEncodingService.ReadAllText(filePath, out currentTextEncoding);
                    richTextBox.Text = content;
                }

                richTextBox.ReadOnly = previewMode;
                ShowStatusMessage(previewMode ? DotNetCommander.Language.getString("rtfPreviewMode") : string.Empty);
                MoveCaretToDocumentStart();
                UpdateStatusBar();
            }
            catch (Exception ex)
            {
                DotNetCommander.LogService.LogException("RtfEdit.LoadFile", ex);
                MessageBox.Show(string.Format(DotNetCommander.Language.getString("rtfLoadErrorFormat"), ex.Message),
                    DotNetCommander.Language.getString("error"),
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
            MarkdownRenderOptions options = CaptureMarkdownRenderOptions(Path.GetDirectoryName(currentFilePath));
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

        internal static MarkdownRenderOptions CaptureMarkdownRenderOptions(string baseDirectory = null)
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
                H6FontSize = Settings.Default.MarkdownPreviewH6FontSize,
                BaseDirectory = baseDirectory,
                ImageLabel = DotNetCommander.Language.getString("rtfMarkdownImage")
            };
        }

        internal sealed class MarkdownRenderOptions
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
            public string BaseDirectory { get; init; }
            public string ImageLabel { get; init; }
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

            string targetPath = currentFilePath;
            if (string.IsNullOrEmpty(targetPath) && !TryPromptForSavePath(currentFilePath, out targetPath))
            {
                return;
            }

            if (TryWriteContentToDisk(targetPath, "RtfEdit.SaveFile"))
            {
                currentFilePath = targetPath;
                UpdateWindowTitle();
                NotifySaveSuccess(targetPath);
            }
        }

        public void SaveFileAs()
        {
            if (!TryPromptForSavePath(currentFilePath, out string targetPath))
                return;

            if (TryWriteContentToDisk(targetPath, "RtfEdit.SaveFileAs"))
            {
                if (!previewMode)
                {
                    currentFilePath = targetPath;
                    UpdateWindowTitle();
                }

                NotifySaveSuccess(targetPath);
            }
        }

        private bool TryPromptForSavePath(string initialPath, out string selectedPath)
        {
            selectedPath = null;
            using SaveFileDialog saveDialog = new SaveFileDialog();
            saveDialog.Filter = DotNetCommander.Language.getString("rtfFileDialogFilter");
            saveDialog.FilterIndex = GetRtfSaveFilterIndex(initialPath);

            if (!string.IsNullOrWhiteSpace(initialPath))
            {
                saveDialog.FileName = Path.GetFileName(initialPath);
                string initialDirectory = Path.GetDirectoryName(initialPath);
                if (!string.IsNullOrWhiteSpace(initialDirectory) && Directory.Exists(initialDirectory))
                {
                    saveDialog.InitialDirectory = initialDirectory;
                }
            }

            if (saveDialog.ShowDialog(this) != DialogResult.OK)
                return false;

            selectedPath = saveDialog.FileName;
            return true;
        }

        private bool TryWriteContentToDisk(string path, string logContext)
        {
            try
            {
                string extension = Path.GetExtension(path)?.ToLowerInvariant() ?? string.Empty;
                if (extension == ".rtf")
                    richTextBox.SaveFile(path, RichTextBoxStreamType.RichText);
                else
                    DotNetCommander.TextFileEncodingService.WriteAllText(path, richTextBox.Text, currentTextEncoding);

                return true;
            }
            catch (Exception ex)
            {
                DotNetCommander.LogService.LogException(logContext, ex);
                ShowStatusMessage(DotNetCommander.Language.getString("rtfSaveFailed"));
                MessageBox.Show(string.Format(DotNetCommander.Language.getString("rtfSaveErrorFormat"), ex.Message),
                    DotNetCommander.Language.getString("error"),
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                return false;
            }
        }

        private static int GetRtfSaveFilterIndex(string filePath)
        {
            string extension = Path.GetExtension(filePath)?.ToLowerInvariant();
            return extension == ".txt" ? 2 : 1;
        }

        private void NotifySaveSuccess(string path)
        {
            string message = string.Format(DotNetCommander.Language.getString("rtfSavedFormat"), Path.GetFileName(path));
            if (Settings.Default.ShowEditorStatusBar)
            {
                ShowStatusMessage(message);
                UpdateStatusBar();
                return;
            }

            MessageBox.Show(DotNetCommander.Language.getString("rtfSaveSuccess"),
                DotNetCommander.Language.getString("rtfSuccessTitle"),
                MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void NotifySaveBlocked()
        {
            string message = DotNetCommander.Language.getString("rtfPreviewReadOnly");
            if (Settings.Default.ShowEditorStatusBar)
            {
                ShowStatusMessage(message);
                return;
            }

            MessageBox.Show(message, DotNetCommander.Language.getString("Info"),
                MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void UpdateWindowTitle()
        {
            if (string.IsNullOrEmpty(currentFilePath))
                Text = DotNetCommander.Language.getString("rtfEditorTitle");
            else
                Text = string.Format(
                    DotNetCommander.Language.getString(previewMode ? "rtfPreviewTitleFormat" : "rtfEditorTitleFormat"),
                    Path.GetFileName(currentFilePath));
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
                string content = DotNetCommander.TextFileEncodingService.ReadAllText(filePath, out currentTextEncoding);
                if (string.IsNullOrWhiteSpace(content))
                {
                    richTextBox.Rtf = EmptyDocumentRtf;
                    return;
                }

                throw;
            }
        }

        private static string EscapeRtf(string text)
        {
            if (string.IsNullOrEmpty(text))
                return string.Empty;

            StringBuilder sb = new StringBuilder();
            for (int index = 0; index < text.Length; index++)
            {
                char c = text[index];
                if (c == '\\') sb.Append("\\\\");
                else if (c == '{') sb.Append("\\{");
                else if (c == '}') sb.Append("\\}");
                else if (c == '\n') sb.Append("\\par ");
                else if (c <= 0x7f)
                    sb.Append(c);
                else if (char.IsHighSurrogate(c))
                {
                    if (index + 1 < text.Length && char.IsLowSurrogate(text[index + 1]))
                    {
                        AppendRtfUnicodeCodeUnit(sb, c);
                        AppendRtfUnicodeCodeUnit(sb, text[++index]);
                    }
                    else
                    {
                        AppendRtfUnicodeCodeUnit(sb, '\uFFFD');
                    }
                }
                else if (char.IsLowSurrogate(c))
                    AppendRtfUnicodeCodeUnit(sb, '\uFFFD');
                else
                    AppendRtfUnicodeCodeUnit(sb, c);
            }

            return sb.ToString();
        }

        private static void AppendRtfUnicodeCodeUnit(StringBuilder sb, char value)
        {
            sb.Append("\\u");
            sb.Append(unchecked((short)value));
            sb.Append('?');
        }

        internal static string ConvertMarkdownToRtf(string markdown, int tableWidth, MarkdownRenderOptions options)
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

            var lines = (markdown ?? string.Empty).Replace("\r\n", "\n").Replace("\r", "\n").Split('\n');
            int lineIndex = 0;
            bool inCodeBlock = false;
            while (lineIndex < lines.Length)
            {
                string line = lines[lineIndex].TrimEnd();

                if (line.TrimStart().StartsWith("```", StringComparison.Ordinal))
                {
                    inCodeBlock = !inCodeBlock;
                    sb.Append(inCodeBlock ? "\\pard\\li360\\f1 " : "\\pard\\li0\\f0\\par ");
                    lineIndex++;
                    continue;
                }

                if (inCodeBlock)
                {
                    sb.Append(EscapeRtf(line));
                    sb.Append("\\line ");
                    lineIndex++;
                    continue;
                }

                if (IsTableRow(line) && lineIndex + 1 < lines.Length && IsTableSeparatorRow(lines[lineIndex + 1]))
                {
                    lineIndex = AppendMarkdownTable(sb, lines, lineIndex, bodySize, tableWidth, options);
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
                    sb.Append("\\fs" + ToRtfHalfPoints(options.H6FontSize, 9) + "\\b " + ApplyInlineMarkdown(line.Substring(7), options, tableWidth) + "\\b0\\fs" + bodySize + "\\par ");
                else if (line.StartsWith("##### "))
                    sb.Append("\\fs" + ToRtfHalfPoints(options.H5FontSize, 10) + "\\b " + ApplyInlineMarkdown(line.Substring(6), options, tableWidth) + "\\b0\\fs" + bodySize + "\\par ");
                else if (line.StartsWith("#### "))
                    sb.Append("\\fs" + ToRtfHalfPoints(options.H4FontSize, 11) + "\\b " + ApplyInlineMarkdown(line.Substring(5), options, tableWidth) + "\\b0\\fs" + bodySize + "\\par ");
                else if (line.StartsWith("### "))
                    sb.Append("\\fs" + ToRtfHalfPoints(options.H3FontSize, 12) + "\\b " + ApplyInlineMarkdown(line.Substring(4), options, tableWidth) + "\\b0\\fs" + bodySize + "\\par ");
                else if (line.StartsWith("## "))
                    sb.Append("\\fs" + ToRtfHalfPoints(options.H2FontSize, 13) + "\\b " + ApplyInlineMarkdown(line.Substring(3), options, tableWidth) + "\\b0\\fs" + bodySize + "\\par ");
                else if (line.StartsWith("# "))
                    sb.Append("\\fs" + ToRtfHalfPoints(options.H1FontSize, 14) + "\\b " + ApplyInlineMarkdown(line.Substring(2), options, tableWidth) + "\\b0\\fs" + bodySize + "\\par ");
                else if (TryAppendMarkdownQuote(sb, line, options, tableWidth, bodySize))
                {
                }
                else if (TryAppendMarkdownListItem(sb, line, options, tableWidth, bodySize))
                {
                }
                else
                    sb.Append(ApplyInlineMarkdown(line, options, tableWidth) + "\\par ");

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
        private static readonly Regex UnorderedListRegex = new Regex(
            @"^(?<indent>\s*)[-+*]\s+(?<text>.+)$",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);
        private static readonly Regex OrderedListRegex = new Regex(
            @"^(?<indent>\s*)(?<number>\d+)[.)]\s+(?<text>.+)$",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);

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

        private static int AppendMarkdownTable(StringBuilder sb, string[] lines, int startIndex, int bodySize,
            int tableWidth, MarkdownRenderOptions options)
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

            AppendMarkdownTableRow(sb, headerCells, alignments, columnCount, colWidths, true, options, tableWidth);
            foreach (var row in bodyRows)
            {
                AppendMarkdownTableRow(sb, row, alignments, columnCount, colWidths, false, options, tableWidth);
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

        private static void AppendMarkdownTableRow(StringBuilder sb, System.Collections.Generic.List<string> cells,
            System.Collections.Generic.List<char> alignments, int columnCount, int[] colWidths, bool isHeader,
            MarkdownRenderOptions options, int tableWidth)
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

                sb.Append(ApplyInlineMarkdown(cellText, options, tableWidth));

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

        private static bool TryAppendMarkdownQuote(StringBuilder sb, string line, MarkdownRenderOptions options,
            int tableWidth, int bodySize)
        {
            int index = 0;
            int depth = 0;
            while (index < line.Length)
            {
                while (index < line.Length && char.IsWhiteSpace(line[index]))
                    index++;
                if (index >= line.Length || line[index] != '>')
                    break;
                depth++;
                index++;
                if (index < line.Length && line[index] == ' ')
                    index++;
            }

            if (depth == 0)
                return false;

            int indent = Math.Min(6, depth) * 360;
            sb.Append("\\pard\\li");
            sb.Append(indent);
            sb.Append("\\brdrl\\brdrs\\brdrw15\\brsp120\\i ");
            sb.Append(ApplyInlineMarkdown(line.Substring(index), options, tableWidth));
            sb.Append("\\i0\\par\\pard\\li0\\fs");
            sb.Append(bodySize);
            sb.Append(' ');
            return true;
        }

        private static bool TryAppendMarkdownListItem(StringBuilder sb, string line, MarkdownRenderOptions options,
            int tableWidth, int bodySize)
        {
            Match match = OrderedListRegex.Match(line);
            bool ordered = match.Success;
            if (!ordered)
                match = UnorderedListRegex.Match(line);
            if (!match.Success)
                return false;

            int indentLevel = Math.Min(8, match.Groups["indent"].Value.Replace("\t", "    ").Length / 2);
            int indent = 360 + indentLevel * 360;
            sb.Append("\\pard\\li");
            sb.Append(indent);
            sb.Append("\\fi-240 ");
            if (ordered)
            {
                sb.Append(EscapeRtf(match.Groups["number"].Value));
                sb.Append(".\\tab ");
            }
            else
            {
                sb.Append("{\\f1\\'B7}\\tab ");
            }

            sb.Append(ApplyInlineMarkdown(match.Groups["text"].Value, options, tableWidth));
            sb.Append("\\par\\pard\\li0\\fi0\\fs");
            sb.Append(bodySize);
            sb.Append(' ');
            return true;
        }

        private static string ApplyInlineMarkdown(string text, MarkdownRenderOptions options, int tableWidth)
        {
            if (string.IsNullOrEmpty(text))
                return string.Empty;

            var sb = new StringBuilder(text.Length + 32);
            AppendInlineMarkdown(sb, text, options, tableWidth, 0);
            return sb.ToString();
        }

        private static void AppendInlineMarkdown(StringBuilder sb, string text, MarkdownRenderOptions options,
            int tableWidth, int depth)
        {
            if (depth > 16)
            {
                sb.Append(EscapeRtf(text));
                return;
            }

            int index = 0;
            while (index < text.Length)
            {
                if (text[index] == '\\' && index + 1 < text.Length)
                {
                    sb.Append(EscapeRtf(text.Substring(index + 1, 1)));
                    index += 2;
                    continue;
                }

                if (TryParseMarkdownLink(text, index, true, out string imageAlt, out string imageTarget, out int imageEnd))
                {
                    if (!TryAppendMarkdownImage(sb, imageAlt, imageTarget, options, tableWidth))
                        AppendMarkdownImageFallback(sb, imageAlt, imageTarget, options, tableWidth, depth);
                    index = imageEnd;
                    continue;
                }

                if (TryParseMarkdownLink(text, index, false, out string linkText, out string linkTarget, out int linkEnd))
                {
                    AppendMarkdownLink(sb, linkText, linkTarget, options, tableWidth, depth);
                    index = linkEnd;
                    continue;
                }

                if (TryAppendDelimitedInline(sb, text, ref index, "`", "`", "{\\f1 ", "}",
                    options, tableWidth, depth, parseNested: false) ||
                    TryAppendDelimitedInline(sb, text, ref index, "<code>", "</code>", "{\\f1 ", "}",
                        options, tableWidth, depth, parseNested: false, ignoreCase: true) ||
                    TryAppendDelimitedInline(sb, text, ref index, "**", "**", "{\\b ", "}",
                        options, tableWidth, depth, parseNested: true) ||
                    TryAppendDelimitedInline(sb, text, ref index, "__", "__", "{\\b ", "}",
                        options, tableWidth, depth, parseNested: true) ||
                    TryAppendDelimitedInline(sb, text, ref index, "~~", "~~", "{\\strike ", "}",
                        options, tableWidth, depth, parseNested: true) ||
                    TryAppendDelimitedInline(sb, text, ref index, "<u>", "</u>", "{\\ul ", "}",
                        options, tableWidth, depth, parseNested: true, ignoreCase: true) ||
                    TryAppendDelimitedInline(sb, text, ref index, "*", "*", "{\\i ", "}",
                        options, tableWidth, depth, parseNested: true) ||
                    TryAppendDelimitedInline(sb, text, ref index, "_", "_", "{\\i ", "}",
                        options, tableWidth, depth, parseNested: true))
                {
                    continue;
                }

                int literalLength = char.IsHighSurrogate(text[index]) &&
                    index + 1 < text.Length &&
                    char.IsLowSurrogate(text[index + 1])
                        ? 2
                        : 1;
                sb.Append(EscapeRtf(text.Substring(index, literalLength)));
                index += literalLength;
            }
        }

        private static bool TryAppendDelimitedInline(StringBuilder sb, string text, ref int index,
            string opening, string closing, string rtfPrefix, string rtfSuffix, MarkdownRenderOptions options,
            int tableWidth, int depth, bool parseNested, bool ignoreCase = false)
        {
            StringComparison comparison = ignoreCase ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
            if (index + opening.Length > text.Length ||
                !text.AsSpan(index, opening.Length).Equals(opening.AsSpan(), comparison))
                return false;

            int contentStart = index + opening.Length;
            int closingIndex = text.IndexOf(closing, contentStart, comparison);
            if (closingIndex < contentStart || closingIndex == contentStart)
                return false;

            string content = text.Substring(contentStart, closingIndex - contentStart);
            sb.Append(rtfPrefix);
            if (parseNested)
                AppendInlineMarkdown(sb, content, options, tableWidth, depth + 1);
            else
                sb.Append(EscapeRtf(content));
            sb.Append(rtfSuffix);
            index = closingIndex + closing.Length;
            return true;
        }

        private static bool TryParseMarkdownLink(string text, int index, bool image, out string label,
            out string target, out int endIndex)
        {
            label = null;
            target = null;
            endIndex = index;
            string opening = image ? "![" : "[";
            if (index + opening.Length > text.Length ||
                !text.AsSpan(index, opening.Length).SequenceEqual(opening.AsSpan()))
                return false;

            int labelStart = index + opening.Length;
            int labelEnd = text.IndexOf("](", labelStart, StringComparison.Ordinal);
            if (labelEnd < labelStart)
                return false;

            int targetEnd = text.IndexOf(')', labelEnd + 2);
            if (targetEnd < 0)
                return false;

            label = text.Substring(labelStart, labelEnd - labelStart);
            target = text.Substring(labelEnd + 2, targetEnd - labelEnd - 2).Trim();
            if (target.Length == 0)
                return false;

            endIndex = targetEnd + 1;
            return true;
        }

        private static void AppendMarkdownLink(StringBuilder sb, string label, string target,
            MarkdownRenderOptions options, int tableWidth, int depth)
        {
            sb.Append("{\\ul ");
            AppendInlineMarkdown(sb, label, options, tableWidth, depth + 1);
            sb.Append("} (");
            sb.Append(EscapeRtf(target));
            sb.Append(')');
        }

        private static void AppendMarkdownImageFallback(StringBuilder sb, string alt, string target,
            MarkdownRenderOptions options, int tableWidth, int depth)
        {
            sb.Append("{\\i [");
            sb.Append(EscapeRtf(string.IsNullOrWhiteSpace(options.ImageLabel) ? "Image" : options.ImageLabel));
            if (!string.IsNullOrWhiteSpace(alt))
            {
                sb.Append(": ");
                AppendInlineMarkdown(sb, alt, options, tableWidth, depth + 1);
            }
            sb.Append("]} ");
            AppendMarkdownLink(sb, target, target, options, tableWidth, depth + 1);
        }

        private static bool TryAppendMarkdownImage(StringBuilder sb, string alt, string target,
            MarkdownRenderOptions options, int tableWidth)
        {
            const long maxSourceBytes = 8L * 1024 * 1024;
            try
            {
                if (string.IsNullOrWhiteSpace(options.BaseDirectory))
                    return false;

                string source = target.Trim().Trim('<', '>');
                string imagePath;
                if (Uri.TryCreate(source, UriKind.Absolute, out Uri uri))
                {
                    if (!uri.IsFile)
                        return false;
                    imagePath = uri.LocalPath;
                }
                else
                {
                    source = Uri.UnescapeDataString(source.Replace('/', Path.DirectorySeparatorChar));
                    imagePath = Path.GetFullPath(Path.Combine(options.BaseDirectory, source));
                }

                var fileInfo = new FileInfo(imagePath);
                if (!fileInfo.Exists || fileInfo.Length <= 0 || fileInfo.Length > maxSourceBytes)
                    return false;

                using Image image = Image.FromFile(imagePath);
                if (image.Width <= 0 || image.Height <= 0)
                    return false;

                using var stream = new MemoryStream();
                image.Save(stream, ImageFormat.Png);
                if (stream.Length > maxSourceBytes * 2)
                    return false;

                int widthGoal = Math.Max(720, tableWidth);
                int naturalWidth = (int)Math.Round(image.Width * 1440d / Math.Max(1f, image.HorizontalResolution));
                int naturalHeight = (int)Math.Round(image.Height * 1440d / Math.Max(1f, image.VerticalResolution));
                double scale = naturalWidth > widthGoal ? (double)widthGoal / naturalWidth : 1d;
                int heightGoal = Math.Max(1, (int)Math.Round(naturalHeight * scale));

                sb.Append("{\\pict\\pngblip\\picw");
                sb.Append(image.Width);
                sb.Append("\\pich");
                sb.Append(image.Height);
                sb.Append("\\picwgoal");
                sb.Append(Math.Max(1, (int)Math.Round(naturalWidth * scale)));
                sb.Append("\\pichgoal");
                sb.Append(heightGoal);
                foreach (byte value in stream.ToArray())
                    sb.Append(value.ToString("x2"));
                sb.Append('}');
                return true;
            }
            catch (Exception ex)
            {
                DotNetCommander.LogService.LogException("RtfEdit.MarkdownImage", ex);
                return false;
            }
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
                    openDialog.Filter = DotNetCommander.Language.getString("rtfFileDialogFilter");
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
            var items = new[]
            {
                new DotNetCommander.KeyboardHelpItem("F1", DotNetCommander.Language.getString("rtfHelpShowHelp")),
                new DotNetCommander.KeyboardHelpItem("F4", DotNetCommander.Language.getString("rtfHelpOpenFile")),
                new DotNetCommander.KeyboardHelpItem("Ctrl+S", DotNetCommander.Language.getString("rtfHelpSave")),
                new DotNetCommander.KeyboardHelpItem("Ctrl+Shift+S", DotNetCommander.Language.getString("rtfHelpSaveAs")),
                new DotNetCommander.KeyboardHelpItem("Ctrl+T", DotNetCommander.Language.getString("rtfHelpFontColor")),
                new DotNetCommander.KeyboardHelpItem("Ctrl+B", DotNetCommander.Language.getString("rtfHelpBold")),
                new DotNetCommander.KeyboardHelpItem("Ctrl+I", DotNetCommander.Language.getString("rtfHelpItalic")),
                new DotNetCommander.KeyboardHelpItem("Ctrl+U", DotNetCommander.Language.getString("rtfHelpUnderline")),
                new DotNetCommander.KeyboardHelpItem("Ctrl++ / Ctrl+Wheel Up", DotNetCommander.Language.getString("rtfHelpIncreaseFont")),
                new DotNetCommander.KeyboardHelpItem("Ctrl+- / Ctrl+Wheel Down", DotNetCommander.Language.getString("rtfHelpDecreaseFont")),
                new DotNetCommander.KeyboardHelpItem(DotNetCommander.Language.getString("rtfHelpStyleShortcut"), DotNetCommander.Language.getString("rtfHelpStyleAction")),
                new DotNetCommander.KeyboardHelpItem("Esc", DotNetCommander.Language.getString("rtfHelpClose"))
            };
            using var helpForm = new DotNetCommander.FormKeyboardHelp(
                DotNetCommander.Language.getString("rtfEditorTitle"),
                DotNetCommander.Language.getString("rtfHelpSubtitle"),
                items);
            helpForm.ShowDialog(this);
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            markdownResizeTimer?.Stop();
            savedSize = Size;
            savedLocation = Location;
            base.OnFormClosing(e);
        }

        protected override void OnLoad(EventArgs e)
        {
            if (savedSize.Width > 0 && savedSize.Height > 0)
            {
                Size = savedSize;
            }

            if (savedLocation.X >= 0 && savedLocation.Y >= 0)
            {
                Location = savedLocation;
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
                editorContextMenu?.Dispose();
                ownedEditorFont?.Dispose();
                ownedEditorFont = null;
            }

            base.Dispose(disposing);
        }
    }
}
