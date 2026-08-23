using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using PersonalAutomationTool.Core;
using Xunit;

namespace PersonalAutomationTool.Tests.Core
{
    /// <summary>
    /// Tier 2 con **lock reali del file system**: i test aprono davvero i file con le stesse
    /// combinazioni di <see cref="FileShare"/> usate dall'applicazione e verificano quali operazioni
    /// Windows consente. Nessun mock: la regola che il bug ha violato è una regola del sistema
    /// operativo, e simularla non dimostrerebbe nulla.
    /// </summary>
    public sealed class FileOperationRetryTests : IDisposable
    {
        private readonly DirectoryInfo _root = Directory.CreateTempSubdirectory("FileLock_");

        public void Dispose()
        {
            try { _root.Delete(recursive: true); } catch { /* best-effort cleanup */ }
        }

        private string CreateFile(string name, string content = "contenuto")
        {
            string path = Path.Combine(_root.FullName, name);
            File.WriteAllText(path, content);
            return path;
        }

        // -------------------------------------------------------------------------------
        // 1. La causa del bug: FileShare.ReadWrite non concede il diritto di eliminazione
        // -------------------------------------------------------------------------------

        /// <summary>
        /// Riproduce la causa esatta dell'errore intermittente su "Riporta report": un lettore
        /// aperto con <c>FileShare.ReadWrite</c> — la combinazione usata prima della correzione —
        /// impedisce lo spostamento del file, perché <see cref="File.Move(string,string)"/> richiede
        /// sul file di origine anche il diritto di **eliminazione**.
        /// </summary>
        [Fact]
        public void FileShareReadWrite_SenzaDelete_BloccaLoSpostamento()
        {
            string source = CreateFile("report.xlsx");
            string destination = Path.Combine(_root.FullName, "spostato.xlsx");

            using var reader = new FileStream(source, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);

            var exception = Assert.Throws<IOException>(() => File.Move(source, destination));
            Assert.True(FileOperationRetry.IsSharingViolation(exception),
                $"Attesa violazione di condivisione, ricevuto HResult 0x{exception.HResult:X8}.");
        }

