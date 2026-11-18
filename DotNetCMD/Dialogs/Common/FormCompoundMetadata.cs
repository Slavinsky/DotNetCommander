using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace DotNetCommander
{
    internal sealed class FormCompoundMetadata : Form
    {
        private const int MaximumDisplayedEntries = 10000;
        public FormCompoundMetadata(CompoundMetadataReport report)
        {
            if (report == null) throw new ArgumentNullException(nameof(report));

            Text = Language.getString("compoundMetadataTitle");
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.Sizable;
            MinimizeBox = false;
            MaximizeBox = true;
            ShowInTaskbar = false;
            KeyPreview = true;
            MinimumSize = new Size(700, 480);
            ClientSize = new Size(940, 650);
            AutoScaleMode = AutoScaleMode.Font;
            DialogStyleService.ApplyDialogFont(this);
            KeyDown += (_, e) => { if (e.KeyCode == Keys.Escape) Close(); };

            var layout = new TableLayoutPanel
            {
                ColumnCount = 1,
                Dock = DockStyle.Fill,
                Padding = new Padding(16),
                RowCount = 3
            };
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            layout.Controls.Add(new Label
            {
                AutoSize = false,
                Dock = DockStyle.Fill,
                Font = DialogStyleService.CreateHeaderFont(),
                Text = report.Path,
                Height = 30,
                Margin = new Padding(0, 0, 0, 12)
            }, 0, 0);

            var tabs = new TabControl { Dock = DockStyle.Fill };
            var overview = new TabPage(Language.getString("compoundMetadataHeading"));
            var overviewLayout = new TableLayoutPanel
            {
                ColumnCount = 1,
                Dock = DockStyle.Fill,
                Padding = new Padding(10),
                RowCount = 5
            };
            overviewLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            overviewLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            overviewLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            overviewLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            overviewLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            overviewLayout.Controls.Add(CreateInfoLabel(Language.getString("compoundMetadataDocumentType"), report.DocumentType), 0, 0);
            overviewLayout.Controls.Add(CreateInfoLabel(Language.getString("compoundMetadataRootClsid"), report.RootClsid.ToString("B")), 0, 1);
            overviewLayout.Controls.Add(CreatePropertiesGrid(report), 0, 2);
            overviewLayout.Controls.Add(CreateSignals(report), 0, 3);
            overviewLayout.Controls.Add(new Label
            {
                AutoSize = true,
                Dock = DockStyle.Fill,
                ForeColor = SystemColors.GrayText,
                Margin = new Padding(0, 8, 0, 0),
                Text = Language.getString("compoundMetadataDetectionNote")
            }, 0, 4);
            overview.Controls.Add(overviewLayout);

            var entries = new TabPage(Language.getString("compoundMetadataEntries"));
            entries.Controls.Add(CreateEntriesGrid(report));
            tabs.TabPages.Add(overview);
            tabs.TabPages.Add(entries);
            layout.Controls.Add(tabs, 0, 1);

            var buttons = new FlowLayoutPanel
            {
                AutoSize = true,
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.RightToLeft,
                WrapContents = false,
                Margin = new Padding(0, 10, 0, 0)
            };
            var close = new Button { AutoSize = true, MinimumSize = new Size(110, 34), Text = Language.getString("close") };
            close.Click += (_, __) => Close();
            buttons.Controls.Add(close);
            layout.Controls.Add(buttons, 0, 2);
            Controls.Add(layout);
        }

        private static Label CreateInfoLabel(string label, string value)
        {
            return new Label
            {
                AutoEllipsis = true,
                AutoSize = false,
                Dock = DockStyle.Fill,
                Height = 24,
                Margin = new Padding(0, 0, 0, 8),
                Text = label + ":  " + value
            };
        }

        private static DataGridView CreatePropertiesGrid(CompoundMetadataReport report)
        {
            var grid = CreateGrid();
            grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = Language.getString("compoundMetadataProperty"), Width = 220 });
            grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = Language.getString("compoundMetadataKind"), Width = 90 });
            grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = Language.getString("compoundMetadataValue"), AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill });
            foreach (CompoundPropertySetRow row in report.Properties)
                grid.Rows.Add(row.Name + " (PID " + row.PropertyId + ")", row.TypeName, row.Value);
            if (report.Properties.Count == 0)
                grid.Rows.Add(Language.getString("compoundMetadataProperties"), string.Empty, Language.getString("compoundMetadataNoEvidence"));
            return grid;
        }

        private static Control CreateSignals(CompoundMetadataReport report)
        {
            var panel = new TableLayoutPanel { AutoSize = true, ColumnCount = 1, Dock = DockStyle.Top, RowCount = 5 };
            panel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            panel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            panel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            panel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            panel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            panel.Controls.Add(CreateSignalLabel("compoundMetadataProtection", report.ProtectionEvidence), 0, 0);
            panel.Controls.Add(CreateSignalLabel("compoundMetadataEncryption", report.EncryptionEvidence), 0, 1);
            panel.Controls.Add(CreateSignalLabel("compoundMetadataMacro", report.MacroEvidence), 0, 2);
            panel.Controls.Add(CreateSignalLabel("compoundMetadataEmbedded", report.EmbeddedEvidence), 0, 3);
            panel.Controls.Add(CreateSignalLabel("compoundMetadataReadWarning", report.ReadWarnings), 0, 4);
            return panel;
        }

        private static Label CreateSignalLabel(string resourceKey, System.Collections.Generic.IReadOnlyList<string> paths)
        {
            string evidence = paths.Count == 0 ? Language.getString("compoundMetadataNoEvidence")
                : string.Join(", ", paths.Take(64));
            return new Label
            {
                AutoEllipsis = true,
                AutoSize = false,
                Dock = DockStyle.Fill,
                Height = 34,
                Margin = new Padding(0, 4, 0, 4),
                Text = Language.getString(resourceKey) + ":  " + evidence
            };
        }

        private static DataGridView CreateEntriesGrid(CompoundMetadataReport report)
        {
            var grid = CreateGrid();
            grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = Language.getString("compoundMetadataPath"), AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill, MinimumWidth = 220 });
            grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = Language.getString("compoundMetadataKind"), Width = 100 });
            grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = Language.getString("compoundMetadataSize"), Width = 110 });
            grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = Language.getString("compoundMetadataClsid"), Width = 280 });
            foreach (CompoundCatalogEntry entry in report.Entries.Take(MaximumDisplayedEntries))
            {
                grid.Rows.Add(
                    DisplayPath(entry.FullPath),
                    Language.getString(entry.IsStorage ? "compoundMetadataStorage" : "compoundMetadataStream"),
                    entry.Size.HasValue ? entry.Size.Value.ToString("N0") : string.Empty,
                    entry.Clsid == Guid.Empty ? string.Empty : entry.Clsid.ToString("B"));
            }
            if (report.Entries.Count > MaximumDisplayedEntries)
            {
                int hidden = report.Entries.Count - MaximumDisplayedEntries;
                grid.Rows.Add(
                    string.Format(Language.getString("compoundMetadataEntriesTruncated"), hidden),
                    string.Empty,
                    string.Empty,
                    string.Empty);
            }
            return grid;
        }

        private static string DisplayPath(string path)
        {
            return string.Join("/", path.Split('/').Select(segment => !string.IsNullOrEmpty(segment) && char.IsControl(segment[0])
                ? "\\u" + ((int)segment[0]).ToString("X4") + segment.Substring(1)
                : segment));
        }

        private static DataGridView CreateGrid()
        {
            return new DataGridView
            {
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                AllowUserToResizeRows = false,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None,
                BackgroundColor = SystemColors.Window,
                ClipboardCopyMode = DataGridViewClipboardCopyMode.EnableAlwaysIncludeHeaderText,
                Dock = DockStyle.Fill,
                MultiSelect = false,
                ReadOnly = true,
                RowHeadersVisible = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect
            };
        }
    }
}
