using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Data;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.IO;
using System.Collections.Specialized;
using System.Diagnostics;
using System.Threading.Tasks;


namespace DotNetCommander
{
  public partial class FileBrowser : BrowserPanelBase {
    private const string InternalDragSourceFormat = "DotNetCommander.FileBrowser.SourcePath";
    public String[] selectedFiles;
    public List<String> oldPath = new List<String>();
    public List<SelectionPath> selectionPaths = new List<SelectionPath>();
    public int[] oldSelectedItems;
    private TextBox editBox = new TextBox();
    private int sortColumn = 1;
    private bool sortAscending = true;
    private int browseGeneration = 0;
    private readonly Timer directoryRefreshTimer;
    private FileSystemWatcher directoryWatcher;
    private bool pendingDirectoryRefresh;
    private readonly ArchiveBrowser archiveBrowser;
    private bool archiveMode;
    private string archiveReturnFileName;
    private readonly GedcomBrowser gedcomBrowser;
    private bool gedcomMode;
    private string gedcomReturnFileName;
    private readonly List<BrowserHistoryEntry> navigationHistory = new List<BrowserHistoryEntry>();
    private int navigationHistoryIndex = -1;
    private bool navigatingHistory;
    private bool suppressArchiveHistory;

    public delegate void PathChangeHandler(Object sender, String newPath);
    public event PathChangeHandler PathChange;
    internal event EventHandler<ArchiveDeviceChangedEventArgs> ArchiveDeviceChanged;

    public String CurrentPath;

    FormCopy CopyWindow = null;

    public FileBrowser() {
      InitializeComponent();

      archiveBrowser = new ArchiveBrowser
      {
        Dock = DockStyle.Fill,
        Visible = false
      };
      archiveBrowser.SelectionChanged += ArchiveBrowser_SelectionChanged;
      archiveBrowser.BrowserLocationChanged += ArchiveBrowser_LocationChanged;
      archiveBrowser.LeaveArchiveRequested += (_, __) => ExitArchive(true);
      archiveBrowser.NavigateBackRequested += (_, __) => _ = NavigateBackAsync();
      archiveBrowser.SetImageLists(fileImages, fileImagesLarge);
      Controls.Add(archiveBrowser);

      gedcomBrowser = new GedcomBrowser
      {
        Dock = DockStyle.Fill,
        Visible = false
      };
      gedcomBrowser.SelectionChanged += GedcomBrowser_SelectionChanged;
      gedcomBrowser.BrowserLocationChanged += GedcomBrowser_LocationChanged;
      gedcomBrowser.LeaveGedcomRequested += (_, __) => ExitGedcom(true);
      gedcomBrowser.NavigateBackRequested += (_, __) => _ = NavigateBackAsync();
      Controls.Add(gedcomBrowser);

      directoryRefreshTimer = new Timer();
      directoryRefreshTimer.Interval = 350;
      directoryRefreshTimer.Tick += DirectoryRefreshTimer_Tick;

      editBox.Size = new System.Drawing.Size(0, 0);
      editBox.Location = new System.Drawing.Point(0, 0);
      this.Controls.AddRange(new System.Windows.Forms.Control[] { this.editBox });
      editBox.KeyPress += new System.Windows.Forms.KeyPressEventHandler(this.EditOver);
      editBox.LostFocus += new System.EventHandler(this.FocusOver);
      //editBox.Font = new System.Drawing.Font("Microsoft Sans Serif", 10F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((System.Byte)(0)));
      editBox.Font = browserView.Font;
      //editBox.BackColor = Color.LightYellow;
      //editBox.BorderStyle = BorderStyle.Fixed3D;
      editBox.Hide();
      editBox.Text = "";

      /*browserView.Columns[0].Width = 25;
      browserView.Columns[1].Width = 130;
      browserView.Columns[2].Width = 70;
      browserView.Columns[3].Width = 110;
      browserView.Columns[4].Width = 80;*/

      ColumnHeader Header = new ColumnHeader();
      Header.Text = "";
      Header.Width = 20;
      browserView.Columns.Add(Header);

      Header = new ColumnHeader();
      Header.Text = Language.getString("columnName");
      Header.AutoResize(ColumnHeaderAutoResizeStyle.HeaderSize);
      Header.Width = 150;
      browserView.Columns.Add(Header);

      Header = new ColumnHeader();
      Header.Text = Language.getString("columnType");
      Header.Width = 60;
      browserView.Columns.Add(Header);

      Header = new ColumnHeader();
      Header.Text = Language.getString("columnSize");
      Header.Width = 90;
      Header.TextAlign = HorizontalAlignment.Right;
      browserView.Columns.Add(Header);

      Header = new ColumnHeader();
      Header.Text = Language.getString("columnDate");
      Header.Width = 100;
      browserView.Columns.Add(Header);



      browserView.ContextMenuStrip = contextMenu;
      //ListViewGroup group = new ListViewGroup();
      browserView.Groups.Add(new ListViewGroup("", HorizontalAlignment.Left));
      browserView.Groups.Add(new ListViewGroup("", HorizontalAlignment.Left));

      browserView.ShowGroups = false;

      // Mindestens 1 Item muss im ContextMenü vorhanden sein
      ToolStripMenuItem toolItem = new ToolStripMenuItem(Language.getString("past"));
      toolItem.Tag = "past";
      contextMenu.Items.Add(toolItem);


      // Addressbar
      addressBarCurrentPath.ButtonClick += new EventHandler(addressBar_ButtonClick);
      browserView.ColumnClick += browserView_ColumnClick;
      browserView.ItemDrag += browserView_ItemDrag;
      browserView.DragEnter += browserView_DragEnter;
      browserView.DragOver += browserView_DragOver;
      browserView.DragDrop += browserView_DragDrop;
      ApplyUserSettings();
    }

    public bool IsArchiveMode => archiveMode;
    public bool IsGedcomMode => gedcomMode;
    public bool IsVirtualMode => archiveMode || gedcomMode;
    internal GedcomPersonEntry SelectedGedcomPerson => gedcomMode ? gedcomBrowser.SelectedPerson : null;
    internal bool SelectGedcomPerson(string personId)
    {
      return gedcomMode && gedcomBrowser.SelectPerson(personId);
    }
    public string OpenArchivePath => archiveMode ? archiveBrowser.ArchivePath : null;
    public string OpenArchiveInternalPath => archiveMode ? archiveBrowser.InternalPath : null;
    public string[] SelectedArchiveEntryNames => archiveMode ? archiveBrowser.SelectedEntryNames : Array.Empty<string>();
    public override string DisplayLocation => archiveMode
      ? archiveBrowser.DisplayLocation
      : gedcomMode ? gedcomBrowser.DisplayLocation : CurrentPath;
    public override IReadOnlyList<BrowserItemInfo> Items => archiveMode
      ? archiveBrowser.Items
      : gedcomMode ? gedcomBrowser.Items : browserView.Items.Cast<ListViewItem>()
        .Where(item => !IsParentNavigationItem(item))
        .Select(CreateBrowserItemInfo)
        .ToArray();
    public override IReadOnlyList<BrowserItemInfo> SelectedItems => archiveMode
      ? archiveBrowser.SelectedItems
      : gedcomMode ? gedcomBrowser.SelectedItems : browserView.SelectedItems.Cast<ListViewItem>()
        .Where(item => !IsParentNavigationItem(item))
        .Select(CreateBrowserItemInfo)
        .ToArray();
    public override BrowserPanelCapabilities Capabilities => archiveMode
      ? BrowserPanelCapabilities.ReadOnlyVirtual
      : gedcomMode ? gedcomBrowser.Capabilities : BrowserPanelCapabilities.FullFileSystem;

    public override bool Navigate(string location)
    {
      if (archiveMode)
      {
        return archiveBrowser.Navigate(location);
      }
      if (gedcomMode)
      {
        return gedcomBrowser.Navigate(location);
      }

      return browseTo(location) != null;
    }

