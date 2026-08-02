using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace DotNetCommander
{
    internal sealed class DataSetBrowser : BrowserPanelBase
    {
        private const string TablesPath = "tables";
        private const string RelationsPath = "relations";

        private readonly AddressBar addressBar;
        private readonly DataGridView grid;
        private readonly Label loadingLabel;
        private readonly List<BrowserItemInfo> visibleItems = new List<BrowserItemInfo>();
        private CancellationTokenSource loadCancellation;
        private DataSet dataSet;
        private string internalPath = string.Empty;

        public DataSetBrowser()
        {
            BackColor = Color.White;

            addressBar = new AddressBar
            {
                Dock = DockStyle.Top,
                Height = 32
            };
            addressBar.BackClick += (_, __) => NavigateBackRequested?.Invoke(this, EventArgs.Empty);
            addressBar.ParentClick += (_, __) => NavigateParent();
            addressBar.RefreshClick += (_, __) => RefreshPanel();
            addressBar.ButtonClick += AddressBar_ButtonClick;

            grid = new DataGridView
            {
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                AllowUserToOrderColumns = true,
                AutoGenerateColumns = true,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.DisplayedCells,
                BackgroundColor = Color.White,
                BorderStyle = BorderStyle.None,
                ClipboardCopyMode = DataGridViewClipboardCopyMode.EnableWithoutHeaderText,
                ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing,
                Dock = DockStyle.Fill,
                EditMode = DataGridViewEditMode.EditProgrammatically,
                MultiSelect = true,
                ReadOnly = true,
                RowHeadersVisible = true,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect
            };
            grid.CellDoubleClick += (_, __) => ActivateSelectedItem();
            grid.KeyDown += Grid_KeyDown;
            grid.SelectionChanged += (_, __) => RaiseSelectionChanged();
            grid.DataError += (_, e) => e.ThrowException = false;

            loadingLabel = new Label
            {
                BackColor = Color.White,
                Dock = DockStyle.Fill,
                Font = DialogStyleService.CreateEmphasisFont(),
                ForeColor = Color.FromArgb(88, 96, 105),
                TextAlign = ContentAlignment.MiddleCenter,
                Visible = false
            };

            Controls.Add(loadingLabel);
            Controls.Add(grid);
            Controls.Add(addressBar);
        }

        public event EventHandler LeaveDataSetRequested;
        public event EventHandler NavigateBackRequested;

        public string DataSetPath { get; private set; }
        public string InternalPath => internalPath;
        public int TotalItemCount => IsTableRows
            ? CurrentTable?.Rows.Count ?? 0
            : visibleItems.Count;
        public override string DisplayLocation => string.IsNullOrWhiteSpace(DataSetPath)
            ? string.Empty
            : Path.GetFileName(DataSetPath) + ":\\" + BuildDisplayInternalPath();
        public override IReadOnlyList<BrowserItemInfo> Items => visibleItems;
        public override IReadOnlyList<BrowserItemInfo> SelectedItems => GetSelectedItems();
        public override BrowserPanelCapabilities Capabilities => BrowserPanelCapabilities.Navigate;
        public string CurrentItemName => SelectedItems.Count == 1 ? SelectedItems[0].Name : null;
        public string SelectedLocation => SelectedItems.Count == 1 ? SelectedItems[0].Location : null;

        private bool IsTableRows => internalPath.StartsWith("table/", StringComparison.OrdinalIgnoreCase);
        private DataTable CurrentTable => TryGetTable(internalPath, out DataTable table) ? table : null;

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                loadCancellation?.Cancel();
                loadCancellation?.Dispose();
                loadCancellation = null;
                dataSet?.Dispose();
                dataSet = null;
            }
            base.Dispose(disposing);
        }

        public async Task<bool> OpenDataSetAsync(string path, bool reportFailure = true)
        {
            loadCancellation?.Cancel();
            loadCancellation?.Dispose();
            loadCancellation = new CancellationTokenSource();
            DataSetPath = Path.GetFullPath(path);
            SetLoading(true, Language.getString("dataSetReading"));
            frmWait waitForm = null;
            try
            {
                Task<DataSet> loadTask = DataSetCatalogService.ReadAsync(DataSetPath, loadCancellation.Token);
                if (await Task.WhenAny(loadTask, Task.Delay(1000)) != loadTask &&
                    !IsDisposed &&
                    FindForm() is Form owner &&
                    !owner.IsDisposed)
                {
                    waitForm = new frmWait(Language.getString("dataSetReading"));
                    waitForm.Show(owner);
                    waitForm.Refresh();
                }

                DataSet loaded = await loadTask;
                dataSet?.Dispose();
                dataSet = loaded;
                internalPath = string.Empty;
                RenderCurrentLocation();
                return true;
            }
            catch (OperationCanceledException)
            {
                return false;
            }
            catch (Exception ex)
            {
                if (reportFailure)
                {
                    LogService.LogException("DataSetBrowser.OpenDataSetAsync", ex);
                    MessageBox.Show(
                        FindForm(),
                        Language.getString("dataSetOpenFailed") + Environment.NewLine + ex.Message,
                        Language.getString("error"),
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Error);
                }
                return false;
            }
            finally
            {
                if (waitForm != null && !waitForm.IsDisposed)
                {
                    waitForm.Close();
                    waitForm.Dispose();
                }
                SetLoading(false, string.Empty);
            }
        }

        public override bool Navigate(string location)
        {
            string target = NormalizeInternalPath(location);
            if (!IsValidInternalPath(target))
                return false;

            internalPath = target;
            RenderCurrentLocation();
            return true;
        }

        public override bool NavigateParent()
        {
            if (string.IsNullOrEmpty(internalPath))
            {
                LeaveDataSetRequested?.Invoke(this, EventArgs.Empty);
                return true;
            }

            if (IsTableRows)
                internalPath = TablesPath;
            else if (internalPath.StartsWith("relation/", StringComparison.OrdinalIgnoreCase))
                internalPath = RelationsPath;
            else
                internalPath = string.Empty;

            RenderCurrentLocation();
            return true;
        }

        public override void RefreshPanel()
        {
            if (string.IsNullOrWhiteSpace(DataSetPath))
                return;

            string previousPath = internalPath;
            string previousSelection = SelectedLocation;
            _ = RefreshAsync(previousPath, previousSelection);
        }

        private async Task RefreshAsync(string previousPath, string previousSelection)
        {
            if (await OpenDataSetAsync(DataSetPath))
            {
                Navigate(previousPath);
                SelectLocation(previousSelection);
            }
        }

        public bool SelectLocation(string location)
        {
            if (string.IsNullOrWhiteSpace(location))
                return false;

            if (IsTableRows && location.StartsWith(internalPath + "/row/", StringComparison.OrdinalIgnoreCase))
            {
                string suffix = location.Substring((internalPath + "/row/").Length);
                if (int.TryParse(suffix, NumberStyles.Integer, CultureInfo.InvariantCulture, out int rowIndex)
                    && rowIndex >= 0 && rowIndex < grid.Rows.Count)
                {
                    grid.ClearSelection();
                    grid.Rows[rowIndex].Selected = true;
                    grid.CurrentCell = grid.Rows[rowIndex].Cells.Cast<DataGridViewCell>().FirstOrDefault();
                    return true;
                }
            }

            DataGridViewRow row = grid.Rows.Cast<DataGridViewRow>()
                .FirstOrDefault(candidate => candidate.Tag is NavigationEntry entry
                    && string.Equals(entry.Info.Location, location, StringComparison.OrdinalIgnoreCase));
            if (row == null)
                return false;

            grid.ClearSelection();
            row.Selected = true;
            grid.CurrentCell = row.Cells.Cast<DataGridViewCell>().FirstOrDefault();
            return true;
        }

        public void FocusItems()
        {
            grid.Focus();
        }

        public void ApplyUserSettings(Font browserFont)
        {
            if (browserFont == null)
                return;

            Font = browserFont;
            addressBar.ApplyDisplayFont(browserFont);
            addressBar.Height = Math.Max(30, browserFont.Height + 8);
            grid.Font = browserFont;
            grid.ColumnHeadersDefaultCellStyle.Font = browserFont;
            grid.ColumnHeadersHeight = Math.Max(24, browserFont.Height + 8);
            grid.RowTemplate.Height = Math.Max(22, browserFont.Height + 8);
        }

        private void ActivateSelectedItem()
        {
            NavigationEntry entry = grid.SelectedRows.Cast<DataGridViewRow>()
                .Select(row => row.Tag as NavigationEntry)
                .FirstOrDefault(item => item != null);
            if (entry == null)
                return;

            if (entry.TargetPath == "..")
                NavigateParent();
            else if (!string.IsNullOrWhiteSpace(entry.TargetPath))
                Navigate(entry.TargetPath);
        }

        private void Grid_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                ActivateSelectedItem();
                e.Handled = true;
                e.SuppressKeyPress = true;
            }
            else if (e.KeyCode == Keys.Back)
            {
                NavigateBackRequested?.Invoke(this, EventArgs.Empty);
                e.Handled = true;
                e.SuppressKeyPress = true;
            }
            else if (e.KeyCode == Keys.PageUp && e.Control)
            {
                NavigateParent();
                e.Handled = true;
                e.SuppressKeyPress = true;
            }
        }

        private void RenderCurrentLocation()
        {
            grid.SuspendLayout();
            grid.DataSource = null;
            grid.Columns.Clear();
            grid.Rows.Clear();
            visibleItems.Clear();

            if (string.IsNullOrEmpty(internalPath))
                RenderRoot();
            else if (string.Equals(internalPath, TablesPath, StringComparison.OrdinalIgnoreCase))
                RenderTables();
            else if (string.Equals(internalPath, RelationsPath, StringComparison.OrdinalIgnoreCase))
                RenderRelations();
            else if (IsTableRows)
                RenderTableRows();
            else if (internalPath.StartsWith("relation/", StringComparison.OrdinalIgnoreCase))
                RenderRelationDetails();

            addressBar.Path = DisplayLocation;
            grid.ResumeLayout();
            RaiseLocationChanged(DisplayLocation);
            if (grid.Rows.Count > 0)
            {
                grid.ClearSelection();
                grid.Rows[0].Selected = true;
                if (grid.Rows[0].Cells.Count > 0)
                    grid.CurrentCell = grid.Rows[0].Cells[0];
            }
            grid.Focus();
        }

        private void AddressBar_ButtonClick(object sender, EventArgs e)
        {
            string caption = (sender as Control)?.Text?.Trim();
            if (string.IsNullOrWhiteSpace(caption))
                return;

            caption = caption.TrimEnd('›').TrimEnd();
            if (string.Equals(caption, Path.GetFileName(DataSetPath) + ":", StringComparison.OrdinalIgnoreCase))
            {
                Navigate(string.Empty);
                return;
            }

            if (string.Equals(caption, Language.getString("dataSetTables"), StringComparison.CurrentCultureIgnoreCase))
            {
                Navigate(TablesPath);
                return;
            }

            if (string.Equals(caption, Language.getString("dataSetRelations"), StringComparison.CurrentCultureIgnoreCase))
            {
                Navigate(RelationsPath);
            }
        }

        private void RenderRoot()
        {
            ConfigureIndexColumns(Language.getString("dataSetObject"), Language.getString("dataSetCount"), Language.getString("dataSetDescription"));
            AddNavigationRow("..", "..", string.Empty, "..", true);
            AddNavigationRow(Language.getString("dataSetTables"), (dataSet?.Tables.Count ?? 0).ToString(CultureInfo.CurrentCulture),
                Language.getString("dataSetTablesDescription"), TablesPath, true);
            AddNavigationRow(Language.getString("dataSetRelations"), (dataSet?.Relations.Count ?? 0).ToString(CultureInfo.CurrentCulture),
                Language.getString("dataSetRelationsDescription"), RelationsPath, true);
        }

        private void RenderTables()
        {
            ConfigureIndexColumns(Language.getString("dataSetTable"), Language.getString("dataSetRows"), Language.getString("dataSetColumns"));
            AddNavigationRow("..", string.Empty, string.Empty, "..", true);
            if (dataSet == null)
                return;

            for (int index = 0; index < dataSet.Tables.Count; index++)
            {
                DataTable table = dataSet.Tables[index];
                AddNavigationRow(table.TableName, table.Rows.Count.ToString(CultureInfo.CurrentCulture),
                    table.Columns.Count.ToString(CultureInfo.CurrentCulture), "table/" + index, true);
            }
        }

        private void RenderRelations()
        {
            ConfigureIndexColumns(Language.getString("dataSetRelation"), Language.getString("dataSetParentTable"), Language.getString("dataSetChildTable"));
            AddNavigationRow("..", string.Empty, string.Empty, "..", true);
            if (dataSet == null)
                return;

            for (int index = 0; index < dataSet.Relations.Count; index++)
            {
                DataRelation relation = dataSet.Relations[index];
                AddNavigationRow(relation.RelationName, relation.ParentTable.TableName, relation.ChildTable.TableName,
                    "relation/" + index, true);
            }
        }

        private void RenderTableRows()
        {
            DataTable table = CurrentTable;
            if (table == null)
                return;

            grid.AutoGenerateColumns = true;
            grid.DataSource = table.DefaultView;
            grid.RowHeadersVisible = true;
            visibleItems.Clear();
        }

        private void RenderRelationDetails()
        {
            ConfigureIndexColumns(Language.getString("dataSetProperty"), Language.getString("dataSetValue"), string.Empty);
            if (!TryGetRelation(internalPath, out DataRelation relation))
                return;

            AddDetailRow(Language.getString("dataSetRelation"), relation.RelationName);
            AddDetailRow(Language.getString("dataSetParentTable"), relation.ParentTable.TableName);
            AddDetailRow(Language.getString("dataSetParentColumns"), string.Join(", ", relation.ParentColumns.Select(column => column.ColumnName)));
            AddDetailRow(Language.getString("dataSetChildTable"), relation.ChildTable.TableName);
            AddDetailRow(Language.getString("dataSetChildColumns"), string.Join(", ", relation.ChildColumns.Select(column => column.ColumnName)));
            AddDetailRow(Language.getString("dataSetNested"), relation.Nested.ToString(CultureInfo.CurrentCulture));
        }

        private void ConfigureIndexColumns(string first, string second, string third)
        {
            grid.AutoGenerateColumns = false;
            grid.RowHeadersVisible = false;
            grid.Columns.Add("name", first);
            grid.Columns.Add("value", second);
            if (!string.IsNullOrEmpty(third))
                grid.Columns.Add("details", third);
        }

        private void AddNavigationRow(string name, string value, string details, string targetPath, bool isDirectory)
        {
            int rowIndex = grid.Columns.Count > 2
                ? grid.Rows.Add(name, value, details)
                : grid.Rows.Add(name, value);
            var info = new BrowserItemInfo
            {
                Name = name,
                Location = targetPath == ".." ? ".." : targetPath,
                NativePath = DataSetPath,
                IsDirectory = isDirectory
            };
            grid.Rows[rowIndex].Tag = new NavigationEntry(info, targetPath);
            if (targetPath != "..")
                visibleItems.Add(info);
        }

        private void AddDetailRow(string property, string value)
        {
            int rowIndex = grid.Rows.Add(property, value);
            var info = new BrowserItemInfo
            {
                Name = property,
                Location = internalPath + "/" + rowIndex.ToString(CultureInfo.InvariantCulture),
                NativePath = DataSetPath
            };
            grid.Rows[rowIndex].Tag = new NavigationEntry(info, null);
            visibleItems.Add(info);
        }

        private IReadOnlyList<BrowserItemInfo> GetSelectedItems()
        {
            if (IsTableRows)
            {
                return grid.SelectedRows.Cast<DataGridViewRow>()
                    .Where(row => !row.IsNewRow)
                    .OrderBy(row => row.Index)
                    .Select(row => new BrowserItemInfo
                    {
                        Name = string.Format(CultureInfo.CurrentCulture, Language.getString("dataSetRowFormat"), row.Index + 1),
                        Location = internalPath + "/row/" + row.Index.ToString(CultureInfo.InvariantCulture),
                        NativePath = DataSetPath
                    })
                    .ToArray();
            }

            return grid.SelectedRows.Cast<DataGridViewRow>()
                .Select(row => row.Tag as NavigationEntry)
                .Where(entry => entry != null && entry.TargetPath != "..")
                .Select(entry => entry.Info)
                .ToArray();
        }

        private bool IsValidInternalPath(string path)
        {
            if (string.IsNullOrEmpty(path) ||
                string.Equals(path, TablesPath, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(path, RelationsPath, StringComparison.OrdinalIgnoreCase))
                return true;

            return path.StartsWith("table/", StringComparison.OrdinalIgnoreCase)
                ? TryGetTable(path, out DataTable _)
                : path.StartsWith("relation/", StringComparison.OrdinalIgnoreCase)
                    && TryGetRelation(path, out DataRelation _);
        }

        private bool TryGetTable(string path, out DataTable value)
        {
            value = null;
            if (dataSet == null || !TryParseIndex(path, "table/", dataSet.Tables.Count, out int index))
                return false;
            value = dataSet.Tables[index];
            return true;
        }

        private bool TryGetRelation(string path, out DataRelation value)
        {
            value = null;
            if (dataSet == null || !TryParseIndex(path, "relation/", dataSet.Relations.Count, out int index))
                return false;
            value = dataSet.Relations[index];
            return true;
        }

        private static bool TryParseIndex(string path, string prefix, int count, out int index)
        {
            index = -1;
            if (string.IsNullOrWhiteSpace(path) || !path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                return false;
            return int.TryParse(path.Substring(prefix.Length), NumberStyles.Integer, CultureInfo.InvariantCulture, out index)
                && index >= 0 && index < count;
        }

        private string BuildDisplayInternalPath()
        {
            if (string.IsNullOrEmpty(internalPath))
                return string.Empty;
            if (string.Equals(internalPath, TablesPath, StringComparison.OrdinalIgnoreCase))
                return Language.getString("dataSetTables");
            if (string.Equals(internalPath, RelationsPath, StringComparison.OrdinalIgnoreCase))
                return Language.getString("dataSetRelations");
            if (TryGetTable(internalPath, out DataTable table))
                return Language.getString("dataSetTables") + "\\" + table.TableName;
            if (TryGetRelation(internalPath, out DataRelation relation))
                return Language.getString("dataSetRelations") + "\\" + relation.RelationName;
            return internalPath;
        }

        private static string NormalizeInternalPath(string path)
        {
            return (path ?? string.Empty).Trim().Trim('\\', '/').Replace('\\', '/');
        }

        private void SetLoading(bool loading, string text)
        {
            loadingLabel.Text = text ?? string.Empty;
            loadingLabel.Visible = loading;
            grid.Visible = !loading;
            if (loading)
                loadingLabel.BringToFront();
        }

        private sealed class NavigationEntry
        {
            public NavigationEntry(BrowserItemInfo info, string targetPath)
            {
                Info = info;
                TargetPath = targetPath;
            }
            public BrowserItemInfo Info { get; }
            public string TargetPath { get; }
        }
    }
}
