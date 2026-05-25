using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using ClosedXML.Excel;
using PersonalAutomationTool.Core;

namespace PersonalAutomationTool.Modules.Excel
{
    public class ExcelViewModel : ViewModelBase, IDisposable
    {
        public List<string> Trains { get; } = new List<string>
        {
            "E404P",
            "ETR700",
            "ETR1000",
            "ETR1000 I-F"
        };

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

        public ObservableCollection<string> AvailableFolders { get; } = new ObservableCollection<string>();

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

        public ObservableCollection<ExcelFieldViewModel> FormFields { get; } = new ObservableCollection<ExcelFieldViewModel>();

        public ICommand SpostaReportCommand { get; }

        public ExcelViewModel()
        {
            // Set default selection
            SelectedTrain = Trains.FirstOrDefault();

            SpostaReportCommand = new RelayCommand(ExecuteSpostaReport, CanExecuteSpostaReport);

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
                        var existingFiles = Directory.GetFiles(searchDir, "Report Interventi*.xls*");
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
                FormFields.Clear();

                using (var workbook = new XLWorkbook(filePath))
                {
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

                        if (columnValidations.Any())
                        {
                            fieldViewModel.IsComboBox = true;
                            var allOptions = new HashSet<string>();

                            foreach (var validation in columnValidations)
                            {
                                string listValue = validation.Value;
                                if (string.IsNullOrEmpty(listValue)) continue;

                                string formula = listValue.StartsWith("=") ? listValue.Substring(1) : listValue;
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
                                    else if (formula.Contains("!"))
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
                                    else if (formula.Contains(":"))
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
                                    var opts = listValue.Trim('"').Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries).Select(s => s.Trim());
                                    foreach (var o in opts)
                                    {
                                        allOptions.Add(o);
                                    }
                                }
                            }

                            fieldViewModel.Options = allOptions.ToList();
                            if (fieldName.Equals("VERSIONE SW PRESENTE", StringComparison.OrdinalIgnoreCase))
                            {
                                File.WriteAllText(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "debug_options.txt"), "Opzioni trovate: " + allOptions.Count + "\n" + string.Join("\n", allOptions));
                            }
                        }

                        FormFields.Add(fieldViewModel);
                    }
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

                // Cerca prima nella cartella selezionata
                string searchDir = string.IsNullOrEmpty(SelectedFolder) 
                    ? AppConfig.LogAndDumpFolder 
                    : Path.Combine(AppConfig.LogAndDumpFolder, SelectedFolder);

                if (Directory.Exists(searchDir))
                {
                    var existingFiles = Directory.GetFiles(searchDir, "Report Interventi*.xls*");
                    if (existingFiles.Length > 0)
                    {
                        LoadExcelFields(existingFiles[0]);
                        return;
                    }
                }

                // Se non lo trova nella cartella selezionata, cerca nella root di LOG & DUMP
                if (Directory.Exists(AppConfig.LogAndDumpFolder))
                {
                    var rootFiles = Directory.GetFiles(AppConfig.LogAndDumpFolder, "Report Interventi*.xls*");
                    if (rootFiles.Length > 0)
                    {
                        LoadExcelFields(rootFiles[0]);
                    }
                }
            }
            catch
            {
                // Ignora silenziosamente errori nel caricamento automatico
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

        public void Dispose()
        {
            AppWatcher.OnLogDumpFolderChanged -= AppWatcher_OnLogDumpFolderChanged;
        }
    }
}
