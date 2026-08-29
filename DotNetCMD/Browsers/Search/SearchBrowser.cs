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
    internal sealed class SearchBrowser : BrowserPanelBase
    {
        private readonly SearchService searchService = new SearchService();
        private readonly AddressBar addressBar;
        private readonly ListView resultView;
        private readonly Label statusLabel;
        private readonly Button cancelButton;
        private readonly List<BrowserItemInfo> visibleItems = new List<BrowserItemInfo>();
        private CancellationTokenSource searchCancellation;
        private SearchQuery currentQuery;
        private int searchGeneration;

        public SearchBrowser()
        {
            BackColor = SystemColors.Window;

            addressBar = new AddressBar
            {
                Dock = DockStyle.Top,
                Height = 32
            };
            addressBar.BackClick += (_, __) => LeaveRequested?.Invoke(this, EventArgs.Empty);
            addressBar.ParentClick += (_, __) => LeaveRequested?.Invoke(this, EventArgs.Empty);
            addressBar.RefreshClick += (_, __) => RefreshPanel();

            resultView = new ListView
            {
                AllowColumnReorder = true,
                Dock = DockStyle.Fill,
                FullRowSelect = true,
                HideSelection = false,
                MultiSelect = true,
                UseCompatibleStateImageBehavior = false,
                View = System.Windows.Forms.View.Details
            };
            resultView.Columns.Add(string.Empty, 22);
            resultView.Columns.Add(Language.getString("columnName"), 220);
            resultView.Columns.Add(Language.getString("searchColumnLocation"), 360);
            resultView.Columns.Add(Language.getString("columnType"), 80);
            resultView.Columns.Add(Language.getString("columnSize"), 100, HorizontalAlignment.Right);
            resultView.Columns.Add(Language.getString("columnDate"), 140);
            resultView.SelectedIndexChanged += (_, __) => RaiseSelectionChanged();
            resultView.ItemActivate += (_, __) => ActivateSelectedResult();
            resultView.KeyDown += ResultView_KeyDown;

            var statusPanel = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 28,
                BackColor = SystemColors.Control
            };
            cancelButton = new Button
            {
                Dock = DockStyle.Right,
                FlatStyle = FlatStyle.System,
                Text = Language.getString("cancel"),
                Width = 92,
                Visible = false
            };
            cancelButton.Click += (_, __) => searchCancellation?.Cancel();
            statusLabel = new Label
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(6, 0, 6, 0),
                TextAlign = ContentAlignment.MiddleLeft
            };
            statusPanel.Controls.Add(statusLabel);
            statusPanel.Controls.Add(cancelButton);

            Controls.Add(resultView);
            Controls.Add(statusPanel);
            Controls.Add(addressBar);
        }

        public event EventHandler LeaveRequested;
        public event EventHandler<SearchResultActivatedEventArgs> ResultActivated;

        public SearchQuery CurrentQuery => currentQuery?.Clone();
        public bool IsSearching => searchCancellation != null;
        public override string DisplayLocation => currentQuery == null
            ? Language.getString("searchResults")
            : string.Format(
                CultureInfo.CurrentCulture,
                Language.getString("searchDisplayLocationFormat"),
                currentQuery.NamePattern,
                currentQuery.ScopeDirectory);
        public override IReadOnlyList<BrowserItemInfo> Items => visibleItems;
        public override IReadOnlyList<BrowserItemInfo> SelectedItems => resultView.SelectedItems
            .Cast<ListViewItem>()
            .Select(item => item.Tag as BrowserItemInfo)
            .Where(item => item != null)
            .ToArray();
        public override BrowserPanelCapabilities Capabilities =>
            BrowserPanelCapabilities.Preview |
            BrowserPanelCapabilities.CopyOut |
            BrowserPanelCapabilities.Delete;

        public void SetImageLists(ImageList smallImages, ImageList largeImages)
        {
            resultView.SmallImageList = smallImages;
            resultView.LargeImageList = largeImages;
        }

        public void ApplyUserSettings(Font browserFont, FileBrowser.BrowserColumnWidths widths, System.Windows.Forms.View view)
        {
            if (browserFont != null)
            {
                resultView.Font = browserFont;
                addressBar.ApplyDisplayFont(browserFont);
                addressBar.Height = Math.Max(30, browserFont.Height + 8);
            }

            resultView.Columns[1].Width = Math.Max(100, widths.NameWidth);
            resultView.Columns[2].Width = Math.Max(180, widths.NameWidth + widths.TypeWidth);
            resultView.Columns[3].Width = Math.Max(60, widths.TypeWidth);
            resultView.Columns[4].Width = Math.Max(70, widths.SizeWidth);
            resultView.Columns[5].Width = Math.Max(90, widths.DateWidth);
            resultView.View = view == System.Windows.Forms.View.Details ? view : System.Windows.Forms.View.Details;
        }

        public async Task StartSearchAsync(SearchQuery query)
        {
            if (query == null)
                throw new ArgumentNullException(nameof(query));

            CancelSearch();
            currentQuery = query.Clone();
            int generation = ++searchGeneration;
            searchCancellation = new CancellationTokenSource();
            CancellationToken token = searchCancellation.Token;

            visibleItems.Clear();
            resultView.Items.Clear();
            addressBar.Path = currentQuery.ScopeDirectory;
            statusLabel.Text = Language.getString("searchStarting");
            cancelButton.Visible = true;
            RaiseLocationChanged(DisplayLocation);
            RaiseSelectionChanged();

            var progress = new Progress<SearchProgress>(value =>
            {
                if (generation != searchGeneration || IsDisposed)
                    return;
                statusLabel.Text = string.Format(
                    CultureInfo.CurrentCulture,
                    Language.getString("searchProgressFormat"),
                    value.ScannedItems,
                    value.FoundItems,
                    value.SkippedDirectories,
                    value.CurrentPath);
            });

            try
            {
                IReadOnlyList<BrowserItemInfo> results = await searchService.SearchAsync(currentQuery, progress, token);
                if (generation != searchGeneration || token.IsCancellationRequested)
                    return;

                visibleItems.AddRange(results);
                PopulateResults(results);
                statusLabel.Text = string.Format(
                    CultureInfo.CurrentCulture,
                    Language.getString("searchCompletedFormat"),
                    results.Count);
            }
            catch (OperationCanceledException)
            {
                if (generation == searchGeneration)
                    statusLabel.Text = Language.getString("searchCancelled");
            }
            catch (Exception ex)
            {
                LogService.LogException("SearchBrowser.StartSearchAsync", ex);
                if (generation == searchGeneration)
                {
                    statusLabel.Text = Language.getString("searchFailed");
                    MessageBox.Show(FindForm(), ex.Message, Language.getString("error"), MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
            finally
            {
                if (generation == searchGeneration)
                {
                    searchCancellation?.Dispose();
                    searchCancellation = null;
                    cancelButton.Visible = false;
                }
            }
        }

        public void CancelSearch()
        {
            searchGeneration++;
            searchCancellation?.Cancel();
            searchCancellation?.Dispose();
            searchCancellation = null;
            cancelButton.Visible = false;
        }

        public void FocusItems()
        {
            resultView.Focus();
        }

        public override bool Navigate(string location)
        {
            BrowserItemInfo item = visibleItems.FirstOrDefault(candidate =>
                string.Equals(candidate.NativePath, location, StringComparison.OrdinalIgnoreCase));
            if (item == null)
                return false;

            ListViewItem viewItem = resultView.Items.Cast<ListViewItem>()
                .FirstOrDefault(candidate => ReferenceEquals(candidate.Tag, item));
            if (viewItem == null)
                return false;

            viewItem.Selected = true;
            viewItem.Focused = true;
            viewItem.EnsureVisible();
            return true;
        }

        public override bool NavigateParent()
        {
            LeaveRequested?.Invoke(this, EventArgs.Empty);
            return true;
        }

        public override void RefreshPanel()
        {
            if (currentQuery != null)
                _ = StartSearchAsync(currentQuery);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
                CancelSearch();
            base.Dispose(disposing);
        }

        private void PopulateResults(IReadOnlyList<BrowserItemInfo> results)
        {
            resultView.BeginUpdate();
            try
            {
                resultView.Items.Clear();
                foreach (BrowserItemInfo result in results)
                {
                    string type = result.IsDirectory
                        ? Language.getString("archiveFolderType")
                        : Path.GetExtension(result.Name).TrimStart('.');
                    var item = new ListViewItem(string.Empty)
                    {
                        ImageIndex = -1,
                        Tag = result
                    };
                    item.SubItems.Add(result.Name);
                    item.SubItems.Add(Path.GetDirectoryName(result.NativePath) ?? string.Empty);
                    item.SubItems.Add(type);
                    item.SubItems.Add(result.Size.HasValue ? result.Size.Value.ToString("N0", CultureInfo.CurrentCulture) : string.Empty);
                    item.SubItems.Add(result.Modified?.ToString("g", CultureInfo.CurrentCulture) ?? string.Empty);
                    resultView.Items.Add(item);
                }

                if (resultView.Items.Count > 0)
                {
                    resultView.Items[0].Selected = true;
                    resultView.Items[0].Focused = true;
                }
            }
            finally
            {
                resultView.EndUpdate();
            }
        }

        private void ActivateSelectedResult()
        {
            BrowserItemInfo selected = SelectedItems.FirstOrDefault();
            if (selected != null)
                ResultActivated?.Invoke(this, new SearchResultActivatedEventArgs(selected.NativePath, selected.IsDirectory));
        }

        private void ResultView_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Escape)
            {
                if (IsSearching)
                    searchCancellation?.Cancel();
                else
                    LeaveRequested?.Invoke(this, EventArgs.Empty);
                e.Handled = true;
            }
        }
    }

    internal sealed class SearchResultActivatedEventArgs : EventArgs
    {
        public SearchResultActivatedEventArgs(string path, bool isDirectory)
        {
            Path = path;
            IsDirectory = isDirectory;
        }

        public string Path { get; }
        public bool IsDirectory { get; }
    }
}