    public override bool NavigateParent()
    {
      if (archiveMode)
      {
        return archiveBrowser.NavigateParent();
      }
      if (gedcomMode)
      {
        return gedcomBrowser.NavigateParent();
      }

      if (string.IsNullOrWhiteSpace(CurrentPath))
      {
        return false;
      }

      string childPath = Path.GetFullPath(CurrentPath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
      DirectoryInfo parent = Directory.GetParent(childPath);
      if (parent == null || browseTo(parent.FullName) == null)
      {
        return false;
      }

      selectFile(Path.GetFileName(childPath));
      return true;
    }

    public override async Task<bool> NavigateBackAsync()
    {
      if (navigationHistoryIndex <= 0)
      {
        return false;
      }

      UpdateCurrentHistorySelection();
      int previousIndex = navigationHistoryIndex;
      BrowserHistoryEntry target = navigationHistory[--navigationHistoryIndex];
      navigatingHistory = true;
      try
      {
        bool navigated;
        if (target.IsGedcom)
        {
          if (!gedcomMode || !string.Equals(gedcomBrowser.GedcomPath, target.Location, StringComparison.OrdinalIgnoreCase))
          {
            navigated = await EnterGedcomAsync(target.Location);
          }
          else
          {
            navigated = true;
          }

          if (navigated)
          {
            gedcomBrowser.SelectPerson(target.SelectedLocation);
          }
        }
        else if (target.IsArchive)
        {
          if (!archiveMode || !string.Equals(archiveBrowser.ArchivePath, target.Location, StringComparison.OrdinalIgnoreCase))
          {
            navigated = await EnterArchiveAsync(target.Location);
          }
          else
          {
            navigated = true;
          }

          if (navigated)
          {
            suppressArchiveHistory = true;
            try
            {
              navigated = archiveBrowser.Navigate(target.InternalPath);
              archiveBrowser.SelectEntry(target.SelectedLocation);
            }
            finally
            {
              suppressArchiveHistory = false;
            }
          }
        }
        else
        {
          if (archiveMode)
          {
            ExitArchive(false, false);
          }
          if (gedcomMode)
          {
            ExitGedcom(false, false);
          }

          navigated = browseTo(target.Location) != null;
          if (navigated && !string.IsNullOrWhiteSpace(target.SelectedLocation))
          {
            selectFile(Path.GetFileName(target.SelectedLocation));
          }
        }

        if (!navigated)
        {
          navigationHistoryIndex = previousIndex;
        }

        return navigated;
      }
      finally
      {
        navigatingHistory = false;
      }
    }

    public override void RefreshPanel()
    {
      RefreshCurrentDirectory();
    }

    public void ApplyUserSettings()
    {
      float fontSize = Properties.Settings.Default.BrowserFontSize;
      if (fontSize < 6f)
      {
        fontSize = 11.25f;
      }

      string fontName = Properties.Settings.Default.BrowserFontName;
      if (string.IsNullOrWhiteSpace(fontName))
      {
        fontName = "Microsoft Sans Serif";
      }

      Font browserFont;
      try
      {
        browserFont = new Font(fontName, fontSize, FontStyle.Regular, GraphicsUnit.Point);
      }
      catch
      {
        browserFont = new Font("Microsoft Sans Serif", 11.25f, FontStyle.Regular, GraphicsUnit.Point);
      }

      Font = browserFont;
      browserView.Font = browserFont;
      editBox.Font = browserFont;
      addressBarCurrentPath.ApplyDisplayFont(browserFont);

      browserView.Columns[0].Width = 20;
      browserView.Columns[1].Text = Language.getString("columnName");
      browserView.Columns[2].Text = Language.getString("columnType");
      browserView.Columns[3].Text = Language.getString("columnSize");
      browserView.Columns[4].Text = Language.getString("columnDate");
      browserView.Columns[1].Width = Math.Max(80, Properties.Settings.Default.FileBrowserNameColumnWidth);
      browserView.Columns[2].Width = Math.Max(50, Properties.Settings.Default.FileBrowserTypeColumnWidth);
      browserView.Columns[3].Width = Math.Max(60, Properties.Settings.Default.FileBrowserSizeColumnWidth);
      browserView.Columns[4].Width = Math.Max(80, Properties.Settings.Default.FileBrowserDateColumnWidth);
      archiveBrowser.ApplyUserSettings(browserFont, GetColumnWidths(), browserView.View);
      gedcomBrowser.ApplyUserSettings(browserFont, GetColumnWidths(), browserView.View);

      if (!Properties.Settings.Default.FileBrowserLoadIcons)
      {
        foreach (ListViewItem item in browserView.Items)
        {
          item.ImageIndex = -1;
        }
      }
      else
      {
        QueueIconLoadingForCurrentItems();
      }

      ConfigureDirectoryWatcher();
    }

    /**
     * Browse to a path and return the full path string.
     **/
    public String browseTo(String path) {
      DirectoryInfo curDir;
      FileInfo[] fileArray;
      DirectoryInfo[] dirArray;
      ListViewItem Item;
      List<ListViewItem> pendingItems = new List<ListViewItem>();
      List<IconLoadRequest> iconRequests = new List<IconLoadRequest>();
      int currentBrowseGeneration = ++browseGeneration;

      path = NormalizeEnteredPath(path);
      if (string.IsNullOrWhiteSpace(path))
        return null;

      if (archiveMode)
      {
        ExitArchive(false, false);
      }
      if (gedcomMode)
      {
        ExitGedcom(false, false);
      }

      try
      {
        curDir = new DirectoryInfo(path);
        if (!curDir.Exists)
        {
          ShowBrowseError(Language.getString("browsePathNotFound"), path);
          return null;
        }

        addressBarCurrentPath.Path = curDir.FullName;
        fileArray = curDir.GetFiles();
        dirArray = curDir.GetDirectories();

        updateSelectionPath();

        browserView.BeginUpdate();
        try
        {
          browserView.Items.Clear();

          if (Path.GetFullPath(curDir.FullName) != Path.GetPathRoot(curDir.FullName)) {
            Item = new ListViewItem("", 0);
            Item.ImageIndex = -1;
            Item.SubItems.Add("..");
            Item.Group = browserView.Groups[0];
            if (curDir.Parent != null)
              Item.Tag = curDir.Parent.FullName;
            pendingItems.Add(Item);
            iconRequests.Add(new IconLoadRequest(Item, curDir.FullName, true));
          }

          foreach (DirectoryInfo dir in dirArray) {
            if ((dir.Attributes & FileAttributes.ReparsePoint) > 0) {
              continue;
            }
            Item = new ListViewItem("", 0);
            Item.ImageIndex = -1;
            Item.SubItems.Add(dir.Name);
            Item.SubItems.Add("");
            Item.SubItems.Add("");
            Item.SubItems.Add(dir.LastWriteTime.ToShortDateString() + " " + dir.LastWriteTime.ToShortTimeString());
            Item.Tag = dir.FullName;

            Item.Group = browserView.Groups[0];
            pendingItems.Add(Item);
            iconRequests.Add(new IconLoadRequest(Item, dir.FullName, true));
          }

          foreach (FileInfo file in fileArray) {
            Item = new ListViewItem("", 0);
            Item.ImageIndex = -1;
            Item.SubItems.Add(file.Name);
            Item.SubItems.Add(file.Extension.Replace(".", ""));
            Item.SubItems.Add(String.Format("{0:0,0}", file.Length));
            Item.SubItems.Add(file.LastWriteTime.ToShortDateString() + " " + file.LastWriteTime.ToShortTimeString());
            Item.Group = browserView.Groups[1];
            Item.Tag = file.FullName;
            if ((file.Attributes & FileAttributes.ReparsePoint) > 0) {
              String targetPath = null;
              if ((targetPath = OS.ShortcutGetTargetPath(file.FullName)) != null)
                Item.Tag = targetPath;
            }
            pendingItems.Add(Item);
            iconRequests.Add(new IconLoadRequest(Item, file.FullName, false));
          }
          if (pendingItems.Count > 0)
          {
            browserView.Items.AddRange(pendingItems.ToArray());
          }
        }
        finally
        {
          browserView.EndUpdate();
        }

        ApplySorting();
        UpdateItemTextForView(browserView.View);
        QueueIconLoading(currentBrowseGeneration, iconRequests);

        CurrentPath = curDir.FullName;
        ConfigureDirectoryWatcher();
        preSelection();
        RecordHistory(new BrowserHistoryEntry
        {
          Location = curDir.FullName,
          IsArchive = false
        });

        PathChange?.Invoke(this, curDir.FullName);
        RaiseLocationChanged(curDir.FullName);
        return curDir.FullName;
      }
      catch (UnauthorizedAccessException ex)
      {
        LogService.LogException("FileBrowser.browseTo", ex);
        ShowBrowseError(Language.getString("browseAccessDenied"), path);
      }
      catch (DirectoryNotFoundException ex)
      {
        LogService.LogException("FileBrowser.browseTo", ex);
        ShowBrowseError(Language.getString("browsePathNotFound"), path);
      }
      catch (IOException ex)
      {
        LogService.LogException("FileBrowser.browseTo", ex);
        ShowBrowseError(Language.getString("browseOpenPathFailed"), path);
      }
      catch (Exception ex)
      {
        LogService.LogException("FileBrowser.browseTo", ex);
        ShowBrowseError(Language.getString("browseOpenPathFailed"), path);
      }

      return null;
    }

    /**
     * Selektiert ein Item nach ein Ordner wechsel
     **/
    private void preSelection() {
      if (selectionPaths.Count > 0) {

        foreach (SelectionPath selectPath in selectionPaths) {
          if (selectPath.path == CurrentPath) {
            foreach (ListViewItem item in browserView.Items) {
              if ((String)item.Tag == selectPath.selectedPath) {
                item.Selected = true;
                return;
              }
            }
            return;
          }
        }
      }
      if (browserView.Items.Count > 0)
      {
        browserView.Items[0].Selected = true;
      }

      /*String str = Path.GetDirectoryName(oldPath.Last());
      if (CurrentPath == Path.GetDirectoryName(oldPath.Last()))
      {
          foreach (ListViewItem item in browserView.Groups[0].Items)
          {
              if (item.Tag == oldPath.Last())
              {
                  item.Selected = true;
              }
          }
      }
      else
      {
          browserView.Items[0].Selected = true;
      }*/
    }

    private void updateSelectionPath() {
      if (browserView.SelectedItems.Count == 0)
        return;

      // Search current path in the list and update selection
      foreach (SelectionPath selectPath in selectionPaths) {
        if (selectPath.path == CurrentPath) {
          selectPath.selectedPath = browserView.SelectedItems[0].Tag as String;
          return;
        }
      }

      // add new item in the list
      SelectionPath newSelectPath = new SelectionPath();
      newSelectPath.path = CurrentPath;
      newSelectPath.selectedPath = browserView.SelectedItems[0].Tag as String;
      selectionPaths.Add(newSelectPath);
    }

    private async void browserView_ItemActivate(object sender, EventArgs e) {
      ListView listView = (ListView)sender;
      ListView.SelectedListViewItemCollection SelectedItem = listView.SelectedItems;
      if (SelectedItem == null || SelectedItem.Count == 0) { return; }
      if (listView.SelectedItems.Count > 0) {
        String newPath = SelectedItem[0].Tag as String;
        if ((ModifierKeys & Keys.Shift) == Keys.Shift && WinContextMenu.RunInPersistentConsole(newPath))
        {
          return;
        }
        /*if (newPath.IndexOf(".lnk") > 0)
        {
            newPath = OS.ShortcutGetTargetPath(newPath);
        }*/
        if (System.IO.Directory.Exists(newPath)) {
          browseTo(newPath);
        }
        else if (FileTypeClassifier.IsSupportedArchive(newPath))
        {
          await EnterArchiveAsync(newPath);
        }
        else {
          WinContextMenu.Open(newPath);
        }
      }
    }

    private void browserView_SelectedIndexChanged(object sender, EventArgs e) {
      selectedFiles = new String[browserView.SelectedItems.Count];
      int i = 0;
      foreach (ListViewItem selectedItem in browserView.SelectedItems) {
        selectedFiles[i++] = selectedItem.Tag as String;
        selectedItem.Focused = true;
      }

      UpdateCurrentHistorySelection();
      RaiseSelectionChanged();
    }

    private void browserView_BeforeLabelEdit(object sender, LabelEditEventArgs e) {
      e.CancelEdit = true;

      /*editBox.Size = new Size(browserView.Columns[1].Width + browserView.Columns[2].Width, browserView.SelectedItems[0].Bounds.Height);
      editBox.Location = new Point(browserView.Columns[0].Width + 5, browserView.SelectedItems[0].Bounds.Bottom + 7);
      editBox.Show() ;
      editBox.Text = browserView.SelectedItems[0].SubItems[1].Text;
editBox.SelectAll() ;
      editBox.BringToFront();
editBox.Focus();*/
      startSelectedItemEdit();
    }

    private void EditOver(object sender, System.Windows.Forms.KeyPressEventArgs e) {
      if (e.KeyChar == 13) {
        renameItem(browserView.SelectedItems[0], editBox.Text);
        editBox.Hide();
      }

      if (e.KeyChar == 27)
        editBox.Hide();
    }

    private void FocusOver(object sender, System.EventArgs e) {
      renameItem(browserView.SelectedItems[0], editBox.Text);
      editBox.Hide();
    }

    private void startSelectedItemEdit() {
      if (browserView.SelectedItems[0].SubItems[1].Text == "..")
        return;

      editBox.Size = new Size(browserView.Columns[1].Width + browserView.Columns[2].Width, browserView.SelectedItems[0].Bounds.Height);
      editBox.Location = new Point(browserView.Columns[0].Width + 5, browserView.SelectedItems[0].Bounds.Bottom + 7);
      editBox.Show();
      editBox.Text = browserView.SelectedItems[0].SubItems[1].Text;
      editBox.SelectAll();
      editBox.BringToFront();
      editBox.Focus();
    }

    public void renameItem(ListViewItem item, String newName) {
      String newPath = System.IO.Path.GetDirectoryName(item.Tag as String) + "\\" + newName;
      newPath = newPath.Replace("\\\\", "\\");
      renameDirFile(item.Tag as String, newPath);
      item.Tag = newPath;
      item.SubItems[1].Text = newName;
    }

    public void renameDirFile(String oldPath, String newPath) {
      if (newPath == oldPath)
        return;

      if (System.IO.Directory.Exists(oldPath)) {
        System.IO.Directory.Move(oldPath, newPath);
      }
      else {
        System.IO.File.Move(oldPath, newPath);
      }
    }

    public void RenameSelectedItem()
    {
      if (browserView.SelectedItems.Count > 0)
      {
        startSelectedItemEdit();
      }
    }

    private void contextMenu_ItemClicked(object sender, ToolStripItemClickedEventArgs e) {
      if (!(e.ClickedItem.Tag is string tag))
        return;

      if (WinContextMenu.Execute(tag, selectedFiles, CurrentPath, FindForm()))
        return;

      if (tag == "rename")
        startSelectedItemEdit();
      else if (tag == "copy") {
        StringCollection paths = new StringCollection();
        foreach (ListViewItem selectedItem in browserView.SelectedItems) {
          paths.Add(selectedItem.Tag as String);
        }
        Clipboard.SetFileDropList(paths);
      }
      else if (tag == "past") {
        StringCollection paths = Clipboard.GetFileDropList();
        CopyWindow = new FormCopy(paths, CurrentPath);
        CopyWindow.ActionComplete += new FormCopy.ActionCompleteHandler(this.CopyComplete);
        CopyWindow.ShowDialog(FindForm());
      }
      else if (tag == "cut") {
        StringCollection paths = new StringCollection();
        foreach (ListViewItem selectedItem in browserView.SelectedItems) {
          paths.Add(selectedItem.Tag as String);
        }
        Clipboard.SetFileDropList(paths);

        /*StringCollection paths = Clipboard.GetFileDropList();
        CopyWindow = new FormCopy(paths, CurrentPath, FormCopy.Type.Move);
        CopyWindow.ActionComplete += new FormCopy.ActionCompleteHandler(this.CopyComplete);
        CopyWindow.Show();*/
      }
    }

    private void contextMenu_Opening(object sender, CancelEventArgs e) {
      // Rightclick Menu
      ToolStripMenuItem toolItem;
      contextMenu.Items.Clear();

      if (browserView.SelectedItems.Count > 0) {
        toolItem = new ToolStripMenuItem(Language.getString("copy"));
        toolItem.Tag = "copy";
        contextMenu.Items.Add(toolItem);

        toolItem = new ToolStripMenuItem(Language.getString("cut"));
        toolItem.Tag = "cut";
        contextMenu.Items.Add(toolItem);
      }

      toolItem = new ToolStripMenuItem(Language.getString("past"));
      toolItem.Tag = "past";
      contextMenu.Items.Add(toolItem);

      if (browserView.SelectedItems.Count > 0) {
        toolItem = new ToolStripMenuItem(Language.getString("rename"));
        toolItem.Tag = "rename";
        contextMenu.Items.Add(toolItem);
      }

      if (contextMenu.Items.Count > 0)
      {
        contextMenu.Items.Add(new ToolStripSeparator());
      }

      WinContextMenu.AppendShellItems(contextMenu, selectedFiles, CurrentPath);

      if (contextMenu.Items.Count > 0)
      {
        contextMenu.Items.Add(new ToolStripSeparator());
      }

      var viewMenu = new ToolStripMenuItem(Language.getString("viewMode"));
      AddViewMenuItem(viewMenu, System.Windows.Forms.View.Details, Language.getString("viewDetails"));
      AddViewMenuItem(viewMenu, System.Windows.Forms.View.List, Language.getString("viewList"));
      AddViewMenuItem(viewMenu, System.Windows.Forms.View.SmallIcon, Language.getString("viewSmallIcons"));
      AddViewMenuItem(viewMenu, System.Windows.Forms.View.LargeIcon, Language.getString("viewLargeIcons"));
      AddViewMenuItem(viewMenu, System.Windows.Forms.View.Tile, Language.getString("viewTiles"));
      contextMenu.Items.Add(viewMenu);
    }

    private void AddViewMenuItem(ToolStripMenuItem parent, System.Windows.Forms.View view, string text)
    {
      ToolStripMenuItem item = new ToolStripMenuItem(text);
      item.Tag = view;
      item.Checked = browserView.View == view;
      item.Click += ViewMenuItem_Click;
      parent.DropDownItems.Add(item);
    }

    private void ViewMenuItem_Click(object sender, EventArgs e)
    {
      if (sender is ToolStripMenuItem menuItem && menuItem.Tag is System.Windows.Forms.View view)
      {
        if (menuItem.OwnerItem is ToolStripMenuItem parent)
        {
          foreach (ToolStripItem item in parent.DropDownItems)
          {
            if (item is ToolStripMenuItem child)
              child.Checked = child == menuItem;
          }
        }
        SetView(view);
      }
    }

    private void SetView(System.Windows.Forms.View view)
    {
      browserView.LargeImageList = fileImagesLarge;
      browserView.SmallImageList = fileImages;
      browserView.View = view;
      browserView.ShowGroups = view == System.Windows.Forms.View.Details;
      if (view == System.Windows.Forms.View.Tile)
      {
        browserView.TileSize = new Size(400, 48);
      }
      UpdateItemTextForView(view);
      QueueIconLoadingForCurrentItems();
      archiveBrowser.ApplyUserSettings(browserView.Font, GetColumnWidths(), view);
      gedcomBrowser.ApplyUserSettings(browserView.Font, GetColumnWidths(), view);
    }

    private void UpdateItemTextForView(System.Windows.Forms.View view)
    {
      foreach (ListViewItem item in browserView.Items)
      {
        if (item.SubItems.Count < 2)
          continue;

        if (view == System.Windows.Forms.View.Details)
        {
          item.Text = string.Empty;
        }
        else
        {
          if (string.IsNullOrEmpty(item.Text))
          {
            item.Text = item.SubItems[1].Text;
          }
        }
      }
    }

    private void ApplySorting()
    {
      browserView.ListViewItemSorter = new ListViewItemComparer(sortColumn, sortAscending);
      browserView.Sort();
    }

    private void browserView_ColumnClick(object sender, ColumnClickEventArgs e)
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

      ApplySorting();
    }

