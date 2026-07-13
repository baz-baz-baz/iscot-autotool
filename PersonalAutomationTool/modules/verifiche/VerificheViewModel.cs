using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using ClosedXML.Excel;
using PersonalAutomationTool.Core;

namespace PersonalAutomationTool.Modules.Verifiche
{
    public class VerificheViewModel : ViewModelBase
    {
        public ObservableCollection<VerificheModel> VerificheList500 { get; } = [];
        public ObservableCollection<VerificheModel> VerificheList700 { get; } = [];
        public ObservableCollection<VerificheModel> VerificheList1000 { get; } = [];

        public VerificheViewModel()
        {
            LoadDataForFleet(@"Hitachi Group\SSB_SST - Interventi ETR500\Censimento ETR500\Verifiche ETR500", "500", VerificheList500);
            LoadDataForFleet(@"Hitachi Group\SSB_SST - INTERVENTI ETR700 ELO BL3", "700", VerificheList700);
            LoadDataForFleet(@"Hitachi Group\SSB_SST - Interventi ETR1000", "1000", VerificheList1000);
        }

        private static void LoadDataForFleet(string relativePath, string fleetIdentifier, ObservableCollection<VerificheModel> collection)
        {
            try
            {
                string userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                string folderPath = Path.Combine(userProfile, relativePath);
                string filePath = string.Empty;

                // Fallback nel caso in cui il path profondo non esista (cerca dalla cartella principale della flotta)
                if (!Directory.Exists(folderPath))
                {
                    string fallbackPath = Path.Combine(userProfile, @"Hitachi Group\SSB_SST - Interventi ETR" + fleetIdentifier);
                    if (fleetIdentifier == "700") fallbackPath = Path.Combine(userProfile, @"Hitachi Group\SSB_SST - INTERVENTI ETR700 ELO BL3");
                    if (Directory.Exists(fallbackPath)) folderPath = fallbackPath;
                }

                if (Directory.Exists(folderPath))
                {
                    // Cerca un file excel "Verifiche" nella directory principale, nelle sottocartelle e nelle sotto-sottocartelle (max 3 livelli) per evitare eccezioni
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
                                // Prendi il più recente se ce ne sono multipli
                                var recentFile = files.OrderByDescending(f => File.GetLastWriteTime(f)).First();
                                filePath = recentFile;
                                break;
                            }
                        }
                        catch { /* Ignora eccezioni di accesso */ }
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
