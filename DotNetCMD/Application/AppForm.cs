using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Resources;
using System.Text;
using System.Windows.Forms;
using static System.Windows.Forms.VisualStyles.VisualStyleElement;

namespace DotNetCommander
{
    
    public partial class AppForm : Form
    {
        FileBrowser lastFileBrowser = null;
        private readonly CommandService commandService = new CommandService();
        private readonly Dictionary<FileBrowser, Dictionary<string, string>> browserDrivePaths = new Dictionary<FileBrowser, Dictionary<string, string>>();
        private readonly Dictionary<FileBrowser, ToolStripButton> archiveDeviceButtons = new Dictionary<FileBrowser, ToolStripButton>();
        private QuickViewControl quickViewControl;
        private int quickViewGeneration;
        private bool quickViewEnabled;
        private ToolStripButton quickViewButton;
        private ToolStripButton viewButton;
        private ToolStripButton editButton;
        private ToolStripButton copyButton;
        private ToolStripButton moveButton;
        private ToolStripButton newFolderButton;
        private ToolStripButton deleteButton;
        private readonly ModifierKeyMessageFilter modifierKeyMessageFilter;
        private Keys lastDisplayedModifiers = Keys.None;
        private readonly List<string> commandHistory = new List<string>();
        private int commandHistoryIndex;
        private string commandHistoryDraft = string.Empty;
        private static readonly bool ShowMemoryUsageInTitle = false;

        public AppForm()
        {
            InitializeComponent();
            modifierKeyMessageFilter = new ModifierKeyMessageFilter(UpdateCommandButtonLabels);
            browserDrivePaths[fileBrowserLeft] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            browserDrivePaths[fileBrowserRight] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            RestoreRememberedDrivePaths();

            fileBrowserLeft.SelectionChanged += FileBrowser_SelectionChanged;
            fileBrowserRight.SelectionChanged += FileBrowser_SelectionChanged;
            fileBrowserLeft.ArchiveDeviceChanged += FileBrowser_ArchiveDeviceChanged;
            fileBrowserRight.ArchiveDeviceChanged += FileBrowser_ArchiveDeviceChanged;

            quickViewButton = AddCommandButton(GetQuickViewButtonText(), Button_QuickView, true);
            toolStripButtons.Items.Add(new ToolStripSeparator());

            viewButton = AddCommandButton("F3 " + Language.getString("view"), new EventHandler(Button_View));
            toolStripButtons.Items.Add(new ToolStripSeparator());

            editButton = AddCommandButton("F4 " + Language.getString("edit"), new EventHandler(Button_Edit));
            toolStripButtons.Items.Add(new ToolStripSeparator());

            copyButton = AddCommandButton("F5 " + Language.getString("copy"), new EventHandler(Button_Copy));
            toolStripButtons.Items.Add(new ToolStripSeparator());

            moveButton = AddCommandButton("F6 " + Language.getString("move"), new EventHandler(Button_Move));
            toolStripButtons.Items.Add(new ToolStripSeparator());

            newFolderButton = AddCommandButton("F7 " + Language.getString("newFolder"), new EventHandler(Button_NewFolder));
            toolStripButtons.Items.Add(new ToolStripSeparator());

            deleteButton = AddCommandButton("F8 " + Language.getString("delete"), new EventHandler(Button_Delete));

            ApplyLocalization();
            
            // Включаем обработку клавиш в форме
            this.KeyPreview = true;

            /*
             * Laufwerke Hinzufügen
             */
            OS.GetDriverToolList(toolStripDrivers,toolStripButton_Click);
            ApplyUiSettings();

            AppForm_Resize(this, EventArgs.Empty);
        }

        private ToolStripButton AddCommandButton(String text,EventHandler clickHandler, bool checkable = false)
        {
            ToolStripButton cmdItem = new ToolStripButton();
            cmdItem.Text = text;
            cmdItem.Font = new Font("Segoe UI", 11.0f, FontStyle.Bold);
            cmdItem.AutoSize = false;
            cmdItem.Width = 100;
            cmdItem.Anchor = AnchorStyles.Left | AnchorStyles.Right;
            cmdItem.CheckOnClick = checkable;
            if(clickHandler != null)
                cmdItem.Click += clickHandler;
            toolStripButtons.Items.Add(cmdItem);
            return cmdItem;
        }

        private void ApplyLocalization()
        {
            string menuFile = Language.getString("menuFile");
            string menuEdit = Language.getString("menuEdit");
            string menuView = Language.getString("menuView");
            string menuTools = Language.getString("menuTools");
            string menuHelp = Language.getString("menuHelp");

            this.dateiToolStripMenuItem1.Text = menuFile;
            this.bearbeitenToolStripMenuItem1.Text = menuEdit;
            this.ansichtToolStripMenuItem1.Text = menuView;
            this.toolsToolStripMenuItem.Text = menuTools;
            this.helpToolStripMenuItem.Text = menuHelp;

            this.dateiToolStripMenuItem.Text = menuFile;
            this.bearbeitenToolStripMenuItem.Text = menuEdit;
            this.ansichtToolStripMenuItem.Text = menuView;

            CreateFileMenu(this.dateiToolStripMenuItem1);
            CreateFileMenu(this.dateiToolStripMenuItem);
            CreateEditMenu(this.bearbeitenToolStripMenuItem1);
            CreateEditMenu(this.bearbeitenToolStripMenuItem);
            CreateViewMenu(this.ansichtToolStripMenuItem1);
            CreateToolsMenu(this.toolsToolStripMenuItem);
            CreateHelpMenu(this.helpToolStripMenuItem);

            if (quickViewButton != null)
            {
                quickViewButton.Text = GetQuickViewButtonText();
            }

            UpdateCommandButtonLabels(GetRelevantModifiers(Control.ModifierKeys));
            toolStripTextBoxCommand.ToolTipText = Language.getString("commandLineToolTip");
            UpdateCommandLinePrompt();
            UpdateStatusHint();
        }

