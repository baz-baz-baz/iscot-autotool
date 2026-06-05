using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Windows.Input;
using System.Windows.Threading;
using PersonalAutomationTool.Core;

namespace PersonalAutomationTool.Modules.Home
{
    public class HomeViewModel : ViewModelBase
    {
        private readonly DispatcherTimer _timer;

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

        public ICommand AggiornaTicketCommand { get; }
        public ICommand ZipCommand { get; }
        public ICommand LogDumpReteCommand { get; }
        public ICommand EliminaCommand { get; }
        public ICommand AggiornaDataCommand { get; }

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

            LoadPendingItems();
        }

        private void UpdateClock()
        {
            var now = DateTime.Now;
            CurrentTime = now.ToString("HH:mm:ss");
            CurrentDate = now.ToString("dddd d MMMM yyyy", new System.Globalization.CultureInfo("it-IT"));
        }

        private void LoadPendingItems()
        {
            PendingItems.Clear();
            if (!Directory.Exists(AppConfig.LogAndDumpFolder))
                return;

            try
            {
                var mainDirs = Directory.GetDirectories(AppConfig.LogAndDumpFolder);
                foreach (var dir in mainDirs)
                {
                    var dirInfo = new DirectoryInfo(dir);
                    var subDirs = dirInfo.GetDirectories();
                    if (subDirs.Length == 0) continue; // Mostra solo chi ha sottocartelle o tutti?
                    // Secondo lo screen, se ha file o cartelle, contiamole
                    
                    var data = dirInfo.LastWriteTime; // O CreationTime, ma LastWriteTime è più affidabile
                    int giorni = (DateTime.Now - data).Days;

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
                        string name = subDir.Name.ToUpper();
                        if (name.Contains("LOG") || name.Contains("DUMP"))
                        {
                            model.SubFolders.Add($"- {subDir.Name}");
                        }
                    }
                    
                    PendingItems.Add(model);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Errore caricamento Home: {ex.Message}");
            }
        }

        private void OnAggiornaTicket(object? parameter)
        {
            if (SelectedItem == null)
                return;

            string path = SelectedItem.Percorso;
            if (Directory.Exists(path))
            {
                try
                {
                    // Usa OldTicket come "Ticket 1" e NewTicket come "Ticket 2"
                    string? ticket1 = OldTicket?.Trim();
                    string? ticket2 = NewTicket?.Trim();

                    if (string.IsNullOrWhiteSpace(ticket1) && string.IsNullOrWhiteSpace(ticket2))
                        return;

                    var subDirs = Directory.GetDirectories(path);
                    
                    // Trova i ticket correnti (assumendo che il ticket sia la prima parola nel nome della cartella)
                    var currentTickets = subDirs
                        .Select(d => new DirectoryInfo(d).Name.Split(' ').FirstOrDefault())
                        .Where(t => !string.IsNullOrEmpty(t))
                        .Distinct()
                        .OrderBy(t => t) // Ordina alfabeticamente per coerenza
                        .ToList();

                    for (int i = 0; i < subDirs.Length; i++)
                    {
                        var dirInfo = new DirectoryInfo(subDirs[i]);
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
                                        Directory.Move(dirInfo.FullName, newPath);
                                }
                            }
                        }
                    }
                    
                    // Salva lo stato prima di ricaricare
                    var expandedItems = PendingItems.Where(p => p.IsExpanded).Select(p => p.TipoTreno).ToList();
                    string? selectedTreno = SelectedItem?.TipoTreno;

                    // Ricarica la lista per mostrare i cambiamenti
                    LoadPendingItems();
                    
                    // Ripristina lo stato di espansione e la selezione
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
                    
