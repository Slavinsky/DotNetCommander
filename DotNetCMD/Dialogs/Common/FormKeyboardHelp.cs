using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace DotNetCommander
{
    internal readonly struct KeyboardHelpItem
    {
        public KeyboardHelpItem(string shortcut, string action)
        {
            Shortcut = shortcut ?? string.Empty;
            Action = action ?? string.Empty;
        }

        public string Shortcut { get; }
        public string Action { get; }
    }

    internal sealed class FormKeyboardHelp : Form
    {
        private readonly List<Font> ownedFonts = new List<Font>();

        public FormKeyboardHelp(string title, string subtitle, IEnumerable<KeyboardHelpItem> items)
        {
            Text = title;
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.Sizable;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;
            KeyPreview = true;
            MinimumSize = new Size(600, 430);
            ClientSize = new Size(700, 520);
            AutoScaleMode = AutoScaleMode.Font;
            Font = Own(DialogStyleService.CreateDialogFont());
            Icon = Properties.Resources.icon;

            var badge = new Label
            {
                Anchor = AnchorStyles.None,
                AutoSize = false,
                BackColor = Color.FromArgb(40, 84, 125),
                ForeColor = Color.White,
                Font = Own(DialogStyleService.CreateEmphasisFont()),
                Text = "F1",
                TextAlign = ContentAlignment.MiddleCenter,
                Size = new Size(52, 44),
                Margin = new Padding(0, 2, 18, 0)
            };

            var titleLabel = new Label
            {
                AutoSize = true,
                Font = Own(DialogStyleService.CreateHeaderFont()),
                Text = title,
                Margin = new Padding(0, 0, 0, 5)
            };
            var subtitleLabel = new Label
            {
                AutoSize = true,
                Font = Own(DialogStyleService.CreateBodyFont()),
                ForeColor = SystemColors.GrayText,
                Text = subtitle,
                Margin = new Padding(0)
            };
            var headerText = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                AutoSize = true,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                Margin = new Padding(0)
            };
            headerText.Controls.Add(titleLabel);
            headerText.Controls.Add(subtitleLabel);

            var header = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                Height = 94,
                ColumnCount = 2,
                Padding = new Padding(20, 18, 20, 12),
                BackColor = SystemColors.Window
            };
            header.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 70));
            header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            header.Controls.Add(badge, 0, 0);
            header.Controls.Add(headerText, 1, 0);

            var grid = CreateShortcutGrid();
            foreach (KeyboardHelpItem item in items ?? Array.Empty<KeyboardHelpItem>())
                grid.Rows.Add(item.Shortcut, item.Action);
            grid.ClearSelection();
            grid.SelectionChanged += (_, __) => grid.ClearSelection();

            var gridHost = new Panel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(20, 12, 20, 8),
                BackColor = SystemColors.Window
            };
            gridHost.Controls.Add(grid);

            var closeHint = new Label
            {
                AutoSize = true,
                Anchor = AnchorStyles.Left,
                Font = Own(DialogStyleService.CreateCaptionFont()),
                ForeColor = SystemColors.GrayText,
                Text = Language.getString("keyboardHelpCloseHint")
            };
            var closeButton = new Button
            {
                AutoSize = false,
                DialogResult = DialogResult.OK,
                Text = Language.getString("close"),
                Size = new Size(110, 34),
                Anchor = AnchorStyles.Right
            };
            var footer = new TableLayoutPanel
            {
                Dock = DockStyle.Bottom,
                Height = 62,
                ColumnCount = 2,
                Padding = new Padding(20, 8, 20, 14),
                BackColor = SystemColors.Control
            };
            footer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            footer.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            footer.Controls.Add(closeHint, 0, 0);
            footer.Controls.Add(closeButton, 1, 0);

            Controls.Add(gridHost);
            Controls.Add(footer);
            Controls.Add(header);
            AcceptButton = closeButton;
            CancelButton = closeButton;
        }

        private DataGridView CreateShortcutGrid()
        {
            var grid = new DataGridView
            {
                Dock = DockStyle.Fill,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                AllowUserToResizeRows = false,
                AutoGenerateColumns = false,
                BackgroundColor = SystemColors.Window,
                BorderStyle = BorderStyle.None,
                CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal,
                ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.None,
                ColumnHeadersHeight = 34,
                ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing,
                EnableHeadersVisualStyles = false,
                GridColor = SystemColors.ControlLight,
                MultiSelect = false,
                ReadOnly = true,
                RowHeadersVisible = false,
                RowTemplate = { Height = 31 },
                ScrollBars = ScrollBars.Vertical,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect
            };
            grid.ColumnHeadersDefaultCellStyle.BackColor = SystemColors.Control;
            grid.ColumnHeadersDefaultCellStyle.ForeColor = SystemColors.ControlText;
            grid.ColumnHeadersDefaultCellStyle.Font = Own(DialogStyleService.CreateEmphasisFont());
            grid.DefaultCellStyle.BackColor = SystemColors.Window;
            grid.DefaultCellStyle.ForeColor = SystemColors.ControlText;
            grid.DefaultCellStyle.Padding = new Padding(6, 2, 6, 2);
            grid.DefaultCellStyle.SelectionBackColor = SystemColors.Window;
            grid.DefaultCellStyle.SelectionForeColor = SystemColors.ControlText;
            grid.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(248, 249, 250);
            grid.AlternatingRowsDefaultCellStyle.SelectionBackColor = Color.FromArgb(248, 249, 250);

            var shortcutColumn = new DataGridViewTextBoxColumn
            {
                HeaderText = Language.getString("keyboardHelpShortcut"),
                Width = 190,
                MinimumWidth = 150,
                SortMode = DataGridViewColumnSortMode.NotSortable,
                Resizable = DataGridViewTriState.False
            };
            shortcutColumn.DefaultCellStyle.Font = Own(DialogStyleService.CreateEmphasisFont());
            var actionColumn = new DataGridViewTextBoxColumn
            {
                AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill,
                HeaderText = Language.getString("keyboardHelpAction"),
                MinimumWidth = 260,
                SortMode = DataGridViewColumnSortMode.NotSortable
            };
            grid.Columns.Add(shortcutColumn);
            grid.Columns.Add(actionColumn);
            return grid;
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            if (!disposing)
                return;

            foreach (Font font in ownedFonts)
                font.Dispose();
            ownedFonts.Clear();
        }

        private Font Own(Font font)
        {
            ownedFonts.Add(font);
            return font;
        }
    }
}