    private void CreateFileMenu(ToolStripMenuItem parent) {
      parent.DropDownItems.Clear();
      ToolStripMenuItem newFileItem = CreateMenuItem("newFile", Edit_NewFile);
      newFileItem.ShortcutKeys = Keys.Control | Keys.N;
      parent.DropDownItems.Add(newFileItem);
       ToolStripMenuItem compareMenuItem = CreateMenuItem("compare", Button_Compare);
       compareMenuItem.ShortcutKeys = Keys.Shift | Keys.F3;
       parent.DropDownItems.Add(compareMenuItem);
       parent.DropDownItems.Add(new ToolStripSeparator());
       ToolStripMenuItem createArchiveItem = CreateMenuItem("archiveCreate", Button_CreateArchive);
       createArchiveItem.ShortcutKeys = Keys.Alt | Keys.F5;
       parent.DropDownItems.Add(createArchiveItem);
       ToolStripMenuItem extractArchiveItem = CreateMenuItem("archiveExtract", Button_ExtractArchive);
       extractArchiveItem.ShortcutKeys = Keys.Alt | Keys.F9;
       parent.DropDownItems.Add(extractArchiveItem);
       parent.DropDownItems.Add(new ToolStripSeparator());
    }


    private void CreateEditMenu(ToolStripMenuItem parent)
        {
            parent.DropDownItems.Clear();
            parent.DropDownItems.Add(CreateMenuItem("copy", Button_Copy));
            parent.DropDownItems.Add(CreateMenuItem("cut", Edit_Cut));
            parent.DropDownItems.Add(CreateMenuItem("past", Edit_Paste));
            parent.DropDownItems.Add(new ToolStripSeparator());
            parent.DropDownItems.Add(CreateMenuItem("rename", Edit_Rename));
        }

        private void CreateViewMenu(ToolStripMenuItem parent)
        {
            parent.DropDownItems.Clear();

            ToolStripMenuItem quickViewMenuItem = CreateMenuItem("quickView", Button_QuickViewMenu);
            quickViewMenuItem.ShortcutKeys = Keys.F2;
            quickViewMenuItem.Checked = quickViewEnabled;

            parent.DropDownItems.Add(quickViewMenuItem);
            parent.DropDownItems.Add(CreateStatusBarMenuItem());
        }

        private void CreateToolsMenu(ToolStripMenuItem parent)
        {
            parent.DropDownItems.Clear();
            parent.DropDownItems.Add(CreateMenuItem("options", OpenSettings));
        }

        private void CreateHelpMenu(ToolStripMenuItem parent)
        {
            parent.DropDownItems.Clear();
            parent.DropDownItems.Add(CreateMenuItem("readme", OpenReadme));
            parent.DropDownItems.Add(CreateMenuItem("changeLog", OpenChangeLog));
            parent.DropDownItems.Add(CreateMenuItem("roadmap", OpenRoadmap));
            parent.DropDownItems.Add(new ToolStripSeparator());
            parent.DropDownItems.Add(CreateMenuItem("about", OpenAbout));
        }

        private ToolStripMenuItem CreateMenuItem(string resourceKey, EventHandler handler)
        {
            var item = new ToolStripMenuItem(Language.getString(resourceKey));
            item.Click += handler;
            return item;
        }

        private ToolStripMenuItem CreateStatusBarMenuItem()
        {
            var item = new ToolStripMenuItem(Language.getString("settingsShowStatusHints"))
            {
                Checked = Properties.Settings.Default.ShowStatusHints,
                CheckOnClick = true
            };
            item.Click += ToggleStatusBarVisibility;
            return item;
        }

        private void Edit_Cut(object sender, EventArgs e)
        {
            commandService.CopySelectionToClipboard(lastFileBrowser ?? fileBrowserLeft);
        }

        private void Edit_Paste(object sender, EventArgs e)
        {
            commandService.PasteFromClipboard(lastFileBrowser ?? fileBrowserLeft, CopyComplete, this);
        }

        private void Edit_Rename(object sender, EventArgs e)
        {
            commandService.RenameSelection(lastFileBrowser);
        }

        private void Edit_NewFile(object sender, EventArgs e)
        {
            commandService.CreateNewFile(lastFileBrowser ?? fileBrowserLeft, this);
        }

        private void Edit_NewFileInline(object sender, EventArgs e)
        {
            FileBrowser source = lastFileBrowser ?? fileBrowserLeft;
            commandService.CreateNewFileInline(source, this, source?.GetCurrentItemName());
        }

        private void ToggleStatusBarVisibility(object sender, EventArgs e)
        {
            if (sender is ToolStripMenuItem item)
            {
                Properties.Settings.Default.ShowStatusHints = item.Checked;
                Properties.Settings.Default.Save();
                ApplyUiSettings();
            }
        }

        private string GetQuickViewButtonText()
        {
            return "F2 " + Language.getString("quickView");
        }

        public String ValidPath(String oldPath,String newPath)
        {
            if (newPath == null)
                return oldPath;
            else
                return newPath;
        }

        private void AppForm_Load(object sender, EventArgs e)
        {
            Application.AddMessageFilter(modifierKeyMessageFilter);
            RestoreWindowBounds();

            /*
             * Rechter Browser Initialisieren
             */
            String startPath = OS.GetStartPath();

            if (Properties.Settings.Default.RightBrowserLastPath.Length > 0 && FileSystemService.DirectoryExists(Properties.Settings.Default.RightBrowserLastPath))
                fileBrowserRight.browseTo(Properties.Settings.Default.RightBrowserLastPath);
            else
                fileBrowserRight.browseTo(startPath);

      /*
       * Linker Browser Initialisieren
       */
      if (Properties.Settings.Default.LeftBrowserLastPath.Length > 0 && FileSystemService.DirectoryExists(Properties.Settings.Default.LeftBrowserLastPath))
        fileBrowserLeft.browseTo(Properties.Settings.Default.LeftBrowserLastPath);
      else
        fileBrowserLeft.browseTo(startPath);
            lastFileBrowser = fileBrowserLeft;
            UpdateCommandLinePrompt();
        }

        private void toolStripButton_Click(object sender, EventArgs e)
        {
            ToolStripButton Button = (ToolStripButton)sender;
            if (Button.Tag is ArchiveDeviceTag archiveDevice)
            {
                lastFileBrowser = archiveDevice.Browser;
                archiveDevice.Browser.ActivateArchiveDevice();
                fileBrowser_PathChange(archiveDevice.Browser, archiveDevice.Browser.DisplayLocation);
                return;
            }

            FileBrowser targetBrowser = lastFileBrowser ?? fileBrowserLeft;
            string targetPath = GetDriveNavigationPath(targetBrowser, Button.Tag?.ToString());
            targetBrowser.browseTo(targetPath);
        }

