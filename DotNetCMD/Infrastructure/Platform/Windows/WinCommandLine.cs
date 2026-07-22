using System;
using System.Diagnostics;
using System.IO;

namespace DotNetCommander
{
    internal static class WinCommandLine
    {
        public static void Execute(string command, string workingDirectory, bool keepConsoleOpen)
        {
            if (string.IsNullOrWhiteSpace(command))
            {
                return;
            }

            string resolvedWorkingDirectory = !string.IsNullOrWhiteSpace(workingDirectory) && Directory.Exists(workingDirectory)
                ? workingDirectory
                : Environment.CurrentDirectory;
            string commandProcessor = Environment.GetEnvironmentVariable("ComSpec");
            if (string.IsNullOrWhiteSpace(commandProcessor))
            {
                commandProcessor = "cmd.exe";
            }

            var startInfo = new ProcessStartInfo
            {
                FileName = commandProcessor,
                WorkingDirectory = resolvedWorkingDirectory,
                UseShellExecute = true
            };
            startInfo.ArgumentList.Add("/D");
            startInfo.ArgumentList.Add(keepConsoleOpen ? "/K" : "/C");
            startInfo.ArgumentList.Add(command);

            Process.Start(startInfo);
        }
    }
}
