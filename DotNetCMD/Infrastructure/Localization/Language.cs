using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Resources;
using System.Reflection;
using System.Globalization;
using System.Threading;

namespace DotNetCommander
{
    class Language
    {
        public static ResourceManager rm = new ResourceManager("DotNetCommander.Resources.Language", Assembly.GetExecutingAssembly());
        public static CultureInfo culture = CultureInfo.CurrentUICulture;

        public static void ApplyConfiguredCulture(string configuredLanguage)
        {
            CultureInfo selectedCulture = ResolveCulture(configuredLanguage);
            culture = selectedCulture;
            Thread.CurrentThread.CurrentUICulture = selectedCulture;
            CultureInfo.DefaultThreadCurrentUICulture = selectedCulture;
        }

        public static String getString(String name)
        {
            CultureInfo currentCulture = culture ?? CultureInfo.CurrentUICulture;
            string value = rm.GetString(name, currentCulture);

            if (string.IsNullOrEmpty(value) && currentCulture != CultureInfo.InvariantCulture)
            {
                value = rm.GetString(name, CultureInfo.InvariantCulture);
            }

            return string.IsNullOrEmpty(value) ? name : value;
        }

        private static CultureInfo ResolveCulture(string configuredLanguage)
        {
            if (string.IsNullOrWhiteSpace(configuredLanguage) ||
                string.Equals(configuredLanguage, "auto", StringComparison.OrdinalIgnoreCase))
            {
                return CultureInfo.CurrentUICulture;
            }

            try
            {
                return CultureInfo.GetCultureInfo(configuredLanguage);
            }
            catch (CultureNotFoundException)
            {
                return CultureInfo.CurrentUICulture;
            }
        }
    }
}