        private void AppForm_Resize(object sender, EventArgs e)
        {
            int buttonCount = toolStripButtons.Items.OfType<ToolStripButton>().Count();
            if (buttonCount == 0)
                return;

            int width = Math.Max(80, (this.Width / buttonCount) - 12);

            foreach(Object ItemObj in toolStripButtons.Items)
            {
                if(ItemObj is ToolStripButton button)
                    button.Width = width;
            }

            if (toolStripTextBoxCommand != null && toolStripLabelCommandPath != null)
            {
                toolStripTextBoxCommand.Width = Math.Max(
                    120,
                    toolStripCommandLine.ClientSize.Width - toolStripLabelCommandPath.Width - 18);
            }

        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            // Обработка горячих клавиш
            if (keyData == (Keys.Control | Keys.PageDown))
            {
                _ = (lastFileBrowser ?? fileBrowserLeft).OpenSelectedContainerAsync();
                return true;
            }
            if (keyData == (Keys.Control | Keys.L))
            {
                toolStripTextBoxCommand.Focus();
                toolStripTextBoxCommand.SelectAll();
                return true;
            }
            if (keyData == (Keys.Control | Keys.Enter))
            {
                InsertCurrentItemNameIntoCommandLine();
                return true;
            }
            if (keyData == (Keys.Control | Keys.Q) || keyData == Keys.F2)
            {
                ToggleQuickView();
                return true;
            }
            if (keyData == Keys.F1)
            {
                ShowKeyboardHelp();
                return true;
            }
            if (keyData == Keys.F3)
            {
                Button_View(null, null);
                return true;
            }
            else if (keyData == (Keys.Shift | Keys.F3))
            {
                Button_Compare(null, null);
                return true;
            }
            else if (keyData == Keys.F4)
            {
                Button_Edit(null, null);
                return true;
            }
            else if (keyData == (Keys.Shift | Keys.F4))
            {
                Edit_NewFileInline(null, null);
                return true;
            }
            else if (keyData == (Keys.Control | Keys.N))
            {
                Edit_NewFile(null, null);
                return true;
            }
             else if (keyData == (Keys.Control | Keys.R))
             {
                 RefreshActiveBrowser();
                 return true;
             }
             else if (keyData == (Keys.Alt | Keys.F5))
             {
                 Button_CreateArchive(null, null);
                 return true;
             }
             else if (keyData == (Keys.Shift | Keys.F5))
            {
                Button_Copy(null, null);
                return true;
            }
            else if (keyData == Keys.F5)
            {
                Button_Copy(null, null);
                return true;
            }
            else if (keyData == (Keys.Shift | Keys.F6))
            {
                Button_Move(null, null);
                return true;
            }
            else if (keyData == Keys.F6)
            {
                Button_Move(null, null);
                return true;
            }
            else if (keyData == Keys.F7)
            {
                Button_NewFolder(null, null);
                return true;
            }
             else if (keyData == Keys.F8)
             {
                 Button_Delete(null, null);
                 return true;
             }
             else if (keyData == (Keys.Alt | Keys.F9))
             {
                 Button_ExtractArchive(null, null);
                 return true;
             }
            
            return base.ProcessCmdKey(ref msg, keyData);
        }

    /*
     * Comand Buttons (e.g.: Copy, Past)
     */
    private void Button_View(object sender, EventArgs e)
    {
        commandService.ViewSelection(lastFileBrowser, this);
    }

    private void Button_Compare(object sender, EventArgs e)
    {
        FileBrowser source = lastFileBrowser ?? fileBrowserLeft;
        commandService.CompareSelections(source, GetPassiveBrowser(source), this);
    }

    private void Button_Edit(object sender, EventArgs e)
    {
        commandService.EditSelection(lastFileBrowser, this);
    }
    private void Button_Copy(object sender, EventArgs e)
        {
            FileBrowser source = lastFileBrowser ?? fileBrowserLeft;
            if ((Control.ModifierKeys & Keys.Alt) == Keys.Alt)
            {
                commandService.CreateArchive(source, GetPassiveBrowser(source), this);
                return;
            }

            if ((Control.ModifierKeys & Keys.Shift) == Keys.Shift)
            {
                commandService.CopySelectionInPlace(source, CopyComplete, this);
                return;
            }

            commandService.CopySelection(source, GetPassiveBrowser(source), CopyComplete, this);
        }

        private void Button_CreateArchive(object sender, EventArgs e)
        {
            FileBrowser source = lastFileBrowser ?? fileBrowserLeft;
            commandService.CreateArchive(source, GetPassiveBrowser(source), this);
        }

        private void Button_ExtractArchive(object sender, EventArgs e)
        {
            FileBrowser source = lastFileBrowser ?? fileBrowserLeft;
            commandService.ExtractArchive(source, GetPassiveBrowser(source), this);
        }

        private void Button_Move(object sender, EventArgs e)
        {
            FileBrowser source = lastFileBrowser ?? fileBrowserLeft;
            if ((Control.ModifierKeys & Keys.Shift) == Keys.Shift)
            {
                commandService.RenameSelection(source);
                return;
            }

            commandService.MoveSelection(source, GetPassiveBrowser(source), CopyComplete, this);
        }

        private void Button_NewFolder(object sender, EventArgs e)
        {
            commandService.CreateFolder(lastFileBrowser, this);
        }

        private void Button_Delete(object sender, EventArgs e)
        {
            commandService.DeleteSelection(lastFileBrowser, () => lastFileBrowser?.Refrech(), this);
        }

        private void RefreshActiveBrowser()
        {
            FileBrowser source = lastFileBrowser ?? fileBrowserLeft;
            source?.RefreshCurrentDirectory();
        }

        private void UpdateCommandButtonLabels(Keys modifiers)
        {
            Keys relevantModifiers = GetRelevantModifiers(modifiers);
            if (relevantModifiers == lastDisplayedModifiers && editButton != null && !string.IsNullOrWhiteSpace(editButton.Text))
            {
                return;
            }

            lastDisplayedModifiers = relevantModifiers;

            if (quickViewButton != null)
                quickViewButton.Text = GetQuickViewButtonText();
            if (viewButton != null)
                viewButton.Text = GetViewButtonText(relevantModifiers);
            if (editButton != null)
                editButton.Text = GetEditButtonText(relevantModifiers);
            if (copyButton != null)
                copyButton.Text = GetCopyButtonText(relevantModifiers);
            if (moveButton != null)
                moveButton.Text = GetMoveButtonText(relevantModifiers);
            if (newFolderButton != null)
                newFolderButton.Text = "F7 " + Language.getString("newFolder");
            if (deleteButton != null)
                deleteButton.Text = "F8 " + Language.getString("delete");

            UpdateStatusHint(relevantModifiers);
        }

