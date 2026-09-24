using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using DotNetCommander;
using Xunit;

namespace DotNetCommander.Tests
{
    public sealed class FileOperationServiceTests
    {
        [Fact]
        public async Task CopySingleFile_ToExactTargetPath_CreatesFile()
        {
            using var directory = new TemporaryDirectory();
            string source = directory.GetPath("source.txt");
            string target = directory.GetPath("renamed.txt");
            File.WriteAllText(source, "payload");

            FileOperationResult result = await FileOperationService.ExecuteCopyOrMoveAsync(
                new[] { source }, target, FormCopy.Type.Copy, false, null, null, null, CancellationToken.None);

            Assert.Equal(FileOperationRunResult.Completed, result.RunResult);
            Assert.Equal(1, result.CompletedEntries);
            Assert.Equal("payload", File.ReadAllText(target));
            Assert.True(File.Exists(source));
        }

        [Fact]
        public async Task CopySingleFile_WhenDestinationIsDirectory_WritesFileIntoDirectory()
        {
            using var directory = new TemporaryDirectory();
            string source = directory.GetPath("report.txt");
            string destinationDirectory = directory.GetPath("inbox");
            Directory.CreateDirectory(destinationDirectory);
            File.WriteAllText(source, "payload");

            FileOperationResult result = await FileOperationService.ExecuteCopyOrMoveAsync(
                new[] { source }, destinationDirectory, FormCopy.Type.Copy, false, null, null, null, CancellationToken.None);

            Assert.Equal(FileOperationRunResult.Completed, result.RunResult);
            Assert.Equal("payload", File.ReadAllText(Path.Combine(destinationDirectory, "report.txt")));
        }

        [Fact]
        public async Task CopySingleDirectory_ToExistingTargetDirectory_MergesContents()
        {
            using var directory = new TemporaryDirectory();
            string source = directory.GetPath("MyDir");
            Directory.CreateDirectory(source);
            File.WriteAllText(Path.Combine(source, "a.txt"), "a");
            string target = directory.GetPath(Path.Combine("target", "MyDir"));
            Directory.CreateDirectory(target);
            File.WriteAllText(Path.Combine(target, "existing.txt"), "keep");

            FileOperationResult result = await FileOperationService.ExecuteCopyOrMoveAsync(
                new[] { source }, target, FormCopy.Type.Copy, false, null, null, null, CancellationToken.None);

            Assert.Equal(FileOperationRunResult.Completed, result.RunResult);
            Assert.Equal("a", File.ReadAllText(Path.Combine(target, "a.txt")));
            Assert.Equal("keep", File.ReadAllText(Path.Combine(target, "existing.txt")));
        }

        [Fact]
        public async Task CopyTwoFiles_PreservesEachFileName()
        {
            using var directory = new TemporaryDirectory();
            string first = directory.GetPath("first.txt");
            string second = directory.GetPath("second.txt");
            string destination = directory.GetPath("out");
            Directory.CreateDirectory(destination);
            File.WriteAllText(first, "1");
            File.WriteAllText(second, "2");

            FileOperationResult result = await FileOperationService.ExecuteCopyOrMoveAsync(
                new[] { first, second }, destination, FormCopy.Type.Copy, false, null, null, null, CancellationToken.None);

            Assert.Equal(FileOperationRunResult.Completed, result.RunResult);
            Assert.Equal(2, result.CompletedEntries);
            Assert.Equal("1", File.ReadAllText(Path.Combine(destination, "first.txt")));
            Assert.Equal("2", File.ReadAllText(Path.Combine(destination, "second.txt")));
        }

        [Fact]
        public async Task CopyDirectory_CreatesNestedStructure()
        {
            using var directory = new TemporaryDirectory();
            string source = directory.GetPath("tree");
            Directory.CreateDirectory(Path.Combine(source, "sub"));
            File.WriteAllText(Path.Combine(source, "root.txt"), "r");
            File.WriteAllText(Path.Combine(source, "sub", "nested.txt"), "n");
            string destination = directory.GetPath("out");
            Directory.CreateDirectory(destination);

            FileOperationResult result = await FileOperationService.ExecuteCopyOrMoveAsync(
                new[] { source }, Path.Combine(destination, "tree"), FormCopy.Type.Copy, false, null, null, null, CancellationToken.None);

            Assert.Equal(FileOperationRunResult.Completed, result.RunResult);
            Assert.Equal("r", File.ReadAllText(Path.Combine(destination, "tree", "root.txt")));
            Assert.Equal("n", File.ReadAllText(Path.Combine(destination, "tree", "sub", "nested.txt")));
            Assert.True(Directory.Exists(source));
        }

