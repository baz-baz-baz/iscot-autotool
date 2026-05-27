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
    public class ExcelViewModel : ViewModelBase, IDisposable
    {
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

        public ObservableCollection<string> AvailableFolders { get; } = new();

        private string? _selectedFolder;
        public string? SelectedFolder
        {
            get => _selectedFolder;
            set
            {
                if (SetProperty(ref _selectedFolder, value))
                {
                    CheckAndLoadExistingReport();
                }
            }
        }

        public ObservableCollection<ExcelFieldViewModel> FormFields { get; } = new();

        public ICommand SpostaReportCommand { get; }
        public ICommand ScriviReportCommand { get; }
        public ICommand PulisciCampiCommand { get; }

        private string? _currentExcelFilePath;

        public ExcelViewModel()
        {
            // Set default selection
            SelectedTrain = Trains.FirstOrDefault();

            SpostaReportCommand = new RelayCommand(ExecuteSpostaReport, CanExecuteSpostaReport);
            ScriviReportCommand = new RelayCommand(ExecuteScriviReport, CanExecuteScriviReport);
            PulisciCampiCommand = new RelayCommand(ExecutePulisciCampi);

            // Subscribe to folder changes
            AppWatcher.OnLogDumpFolderChanged += AppWatcher_OnLogDumpFolderChanged;
        }

        private bool CanExecuteSpostaReport(object? parameter)
        {
            return SelectedTrain == "ETR700";
        }

        private void ExecuteSpostaReport(object? parameter)
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

                var files = Directory.GetFiles(hitachiDir, "Report Interventi*.xls*");
                if (files.Length == 0)
                {
                    // Controlla se c'è un file già presente in LOG & DUMP o nella cartella selezionata
                    string searchDir = string.IsNullOrEmpty(SelectedFolder) ? AppConfig.LogAndDumpFolder : Path.Combine(AppConfig.LogAndDumpFolder, SelectedFolder);
                    if (Directory.Exists(searchDir))
                    {
                        var existingFiles = Directory.GetFiles(searchDir, $"Report Interventi*{SelectedTrain}*.xls*");
                        if (existingFiles.Length > 0)
                        {
                            LoadExcelFields(existingFiles[0]);
                            MessageBox.Show("Il file 'Report Interventi' era già presente in LOG & DUMP. I campi sono stati caricati.", "Info", MessageBoxButton.OK, MessageBoxImage.Information);
                            return;
                        }
                    }

                    MessageBox.Show("File 'Report Interventi' non trovato nella cartella Hitachi né in LOG & DUMP:\n" + hitachiDir, "Attenzione", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                string originalFile = files[0];
                string fileName = Path.GetFileName(originalFile);

                string currentYear = DateTime.Now.Year.ToString();
                string targetFolderName = $"REPORT OLD ETR700 ANNO {currentYear}";
                string targetFolder = Path.Combine(reportOldBaseDir, targetFolderName);

                if (!Directory.Exists(targetFolder))
                {
                    Directory.CreateDirectory(targetFolder);
                }

                string copyDestination = Path.Combine(targetFolder, fileName);
                string moveDestination = Path.Combine(AppConfig.LogAndDumpFolder, fileName);

                // Make a copy
                File.Copy(originalFile, copyDestination, true);

                // Move original
                File.Move(originalFile, moveDestination, true);

                LoadExcelFields(moveDestination);

                MessageBox.Show("Report spostato e campi caricati con successo!", "Successo", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Si è verificato un errore durante l'operazione:\n{ex.Message}", "Errore", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void LoadExcelFields(string filePath)
        {
            try
            {
                _currentExcelFilePath = filePath;
                FormFields.Clear();

                using var workbook = new XLWorkbook(filePath);
                var worksheet = workbook.Worksheets.FirstOrDefault();
                    if (worksheet == null) return;

                    // Colonne da B ad AA (indice 2 a 27)
                    for (int col = 2; col <= 27; col++)
                    {
                        var headerCell = worksheet.Cell(1, col); // Assumiamo intestazioni su riga 1
                        string fieldName = headerCell.GetString();

                        if (string.IsNullOrWhiteSpace(fieldName))
                        {
                            fieldName = $"Colonna {worksheet.Column(col).ColumnLetter()}";
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

                        FormFields.Add(fieldViewModel);
                    }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Errore durante la lettura del file Excel:\n{ex.Message}", "Errore Excel", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void CheckAndLoadExistingReport()
        {
            try
            {
                FormFields.Clear();
                if (string.IsNullOrEmpty(SelectedTrain)) return;

                // Cerca prima nella cartella selezionata
                string searchDir = string.IsNullOrEmpty(SelectedFolder) 
                    ? AppConfig.LogAndDumpFolder 
                    : Path.Combine(AppConfig.LogAndDumpFolder, SelectedFolder);

                if (Directory.Exists(searchDir))
                {
                    var existingFiles = Directory.GetFiles(searchDir, $"Report Interventi*{SelectedTrain}*.xls*");
                    if (existingFiles.Length > 0)
                    {
                        LoadExcelFields(existingFiles[0]);
                        AutoFillReportFields();
                        return;
                    }
                }

                // Se non lo trova nella cartella selezionata, cerca nella root di LOG & DUMP
                if (Directory.Exists(AppConfig.LogAndDumpFolder))
                {
                    var rootFiles = Directory.GetFiles(AppConfig.LogAndDumpFolder, $"Report Interventi*{SelectedTrain}*.xls*");
                    if (rootFiles.Length > 0)
                    {
                        LoadExcelFields(rootFiles[0]);
                    }
                }
                
                AutoFillReportFields();
            }
            catch
            {
                // Ignora silenziosamente errori nel caricamento automatico
            }
        }

        private void AutoFillReportFields()
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
                    var subDateMatch = Regex.Match(subName, @"\b(\d{2})(\d{2})(\d{2})\b");
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
                var dateMatch = Regex.Match(folderName, @"\b(\d{2})(\d{2})(\d{2})\b");
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

            // Ticket
            var ticketField = FormFields.FirstOrDefault(f => f.FieldName.Contains("TICKET", StringComparison.OrdinalIgnoreCase) && f.FieldName.Contains("ASTS", StringComparison.OrdinalIgnoreCase));
            if (ticketField != null)
            {
                var ticketMatch = Regex.Match(combinedSearchString, @"SR[-_\s]*([0-9]+)", RegexOptions.IgnoreCase);
                if (ticketMatch.Success)
                {
                    ticketField.FieldValue = ticketMatch.Groups[1].Value;
                }
                else
                {
                    var standaloneTicketMatch = Regex.Match(combinedSearchString, @"\b\d{7,8}\b");
                    if (standaloneTicketMatch.Success)
                    {
                        ticketField.FieldValue = standaloneTicketMatch.Value;
                    }
                }
            }

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
                    string cleanOpt = Regex.Replace(opt, @"^(IsMan|Sub|ASTS|Hitachi|Man|TEC)[\s\-]*", "", RegexOptions.IgnoreCase).Replace(".", "");
                    var nameParts = cleanOpt.Split(new[] { ' ', '-', '_' }, StringSplitOptions.RemoveEmptyEntries)
                                            .Where(p => p.Length >= 3).ToList();
                    
                    int score = 0;
                    foreach(var part in nameParts)
                    {
                        if (normSearch.Contains(part.ToLower()))
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
                var folderWords = combinedSearchString.Split(new[] { ' ', '_' }, StringSplitOptions.RemoveEmptyEntries)
                                  .Where(w => w.Contains('.') || w.Contains("CR", StringComparison.OrdinalIgnoreCase) || w.Any(char.IsDigit)).ToList();

                string bestMatch = "";
                int maxScore = 0;

                foreach (var opt in swField.Options)
                {
                    int score = 0;
                    foreach (var fw in folderWords)
                    {
                        if (fw.Length < 3) continue;
                        
                        var subParts = Regex.Split(fw, @"(?=CR)", RegexOptions.IgnoreCase);
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

            // info_ticket.json reading
            string jsonPath = Path.Combine(folderPath, "info_ticket.json");
            if (File.Exists(jsonPath))
            {
                try
                {
                    string json = File.ReadAllText(jsonPath);
                    var groups = JsonSerializer.Deserialize<ObservableCollection<LocoGroupModel>>(json);
                    if (groups != null && groups.Count > 0)
                    {
                        var firstInput = groups[0].Inputs.FirstOrDefault();
                        
                        if (formDict.TryGetValue("SN", out var snField)) snField.FieldValue = groups[0].GroupLocoName;
                        
                        if (firstInput != null)
                        {
                            if (formDict.TryGetValue("N. ODL Trenitalia", out var odlField)) odlField.FieldValue = firstInput.Avviso;
                            if (formDict.TryGetValue("AVARIA SEGNALATA", out var avariaField)) avariaField.FieldValue = firstInput.Avaria;
                            if (formDict.TryGetValue("DESCRIZIONE INTERVENTO EFFETTUATO", out var interventoField)) interventoField.FieldValue = firstInput.Intervento;
                        }
                    }
                }
                catch { }
            }
            else
            {
                // Fallback for SN
                var snMatch = Regex.Match(folderName, @"\b\d{3}\b");
                if (snMatch.Success && formDict.TryGetValue("SN", out var snField2))
                {
                    snField2.FieldValue = snMatch.Value;
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

        private void ExecuteScriviReport(object? parameter)
        {
            if (string.IsNullOrEmpty(_currentExcelFilePath) || !File.Exists(_currentExcelFilePath))
            {
                MessageBox.Show("Nessun file Excel caricato.", "Errore", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                int targetRow = 1;

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
                            if (cellsWithValues.Count() > 0)
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
                    for (int i = 0; i < FormFields.Count; i++)
                    {
                        int col = i + 2; // FormFields parte dalla colonna B (indice 2)
                        var field = FormFields[i];

                        // Scrive solo se c'è un valore
                        if (!string.IsNullOrWhiteSpace(field.FieldValue))
                        {
                            worksheetInterop.Cells[targetRow, col].Value = field.FieldValue;
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
                    
                    MessageBox.Show($"Report salvato con successo alla riga {targetRow}.", "Successo", MessageBoxButton.OK, MessageBoxImage.Information);
                    
                    // Pulisci i campi dopo il salvataggio
                    ExecutePulisciCampi(null);
            }
            catch (IOException ioEx)
            {
                MessageBox.Show($"Impossibile salvare il report perché il file Excel è attualmente aperto. Chiudi Excel e riprova.\n\nDettaglio: {ioEx.Message}", "File Aperto", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Errore durante il salvataggio nel file Excel:\n{ex.Message}\n{ex.StackTrace}", "Errore", MessageBoxButton.OK, MessageBoxImage.Error);
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
    }
}
