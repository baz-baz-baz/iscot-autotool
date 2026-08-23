using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Windows.Input;
using System.Windows.Threading;
using PersonalAutomationTool.Core;
using PersonalAutomationTool.Core.Dialogs;

namespace PersonalAutomationTool.Modules.Home
{
    public partial class HomeViewModel : ViewModelBase
    {
        [System.Text.RegularExpressions.GeneratedRegex(@"\b\d{6}\b")]
        private static partial System.Text.RegularExpressions.Regex MyDateRegex();

        private readonly DispatcherTimer _timer;

        /// <summary>
        /// Cultura usata per la data estesa. Era ricostruita a ogni tick dell'orologio (una
        /// nuova CultureInfo al secondo, per tutta la durata della sessione).
        /// </summary>
        private static readonly System.Globalization.CultureInfo ItalianCulture = new("it-IT");

        /// <summary>Giorno per cui <see cref="CurrentDate"/> è già stato formattato.</summary>
        private DateTime _lastRenderedDate = DateTime.MinValue;

        private string _currentTime = string.Empty;
        public string CurrentTime
        {
            get => _currentTime;
            set => SetProperty(ref _currentTime, value);
        }

        private string _currentDate = string.Empty;
        public string CurrentDate
        {
            get => _currentDate;
            set => SetProperty(ref _currentDate, value);
        }

        public ObservableCollection<PendingMaintenanceModel> PendingItems { get; } = [];

        private string _oldTicket = string.Empty;
        public string OldTicket
        {
            get => _oldTicket;
            set => SetProperty(ref _oldTicket, value);
        }

        private string _newTicket = string.Empty;
        public string NewTicket
        {
            get => _newTicket;
            set => SetProperty(ref _newTicket, value);
        }

        private PendingMaintenanceModel? _selectedItem;
        public PendingMaintenanceModel? SelectedItem
        {
            get => _selectedItem;
            set => SetProperty(ref _selectedItem, value);
        }

        /// <summary>Overlay di avanzamento (intervento 4.2, Sprint 3) per le operazioni pesanti: zip, spostamento in rete, eliminazione.</summary>
        private bool _isLoading;
        public bool IsLoading
        {
            get => _isLoading;
            set => SetProperty(ref _isLoading, value);
        }

        private string _loadingMessage = string.Empty;
        public string LoadingMessage
        {
            get => _loadingMessage;
            set => SetProperty(ref _loadingMessage, value);
        }

        public ICommand AggiornaTicketCommand { get; }
        public ICommand ZipCommand { get; }
        public ICommand LogDumpReteCommand { get; }
        public ICommand EliminaCommand { get; }
        public ICommand AggiornaDataCommand { get; }
        public ICommand AnnullaRinominaCommand { get; }

        public HomeViewModel()
        {
            _timer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(1)
            };
            _timer.Tick += (s, e) => UpdateClock();
            _timer.Start();
            UpdateClock();

            AggiornaTicketCommand = new RelayCommand(OnAggiornaTicket);
            ZipCommand = new RelayCommand(OnZip);
            LogDumpReteCommand = new RelayCommand(OnLogDumpRete);
            EliminaCommand = new RelayCommand(OnElimina);
            AggiornaDataCommand = new RelayCommand(OnAggiornaData);
            AnnullaRinominaCommand = new RelayCommand(OnAnnullaRinomina);

            _ = LoadPendingItemsAsync();

            AppWatcher.OnLogDumpFolderChanged += AppWatcher_OnLogDumpFolderChanged;
        }

        private void AppWatcher_OnLogDumpFolderChanged()
        {
            _ = ReloadAndPreserveStateAsync();
        }

        private void UpdateClock()
        {
            var now = DateTime.Now;
            CurrentTime = now.ToString("HH:mm:ss");

            // La data estesa cambia solo a mezzanotte: riformattarla a ogni secondo produceva
            // una stringa e una CultureInfo usa-e-getta al secondo, senza alcun effetto visibile.
            var today = now.Date;
            if (today != _lastRenderedDate)
            {
                _lastRenderedDate = today;
                CurrentDate = now.ToString("dddd d MMMM yyyy", ItalianCulture);
            }
        }

