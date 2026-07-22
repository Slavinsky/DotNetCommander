using System;
using System.Diagnostics;
using System.IO;
using System.Threading;

namespace DotNetCommander
{
    internal static class PerfTrace
    {
        public static readonly bool Enabled = false;
        private static readonly object Sync = new object();
        private static readonly string LogPath = Path.Combine(@"C:\tmp", "DotNetCommander-filebrowser.log");
        private static bool sessionStarted;

        public static string CurrentLogPath => LogPath;

        public static void Log(string area, string message)
        {
            if (!Enabled)
                return;

            try
            {
                lock (Sync)
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(LogPath) ?? @"C:\tmp");
                    if (!sessionStarted)
                    {
                        File.AppendAllText(
                            LogPath,
                            Environment.NewLine + "===== SESSION " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff") + " =====" + Environment.NewLine);
                        sessionStarted = true;
                    }

                    string line = string.Format(
                        "{0:HH:mm:ss.fff} [T{1}] {2} | {3}{4}",
                        DateTime.Now,
                        Thread.CurrentThread.ManagedThreadId,
                        area,
                        message,
                        Environment.NewLine);
                    File.AppendAllText(LogPath, line);
                }
            }
            catch
            {
            }
        }

        public static long ElapsedMs(Stopwatch stopwatch)
        {
            return stopwatch?.ElapsedMilliseconds ?? 0;
        }
    }
}
