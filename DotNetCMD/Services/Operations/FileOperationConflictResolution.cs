using System;

namespace DotNetCommander
{
    internal enum FileConflictResolutionAction
    {
        Overwrite,
        Skip,
        Rename
    }

    internal sealed class FileConflictResolution
    {
        public FileConflictResolution(FileConflictResolutionAction action, string destinationPath = null)
        {
            Action = action;
            DestinationPath = destinationPath;
        }

        public FileConflictResolutionAction Action { get; }
        public string DestinationPath { get; }
    }

    internal sealed class FileOperationConflict
    {
        public FileOperationConflict(string sourcePath, string destinationPath)
        {
            SourcePath = sourcePath ?? string.Empty;
            DestinationPath = destinationPath ?? string.Empty;
        }

        public string SourcePath { get; }
        public string DestinationPath { get; }
    }
}