    public async Task<bool> EnterArchiveAsync(string archivePath)
    {
      if (string.IsNullOrWhiteSpace(archivePath) ||
          (!FileTypeClassifier.IsSupportedArchive(archivePath) && !FileTypeClassifier.TryDetectArchiveFormat(archivePath, out _)))
      {
        return false;
      }

      if (gedcomMode)
      {
        ExitGedcom(false, false);
      }

      archiveReturnFileName = Path.GetFileName(archivePath);
      archiveMode = true;
      selectedFiles = Array.Empty<string>();
      browserView.Visible = false;
      addressBarCurrentPath.Visible = false;
      archiveBrowser.Visible = true;
      archiveBrowser.BringToFront();
      ConfigureDirectoryWatcher();
      RaiseSelectionChanged();

      bool previousSuppressArchiveHistory = suppressArchiveHistory;
      suppressArchiveHistory = true;
      bool opened;
      try
      {
        opened = await archiveBrowser.OpenArchiveAsync(archivePath);
      }
      finally
      {
        suppressArchiveHistory = previousSuppressArchiveHistory;
      }

      if (!opened)
      {
        ExitArchive(true, false);
        return false;
      }

      RecordHistory(new BrowserHistoryEntry
      {
        IsArchive = true,
        Location = archiveBrowser.ArchivePath,
        InternalPath = archiveBrowser.InternalPath
      });
      ArchiveDeviceChanged?.Invoke(this, new ArchiveDeviceChangedEventArgs(true, archiveBrowser.ArchivePath));
      PathChange?.Invoke(this, DisplayLocation);
      return true;
    }

