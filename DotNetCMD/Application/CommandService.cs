using System;
using System.Collections.Specialized;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace DotNetCommander
{
    internal sealed class CommandService
    {
        private const long PreviewMaxBytes = 9 * 1024 * 1024;

        public void CopySelectionToClipboard(FileBrowser source)
        {
            string[] selectedFiles = source?.selectedFiles;
            if (selectedFiles == null || selectedFiles.Length == 0)
            {
                return;
            }

            StringCollection paths = new StringCollection();
            foreach (string path in selectedFiles)
            {
                paths.Add(path);
            }

            Clipboard.SetFileDropList(paths);
        }

        public void PasteFromClipboard(FileBrowser destination, Action<int> onComplete, IWin32Window owner)
        {
            if (destination?.IsVirtualMode == true)
            {
                ShowArchiveReadOnly(owner);
                return;
            }

            if (destination == null || !Clipboard.ContainsFileDropList())
            {
                return;
            }

            StringCollection paths = Clipboard.GetFileDropList();
            FormCopy copyWindow = new FormCopy(paths, destination.CurrentPath);
            copyWindow.ActionComplete += result => onComplete?.Invoke(result);
            copyWindow.ShowDialog(owner);
        }

        public void RenameSelection(FileBrowser source)
        {
            if (source?.IsVirtualMode == true)
            {
                ShowArchiveReadOnly(source.FindForm());
                return;
            }

            source?.RenameSelectedItem();
        }

        public async void ViewSelection(FileBrowser source, IWin32Window owner)
        {
            try
            {
                string filePath = source?.IsArchiveMode == true
                    ? await source.MaterializeSelectedArchiveFileAsync()
                    : GetFirstSelectedFile(source);
                if (string.IsNullOrWhiteSpace(filePath))
                {
                    return;
                }

                FileContentKind kind = FileTypeClassifier.Classify(filePath);

                if (kind == FileContentKind.Csv)
                {
                    View.CsvView csvView = new View.CsvView();
                    csvView.LoadFile(filePath);
                    ShowOwned(csvView, owner);
                    return;
                }

                if (FileSystemService.FileExists(filePath) && FileSystemService.GetFileLength(filePath) > PreviewMaxBytes)
                {
                    MessageBox.Show(owner, Language.getString("fileTooLargePreview"), Language.getString("Info"), MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                if (kind == FileContentKind.Image)
                {
                    View.ImageView formView = new View.ImageView();
                    formView.OpenFile(filePath);
                    ShowOwned(formView, owner);
                    return;
                }

                if (kind == FileContentKind.Markdown)
                {
                    View.RtfEdit preview = new View.RtfEdit();
                    preview.LoadFile(filePath, true);
                    ShowOwned(preview, owner);
                    return;
                }

                if (kind == FileContentKind.RichText)
                {
                    View.RtfEdit preview = new View.RtfEdit();
                    preview.LoadFile(filePath, true);
                    ShowOwned(preview, owner);
                    return;
                }

                if (kind == FileContentKind.Text)
                {
                    View.TextEdit preview = new View.TextEdit();
                    preview.LoadFile(filePath, true);
                    ShowOwned(preview, owner);
                    return;
                }

                MessageBox.Show(owner, Language.getString("previewNotSupported"), Language.getString("Info"), MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                LogService.LogException("CommandService.ViewSelection", ex);
                MessageBox.Show(owner, ex.Message, Language.getString("error"), MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        public void EditSelection(FileBrowser source, IWin32Window owner)
        {
            if (source?.IsVirtualMode == true)
            {
                ShowArchiveReadOnly(owner);
                return;
            }

            string filePath = GetFirstSelectedFile(source);
            if (string.IsNullOrWhiteSpace(filePath))
            {
                return;
            }

            try
            {
                if (!TryOpenEditor(filePath, owner, false))
                {
                    MessageBox.Show(owner, Language.getString("editSelectionNotSupported"), Language.getString("Info"), MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }
            catch (Exception ex)
            {
                LogService.LogException("CommandService.EditSelection", ex);
                MessageBox.Show(owner, ex.Message, Language.getString("error"), MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        public void CopySelection(FileBrowser source, FileBrowser destination, Action<int> onComplete, IWin32Window owner)
        {
            if (source?.IsArchiveMode == true)
            {
                ExtractArchiveSelection(source, destination, onComplete, owner);
                return;
            }
            if (source?.IsVirtualMode == true || destination?.IsVirtualMode == true)
            {
                ShowArchiveReadOnly(owner);
                return;
            }

            ShowCopyDialog(source, destination, FormCopy.Type.Copy, onComplete, owner);
        }

        public void CopySelectionInPlace(FileBrowser source, Action<int> onComplete, IWin32Window owner)
        {
            if (source?.IsVirtualMode == true)
            {
                ShowArchiveReadOnly(owner);
                return;
            }

            if (source == null)
            {
                return;
            }

            ShowCopyDialog(source, source, FormCopy.Type.Copy, onComplete, owner);
        }

        public void MoveSelection(FileBrowser source, FileBrowser destination, Action<int> onComplete, IWin32Window owner)
        {
            if (source?.IsVirtualMode == true || destination?.IsVirtualMode == true)
            {
                ShowArchiveReadOnly(owner);
                return;
            }

            ShowCopyDialog(source, destination, FormCopy.Type.Move, onComplete, owner);
        }

        public void CreateFolder(FileBrowser source, IWin32Window owner)
        {
            if (source?.IsVirtualMode == true)
            {
                ShowArchiveReadOnly(owner);
                return;
            }

            if (source == null || string.IsNullOrWhiteSpace(source.CurrentPath))
            {
                return;
            }

            try
            {
                using FormNewFolder dialog = new FormNewFolder(source.CurrentPath, Language.getString("newFolder"));
                if (dialog.ShowDialog(owner) != DialogResult.OK)
                {
                    return;
                }

                string targetPath = dialog.TargetPath;
                if (string.IsNullOrWhiteSpace(targetPath))
                {
                    return;
                }

                FileSystemService.CreateDirectory(targetPath);
                if (dialog.OpenCreatedFolder)
                {
                    source.browseTo(targetPath);
                    return;
                }

                source.Refrech();
                string firstSegment = GetFirstRelativePathSegment(dialog.RelativePath);
                if (!string.IsNullOrWhiteSpace(firstSegment))
                {
                    source.selectFile(firstSegment);
                }
            }
            catch (Exception ex)
            {
                LogService.LogException("CommandService.CreateFolder", ex);
                MessageBox.Show(owner, ex.Message, Language.getString("error"), MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        public void CreateNewFile(FileBrowser source, IWin32Window owner)
        {
            if (source?.IsVirtualMode == true)
            {
                ShowArchiveReadOnly(owner);
                return;
            }

            if (source == null || string.IsNullOrWhiteSpace(source.CurrentPath))
            {
                return;
            }

            try
            {
                using SaveFileDialog saveDialog = new SaveFileDialog();
                saveDialog.Title = Language.getString("newFileDialogTitle");
                saveDialog.InitialDirectory = source.CurrentPath;
                saveDialog.FileName = Language.getString("newFileDefaultName");
                saveDialog.Filter = "Text files (*.txt)|*.txt|Markdown files (*.md)|*.md|All files (*.*)|*.*";
                saveDialog.FilterIndex = 1;
                saveDialog.OverwritePrompt = false;
                saveDialog.CheckFileExists = false;
                saveDialog.RestoreDirectory = true;

                if (saveDialog.ShowDialog(owner) != DialogResult.OK)
                {
                    return;
                }

                string filePath = saveDialog.FileName;
                if (string.IsNullOrWhiteSpace(filePath))
                {
                    return;
                }

                CreateEmptyFileAndOpenEditor(source, owner, filePath, null);
            }
            catch (Exception ex)
            {
                LogService.LogException("CommandService.CreateNewFile", ex);
                MessageBox.Show(owner, ex.Message, Language.getString("error"), MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        public void CreateNewFileInline(FileBrowser source, IWin32Window owner, string suggestedFileName = null)
        {
            if (source?.IsVirtualMode == true)
            {
                ShowArchiveReadOnly(owner);
                return;
            }

            if (source == null || string.IsNullOrWhiteSpace(source.CurrentPath))
            {
                return;
            }

            try
            {
                string defaultFileName = string.IsNullOrWhiteSpace(suggestedFileName)
                    ? Language.getString("newFileDefaultName")
                    : suggestedFileName;

                using FormNewFile dialog = new FormNewFile(source.CurrentPath, defaultFileName, CreateNewFileEditorOptions());
                if (dialog.ShowDialog(owner) != DialogResult.OK)
                {
                    return;
                }

                string filePath = dialog.TargetPath;
                if (string.IsNullOrWhiteSpace(filePath))
                {
                    return;
                }

                CreateEmptyFileAndOpenEditor(source, owner, filePath, dialog.SelectedOption);
            }
            catch (Exception ex)
            {
                LogService.LogException("CommandService.CreateNewFileInline", ex);
                MessageBox.Show(owner, ex.Message, Language.getString("error"), MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        public void DeleteSelection(FileBrowser source, Action onSuccess, IWin32Window owner)
        {
            if (source?.IsVirtualMode == true)
            {
                ShowArchiveReadOnly(owner);
                return;
            }

            string[] selectedFiles = source?.selectedFiles;
            if (selectedFiles == null || selectedFiles.Length == 0)
            {
                return;
            }

            FormDelete deleteWindow = new FormDelete(selectedFiles);
            deleteWindow.ActionComplete += result =>
            {
                if (result == 0)
                {
                    onSuccess?.Invoke();
                }
            };
            deleteWindow.ShowDialog(owner);
        }

        public void CreateArchive(FileBrowser source, FileBrowser passive, IWin32Window owner)
        {
            if (source?.IsVirtualMode == true)
            {
                ShowArchiveReadOnly(owner);
                return;
            }

            string[] selectedFiles = source?.selectedFiles;
            if (selectedFiles == null || selectedFiles.Length == 0)
            {
                MessageBox.Show(owner, Language.getString("archiveNoItems"), Language.getString("Info"), MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            string initialDirectory = passive != null && !passive.IsVirtualMode && !string.IsNullOrWhiteSpace(passive.CurrentPath)
                ? passive.CurrentPath
                : source.CurrentPath;

            try
            {
                string suggestedArchivePath = Path.Combine(
                    initialDirectory,
                    BuildDefaultArchiveName(selectedFiles) + ".zip");
                using var operationDialog = new FormArchiveOperation(ArchiveOperationMode.Create, selectedFiles, suggestedArchivePath);
                if (operationDialog.ShowDialog(owner) != DialogResult.OK)
                {
                    return;
                }

                string archivePath = operationDialog.DestinationPath;
                RefreshBrowsers(source, passive);
                SelectPathInMatchingBrowser(source, archivePath);
                SelectPathInMatchingBrowser(passive, archivePath);
            }
            catch (Exception ex)
            {
                LogService.LogException("CommandService.CreateArchive", ex);
                MessageBox.Show(owner, ex.Message, Language.getString("error"), MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        public void ExtractArchive(FileBrowser source, FileBrowser passive, IWin32Window owner)
        {
            string archivePath = source?.IsArchiveMode == true
                ? source.OpenArchivePath
                : source?.selectedFiles?
                    .FirstOrDefault(path => File.Exists(path) && FileTypeClassifier.IsSupportedArchive(path));
            if (string.IsNullOrWhiteSpace(archivePath))
            {
                MessageBox.Show(owner, Language.getString("archiveSelectArchive"), Language.getString("Info"), MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            string initialDirectory = passive != null && !passive.IsVirtualMode && !string.IsNullOrWhiteSpace(passive.CurrentPath)
                ? passive.CurrentPath
                : Path.GetDirectoryName(archivePath);

            try
            {
                using var operationDialog = new FormArchiveOperation(
                    ArchiveOperationMode.Extract,
                    new[] { archivePath },
                    initialDirectory);
                if (operationDialog.ShowDialog(owner) != DialogResult.OK)
                {
                    return;
                }

                RefreshBrowsers(source, passive);
                if (operationDialog.OperationResult?.SkippedEntries > 0)
                {
                    MessageBox.Show(
                        owner,
                        string.Format(Language.getString("archiveCompletedWithSkipped"), operationDialog.OperationResult.SkippedEntries),
                        Language.getString("Info"),
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Information);
                }
            }
            catch (Exception ex)
            {
                LogService.LogException("CommandService.ExtractArchive", ex);
                MessageBox.Show(owner, ex.Message, Language.getString("error"), MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        public void CompareSelections(FileBrowser active, FileBrowser passive, IWin32Window owner)
        {
            string leftFilePath = ResolveCompareSourcePath(active);
            if (string.IsNullOrWhiteSpace(leftFilePath))
            {
                MessageBox.Show(owner, Language.getString("compareSelectSource"), Language.getString("Info"), MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            string rightFilePath = ResolveCompareTargetPath(active, passive, leftFilePath);
            if (string.IsNullOrWhiteSpace(rightFilePath))
            {
                MessageBox.Show(owner, Language.getString("compareSelectTarget"), Language.getString("Info"), MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            if (Directory.Exists(leftFilePath) || Directory.Exists(rightFilePath))
            {
                MessageBox.Show(owner, Language.getString("compareFoldersNotSupported"), Language.getString("Info"), MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            try
            {
                View.FileCompareForm compareForm = new View.FileCompareForm();
                compareForm.LoadFiles(leftFilePath, rightFilePath);
                ShowOwned(compareForm, owner);
            }
            catch (Exception ex)
            {
                LogService.LogException("CommandService.CompareSelections", ex);
                MessageBox.Show(owner, ex.Message, Language.getString("error"), MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void ShowCopyDialog(FileBrowser source, FileBrowser destination, FormCopy.Type operationType, Action<int> onComplete, IWin32Window owner)
        {
            if (source?.selectedFiles == null || source.selectedFiles.Length == 0 || destination == null)
            {
                return;
            }

            try
            {
                FormCopy copyWindow = new FormCopy(source.selectedFiles, destination.CurrentPath, operationType);
                copyWindow.ActionComplete += result => onComplete?.Invoke(result);
                copyWindow.ShowDialog(owner);
            }
            catch (Exception ex)
            {
                LogService.LogException("CommandService.ShowCopyDialog", ex);
                MessageBox.Show(owner, ex.Message, Language.getString("error"), MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void ExtractArchiveSelection(FileBrowser source, FileBrowser destination, Action<int> onComplete, IWin32Window owner)
        {
            string[] entries = source?.SelectedArchiveEntryNames;
            if (entries == null || entries.Length == 0)
            {
                return;
            }

            if (destination == null || destination.IsVirtualMode || string.IsNullOrWhiteSpace(destination.CurrentPath))
            {
                MessageBox.Show(owner, Language.getString("archiveCopyRequiresFilePanel"), Language.getString("Info"), MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            using var dialog = new FormArchiveOperation(
                ArchiveOperationMode.Extract,
                new[] { source.OpenArchivePath },
                destination.CurrentPath,
                entries,
                source.OpenArchiveInternalPath);
            if (dialog.ShowDialog(owner) == DialogResult.OK)
            {
                destination.Refrech();
                onComplete?.Invoke(0);
            }
        }

        private static void ShowArchiveReadOnly(IWin32Window owner)
        {
            MessageBox.Show(owner, Language.getString("virtualPanelReadOnly"), Language.getString("Info"), MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private static string GetFirstSelectedFile(FileBrowser source)
        {
            return source?.selectedFiles != null && source.selectedFiles.Length > 0
                ? source.selectedFiles[0]
                : null;
        }

        private static string BuildDefaultArchiveName(string[] selectedFiles)
        {
            if (selectedFiles?.Length == 1)
            {
                string selectedPath = selectedFiles[0]?.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                string name = Directory.Exists(selectedPath)
                    ? Path.GetFileName(selectedPath)
                    : Path.GetFileNameWithoutExtension(selectedPath);
                if (!string.IsNullOrWhiteSpace(name))
                {
                    return name;
                }
            }

            return Language.getString("archiveDefaultName");
        }

        private static void RefreshBrowsers(FileBrowser first, FileBrowser second)
        {
            first?.Refrech();
            if (second != null && !ReferenceEquals(first, second))
            {
                second.Refrech();
            }
        }

        private static void SelectPathInMatchingBrowser(FileBrowser browser, string path)
        {
            if (browser == null || string.IsNullOrWhiteSpace(path))
            {
                return;
            }

            string parentPath = Path.GetDirectoryName(path);
            if (string.Equals(browser.CurrentPath?.TrimEnd('\\'), parentPath?.TrimEnd('\\'), StringComparison.OrdinalIgnoreCase))
            {
                browser.selectFile(Path.GetFileName(path));
            }
        }

        private static string ResolveCompareSourcePath(FileBrowser active)
        {
            return GetFirstSelectedFile(active);
        }

        private static string ResolveCompareTargetPath(FileBrowser active, FileBrowser passive, string sourcePath)
        {
            if (passive?.IsVirtualMode == true)
            {
                return null;
            }

            string selectedTarget = GetFirstSelectedFile(passive);
            if (!string.IsNullOrWhiteSpace(selectedTarget))
            {
                return selectedTarget;
            }

            if (passive == null || string.IsNullOrWhiteSpace(passive.CurrentPath))
            {
                return null;
            }

            string fileName = active?.GetCurrentItemName();
            if (string.IsNullOrWhiteSpace(fileName))
            {
                fileName = Path.GetFileName(sourcePath);
            }

            if (string.IsNullOrWhiteSpace(fileName))
            {
                return null;
            }

            string candidatePath = Path.Combine(passive.CurrentPath, fileName);
            return FileSystemService.FileExists(candidatePath) ? candidatePath : null;
        }

        private void CreateEmptyFileAndOpenEditor(FileBrowser source, IWin32Window owner, string filePath, NewFileEditorOption fallbackOption)
        {
            if (!FileSystemService.FileExists(filePath))
            {
                string extension = Path.GetExtension(filePath) ?? string.Empty;
                if (string.Equals(extension, ".rtf", StringComparison.OrdinalIgnoreCase))
                {
                    File.WriteAllText(filePath, View.RtfEdit.CreateEmptyDocumentRtf(), System.Text.Encoding.ASCII);
                }
                else
                {
                    using FileStream stream = FileSystemService.OpenCreate(filePath);
                }
            }

            source.Refrech();
            source.selectFile(Path.GetFileName(filePath));

            if (!TryOpenEditor(filePath, owner, false, fallbackOption))
            {
                MessageBox.Show(owner, Language.getString("editorNotSupported"), Language.getString("Info"), MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

        private bool TryOpenEditor(string filePath, IWin32Window owner, bool previewMode, NewFileEditorOption fallbackOption = null)
        {
            string extension = Path.GetExtension(filePath) ?? string.Empty;
            NewFileEditorOption editorOption = ResolveEditorOption(extension, fallbackOption);
            if (editorOption == null)
            {
                return false;
            }

            if (string.Equals(editorOption.Key, "rtf", StringComparison.OrdinalIgnoreCase))
            {
                View.RtfEdit rtfEditor = new View.RtfEdit();
                rtfEditor.LoadFile(filePath, previewMode);
                ShowOwned(rtfEditor, owner);
                return true;
            }

            View.TextEdit formEdit = new View.TextEdit();
            formEdit.LoadFile(filePath, previewMode);
            ShowOwned(formEdit, owner);
            return true;
        }

        private static NewFileEditorOption ResolveEditorOption(string extension, NewFileEditorOption fallbackOption)
        {
            if (string.Equals(extension, ".rtf", StringComparison.OrdinalIgnoreCase))
            {
                return FindEditorOption("rtf");
            }

            if (string.Equals(extension, ".md", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(extension, ".markdown", StringComparison.OrdinalIgnoreCase))
            {
                return FindEditorOption("markdown");
            }

            if (FileTypeClassifier.ClassifyExtension(extension) == FileContentKind.Text)
            {
                return FindEditorOption("text");
            }

            return fallbackOption;
        }

        private static NewFileEditorOption FindEditorOption(string key)
        {
            foreach (NewFileEditorOption option in CreateNewFileEditorOptions())
            {
                if (string.Equals(option.Key, key, StringComparison.OrdinalIgnoreCase))
                {
                    return option;
                }
            }

            return null;
        }

        private static NewFileEditorOption[] CreateNewFileEditorOptions()
        {
            return new[]
            {
                new NewFileEditorOption("text", Language.getString("editorTypeText"), ".txt"),
                new NewFileEditorOption("markdown", Language.getString("editorTypeMarkdown"), ".md"),
                new NewFileEditorOption("rtf", Language.getString("editorTypeRtf"), ".rtf")
            };
        }

        private static string GetFirstRelativePathSegment(string relativePath)
        {
            if (string.IsNullOrWhiteSpace(relativePath))
            {
                return null;
            }

            string[] segments = relativePath.Split(new[] { '\\', '/' }, StringSplitOptions.RemoveEmptyEntries);
            return segments.Length > 0 ? segments[0] : null;
        }

        private static void ShowOwned(Form childForm, IWin32Window owner)
        {
            if (owner is Form ownerForm)
            {
                childForm.Show(ownerForm);
                return;
            }

            childForm.Show();
        }
    }
}