        [Fact]
        public async Task Copy_WithSkipResolution_KeepsExistingTargetUnchanged()
        {
            using var directory = new TemporaryDirectory();
            string source = directory.GetPath("file.txt");
            string destination = directory.GetPath("out");
            Directory.CreateDirectory(destination);
            string existingTarget = Path.Combine(destination, "file.txt");
            File.WriteAllText(source, "new");
            File.WriteAllText(existingTarget, "old");
            var resolutions = new Dictionary<string, FileConflictResolution>(StringComparer.OrdinalIgnoreCase)
            {
                [existingTarget] = new FileConflictResolution(FileConflictResolutionAction.Skip)
            };

            FileOperationResult result = await FileOperationService.ExecuteCopyOrMoveAsync(
                new[] { source }, existingTarget, FormCopy.Type.Copy, false, resolutions, null, null, CancellationToken.None);

            Assert.Equal(FileOperationRunResult.Completed, result.RunResult);
            Assert.Equal(1, result.SkippedEntries);
            Assert.Equal("old", File.ReadAllText(existingTarget));
        }

        [Fact]
        public async Task Copy_WithOverwriteResolution_ReplacesContent()
        {
            using var directory = new TemporaryDirectory();
            string source = directory.GetPath("file.txt");
            string destination = directory.GetPath("out");
            Directory.CreateDirectory(destination);
            string existingTarget = Path.Combine(destination, "file.txt");
            File.WriteAllText(source, "new");
            File.WriteAllText(existingTarget, "old");
            var resolutions = new Dictionary<string, FileConflictResolution>(StringComparer.OrdinalIgnoreCase)
            {
                [existingTarget] = new FileConflictResolution(FileConflictResolutionAction.Overwrite)
            };

            FileOperationResult result = await FileOperationService.ExecuteCopyOrMoveAsync(
                new[] { source }, existingTarget, FormCopy.Type.Copy, false, resolutions, null, null, CancellationToken.None);

            Assert.Equal(FileOperationRunResult.Completed, result.RunResult);
            Assert.Equal("new", File.ReadAllText(existingTarget));
        }

        [Fact]
        public async Task Copy_WithRenameResolution_WritesToNewName()
        {
            using var directory = new TemporaryDirectory();
            string source = directory.GetPath("file.txt");
            string destination = directory.GetPath("out");
            Directory.CreateDirectory(destination);
            string existingTarget = Path.Combine(destination, "file.txt");
            string renamedTarget = Path.Combine(destination, "file (2).txt");
            File.WriteAllText(source, "new");
            File.WriteAllText(existingTarget, "old");
            var resolutions = new Dictionary<string, FileConflictResolution>(StringComparer.OrdinalIgnoreCase)
            {
                [existingTarget] = new FileConflictResolution(FileConflictResolutionAction.Rename, renamedTarget)
            };

            FileOperationResult result = await FileOperationService.ExecuteCopyOrMoveAsync(
                new[] { source }, existingTarget, FormCopy.Type.Copy, false, resolutions, null, null, CancellationToken.None);

            Assert.Equal(FileOperationRunResult.Completed, result.RunResult);
            Assert.Equal("old", File.ReadAllText(existingTarget));
            Assert.Equal("new", File.ReadAllText(renamedTarget));
        }

        [Fact]
        public async Task Copy_ExistingTargetWithoutOverwrite_FaultsWithoutTouchingTarget()
        {
            using var directory = new TemporaryDirectory();
            string source = directory.GetPath("file.txt");
            string destination = directory.GetPath("out");
            Directory.CreateDirectory(destination);
            string existingTarget = Path.Combine(destination, "file.txt");
            File.WriteAllText(source, "new");
            File.WriteAllText(existingTarget, "old");

            await Assert.ThrowsAsync<IOException>(() => FileOperationService.ExecuteCopyOrMoveAsync(
                new[] { source }, existingTarget, FormCopy.Type.Copy, false, null, null, null, CancellationToken.None));

            Assert.Equal("old", File.ReadAllText(existingTarget));
            Assert.Equal("new", File.ReadAllText(source));
        }

