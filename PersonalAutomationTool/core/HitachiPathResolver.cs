using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace PersonalAutomationTool.Core
{
    /// <summary>
    /// Trova dove vive davvero, sulla macchina del tecnico, la cartella radice sincronizzata
    /// <c>"Hitachi Group"</c> — da cui <see cref="HitachiPathsManager"/>, <c>VerifichePathsManager</c> e
    /// <c>HomeViewModel.GetLogDumpReteBasePath</c> derivano tutti i percorsi operativi (Report
    /// Interventi, Verifiche, Log Dump in rete).
    ///
    /// <para>
    /// <b>Il difetto che questa classe corregge.</b> I tre consumatori sopra assumevano tutti la stessa
    /// cosa: <c>%USERPROFILE%\Hitachi Group</c>. È rimasto vero finché il client OneDrive/SharePoint di
    /// ogni tecnico sincronizzava allo stesso modo — smette di esserlo appena qualcuno sposta la
    /// sincronizzazione sul Desktop (<c>%USERPROFILE%\Desktop\Hitachi Group</c>, il caso reale
    /// verificato: "Verifica Percorsi Hitachi" segnalava ERRORE ovunque perché cercava la cartella nel
    /// posto sbagliato, mentre esisteva un livello sotto). Da qui la richiesta di provare più candidati
    /// invece di uno fisso, con lo stesso spirito di
    /// <c>HomeViewModel.GetLogDumpReteBasePath</c> — che già scansionava le variabili d'ambiente
    /// <c>OneDrive*</c> per lo stesso motivo, ma solo per sé stesso: qui la stessa idea diventa
    /// condivisa, invece di essere riscritta una terza volta per Hitachi/Verifiche.
    /// </para>
    ///
    /// <para>
    /// <b>Sola lettura, senza eccezioni.</b> Ogni metodo qui dentro chiama solo
    /// <see cref="Directory.Exists"/>/<see cref="Directory.GetDirectories(string, string)"/>: mai
    /// <see cref="Directory.CreateDirectory"/> o equivalenti. Un candidato che non esiste viene
    /// semplicemente scartato — la responsabilità di segnalarlo come "non trovato" resta a
    /// <c>PathHealthCheckService</c>, che verifica il percorso finale già risolto, esattamente come
    /// faceva prima con il percorso fisso.
    /// </para>
    /// </summary>
    public static class HitachiPathResolver
    {
        /// <summary>Nome reale della cartella radice sincronizzata, uguale su tutte le installazioni.</summary>
        public const string NomeCartellaRadice = "Hitachi Group";

        private static readonly string[] VariabiliOneDrive = ["OneDriveCommercial", "OneDrive", "OneDriveConsumer"];

        private static string ConfigFilePath => AppPaths.DataFile("paths_config.json");

        /// <summary>
        /// Risolve la radice <c>"Hitachi Group"</c> attiva su questa macchina, oppure
        /// <see langword="null"/> se non esiste in nessuna delle posizioni candidate. Priorità:
        /// override manuale (<c>paths_config.json</c>) più le posizioni note, nell'ordine restituito da
        /// <see cref="CandidateBaseRoots"/> — Desktop prima di <c>%USERPROFILE%</c>, perché è la
        /// sincronizzazione più recente e più probabile secondo il caso reale che ha motivato
        /// l'intervento.
        /// </summary>
        public static string? ResolveRoot()
        {
            string? overrideManuale = LeggiOverrideManuale();
            if (!string.IsNullOrWhiteSpace(overrideManuale) && Directory.Exists(overrideManuale))
            {
                return overrideManuale;
            }

            return TrovaRadiceFraCandidate(CandidateBaseRoots());
        }

        /// <summary>
        /// Cartelle "base" candidate a contenere <c>"Hitachi Group"</c>, nell'ordine di priorità della
        /// richiesta: Desktop, profilo utente, <c>%USERPROFILE%\Desktop</c> esplicito (di norma coincide
        /// col Desktop speciale, ma non quando quest'ultimo è reindirizzato da criteri aziendali), le
        /// variabili d'ambiente OneDrive note, infine qualunque cartella <c>OneDrive*</c> sotto il
        /// profilo (copre il caso "OneDrive - NomeAzienda", il cui suffisso non è prevedibile).
        ///
        /// <para>
        /// Pubblico e non solo interno perché <c>HomeViewModel.GetLogDumpReteBasePath</c> lo riusa per
        /// evitare di duplicare la scansione OneDrive di nuovo — quel chiamante prova comunque due
        /// sotto-percorsi diversi per radice (con e senza <c>"Hitachi Group"</c> in mezzo), quindi non
        /// può limitarsi a chiamare <see cref="ResolveRoot"/>.
        /// </para>
        /// </summary>
        public static IReadOnlyList<string> CandidateBaseRoots()
        {
            string profiloUtente = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile, Environment.SpecialFolderOption.DoNotVerify);

            // AppConfig.RisolviDesktop() solleva InvalidOperationException solo se né il Desktop né il
            // profilo utente sono risolvibili: un'evenienza che avrebbe già impedito l'avvio
            // dell'applicazione in AppConfig.Initialize() (chiamato prima di qualunque finestra). Se il
            // codice arriva fin qui quella chiamata non ha mai lanciato — il try/catch resta comunque,
            // coerente con "sola lettura, senza eccezioni" di questa classe.
            string desktop;
            try
            {
                desktop = AppConfig.RisolviDesktop();
            }
            catch (InvalidOperationException)
            {
                desktop = string.Empty;
            }

            var radiciOneDrive = VariabiliOneDrive
                .Select(Environment.GetEnvironmentVariable)
                .Where(v => !string.IsNullOrEmpty(v))
                .Select(v => v!);

            return ComponiRadiciCandidate(profiloUtente, desktop, radiciOneDrive, TrovaCartelleOneDriveWildcard(profiloUtente));
        }

        /// <summary>
        /// Combina l'elenco di sorgenti in un'unica lista ordinata e senza duplicati, senza toccare il
        /// disco: separata da <see cref="CandidateBaseRoots"/> per essere verificabile con input
        /// sintetici, senza dipendere dalle variabili d'ambiente reali della macchina che esegue i test.
        /// Un percorso non assoluto (cartella speciale non risolvibile, valore d'ambiente vuoto) viene
        /// scartato invece di produrre un candidato relativo alla cartella corrente.
        /// </summary>
        internal static IReadOnlyList<string> ComponiRadiciCandidate(
            string profiloUtente, string desktop, IEnumerable<string> radiciOneDrive, IEnumerable<string> radiciOneDriveWildcard)
        {
            var radici = new List<string>();

            if (Path.IsPathFullyQualified(desktop)) radici.Add(desktop);

            if (Path.IsPathFullyQualified(profiloUtente))
            {
                radici.Add(profiloUtente);
                radici.Add(Path.Combine(profiloUtente, "Desktop"));
            }

            foreach (string radice in radiciOneDrive.Concat(radiciOneDriveWildcard))
            {
                if (Path.IsPathFullyQualified(radice)) radici.Add(radice);
            }

            return radici.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        }

        /// <summary>
        /// Prima cartella <c>"Hitachi Group"</c> esistente fra le radici candidate, nell'ordine dato.
        /// Unico punto che tocca il disco per la risoluzione della radice: separato da
        /// <see cref="ComponiRadiciCandidate"/> per poter essere verificato con cartelle vere su disco
        /// (Tier 2), senza replicare candidati sintetici irraggiungibili.
        /// </summary>
        internal static string? TrovaRadiceFraCandidate(IEnumerable<string> radiciBase)
        {
            foreach (string radiceBase in radiciBase)
            {
                string candidato = Path.Combine(radiceBase, NomeCartellaRadice);
                if (Directory.Exists(candidato)) return candidato;
            }

            return null;
        }

        /// <summary>
        /// Combina la radice già risolta (se trovata) con il resto dei segmenti di
        /// <paramref name="segmentiRelativi"/>, quando il primo segmento è letteralmente
        /// <c>"Hitachi Group"</c> — la forma con cui <c>hitachi_paths.json</c> e
        /// <c>verifiche_paths.json</c> la registrano da sempre, invariata da questo intervento per non
        /// rompere le configurazioni già scritte sui PC dei tecnici.
        ///
        /// <para>
        /// <b>Fallback invariato rispetto al comportamento precedente</b> quando <paramref name="radiceRisolta"/>
        /// è <see langword="null"/> (radice non trovata da nessuna parte) o il primo segmento non è
        /// <c>"Hitachi Group"</c>: <c>Path.Combine(userProfile, tutti i segmenti)</c>, esattamente come
        /// facevano <c>HitachiPathsManager.GetHitachiDir</c> e <c>VerifichePathsManager.Combina</c> prima
        /// di questo intervento. Necessario perché il chiamante (tipicamente
        /// <c>PathHealthCheckService</c>) continui a mostrare un percorso plausibile — quello dove
        /// l'app si sarebbe aspettata di trovarla — invece di <see langword="null"/>, quando la
        /// cartella non è raggiungibile da nessuna parte.
        /// </para>
        /// </summary>
        public static string CombinaSottoPercorso(string userProfile, string? radiceRisolta, IReadOnlyList<string> segmentiRelativi)
        {
            if (radiceRisolta != null && segmentiRelativi.Count > 0 &&
                segmentiRelativi[0].Equals(NomeCartellaRadice, StringComparison.OrdinalIgnoreCase))
            {
                string[] resto = [.. segmentiRelativi.Skip(1)];
                return resto.Length == 0 ? radiceRisolta : Path.Combine([radiceRisolta, .. resto]);
            }

            return Path.Combine([userProfile, .. segmentiRelativi]);
        }

        private static string[] TrovaCartelleOneDriveWildcard(string profiloUtente)
        {
            if (!Path.IsPathFullyQualified(profiloUtente) || !Directory.Exists(profiloUtente)) return [];

            try
            {
                return Directory.GetDirectories(profiloUtente, "OneDrive*");
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                Debug.WriteLine($"Scansione delle cartelle OneDrive* non riuscita: {ex.Message}");
                return [];
            }
        }

        /// <summary>
        /// Override manuale opzionale per la macchina segnalata come eccezione (nessun percorso noto
        /// funziona): un tecnico o l'assistenza scrive a mano <c>paths_config.json</c> con la radice
        /// esatta, senza dover attendere una nuova release. Nessuna UI la scrive ancora — è
        /// deliberatamente un file letto, non gestito da un dialog, come richiesto: l'unico caso d'uso
        /// previsto è un intervento mirato di supporto.
        /// </summary>
        internal static string? LeggiOverrideManuale()
        {
            try
            {
                string percorso = ConfigFilePath;
                if (!File.Exists(percorso)) return null;

                string json = File.ReadAllText(percorso);
                var config = JsonSerializer.Deserialize<PathsOverrideConfig>(json);
                return string.IsNullOrWhiteSpace(config?.HitachiGroupRootOverride) ? null : config.HitachiGroupRootOverride;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Lettura di paths_config.json non riuscita: {ex.Message}");
                return null;
            }
        }
    }

    /// <summary>Schema di <c>paths_config.json</c>: un solo campo opzionale, per ora.</summary>
    internal sealed class PathsOverrideConfig
    {
        public string? HitachiGroupRootOverride { get; set; }
    }
}
