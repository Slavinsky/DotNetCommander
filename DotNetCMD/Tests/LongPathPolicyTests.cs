using DotNetCommander;
using Xunit;

namespace DotNetCommander.Tests
{
    public sealed class LongPathPolicyTests
    {
        [Fact]
        public void ShortDrivePath_StaysUnchanged()
        {
            string path = @"C:\temp\file.txt";

            Assert.False(LongPathPolicy.NeedsExtendedPrefix(path));
            Assert.Equal(path, LongPathPolicy.Apply(path));
        }

        [Fact]
        public void LongDrivePath_GetsDevicePrefix()
        {
            string path = @"C:\deep\" + new string('a', 300);

            Assert.True(LongPathPolicy.NeedsExtendedPrefix(path));
            Assert.StartsWith(@"\\?\C:\deep\", LongPathPolicy.Apply(path));
        }

        [Fact]
        public void LongUncPath_GetsUncDevicePrefix()
        {
            string path = @"\\server\share\deep\" + new string('b', 300);

            Assert.True(LongPathPolicy.NeedsExtendedPrefix(path));
            Assert.StartsWith(@"\\?\UNC\server\share\", LongPathPolicy.Apply(path));
        }

        [Fact]
        public void ShortUncPath_StaysUnchanged()
        {
            string path = @"\\server\share\file.txt";

            Assert.False(LongPathPolicy.NeedsExtendedPrefix(path));
            Assert.Equal(path, LongPathPolicy.Apply(path));
        }

        [Fact]
        public void LongPathAlreadyUsingDevicePrefix_IsLeftAlone()
        {
            string path = @"\\?\C:\deep\" + new string('c', 300);

            Assert.False(LongPathPolicy.NeedsExtendedPrefix(path));
            Assert.Equal(path, LongPathPolicy.Apply(path));
        }

        [Fact]
        public void LongRelativePath_StaysUnchanged()
        {
            string path = @"deep\" + new string('d', 300);

            Assert.False(LongPathPolicy.NeedsExtendedPrefix(path));
            Assert.Equal(path, LongPathPolicy.Apply(path));
        }

        [Fact]
        public void NullOrWhitespace_StaysUnchanged()
        {
            Assert.False(LongPathPolicy.NeedsExtendedPrefix(null));
            Assert.False(LongPathPolicy.NeedsExtendedPrefix(string.Empty));
            Assert.False(LongPathPolicy.NeedsExtendedPrefix("   "));
            Assert.Null(LongPathPolicy.Apply(null));
            Assert.Equal(string.Empty, LongPathPolicy.Apply(string.Empty));
        }

        [Fact]
        public void PrefixThreshold_IsExactlyMaxPath()
        {
            string at259 = "C:\\" + new string('e', 256);
            string at260 = "C:\\" + new string('e', 257);

            Assert.Equal(259, at259.Length);
            Assert.False(LongPathPolicy.NeedsExtendedPrefix(at259));

            Assert.Equal(260, at260.Length);
            Assert.True(LongPathPolicy.NeedsExtendedPrefix(at260));
            Assert.StartsWith(@"\\?\C:\", LongPathPolicy.Apply(at260));
        }
    }
}