        [Fact]
        public async Task Move_SameVolumeFile_MovesFile()
        {
            using var directory = new TemporaryDirectory();
            string source = directory.GetPath("move.txt");
            string target = directory.GetPath("moved.txt");
            File.WriteAllText(source, "payload");

            FileOperationResult result = await FileOperationService.ExecuteCopyOrMoveAsync(
                new[] { source }, target, FormCopy.Type.Move, false, null, null, null, CancellationToken.None);

            Assert.Equal(FileOperationRunResult.Completed, result.RunResult);
            Assert.False(File.Exists(source));
            Assert.Equal("payload", File.ReadAllText(target));
        }

        [Fact]
        public async Task MoveDirectory_SameVolumeToNewTarget_MovesWholeTree()
        {
            using var directory = new TemporaryDirectory();
            string source = directory.GetPath("folder");
            Directory.CreateDirectory(Path.Combine(source, "sub"));
            File.WriteAllText(Path.Combine(source, "sub", "deep.txt"), "deep");
            string target = directory.GetPath("folder-moved");

            FileOperationResult result = await FileOperationService.ExecuteCopyOrMoveAsync(
                new[] { source }, target, FormCopy.Type.Move, false, null, null, null, CancellationToken.None);

            Assert.Equal(FileOperationRunResult.Completed, result.RunResult);
            Assert.False(Directory.Exists(source));
            Assert.Equal("deep", File.ReadAllText(Path.Combine(target, "sub", "deep.txt")));
        }

        [Fact]
        public async Task Delete_File_RemovesIt()
        {
            using var directory = new TemporaryDirectory();
            string source = directory.GetPath("gone.txt");
            File.WriteAllText(source, "x");

            FileOperationResult result = await FileOperationService.ExecuteDeleteAsync(
                new[] { source }, null, null, CancellationToken.None);

            Assert.Equal(FileOperationRunResult.Completed, result.RunResult);
            Assert.False(File.Exists(source));
        }

        [Fact]
        public async Task Delete_Directory_RemovesNestedTree()
        {
            using var directory = new TemporaryDirectory();
            string root = directory.GetPath("tree");
            Directory.CreateDirectory(Path.Combine(root, "sub"));
            File.WriteAllText(Path.Combine(root, "a.txt"), "a");
            File.WriteAllText(Path.Combine(root, "sub", "b.txt"), "b");

            FileOperationResult result = await FileOperationService.ExecuteDeleteAsync(
                new[] { root }, null, null, CancellationToken.None);

            Assert.Equal(FileOperationRunResult.Completed, result.RunResult);
            Assert.False(Directory.Exists(root));
        }

        [Fact]
        public void CollectConflicts_ReportsOnlyExistingTargets()
        {
            using var directory = new TemporaryDirectory();
            string first = directory.GetPath("first.txt");
            string second = directory.GetPath("second.txt");
            string destination = directory.GetPath("out");
            Directory.CreateDirectory(destination);
            File.WriteAllText(first, "1");
            File.WriteAllText(second, "2");
            File.WriteAllText(Path.Combine(destination, "first.txt"), "existing");

            IReadOnlyList<FileOperationConflict> conflicts = FileOperationService.CollectCopyOrMoveConflicts(
                new[] { first, second }, destination, FormCopy.Type.Copy, CancellationToken.None);

            FileOperationConflict conflict = Assert.Single(conflicts);
            Assert.Equal(first, conflict.SourcePath);
            Assert.Equal(Path.Combine(destination, "first.txt"), conflict.DestinationPath);
        }

        [Fact]
        public async Task FailureHandler_LockedDestination_ReportsFailureAndKeepsTarget()
        {
            using var directory = new TemporaryDirectory();
            string source = directory.GetPath("file.txt");
            string destination = directory.GetPath("out");
            Directory.CreateDirectory(destination);
            string existingTarget = Path.Combine(destination, "file.txt");
            File.WriteAllText(source, "new");
            File.WriteAllText(existingTarget, "old");
            int handlerCalls = 0;

            using (var lockedTarget = new FileStream(existingTarget, FileMode.Open, FileAccess.Read, FileShare.None))
            {
                FileOperationResult result = await FileOperationService.ExecuteCopyOrMoveAsync(
                    new[] { source },
                    existingTarget,
                    FormCopy.Type.Copy,
                    true,
                    null,
                    null,
                    failure =>
                    {
                        handlerCalls++;
                        return FileOperationFailureAction.Skip;
                    },
                    CancellationToken.None);

                Assert.Equal(FileOperationRunResult.CompletedWithErrors, result.RunResult);
                Assert.Equal(1, result.FailedEntries);
                Assert.Single(result.Failures);
            }

            Assert.Equal(1, handlerCalls);
            Assert.Equal("old", File.ReadAllText(existingTarget));
            Assert.Equal("new", File.ReadAllText(source));
        }

