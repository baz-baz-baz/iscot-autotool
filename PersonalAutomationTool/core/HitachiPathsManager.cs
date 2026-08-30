using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace PersonalAutomationTool.Core
{
    public class HitachiPathConfig
    {
        /// <summary>Valore di <c>SelectedTrain</c> a cui questo percorso è associato (es. "ETR700", "E404P").</summary>
        public string Train { get; set; } = string.Empty;

        /// <summary>Segmenti del percorso relativo a <c>%USERPROFILE%</c>, es. ["Hitachi Group", "SSB_SST - Interventi ETR1000"].</summary>
        public List<string> RelativePath { get; set; } = new();
    }

    /// <summary>
    /// Carica la cartella Hitachi/SharePoint-OneDrive base per ciascun treno da
    /// <c>hitachi_paths.json</c> invece di averla scritta due volte, identica, dentro
    /// <c>ExcelViewModel.ExecuteSpostaReport</c> ed <c>ExecuteRiportaReport</c>.
    /// <para>
    /// Scope volutamente limitato: copre solo la cartella Hitachi "base" per treno (il pezzo che
    /// smette di funzionare, con l'errore "Cartella Hitachi non trovata", se qualcuno rinomina una
    /// cartella SharePoint). La struttura del sotto-percorso di destinazione per il salvataggio dei
    /// vecchi report (`targetFolder` in ExcelViewModel) ha forme non uniformi da treno a treno
    /// (con/senza anno nel nome, profondità diversa, maiuscole diverse) ed è rimasta volutamente
    /// inline in C#: forzarla in questo schema JSON avrebbe richiesto generalizzazioni non
    /// richieste da questo intervento e avrebbe aumentato il rischio di regressione senza un
    /// beneficio proporzionato. Non sono stati toccati i percorsi di VerificheViewModel né la
    /// risoluzione della cartella di rete in HomeViewModel.GetLogDumpReteBasePath: sono scope
    /// futuri, non di questo intervento (vedi PROJECT_MEMORY.md §6).
    /// </para>
    /// </summary>
    public static class HitachiPathsManager
    {
        private static readonly JsonSerializerOptions _jsonOptions = new() { WriteIndented = true };

        // Stessa cache "solo testo, validata su mtime + dimensione" già usata da
        // DestinatariManager e ShortcutsManager: evita di rileggere il file da disco a ogni
        // Sposta/Riporta Report.
        private static readonly object _cacheLock = new();
        private static string? _cachedJson;
        private static DateTime _cachedWriteTimeUtc;
        private static long _cachedLength = -1;

        private static string ConfigFilePath => AppPaths.DataFile("hitachi_paths.json");

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

        public static List<HitachiPathConfig> LoadConfig()
        {
            string path = ConfigFilePath;
            if (!File.Exists(path))
            {
                var defaultConfig = CreateDefaultConfig();
                SaveConfig(defaultConfig);
                return defaultConfig;
            }

            try
            {
                string? json = ReadConfigJson(path);
                if (json == null) return CreateDefaultConfig();

                var config = JsonSerializer.Deserialize<List<HitachiPathConfig>>(json);
                return (config == null || config.Count == 0) ? CreateDefaultConfig() : config;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Errore lettura hitachi_paths.json: {ex.Message}");
                return CreateDefaultConfig();
            }
        }

        public static void SaveConfig(List<HitachiPathConfig> config)
        {
            try
            {
                string json = JsonSerializer.Serialize(config, _jsonOptions);
                File.WriteAllText(ConfigFilePath, json);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Errore salvataggio hitachi_paths.json: {ex.Message}");
            }
            finally
            {
                lock (_cacheLock)
                {
                    _cachedJson = null;
                    _cachedLength = -1;
                    _cachedWriteTimeUtc = default;
                }
            }
        }

        /// <summary>
        /// Restituisce il percorso completo della cartella Hitachi base per <paramref name="train"/>,
        /// oppure <see langword="null"/> se il treno non è presente in configurazione — lo stesso
        /// esito del <c>return;</c> senza operazioni del ramo <c>else</c> nel codice originale.
        /// </summary>
        public static string? GetHitachiDir(string userProfile, string? train)
        {
            if (string.IsNullOrEmpty(train)) return null;

            var config = LoadConfig();
            var match = config.FirstOrDefault(c => c.Train.Equals(train, StringComparison.Ordinal));
            if (match == null || match.RelativePath.Count == 0) return null;

            var segments = new string[match.RelativePath.Count + 1];
            segments[0] = userProfile;
            match.RelativePath.CopyTo(segments, 1);
            return Path.Combine(segments);
        }

        /// <summary>
        /// Sottocartella "vecchi report" dentro la cartella Hitachi base del treno, dove
        /// <c>ExcelViewModel.ExecuteSpostaReport</c> ("Sposta Report") archivia il report sostituito
        /// prima di caricarne uno nuovo. <see langword="null"/> se il treno non è configurato o non è
        /// una delle quattro flotte che EXCEL gestisce.
        ///
        /// <para>
        /// <b>Perché è qui e non più solo inline in <c>ExecuteSpostaReport</c>.</b> La struttura non è
        /// uniforme da treno a treno (con/senza anno nel nome, profondità diversa, maiuscole diverse) —
        /// per questo alle origini di <see cref="HitachiPathsManager"/> era rimasta deliberatamente
        /// inline, "non abbastanza uniforme da meritare l'estrazione". È rimasta tale finché a
        /// consumarla è stato un solo chiamante. Da quando anche
        /// <c>PathHealthCheckService.EseguiControllo</c> (Sprint 17, §6.1-undevicies) deve conoscere
        /// questi stessi quattro percorsi per segnalarne lo stato, il rischio non è più solo estetico:
        /// due copie della stessa logica possono divergere in silenzio esattamente come questa classe
        /// esiste per evitare che accada ai percorsi Hitachi base.
        /// </para>
        /// </summary>
        /// <param name="anno">
        /// Anno da usare nei due nomi che lo includono (ETR700, E404P). Parametro esplicito — invece
        /// di leggere <c>DateTime.Now.Year</c> internamente — per restare una funzione pura e
        /// testabile in modo deterministico, senza dipendere da quando gira il chiamante o la suite.
        /// </param>
        public static string? GetReportOldFolder(string userProfile, string? train, int anno)
        {
            string? hitachiDir = GetHitachiDir(userProfile, train);
            if (hitachiDir == null) return null;

            return train switch
            {
                "ETR700" => Path.Combine(hitachiDir, "REPORT INTERVENTI ETR700 OLD", $"REPORT OLD ETR700 ANNO {anno}"),
                "E404P" => Path.Combine(hitachiDir, $"REPORT INTERVENTI OLD_ModifyYear{anno}"),
                "ETR1000 / 1000FH" => Path.Combine(hitachiDir, "OLD REPORT"),
                "ETR1000 I-F" => Path.Combine(hitachiDir, "OLD Report"),
                _ => null
            };
        }

        /// <summary>
        /// Valori identici a quelli finora scritti due volte, letteralmente, dentro
        /// <c>ExcelViewModel.ExecuteSpostaReport</c> ed <c>ExecuteRiportaReport</c>.
        /// </summary>
        private static List<HitachiPathConfig> CreateDefaultConfig()
        {
            return new List<HitachiPathConfig>
            {
                new() { Train = "ETR700", RelativePath = new List<string> { "Hitachi Group", "SSB_SST - INTERVENTI ETR700 ELO BL3" } },
                new() { Train = "E404P", RelativePath = new List<string> { "Hitachi Group", "SSB_SST - Interventi ETR500", "REPORT INTERVENTI NAPOLI - MILANO" } },
                new() { Train = "ETR1000 / 1000FH", RelativePath = new List<string> { "Hitachi Group", "SSB_SST - Interventi ETR1000" } },
                // "ETR1000 ITA-FR" (senza la "A" finale): nome reale della cartella su disco,
                // confermato dal committente dopo l'errore "Cartella Hitachi non trovata" — il
                // valore precedente, "ETR1000 ITA-FRA", non corrisponde a nessuna cartella esistente.
                new() { Train = "ETR1000 I-F", RelativePath = new List<string> { "Hitachi Group", "SSB_SST - Interventi ETR1000", "ETR1000 ITA-FR" } }
            };
        }
    }
}