    public async Task<bool> OpenSelectedArchiveBySignatureAsync()
    {
      if (archiveMode || browserView.SelectedItems.Count != 1)
      {
        return false;
      }

      string selectedPath = browserView.SelectedItems[0].Tag as string;
      return File.Exists(selectedPath) && FileTypeClassifier.TryDetectArchiveFormat(selectedPath, out _)
        ? await EnterArchiveAsync(selectedPath)
        : false;
    }

    public async Task<bool> OpenSelectedContainerAsync()
    {
      if (IsVirtualMode || browserView.SelectedItems.Count != 1)
      {
        return false;
      }

      string selectedPath = browserView.SelectedItems[0].Tag as string;
      if (!File.Exists(selectedPath))
      {
        return false;
      }

      if (string.Equals(Path.GetExtension(selectedPath), ".ged", StringComparison.OrdinalIgnoreCase))
      {
        return await EnterGedcomAsync(selectedPath);
      }

      return await OpenSelectedArchiveBySignatureAsync();
    }

    public async Task<bool> EnterGedcomAsync(string gedcomPath)
    {
      if (string.IsNullOrWhiteSpace(gedcomPath) || !File.Exists(gedcomPath) ||
          !string.Equals(Path.GetExtension(gedcomPath), ".ged", StringComparison.OrdinalIgnoreCase))
      {
        return false;
      }

      if (archiveMode)
      {
        ExitArchive(false, false);
      }

      gedcomReturnFileName = Path.GetFileName(gedcomPath);
      gedcomMode = true;
      selectedFiles = Array.Empty<string>();
      browserView.Visible = false;
      addressBarCurrentPath.Visible = false;
      gedcomBrowser.Visible = true;
      gedcomBrowser.BringToFront();
      ConfigureDirectoryWatcher();
      RaiseSelectionChanged();

      bool opened = await gedcomBrowser.OpenGedcomAsync(gedcomPath);
      if (!opened)
      {
        ExitGedcom(true, false);
        return false;
      }

      RecordHistory(new BrowserHistoryEntry
      {
        IsGedcom = true,
        Location = gedcomBrowser.GedcomPath
      });
      PathChange?.Invoke(this, DisplayLocation);
      return true;
    }

    public async Task<string> MaterializeSelectedArchiveFileAsync()
    {
      return archiveMode
        ? await archiveBrowser.MaterializeSelectedFileAsync()
        : null;
    }

    public void ActivateArchiveDevice()
    {
      if (!archiveMode)
      {
        return;
      }

      archiveBrowser.Navigate(string.Empty);
    }

    public void ExitArchive(bool restoreSelection, bool recordHistory = true)
    {
      if (!archiveMode)
      {
        return;
      }

      string fileToSelect = archiveReturnFileName;
      archiveMode = false;
      archiveBrowser.Visible = false;
      browserView.Visible = true;
      addressBarCurrentPath.Visible = true;
      browserView.BringToFront();
      addressBarCurrentPath.BringToFront();
      ConfigureDirectoryWatcher();
      if (restoreSelection && !string.IsNullOrWhiteSpace(fileToSelect))
      {
        selectFile(fileToSelect);
      }

      ArchiveDeviceChanged?.Invoke(this, new ArchiveDeviceChangedEventArgs(false, archiveBrowser.ArchivePath));
      if (recordHistory)
      {
        RecordHistory(new BrowserHistoryEntry
        {
          Location = CurrentPath,
          IsArchive = false,
          SelectedLocation = browserView.SelectedItems.Count == 1 ? browserView.SelectedItems[0].Tag as string : null
        });
      }

      PathChange?.Invoke(this, CurrentPath);
      RaiseLocationChanged(CurrentPath);
      RaiseSelectionChanged();
      browserView.Focus();
    }

