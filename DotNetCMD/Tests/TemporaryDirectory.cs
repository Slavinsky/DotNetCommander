namespace DotNetCommander.Tests
{
    internal sealed class TemporaryDirectory : IDisposable
    {
        public TemporaryDirectory()
        {
            Root = Directory.CreateDirectory(
                System.IO.Path.Combine(
                    System.IO.Path.GetTempPath(),
                    "DotNetCommander.Tests-" + Guid.NewGuid().ToString("N"))).FullName;
        }

        public string Root { get; }

        public string GetPath(string relativePath)
        {
            return System.IO.Path.Combine(Root, relativePath.Replace('/', System.IO.Path.DirectorySeparatorChar));
        }

        public void Dispose()
        {
            try
            {
                Directory.Delete(Root, true);
            }
            catch
            {
            }
        }
    }
}
