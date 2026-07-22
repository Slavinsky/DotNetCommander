using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Windows.Forms;

namespace DotNetCommander
{
    internal sealed class GedcomQuickViewControl : Panel
    {
        private const int NodeWidth = 180;
        private const int NodeHeight = 90;
        private const int VerticalSpacing = 150;
        private const float MinZoom = 0.3f;
        private const float MaxZoom = 3f;
        private const float ZoomStep = 0.1f;
        private GedcomPersonEntry selectedPerson;
        private float zoom = 1f;
        private float panX;
        private float panY;
        private bool dragging;
        private Point lastMousePosition;
        private readonly List<PersonHitTarget> personHitTargets = new List<PersonHitTarget>();

        public GedcomQuickViewControl()
        {
            BackColor = Color.White;
            Dock = DockStyle.Fill;
            Visible = false;
            DoubleBuffered = true;
            ResizeRedraw = true;
            TabStop = true;
        }

        public event EventHandler<GedcomPersonActivatedEventArgs> PersonActivated;

        public void DisplayPerson(GedcomPersonEntry person)
        {
            selectedPerson = person;
            ResetView();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            if (selectedPerson == null)
            {
                return;
            }

            Graphics graphics = e.Graphics;
            personHitTargets.Clear();
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            graphics.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAlias;
            GraphicsState state = graphics.Save();
            graphics.TranslateTransform(panX, panY);
            graphics.ScaleTransform(zoom, zoom);

            int centerX = ClientSize.Width / 2;
            int centerY = ClientSize.Height / 2;
            DrawPersonBox(graphics, selectedPerson, centerX, centerY, Color.LightGreen, true);
            DrawParents(graphics, centerX, centerY);
            DrawChildren(graphics, centerX, centerY);
            DrawSpouses(graphics, centerX, centerY);
            graphics.Restore(state);

            using var font = new Font("Segoe UI", 9f);
            using var brush = new SolidBrush(Color.DimGray);
            graphics.DrawString($"{zoom:P0}   Mouse wheel: zoom   Drag: pan   Double-click: select", font, brush, 8, 8);
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            Focus();
            if (e.Button == MouseButtons.Left)
            {
                dragging = true;
                lastMousePosition = e.Location;
                Cursor = Cursors.Hand;
                Capture = true;
            }
        }

