using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace DotNetCommander
{
    internal static class WinContextMenu
    {
        public const string CommandOpen = "shell.open";
        public const string CommandOpenWith = "shell.openWith";
        public const string CommandProperties = "shell.properties";

        public static void AppendShellItems(ContextMenuStrip menu, IReadOnlyList<string> selectedPaths, string currentPath)
        {
            if (menu == null || !OperatingSystem.IsWindows())
            {
                return;
            }

            List<string> targets = GetTargets(selectedPaths, currentPath);
            if (targets.Count == 0)
            {
                return;
            }

            if (menu.Items.Count > 0)
            {
                menu.Items.Add(new ToolStripSeparator());
            }

            menu.Items.Add(CreateMenuItem(Language.getString("open"), CommandOpen));

            if (targets.Count == 1 && CanOpenWith(targets[0]))
            {
                menu.Items.Add(CreateMenuItem(Language.getString("openWith"), CommandOpenWith));
            }

            if (targets.Count == 1)
            {
                menu.Items.Add(CreateMenuItem(Language.getString("properties"), CommandProperties));
            }
        }

        public static bool Execute(string command, IReadOnlyList<string> selectedPaths, string currentPath, IWin32Window owner)
        {
            List<string> targets = GetTargets(selectedPaths, currentPath);
            if (targets.Count == 0)
            {
                return false;
            }

            try
            {
                switch (command)
                {
                    case CommandOpen:
                        foreach (string path in targets)
                        {
                            Open(path);
                        }
                        return true;
                    case CommandOpenWith:
                        if (targets.Count == 1 && CanOpenWith(targets[0]))
                        {
                            OpenWith(targets[0]);
                            return true;
                        }
                        return false;
                    case CommandProperties:
                        if (targets.Count == 1)
                        {
                            ShowProperties(targets[0]);
                            return true;
                        }
                        return false;
                    default:
                        return false;
                }
            }
            catch (Exception ex)
            {
                LogService.LogException("WinContextMenu.Execute", ex);
                MessageBox.Show(
                    owner,
                    string.Format(Language.getString("shellActionFailedFormat"), command) + Environment.NewLine + ex.Message,
                    Language.getString("error"),
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
                return true;
            }
        }

        public static void Open(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return;
            }

            string workingDirectory = ResolveWorkingDirectory(path);
            ProcessStartInfo startInfo = new ProcessStartInfo
            {
                FileName = path,
                WorkingDirectory = workingDirectory,
                UseShellExecute = true,
                Verb = "open"
            };

            Process.Start(startInfo);
        }

        private static void OpenWith(string path)
        {
            OPENASINFO openAsInfo = new OPENASINFO
            {
                pcszFile = path,
                oaifInFlags = OpenAsFlags.OAIF_EXEC
            };

            int hr = SHOpenWithDialog(nint.Zero, ref openAsInfo);
            if (hr < 0)
            {
                Marshal.ThrowExceptionForHR(hr);
            }
        }

        public static bool CanRunInPersistentConsole(string path)
        {
            if (string.IsNullOrWhiteSpace(path) || Directory.Exists(path) || !File.Exists(path))
            {
                return false;
            }

            string extension = Path.GetExtension(path);
            return string.Equals(extension, ".bat", StringComparison.OrdinalIgnoreCase)
                || string.Equals(extension, ".cmd", StringComparison.OrdinalIgnoreCase)
                || string.Equals(extension, ".exe", StringComparison.OrdinalIgnoreCase)
                || string.Equals(extension, ".com", StringComparison.OrdinalIgnoreCase)
                || string.Equals(extension, ".ps1", StringComparison.OrdinalIgnoreCase);
        }

        public static bool RunInPersistentConsole(string path)
        {
            if (!CanRunInPersistentConsole(path))
            {
                return false;
            }

            string extension = Path.GetExtension(path);
            string workingDirectory = ResolveWorkingDirectory(path);
            ProcessStartInfo startInfo;

            if (string.Equals(extension, ".ps1", StringComparison.OrdinalIgnoreCase))
            {
                startInfo = new ProcessStartInfo
                {
                    FileName = "powershell.exe",
                    Arguments = $"-NoExit -ExecutionPolicy Bypass -File \"{path}\"",
                    WorkingDirectory = workingDirectory,
                    UseShellExecute = true
                };
            }
            else
            {
                string command = string.Equals(extension, ".bat", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(extension, ".cmd", StringComparison.OrdinalIgnoreCase)
                    ? $"call \"{path}\""
                    : $"\"{path}\"";

                startInfo = new ProcessStartInfo
                {
                    FileName = "cmd.exe",
                    Arguments = $"/k {command}",
                    WorkingDirectory = workingDirectory,
                    UseShellExecute = true
                };
            }

            Process.Start(startInfo);
            return true;
        }
        
        private static void ThrowShellExecuteError()
        {
            throw new InvalidOperationException(
                string.Format(
                    Language.getString("shellExecuteFailedFormat"),
                    Marshal.GetLastWin32Error()));
        }

        private static void ShowProperties(string path)
        {
            SHELLEXECUTEINFO executeInfo = new SHELLEXECUTEINFO
            {
                cbSize = (uint)Marshal.SizeOf<SHELLEXECUTEINFO>(),
                lpFile = path,
                lpVerb = "properties",
                lpDirectory = ResolveWorkingDirectory(path),
                nShow = 5,
                fMask = 0x0000000C
            };

            if (!ShellExecuteEx(ref executeInfo))
            {
                ThrowShellExecuteError();
            }
        }

        private static List<string> GetTargets(IReadOnlyList<string> selectedPaths, string currentPath)
        {
            if (selectedPaths != null)
            {
                List<string> selected = selectedPaths
                    .Where(path => !string.IsNullOrWhiteSpace(path))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();

                if (selected.Count > 0)
                {
                    return selected;
                }
            }

            return string.IsNullOrWhiteSpace(currentPath)
                ? new List<string>()
                : new List<string> { currentPath };
        }

        private static bool CanOpenWith(string path)
        {
            return File.Exists(path) && !Directory.Exists(path);
        }

        private static string ResolveWorkingDirectory(string path)
        {
            if (Directory.Exists(path))
            {
                return path;
            }

            if (File.Exists(path))
            {
                return Path.GetDirectoryName(path) ?? Environment.CurrentDirectory;
            }

            return Environment.CurrentDirectory;
        }

        private static ToolStripMenuItem CreateMenuItem(string text, string tag)
        {
            return new ToolStripMenuItem(text) { Tag = tag };
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct SHELLEXECUTEINFO
        {
            public uint cbSize;
            public uint fMask;
            public nint hwnd;
            [MarshalAs(UnmanagedType.LPWStr)] public string lpVerb;
            [MarshalAs(UnmanagedType.LPWStr)] public string lpFile;
            [MarshalAs(UnmanagedType.LPWStr)] public string lpParameters;
            [MarshalAs(UnmanagedType.LPWStr)] public string lpDirectory;
            public int nShow;
            public nint hInstApp;
            public nint lpIDList;
            [MarshalAs(UnmanagedType.LPWStr)] public string lpClass;
            public nint hkeyClass;
            public uint dwHotKey;
            public nint hIconOrMonitor;
            public nint hProcess;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct OPENASINFO
        {
            [MarshalAs(UnmanagedType.LPWStr)] public string pcszFile;
            [MarshalAs(UnmanagedType.LPWStr)] public string pcszClass;
            public OpenAsFlags oaifInFlags;
        }

        [Flags]
        private enum OpenAsFlags : uint
        {
            OAIF_EXEC = 0x00000004
        }

        [DllImport("shell32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool ShellExecuteEx(ref SHELLEXECUTEINFO lpExecInfo);

        [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
        private static extern int SHOpenWithDialog(nint hwndParent, ref OPENASINFO poainfo);
    }
}