        private static Keys GetRelevantModifiers(Keys modifiers)
        {
            return modifiers & (Keys.Shift | Keys.Alt | Keys.Control);
        }

        private static string GetViewButtonText(Keys modifiers)
        {
            if ((modifiers & Keys.Shift) == Keys.Shift)
            {
                return "Shift+F3 " + Language.getString("compare");
            }

            return "F3 " + Language.getString("view");
        }

        private static string GetEditButtonText(Keys modifiers)
        {
            if ((modifiers & Keys.Alt) == Keys.Alt)
            {
                return "Alt+F4 " + Language.getString("close");
            }

            if ((modifiers & Keys.Shift) == Keys.Shift)
            {
                return "Shift+F4 " + Language.getString("newFile");
            }

            return "F4 " + Language.getString("edit");
        }

        private static string GetCopyButtonText(Keys modifiers)
        {
            if ((modifiers & Keys.Alt) == Keys.Alt)
            {
                return "Alt+F5 " + Language.getString("archiveCreate").TrimEnd('.');
            }

            if ((modifiers & Keys.Shift) == Keys.Shift)
            {
                return "Shift+F5 " + Language.getString("copy");
            }

            return "F5 " + Language.getString("copy");
        }

        private static string GetMoveButtonText(Keys modifiers)
        {
            if ((modifiers & Keys.Shift) == Keys.Shift)
            {
                return "Shift+F6 " + Language.getString("rename");
            }

            return "F6 " + Language.getString("move");
        }

        private void Button_QuickView(object sender, EventArgs e)
        {
            quickViewEnabled = quickViewButton.Checked;
            ApplyQuickViewState();
            ApplyLocalization();
            UpdateStatusHint();
        }

        private void Button_QuickViewMenu(object sender, EventArgs e)
        {
            quickViewButton.Checked = !quickViewButton.Checked;
            quickViewEnabled = quickViewButton.Checked;
            ApplyQuickViewState();
            ApplyLocalization();
            UpdateStatusHint();
        }

        private void ToggleQuickView()
        {
            quickViewButton.Checked = !quickViewButton.Checked;
            quickViewEnabled = quickViewButton.Checked;
            ApplyQuickViewState();
            UpdateStatusHint();
        }

        private void ApplyQuickViewState()
        {
            if (quickViewEnabled)
            {
                EnableQuickView();
                UpdateQuickView();
            }
            else
            {
                DisableQuickView();
            }
        }

        private void EnableQuickView()
        {
            if (quickViewControl == null)
            {
                quickViewControl = new QuickViewControl();
                quickViewControl.GedcomPersonActivated += QuickViewControl_GedcomPersonActivated;
            }

            ConfigureQuickViewHost();
        }

        private void QuickViewControl_GedcomPersonActivated(object sender, GedcomPersonActivatedEventArgs e)
        {
            FileBrowser sourceBrowser = lastFileBrowser ?? fileBrowserLeft;
            if (sourceBrowser?.IsGedcomMode == true)
            {
                sourceBrowser.SelectGedcomPerson(e.PersonId);
            }
        }

        private void DisableQuickView()
        {
            RemoveQuickViewControl();
            EnsureBrowserInPanel(fileBrowserLeft, splitContainer1.Panel1);
            EnsureBrowserInPanel(fileBrowserRight, splitContainer1.Panel2);
        }

        private void ConfigureQuickViewHost()
        {
            if (quickViewControl == null)
                return;

            bool sourceIsLeft = lastFileBrowser == null || lastFileBrowser == fileBrowserLeft;

            RemoveQuickViewControl();
            EnsureBrowserInPanel(fileBrowserLeft, splitContainer1.Panel1);
            EnsureBrowserInPanel(fileBrowserRight, splitContainer1.Panel2);

            if (sourceIsLeft)
            {
                splitContainer1.Panel2.Controls.Remove(fileBrowserRight);
                quickViewControl.Dock = DockStyle.Fill;
                splitContainer1.Panel2.Controls.Add(quickViewControl);
            }
            else
            {
                splitContainer1.Panel1.Controls.Remove(fileBrowserLeft);
                quickViewControl.Dock = DockStyle.Fill;
                splitContainer1.Panel1.Controls.Add(quickViewControl);
            }
        }

        private void EnsureBrowserInPanel(Control browser, SplitterPanel panel)
        {
            if (!panel.Controls.Contains(browser))
            {
                panel.Controls.Add(browser);
            }
            browser.Dock = DockStyle.Fill;
        }

        private void RemoveQuickViewControl()
        {
            if (quickViewControl == null)
                return;

            if (splitContainer1.Panel1.Controls.Contains(quickViewControl))
            {
                splitContainer1.Panel1.Controls.Remove(quickViewControl);
            }

            if (splitContainer1.Panel2.Controls.Contains(quickViewControl))
            {
                splitContainer1.Panel2.Controls.Remove(quickViewControl);
            }
        }

        private async void UpdateQuickView()
        {
            if (!quickViewEnabled || quickViewControl == null)
                return;

            int generation = ++quickViewGeneration;
            FileBrowser sourceBrowser = lastFileBrowser ?? fileBrowserLeft;
            if (sourceBrowser?.IsGedcomMode == true)
            {
                quickViewControl.DisplayGedcomPerson(sourceBrowser.SelectedGedcomPerson);
                return;
            }

            string fileToPreview;
            try
            {
                fileToPreview = sourceBrowser?.IsArchiveMode == true
                    ? await sourceBrowser.MaterializeSelectedArchiveFileAsync()
                    : sourceBrowser?.selectedFiles != null && sourceBrowser.selectedFiles.Length > 0
                        ? sourceBrowser.selectedFiles[0]
                        : null;
            }
            catch (Exception ex)
            {
                LogService.LogException("AppForm.UpdateQuickView", ex);
                fileToPreview = null;
            }

            if (generation != quickViewGeneration || !quickViewEnabled)
            {
                return;
            }

            quickViewControl.DisplayFile(fileToPreview);
        }

