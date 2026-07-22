using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace DotNetCommander
{
    internal sealed class CsvTableLoader
    {
        public DataTable Load(string filePath, IProgress<CsvLoadProgress> progress, CancellationToken cancellationToken)
        {
            return LoadCore(filePath, progress, cancellationToken);
        }

        public Task<DataTable> LoadAsync(string filePath, IProgress<CsvLoadProgress> progress, CancellationToken cancellationToken)
        {
            return Task.Run(() => LoadCore(filePath, progress, cancellationToken), cancellationToken);
        }

        private static DataTable LoadCore(string filePath, IProgress<CsvLoadProgress> progress, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            using var reader = new CsvByteLineReader(filePath);
            long totalBytes = reader.Length;
            progress?.Report(new CsvLoadProgress("Opening", 0, totalBytes, 0));

            var table = new DataTable(Path.GetFileName(filePath));
            string headerLine = reader.ReadLine();
            if (headerLine == null)
            {
                progress?.Report(new CsvLoadProgress("Completed", totalBytes, totalBytes, 0));
                return table;
            }

            char delimiter = DetectDelimiter(headerLine);
            List<string> headers = ParseLine(headerLine, delimiter);
            EnsureColumns(table, headers.Count);

            for (int index = 0; index < headers.Count; index++)
            {
                table.Columns[index].ColumnName = GetUniqueColumnName(table, headers[index], index);
            }

            int rowsRead = 0;
            int lastReportedPercent = -1;
            long lastReportedBytes = -1;
            string lastReportedPhase = null;
            var progressStopwatch = Stopwatch.StartNew();

            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();

                string line = reader.ReadLine();
                if (line == null)
                {
                    break;
                }

                List<string> values = ParseLine(line, delimiter);
                EnsureColumns(table, values.Count);
                DataRow row = table.NewRow();
                for (int index = 0; index < table.Columns.Count; index++)
                {
                    row[index] = index < values.Count ? values[index] : string.Empty;
                }

                table.Rows.Add(row);
                rowsRead++;

                ReportProgress(progress, reader.Position, totalBytes, rowsRead, "Reading", ref lastReportedPercent, ref lastReportedBytes, ref lastReportedPhase, progressStopwatch);
            }

            progress?.Report(new CsvLoadProgress("Completed", totalBytes, totalBytes, rowsRead));
            return table;
        }

        private static void EnsureColumns(DataTable table, int count)
        {
            while (table.Columns.Count < count)
            {
                table.Columns.Add($"Column{table.Columns.Count + 1}");
            }
        }

        private static string GetUniqueColumnName(DataTable table, string sourceName, int index)
        {
            string baseName = string.IsNullOrWhiteSpace(sourceName) ? $"Column{index + 1}" : sourceName.Trim().Trim('"');
            string candidate = baseName;
            int suffix = 2;

            while (table.Columns.Cast<DataColumn>().Any(column =>
                       !ReferenceEquals(column, table.Columns[index]) &&
                       string.Equals(column.ColumnName, candidate, StringComparison.OrdinalIgnoreCase)))
            {
                candidate = $"{baseName}_{suffix}";
                suffix++;
            }

            return candidate;
        }

        private static char DetectDelimiter(string headerLine)
        {
            char[] delimiters = { ',', ';', '\t', '|', ':' };
            char bestDelimiter = ',';
            int maxCount = -1;

            foreach (char delimiter in delimiters)
            {
                int count = CountDelimiter(headerLine, delimiter);
                if (count > maxCount)
                {
                    maxCount = count;
                    bestDelimiter = delimiter;
                }
            }

            return bestDelimiter;
        }

        private static int CountDelimiter(string line, char delimiter)
        {
            bool inQuotes = false;
            int count = 0;

            foreach (char ch in line)
            {
                if (ch == '"')
                {
                    inQuotes = !inQuotes;
                    continue;
                }

                if (!inQuotes && ch == delimiter)
                {
                    count++;
                }
            }

            return count;
        }

        private static List<string> ParseLine(string line, char delimiter)
        {
            var values = new List<string>();
            var builder = new StringBuilder(line.Length);
            bool inQuotes = false;

            for (int index = 0; index < line.Length; index++)
            {
                char current = line[index];
                if (current == '"')
                {
                    if (inQuotes && index + 1 < line.Length && line[index + 1] == '"')
                    {
                        builder.Append('"');
                        index++;
                    }
                    else
                    {
                        inQuotes = !inQuotes;
                    }

                    continue;
                }

                if (!inQuotes && current == delimiter)
                {
                    values.Add(builder.ToString().Trim());
                    builder.Clear();
                    continue;
                }

                builder.Append(current);
            }

            values.Add(builder.ToString().Trim());
            return values;
        }

        private static void ReportProgress(
            IProgress<CsvLoadProgress> progress,
            long bytesRead,
            long totalBytes,
            int rowsRead,
            string phase,
            ref int lastReportedPercent,
            ref long lastReportedBytes,
            ref string lastReportedPhase,
            Stopwatch progressStopwatch)
        {
            if (progress == null)
            {
                return;
            }

            int percent = totalBytes > 0
                ? Math.Max(0, Math.Min(100, (int)Math.Round((double)bytesRead / totalBytes * 100)))
                : 0;

            bool phaseChanged = !string.Equals(lastReportedPhase, phase, StringComparison.Ordinal);
            bool intervalElapsed = progressStopwatch.ElapsedMilliseconds >= 120;
            bool enoughRows = rowsRead % 1024 == 0;
            bool enoughBytes = lastReportedBytes < 0 || bytesRead - lastReportedBytes >= 256L * 1024L;
            bool completed = bytesRead >= totalBytes;

            if (!completed &&
                !phaseChanged &&
                (!intervalElapsed || (!enoughRows && !enoughBytes)) &&
                percent == lastReportedPercent)
            {
                return;
            }

            lastReportedPercent = percent;
            lastReportedBytes = bytesRead;
            lastReportedPhase = phase;
            progressStopwatch.Restart();
            progress.Report(new CsvLoadProgress(phase, bytesRead, totalBytes, rowsRead));
        }
    }

    internal sealed class CsvLoadProgress
    {
        public CsvLoadProgress(string phase, long bytesRead, long totalBytes, int rowsRead)
        {
            Phase = phase;
            BytesRead = bytesRead;
            TotalBytes = totalBytes;
            RowsRead = rowsRead;
        }

        public string Phase { get; }
        public long BytesRead { get; }
        public long TotalBytes { get; }
        public int RowsRead { get; }
        public int Percent => TotalBytes <= 0 ? 0 : Math.Max(0, Math.Min(100, (int)Math.Round((double)BytesRead / TotalBytes * 100)));
    }

    internal sealed class CsvByteLineReader : IDisposable
    {
        private readonly FileStream stream;
        private readonly Encoding encoding;

        public CsvByteLineReader(string filePath)
        {
            stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            encoding = DetectEncoding(stream);
        }

        public long Position => stream.Position;
        public long Length => stream.Length;

        public string ReadLine()
        {
            if (stream.Position >= stream.Length)
            {
                return null;
            }

            var bytes = new List<byte>(256);
            while (true)
            {
                int value = stream.ReadByte();
                if (value == -1)
                {
                    break;
                }

                if (value == '\n')
                {
                    break;
                }

                if (value == '\r')
                {
                    long nextPosition = stream.Position;
                    int nextValue = stream.ReadByte();
                    if (nextValue != '\n' && nextValue != -1)
                    {
                        stream.Position = nextPosition;
                    }
                    break;
                }

                bytes.Add((byte)value);
            }

            return encoding.GetString(bytes.ToArray());
        }

        public void Dispose()
        {
            stream.Dispose();
        }

        private static Encoding DetectEncoding(FileStream stream)
        {
            Span<byte> bom = stackalloc byte[4];
            int bytesRead = stream.Read(bom);

            if (bytesRead >= 3 && bom[0] == 0xEF && bom[1] == 0xBB && bom[2] == 0xBF)
            {
                return new UTF8Encoding(false, true);
            }

            if (bytesRead >= 4 && bom[0] == 0xFF && bom[1] == 0xFE && bom[2] == 0x00 && bom[3] == 0x00)
            {
                return Encoding.UTF32;
            }

            if (bytesRead >= 2 && bom[0] == 0xFF && bom[1] == 0xFE)
            {
                return Encoding.Unicode;
            }

            if (bytesRead >= 2 && bom[0] == 0xFE && bom[1] == 0xFF)
            {
                return Encoding.BigEndianUnicode;
            }

            stream.Position = 0;
            return new UTF8Encoding(false, true);
        }
    }
}