        [Fact]
        public async Task Cancellation_OnFirstRunningReport_StopsOperation()
        {
            using var directory = new TemporaryDirectory();
            string source = directory.GetPath("file.txt");
            string destination = directory.GetPath("out");
            Directory.CreateDirectory(destination);
            File.WriteAllText(source, "payload");
            using var cancellation = new CancellationTokenSource();
            var progress = new CallbackProgress(info =>
            {
                if (info.Phase == FileOperationPhase.Running)
                    cancellation.Cancel();
            });

            FileOperationResult result = await FileOperationService.ExecuteCopyOrMoveAsync(
                new[] { source },
                Path.Combine(destination, "file.txt"),
                FormCopy.Type.Copy,
                false,
                null,
                progress,
                null,
                cancellation.Token);

            Assert.Equal(FileOperationRunResult.Cancelled, result.RunResult);
            Assert.False(File.Exists(Path.Combine(destination, "file.txt")));
        }

        [Fact]
        public async Task CopyDirectory_UnicodeNames_Completes()
        {
            using var directory = new TemporaryDirectory();
            string source = directory.GetPath("src");
            string target = directory.GetPath("dst");
            CreateUnicodeTree(source);

            FileOperationResult result = await FileOperationService.ExecuteCopyOrMoveAsync(
                new[] { source }, target, FormCopy.Type.Copy, false, null, null, null, CancellationToken.None);

            Assert.Equal(FileOperationRunResult.Completed, result.RunResult);
            Assert.Equal("привіт", File.ReadAllText(Path.Combine(target, "Копія файлу 🚀.txt")));
            Assert.Equal("こんにちは", File.ReadAllText(Path.Combine(target, "日本語ファイル.txt")));
            Assert.Equal("données", File.ReadAllText(Path.Combine(target, "Ünïcödé папка", "contenu — données.txt")));
        }

        [Fact]
        public async Task DeleteDirectory_UnicodeNames_Completes()
        {
            using var directory = new TemporaryDirectory();
            string source = directory.GetPath("src");
            CreateUnicodeTree(source);

            FileOperationResult result = await FileOperationService.ExecuteDeleteAsync(
                new[] { source }, null, null, CancellationToken.None);

            Assert.Equal(FileOperationRunResult.Completed, result.RunResult);
            Assert.False(Directory.Exists(source));
        }

        [Fact]
        public async Task Copy_PreservesHiddenAndSystemAttributes()
        {
            using var directory = new TemporaryDirectory();
            string source = directory.GetPath("secret.txt");
            string target = directory.GetPath("secret-copy.txt");
            File.WriteAllText(source, "hidden payload");
            File.SetAttributes(source, FileAttributes.Hidden | FileAttributes.System);

            FileOperationResult result = await FileOperationService.ExecuteCopyOrMoveAsync(
                new[] { source }, target, FormCopy.Type.Copy, false, null, null, null, CancellationToken.None);

            Assert.Equal(FileOperationRunResult.Completed, result.RunResult);
            FileAttributes attributes = File.GetAttributes(target);
            Assert.True((attributes & FileAttributes.Hidden) != 0);
            Assert.True((attributes & FileAttributes.System) != 0);
            Assert.Equal("hidden payload", File.ReadAllText(target));
        }

        [Fact]
        public async Task Copy_PreservesReadOnlyAttribute()
        {
            using var directory = new TemporaryDirectory();
            string source = directory.GetPath("locked.txt");
            string target = directory.GetPath("locked-copy.txt");
            File.WriteAllText(source, "ro payload");
            File.SetAttributes(source, FileAttributes.ReadOnly);

            FileOperationResult result = await FileOperationService.ExecuteCopyOrMoveAsync(
                new[] { source }, target, FormCopy.Type.Copy, false, null, null, null, CancellationToken.None);

            Assert.Equal(FileOperationRunResult.Completed, result.RunResult);
            Assert.True((File.GetAttributes(target) & FileAttributes.ReadOnly) != 0);
            Assert.Equal("ro payload", File.ReadAllText(target));
        }

