using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using ClosedXML.Excel;
using PersonalAutomationTool.Core;

namespace PersonalAutomationTool.Modules.Verifiche
{
    public class VerificheViewModel : ViewModelBase
    {
        private readonly List<FileSystemWatcher> _watchers = new();
        private readonly DispatcherTimer _refreshTimer;
        private readonly Dictionary<string, DateTime> _lastFileWriteTimes = new();
        private static readonly object _timerLock = new();
        private System.Threading.Timer? _debounceTimer;

        /// <summary>0 = nessuna scansione in corso, 1 = scansione in corso (guardia anti-rientranza).</summary>
        private int _isScanningFiles;

        private static readonly string[] PollingRelativePaths = [
            @"Hitachi Group\SSB_SST - Interventi ETR500",
            @"Hitachi Group\SSB_SST - INTERVENTI ETR700 ELO BL3",
            @"Hitachi Group\SSB_SST - Interventi ETR700",
            @"Hitachi Group\SSB_SST - Interventi ETR1000",
            @"Hitachi Group\SSB_SST - Interventi ETR1000FH"
        ];

        private static readonly string[] WatcherRelativePaths = [
            @"Hitachi Group\SSB_SST - Interventi ETR500",
            @"Hitachi Group\SSB_SST - INTERVENTI ETR700 ELO BL3",
            @"Hitachi Group\SSB_SST - Interventi ETR1000"
        ];

        public static VerificheViewModel? Instance { get; private set; }
        public static event Action? OnVerificheDataUpdated;

        public ObservableCollection<VerificheModel> VerificheList500 { get; } = [];
        public ObservableCollection<VerificheModel> VerificheList700 { get; } = [];
        public ObservableCollection<VerificheModel> VerificheList1000 { get; } = [];

        private bool _isBusy;
        /// <summary>Vero durante l'archiviazione di una verifica: la vista mostra l'overlay di attesa.</summary>
        public bool IsBusy
        {
            get => _isBusy;
            set => SetProperty(ref _isBusy, value);
        }

        private string _statoOperazione = string.Empty;
        public string StatoOperazione
        {
            get => _statoOperazione;
            set => SetProperty(ref _statoOperazione, value);
        }

        /// <summary>
        /// "Verifica Eseguita": archivia la riga nel foglio storico, la toglie dal foglio principale
        /// e rinomina il file. Il parametro è la <see cref="VerificheModel"/> della riga.
        /// </summary>
        public System.Windows.Input.ICommand VerificaEseguitaCommand { get; }

        public VerificheViewModel()
        {
            Instance = this;
            VerificaEseguitaCommand = new RelayCommand(
                async param => await EseguiVerificaEseguitaAsync(param as VerificheModel),
                _ => !IsBusy);

            _ = ReloadAllDataAsync();

            SetupWatchers();

            _refreshTimer = new DispatcherTimer
            {
                // Rete di sicurezza per gli eventi persi dai FileSystemWatcher già attivi
                // (SetupWatchers), non il canale primario di aggiornamento: a 5s la scansione
                // ricorsiva di 3 alberi OneDrive (ScanForFileUpdates) girava dodici volte al
                // minuto anche a app inattiva. 60s mantiene la stessa funzione di backstop con
                // un dodicesimo dell'I/O su disco.
                Interval = TimeSpan.FromSeconds(60)
            };
            _refreshTimer.Tick += (s, e) => CheckForFileUpdates();
            _refreshTimer.Start();
        }

        public static List<VerificheModel> GetVerificheForFleetStatic(string fleetIdentifier)
        {
            if (Instance != null)
            {
                if (fleetIdentifier == "500") return Instance.VerificheList500.ToList();
                if (fleetIdentifier == "700") return Instance.VerificheList700.ToList();
                if (fleetIdentifier == "1000") return Instance.VerificheList1000.ToList();
            }

            var list = new List<VerificheModel>();
            if (fleetIdentifier == "500")
                LoadDataForFleet(@"Hitachi Group\SSB_SST - Interventi ETR500\Censimento ETR500\Verifiche ETR500", "500", list);
            else if (fleetIdentifier == "700")
                LoadDataForFleet(@"Hitachi Group\SSB_SST - INTERVENTI ETR700 ELO BL3", "700", list);
            else if (fleetIdentifier == "1000")
                LoadDataForFleet(@"Hitachi Group\SSB_SST - Interventi ETR1000", "1000", list);

            return list;
        }

