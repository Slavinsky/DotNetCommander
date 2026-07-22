using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace DotNetCommander
{
    internal sealed class InteractiveImageViewControl : Control
    {
        private const float MinZoom = 0.1f;
        private const float MaxZoom = 10f;
        private const float ZoomFactor = 1.15f;
        private Image image;
        private float zoom = 1f;
        private float panX;
        private float panY;
        private bool dragging;
        private Point lastMousePosition;

        public InteractiveImageViewControl()
        {
            BackColor = Color.Black;
            Dock = DockStyle.Fill;
            Visible = false;
            DoubleBuffered = true;
            ResizeRedraw = true;
            TabStop = true;
        }

        public void SetImage(Image value)
        {
            Image previous = image;
            image = value;
            previous?.Dispose();
            ResetView();
        }

        public void ClearImage()
        {
            SetImage(null);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                image?.Dispose();
                image = null;
            }
            base.Dispose(disposing);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            if (image == null || ClientSize.Width <= 0 || ClientSize.Height <= 0)
            {
                return;
            }

            e.Graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
            e.Graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
            float fitScale = Math.Min((float)ClientSize.Width / image.Width, (float)ClientSize.Height / image.Height);
            float scale = fitScale * zoom;
            float width = image.Width * scale;
            float height = image.Height * scale;
            float x = (ClientSize.Width - width) / 2f + panX;
            float y = (ClientSize.Height - height) / 2f + panY;
            e.Graphics.DrawImage(image, x, y, width, height);

            using var overlayBrush = new SolidBrush(Color.FromArgb(170, 0, 0, 0));
            using var textBrush = new SolidBrush(Color.WhiteSmoke);
            using var font = new Font("Segoe UI", 9f);
            string text = $"{zoom:P0}   Mouse wheel: zoom   Drag: pan";
            SizeF textSize = e.Graphics.MeasureString(text, font);
            e.Graphics.FillRectangle(overlayBrush, 6, 6, textSize.Width + 10, textSize.Height + 6);
            e.Graphics.DrawString(text, font, textBrush, 11, 9);
        }

        protected override void OnMouseEnter(EventArgs e)
        {
            base.OnMouseEnter(e);
            Focus();
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            Focus();
            if (e.Button == MouseButtons.Left && image != null)
            {
                dragging = true;
                lastMousePosition = e.Location;
                Cursor = Cursors.Hand;
                Capture = true;
            }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (!dragging)
            {
                return;
            }

            panX += e.X - lastMousePosition.X;
            panY += e.Y - lastMousePosition.Y;
            lastMousePosition = e.Location;
            Invalidate();
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            if (e.Button == MouseButtons.Left)
            {
                dragging = false;
                Cursor = Cursors.Default;
                Capture = false;
            }
        }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            base.OnMouseWheel(e);
            if (image == null)
            {
                return;
            }

            float nextZoom = e.Delta > 0
                ? Math.Min(MaxZoom, zoom * ZoomFactor)
                : Math.Max(MinZoom, zoom / ZoomFactor);
            ZoomAt(nextZoom, e.Location);
        }

        protected override void OnDoubleClick(EventArgs e)
        {
            base.OnDoubleClick(e);
            ResetView();
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (keyData == Keys.Add || keyData == Keys.Oemplus || keyData == (Keys.Shift | Keys.Oemplus))
            {
                ZoomAt(Math.Min(MaxZoom, zoom * ZoomFactor), new Point(ClientSize.Width / 2, ClientSize.Height / 2));
                return true;
            }
            if (keyData == Keys.Subtract || keyData == Keys.OemMinus)
            {
                ZoomAt(Math.Max(MinZoom, zoom / ZoomFactor), new Point(ClientSize.Width / 2, ClientSize.Height / 2));
                return true;
            }
            if (keyData == Keys.Home || keyData == Keys.D0 || keyData == Keys.NumPad0)
            {
                ResetView();
                return true;
            }
            return base.ProcessCmdKey(ref msg, keyData);
        }

        private void ZoomAt(float nextZoom, Point anchor)
        {
            if (Math.Abs(nextZoom - zoom) < 0.0001f)
            {
                return;
            }

            float factor = nextZoom / zoom;
            float relativeX = anchor.X - ClientSize.Width / 2f;
            float relativeY = anchor.Y - ClientSize.Height / 2f;
            panX = relativeX - (relativeX - panX) * factor;
            panY = relativeY - (relativeY - panY) * factor;
            zoom = nextZoom;
            Invalidate();
        }

        private void ResetView()
        {
            zoom = 1f;
            panX = 0;
            panY = 0;
            Invalidate();
        }
    }
}