        protected override void OnMouseEnter(EventArgs e)
        {
            base.OnMouseEnter(e);
            Focus();
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (!dragging)
            {
                Cursor = HitTestPerson(e.Location) != null ? Cursors.Hand : Cursors.Default;
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

        protected override void OnMouseDoubleClick(MouseEventArgs e)
        {
            base.OnMouseDoubleClick(e);
            if (e.Button != MouseButtons.Left)
            {
                return;
            }

            GedcomPersonEntry person = HitTestPerson(e.Location);
            if (person != null && !string.IsNullOrWhiteSpace(person.Id))
            {
                PersonActivated?.Invoke(this, new GedcomPersonActivatedEventArgs(person.Id));
            }
        }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            base.OnMouseWheel(e);
            float previousZoom = zoom;
            zoom = e.Delta > 0
                ? Math.Min(MaxZoom, zoom + ZoomStep)
                : Math.Max(MinZoom, zoom - ZoomStep);
            float factor = zoom / previousZoom;
            panX = e.X - (e.X - panX) * factor;
            panY = e.Y - (e.Y - panY) * factor;
            Invalidate();
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (keyData == Keys.Add || keyData == Keys.Oemplus || keyData == (Keys.Shift | Keys.Oemplus))
            {
                zoom = Math.Min(MaxZoom, zoom + ZoomStep);
                Invalidate();
                return true;
            }
            if (keyData == Keys.Subtract || keyData == Keys.OemMinus)
            {
                zoom = Math.Max(MinZoom, zoom - ZoomStep);
                Invalidate();
                return true;
            }
            if (keyData == Keys.Home || keyData == Keys.D0 || keyData == Keys.NumPad0)
            {
                ResetView();
                return true;
            }
            return base.ProcessCmdKey(ref msg, keyData);
        }

        private void ResetView()
        {
            zoom = 1f;
            panX = 0;
            panY = 0;
            Invalidate();
        }

        private void DrawParents(Graphics graphics, int centerX, int centerY)
        {
            var parents = selectedPerson.FamiliesAsChild
                .SelectMany(family => new[] { family.Husband, family.Wife })
                .Where(person => person != null)
                .Distinct()
                .ToList();
            DrawPersonRow(graphics, parents, centerX, centerY - VerticalSpacing, Color.LightBlue, (person, x, y) =>
                DrawLine(graphics, x, y + NodeHeight / 2, centerX, centerY - NodeHeight / 2));
        }

        private void DrawChildren(Graphics graphics, int centerX, int centerY)
        {
            var children = selectedPerson.FamiliesAsSpouse
                .SelectMany(family => family.Children)
                .Where(person => person != null)
                .Distinct()
                .ToList();
            DrawPersonRow(graphics, children, centerX, centerY + VerticalSpacing, Color.LightYellow, (person, x, y) =>
                DrawLine(graphics, centerX, centerY + NodeHeight / 2, x, y - NodeHeight / 2));
        }

        private void DrawSpouses(Graphics graphics, int centerX, int centerY)
        {
            var spouses = selectedPerson.FamiliesAsSpouse
                .Select(family => ReferenceEquals(family.Husband, selectedPerson) ? family.Wife : family.Husband)
                .Where(person => person != null)
                .Distinct()
                .ToList();
            for (int index = 0; index < spouses.Count; index++)
            {
                int side = index % 2 == 0 ? 1 : -1;
                int distance = index / 2 + 1;
                int x = centerX + side * distance * (NodeWidth + 90);
                DrawPersonBox(graphics, spouses[index], x, centerY, Color.LightCoral, false);
                int startX = x < centerX ? x + NodeWidth / 2 : x - NodeWidth / 2;
                int endX = x < centerX ? centerX - NodeWidth / 2 : centerX + NodeWidth / 2;
                DrawLine(graphics, startX, centerY, endX, centerY);
            }
        }

        private void DrawPersonRow(
            Graphics graphics,
            IReadOnlyList<GedcomPersonEntry> people,
            int centerX,
            int rowY,
            Color color,
            Action<GedcomPersonEntry, int, int> drawConnection)
        {
            if (people.Count == 0)
            {
                return;
            }

            int spacing = NodeWidth + 45;
            int startX = centerX - (people.Count - 1) * spacing / 2;
            for (int index = 0; index < people.Count; index++)
            {
                int x = startX + index * spacing;
                drawConnection(people[index], x, rowY);
                DrawPersonBox(graphics, people[index], x, rowY, color, false);
            }
        }

        private void DrawPersonBox(Graphics graphics, GedcomPersonEntry person, int x, int y, Color color, bool selected)
        {
            var rectangle = new Rectangle(x - NodeWidth / 2, y - NodeHeight / 2, NodeWidth, NodeHeight);
            personHitTargets.Add(new PersonHitTarget(person, rectangle));
            using (var brush = new SolidBrush(color))
            {
                graphics.FillRectangle(brush, rectangle);
            }
            using (var pen = new Pen(selected ? Color.DarkBlue : Color.Gray, selected ? 3f : 1f))
            {
                graphics.DrawRectangle(pen, rectangle);
            }

            string name = string.Join(" ", new[] { person.FirstName, person.LastName }.Where(value => !string.IsNullOrWhiteSpace(value)));
            using var nameFont = new Font("Segoe UI", 11f, FontStyle.Bold);
            using var dateFont = new Font("Segoe UI", 9f);
            using var nameBrush = new SolidBrush(Color.Black);
            using var dateBrush = new SolidBrush(Color.DimGray);
            using var format = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
            graphics.DrawString(name, nameFont, nameBrush, new RectangleF(rectangle.X + 3, rectangle.Y + 3, rectangle.Width - 6, 48), format);

            string dates = string.Empty;
            if (!string.IsNullOrWhiteSpace(person.BirthDateText))
            {
                dates = "b. " + person.BirthDateText;
            }
            if (!string.IsNullOrWhiteSpace(person.DeathDateText))
            {
                dates += (dates.Length == 0 ? string.Empty : Environment.NewLine) + "d. " + person.DeathDateText;
            }
            graphics.DrawString(dates, dateFont, dateBrush, new RectangleF(rectangle.X + 3, rectangle.Y + 49, rectangle.Width - 6, 36), format);
        }

        private static void DrawLine(Graphics graphics, int x1, int y1, int x2, int y2)
        {
            using var pen = new Pen(Color.Gray, 2f);
            graphics.DrawLine(pen, x1, y1, x2, y2);
        }

        private GedcomPersonEntry HitTestPerson(Point clientPoint)
        {
            if (personHitTargets.Count == 0 || zoom <= 0f)
            {
                return null;
            }

            PointF[] points = { new PointF(clientPoint.X, clientPoint.Y) };
            using (var transform = new Matrix())
            {
                transform.Translate(panX, panY);
                transform.Scale(zoom, zoom);
                transform.Invert();
                transform.TransformPoints(points);
            }

            for (int index = personHitTargets.Count - 1; index >= 0; index--)
            {
                PersonHitTarget target = personHitTargets[index];
                if (target.Bounds.Contains(points[0]))
                {
                    return target.Person;
                }
            }
            return null;
        }

        private sealed class PersonHitTarget
        {
            public PersonHitTarget(GedcomPersonEntry person, RectangleF bounds)
            {
                Person = person;
                Bounds = bounds;
            }

            public GedcomPersonEntry Person { get; }
            public RectangleF Bounds { get; }
        }
    }

    internal sealed class GedcomPersonActivatedEventArgs : EventArgs
    {
        public GedcomPersonActivatedEventArgs(string personId)
        {
            PersonId = personId;
        }

        public string PersonId { get; }
    }
}
