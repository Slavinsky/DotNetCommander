using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace DotNetCommander
{
    [Flags]
    public enum BrowserPanelCapabilities
    {
        None = 0,
        Navigate = 1,
        Preview = 2,
        CopyOut = 4,
        Create = 8,
        Rename = 16,
        Delete = 32,
        Paste = 64,
        DragDrop = 128,
        FullFileSystem = Navigate | Preview | CopyOut | Create | Rename | Delete | Paste | DragDrop,
        ReadOnlyVirtual = Navigate | Preview | CopyOut
    }

    public sealed class BrowserItemInfo
    {
        public string Name { get; set; }
        public string Location { get; set; }
        public string NativePath { get; set; }
        public bool IsDirectory { get; set; }
        public long? Size { get; set; }
        public DateTime? Modified { get; set; }
    }

    public abstract class BrowserPanelBase : UserControl
    {
        public event EventHandler SelectionChanged;
        public event EventHandler<BrowserLocationChangedEventArgs> BrowserLocationChanged;

        public abstract string DisplayLocation { get; }
        public abstract IReadOnlyList<BrowserItemInfo> Items { get; }
        public abstract IReadOnlyList<BrowserItemInfo> SelectedItems { get; }
        public abstract BrowserPanelCapabilities Capabilities { get; }
        public bool IsReadOnly => (Capabilities & (BrowserPanelCapabilities.Create | BrowserPanelCapabilities.Rename | BrowserPanelCapabilities.Delete)) == 0;

        public abstract bool Navigate(string location);
        public abstract bool NavigateParent();
        public virtual Task<bool> NavigateBackAsync()
        {
            return Task.FromResult(false);
        }
        public abstract void RefreshPanel();

        protected void RaiseSelectionChanged()
        {
            SelectionChanged?.Invoke(this, EventArgs.Empty);
        }

        protected void RaiseLocationChanged(string location)
        {
            BrowserLocationChanged?.Invoke(this, new BrowserLocationChangedEventArgs(location));
        }
    }

    public sealed class BrowserLocationChangedEventArgs : EventArgs
    {
        public BrowserLocationChangedEventArgs(string location)
        {
            Location = location ?? string.Empty;
        }

        public string Location { get; }
    }
}
