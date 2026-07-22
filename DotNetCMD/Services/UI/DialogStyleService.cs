using System;
using System.Drawing;
using System.Windows.Forms;

namespace DotNetCommander
{
    internal static class DialogStyleService
    {
        private const string DefaultDialogFontName = "Segoe UI";
        private const float DefaultDialogFontSize = 10f;
        private const float DefaultDialogCaptionFontSize = 9f;
        private const float DefaultDialogEmphasisFontSize = 10f;
        private const float DefaultDialogHeaderFontSize = 13f;

        public static Font CreateDialogFont(FontStyle style = FontStyle.Regular, float? sizeOverride = null)
        {
            string fontName = Properties.Settings.Default.DialogFontName;
            if (string.IsNullOrWhiteSpace(fontName))
            {
                fontName = DefaultDialogFontName;
            }

            float fontSize = sizeOverride ?? Properties.Settings.Default.DialogFontSize;
            if (fontSize < 8f)
            {
                fontSize = DefaultDialogFontSize;
            }

            try
            {
                return new Font(fontName, fontSize, style, GraphicsUnit.Point);
            }
            catch
            {
                return new Font(DefaultDialogFontName, fontSize, style, GraphicsUnit.Point);
            }
        }

        public static Font CreateBodyFont(FontStyle style = FontStyle.Regular)
        {
            return CreateDialogFont(style, GetConfiguredSize(Properties.Settings.Default.DialogFontSize, DefaultDialogFontSize));
        }

        public static Font CreateCaptionFont(FontStyle style = FontStyle.Regular)
        {
            return CreateDialogFont(style, GetConfiguredSize(Properties.Settings.Default.DialogCaptionFontSize, DefaultDialogCaptionFontSize));
        }

        public static Font CreateHeaderFont()
        {
            float size = GetConfiguredSize(Properties.Settings.Default.DialogHeaderFontSize, DefaultDialogHeaderFontSize);
            return CreateDialogFont(FontStyle.Bold, size);
        }

        public static Font CreateEmphasisFont()
        {
            float size = GetConfiguredSize(Properties.Settings.Default.DialogEmphasisFontSize, DefaultDialogEmphasisFontSize);
            return CreateDialogFont(FontStyle.Bold, size);
        }

        public static void ApplyDialogFont(Control root)
        {
            if (root == null)
                return;

            Font dialogFont = CreateDialogFont();
            root.Font = dialogFont;
        }

        private static float GetConfiguredSize(float configuredSize, float fallbackSize)
        {
            return configuredSize < 8f ? fallbackSize : configuredSize;
        }
    }
}
