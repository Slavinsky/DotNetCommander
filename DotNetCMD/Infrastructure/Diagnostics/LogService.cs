using System;
using System.IO;
using System.Text;

namespace DotNetCommander
{
    internal static class LogService
    {
        private static readonly object SyncRoot = new object();

        public static string GetLogDirectory()
        {
            string configPath = SettingsStorage.GetUserConfigPath();
            string baseDirectory = Path.GetDirectoryName(configPath) ?? AppDomain.CurrentDomain.BaseDirectory;
            string logDirectory = Path.Combine(baseDirectory, "logs");
            Directory.CreateDirectory(logDirectory);
            return logDirectory;
        }

        public static string GetCurrentLogPath()
        {
            return Path.Combine(GetLogDirectory(), $"DotNetCommander-{DateTime.Now:yyyy-MM-dd}.log");
        }

        public static void LogInfo(string context, string message)
        {
            WriteEntry("INFO", context, message, null);
        }

        public static void LogException(string context, Exception exception)
        {
            if (exception == null)
            {
                return;
            }

            WriteEntry("ERROR", context, exception.Message, exception);
        }

        private static void WriteEntry(string level, string context, string message, Exception exception)
        {
            try
            {
                StringBuilder builder = new StringBuilder();
                builder.Append('[').Append(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff")).Append("] ");
                builder.Append(level).Append(" ");
                builder.Append(context);
                if (!string.IsNullOrWhiteSpace(message))
                {
                    builder.Append(": ").Append(message);
                }

                if (exception != null)
                {
                    builder.AppendLine();
                    builder.Append(exception);
                }

                lock (SyncRoot)
                {
                    File.AppendAllText(GetCurrentLogPath(), builder.ToString() + Environment.NewLine + Environment.NewLine, Encoding.UTF8);
                }
            }
            catch
            {
            }
        }
    }
}