        /// <summary>
        /// Flusso completo di "Verifica Eseguita": chiede il cognome, esegue l'archiviazione fuori
        /// dal thread UI e ricarica le tabelle.
        ///
        /// <para>
        /// <b>Il dialog è aperto qui e non nel servizio</b>, così tutta la parte che tocca il disco
        /// resta senza dipendenze dalla UI e verificabile. L'annullamento o un campo vuoto
        /// interrompono prima di qualunque accesso al file: non viene creato nemmeno il backup.
        /// </para>
        /// </summary>
        private async Task EseguiVerificaEseguitaAsync(VerificheModel? riga)
        {
            if (riga == null || IsBusy) return;

            if (!riga.PuoEssereArchiviata)
            {
                MessageBox.Show(
                    "Non è stato possibile risalire alla riga di origine nel file Excel, quindi " +
                    "l'archiviazione è stata annullata per non rischiare di modificare la riga sbagliata.\n\n" +
                    "Attendere il prossimo aggiornamento automatico dell'elenco e riprovare.",
                    "Verifica Eseguita", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            string userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            var percorsi = VerifichePathsManager.Risolvi(userProfile, riga.FleetIdentifier);
            if (percorsi == null)
            {
                MessageBox.Show(
                    $"Nessun percorso configurato per la flotta '{riga.FleetIdentifier}' in verifiche_paths.json.",
                    "Verifica Eseguita", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            var dialog = new CognomeDialog($"Treno {riga.Treno} — Loco {riga.Loco}")
            {
                Owner = Application.Current?.MainWindow
            };

            if (dialog.ShowDialog() != true) return;   // annullato o campo vuoto: nessuna modifica

            string cognome = dialog.Cognome;
            var momento = DateTime.Now;

            ArchiviazioneEsito esito;
            try
            {
                IsBusy = true;
                StatoOperazione = "Archiviazione della verifica in corso...";

                // Copia backup, riscrittura del pacchetto OpenXML e rinomina: tutta roba che tocca
                // cartelle OneDrive, quindi mai sul dispatcher (§3, vincolo 1).
                esito = await Task.Run(() =>
                    VerificheArchivioService.Archivia(riga, percorsi, cognome, momento))
                    .ConfigureAwait(true);
            }
            catch (Exception ex)
            {
                esito = new ArchiviazioneEsito(false, $"Errore imprevisto:\n{ex.Message}");
            }
            finally
            {
                IsBusy = false;
                StatoOperazione = string.Empty;
            }

            if (esito.Riuscita)
            {
                // Ricarica: la riga archiviata sparisce dall'elenco a video.
                await ReloadAllDataAsync().ConfigureAwait(true);
                MessageBox.Show(esito.Messaggio, "Verifica Eseguita", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            else
            {
                MessageBox.Show(esito.Messaggio, "Verifica Eseguita", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        public async Task ReloadAllDataAsync()
        {
            await Task.Run(() =>
            {
                var list500 = new List<VerificheModel>();
                var list700 = new List<VerificheModel>();
                var list1000 = new List<VerificheModel>();

                LoadDataForFleet(@"Hitachi Group\SSB_SST - Interventi ETR500\Censimento ETR500\Verifiche ETR500", "500", list500);
                LoadDataForFleet(@"Hitachi Group\SSB_SST - INTERVENTI ETR700 ELO BL3", "700", list700);
                LoadDataForFleet(@"Hitachi Group\SSB_SST - Interventi ETR1000", "1000", list1000);

                Application.Current?.Dispatcher.InvokeAsync(() =>
                {
                    UpdateCollection(VerificheList500, list500);
                    UpdateCollection(VerificheList700, list700);
                    UpdateCollection(VerificheList1000, list1000);
                    OnVerificheDataUpdated?.Invoke();
                });
            });
        }

        private static void UpdateCollection(ObservableCollection<VerificheModel> target, List<VerificheModel> source)
        {
            target.Clear();
            foreach (var item in source)
            {
                target.Add(item);
            }
        }

        private void SetupWatchers()
        {
            string userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

            foreach (var rel in WatcherRelativePaths)
            {
                string path = Path.Combine(userProfile, rel);
                if (Directory.Exists(path))
                {
                    try
                    {
                        var watcher = new FileSystemWatcher(path)
                        {
                            IncludeSubdirectories = true,
                            Filter = "*.xlsx",
                            NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.CreationTime
                        };

                        watcher.Changed += OnFileChanged;
                        watcher.Created += OnFileChanged;
                        watcher.Deleted += OnFileChanged;
                        watcher.Renamed += OnFileChanged;
                        watcher.EnableRaisingEvents = true;

                        _watchers.Add(watcher);
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Watcher error: {ex.Message}");
                    }
                }
            }
        }

        private void OnFileChanged(object sender, FileSystemEventArgs e)
        {
            if (Path.GetFileName(e.FullPath).StartsWith("~$")) return;

            lock (_timerLock)
            {
                _debounceTimer?.Dispose();
                _debounceTimer = new System.Threading.Timer(_ =>
                {
                    _ = ReloadAllDataAsync();
                }, null, 500, System.Threading.Timeout.Infinite);
            }
        }

        /// <summary>
        /// Tick del timer di refresh. La scansione tocca alberi di cartelle OneDrive/di rete in modo
        /// ricorsivo: eseguirla in linea sul thread UI (come avveniva prima) bloccava l'interfaccia
        /// a ogni tick. Viene quindi delegata al thread pool, con guardia anti-rientranza per evitare
        /// che scansioni lente si accavallino quando il disco è occupato.
        /// </summary>
        private void CheckForFileUpdates()
        {
            if (System.Threading.Interlocked.Exchange(ref _isScanningFiles, 1) == 1)
                return;

            _ = Task.Run(() =>
            {
                bool hasChanges;
                try
                {
                    hasChanges = ScanForFileUpdates();
                }
                finally
                {
                    System.Threading.Interlocked.Exchange(ref _isScanningFiles, 0);
                }

                if (hasChanges)
                {
                    _ = ReloadAllDataAsync();
                }
            });
        }

        /// <summary>
        /// Confronta le date di ultima modifica dei file "Verifiche" con quelle memorizzate.
        /// Eseguita solo dal thread pool e serializzata da <see cref="_isScanningFiles"/>,
        /// quindi l'accesso a <see cref="_lastFileWriteTimes"/> resta a thread singolo.
        /// </summary>
        private bool ScanForFileUpdates()
        {
            string userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

            bool hasChanges = false;
            foreach (var rel in PollingRelativePaths)
            {
                string folderPath = Path.Combine(userProfile, rel);
                if (Directory.Exists(folderPath))
                {
                    try
                    {
                        // EnumerateFiles evita di materializzare l'intero array di percorsi
                        // prima di iniziare a filtrare (rilevante su alberi OneDrive profondi).
                        foreach (var file in Directory.EnumerateFiles(folderPath, "*Verifiche*.xlsx", SearchOption.AllDirectories))
                        {
                            if (Path.GetFileName(file).StartsWith("~$") ||
                                file.Contains("OLD", StringComparison.OrdinalIgnoreCase) ||
                                file.Contains("VECCH", StringComparison.OrdinalIgnoreCase) ||
                                file.Contains("ARCHIV", StringComparison.OrdinalIgnoreCase))
                            {
                                continue;
                            }

                            var lastWrite = File.GetLastWriteTime(file);
                            if (_lastFileWriteTimes.TryGetValue(file, out var prevWrite))
                            {
                                if (lastWrite != prevWrite)
                                {
                                    _lastFileWriteTimes[file] = lastWrite;
                                    hasChanges = true;
                                }
                            }
                            else
                            {
                                _lastFileWriteTimes[file] = lastWrite;
                            }
                        }
                    }
                    catch { }
                }
            }

            return hasChanges;
        }

        private static void LoadDataForFleet(string relativePath, string fleetIdentifier, List<VerificheModel> collection)
        {
            try
            {
                string userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                var folderPaths = new List<string> { Path.Combine(userProfile, relativePath) };

                if (fleetIdentifier == "1000")
                {
                    folderPaths.Add(Path.Combine(userProfile, @"Hitachi Group\SSB_SST - Interventi ETR1000FH"));
                    folderPaths.Add(Path.Combine(userProfile, @"Hitachi Group\SSB_SST - Interventi ETR1000 FH"));
                    folderPaths.Add(Path.Combine(userProfile, @"Hitachi Group\SSB_SST - Interventi ETR1000IF"));
                }
                else if (fleetIdentifier == "700")
                {
                    folderPaths.Add(Path.Combine(userProfile, @"Hitachi Group\SSB_SST - INTERVENTI ETR700 ELO BL3"));
                    folderPaths.Add(Path.Combine(userProfile, @"Hitachi Group\SSB_SST - Interventi ETR700"));
                }
                else if (fleetIdentifier == "500")
                {
                    folderPaths.Add(Path.Combine(userProfile, @"Hitachi Group\SSB_SST - Interventi ETR500"));
                }

                foreach (var folder in RemoveNestedRoots(folderPaths.Distinct()))
                {
                    if (!Directory.Exists(folder)) continue;

                    // UNA sola enumerazione ricorsiva dei file, invece di una enumerazione ricorsiva
                    // di tutte le sottocartelle seguita da una enumerazione di file per ciascuna.
                    // Su OneDrive/SharePoint ogni enumerazione è una chiamata potenzialmente lenta:
                    // la versione precedente ne faceva 1 + N (N = numero di sottocartelle dell'albero),
                    // questa ne fa una sola.
                    //
                    // Il risultato è identico. Il filtro che la versione precedente applicava alle
                    // *cartelle* (escludendo quelle con OLD/VECCH/ARCHIV nel percorso) era
                    // ridondante: il percorso completo di un file include quello della sua cartella,
                    // quindi un file dentro una cartella esclusa viene comunque scartato dal filtro
                    // sul percorso del file, che è rimasto invariato qui sotto.
                    //
                    // Selezione del file più recente in un'unica passata: la versione ancora
                    // precedente ordinava l'intera lista chiamando File.GetLastWriteTime O(n log n)
                    // volte (una syscall per confronto, molto costosa su cartelle sincronizzate).
                    string? mostRecentFile = null;
                    DateTime mostRecentWrite = DateTime.MinValue;

                    try
                    {
                        foreach (var f in Directory.EnumerateFiles(folder, "*Verifiche*.xlsx", SearchOption.AllDirectories))
                        {
                            if (Path.GetFileName(f).StartsWith("~$") ||
                                f.Contains("OLD", StringComparison.OrdinalIgnoreCase) ||
                                f.Contains("VECCH", StringComparison.OrdinalIgnoreCase) ||
                                f.Contains("ARCHIV", StringComparison.OrdinalIgnoreCase))
                            {
                                continue;
                            }

                            var write = File.GetLastWriteTime(f);
                            // ">" (non ">=") preserva l'ordinamento stabile dell'implementazione
                            // originale: a parità di data vince il primo file incontrato.
                            if (mostRecentFile == null || write > mostRecentWrite)
                            {
                                mostRecentFile = f;
                                mostRecentWrite = write;
                            }
                        }
                    }
                    catch { }

                    if (mostRecentFile != null)
                    {
                        ParseExcelFile(mostRecentFile, fleetIdentifier, collection);
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Errore caricamento Verifiche {fleetIdentifier}: {ex.Message}");
            }
        }

        /// <summary>
        /// Scarta, fra le radici di ricerca di una flotta, quelle che sono già contenute (come
        /// sottocartella, a qualunque profondità) in un'altra radice della stessa lista.
        ///
        /// <para>
        /// <b>Il bug che questo corregge (segnalato dal committente: le verifiche ETR500 comparivano
        /// duplicate).</b> Per la flotta "500", <c>LoadDataForFleet</c> cerca in due radici:
        /// <c>Interventi ETR500\Censimento ETR500\Verifiche ETR500</c> (il percorso base) e
        /// <c>Interventi ETR500</c> (aggiunta subito dopo) — ma la seconda è la cartella
        /// <b>madre</b> della prima. La ricerca del file più recente scansiona ricorsivamente
        /// ciascuna radice per conto proprio: sulla radice "madre" la scansione ricorsiva
        /// attraversa comunque la sottocartella "Censimento", trova lì lo stesso identico file
        /// <c>.xlsx</c> già trovato scansionando la radice "figlia", e — se è il file più recente
        /// dell'intero sottoalbero, che è il caso normale, dato che è l'unico report della
        /// cartella — lo seleziona di nuovo come "file più recente" anche per questa seconda radice.
        /// Risultato: <see cref="ParseExcelFile"/> viene chiamato due volte sullo stesso file, e
        /// ogni riga del foglio finisce due volte in <paramref name="collection"/> (dal chiamante).
        /// </para>
        ///
        /// <para>
        /// Le altre due flotte non hanno questo problema: le radici aggiuntive di "1000"
        /// (<c>ETR1000FH</c>, <c>ETR1000 FH</c>, <c>ETR1000IF</c>) e di "700"
        /// (<c>INTERVENTI ETR700 ELO BL3</c>, <c>Interventi ETR700</c>) sono cartelle sorelle, non
        /// annidate l'una nell'altra — verificato qui con un test dedicato, non per ispezione visiva.
        /// La correzione non è quindi un caso speciale per "500": è una deduplicazione generale,
        /// che per costruzione non cambia nulla dove le radici non si annidano.
        /// </para>
        ///
        /// <para>
        /// Non richiede che le cartelle esistano sul disco (la lista può contenere percorsi non
        /// presenti sulla macchina corrente: <c>LoadDataForFleet</c> li scarta comunque dopo, con
        /// <c>Directory.Exists</c>): il confronto è puramente testuale sui percorsi normalizzati,
        /// il che la rende testabile senza un vero albero di cartelle.
        /// </para>
        /// </summary>
        internal static List<string> RemoveNestedRoots(IEnumerable<string> paths)
        {
            var list = paths.ToList();
            return list.Where(candidate => !list.Any(other =>
                    !string.Equals(other, candidate, StringComparison.OrdinalIgnoreCase) &&
                    IsSubPathOf(candidate, other)))
                .ToList();
        }

        /// <summary>Vero se <paramref name="path"/> è <paramref name="potentialAncestor"/> stesso oppure una sua sottocartella, a qualunque profondità.</summary>
        private static bool IsSubPathOf(string path, string potentialAncestor)
        {
            string normalizedPath = NormalizePath(path);
            string normalizedAncestor = NormalizePath(potentialAncestor);

            return normalizedPath.Equals(normalizedAncestor, StringComparison.OrdinalIgnoreCase) ||
                   normalizedPath.StartsWith(normalizedAncestor + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
        }

        private static string NormalizePath(string path) =>
            path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                .Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);

        /// <summary>
        /// Estrae le verifiche da un file Excel. Percorso primario: lettura SAX in streaming
        /// (<see cref="VerificheExcelReader"/>, intervento 3.4), che non costruisce alcun DOM.
        /// Se fallisce per un motivo qualsiasi — formato inatteso, pacchetto OpenXML malformato,
        /// file <c>.xls</c> legacy — si ricade sul percorso ClosedXML originale, invariato: la
        /// lettura è più lenta e più costosa in memoria, ma nessuna verifica viene persa.
        /// </summary>
        private static void ParseExcelFile(string filePath, string fleetIdentifier, List<VerificheModel> collection)
        {
            try
            {
                var rows = VerificheExcelReader.Read(filePath);
                foreach (var row in rows)
                {
                    var model = BuildModel(row.Treno, row.Loco, row.Avaria, fleetIdentifier);
                    // Provenienza della riga: senza file e numero di riga "Verifica Eseguita" non
                    // saprebbe quale workbook aprire né quale riga togliere (vedi VerificheModel).
                    model.SourceFilePath = filePath;
                    model.SourceRowNumber = row.RowNumber;
                    // Valori grezzi del foglio: la guardia anti-riga-sbagliata li confronta con quelli
                    // riletti dal file al momento della scrittura. Treno/Loco del modello non servono
                    // allo scopo, perche per la flotta 1000 Treno viene sostituito dal numero risolto
                    // tramite il database (vedi VerificheModel.SourceTreno).
                    model.SourceTreno = row.Treno;
                    model.SourceLoco = row.Loco;
                    collection.Add(model);
                }
                return;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Lettura SAX non riuscita su {filePath} ({ex.Message}); ripiego su ClosedXML.");
            }

            ParseExcelFileWithClosedXml(filePath, fleetIdentifier, collection);
        }

        /// <summary>
        /// Applica al valore grezzo di una riga le stesse regole di normalizzazione del percorso
        /// originale, comprese la risoluzione del treno da <c>flotte</c> per la sola flotta "1000".
        /// Condivisa fra percorso SAX e percorso ClosedXML, così le due strade non possono divergere.
        /// </summary>
        private static VerificheModel BuildModel(string treno, string loco, string avaria, string fleetIdentifier)
        {
            var model = new VerificheModel
            {
                Treno = treno,
                Loco = loco,
                Avaria = avaria,
                FleetIdentifier = fleetIdentifier
            };

            if (fleetIdentifier == "1000" && !string.IsNullOrWhiteSpace(model.Loco))
            {
                if (model.Treno != null && model.Treno.StartsWith("ETR100"))
                {
                    string? trenoFromDb = Core.FlotteCache.FindTrenoByLoco(model.Loco);
                    if (!string.IsNullOrEmpty(trenoFromDb))
                    {
                        model.Treno = trenoFromDb;
                    }
                }
            }

            return model;
        }

        private static void ParseExcelFileWithClosedXml(string filePath, string fleetIdentifier, List<VerificheModel> collection)
        {
            try
            {
                using var workbook = new XLWorkbook(filePath);
                var worksheet = workbook.Worksheet(1);
                var rowsUsed = worksheet.RowsUsed().ToList();
                if (rowsUsed.Count < 2) return;
                
                IXLRow? actualHeaderRow = null;
                int headerRowIndexInList = -1;

                for (int i = 0; i < rowsUsed.Count; i++)
                {
                    var r = rowsUsed[i];
                    bool hasTreno = false;
                    foreach (var cell in r.CellsUsed())
                    {
                        if (cell.GetString().Trim().Contains("TRENO", StringComparison.OrdinalIgnoreCase))
                        {
                            hasTreno = true;
                            break;
                        }
                    }
                    
                    if (hasTreno)
                    {
                        actualHeaderRow = r;
                        headerRowIndexInList = i;
                        break;
                    }
                }

                if (actualHeaderRow == null)
                {
                    actualHeaderRow = rowsUsed[0];
                    headerRowIndexInList = 0;
                }

                var dataRows = rowsUsed.Skip(headerRowIndexInList + 1);

                int trenoIdx = -1, locoIdx = -1, avariaIdx = -1;

                foreach (var cell in actualHeaderRow.CellsUsed())
                {
                    string headerText = cell.GetString().Trim();
                    if (headerText.Contains("TRENO", StringComparison.OrdinalIgnoreCase)) trenoIdx = cell.Address.ColumnNumber;
                    else if (headerText.Contains("LOCO", StringComparison.OrdinalIgnoreCase)) locoIdx = cell.Address.ColumnNumber;
                    else if (headerText.Contains("AVARIA", StringComparison.OrdinalIgnoreCase) || headerText.Contains("ING/SVI", StringComparison.OrdinalIgnoreCase)) avariaIdx = cell.Address.ColumnNumber;
                }

                if (trenoIdx == -1) trenoIdx = 1;
                if (locoIdx == -1) locoIdx = 2;
                if (avariaIdx == -1) avariaIdx = 3;

                // FlotteCache (core/FlotteCache.cs) tiene l'intera tabella flotte in memoria, quindi
                // qui non serve più né aprire una connessione SQLite né una cache locale per evitare
                // ricerche ripetute: ogni ricerca è già una scansione in memoria.
                foreach (var row in dataRows)
                {
                    string treno = row.Cell(trenoIdx).GetString()?.Trim() ?? string.Empty;
                    string loco = row.Cell(locoIdx).GetString()?.Trim() ?? string.Empty;
                    string avaria = row.Cell(avariaIdx).GetString()?.Trim() ?? string.Empty;

                    if (!string.IsNullOrWhiteSpace(treno) || !string.IsNullOrWhiteSpace(loco) || !string.IsNullOrWhiteSpace(avaria))
                    {
                        collection.Add(BuildModel(treno, loco, avaria, fleetIdentifier));
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Errore parse file Excel {filePath}: {ex.Message}");
            }
        }

        // OpenTrainSoftwareDatabase/GetTrenoFromDatabase rimossi: sostituiti da FlotteCache.FindTrenoByLoco
        // (core/FlotteCache.cs), che replica esattamente la stessa query con fallback numerico su loco.
    }
}