        private void FileBrowser_SelectionChanged(object sender, EventArgs e)
        {
            if (sender is FileBrowser browser && browser == lastFileBrowser && quickViewEnabled)
            {
                UpdateQuickView();
            }

            UpdateStatusHint();
        }

        private void fileBrowser_Enter(object sender, EventArgs e)
        {
            lastFileBrowser = sender as FileBrowser;
            fileBrowser_PathChange(lastFileBrowser, lastFileBrowser.DisplayLocation);
            if (quickViewEnabled)
            {
                ConfigureQuickViewHost();
                UpdateQuickView();
            }

            UpdateStatusHint();
        }

        private void fileBrowser_PathChange(Object sender, String newPath)
        {
            FileBrowser browser = sender as FileBrowser;
            if (browser == null)
            {
                return;
            }

            if (!browser.IsVirtualMode)
            {
                RememberDrivePath(browser, newPath);
            }

            int found = 0;
            foreach (ToolStripButton item in toolStripDrivers.Items)
            {
                if (item.Tag is ArchiveDeviceTag archiveDevice)
                {
                    item.Checked = browser.IsArchiveMode && archiveDevice.Browser == browser;
                    continue;
                }

                if (Environment.OSVersion.Platform != PlatformID.Unix)
                {
                    // Windows
                    if (!browser.IsVirtualMode && item.Tag as String == Path.GetPathRoot(newPath))
                    {
                        item.Checked = true;
                    }
                    else
                    {
                        item.Checked = false;
                    }
                }
                else
                {
                    // Linux
                    if (newPath.IndexOf(item.Tag as String) != -1 && item.Tag as String != "/")
                    {
                        item.Checked = true;
                        found++;
                    }
                    else
                    {
                        item.Checked = false;
                    }
                }
            }

            if (Environment.OSVersion.Platform == PlatformID.Unix && found == 0)
            {
                (toolStripDrivers.Items[0] as ToolStripButton).Checked = true;
            }

            if (ShowMemoryUsageInTitle)
            {
                Text = ".NetCommander " + GC.GetTotalMemory(false).ToString();
            }
            else
            {
                Text = ".NetCommander";
            }

            if (quickViewEnabled && sender == lastFileBrowser)
            {
                ConfigureQuickViewHost();
                UpdateQuickView();
            }

            UpdateStatusHint();
            UpdateCommandLinePrompt();
        }

        private void FileBrowser_ArchiveDeviceChanged(object sender, ArchiveDeviceChangedEventArgs e)
        {
            if (!(sender is FileBrowser browser))
            {
                return;
            }

            if (!e.IsMounted)
            {
                if (archiveDeviceButtons.TryGetValue(browser, out ToolStripButton existing))
                {
                    toolStripDrivers.Items.Remove(existing);
                    existing.Dispose();
                    archiveDeviceButtons.Remove(browser);
                }

                return;
            }

            if (!archiveDeviceButtons.TryGetValue(browser, out ToolStripButton button))
            {
                button = new ToolStripButton
                {
                    DisplayStyle = ToolStripItemDisplayStyle.ImageAndText,
                    ImageAlign = ContentAlignment.MiddleCenter
                };
                button.Click += toolStripButton_Click;
                archiveDeviceButtons.Add(browser, button);
                toolStripDrivers.Items.Add(button);
            }

            button.Name = "archiveDevice_" + browser.Name;
            button.Text = Path.GetFileName(e.ArchivePath);
            button.ToolTipText = e.ArchivePath;
            button.Tag = new ArchiveDeviceTag(browser, e.ArchivePath);
            ApplyArchiveDeviceIcon(button, e.ArchivePath);
            fileBrowser_PathChange(browser, browser.DisplayLocation);
        }

        private void ApplyArchiveDeviceIcon(ToolStripButton button, string archivePath)
        {
            if (toolStripDrivers.ImageList == null)
            {
                return;
            }

            string key = "archive-device:" + FileIconCache.GetTypeKey(archivePath, false);
            int imageIndex = toolStripDrivers.ImageList.Images.IndexOfKey(key);
            if (imageIndex < 0)
            {
                using FileIconData icon = FileIconCache.GetIconData(archivePath, false, false);
                if (icon?.Icon == null)
                {
                    return;
                }

                toolStripDrivers.ImageList.Images.Add(key, icon.Icon);
                imageIndex = toolStripDrivers.ImageList.Images.Count - 1;
            }

            button.ImageIndex = imageIndex;
        }

        private void AppForm_FormClosing(object sender, FormClosingEventArgs e)
        {
      Application.RemoveMessageFilter(modifierKeyMessageFilter);
      CloseChildWindows();
      SaveCurrentColumnWidths();
      SaveWindowBounds();
      Properties.Settings.Default.LeftBrowserLastPath = fileBrowserLeft.CurrentPath;
      Properties.Settings.Default.RightBrowserLastPath = fileBrowserRight.CurrentPath;
      Properties.Settings.Default.LeftBrowserDrivePaths = SerializeDrivePaths(browserDrivePaths[fileBrowserLeft]);
      Properties.Settings.Default.RightBrowserDrivePaths = SerializeDrivePaths(browserDrivePaths[fileBrowserRight]);
      Properties.Settings.Default.Save();
    }

        private void CopyComplete(int result)
        {
            fileBrowserRight.Refrech();
            fileBrowserLeft.Refrech();
            UpdateStatusHint();
            UpdateCommandLinePrompt();
        }

        private void UpdateCommandLinePrompt()
        {
            if (toolStripLabelCommandPath == null)
            {
                return;
            }

            string workingDirectory = GetCommandWorkingDirectory();
            toolStripLabelCommandPath.Text = workingDirectory.TrimEnd(Path.DirectorySeparatorChar) + ">";
        }

        private string GetCommandWorkingDirectory()
        {
            FileBrowser activeBrowser = lastFileBrowser ?? fileBrowserLeft;
            string currentPath = activeBrowser?.CurrentPath;
            return !string.IsNullOrWhiteSpace(currentPath) && Directory.Exists(currentPath)
                ? currentPath
                : Application.StartupPath;
        }

