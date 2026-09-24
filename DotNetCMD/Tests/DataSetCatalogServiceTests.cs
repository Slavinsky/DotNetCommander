using System.Data;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using DotNetCommander;
using Xunit;

namespace DotNetCommander.Tests
{
    public sealed class DataSetCatalogServiceTests
    {
        [Fact]
        public async Task ReadAsync_ValidXml_InfersTablesAndRows()
        {
            using var dir = new TemporaryDirectory();
            string path = WriteFile(dir, "valid.dsx",
                "<Root><Item><Name>alpha</Name><Qty>1</Qty></Item><Item><Name>beta</Name><Qty>2</Qty></Item></Root>");

            var dataSet = await DataSetCatalogService.ReadAsync(path, CancellationToken.None);

            Assert.True(dataSet.Tables.Contains("Item"));
            DataTable item = dataSet.Tables["Item"];
            Assert.Equal(2, item.Rows.Count);
            Assert.Equal("alpha", item.Rows[0]["Name"]);
            Assert.Equal("2", item.Rows[1]["Qty"]);
        }

        [Fact]
        public async Task ReadAsync_InvalidXml10Characters_AreSanitized()
        {
            using var dir = new TemporaryDirectory();
            string path = WriteFile(dir, "badchars.dsx",
                "<Root><Item><Name>bad" + ((char)8) + "name</Name><Qty>7</Qty></Item></Root>");

            var dataSet = await DataSetCatalogService.ReadAsync(path, CancellationToken.None);

            DataTable item = dataSet.Tables["Item"];
            Assert.Single(item.Rows);
            Assert.Equal("badname", item.Rows[0]["Name"]);
            Assert.Equal("7", item.Rows[0]["Qty"]);
        }

        [Fact]
        public async Task ReadAsync_DtdIsProhibited()
        {
            using var dir = new TemporaryDirectory();
            string path = WriteFile(dir, "xxe.dsx",
                "<!DOCTYPE Root [<!ENTITY xxe SYSTEM \"file:///c:/windows/win.ini\">]><Root><Item><Name>&xxe;</Name></Item></Root>");

            await Assert.ThrowsAsync<XmlException>(
                () => DataSetCatalogService.ReadAsync(path, CancellationToken.None));
        }

        [Fact]
        public async Task ReadAsync_PreCancelledToken_Throws()
        {
            using var dir = new TemporaryDirectory();
            string path = WriteFile(dir, "cancel.dsx", "<Root><Item><Name>x</Name></Item></Root>");
            using var cts = new CancellationTokenSource();
            cts.Cancel();

            await Assert.ThrowsAnyAsync<OperationCanceledException>(
                () => DataSetCatalogService.ReadAsync(path, cts.Token));
        }

        private static string WriteFile(TemporaryDirectory dir, string name, string content)
        {
            string path = dir.GetPath(name);
            File.WriteAllText(path, content);
            return path;
        }
    }
}