        [Fact]
        public async Task Delete_ReadOnlyFile_Succeeds()
        {
            using var directory = new TemporaryDirectory();
            string source = directory.GetPath("readonly.txt");
            File.WriteAllText(source, "x");
            File.SetAttributes(source, FileAttributes.ReadOnly);

            FileOperationResult result = await FileOperationService.ExecuteDeleteAsync(
                new[] { source }, null, null, CancellationToken.None);

            Assert.Equal(FileOperationRunResult.Completed, result.RunResult);
            Assert.False(File.Exists(source));
        }

        [Fact]
        public async Task Copy_OverReadOnlyTarget_Overwrites()
        {
            using var directory = new TemporaryDirectory();
            string source = directory.GetPath("file.txt");
            string target = directory.GetPath("existing.txt");
            File.WriteAllText(source, "new");
            File.WriteAllText(target, "old");
            File.SetAttributes(target, FileAttributes.ReadOnly);

            FileOperationResult result = await FileOperationService.ExecuteCopyOrMoveAsync(
                new[] { source }, target, FormCopy.Type.Copy, true, null, null, null, CancellationToken.None);

            Assert.Equal(FileOperationRunResult.Completed, result.RunResult);
            Assert.Equal("new", File.ReadAllText(target));
        }

        [Fact]
        public async Task CopyDirectory_WithJunction_SkipsLinkAndDoesNotCopyTargetContents()
        {
            using var directory = new TemporaryDirectory();
            string source = directory.GetPath("src");
            string target = directory.GetPath("dst");
            string sibling = Directory.CreateDirectory(directory.GetPath("sibling")).FullName;
            Directory.CreateDirectory(source);
            File.WriteAllText(Path.Combine(source, "root.txt"), "r");
            File.WriteAllText(Path.Combine(sibling, "inside.txt"), "s");
            CreateJunction(Path.Combine(source, "link"), sibling);

            FileOperationResult result = await FileOperationService.ExecuteCopyOrMoveAsync(
                new[] { source }, target, FormCopy.Type.Copy, false, null, null, null, CancellationToken.None);

            Assert.Equal(FileOperationRunResult.Completed, result.RunResult);
            Assert.Equal("r", File.ReadAllText(Path.Combine(target, "root.txt")));
            Assert.False(Directory.Exists(Path.Combine(target, "link")));
            Assert.Empty(Directory.GetFiles(target, "inside.txt", SearchOption.AllDirectories));
        }

        [Fact]
        public async Task DeleteDirectory_WithJunction_RemovesLinkWithoutTouchingTarget()
        {
            using var directory = new TemporaryDirectory();
            string source = directory.GetPath("src");
            string sibling = Directory.CreateDirectory(directory.GetPath("sibling")).FullName;
            Directory.CreateDirectory(source);
            File.WriteAllText(Path.Combine(source, "root.txt"), "r");
            File.WriteAllText(Path.Combine(sibling, "inside.txt"), "s");
            CreateJunction(Path.Combine(source, "link"), sibling);

            FileOperationResult result = await FileOperationService.ExecuteDeleteAsync(
                new[] { source }, null, null, CancellationToken.None);

            Assert.Equal(FileOperationRunResult.Completed, result.RunResult);
            Assert.False(Directory.Exists(source));
            Assert.True(File.Exists(Path.Combine(sibling, "inside.txt")));
            Assert.Equal("s", File.ReadAllText(Path.Combine(sibling, "inside.txt")));
        }

        [Fact]
        public async Task MoveDirectory_IntoExistingTarget_RemovesJunctionLink_TargetIntact()
        {
            using var directory = new TemporaryDirectory();
            string source = directory.GetPath("src");
            string target = directory.GetPath("dst");
            string sibling = Directory.CreateDirectory(directory.GetPath("sibling")).FullName;
            Directory.CreateDirectory(source);
            Directory.CreateDirectory(target);
            File.WriteAllText(Path.Combine(source, "root.txt"), "r");
            File.WriteAllText(Path.Combine(sibling, "inside.txt"), "s");
            CreateJunction(Path.Combine(source, "link"), sibling);

            FileOperationResult result = await FileOperationService.ExecuteCopyOrMoveAsync(
                new[] { source }, target, FormCopy.Type.Move, false, null, null, null, CancellationToken.None);

            Assert.Equal(FileOperationRunResult.Completed, result.RunResult);
            Assert.Equal("r", File.ReadAllText(Path.Combine(target, "root.txt")));
            Assert.False(Directory.Exists(source));
            Assert.False(Directory.Exists(Path.Combine(target, "link")));
            Assert.Equal("s", File.ReadAllText(Path.Combine(sibling, "inside.txt")));
        }