        private void toolStripTextBoxCommand_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                ExecuteCommandLine(e.Shift);
                e.Handled = true;
                e.SuppressKeyPress = true;
                return;
            }

            if (e.KeyCode == Keys.Up)
            {
                NavigateCommandHistory(-1);
                e.Handled = true;
                e.SuppressKeyPress = true;
                return;
            }

            if (e.KeyCode == Keys.Down)
            {
                NavigateCommandHistory(1);
                e.Handled = true;
                e.SuppressKeyPress = true;
                return;
            }

            if (e.KeyCode == Keys.Escape)
            {
                toolStripTextBoxCommand.Clear();
                (lastFileBrowser ?? fileBrowserLeft)?.Select();
                e.Handled = true;
                e.SuppressKeyPress = true;
            }
        }

        private void ExecuteCommandLine(bool keepConsoleOpen)
        {
            string command = toolStripTextBoxCommand.Text.Trim();
            if (command.Length == 0)
            {
                return;
            }

            try
            {
                WinCommandLine.Execute(command, GetCommandWorkingDirectory(), keepConsoleOpen);
                if (commandHistory.Count == 0 || !string.Equals(commandHistory[commandHistory.Count - 1], command, StringComparison.Ordinal))
                {
                    commandHistory.Add(command);
                }
                commandHistoryIndex = commandHistory.Count;
                commandHistoryDraft = string.Empty;
                toolStripTextBoxCommand.Clear();
            }
            catch (Exception ex)
            {
                LogService.LogException("AppForm.ExecuteCommandLine", ex);
                MessageBox.Show(
                    this,
                    string.Format(Language.getString("commandLineExecutionFailedFormat"), ex.Message),
                    Language.getString("error"),
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }

        private void InsertCurrentItemNameIntoCommandLine()
        {
            string itemName = (lastFileBrowser ?? fileBrowserLeft)?.GetCurrentItemName();
            if (string.IsNullOrWhiteSpace(itemName))
            {
                return;
            }

            string argument = QuoteCommandLineItemName(itemName);
            int selectionStart = toolStripTextBoxCommand.SelectionStart;
            int selectionLength = toolStripTextBoxCommand.SelectionLength;
            string currentText = toolStripTextBoxCommand.Text;
            bool needsLeadingSpace = selectionStart > 0 && !char.IsWhiteSpace(currentText[selectionStart - 1]);
            int followingIndex = selectionStart + selectionLength;
            bool needsTrailingSpace = followingIndex < currentText.Length && !char.IsWhiteSpace(currentText[followingIndex]);
            string insertion = (needsLeadingSpace ? " " : string.Empty)
                + argument
                + (needsTrailingSpace ? " " : string.Empty);

            toolStripTextBoxCommand.Text = currentText.Remove(selectionStart, selectionLength).Insert(selectionStart, insertion);
            toolStripTextBoxCommand.SelectionStart = selectionStart + insertion.Length;
            toolStripTextBoxCommand.SelectionLength = 0;
            toolStripTextBoxCommand.Focus();
        }

        private static string QuoteCommandLineItemName(string itemName)
        {
            return itemName.Any(character => char.IsWhiteSpace(character) || "&()[]{}^=;!'+,`~".IndexOf(character) >= 0)
                ? "\"" + itemName + "\""
                : itemName;
        }

        private void NavigateCommandHistory(int direction)
        {
            if (commandHistory.Count == 0)
            {
                return;
            }

            if (commandHistoryIndex == commandHistory.Count && direction < 0)
            {
                commandHistoryDraft = toolStripTextBoxCommand.Text;
            }

            commandHistoryIndex = Math.Max(0, Math.Min(commandHistory.Count, commandHistoryIndex + direction));
            toolStripTextBoxCommand.Text = commandHistoryIndex == commandHistory.Count
                ? commandHistoryDraft
                : commandHistory[commandHistoryIndex];
            toolStripTextBoxCommand.SelectionStart = toolStripTextBoxCommand.TextLength;
        }

        private void OpenSettings(object sender, EventArgs e)
        {
            using (FormSettings settingsForm = new FormSettings(GetPreferredColumnWidths()))
            {
                settingsForm.ShowDialog(this);
            }

            Language.ApplyConfiguredCulture(Properties.Settings.Default.UiLanguage);
            ApplyLocalization();
            ApplyUiSettings();
        }

        private FileBrowser.BrowserColumnWidths GetPreferredColumnWidths()
        {
            FileBrowser sourceBrowser = lastFileBrowser ?? fileBrowserLeft;
            return sourceBrowser?.GetColumnWidths() ?? new FileBrowser.BrowserColumnWidths(
                Properties.Settings.Default.FileBrowserNameColumnWidth,
                Properties.Settings.Default.FileBrowserTypeColumnWidth,
                Properties.Settings.Default.FileBrowserSizeColumnWidth,
                Properties.Settings.Default.FileBrowserDateColumnWidth);
        }

        private void SaveCurrentColumnWidths()
        {
            FileBrowser.BrowserColumnWidths widths = GetPreferredColumnWidths();
            Properties.Settings.Default.FileBrowserNameColumnWidth = widths.NameWidth;
            Properties.Settings.Default.FileBrowserTypeColumnWidth = widths.TypeWidth;
            Properties.Settings.Default.FileBrowserSizeColumnWidth = widths.SizeWidth;
            Properties.Settings.Default.FileBrowserDateColumnWidth = widths.DateWidth;
        }

        private void SaveWindowBounds()
        {
            Rectangle boundsToSave = WindowState == FormWindowState.Normal ? Bounds : RestoreBounds;
            if (boundsToSave.Width > 0 && boundsToSave.Height > 0)
            {
                Properties.Settings.Default.WindowStartPos = boundsToSave.Location;
                Properties.Settings.Default.WindowStartSize = boundsToSave.Size;
            }

            Properties.Settings.Default.WindowStartMaximized = WindowState == FormWindowState.Maximized;
        }

        private void RestoreWindowBounds()
        {
            Size savedSize = Properties.Settings.Default.WindowStartSize;
            Point savedLocation = Properties.Settings.Default.WindowStartPos;

            Rectangle restoredBounds = BuildRestoredBounds(savedLocation, savedSize);
            Bounds = restoredBounds;
            StartPosition = FormStartPosition.Manual;

            if (Properties.Settings.Default.WindowStartMaximized)
            {
                WindowState = FormWindowState.Maximized;
            }
        }

        private string GetDriveNavigationPath(FileBrowser browser, string driveRoot)
        {
            if (string.IsNullOrWhiteSpace(driveRoot))
            {
                return driveRoot;
            }

            if (browser != null &&
                browserDrivePaths.TryGetValue(browser, out Dictionary<string, string> knownPaths) &&
                knownPaths.TryGetValue(driveRoot, out string rememberedPath) &&
                !string.IsNullOrWhiteSpace(rememberedPath) &&
                FileSystemService.DirectoryExists(rememberedPath))
            {
                return rememberedPath;
            }

            return driveRoot;
        }

        private void RememberDrivePath(FileBrowser browser, string path)
        {
            if (browser == null || string.IsNullOrWhiteSpace(path))
            {
                return;
            }

            string driveRoot = Path.GetPathRoot(path);
            if (string.IsNullOrWhiteSpace(driveRoot))
            {
                return;
            }

            if (!browserDrivePaths.TryGetValue(browser, out Dictionary<string, string> knownPaths))
            {
                knownPaths = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                browserDrivePaths[browser] = knownPaths;
            }

            knownPaths[driveRoot] = path;
        }

        private void RestoreRememberedDrivePaths()
        {
            browserDrivePaths[fileBrowserLeft] = DeserializeDrivePaths(Properties.Settings.Default.LeftBrowserDrivePaths);
            browserDrivePaths[fileBrowserRight] = DeserializeDrivePaths(Properties.Settings.Default.RightBrowserDrivePaths);
        }

        private static string SerializeDrivePaths(Dictionary<string, string> drivePaths)
        {
            if (drivePaths == null || drivePaths.Count == 0)
            {
                return string.Empty;
            }

            var builder = new StringBuilder();
            foreach (KeyValuePair<string, string> pair in drivePaths.OrderBy(item => item.Key, StringComparer.OrdinalIgnoreCase))
            {
                if (string.IsNullOrWhiteSpace(pair.Key) || string.IsNullOrWhiteSpace(pair.Value))
                {
                    continue;
                }

                builder.Append(pair.Key.Replace("|", string.Empty));
                builder.Append('|');
                builder.AppendLine(pair.Value.Replace("\r", string.Empty).Replace("\n", string.Empty));
            }

            return builder.ToString();
        }

        private static Dictionary<string, string> DeserializeDrivePaths(string serialized)
        {
            var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (string.IsNullOrWhiteSpace(serialized))
            {
                return result;
            }

            string[] lines = serialized.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (string line in lines)
            {
                int separatorIndex = line.IndexOf('|');
                if (separatorIndex <= 0 || separatorIndex >= line.Length - 1)
                {
                    continue;
                }

                string driveRoot = line.Substring(0, separatorIndex).Trim();
                string path = line.Substring(separatorIndex + 1).Trim();
                if (string.IsNullOrWhiteSpace(driveRoot) || string.IsNullOrWhiteSpace(path))
                {
                    continue;
                }

                result[driveRoot] = path;
            }

            return result;
        }

        private Rectangle BuildRestoredBounds(Point savedLocation, Size savedSize)
        {
            Rectangle primaryWorkingArea = Screen.PrimaryScreen?.WorkingArea ?? new Rectangle(0, 0, 1024, 768);
            Size desiredSize = NormalizeSavedSize(savedSize, primaryWorkingArea.Size);
            Rectangle desiredBounds = new Rectangle(savedLocation, desiredSize);

            if (IsWindowRectVisible(desiredBounds))
            {
                return desiredBounds;
            }

            Size fallbackSize = NormalizeSavedSize(savedSize, primaryWorkingArea.Size);
            Point centeredLocation = new Point(
                primaryWorkingArea.Left + Math.Max(0, (primaryWorkingArea.Width - fallbackSize.Width) / 2),
                primaryWorkingArea.Top + Math.Max(0, (primaryWorkingArea.Height - fallbackSize.Height) / 2));
            return new Rectangle(centeredLocation, fallbackSize);
        }

        private static Size NormalizeSavedSize(Size savedSize, Size maxSize)
        {
            int width = savedSize.Width > 0 ? savedSize.Width : 800;
            int height = savedSize.Height > 0 ? savedSize.Height : 600;

            width = Math.Max(640, Math.Min(width, maxSize.Width));
            height = Math.Max(480, Math.Min(height, maxSize.Height));
            return new Size(width, height);
        }

        private static bool IsWindowRectVisible(Rectangle bounds)
        {
            if (bounds.Width <= 0 || bounds.Height <= 0)
            {
                return false;
            }

            foreach (Screen screen in Screen.AllScreens)
            {
                Rectangle visibleArea = Rectangle.Intersect(bounds, screen.WorkingArea);
                if (visibleArea.Width >= 120 && visibleArea.Height >= 120)
                {
                    return true;
                }
            }

            return false;
        }

        private void OpenAbout(object sender, EventArgs e)
        {
            using (FormAbout aboutForm = new FormAbout())
            {
                aboutForm.ShowDialog(this);
            }
        }

        private void OpenChangeLog(object sender, EventArgs e)
        {
            OpenMarkdownDocument("CHANGE.md");
        }

        private void OpenReadme(object sender, EventArgs e)
        {
            OpenMarkdownDocument("README.md");
        }

        private void OpenRoadmap(object sender, EventArgs e)
        {
            OpenMarkdownDocument("ROADMAP.md");
        }

        private void OpenMarkdownDocument(string fileName)
        {
            string sourcePath = Path.Combine(Application.StartupPath, fileName);
            if (!FileSystemService.FileExists(sourcePath))
            {
                MessageBox.Show(string.Format(Language.getString("documentNotFoundFormat"), fileName), Language.getString("error"), MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            View.RtfEdit preview = new View.RtfEdit();
            preview.LoadFile(sourcePath, true);
            preview.Show(this);
        }

        private void ApplyUiSettings()
        {
            fileBrowserLeft.ApplyUserSettings();
            fileBrowserRight.ApplyUserSettings();
            statusStripMain.Visible = Properties.Settings.Default.ShowStatusHints;
            UpdateStatusHint();

            if (quickViewEnabled)
            {
                UpdateQuickView();
            }
        }

        private void UpdateStatusHint()
        {
            UpdateStatusHint(GetRelevantModifiers(Control.ModifierKeys));
        }

        private void UpdateStatusHint(Keys modifiers)
        {
            if (toolStripStatusLabelInfo == null)
            {
                return;
            }

            if (!Properties.Settings.Default.ShowStatusHints)
            {
                toolStripStatusLabelInfo.Text = string.Empty;
                toolStripStatusLabelInfo.ToolTipText = string.Empty;
                return;
            }

            string infoText = BuildStatusHintText(modifiers);
            toolStripStatusLabelInfo.Text = infoText;
            toolStripStatusLabelInfo.ToolTipText = infoText;
        }

        private string BuildStatusHintText(Keys modifiers)
        {
            FileBrowser activeBrowser = lastFileBrowser ?? fileBrowserLeft;
            string activeSide = activeBrowser == fileBrowserRight
                ? Language.getString("statusSideRight")
                : Language.getString("statusSideLeft");
            FileBrowser.BrowserStatusInfo info = activeBrowser?.GetStatusInfo();
            if (info == null)
            {
                return string.Format(Language.getString("statusHintNoSelectionFormat"), activeSide);
            }

            List<string> parts = new List<string>
            {
                string.Format(Language.getString("statusHintActiveFormat"), activeSide),
                string.Format(Language.getString("statusHintFoldersFormat"), info.DirectoryCount),
                string.Format(Language.getString("statusHintFilesFormat"), info.FileCount),
                string.Format(Language.getString("statusHintSelectedFormat"), info.SelectedCount, info.SelectedDirectoryCount, info.SelectedFileCount)
            };

            string currentItemInfo = BuildCurrentItemStatusText(info);
            if (!string.IsNullOrWhiteSpace(currentItemInfo))
            {
                parts.Add(currentItemInfo);
            }

            return string.Join("   |   ", parts);
        }

        private static string BuildCurrentItemStatusText(FileBrowser.BrowserStatusInfo info)
        {
            if (info == null || string.IsNullOrWhiteSpace(info.CurrentItemName) || info.CurrentItemName == "..")
            {
                return null;
            }

            List<string> parts = new List<string> { info.CurrentItemName };
            if (info.CurrentItemIsDirectory)
            {
                parts.Add(Language.getString("statusHintDirectoryMarker"));
            }
            else if (info.CurrentItemSizeBytes.HasValue)
            {
                parts.Add(string.Format(Language.getString("statusHintBytesFormat"), info.CurrentItemSizeBytes.Value));
            }

            if (!string.IsNullOrWhiteSpace(info.CurrentItemModifiedText))
            {
                parts.Add(info.CurrentItemModifiedText);
            }

            return string.Join(", ", parts);
        }

        private void ShowKeyboardHelp()
        {
            StringBuilder builder = new StringBuilder();
            builder.AppendLine("DotNetCommander");
            builder.AppendLine();
            builder.AppendLine("F1 - " + Language.getString("helpShowThis"));
            builder.AppendLine("F2 / Ctrl+Q - " + Language.getString("helpToggleQuickView"));
            builder.AppendLine("F3 - " + Language.getString("view"));
            builder.AppendLine("Shift+F3 - " + Language.getString("compare"));
            builder.AppendLine("F4 - " + Language.getString("edit"));
            builder.AppendLine("Shift+F4 - " + Language.getString("newFile"));
            builder.AppendLine("Ctrl+N - " + Language.getString("helpNewFileDialog"));
            builder.AppendLine("F5 - " + Language.getString("copy"));
            builder.AppendLine("Shift+F5 - " + Language.getString("helpCopyInPlace"));
            builder.AppendLine("Alt+F5 - " + Language.getString("archiveCreate"));
            builder.AppendLine("F6 - " + Language.getString("move"));
            builder.AppendLine("Shift+F6 - " + Language.getString("rename"));
            builder.AppendLine("F7 - " + Language.getString("newFolder"));
            builder.AppendLine("F8 - " + Language.getString("delete"));
            builder.AppendLine("Alt+F9 - " + Language.getString("archiveExtract"));
            builder.AppendLine("Ctrl+PgDn - " + Language.getString("helpOpenArchiveBySignature"));
            builder.AppendLine("Backspace - " + Language.getString("helpHistoryBack"));
            builder.AppendLine("Alt+Up - " + Language.getString("helpNavigateParent"));
            builder.AppendLine("Ctrl+R - " + Language.getString("helpRefreshActivePanel"));
            builder.AppendLine("Ctrl+L - " + Language.getString("helpFocusCommandLine"));
            builder.AppendLine("Ctrl+Enter - " + Language.getString("helpCopyNameToCommandLine"));
            builder.AppendLine("Shift+Enter - " + Language.getString("helpPersistentConsole"));
            builder.AppendLine("Alt+F4 - " + Language.getString("helpCloseApplication"));

            MessageBox.Show(this, builder.ToString(), Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private FileBrowser GetPassiveBrowser(FileBrowser activeBrowser)
        {
            return activeBrowser == fileBrowserRight ? fileBrowserLeft : fileBrowserRight;
        }

        private void CloseChildWindows()
        {
            Form[] openForms = Application.OpenForms.Cast<Form>().Where(form => form != this).ToArray();
            foreach (Form form in openForms)
            {
                try
                {
                    form.Close();
                }
                catch (Exception ex)
                {
                    LogService.LogException("AppForm.CloseChildWindows", ex);
                }
            }
        }

        private sealed class ArchiveDeviceTag
        {
            public ArchiveDeviceTag(FileBrowser browser, string archivePath)
            {
                Browser = browser;
                ArchivePath = archivePath;
            }

            public FileBrowser Browser { get; }
            public string ArchivePath { get; }
        }

        private sealed class ModifierKeyMessageFilter : IMessageFilter
        {
            private readonly Action<Keys> updateLabels;

            public ModifierKeyMessageFilter(Action<Keys> updateLabels)
            {
                this.updateLabels = updateLabels;
            }

            public bool PreFilterMessage(ref Message m)
            {
                switch (m.Msg)
                {
                    case 0x0100:
                    case 0x0101:
                    case 0x0104:
                    case 0x0105:
                        updateLabels?.Invoke(GetRelevantModifiers(Control.ModifierKeys));
                        break;
                }

                return false;
            }
        }
    }
}
