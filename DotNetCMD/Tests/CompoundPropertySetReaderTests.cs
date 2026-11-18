using System;
using System.IO;
using System.Linq;
using Xunit;

namespace DotNetCommander.Tests
{
    public class CompoundPropertySetReaderTests
    {
        [Fact]
        public void Read_IntegerProperty_ReadsValueImmediatelyAfterType()
        {
            using var stream = new MemoryStream();
            using var writer = new BinaryWriter(stream);
            writer.Write((ushort)0xFFFE); writer.Write((ushort)0);
            writer.Write(0u); writer.Write(new byte[16]); writer.Write(1u);
            writer.Write(new Guid("F29F85E0-4FF9-1068-AB91-08002B27B3D9").ToByteArray());
            writer.Write(48u); writer.Write(24u); writer.Write(1u);
            writer.Write(14u); writer.Write(16u); writer.Write(3u); writer.Write(42);
            var row = Assert.Single(CompoundPropertySetReader.Read("SummaryInformation", stream.ToArray()));
            Assert.Equal("42", row.Value);
            Assert.Equal("I4", row.TypeName);
        }
    }
}
