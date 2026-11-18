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
        private readonly BufferedListView compoundView;
        private readonly CompoundMetadataService metadataService = new CompoundMetadataService();
        private readonly Label loadingLabel;
        private readonly List<CompoundCatalogEntry> catalog = new List<CompoundCatalogEntry>();
        private readonly List<BrowserItemInfo> visibleItems = new List<BrowserItemInfo>();
        private CancellationTokenSource operationCancellation;
        private CancellationTokenSource metadataCancellation;
        private string internalPath = string.Empty;

        public CompoundBrowser()
        {
            BackColor = Color.White;
            addressBar = new AddressBar { Dock = DockStyle.Top, Height = 32 };
            addressBar.BackClick += (_, __) =>
            {
                CancelMetadataRead();
                NavigateBackRequested?.Invoke(this, EventArgs.Empty);
            };
            addressBar.ParentClick += (_, __) => NavigateParent();
            addressBar.RefreshClick += (_, __) => RefreshPanel();
            addressBar.ButtonClick += AddressBar_ButtonClick;

            compoundView = new BufferedListView
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
            compoundView.MouseDown += CompoundView_MouseDown;
            compoundView.ContextMenuStrip = CreateContextMenu();

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
        public event EventHandler EmbeddedPayloadExportRequested;

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
        public (string FullPath, string Name, bool IsStorage)[] SelectedCompoundExportItems => compoundView.SelectedItems
            .Cast<ListViewItem>()
            .Select(item => item.Tag as CompoundViewItem)
            .Where(item => item != null && !item.IsParent)
            .Select(item => (item.FullPath, item.Name, item.IsStorage))
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
            BrowserRowColoring.Apply(compoundView, Properties.Settings.Default.BrowserRowColorMode);
        }

        public async Task<bool> OpenCompoundAsync(string path, bool reportFailure = true)
        {
            CancelCurrentOperation();
            CancelMetadataRead();
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
                    if (waitForm != null && !waitForm.IsDisposed)
                    {
                        waitForm.Close();
                        waitForm.Dispose();
                        waitForm = null;
                    }

                    using var integrityDialog = new FormCompoundIntegrity(CompoundPath, ex, ExportReachableStreamsAsync);
                    integrityDialog.ShowDialog(FindForm());
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
            CancelMetadataRead();
            internalPath = normalized;
            RenderCurrentPath();
            return true;
        }

        public override bool NavigateParent()
        {
            CancelMetadataRead();
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

        private void ShowIntegrityReport()
        {
            if (string.IsNullOrWhiteSpace(CompoundPath) || loadingLabel.Visible)
                return;

            using var dialog = new FormCompoundIntegrity(CompoundPath, recoveryAction: ExportReachableStreamsAsync);
            dialog.ShowDialog(FindForm());
        }

        private async Task<CompoundRecoveryResult> ExportReachableStreamsAsync(
            string destination,
            IProgress<CompoundRecoveryProgress> progress,
            CancellationToken cancellationToken)
        {
            using IDisposable exportSession = catalogService.RetainSession();
            CompoundRecoveryResult result = await catalogService.MaterializeBestEffortRecoveryAsync(
                CompoundPath,
                progress,
                cancellationToken);
            if (string.IsNullOrWhiteSpace(result.StagedRootPath))
                return result;

            try
            {
                using var copyWindow = new FormCopy(
                    new[] { result.StagedRootPath },
                    destination,
                    FormCopy.Type.Copy,
                    allowOverwrite: false);
                copyWindow.ActionComplete += code => result.DestinationCopyResultCode = code;
                copyWindow.ShowDialog(FindForm());
                result.DestinationCopyStarted = true;
                return result;
            }
            finally
            {
                catalogService.ReleaseExportFiles(new[] { result.StagedRootPath });
            }
        }

        public async Task<string> MaterializeSelectedStreamAsync()
        {
            string selected = SelectedStreamPaths.Length == 1 ? SelectedStreamPaths[0] : null;
            if (selected == null)
                return null;
            operationCancellation ??= new CancellationTokenSource();
            return await catalogService.MaterializeStreamAsync(CompoundPath, selected, operationCancellation.Token);
        }

        public async Task<string[]> MaterializeSelectedEntriesAsync()
        {
            var selected = SelectedCompoundExportItems;
            if (selected.Length == 0)
                return Array.Empty<string>();

            operationCancellation ??= new CancellationTokenSource();
            CancellationToken cancellationToken = operationCancellation.Token;
            CompoundCatalogEntry[] catalogSnapshot = catalog.ToArray();
            await Task.Run(
                () => ValidateSelectedExport(catalogSnapshot, selected, cancellationToken),
                cancellationToken).ConfigureAwait(false);

            var paths = new List<string>(selected.Length);
            try
            {
                long materializedBytes = 0;
                foreach (var item in selected)
                {
                    string path = item.IsStorage
                        ? await catalogService.MaterializeStorageForExportAsync(CompoundPath, item.FullPath, cancellationToken).ConfigureAwait(false)
                        : await catalogService.MaterializeStreamForExportAsync(CompoundPath, item.FullPath, cancellationToken).ConfigureAwait(false);
                    paths.Add(path);
                    materializedBytes = AddMaterializedBytes(path, materializedBytes);
                }
            }
            catch
            {
                catalogService.ReleaseExportFiles(paths);
                throw;
            }
            return paths.Where(path => !string.IsNullOrWhiteSpace(path)).ToArray();
        }

        public async Task<string> MaterializeSelectedEmbeddedPayloadAsync()
        {
            if (!HasSingleSelectedEmbeddedPayload())
                return null;

            CompoundViewItem selected = compoundView.SelectedItems[0].Tag as CompoundViewItem;
            operationCancellation ??= new CancellationTokenSource();
            return await catalogService.MaterializeEmbeddedPayloadForExportAsync(
                CompoundPath,
                selected.FullPath,
                operationCancellation.Token);
        }

        private static void ValidateSelectedExport(
            IReadOnlyList<CompoundCatalogEntry> catalogEntries,
            IReadOnlyList<(string FullPath, string Name, bool IsStorage)> selected,
            CancellationToken cancellationToken)
        {
            var safeNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var item in selected)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (!safeNames.Add(CompoundFileCatalogService.GetSafeExportFileName(item.Name)))
                    throw new InvalidDataException(Language.getString("compoundExportNameCollision"));
            }

            var selectedStreams = new HashSet<string>(
                selected.Where(item => !item.IsStorage).Select(item => item.FullPath),
                StringComparer.Ordinal);
            var selectedStorages = new HashSet<string>(
                selected.Where(item => item.IsStorage).Select(item => item.FullPath),
                StringComparer.Ordinal);
            long plannedBytes = 0;
            foreach (CompoundCatalogEntry entry in catalogEntries)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (entry.IsStorage || !IsSelectedForExport(entry.FullPath, selectedStreams, selectedStorages))
                    continue;

                long size = entry.Size ?? 0;
                if (size < 0
                    || size > CompoundFileCatalogService.MaximumExportStreamBytes
                    || plannedBytes > CompoundFileCatalogService.MaximumExportTotalBytes - size)
                    throw new InvalidDataException(Language.getString("compoundExportLimitExceeded"));
                plannedBytes += size;
            }
        }

        private static bool IsSelectedForExport(
            string entryPath,
            ISet<string> selectedStreams,
            ISet<string> selectedStorages)
        {
            if (selectedStreams.Contains(entryPath))
                return true;

            int separator = entryPath.LastIndexOf('/');
            while (separator >= 0)
            {
                if (selectedStorages.Contains(entryPath.Substring(0, separator)))
                    return true;
                separator = entryPath.LastIndexOf('/', separator - 1);
            }

            return false;
        }

        private static long AddMaterializedBytes(string path, long currentTotal)
        {
            IEnumerable<string> files = File.Exists(path)
                ? new[] { path }
                : Directory.Exists(path)
                    ? Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories)
                    : throw new FileNotFoundException("The staged compound export item was not found.", path);

            foreach (string file in files)
            {
                long size = new FileInfo(file).Length;
                if (size > CompoundFileCatalogService.MaximumExportStreamBytes
                    || currentTotal > CompoundFileCatalogService.MaximumExportTotalBytes - size)
                    throw new InvalidDataException(Language.getString("compoundExportLimitExceeded"));
                currentTotal += size;
            }

            return currentTotal;
        }

        public IDisposable RetainExportSession() => catalogService.RetainSession();

        public void ReleaseExportFiles(IEnumerable<string> paths) => catalogService.ReleaseExportFiles(paths);

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
                CancelMetadataRead();
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
            else if (selected.IsEmbedded)
            {
                MessageBox.Show(
                    FindForm(),
                    Language.getString("compoundEmbeddedEnterBlocked"),
                    Language.getString("compoundExternalOpenTitle"),
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
            }
            else
            {
                await MaterializeAndOpenAsync(selected);
            }
        }

        private bool HasSingleSelectedStream()
        {
            return compoundView.SelectedItems.Count == 1
                && compoundView.SelectedItems[0].Tag is CompoundViewItem selected
                && !selected.IsParent
                && !selected.IsStorage;
        }

        private bool HasSingleSelectedEmbeddedPayload()
        {
            if (!HasSingleSelectedStream())
                return false;
            CompoundViewItem selected = compoundView.SelectedItems[0].Tag as CompoundViewItem;
            return CompoundFileCatalogService.IsOle10NativeStreamName(selected.Name)
                || string.Equals(selected.Name, "Package", StringComparison.OrdinalIgnoreCase);
        }

        private void CancelMetadataRead()
        {
            metadataCancellation?.Cancel();
        }

        private async Task ShowMetadataAsync()
        {
            if (string.IsNullOrWhiteSpace(CompoundPath) || loadingLabel.Visible || metadataCancellation != null)
                return;

            string path = CompoundPath;
            var cancellation = new CancellationTokenSource();
            metadataCancellation = cancellation;
            frmWait waitForm = null;
            try
            {
                Task<CompoundMetadataReport> readTask = metadataService.ReadAsync(path, cancellation.Token);
                if (await Task.WhenAny(readTask, Task.Delay(1000)) != readTask
                    && !IsDisposed && FindForm() is Form owner && !owner.IsDisposed)
                {
                    waitForm = new frmWait(Language.getString("compoundMetadataReading"));
                    waitForm.Show(owner);
                    waitForm.Refresh();
                }

                CompoundMetadataReport report = await readTask;
                if (waitForm != null && !waitForm.IsDisposed)
                {
                    waitForm.Close();
                    waitForm.Dispose();
                    waitForm = null;
                }
                if (!IsDisposed
                    && string.Equals(path, CompoundPath, StringComparison.OrdinalIgnoreCase)
                    && FindForm() is Form dialogOwner && !dialogOwner.IsDisposed)
                {
                    using var dialog = new FormCompoundMetadata(report);
                    dialog.ShowDialog(dialogOwner);
                }
            }
            catch (Exception ex)
            {
                if (ex is OperationCanceledException)
                    return;
                LogService.LogException("CompoundBrowser.ShowMetadata", ex);
                if (!IsDisposed && FindForm() is Form owner && !owner.IsDisposed)
                    MessageBox.Show(owner, Language.getString("compoundMetadataFailed") + Environment.NewLine + ex.Message,
                        Language.getString("error"), MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                if (waitForm != null && !waitForm.IsDisposed)
                {
                    waitForm.Close();
                    waitForm.Dispose();
                }
                metadataCancellation = null;
                cancellation.Dispose();
            }
        }

        private async Task MaterializeAndOpenAsync(CompoundViewItem stream)
        {
            try
            {
                SetLoading(true, Language.getString("compoundOpeningStream"));
                operationCancellation ??= new CancellationTokenSource();
                string materialized = await catalogService.MaterializeStreamAsync(
                    CompoundPath,
                    stream.FullPath,
                    operationCancellation.Token);
                if (!string.IsNullOrWhiteSpace(materialized))
                    WinContextMenu.Open(materialized);
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex)
            {
                LogService.LogException("CompoundBrowser.MaterializeAndOpenAsync", ex);
                MessageBox.Show(FindForm(), ex.Message, Language.getString("error"), MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                SetLoading(false, string.Empty);
            }
        }

        private void OpenSelectedExternally(object sender, EventArgs e)
        {
            CompoundViewItem selected = GetSingleSelectedStream();
            if (selected == null)
                return;

            if (selected.IsEmbedded)
            {
                DialogResult answer = MessageBox.Show(
                    FindForm(),
                    Language.getString("compoundExternalOpenConfirm"),
                    Language.getString("compoundExternalOpenTitle"),
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Warning);
                if (answer != DialogResult.Yes)
                    return;
            }

            _ = MaterializeAndOpenAsync(selected);
        }

        private CompoundViewItem GetSingleSelectedStream()
        {
            return compoundView.SelectedItems.Count == 1
                ? (compoundView.SelectedItems[0].Tag as CompoundViewItem) is CompoundViewItem item &&
                  !item.IsParent && !item.IsStorage
                    ? item
                    : null
                : null;
        }

        private ContextMenuStrip CreateContextMenu()
        {
            var menu = new ContextMenuStrip();
            var openExternalItem = new ToolStripMenuItem(Language.getString("compoundOpenExternally"));
            openExternalItem.Click += OpenSelectedExternally;
            menu.Items.Add(openExternalItem);
            var metadataItem = new ToolStripMenuItem(Language.getString("compoundMetadataMenu"));
            metadataItem.Click += async (_, __) => await ShowMetadataAsync();
            menu.Items.Add(metadataItem);
            var integrityItem = new ToolStripMenuItem(Language.getString("compoundIntegrityMenu"));
            integrityItem.Click += (_, __) => ShowIntegrityReport();
            menu.Items.Add(integrityItem);
            var exportPayloadItem = new ToolStripMenuItem(Language.getString("compoundExportPayload"));
            exportPayloadItem.Click += (_, __) => EmbeddedPayloadExportRequested?.Invoke(this, EventArgs.Empty);
            menu.Items.Add(exportPayloadItem);
            menu.Opening += (_, __) =>
            {
                openExternalItem.Enabled = GetSingleSelectedStream() != null;
                exportPayloadItem.Enabled = HasSingleSelectedEmbeddedPayload();
            };
            return menu;
        }

        private void CompoundView_MouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Right)
                return;

            ListViewItem hit = compoundView.GetItemAt(e.X, e.Y);
            if (hit == null || hit.Selected)
                return;

            foreach (ListViewItem selected in compoundView.SelectedItems.Cast<ListViewItem>().ToArray())
                selected.Selected = false;
            hit.Selected = true;
            hit.Focused = true;
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
                        IsEmbedded = entry.IsEmbedded,
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
            BrowserRowColoring.Apply(compoundView, Properties.Settings.Default.BrowserRowColorMode);


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
            public bool IsEmbedded { get; set; }
            public long? Size { get; set; }
            public DateTime? Modified { get; set; }
        }
    }
}
