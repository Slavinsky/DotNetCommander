using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;
using System.Drawing;
using System.IO;
using System.Threading.Tasks;

namespace DotNetCommander
{
    class Win32
    {
        public const uint SHGFI_ICON = 0x100;
        public const uint SHGFI_LARGEICON = 0x0; // 'Large icon
        public const uint SHGFI_SMALLICON = 0x1; // 'Small icon
        public const uint SHGFI_USEFILEATTRIBUTES = 0x10;     // use passed dwFileAttribute
        private const uint FILE_ATTRIBUTE_DIRECTORY = 0x10;
        private const uint FILE_ATTRIBUTE_NORMAL = 0x80;
        private const uint DRIVE_UNKNOWN = 0;
        private const uint DRIVE_NO_ROOT_DIR = 1;
        private const uint DRIVE_REMOVABLE = 2;
        private const uint DRIVE_FIXED = 3;
        private const uint DRIVE_REMOTE = 4;
        private const uint DRIVE_CDROM = 5;
        private const uint DRIVE_RAMDISK = 6;

         [StructLayout(LayoutKind.Sequential)]
        public struct SHFILEINFO
        {
            public IntPtr hIcon;
            public int iIcon;
            public int dwAttributes;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
            public string szDisplayName;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)]
            public string szTypeName;
        }

        [DllImport("shell32.dll",BestFitMapping=false,ThrowOnUnmappableChar=true)]
        public static extern IntPtr SHGetFileInfo(string pszPath, uint dwFileAttributes, ref SHFILEINFO psfi, uint cbSizeFileInfo, uint uFlags);

        [DllImport("User32.dll")]
        public static extern int DestroyIcon(IntPtr hIcon);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
        private static extern uint GetDriveType(string lpRootPathName);

        public static FileIconData GetIconData(string fileName, bool isDirectory, bool largeIcon = false)
        {
            SHFILEINFO shinfo = new SHFILEINFO();
            try
            {
                uint sizeFlag = largeIcon ? SHGFI_LARGEICON : SHGFI_SMALLICON;
                uint fileAttributes = isDirectory ? FILE_ATTRIBUTE_DIRECTORY : FILE_ATTRIBUTE_NORMAL;
                string iconLookupTarget = GetIconLookupTarget(fileName, isDirectory);
                SHGetFileInfo(
                    iconLookupTarget,
                    fileAttributes,
                    ref shinfo,
                    (uint)Marshal.SizeOf(shinfo),
                    SHGFI_ICON | SHGFI_USEFILEATTRIBUTES | sizeFlag);
            }
            catch (Exception)
            {
                return null;
            }

            if (shinfo.hIcon == IntPtr.Zero)
                return null;

            try
            {
                Icon icon = (Icon)Icon.FromHandle(shinfo.hIcon).Clone();
                return new FileIconData(shinfo.iIcon.ToString(), icon);
            }
            finally
            {
                DestroyIcon(shinfo.hIcon);
            }
        }

        private static string GetIconLookupTarget(string fileName, bool isDirectory)
        {
            if (isDirectory)
            {
                return string.IsNullOrWhiteSpace(fileName) ? "folder" : fileName;
            }

            string extension = Path.GetExtension(fileName);
            if (!string.IsNullOrWhiteSpace(extension))
            {
                return extension;
            }

            return string.IsNullOrWhiteSpace(fileName) ? "file" : fileName;
        }

        public static int FindIcon(string FileName, ImageList imageList, bool largeIcon = false)
        {
            // http://www.codeproject.com/KB/files/fileicon.aspx
            //try
            //{
            String key = null;
            IntPtr hImgSmall; //the handle to the system image list
            int image_index;

            Win32.SHFILEINFO shinfo = new Win32.SHFILEINFO();
      try {
        uint sizeFlag = largeIcon ? SHGFI_LARGEICON : SHGFI_SMALLICON;
        hImgSmall = Win32.SHGetFileInfo(FileName, 0, ref shinfo, (uint)Marshal.SizeOf(shinfo), Win32.SHGFI_ICON | sizeFlag);
      }
      catch (Exception ex) {
        LogService.LogException("Win32.FindIcon.SHGetFileInfo", ex);
        return -1;
      }

            if (shinfo.hIcon == IntPtr.Zero)
            {
                return -1;
            }

            key = shinfo.iIcon.ToString();

            if ((image_index = imageList.Images.IndexOfKey(key)) == -1)
            {
                try
                {
                    Icon myIcon = (Icon)Icon.FromHandle(shinfo.hIcon).Clone();
                    imageList.Images.Add(key, myIcon);
                    image_index = imageList.Images.Count - 1;
                }
                catch (ArgumentException ex)
                {
                    LogService.LogException("Win32.FindIcon.IconFromHandle", ex);
                    return -1;
                }
                catch (ExternalException ex)
                {
                    LogService.LogException("Win32.FindIcon.IconClone", ex);
                    return -1;
                }
            }
            try
            {
                return image_index;
            }
            finally
            {
                DestroyIcon(shinfo.hIcon); // Cleanup
            }
            //}
            // catch
            //{
            //    return 0;
            //}
        }

        public static String ShortcutGetTargetPath(String path)
        {
            Type shellType = Type.GetTypeFromProgID("WScript.Shell");
            if (shellType == null)
            {
                return string.Empty;
            }

            object shellInstance = null;
            object shortcutInstance = null;
            try
            {
                shellInstance = Activator.CreateInstance(shellType);
                if (shellInstance == null)
                {
                    return string.Empty;
                }

                dynamic shell = shellInstance;
                shortcutInstance = shell.CreateShortcut(path);
                if (shortcutInstance == null)
                {
                    return string.Empty;
                }

                dynamic shortcut = shortcutInstance;
                return shortcut.TargetPath as string ?? string.Empty;
            }
            catch (Exception ex)
            {
                LogService.LogException("Win32.ShortcutGetTargetPath", ex);
                return string.Empty;
            }
            finally
            {
                if (shortcutInstance != null && Marshal.IsComObject(shortcutInstance))
                {
                    Marshal.FinalReleaseComObject(shortcutInstance);
                }

                if (shellInstance != null && Marshal.IsComObject(shellInstance))
                {
                    Marshal.FinalReleaseComObject(shellInstance);
                }
            }
        }

        private static void AddDriveListItem(ToolStrip strip,String name, String text, String tag, EventHandler toolStipButtonClickHandler, bool loadDriveIcon)
        {
            ToolStripButton Item = null;
            try
            {
                if (strip.IsDisposed || strip.Disposing)
                    return;

                Item = new ToolStripButton();
                Item.Name = name;
                Item.Text = text;
                Item.Tag = tag;
                Item.Click += new System.EventHandler(toolStipButtonClickHandler);
                int imageIndex = loadDriveIcon ? FindIcon(Item.Tag as string, strip.ImageList) : -1;
                Item.ImageIndex = imageIndex >= 0 ? imageIndex : -1;
                Item.ImageAlign = ContentAlignment.MiddleCenter;
                strip.Items.Add(Item);
                Item = null;
            }
            finally
            {
                if (Item != null)
                    Item.Dispose();
            }
        }

    //public static void GetDriverToolList(ToolStrip toolStip, EventHandler toolStipButtonClickHandler) {
    //  String[] drives = System.Environment.GetLogicalDrives();
    //  foreach (string dr in drives) {
    //    DriveInfo di = new DriveInfo(dr);
    //    String text;

    //    if (di.IsReady) {
    //      text = di.VolumeLabel;
    //    }
    //    else {
    //      text = di.DriveType.ToString();
    //    }
    //    text += " (" + dr.Replace("\\", "") + ")";

    //    // Add to drivelist
    //    AddDriveListItem(toolStip, dr, text, di.Name, toolStipButtonClickHandler);
    //  }
    //}


    public static async Task GetDriverToolListAsync(ToolStrip toolStip, EventHandler toolStipButtonClickHandler) {
      String[] drives = System.Environment.GetLogicalDrives();

      foreach (string dr in drives) {
        DriveListItemInfo itemInfo = await Task.Run(() => BuildDriveListItemInfo(dr)).ConfigureAwait(false);
        if (itemInfo == null || toolStip.IsDisposed || toolStip.Disposing)
          continue;

        try
        {
          if (!toolStip.IsHandleCreated)
          {
            continue;
          }

          toolStip.BeginInvoke(new Action(() =>
          {
            if (toolStip.IsDisposed || toolStip.Disposing)
              return;

            AddDriveListItem(toolStip, itemInfo.Name, itemInfo.Text, itemInfo.Tag, toolStipButtonClickHandler, itemInfo.LoadDriveIcon);
          }));
        }
        catch (InvalidOperationException)
        {
          return;
        }
      }
    }

    private static DriveListItemInfo BuildDriveListItemInfo(string drivePath)
    {
      try
      {
        uint driveType = GetDriveType(drivePath);
        string shortName = drivePath.Replace("\\", "");

        if (driveType == DRIVE_REMOTE)
        {
          return new DriveListItemInfo(drivePath, $"Network ({shortName})", drivePath, false);
        }

        if (driveType == DRIVE_NO_ROOT_DIR || driveType == DRIVE_UNKNOWN)
        {
          return new DriveListItemInfo(drivePath, shortName, drivePath, false);
        }

        string text = BuildSafeDriveLabel(drivePath, driveType, shortName);
        return new DriveListItemInfo(drivePath, text, drivePath, true);
      }
      catch (Exception ex)
      {
        LogService.LogException("Win32.BuildDriveListItemInfo", ex);
        string fallbackText = drivePath.Replace("\\", "");
        return new DriveListItemInfo(drivePath, fallbackText, drivePath, false);
      }
    }

    private static string BuildSafeDriveLabel(string drivePath, uint driveType, string shortName)
    {
      if (driveType == DRIVE_FIXED || driveType == DRIVE_RAMDISK)
      {
        try
        {
          DriveInfo di = new DriveInfo(drivePath);
          if (di.IsReady && !string.IsNullOrWhiteSpace(di.VolumeLabel))
          {
            return di.VolumeLabel + " (" + shortName + ")";
          }
        }
        catch (Exception ex)
        {
          LogService.LogException("Win32.BuildSafeDriveLabel", ex);
        }
      }

      return GetDriveTypeCaption(driveType) + " (" + shortName + ")";
    }

    private static string GetDriveTypeCaption(uint driveType)
    {
      return driveType switch
      {
        DRIVE_REMOVABLE => "Removable",
        DRIVE_FIXED => "Local Disk",
        DRIVE_REMOTE => "Network",
        DRIVE_CDROM => "CD-ROM",
        DRIVE_RAMDISK => "RAM Disk",
        DRIVE_NO_ROOT_DIR => "Unavailable",
        _ => "Drive"
      };
    }

    private sealed class DriveListItemInfo
    {
      public DriveListItemInfo(string name, string text, string tag, bool loadDriveIcon)
      {
        Name = name;
        Text = text;
        Tag = tag;
        LoadDriveIcon = loadDriveIcon;
      }

      public string Name { get; }
      public string Text { get; }
      public string Tag { get; }
      public bool LoadDriveIcon { get; }
    }


    public static String GetStartPath()
        {
            return "C:\\";
        }
    }
}