        private static void CreateUnicodeTree(string source)
        {
            Directory.CreateDirectory(Path.Combine(source, "Ünïcödé папка"));
            File.WriteAllText(Path.Combine(source, "Копія файлу 🚀.txt"), "привіт");
            File.WriteAllText(Path.Combine(source, "日本語ファイル.txt"), "こんにちは");
            File.WriteAllText(Path.Combine(source, "Ünïcödé папка", "contenu — données.txt"), "données");
        }

        private static void CreateJunction(string path, string target)
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = "/C mklink /J \"" + path + "\" \"" + target + "\"",
                CreateNoWindow = true,
                UseShellExecute = false
            };
            using Process process = Process.Start(startInfo);
            process.WaitForExit(10000);
            Assert.True(Directory.Exists(path), "junction was not created: " + path);
        }

        [Fact]
        public async Task CopyDirectory_WithAccessDeniedSubdirectory_ReportsStructuredFailure_NothingCopied()
        {
            using var directory = new TemporaryDirectory();
            string source = directory.GetPath("source");
            string target = directory.GetPath("target");
            string denied = Directory.CreateDirectory(Path.Combine(source, "denied")).FullName;
            File.WriteAllText(Path.Combine(source, "root.txt"), "r");
            File.WriteAllText(Path.Combine(denied, "inner.txt"), "i");

            DenyAccess(denied);
            try
            {
                FileOperationResult result = await FileOperationService.ExecuteCopyOrMoveAsync(
                    new[] { source }, target, FormCopy.Type.Copy, false, null, null, null, CancellationToken.None);

                Assert.Equal(FileOperationRunResult.CompletedWithErrors, result.RunResult);
                Assert.Equal(1, result.FailedEntries);
                FileOperationFailure failure = Assert.Single(result.Failures);
                Assert.IsType<DirectoryAccessDeniedException>(failure.Exception);
                Assert.Equal(denied, failure.SourcePath);
                Assert.IsAssignableFrom<UnauthorizedAccessException>(failure.Exception.InnerException);
                Assert.False(Directory.Exists(target));
            }
            finally
            {
                RestoreAccess(denied);
            }

            Assert.Equal("r", File.ReadAllText(Path.Combine(source, "root.txt")));
            Assert.Equal("i", File.ReadAllText(Path.Combine(denied, "inner.txt")));
        }

        [Fact]
        public async Task DeleteDirectory_WithAccessDeniedSubdirectory_ReportsStructuredFailure_NothingDeleted()
        {
            using var directory = new TemporaryDirectory();
            string source = directory.GetPath("source");
            string denied = Directory.CreateDirectory(Path.Combine(source, "denied")).FullName;
            File.WriteAllText(Path.Combine(source, "root.txt"), "r");
            File.WriteAllText(Path.Combine(denied, "inner.txt"), "i");

            DenyAccess(denied);
            try
            {
                FileOperationResult result = await FileOperationService.ExecuteDeleteAsync(
                    new[] { source }, null, null, CancellationToken.None);

                Assert.Equal(FileOperationRunResult.CompletedWithErrors, result.RunResult);
                Assert.Equal(1, result.FailedEntries);
                FileOperationFailure failure = Assert.Single(result.Failures);
                Assert.IsType<DirectoryAccessDeniedException>(failure.Exception);
            }
            finally
            {
                RestoreAccess(denied);
            }

            Assert.True(File.Exists(Path.Combine(source, "root.txt")));
            Assert.True(File.Exists(Path.Combine(denied, "inner.txt")));
        }

        [Fact]
        public void CollectCopyOrMoveConflicts_WithAccessDeniedSubdirectory_DoesNotThrow()
        {
            using var directory = new TemporaryDirectory();
            string source = directory.GetPath("source");
            string destination = directory.GetPath("destination");
            string denied = Directory.CreateDirectory(Path.Combine(source, "denied")).FullName;
            File.WriteAllText(Path.Combine(source, "root.txt"), "r");
            File.WriteAllText(Path.Combine(denied, "inner.txt"), "i");

            DenyAccess(denied);
            try
            {
                IReadOnlyList<FileOperationConflict> conflicts = FileOperationService.CollectCopyOrMoveConflicts(
                    new[] { source }, destination, FormCopy.Type.Copy, CancellationToken.None);
                Assert.NotNull(conflicts);
            }
            finally
            {
                RestoreAccess(denied);
            }
        }

        [Fact]
        public async Task CopyDirectory_WithAccessDeniedFile_SkipsFileViaFailureHandler_CopiesRest()
        {
            using var directory = new TemporaryDirectory();
            string source = directory.GetPath("source");
            string target = directory.GetPath("target");
            Directory.CreateDirectory(source);
            string deniedFile = Path.Combine(source, "denied.txt");
            File.WriteAllText(deniedFile, "secret");
            File.WriteAllText(Path.Combine(source, "ok.txt"), "plain");

            DenyAccess(deniedFile);
            try
            {
                int handlerCalls = 0;
                FileOperationResult result = await FileOperationService.ExecuteCopyOrMoveAsync(
                    new[] { source }, target, FormCopy.Type.Copy, false, null, null,
                    failure =>
                    {
                        handlerCalls++;
                        return FileOperationFailureAction.Skip;
                    },
                    CancellationToken.None);

                Assert.Equal(FileOperationRunResult.CompletedWithErrors, result.RunResult);
                Assert.Equal(1, handlerCalls);
                Assert.Equal(1, result.FailedEntries);
                Assert.Single(result.Failures);
                Assert.IsAssignableFrom<UnauthorizedAccessException>(result.Failures[0].Exception);
                Assert.Equal("plain", File.ReadAllText(Path.Combine(target, "ok.txt")));
                Assert.False(File.Exists(Path.Combine(target, "denied.txt")));
            }
            finally
            {
                RestoreAccess(deniedFile);
            }

            Assert.Equal("secret", File.ReadAllText(deniedFile));
        }

        [Fact]
        public async Task CopyDirectory_WithAccessDeniedRoot_ReportsStructuredFailure_NothingCopied()
        {
            using var directory = new TemporaryDirectory();
            string source = directory.GetPath("source");
            string target = directory.GetPath("target");
            Directory.CreateDirectory(source);
            File.WriteAllText(Path.Combine(source, "root.txt"), "r");

            DenyAccess(source);
            try
            {
                FileOperationResult result = await FileOperationService.ExecuteCopyOrMoveAsync(
                    new[] { source }, target, FormCopy.Type.Copy, false, null, null, null, CancellationToken.None);

                Assert.Equal(FileOperationRunResult.CompletedWithErrors, result.RunResult);
                Assert.Equal(1, result.FailedEntries);
                Assert.IsType<DirectoryAccessDeniedException>(Assert.Single(result.Failures).Exception);
                Assert.False(Directory.Exists(target));
            }
            finally
            {
                RestoreAccess(source);
            }

            Assert.True(File.Exists(Path.Combine(source, "root.txt")));
        }

        private static string TestAccount => Environment.UserDomainName + "\\" + Environment.UserName;

        private static void DenyAccess(string path)
        {
            string arguments = path.EndsWith(".txt", StringComparison.OrdinalIgnoreCase)
                ? "\"" + path + "\" /deny \"" + TestAccount + "\":F"
                : "\"" + path + "\" /deny \"" + TestAccount + "\":(OI)(CI)F";
            RunIcacls(arguments);
        }

        private static void RestoreAccess(string path)
        {
            RunIcacls("\"" + path + "\" /remove:d \"" + TestAccount + "\"");
        }

        private static void RunIcacls(string arguments)
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = "icacls.exe",
                Arguments = arguments,
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
            using Process process = Process.Start(startInfo);
            process.StandardOutput.ReadToEnd();
            process.StandardError.ReadToEnd();
            process.WaitForExit(15000);
            Assert.Equal(0, process.ExitCode);
        }

        private sealed class CallbackProgress : IProgress<FileOperationProgressInfo>
        {
            private readonly Action<FileOperationProgressInfo> callback;

            public CallbackProgress(Action<FileOperationProgressInfo> callback)
            {
                this.callback = callback;
            }

            public void Report(FileOperationProgressInfo value) => callback(value);
        }
    }
}
