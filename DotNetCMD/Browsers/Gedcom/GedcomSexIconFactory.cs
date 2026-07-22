using System;
using System.Drawing;
using System.Drawing.Drawing2D;

namespace DotNetCommander
{
    internal static class GedcomSexIconFactory
    {
        public static Bitmap CreateMaleIcon(int size)
        {
            return CreateIcon(size, Color.FromArgb(55, 128, 235), DrawMaleSymbol);
        }

        public static Bitmap CreateFemaleIcon(int size)
        {
            return CreateIcon(size, Color.FromArgb(226, 72, 145), DrawFemaleSymbol);
        }

        public static Bitmap CreateUnknownIcon(int size)
        {
            return CreateIcon(size, Color.FromArgb(126, 137, 153), DrawUnknownSymbol);
        }

        private static Bitmap CreateIcon(int size, Color background, Action<Graphics, float> drawSymbol)
        {
            var bitmap = new Bitmap(size, size, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
            using Graphics graphics = Graphics.FromImage(bitmap);
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
            float inset = Math.Max(1f, size * 0.04f);
            using (var brush = new SolidBrush(background))
            {
                graphics.FillEllipse(brush, inset, inset, size - inset * 2f, size - inset * 2f);
            }
            drawSymbol(graphics, size);
            return bitmap;
        }

        private static void DrawMaleSymbol(Graphics graphics, float size)
        {
            using var pen = CreateSymbolPen(size);
            graphics.DrawEllipse(pen, size * 0.25f, size * 0.39f, size * 0.36f, size * 0.36f);
            graphics.DrawLine(pen, size * 0.57f, size * 0.42f, size * 0.78f, size * 0.21f);
            graphics.DrawLine(pen, size * 0.62f, size * 0.21f, size * 0.78f, size * 0.21f);
            graphics.DrawLine(pen, size * 0.78f, size * 0.21f, size * 0.78f, size * 0.37f);
        }

        private static void DrawFemaleSymbol(Graphics graphics, float size)
        {
            using var pen = CreateSymbolPen(size);
            graphics.DrawEllipse(pen, size * 0.32f, size * 0.20f, size * 0.36f, size * 0.36f);
            graphics.DrawLine(pen, size * 0.50f, size * 0.56f, size * 0.50f, size * 0.80f);
            graphics.DrawLine(pen, size * 0.37f, size * 0.69f, size * 0.63f, size * 0.69f);
        }

        private static void DrawUnknownSymbol(Graphics graphics, float size)
        {
            using var pen = CreateSymbolPen(size);
            graphics.DrawEllipse(pen, size * 0.28f, size * 0.28f, size * 0.44f, size * 0.44f);
            graphics.DrawLine(pen, size * 0.30f, size * 0.70f, size * 0.70f, size * 0.30f);
        }

        private static Pen CreateSymbolPen(float size)
        {
            return new Pen(Color.White, Math.Max(1.5f, size * 0.105f))
            {
                StartCap = LineCap.Round,
                EndCap = LineCap.Round,
                LineJoin = LineJoin.Round
            };
        }
    }
}
