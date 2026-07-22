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
    internal sealed class ArchiveBrowser : BrowserPanelBase
    {
        private readonly TextBox locationBox;
        private readonly ListView archiveView;
        private readonly Label loadingLabel;
        private readonly List<ArchiveCatalogEntry> catalog = new List<ArchiveCatalogEntry>();
        private readonly List<BrowserItemInfo> visibleItems = new List<BrowserItemInfo>();
        private CancellationTokenSource loadCancellation;
        private string internalPath = string.Empty;
        private ImageList smallImages;
        private ImageList largeImages;
        private int iconGeneration;

        public ArchiveBrowser()
        {
            BackColor = Color.White;

            var locationPanel = new Panel
            {
                BackColor = SystemColors.Control,
                Dock = DockStyle.Top,
                Height = 32,
                Padding = new Padding(5, 3, 5, 3)
            };
            locationBox = new TextBox
            {
                BackColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle,
                Dock = DockStyle.Fill,
                ReadOnly = true
            };
            locationPanel.Controls.Add(locationBox);

            archiveView = new ListView
            {
                AllowColumnReorder = true,
                Dock = DockStyle.Fill,
                FullRowSelect = true,
                HideSelection = false,
                MultiSelect = true,
                UseCompatibleStateImageBehavior = false,
                View = System.Windows.Forms.View.Details
            };
            archiveView.Columns.Add(string.Empty, 20);
            archiveView.Columns.Add(Language.getString("columnName"), 240);
            archiveView.Columns.Add(Language.getString("columnType"), 80);
            archiveView.Columns.Add(Language.getString("columnSize"), 100, HorizontalAlignment.Right);
            archiveView.Columns.Add(Language.getString("columnDate"), 140);
            archiveView.ItemActivate += ArchiveView_ItemActivate;
            archiveView.SelectedIndexChanged += (_, __) => RaiseSelectionChanged();
            archiveView.KeyDown += ArchiveView_KeyDown;

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
            Controls.Add(archiveView);
            Controls.Add(locationPanel);
        }

        public event EventHandler LeaveArchiveRequested;
        public event EventHandler NavigateBackRequested;

        public string ArchivePath { get; private set; }
        public string InternalPath => internalPath;
        public override string DisplayLocation => string.IsNullOrWhiteSpace(ArchivePath)
            ? string.Empty
            : Path.GetFileName(ArchivePath) + ":\\" + internalPath.Replace('/', '\\');
        public override IReadOnlyList<BrowserItemInfo> Items => visibleItems;
        public override IReadOnlyList<BrowserItemInfo> SelectedItems => archiveView.SelectedItems
            .Cast<ListViewItem>()
            .Select(item => item.Tag as ArchiveViewItem)
            .Where(item => item != null && !item.IsParent)
            .Select(ToBrowserItemInfo)
            .ToArray();
        public override BrowserPanelCapabilities Capabilities => BrowserPanelCapabilities.ReadOnlyVirtual;

        public string[] SelectedEntryNames => archiveView.SelectedItems
            .Cast<ListViewItem>()
            .Select(item => item.Tag as ArchiveViewItem)
            .Where(item => item != null && !item.IsParent)
            .Select(item => item.FullName)
            .ToArray();

        public void SetImageLists(ImageList smallImageList, ImageList largeImageList)
        {
            smallImages = smallImageList;
            largeImages = largeImageList;
            archiveView.SmallImageList = smallImages;
            archiveView.LargeImageList = largeImages;
        }

        public void SelectEntry(string fullName)
        {
            if (string.IsNullOrWhiteSpace(fullName))
            {
                return;
            }

            ListViewItem item = archiveView.Items.Cast<ListViewItem>().FirstOrDefault(candidate =>
                candidate.Tag is ArchiveViewItem archiveItem &&
                string.Equals(archiveItem.FullName, fullName, StringComparison.OrdinalIgnoreCase));
            if (item != null)
            {
                item.Selected = true;
                item.Focused = true;
                item.EnsureVisible();
            }
        }

        public async Task<bool> OpenArchiveAsync(string archivePath)
        {
            loadCancellation?.Cancel();
            loadCancellation?.Dispose();
            loadCancellation = new CancellationTokenSource();
            ArchivePath = Path.GetFullPath(archivePath);
            internalPath = string.Empty;
            SetLoading(true, Language.getString("archiveReadingCatalog"));

            try
            {
                IReadOnlyList<ArchiveCatalogEntry> entries = await ArchiveCatalogService.ReadCatalogAsync(
                    ArchivePath,
                    loadCancellation.Token);
                catalog.Clear();
                catalog.AddRange(entries);
                RenderCurrentPath();
                return true;
            }
            catch (OperationCanceledException)
            {
                return false;
            }
            catch (Exception ex)
            {
                LogService.LogException("ArchiveBrowser.OpenArchiveAsync", ex);
                MessageBox.Show(FindForm(), ex.Message, Language.getString("error"), MessageBoxButtons.OK, MessageBoxIcon.Error);
                return false;
            }
            finally
            {
                SetLoading(false, string.Empty);
            }
        }

        public override bool Navigate(string location)
        {
            string normalized = NormalizeInternalPath(location);
            if (!DirectoryExists(normalized))
            {
                return false;
            }

            internalPath = normalized;
            RenderCurrentPath();
            return true;
        }

        public override bool NavigateParent()
        {
            if (string.IsNullOrEmpty(internalPath))
            {
                LeaveArchiveRequested?.Invoke(this, EventArgs.Empty);
                return true;
            }

            string childPath = internalPath;
            int separator = internalPath.LastIndexOf('/');
            internalPath = separator < 0 ? string.Empty : internalPath.Substring(0, separator);
            RenderCurrentPath(childPath);
            return true;
        }

        public override void RefreshPanel()
        {
            _ = RefreshAsync();
        }

        public async Task<string> MaterializeSelectedFileAsync()
        {
            ArchiveViewItem selected = archiveView.SelectedItems.Count == 1
                ? archiveView.SelectedItems[0].Tag as ArchiveViewItem
                : null;
            if (selected == null || selected.IsDirectory || selected.IsParent)
            {
                return null;
            }

            return await ArchiveCatalogService.MaterializeFileAsync(
                ArchivePath,
                selected.FullName,
                CancellationToken.None);
        }

        public void ApplyUserSettings(Font browserFont, FileBrowser.BrowserColumnWidths widths, System.Windows.Forms.View view)
        {
            if (browserFont != null)
            {
                Font = browserFont;
                archiveView.Font = browserFont;
                locationBox.Font = browserFont;
            }

            archiveView.Columns[1].Width = widths.NameWidth;
            archiveView.Columns[2].Width = widths.TypeWidth;
            archiveView.Columns[3].Width = widths.SizeWidth;
            archiveView.Columns[4].Width = widths.DateWidth;
            archiveView.View = view;
            UpdateItemTextForView();
            QueueIconLoading();
        }

        private async Task RefreshAsync()
        {
            string pathToRestore = internalPath;
            if (await OpenArchiveAsync(ArchivePath))
            {
                Navigate(pathToRestore);
            }
        }

        private async void ArchiveView_ItemActivate(object sender, EventArgs e)
        {
            if (archiveView.SelectedItems.Count != 1)
            {
                return;
            }

            ArchiveViewItem selected = archiveView.SelectedItems[0].Tag as ArchiveViewItem;
            if (selected == null)
            {
                return;
            }

            if (selected.IsParent)
            {
                NavigateParent();
            }
            else if (selected.IsDirectory)
            {
                Navigate(selected.FullName);
            }
            else
            {
                try
                {
                    SetLoading(true, Language.getString("archiveOpeningEntry"));
                    string materializedPath = await ArchiveCatalogService.MaterializeFileAsync(
                        ArchivePath,
                        selected.FullName,
                        CancellationToken.None);
                    WinContextMenu.Open(materializedPath);
                }
                catch (Exception ex)
                {
                    LogService.LogException("ArchiveBrowser.ArchiveView_ItemActivate", ex);
                    MessageBox.Show(FindForm(), ex.Message, Language.getString("error"), MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
                finally
                {
                    SetLoading(false, string.Empty);
                }
            }
        }

        private void ArchiveView_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Back)
            {
                NavigateBackRequested?.Invoke(this, EventArgs.Empty);
                e.Handled = true;
                e.SuppressKeyPress = true;
            }
            else if (e.KeyCode == Keys.Up && e.Alt)
            {
                NavigateParent();
                e.Handled = true;
                e.SuppressKeyPress = true;
            }
        }

        private void RenderCurrentPath(string preferredEntry = null)
        {
            iconGeneration++;
            archiveView.BeginUpdate();
            try
            {
                archiveView.Items.Clear();
                visibleItems.Clear();
                var parentItem = new ArchiveViewItem { Name = "..", IsDirectory = true, IsParent = true };
                archiveView.Items.Add(CreateListViewItem(parentItem));

                foreach (ArchiveViewItem item in BuildChildren(internalPath)
                    .OrderByDescending(item => item.IsDirectory)
                    .ThenBy(item => item.Name, StringComparer.CurrentCultureIgnoreCase))
                {
                    archiveView.Items.Add(CreateListViewItem(item));
                    visibleItems.Add(ToBrowserItemInfo(item));
                }
            }
            finally
            {
                archiveView.EndUpdate();
            }

            locationBox.Text = DisplayLocation;
            RaiseLocationChanged(DisplayLocation);
            ListViewItem itemToSelect = string.IsNullOrWhiteSpace(preferredEntry)
                ? null
                : archiveView.Items.Cast<ListViewItem>().FirstOrDefault(item =>
                    item.Tag is ArchiveViewItem archiveItem &&
                    string.Equals(archiveItem.FullName, preferredEntry, StringComparison.OrdinalIgnoreCase));
            if (itemToSelect != null)
            {
                itemToSelect.Selected = true;
                itemToSelect.Focused = true;
                itemToSelect.EnsureVisible();
            }
            else if (archiveView.Items.Count > 0)
            {
                archiveView.Items[0].Selected = true;
            }

            archiveView.Focus();
            QueueIconLoading();
        }

        private IEnumerable<ArchiveViewItem> BuildChildren(string path)
        {
            string prefix = string.IsNullOrEmpty(path) ? string.Empty : path.TrimEnd('/') + "/";
            var children = new Dictionary<string, ArchiveViewItem>(StringComparer.OrdinalIgnoreCase);
            foreach (ArchiveCatalogEntry entry in catalog)
            {
                if (!entry.FullName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                string remainder = entry.FullName.Substring(prefix.Length);
                if (string.IsNullOrEmpty(remainder))
                {
                    continue;
                }

                int separator = remainder.IndexOf('/');
                string childName = separator < 0 ? remainder : remainder.Substring(0, separator);
                string childPath = prefix + childName;
                bool isDirectory = separator >= 0 || entry.IsDirectory;
                if (!children.TryGetValue(childName, out ArchiveViewItem child))
                {
                    child = new ArchiveViewItem
                    {
                        Name = childName,
                        FullName = childPath,
                        IsDirectory = isDirectory,
                        Length = isDirectory ? 0 : entry.Length,
                        Modified = entry.Modified
                    };
                    children.Add(childName, child);
                }
                else if (isDirectory)
                {
                    child.IsDirectory = true;
                    child.Length = 0;
                }
            }

            return children.Values;
        }

        private ListViewItem CreateListViewItem(ArchiveViewItem item)
        {
            var listItem = new ListViewItem(archiveView.View == System.Windows.Forms.View.Details ? string.Empty : item.Name) { Tag = item };
            listItem.SubItems.Add(item.Name);
            listItem.SubItems.Add(item.IsDirectory
                ? Language.getString("archiveFolderType")
                : Path.GetExtension(item.Name).TrimStart('.').ToUpperInvariant());
            listItem.SubItems.Add(item.IsDirectory ? string.Empty : item.Length.ToString("N0", CultureInfo.CurrentCulture));
            listItem.SubItems.Add(item.Modified?.ToString("g", CultureInfo.CurrentCulture) ?? string.Empty);
            return listItem;
        }

        private void QueueIconLoading()
        {
            if (!Properties.Settings.Default.FileBrowserLoadIcons || smallImages == null || largeImages == null)
            {
                return;
            }

            int generation = iconGeneration;
            bool loadLarge = Properties.Settings.Default.FileBrowserLoadLargeIcons &&
                (archiveView.View == System.Windows.Forms.View.LargeIcon || archiveView.View == System.Windows.Forms.View.Tile);
            var batches = archiveView.Items.Cast<ListViewItem>()
                .Where(item => item.Tag is ArchiveViewItem)
                .GroupBy(item =>
                {
                    ArchiveViewItem archiveItem = (ArchiveViewItem)item.Tag;
                    return FileIconCache.GetTypeKey(archiveItem.Name, archiveItem.IsDirectory);
                }, StringComparer.OrdinalIgnoreCase)
                .Select(group => new ArchiveIconBatch
                {
                    Items = group.ToArray(),
                    Item = (ArchiveViewItem)group.First().Tag
                })
                .ToArray();

            Task.Run(() =>
            {
                foreach (ArchiveIconBatch batch in batches)
                {
                    FileIconData smallIcon = FileIconCache.GetIconData(batch.Item.Name, batch.Item.IsDirectory, false);
                    FileIconData largeIcon = loadLarge
                        ? FileIconCache.GetIconData(batch.Item.Name, batch.Item.IsDirectory, true)
                        : null;
                    if (smallIcon == null && largeIcon == null)
                    {
                        continue;
                    }

                    if (!IsHandleCreated)
                    {
                        smallIcon?.Dispose();
                        largeIcon?.Dispose();
                        return;
                    }

                    try
                    {
                        BeginInvoke(new Action(() => ApplyIconResult(generation, batch.Items, smallIcon, largeIcon)));
                    }
                    catch (InvalidOperationException)
                    {
                        smallIcon?.Dispose();
                        largeIcon?.Dispose();
                        return;
                    }
                }
            });
        }

        private void ApplyIconResult(int generation, ListViewItem[] items, FileIconData smallIcon, FileIconData largeIcon)
        {
            using (smallIcon)
            using (largeIcon)
            {
                string key = smallIcon?.Key ?? largeIcon?.Key;
                if (generation != iconGeneration || string.IsNullOrWhiteSpace(key))
                {
                    return;
                }

                int imageIndex = smallImages.Images.IndexOfKey(key);
                if (imageIndex < 0)
                {
                    Icon smallToStore = smallIcon != null ? (Icon)smallIcon.Icon.Clone() : (Icon)largeIcon.Icon.Clone();
                    Icon largeToStore = largeIcon != null ? (Icon)largeIcon.Icon.Clone() : (Icon)smallIcon.Icon.Clone();
                    smallImages.Images.Add(key, smallToStore);
                    largeImages.Images.Add(key, largeToStore);
                    imageIndex = smallImages.Images.Count - 1;
                }
                else if (largeIcon != null)
                {
                    using Bitmap replacement = largeIcon.Icon.ToBitmap();
                    largeImages.Images[imageIndex] = replacement;
                }

                foreach (ListViewItem item in items)
                {
                    if (item.ListView == archiveView)
                    {
                        item.ImageIndex = imageIndex;
                    }
                }
            }
        }

        private void UpdateItemTextForView()
        {
            foreach (ListViewItem item in archiveView.Items)
            {
                ArchiveViewItem archiveItem = item.Tag as ArchiveViewItem;
                if (archiveItem != null)
                {
                    item.Text = archiveView.View == System.Windows.Forms.View.Details ? string.Empty : archiveItem.Name;
                }
            }
        }

        private static BrowserItemInfo ToBrowserItemInfo(ArchiveViewItem item)
        {
            return new BrowserItemInfo
            {
                Name = item.Name,
                Location = item.FullName,
                IsDirectory = item.IsDirectory,
                Size = item.IsDirectory ? null : item.Length,
                Modified = item.Modified
            };
        }

        private bool DirectoryExists(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                return true;
            }

            string prefix = path.TrimEnd('/') + "/";
            return catalog.Any(entry =>
                entry.IsDirectory && string.Equals(entry.FullName, path, StringComparison.OrdinalIgnoreCase) ||
                entry.FullName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
        }

        private void SetLoading(bool loading, string text)
        {
            loadingLabel.Text = text ?? string.Empty;
            loadingLabel.Visible = loading;
            archiveView.Visible = !loading;
            if (loading)
            {
                loadingLabel.BringToFront();
            }
        }

        private static string NormalizeInternalPath(string value)
        {
            return (value ?? string.Empty).Replace('\\', '/').Trim('/');
        }

        private sealed class ArchiveViewItem
        {
            public string Name { get; set; }
            public string FullName { get; set; }
            public bool IsDirectory { get; set; }
            public bool IsParent { get; set; }
            public long Length { get; set; }
            public DateTime? Modified { get; set; }
        }

        private sealed class ArchiveIconBatch
        {
            public ArchiveViewItem Item { get; set; }
            public ListViewItem[] Items { get; set; }
        }
    }
}
