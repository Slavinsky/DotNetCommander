using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace DotNetCommander
{
    static class Program
    {
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Language.ApplyConfiguredCulture(Properties.Settings.Default.UiLanguage);
            CompoundFileCatalogService.CleanupStaleMaterializationRoots(
                Path.Combine(Path.GetTempPath(), "DotNetCommander", "CompoundPreview"));
            Application.Run(new AppForm());
        }
    }
}
