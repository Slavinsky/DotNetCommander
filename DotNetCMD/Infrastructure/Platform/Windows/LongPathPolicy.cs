using System;
using System.IO;

namespace DotNetCommander
{
    /// <summary>
    /// Long-path policy for raw Win32 and shell boundaries (SHGetFileInfo,
    /// ShellExecuteEx, process launches). BCL file APIs handle long paths on
    /// .NET 8 themselves and must keep receiving plain paths; only calls that
    /// still fail with ERROR_FILENAME_EXCED_RANGE are retried with the
    /// extended-length (`\\?\` / `\\?\UNC\`) form produced here.
    /// </summary>
    internal static class LongPathPolicy
    {
        public const int MaxPath = 260;

        private const string DevicePrefix = @"\\?\";
        private const string DeviceNamespacePrefix = @"\\.\";
        private const string UncDevicePrefix = @"\\?\UNC\";

        public static bool NeedsExtendedPrefix(string path)
        {
            if (string.IsNullOrWhiteSpace(path) || path.Length < MaxPath)
            {
                return false;
            }

            if (path.StartsWith(DevicePrefix, StringComparison.Ordinal)
                || path.StartsWith(DeviceNamespacePrefix, StringComparison.Ordinal))
            {
                return false;
            }

            return Path.IsPathRooted(path);
        }

        public static string Apply(string path)
        {
            if (!NeedsExtendedPrefix(path))
            {
                return path;
            }

            if (path.Length >= 2 && IsSeparator(path[0]) && IsSeparator(path[1]))
            {
                return UncDevicePrefix + path.Substring(2);
            }

            return DevicePrefix + path;
        }

        private static bool IsSeparator(char value)
        {
            return value == '\\' || value == '/';
        }
    }
}
