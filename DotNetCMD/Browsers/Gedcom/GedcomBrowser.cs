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
    internal sealed class GedcomRecordText
    {
        public string Text { get; set; }
        public string Title { get; set; }
        public string Tag { get; set; }
    }

    internal sealed class GedcomBrowser : BrowserPanelBase
    {
        private const string TagPathPrefix = "tag/";

        private readonly AddressBar addressBar;
        private readonly BufferedListView browserView;
        private readonly Label loadingLabel;
        private readonly ImageList sexIconsSmall;
        private readonly ImageList sexIconsLarge;
        private readonly List<BrowserItemInfo> visibleItems = new List<BrowserItemInfo>();
        private CancellationTokenSource loadCancellation;
        private GedcomCatalog catalog;
        private string internalPath = string.Empty;
        private int sortColumn = 2;
        private bool sortAscending = true;
        private FileBrowser.BrowserColumnWidths columnWidths;

        public GedcomBrowser()
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

            sexIconsSmall = CreateSexImageList(16);
            sexIconsLarge = CreateSexImageList(32);

            browserView = new BufferedListView
            {
                AllowColumnReorder = true,
                Dock = DockStyle.Fill,
                FullRowSelect = true,
                HideSelection = false,
                MultiSelect = true,
                UseCompatibleStateImageBehavior = false,
                View = System.Windows.Forms.View.Details,
                SmallImageList = sexIconsSmall,
                LargeImageList = sexIconsLarge
            };
            browserView.ItemActivate += (_, __) => ActivateSelectedItem();
            browserView.SelectedIndexChanged += (_, __) => RaiseSelectionChanged();
            browserView.ColumnClick += BrowserView_ColumnClick;
            browserView.KeyDown += BrowserView_KeyDown;

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
            Controls.Add(browserView);
            Controls.Add(addressBar);
        }

        public event EventHandler LeaveGedcomRequested;
        public event EventHandler NavigateBackRequested;

        public string GedcomPath { get; private set; }
        public string InternalPath => internalPath;
        public override string DisplayLocation => string.IsNullOrWhiteSpace(GedcomPath)
            ? string.Empty
            : Path.GetFileName(GedcomPath) + ":\\" + GetInternalDisplayPath();
        public override IReadOnlyList<BrowserItemInfo> Items => visibleItems;
        public override IReadOnlyList<BrowserItemInfo> SelectedItems => browserView.SelectedItems
            .Cast<ListViewItem>()
            .Select(item => item.Tag as ViewEntry)
            .Where(entry => entry != null && entry.TargetPath != "..")
            .Select(entry => entry.Info)
            .ToArray();
        public override BrowserPanelCapabilities Capabilities => BrowserPanelCapabilities.Navigate;
        public string CurrentItemName => GetCurrentEntry()?.Info.Name;
        public GedcomPersonEntry SelectedPerson => browserView.SelectedItems.Count == 1
            ? (browserView.SelectedItems[0].Tag as ViewEntry)?.Model as GedcomPersonEntry
            : null;

        public bool TryGetSelectedRecordText(out string text, out string title, out string tag)
        {
            text = null;
            title = null;
            tag = null;

            GedcomRecordText record = GetSelectedRecordTexts().FirstOrDefault();
            if (record == null)
                return false;

            text = record.Text;
            title = record.Title;
            tag = record.Tag;
            return true;
        }

        public IReadOnlyList<GedcomRecordText> GetSelectedRecordTexts()
        {
            return browserView.SelectedItems.Cast<ListViewItem>()
                .Select(item => item.Tag as ViewEntry)
                .Select(CreateRecordText)
                .Where(record => record != null)
                .ToArray();
        }

        internal void FocusItems()
        {
            browserView.Focus();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                loadCancellation?.Cancel();
                loadCancellation?.Dispose();
                loadCancellation = null;
                sexIconsSmall?.Dispose();
                sexIconsLarge?.Dispose();
            }
            base.Dispose(disposing);
        }

        public async Task<bool> OpenGedcomAsync(string gedcomPath)
        {
            loadCancellation?.Cancel();
            loadCancellation?.Dispose();
            loadCancellation = new CancellationTokenSource();
            GedcomPath = Path.GetFullPath(gedcomPath);
            SetLoading(true, Language.getString("gedcomReadingCatalog"));
            try
            {
                catalog = await GedcomCatalogService.ReadAsync(GedcomPath, loadCancellation.Token);
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
                LogService.LogException("GedcomBrowser.OpenGedcomAsync", ex);
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
            string target = NormalizeInternalPath(location);
            string tag = null;
            if (!string.IsNullOrEmpty(target) && !TryGetTagFromPath(target, out tag))
                return false;
            if (!string.IsNullOrEmpty(target) && !HasTag(tag))
                return false;

            internalPath = target;
            RenderCurrentLocation();
            return true;
        }

        public override bool NavigateParent()
        {
            if (string.IsNullOrEmpty(internalPath))
            {
                LeaveGedcomRequested?.Invoke(this, EventArgs.Empty);
                return true;
            }

            internalPath = string.Empty;
            RenderCurrentLocation();
            return true;
        }

        public override void RefreshPanel()
        {
            if (string.IsNullOrWhiteSpace(GedcomPath))
                return;

            string previousPath = internalPath;
            string previousSelection = SelectedItems.Count == 1 ? SelectedItems[0].Location : null;
            _ = RefreshAsync(previousPath, previousSelection);
        }

        private async Task RefreshAsync(string previousPath, string previousSelection)
        {
            if (await OpenGedcomAsync(GedcomPath))
            {
                Navigate(previousPath);
                SelectLocation(previousSelection);
            }
        }

        public bool SelectPerson(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
                return false;

            if (!string.Equals(internalPath, TagPathPrefix + "INDI", StringComparison.OrdinalIgnoreCase))
            {
                internalPath = TagPathPrefix + "INDI";
                RenderCurrentLocation();
            }

            return SelectLocation(id);
        }

        public bool SelectLocation(string location)
        {
            if (string.IsNullOrWhiteSpace(location))
                return false;

            ListViewItem selected = browserView.Items.Cast<ListViewItem>()
                .FirstOrDefault(item => item.Tag is ViewEntry entry &&
                    string.Equals(entry.Info.Location, location, StringComparison.OrdinalIgnoreCase));
            if (selected == null)
                return false;

            browserView.SelectedIndices.Clear();
            selected.Selected = true;
            selected.Focused = true;
            selected.EnsureVisible();
            return true;
        }

        public void ApplyUserSettings(Font browserFont, FileBrowser.BrowserColumnWidths widths, System.Windows.Forms.View view)
        {
            columnWidths = widths;
            if (browserFont != null)
            {
                Font = browserFont;
                addressBar.ApplyDisplayFont(browserFont);
                addressBar.Height = Math.Max(30, browserFont.Height + 8);
                browserView.Font = browserFont;
            }

            browserView.View = view;
            if (catalog != null)
                RenderCurrentLocation();
            BrowserRowColoring.Apply(browserView, Properties.Settings.Default.BrowserRowColorMode);
        }



        private void ActivateSelectedItem()
        {
            ViewEntry entry = GetCurrentEntry();
            if (entry == null)
                return;
            if (entry.TargetPath == "..")
                NavigateParent();
            else if (!string.IsNullOrWhiteSpace(entry.TargetPath))
                Navigate(entry.TargetPath);
        }

        private void BrowserView_KeyDown(object sender, KeyEventArgs e)
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

        private void BrowserView_ColumnClick(object sender, ColumnClickEventArgs e)
        {
            int column = e.Column == 0 ? 1 : e.Column;
            if (column == sortColumn)
                sortAscending = !sortAscending;
            else
            {
                sortColumn = column;
                sortAscending = true;
            }
            RenderCurrentLocation();
        }

        private void RenderCurrentLocation()
        {
            browserView.BeginUpdate();
            try
            {
                browserView.Items.Clear();
                visibleItems.Clear();
                if (string.IsNullOrEmpty(internalPath))
                {
                    RenderRoot();
                }
                else if (TryGetTagFromPath(internalPath, out string tag))
                {
                    if (string.Equals(tag, "INDI", StringComparison.OrdinalIgnoreCase))
                        RenderPeople();
                    else if (string.Equals(tag, "FAM", StringComparison.OrdinalIgnoreCase))
                        RenderFamilies();
                    else
                        RenderRecords(tag);
                }
            }
            finally
            {
                browserView.EndUpdate();
            }
            BrowserRowColoring.Apply(browserView, Properties.Settings.Default.BrowserRowColorMode);


            addressBar.Path = DisplayLocation;
            RaiseLocationChanged(DisplayLocation);
            if (browserView.Items.Count > 0)
                browserView.Items[0].Selected = true;
            browserView.Focus();
        }

        private void RenderRoot()
        {
            ConfigureColumns(
                ("", 20, HorizontalAlignment.Left),
                (Language.getString("gedcomObject"), GetWidth(columnWidths.NameWidth, 220), HorizontalAlignment.Left),
                (Language.getString("gedcomTag"), GetWidth(columnWidths.TypeWidth, 90), HorizontalAlignment.Left),
                (Language.getString("gedcomCount"), GetWidth(columnWidths.SizeWidth, 90), HorizontalAlignment.Right),
                (Language.getString("gedcomDescription"), GetWidth(columnWidths.DateWidth, 260), HorizontalAlignment.Left));
            AddParentItem();

            IEnumerable<IGrouping<string, GedcomRecordEntry>> groups = (catalog?.Records ?? Array.Empty<GedcomRecordEntry>())
                .Where(record => !string.IsNullOrWhiteSpace(record.Tag))
                .GroupBy(record => record.Tag.ToUpperInvariant())
                .OrderBy(group => GetTagOrder(group.Key))
                .ThenBy(group => group.Key, StringComparer.OrdinalIgnoreCase);
            foreach (IGrouping<string, GedcomRecordEntry> group in groups)
            {
                string tag = group.Key;
                int count = string.Equals(tag, "INDI", StringComparison.OrdinalIgnoreCase)
                    ? catalog.People.Count
                    : string.Equals(tag, "FAM", StringComparison.OrdinalIgnoreCase)
                        ? catalog.Families.Count
                        : group.Count();
                string name = GetTagDisplayName(tag);
                string target = TagPathPrefix + tag;
                var info = new BrowserItemInfo
                {
                    Name = name,
                    Location = target,
                    NativePath = GedcomPath,
                    IsDirectory = true
                };
                AddItem(new ViewEntry(info, target, null), -1,
                    name,
                    tag,
                    count.ToString(CultureInfo.CurrentCulture),
                    Language.getString("gedcomLevelZeroDescription"));
            }
        }

        private void RenderPeople()
        {
            ConfigureColumns(
                ("", 20, HorizontalAlignment.Left),
                (Language.getString("gedcomFirstName"), GetWidth(columnWidths.NameWidth, 180), HorizontalAlignment.Left),
                (Language.getString("gedcomLastName"), GetWidth(columnWidths.TypeWidth, 180), HorizontalAlignment.Left),
                (Language.getString("gedcomAge"), GetWidth(columnWidths.SizeWidth, 80), HorizontalAlignment.Right),
                (Language.getString("gedcomBirthDate"), GetWidth(columnWidths.DateWidth, 140), HorizontalAlignment.Left));
            AddParentItem();

            IEnumerable<GedcomPersonEntry> ordered = catalog?.People ?? Array.Empty<GedcomPersonEntry>();
            Func<GedcomPersonEntry, object> selector = sortColumn switch
            {
                1 => person => person.FirstName ?? string.Empty,
                2 => person => person.LastName ?? string.Empty,
                3 => person => person.Age ?? int.MaxValue,
                4 => person => person.BirthDate ?? DateTime.MaxValue,
                _ => person => person.LastName ?? string.Empty
            };
            ordered = sortAscending
                ? ordered.OrderBy(selector, GedcomValueComparer.Instance)
                : ordered.OrderByDescending(selector, GedcomValueComparer.Instance);
            foreach (GedcomPersonEntry person in ordered)
            {
                string name = GetDisplayName(person);
                BrowserItemInfo info = ToBrowserItemInfo(person);
                AddItem(new ViewEntry(info, null, person), GetSexImageIndex(person.Sex),
                    person.FirstName ?? string.Empty,
                    person.LastName ?? string.Empty,
                    person.Age?.ToString(CultureInfo.CurrentCulture) ?? string.Empty,
                    person.BirthDateText ?? string.Empty);
            }
        }

        private void RenderFamilies()
        {
            ConfigureColumns(
                ("", 20, HorizontalAlignment.Left),
                (Language.getString("gedcomIdentifier"), GetWidth(columnWidths.NameWidth, 130), HorizontalAlignment.Left),
                (Language.getString("gedcomHusband"), GetWidth(columnWidths.TypeWidth, 180), HorizontalAlignment.Left),
                (Language.getString("gedcomWife"), GetWidth(columnWidths.SizeWidth, 180), HorizontalAlignment.Left),
                (Language.getString("gedcomChildren"), GetWidth(columnWidths.DateWidth, 100), HorizontalAlignment.Right));
            AddParentItem();

            IEnumerable<GedcomFamilyEntry> families = catalog?.Families ?? Array.Empty<GedcomFamilyEntry>();
            families = sortAscending
                ? families.OrderBy(family => family.Id, StringComparer.OrdinalIgnoreCase)
                : families.OrderByDescending(family => family.Id, StringComparer.OrdinalIgnoreCase);
            foreach (GedcomFamilyEntry family in families)
            {
                string name = family.Id ?? Language.getString("gedcomFamily");
                var info = new BrowserItemInfo
                {
                    Name = name,
                    Location = family.Id,
                    NativePath = GedcomPath
                };
                AddItem(new ViewEntry(info, null, family), -1,
                    name,
                    GetDisplayName(family.Husband),
                    GetDisplayName(family.Wife),
                    family.Children.Count.ToString(CultureInfo.CurrentCulture));
            }
        }

        private void RenderRecords(string tag)
        {
            ConfigureColumns(
                ("", 20, HorizontalAlignment.Left),
                (Language.getString("gedcomIdentifier"), GetWidth(columnWidths.NameWidth, 150), HorizontalAlignment.Left),
                (Language.getString("gedcomSummary"), GetWidth(columnWidths.TypeWidth, 260), HorizontalAlignment.Left),
                (Language.getString("gedcomFields"), GetWidth(columnWidths.SizeWidth, 90), HorizontalAlignment.Right),
                (Language.getString("gedcomTag"), GetWidth(columnWidths.DateWidth, 100), HorizontalAlignment.Left));
            AddParentItem();

            IEnumerable<GedcomRecordEntry> records = (catalog?.Records ?? Array.Empty<GedcomRecordEntry>())
                .Where(record => string.Equals(record.Tag, tag, StringComparison.OrdinalIgnoreCase));
            records = sortAscending
                ? records.OrderBy(record => record.Id ?? GetRecordSummary(record), StringComparer.CurrentCultureIgnoreCase)
                : records.OrderByDescending(record => record.Id ?? GetRecordSummary(record), StringComparer.CurrentCultureIgnoreCase);
            int index = 0;
            foreach (GedcomRecordEntry record in records)
            {
                index++;
                string identifier = string.IsNullOrWhiteSpace(record.Id)
                    ? tag + " #" + index.ToString(CultureInfo.CurrentCulture)
                    : record.Id;
                string location = TagPathPrefix + tag + "/record/" + index.ToString(CultureInfo.InvariantCulture);
                var info = new BrowserItemInfo
                {
                    Name = identifier,
                    Location = location,
                    NativePath = GedcomPath
                };
                AddItem(new ViewEntry(info, null, record), -1,
                    identifier,
                    GetRecordSummary(record),
                    record.Fields.Count.ToString(CultureInfo.CurrentCulture),
                    tag);
            }
        }

        private void ConfigureColumns(params (string Text, int Width, HorizontalAlignment Alignment)[] columns)
        {
            browserView.Columns.Clear();
            foreach ((string text, int width, HorizontalAlignment alignment) in columns)
                browserView.Columns.Add(text, width, alignment);
        }

        private void AddParentItem()
        {
            var info = new BrowserItemInfo
            {
                Name = "..",
                Location = "..",
                NativePath = GedcomPath,
                IsDirectory = true
            };
            AddItem(new ViewEntry(info, "..", null), -1, "..", string.Empty, string.Empty, string.Empty);
        }

        private void AddItem(ViewEntry entry, int imageIndex, params string[] columns)
        {
            var item = new ListViewItem(browserView.View == System.Windows.Forms.View.Details ? string.Empty : entry.Info.Name)
            {
                Tag = entry,
                ImageIndex = imageIndex
            };
            foreach (string value in columns)
                item.SubItems.Add(value ?? string.Empty);
            browserView.Items.Add(item);
            if (entry.TargetPath != "..")
                visibleItems.Add(entry.Info);
        }

        private void AddressBar_ButtonClick(object sender, EventArgs e)
        {
            string caption = (sender as Control)?.Text?.Trim();
            if (string.IsNullOrWhiteSpace(caption))
                return;
            if (string.Equals(caption, Path.GetFileName(GedcomPath) + ":", StringComparison.OrdinalIgnoreCase))
            {
                Navigate(string.Empty);
                return;
            }

            string tag = GetAvailableTags()
                .FirstOrDefault(candidate => string.Equals(
                    GetTagDisplayName(candidate),
                    caption,
                    StringComparison.CurrentCultureIgnoreCase));
            if (!string.IsNullOrWhiteSpace(tag))
                Navigate(TagPathPrefix + tag);
        }

        private ViewEntry GetCurrentEntry()
        {
            ListViewItem current = browserView.FocusedItem?.Selected == true
                ? browserView.FocusedItem
                : browserView.SelectedItems.Cast<ListViewItem>().FirstOrDefault();
            return current?.Tag as ViewEntry;
        }

        private GedcomRecordEntry FindRecord(object model)
        {
            if (model is GedcomRecordEntry record)
                return record;

            string id = model switch
            {
                GedcomPersonEntry person => person.Id,
                GedcomFamilyEntry family => family.Id,
                _ => null
            };
            if (string.IsNullOrWhiteSpace(id))
                return null;

            return catalog?.Records?.FirstOrDefault(record =>
                string.Equals(record.Id, id, StringComparison.OrdinalIgnoreCase));
        }

        private GedcomRecordText CreateRecordText(ViewEntry entry)
        {
            GedcomRecordEntry record = FindRecord(entry?.Model);
            if (record == null)
                return null;

            return new GedcomRecordText
            {
                Title = string.IsNullOrWhiteSpace(record.Id) ? entry.Info.Name : record.Id,
                Tag = record.Tag,
                Text = string.Join(Environment.NewLine, new[] { FormatGedcomLine(0, record.Id, record.Tag, record.Value) }
                    .Concat(record.Fields.Select(field => FormatGedcomLine(field.Level, field.Xref, field.Tag, field.Value))))
            };
        }

        private static string FormatGedcomLine(int level, string xref, string tag, string value)
        {
            var parts = new List<string> { level.ToString(CultureInfo.InvariantCulture) };
            if (!string.IsNullOrWhiteSpace(xref))
                parts.Add(xref.Trim());
            if (!string.IsNullOrWhiteSpace(tag))
                parts.Add(tag.Trim());
            if (!string.IsNullOrWhiteSpace(value))
                parts.Add(value.Trim());
            return string.Join(" ", parts);
        }

        private bool HasTag(string tag)
        {
            return GetAvailableTags().Any(value => string.Equals(value, tag, StringComparison.OrdinalIgnoreCase));
        }

        private IEnumerable<string> GetAvailableTags()
        {
            return (catalog?.Records ?? Array.Empty<GedcomRecordEntry>())
                .Select(record => record.Tag)
                .Where(tag => !string.IsNullOrWhiteSpace(tag))
                .Distinct(StringComparer.OrdinalIgnoreCase);
        }

        private string GetInternalDisplayPath()
        {
            return TryGetTagFromPath(internalPath, out string tag)
                ? GetTagDisplayName(tag)
                : string.Empty;
        }

        private static bool TryGetTagFromPath(string path, out string tag)
        {
            tag = null;
            if (string.IsNullOrWhiteSpace(path) ||
                !path.StartsWith(TagPathPrefix, StringComparison.OrdinalIgnoreCase))
                return false;
            string remainder = path.Substring(TagPathPrefix.Length);
            int slash = remainder.IndexOf('/');
            tag = (slash < 0 ? remainder : remainder.Substring(0, slash)).Trim().ToUpperInvariant();
            return tag.Length > 0;
        }

        private static string NormalizeInternalPath(string path)
        {
            string value = (path ?? string.Empty).Trim().Trim('\\', '/').Replace('\\', '/');
            return value.StartsWith(TagPathPrefix, StringComparison.OrdinalIgnoreCase)
                ? TagPathPrefix + value.Substring(TagPathPrefix.Length).ToUpperInvariant()
                : value;
        }

        private string GetTagDisplayName(string tag)
        {
            if (string.Equals(tag, "INDI", StringComparison.OrdinalIgnoreCase))
                return Language.getString("gedcomPersons");
            if (string.Equals(tag, "FAM", StringComparison.OrdinalIgnoreCase))
                return Language.getString("gedcomFamilies");
            if (string.Equals(tag, "NOTE", StringComparison.OrdinalIgnoreCase))
                return Language.getString("gedcomNotes");
            return tag;
        }

        private static int GetTagOrder(string tag)
        {
            if (string.Equals(tag, "INDI", StringComparison.OrdinalIgnoreCase))
                return 0;
            if (string.Equals(tag, "FAM", StringComparison.OrdinalIgnoreCase))
                return 1;
            if (string.Equals(tag, "NOTE", StringComparison.OrdinalIgnoreCase))
                return 2;
            return 10;
        }

        private static string GetRecordSummary(GedcomRecordEntry record)
        {
            if (!string.IsNullOrWhiteSpace(record.Value))
                return record.Value.Trim();

            GedcomFieldEntry preferred = record.Fields.FirstOrDefault(field =>
                string.Equals(field.Tag, "NAME", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(field.Tag, "TITL", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(field.Tag, "TEXT", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(field.Tag, "NOTE", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(field.Tag, "FILE", StringComparison.OrdinalIgnoreCase));
            if (preferred != null && !string.IsNullOrWhiteSpace(preferred.Value))
                return preferred.Value.Trim();

            return string.Join("; ", record.Fields
                .Where(field => !string.IsNullOrWhiteSpace(field.Value))
                .Take(2)
                .Select(field => field.Tag + " " + field.Value));
        }

        private void SetLoading(bool loading, string text)
        {
            loadingLabel.Text = text ?? string.Empty;
            loadingLabel.Visible = loading;
            browserView.Visible = !loading;
            if (loading)
                loadingLabel.BringToFront();
        }

        private static string GetDisplayName(GedcomPersonEntry person)
        {
            return person == null
                ? string.Empty
                : string.Join(" ", new[] { person.FirstName, person.LastName }
                    .Where(value => !string.IsNullOrWhiteSpace(value)));
        }

        private static ImageList CreateSexImageList(int size)
        {
            var images = new ImageList
            {
                ColorDepth = ColorDepth.Depth32Bit,
                ImageSize = new Size(size, size),
                TransparentColor = Color.Transparent
            };
            images.Images.Add("sex-male", GedcomSexIconFactory.CreateMaleIcon(size));
            images.Images.Add("sex-female", GedcomSexIconFactory.CreateFemaleIcon(size));
            images.Images.Add("sex-unknown", GedcomSexIconFactory.CreateUnknownIcon(size));
            return images;
        }

        private static int GetSexImageIndex(string sex)
        {
            if (string.Equals(sex?.Trim(), "M", StringComparison.OrdinalIgnoreCase))
                return 0;
            if (string.Equals(sex?.Trim(), "F", StringComparison.OrdinalIgnoreCase))
                return 1;
            return 2;
        }

        private BrowserItemInfo ToBrowserItemInfo(GedcomPersonEntry person)
        {
            return new BrowserItemInfo
            {
                Name = GetDisplayName(person),
                Location = person.Id,
                NativePath = GedcomPath,
                IsDirectory = false,
                Modified = person.BirthDate
            };
        }

        private static int GetWidth(int configured, int fallback)
        {
            return configured > 0 ? configured : fallback;
        }

        private sealed class ViewEntry
        {
            public ViewEntry(BrowserItemInfo info, string targetPath, object model)
            {
                Info = info;
                TargetPath = targetPath;
                Model = model;
            }

            public BrowserItemInfo Info { get; }
            public string TargetPath { get; }
            public object Model { get; }
        }

        private sealed class GedcomValueComparer : IComparer<object>
        {
            public static readonly GedcomValueComparer Instance = new GedcomValueComparer();

            public int Compare(object left, object right)
            {
                if (left is int leftInt && right is int rightInt)
                    return leftInt.CompareTo(rightInt);
                if (left is DateTime leftDate && right is DateTime rightDate)
                    return leftDate.CompareTo(rightDate);
                return StringComparer.CurrentCultureIgnoreCase.Compare(left?.ToString(), right?.ToString());
            }
        }
    }
}