                    // Ripulisci i campi
                    OldTicket = string.Empty;
                    NewTicket = string.Empty;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Errore aggiornamento ticket: {ex.Message}");
                }
            }
        }

        private void OnAggiornaData(object? parameter)
        {
            if (SelectedItem == null)
                return;

            string path = SelectedItem.Percorso;
            if (Directory.Exists(path))
            {
                try
                {
                    var subDirs = Directory.GetDirectories(path);
                    string todayString = DateTime.Now.ToString("ddMMyy");
                    var dateRegex = new System.Text.RegularExpressions.Regex(@"\b\d{6}\b");

                    foreach (var subDir in subDirs)
                    {
                        var dirInfo = new DirectoryInfo(subDir);
                        string currentName = dirInfo.Name;
                        
                        var match = dateRegex.Match(currentName);
                        if (match.Success)
                        {
                            string newName = currentName.Substring(0, match.Index) + todayString + currentName.Substring(match.Index + match.Length);
                            string? parentFolder = dirInfo.Parent?.FullName;
                            if (parentFolder != null)
                            {
                                string newPath = Path.Combine(parentFolder, newName);
                                if (dirInfo.FullName != newPath)
                                    Directory.Move(dirInfo.FullName, newPath);
                            }
                        }
                    }
                    
                    var expandedItems = PendingItems.Where(p => p.IsExpanded).Select(p => p.TipoTreno).ToList();
                    string? selectedTreno = SelectedItem?.TipoTreno;

                    LoadPendingItems();
                    
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
                catch (Exception ex)
                {
                    System.Windows.MessageBox.Show($"Errore durante l'aggiornamento della data:\n{ex.Message}", "Errore", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
                }
            }
        }

        private void OnZip(object? parameter)
        {
            if (SelectedItem == null)
                return;

            string path = SelectedItem.Percorso;
            if (Directory.Exists(path))
            {
                try
                {
                    var subDirs = Directory.GetDirectories(path);
                    foreach (var subDir in subDirs)
                    {
                        var dirInfo = new DirectoryInfo(subDir);
                        string zipFilePath = Path.Combine(dirInfo.Parent?.FullName ?? string.Empty, dirInfo.Name + ".zip");
                        
                        // Se il file zip non esiste già, crealo
                        if (!File.Exists(zipFilePath))
                        {
                            System.IO.Compression.ZipFile.CreateFromDirectory(subDir, zipFilePath);
                        }
                    }
                    
                    System.Windows.MessageBox.Show("Archiviazione Zip completata con successo!", "Successo", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    System.Windows.MessageBox.Show($"Errore durante la creazione dei file Zip:\n{ex.Message}", "Errore", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
                }
            }
        }

        private void OnLogDumpRete(object? parameter)
        {
            if (SelectedItem == null)
                return;

            string path = SelectedItem.Percorso;
            if (!Directory.Exists(path))
                return;

            try
            {
                // Identifica la cartella base di rete (assumiamo sia nel profilo utente)
                string userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                string basePath = Path.Combine(userProfile, "Hitachi Group", "SSB_SST - LOG_DUMP_per_Reale");

                if (!Directory.Exists(basePath))
                {
                    System.Windows.MessageBox.Show($"La cartella di rete di base non esiste:\n{basePath}", "Errore", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
                    return;
                }

                // Estrai il Tipo Treno dal nome della riga selezionata (es: "E404P 30" -> "E404P")
                string rawTrainType = SelectedItem.TipoTreno.Split(' ').FirstOrDefault()?.ToUpper() ?? string.Empty;
                if (string.IsNullOrEmpty(rawTrainType))
                    return;

                // Mappatura specifica per 1000FH
                string networkTrainType = rawTrainType == "1000FH" ? "ETR1001" : rawTrainType;

                string trainTypePath = Path.Combine(basePath, networkTrainType);
                if (!Directory.Exists(trainTypePath))
                {
                    System.Windows.MessageBox.Show($"La cartella del tipo treno ({networkTrainType}) non esiste nel percorso di rete.", "Attenzione", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
                    return;
                }

                var zipFiles = Directory.GetFiles(path, "*.zip");
                if (zipFiles.Length == 0)
                {
                    System.Windows.MessageBox.Show("Nessun file ZIP trovato. Esegui prima l'operazione di ZIP.", "Informazione", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
                    return;
                }

                int movedCount = 0;
                foreach (var zipFile in zipFiles)
                {
                    string fileName = Path.GetFileNameWithoutExtension(zipFile);
                    var parts = fileName.Split(' ');
                    
                    // Ci aspettiamo un nome del tipo: [Ticket] [LOG/DUMP] [Treno] [Loco] ...
                    // Es: "1247654 DUMP E404P 627 04.02HR 300526 Todde"
                    if (parts.Length >= 4)
                    {
                        string logOrDump = parts[1].ToUpper(); // "LOG" o "DUMP"
                        string fileTrainType = parts[2].ToUpper(); // "E404P" o "1000FH"
                        string loco = parts[3]; // "627"

                        string networkFileTrainType = fileTrainType == "1000FH" ? "ETR1001" : fileTrainType;

                        if ((logOrDump == "LOG" || logOrDump == "DUMP") && networkFileTrainType == networkTrainType)
                        {
                            // Ricerca elastica della cartella della loco all'interno di trainTypePath
                            string[] possibleDirNames = [
                                loco,                                      // es. "101" o "627"
                                $"{networkFileTrainType}_{loco}",          // es. "ETR1001_31" o "E404P_627"
                                $"{fileTrainType}_{loco}",                 // es. "1000FH_31"
                                $"{networkFileTrainType} {loco}",          // es. "ETR1001 31"
                                $"{fileTrainType} {loco}"                  // es. "1000FH 31"
                            ];

                            string locoFolderName = string.Empty;
                            if (Directory.Exists(trainTypePath))
                            {
                                foreach (var dir in Directory.GetDirectories(trainTypePath))
                                {
                                    string dirName = Path.GetFileName(dir);
                                    if (possibleDirNames.Contains(dirName, StringComparer.OrdinalIgnoreCase))
                                    {
                                        locoFolderName = dirName;
                                        break;
                                    }
                                }
                            }

                            if (string.IsNullOrEmpty(locoFolderName))
                            {
                                System.Diagnostics.Debug.WriteLine($"Cartella della loco ({loco}) non trovata in {trainTypePath}, lo zip viene ignorato.");
                                continue;
                            }

                            string targetDir = Path.Combine(trainTypePath, locoFolderName, logOrDump);
                            
                            if (!Directory.Exists(targetDir))
                            {
                                System.Diagnostics.Debug.WriteLine($"Cartella di destinazione LOG/DUMP non trovata, lo zip viene ignorato: {targetDir}");
                                continue;
                            }

                            string destFile = Path.Combine(targetDir, Path.GetFileName(zipFile));
                            
                            // Spostiamo il file (sovrascrivendo se esiste)
                            if (File.Exists(destFile))
                                File.Delete(destFile);
                                
                            File.Move(zipFile, destFile);
                            movedCount++;
                        }
                    }
                }

                if (movedCount > 0)
                {
                    System.Windows.MessageBox.Show($"Operazione completata! {movedCount} file ZIP sono stati spostati in rete.", "Successo", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
                }
                else
                {
                    System.Windows.MessageBox.Show("Nessun file ZIP è stato spostato. Verifica che le cartelle di destinazione in rete esistano già e che i nomi corrispondano.", "Attenzione", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
                }
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Errore durante lo spostamento in rete:\n{ex.Message}", "Errore", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            }
        }

        private void OnElimina(object? parameter)
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
                try
                {
                    if (Directory.Exists(path))
                    {
                        // Il 'true' serve per eliminare la cartella e tutto il suo contenuto (file zip, sottocartelle, ecc.)
                        Directory.Delete(path, true);
                    }
                    
                    // Ricarichiamo la lista per far sparire la cartella eliminata dalla UI
                    LoadPendingItems();
                }
                catch (Exception ex)
                {
                    System.Windows.MessageBox.Show($"Errore durante l'eliminazione:\n{ex.Message}", "Errore", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
                }
            }
        }
    }
}
