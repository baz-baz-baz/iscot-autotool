using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace PersonalAutomationTool.Modules.Verifiche
{
    /// <summary>
    /// Percorsi di archiviazione per una flotta: dove sta il file "Verifiche" corrente e dove va
    /// depositata la copia di sicurezza prima di modificarlo.
    /// </summary>
    public sealed class VerifichePathConfig
    {
        /// <summary>Identificatore di flotta: <c>"500"</c>, <c>"700"</c>, <c>"1000"</c>.</summary>
        public string Fleet { get; set; } = string.Empty;

        /// <summary>Segmenti della cartella principale, relativi a <c>%USERPROFILE%</c>.</summary>
        public List<string> MainFolder { get; set; } = [];

        /// <summary>Segmenti della cartella di archivio (OLD), relativi a <c>%USERPROFILE%</c>.</summary>
        public List<string> OldFolder { get; set; } = [];

        /// <summary>Prefisso del nome file, es. <c>"Verifiche ETR500"</c>.</summary>
        public string FilePrefix { get; set; } = string.Empty;
    }

    /// <summary>
    /// Carica da <c>verifiche_paths.json</c> le cartelle usate dall'azione "Verifica Eseguita".
    ///
    /// <para>
    /// <b>Perché un file separato da <c>hitachi_paths.json</c>.</b> Quest'ultimo mappa treno →
    /// <i>una</i> cartella base, ed è consumato da EXCEL (Sposta/Riporta Report); il suo commento
    /// dichiara esplicitamente che i percorsi di VERIFICHE erano fuori dal suo scope. Qui servono
    /// <b>due</b> cartelle per flotta più un prefisso di nome file, e le chiavi sono gli
    /// identificatori di flotta ("500"/"700"/"1000"), non le etichette treno di EXCEL. Forzare i due
    /// schemi in uno solo avrebbe cambiato la forma di una configurazione già installata sulle
    /// macchine dei tecnici, con il rischio di rompere Sposta/Riporta Report senza alcun beneficio.
    /// </para>
    ///
    /// <para>
    /// Stessa cache "solo testo, validata su mtime + dimensione" di <c>DestinatariManager</c>,
    /// <c>ShortcutsManager</c> e <c>HitachiPathsManager</c>: il file sta in una cartella di output
    /// locale, ma la lettura avviene a ogni archiviazione e non ha senso ripeterla.
    /// </para>
    /// </summary>
    public static class VerifichePathsManager
    {
        private static readonly JsonSerializerOptions _jsonOptions = new() { WriteIndented = true };

        private static readonly object _cacheLock = new();
        private static string? _cachedJson;
        private static DateTime _cachedWriteTimeUtc;
        private static long _cachedLength = -1;

        private static string ConfigFilePath =>
            PersonalAutomationTool.Core.AppPaths.DataFile("verifiche_paths.json");

        public static List<VerifichePathConfig> LoadConfig()
        {
            string path = ConfigFilePath;
            if (!File.Exists(path))
            {
                var predefinita = CreateDefaultConfig();
                SaveConfig(predefinita);
                return predefinita;
            }

            try
            {
                string? json = ReadConfigJson(path);
                if (json == null) return CreateDefaultConfig();

                var config = JsonSerializer.Deserialize<List<VerifichePathConfig>>(json);
                return (config == null || config.Count == 0) ? CreateDefaultConfig() : config;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Errore lettura verifiche_paths.json: {ex.Message}");
                return CreateDefaultConfig();
            }
        }

        public static void SaveConfig(List<VerifichePathConfig> config)
        {
            try
            {
                File.WriteAllText(ConfigFilePath, JsonSerializer.Serialize(config, _jsonOptions));
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Errore salvataggio verifiche_paths.json: {ex.Message}");
            }
            finally
            {
                InvalidateCache();
            }
        }

        /// <summary>
        /// Configurazione della flotta indicata, con i percorsi già risolti in assoluto rispetto al
        /// profilo utente. <c>null</c> se la flotta non è configurata.
        /// </summary>
        public static VerifichePercorsiRisolti? Risolvi(string userProfile, string fleetIdentifier)
        {
            var match = LoadConfig().FirstOrDefault(c =>
                c.Fleet.Equals(fleetIdentifier, StringComparison.OrdinalIgnoreCase));

            if (match == null || match.MainFolder.Count == 0) return null;

            return new VerifichePercorsiRisolti(
                CartellaPrincipale: Combina(userProfile, match.MainFolder),
                CartellaOld: match.OldFolder.Count > 0 ? Combina(userProfile, match.OldFolder) : null,
                PrefissoFile: match.FilePrefix);
        }

        private static string Combina(string userProfile, List<string> segmenti)
        {
            var parti = new string[segmenti.Count + 1];
            parti[0] = userProfile;
            segmenti.CopyTo(parti, 1);
            return Path.Combine(parti);
        }

        private static string? ReadConfigJson(string path)
        {
            var info = new FileInfo(path);
            if (!info.Exists) return null;

            lock (_cacheLock)
            {
                if (_cachedJson != null &&
                    _cachedLength == info.Length &&
                    _cachedWriteTimeUtc == info.LastWriteTimeUtc)
                {
                    return _cachedJson;
                }

                string json = File.ReadAllText(path);
                _cachedJson = json;
                _cachedLength = info.Length;
                _cachedWriteTimeUtc = info.LastWriteTimeUtc;
                return json;
            }
        }

        private static void InvalidateCache()
        {
            lock (_cacheLock)
            {
                _cachedJson = null;
                _cachedLength = -1;
                _cachedWriteTimeUtc = default;
            }
        }

        /// <summary>
        /// Percorsi dettati dal committente. Le maiuscole seguono i nomi <b>reali</b> delle cartelle
        /// su disco, che non sono uniformi fra flotte ("INTERVENTI ETR700" in maiuscolo,
        /// "Interventi ETR500" no): su Windows il confronto è insensibile alle maiuscole, ma questi
        /// percorsi finiscono anche nei messaggi d'errore mostrati al tecnico.
        /// </summary>
        private static List<VerifichePathConfig> CreateDefaultConfig() =>
        [
            new()
            {
                Fleet = "500",
                FilePrefix = "Verifiche ETR500",
                MainFolder = ["Hitachi Group", "SSB_SST - Interventi ETR500", "Censimento ETR500", "Verifiche ETR500"],
                OldFolder = ["Hitachi Group", "SSB_SST - Interventi ETR500", "Censimento ETR500", "Verifiche ETR500", "Verifiche effettuate"]
            },
            new()
            {
                Fleet = "700",
                FilePrefix = "Verifiche ETR700",
                MainFolder = ["Hitachi Group", "SSB_SST - INTERVENTI ETR700 ELO BL3"],
                OldFolder = ["Hitachi Group", "SSB_SST - INTERVENTI ETR700 ELO BL3", "CENSIMENTI ETR700", "VERIFICHE ETR700 OLD"]
            },
            new()
            {
                Fleet = "1000",
                FilePrefix = "Verifiche ETR1000",
                MainFolder = ["Hitachi Group", "SSB_SST - Interventi ETR1000"],
                OldFolder = ["Hitachi Group", "SSB_SST - Interventi ETR1000", "OLD Verifica Aggiuntiva ETR1000"]
            }
        ];
    }

    /// <summary>Percorsi di una flotta, già assoluti.</summary>
    public sealed record VerifichePercorsiRisolti(string CartellaPrincipale, string? CartellaOld, string PrefissoFile);
}
