using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using DotNetCommander;
using Xunit;

namespace DotNetCommander.Tests
{
    public sealed class CsvTableLoaderTests
    {
        [Fact]
        public void Load_CommaSeparated_BuildsTable()
        {
            using var dir = new TemporaryDirectory();
            string path = WriteFile(dir, "people.csv", "Name,Age\nJohn,30\nJane,25\n");

            var table = new CsvTableLoader().Load(path, null, CancellationToken.None);

            Assert.Equal(2, table.Columns.Count);
            Assert.Equal("Name", table.Columns[0].ColumnName);
            Assert.Equal("Age", table.Columns[1].ColumnName);
            Assert.Equal(2, table.Rows.Count);
            Assert.Equal("John", table.Rows[0][0]);
            Assert.Equal("30", table.Rows[0][1]);
            Assert.Equal("Jane", table.Rows[1][0]);
            Assert.Equal("25", table.Rows[1][1]);
        }

        [Fact]
        public void Load_DetectsSemicolonDelimiter()
        {
            using var dir = new TemporaryDirectory();
            string path = WriteFile(dir, "eu.csv", "a;b;c\n1;2;3\n");

            var table = new CsvTableLoader().Load(path, null, CancellationToken.None);

            Assert.Equal(3, table.Columns.Count);
            Assert.Single(table.Rows);
            Assert.Equal("2", table.Rows[0][1]);
        }

        [Fact]
        public void Load_ParsesQuotedFieldsWithEscapedQuotes()
        {
            using var dir = new TemporaryDirectory();
            string path = WriteFile(dir, "quoted.csv", "\"Product, Large\";Price\n\"Widget \"\"X\"\"; deluxe\";9\n");

            var table = new CsvTableLoader().Load(path, null, CancellationToken.None);

            Assert.Equal(2, table.Columns.Count);
            Assert.Equal("Product, Large", table.Columns[0].ColumnName);
            Assert.Single(table.Rows);
            Assert.Equal("Widget \"X\"; deluxe", table.Rows[0][0]);
            Assert.Equal("9", table.Rows[0][1]);
        }

        [Fact]
        public void Load_PadsShortRows_AndExpandsForLongRows()
        {
            using var dir = new TemporaryDirectory();
            string path = WriteFile(dir, "ragged.csv", "A,B\nonlyA\na,b,c,d\n");

            var table = new CsvTableLoader().Load(path, null, CancellationToken.None);

            Assert.Equal(4, table.Columns.Count);
            Assert.Equal(2, table.Rows.Count);
            Assert.Equal("onlyA", table.Rows[0][0]);
            Assert.Equal(string.Empty, table.Rows[0][1]);
            Assert.Equal("c", table.Rows[1][2]);
            Assert.Equal("d", table.Rows[1][3]);
            Assert.Equal("Column3", table.Columns[2].ColumnName);
            Assert.Equal("Column4", table.Columns[3].ColumnName);
        }

        [Fact]
        public void Load_DuplicateHeaderNamesGetUniqueColumnNames()
        {
            using var dir = new TemporaryDirectory();
            string path = WriteFile(dir, "dupes.csv", "id,ID,name\n1,2,x\n");

            var table = new CsvTableLoader().Load(path, null, CancellationToken.None);

            Assert.Equal(3, table.Columns.Count);
            Assert.Equal("id", table.Columns[0].ColumnName);
            Assert.Equal("ID_2", table.Columns[1].ColumnName);
            Assert.Equal("name", table.Columns[2].ColumnName);
        }

        [Fact]
        public void Load_EmptyHeaderCellsGetPositionalNames()
        {
            using var dir = new TemporaryDirectory();
            string path = WriteFile(dir, "blank.csv", ",b\n1,2\n");

            var table = new CsvTableLoader().Load(path, null, CancellationToken.None);

            Assert.Equal(2, table.Columns.Count);
            Assert.Equal("Column1", table.Columns[0].ColumnName);
            Assert.Equal("b", table.Columns[1].ColumnName);
        }

        [Fact]
        public void Load_EmptyFile_ReturnsEmptyTable()
        {
            using var dir = new TemporaryDirectory();
            string path = WriteFile(dir, "empty.csv", string.Empty);

            var table = new CsvTableLoader().Load(path, null, CancellationToken.None);

            Assert.Empty(table.Columns);
            Assert.Empty(table.Rows);
        }

        [Fact]
        public void Load_HeaderOnlyFile_HasColumnsButNoRows()
        {
            using var dir = new TemporaryDirectory();
            string path = WriteFile(dir, "header.csv", "A,B,C\n");

            var table = new CsvTableLoader().Load(path, null, CancellationToken.None);

            Assert.Equal(3, table.Columns.Count);
            Assert.Empty(table.Rows);
        }

        [Fact]
        public void Load_ReportsCompletedProgressWithRowCount()
        {
            using var dir = new TemporaryDirectory();
            string path = WriteFile(dir, "progress.csv", "A\n1\n2\n3\n");
            var sink = new ProgressSink();

            var table = new CsvTableLoader().Load(path, sink, CancellationToken.None);

            Assert.Equal(3, table.Rows.Count);
            Assert.NotEmpty(sink.Reports);
            CsvLoadProgress completed = sink.Reports[sink.Reports.Count - 1];
            Assert.Equal("Completed", completed.Phase);
            Assert.Equal(3, completed.RowsRead);
            Assert.Equal(100, completed.Percent);
        }

        [Fact]
        public void Load_PreCancelledToken_Throws()
        {
            using var dir = new TemporaryDirectory();
            string path = WriteFile(dir, "cancel.csv", "A\n1\n");
            using var cts = new CancellationTokenSource();
            cts.Cancel();

            Assert.Throws<OperationCanceledException>(
                () => new CsvTableLoader().Load(path, null, cts.Token));
        }

        private static string WriteFile(TemporaryDirectory dir, string name, string content)
        {
            string path = dir.GetPath(name);
            File.WriteAllText(path, content);
            return path;
        }

        private sealed class ProgressSink : IProgress<CsvLoadProgress>
        {
            public List<CsvLoadProgress> Reports { get; } = new List<CsvLoadProgress>();

            public void Report(CsvLoadProgress value)
            {
                Reports.Add(value);
            }
        }
    }
}
