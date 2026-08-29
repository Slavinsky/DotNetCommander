using System;
using System.Drawing;
using System.IO;
using System.Text.RegularExpressions;
using System.Windows.Forms;

namespace DotNetCommander
{
    internal sealed class FormSearch : Form
    {
        private readonly TextBox scopeText;
        private readonly TextBox patternText;
        private readonly TextBox contentText;
        private readonly CheckBox recursiveCheck;
        private readonly CheckBox directoriesCheck;
        private readonly CheckBox regexCheck;
        private readonly NumericUpDown depthValue;

        public FormSearch(string initialDirectory)
        {
            Text = Language.getString("searchTitle");
            Font = DialogStyleService.CreateDialogFont();
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.CenterParent;
            ClientSize = new Size(620, 340);

            var layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(16),
                ColumnCount = 3,
                RowCount = 8
            };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            for (int row = 0; row < 7; row++)
                layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

            scopeText = AddTextRow(layout, 0, "searchScope", initialDirectory ?? string.Empty);
            var browseButton = new Button { AutoSize = true, Text = Language.getString("searchBrowse") };
            browseButton.Click += BrowseScope;
            layout.Controls.Add(browseButton, 2, 0);

            patternText = AddTextRow(layout, 1, "searchNamePattern", "*");
            contentText = AddTextRow(layout, 2, "searchContentText", string.Empty);
            recursiveCheck = AddCheck(layout, 3, "searchRecursive", true);
            directoriesCheck = AddCheck(layout, 4, "searchIncludeDirectories", true);
            regexCheck = AddCheck(layout, 5, "searchUseRegex", false);

            var depthLabel = new Label { AutoSize = true, Anchor = AnchorStyles.Left, Text = Language.getString("searchMaxDepth") };
            depthValue = new NumericUpDown { Minimum = 0, Maximum = 999, Value = 0, Width = 90 };
            var depthHint = new Label { AutoSize = true, Anchor = AnchorStyles.Left, Text = Language.getString("searchUnlimitedDepth") };
            layout.Controls.Add(depthLabel, 0, 6);
            layout.Controls.Add(depthValue, 1, 6);
            layout.Controls.Add(depthHint, 2, 6);

            var buttons = new FlowLayoutPanel
            {
                AutoSize = true,
                Anchor = AnchorStyles.Bottom | AnchorStyles.Right,
                FlowDirection = FlowDirection.RightToLeft,
                WrapContents = false
            };
            var cancelButton = new Button { AutoSize = true, DialogResult = DialogResult.Cancel, Text = Language.getString("cancel") };
            var searchButton = new Button { AutoSize = true, Text = Language.getString("searchStart") };
            searchButton.Click += StartSearch;
            buttons.Controls.Add(cancelButton);
            buttons.Controls.Add(searchButton);
            layout.Controls.Add(buttons, 0, 7);
            layout.SetColumnSpan(buttons, 3);

            AcceptButton = searchButton;
            CancelButton = cancelButton;
            Controls.Add(layout);
        }

        public SearchQuery Query { get; private set; }

        private static TextBox AddTextRow(TableLayoutPanel layout, int row, string labelKey, string value)
        {
            var label = new Label { AutoSize = true, Anchor = AnchorStyles.Left, Text = Language.getString(labelKey) };
            var textBox = new TextBox { Anchor = AnchorStyles.Left | AnchorStyles.Right, Text = value };
            layout.Controls.Add(label, 0, row);
            layout.Controls.Add(textBox, 1, row);
            layout.SetColumnSpan(textBox, 2);
            return textBox;
        }

        private static CheckBox AddCheck(TableLayoutPanel layout, int row, string key, bool value)
        {
            var checkBox = new CheckBox { AutoSize = true, Checked = value, Text = Language.getString(key) };
            layout.Controls.Add(checkBox, 1, row);
            layout.SetColumnSpan(checkBox, 2);
            return checkBox;
        }

        private void BrowseScope(object sender, EventArgs e)
        {
            using var dialog = new FolderBrowserDialog
            {
                Description = Language.getString("searchScope"),
                SelectedPath = Directory.Exists(scopeText.Text) ? scopeText.Text : string.Empty,
                ShowNewFolderButton = false
            };
            if (dialog.ShowDialog(this) == DialogResult.OK)
                scopeText.Text = dialog.SelectedPath;
        }

        private void StartSearch(object sender, EventArgs e)
        {
            string scope = scopeText.Text.Trim();
            if (!Directory.Exists(scope))
            {
                MessageBox.Show(this, Language.getString("searchInvalidScope"), Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                scopeText.Focus();
                return;
            }

            string pattern = string.IsNullOrWhiteSpace(patternText.Text) ? "*" : patternText.Text.Trim();
            if (regexCheck.Checked)
            {
                try { _ = new Regex(pattern); }
                catch (ArgumentException ex)
                {
                    MessageBox.Show(this, ex.Message, Language.getString("searchInvalidRegex"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    patternText.Focus();
                    return;
                }
            }

            Query = new SearchQuery
            {
                ScopeDirectory = Path.GetFullPath(scope),
                NamePattern = pattern,
                ContentText = contentText.Text,
                Recursive = recursiveCheck.Checked,
                IncludeDirectories = directoriesCheck.Checked,
                UseRegex = regexCheck.Checked,
                MaxDepth = depthValue.Value == 0 ? -1 : Decimal.ToInt32(depthValue.Value)
            };
            DialogResult = DialogResult.OK;
            Close();
        }
    }
}
