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
        private ToolStripLabel functionModifierLabel;
        private ToolStripButton quickViewButton;
        private ToolStripButton viewButton;
        private ToolStripButton editButton;
        private ToolStripButton copyButton;
        private ToolStripButton moveButton;
        private ToolStripButton newFolderButton;
        private ToolStripButton deleteButton;
        private readonly ModifierKeyMessageFilter modifierKeyMessageFilter;
        private readonly List<string> commandHistory = new List<string>();
        private int commandHistoryIndex;
        private string commandHistoryDraft = string.Empty;
        private static readonly bool ShowMemoryUsageInTitle = false;

        public AppForm()
        {
            InitializeComponent();
            splitContainer1.BringToFront();
            modifierKeyMessageFilter = new ModifierKeyMessageFilter(UpdateCommandButtonLabels, TryHandlePanelSwitchMessage);
            browserDrivePaths[fileBrowserLeft] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            browserDrivePaths[fileBrowserRight] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            RestoreRememberedDrivePaths();

            fileBrowserLeft.SelectionChanged += FileBrowser_SelectionChanged;
            fileBrowserRight.SelectionChanged += FileBrowser_SelectionChanged;
            fileBrowserLeft.AdjacentPanelRequested += FileBrowser_AdjacentPanelRequested;
            fileBrowserRight.AdjacentPanelRequested += FileBrowser_AdjacentPanelRequested;
            fileBrowserLeft.ArchiveDeviceChanged += FileBrowser_ArchiveDeviceChanged;
            fileBrowserRight.ArchiveDeviceChanged += FileBrowser_ArchiveDeviceChanged;
            fileBrowserLeft.FeedToPanelRequested += FileBrowser_FeedToPanelRequested;
            fileBrowserRight.FeedToPanelRequested += FileBrowser_FeedToPanelRequested;

            functionModifierLabel = new ToolStripLabel
            {
                AutoSize = false,
                Width = 64,
                Font = new Font("Segoe UI", 10.0f, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleCenter
            };
            toolStripButtons.Items.Add(functionModifierLabel);
            toolStripButtons.Items.Add(new ToolStripSeparator());

            quickViewButton = AddCommandButton(GetQuickViewButtonText(Keys.None), Button_QuickView);
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
            parent.DropDownItems.Add(new ToolStripSeparator());
            ToolStripMenuItem copyPathsItem = CreateMenuItem("copyPathsAsText", Edit_CopyPathsAsText);
            ToolStripMenuItem copyNamesItem = CreateMenuItem("copyNamesAsText", Edit_CopyNamesAsText);
            ToolStripMenuItem copyRelativePathsItem = CreateMenuItem("copyRelativePathsAsText", Edit_CopyRelativePathsAsText);
            copyPathsItem.Enabled = false;
            copyNamesItem.Enabled = false;
            copyRelativePathsItem.Enabled = false;
            parent.DropDownOpening += (_, __) =>
            {
                FileBrowser active = lastFileBrowser ?? fileBrowserLeft;
                bool canCopySelection = active != null && !active.IsVirtualMode && active.SelectedItems.Count > 0;
                copyPathsItem.Enabled = canCopySelection;
                copyNamesItem.Enabled = canCopySelection;
                copyRelativePathsItem.Enabled = canCopySelection && !string.IsNullOrWhiteSpace(active.CurrentPath);
            };
            parent.DropDownItems.Add(copyPathsItem);
            parent.DropDownItems.Add(copyNamesItem);
            parent.DropDownItems.Add(copyRelativePathsItem);
            parent.DropDownItems.Add(CreateMenuItem("paste", Edit_Paste));
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
            ToolStripMenuItem rawViewMenuItem = CreateMenuItem("rawView", Button_RawView);
            rawViewMenuItem.ShortcutKeys = Keys.Control | Keys.F3;
            parent.DropDownItems.Add(rawViewMenuItem);
            parent.DropDownItems.Add(CreateStatusBarMenuItem());
        }

        private void CreateToolsMenu(ToolStripMenuItem parent)
        {
            parent.DropDownItems.Clear();
            ToolStripMenuItem searchItem = CreateMenuItem("search", OpenSearch);
            searchItem.ShortcutKeys = Keys.Alt | Keys.F7;
            parent.DropDownItems.Add(searchItem);
            parent.DropDownItems.Add(CreateMenuItem("aiOrganizerMenu", OpenAiOrganizer));
            parent.DropDownItems.Add(new ToolStripSeparator());
            parent.DropDownItems.Add(CreateMenuItem("vectorIndexFolderMenu", OpenVectorIndex));
            parent.DropDownItems.Add(CreateMenuItem("semanticSearchMenu", OpenSemanticSearch));
            parent.DropDownItems.Add(CreateMenuItem("vectorRagMenu", OpenVectorRag));
            parent.DropDownItems.Add(new ToolStripSeparator());
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

        private void Edit_CopyPathsAsText(object sender, EventArgs e)
        {
            commandService.CopySelectionPathsAsText(lastFileBrowser ?? fileBrowserLeft, this);
        }

        private void Edit_CopyNamesAsText(object sender, EventArgs e)
        {
            commandService.CopySelectionNamesAsText(lastFileBrowser ?? fileBrowserLeft, this);
        }

        private void Edit_CopyRelativePathsAsText(object sender, EventArgs e)
        {
            commandService.CopySelectionRelativePathsAsText(lastFileBrowser ?? fileBrowserLeft, this);
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

            int fixedWidth = toolStripButtons.Padding.Horizontal;
            foreach (ToolStripItem item in toolStripButtons.Items)
            {
                if (item is not ToolStripButton)
                {
                    fixedWidth += item.Width + item.Margin.Horizontal;
                }
            }

            int buttonMargins = toolStripButtons.Items
                .OfType<ToolStripButton>()
                .Sum(button => button.Margin.Horizontal);
            int availableWidth = toolStripButtons.ClientSize.Width - fixedWidth - buttonMargins;
            int width = Math.Max(48, availableWidth / buttonCount);

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
            if (keyData == (Keys.Control | Keys.PageUp))
            {
                (lastFileBrowser ?? fileBrowserLeft).NavigateParent();
                return true;
            }
            if (keyData == (Keys.Control | Keys.PageDown))
            {
                _ = (lastFileBrowser ?? fileBrowserLeft).OpenSelectedContainerAsync(includeCompound: true);
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
            if (keyData == Keys.Multiply)
            {
                FileBrowser focusedBrowser = fileBrowserLeft.IsPhysicalFileListFocused
                    ? fileBrowserLeft
                    : fileBrowserRight.IsPhysicalFileListFocused ? fileBrowserRight : null;
                if (focusedBrowser != null && focusedBrowser.InvertPhysicalSelection())
                    return true;
            }
            if (keyData == Keys.Add || keyData == Keys.Subtract)
            {
                FileBrowser focusedBrowser = fileBrowserLeft.IsPhysicalFileListFocused
                    ? fileBrowserLeft
                    : fileBrowserRight.IsPhysicalFileListFocused ? fileBrowserRight : null;
                if (focusedBrowser != null)
                {
                    bool selectMatches = keyData == Keys.Add;
                    using var dialog = new FormSelectionMask(selectMatches);
                    if (dialog.ShowDialog(this) == DialogResult.OK)
                        focusedBrowser.ApplyPhysicalSelectionMask(dialog.Mask, selectMatches);
                    return true;
                }
            }
            if (keyData == (Keys.Control | Keys.Shift | Keys.C))
            {
                FileBrowser focusedBrowser = fileBrowserLeft.IsPhysicalFileListFocused
                    ? fileBrowserLeft
                    : fileBrowserRight.IsPhysicalFileListFocused ? fileBrowserRight : null;
                if (focusedBrowser != null)
                {
                    commandService.CopySelectionPathsAsText(focusedBrowser, this);
                    return true;
                }
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
            else if (keyData == (Keys.Control | Keys.F3))
            {
                Button_RawView(null, null);
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
            else if (keyData == (Keys.Alt | Keys.F7))
            {
                OpenSearch(null, null);
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

        protected override bool ProcessDialogKey(Keys keyData)
        {
            if (keyData == Keys.Tab || keyData == (Keys.Shift | Keys.Tab))
            {
                ActivateAdjacentPanel();
                return true;
            }

            return base.ProcessDialogKey(keyData);
        }

        private bool TryHandlePanelSwitchMessage(Message message)
        {
            if (message.Msg != 0x0100 || Form.ActiveForm != this)
            {
                return false;
            }

            Keys keyCode = (Keys)message.WParam.ToInt32() & Keys.KeyCode;
            if (keyCode != Keys.Tab)
            {
                return false;
            }

            Keys modifiers = Control.ModifierKeys & (Keys.Control | Keys.Alt | Keys.Shift);
            if (modifiers != Keys.None && modifiers != Keys.Shift)
            {
                return false;
            }

            Control sourceControl = Control.FromHandle(message.HWnd);
            if (sourceControl == null || (sourceControl != this && sourceControl.FindForm() != this))
            {
                return false;
            }

            ActivateAdjacentPanel();
            return true;
        }

        private void ActivateAdjacentPanel()
        {
            FileBrowser currentBrowser = lastFileBrowser ?? fileBrowserLeft;
            FileBrowser targetBrowser = currentBrowser == fileBrowserRight
                ? fileBrowserLeft
                : fileBrowserRight;

            lastFileBrowser = targetBrowser;
            if (quickViewEnabled)
            {
                ConfigureQuickViewHost();
            }

            targetBrowser.ActivatePanel();
        }

        private void FileBrowser_AdjacentPanelRequested(object sender, EventArgs e)
        {
            if (sender is FileBrowser browser)
            {
                lastFileBrowser = browser;
            }

            ActivateAdjacentPanel();
        }

    /*
     * Comand Buttons (e.g.: Copy, Paste)
     */
    private void Button_View(object sender, EventArgs e)
    {
        Keys modifiers = GetRelevantModifiers(Control.ModifierKeys);
        if (modifiers == Keys.Control)
        {
            Button_RawView(sender, e);
        }
        else if (modifiers == Keys.Shift)
        {
            Button_Compare(sender, e);
        }
        else if (modifiers == Keys.None)
        {
            commandService.ViewSelection(lastFileBrowser, this);
        }
    }

    private void Button_RawView(object sender, EventArgs e)
    {
        commandService.ViewSelectionRaw(lastFileBrowser ?? fileBrowserLeft, this);
    }

    private void Button_Compare(object sender, EventArgs e)
    {
        FileBrowser source = lastFileBrowser ?? fileBrowserLeft;
        commandService.CompareSelections(source, GetPassiveBrowser(source), this);
    }

    private void Button_Edit(object sender, EventArgs e)
    {
        Keys modifiers = GetRelevantModifiers(Control.ModifierKeys);
        if (modifiers == Keys.Alt)
        {
            Close();
        }
        else if (modifiers == Keys.Shift)
        {
            Edit_NewFileInline(sender, e);
        }
        else if (modifiers == Keys.None)
        {
            commandService.EditSelection(lastFileBrowser, this);
        }
    }
    private void Button_Copy(object sender, EventArgs e)
        {
            FileBrowser source = lastFileBrowser ?? fileBrowserLeft;
            Keys modifiers = GetRelevantModifiers(Control.ModifierKeys);
            if (modifiers == Keys.Alt)
            {
                commandService.CreateArchive(source, GetPassiveBrowser(source), this);
                return;
            }

            if (modifiers == Keys.Shift)
            {
                commandService.CopySelectionInPlace(source, CopyComplete, this);
                return;
            }

            if (modifiers == Keys.None)
            {
                commandService.CopySelection(source, GetPassiveBrowser(source), CopyComplete, this);
            }
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
            Keys modifiers = GetRelevantModifiers(Control.ModifierKeys);
            if (modifiers == Keys.Shift)
            {
                commandService.RenameSelection(source);
                return;
            }

            if (modifiers == Keys.None)
            {
                commandService.MoveSelection(source, GetPassiveBrowser(source), CopyComplete, this);
            }
        }

        private void Button_NewFolder(object sender, EventArgs e)
        {
            Keys modifiers = GetRelevantModifiers(Control.ModifierKeys);
            if (modifiers == Keys.None)
            {
                commandService.CreateFolder(lastFileBrowser, this);
            }
            else if (modifiers == Keys.Alt)
            {
                OpenSearch(sender, e);
            }
        }

        private async void OpenSearch(object sender, EventArgs e)
        {
            FileBrowser target = lastFileBrowser ?? fileBrowserLeft;
            string initialDirectory = !string.IsNullOrWhiteSpace(target?.CurrentPath)
                ? target.CurrentPath
                : Environment.CurrentDirectory;
            using var dialog = new FormSearch(initialDirectory);
            if (dialog.ShowDialog(this) != DialogResult.OK || dialog.Query == null)
            {
                return;
            }

            target.ActivatePanel();
            await target.EnterSearchAsync(dialog.Query);
        }

        private void OpenAiOrganizer(object sender, EventArgs e)
        {
            FileBrowser target = lastFileBrowser ?? fileBrowserLeft;
            if (target == null || target.IsVirtualMode || string.IsNullOrWhiteSpace(target.CurrentPath) || !Directory.Exists(target.CurrentPath))
            {
                MessageBox.Show(this, Language.getString("aiOrganizerPhysicalFolderRequired"), Language.getString("Info"), MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            using var dialog = new FormAiOrganizer(target.CurrentPath);
            dialog.ShowDialog(this);
            if (dialog.AppliedChanges)
            {
                fileBrowserLeft?.RefreshCurrentDirectory();
                fileBrowserRight?.RefreshCurrentDirectory();
            }
        }

        private void OpenVectorIndex(object sender, EventArgs e)
        {
            string path = GetActivePhysicalDirectory();
            if (path == null) return;
            using var dialog = new FormVectorIndex(path);
            dialog.ShowDialog(this);
        }

        private void OpenSemanticSearch(object sender, EventArgs e)
        {
            string path = GetActivePhysicalDirectory();
            if (path == null) return;
            using var dialog = new FormSemanticSearch(path, false);
            dialog.ShowDialog(this);
        }

        private void OpenVectorRag(object sender, EventArgs e)
        {
            string path = GetActivePhysicalDirectory();
            if (path == null) return;
            using var dialog = new FormSemanticSearch(path, true);
            dialog.ShowDialog(this);
        }

        private string GetActivePhysicalDirectory()
        {
            FileBrowser target = lastFileBrowser ?? fileBrowserLeft;
            if (target != null && !target.IsVirtualMode && !string.IsNullOrWhiteSpace(target.CurrentPath) && Directory.Exists(target.CurrentPath))
                return target.CurrentPath;
            MessageBox.Show(this, Language.getString("aiOrganizerPhysicalFolderRequired"), Language.getString("Info"), MessageBoxButtons.OK, MessageBoxIcon.Information);
            return null;
        }

        private void Button_Delete(object sender, EventArgs e)
        {
            if (GetRelevantModifiers(Control.ModifierKeys) == Keys.None)
            {
                commandService.DeleteSelection(lastFileBrowser, () => lastFileBrowser?.Refrech(), this);
            }
        }

        private void RefreshActiveBrowser()
        {
            FileBrowser source = lastFileBrowser ?? fileBrowserLeft;
            source?.RefreshCurrentDirectory();
        }

        private void UpdateCommandButtonLabels(Keys modifiers)
        {
            Keys relevantModifiers = GetRelevantModifiers(modifiers);

            if (functionModifierLabel != null)
                functionModifierLabel.Text = GetModifierLabelText(relevantModifiers);
            if (quickViewButton != null)
                SetFunctionButtonText(quickViewButton, GetQuickViewButtonText(relevantModifiers));
            if (viewButton != null)
                SetFunctionButtonText(viewButton, GetViewButtonText(relevantModifiers));
            if (editButton != null)
                SetFunctionButtonText(editButton, GetEditButtonText(relevantModifiers));
            if (copyButton != null)
                SetFunctionButtonText(copyButton, GetCopyButtonText(relevantModifiers));
            if (moveButton != null)
                SetFunctionButtonText(moveButton, GetMoveButtonText(relevantModifiers));
            if (newFolderButton != null)
                SetFunctionButtonText(
                    newFolderButton,
                    relevantModifiers == Keys.None
                        ? "F7 " + Language.getString("newFolder")
                        : relevantModifiers == Keys.Alt
                            ? "F7 " + Language.getString("search")
                            : string.Empty);
            if (deleteButton != null)
                SetFunctionButtonText(
                    deleteButton,
                    relevantModifiers == Keys.None ? "F8 " + Language.getString("delete") : string.Empty);

            UpdateStatusHint(relevantModifiers);
        }

        private static void SetFunctionButtonText(ToolStripButton button, string text)
        {
            button.Text = text;
            button.Enabled = !string.IsNullOrWhiteSpace(text);
        }

        private static Keys GetRelevantModifiers(Keys modifiers)
        {
            return modifiers & (Keys.Shift | Keys.Alt | Keys.Control);
        }

        private static string GetModifierLabelText(Keys modifiers)
        {
            List<string> names = new List<string>();
            if ((modifiers & Keys.Control) == Keys.Control)
                names.Add("Ctrl");
            if ((modifiers & Keys.Alt) == Keys.Alt)
                names.Add("Alt");
            if ((modifiers & Keys.Shift) == Keys.Shift)
                names.Add("Shift");
            return string.Join("+", names);
        }

        private static string GetQuickViewButtonText(Keys modifiers)
        {
            return modifiers == Keys.None
                ? "F2 " + Language.getString("quickView")
                : string.Empty;
        }

        private static string GetViewButtonText(Keys modifiers)
        {
            if (modifiers == Keys.Control)
                return "F3 " + Language.getString("rawView");
            if (modifiers == Keys.Shift)
                return "F3 " + Language.getString("compare");
            return modifiers == Keys.None
                ? "F3 " + Language.getString("view")
                : string.Empty;
        }

        private static string GetEditButtonText(Keys modifiers)
        {
            if (modifiers == Keys.Alt)
                return "F4 " + Language.getString("close");
            if (modifiers == Keys.Shift)
                return "F4 " + Language.getString("newFile");
            return modifiers == Keys.None
                ? "F4 " + Language.getString("edit")
                : string.Empty;
        }

        private static string GetCopyButtonText(Keys modifiers)
        {
            if (modifiers == Keys.Alt)
                return "F5 " + Language.getString("archiveCreate").TrimEnd('.');
            if (modifiers == Keys.Shift)
                return "F5 " + Language.getString("helpCopyInPlace");
            return modifiers == Keys.None
                ? "F5 " + Language.getString("copy")
                : string.Empty;
        }

        private static string GetMoveButtonText(Keys modifiers)
        {
            if (modifiers == Keys.Shift)
                return "F6 " + Language.getString("rename");
            return modifiers == Keys.None
                ? "F6 " + Language.getString("move")
                : string.Empty;
        }

        private void Button_QuickView(object sender, EventArgs e)
        {
            if (GetRelevantModifiers(Control.ModifierKeys) == Keys.None)
            {
                ToggleQuickView();
                ApplyLocalization();
            }
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

            if (sourceBrowser?.IsCompoundMode == true &&
                sourceBrowser.SelectedCompoundStreamPaths.Length == 1 &&
                string.Equals(sourceBrowser.SelectedCompoundStreamPaths[0], "WordDocument", StringComparison.OrdinalIgnoreCase))
            {
                quickViewControl.DisplayWordBinaryDocument(sourceBrowser.OpenVirtualSourcePath);
                return;
            }

            string fileToPreview;
            try
            {
                fileToPreview = sourceBrowser?.IsArchiveMode == true
                    ? await sourceBrowser.MaterializeSelectedArchiveFileAsync()
                    : sourceBrowser?.IsCompoundMode == true
                        ? await sourceBrowser.MaterializeSelectedCompoundStreamAsync()
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
                quickViewControl?.ApplyUserSettings();
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
            var items = new[]
            {
                new KeyboardHelpItem("F1", Language.getString("helpShowThis")),
                new KeyboardHelpItem("F2 / Ctrl+Q", Language.getString("helpToggleQuickView")),
                new KeyboardHelpItem("F3", Language.getString("view")),
                new KeyboardHelpItem("Ctrl+F3", Language.getString("rawView")),
                new KeyboardHelpItem("Shift+F3", Language.getString("compare")),
                new KeyboardHelpItem("F4", Language.getString("edit")),
                new KeyboardHelpItem("Shift+F4", Language.getString("newFile")),
                new KeyboardHelpItem("Ctrl+N", Language.getString("helpNewFileDialog")),
                new KeyboardHelpItem("F5", Language.getString("copy")),
                new KeyboardHelpItem("Shift+F5", Language.getString("helpCopyInPlace")),
                new KeyboardHelpItem("Alt+F5", Language.getString("archiveCreate")),
                new KeyboardHelpItem("F6", Language.getString("move")),
                new KeyboardHelpItem("Shift+F6", Language.getString("rename")),
                new KeyboardHelpItem("F7", Language.getString("newFolder")),
                new KeyboardHelpItem("Alt+F7", Language.getString("search")),
                new KeyboardHelpItem("F8", Language.getString("delete")),
                new KeyboardHelpItem("Num *", Language.getString("helpInvertPhysicalSelection")),
                new KeyboardHelpItem("Num +", Language.getString("helpSelectByMask")),
                new KeyboardHelpItem("Num -", Language.getString("helpUnselectByMask")),
                new KeyboardHelpItem("Ctrl+Shift+C", Language.getString("copyPathsAsText")),
                new KeyboardHelpItem("Alt+F9", Language.getString("archiveExtract")),
                new KeyboardHelpItem("Tab", Language.getString("helpSwitchPanel")),
                new KeyboardHelpItem("Ctrl+PgDn", Language.getString("helpOpenArchiveBySignature")),
                new KeyboardHelpItem("Backspace", Language.getString("helpHistoryBack")),
                new KeyboardHelpItem("Ctrl+PgUp", Language.getString("helpNavigateParent")),
                new KeyboardHelpItem("Ctrl+R", Language.getString("helpRefreshActivePanel")),
                new KeyboardHelpItem("Ctrl+G", Language.getString("helpGoToLocation")),
                new KeyboardHelpItem("Ctrl+F", Language.getString("helpFeedToPanel")),
                new KeyboardHelpItem("Ctrl+L", Language.getString("helpFocusCommandLine")),
                new KeyboardHelpItem("Ctrl+Enter", Language.getString("helpCopyNameToCommandLine")),
                new KeyboardHelpItem("Shift+Enter", Language.getString("helpPersistentConsole")),
                new KeyboardHelpItem("Alt+F4", Language.getString("helpCloseApplication"))
            };
            using var helpForm = new FormKeyboardHelp(
                "DotNetCommander",
                Language.getString("mainHelpSubtitle"),
                items);
            helpForm.ShowDialog(this);
        }

        private FileBrowser GetPassiveBrowser(FileBrowser activeBrowser)
        {
            return activeBrowser == fileBrowserRight ? fileBrowserLeft : fileBrowserRight;
        }

        private void FileBrowser_FeedToPanelRequested(object sender, SearchFeedEventArgs e)
        {
            if (sender is not FileBrowser source)
            {
                return;
            }

            FileBrowser target = GetPassiveBrowser(source);
            if (target == null || ReferenceEquals(target, source))
            {
                return;
            }

            source.ExitSearch(false);
            target.ActivatePanel();
            target.EnterResultsSnapshot(e.Items, e.Description);
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
            private readonly Func<Message, bool> handlePanelSwitch;

            public ModifierKeyMessageFilter(Action<Keys> updateLabels, Func<Message, bool> handlePanelSwitch)
            {
                this.updateLabels = updateLabels;
                this.handlePanelSwitch = handlePanelSwitch;
            }

            public bool PreFilterMessage(ref Message m)
            {
                if (m.Msg == 0x0100 && handlePanelSwitch?.Invoke(m) == true)
                {
                    return true;
                }

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
