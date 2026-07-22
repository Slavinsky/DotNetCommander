using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Drawing;
using System.Windows.Forms;

namespace DotNetCommander
{
    internal sealed class FileIconData : IDisposable
    {
        public FileIconData(string key, Icon icon)
        {
            Key = key;
            Icon = icon;
        }

        public string Key { get; }
        public Icon Icon { get; }

        public void Dispose()
        {
            Icon?.Dispose();
        }
    }

    class OS
    {
        /**
         * Search Icon from File/Dir and add this to imageList.
         * @return index of the Icon in the imageList
         */
        public static int FindIcon(string fileName, ImageList imageList, bool largeIcon = false)
        {
            if (Environment.OSVersion.Platform == PlatformID.Win32NT || Environment.OSVersion.Platform == PlatformID.Win32S || Environment.OSVersion.Platform == PlatformID.Win32Windows || Environment.OSVersion.Platform == PlatformID.WinCE)
            {
                return Win32.FindIcon(fileName, imageList, largeIcon);
            }
            else
            {
                return 0;
            }
        }

        public static FileIconData GetIconData(string fileName, bool isDirectory, bool largeIcon = false)
        {
            if (Environment.OSVersion.Platform == PlatformID.Win32NT || Environment.OSVersion.Platform == PlatformID.Win32S || Environment.OSVersion.Platform == PlatformID.Win32Windows || Environment.OSVersion.Platform == PlatformID.WinCE)
            {
                return Win32.GetIconData(fileName, isDirectory, largeIcon);
            }

            return null;
        }

        public static String ShortcutGetTargetPath(String path)
        {
            if (Environment.OSVersion.Platform == PlatformID.Win32NT || Environment.OSVersion.Platform == PlatformID.Win32S || Environment.OSVersion.Platform == PlatformID.Win32Windows || Environment.OSVersion.Platform == PlatformID.WinCE)
            {
                return Win32.ShortcutGetTargetPath(path);
            }
            else
            {
                return null;
            }
        }

    /**
     * Win32: Return a ToolStrip with all Drivers.
     * Unix: Return a ToolStrip with know Root Folders (home/bin/etc...)
     * @return ToolStrip
     */
    public static void GetDriverToolList(ToolStrip toolStip, EventHandler toolStipButtonClickHandler) {
      toolStip.ImageList = new ImageList();
      toolStip.ImageList.ColorDepth = ColorDepth.Depth32Bit;

      if (Environment.OSVersion.Platform == PlatformID.Win32NT || Environment.OSVersion.Platform == PlatformID.Win32S || Environment.OSVersion.Platform == PlatformID.Win32Windows || Environment.OSVersion.Platform == PlatformID.WinCE) {
        StartDriveListLoadingWhenReady(toolStip, toolStipButtonClickHandler);
      }
      else if (Environment.OSVersion.Platform == PlatformID.Unix) {
        Unix.GetDriverToolList(toolStip, toolStipButtonClickHandler);
      }
    }

    private static void StartDriveListLoadingWhenReady(ToolStrip toolStip, EventHandler toolStipButtonClickHandler)
    {
      if (toolStip.IsDisposed || toolStip.Disposing)
        return;

      if (toolStip.IsHandleCreated)
      {
        _ = Win32.GetDriverToolListAsync(toolStip, toolStipButtonClickHandler);
        return;
      }

      EventHandler handler = null;
      handler = (sender, args) =>
      {
        toolStip.HandleCreated -= handler;
        if (!toolStip.IsDisposed && !toolStip.Disposing)
        {
          _ = Win32.GetDriverToolListAsync(toolStip, toolStipButtonClickHandler);
        }
      };

      toolStip.HandleCreated += handler;
    }

    public static String GetStartPath()
        {
            if (Environment.OSVersion.Platform == PlatformID.Win32NT || Environment.OSVersion.Platform == PlatformID.Win32S || Environment.OSVersion.Platform == PlatformID.Win32Windows || Environment.OSVersion.Platform == PlatformID.WinCE)
            {
                return Win32.GetStartPath();
            }
            else if (Environment.OSVersion.Platform == PlatformID.Unix)
            {
                return Unix.GetStartPath();
            }
            else
            {
                return null;
            }
        }
    }
}
