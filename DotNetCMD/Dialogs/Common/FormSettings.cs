using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Text;
using System.Linq;
using System.Windows.Forms;

namespace DotNetCommander
{
    public sealed class FormSettings : Form
    {
        private readonly FileBrowser.BrowserColumnWidths initialColumnWidths;
        private readonly ListBox categoryListBox;
        private readonly Panel pageHostPanel;
        private readonly Panel viewPage;
        private readonly Panel editorPage;
        private readonly Panel richTextPage;
        private readonly Panel operationsPage;
        private readonly Panel performancePage;
        private readonly ComboBox browserFontComboBox;
        private readonly ComboBox textEditorFontComboBox;
        private readonly ComboBox rtfEditorFontComboBox;
        private readonly ComboBox markdownPreviewFontComboBox;
        private readonly ComboBox markdownPreviewCodeFontComboBox;
        private readonly NumericUpDown browserFontSizeNumeric;
        private readonly NumericUpDown textEditorFontSizeNumeric;
        private readonly NumericUpDown rtfEditorFontSizeNumeric;
        private readonly NumericUpDown markdownPreviewBaseFontSizeNumeric;
        private readonly NumericUpDown markdownPreviewH1FontSizeNumeric;
        private readonly NumericUpDown markdownPreviewH2FontSizeNumeric;
        private readonly NumericUpDown markdownPreviewH3FontSizeNumeric;
        private readonly NumericUpDown markdownPreviewH4FontSizeNumeric;
        private readonly NumericUpDown markdownPreviewH5FontSizeNumeric;
        private readonly NumericUpDown markdownPreviewH6FontSizeNumeric;
        private readonly NumericUpDown nameColumnWidthNumeric;
        private readonly NumericUpDown typeColumnWidthNumeric;
        private readonly NumericUpDown sizeColumnWidthNumeric;
        private readonly NumericUpDown dateColumnWidthNumeric;
        private readonly ComboBox uiLanguageComboBox;
        private readonly ComboBox dialogFontComboBox;
        private readonly CheckBox textEditorWordWrapCheckBox;
        private readonly CheckBox showEditorStatusBarCheckBox;
        private readonly CheckBox markdownPreviewRerenderOnResizeCheckBox;
        private readonly CheckBox showStatusHintsCheckBox;
        private readonly CheckBox quickViewCsvEnabledCheckBox;
        private readonly CheckBox overwriteExistingFilesCheckBox;
        private readonly NumericUpDown quickViewCsvMaxMbNumeric;
        private readonly NumericUpDown dialogFontSizeNumeric;
        private readonly NumericUpDown dialogCaptionFontSizeNumeric;
        private readonly NumericUpDown dialogEmphasisFontSizeNumeric;
        private readonly NumericUpDown dialogHeaderFontSizeNumeric;
        private readonly CheckBox loadIconsCheckBox;
        private readonly CheckBox loadLargeIconsCheckBox;
        private readonly CheckBox watchDirectoryChangesCheckBox;
        private readonly TextBox settingsPathTextBox;
        private readonly Panel dialogPreviewPanel;
        private readonly Label dialogPreviewTitleLabel;
        private readonly Label dialogPreviewHeaderLabel;
        private readonly Label dialogPreviewBodyLabel;
        private readonly Label dialogPreviewEmphasisLabel;
        private readonly Label dialogPreviewCaptionLabel;

        public FormSettings(FileBrowser.BrowserColumnWidths initialColumnWidths)
        {
            this.initialColumnWidths = initialColumnWidths;
            Text = Language.getString("options");
            StartPosition = FormStartPosition.CenterParent;
            MinimumSize = new Size(820, 560);
            Size = new Size(980, 640);

            categoryListBox = new ListBox
            {
                Dock = DockStyle.Left,
                Width = 220,
                BorderStyle = BorderStyle.FixedSingle,
                Font = DialogStyleService.CreateDialogFont(sizeOverride: 10.5f),
                IntegralHeight = false
            };
            categoryListBox.Items.Add(Language.getString("settingsTabView"));
            categoryListBox.Items.Add(Language.getString("settingsTabEditor"));
            categoryListBox.Items.Add(Language.getString("settingsTabRichText"));
            categoryListBox.Items.Add(Language.getString("settingsTabOperations"));
            categoryListBox.Items.Add(Language.getString("settingsTabPerformance"));
            categoryListBox.SelectedIndexChanged += (_, __) => ShowSelectedPage();

            pageHostPanel = new Panel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(16)
            };

