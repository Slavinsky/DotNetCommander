using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Text;
using System.Windows.Forms;
using System.IO;
using DotNetCommander.Properties;

namespace View
{
    public partial class TextEdit : Form
    {
        private TextBox textBox;
        private StatusStrip statusStrip;
        private ToolStripStatusLabel statusMessageLabel;
        private ToolStripStatusLabel statusStatsLabel;
        private string currentFilePath;
        private bool previewMode;
        private static Point _savedLocation = new Point(-1, -1); // Позиция формы
        private static Size _savedSize = new Size(0, 0); // Размер формы
        
        public TextEdit()
        {
            InitializeComponent();
            InitializeStatusBar();
            InitializeTextBox();
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

        private void InitializeTextBox()
        {
            textBox = new TextBox();
            textBox.Multiline = true;
            textBox.Font = CreateEditorFont();
            textBox.Dock = DockStyle.Fill;
            textBox.KeyDown += TextBox_KeyDown;
            textBox.KeyUp += (_, __) => UpdateStatusBar();
            textBox.MouseWheel += TextBox_MouseWheel;
            textBox.MouseUp += (_, __) => UpdateStatusBar();
            textBox.TextChanged += (_, __) => UpdateStatusBar();
            ApplyWordWrapState();
            
            this.Controls.Add(textBox);
            ApplyStatusBarSettings();
            UpdateStatusBar();
        }

    private void InitializeComponent() {
      SuspendLayout();
      // 
      // TextEdit
      // 
      AutoScaleDimensions = new SizeF(10F, 25F);
      AutoScaleMode = AutoScaleMode.Font;
      ClientSize = new Size(800, 900);
      KeyPreview = true;
      Margin = new Padding(2, 5, 2, 5);
      Name = "TextEdit";
      Text = "Text Editor";
      ResumeLayout(false);
    }

        private void TextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Control && e.Shift && e.KeyCode == Keys.S)
            {
                SaveFileAs();
                e.Handled = true;
            }
            // Обработка клавиши Ctrl+S для сохранения
            else if (e.Control && e.KeyCode == Keys.S)
            {
                SaveFile();
                e.Handled = true;
            }
            // Обработка клавиш Ctrl+B, Ctrl+I, Ctrl+U для Markdown форматирования
            else if (e.Control && e.KeyCode == Keys.B)
            {
                InsertMarkdownFormatting("**", "**");
                e.Handled = true;
            }
            else if (e.Control && e.KeyCode == Keys.I)
            {
                InsertMarkdownFormatting("_", "_");
                e.Handled = true;
            }
            else if (e.Control && e.KeyCode == Keys.U)
            {
                InsertMarkdownFormatting("<u>", "</u>");
                e.Handled = true;
            }
            // Обработка клавиш Ctrl+ и Ctrl-
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
            else if (e.Control && e.KeyCode == Keys.W)
            {
                ToggleWordWrap();
                e.Handled = true;
            }
            else if (e.Control && e.Shift && e.KeyCode == Keys.F)
            {
                ShowFontDialog();
                e.Handled = true;
            }
        }

        private void TextBox_MouseWheel(object sender, MouseEventArgs e)
        {
            // Обработка колесика мыши с Ctrl
            if (ModifierKeys == Keys.Control)
            {
                if (e.Delta > 0)
                {
                    ChangeFontSize(1f); // Увеличить шрифт
                }
                else
                {
                    ChangeFontSize(-1f); // Уменьшить шрифт
                }
            }
        }

        private void ChangeFontSize(float delta)
        {
            Font oldFont = textBox.Font;
            float newFontSize = Math.Max(6f, Math.Min(72f, oldFont.Size + delta));
            
            if (Math.Abs(newFontSize - oldFont.Size) > 0.1f)
            {
                textBox.Font = new Font(oldFont.Name, newFontSize, oldFont.Style);
                SaveEditorAppearance();
            }
        }

        private void ToggleWordWrap()
        {
            Settings.Default.TextEditorWordWrap = !Settings.Default.TextEditorWordWrap;
            ApplyWordWrapState();
            Settings.Default.Save();
        }

