using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Input;
using ClosedXML.Excel;
using PersonalAutomationTool.Core;
using PersonalAutomationTool.Modules.Email.Dialogs;

namespace PersonalAutomationTool.Modules.Excel
{
    public partial class ExcelViewModel : ViewModelBase, IDisposable
    {
        private static readonly char[] TechNameSeparators = [' ', '-', '_'];
        private static readonly char[] SwVersionSeparators = [' ', '_'];
        public List<string> Trains { get; } =
        [
            "E404P",
            "ETR700",
            "ETR1000",
            "ETR1000 I-F"
        ];

        private string? _selectedTrain;
        public string? SelectedTrain
        {
            get => _selectedTrain;
            set
            {
                if (SetProperty(ref _selectedTrain, value))
                {
                    UpdateFolders();
                }
            }
        }

        public ObservableCollection<string> AvailableFolders { get; } = [];

        private string? _selectedFolder;
        public string? SelectedFolder
        {
            get => _selectedFolder;
            set
            {
                if (SetProperty(ref _selectedFolder, value))
                {
                    _ = CheckAndLoadExistingReportAsync();
                }
            }
        }

        public ObservableCollection<ExcelFieldViewModel> FormFields { get; } = [];

        public ICommand SpostaReportCommand { get; }
        public ICommand ScriviReportCommand { get; }
        public ICommand RiportaReportCommand { get; }
        public ICommand PulisciCampiCommand { get; }

        private string? _currentExcelFilePath;
        private bool _isWritingReport = false;

        public ExcelViewModel()
        {
            // Set default selection
            SelectedTrain = Trains.FirstOrDefault();

            SpostaReportCommand = new RelayCommand(ExecuteSpostaReport, CanExecuteSpostaReport);
            ScriviReportCommand = new RelayCommand(ExecuteScriviReport, CanExecuteScriviReport);
            RiportaReportCommand = new RelayCommand(ExecuteRiportaReport, CanExecuteRiportaReport);
            PulisciCampiCommand = new RelayCommand(ExecutePulisciCampi);

            // Subscribe to folder changes
            AppWatcher.OnLogDumpFolderChanged += AppWatcher_OnLogDumpFolderChanged;
        }

        private bool CanExecuteSpostaReport(object? parameter)
        {
            return SelectedTrain == "ETR700";
        }

