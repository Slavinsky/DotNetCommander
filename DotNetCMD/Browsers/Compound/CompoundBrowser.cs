using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace DotNetCommander
{
    internal sealed class CompoundBrowser : BrowserPanelBase
    {
        private readonly CompoundFileCatalogService catalogService = new CompoundFileCatalogService();
        private readonly AddressBar addressBar;
        private readonly ListView compoundView;
        private readonly Label loadingLabel;
        private readonly List<CompoundCatalogEntry> catalog = new List<CompoundCatalogEntry>();
        private readonly List<BrowserItemInfo> visibleItems = new List<BrowserItemInfo>();
        private CancellationTokenSource operationCancellation;
        private string internalPath = string.Empty;

        public CompoundBrowser()
        {
            BackColor = Color.White;
            addressBar = new AddressBar { Dock = DockStyle.Top, Height = 32 };
            addressBar.BackClick += (_, __) => NavigateBackRequested?.Invoke(this, EventArgs.Empty);
            addressBar.ParentClick += (_, __) => NavigateParent();
            addressBar.RefreshClick += (_, __) => RefreshPanel();
            addressBar.ButtonClick += AddressBar_ButtonClick;

            compoundView = new ListView
            {
                AllowColumnReorder = true,
                Dock = DockStyle.Fill,
                FullRowSelect = true,
                HideSelection = false,
                MultiSelect = true,
                UseCompatibleStateImageBehavior = false,
                View = System.Windows.Forms.View.Details
            };
            compoundView.Columns.Add(string.Empty, 20);
            compoundView.Columns.Add(Language.getString("columnName"), 240);
            compoundView.Columns.Add(Language.getString("columnType"), 100);
            compoundView.Columns.Add(Language.getString("columnSize"), 100, HorizontalAlignment.Right);
            compoundView.Columns.Add(Language.getString("columnDate"), 140);
            compoundView.ItemActivate += (_, __) => ActivateSelectedItem();
            compoundView.SelectedIndexChanged += (_, __) => RaiseSelectionChanged();
            compoundView.KeyDown += CompoundView_KeyDown;

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
            Controls.Add(compoundView);
            Controls.Add(addressBar);
        }

        public event EventHandler LeaveCompoundRequested;
        public event EventHandler NavigateBackRequested;

        public string CompoundPath { get; private set; }
        public string InternalPath => internalPath;
        public string DocumentType { get; private set; }
        public override string DisplayLocation => string.IsNullOrWhiteSpace(CompoundPath)
            ? string.Empty
            : Path.GetFileName(CompoundPath) + ":\\" + internalPath.Replace('/', '\\');
        public override IReadOnlyList<BrowserItemInfo> Items => visibleItems;
        public override IReadOnlyList<BrowserItemInfo> SelectedItems => compoundView.SelectedItems
            .Cast<ListViewItem>()
            .Select(item => item.Tag as CompoundViewItem)
            .Where(item => item != null && !item.IsParent)
            .Select(ToBrowserItemInfo)
            .ToArray();
        public override BrowserPanelCapabilities Capabilities => BrowserPanelCapabilities.ReadOnlyVirtual;
        public string CurrentItemName => SelectedItems.Count == 1 ? SelectedItems[0].Name : null;
        public string SelectedLocation => SelectedItems.Count == 1 ? SelectedItems[0].Location : null;
        public string[] SelectedStreamPaths => compoundView.SelectedItems.Cast<ListViewItem>()
            .Select(item => item.Tag as CompoundViewItem)
            .Where(item => item != null && !item.IsParent && !item.IsStorage)
            .Select(item => item.FullPath)
            .ToArray();

        public void FocusItems() => compoundView.Focus();

        public void ApplyUserSettings(Font browserFont, FileBrowser.BrowserColumnWidths widths, System.Windows.Forms.View view)
        {
            compoundView.Font = browserFont;
            addressBar.ApplyDisplayFont(browserFont);
            addressBar.Height = Math.Max(30, browserFont.Height + 8);
            compoundView.Columns[1].Width = Math.Max(80, widths.NameWidth);
            compoundView.Columns[2].Width = Math.Max(50, widths.TypeWidth);
            compoundView.Columns[3].Width = Math.Max(60, widths.SizeWidth);
            compoundView.Columns[4].Width = Math.Max(80, widths.DateWidth);
            compoundView.View = view == System.Windows.Forms.View.Details ? view : System.Windows.Forms.View.Details;
        }

        public async Task<bool> OpenCompoundAsync(string path, bool reportFailure = true)
        {
            CancelCurrentOperation();
            operationCancellation = new CancellationTokenSource();
            CompoundPath = Path.GetFullPath(path);
            SetLoading(true, Language.getString("compoundReading"));
            frmWait waitForm = null;
            try
            {
                Task<CompoundFileCatalog> loadTask = catalogService.ReadCatalogAsync(CompoundPath, operationCancellation.Token);
                if (await Task.WhenAny(loadTask, Task.Delay(1000)) != loadTask &&
                    !IsDisposed && FindForm() is Form owner && !owner.IsDisposed)
                {
                    waitForm = new frmWait(Language.getString("compoundReading"));
                    waitForm.Show(owner);
                    waitForm.Refresh();
                }

                CompoundFileCatalog loaded = await loadTask;
                catalog.Clear();
                catalog.AddRange(loaded.Entries);
                DocumentType = loaded.DocumentType;
                internalPath = string.Empty;
                RenderCurrentPath();
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
                    LogService.LogException("CompoundBrowser.OpenCompoundAsync", ex);
                    MessageBox.Show(FindForm(), Language.getString("compoundOpenFailed") + Environment.NewLine + ex.Message,
                        Language.getString("error"), MessageBoxButtons.OK, MessageBoxIcon.Error);
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
            string normalized = NormalizePath(location);
            if (!StorageExists(normalized))
                return false;
            internalPath = normalized;
            RenderCurrentPath();
            return true;
        }

        public override bool NavigateParent()
        {
            if (string.IsNullOrEmpty(internalPath))
            {
                LeaveCompoundRequested?.Invoke(this, EventArgs.Empty);
                return true;
            }

            string child = internalPath;
            int separator = internalPath.LastIndexOf('/');
            internalPath = separator < 0 ? string.Empty : internalPath.Substring(0, separator);
            RenderCurrentPath(child);
            return true;
        }

        public override void RefreshPanel()
        {
            if (!string.IsNullOrWhiteSpace(CompoundPath))
                _ = OpenCompoundAsync(CompoundPath);
        }

        public async Task<string> MaterializeSelectedStreamAsync()
        {
            string selected = SelectedStreamPaths.Length == 1 ? SelectedStreamPaths[0] : null;
            if (selected == null)
                return null;
            operationCancellation ??= new CancellationTokenSource();
            return await catalogService.MaterializeStreamAsync(CompoundPath, selected, operationCancellation.Token);
        }

        public async Task<string[]> MaterializeSelectedStreamsAsync()
        {
            string[] selected = SelectedStreamPaths;
            if (selected.Length == 0)
                return Array.Empty<string>();
            operationCancellation ??= new CancellationTokenSource();
            var paths = new List<string>(selected.Length);
            foreach (string streamPath in selected)
            {
                paths.Add(await catalogService.MaterializeStreamAsync(CompoundPath, streamPath, operationCancellation.Token));
            }
            return paths.Where(path => !string.IsNullOrWhiteSpace(path)).ToArray();
        }

        public void SelectLocation(string location)
        {
            ListViewItem item = compoundView.Items.Cast<ListViewItem>().FirstOrDefault(candidate =>
                candidate.Tag is CompoundViewItem viewItem &&
                string.Equals(viewItem.FullPath, location, StringComparison.Ordinal));
            if (item != null)
            {
                item.Selected = true;
                item.Focused = true;
                item.EnsureVisible();
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                CancelCurrentOperation();
                catalogService.Dispose();
            }
            base.Dispose(disposing);
        }

        private async void ActivateSelectedItem()
        {
            CompoundViewItem selected = compoundView.SelectedItems.Count == 1
                ? compoundView.SelectedItems[0].Tag as CompoundViewItem
                : null;
            if (selected == null)
                return;
            if (selected.IsParent)
                NavigateParent();
            else if (selected.IsStorage)
                Navigate(selected.FullPath);
            else
            {
                try
                {
                    SetLoading(true, Language.getString("compoundOpeningStream"));
                    operationCancellation ??= new CancellationTokenSource();
                    string materialized = await catalogService.MaterializeStreamAsync(
                        CompoundPath,
                        selected.FullPath,
                        operationCancellation.Token);
                    if (!string.IsNullOrWhiteSpace(materialized))
                        WinContextMenu.Open(materialized);
                }
                catch (OperationCanceledException)
                {
                }
                catch (Exception ex)
                {
                    LogService.LogException("CompoundBrowser.ActivateSelectedItem", ex);
                    MessageBox.Show(FindForm(), ex.Message, Language.getString("error"), MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
                finally
                {
                    SetLoading(false, string.Empty);
                }
            }
        }

        private void CompoundView_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Back)
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

        private void RenderCurrentPath(string preferredEntry = null)
        {
            compoundView.BeginUpdate();
            try
            {
                compoundView.Items.Clear();
                visibleItems.Clear();
                compoundView.Items.Add(CreateListViewItem(new CompoundViewItem
                {
                    Name = "..",
                    IsStorage = true,
                    IsParent = true
                }));

                foreach (CompoundCatalogEntry entry in catalog
                    .Where(item => string.Equals(item.ParentPath, internalPath, StringComparison.Ordinal))
                    .OrderByDescending(item => item.IsStorage)
                    .ThenBy(item => item.Name, StringComparer.CurrentCultureIgnoreCase))
                {
                    var viewItem = new CompoundViewItem
                    {
                        Name = entry.Name,
                        FullPath = entry.FullPath,
                        IsStorage = entry.IsStorage,
                        Size = entry.Size,
                        Modified = entry.Modified
                    };
                    compoundView.Items.Add(CreateListViewItem(viewItem));
                    visibleItems.Add(ToBrowserItemInfo(viewItem));
                }
            }
            finally
            {
                compoundView.EndUpdate();
            }

            addressBar.Path = DisplayLocation;
            RaiseLocationChanged(DisplayLocation);
            ListViewItem preferred = string.IsNullOrWhiteSpace(preferredEntry) ? null : compoundView.Items.Cast<ListViewItem>()
                .FirstOrDefault(item => item.Tag is CompoundViewItem viewItem &&
                    string.Equals(viewItem.FullPath, preferredEntry, StringComparison.Ordinal));
            if (preferred != null)
            {
                preferred.Selected = true;
                preferred.Focused = true;
                preferred.EnsureVisible();
            }
            else if (compoundView.Items.Count > 0)
            {
                compoundView.Items[0].Selected = true;
            }
            compoundView.Focus();
        }

        private ListViewItem CreateListViewItem(CompoundViewItem item)
        {
            var listItem = new ListViewItem(string.Empty) { Tag = item, ImageIndex = -1 };
            listItem.SubItems.Add(item.Name);
            listItem.SubItems.Add(item.IsStorage ? Language.getString("compoundStorage") : Language.getString("compoundStream"));
            listItem.SubItems.Add(item.IsStorage || !item.Size.HasValue ? string.Empty : item.Size.Value.ToString("N0", CultureInfo.CurrentCulture));
            listItem.SubItems.Add(item.Modified?.ToString("g", CultureInfo.CurrentCulture) ?? string.Empty);
            return listItem;
        }

        private static BrowserItemInfo ToBrowserItemInfo(CompoundViewItem item)
        {
            return new BrowserItemInfo
            {
                Name = item.Name,
                Location = item.FullPath,
                IsDirectory = item.IsStorage,
                Size = item.Size,
                Modified = item.Modified
            };
        }

        private bool StorageExists(string path)
        {
            return string.IsNullOrEmpty(path) || catalog.Any(entry =>
                entry.IsStorage && string.Equals(entry.FullPath, path, StringComparison.Ordinal));
        }

        private void AddressBar_ButtonClick(object sender, EventArgs e)
        {
            string target = (sender as Control)?.Tag as string;
            if (string.IsNullOrWhiteSpace(target))
                return;
            string rootCaption = Path.GetFileName(CompoundPath) + ":\\";
            if (!target.StartsWith(rootCaption, StringComparison.OrdinalIgnoreCase))
                return;
            Navigate(NormalizePath(target.Substring(rootCaption.Length)));
        }

        private static string NormalizePath(string path)
        {
            return (path ?? string.Empty).Replace('\\', '/').Trim('/');
        }

        private void SetLoading(bool loading, string text)
        {
            loadingLabel.Text = text ?? string.Empty;
            loadingLabel.Visible = loading;
            compoundView.Visible = !loading;
            if (loading)
                loadingLabel.BringToFront();
            else
                compoundView.BringToFront();
        }

        private void CancelCurrentOperation()
        {
            operationCancellation?.Cancel();
            operationCancellation?.Dispose();
            operationCancellation = null;
        }

        private sealed class CompoundViewItem
        {
            public string Name { get; set; }
            public string FullPath { get; set; }
            public bool IsStorage { get; set; }
            public bool IsParent { get; set; }
            public long? Size { get; set; }
            public DateTime? Modified { get; set; }
        }
    }
}