        private void ApplyWordWrapState()
        {
            if (textBox == null)
                return;

            bool wordWrapEnabled = Settings.Default.TextEditorWordWrap;
            textBox.WordWrap = wordWrapEnabled;
            textBox.ScrollBars = wordWrapEnabled ? ScrollBars.Vertical : ScrollBars.Both;
        }

        private void ShowFontDialog()
        {
            using FontDialog dialog = new FontDialog();
            dialog.Font = textBox.Font;
            dialog.ShowColor = false;

            if (dialog.ShowDialog(this) == DialogResult.OK)
            {
                textBox.Font = dialog.Font;
                SaveEditorAppearance();
                UpdateStatusBar();
            }
        }

        private static Font CreateEditorFont()
        {
            string fontName = string.IsNullOrWhiteSpace(Settings.Default.TextEditorFontName)
                ? "Consolas"
                : Settings.Default.TextEditorFontName;
            float fontSize = Math.Max(6f, Settings.Default.TextEditorFontSize);

            try
            {
                return new Font(fontName, fontSize);
            }
            catch
            {
                return new Font("Consolas", 12f);
            }
        }

        private void SaveEditorAppearance()
        {
            Settings.Default.TextEditorFontName = textBox.Font.FontFamily.Name;
            Settings.Default.TextEditorFontSize = textBox.Font.Size;
            Settings.Default.Save();
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
            if (statusStatsLabel == null || textBox == null)
                return;

            int textLength = textBox.TextLength;
            int lineCount = textBox.Lines.Length;
            if (lineCount == 0)
                lineCount = 1;

            int selectionLength = textBox.SelectionLength;
            int caretIndex = textBox.SelectionStart;
            int caretLine = textBox.GetLineFromCharIndex(caretIndex) + 1;
            int lineStart = textBox.GetFirstCharIndexFromLine(Math.Max(0, caretLine - 1));
            int caretColumn = Math.Max(1, caretIndex - lineStart + 1);

            statusStatsLabel.Text = $"Lines: {lineCount}   Chars: {textLength:0,0}   Sel: {selectionLength:0,0}   Ln {caretLine}, Col {caretColumn}";
        }

