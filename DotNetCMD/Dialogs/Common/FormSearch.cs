using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
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
        private readonly TextBox minSizeText;
        private readonly TextBox maxSizeText;
        private readonly CheckBox dateFromCheck;
        private readonly DateTimePicker dateFromValue;
        private readonly CheckBox dateToCheck;
        private readonly DateTimePicker dateToValue;
        private readonly ComboBox recentCombo;
        private readonly Button clearRecentButton;

        public FormSearch(string initialDirectory)
        {
            Text = Language.getString("searchTitle");
            Font = DialogStyleService.CreateDialogFont();
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.CenterParent;
            ClientSize = new Size(620, 484);

            var layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(16),
                ColumnCount = 3,
                RowCount = 12
            };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            for (int row = 0; row < 11; row++)
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

            var sizeLabel = new Label { AutoSize = true, Anchor = AnchorStyles.Left, Text = Language.getString("searchSizeFilter") };
            minSizeText = new TextBox { Anchor = AnchorStyles.Left, Width = 110 };
            maxSizeText = new TextBox { Anchor = AnchorStyles.Left, Width = 110 };
            var sizeSeparator = new Label { AutoSize = true, Anchor = AnchorStyles.Left, Text = ".." };
            var sizeFlow = new FlowLayoutPanel
            {
                AutoSize = true,
                Anchor = AnchorStyles.Left,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                Margin = new Padding(0)
            };
            sizeFlow.Controls.Add(minSizeText);
            sizeFlow.Controls.Add(sizeSeparator);
            sizeFlow.Controls.Add(maxSizeText);
            var sizeHint = new Label { AutoSize = true, Anchor = AnchorStyles.Left, Text = Language.getString("searchSizeHint") };
            layout.Controls.Add(sizeLabel, 0, 7);
            layout.Controls.Add(sizeFlow, 1, 7);
            layout.Controls.Add(sizeHint, 2, 7);

            dateFromCheck = new CheckBox { AutoSize = true, Anchor = AnchorStyles.Left, Text = Language.getString("searchDateFrom") };
            dateFromValue = new DateTimePicker
            {
                Anchor = AnchorStyles.Left,
                Width = 140,
                Format = DateTimePickerFormat.Short,
                Enabled = false
            };
            dateFromCheck.CheckedChanged += (_, __) => dateFromValue.Enabled = dateFromCheck.Checked;
            layout.Controls.Add(dateFromCheck, 0, 8);
            layout.Controls.Add(dateFromValue, 1, 8);

            dateToCheck = new CheckBox { AutoSize = true, Anchor = AnchorStyles.Left, Text = Language.getString("searchDateTo") };
            dateToValue = new DateTimePicker
            {
                Anchor = AnchorStyles.Left,
                Width = 140,
                Format = DateTimePickerFormat.Short,
                Enabled = false
            };
            dateToCheck.CheckedChanged += (_, __) => dateToValue.Enabled = dateToCheck.Checked;
            layout.Controls.Add(dateToCheck, 0, 9);
            layout.Controls.Add(dateToValue, 1, 9);

            var recentLabel = new Label { AutoSize = true, Anchor = AnchorStyles.Left, Text = Language.getString("searchRecent") };
            recentCombo = new ComboBox
            {
                Anchor = AnchorStyles.Left | AnchorStyles.Right,
                DropDownStyle = ComboBoxStyle.DropDownList,
                Width = 320
            };
            recentCombo.SelectedIndexChanged += RecentCombo_SelectedIndexChanged;
            clearRecentButton = new Button { AutoSize = true, Text = Language.getString("searchRecentClear") };
            clearRecentButton.Click += ClearRecent;
            layout.Controls.Add(recentLabel, 0, 10);
            layout.Controls.Add(recentCombo, 1, 10);
            layout.Controls.Add(clearRecentButton, 2, 10);
            RebuildRecentList();

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
            layout.Controls.Add(buttons, 0, 11);
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

            long? minSize = null;
            long? maxSize = null;
            if (!TryParseSizeFilter(minSizeText.Text, out minSize) ||
                !TryParseSizeFilter(maxSizeText.Text, out maxSize))
            {
                string raw = string.IsNullOrWhiteSpace(minSizeText.Text) ? maxSizeText.Text : minSizeText.Text;
                MessageBox.Show(
                    this,
                    string.Format(CultureInfo.CurrentCulture, Language.getString("searchInvalidSize"), raw.Trim()),
                    Text,
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                (string.IsNullOrWhiteSpace(minSizeText.Text) ? maxSizeText : minSizeText).Focus();
                return;
            }

            if (minSize.HasValue && maxSize.HasValue && minSize.Value > maxSize.Value)
            {
                MessageBox.Show(this, Language.getString("searchInvalidSizeRange"), Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                minSizeText.Focus();
                return;
            }

            DateTime? modifiedFrom = dateFromCheck.Checked ? dateFromValue.Value.Date : (DateTime?)null;
            DateTime? modifiedTo = dateToCheck.Checked
                ? dateToValue.Value.Date.AddDays(1).AddTicks(-1)
                : (DateTime?)null;
            if (modifiedFrom.HasValue && modifiedTo.HasValue && modifiedFrom.Value > modifiedTo.Value)
            {
                MessageBox.Show(this, Language.getString("searchInvalidDateRange"), Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                dateFromValue.Focus();
                return;
            }

            Query = new SearchQuery
            {
                ScopeDirectory = Path.GetFullPath(scope),
                NamePattern = pattern,
                ContentText = contentText.Text,
                Recursive = recursiveCheck.Checked,
                IncludeDirectories = directoriesCheck.Checked,
                UseRegex = regexCheck.Checked,
                MaxDepth = depthValue.Value == 0 ? -1 : Decimal.ToInt32(depthValue.Value),
                MinSizeBytes = minSize,
                MaxSizeBytes = maxSize,
                ModifiedFrom = modifiedFrom,
                ModifiedTo = modifiedTo
            };
            SearchQueryHistory.Session.Add(Query);
            DialogResult = DialogResult.OK;
            Close();
        }

        private void RebuildRecentList()
        {
            recentCombo.SelectedIndexChanged -= RecentCombo_SelectedIndexChanged;
            try
            {
                recentCombo.Items.Clear();
                foreach (SearchQuery query in SearchQueryHistory.Session.Items)
                {
                    recentCombo.Items.Add(SearchQueryHistory.GetDisplayLabel(query));
                }
                recentCombo.SelectedIndex = -1;
            }
            finally
            {
                recentCombo.SelectedIndexChanged += RecentCombo_SelectedIndexChanged;
            }

            bool hasHistory = recentCombo.Items.Count > 0;
            recentCombo.Enabled = hasHistory;
            clearRecentButton.Enabled = hasHistory;
        }

        private void RecentCombo_SelectedIndexChanged(object sender, EventArgs e)
        {
            int index = recentCombo.SelectedIndex;
            IReadOnlyList<SearchQuery> history = SearchQueryHistory.Session.Items;
            if (index < 0 || index >= history.Count)
                return;

            ApplyQuery(history[index]);
        }

        private void ClearRecent(object sender, EventArgs e)
        {
            SearchQueryHistory.Session.Clear();
            RebuildRecentList();
        }

        private void ApplyQuery(SearchQuery query)
        {
            scopeText.Text = query.ScopeDirectory;
            patternText.Text = query.NamePattern;
            contentText.Text = query.ContentText;
            recursiveCheck.Checked = query.Recursive;
            directoriesCheck.Checked = query.IncludeDirectories;
            regexCheck.Checked = query.UseRegex;
            depthValue.Value = query.MaxDepth < 0
                ? 0
                : Math.Min(depthValue.Maximum, Math.Max(depthValue.Minimum, query.MaxDepth));
            minSizeText.Text = FormatSize(query.MinSizeBytes);
            maxSizeText.Text = FormatSize(query.MaxSizeBytes);
            dateFromCheck.Checked = query.ModifiedFrom.HasValue;
            if (query.ModifiedFrom.HasValue)
                dateFromValue.Value = query.ModifiedFrom.Value.Date;
            dateToCheck.Checked = query.ModifiedTo.HasValue;
            if (query.ModifiedTo.HasValue)
                dateToValue.Value = query.ModifiedTo.Value.Date;
        }

        private static string FormatSize(long? bytes)
        {
            if (!bytes.HasValue)
                return string.Empty;

            long value = bytes.Value;
            if (value >= 1024L * 1024 * 1024 && value % (1024L * 1024 * 1024) == 0)
                return string.Format(CultureInfo.InvariantCulture, "{0}G", value / (1024L * 1024 * 1024));
            if (value >= 1024L * 1024 && value % (1024L * 1024) == 0)
                return string.Format(CultureInfo.InvariantCulture, "{0}M", value / (1024L * 1024));
            if (value >= 1024 && value % 1024 == 0)
                return string.Format(CultureInfo.InvariantCulture, "{0}k", value / 1024);
            return value.ToString(CultureInfo.InvariantCulture);
        }

        private static bool TryParseSizeFilter(string text, out long? bytes)
        {
            bytes = null;
            if (string.IsNullOrWhiteSpace(text))
                return true;

            string token = text.Trim().Replace(" ", string.Empty);
            long multiplier = 1;
            if (token.EndsWith("B", StringComparison.OrdinalIgnoreCase) && token.Length > 1)
                token = token.Substring(0, token.Length - 1);

            if (token.Length > 1)
            {
                char unit = token[token.Length - 1];
                if (unit == 'k' || unit == 'K')
                {
                    multiplier = 1024;
                    token = token.Substring(0, token.Length - 1);
                }
                else if (unit == 'm' || unit == 'M')
                {
                    multiplier = 1024L * 1024;
                    token = token.Substring(0, token.Length - 1);
                }
                else if (unit == 'g' || unit == 'G')
                {
                    multiplier = 1024L * 1024 * 1024;
                    token = token.Substring(0, token.Length - 1);
                }
            }

            if (!double.TryParse(token, NumberStyles.Float, CultureInfo.InvariantCulture, out double value) ||
                value < 0 ||
                double.IsNaN(value) ||
                double.IsInfinity(value))
            {
                return false;
            }

            double total = value * multiplier;
            if (total > long.MaxValue)
                return false;

            bytes = (long)Math.Round(total);
            return true;
        }
    }
}