            viewPage = new Panel { Dock = DockStyle.Fill, Padding = new Padding(8) };
            editorPage = new Panel { Dock = DockStyle.Fill, Padding = new Padding(8) };
            richTextPage = new Panel { Dock = DockStyle.Fill, Padding = new Padding(8) };
            operationsPage = new Panel { Dock = DockStyle.Fill, Padding = new Padding(8) };
            performancePage = new Panel { Dock = DockStyle.Fill, Padding = new Padding(8) };

            browserFontComboBox = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                Width = 280
            };
            foreach (FontFamily family in new InstalledFontCollection().Families.OrderBy(f => f.Name))
            {
                browserFontComboBox.Items.Add(family.Name);
            }

            textEditorFontComboBox = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                Width = 280
            };
            foreach (object item in browserFontComboBox.Items)
            {
                textEditorFontComboBox.Items.Add(item);
            }

            dialogFontComboBox = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                Width = 280
            };
            foreach (object item in browserFontComboBox.Items)
            {
                dialogFontComboBox.Items.Add(item);
            }

            rtfEditorFontComboBox = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                Width = 280
            };
            markdownPreviewFontComboBox = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                Width = 280
            };
            markdownPreviewCodeFontComboBox = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                Width = 280
            };
            foreach (object item in browserFontComboBox.Items)
            {
                rtfEditorFontComboBox.Items.Add(item);
                markdownPreviewFontComboBox.Items.Add(item);
                markdownPreviewCodeFontComboBox.Items.Add(item);
            }

            uiLanguageComboBox = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                Width = 220
            };
            uiLanguageComboBox.Items.Add(new LanguageOption("auto", Language.getString("settingsLanguageAuto")));
            uiLanguageComboBox.Items.Add(new LanguageOption("en", "English"));
            uiLanguageComboBox.Items.Add(new LanguageOption("de-DE", "Deutsch"));
            uiLanguageComboBox.Items.Add(new LanguageOption("uk", "Українська"));

            browserFontSizeNumeric = CreateNumeric(8, 32, 120);
            textEditorFontSizeNumeric = CreateNumeric(6, 72, 120);
            rtfEditorFontSizeNumeric = CreateNumeric(6, 72, 120);
            markdownPreviewBaseFontSizeNumeric = CreateNumeric(8, 48, 120);
            markdownPreviewH1FontSizeNumeric = CreateNumeric(8, 72, 120);
            markdownPreviewH2FontSizeNumeric = CreateNumeric(8, 72, 120);
            markdownPreviewH3FontSizeNumeric = CreateNumeric(8, 72, 120);
            markdownPreviewH4FontSizeNumeric = CreateNumeric(8, 72, 120);
            markdownPreviewH5FontSizeNumeric = CreateNumeric(8, 72, 120);
            markdownPreviewH6FontSizeNumeric = CreateNumeric(8, 72, 120);
            nameColumnWidthNumeric = CreateNumeric(80, 800, 120);
            typeColumnWidthNumeric = CreateNumeric(50, 400, 120);
            sizeColumnWidthNumeric = CreateNumeric(60, 400, 120);
            dateColumnWidthNumeric = CreateNumeric(80, 500, 120);
            quickViewCsvMaxMbNumeric = CreateNumeric(1, 64, 100);
            dialogFontSizeNumeric = CreateNumeric(8, 24, 120);
            dialogCaptionFontSizeNumeric = CreateNumeric(8, 24, 120);
            dialogEmphasisFontSizeNumeric = CreateNumeric(8, 24, 120);
            dialogHeaderFontSizeNumeric = CreateNumeric(8, 28, 120);
            dialogPreviewPanel = new Panel
            {
                Dock = DockStyle.Top,
                Height = 176,
                Padding = new Padding(16, 14, 16, 14),
                BackColor = Color.WhiteSmoke,
                BorderStyle = BorderStyle.FixedSingle
            };
            dialogPreviewTitleLabel = new Label
            {
                Dock = DockStyle.Top,
                Height = 22,
                Text = Language.getString("settingsDialogPreviewTitle")
            };
            dialogPreviewHeaderLabel = new Label
            {
                Dock = DockStyle.Top,
                Height = 34,
                Text = Language.getString("settingsDialogPreviewHeader")
            };
            dialogPreviewBodyLabel = new Label
            {
                Dock = DockStyle.Top,
                Height = 28,
                Text = Language.getString("settingsDialogPreviewBody")
            };
            dialogPreviewEmphasisLabel = new Label
            {
                Dock = DockStyle.Top,
                Height = 30,
                Text = Language.getString("settingsDialogPreviewEmphasis")
            };
            dialogPreviewCaptionLabel = new Label
            {
                Dock = DockStyle.Top,
                Height = 24,
                ForeColor = SystemColors.GrayText,
                Text = Language.getString("settingsDialogPreviewCaption")
            };
            dialogPreviewPanel.Controls.Add(dialogPreviewEmphasisLabel);
            dialogPreviewPanel.Controls.Add(dialogPreviewBodyLabel);
            dialogPreviewPanel.Controls.Add(dialogPreviewCaptionLabel);
            dialogPreviewPanel.Controls.Add(dialogPreviewHeaderLabel);
            dialogPreviewPanel.Controls.Add(dialogPreviewTitleLabel);

            quickViewCsvEnabledCheckBox = new CheckBox
            {
                Dock = DockStyle.Top,
                Height = 34,
                Text = Language.getString("settingsQuickViewCsvEnable")
            };

            overwriteExistingFilesCheckBox = new CheckBox
            {
                Dock = DockStyle.Top,
                Height = 34,
                Text = Language.getString("settingsOverwriteExistingFiles")
            };

            textEditorWordWrapCheckBox = new CheckBox
            {
                Dock = DockStyle.Top,
                Height = 34,
                Text = Language.getString("settingsTextEditorWordWrap")
            };

            showEditorStatusBarCheckBox = new CheckBox
            {
                Dock = DockStyle.Top,
                Height = 34,
                Text = Language.getString("settingsShowEditorStatusBar")
            };

            markdownPreviewRerenderOnResizeCheckBox = new CheckBox
            {
                Dock = DockStyle.Top,
                Height = 34,
                Text = Language.getString("settingsMarkdownPreviewRerenderOnResize")
            };

            loadIconsCheckBox = new CheckBox
            {
                Dock = DockStyle.Top,
                Height = 34,
                Text = Language.getString("settingsLoadIcons")
            };

            showStatusHintsCheckBox = new CheckBox
            {
                Dock = DockStyle.Top,
                Height = 34,
                Text = Language.getString("settingsShowStatusHints")
            };

            loadLargeIconsCheckBox = new CheckBox
            {
                Dock = DockStyle.Top,
                Height = 34,
                Text = Language.getString("settingsLoadLargeIcons")
            };

            watchDirectoryChangesCheckBox = new CheckBox
            {
                Dock = DockStyle.Top,
                Height = 34,
                Text = Language.getString("settingsWatchDirectoryChanges")
            };

            settingsPathTextBox = new TextBox
            {
                ReadOnly = true,
                Dock = DockStyle.Top
            };

            BuildViewPage();
            BuildEditorPage();
            BuildRichTextPage();
            BuildOperationsPage();
            BuildPerformancePage();

            pageHostPanel.Controls.Add(viewPage);
            pageHostPanel.Controls.Add(editorPage);
            pageHostPanel.Controls.Add(richTextPage);
            pageHostPanel.Controls.Add(operationsPage);
            pageHostPanel.Controls.Add(performancePage);

            var buttonPanel = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 64,
                Padding = new Padding(12)
            };

            var okButton = new Button
            {
                Text = Language.getString("ok"),
                DialogResult = DialogResult.OK,
                Width = 110,
                Height = 36,
                Anchor = AnchorStyles.Right | AnchorStyles.Bottom
            };

            var cancelButton = new Button
            {
                Text = Language.getString("cancel"),
                DialogResult = DialogResult.Cancel,
                Width = 110,
                Height = 36,
                Anchor = AnchorStyles.Right | AnchorStyles.Bottom
            };

            buttonPanel.Resize += (_, __) =>
            {
                cancelButton.Location = new Point(buttonPanel.ClientSize.Width - cancelButton.Width, 8);
                okButton.Location = new Point(cancelButton.Left - okButton.Width - 8, 8);
            };
            buttonPanel.Controls.Add(okButton);
            buttonPanel.Controls.Add(cancelButton);

            Controls.Add(pageHostPanel);
            Controls.Add(categoryListBox);
            Controls.Add(buttonPanel);

            AcceptButton = okButton;
            CancelButton = cancelButton;

            LoadSettings();
            HookDialogPreviewEvents();
            UpdateDialogPreview();
            categoryListBox.SelectedIndex = 0;
            okButton.Click += (_, __) => SaveSettings();
        }

        private void BuildViewPage()
        {
            var layout = CreateSettingsLayout();
            AddRow(layout, Language.getString("settingsLanguage"), uiLanguageComboBox);
            AddRow(layout, Language.getString("settingsBrowserFontName"), browserFontComboBox);
            AddRow(layout, Language.getString("settingsBrowserFontSize"), browserFontSizeNumeric);
            AddRow(layout, Language.getString("settingsColumnNameWidth"), nameColumnWidthNumeric);
            AddRow(layout, Language.getString("settingsColumnTypeWidth"), typeColumnWidthNumeric);
            AddRow(layout, Language.getString("settingsColumnSizeWidth"), sizeColumnWidthNumeric);
            AddRow(layout, Language.getString("settingsColumnDateWidth"), dateColumnWidthNumeric);

            var optionsPanel = new Panel
            {
                Dock = DockStyle.Top,
                Height = 42
            };
            optionsPanel.Controls.Add(showStatusHintsCheckBox);

            viewPage.Controls.Add(optionsPanel);
            viewPage.Controls.Add(layout);
            viewPage.Controls.Add(CreatePageHeader(Language.getString("settingsViewTitle"), Language.getString("settingsViewIntro")));
        }

        private void BuildEditorPage()
        {
            var layout = CreateSettingsLayout();
            AddRow(layout, Language.getString("settingsTextEditorFontName"), textEditorFontComboBox);
            AddRow(layout, Language.getString("settingsTextEditorFontSize"), textEditorFontSizeNumeric);

            var optionsPanel = new Panel
            {
                Dock = DockStyle.Top,
                Height = 76
            };
            optionsPanel.Controls.Add(showEditorStatusBarCheckBox);
            optionsPanel.Controls.Add(textEditorWordWrapCheckBox);

            editorPage.Controls.Add(optionsPanel);
            editorPage.Controls.Add(layout);
            editorPage.Controls.Add(CreatePageHeader(Language.getString("settingsEditorTitle"), Language.getString("settingsEditorIntro")));
        }

        private void BuildRichTextPage()
        {
            var layout = CreateSettingsLayout();
            AddRow(layout, Language.getString("settingsRtfEditorFontName"), rtfEditorFontComboBox);
            AddRow(layout, Language.getString("settingsRtfEditorFontSize"), rtfEditorFontSizeNumeric);
            AddRow(layout, Language.getString("settingsMarkdownPreviewFontName"), markdownPreviewFontComboBox);
            AddRow(layout, Language.getString("settingsMarkdownPreviewCodeFontName"), markdownPreviewCodeFontComboBox);
            AddRow(layout, Language.getString("settingsMarkdownPreviewBaseFontSize"), markdownPreviewBaseFontSizeNumeric);
            AddRow(layout, Language.getString("settingsMarkdownPreviewH1FontSize"), markdownPreviewH1FontSizeNumeric);
            AddRow(layout, Language.getString("settingsMarkdownPreviewH2FontSize"), markdownPreviewH2FontSizeNumeric);
            AddRow(layout, Language.getString("settingsMarkdownPreviewH3FontSize"), markdownPreviewH3FontSizeNumeric);
            AddRow(layout, Language.getString("settingsMarkdownPreviewH4FontSize"), markdownPreviewH4FontSizeNumeric);
            AddRow(layout, Language.getString("settingsMarkdownPreviewH5FontSize"), markdownPreviewH5FontSizeNumeric);
            AddRow(layout, Language.getString("settingsMarkdownPreviewH6FontSize"), markdownPreviewH6FontSizeNumeric);

            var optionsPanel = new Panel
            {
                Dock = DockStyle.Top,
                Height = 42
            };
            optionsPanel.Controls.Add(markdownPreviewRerenderOnResizeCheckBox);

            richTextPage.Controls.Add(optionsPanel);
            richTextPage.Controls.Add(layout);
            richTextPage.Controls.Add(CreatePageHeader(Language.getString("settingsRichTextTitle"), Language.getString("settingsRichTextIntro")));
        }

        private void BuildOperationsPage()
        {
            var quickViewPanel = new Panel
            {
                Dock = DockStyle.Top,
                Height = 468
            };

            var dialogLayout = CreateSettingsLayout();
            AddRow(dialogLayout, Language.getString("settingsDialogFontName"), dialogFontComboBox);
            AddRow(dialogLayout, Language.getString("settingsDialogFontSize"), dialogFontSizeNumeric);
            AddRow(dialogLayout, Language.getString("settingsDialogCaptionFontSize"), dialogCaptionFontSizeNumeric);
            AddRow(dialogLayout, Language.getString("settingsDialogEmphasisFontSize"), dialogEmphasisFontSizeNumeric);
            AddRow(dialogLayout, Language.getString("settingsDialogHeaderFontSize"), dialogHeaderFontSizeNumeric);
            dialogLayout.Dock = DockStyle.Top;

            var sizePanel = new FlowLayoutPanel
            {
                Dock = DockStyle.Top,
                Height = 42,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                Padding = new Padding(0, 4, 0, 0)
            };

            var sizeLabel = new Label
            {
                AutoSize = true,
                Text = Language.getString("settingsQuickViewCsvMaxSize"),
                Margin = new Padding(0, 8, 12, 0)
            };

            var mbLabel = new Label
            {
                AutoSize = true,
                Text = "MB",
                Margin = new Padding(8, 8, 0, 0)
            };

            var noteLabel = new Label
            {
                Dock = DockStyle.Top,
                Height = 52,
                ForeColor = SystemColors.GrayText,
                Text = Language.getString("settingsQuickViewCsvNote")
            };

            sizePanel.Controls.Add(sizeLabel);
            sizePanel.Controls.Add(quickViewCsvMaxMbNumeric);
            sizePanel.Controls.Add(mbLabel);

            quickViewPanel.Controls.Add(noteLabel);
            quickViewPanel.Controls.Add(sizePanel);
            quickViewPanel.Controls.Add(dialogPreviewPanel);
            quickViewPanel.Controls.Add(dialogLayout);
            quickViewPanel.Controls.Add(overwriteExistingFilesCheckBox);
            quickViewPanel.Controls.Add(quickViewCsvEnabledCheckBox);

            operationsPage.Controls.Add(quickViewPanel);
            operationsPage.Controls.Add(CreatePageHeader(Language.getString("settingsOperationsTitle"), Language.getString("settingsOperationsIntro")));
        }

        private void BuildPerformancePage()
        {
            var storageHintLabel = new Label
            {
                Dock = DockStyle.Top,
                Height = 48,
                ForeColor = SystemColors.GrayText,
                Text = Language.getString("settingsStorageFormat")
            };

            var storageLabel = new Label
            {
                Dock = DockStyle.Top,
                Height = 24,
                Text = Language.getString("settingsStorage")
            };

            performancePage.Controls.Add(storageHintLabel);
            performancePage.Controls.Add(settingsPathTextBox);
            performancePage.Controls.Add(storageLabel);
            performancePage.Controls.Add(watchDirectoryChangesCheckBox);
            performancePage.Controls.Add(loadLargeIconsCheckBox);
            performancePage.Controls.Add(loadIconsCheckBox);
            performancePage.Controls.Add(CreatePageHeader(Language.getString("settingsPerformanceTitle"), Language.getString("settingsPerformanceIntro")));
        }

        private void LoadSettings()
        {
            SelectLanguageOption(Properties.Settings.Default.UiLanguage);
            SelectFontComboValue(browserFontComboBox, Properties.Settings.Default.BrowserFontName, "Microsoft Sans Serif");
            browserFontSizeNumeric.Value = (decimal)Math.Max(8f, Properties.Settings.Default.BrowserFontSize);
            SelectFontComboValue(dialogFontComboBox, Properties.Settings.Default.DialogFontName, "Segoe UI");
            dialogFontSizeNumeric.Value = (decimal)Math.Max(8f, Properties.Settings.Default.DialogFontSize);
            dialogCaptionFontSizeNumeric.Value = (decimal)Math.Max(8f, Properties.Settings.Default.DialogCaptionFontSize);
            dialogEmphasisFontSizeNumeric.Value = (decimal)Math.Max(8f, Properties.Settings.Default.DialogEmphasisFontSize);
            dialogHeaderFontSizeNumeric.Value = (decimal)Math.Max(8f, Properties.Settings.Default.DialogHeaderFontSize);
            SelectFontComboValue(textEditorFontComboBox, Properties.Settings.Default.TextEditorFontName, "Consolas");
            textEditorFontSizeNumeric.Value = (decimal)Math.Max(6f, Properties.Settings.Default.TextEditorFontSize);
            SelectFontComboValue(rtfEditorFontComboBox, Properties.Settings.Default.RtfEditorFontName, "Segoe UI");
            SelectFontComboValue(markdownPreviewFontComboBox, Properties.Settings.Default.MarkdownPreviewFontName, "Segoe UI");
            SelectFontComboValue(markdownPreviewCodeFontComboBox, Properties.Settings.Default.MarkdownPreviewCodeFontName, "Consolas");
            rtfEditorFontSizeNumeric.Value = (decimal)Math.Max(6f, Properties.Settings.Default.RtfEditorFontSize);
            markdownPreviewBaseFontSizeNumeric.Value = Math.Max(markdownPreviewBaseFontSizeNumeric.Minimum, Properties.Settings.Default.MarkdownPreviewBaseFontSize);
            markdownPreviewH1FontSizeNumeric.Value = Math.Max(markdownPreviewH1FontSizeNumeric.Minimum, Properties.Settings.Default.MarkdownPreviewH1FontSize);
            markdownPreviewH2FontSizeNumeric.Value = Math.Max(markdownPreviewH2FontSizeNumeric.Minimum, Properties.Settings.Default.MarkdownPreviewH2FontSize);
            markdownPreviewH3FontSizeNumeric.Value = Math.Max(markdownPreviewH3FontSizeNumeric.Minimum, Properties.Settings.Default.MarkdownPreviewH3FontSize);
            markdownPreviewH4FontSizeNumeric.Value = Math.Max(markdownPreviewH4FontSizeNumeric.Minimum, Properties.Settings.Default.MarkdownPreviewH4FontSize);
            markdownPreviewH5FontSizeNumeric.Value = Math.Max(markdownPreviewH5FontSizeNumeric.Minimum, Properties.Settings.Default.MarkdownPreviewH5FontSize);
            markdownPreviewH6FontSizeNumeric.Value = Math.Max(markdownPreviewH6FontSizeNumeric.Minimum, Properties.Settings.Default.MarkdownPreviewH6FontSize);
            nameColumnWidthNumeric.Value = Math.Max(nameColumnWidthNumeric.Minimum, initialColumnWidths.NameWidth);
            typeColumnWidthNumeric.Value = Math.Max(typeColumnWidthNumeric.Minimum, initialColumnWidths.TypeWidth);
            sizeColumnWidthNumeric.Value = Math.Max(sizeColumnWidthNumeric.Minimum, initialColumnWidths.SizeWidth);
            dateColumnWidthNumeric.Value = Math.Max(dateColumnWidthNumeric.Minimum, initialColumnWidths.DateWidth);

            quickViewCsvEnabledCheckBox.Checked = Properties.Settings.Default.QuickViewCsvEnabled;
            overwriteExistingFilesCheckBox.Checked = Properties.Settings.Default.OverwriteExistingFiles;
            quickViewCsvMaxMbNumeric.Value = Math.Max(1, Properties.Settings.Default.QuickViewCsvMaxBytes / (1024 * 1024));
            textEditorWordWrapCheckBox.Checked = Properties.Settings.Default.TextEditorWordWrap;
            showStatusHintsCheckBox.Checked = Properties.Settings.Default.ShowStatusHints;
            showEditorStatusBarCheckBox.Checked = Properties.Settings.Default.ShowEditorStatusBar;
            markdownPreviewRerenderOnResizeCheckBox.Checked = Properties.Settings.Default.MarkdownPreviewRerenderOnResize;

            loadIconsCheckBox.Checked = Properties.Settings.Default.FileBrowserLoadIcons;
            loadLargeIconsCheckBox.Checked = Properties.Settings.Default.FileBrowserLoadLargeIcons;
            watchDirectoryChangesCheckBox.Checked = Properties.Settings.Default.FileBrowserWatchDirectoryChanges;
            settingsPathTextBox.Text = SettingsStorage.GetUserConfigPath();
        }

        private void SaveSettings()
        {
            Properties.Settings.Default.UiLanguage = (uiLanguageComboBox.SelectedItem as LanguageOption)?.Value ?? "auto";
            Properties.Settings.Default.BrowserFontName = browserFontComboBox.SelectedItem?.ToString() ?? "Microsoft Sans Serif";
            Properties.Settings.Default.BrowserFontSize = (float)browserFontSizeNumeric.Value;
            Properties.Settings.Default.DialogFontName = dialogFontComboBox.SelectedItem?.ToString() ?? "Segoe UI";
            Properties.Settings.Default.DialogFontSize = (float)dialogFontSizeNumeric.Value;
            Properties.Settings.Default.DialogCaptionFontSize = (float)dialogCaptionFontSizeNumeric.Value;
            Properties.Settings.Default.DialogEmphasisFontSize = (float)dialogEmphasisFontSizeNumeric.Value;
            Properties.Settings.Default.DialogHeaderFontSize = (float)dialogHeaderFontSizeNumeric.Value;
            Properties.Settings.Default.TextEditorFontName = textEditorFontComboBox.SelectedItem?.ToString() ?? "Consolas";
            Properties.Settings.Default.TextEditorFontSize = (float)textEditorFontSizeNumeric.Value;
            Properties.Settings.Default.RtfEditorFontName = rtfEditorFontComboBox.SelectedItem?.ToString() ?? "Segoe UI";
            Properties.Settings.Default.RtfEditorFontSize = (float)rtfEditorFontSizeNumeric.Value;
            Properties.Settings.Default.MarkdownPreviewFontName = markdownPreviewFontComboBox.SelectedItem?.ToString() ?? "Segoe UI";
            Properties.Settings.Default.MarkdownPreviewCodeFontName = markdownPreviewCodeFontComboBox.SelectedItem?.ToString() ?? "Consolas";
            Properties.Settings.Default.MarkdownPreviewBaseFontSize = (int)markdownPreviewBaseFontSizeNumeric.Value;
            Properties.Settings.Default.MarkdownPreviewH1FontSize = (int)markdownPreviewH1FontSizeNumeric.Value;
            Properties.Settings.Default.MarkdownPreviewH2FontSize = (int)markdownPreviewH2FontSizeNumeric.Value;
            Properties.Settings.Default.MarkdownPreviewH3FontSize = (int)markdownPreviewH3FontSizeNumeric.Value;
            Properties.Settings.Default.MarkdownPreviewH4FontSize = (int)markdownPreviewH4FontSizeNumeric.Value;
            Properties.Settings.Default.MarkdownPreviewH5FontSize = (int)markdownPreviewH5FontSizeNumeric.Value;
            Properties.Settings.Default.MarkdownPreviewH6FontSize = (int)markdownPreviewH6FontSizeNumeric.Value;
            Properties.Settings.Default.TextEditorWordWrap = textEditorWordWrapCheckBox.Checked;
            Properties.Settings.Default.ShowEditorStatusBar = showEditorStatusBarCheckBox.Checked;
            Properties.Settings.Default.MarkdownPreviewRerenderOnResize = markdownPreviewRerenderOnResizeCheckBox.Checked;
            Properties.Settings.Default.ShowStatusHints = showStatusHintsCheckBox.Checked;
            Properties.Settings.Default.FileBrowserNameColumnWidth = (int)nameColumnWidthNumeric.Value;
            Properties.Settings.Default.FileBrowserTypeColumnWidth = (int)typeColumnWidthNumeric.Value;
            Properties.Settings.Default.FileBrowserSizeColumnWidth = (int)sizeColumnWidthNumeric.Value;
            Properties.Settings.Default.FileBrowserDateColumnWidth = (int)dateColumnWidthNumeric.Value;

            Properties.Settings.Default.QuickViewCsvEnabled = quickViewCsvEnabledCheckBox.Checked;
            Properties.Settings.Default.OverwriteExistingFiles = overwriteExistingFilesCheckBox.Checked;
            Properties.Settings.Default.QuickViewCsvMaxBytes = (int)quickViewCsvMaxMbNumeric.Value * 1024 * 1024;

            Properties.Settings.Default.FileBrowserLoadIcons = loadIconsCheckBox.Checked;
            Properties.Settings.Default.FileBrowserLoadLargeIcons = loadLargeIconsCheckBox.Checked;
            Properties.Settings.Default.FileBrowserWatchDirectoryChanges = watchDirectoryChangesCheckBox.Checked;
            Properties.Settings.Default.Save();
        }

        private void SelectLanguageOption(string value)
        {
            LanguageOption selectedOption = uiLanguageComboBox.Items
                .OfType<LanguageOption>()
                .FirstOrDefault(option => string.Equals(option.Value, value, StringComparison.OrdinalIgnoreCase))
                ?? uiLanguageComboBox.Items.OfType<LanguageOption>().FirstOrDefault();

            uiLanguageComboBox.SelectedItem = selectedOption;
        }

        private void ShowSelectedPage()
        {
            viewPage.Visible = categoryListBox.SelectedIndex == 0;
            editorPage.Visible = categoryListBox.SelectedIndex == 1;
            richTextPage.Visible = categoryListBox.SelectedIndex == 2;
            operationsPage.Visible = categoryListBox.SelectedIndex == 3;
            performancePage.Visible = categoryListBox.SelectedIndex == 4;
        }

        private static void SelectFontComboValue(ComboBox comboBox, string value, string fallbackValue)
        {
            comboBox.SelectedItem = value;
            if (comboBox.SelectedIndex < 0 && comboBox.Items.Count > 0)
            {
                comboBox.SelectedItem = fallbackValue;
            }
        }

        private static NumericUpDown CreateNumeric(decimal minimum, decimal maximum, int width)
        {
            return new NumericUpDown
            {
                Minimum = minimum,
                Maximum = maximum,
                Width = width
            };
        }

        private static TableLayoutPanel CreateSettingsLayout()
        {
            return new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                ColumnCount = 2,
                Padding = new Padding(0, 8, 0, 0)
            };
        }

        private static void AddRow(TableLayoutPanel layout, string labelText, Control control)
        {
            int rowIndex = layout.RowCount++;
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            var label = new Label
            {
                AutoSize = true,
                Text = labelText,
                Margin = new Padding(0, 8, 16, 8)
            };
            control.Margin = new Padding(0, 4, 0, 4);

            layout.Controls.Add(label, 0, rowIndex);
            layout.Controls.Add(control, 1, rowIndex);
        }

        private void HookDialogPreviewEvents()
        {
            dialogFontComboBox.SelectedIndexChanged += (_, __) => UpdateDialogPreview();
            dialogFontSizeNumeric.ValueChanged += (_, __) => UpdateDialogPreview();
            dialogCaptionFontSizeNumeric.ValueChanged += (_, __) => UpdateDialogPreview();
            dialogEmphasisFontSizeNumeric.ValueChanged += (_, __) => UpdateDialogPreview();
            dialogHeaderFontSizeNumeric.ValueChanged += (_, __) => UpdateDialogPreview();
        }

        private void UpdateDialogPreview()
        {
            string fontName = dialogFontComboBox.SelectedItem?.ToString();
            if (string.IsNullOrWhiteSpace(fontName))
            {
                fontName = "Segoe UI";
            }

            ApplyPreviewFont(dialogPreviewTitleLabel, fontName, 9f, FontStyle.Regular, SystemColors.GrayText);
            ApplyPreviewFont(dialogPreviewHeaderLabel, fontName, (float)dialogHeaderFontSizeNumeric.Value, FontStyle.Bold, SystemColors.ControlText);
            ApplyPreviewFont(dialogPreviewBodyLabel, fontName, (float)dialogFontSizeNumeric.Value, FontStyle.Regular, SystemColors.ControlText);
            ApplyPreviewFont(dialogPreviewEmphasisLabel, fontName, (float)dialogEmphasisFontSizeNumeric.Value, FontStyle.Bold, Color.FromArgb(40, 84, 125));
            ApplyPreviewFont(dialogPreviewCaptionLabel, fontName, (float)dialogCaptionFontSizeNumeric.Value, FontStyle.Regular, SystemColors.GrayText);
        }

        private static void ApplyPreviewFont(Control control, string fontName, float fontSize, FontStyle fontStyle, Color color)
        {
            if (control.Tag is Font managedFont)
            {
                managedFont.Dispose();
            }

            control.Font = CreatePreviewFont(fontName, fontSize, fontStyle);
            control.Tag = control.Font;
            control.ForeColor = color;
        }

        private static Font CreatePreviewFont(string fontName, float size, FontStyle style)
        {
            float safeSize = Math.Max(8f, size);

            try
            {
                return new Font(fontName, safeSize, style, GraphicsUnit.Point);
            }
            catch
            {
                return new Font("Segoe UI", safeSize, style, GraphicsUnit.Point);
            }
        }

        private static Control CreatePageHeader(string title, string description)
        {
            var panel = new Panel
            {
                Dock = DockStyle.Top,
                Height = 76
            };

            panel.Controls.Add(new Label
            {
                Dock = DockStyle.Top,
                Height = 30,
                Font = DialogStyleService.CreateHeaderFont(),
                Text = title
            });

            panel.Controls.Add(new Label
            {
                Dock = DockStyle.Fill,
                ForeColor = SystemColors.GrayText,
                Text = description
            });

            return panel;
        }

        private sealed class LanguageOption
        {
            public LanguageOption(string value, string displayName)
            {
                Value = value;
                DisplayName = displayName;
            }

            public string Value { get; }
            public string DisplayName { get; }

            public override string ToString()
            {
                return DisplayName;
            }
        }
    }
}
