using System;
using System.IO;

namespace PersonalAutomationTool.Core
{
    public static class AppConfig
    {
        /// <summary>
        /// Percorso assoluto alla cartella "LOG & DUMP" sul desktop dell'utente.
        /// </summary>
        public static string LogAndDumpFolder { get; private set; } = string.Empty;

        public static void Initialize()
        {
            string desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
            LogAndDumpFolder = Path.Combine(desktopPath, "LOG & DUMP");

            if (!Directory.Exists(LogAndDumpFolder))
            {
                Directory.CreateDirectory(LogAndDumpFolder);
            }
        }
    }
}
