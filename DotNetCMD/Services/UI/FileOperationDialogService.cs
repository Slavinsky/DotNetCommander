using System;
using System.Globalization;
using System.Text;
using System.Windows.Forms;

namespace DotNetCommander
{
    internal static class FileOperationDialogService
    {
        public static FileOperationFailureAction PromptForFailure(IWin32Window owner, FileOperationFailure failure)
        {
            string path = string.IsNullOrWhiteSpace(failure.DestinationPath)
                ? failure.SourcePath
                : failure.SourcePath + Environment.NewLine + "→ " + failure.DestinationPath;
            string message = string.Format(
                CultureInfo.CurrentUICulture,
                Language.getString("operationFailurePrompt"),
                path,
                failure.Exception.Message,
                failure.Attempt);

            DialogResult answer = MessageBox.Show(
                owner,
                message,
                Language.getString("operationFailureTitle"),
                MessageBoxButtons.AbortRetryIgnore,
                MessageBoxIcon.Warning,
                MessageBoxDefaultButton.Button2);

            return answer switch
            {
                DialogResult.Retry => FileOperationFailureAction.Retry,
                DialogResult.Ignore => FileOperationFailureAction.Skip,
                _ => FileOperationFailureAction.Cancel
            };
        }

        public static void ShowSummary(IWin32Window owner, FileOperationResult result)
        {
            if (result == null)
                return;

            var message = new StringBuilder();
            message.AppendLine(result.RunResult == FileOperationRunResult.Cancelled
                ? Language.getString("operationSummaryCancelled")
                : Language.getString("operationSummaryCompleted"));
            message.AppendLine();
            message.AppendLine(string.Format(
                CultureInfo.CurrentUICulture,
                Language.getString("operationSummaryCounts"),
                result.CompletedEntries,
                result.SkippedEntries,
                result.FailedEntries));
            message.AppendLine(string.Format(
                CultureInfo.CurrentUICulture,
                Language.getString("operationSummaryDuration"),
                FormatDuration(result.Elapsed)));

            if (result.Failures.Count > 0)
            {
                message.AppendLine();
                message.AppendLine(Language.getString("operationSummaryErrors"));
                int shown = Math.Min(5, result.Failures.Count);
                for (int index = 0; index < shown; index++)
                {
                    FileOperationFailure failure = result.Failures[index];
                    message.AppendLine("• " + (failure.SourcePath ?? failure.DestinationPath) + ": " + failure.Exception.Message);
                }

                if (result.Failures.Count > shown)
                {
                    message.AppendLine(string.Format(
                        CultureInfo.CurrentUICulture,
                        Language.getString("operationSummaryMoreErrors"),
                        result.Failures.Count - shown));
                }
            }

            MessageBoxIcon icon = result.FailedEntries > 0 ? MessageBoxIcon.Warning : MessageBoxIcon.Information;
            MessageBox.Show(owner, message.ToString().TrimEnd(), Language.getString("operationSummaryTitle"), MessageBoxButtons.OK, icon);
        }

        private static string FormatDuration(TimeSpan duration)
        {
            return duration.TotalHours >= 1
                ? duration.ToString(@"h\:mm\:ss", CultureInfo.InvariantCulture)
                : duration.ToString(@"m\:ss", CultureInfo.InvariantCulture);
        }
    }
}
