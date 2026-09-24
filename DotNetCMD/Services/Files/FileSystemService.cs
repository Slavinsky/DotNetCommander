using System;
using System.Collections.Generic;
using System.IO;

namespace DotNetCommander
{
    internal static class FileSystemService
    {
        public static bool DirectoryExists(string path) => !string.IsNullOrWhiteSpace(path) && Directory.Exists(path);

        public static bool FileExists(string path) => !string.IsNullOrWhiteSpace(path) && File.Exists(path);

        public static IEnumerable<string> EnumerateFiles(string path, string pattern, SearchOption searchOption)
        {
            return Directory.EnumerateFiles(path, pattern, searchOption);
        }

        public static IEnumerable<string> EnumerateDirectories(string path, string pattern, SearchOption searchOption)
        {
            return Directory.EnumerateDirectories(path, pattern, searchOption);
        }

        public static bool IsReparsePoint(string path)
        {
            try
            {
                return (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0;
            }
            catch (Exception)
            {
                return false;
            }
        }

        public static List<string> CollectDirectories(string rootPath)
        {
            var directories = new List<string>();
            Walk(rootPath, null, directories);
            return directories;
        }

        public static List<string> CollectFiles(string rootPath)
        {
            var files = new List<string>();
            Walk(rootPath, files, null);
            return files;
        }

        private static void Walk(string rootPath, List<string> files, List<string> directories)
        {
            if (IsReparsePoint(rootPath))
            {
                return;
            }

            var pending = new Stack<string>();
            pending.Push(rootPath);
            while (pending.Count > 0)
            {
                string current = pending.Pop();
                try
                {
                    files?.AddRange(Directory.EnumerateFiles(current));
                    foreach (string directory in Directory.EnumerateDirectories(current))
                    {
                        directories?.Add(directory);
                        if (!IsReparsePoint(directory))
                        {
                            pending.Push(directory);
                        }
                    }
                }
                catch (UnauthorizedAccessException ex)
                {
                    throw new DirectoryAccessDeniedException(current, ex);
                }
            }
        }

        public static void CreateDirectory(string path)
        {
            Directory.CreateDirectory(path);
        }

        public static void DeleteDirectory(string path, bool recursive)
        {
            Directory.Delete(path, recursive);
        }

        public static void DeleteFile(string path)
        {
            try
            {
                File.Delete(path);
            }
            catch (UnauthorizedAccessException)
            {
                ClearReadOnlyAttribute(path);
                File.Delete(path);
            }
        }

        public static void MoveDirectory(string sourcePath, string destinationPath)
        {
            Directory.Move(sourcePath, destinationPath);
        }

        public static void MoveFile(string sourcePath, string destinationPath)
        {
            ClearReadOnlyAttributeIfPresent(destinationPath);
            File.Move(sourcePath, destinationPath, true);
        }

        public static void CommitTemporaryFile(string temporaryPath, string destinationPath, bool overwriteExisting)
        {
            if (overwriteExisting)
            {
                ClearReadOnlyAttributeIfPresent(destinationPath);
            }

            File.Move(temporaryPath, destinationPath, overwriteExisting);
        }

        public static void CopyAttributes(string sourcePath, string destinationPath)
        {
            try
            {
                FileAttributes attributes = File.GetAttributes(sourcePath) & ~FileAttributes.ReparsePoint;
                File.SetAttributes(destinationPath, attributes);
            }
            catch (Exception ex)
            {
                LogService.LogException("FileSystemService.CopyAttributes", ex);
            }
        }

        private static void ClearReadOnlyAttributeIfPresent(string path)
        {
            if (File.Exists(path))
            {
                ClearReadOnlyAttribute(path);
            }
        }

        private static void ClearReadOnlyAttribute(string path)
        {
            FileAttributes attributes = File.GetAttributes(path);
            if ((attributes & FileAttributes.ReadOnly) != 0)
            {
                File.SetAttributes(path, attributes & ~FileAttributes.ReadOnly);
            }
        }

        public static FileStream OpenRead(string path)
        {
            return new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        }

        public static FileStream OpenWrite(string path, bool overwriteExisting)
        {
            if (overwriteExisting)
            {
                ClearReadOnlyAttributeIfPresent(path);
            }

            return new FileStream(
                path,
                overwriteExisting ? FileMode.Create : FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None);
        }

        public static FileStream OpenCreate(string path)
        {
            return new FileStream(path, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
        }

        public static long GetFileLength(string path)
        {
            return new FileInfo(path).Length;
        }

        public static string EnsureUniqueDirectoryPath(string parentPath, string defaultName)
        {
            string candidatePath;
            int suffix = 1;

            do
            {
                candidatePath = suffix == 1
                    ? Path.Combine(parentPath, defaultName)
                    : Path.Combine(parentPath, defaultName + " (" + suffix + ")");
                suffix++;
            }
            while (DirectoryExists(candidatePath));

            return candidatePath;
        }

        public static void EnsureParentDirectory(string path)
        {
            string directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory))
            {
                CreateDirectory(directory);
            }
        }

        public static string GetPathRoot(string path)
        {
            return Path.GetPathRoot(Path.GetFullPath(path)) ?? string.Empty;
        }
    }

    internal sealed class DirectoryAccessDeniedException : IOException
    {
        public DirectoryAccessDeniedException(string path, Exception innerException)
            : base("Access to the directory is denied: '" + path + "'.", innerException)
        {
            Path = path;
        }

        public string Path { get; }
    }
}