        private async void ExecuteSpostaReport(object? parameter)
        {
            try
            {
                string userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                string hitachiDir = Path.Combine(userProfile, "Hitachi Group", "SSB_SST - INTERVENTI ETR700 ELO BL3");
                string reportOldBaseDir = Path.Combine(hitachiDir, "REPORT INTERVENTI ETR700 OLD");
                
                if (!Directory.Exists(hitachiDir))
                {
                    MessageBox.Show("Cartella Hitachi non trovata:\n" + hitachiDir, "Errore", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                string? originalFile = null;
                string? movedFile = null;
                bool alreadyInLogDump = false;
                string? currentSelectedFolder = SelectedFolder;
                string? currentSelectedTrain = SelectedTrain;

                await Task.Run(() => 
                {
                    var files = Directory.GetFiles(hitachiDir, "Report Interventi*.xls*");
                    if (files.Length == 0)
                    {
                        string searchDir = string.IsNullOrEmpty(currentSelectedFolder) ? AppConfig.LogAndDumpFolder : Path.Combine(AppConfig.LogAndDumpFolder, currentSelectedFolder);
                        if (Directory.Exists(searchDir))
                        {
                            var existingFiles = Directory.GetFiles(searchDir, $"Report Interventi*{currentSelectedTrain}*.xls*");
                            if (existingFiles.Length > 0)
                            {
                                originalFile = existingFiles[0];
                                alreadyInLogDump = true;
                                return;
                            }
                        }
                    }
                    else
                    {
                        originalFile = files[0];
                        string fileName = Path.GetFileName(originalFile);

                        string currentYear = DateTime.Now.Year.ToString();
                        string targetFolderName = $"REPORT OLD ETR700 ANNO {currentYear}";
                        string targetFolder = Path.Combine(reportOldBaseDir, targetFolderName);

                        if (!Directory.Exists(targetFolder)) Directory.CreateDirectory(targetFolder);

                        string copyDestination = Path.Combine(targetFolder, fileName);
                        movedFile = Path.Combine(AppConfig.LogAndDumpFolder, fileName);

                        File.Copy(originalFile, copyDestination, true);
                        File.Move(originalFile, movedFile, true);
                    }
                });

                if (originalFile == null)
                {
                    MessageBox.Show("File 'Report Interventi' non trovato nella cartella Hitachi né in LOG & DUMP:\n" + hitachiDir, "Attenzione", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                if (alreadyInLogDump)
                {
                    await LoadExcelFieldsAsync(originalFile);
                    MessageBox.Show("Il file 'Report Interventi' era già presente in LOG & DUMP. I campi sono stati caricati.", "Info", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                if (movedFile != null)
                {
                    await LoadExcelFieldsAsync(movedFile);
                    MessageBox.Show("Report spostato e campi caricati con successo!", "Successo", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Si è verificato un errore durante l'operazione:\n{ex.Message}", "Errore", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task LoadExcelFieldsAsync(string filePath)
        {
            try
            {
                _currentExcelFilePath = filePath;

                var fields = await Task.Run(() => 
                {
                    var result = new List<ExcelFieldViewModel>();
                    using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                    using var workbook = new XLWorkbook(fs);
                    var worksheet = workbook.Worksheets.FirstOrDefault();
                    if (worksheet == null) return result;

                    // Colonne da B ad AA (indice 2 a 27)
                    for (int col = 2; col <= 27; col++)
                    {
                        var headerCell = worksheet.Cell(1, col); // Assumiamo intestazioni su riga 1
                        string fieldName = headerCell.GetString();

                        if (string.IsNullOrWhiteSpace(fieldName))
                        {
                            fieldName = $"Colonna {worksheet.Column(col).ColumnLetter()}";
                        }

                        if (fieldName == "Colonna T" || fieldName == "Colonna W")
                        {
                            fieldName = "Rev.";
                        }

                        var fieldViewModel = new ExcelFieldViewModel
                        {
                            FieldName = fieldName
                        };

                        // Cerca tutte le Data Validation applicate a questa colonna
                        var columnValidations = worksheet.DataValidations
                            .Where(dv => dv.Ranges.Any(r => r.RangeAddress.FirstAddress.ColumnNumber <= col && r.RangeAddress.LastAddress.ColumnNumber >= col) && dv.AllowedValues == XLAllowedValues.List)
                            .ToList();

                        if (columnValidations.Count > 0)
                        {
                            fieldViewModel.IsComboBox = true;
                            var allOptions = new HashSet<string>();

                            foreach (var validation in columnValidations)
                            {
                                string listValue = validation.Value;
                                if (string.IsNullOrEmpty(listValue)) continue;

                                string formula = listValue.StartsWith('=') ? listValue[1..] : listValue;
                                IXLRange? range = null;

                                try
                                {
                                    // 1. Cerca nei Named Ranges globali
                                    var namedRange = workbook.DefinedNames.FirstOrDefault(n => n.Name.Equals(formula, StringComparison.OrdinalIgnoreCase));
                                    // 2. Cerca nei Named Ranges del foglio
                                    var wsNamedRange = worksheet.DefinedNames.FirstOrDefault(n => n.Name.Equals(formula, StringComparison.OrdinalIgnoreCase));

                                    if (namedRange != null)
                                    {
                                        range = namedRange.Ranges.FirstOrDefault();
                                    }
                                    else if (wsNamedRange != null)
                                    {
                                        range = wsNamedRange.Ranges.FirstOrDefault();
                                    }
                                    else if (formula.Contains('!'))
                                    {
                                        // Riferimento ad un altro foglio
                                        var parts = formula.Split('!');
                                        string sheetName = parts[0].Trim('\'');
                                        string address = parts[1];
                                        if (workbook.TryGetWorksheet(sheetName, out var targetSheet))
                                        {
                                            range = targetSheet.Range(address);
                                        }
                                    }
                                    else if (formula.Contains(':'))
                                    {
                                        // Riferimento nello stesso foglio
                                        range = worksheet.Range(formula);
                                    }
                                }
                                catch
                                {
                                    range = null;
                                }

                                if (range != null)
                                {
                                    foreach (var c in range.CellsUsed())
                                    {
                                        string val = c.GetString();
                                        if (!string.IsNullOrWhiteSpace(val))
                                        {
                                            allOptions.Add(val.Trim());
                                        }
                                    }
                                }
                                else
                                {
                                    // Lista esplicita CSV
                                    var opts = listValue.Trim('"').Split([',', ';'], StringSplitOptions.RemoveEmptyEntries).Select(s => s.Trim());
                                    foreach (var o in opts)
                                    {
                                        allOptions.Add(o);
                                    }
                                }
                            }

                            fieldViewModel.Options = [.. allOptions];
                        }

                        if (fieldName.Contains("Tipologia", StringComparison.OrdinalIgnoreCase))
                        {
                            fieldViewModel.IsComboBox = true;
                            fieldViewModel.Options = ["Assistenza", "Mis", "Extragaranzia", "Supporto", "Semestrale", "Annuale", "Upgrade"];
                        }

                        if (fieldName.Contains("Categoria", StringComparison.OrdinalIgnoreCase) && fieldName.Contains("Avaria", StringComparison.OrdinalIgnoreCase))
                        {
                            fieldViewModel.IsComboBox = true;
                            fieldViewModel.Options = ["Oscuram Monitor", "Verifica", "Catena Radio", "Catena Vigilante", "Catena RSDD", "JRU", "Data Logger", "Nulla di Riscontrato", "RIML", "Odometria", "Perdita Rid. SSB", "Altro"];
                        }

                        if (fieldName.Contains("Descrizione LRU", StringComparison.OrdinalIgnoreCase))
                        {
                            var list = fieldViewModel.Options;
                            if (list != null)
                            {
                                int eloIndex = list.FindIndex(o => o.Contains("ELO", StringComparison.OrdinalIgnoreCase) && o.Contains("Logic Onboard", StringComparison.OrdinalIgnoreCase));
                                if (eloIndex > 0)
                                {
                                    list.RemoveRange(0, eloIndex);
                                }
                            }
                        }

                        if (fieldName.Contains("Versione", StringComparison.OrdinalIgnoreCase) && fieldName.Contains("SW", StringComparison.OrdinalIgnoreCase))
                        {
                            fieldViewModel.IsComboBox = true;
                            fieldViewModel.Options = ["04.01.0002HR", "04.02.0007HR", "04.04.0003HR", "02.02.0004_ELO_BL3", "02.02.0006_ELO_BL3", "02.02.0007_ELO_BL3"];
                        }

                        result.Add(fieldViewModel);
                    }
                    return result;
                });

                FormFields.Clear();
                foreach (var f in fields) FormFields.Add(f);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Errore durante la lettura del file Excel:\n{ex.Message}", "Errore Excel", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task CheckAndLoadExistingReportAsync()
        {
            try
            {
                if (_isWritingReport) return;
                FormFields.Clear();
                if (string.IsNullOrEmpty(SelectedTrain)) return;

                string searchDir = string.IsNullOrEmpty(SelectedFolder) 
                    ? AppConfig.LogAndDumpFolder 
                    : Path.Combine(AppConfig.LogAndDumpFolder, SelectedFolder);
                string? currentTrain = SelectedTrain;

                string? foundFile = await Task.Run(() => 
                {
                    if (Directory.Exists(searchDir))
                    {
                        var existingFiles = Directory.GetFiles(searchDir, $"Report Interventi*{currentTrain}*.xls*");
                        if (existingFiles.Length > 0) return existingFiles[0];
                    }

                    if (Directory.Exists(AppConfig.LogAndDumpFolder))
                    {
                        var rootFiles = Directory.GetFiles(AppConfig.LogAndDumpFolder, $"Report Interventi*{currentTrain}*.xls*");
                        if (rootFiles.Length > 0) return rootFiles[0];
                    }
                    return null;
                });

                if (foundFile != null)
                {
                    await LoadExcelFieldsAsync(foundFile);
                }
                
                await AutoFillReportFieldsAsync();
            }
            catch
            {
                // Ignora silenziosamente errori nel caricamento automatico
            }
        }

        private async Task AutoFillReportFieldsAsync()
        {
            if (FormFields.Count == 0 || string.IsNullOrEmpty(SelectedFolder)) return;

            string folderPath = Path.Combine(AppConfig.LogAndDumpFolder, SelectedFolder);
            if (!Directory.Exists(folderPath)) return;

            string folderName = new DirectoryInfo(folderPath).Name;
            var formDict = new Dictionary<string, ExcelFieldViewModel>(StringComparer.OrdinalIgnoreCase);
            foreach (var f in FormFields)
            {
                string key = f.FieldName.Trim();
                if (!formDict.ContainsKey(key))
                {
                    formDict[key] = f;
                }
            }

            await Task.Run(() => 
            {
                // Estrazione nomi sottocartelle
                string allSubfolderNames = "";
            try
            {
                var subDirs = Directory.GetDirectories(folderPath);
                allSubfolderNames = string.Join(" ", subDirs.Select(d => new DirectoryInfo(d).Name));
            }
            catch { }

            string combinedSearchString = folderName + " " + allSubfolderNames;

            // Cliente
            if (formDict.TryGetValue("Cliente", out var clienteField))
            {
                if (clienteField.IsComboBox)
                {
                    var match = clienteField.Options.FirstOrDefault(o => o.Contains("Hitachi", StringComparison.OrdinalIgnoreCase));
                    if (match != null) clienteField.FieldValue = match;
                    else clienteField.FieldValue = "Hitachi";
                }
                else
                {
                    clienteField.FieldValue = "Hitachi";
                }
            }

            // Data Chiamata e Data Intervento
            string folderDate = "";
            try
            {
                var subDirs = Directory.GetDirectories(folderPath);
                foreach (var subDir in subDirs)
                {
                    string subName = new DirectoryInfo(subDir).Name;
                    var subDateMatch = FolderDateRegex().Match(subName);
                    if (subDateMatch.Success)
                    {
                        folderDate = $"{subDateMatch.Groups[1].Value}/{subDateMatch.Groups[2].Value}/20{subDateMatch.Groups[3].Value}";
                        break;
                    }
                }
            }
            catch { }

            if (string.IsNullOrEmpty(folderDate))
            {
                var dateMatch = FolderDateRegex().Match(folderName);
                if (dateMatch.Success)
                {
                    folderDate = $"{dateMatch.Groups[1].Value}/{dateMatch.Groups[2].Value}/20{dateMatch.Groups[3].Value}";
                }
                else
                {
                    folderDate = Directory.GetCreationTime(folderPath).ToString("dd/MM/yyyy");
                }
            }

            if (formDict.TryGetValue("DATA CHIAMATA", out var dataChiamata)) dataChiamata.FieldValue = folderDate;
            if (formDict.TryGetValue("DATA INTERVENTO", out var dataIntervento)) dataIntervento.FieldValue = folderDate;

            // Sito Intervento
            if (formDict.TryGetValue("SITO INTERVENTO", out var sito)) sito.FieldValue = "Milano Martesana";

            // (Ticket ASTS extraction is moved down to be handled together with info_ticket.json)

            // Rotabile
            if (!string.IsNullOrEmpty(SelectedTrain) && formDict.TryGetValue("ROTABILE", out var rotabileField))
            {
                if (rotabileField.IsComboBox)
                {
                    var matchingOption = rotabileField.Options.FirstOrDefault(o => o.Contains(SelectedTrain, StringComparison.OrdinalIgnoreCase));
                    if (matchingOption != null) rotabileField.FieldValue = matchingOption;
                    else rotabileField.FieldValue = SelectedTrain;
                }
                else
                {
                    rotabileField.FieldValue = SelectedTrain;
                }
            }

            // Tecnico & SW
            var techField = FormFields.FirstOrDefault(f => f.FieldName.Contains("TECNICO", StringComparison.OrdinalIgnoreCase) && f.FieldName.Contains("ASTS", StringComparison.OrdinalIgnoreCase));
            if (techField != null && techField.IsComboBox)
            {
                string normSearch = combinedSearchString.Replace(" ", "").Replace("_", "").Replace(".", "").ToLower();
                
                string bestTech = "";
                int maxTechScore = 0;
                
                foreach (var opt in techField.Options)
                {
                    string cleanOpt = TechPrefixRegex().Replace(opt, "").Replace(".", "");
                    var nameParts = cleanOpt.Split(TechNameSeparators, StringSplitOptions.RemoveEmptyEntries)
                                            .Where(p => p.Length >= 3).ToList();
                    
                    int score = 0;
                    foreach(var part in nameParts)
                    {
                        if (normSearch.Contains(part, StringComparison.OrdinalIgnoreCase))
                        {
                            score += part.Length;
                        }
                    }
                    
                    string solidName = string.Join("", nameParts).ToLower();
                    if (solidName.Length >= 4 && normSearch.Contains(solidName))
                    {
                        score += solidName.Length * 2;
                    }

                    if (score > maxTechScore && score > 0)
                    {
                        maxTechScore = score;
                        bestTech = opt;
                    }
                }
                
                if (maxTechScore > 0)
                {
                    techField.FieldValue = bestTech;
                }
            }

            if (formDict.TryGetValue("VERSIONE SW PRESENTE", out var swField) && swField.IsComboBox)
            {
                var folderWords = combinedSearchString.Split(SwVersionSeparators, StringSplitOptions.RemoveEmptyEntries)
                                  .Where(w => w.Contains('.') || w.Contains("CR", StringComparison.OrdinalIgnoreCase) || w.Any(char.IsDigit)).ToList();

                string bestMatch = "";
                int maxScore = 0;

                foreach (var opt in swField.Options)
                {
                    int score = 0;
                    foreach (var fw in folderWords)
                    {
                        if (fw.Length < 3) continue;
                        
                        var subParts = CrRegex().Split(fw);
                        foreach(var sp in subParts)
                        {
                            if (sp.Length >= 2 && opt.Contains(sp, StringComparison.OrdinalIgnoreCase))
                            {
                                score += sp.Length;
                            }
                        }
                    }
                    if (score > maxScore)
                    {
                        maxScore = score;
                        bestMatch = opt;
                    }
                }
                if (maxScore > 0)
                {
                    swField.FieldValue = bestMatch;
                }
            }

            // --- GESTIONE COMBINATA TICKET ASTS E INFO_TICKET.JSON ---
            
            formDict.TryGetValue("SN", out var snField);
            formDict.TryGetValue("N. ODL Trenitalia", out var odlField);
            var ticketAstsField = FormFields.FirstOrDefault(f => f.FieldName.Contains("TICKET", StringComparison.OrdinalIgnoreCase) && f.FieldName.Contains("ASTS", StringComparison.OrdinalIgnoreCase));
            formDict.TryGetValue("AVARIA SEGNALATA", out var avariaField);
            formDict.TryGetValue("DESCRIZIONE INTERVENTO EFFETTUATO", out var interventoField);

            var ticketLocoMap = new Dictionary<string, string>(); 
            var ticketsList = new List<string>();
            try
            {
                var subDirs = Directory.GetDirectories(folderPath);
                foreach(var subDir in subDirs)
                {
                    string subName = Path.GetFileName(subDir);
                    
                    string ticket = "";
                    var ticketMatch = TicketSrRegex().Match(subName);
                    if (ticketMatch.Success) ticket = ticketMatch.Groups[1].Value;
                    else {
                        var standaloneTicketMatch = StandaloneTicketRegex().Match(subName);
                        if (standaloneTicketMatch.Success && !standaloneTicketMatch.Value.StartsWith("202")) 
                            ticket = standaloneTicketMatch.Value;
                    }
                    
                    string loco = "";
                    var locoMatch = Regex.Match(subName, $@"{SelectedTrain}\s*[-_]?\s*(\d{{3,4}})", RegexOptions.IgnoreCase);
                    if (locoMatch.Success) loco = locoMatch.Groups[1].Value;
                    else
                    {
                        var standaloneLocoMatch = StandaloneLocoRegex().Match(subName);
                        if (standaloneLocoMatch.Success && !standaloneLocoMatch.Value.StartsWith("202") && standaloneLocoMatch.Value != ticket)
                            loco = standaloneLocoMatch.Value;
                    }
                    
                    if (!string.IsNullOrEmpty(ticket))
                    {
                        if (!ticketsList.Contains(ticket)) ticketsList.Add(ticket);
                        if (!string.IsNullOrEmpty(loco) && !ticketLocoMap.ContainsKey(ticket))
                            ticketLocoMap[ticket] = loco;
                    }
                }
            }
            catch { }

            if (ticketsList.Count == 0)
            {
                var ticketMatch = TicketSrRegex().Match(combinedSearchString);
                if (ticketMatch.Success) ticketsList.Add(ticketMatch.Groups[1].Value);
                else
                {
                    var standaloneTicketMatch = StandaloneTicketRegex().Match(combinedSearchString);
                    if (standaloneTicketMatch.Success && !standaloneTicketMatch.Value.StartsWith("202"))
                        ticketsList.Add(standaloneTicketMatch.Value);
                }
            }

            string jsonPath = Path.Combine(folderPath, "info_ticket.json");
            var allInputsWithLoco = new List<(LocoGroupModel Group, TicketInputModel Input)>();
            bool infoTicketLoaded = false;
            
            if (File.Exists(jsonPath))
            {
                try
                {
                    string json = File.ReadAllText(jsonPath);
                    var groups = JsonSerializer.Deserialize<ObservableCollection<LocoGroupModel>>(json);
                    if (groups != null)
                    {
                        foreach (var g in groups)
                        {
                            if (g.Inputs != null)
                            {
                                foreach (var i in g.Inputs)
                                {
                                    if (!string.IsNullOrWhiteSpace(i.Avviso))
                                    {
                                        allInputsWithLoco.Add((g, i));
                                    }
                                }
                            }
                        }
                    }
                }
                catch { }
            }

            bool isUpdating = false;
            void AggiornaCampi(string? ticketSelezionato)
            {
                if (isUpdating || string.IsNullOrWhiteSpace(ticketSelezionato)) return;
                isUpdating = true;
                
                string? targetLoco = null;
                if (ticketLocoMap.TryGetValue(ticketSelezionato, out var l))
                {
                    targetLoco = l;
                }
                
                if (targetLoco != null)
                {
                    if (snField != null) snField.FieldValue = targetLoco;
                    
                    var infoMatch = allInputsWithLoco.FirstOrDefault(x => x.Group.GroupLocoName == targetLoco);
                    if (infoMatch.Group != null && infoMatch.Input != null)
                    {
                        if (avariaField != null) avariaField.FieldValue = infoMatch.Input.Avaria;
                        if (interventoField != null) interventoField.FieldValue = infoMatch.Input.Intervento;
                        if (odlField != null) odlField.FieldValue = infoMatch.Input.Avviso;
                    }
                }
                
                isUpdating = false;
            }

            if (ticketsList.Count > 0 && ticketAstsField != null)
            {
                infoTicketLoaded = true; // Segnaliamo come caricato per evitare il vecchio fallback
                
                ticketAstsField.IsComboBox = true;
                ticketAstsField.Options = ticketsList;
                ticketAstsField.FieldValue = ticketsList[0];
                
                AggiornaCampi(ticketAstsField.FieldValue);
                
                ticketAstsField.PropertyChanged += (s, e) =>
                {
                    if (e.PropertyName == nameof(ExcelFieldViewModel.FieldValue))
                    {
                        AggiornaCampi(ticketAstsField.FieldValue);
                    }
                };
            }
            else if (allInputsWithLoco.Count > 0)
            {
                infoTicketLoaded = true;
                var first = allInputsWithLoco[0];
                if (snField != null) snField.FieldValue = first.Group.GroupLocoName;
                if (odlField != null) odlField.FieldValue = first.Input.Avviso;
                if (avariaField != null) avariaField.FieldValue = first.Input.Avaria;
                if (interventoField != null) interventoField.FieldValue = first.Input.Intervento;
            }

            if (!infoTicketLoaded)
            {
                // Fallback for SN: cerca nel nome della cartella e nelle sottocartelle
                if (formDict.TryGetValue("SN", out var snField2))
                {
                    bool found = false;
                    
                    // 1. Cerca il nome treno + 3/4 cifre (es. ETR700 101)
                    var snMatch = Regex.Match(combinedSearchString, $@"{SelectedTrain}\s*[-_]?\s*(\d{{3,4}})", RegexOptions.IgnoreCase);
                    if (snMatch.Success)
                    {
                        snField2.FieldValue = snMatch.Groups[1].Value;
                        found = true;
                    }
                    
                    // 2. Verifica con le opzioni del menu a tendina o cerca altri numeri validi
                    if (!found || (snField2.Options != null && snField2.Options.Count > 0 && !snField2.Options.Contains(snField2.FieldValue)))
                    {
                        var allMatches = StandaloneLocoRegex().Matches(combinedSearchString);
                        foreach (Match m in allMatches)
                        {
                            if (snField2.Options != null && snField2.Options.Contains(m.Value))
                            {
                                snField2.FieldValue = m.Value;
                                found = true;
                                break;
                            }
                        }
                        
                        if (!found)
                        {
                            foreach (Match m in allMatches)
                            {
                                if (!m.Value.StartsWith("202")) // Evita di confondere l'anno con la loco
                                {
                                    snField2.FieldValue = m.Value;
                                    break;
                                }
                            }
                        }
                    }
                }
            }

            // Scarico Dati Locale
            if (formDict.TryGetValue("Scarico Dati Locale", out var scaricoField))
            {
                bool hasLogs = false;
                try
                {
                    var allDirs = Directory.GetDirectories(folderPath, "*", SearchOption.TopDirectoryOnly);
                    var logDumpDirs = allDirs.Where(d => new DirectoryInfo(d).Name.Contains("LOG", StringComparison.OrdinalIgnoreCase) || 
                                                         new DirectoryInfo(d).Name.Contains("DUMP", StringComparison.OrdinalIgnoreCase)).ToList();
                    
                    foreach (var ldDir in logDumpDirs)
                    {
                        var files = Directory.GetFiles(ldDir, "*", SearchOption.AllDirectories);
                        if (files.Length > 0)
                        {
                            hasLogs = true;
                            break;
                        }
                    }
                }
                catch { }
                
                if (scaricoField.IsComboBox)
                {
                    string val = hasLogs ? "Sì" : "No";
                    var matchingOpt = scaricoField.Options.FirstOrDefault(o => string.Equals(o, val, StringComparison.OrdinalIgnoreCase) || string.Equals(o, val.Replace("ì", "i"), StringComparison.OrdinalIgnoreCase));
                    if (matchingOpt != null) scaricoField.FieldValue = matchingOpt;
                    else scaricoField.FieldValue = val;
                }
                else
                {
                    scaricoField.FieldValue = hasLogs ? "Sì" : "No";
                }
            }
            });
        }

        private void AppWatcher_OnLogDumpFolderChanged()
        {
            // Re-evaluates folders since something changed in LOG & DUMP
            UpdateFolders();
        }

        private void UpdateFolders()
        {
            AvailableFolders.Clear();

            if (string.IsNullOrWhiteSpace(SelectedTrain))
            {
                return;
            }

            try
            {
                string logPath = AppConfig.LogAndDumpFolder;
                if (!Directory.Exists(logPath))
                {
                    return;
                }

                // Get all top-level directories
                var directories = Directory.GetDirectories(logPath);

                foreach (var dir in directories)
                {
                    var dirName = Path.GetFileName(dir);
                    if (string.IsNullOrEmpty(dirName)) continue;

                    // Filter based on the selected train name
                    if (dirName.Contains(SelectedTrain, StringComparison.OrdinalIgnoreCase))
                    {
                        AvailableFolders.Add(dirName);
                    }
                }
                
                // Optional: Select the first one automatically if available
                if (AvailableFolders.Count > 0)
                {
                    SelectedFolder = AvailableFolders[0];
                }
                else
                {
                    SelectedFolder = null;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error updating folders: {ex.Message}");
            }
        }

        private bool CanExecuteScriviReport(object? parameter)
        {
            return !string.IsNullOrEmpty(_currentExcelFilePath) && File.Exists(_currentExcelFilePath);
        }

        private async void ExecuteScriviReport(object? parameter)
        {
            if (string.IsNullOrEmpty(_currentExcelFilePath) || !File.Exists(_currentExcelFilePath))
            {
                MessageBox.Show("Nessun file Excel caricato.", "Errore", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                _isWritingReport = true;
                var fieldsData = FormFields.Select(f => f.FieldValue).ToList();
                int targetRow = 1;

                await Task.Run(() => 
                {
                    // 1. Usa ClosedXML per trovare l'ultima riga reale in modo veloce e preciso, ignorando spazi vuoti o formattazione.
                    {
                    using var fs = new FileStream(_currentExcelFilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                    using var workbook = new XLWorkbook(fs);
                    var ws = workbook.Worksheets.FirstOrDefault();
                    if (ws != null)
                    {
                        int lastRow = 1;
                        for (int col = 2; col <= 27; col++)
                        {
                            // Escludi righe oltre la 900.000 per evitare falsi positivi dovuti a "Ctrl+Giù" accidentali
                            var cellsWithValues = ws.Column(col).CellsUsed(c => !c.Value.IsBlank && c.Address.RowNumber < 900000);
                            if (cellsWithValues.Any())
                            {
                                int maxRow = cellsWithValues.Max(c => c.Address.RowNumber);
                                if (maxRow > lastRow)
                                {
                                    lastRow = maxRow;
                                }
                            }
                        }
                        targetRow = lastRow + 1;
                    }
                }

                // 2. Usa Excel Interop per scrivere i valori in modo nativo, per NON alterare in alcun modo la formattazione e la struttura del file
                Type? excelType = Type.GetTypeFromProgID("Excel.Application");
                if (excelType == null)
                {
                    MessageBox.Show("Excel non risulta installato. Impossibile salvare senza alterare il file.", "Errore", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                dynamic? excelApp = Activator.CreateInstance(excelType);
                if (excelApp == null)
                {
                    MessageBox.Show("Impossibile avviare Excel.", "Errore", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                try
                {
                    excelApp.Visible = false;
                    excelApp.DisplayAlerts = false;

                    dynamic workbookInterop = excelApp.Workbooks.Open(_currentExcelFilePath);
                    dynamic worksheetInterop = workbookInterop.Worksheets[1]; // Interop è 1-based

                    // Scrivi i valori
                    for (int i = 0; i < fieldsData.Count; i++)
                    {
                        int col = i + 2; // FormFields parte dalla colonna B (indice 2)
                        string? val = fieldsData[i];

                        // Scrive solo se c'è un valore
                        if (!string.IsNullOrWhiteSpace(val))
                        {
                            worksheetInterop.Cells[targetRow, col].Value = val;
                        }
                    }

                    workbookInterop.Save();
                    workbookInterop.Close();
                }
                finally
                {
                    excelApp.Quit();
                    System.Runtime.InteropServices.Marshal.ReleaseComObject(excelApp);
                }
                }); // Fine Task.Run
                    
                MessageBox.Show($"Report salvato con successo alla riga {targetRow}.", "Successo", MessageBoxButton.OK, MessageBoxImage.Information);
                    
                // (I campi non vengono puliti automaticamente qui per permettere il 'Riporta Report')
            }
            catch (IOException ioEx)
            {
                MessageBox.Show($"Impossibile salvare il report perché il file Excel è attualmente aperto. Chiudi Excel e riprova.\n\nDettaglio: {ioEx.Message}", "File Aperto", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Errore durante il salvataggio nel file Excel:\n{ex.Message}\n{ex.StackTrace}", "Errore", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                _isWritingReport = false;
            }
        }

        private bool CanExecuteRiportaReport(object? parameter)
        {
            return !string.IsNullOrEmpty(_currentExcelFilePath) && File.Exists(_currentExcelFilePath) && SelectedTrain == "ETR700";
        }

        private async void ExecuteRiportaReport(object? parameter)
        {
            try
            {
                if (string.IsNullOrEmpty(_currentExcelFilePath) || !File.Exists(_currentExcelFilePath))
                {
                    MessageBox.Show("Nessun file Excel caricato.", "Errore", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // Trova il tecnico selezionato
                var techField = FormFields.FirstOrDefault(f => f.FieldName.Contains("TECNICO", StringComparison.OrdinalIgnoreCase) && f.FieldName.Contains("ASTS", StringComparison.OrdinalIgnoreCase));
                string technician = techField?.FieldValue ?? "Tecnico";
                
                // Pulisci il nome del tecnico
                string cleanTech = technician;
                if (string.IsNullOrWhiteSpace(cleanTech)) 
                {
                    cleanTech = "Tecnico";
                }
                else
                {
                    if (cleanTech.Contains('-'))
                    {
                        cleanTech = cleanTech.Split('-').Last().Trim();
                    }
                    
                    // Se l'ultima parola è di un solo carattere (es. l'iniziale del nome), la rimuoviamo per avere solo il cognome
                    var parts = cleanTech.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length > 1 && parts.Last().Length == 1)
                    {
                        cleanTech = string.Join(" ", parts.Take(parts.Length - 1));
                    }
                }

                string currentDateTime = DateTime.Now.ToString("ddMMyy HH_mm");
                string newFileName = $"Report Interventi ETR700 {currentDateTime} {cleanTech}{Path.GetExtension(_currentExcelFilePath)}";

                string userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                string hitachiDir = Path.Combine(userProfile, "Hitachi Group", "SSB_SST - INTERVENTI ETR700 ELO BL3");

                if (!Directory.Exists(hitachiDir))
                {
                    MessageBox.Show("Cartella d'origine Hitachi non trovata:\n" + hitachiDir, "Errore", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                string destinationPath = Path.Combine(hitachiDir, newFileName);

                await Task.Run(() => 
                {
                    File.Move(_currentExcelFilePath, destinationPath, true);
                });

                MessageBox.Show($"Report riportato con successo nella cartella d'origine come:\n{newFileName}", "Successo", MessageBoxButton.OK, MessageBoxImage.Information);
                
                _currentExcelFilePath = null;
                FormFields.Clear();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Si è verificato un errore durante l'operazione:\n{ex.Message}", "Errore", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ExecutePulisciCampi(object? parameter)
        {
            foreach (var field in FormFields)
            {
                field.FieldValue = string.Empty;
            }
        }

        public void Dispose()
        {
            AppWatcher.OnLogDumpFolderChanged -= AppWatcher_OnLogDumpFolderChanged;
            GC.SuppressFinalize(this);
        }

        [GeneratedRegex(@"\b(\d{2})(\d{2})(\d{2})\b")]
        private static partial Regex FolderDateRegex();

        [GeneratedRegex(@"^(IsMan|Sub|ASTS|Hitachi|Man|TEC)[\s\-]*", RegexOptions.IgnoreCase)]
        private static partial Regex TechPrefixRegex();

        [GeneratedRegex(@"(?=CR)", RegexOptions.IgnoreCase)]
        private static partial Regex CrRegex();

        [GeneratedRegex(@"SR[-_\s]*([0-9]+)", RegexOptions.IgnoreCase)]
        private static partial Regex TicketSrRegex();

        [GeneratedRegex(@"\b\d{7,8}\b")]
        private static partial Regex StandaloneTicketRegex();

        [GeneratedRegex(@"\b\d{3,4}\b")]
        private static partial Regex StandaloneLocoRegex();
    }
}