    public void ExitGedcom(bool restoreSelection, bool recordHistory = true)
    {
      if (!gedcomMode)
      {
        return;
      }

      string fileToSelect = gedcomReturnFileName;
      gedcomMode = false;
      gedcomBrowser.Visible = false;
      browserView.Visible = true;
      addressBarCurrentPath.Visible = true;
      browserView.BringToFront();
      addressBarCurrentPath.BringToFront();
      ConfigureDirectoryWatcher();
      if (restoreSelection && !string.IsNullOrWhiteSpace(fileToSelect))
      {
        selectFile(fileToSelect);
      }

      if (recordHistory)
      {
        RecordHistory(new BrowserHistoryEntry
        {
          Location = CurrentPath,
          SelectedLocation = browserView.SelectedItems.Count == 1 ? browserView.SelectedItems[0].Tag as string : null
        });
      }

      PathChange?.Invoke(this, CurrentPath);
      RaiseLocationChanged(CurrentPath);
      RaiseSelectionChanged();
      browserView.Focus();
    }

    private void ArchiveBrowser_SelectionChanged(object sender, EventArgs e)
    {
      selectedFiles = Array.Empty<string>();
      UpdateCurrentHistorySelection();
      RaiseSelectionChanged();
    }

    private void ArchiveBrowser_LocationChanged(object sender, BrowserLocationChangedEventArgs e)
    {
      RaiseLocationChanged(e.Location);
      if (!archiveMode)
      {
        return;
      }

      if (!suppressArchiveHistory)
      {
        RecordHistory(new BrowserHistoryEntry
        {
          IsArchive = true,
          Location = archiveBrowser.ArchivePath,
          InternalPath = archiveBrowser.InternalPath
        });
      }

      PathChange?.Invoke(this, e.Location);
    }

    private void GedcomBrowser_SelectionChanged(object sender, EventArgs e)
    {
      selectedFiles = Array.Empty<string>();
      UpdateCurrentHistorySelection();
      RaiseSelectionChanged();
    }

    private void GedcomBrowser_LocationChanged(object sender, BrowserLocationChangedEventArgs e)
    {
      RaiseLocationChanged(e.Location);
      if (gedcomMode)
      {
        PathChange?.Invoke(this, e.Location);
      }
    }

    private void RecordHistory(BrowserHistoryEntry entry)
    {
      if (navigatingHistory || entry == null || string.IsNullOrWhiteSpace(entry.Location))
      {
        return;
      }

      UpdateCurrentHistorySelection();
      if (navigationHistoryIndex >= 0 && navigationHistoryIndex < navigationHistory.Count)
      {
        BrowserHistoryEntry current = navigationHistory[navigationHistoryIndex];
        if (current.IsSameLocation(entry))
        {
          current.InternalPath = entry.InternalPath;
          return;
        }
      }

      if (navigationHistoryIndex < navigationHistory.Count - 1)
      {
        navigationHistory.RemoveRange(navigationHistoryIndex + 1, navigationHistory.Count - navigationHistoryIndex - 1);
      }

      navigationHistory.Add(entry);
      navigationHistoryIndex = navigationHistory.Count - 1;
    }

    private void UpdateCurrentHistorySelection()
    {
      if (navigationHistoryIndex < 0 || navigationHistoryIndex >= navigationHistory.Count)
      {
        return;
      }

      BrowserHistoryEntry current = navigationHistory[navigationHistoryIndex];
      if (archiveMode && current.IsArchive &&
          string.Equals(current.Location, archiveBrowser.ArchivePath, StringComparison.OrdinalIgnoreCase) &&
          string.Equals(current.InternalPath ?? string.Empty, archiveBrowser.InternalPath ?? string.Empty, StringComparison.OrdinalIgnoreCase))
      {
        current.SelectedLocation = archiveBrowser.SelectedItems.Count == 1
          ? archiveBrowser.SelectedItems[0].Location
          : null;
      }
      else if (gedcomMode && current.IsGedcom &&
               string.Equals(current.Location, gedcomBrowser.GedcomPath, StringComparison.OrdinalIgnoreCase))
      {
        current.SelectedLocation = gedcomBrowser.SelectedItems.Count == 1
          ? gedcomBrowser.SelectedItems[0].Location
          : null;
      }
      else if (!IsVirtualMode && !current.IsArchive && !current.IsGedcom &&
               string.Equals(current.Location, CurrentPath, StringComparison.OrdinalIgnoreCase))
      {
        current.SelectedLocation = browserView.SelectedItems.Count == 1
          ? browserView.SelectedItems[0].Tag as string
          : null;
      }
    }

    public void Refrech() {
      if (archiveMode)
      {
        archiveBrowser.RefreshPanel();
        return;
      }
      if (gedcomMode)
      {
        gedcomBrowser.RefreshPanel();
        return;
      }

      browseTo(CurrentPath);
    }

    public void RefreshCurrentDirectory()
    {
      if (archiveMode)
      {
        archiveBrowser.RefreshPanel();
        return;
      }
      if (gedcomMode)
      {
        gedcomBrowser.RefreshPanel();
        return;
      }

      if (string.IsNullOrWhiteSpace(CurrentPath))
        return;

      Refrech();
    }

    public BrowserColumnWidths GetColumnWidths()
    {
      return new BrowserColumnWidths(
        browserView.Columns[1].Width,
        browserView.Columns[2].Width,
        browserView.Columns[3].Width,
        browserView.Columns[4].Width);
    }

    public BrowserStatusInfo GetStatusInfo()
    {
      if (archiveMode)
      {
        BrowserItemInfo[] archiveItems = archiveBrowser.Items.ToArray();
        BrowserItemInfo[] selectedArchiveItems = archiveBrowser.SelectedItems.ToArray();
        BrowserStatusInfo archiveInfo = new BrowserStatusInfo
        {
          DirectoryCount = archiveItems.Count(item => item.IsDirectory),
          FileCount = archiveItems.Count(item => !item.IsDirectory),
          SelectedCount = selectedArchiveItems.Length,
          SelectedDirectoryCount = selectedArchiveItems.Count(item => item.IsDirectory),
          SelectedFileCount = selectedArchiveItems.Count(item => !item.IsDirectory)
        };

        if (selectedArchiveItems.Length == 1)
        {
          BrowserItemInfo selected = selectedArchiveItems[0];
          archiveInfo.CurrentItemName = selected.Name;
          archiveInfo.CurrentItemPath = archiveBrowser.ArchivePath + " :: /" + selected.Location;
          archiveInfo.CurrentItemModifiedText = selected.Modified?.ToString("g");
          archiveInfo.CurrentItemIsDirectory = selected.IsDirectory;
          archiveInfo.CurrentItemSizeBytes = selected.Size;
        }

        return archiveInfo;
      }
      if (gedcomMode)
      {
        BrowserItemInfo[] gedcomItems = gedcomBrowser.Items.ToArray();
        BrowserItemInfo[] selectedGedcomItems = gedcomBrowser.SelectedItems.ToArray();
        BrowserStatusInfo gedcomInfo = new BrowserStatusInfo
        {
          FileCount = gedcomItems.Length,
          SelectedCount = selectedGedcomItems.Length,
          SelectedFileCount = selectedGedcomItems.Length
        };
        if (selectedGedcomItems.Length == 1)
        {
          BrowserItemInfo selected = selectedGedcomItems[0];
          gedcomInfo.CurrentItemName = selected.Name;
          gedcomInfo.CurrentItemPath = gedcomBrowser.GedcomPath + " :: " + selected.Location;
          gedcomInfo.CurrentItemModifiedText = selected.Modified?.ToShortDateString();
        }
        return gedcomInfo;
      }

      BrowserStatusInfo info = new BrowserStatusInfo();

      foreach (ListViewItem item in browserView.Items)
      {
        if (item == null || IsParentNavigationItem(item))
          continue;

        if (IsDirectoryItem(item))
          info.DirectoryCount++;
        else
          info.FileCount++;
      }

      info.SelectedCount = browserView.SelectedItems.Count;
      foreach (ListViewItem item in browserView.SelectedItems)
      {
        if (item == null || IsParentNavigationItem(item))
          continue;

        if (IsDirectoryItem(item))
          info.SelectedDirectoryCount++;
        else
          info.SelectedFileCount++;
      }

      if (browserView.SelectedItems.Count == 1)
      {
        ListViewItem selectedItem = browserView.SelectedItems[0];
        info.CurrentItemName = selectedItem.SubItems.Count > 1 ? selectedItem.SubItems[1].Text : null;
        info.CurrentItemPath = selectedItem.Tag as string;
        info.CurrentItemModifiedText = selectedItem.SubItems.Count > 4 ? selectedItem.SubItems[4].Text : null;
        info.CurrentItemIsDirectory = IsDirectoryItem(selectedItem);

        if (!info.CurrentItemIsDirectory &&
            !string.IsNullOrWhiteSpace(info.CurrentItemPath) &&
            File.Exists(info.CurrentItemPath))
        {
          try
          {
            info.CurrentItemSizeBytes = new FileInfo(info.CurrentItemPath).Length;
          }
          catch
          {
            info.CurrentItemSizeBytes = null;
          }
        }
      }

      return info;
    }

