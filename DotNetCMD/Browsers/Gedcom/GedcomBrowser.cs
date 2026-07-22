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
    internal sealed class GedcomBrowser : BrowserPanelBase
    {
        private readonly TextBox locationBox;
        private readonly ListView peopleView;
        private readonly Label loadingLabel;
        private readonly ImageList sexIconsSmall;
        private readonly ImageList sexIconsLarge;
        private readonly List<GedcomPersonEntry> people = new List<GedcomPersonEntry>();
        private readonly List<BrowserItemInfo> visibleItems = new List<BrowserItemInfo>();
        private CancellationTokenSource loadCancellation;
        private int sortColumn = 2;
        private bool sortAscending = true;

        public GedcomBrowser()
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

            sexIconsSmall = CreateSexImageList(16);
            sexIconsLarge = CreateSexImageList(32);

            peopleView = new ListView
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
            peopleView.Columns.Add(string.Empty, 20);
            peopleView.Columns.Add("FirstName", 180);
            peopleView.Columns.Add("LastName", 180);
            peopleView.Columns.Add("Age", 80, HorizontalAlignment.Right);
            peopleView.Columns.Add("BirthDate", 140);
            peopleView.ItemActivate += (_, __) => ActivateSelectedItem();
            peopleView.SelectedIndexChanged += (_, __) => RaiseSelectionChanged();
            peopleView.ColumnClick += PeopleView_ColumnClick;
            peopleView.KeyDown += PeopleView_KeyDown;

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
            Controls.Add(peopleView);
            Controls.Add(locationPanel);
        }

        public event EventHandler LeaveGedcomRequested;
        public event EventHandler NavigateBackRequested;

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                loadCancellation?.Cancel();
                loadCancellation?.Dispose();
                sexIconsSmall?.Dispose();
                sexIconsLarge?.Dispose();
            }
            base.Dispose(disposing);
        }

        public string GedcomPath { get; private set; }
        public override string DisplayLocation => string.IsNullOrWhiteSpace(GedcomPath)
            ? string.Empty
            : Path.GetFileName(GedcomPath) + ":\\";
        public override IReadOnlyList<BrowserItemInfo> Items => visibleItems;
        public override IReadOnlyList<BrowserItemInfo> SelectedItems => peopleView.SelectedItems
            .Cast<ListViewItem>()
            .Select(item => item.Tag as GedcomPersonEntry)
            .Where(item => item != null)
            .Select(ToBrowserItemInfo)
            .ToArray();
        public override BrowserPanelCapabilities Capabilities => BrowserPanelCapabilities.Navigate;
        public string CurrentItemName
        {
            get
            {
                ListViewItem current = peopleView.FocusedItem?.Selected == true
                    ? peopleView.FocusedItem
                    : peopleView.SelectedItems.Cast<ListViewItem>().FirstOrDefault();
                GedcomPersonEntry person = current?.Tag as GedcomPersonEntry;
                return person == null ? null : GetDisplayName(person);
            }
        }
        public GedcomPersonEntry SelectedPerson => peopleView.SelectedItems.Count == 1
            ? peopleView.SelectedItems[0].Tag as GedcomPersonEntry
            : null;

        public async Task<bool> OpenGedcomAsync(string gedcomPath)
        {
            loadCancellation?.Cancel();
            loadCancellation?.Dispose();
            loadCancellation = new CancellationTokenSource();
            GedcomPath = Path.GetFullPath(gedcomPath);
            SetLoading(true, Language.getString("gedcomReadingCatalog"));
            try
            {
                IReadOnlyList<GedcomPersonEntry> entries = await GedcomCatalogService.ReadPeopleAsync(GedcomPath, loadCancellation.Token);
                people.Clear();
                people.AddRange(entries);
                RenderPeople();
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
            return string.IsNullOrWhiteSpace(location) || string.Equals(location, DisplayLocation, StringComparison.OrdinalIgnoreCase);
        }

        public override bool NavigateParent()
        {
            LeaveGedcomRequested?.Invoke(this, EventArgs.Empty);
            return true;
        }

        public override void RefreshPanel()
        {
            if (!string.IsNullOrWhiteSpace(GedcomPath))
            {
                _ = OpenGedcomAsync(GedcomPath);
            }
        }

        public bool SelectPerson(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                return false;
            }

            ListViewItem selected = peopleView.Items.Cast<ListViewItem>()
                .FirstOrDefault(item => item.Tag is GedcomPersonEntry person && string.Equals(person.Id, id, StringComparison.OrdinalIgnoreCase));
            if (selected != null)
            {
                if (selected.Selected && peopleView.SelectedItems.Count == 1)
                {
                    selected.Focused = true;
                    selected.EnsureVisible();
                    return true;
                }

                peopleView.SelectedIndices.Clear();
                selected.Selected = true;
                selected.Focused = true;
                selected.EnsureVisible();
                return true;
            }
            return false;
        }

        public void ApplyUserSettings(Font browserFont, FileBrowser.BrowserColumnWidths widths, System.Windows.Forms.View view)
        {
            if (browserFont != null)
            {
                Font = browserFont;
                peopleView.Font = browserFont;
                locationBox.Font = browserFont;
            }

            peopleView.Columns[1].Width = widths.NameWidth;
            peopleView.Columns[2].Width = widths.TypeWidth;
            peopleView.Columns[3].Width = widths.SizeWidth;
            peopleView.Columns[4].Width = widths.DateWidth;
            peopleView.View = view;
            UpdateItemTextForView();
        }

        private void ActivateSelectedItem()
        {
            if (peopleView.SelectedItems.Count == 1 && peopleView.SelectedItems[0].Tag == null)
            {
                NavigateParent();
            }
        }

        private void PeopleView_KeyDown(object sender, KeyEventArgs e)
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

        private void PeopleView_ColumnClick(object sender, ColumnClickEventArgs e)
        {
            int column = e.Column == 0 ? 1 : e.Column;
            if (column == sortColumn)
            {
                sortAscending = !sortAscending;
            }
            else
            {
                sortColumn = column;
                sortAscending = true;
            }
            RenderPeople();
        }

        private void RenderPeople()
        {
            IEnumerable<GedcomPersonEntry> ordered = people;
            Func<GedcomPersonEntry, object> keySelector = sortColumn switch
            {
                1 => person => person.FirstName ?? string.Empty,
                2 => person => person.LastName ?? string.Empty,
                3 => person => person.Age ?? int.MaxValue,
                4 => person => person.BirthDate ?? DateTime.MaxValue,
                _ => person => person.LastName ?? string.Empty
            };
            ordered = sortAscending
                ? ordered.OrderBy(keySelector, GedcomValueComparer.Instance)
                : ordered.OrderByDescending(keySelector, GedcomValueComparer.Instance);

            peopleView.BeginUpdate();
            try
            {
                peopleView.Items.Clear();
                visibleItems.Clear();
                var parentItem = new ListViewItem(peopleView.View == System.Windows.Forms.View.Details ? string.Empty : "..");
                parentItem.SubItems.Add("..");
                parentItem.SubItems.Add(string.Empty);
                parentItem.SubItems.Add(string.Empty);
                parentItem.SubItems.Add(string.Empty);
                peopleView.Items.Add(parentItem);
                foreach (GedcomPersonEntry person in ordered)
                {
                    var item = new ListViewItem(peopleView.View == System.Windows.Forms.View.Details ? string.Empty : GetDisplayName(person))
                    {
                        Tag = person,
                        ImageIndex = GetSexImageIndex(person.Sex)
                    };
                    item.SubItems.Add(person.FirstName ?? string.Empty);
                    item.SubItems.Add(person.LastName ?? string.Empty);
                    item.SubItems.Add(person.Age?.ToString(CultureInfo.CurrentCulture) ?? string.Empty);
                    item.SubItems.Add(person.BirthDateText ?? string.Empty);
                    peopleView.Items.Add(item);
                    visibleItems.Add(ToBrowserItemInfo(person));
                }
            }
            finally
            {
                peopleView.EndUpdate();
            }

            locationBox.Text = DisplayLocation;
            RaiseLocationChanged(DisplayLocation);
            if (peopleView.Items.Count > 0)
            {
                peopleView.Items[0].Selected = true;
            }
            peopleView.Focus();
        }

        private void UpdateItemTextForView()
        {
            foreach (ListViewItem item in peopleView.Items)
            {
                GedcomPersonEntry person = item.Tag as GedcomPersonEntry;
                item.Text = peopleView.View == System.Windows.Forms.View.Details
                    ? string.Empty
                    : person == null ? ".." : GetDisplayName(person);
            }
        }

        private void SetLoading(bool loading, string text)
        {
            loadingLabel.Text = text ?? string.Empty;
            loadingLabel.Visible = loading;
            peopleView.Visible = !loading;
            if (loading)
            {
                loadingLabel.BringToFront();
            }
        }

        private static string GetDisplayName(GedcomPersonEntry person)
        {
            return string.Join(" ", new[] { person.FirstName, person.LastName }.Where(value => !string.IsNullOrWhiteSpace(value)));
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
            {
                return 0;
            }
            if (string.Equals(sex?.Trim(), "F", StringComparison.OrdinalIgnoreCase))
            {
                return 1;
            }
            return 2;
        }

        private static BrowserItemInfo ToBrowserItemInfo(GedcomPersonEntry person)
        {
            return new BrowserItemInfo
            {
                Name = GetDisplayName(person),
                Location = person.Id,
                IsDirectory = false,
                Modified = person.BirthDate
            };
        }

        private sealed class GedcomValueComparer : IComparer<object>
        {
            public static readonly GedcomValueComparer Instance = new GedcomValueComparer();

            public int Compare(object left, object right)
            {
                if (left is int leftInt && right is int rightInt)
                {
                    return leftInt.CompareTo(rightInt);
                }
                if (left is DateTime leftDate && right is DateTime rightDate)
                {
                    return leftDate.CompareTo(rightDate);
                }
                return StringComparer.CurrentCultureIgnoreCase.Compare(left?.ToString(), right?.ToString());
            }
        }
    }
}
