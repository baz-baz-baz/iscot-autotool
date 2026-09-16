using System;
using System.Diagnostics;
using System.IO;
using System.Text;

namespace PersonalAutomationTool.Core
{
    /// <summary>
    /// "Reset a fabbrica": riporta l'applicazione esattamente allo stato di un primo avvio su un PC
    /// nuovo — cartella dati assente, quindi <see cref="AppPaths.Initialize"/> che la ricrea dai seed
    /// incorporati in questa release — e riavvia l'eseguibile.
    ///
    /// <para>
    /// <b>Perché serve.</b> I meccanismi di allineamento incrementale
    /// (<c>DatabaseSeedSyncService</c> per le tabelle master, <c>DestinatariManager.ApplyKnownRecipientUpdates</c>
    /// per <c>destinatari.json</c>) aggiornano il PC del tecnico solo nei casi previsti: il primo
    /// confronta <c>PRAGMA user_version</c>, il secondo riconosce un'azione solo se il suo valore
    /// coincide <b>esattamente</b> con il vecchio default noto. Un file locale divergente da entrambi —
    /// una personalizzazione dimenticata, una versione mai censita, un aggiornamento saltato — resta
    /// fermo per sempre senza che nessuno se ne accorga. Questo comando è la via d'uscita che non
    /// richiede di collegarsi alla macchina: azzera lo stato locale e lascia ripartire l'applicazione
    /// dai dati della release in corso.
    /// </para>
    ///
    /// <para>
    /// <b>Perché un helper <c>.cmd</c> esterno e non un <c>Directory.Delete</c> qui.</b> Stessa ragione
    /// di <see cref="AutoUpdateService"/>: finché il processo è vivo tiene aperti i file che deve
    /// rimuovere (gli handle SQLite di <c>train_software.db</c> ed <c>emails.db</c>, il log di
    /// <see cref="CrashReporter"/>). L'unico momento sicuro è <b>dopo</b> l'uscita, quindi da un altro
    /// processo: lo script attende la fine di questo PID, sposta la cartella e riavvia. Anche il
    /// formato <c>.cmd</c> è la stessa scelta — le workstation d'officina possono avere criteri
    /// PowerShell restrittivi, <c>cmd.exe</c> no.
    /// </para>
    ///
    /// <para>
    /// <b>Perché <c>move</c> e non <c>rmdir /s /q</c>.</b> Una cancellazione ricorsiva è irreversibile e
    /// porterebbe via anche lo storico locale del tecnico (<c>renamer_log</c>) insieme ai dati da
    /// azzerare. Rinominare la cartella con un suffisso datato ha per l'utente lo stesso effetto —
    /// all'avvio successivo la cartella dati non esiste e viene ricreata da zero — ma è istantaneo
    /// (nessuna copia), ed è recuperabile se qualcosa di importante era lì dentro. Se lo spostamento
    /// non riesce nemmeno dopo i tentativi previsti, l'applicazione riparte con i dati intatti: il
    /// fallimento peggiore è "il reset non è avvenuto", mai "i dati sono spariti a metà".
    /// </para>
    /// </summary>
    public static class FactoryResetService
    {
        private const string SuffissoBackup = "-backup-";

        /// <summary>
        /// Quattro argomenti posizionali: PID di questo processo, cartella dati da spostare, percorso
        /// di destinazione del backup, eseguibile da riavviare. Struttura e limiti dei cicli di attesa
        /// ricalcano <c>AutoUpdateService.ScriptHotSwap</c>, incluso l'idioma finale di
        /// auto-cancellazione.
        /// </summary>
        private const string ScriptReset = """
            @echo off
            setlocal EnableDelayedExpansion
            set "PID=%~1"
            set "DATAFOLDER=%~2"
            set "BACKUPFOLDER=%~3"
            set "TARGETEXE=%~4"

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
            :moveloop
            move "%DATAFOLDER%" "%BACKUPFOLDER%" >NUL 2>&1
            if errorlevel 1 (
                set /a COUNT+=1
                if !COUNT! GEQ 15 goto aftermove
                timeout /t 1 /nobreak >NUL
                goto moveloop
            )
            :aftermove

            start "" "%TARGETEXE%"
            (goto) 2>nul & del "%~f0"
            """;

        /// <summary>
        /// Prepara il reset e avvia l'helper: da qui in poi il chiamante deve chiudere l'applicazione
        /// (<c>Application.Current.Shutdown()</c>) senza altre scritture sulla cartella dati.
        /// Restituisce il percorso della copia di sicurezza, da mostrare all'utente.
        /// </summary>
        /// <exception cref="InvalidOperationException">
        /// Se la cartella dati non è riconoscibile come quella dell'applicazione, o se non si riesce a
        /// determinare l'eseguibile da riavviare: in entrambi i casi nulla è stato ancora toccato.
        /// </exception>
        public static string AvviaReset()
        {
            string cartellaDati = AppPaths.DataFolder;
            if (!CartellaResettabile(cartellaDati))
            {
                throw new InvalidOperationException(
                    $"La cartella dati '{cartellaDati}' non è riconosciuta come cartella dell'applicazione: reset annullato.");
            }

            string? eseguibile = Environment.ProcessPath;
            if (string.IsNullOrEmpty(eseguibile))
            {
                throw new InvalidOperationException(
                    "Impossibile determinare l'eseguibile da riavviare: reset annullato.");
            }

            DateTime adesso = DateTime.Now;
            string cartellaBackup = ComponiPercorsoBackup(cartellaDati, adesso);

            SpostaCartellaLegacy(adesso);

            // Deliberatamente il temporaneo di sistema e non AppPaths.TempFile, che vive DENTRO la
            // cartella dati: lo script verrebbe spostato via insieme ad essa mentre è in esecuzione.
            string script = ScriviScriptHelper(Path.GetTempPath());
            AvviaHelper(script, cartellaDati, cartellaBackup, eseguibile);

            return cartellaBackup;
        }

