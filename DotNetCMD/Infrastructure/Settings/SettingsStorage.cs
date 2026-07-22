using System.Configuration;

namespace DotNetCommander
{
    internal static class SettingsStorage
    {
        public static string GetUserConfigPath()
        {
            return ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.PerUserRoamingAndLocal).FilePath;
        }
    }
}
