using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;

namespace DotNetCommander
{
    internal static class FileIconCache
    {
        private static readonly object SyncRoot = new object();
        private static readonly Dictionary<string, Icon> Icons = new Dictionary<string, Icon>(StringComparer.OrdinalIgnoreCase);

        public static string GetTypeKey(string path, bool isDirectory)
        {
            if (isDirectory)
            {
                return "folder";
            }

            string extension = Path.GetExtension(path ?? string.Empty);
            return string.IsNullOrWhiteSpace(extension)
                ? "file"
                : "extension:" + extension.ToLowerInvariant();
        }

        public static FileIconData GetIconData(string path, bool isDirectory, bool largeIcon)
        {
            string typeKey = GetTypeKey(path, isDirectory);
            string cacheKey = (largeIcon ? "large:" : "small:") + typeKey;

            lock (SyncRoot)
            {
                if (Icons.TryGetValue(cacheKey, out Icon cachedIcon))
                {
                    return new FileIconData(typeKey, (Icon)cachedIcon.Clone());
                }

                using FileIconData iconData = OS.GetIconData(path, isDirectory, largeIcon);
                if (iconData?.Icon == null)
                {
                    return null;
                }

                Icon storedIcon = (Icon)iconData.Icon.Clone();
                Icons.Add(cacheKey, storedIcon);
                return new FileIconData(typeKey, (Icon)storedIcon.Clone());
            }
        }
    }
}
