using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Windows;
using PersonalAutomationTool.Modules.DestinatariMail;
using PersonalAutomationTool.Modules.Email.Dialogs;

namespace PersonalAutomationTool.Modules.Email
{
    public static partial class EmailService
    {
        [System.Text.RegularExpressions.GeneratedRegex(@"\s+")]
        private static partial System.Text.RegularExpressions.Regex MultipleSpacesRegex();

        [System.Text.RegularExpressions.GeneratedRegex(@"\b\d{6}\b")]
        private static partial System.Text.RegularExpressions.Regex SixDigitsRegex();

        [System.Text.RegularExpressions.GeneratedRegex(@"(?<!\d)\d{7,8}(?!\d)")]
        private static partial System.Text.RegularExpressions.Regex TicketRegex();

        public static void GenerateChiusuraTicketEmail(string cartella, string trainType, ObservableCollection<LocoGroupModel> locoGroups, bool isNdPrefix = false, string actionType = "Chiusura Ticket")
        {
            try
            {
                Type? outlookType = Type.GetTypeFromProgID("Outlook.Application");
                if (outlookType == null)
                {
                    MessageBox.Show("Impossibile trovare Microsoft Outlook nel sistema. Assicurati che sia installato.", "Errore Outlook", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                dynamic outlookApp = Activator.CreateInstance(outlookType)!;
                dynamic mailItem = outlookApp.CreateItem(0); // 0 = olMailItem

                // Inizializza l'Inspector per forzare Outlook a generare la firma predefinita in HTMLBody
                var inspector = mailItem.GetInspector;
                string signatureHtml = mailItem.HTMLBody ?? string.Empty;

                string baseLogDump = PersonalAutomationTool.Core.AppConfig.LogAndDumpFolder;
                string folderPath = Path.Combine(baseLogDump, cartella);
                
                // Salva il file json anche quando si usa la generazione veloce senza dialog (es. scadenze)
                if (Directory.Exists(folderPath))
                {
                    try
                    {
                        string cacheFile = Path.Combine(folderPath, "info_ticket.json");
                        string json = System.Text.Json.JsonSerializer.Serialize(locoGroups);
                        File.WriteAllText(cacheFile, json);
                    }
                    catch { }
                }
                
                string subject = $"CHIUSURA TICKET {cartella}"; // fallback
                List<string> logFolders = [];
                List<string> dumpFolders = [];

                if (Directory.Exists(folderPath))
                {
                    List<string> tickets = [];
                    List<string> locos = [];
                    string extractedDate = "";
                    string extractedUser = "";

                    var dirs = Directory.GetDirectories(folderPath);
                    foreach (var dir in dirs)
                    {
                        string dirName = new DirectoryInfo(dir).Name;
                        if (dirName.Contains(" LOG ")) logFolders.Add(dirName);
                        if (dirName.Contains(" DUMP ")) dumpFolders.Add(dirName);

                        var parts = dirName.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                        if (parts.Length >= 4 && parts[0].StartsWith("SR", StringComparison.OrdinalIgnoreCase))
                        {
                            tickets.Add(parts[0].ToUpper());
                            
                            var dateMatch = SixDigitsRegex().Match(dirName);
                            if (dateMatch.Success)
                            {
                                extractedDate = dateMatch.Value;
                                int dateIndex = Array.IndexOf(parts, extractedDate);
                                if (dateIndex > 0)
                                {
                                    extractedUser = string.Join(" ", parts.Skip(dateIndex + 1));
                                    
                                    int locoStartIndex = 3;
                                    if (parts.Length > 3 && (parts[3].Equals("I-F", StringComparison.OrdinalIgnoreCase) || parts[3].Equals("FH", StringComparison.OrdinalIgnoreCase) || parts[3].Equals("I-F", StringComparison.OrdinalIgnoreCase)))
                                    {
                                        locoStartIndex = 4;
                                    }
                                    
                                    if (dateIndex > locoStartIndex)
                                    {
                                        string locoString = string.Join(" ", parts.Skip(locoStartIndex).Take(dateIndex - locoStartIndex));
                                        var splittedLocos = locoString.Split('-', StringSplitOptions.RemoveEmptyEntries);
                                        foreach(var s in splittedLocos) locos.Add(s.Trim());
                                    }
                                }
                            }
                        }
                    }

                    if (tickets.Count > 0)
                    {
                        tickets = [.. tickets.Distinct()];
                        locos = [.. locos.Distinct()];

                        string ticketsStr = string.Join(" - ", tickets);
                        string locosStr = string.Join(" - ", locos);

                        if (actionType == "Log Dump" && trainType == "E404P")
                        {
                            subject = $"{ticketsStr} LOG E DUMP in rete {trainType} {locosStr} IMC AV Milano {extractedDate} {extractedUser}".Trim();
                        }
                        else
                        {
                            subject = $"CHIUSURA TICKET {ticketsStr} {trainType} {locosStr} IMC AV Milano {extractedDate} {extractedUser}".Trim();
                        }
                    }
                    else
                    {
                        var pdfFiles = Directory.GetFiles(folderPath, "*.pdf");
                        if (pdfFiles.Length > 0)
                        {
                            string pdfName = Path.GetFileNameWithoutExtension(pdfFiles[0]);
                            string[] prefixesToRemove = ["FL ", "NC ", "NdL "];
                            foreach (var prefix in prefixesToRemove)
                            {
                                if (pdfName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                                {
                                    pdfName = pdfName[prefix.Length..].Trim();
                                    break;
                                }
                            }
                            subject = $"CHIUSURA TICKET {pdfName}";
                        }
                    }
                }

                if (actionType == "Scadenza 6 Mesi" || actionType == "Scadenza 12 Mesi")
                {
                    var cParts = cartella.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                    string tNum = "";
                    string dateStr = "";
                    var dateMatch = SixDigitsRegex().Match(cartella);
                    if (dateMatch.Success) 
                    {
                        dateStr = dateMatch.Value;
                        int dateIndex = Array.IndexOf(cParts, dateStr);
                        int locoStartIndex = 1;
                        if (cParts.Length > 1 && (cParts[1].Equals("I-F", StringComparison.OrdinalIgnoreCase) || cParts[1].Equals("FH", StringComparison.OrdinalIgnoreCase)))
                            locoStartIndex = 2;
                        if (dateIndex > locoStartIndex)
                            tNum = string.Join(" ", cParts.Skip(locoStartIndex).Take(dateIndex - locoStartIndex));
                    }
                    else if (logFolders.Count > 0)
                    {
                        dateMatch = SixDigitsRegex().Match(logFolders[0]);
                        if (dateMatch.Success) dateStr = dateMatch.Value;
                        
                        int locoStartIndex = 1;
                        if (cParts.Length > 1 && (cParts[1].Equals("I-F", StringComparison.OrdinalIgnoreCase) || cParts[1].Equals("FH", StringComparison.OrdinalIgnoreCase)))
                            locoStartIndex = 2;
                        if (cParts.Length > locoStartIndex)
                            tNum = cParts[locoStartIndex];
                    }
                    {
                        dateMatch = SixDigitsRegex().Match(logFolders[0]);
                        if (dateMatch.Success) dateStr = dateMatch.Value;
                    }
                    
                    string periodo = actionType == "Scadenza 6 Mesi" ? "semestrale" : "annuale";
                    subject = $"Revisione {periodo} {trainType} - {tNum} {dateStr}".Trim();
                }
                else if (actionType == "Scadenze Francesi")
                {
                    string scadenzaName = "I0";
                    if (Directory.Exists(folderPath))
                    {
                        var txtFiles = Directory.GetFiles(folderPath, "*.txt");
                        if (txtFiles.Length > 0)
                        {
                            scadenzaName = Path.GetFileNameWithoutExtension(txtFiles[0]);
                        }
                    }

                    var cParts = cartella.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                    string tNum = "";
                    string dateStr = "";
                    string myExtractedUser = "";
                    var dateMatch = SixDigitsRegex().Match(cartella);
                    if (dateMatch.Success) 
                    {
                        dateStr = dateMatch.Value;
                        int dateIndex = Array.IndexOf(cParts, dateStr);
                        
                        int locoStartIndex = 1;
                        if (cParts.Length > 1 && (cParts[1].Equals("I-F", StringComparison.OrdinalIgnoreCase) || cParts[1].Equals("FH", StringComparison.OrdinalIgnoreCase)))
                            locoStartIndex = 2;
                        
                        if (dateIndex > locoStartIndex)
                            tNum = string.Join(" ", cParts.Skip(locoStartIndex).Take(dateIndex - locoStartIndex));
                            
                        if (dateIndex >= 0 && dateIndex < cParts.Length - 1)
                        {
                            myExtractedUser = string.Join(" ", cParts.Skip(dateIndex + 1));
                        }
                    }
                    
                    if (string.IsNullOrEmpty(myExtractedUser) && logFolders.Count > 0)
                    {
                        var logParts = logFolders[0].Split(' ', StringSplitOptions.RemoveEmptyEntries);
                        var lmDateMatch = SixDigitsRegex().Match(logFolders[0]);
                        if (lmDateMatch.Success)
                        {
                            int lmDateIndex = Array.IndexOf(logParts, lmDateMatch.Value);
                            if (lmDateIndex >= 0 && lmDateIndex < logParts.Length - 1)
                            {
                                myExtractedUser = string.Join(" ", logParts.Skip(lmDateIndex + 1));
                            }
                        }
                    }
                    
                    if (!dateMatch.Success && logFolders.Count > 0)
                    {
                        dateMatch = SixDigitsRegex().Match(logFolders[0]);
                        if (dateMatch.Success) dateStr = dateMatch.Value;
                        
                        int locoStartIndex = 1;
                        if (cParts.Length > 1 && (cParts[1].Equals("I-F", StringComparison.OrdinalIgnoreCase) || cParts[1].Equals("FH", StringComparison.OrdinalIgnoreCase)))
                            locoStartIndex = 2;
                        if (cParts.Length > locoStartIndex)
                            tNum = cParts[locoStartIndex];
                    }
                    {
                        dateMatch = SixDigitsRegex().Match(logFolders[0]);
                        if (dateMatch.Success) dateStr = dateMatch.Value;
                    }

                    string displayTrainType = trainType == "ETR1000IF" ? "ETR1000 I-F" : trainType;
                    subject = $"Revisione {scadenzaName} {displayTrainType} {tNum} {dateStr} {myExtractedUser}".Trim();
                }

                subject = MultipleSpacesRegex().Replace(subject, " ");
                if (isNdPrefix)
                {
                    subject = "ND " + subject;
                }
                mailItem.Subject = subject;

                // Ottieni Destinatari
                var destinatariConfig = DestinatariManager.LoadConfig();
                var trainConfig = destinatariConfig.FirstOrDefault(t => t.TrainName.Equals(trainType, StringComparison.OrdinalIgnoreCase));
                if (trainConfig != null)
                {
                    var actionConfig = trainConfig.Actions.FirstOrDefault(a => a.ActionName.Equals(actionType, StringComparison.OrdinalIgnoreCase));
                    if (actionConfig != null)
                    {
                        mailItem.To = actionConfig.ToRecipients;
                        mailItem.CC = actionConfig.CcRecipients;
                    }
                }

                int currentHour = DateTime.Now.Hour;
                string saluto = "Buongiorno,";
                if (currentHour >= 4 && currentHour < 14)
                {
                    saluto = "Buongiorno,";
                }
                else if (currentHour >= 14 && currentHour < 18)
                {
                    saluto = "Buon pomeriggio,";
                }
                else
                {
                    saluto = "Buonanotte,";
                }

                List<string> bottomTickets = [];
                if (Directory.Exists(folderPath))
                {
                    try
                    {
                        var allEntries = Directory.GetFileSystemEntries(folderPath);
                        foreach (var entry in allEntries)
                        {
                            string name = Path.GetFileNameWithoutExtension(entry);
                            var match = TicketRegex().Match(name);
                            if (match.Success)
                            {
                                bottomTickets.Add(match.Value);
                            }
                        }
                        bottomTickets = [.. bottomTickets.Distinct()];
                    }
                    catch { }
                }

                // Genera Corpo HTML
                StringBuilder htmlBuilder = new();
                htmlBuilder.Append("<div style='font-family: Calibri, sans-serif; font-size: 11pt; color: black;'>");
                htmlBuilder.Append($"<p style='font-size: 14pt;'>{saluto}</p>");
                
                var allFolders = logFolders.Concat(dumpFolders).ToList();
                allFolders.Sort((a, b) =>
                {
                    static string GetSuffix(string f)
                    {
                        string[] tokens = [" NVRAM DUMP ", " DUMP ", " LOG "];
                        foreach (var t in tokens)
                        {
                            int idx = f.IndexOf(t, StringComparison.OrdinalIgnoreCase);
                            if (idx != -1) return f[(idx + t.Length)..].Trim();
                        }
                        return f;
                    }

                    int cmp = string.Compare(GetSuffix(a), GetSuffix(b), StringComparison.OrdinalIgnoreCase);
                    if (cmp != 0) return cmp;
                    
                    bool isLogA = a.Contains(" LOG ");
                    bool isLogB = b.Contains(" LOG ");
                    if (isLogA && !isLogB) return -1;
                    if (!isLogA && isLogB) return 1;
                    
                    return string.Compare(a, b, StringComparison.OrdinalIgnoreCase);
                });

                if (actionType == "Log Dump" && trainType == "E404P")
                {
                    htmlBuilder.Append("<p style='font-size: 14pt;'>Con la presente comunica l'inserimento in rete, in data odierna, dei seguenti Files:</p>");
                    htmlBuilder.Append("<ul>");
                    foreach(var f in allFolders) {
                        htmlBuilder.Append($"<li style='font-size: 14pt;'><b>{WebUtility.HtmlEncode(f)}</b></li>");
                    }
                    htmlBuilder.Append("</ul><br>");
                }
                else
                {
                    htmlBuilder.Append("<p style='font-size: 14pt;'>con la presente vi invio la chiusura del ticket in oggetto.</p>");
                    
                    string[] targetTrains = ["ETR700", "ETR1000", "ETR1000I-F", "ETR1000FH"];
                    if (targetTrains.Contains(trainType, StringComparer.OrdinalIgnoreCase))
                    {
                        htmlBuilder.Append("<p style='font-size: 14pt;'>Confermo l'inserimento in rete dei seguenti files:</p>");
                        if (allFolders.Count > 0)
                        {
                            string foldersHtml = string.Join("<br>", allFolders.Select(f => $"<b>{WebUtility.HtmlEncode(f)}</b>"));
                            htmlBuilder.Append($"<p style='font-size: 14pt;'>{foldersHtml}</p>");
                        }
                    }

                    htmlBuilder.Append("<p style='font-size: 14pt;'>Di seguito la descrizione delle avarie segnalate dal PdC e dell'intervento effettuato:</p>");
                }

                // Le tabelle verranno generate singolarmente per ogni avviso all'interno del ciclo

                foreach (var group in locoGroups)
                {
                    foreach (var input in group.Inputs)
                    {
                        string treno = string.Empty;
                        string loco = group.GroupLocoName ?? string.Empty;
                        string avviso = input.Avviso ?? string.Empty;
                        string dataOra = input.DataOra ?? string.Empty;
                        string avaria = input.Avaria ?? string.Empty;
                        
                        string combinedAvaria = avaria;
                        if (actionType.Equals("Chiusura Ticket", StringComparison.OrdinalIgnoreCase))
                        {
                            combinedAvaria = $"Avviso={avviso} Data={dataOra}\n\n{avaria}".Trim();
                        }
                        else if (actionType.Equals("Log Dump", StringComparison.OrdinalIgnoreCase))
                        {
                            if (!string.IsNullOrWhiteSpace(avviso) || !string.IsNullOrWhiteSpace(dataOra))
                            {
                                combinedAvaria = $"Avviso={avviso} Data={dataOra}\n\n{avaria}".Trim();
                            }
                        }
                        
                        string intervento = input.Intervento ?? string.Empty;
                        string versioneSW = string.Empty;

                        // Ottieni treno e versione SW dal database
                        try
                        {
                            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
                            string dbPath = Path.Combine(baseDir, "modules", "database", "train_software.db");
                            if (File.Exists(dbPath))
                            {
                                using var dbManager = new PersonalAutomationTool.Modules.Database.DatabaseManager(dbPath);
                                string dbTrainType = trainType;
                                if (dbTrainType.Equals("ETR1000IF", StringComparison.OrdinalIgnoreCase))
                                {
                                    dbTrainType = "ETR1000 I-F";
                                }
                                else if (dbTrainType.Equals("ETR1000FH", StringComparison.OrdinalIgnoreCase))
                                {
                                    dbTrainType = "ETR1001FH";
                                }
                                
                                string locoSafe = loco.Replace("'", "''");
                                string trainTypeSafe = dbTrainType.Replace("'", "''");
                                var dt = dbManager.ExecuteQuery($"SELECT treno, software FROM flotte WHERE tipo = '{trainTypeSafe}' AND loco = '{locoSafe}'");
                                
                                if (dt.Rows.Count == 0 && locoSafe.Contains(' '))
                                {
                                    var parts = locoSafe.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                                    string firstPart = parts[0];
                                    dt = dbManager.ExecuteQuery($"SELECT treno, software FROM flotte WHERE tipo = '{trainTypeSafe}' AND loco = '{firstPart}'");
                                    if (dt.Rows.Count > 0)
                                    {
                                        loco = firstPart;
                                        if (parts.Length > 1)
                                        {
                                            versioneSW = parts[1];
                                        }
                                    }
                                }

                                if (dt.Rows.Count > 0)
                                {
                                    treno = dt.Rows[0]["treno"]?.ToString() ?? string.Empty;
                                    if (string.IsNullOrEmpty(versioneSW))
                                    {
                                        versioneSW = dt.Rows[0]["software"]?.ToString() ?? string.Empty;
                                    }
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"Errore db: {ex.Message}");
                        }

                        htmlBuilder.Append("<table style='width: 100%; border-collapse: collapse; border: 1px solid #ddd; margin-top: 20px; margin-bottom: 20px;'>");
                        htmlBuilder.Append("<thead>");
                        htmlBuilder.Append("<tr style='background-color: #A6A6A6; color: black; font-weight: bold; text-align: center;'>");
                        htmlBuilder.Append("<th style='border: 1px solid white; padding: 10px;'><i>TRENO</i></th>");
                        htmlBuilder.Append("<th style='border: 1px solid white; padding: 10px;'><i>LOCOMOTORE</i></th>");
                        htmlBuilder.Append("<th style='border: 1px solid white; padding: 10px;'><i>AVARIA SEGNALATA</i></th>");
                        htmlBuilder.Append("<th style='border: 1px solid white; padding: 10px;'><i>DESCRIZIONE INTERVENTO</i></th>");
                        htmlBuilder.Append("<th style='border: 1px solid white; padding: 10px;'><i>VERSIONE SW</i></th>");
                        htmlBuilder.Append("</tr>");
                        htmlBuilder.Append("</thead>");
                        htmlBuilder.Append("<tbody>");

                        htmlBuilder.Append("<tr style='text-align: center; vertical-align: middle; border: 1px solid #ddd;'>");
                        htmlBuilder.Append($"<td style='border: 1px solid #ddd; padding: 10px; font-weight: bold;'>{WebUtility.HtmlEncode(treno)}</td>");
                        htmlBuilder.Append($"<td style='border: 1px solid #ddd; padding: 10px; font-weight: bold;'>{WebUtility.HtmlEncode(loco)}</td>");
                        htmlBuilder.Append($"<td style='border: 1px solid #ddd; padding: 10px; font-size: 14pt;'>{WebUtility.HtmlEncode(combinedAvaria).Replace("\n", "<br>").Replace("\r", "")}</td>");
                        htmlBuilder.Append($"<td style='border: 1px solid #ddd; padding: 10px; font-size: 14pt;'>{WebUtility.HtmlEncode(intervento).Replace("\n", "<br>").Replace("\r", "")}</td>");
                        htmlBuilder.Append($"<td style='border: 1px solid #ddd; padding: 10px; font-weight: bold;'>{WebUtility.HtmlEncode(versioneSW)}</td>");
                        htmlBuilder.Append("</tr>");

                        htmlBuilder.Append("</tbody>");
                        htmlBuilder.Append("</table>");
                        htmlBuilder.Append("<br>"); // Aggiunge ulteriore spazio netto in Outlook
                    }
                }

                htmlBuilder.Append("<p style='margin-top: 20px; font-size: 14pt; margin-bottom: 5px;'>Cordiali saluti,</p>");
                
                if (bottomTickets.Count > 0)
                {
                    string joinedTickets = string.Join(", ", bottomTickets);
                    htmlBuilder.Append($"<p style='font-family: \"Calibri Light\", Calibri, sans-serif; font-size: 14pt; color: #A6A6A6; font-weight: 300; margin-top: 0px;'>Ticket: {joinedTickets}</p>");
                }

                htmlBuilder.Append("</div>");

                string bodyContent = htmlBuilder.ToString();

                if (!string.IsNullOrEmpty(signatureHtml))
                {
                    int bodyIdx = signatureHtml.IndexOf("<body", StringComparison.OrdinalIgnoreCase);
                    if (bodyIdx != -1)
                    {
                        int closingIdx = signatureHtml.IndexOf('>', bodyIdx);
                        if (closingIdx != -1)
                        {
                            signatureHtml = signatureHtml.Insert(closingIdx + 1, bodyContent);
                        }
                        else
                        {
                            signatureHtml = bodyContent + signatureHtml;
                        }
                    }
                    else
                    {
                        signatureHtml = bodyContent + signatureHtml;
                    }
                    mailItem.HTMLBody = signatureHtml;
                }
                else
                {
                    mailItem.HTMLBody = "<html><body>" + bodyContent + "</body></html>";
                }

                // Cerca allegato
                // Utilizza le variabili baseLogDump e folderPath già definite in precedenza
                
                if (actionType != "Log Dump" && Directory.Exists(folderPath))
                {
                    var pdfFiles = Directory.GetFiles(folderPath, "*.pdf");
                    foreach (var pdf in pdfFiles)
                    {
                        mailItem.Attachments.Add(pdf);
                    }
                }

                mailItem.Display(false); // Mostra l'email in modo asincrono, non bloccare l'interfaccia utente in attesa di chiusura.
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Si è verificato un errore durante la generazione dell'email:\n{ex.Message}", "Errore", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
