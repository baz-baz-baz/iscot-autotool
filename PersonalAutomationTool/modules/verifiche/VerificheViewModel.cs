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

        public VerificheViewModel()
        {
            Instance = this;
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

                foreach (var folder in folderPaths.Distinct())
                {
                    if (!Directory.Exists(folder)) continue;

                    var validSubFolders = new List<string> { folder };
                    try
                    {
                        foreach (var d in Directory.EnumerateDirectories(folder, "*", SearchOption.AllDirectories))
                        {
                            if (!d.Contains("OLD", StringComparison.OrdinalIgnoreCase) &&
                                !d.Contains("VECCH", StringComparison.OrdinalIgnoreCase) &&
                                !d.Contains("ARCHIV", StringComparison.OrdinalIgnoreCase))
                            {
                                validSubFolders.Add(d);
                            }
                        }
                    }
                    catch { }

                    // Selezione del file più recente in un'unica passata: la versione precedente
                    // ordinava l'intera lista chiamando File.GetLastWriteTime O(n log n) volte
                    // (una syscall per confronto, molto costosa su cartelle sincronizzate).
                    string? mostRecentFile = null;
                    DateTime mostRecentWrite = DateTime.MinValue;

                    foreach (var dir in validSubFolders)
                    {
                        try
                        {
                            foreach (var f in Directory.EnumerateFiles(dir, "*Verifiche*.xlsx"))
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
                    }

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

        private static void ParseExcelFile(string filePath, string fleetIdentifier, List<VerificheModel> collection)
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

                // La connessione SQLite viene aperta al massimo UNA volta per file (in modo pigro,
                // solo se serve davvero) invece di una volta per riga: su un foglio da qualche
                // centinaio di righe si passa da centinaia di open/close a uno solo.
                PersonalAutomationTool.Modules.Database.DatabaseManager? db = null;
                var locoTrenoCache = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

                try
                {
                    foreach (var row in dataRows)
                    {
                        var model = new VerificheModel
                        {
                            Treno = row.Cell(trenoIdx).GetString()?.Trim() ?? string.Empty,
                            Loco = row.Cell(locoIdx).GetString()?.Trim() ?? string.Empty,
                            Avaria = row.Cell(avariaIdx).GetString()?.Trim() ?? string.Empty
                        };

                        if (fleetIdentifier == "1000" && !string.IsNullOrWhiteSpace(model.Loco))
                        {
                            if (model.Treno != null && model.Treno.StartsWith("ETR100"))
                            {
                                if (!locoTrenoCache.TryGetValue(model.Loco, out var trenoFromDb))
                                {
                                    db ??= OpenTrainSoftwareDatabase();
                                    trenoFromDb = GetTrenoFromDatabase(db, model.Loco);
                                    locoTrenoCache[model.Loco] = trenoFromDb;
                                }

                                if (!string.IsNullOrEmpty(trenoFromDb))
                                {
                                    model.Treno = trenoFromDb;
                                }
                            }
                        }

                        if (!string.IsNullOrWhiteSpace(model.Treno) || !string.IsNullOrWhiteSpace(model.Loco) || !string.IsNullOrWhiteSpace(model.Avaria))
                        {
                            collection.Add(model);
                        }
                    }
                }
                finally
                {
                    db?.Dispose();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Errore parse file Excel {filePath}: {ex.Message}");
            }
        }

        private static PersonalAutomationTool.Modules.Database.DatabaseManager? OpenTrainSoftwareDatabase()
        {
            try
            {
                string dbPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "modules", "database", "train_software.db");
                if (File.Exists(dbPath))
                {
                    return new PersonalAutomationTool.Modules.Database.DatabaseManager(dbPath);
                }
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"Db open error: {ex.Message}"); }
            return null;
        }

        private static string GetTrenoFromDatabase(PersonalAutomationTool.Modules.Database.DatabaseManager? db, string loco)
        {
            if (db == null) return "";

            try
            {
                string query = "SELECT treno FROM flotte WHERE loco = @loco";
                var parameters = new Dictionary<string, object?> { { "@loco", loco } };
                if (int.TryParse(loco, out int locoInt))
                {
                    query += " OR loco = @locoInt";
                    parameters["@locoInt"] = locoInt;
                }

                var data = db.ExecuteQuery(query, parameters);
                if (data.Rows.Count > 0 && !data.Columns.Contains("Errore"))
                {
                    string trenoDb = data.Rows[0]["treno"]?.ToString() ?? "";
                    if (!string.IsNullOrEmpty(trenoDb))
                    {
                        return trenoDb;
                    }
                }
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"Db search error: {ex.Message}"); }
            return "";
        }
    }
}