        /// <summary>La correzione: aggiungendo <c>FileShare.Delete</c> lo spostamento riesce anche con il lettore aperto.</summary>
        [Fact]
        public void FileShareReadWriteDelete_ConsenteLoSpostamentoConLettoreAperto()
        {
            string source = CreateFile("report.xlsx");
            string destination = Path.Combine(_root.FullName, "spostato.xlsx");

            using (var reader = new FileStream(source, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
            {
                File.Move(source, destination);

                // Lo stream resta valido: l'handle segue il file, non il percorso.
                reader.Position = 0;
                using var streamReader = new StreamReader(reader, leaveOpen: true);
                Assert.Equal("contenuto", streamReader.ReadToEnd());
            }

            Assert.True(File.Exists(destination));
            Assert.False(File.Exists(source));
        }

        // -------------------------------------------------------------------------------
        // 2. Riconoscimento delle eccezioni
        // -------------------------------------------------------------------------------

        [Fact]
        public void IsSharingViolation_RiconosceIlCodiceDiCondivisione()
        {
            var sharing = new IOException("in uso") { HResult = FileOperationRetry.HResultSharingViolation };
            var locking = new IOException("bloccato") { HResult = FileOperationRetry.HResultLockViolation };

            Assert.True(FileOperationRetry.IsSharingViolation(sharing));
            Assert.True(FileOperationRetry.IsSharingViolation(locking));
        }

        [Fact]
        public void IsSharingViolation_NonRiconosceAltriErrori()
        {
            // Errori che non migliorano aspettando: devono propagarsi subito.
            Assert.False(FileOperationRetry.IsSharingViolation(new FileNotFoundException()));
            Assert.False(FileOperationRetry.IsSharingViolation(new UnauthorizedAccessException()));
            Assert.False(FileOperationRetry.IsSharingViolation(new DirectoryNotFoundException()));
            Assert.False(FileOperationRetry.IsSharingViolation(new InvalidOperationException()));
        }

        // -------------------------------------------------------------------------------
        // 3. Comportamento della riprova
        // -------------------------------------------------------------------------------

        [Fact]
        public async Task ExecuteAsync_RiescAlPrimoTentativo_NonRiprova()
        {
            int calls = 0;
            int retries = 0;

            await FileOperationRetry.ExecuteAsync(() => calls++, onRetry: (_, _) => retries++);

            Assert.Equal(1, calls);
            Assert.Equal(0, retries);
        }

        [Fact]
        public async Task ExecuteAsync_LockTemporaneo_RiescDopoIlRilascio()
        {
            // Lo scenario reale: il file è agganciato quando si tenta la prima volta e viene
            // rilasciato poco dopo, come fa il client di sincronizzazione.
            int attempts = 0;
            await FileOperationRetry.ExecuteAsync(
                () =>
                {
                    attempts++;
                    if (attempts < 3) throw new IOException("in uso") { HResult = FileOperationRetry.HResultSharingViolation };
                },
                initialDelayMs: 10);

            Assert.Equal(3, attempts);
        }

        [Fact]
        public async Task ExecuteAsync_LockPermanente_PropagaLEccezioneDopoITentativiPrevisti()
        {
            int attempts = 0;

            var exception = await Assert.ThrowsAsync<IOException>(() =>
                FileOperationRetry.ExecuteAsync(
                    () =>
                    {
                        attempts++;
                        throw new IOException("sempre in uso") { HResult = FileOperationRetry.HResultSharingViolation };
                    },
                    maxAttempts: 4,
                    initialDelayMs: 10));

            Assert.Equal(4, attempts);
            Assert.Equal("sempre in uso", exception.Message);
        }

        [Fact]
        public async Task ExecuteAsync_ErroreNonRiprovabile_PropagaSubitoSenzaAttendere()
        {
            int attempts = 0;
            var stopwatch = Stopwatch.StartNew();

            await Assert.ThrowsAsync<FileNotFoundException>(() =>
                FileOperationRetry.ExecuteAsync(
                    () => { attempts++; throw new FileNotFoundException(); },
                    initialDelayMs: 5000));

            stopwatch.Stop();
            Assert.Equal(1, attempts);
            Assert.True(stopwatch.ElapsedMilliseconds < 1000,
                $"Un errore non riprovabile non deve attendere: attesi <1000ms, misurati {stopwatch.ElapsedMilliseconds}ms.");
        }

        [Fact]
        public async Task ExecuteAsync_BackoffEsponenziale_RaddoppiaLAttesa()
        {
            var delays = new System.Collections.Generic.List<int>();

            await Assert.ThrowsAsync<IOException>(() =>
                FileOperationRetry.ExecuteAsync(
                    () => throw new IOException("in uso") { HResult = FileOperationRetry.HResultSharingViolation },
                    maxAttempts: 4,
                    initialDelayMs: 10,
                    onRetry: (_, delayMs) => delays.Add(delayMs)));

            Assert.Equal([10, 20, 40], delays);
        }

        [Fact]
        public async Task ExecuteAsync_SuFileRealmenteBloccato_RiescQuandoIlLockVieneRilasciato()
        {
            // Prova end-to-end con un lock vero: un thread tiene il file aperto senza FileShare.Delete
            // e lo rilascia dopo un breve intervallo. Lo spostamento deve riuscire alla riprova.
            string source = CreateFile("bloccato.xlsx");
            string destination = Path.Combine(_root.FullName, "destinazione.xlsx");

            var locked = new ManualResetEventSlim(false);
            var released = new ManualResetEventSlim(false);
            var locker = Task.Run(() =>
            {
                using var stream = new FileStream(source, FileMode.Open, FileAccess.Read, FileShare.Read);
                locked.Set(); // il lock è ora realmente acquisito: sicuro avviare il tentativo di spostamento
                Thread.Sleep(250);
                released.Set();
            });

            // Senza questa attesa il primo tentativo di File.Move potrebbe correre più veloce
            // dell'apertura dello stream nel thread "locker" (race intermittente scoperta durante
            // questa sessione: il test falliva a intermittenza — FileNotFoundException o IOException
            // a seconda del timing esatto — perché lo spostamento riusciva PRIMA che il lock
            // esistesse, e il locker trovava poi il file già spostato).
            locked.Wait();

            await FileOperationRetry.ExecuteAsync(
                () => File.Move(source, destination),
                initialDelayMs: 100);

            await locker;
            Assert.True(released.IsSet);
            Assert.True(File.Exists(destination));
        }

        [Fact]
        public void Execute_VarianteSincrona_RiprovaComeQuellaAsincrona()
        {
            int attempts = 0;

            FileOperationRetry.Execute(
                () =>
                {
                    attempts++;
                    if (attempts < 2) throw new IOException("in uso") { HResult = FileOperationRetry.HResultSharingViolation };
                },
                initialDelayMs: 10);

            Assert.Equal(2, attempts);
        }

        // -------------------------------------------------------------------------------
        // 4. Messaggio diagnostico
        // -------------------------------------------------------------------------------

        [Fact]
        public void BuildDiagnosticMessage_ContieneNomeFilePercorsoECauseProbabili()
        {
            string path = @"C:\Users\tecnico\Desktop\LOG & DUMP\Report Interventi ETR700.xlsx";
            var exception = new IOException("Il processo non può accedere al file.");

            string message = FileOperationRetry.BuildDiagnosticMessage(path, exception);

            Assert.Contains("Report Interventi ETR700.xlsx", message);
            Assert.Contains(path, message);
            Assert.Contains("Excel", message);
            Assert.Contains("OneDrive", message);
            Assert.Contains(exception.Message, message);
        }
    }
}
