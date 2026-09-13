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
            // Prima di ogni altra cosa: prepara la cartella dati (%LOCALAPPDATA%\iscot-autotool) e vi
            // trasferisce lo stato della 2.0.0 o rimasto accanto all'eseguibile. Deve precedere qualunque
            // lettura di configurazioni o database, perché da qui in poi tutti i percorsi scrivibili
            // passano da AppPaths.
            AppPaths.Initialize();

            LogAndDumpFolder = Path.Combine(RisolviDesktop(), "LOG & DUMP");

            if (!Directory.Exists(LogAndDumpFolder))
            {
                Directory.CreateDirectory(LogAndDumpFolder);
            }
        }

        /// <summary>
        /// Desktop dell'utente, sempre come percorso assoluto. <see cref="Environment.GetFolderPath(Environment.SpecialFolder)"/>
        /// restituisce una stringa <b>vuota</b> se la cartella non esiste fisicamente — caso reale con il
        /// Desktop reindirizzato su OneDrive non ancora sincronizzato — e <c>Path.Combine("", "LOG &amp; DUMP")</c>
        /// diventerebbe un percorso relativo alla cartella corrente: LOG &amp; DUMP creata accanto
        /// all'eseguibile, lo stesso difetto corretto nello Sprint 29 (§6.1-tricies-semel di PROJECT_MEMORY.md).
        /// </summary>
        internal static string RisolviDesktop()
        {
            string desktop = Environment.GetFolderPath(Environment.SpecialFolder.Desktop, Environment.SpecialFolderOption.DoNotVerify);
            if (Path.IsPathFullyQualified(desktop)) return desktop;

            string profilo = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile, Environment.SpecialFolderOption.DoNotVerify);
            if (Path.IsPathFullyQualified(profilo)) return Path.Combine(profilo, "Desktop");

            throw new InvalidOperationException(
                "Impossibile determinare il Desktop dell'utente: la cartella LOG & DUMP non avrebbe un percorso valido.");
        }
    }
}