        private async System.Threading.Tasks.Task LoadPendingItemsAsync()
        {
            if (!Directory.Exists(AppConfig.LogAndDumpFolder))
            {
                PendingItems.Clear();
                return;
            }

            try
            {
                string folder = AppConfig.LogAndDumpFolder;
                var items = await System.Threading.Tasks.Task.Run(() =>
                {
                    var resultList = new System.Collections.Generic.List<PendingMaintenanceModel>();
                    var mainDirs = Directory.GetDirectories(folder);
                    var now = DateTime.Now; // letto una volta invece che a ogni cartella
                    foreach (var dir in mainDirs)
                    {
                        var dirInfo = new DirectoryInfo(dir);
                        var subDirs = dirInfo.GetDirectories();
                        if (subDirs.Length == 0) continue;

                        var data = dirInfo.LastWriteTime;
                        int giorni = (now - data).Days;

                        var model = new PendingMaintenanceModel
                        {
                            TipoTreno = dirInfo.Name,
                            NumeroCartelle = subDirs.Length,
                            Data = data.ToString("dd/MM/yyyy"),
                            Giorni = Math.Max(0, giorni),
                            Percorso = dirInfo.FullName
                        };

                        foreach (var subDir in subDirs)
                        {
                            // Confronto case-insensitive diretto: evita la stringa temporanea
                            // prodotta da ToUpper() per ogni sottocartella.
                            string name = subDir.Name;
                            if (name.Contains("LOG", StringComparison.OrdinalIgnoreCase) ||
                                name.Contains("DUMP", StringComparison.OrdinalIgnoreCase))
                            {
                                model.SubFolders.Add($"- {name}");
                            }
                        }

                        resultList.Add(model);
                    }
                    return resultList;
                });

                PendingItems.Clear();
                foreach (var item in items)
                {
                    PendingItems.Add(item);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Errore caricamento Home: {ex.Message}");
            }
        }

        private async System.Threading.Tasks.Task ReloadAndPreserveStateAsync()
        {
            var expandedItems = PendingItems.Where(p => p.IsExpanded).Select(p => p.TipoTreno).ToList();
            string? selectedTreno = SelectedItem?.TipoTreno;

            await LoadPendingItemsAsync();

            foreach (var item in PendingItems)
            {
                if (expandedItems.Contains(item.TipoTreno))
                {
                    item.IsExpanded = true;
                }
            }
            if (selectedTreno != null)
            {
                SelectedItem = PendingItems.FirstOrDefault(p => p.TipoTreno == selectedTreno);
            }
        }

        private async void OnAggiornaTicket(object? parameter)
        {
            if (SelectedItem == null)
                return;

            string path = SelectedItem.Percorso;
            if (!Directory.Exists(path))
                return;

            try
            {
                // Usa OldTicket come "Ticket 1" e NewTicket come "Ticket 2"
                string? ticket1 = OldTicket?.Trim();
                string? ticket2 = NewTicket?.Trim();

                if (string.IsNullOrWhiteSpace(ticket1) && string.IsNullOrWhiteSpace(ticket2))
                    return;

                // Calcola il piano (nessuna scrittura) su thread pool: la logica di decisione è
                // identica a prima, solo separata dall'esecuzione per poter mostrare l'anteprima
                // (intervento 4.1, Sprint 3) prima di spostare qualunque cartella.
                var plan = await System.Threading.Tasks.Task.Run(() =>
                {
                    var operations = new System.Collections.Generic.List<(string OldPath, string NewPath)>();
                    var subDirs = Directory.GetDirectories(path);

                    // Trova i ticket correnti (assumendo che il ticket sia la prima parola nel nome della cartella)
                    var currentTickets = subDirs
                        .Select(d => new DirectoryInfo(d).Name.Split(' ').FirstOrDefault())
                        .Where(t => !string.IsNullOrEmpty(t))
                        .Distinct()
                        .OrderBy(t => t) // Ordina alfabeticamente per coerenza
                        .ToList();

                    foreach (var subDir in subDirs)
                    {
                        var dirInfo = new DirectoryInfo(subDir);
                        var parts = dirInfo.Name.Split(' ');
                        if (parts.Length > 0)
                        {
                            string currentTicket = parts[0];
                            string? newTicketForThisDir = null;

                            // Assegna il nuovo ticket in base a se è il primo o il secondo ticket trovato
                            if (currentTickets.Count > 0 && currentTicket == currentTickets[0] && !string.IsNullOrWhiteSpace(ticket1))
                            {
                                newTicketForThisDir = ticket1;
                            }
                            else if (currentTickets.Count > 1 && currentTicket == currentTickets[1] && !string.IsNullOrWhiteSpace(ticket2))
                            {
                                newTicketForThisDir = ticket2;
                            }
                            // Se il treno ha un solo ticket ma l'utente ha compilato ticket1 e ticket2,
                            // il ticket1 viene applicato a tutte le cartelle di quel ticket.

                            if (!string.IsNullOrEmpty(newTicketForThisDir))
                            {
                                parts[0] = newTicketForThisDir;
                                string newName = string.Join(" ", parts);
                                string? parentFolder = dirInfo.Parent?.FullName;
                                if (parentFolder != null)
                                {
                                    string newPath = Path.Combine(parentFolder, newName);
                                    if (dirInfo.FullName != newPath)
                                    {
                                        operations.Add((dirInfo.FullName, newPath));
                                    }
                                }
                            }
                        }
                    }
                    return operations;
                });

                if (plan.Count == 0)
                    return;

                if (!RenamePreviewDialog.Confirm(System.Windows.Application.Current?.MainWindow, plan))
                    return;

                await System.Threading.Tasks.Task.Run(() =>
                {
                    foreach (var (oldPath, newPath) in plan)
                    {
                        Directory.Move(oldPath, newPath);
                    }
                });

                // Storico inverso per l'annulla (intervento 4.3, Sprint 3).
                RenamerLog.RecordBatch(RenameBatchKind.HomeTicket, plan);

                await ReloadAndPreserveStateAsync();

                // Ripulisci i campi
                OldTicket = string.Empty;
                NewTicket = string.Empty;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Errore aggiornamento ticket: {ex.Message}");
            }
        }

        private async void OnAggiornaData(object? parameter)
        {
            if (SelectedItem == null)
                return;

            string path = SelectedItem.Percorso;
            if (!Directory.Exists(path))
                return;

            try
            {
                var plan = await System.Threading.Tasks.Task.Run(() =>
                {
                    var operations = new System.Collections.Generic.List<(string OldPath, string NewPath)>();
                    var subDirs = Directory.GetDirectories(path);
                    string todayString = DateTime.Now.ToString("ddMMyy");
                    foreach (var subDir in subDirs)
                    {
                        var dirInfo = new DirectoryInfo(subDir);
                        string currentName = dirInfo.Name;

                        var match = MyDateRegex().Match(currentName);
                        if (match.Success)
                        {
                            string newName = string.Concat(currentName.AsSpan(0, match.Index), todayString, currentName.AsSpan(match.Index + match.Length));
                            string? parentFolder = dirInfo.Parent?.FullName;
                            if (parentFolder != null)
                            {
                                string newPath = Path.Combine(parentFolder, newName);
                                if (dirInfo.FullName != newPath)
                                {
                                    operations.Add((dirInfo.FullName, newPath));
                                }
                            }
                        }
                    }
                    return operations;
                });

                if (plan.Count == 0)
                    return;

                if (!RenamePreviewDialog.Confirm(System.Windows.Application.Current?.MainWindow, plan))
                    return;

                await System.Threading.Tasks.Task.Run(() =>
                {
                    foreach (var (oldPath, newPath) in plan)
                    {
                        Directory.Move(oldPath, newPath);
                    }
                });

                RenamerLog.RecordBatch(RenameBatchKind.HomeData, plan);

                await ReloadAndPreserveStateAsync();
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Errore durante l'aggiornamento della data:\n{ex.Message}", "Errore", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            }
        }

        private async void OnAnnullaRinomina(object? parameter)
        {
            var result = await System.Threading.Tasks.Task.Run(() =>
                RenamerLog.UndoLastBatch(RenameBatchKind.HomeTicket, RenameBatchKind.HomeData));

            if (!result.BatchFound)
            {
                System.Windows.MessageBox.Show("Nessuna rinomina da annullare.", "Info", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
                return;
            }

            if (result.Errors.Count > 0)
            {
                string msg = $"Ripristinate {result.Restored} cartelle. Alcune non sono state ripristinate:\n\n" + string.Join("\n", result.Errors);
                System.Windows.MessageBox.Show(msg, "Attenzione", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
            }
            else
            {
                System.Windows.MessageBox.Show($"Rinomina annullata: {result.Restored} cartelle ripristinate al nome precedente.", "Fatto", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
            }

            await ReloadAndPreserveStateAsync();
        }

        private async void OnZip(object? parameter)
        {
            if (SelectedItem == null)
                return;

            string path = SelectedItem.Percorso;
            if (!Directory.Exists(path))
                return;

            IsLoading = true;
            LoadingMessage = "Archiviazione in corso...";
            try
            {
                // IProgress<T> cattura il SynchronizationContext della UI al momento della
                // costruzione: Report() dal thread pool marshalla automaticamente su Dispatcher,
                // come richiesto da §6.5 (le assegnazioni a proprietà osservabili devono restare
                // sul thread giusto, qui lo sono per costruzione invece che per affidamento a WPF).
                IProgress<(int Current, int Total)> progress = new Progress<(int Current, int Total)>(p =>
                    LoadingMessage = $"Archiviazione {p.Current} di {p.Total}...");

                await System.Threading.Tasks.Task.Run(() =>
                {
                    var subDirs = Directory.GetDirectories(path);
                    for (int i = 0; i < subDirs.Length; i++)
                    {
                        var dirInfo = new DirectoryInfo(subDirs[i]);
                        string zipFilePath = Path.Combine(dirInfo.Parent?.FullName ?? string.Empty, dirInfo.Name + ".zip");

                        // Se il file zip non esiste già, crealo
                        if (!File.Exists(zipFilePath))
                        {
                            System.IO.Compression.ZipFile.CreateFromDirectory(subDirs[i], zipFilePath);
                        }
                        progress.Report((i + 1, subDirs.Length));
                    }
                });

                System.Windows.MessageBox.Show("Archiviazione Zip completata con successo!", "Successo", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Errore durante la creazione dei file Zip:\n{ex.Message}", "Errore", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            }
            finally
            {
                IsLoading = false;
            }
        }

        private async void OnLogDumpRete(object? parameter)
        {
            if (SelectedItem == null)
                return;

            string path = SelectedItem.Percorso;
            if (!Directory.Exists(path))
                return;

            try
            {
                // Estrai il Tipo Treno dal nome della riga selezionata (es: "E404P 30" -> "E404P", "ETR700 12" -> "ETR700")
                string rawTrainType = SelectedItem.TipoTreno.Split(' ').FirstOrDefault()?.ToUpper() ?? string.Empty;
                if (string.IsNullOrEmpty(rawTrainType))
                    return;

                // Risoluzione dei percorsi di rete spostata sul thread pool (criticità G di
                // PROJECT_MEMORY.md §6.4). GetLogDumpReteBasePath e ResolveTrainTypePath fanno
                // Directory.Exists/GetDirectories su percorsi OneDrive o di rete: su una connessione
                // lenta o disconnessa ognuna può bloccare per secondi, e prima giravano tutte sul
                // dispatcher prima ancora di entrare nel Task.Run sottostante — la finestra restava
                // congelata già al clic del pulsante.
                IsLoading = true;
                LoadingMessage = "Ricerca cartelle di rete...";

                var (basePath, trainTypePath, zipFiles) = await System.Threading.Tasks.Task.Run<(string BasePath, string? TrainTypePath, string[]? ZipFiles)>(() =>
                {
                    string baseP = GetLogDumpReteBasePath();
                    if (!Directory.Exists(baseP)) return (baseP, null, null);

                    string? trainP = ResolveTrainTypePath(baseP, rawTrainType);
                    if (string.IsNullOrEmpty(trainP) || !Directory.Exists(trainP)) return (baseP, null, null);

                    return (baseP, trainP, Directory.GetFiles(path, "*.zip"));
                });

                if (!Directory.Exists(basePath))
                {
                    IsLoading = false;
                    System.Windows.MessageBox.Show($"La cartella di rete di base non esiste:\n{basePath}", "Attenzione", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
                    return;
                }

                if (string.IsNullOrEmpty(trainTypePath))
                {
                    IsLoading = false;
                    System.Windows.MessageBox.Show($"La cartella del tipo treno ({rawTrainType}) non è stata trovata nei percorsi di rete esistenti.", "Attenzione", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
                    return;
                }

                if (zipFiles == null || zipFiles.Length == 0)
                {
                    IsLoading = false;
                    System.Windows.MessageBox.Show("Nessun file ZIP trovato. Esegui prima l'operazione di ZIP.", "Informazione", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
                    return;
                }

                int movedCount = 0;
                var skippedLog = new System.Collections.Generic.List<string>();

                IsLoading = true;
                LoadingMessage = $"Spostamento 0 di {zipFiles.Length}...";
                IProgress<(int Current, int Total)> progress = new Progress<(int Current, int Total)>(p =>
                    LoadingMessage = $"Spostamento {p.Current} di {p.Total}...");

                await System.Threading.Tasks.Task.Run(() =>
                {
                    for (int fi = 0; fi < zipFiles.Length; fi++)
                    {
                        progress.Report((fi + 1, zipFiles.Length));

                        var zipFile = zipFiles[fi];
                        string fileName = Path.GetFileNameWithoutExtension(zipFile);
                        var parts = fileName.Split(' ');

                        // Ci aspettiamo un nome del tipo: [Ticket] [LOG/DUMP] [Treno] [Loco] ...
                        // Es: "1247654 DUMP ETR700 117 04.02HR 300526 Todde"
                        if (parts.Length >= 4)
                        {
                            string logOrDump = parts[1].ToUpper(); // "LOG" o "DUMP"
                            string fileTrainType = parts[2].ToUpper(); // "ETR700", "E404P", "1000FH"
                            string loco = parts[3]; // "117", "627"

                            if ((logOrDump == "LOG" || logOrDump == "DUMP") && AreTrainTypesCompatible(fileTrainType, rawTrainType))
                            {
                                // Cerca la cartella della loco ESISTENTE (es. "ETR700-117", "E404P_627", "117", ecc.)
                                string? locoFolderPath = FindExistingLocoFolder(trainTypePath, loco, rawTrainType, fileTrainType);
                                if (string.IsNullOrEmpty(locoFolderPath))
                                {
                                    skippedLog.Add($"File {Path.GetFileName(zipFile)}: Cartella per loco '{loco}' non trovata in rete in '{Path.GetFileName(trainTypePath)}'.");
                                    continue;
                                }

                                // Cerca la sottocartella ESISTENTE ("Log", "Dump", "LOG", "DUMP")
                                string? targetDir = FindExistingTargetSubfolder(locoFolderPath, logOrDump);
                                if (string.IsNullOrEmpty(targetDir))
                                {
                                    skippedLog.Add($"File {Path.GetFileName(zipFile)}: Sottocartella '{logOrDump}' non trovata in '{Path.GetFileName(locoFolderPath)}'.");
                                    continue;
                                }

                                string destFile = Path.Combine(targetDir, Path.GetFileName(zipFile));
                                
                                // Spostiamo il file (sovrascrivendo se esiste già)
                                if (File.Exists(destFile))
                                    File.Delete(destFile);
                                    
                                File.Move(zipFile, destFile);
                                movedCount++;
                            }
                        }
                    }
                });

                if (movedCount > 0)
                {
                    string msg = $"Operazione completata! {movedCount} file ZIP sono stati spostati in rete nelle cartelle esistenti.";
                    if (skippedLog.Count > 0)
                    {
                        msg += "\n\nFile non spostati:\n" + string.Join("\n", skippedLog);
                    }
                    System.Windows.MessageBox.Show(msg, "Esito Spostamento", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
                }
                else
                {
                    string msg = "Nessun file ZIP è stato spostato.";
                    if (skippedLog.Count > 0)
                    {
                        msg += "\n\nDettagli:\n" + string.Join("\n", skippedLog);
                    }
                    else
                    {
                        msg += "\nVerifica che il nome del file ZIP rispetti la convenzione '[Ticket] [LOG/DUMP] [Treno] [Loco]...'.";
                    }
                    System.Windows.MessageBox.Show(msg, "Attenzione", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
                }
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Errore durante lo spostamento in rete:\n{ex.Message}", "Errore", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            }
            finally
            {
                IsLoading = false;
            }
        }

        private static string? FindExistingLocoFolder(string trainTypePath, string loco, string rawTrainType, string fileTrainType)
        {
            if (!Directory.Exists(trainTypePath))
                return null;

            var existingDirs = Directory.GetDirectories(trainTypePath);
            
            string[] exactCandidates = [
                $"{rawTrainType}-{loco}",
                $"{fileTrainType}-{loco}",
                $"{rawTrainType}_{loco}",
                $"{fileTrainType}_{loco}",
                $"{rawTrainType} {loco}",
                $"{fileTrainType} {loco}",
                loco
            ];

            foreach (var dir in existingDirs)
            {
                string dirName = Path.GetFileName(dir);
                if (exactCandidates.Any(c => dirName.Equals(c, StringComparison.OrdinalIgnoreCase)))
                {
                    return dir;
                }
            }

            string cleanLoco = loco.Trim();
            foreach (var dir in existingDirs)
            {
                string dirName = Path.GetFileName(dir);
                var tokens = dirName.Split(['-', '_', ' '], StringSplitOptions.RemoveEmptyEntries);
                if (tokens.Contains(cleanLoco, StringComparer.OrdinalIgnoreCase))
                {
                    return dir;
                }
            }

            foreach (var dir in existingDirs)
            {
                string dirName = Path.GetFileName(dir);
                if (dirName.EndsWith(cleanLoco, StringComparison.OrdinalIgnoreCase))
                {
                    return dir;
                }
            }

            return null;
        }

        private static string? FindExistingTargetSubfolder(string locoFolderPath, string logOrDump)
        {
            if (!Directory.Exists(locoFolderPath))
                return null;

            var existingSubDirs = Directory.GetDirectories(locoFolderPath);
            foreach (var subDir in existingSubDirs)
            {
                string subDirName = Path.GetFileName(subDir);
                if (subDirName.Equals(logOrDump, StringComparison.OrdinalIgnoreCase))
                {
                    return subDir;
                }
            }

            return null;
        }

        private static string GetLogDumpReteBasePath()
        {
            string userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            string[] subPaths = [
                Path.Combine("Hitachi Group", "SSB_SST - LOG_DUMP_per_Reale"),
                "SSB_SST - LOG_DUMP_per_Reale"
            ];

            var searchRoots = new System.Collections.Generic.List<string> { userProfile };

            string[] envVars = [ "OneDriveCommercial", "OneDrive", "OneDriveConsumer", "USERPROFILE" ];
            foreach (var envVar in envVars)
            {
                string? val = Environment.GetEnvironmentVariable(envVar);
                if (!string.IsNullOrEmpty(val) && Directory.Exists(val))
                {
                    searchRoots.Add(val);
                }
            }

            if (Directory.Exists(userProfile))
            {
                try
                {
                    foreach (var dir in Directory.GetDirectories(userProfile, "OneDrive*"))
                    {
                        searchRoots.Add(dir);
                    }
                }
                catch { }
            }

            foreach (var root in searchRoots.Distinct())
            {
                foreach (var sub in subPaths)
                {
                    string candidate = Path.Combine(root, sub);
                    if (Directory.Exists(candidate))
                        return candidate;
                }
            }

            return Path.Combine(userProfile, "Hitachi Group", "SSB_SST - LOG_DUMP_per_Reale");
        }

        private static readonly string[] Etr1000Candidates = ["ETR1001", "ETR1000", "1000FH", "ETR1000_1001"];
        private static readonly string[] Etr500Candidates = ["E404P", "ETR500", "E404"];
        private static readonly string[] Etr700Candidates = ["ETR700", "700"];
        private static readonly string[] Etr421Candidates = ["ETR421", "421"];
        private static readonly string[] Etr521Candidates = ["ETR521", "521"];
        private static readonly string[] Etr522Candidates = ["ETR522", "522"];

        private static string? ResolveTrainTypePath(string basePath, string rawTrainType)
        {
            string primaryName = rawTrainType == "1000FH" ? "ETR1001" : rawTrainType;
            var candidateNames = new System.Collections.Generic.List<string> { primaryName, rawTrainType };

            if (rawTrainType.Contains("1000") || rawTrainType.Contains("1001") || rawTrainType == "1000FH")
            {
                candidateNames.AddRange(Etr1000Candidates);
            }
            else if (rawTrainType.Contains("500") || rawTrainType.Contains("404") || rawTrainType == "E404P")
            {
                candidateNames.AddRange(Etr500Candidates);
            }
            else if (rawTrainType.Contains("700"))
            {
                candidateNames.AddRange(Etr700Candidates);
            }
            else if (rawTrainType.Contains("421"))
            {
                candidateNames.AddRange(Etr421Candidates);
            }
            else if (rawTrainType.Contains("521"))
            {
                candidateNames.AddRange(Etr521Candidates);
            }
            else if (rawTrainType.Contains("522"))
            {
                candidateNames.AddRange(Etr522Candidates);
            }

            if (Directory.Exists(basePath))
            {
                var existingDirs = Directory.GetDirectories(basePath);
                foreach (var candidate in candidateNames.Distinct())
                {
                    foreach (var dir in existingDirs)
                    {
                        string dirName = Path.GetFileName(dir);
                        if (dirName.Equals(candidate, StringComparison.OrdinalIgnoreCase))
                        {
                            return dir;
                        }
                    }
                }
            }

            return null;
        }

        private static bool AreTrainTypesCompatible(string type1, string type2)
        {
            if (string.Equals(type1, type2, StringComparison.OrdinalIgnoreCase))
                return true;

            static string Normalize(string t)
            {
                t = t.ToUpper();
                if (t.Contains("1000") || t.Contains("1001") || t == "1000FH") return "ETR1000";
                if (t.Contains("500") || t.Contains("404") || t == "E404P") return "ETR500";
                if (t.Contains("700")) return "ETR700";
                if (t.Contains("421")) return "ETR421";
                if (t.Contains("521")) return "ETR521";
                if (t.Contains("522")) return "ETR522";
                return t;
            }

            return Normalize(type1) == Normalize(type2);
        }

        private async void OnElimina(object? parameter)
        {
            if (SelectedItem == null)
                return;

            string path = SelectedItem.Percorso;
            
            var result = System.Windows.MessageBox.Show(
                $"Sei sicuro di voler eliminare definitivamente la cartella '{SelectedItem.TipoTreno}' e tutto il suo contenuto?\n\nL'operazione non può essere annullata.", 
                "Conferma Eliminazione", 
                System.Windows.MessageBoxButton.YesNo, 
                System.Windows.MessageBoxImage.Warning);
                
            if (result == System.Windows.MessageBoxResult.Yes)
            {
                IsLoading = true;
                LoadingMessage = "Eliminazione in corso...";
                try
                {
                    // L'intera eliminazione (scansione ricorsiva degli attributi + Directory.Delete)
                    // girava sul thread UI: su una cartella LOG/DUMP con migliaia di file la
                    // finestra restava congelata e Windows la marcava "Non risponde".
                    // Spostata sul thread pool; le eccezioni risalgono comunque all'await e
                    // finiscono nello stesso catch di prima.
                    // Nessun conteggio "N di M" qui (a differenza di zip/spostamento rete, §6, punto
                    // 4.2): Directory.Delete(path, true) è una singola chiamata indivisibile, ed
                    // enumerare prima i file solo per calcolare un totale raddoppierebbe l'I/O senza
                    // un beneficio reale di percezione — l'operazione è già la più veloce delle tre.
                    await System.Threading.Tasks.Task.Run(() =>
                    {
                        if (!Directory.Exists(path)) return;

                        // Rimuovi l'attributo di sola lettura dai file e dalle cartelle (spesso impostato da OneDrive o da file scaricati)
                        var dirInfo = new DirectoryInfo(path);
                        foreach (var file in dirInfo.EnumerateFiles("*", SearchOption.AllDirectories))
                        {
                            file.Attributes &= ~FileAttributes.ReadOnly;
                        }
                        foreach (var dir in dirInfo.EnumerateDirectories("*", SearchOption.AllDirectories))
                        {
                            dir.Attributes &= ~FileAttributes.ReadOnly;
                        }
                        dirInfo.Attributes &= ~FileAttributes.ReadOnly;

                        // Il 'true' serve per eliminare la cartella e tutto il suo contenuto (file zip, sottocartelle, ecc.)
                        Directory.Delete(path, true);
                    });

                    // Ricarichiamo la lista per far sparire la cartella eliminata dalla UI
                    await LoadPendingItemsAsync();
                }
                catch (Exception ex)
                {
                    System.Windows.MessageBox.Show($"Errore durante l'eliminazione:\n{ex.Message}", "Errore", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
                }
                finally
                {
                    IsLoading = false;
                }
            }
        }
    }
}
