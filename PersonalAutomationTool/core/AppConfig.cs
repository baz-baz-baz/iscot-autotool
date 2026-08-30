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
            // Prima di ogni altra cosa: prepara %APPDATA% e vi trasferisce lo stato eventualmente
            // rimasto accanto all'eseguibile. Deve precedere qualunque lettura di configurazioni o
            // database, perché da qui in poi tutti i percorsi scrivibili passano da AppPaths.
            AppPaths.Initialize();

            string desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
            LogAndDumpFolder = Path.Combine(desktopPath, "LOG & DUMP");

            if (!Directory.Exists(LogAndDumpFolder))
            {
                Directory.CreateDirectory(LogAndDumpFolder);
            }
        }
    }
}
