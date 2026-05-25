using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using ClosedXML.Excel;

class Program
{
    static void Main(string[] args)
    {
        string logPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "LOG & DUMP");
        var files = Directory.GetFiles(logPath, "Report Interventi*.xls*", SearchOption.AllDirectories);
        if (files.Length == 0) return;
        string file = files[0];
        
        try {
            using (var workbook = new XLWorkbook(file)) {
                var worksheet = workbook.Worksheets.FirstOrDefault();
                if (worksheet == null) return;
                int col = 27; // AA
                
                var columnValidations = worksheet.DataValidations
                            .Where(dv => dv.Ranges.Any(r => r.RangeAddress.FirstAddress.ColumnNumber <= col && r.RangeAddress.LastAddress.ColumnNumber >= col) && dv.AllowedValues == XLAllowedValues.List)
                            .ToList();
                            
                Console.WriteLine($"Trovate {columnValidations.Count} regole di validation per la colonna {col}.");

                if (columnValidations.Any())
                {
                    var allOptions = new HashSet<string>();

                    foreach (var validation in columnValidations)
                    {
                        string listValue = validation.Value;
                        if (string.IsNullOrEmpty(listValue)) continue;
                        
                        Console.WriteLine($"Analizzo regola: {listValue}");

                        string formula = listValue.StartsWith("=") ? listValue.Substring(1) : listValue;
                        IXLRange? range = null;

                        try
                        {
                            var namedRange = workbook.DefinedNames.FirstOrDefault(n => n.Name.Equals(formula, StringComparison.OrdinalIgnoreCase));
                            var wsNamedRange = worksheet.DefinedNames.FirstOrDefault(n => n.Name.Equals(formula, StringComparison.OrdinalIgnoreCase));

                            if (namedRange != null) range = namedRange.Ranges.FirstOrDefault();
                            else if (wsNamedRange != null) range = wsNamedRange.Ranges.FirstOrDefault();
                            else if (formula.Contains("!"))
                            {
                                var parts = formula.Split('!');
                                string sheetName = parts[0].Trim('\'');
                                string address = parts[1];
                                if (workbook.TryGetWorksheet(sheetName, out var targetSheet)) range = targetSheet.Range(address);
                            }
                            else if (formula.Contains(":"))
                            {
                                range = worksheet.Range(formula);
                            }
                        }
                        catch { range = null; }

                        if (range != null)
                        {
                            foreach (var c in range.CellsUsed())
                            {
                                string val = c.GetString();
                                if (!string.IsNullOrWhiteSpace(val)) allOptions.Add(val.Trim());
                            }
                        }
                        else
                        {
                            var opts = listValue.Trim('"').Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries).Select(s => s.Trim());
                            foreach (var o in opts) allOptions.Add(o);
                        }
                    }

                    var OptionsList = allOptions.ToList();
                    Console.WriteLine("Opzioni combinate:");
                    foreach(var opt in OptionsList) {
                        Console.WriteLine("- " + opt);
                    }
                }
            }
        } catch (Exception ex) { Console.WriteLine(ex.Message); }
    }
}