    private void CopyComplete(int result) {
      Refrech();
    }

    private void QueueIconLoadingForCurrentItems()
    {
      List<IconLoadRequest> iconRequests = new List<IconLoadRequest>();
      foreach (ListViewItem item in browserView.Items)
      {
        if (item?.Tag is string iconPath && !string.IsNullOrWhiteSpace(iconPath))
        {
          bool isDirectory = IsDirectoryItem(item);
          iconRequests.Add(new IconLoadRequest(item, iconPath, isDirectory));
        }
      }

      QueueIconLoading(browseGeneration, iconRequests);
    }

    private void QueueIconLoading(int generation, List<IconLoadRequest> iconRequests)
    {
      if (!Properties.Settings.Default.FileBrowserLoadIcons)
        return;

      if (iconRequests == null || iconRequests.Count == 0)
        return;

      bool loadLargeIcons = ShouldLoadLargeIcons();
      List<IconLoadBatch> iconBatches = BuildIconBatches(iconRequests);
      Task.Run(() => LoadIconsAsync(generation, iconBatches, loadLargeIcons));
    }

    private List<IconLoadBatch> BuildIconBatches(List<IconLoadRequest> iconRequests)
    {
      Dictionary<string, IconLoadBatch> batches = new Dictionary<string, IconLoadBatch>(StringComparer.OrdinalIgnoreCase);
      foreach (IconLoadRequest request in iconRequests)
      {
        if (!batches.TryGetValue(request.LookupCacheKey, out IconLoadBatch batch))
        {
          batch = new IconLoadBatch(request.IconPath, request.IsDirectory, request.LookupCacheKey);
          batches.Add(request.LookupCacheKey, batch);
        }

        batch.Items.Add(request.Item);
      }

      return batches.Values.ToList();
    }

