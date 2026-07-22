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
            File.Delete(path);
        }

        public static void MoveDirectory(string sourcePath, string destinationPath)
        {
            Directory.Move(sourcePath, destinationPath);
        }

        public static void MoveFile(string sourcePath, string destinationPath)
        {
            File.Move(sourcePath, destinationPath, true);
        }

        public static FileStream OpenRead(string path)
        {
            return new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        }

        public static FileStream OpenWrite(string path, bool overwriteExisting)
        {
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
}
