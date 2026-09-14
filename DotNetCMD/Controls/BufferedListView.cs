using System.Windows.Forms;

namespace DotNetCommander
{
    internal sealed class BufferedListView : ListView
    {
        public BufferedListView()
        {
            DoubleBuffered = true;
            ResizeRedraw = true;
        }
    }
}