    private void LoadIconsAsync(int generation, List<IconLoadBatch> iconBatches, bool loadLargeIcons)
    {
      foreach (IconLoadBatch batch in iconBatches)
      {
        FileIconData smallIcon = FileIconCache.GetIconData(batch.IconPath, batch.IsDirectory, false);
        FileIconData largeIcon = loadLargeIcons
          ? FileIconCache.GetIconData(batch.IconPath, batch.IsDirectory, true)
          : null;

        if (smallIcon == null && largeIcon == null)
          continue;

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
    }

    private void ApplyIconResult(int generation, List<ListViewItem> items, FileIconData smallIcon, FileIconData largeIcon)
    {
      using (smallIcon)
      using (largeIcon)
      {
        string key = smallIcon?.Key ?? largeIcon?.Key;
        if (generation != browseGeneration || items == null || items.Count == 0 || string.IsNullOrEmpty(key))
          return;

        int imageIndex = fileImages.Images.IndexOfKey(key);
        if (imageIndex == -1)
        {
          Icon smallToStore = smallIcon != null ? (Icon)smallIcon.Icon.Clone() : (Icon)largeIcon.Icon.Clone();
          Icon largeToStore = largeIcon != null ? (Icon)largeIcon.Icon.Clone() : (Icon)smallIcon.Icon.Clone();
          fileImages.Images.Add(key, smallToStore);
          fileImagesLarge.Images.Add(key, largeToStore);
          imageIndex = fileImages.Images.Count - 1;
        }
        else if (largeIcon != null)
        {
          using Bitmap replacement = largeIcon.Icon.ToBitmap();
          fileImagesLarge.Images[imageIndex] = replacement;
        }

        foreach (ListViewItem item in items)
        {
          if (item != null && item.ListView == browserView)
          {
            item.ImageIndex = imageIndex;
          }
        }
      }
    }

    private bool ShouldLoadLargeIcons()
    {
      if (!Properties.Settings.Default.FileBrowserLoadLargeIcons)
        return false;

      return browserView.View == System.Windows.Forms.View.LargeIcon || browserView.View == System.Windows.Forms.View.Tile;
    }

    private bool IsDirectoryItem(ListViewItem item)
    {
      if (item == null || item.SubItems.Count < 2)
        return false;

      if (IsParentNavigationItem(item))
        return true;

      return item.Group == browserView.Groups[0];
    }

    private static bool IsParentNavigationItem(ListViewItem item)
    {
      return item != null && item.SubItems.Count > 1 && item.SubItems[1].Text == "..";
    }

    private void refrechSize(ListViewItem item) {
      String selectedPath = item.Tag as String;
      long length = -1;
      if (Directory.Exists(selectedPath)) {
        length = getDirectorySize(new DirectoryInfo(selectedPath));
      }
      else {
        length = (new FileInfo(selectedPath)).Length;
      }
      item.SubItems[3].Text = String.Format("{0:0,0}", length);
      item.Selected = true;
    }

    private long getDirectorySize(DirectoryInfo dir) {
      long length = 0;

      // Get Size from all Files
      foreach (FileInfo file in dir.GetFiles()) {
        length += file.Length;
      }

      // Get Size from subdirectory using recursion.
      foreach (DirectoryInfo searchDir in dir.GetDirectories()) {
        length += getDirectorySize(searchDir);
      }
      return length;
    }

    private async void browserView_KeyDown(object sender, KeyEventArgs e) {
      if (e.Control && e.KeyCode == Keys.PageDown && browserView.SelectedItems.Count == 1)
      {
        await OpenSelectedContainerAsync();

        e.Handled = true;
        e.SuppressKeyPress = true;
        return;
      }

      if (e.KeyCode == Keys.Back)
      {
        await NavigateBackAsync();
        e.Handled = true;
        e.SuppressKeyPress = true;
        return;
      }

      if (e.KeyCode == Keys.Up && e.Alt)
      {
        NavigateParent();
        e.Handled = true;
        e.SuppressKeyPress = true;
        return;
      }

      if (e.KeyCode == System.Windows.Forms.Keys.Enter && e.Shift && browserView.SelectedItems.Count > 0) {
        string selectedPath = browserView.SelectedItems[0].Tag as string;
        if (WinContextMenu.RunInPersistentConsole(selectedPath))
        {
          e.Handled = true;
          e.SuppressKeyPress = true;
          return;
        }
      }

      if (e.KeyCode == System.Windows.Forms.Keys.Space && browserView.SelectedItems.Count > 0) {
        // Get Size from Selected Item
        refrechSize(browserView.SelectedItems[0]);
      }
    }

    private void browserView_ItemDrag(object sender, ItemDragEventArgs e)
    {
      string[] dragPaths = GetSelectedPathsForDragDrop();
      if (dragPaths.Length == 0)
        return;

      DataObject dataObject = new DataObject();
      dataObject.SetData(DataFormats.FileDrop, true, dragPaths);
      dataObject.SetData(InternalDragSourceFormat, CurrentPath ?? string.Empty);

      DragDropEffects allowedEffects = DragDropEffects.Copy | DragDropEffects.Move;
      browserView.DoDragDrop(dataObject, allowedEffects);
    }

    private void browserView_DragEnter(object sender, DragEventArgs e)
    {
      e.Effect = GetDragDropEffect(e);
    }

    private void browserView_DragOver(object sender, DragEventArgs e)
    {
      e.Effect = GetDragDropEffect(e);

      Point clientPoint = browserView.PointToClient(new Point(e.X, e.Y));
      ListViewItem hoveredItem = browserView.GetItemAt(clientPoint.X, clientPoint.Y);
      if (hoveredItem != null)
      {
        hoveredItem.Selected = true;
        hoveredItem.Focused = true;
      }
      else if (browserView.Items.Count > 0)
      {
        browserView.Focus();
      }
    }

    private void browserView_DragDrop(object sender, DragEventArgs e)
    {
      string[] sourcePaths = ExtractDroppedPaths(e.Data);
      if (sourcePaths.Length == 0)
        return;

      string destinationPath = ResolveDropDestination(e);
      if (string.IsNullOrWhiteSpace(destinationPath) || !Directory.Exists(destinationPath))
        return;

      FormCopy.Type operationType = ResolveDropOperationType(e, sourcePaths, destinationPath);
      CopyWindow = new FormCopy(sourcePaths, destinationPath, operationType);
      CopyWindow.ActionComplete += new FormCopy.ActionCompleteHandler(this.CopyComplete);
      CopyWindow.ShowDialog(FindForm());
    }

    private string[] GetSelectedPathsForDragDrop()
    {
      return browserView.SelectedItems
        .Cast<ListViewItem>()
        .Where(item => item?.Tag is string path
          && !string.IsNullOrWhiteSpace(path)
          && item.SubItems.Count > 1
          && item.SubItems[1].Text != "..")
        .Select(item => item.Tag as string)
        .Where(path => FileSystemService.FileExists(path) || FileSystemService.DirectoryExists(path))
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .ToArray();
    }

    private static string[] ExtractDroppedPaths(IDataObject data)
    {
      if (data == null || !data.GetDataPresent(DataFormats.FileDrop))
        return Array.Empty<string>();

      return data.GetData(DataFormats.FileDrop) as string[] ?? Array.Empty<string>();
    }

    private DragDropEffects GetDragDropEffect(DragEventArgs e)
    {
      string[] sourcePaths = ExtractDroppedPaths(e.Data);
      if (sourcePaths.Length == 0)
        return DragDropEffects.None;

      string destinationPath = ResolveDropDestination(e);
      if (string.IsNullOrWhiteSpace(destinationPath) || !Directory.Exists(destinationPath))
        return DragDropEffects.None;

      FormCopy.Type operationType = ResolveDropOperationType(e, sourcePaths, destinationPath);
      return operationType == FormCopy.Type.Move ? DragDropEffects.Move : DragDropEffects.Copy;
    }

    private string ResolveDropDestination(DragEventArgs e)
    {
      if (string.IsNullOrWhiteSpace(CurrentPath))
        return null;

      Point clientPoint = browserView.PointToClient(new Point(e.X, e.Y));
      ListViewItem hoveredItem = browserView.GetItemAt(clientPoint.X, clientPoint.Y);
      if (hoveredItem?.Tag is string hoveredPath && Directory.Exists(hoveredPath))
      {
        return hoveredPath;
      }

      return CurrentPath;
    }

    private FormCopy.Type ResolveDropOperationType(DragEventArgs e, string[] sourcePaths, string destinationPath)
    {
      int keyState = e.KeyState;
      bool controlPressed = (keyState & 8) == 8;
      bool shiftPressed = (keyState & 4) == 4;

      if (controlPressed)
        return FormCopy.Type.Copy;

      if (shiftPressed)
        return FormCopy.Type.Move;

      if (e.Data != null && e.Data.GetDataPresent(InternalDragSourceFormat))
        return FormCopy.Type.Move;

      if (sourcePaths.Length > 0 && IsSameRoot(sourcePaths[0], destinationPath))
        return FormCopy.Type.Move;

      return FormCopy.Type.Copy;
    }

    private static bool IsSameRoot(string sourcePath, string destinationPath)
    {
      if (string.IsNullOrWhiteSpace(sourcePath) || string.IsNullOrWhiteSpace(destinationPath))
        return false;

      string sourceRoot = Path.GetPathRoot(sourcePath);
      string destinationRoot = Path.GetPathRoot(destinationPath);
      return !string.IsNullOrWhiteSpace(sourceRoot)
        && string.Equals(sourceRoot, destinationRoot, StringComparison.OrdinalIgnoreCase);
    }

    public void selectFile(String name) {
      foreach (ListViewItem item in browserView.Items) {
        if (string.Equals(item.SubItems[1].Text, name, StringComparison.OrdinalIgnoreCase)) {
          item.Selected = true;
          item.Focused = true;
          item.EnsureVisible();
        }
        else {
          item.Selected = false;
        }
      }
    }

    public string GetCurrentItemName()
    {
      if (archiveMode)
      {
        return archiveBrowser.CurrentItemName;
      }
      if (gedcomMode)
      {
        return gedcomBrowser.CurrentItemName;
      }

      if (browserView.SelectedItems.Count == 0)
        return null;

      ListViewItem currentItem = browserView.FocusedItem?.Selected == true
        ? browserView.FocusedItem
        : browserView.SelectedItems[0];
      string itemName = currentItem.SubItems.Count > 1
        ? currentItem.SubItems[1].Text
        : null;

      if (string.IsNullOrWhiteSpace(itemName) || itemName == "..")
        return null;

      return itemName;
    }

    private BrowserItemInfo CreateBrowserItemInfo(ListViewItem item)
    {
      bool isDirectory = IsDirectoryItem(item);
      long? size = null;
      if (!isDirectory && item.SubItems.Count > 3)
      {
        string digits = new string(item.SubItems[3].Text.Where(char.IsDigit).ToArray());
        if (long.TryParse(digits, out long parsedSize))
        {
          size = parsedSize;
        }
      }

      DateTime? modified = null;
      if (item.SubItems.Count > 4 && DateTime.TryParse(item.SubItems[4].Text, out DateTime parsedDate))
      {
        modified = parsedDate;
      }

      return new BrowserItemInfo
      {
        Name = item.SubItems.Count > 1 ? item.SubItems[1].Text : item.Text,
        Location = item.Tag as string,
        NativePath = item.Tag as string,
        IsDirectory = isDirectory,
        Size = size,
        Modified = modified
      };
    }


    private void addressBar_ButtonClick(object sender, EventArgs e) {
      browseTo((sender as Button).Tag as String);
    }

    private void addressBarCurrentPath_PathChange(object sender, string newPath) {
      newPath = NormalizeEnteredPath(newPath);
      if (string.IsNullOrWhiteSpace(newPath))
        return;

      if (TryBrowseToFilePath(newPath))
        return;

      browseTo(newPath);
    }

    private bool TryBrowseToFilePath(string path)
    {
      path = NormalizeEnteredPath(path);
      if (string.IsNullOrWhiteSpace(path))
        return false;

      string directoryPath = Path.GetDirectoryName(path);
      string fileName = Path.GetFileName(path);
      if (string.IsNullOrWhiteSpace(directoryPath) || string.IsNullOrWhiteSpace(fileName))
        return false;

      bool fileExists = FileSystemService.FileExists(path);
      bool parentDirectoryExists = FileSystemService.DirectoryExists(directoryPath);
      if (!fileExists && !parentDirectoryExists)
        return false;

      if (browseTo(directoryPath) == null)
        return true;

      selectFile(fileName);
      return true;
    }

    private static string NormalizeEnteredPath(string path)
    {
      return string.IsNullOrWhiteSpace(path)
        ? string.Empty
        : path.Trim().Trim('"');
    }

    private void ShowBrowseError(string messageTemplate, string path)
    {
      string message = string.Format(messageTemplate, path);
      MessageBox.Show(FindForm(), message, Language.getString("error"), MessageBoxButtons.OK, MessageBoxIcon.Error);
    }

    private void FileBrowser_SizeChanged(object sender, EventArgs e) {
      FileBrowser self =   sender as FileBrowser;
      browserView.Width = self.Width-2; 
      browserView.Height = self.Height-56;
      addressBarCurrentPath.Width = self.Width - 2;
    }

    private void ConfigureDirectoryWatcher()
    {
      bool shouldWatch = Properties.Settings.Default.FileBrowserWatchDirectoryChanges
        && !IsVirtualMode
        && !string.IsNullOrWhiteSpace(CurrentPath)
        && Directory.Exists(CurrentPath);

      if (!shouldWatch)
      {
        if (directoryWatcher != null)
        {
          directoryWatcher.EnableRaisingEvents = false;
        }

        directoryRefreshTimer.Stop();
        pendingDirectoryRefresh = false;
        return;
      }

      if (directoryWatcher == null)
      {
        directoryWatcher = new FileSystemWatcher();
        directoryWatcher.IncludeSubdirectories = false;
        directoryWatcher.NotifyFilter = NotifyFilters.DirectoryName | NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.Size;
        directoryWatcher.Changed += DirectoryWatcher_Changed;
        directoryWatcher.Created += DirectoryWatcher_Changed;
        directoryWatcher.Deleted += DirectoryWatcher_Changed;
        directoryWatcher.Renamed += DirectoryWatcher_Renamed;
        directoryWatcher.Error += DirectoryWatcher_Error;
      }

      bool watcherWasEnabled = directoryWatcher.EnableRaisingEvents;
      if (watcherWasEnabled)
      {
        directoryWatcher.EnableRaisingEvents = false;
      }

      directoryWatcher.Path = CurrentPath;
      directoryWatcher.EnableRaisingEvents = true;
    }

    private void DirectoryWatcher_Changed(object sender, FileSystemEventArgs e)
    {
      ScheduleDirectoryRefresh();
    }

    private void DirectoryWatcher_Renamed(object sender, RenamedEventArgs e)
    {
      ScheduleDirectoryRefresh();
    }

    private void DirectoryWatcher_Error(object sender, ErrorEventArgs e)
    {
      ScheduleDirectoryRefresh();
    }

    private void ScheduleDirectoryRefresh()
    {
      pendingDirectoryRefresh = true;
      if (!IsHandleCreated)
        return;

      try
      {
        BeginInvoke(new Action(() =>
        {
          directoryRefreshTimer.Stop();
          directoryRefreshTimer.Start();
        }));
      }
      catch (InvalidOperationException)
      {
      }
    }

    private void DirectoryRefreshTimer_Tick(object sender, EventArgs e)
    {
      directoryRefreshTimer.Stop();
      if (!pendingDirectoryRefresh)
        return;

      pendingDirectoryRefresh = false;
      if (!Properties.Settings.Default.FileBrowserWatchDirectoryChanges)
        return;

      if (string.IsNullOrWhiteSpace(CurrentPath) || !Directory.Exists(CurrentPath))
        return;

      Refrech();
    }

    private class ListViewItemComparer : IComparer
    {
      private readonly int column;
      private readonly bool ascending;

      public ListViewItemComparer(int column, bool ascending)
      {
        this.column = column;
        this.ascending = ascending;
      }

      public int Compare(object x, object y)
      {
        var itemX = x as ListViewItem;
        var itemY = y as ListViewItem;
        if (itemX == null || itemY == null)
          return 0;

        int result = CompareItems(itemX, itemY);
        return ascending ? result : -result;
      }

      private int CompareItems(ListViewItem x, ListViewItem y)
      {
        string nameX = GetName(x);
        string nameY = GetName(y);

        bool xParent = IsParentItem(nameX);
        bool yParent = IsParentItem(nameY);
        if (xParent || yParent)
        {
          if (xParent && !yParent) return -1;
          if (!xParent && yParent) return 1;
        }

        bool xDir = IsDirectory(x);
        bool yDir = IsDirectory(y);
        if (xDir != yDir)
        {
          return xDir ? -1 : 1;
        }

        if (column == 3)
        {
          long sizeX = ParseSize(GetColumnText(x, column));
          long sizeY = ParseSize(GetColumnText(y, column));
          int compare = sizeX.CompareTo(sizeY);
          return compare != 0 ? compare : StringComparer.CurrentCultureIgnoreCase.Compare(GetName(x), GetName(y));
        }
        else if (column == 4)
        {
          DateTime dateX = ParseDate(GetColumnText(x, column));
          DateTime dateY = ParseDate(GetColumnText(y, column));
          int compare = dateX.CompareTo(dateY);
          return compare != 0 ? compare : StringComparer.CurrentCultureIgnoreCase.Compare(GetName(x), GetName(y));
        }
        else
        {
          return StringComparer.CurrentCultureIgnoreCase.Compare(
            GetColumnText(x, column),
            GetColumnText(y, column));
        }
      }

      private static bool IsParentItem(string name) => name == "..";

      private static string GetName(ListViewItem item)
      {
        return item.SubItems.Count > 1 ? item.SubItems[1].Text : item.Text;
      }

      private static string GetColumnText(ListViewItem item, int col)
      {
        if (col == 0)
          col = 1;
        if (col < item.SubItems.Count)
          return item.SubItems[col].Text;
        return item.Text;
      }

      private static bool IsDirectory(ListViewItem item)
      {
        string path = item.Tag as string;
        if (string.IsNullOrEmpty(path))
          return false;
        return Directory.Exists(path);
      }

      private static long ParseSize(string text)
      {
        if (string.IsNullOrWhiteSpace(text))
          return 0;
        string normalized = new string(text.Where(char.IsDigit).ToArray());
        if (long.TryParse(normalized, out long value))
          return value;
        return 0;
      }

      private static DateTime ParseDate(string text)
      {
        if (DateTime.TryParse(text, out DateTime value))
          return value;
        return DateTime.MinValue;
      }
    }

    public readonly struct BrowserColumnWidths
    {
      public BrowserColumnWidths(int nameWidth, int typeWidth, int sizeWidth, int dateWidth)
      {
        NameWidth = nameWidth;
        TypeWidth = typeWidth;
        SizeWidth = sizeWidth;
        DateWidth = dateWidth;
      }

      public int NameWidth { get; }
      public int TypeWidth { get; }
      public int SizeWidth { get; }
      public int DateWidth { get; }
    }

    public sealed class BrowserStatusInfo
    {
      public int DirectoryCount { get; set; }
      public int FileCount { get; set; }
      public int SelectedCount { get; set; }
      public int SelectedDirectoryCount { get; set; }
      public int SelectedFileCount { get; set; }
      public string CurrentItemName { get; set; }
      public string CurrentItemPath { get; set; }
      public string CurrentItemModifiedText { get; set; }
      public bool CurrentItemIsDirectory { get; set; }
      public long? CurrentItemSizeBytes { get; set; }
    }

    private sealed class IconLoadRequest
    {
      public IconLoadRequest(ListViewItem item, string iconPath, bool isDirectory)
      {
        Item = item;
        IconPath = iconPath;
        IsDirectory = isDirectory;
        LookupCacheKey = BuildLookupCacheKey(iconPath, isDirectory);
      }

      public ListViewItem Item { get; }
      public string IconPath { get; }
      public bool IsDirectory { get; }
      public string LookupCacheKey { get; }

      private static string BuildLookupCacheKey(string iconPath, bool isDirectory)
      {
        if (isDirectory)
          return "<dir>";

        string extension = Path.GetExtension(iconPath);
        if (!string.IsNullOrWhiteSpace(extension))
          return extension.ToLowerInvariant();

        return "<file>";
      }
    }

    private sealed class IconLoadBatch
    {
      public IconLoadBatch(string iconPath, bool isDirectory, string lookupCacheKey)
      {
        IconPath = iconPath;
        IsDirectory = isDirectory;
        LookupCacheKey = lookupCacheKey;
        Items = new List<ListViewItem>();
      }

      public string IconPath { get; }
      public bool IsDirectory { get; }
      public string LookupCacheKey { get; }
      public List<ListViewItem> Items { get; }
    }
  }

  public class SelectionPath
    {
        public String path;        /**< Path */
        public String selectedPath;   /**< Selected Item in the Path */
    }

  internal sealed class ArchiveDeviceChangedEventArgs : EventArgs
  {
    public ArchiveDeviceChangedEventArgs(bool isMounted, string archivePath)
    {
      IsMounted = isMounted;
      ArchivePath = archivePath;
    }

    public bool IsMounted { get; }
    public string ArchivePath { get; }
  }

  internal sealed class BrowserHistoryEntry
  {
    public bool IsArchive { get; set; }
    public bool IsGedcom { get; set; }
    public string Location { get; set; }
    public string InternalPath { get; set; }
    public string SelectedLocation { get; set; }

    public bool IsSameLocation(BrowserHistoryEntry other)
    {
      return other != null &&
        IsArchive == other.IsArchive &&
        IsGedcom == other.IsGedcom &&
        string.Equals(Location, other.Location, StringComparison.OrdinalIgnoreCase) &&
        (!IsArchive || string.Equals(InternalPath ?? string.Empty, other.InternalPath ?? string.Empty, StringComparison.OrdinalIgnoreCase));
    }
  }

}
