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

        public ObservableCollection<VerificheModel> VerificheList500 { get; } = [];
        public ObservableCollection<VerificheModel> VerificheList700 { get; } = [];
        public ObservableCollection<VerificheModel> VerificheList1000 { get; } = [];

        public VerificheViewModel()
        {
            _ = ReloadAllDataAsync();

            SetupWatchers();

            _refreshTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(5)
            };
            _refreshTimer.Tick += (s, e) => CheckForFileUpdates();
            _refreshTimer.Start();
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
            string[] relativePaths = [
                @"Hitachi Group\SSB_SST - Interventi ETR500",
                @"Hitachi Group\SSB_SST - INTERVENTI ETR700 ELO BL3",
                @"Hitachi Group\SSB_SST - Interventi ETR1000"
            ];

            foreach (var rel in relativePaths)
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

        private void CheckForFileUpdates()
        {
            string userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            string[] relativePaths = [
                @"Hitachi Group\SSB_SST - Interventi ETR500",
                @"Hitachi Group\SSB_SST - INTERVENTI ETR700 ELO BL3",
                @"Hitachi Group\SSB_SST - Interventi ETR1000"
            ];

            bool hasChanges = false;
            foreach (var rel in relativePaths)
            {
                string folderPath = Path.Combine(userProfile, rel);
                if (Directory.Exists(folderPath))
                {
                    try
                    {
                        var files = Directory.GetFiles(folderPath, "*Verifiche*.xlsx", SearchOption.AllDirectories)
                                             .Where(f => !Path.GetFileName(f).StartsWith("~$"));

                        foreach (var file in files)
                        {
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

            if (hasChanges)
            {
                _ = ReloadAllDataAsync();
            }
        }

        private static void LoadDataForFleet(string relativePath, string fleetIdentifier, List<VerificheModel> collection)
        {
            try
            {
                string userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                string folderPath = Path.Combine(userProfile, relativePath);
                string filePath = string.Empty;

                if (!Directory.Exists(folderPath))
                {
                    string fallbackPath = Path.Combine(userProfile, @"Hitachi Group\SSB_SST - Interventi ETR" + fleetIdentifier);
                    if (fleetIdentifier == "700") fallbackPath = Path.Combine(userProfile, @"Hitachi Group\SSB_SST - INTERVENTI ETR700 ELO BL3");
                    if (Directory.Exists(fallbackPath)) folderPath = fallbackPath;
                }

                if (Directory.Exists(folderPath))
                {
                    var allFolders = new System.Collections.Generic.List<string> { folderPath };
                    try {
                        var level1 = Directory.GetDirectories(folderPath);
                        allFolders.AddRange(level1);
                        foreach (var d1 in level1) {
                            try { allFolders.AddRange(Directory.GetDirectories(d1)); } catch (Exception ex) { System.Diagnostics.Debug.WriteLine(ex.Message); }
                        }
                    } catch (Exception ex) { System.Diagnostics.Debug.WriteLine(ex.Message); }
                    
                    foreach (var dir in allFolders)
                    {
                        try 
                        {
                            var files = Directory.GetFiles(dir, "*Verifiche*.xlsx")
                                                 .Where(f => !Path.GetFileName(f).StartsWith("~$"))
                                                 .ToArray();
                            if (files.Length > 0)
                            {
                                var recentFile = files.OrderByDescending(f => File.GetLastWriteTime(f)).First();
                                filePath = recentFile;
                                break;
                            }
                        }
                        catch { }
                    }
                }

                if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
                {
                    System.Diagnostics.Debug.WriteLine($"File Verifiche non trovato per la flotta {fleetIdentifier}");
                    return;
                }

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
                            string trenoFromDb = GetTrenoFromDatabase(model.Loco);
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
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Errore caricamento Verifiche {fleetIdentifier}: {ex.Message}");
            }
        }

        private static string GetTrenoFromDatabase(string loco)
        {
            try
            {
                string dbPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "modules", "database", "train_software.db");
                if (File.Exists(dbPath))
                {
                    using var db = new PersonalAutomationTool.Modules.Database.DatabaseManager(dbPath);
                    string query = "SELECT treno FROM flotte WHERE loco = @loco";
                    var parameters = new System.Collections.Generic.Dictionary<string, object?> { { "@loco", loco } };
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
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"Db search error: {ex.Message}"); }
            return "";
        }
    }
}
