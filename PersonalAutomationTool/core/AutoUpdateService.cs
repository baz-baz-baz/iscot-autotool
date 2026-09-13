using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace PersonalAutomationTool.Core
{
    /// <summary>
    /// Auto-update zero-click all'avvio: confronta la versione dell'eseguibile in corso con l'ultima
    /// release pubblicata su GitHub e, se più recente, la scarica e sostituisce il binario da sola,
    /// senza alcuna conferma dell'utente.
    ///
    /// <para>
    /// <b>Perché un helper esterno e non un semplice <c>File.Copy</c>.</b> Windows blocca la scrittura
    /// su un eseguibile mentre è in esecuzione (il file è mappato in memoria dal loader). L'unico modo
    /// per sostituirlo è farlo da un <b>altro</b> processo, dopo che questo si è chiuso: da qui lo
    /// script <see cref="ScriptHotSwap"/>, lanciato appena prima di uscire, che attende la terminazione
    /// per PID, copia il nuovo file al posto del vecchio e riavvia.
    /// </para>
    ///
    /// <para>
    /// <b>Perché un file <c>.cmd</c> e non uno script PowerShell.</b> Le workstation d'officina a cui
    /// è destinata la distribuzione stand-alone (§6.1-duodevicies di PROJECT_MEMORY.md) sono macchine
    /// aziendali che possono avere criteri di esecuzione PowerShell più restrittivi (Restricted/
    /// AllSigned): uno script <c>.cmd</c> lanciato da <c>cmd.exe</c> non incontra quel vincolo ed è
    /// meno probabile che venga bloccato da un criterio di gruppo.
    /// </para>
    ///
    /// <para>
    /// <b>Nessun'interazione con il pattern istanza singola</b> (<see cref="SingleInstanceGuard"/>,
    /// §6.1-vicies-bis): questa classe non conosce il <c>Mutex</c>, non lo tocca. È <c>App.xaml.cs</c>
    /// a disporre esplicitamente della guardia subito prima di uscire quando l'aggiornamento è pronto,
    /// così il nuovo processo — avviato dall'helper solo dopo che questo è già terminato — trova il
    /// nome del mutex libero e diventa lui stesso l'istanza primaria. La prova che questo funziona è
    /// già in <c>SingleInstanceGuardTests.DopoIlDisposeDellaPrima_UnaNuovaIstanzaPuoDiventarePrimaria</c>:
    /// il rilascio esplicito del mutex rende immediatamente possibile una nuova istanza primaria con lo
    /// stesso nome.
    /// </para>
    /// </summary>
    public static class AutoUpdateService
    {
        private const string RepoOwner = "baz-baz-baz";
        private const string RepoName = "iscot-autotool";
        private const string NomeEseguibile = "PersonalAutomationTool.exe";

        private static readonly TimeSpan TimeoutControlloVersione = TimeSpan.FromSeconds(4);
        private static readonly TimeSpan TimeoutDownload = TimeSpan.FromMinutes(5);

        private static readonly HttpClient HttpClient = new() { Timeout = Timeout.InfiniteTimeSpan };

        private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

        /// <summary>
        /// Script di sostituzione a caldo, scritto su disco al momento del download e lanciato appena
        /// prima che questo processo esca. Tre argomenti posizionali: PID di questo processo, percorso
        /// del nuovo eseguibile scaricato, percorso dell'eseguibile da sostituire.
        ///
        /// <para>
        /// Entrambi i cicli di attesa (terminazione del processo, poi disponibilità del file per la
        /// copia — l'antivirus o un handle in chiusura possono tenerlo occupato per una frazione di
        /// secondo dopo l'uscita) sono limitati (30 e 15 tentativi da 1s) per non restare bloccati
        /// all'infinito in un caso limite: se il timeout scatta, l'ultima istruzione tenta comunque di
        /// avviare l'eseguibile presente in quel momento a <c>TARGETEXE</c>, piuttosto che lasciare la
        /// macchina senza applicazione avviabile.
        /// </para>
        ///
        /// <para>
        /// L'ultima riga è l'idioma classico batch per l'auto-cancellazione: <c>cmd.exe</c> carica
        /// l'intero file prima di eseguirlo, quindi può cancellare se stesso mentre l'istruzione
        /// <c>goto</c> fallisce silenziosamente sul file ormai rimosso.
        /// </para>
        /// </summary>
        private const string ScriptHotSwap = """
            @echo off
            setlocal EnableDelayedExpansion
            set "PID=%~1"
            set "NEWEXE=%~2"
            set "TARGETEXE=%~3"

            set COUNT=0
            :waitloop
            tasklist /FI "PID eq %PID%" 2>NUL | find /I "%PID%" >NUL
            if not errorlevel 1 (
                set /a COUNT+=1
                if !COUNT! GEQ 30 goto afterwait
                timeout /t 1 /nobreak >NUL
                goto waitloop
            )
            :afterwait

            set COUNT=0
            :copyloop
            copy /y "%NEWEXE%" "%TARGETEXE%" >NUL 2>&1
            if errorlevel 1 (
                set /a COUNT+=1
                if !COUNT! GEQ 15 goto aftercopy
                timeout /t 1 /nobreak >NUL
                goto copyloop
            )
            :aftercopy

            start "" "%TARGETEXE%"
            del "%NEWEXE%" >NUL 2>&1
            (goto) 2>nul & del "%~f0"
            """;

        /// <summary>
        /// Controlla se esiste una release più recente su GitHub e, se sì, la scarica e predispone la
        /// sostituzione a caldo. Non lancia mai un'eccezione: qualunque errore (rete assente, GitHub
        /// irraggiungibile, timeout, risposta inattesa) viene inghiottito e trattato come "nessun
        /// aggiornamento disponibile", perché un controllo di aggiornamento non deve mai impedire
        /// l'avvio normale dell'applicazione con l'ultima versione locale funzionante.
        /// </summary>
        /// <param name="progress">Messaggi testuali di stato per l'eventuale UI di caricamento.</param>
        /// <param name="cancellationToken">Permette al chiamante di annullare il controllo.</param>
        /// <returns>
        /// <c>true</c> se un aggiornamento è stato scaricato e l'helper di sostituzione è stato
        /// avviato: il chiamante deve rilasciare le risorse esclusive (il <c>Mutex</c> dell'istanza
        /// singola) e terminare immediatamente, <b>senza</b> aprire la finestra principale.
        /// <c>false</c> in ogni altro caso: nessun aggiornamento, o il controllo non è andato a buon
        /// fine — l'avvio prosegue normalmente con la versione locale.
        /// </returns>
        public static async Task<bool> VerificaEAggiornaAsync(IProgress<string>? progress = null, CancellationToken cancellationToken = default)
        {
            try
            {
                string? eseguibileCorrente = Environment.ProcessPath;
                if (eseguibileCorrente is null ||
                    !string.Equals(Path.GetFileName(eseguibileCorrente), NomeEseguibile, StringComparison.OrdinalIgnoreCase))
                {
                    // dotnet run, test host, o comunque non il pacchetto pubblicato single-file: non
                    // esiste un .exe stand-alone da sostituire.
                    return false;
                }

                progress?.Report("Verifica aggiornamenti...");

                GitHubReleaseDto? release = await ScaricaUltimaReleaseAsync(cancellationToken).ConfigureAwait(false);
                if (release?.TagName is null) return false;

                Version versioneLocale = Assembly.GetExecutingAssembly().GetName().Version ?? new Version(0, 0, 0, 0);
                if (!IsRemoteVersionNewer(release.TagName, versioneLocale)) return false;

                GitHubReleaseAssetDto? asset = release.Assets?.FirstOrDefault(a =>
                    a.Name is not null && a.Name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase));
                if (asset?.BrowserDownloadUrl is null) return false;

                string cartellaUpdate = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "PersonalAutomationTool", "updates");
                Directory.CreateDirectory(cartellaUpdate);
                RimuoviFileResidui(cartellaUpdate);

                string nuovoEseguibile = Path.Combine(cartellaUpdate, "update_" + NomeEseguibile);
                await ScaricaFileAsync(asset.BrowserDownloadUrl, nuovoEseguibile, progress, cancellationToken).ConfigureAwait(false);

                string scriptPath = Path.Combine(cartellaUpdate, "apply_update.cmd");
                await File.WriteAllTextAsync(scriptPath, ScriptHotSwap, Encoding.ASCII, cancellationToken).ConfigureAwait(false);

                AvviaHelperESostituzione(scriptPath, nuovoEseguibile, eseguibileCorrente);
                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Controllo aggiornamenti non riuscito, si prosegue con la versione locale: {ex.Message}");
                return false;
            }
        }

        private static async Task<GitHubReleaseDto?> ScaricaUltimaReleaseAsync(CancellationToken cancellationToken)
        {
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(TimeoutControlloVersione);

            using var request = new HttpRequestMessage(HttpMethod.Get,
                $"https://api.github.com/repos/{RepoOwner}/{RepoName}/releases/latest");
            // GitHub rifiuta le richieste API prive di User-Agent con 403.
            request.Headers.UserAgent.ParseAdd("iscot-autotool-updater");
            request.Headers.Accept.ParseAdd("application/vnd.github+json");

            using HttpResponseMessage risposta = await HttpClient.SendAsync(request, timeoutCts.Token).ConfigureAwait(false);
            if (!risposta.IsSuccessStatusCode) return null;

            await using Stream contenuto = await risposta.Content.ReadAsStreamAsync(timeoutCts.Token).ConfigureAwait(false);
            return await JsonSerializer.DeserializeAsync<GitHubReleaseDto>(contenuto, JsonOptions, timeoutCts.Token).ConfigureAwait(false);
        }

        private static async Task ScaricaFileAsync(string url, string percorsoDestinazione, IProgress<string>? progress, CancellationToken cancellationToken)
        {
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(TimeoutDownload);

            using HttpResponseMessage risposta = await HttpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, timeoutCts.Token).ConfigureAwait(false);
            risposta.EnsureSuccessStatusCode();

            long? totale = risposta.Content.Headers.ContentLength;
            await using Stream sorgente = await risposta.Content.ReadAsStreamAsync(timeoutCts.Token).ConfigureAwait(false);
            await using FileStream destinazione = File.Create(percorsoDestinazione);

            var buffer = new byte[81920];
            long scaricati = 0;
            int ultimaPercentualeSegnalata = -1;
            int letti;

            while ((letti = await sorgente.ReadAsync(buffer, timeoutCts.Token).ConfigureAwait(false)) > 0)
            {
                await destinazione.WriteAsync(buffer.AsMemory(0, letti), timeoutCts.Token).ConfigureAwait(false);
                scaricati += letti;

                if (totale is > 0 && progress is not null)
                {
                    int percentuale = (int)(scaricati * 100 / totale.Value);
                    if (percentuale != ultimaPercentualeSegnalata)
                    {
                        ultimaPercentualeSegnalata = percentuale;
                        progress.Report($"Aggiornamento all'ultima versione in corso... {percentuale}%");
                    }
                }
            }
        }

        /// <summary>
        /// Ripulisce eventuali residui di un tentativo di aggiornamento precedente (es. l'helper si è
        /// auto-cancellato ma il download interrotto era rimasto). Best-effort: un file che non si
        /// riesce a cancellare (ancora in uso) non deve impedire il nuovo tentativo.
        /// </summary>
        private static void RimuoviFileResidui(string cartellaUpdate)
        {
            foreach (string file in Directory.EnumerateFiles(cartellaUpdate))
            {
                try
                {
                    File.Delete(file);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Pulizia del file residuo '{file}' non riuscita: {ex.Message}");
                }
            }
        }

        private static void AvviaHelperESostituzione(string scriptPath, string nuovoEseguibile, string eseguibileTarget)
        {
            var psi = new ProcessStartInfo
            {
                FileName = scriptPath,
                Arguments = $"{Environment.ProcessId} \"{nuovoEseguibile}\" \"{eseguibileTarget}\"",
                UseShellExecute = true,
                WindowStyle = ProcessWindowStyle.Hidden,
                CreateNoWindow = true,
            };
            Process.Start(psi);
        }

        /// <summary>
        /// Confronta il tag di una release GitHub (es. <c>"v1.2.0"</c> o <c>"1.2.0"</c>) con la
        /// versione dell'eseguibile locale. Un tag non interpretabile come versione viene trattato come
        /// "nessun aggiornamento" — mai come un'eccezione che interromperebbe l'avvio.
        ///
        /// <para>
        /// <b>Normalizzazione a quattro componenti prima del confronto.</b> <see cref="Version"/>
        /// tratta i componenti non specificati (Build/Revision) come <c>-1</c>, non come <c>0</c>: senza
        /// normalizzare, <c>Version.Parse("1.2.0")</c> (Revision <c>-1</c>) risulterebbe "minore" di
        /// <c>Version.Parse("1.2.0.0")</c> (Revision <c>0</c>) pur essendo la stessa versione — un tag
        /// GitHub a tre componenti apparirebbe sempre "più vecchio" della versione a quattro componenti
        /// che l'assembly riporta di default, innescando un aggiornamento ridondante a ogni avvio.
        /// </para>
        /// </summary>
        internal static bool IsRemoteVersionNewer(string? remoteTag, Version versioneLocale)
        {
            if (!TryParseVersion(remoteTag, out Version? versioneRemota) || versioneRemota is null) return false;

            return NormalizzaAQuattroComponenti(versioneRemota) > NormalizzaAQuattroComponenti(versioneLocale);
        }

        private static Version NormalizzaAQuattroComponenti(Version v) =>
            new(Math.Max(v.Major, 0), Math.Max(v.Minor, 0), Math.Max(v.Build, 0), Math.Max(v.Revision, 0));

        /// <summary>
        /// Estrae una <see cref="Version"/> da un tag SemVer-ish: tollera il prefisso <c>"v"</c> e
        /// ignora un eventuale suffisso di pre-release/build metadata (<c>"-beta"</c>, <c>"+001"</c>) —
        /// per questo repository i tag di release sono sempre numerici puri, ma la tolleranza costa
        /// nulla ed evita un falso "formato non valido" se in futuro comparisse un tag di pre-release.
        /// </summary>
        internal static bool TryParseVersion(string? tag, out Version? versione)
        {
            versione = null;
            if (string.IsNullOrWhiteSpace(tag)) return false;

            string ripulito = tag.Trim();
            if (ripulito.StartsWith("v", StringComparison.OrdinalIgnoreCase)) ripulito = ripulito[1..];

            int fineParteNumerica = ripulito.IndexOfAny(['-', '+']);
            if (fineParteNumerica >= 0) ripulito = ripulito[..fineParteNumerica];

            return Version.TryParse(ripulito, out versione);
        }

        private sealed class GitHubReleaseDto
        {
            [JsonPropertyName("tag_name")]
            public string? TagName { get; set; }

            [JsonPropertyName("assets")]
            public GitHubReleaseAssetDto[]? Assets { get; set; }
        }

        private sealed class GitHubReleaseAssetDto
        {
            [JsonPropertyName("name")]
            public string? Name { get; set; }

            [JsonPropertyName("browser_download_url")]
            public string? BrowserDownloadUrl { get; set; }
        }
    }
}