        private void ShowKeyboardHelp()
        {
            StringBuilder builder = new StringBuilder();
            builder.AppendLine("Text Editor");
            builder.AppendLine();
            builder.AppendLine("F1 - Show this help");
            builder.AppendLine("Ctrl+S - Save file");
            builder.AppendLine("Ctrl+Shift+S - Save file as");
            builder.AppendLine("Ctrl+O - Open text file");
            builder.AppendLine("Ctrl+W - Toggle word wrap");
            builder.AppendLine("Ctrl+Shift+F - Choose font");
            builder.AppendLine("Ctrl++ / Ctrl+Wheel Up - Increase font size");
            builder.AppendLine("Ctrl+- / Ctrl+Wheel Down - Decrease font size");

            if (!previewMode)
            {
                builder.AppendLine("Ctrl+B - Bold markdown selection");
                builder.AppendLine("Ctrl+I - Italic markdown selection");
                builder.AppendLine("Ctrl+U - Underline markdown selection");
            }

            builder.AppendLine("Esc - Close editor");
            MessageBox.Show(this, builder.ToString(), Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void InsertMarkdownFormatting(string prefix, string suffix)
        {
            int selectionStart = textBox.SelectionStart;
            int selectionLength = textBox.SelectionLength;
            
            if (selectionLength > 0)
            {
                // Якщо вибраний текст - обгортаємо його форматуванням
                string selectedText = textBox.Text.Substring(selectionStart, selectionLength);
                string newText = prefix + selectedText + suffix;
                
                textBox.Text = textBox.Text.Remove(selectionStart, selectionLength).Insert(selectionStart, newText);
                // Встановлюємо нову позицію курсора після форматування
                textBox.SelectionStart = selectionStart + newText.Length;
            }
            else
            {
                // Якщо текст не вибраний - вставляємо placeholder
                string placeholder = prefix + "текст" + suffix;
                textBox.Text = textBox.Text.Insert(selectionStart, placeholder);
                // Встановлюємо курсор всередині placeholder'а
                textBox.SelectionStart = selectionStart + prefix.Length;
            }
            textBox.SelectionLength = 0;
        }

        public void LoadFile(string filePath, bool preview = false)
        {
            try
            {
                currentFilePath = filePath;
                previewMode = preview;
                Text = Path.GetFileName(filePath) + " - Text Editor";
                if (previewMode)
                {
                    Text = Path.GetFileName(filePath) + " - View";
                    textBox.ReadOnly = true;
                    ShowStatusMessage("Preview mode");
                }
                else
                {
                    textBox.ReadOnly = false;
                    ShowStatusMessage(string.Empty);
                }
                
                // Читаем содержимое файла с корректной обработкой переносов строк
                string content = File.ReadAllText(filePath);
                // Обрабатываем разные типы переносов строк (Windows, Unix, Mac)
                content = content.Replace("\r\n", "\n").Replace("\r", "\n").Replace("\n", Environment.NewLine);
                textBox.Text = content;
                MoveCaretToDocumentStart();
                UpdateStatusBar();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading file: {ex.Message}", "Error", 
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void MoveCaretToDocumentStart()
        {
            if (textBox == null)
                return;

            textBox.SelectionStart = 0;
            textBox.SelectionLength = 0;
            if (!IsHandleCreated)
                return;

            BeginInvoke(new Action(() =>
            {
                if (IsDisposed || textBox.IsDisposed)
                    return;

                textBox.SelectionStart = 0;
                textBox.SelectionLength = 0;
                textBox.ScrollToCaret();
                UpdateStatusBar();
            }));
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
                File.WriteAllText(currentFilePath, textBox.Text);
                Text = Path.GetFileName(currentFilePath) + " - Text Editor";
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
                File.WriteAllText(targetPath, textBox.Text);
                if (!previewMode)
                {
                    currentFilePath = targetPath;
                    Text = Path.GetFileName(currentFilePath) + " - Text Editor";
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
            saveDialog.Filter = "Text files (*.txt)|*.txt|Markdown files (*.md)|*.md|All files (*.*)|*.*";
            saveDialog.FilterIndex = GetTextSaveFilterIndex(currentFilePath);

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
            return true;
        }

        private static int GetTextSaveFilterIndex(string filePath)
        {
            string extension = Path.GetExtension(filePath)?.ToLowerInvariant();
            return extension == ".md" ? 2 : 1;
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

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            // Сохраняем размер и позицию формы перед закрытием
            _savedSize = this.Size;
            _savedLocation = this.Location;
            
            base.OnFormClosing(e);
        }
        
        protected override void OnLoad(EventArgs e)
        {
            // Устанавливаем сохраненный размер и позицию формы, если они существуют
            if (_savedSize.Width > 0 && _savedSize.Height > 0)
            {
                this.Size = _savedSize;
            }
            
            if (_savedLocation.X >= 0 && _savedLocation.Y >= 0)
            {
                this.Location = _savedLocation;
            }

            ApplyStatusBarSettings();
            UpdateStatusBar();
            base.OnLoad(e);
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (keyData == Keys.F1)
            {
                ShowKeyboardHelp();
                return true;
            }
            else if (keyData == (Keys.Control | Keys.O) || keyData == Keys.F4)
            {
                OpenFileDialog openDialog = new OpenFileDialog();
                openDialog.Filter = "Text files (*.txt)|*.txt|Markdown files (*.md)|*.md|All files (*.*)|*.*";
                openDialog.FilterIndex = 1;
                
                if (openDialog.ShowDialog() == DialogResult.OK)
                {
                    LoadFile(openDialog.FileName);
                }
                return true;
            }
            else if (keyData == Keys.Escape)
            {
                this.Close();
                return true;
            }

            return base.ProcessCmdKey(ref msg, keyData);
        }
    }
}