        /// <summary>
        /// Vero solo se <paramref name="cartella"/> è un percorso assoluto il cui ultimo segmento è
        /// quello della cartella applicativa (<c>iscot-autotool</c>, letto da
        /// <see cref="AppPaths.CartellaDatiPredefinita"/> per non duplicarne il nome). È la guardia che
        /// impedisce, in qualunque scenario di percorso non risolto, di passare all'helper una cartella
        /// qualsiasi — o una radice di volume — da spostare.
        /// </summary>
        internal static bool CartellaResettabile(string? cartella)
        {
            if (string.IsNullOrWhiteSpace(cartella)) return false;
            if (!Path.IsPathFullyQualified(cartella)) return false;

            string atteso = NomeUltimoSegmento(AppPaths.CartellaDatiPredefinita);
            return string.Equals(NomeUltimoSegmento(cartella), atteso, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Percorso della copia di sicurezza: stessa cartella padre, stesso nome più un suffisso
        /// datato. Deve restare <b>fuori</b> dalla cartella di partenza — <c>move</c> di una cartella
        /// dentro sé stessa fallisce.
        /// </summary>
        internal static string ComponiPercorsoBackup(string cartella, DateTime adesso)
        {
            string normalizzata = cartella.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            string padre = Path.GetDirectoryName(normalizzata)!;
            string nome = Path.GetFileName(normalizzata);

            return Path.Combine(padre, $"{nome}{SuffissoBackup}{adesso:yyyyMMddHHmmss}");
        }

        /// <summary>
        /// Scrive l'helper in <paramref name="cartellaTemp"/> con un nome univoco e lo restituisce.
        /// ASCII come <c>AutoUpdateService</c>: <c>cmd.exe</c> interpreta il file con la code page della
        /// console, non con UTF-8.
        /// </summary>
        internal static string ScriviScriptHelper(string cartellaTemp)
        {
            Directory.CreateDirectory(cartellaTemp);
            string percorso = Path.Combine(cartellaTemp, $"reset_fabbrica_{Guid.NewGuid():N}.cmd");
            File.WriteAllText(percorso, ScriptReset, Encoding.ASCII);
            return percorso;
        }

        /// <summary>
        /// Sposta da parte anche la cartella dati della 2.0.0 (<see cref="AppPaths.LegacyDataFolder"/>)
        /// se esiste ancora.
        ///
        /// <para>
        /// <b>Senza questo passo il reset sarebbe inefficace proprio sulle macchine che lo richiedono.</b>
        /// <see cref="AppPaths.Prepara"/> tratta quella cartella come prima origine di migrazione a
        /// <b>ogni</b> avvio: trovata la cartella dati vuota, vi ricopierebbe il vecchio
        /// <c>destinatari.json</c> e i vecchi <c>.db</c> — cioè esattamente i dati obsoleti che il reset
        /// doveva eliminare, al posto dei seed di questa release.
        /// </para>
        ///
        /// <para>
        /// Fatto qui e non nell'helper: è solo un'origine di lettura all'avvio, nessun handle aperto da
        /// questo processo. Best-effort come <c>DatabaseSeedSyncService.EseguiBackupPreventivo</c> — un
        /// fallimento non deve impedire il reset della cartella dati vera.
        /// </para>
        /// </summary>
        private static void SpostaCartellaLegacy(DateTime adesso)
        {
            string legacy = AppPaths.LegacyDataFolder;

            try
            {
                if (!Path.IsPathFullyQualified(legacy) || !Directory.Exists(legacy)) return;
                Directory.Move(legacy, ComponiPercorsoBackup(legacy, adesso));
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                Debug.WriteLine($"Spostamento della cartella legacy '{legacy}' non riuscito: {ex.Message}");
            }
        }

        private static void AvviaHelper(string scriptPath, string cartellaDati, string cartellaBackup, string eseguibile)
        {
            var psi = new ProcessStartInfo
            {
                FileName = scriptPath,
                Arguments = $"{Environment.ProcessId} \"{cartellaDati}\" \"{cartellaBackup}\" \"{eseguibile}\"",
                UseShellExecute = true,
                WindowStyle = ProcessWindowStyle.Hidden,
                CreateNoWindow = true,
            };
            Process.Start(psi);
        }

        private static string NomeUltimoSegmento(string percorso) =>
            Path.GetFileName(percorso.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
    }
}
