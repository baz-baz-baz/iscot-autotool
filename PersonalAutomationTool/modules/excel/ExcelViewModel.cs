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

        /// <summary>
        /// Campi evidenziati in giallo nel form. Era un array allocato dentro il ciclo sulle colonne
        /// (una nuova istanza per ognuna delle ~26 colonne, a ogni caricamento del report).
        /// </summary>
        private static readonly string[] ImportantKeywords = [
            "DATA CHIAMATA", "SITO INTERVENTO", "SITO", "TICKET", "N. ODL", "Cliente",
            "DATA INTERVENTO", "Inizio", "Fine", "ROTABILE", "SN", "LOCO",
            "Tipologia", "AVARIA SEGNALATA", "CATEGORIA AVARIA", "Scarico Dati",
            "Descrizione intervento effettuato", "TECNICO ASTS", "VERSIONE SW", "TECNICO HRSTS", "SW Installato"
        ];

        public List<string> Trains { get; } =
        [
            "E404P",
            "ETR700",
            "ETR1000 / 1000FH",
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

        private bool _isLoading;
        public bool IsLoading
        {
            get => _isLoading;
            set => SetProperty(ref _isLoading, value);
        }

        private string _loadingMessage = "Caricamento...";
        public string LoadingMessage
        {
            get => _loadingMessage;
            set => SetProperty(ref _loadingMessage, value);
        }

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
            return SelectedTrain == "ETR700" || SelectedTrain == "E404P" || SelectedTrain == "ETR1000 / 1000FH" || SelectedTrain == "ETR1000 I-F";
        }

        private async void ExecuteSpostaReport(object? parameter)
        {
            IsLoading = true;
            LoadingMessage = "Spostamento report in corso...";
            try
            {
                string userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                string hitachiDir = "";
                string targetFolder = "";
                string currentYear = DateTime.Now.Year.ToString();

                if (SelectedTrain == "ETR700")
                {
                    hitachiDir = Path.Combine(userProfile, "Hitachi Group", "SSB_SST - INTERVENTI ETR700 ELO BL3");
                    string reportOldBaseDir = Path.Combine(hitachiDir, "REPORT INTERVENTI ETR700 OLD");
                    targetFolder = Path.Combine(reportOldBaseDir, $"REPORT OLD ETR700 ANNO {currentYear}");
                }
                else if (SelectedTrain == "E404P")
                {
                    hitachiDir = Path.Combine(userProfile, "Hitachi Group", "SSB_SST - Interventi ETR500", "REPORT INTERVENTI NAPOLI - MILANO");
                    targetFolder = Path.Combine(hitachiDir, $"REPORT INTERVENTI OLD_ModifyYear{currentYear}");
                }
                else if (SelectedTrain == "ETR1000 / 1000FH")
                {
                    hitachiDir = Path.Combine(userProfile, "Hitachi Group", "SSB_SST - Interventi ETR1000");
                    targetFolder = Path.Combine(hitachiDir, "OLD REPORT");
                }
                else if (SelectedTrain == "ETR1000 I-F")
                {
                    hitachiDir = Path.Combine(userProfile, "Hitachi Group", "SSB_SST - Interventi ETR1000", "ETR1000 ITA-FRA");
                    targetFolder = Path.Combine(hitachiDir, "OLD Report");
                }
                else
                {
                    return;
                }
                
                if (!Directory.Exists(hitachiDir))
                {
                    IsLoading = false;
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
                    var files = Directory.GetFiles(hitachiDir, "Report Interventi*.xls*")
                        .Select(f => new FileInfo(f))
                        .OrderByDescending(fi => fi.LastWriteTime)
                        .Select(fi => fi.FullName)
                        .ToArray();
                    if (files.Length == 0)
                    {
                        string searchDir = string.IsNullOrEmpty(currentSelectedFolder) ? AppConfig.LogAndDumpFolder : Path.Combine(AppConfig.LogAndDumpFolder, currentSelectedFolder);
                        if (Directory.Exists(searchDir))
                        {
                            var existingFiles = Directory.GetFiles(searchDir, "Report Interventi*.xls*")
                                .Where(f => MatchesTrain(f, currentSelectedTrain))
                                .Select(f => new FileInfo(f))
                                .OrderByDescending(fi => fi.LastWriteTime)
                                .Select(fi => fi.FullName)
                                .ToArray();
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

                        if (!Directory.Exists(targetFolder)) Directory.CreateDirectory(targetFolder);

                        string copyDestination = Path.Combine(targetFolder, fileName);
                        movedFile = Path.Combine(AppConfig.LogAndDumpFolder, fileName);

                        File.Copy(originalFile, copyDestination, true);
                        File.Move(originalFile, movedFile, true);
                    }
                });

                if (originalFile == null)
                {
                    IsLoading = false;
                    await Task.Delay(100);
                    MessageBox.Show("File 'Report Interventi' non trovato nella cartella Hitachi né in LOG & DUMP:\n" + hitachiDir, "Attenzione", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                if (alreadyInLogDump)
                {
                    await LoadExcelFieldsAsync(originalFile);
                    await AutoFillReportFieldsAsync();
                    IsLoading = false;
                    await Task.Delay(100);
                    MessageBox.Show("Il file 'Report Interventi' era già presente in LOG & DUMP. I campi sono stati caricati.", "Info", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                if (movedFile != null)
                {
                    await LoadExcelFieldsAsync(movedFile);
                    await AutoFillReportFieldsAsync();
                    IsLoading = false;
                    await Task.Delay(100);
                    MessageBox.Show("Report spostato e campi caricati con successo!", "Successo", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                IsLoading = false;
                await Task.Delay(100);
                MessageBox.Show($"Si è verificato un errore durante l'operazione:\n{ex.Message}", "Errore", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsLoading = false;
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

                    string currentTrain = SelectedTrain ?? "";
                    int maxCol = currentTrain == "ETR1000 I-F" ? 24 : 27;

                    // Le convalide dati di tipo "lista" vengono raccolte UNA volta sola: prima la
                    // collezione worksheet.DataValidations veniva rienumerata da capo per ognuna
                    // delle ~26 colonne. L'ordine sorgente è preservato, quindi le opzioni
                    // risultanti sono identiche.
                    var listValidations = worksheet.DataValidations
                        .Where(dv => dv.AllowedValues == XLAllowedValues.List)
                        .ToList();

                    // Colonne da B a X o AA
                    for (int col = 2; col <= maxCol; col++)
                    {
                        var headerCell = worksheet.Cell(1, col); // Assumiamo intestazioni su riga 1
                        string fieldName = headerCell.GetString();

                        if (string.IsNullOrWhiteSpace(fieldName))
                        {
                            fieldName = $"Colonna {worksheet.Column(col).ColumnLetter()}";
                        }
                        else
                        {
                            fieldName = fieldName.Replace("\r\n", " ").Replace("\r", " ").Replace("\n", " ");
                            if (fieldName.Contains("TECNICO HRSTS", StringComparison.OrdinalIgnoreCase) || fieldName.Contains("subappalto", StringComparison.OrdinalIgnoreCase))
                            {
                                fieldName = "TECNICO HRSTS";
                            }
                        }

                        if (fieldName == "Colonna T" || fieldName == "Colonna W" || fieldName == "Colonna R" || fieldName == "Colonna U")
                        {
                            fieldName = "Rev.";
                        }
                        else if (fieldName == "Colonna Y")
                        {
                            fieldName = "Tecnico Cliente";
                        }
                        else if (fieldName == "Colonna Z")
                        {
                            fieldName = "NOTE";
                        }
                        else if (fieldName == "Colonna AA")
                        {
                            fieldName = "SW Installato";
                        }

                        var fieldViewModel = new ExcelFieldViewModel
                        {
                            FieldName = fieldName
                        };

                        // Cerca tutte le Data Validation applicate a questa colonna
                        var columnValidations = listValidations
                            .Where(dv => dv.Ranges.Any(r => r.RangeAddress.FirstAddress.ColumnNumber <= col && r.RangeAddress.LastAddress.ColumnNumber >= col))
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
                                            val = val.Trim();
                                            if (!val.StartsWith('#') && !val.StartsWith('='))
                                            {
                                                allOptions.Add(val);
                                            }
                                        }
                                    }
                                }
                                else
                                {
                                    // Lista esplicita CSV
                                    var opts = listValue.Trim('"').Split([',', ';'], StringSplitOptions.RemoveEmptyEntries).Select(s => s.Trim());
                                    foreach (var o in opts)
                                    {
                                        if (!o.StartsWith('#') && !o.StartsWith('='))
                                        {
                                            allOptions.Add(o);
                                        }
                                    }
                                }
                            }

                            fieldViewModel.Options = [.. allOptions];
                        }

                        // Forza campi specifici ad essere sempre una TextBox (non ComboBox) 
                        // anche se nel file Excel originale c'è una convalida dati, per permettere l'autocompilazione libera.
                        if (fieldName.Contains("Descrizione intervento effettuato", StringComparison.OrdinalIgnoreCase) || 
                            fieldName.Contains("LRU", StringComparison.OrdinalIgnoreCase) ||
                            fieldName.Equals("Rev.", StringComparison.OrdinalIgnoreCase))
                        {
                            fieldViewModel.IsComboBox = false;
                            fieldViewModel.Options = [];
                        }

                        if (fieldName.Equals("Cliente", StringComparison.OrdinalIgnoreCase) || fieldName.Contains("Cliente", StringComparison.OrdinalIgnoreCase))
                        {
                            fieldViewModel.IsComboBox = true;
                            fieldViewModel.Options = ["Hitachi", "Trenitalia"];
                        }

                        if (fieldName.Contains("Sito", StringComparison.OrdinalIgnoreCase))
                        {
                            fieldViewModel.IsComboBox = true;
                            fieldViewModel.Options = ["Pistoia", "Napoli Gianturco", "Milano Martesana", "Roma S.Lorenzo", "Piacenza", "Firenze", "OMC ETR Vicenza", "IMC AV Mestre"];
                        }

                        if (fieldName.Contains("TECNICO", StringComparison.OrdinalIgnoreCase) && !fieldName.Contains("Cliente", StringComparison.OrdinalIgnoreCase))
                        {
                            if (fieldViewModel.Options != null && fieldViewModel.Options.Count > 0)
                            {
                                fieldViewModel.Options = [.. fieldViewModel.Options
                                    .Where(opt => !opt.StartsWith('#') 
                                               && !SoftwareVersionRegex().IsMatch(opt) 
                                               && !opt.Contains("ELO BL", StringComparison.OrdinalIgnoreCase))];
                            }
                        }

                        if (fieldName.Contains("Tipologia", StringComparison.OrdinalIgnoreCase))
                        {
                            fieldViewModel.IsComboBox = true;
                            if (SelectedTrain == "E404P" || SelectedTrain == "ETR1000 / 1000FH" || SelectedTrain == "ETR1000 I-F")
                            {
                                fieldViewModel.Options = ["Mis", "Extragaranzia", "Upgrade", "Man Programmata", "Man Predittiva", "Controlli Remoto", "Nulla Riscontrato", "Correttiva con sostit", "Correttiva senza sostit"];
                            }
                            else
                            {
                                fieldViewModel.Options = ["Assistenza", "Mis", "Extragaranzia", "Supporto", "Semestrale", "Annuale", "Upgrade"];
                            }
                        }

                        if (fieldName.Contains("Categoria", StringComparison.OrdinalIgnoreCase) && fieldName.Contains("Avaria", StringComparison.OrdinalIgnoreCase))
                        {
                            fieldViewModel.IsComboBox = true;
                            if (SelectedTrain == "E404P")
                            {
                                fieldViewModel.Options = ["Oscuram Monitor", "Verifica", "Catena Radio", "Catena Vigilante", "Catena RSDD", "JRU", "Data Logger", "RIML", "Odometria", "Perdita Rid. SSB", "Altro"];
                            }
                            else if (SelectedTrain == "ETR1000 / 1000FH" || SelectedTrain == "ETR1000 I-F")
                            {
                                fieldViewModel.Options = ["Oscuram Monitor", "Verifica", "Catena Radio", "Catena Vigilante", "Catena RSDD", "JRU DIS", "Data Logger", "RIML", "Odometria", "Perdita Rid. SSB", "Altro"];
                            }
                            else
                            {
                                fieldViewModel.Options = ["Oscuram Monitor", "Verifica", "Catena Radio", "Catena Vigilante", "Catena RSDD", "JRU", "Data Logger", "Nulla di Riscontrato", "RIML", "Odometria", "Perdita Rid. SSB", "Altro"];
                            }
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
                            if (SelectedTrain == "E404P")
                            {
                                fieldViewModel.Options = ["04.00.34CR", "04.00.35A1", "04.00.35HR", "04.00.36HR", "04.01.0002HR", "04.02.0007HR"];
                            }
                            else if (SelectedTrain == "ETR1000 / 1000FH")
                            {
                                fieldViewModel.Options = ["04.01 HR", "04.03 HR", "01.01.0000 Elo BL3", "02.02.0006 Elo BL3", "02.02.0007 Elo BL3"];
                            }
                            else
                            {
                                fieldViewModel.Options = ["04.01.0002HR", "04.02.0007HR", "04.04.0003HR", "02.02.0004_ELO_BL3", "02.02.0006_ELO_BL3", "02.02.0007_ELO_BL3"];
                            }
                        }

                        fieldViewModel.IsImportant = ImportantKeywords.Any(k =>
                            (k == "SN" || k == "LOCO" || k == "Cliente") ? fieldName.Equals(k, StringComparison.OrdinalIgnoreCase) 
                                                                         : fieldName.Contains(k, StringComparison.OrdinalIgnoreCase));

                        result.Add(fieldViewModel);
                    }
                    return result;
                });

                FormFields.Clear();
                foreach (var f in fields) FormFields.Add(f);
            }
            catch (Exception ex)
            {
                IsLoading = false;
                MessageBox.Show($"Errore durante la lettura del file Excel:\n{ex.Message}", "Errore Excel", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task CheckAndLoadExistingReportAsync()
        {
            try
            {
                if (_isWritingReport) return;
                IsLoading = true;
                LoadingMessage = "Caricamento report in corso...";
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
                        var existingFiles = Directory.GetFiles(searchDir, "Report Interventi*.xls*")
                            .Where(f => MatchesTrain(f, currentTrain))
                            .Select(f => new FileInfo(f))
                            .OrderByDescending(fi => fi.LastWriteTime)
                            .Select(fi => fi.FullName)
                            .ToArray();
                        if (existingFiles.Length > 0) return existingFiles[0];
                    }

                    if (Directory.Exists(AppConfig.LogAndDumpFolder))
                    {
                        var rootFiles = Directory.GetFiles(AppConfig.LogAndDumpFolder, "Report Interventi*.xls*")
                            .Where(f => MatchesTrain(f, currentTrain))
                            .Select(f => new FileInfo(f))
                            .OrderByDescending(fi => fi.LastWriteTime)
                            .Select(fi => fi.FullName)
                            .ToArray();
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
            catch (Exception)
            {
                // Ignora silenziosamente errori nel caricamento automatico
            }
            finally
            {
                IsLoading = false;
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

            string currentTrain = SelectedTrain ?? "";

            await Task.Run(() => 
            {
                // Estrazione nomi sottocartelle
                string allSubfolderNames = "";
            try
            {
                var subDirs = Directory.GetDirectories(folderPath);
                allSubfolderNames = string.Join(" ", subDirs.Select(Path.GetFileName));
            }
            catch { }

            string combinedSearchString = folderName + " " + allSubfolderNames;

            // Cliente
            if (formDict.TryGetValue("Cliente", out var clienteField))
            {
                string targetCliente = currentTrain == "E404P" ? "Trenitalia" : "Hitachi";

                if (clienteField.IsComboBox)
                {
                    var match = clienteField.Options.FirstOrDefault(o => o.Contains(targetCliente, StringComparison.OrdinalIgnoreCase));
                    if (match != null) clienteField.FieldValue = match;
                    else clienteField.FieldValue = targetCliente;
                }
                else
                {
                    clienteField.FieldValue = targetCliente;
                }
            }

            // Data Chiamata e Data Intervento
            string folderDate = "";
            try
            {
                var subDirs = Directory.GetDirectories(folderPath);
                foreach (var subDir in subDirs)
                {
                    string subName = Path.GetFileName(subDir);
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

            // Sito Intervento / Sito
            if (formDict.TryGetValue("SITO INTERVENTO", out var sito) || formDict.TryGetValue("SITO", out sito))
            {
                sito.FieldValue = "Milano Martesana";
            }

            // (Ticket ASTS extraction is moved down to be handled together with info_ticket.json)

            // Rotabile
            if (!string.IsNullOrEmpty(SelectedTrain) && formDict.TryGetValue("ROTABILE", out var rotabileField))
            {
                if (rotabileField.IsComboBox)
                {
                    string? matchingOption = null;
                    if (SelectedTrain == "ETR1000 / 1000FH")
                    {
                        matchingOption = rotabileField.Options.FirstOrDefault(o => o.Contains("ETR 1000", StringComparison.OrdinalIgnoreCase) || o.Contains("ETR1000", StringComparison.OrdinalIgnoreCase));
                    }
                    else if (SelectedTrain == "ETR1000 I-F")
                    {
                        matchingOption = rotabileField.Options.FirstOrDefault(o => (o.Contains("1000", StringComparison.OrdinalIgnoreCase) && (o.Contains("IF", StringComparison.OrdinalIgnoreCase) || o.Contains("I-F", StringComparison.OrdinalIgnoreCase) || o.Contains("ITA", StringComparison.OrdinalIgnoreCase))) || o.Contains("Italia", StringComparison.OrdinalIgnoreCase) || o.Contains("Francia", StringComparison.OrdinalIgnoreCase));
                        matchingOption ??= rotabileField.Options.FirstOrDefault(o => o.Contains("ETR 1000", StringComparison.OrdinalIgnoreCase) || o.Contains("ETR1000", StringComparison.OrdinalIgnoreCase));
                    }
                    
                    matchingOption ??= rotabileField.Options.FirstOrDefault(o => o.Contains(SelectedTrain, StringComparison.OrdinalIgnoreCase));

                    if (matchingOption != null) rotabileField.FieldValue = matchingOption;
                    else rotabileField.FieldValue = SelectedTrain;
                }
                else
                {
                    rotabileField.FieldValue = SelectedTrain;
                }
            }

            // Tecnico & SW
            var techField = FormFields.FirstOrDefault(f => f.FieldName.Contains("TECNICO", StringComparison.OrdinalIgnoreCase) && (f.FieldName.Contains("ASTS", StringComparison.OrdinalIgnoreCase) || f.FieldName.Contains("STS", StringComparison.OrdinalIgnoreCase)));
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

                    if (opt.Contains("Iscot", StringComparison.OrdinalIgnoreCase))
                    {
                        score += 1; // Dai priorità a Iscot a parità di nome
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

            if (!formDict.TryGetValue("VERSIONE SW PRESENTE", out var swField))
            {
                formDict.TryGetValue("SW Installato", out swField);
            }

            if (swField != null && swField.IsComboBox)
            {
                if (SelectedTrain == "ETR1000 I-F")
                {
                    var optBi = swField.Options?.FirstOrDefault(o => o.Contains("Bi-Standard", StringComparison.OrdinalIgnoreCase) || o.Contains("KVB", StringComparison.OrdinalIgnoreCase));
                    if (optBi != null)
                    {
                        swField.FieldValue = optBi;
                    }
                }
                // Regola esplicita per 02.02CR3
                else if (combinedSearchString.Contains("02.02CR3", StringComparison.OrdinalIgnoreCase) && swField.Options != null)
                {
                    var opt6 = swField.Options.FirstOrDefault(o => o.Contains("02.02.0006", StringComparison.OrdinalIgnoreCase));
                    if (opt6 != null)
                    {
                        swField.FieldValue = opt6;
                    }
                }
                else
                {
                    var folderWords = combinedSearchString.Split(SwVersionSeparators, StringSplitOptions.RemoveEmptyEntries)
                                      .Where(w => w.Contains('.') || w.Contains("CR", StringComparison.OrdinalIgnoreCase) || w.Contains("HR", StringComparison.OrdinalIgnoreCase) || w.Contains("A1", StringComparison.OrdinalIgnoreCase) || w.Any(char.IsDigit)).ToList();

                    string bestMatch = "";
                    int maxScore = 0;

                    foreach (var opt in swField.Options ?? Enumerable.Empty<string>())
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
            }

            // --- GESTIONE COMBINATA TICKET ASTS E INFO_TICKET.JSON ---
            
            if (!formDict.TryGetValue("SN", out var snField))
            {
                formDict.TryGetValue("LOCO", out snField);
            }
            formDict.TryGetValue("N. ODL Trenitalia", out var odlField);
            var ticketAstsField = FormFields.FirstOrDefault(f => f.FieldName.Equals("TICKET", StringComparison.OrdinalIgnoreCase) || (f.FieldName.Contains("TICKET", StringComparison.OrdinalIgnoreCase) && (f.FieldName.Contains("ASTS", StringComparison.OrdinalIgnoreCase) || f.FieldName.Contains("STS", StringComparison.OrdinalIgnoreCase))));


            formDict.TryGetValue("AVARIA SEGNALATA", out var avariaField);
            formDict.TryGetValue("DESCRIZIONE INTERVENTO EFFETTUATO", out var interventoField);

            var ticketLocoMap = new Dictionary<string, string>(); 
            var ticketsList = new List<string>();
            try
            {
                var subDirs = Directory.GetDirectories(folderPath);

                // Il pattern dipende solo da SelectedTrain, costante per tutto il ciclo: costruirlo
                // e ricercarlo nella cache statica di Regex a ogni sottocartella era lavoro sprecato.
                var locoRegex = new Regex($@"{SelectedTrain}\s*[-_]?\s*(\d{{3,4}})", RegexOptions.IgnoreCase);

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
                    var locoMatch = locoRegex.Match(subName);
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
                                    if (!string.IsNullOrWhiteSpace(i.Avviso) || !string.IsNullOrWhiteSpace(i.Avaria) || !string.IsNullOrWhiteSpace(i.Intervento))
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
                    
                    var ticketsForLoco = ticketsList.Where(t => ticketLocoMap.ContainsKey(t) && ticketLocoMap[t] == targetLoco).ToList();
                    int ticketIndex = ticketsForLoco.IndexOf(ticketSelezionato);
                    if (ticketIndex < 0) ticketIndex = 0;
                    
                    var inputsForLoco = allInputsWithLoco.Where(x => x.Group.GroupLocoName == targetLoco).ToList();
                    if (inputsForLoco.Count > 0)
                    {
                        var infoMatch = ticketIndex < inputsForLoco.Count ? inputsForLoco[ticketIndex] : inputsForLoco.Last();
                        if (infoMatch.Group != null && infoMatch.Input != null)
                        {
                            if (avariaField != null && !string.IsNullOrWhiteSpace(infoMatch.Input.Avaria)) avariaField.FieldValue = infoMatch.Input.Avaria;
                            if (interventoField != null && !string.IsNullOrWhiteSpace(infoMatch.Input.Intervento)) interventoField.FieldValue = infoMatch.Input.Intervento;
                            if (odlField != null && !string.IsNullOrWhiteSpace(infoMatch.Input.Avviso)) odlField.FieldValue = infoMatch.Input.Avviso;
                        }
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
                if (snField != null)
                {
                    var snField2 = snField;
                    bool found = false;
                    
                    // 1. Cerca il nome treno + 3/4 cifre (es. ETR700 101)
                    var snMatch = Regex.Match(combinedSearchString, $@"{SelectedTrain}\s*[-_]?\s*(\d{{2,4}})", RegexOptions.IgnoreCase);
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
                if (SelectedTrain == "ETR1000 I-F")
                {
                    hasLogs = true;
                }
                else
                {
                    try
                    {
                        // Prima venivano costruiti DUE DirectoryInfo per ogni cartella (uno per
                        // ciascun Contains) e l'elenco completo dei file veniva materializzato in
                        // un array solo per controllarne la lunghezza. Ora si usa Path.GetFileName
                        // e si esce al primo file trovato, senza enumerare l'intero sottoalbero.
                        var allDirs = Directory.GetDirectories(folderPath, "*", SearchOption.TopDirectoryOnly);

                        foreach (var ldDir in allDirs)
                        {
                            string ldName = Path.GetFileName(ldDir);
                            if (!ldName.Contains("LOG", StringComparison.OrdinalIgnoreCase) &&
                                !ldName.Contains("DUMP", StringComparison.OrdinalIgnoreCase))
                            {
                                continue;
                            }

                            if (Directory.EnumerateFiles(ldDir, "*", SearchOption.AllDirectories).Any())
                            {
                                hasLogs = true;
                                break;
                            }
                        }
                    }
                    catch { }
                }
                
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
            if (string.IsNullOrWhiteSpace(SelectedTrain))
            {
                AvailableFolders.Clear();
                return;
            }

            try
            {
                string logPath = AppConfig.LogAndDumpFolder;
                if (!Directory.Exists(logPath))
                {
                    AvailableFolders.Clear();
                    return;
                }

                // La lista viene prima calcolata a parte, poi confrontata con quella corrente.
                // Motivo: AvailableFolders.Clear() svuota l'ItemsSource della ComboBox, che azzera
                // SelectedFolder; questo faceva partire CheckAndLoadExistingReportAsync una prima
                // volta con folder null e una seconda volta subito dopo con la nuova selezione,
                // cioè DUE aperture complete del workbook Excel per ogni singolo evento del
                // FileSystemWatcher su LOG & DUMP (che scatta anche per modifiche nelle
                // sottocartelle, del tutto irrilevanti per l'elenco di primo livello).
                var directories = Directory.GetDirectories(logPath);
                var newFolders = new List<string>(directories.Length);

                foreach (var dir in directories)
                {
                    var dirName = Path.GetFileName(dir);
                    if (string.IsNullOrEmpty(dirName)) continue;

                    if (MatchesTrain(dirName, SelectedTrain))
                    {
                        newFolders.Add(dirName);
                    }
                }

                // Se l'elenco è invariato e la selezione è già quella che verrebbe riapplicata,
                // il ciclo Clear/riempi/riseleziona terminerebbe esattamente nello stesso stato:
                // lo si salta per intero.
                if (FoldersUnchanged(newFolders) &&
                    ((newFolders.Count > 0 && SelectedFolder == newFolders[0]) ||
                     (newFolders.Count == 0 && SelectedFolder == null)))
                {
                    return;
                }

                AvailableFolders.Clear();
                foreach (var name in newFolders)
                {
                    AvailableFolders.Add(name);
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

        private bool FoldersUnchanged(List<string> candidate)
        {
            if (AvailableFolders.Count != candidate.Count) return false;
            for (int i = 0; i < candidate.Count; i++)
            {
                if (!string.Equals(AvailableFolders[i], candidate[i], StringComparison.Ordinal))
                    return false;
            }
            return true;
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

            IsLoading = true;
            LoadingMessage = "Scrittura report in corso...";
            try
            {
                _isWritingReport = true;
                var fieldsData = FormFields.Select(f => f.FieldValue).ToList();
                int targetRow = 1;

                await Task.Run(() => 
                {
                    // 1. Usa ClosedXML per trovare l'ultima riga reale in modo veloce e preciso, ignorando spazi vuoti o formattazione.
                    using var fs = new FileStream(_currentExcelFilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                    using var workbook = new XLWorkbook(fs);
                    var ws = workbook.Worksheets.FirstOrDefault();
                    if (ws != null)
                    {
                        int maxFilledRow = 1;
                        int scanLimit = ws.RangeUsed()?.LastRow()?.RowNumber() ?? 1;

                        // Ogni cella viene recuperata una sola volta: la versione precedente chiamava
                        // ws.Cell(r, c) due volte per colonna (IsEmpty + GetString). Inoltre le righe
                        // oltre scanLimit non vengono più toccate: sono per definizione fuori dal
                        // RangeUsed e quindi vuote, ma interrogarle costringeva ClosedXML a
                        // materializzare in memoria fino a 60 celle fantasma per ogni riga scansionata.
                        static bool HasValue(IXLWorksheet sheet, int row, int col)
                        {
                            var cell = sheet.Cell(row, col);
                            return !cell.IsEmpty() && !string.IsNullOrWhiteSpace(cell.GetString());
                        }

                        // Trova l'ultima riga compilata della tabella analizzando le colonne chiave (B: Data, C: Sito, D: Ticket, G: Loco)
                        for (int r = 2; r <= scanLimit; r++)
                        {
                            bool hasData = HasValue(ws, r, 2) || HasValue(ws, r, 3) ||
                                           HasValue(ws, r, 4) || HasValue(ws, r, 7);

                            if (hasData)
                            {
                                maxFilledRow = r;
                            }
                            else
                            {
                                // Controlla le successive 20 righe per assicurarsi che non ci siano righe vuote in mezzo
                                bool anyDataAhead = false;
                                for (int ahead = 1; ahead <= 20; ahead++)
                                {
                                    int checkRow = r + ahead;
                                    if (checkRow > scanLimit) break;

                                    if (HasValue(ws, checkRow, 2) || HasValue(ws, checkRow, 3) || HasValue(ws, checkRow, 4))
                                    {
                                        anyDataAhead = true;
                                        break;
                                    }
                                }

                                if (!anyDataAhead)
                                {
                                    break;
                                }
                            }
                        }
                        targetRow = maxFilledRow + 1;
                    }
                }); // Fine Task.Run ClosedXML - file completamente rilasciato

                // 2. Usa Excel Interop per scrivere i valori in modo nativo, per NON alterare in alcun modo la formattazione e la struttura del file
                Type? excelType = Type.GetTypeFromProgID("Excel.Application");
                if (excelType == null)
                {
                    IsLoading = false;
                    await Task.Delay(100);
                    MessageBox.Show("Excel non risulta installato. Impossibile salvare senza alterare il file.", "Errore", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                dynamic? excelApp = Activator.CreateInstance(excelType);
                if (excelApp == null)
                {
                    IsLoading = false;
                    await Task.Delay(100);
                    MessageBox.Show("Impossibile avviare Excel.", "Errore", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                await Task.Run(() =>
                {
                dynamic? workbookInterop = null;
                dynamic? worksheetInterop = null;

                try
                {
                    ExecuteComWithRetry(() => excelApp.Visible = false);
                    ExecuteComWithRetry(() => excelApp.DisplayAlerts = false);

                    ExecuteComWithRetry(() => { workbookInterop = excelApp.Workbooks.Open(_currentExcelFilePath); });
                    
                    ExecuteComWithRetry(() => { worksheetInterop = workbookInterop!.Worksheets[1]; }); // Interop è 1-based

                    // Scrivi i valori
                    for (int i = 0; i < fieldsData.Count; i++)
                    {
                        int col = i + 2; // FormFields parte dalla colonna B (indice 2)
                        string? val = fieldsData[i];

                        // Scrive solo se c'è un valore
                        if (!string.IsNullOrWhiteSpace(val))
                        {
                            if (DateTime.TryParseExact(val, "dd/MM/yyyy", System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out DateTime parsedDate))
                            {
                                ExecuteComWithRetry(() => worksheetInterop!.Cells[targetRow, col].Value = parsedDate);
                            }
                            else
                            {
                                ExecuteComWithRetry(() => worksheetInterop!.Cells[targetRow, col].Value = val);
                            }
                        }
                    }

                    ExecuteComWithRetry(() => workbookInterop!.Save());
                    ExecuteComWithRetry(() => workbookInterop!.Close());
                }
                finally
                {
                    // Quit() e i tre rilasci vanno protetti singolarmente: nella versione precedente
                    // erano istruzioni consecutive nel finally, quindi una qualunque eccezione su
                    // Quit() (o su un rilascio) saltava i rilasci successivi e lasciava un processo
                    // EXCEL.EXE orfano in memoria — su macchine datate bastavano pochi salvataggi
                    // falliti per saturare la RAM.
                    TryComCleanup(() => ExecuteComWithRetry(() => excelApp.Quit()));
                    TryComCleanup(() => { if (worksheetInterop != null) System.Runtime.InteropServices.Marshal.ReleaseComObject(worksheetInterop); });
                    TryComCleanup(() => { if (workbookInterop != null) System.Runtime.InteropServices.Marshal.ReleaseComObject(workbookInterop); });
                    TryComCleanup(() => { if (excelApp != null) System.Runtime.InteropServices.Marshal.ReleaseComObject(excelApp); });
                }
                }); // Fine Task.Run Interop
                    
                IsLoading = false;
                await Task.Delay(100);
                MessageBox.Show($"Report salvato con successo alla riga {targetRow}.", "Successo", MessageBoxButton.OK, MessageBoxImage.Information);
                    
                // (I campi non vengono puliti automaticamente qui per permettere il 'Riporta Report')
            }
            catch (IOException ioEx)
            {
                IsLoading = false;
                await Task.Delay(100);
                MessageBox.Show($"Impossibile salvare il report perché il file Excel è attualmente aperto. Chiudi Excel e riprova.\n\nDettaglio: {ioEx.Message}", "File Aperto", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            catch (Exception ex)
            {
                IsLoading = false;
                await Task.Delay(100);
                MessageBox.Show($"Errore durante il salvataggio nel file Excel:\n{ex.Message}\n{ex.StackTrace}", "Errore", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                _isWritingReport = false;
                IsLoading = false;
            }
        }

        private bool CanExecuteRiportaReport(object? parameter)
        {
            return !string.IsNullOrEmpty(_currentExcelFilePath) && File.Exists(_currentExcelFilePath) && (SelectedTrain == "ETR700" || SelectedTrain == "E404P" || SelectedTrain == "ETR1000 / 1000FH" || SelectedTrain == "ETR1000 I-F");
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

                IsLoading = true;
                LoadingMessage = "Ripristino report in corso...";

                // Trova il tecnico selezionato
                var techField = FormFields.FirstOrDefault(f => f.FieldName.Contains("TECNICO", StringComparison.OrdinalIgnoreCase) && (f.FieldName.Contains("ASTS", StringComparison.OrdinalIgnoreCase) || f.FieldName.Contains("STS", StringComparison.OrdinalIgnoreCase)));
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
                
                string userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                string hitachiDir = "";
                string trainPrefix = "";

                if (SelectedTrain == "ETR700")
                {
                    hitachiDir = Path.Combine(userProfile, "Hitachi Group", "SSB_SST - INTERVENTI ETR700 ELO BL3");
                    trainPrefix = "ETR700";
                }
                else if (SelectedTrain == "E404P")
                {
                    hitachiDir = Path.Combine(userProfile, "Hitachi Group", "SSB_SST - Interventi ETR500", "REPORT INTERVENTI NAPOLI - MILANO");
                    trainPrefix = "E404P";
                }
                else if (SelectedTrain == "ETR1000 / 1000FH")
                {
                    hitachiDir = Path.Combine(userProfile, "Hitachi Group", "SSB_SST - Interventi ETR1000");
                    trainPrefix = "ETR1000";
                }
                else if (SelectedTrain == "ETR1000 I-F")
                {
                    hitachiDir = Path.Combine(userProfile, "Hitachi Group", "SSB_SST - Interventi ETR1000", "ETR1000 ITA-FRA");
                    trainPrefix = "ETR1000 Italia_Francia";
                }
                else
                {
                    return;
                }

                string newFileName = $"Report Interventi {trainPrefix} {currentDateTime} {cleanTech}{Path.GetExtension(_currentExcelFilePath)}";

                if (!Directory.Exists(hitachiDir))
                {
                    IsLoading = false;
                    await Task.Delay(100);
                    MessageBox.Show("Cartella d'origine Hitachi non trovata:\n" + hitachiDir, "Errore", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                string destinationPath = Path.Combine(hitachiDir, newFileName);

                await Task.Run(() => 
                {
                    File.Move(_currentExcelFilePath, destinationPath, true);
                });

                IsLoading = false;
                await Task.Delay(100);
                MessageBox.Show($"Report riportato con successo nella cartella d'origine come:\n{newFileName}", "Successo", MessageBoxButton.OK, MessageBoxImage.Information);
                
                _currentExcelFilePath = null;
                FormFields.Clear();
            }
            catch (Exception ex)
            {
                IsLoading = false;
                await Task.Delay(100);
                MessageBox.Show($"Si è verificato un errore durante l'operazione:\n{ex.Message}", "Errore", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsLoading = false;
            }
        }

        private void ExecutePulisciCampi(object? parameter)
        {
            foreach (var field in FormFields)
            {
                field.FieldValue = string.Empty;
            }
        }

        /// <summary>
        /// Esegue un passo di pulizia COM assorbendo eventuali eccezioni, così che i passi
        /// successivi vengano comunque eseguiti.
        /// </summary>
        private static void TryComCleanup(Action action)
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Pulizia COM Excel fallita: {ex.Message}");
            }
        }

        private static void ExecuteComWithRetry(Action action, int maxRetries = 10)
        {
            int retries = 0;
            while (true)
            {
                try
                {
                    action();
                    break;
                }
                catch (Exception ex) when ((uint)ex.HResult == 0x8001010A && retries < maxRetries)
                {
                    retries++;
                    System.Threading.Thread.Sleep(1000);
                }
            }
        }

        private static bool MatchesTrain(string name, string? trainType)
        {
            if (string.IsNullOrEmpty(trainType)) return false;

            string fileName = Path.GetFileName(name);

            if (trainType == "E404P")
            {
                return fileName.Contains("ETR500", StringComparison.OrdinalIgnoreCase) || 
                       fileName.Contains("E404P", StringComparison.OrdinalIgnoreCase);
            }
            if (trainType == "ETR1000 / 1000FH")
            {
                return (fileName.Contains("ETR1000", StringComparison.OrdinalIgnoreCase) || 
                        fileName.Contains("1001", StringComparison.OrdinalIgnoreCase) || 
                        fileName.Contains("1000FH", StringComparison.OrdinalIgnoreCase)) &&
                       !fileName.Contains("Italia", StringComparison.OrdinalIgnoreCase) &&
                       !fileName.Contains("Francia", StringComparison.OrdinalIgnoreCase) &&
                       !fileName.Contains("ITA-FRA", StringComparison.OrdinalIgnoreCase) &&
                       !fileName.Contains("1000IF", StringComparison.OrdinalIgnoreCase) &&
                       !fileName.Contains("I-F", StringComparison.OrdinalIgnoreCase);
            }
            if (trainType == "ETR1000 I-F")
            {
                return fileName.Contains("1000IF", StringComparison.OrdinalIgnoreCase) || 
                       fileName.Contains("Italia", StringComparison.OrdinalIgnoreCase) || 
                       fileName.Contains("Francia", StringComparison.OrdinalIgnoreCase) || 
                       fileName.Contains("ITA-FRA", StringComparison.OrdinalIgnoreCase) || 
                       fileName.Contains("I-F", StringComparison.OrdinalIgnoreCase);
            }
            return fileName.Contains(trainType, StringComparison.OrdinalIgnoreCase);
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

        [GeneratedRegex(@"(?=CR|HR|A1)", RegexOptions.IgnoreCase)]
        private static partial Regex CrRegex();

        [GeneratedRegex(@"SR[-_\s]*([0-9]+)", RegexOptions.IgnoreCase)]
        private static partial Regex TicketSrRegex();

        [GeneratedRegex(@"\b\d{7,8}\b")]
        private static partial Regex StandaloneTicketRegex();

        [GeneratedRegex(@"\b\d{2,4}\b")]
        private static partial Regex StandaloneLocoRegex();

        [GeneratedRegex(@"^\d{2}\.\d{2}")]
        private static partial Regex SoftwareVersionRegex();
    }
}
