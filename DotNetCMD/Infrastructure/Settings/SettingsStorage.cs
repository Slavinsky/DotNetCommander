using System.Configuration;

namespace DotNetCommander
{
    internal static class SettingsStorage
    {
        public static string GetUserConfigPath()
        {
            return ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.PerUserRoamingAndLocal).FilePath;
        }

        public static string GetDefaultVectorStoreBasePath()
        {
            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "DotNetCommander",
                "VectorStores");
        }

        public static string GetVectorStoreBasePath()
        {
            string configuredPath = Properties.Settings.Default.VectorStoreBasePath;
            return string.IsNullOrWhiteSpace(configuredPath)
                ? GetDefaultVectorStoreBasePath()
                : Path.GetFullPath(Environment.ExpandEnvironmentVariables(configuredPath.Trim()));
        }
    }
}
