using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.IO;
using System.Drawing;

namespace DotNetCommander
{
    class Unix
    {
        private static void AddDriveListItem(ToolStrip strip, String name, String text, String tag, EventHandler toolStipButtonClickHandler)
        {
            ToolStripButton Item = null;
            try
            {
                Item = new ToolStripButton();
                Item.Name = name;
                Item.Text = text;
                Item.Tag = tag;
                Item.Click += new System.EventHandler(toolStipButtonClickHandler);
                Item.ImageIndex = OS.FindIcon(Item.Tag as string, strip.ImageList);
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

        public static void GetDriverToolList(ToolStrip toolStip, EventHandler toolStipButtonClickHandler)
        {
            AddDriveListItem(toolStip, "/", "/", "/", toolStipButtonClickHandler);
            AddDriveListItem(toolStip, "home", "home", "/home", toolStipButtonClickHandler);
            AddDriveListItem(toolStip, "media", "media", "/media", toolStipButtonClickHandler);
            AddDriveListItem(toolStip, "etc", "etc", "/etc", toolStipButtonClickHandler);
            AddDriveListItem(toolStip, "mnt", "mnt", "/mnt", toolStipButtonClickHandler);
            AddDriveListItem(toolStip, "bin", "bin", "/bin", toolStipButtonClickHandler);
            AddDriveListItem(toolStip, "dev", "dev", "/dev", toolStipButtonClickHandler);
            AddDriveListItem(toolStip, "lib", "lib", "/lib", toolStipButtonClickHandler);
            AddDriveListItem(toolStip, "boot", "boot", "/boot", toolStipButtonClickHandler);
            AddDriveListItem(toolStip, "proc", "proc", "/proc", toolStipButtonClickHandler);
            AddDriveListItem(toolStip, "opt", "opt", "/opt", toolStipButtonClickHandler);
            AddDriveListItem(toolStip, "sys", "sys", "/sys", toolStipButtonClickHandler);
            AddDriveListItem(toolStip, "var", "var", "/var", toolStipButtonClickHandler);
        }

        public static String GetStartPath()
        {
            return "/";
        }
    }
}
