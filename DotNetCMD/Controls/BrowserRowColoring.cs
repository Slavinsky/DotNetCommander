using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace DotNetCommander
{
    internal static class BrowserRowColoring
    {
        internal const string None = "None";
        internal const string Striped = "Striped";
        internal const string ByExtension = "ByExtension";


        internal static string NormalizeMode(string mode)
        {
            if (string.Equals(mode, Striped, StringComparison.OrdinalIgnoreCase))
                return Striped;
            if (string.Equals(mode, ByExtension, StringComparison.OrdinalIgnoreCase))
                return ByExtension;
            return None;
        }

        internal static void Apply(ListView listView, string mode)
        {
            if (listView == null)
                return;

            string normalizedMode = NormalizeMode(mode);
            Color baseColor = listView.BackColor.IsEmpty ? SystemColors.Window : listView.BackColor;
            Color foregroundColor = listView.ForeColor.IsEmpty ? SystemColors.WindowText : listView.ForeColor;

            for (int index = 0; index < listView.Items.Count; index++)
            {
                ListViewItem item = listView.Items[index];
                Color rowColor = GetRowColor(normalizedMode, index, GetItemName(item), baseColor);
                item.UseItemStyleForSubItems = normalizedMode != None;
                item.BackColor = rowColor;
                item.ForeColor = normalizedMode == None ? Color.Empty : foregroundColor;
            }
        }

        internal static void Apply(DataGridView grid, string mode)
        {
            if (grid == null)
                return;

            string normalizedMode = NormalizeMode(mode);
            Color baseColor = grid.BackgroundColor.IsEmpty ? SystemColors.Window : grid.BackgroundColor;
            Color foregroundColor = grid.ForeColor.IsEmpty ? SystemColors.WindowText : grid.ForeColor;

            for (int index = 0; index < grid.Rows.Count; index++)
            {
                DataGridViewRow row = grid.Rows[index];
                if (row.IsNewRow)
                    continue;

                string rowName = row.Cells.Count == 0 ? string.Empty : row.Cells[0].Value?.ToString();
                Color rowColor = GetRowColor(normalizedMode, index, rowName, baseColor);
                row.DefaultCellStyle.BackColor = rowColor;
                row.DefaultCellStyle.ForeColor = normalizedMode == None ? Color.Empty : foregroundColor;
            }
        }

        internal static Color GetRowColor(string mode, int rowIndex, string name, Color baseColor)
        {
            switch (NormalizeMode(mode))
            {
                case Striped:
                    return rowIndex % 2 == 0 ? baseColor : GetTint(baseColor, Color.FromArgb(125, 155, 190));
                case ByExtension:
                    return GetExtensionColor(name, baseColor);
                default:
                    return Color.Empty;
            }
        }

        private static Color GetExtensionColor(string name, Color baseColor)
        {
            string extension = Path.GetExtension(name ?? string.Empty);
            if (string.IsNullOrEmpty(extension))
                return baseColor;

            switch (extension.ToLowerInvariant())
            {
                case ".png":
                case ".jpg":
                case ".jpeg":
                case ".gif":
                case ".bmp":
                case ".svg":
                    return GetTint(baseColor, Color.FromArgb(80, 140, 205));
                case ".zip":
                case ".tar":
                case ".gz":
                case ".tgz":
                case ".7z":
                case ".rar":
                    return GetTint(baseColor, Color.FromArgb(215, 145, 60));
                case ".cs":
                case ".cpp":
                case ".c":
                case ".h":
                case ".java":
                case ".js":
                case ".ts":
                case ".py":
                case ".json":
                case ".xml":
                    return GetTint(baseColor, Color.FromArgb(75, 160, 105));
                case ".txt":
                case ".md":
                case ".rtf":
                case ".doc":
                case ".docx":
                case ".pdf":
                    return GetTint(baseColor, Color.FromArgb(145, 105, 185));
                default:
                    return baseColor;
            }
        }

        private static string GetItemName(ListViewItem item)
        {
            return item?.SubItems.Count > 1
                ? item.SubItems[1].Text
                : item?.Text;
        }

        private static Color GetTint(Color baseColor, Color accent)
        {
            const double tintStrength = 0.16;
            return Color.FromArgb(
                Blend(baseColor.R, accent.R, tintStrength),
                Blend(baseColor.G, accent.G, tintStrength),
                Blend(baseColor.B, accent.B, tintStrength));
        }

        private static int Blend(int source, int target, double amount)
        {
            return (int)Math.Round(source + (target - source) * amount);
        }
    }
}
