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
    public static class EmailService
    {
        public static void GenerateChiusuraTicketEmail(string cartella, string trainType, ObservableCollection<LocoGroupModel> locoGroups)
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
                
                string subject = $"CHIUSURA TICKET {cartella}"; // fallback

                if (Directory.Exists(folderPath))
                {
                    List<string> tickets = new List<string>();
                    List<string> locos = new List<string>();
                    string extractedDate = "";
                    string extractedUser = "";

                    var dirs = Directory.GetDirectories(folderPath);
                    foreach (var dir in dirs)
                    {
                        string dirName = new DirectoryInfo(dir).Name;
                        var parts = dirName.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                        if (parts.Length >= 7 && parts[0].StartsWith("SR", StringComparison.OrdinalIgnoreCase))
                        {
                            tickets.Add(parts[0].ToUpper());
                            locos.Add(parts[3]);
                            extractedDate = parts[5];
                            extractedUser = string.Join(" ", parts.Skip(6));
                        }
                    }

                    if (tickets.Count > 0)
                    {
                        tickets = tickets.Distinct().ToList();
                        locos = locos.Distinct().ToList();

                        string ticketsStr = string.Join(" - ", tickets);
                        string locosStr = string.Join(" - ", locos);

                        subject = $"CHIUSURA TICKET {ticketsStr} {trainType} {locosStr} IMC AV Milano {extractedDate} {extractedUser}".Trim();
                    }
                    else
                    {
                        var pdfFiles = Directory.GetFiles(folderPath, "*.pdf");
                        if (pdfFiles.Length > 0)
                        {
                            string pdfName = Path.GetFileNameWithoutExtension(pdfFiles[0]);
                            string[] prefixesToRemove = new[] { "FL ", "NC ", "NdL " };
                            foreach (var prefix in prefixesToRemove)
                            {
                                if (pdfName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                                {
                                    pdfName = pdfName.Substring(prefix.Length).Trim();
                                    break;
                                }
                            }
                            subject = $"CHIUSURA TICKET {pdfName}";
                        }
                    }
                }

                subject = System.Text.RegularExpressions.Regex.Replace(subject, @"\s+", " ");
                mailItem.Subject = subject;

                // Ottieni Destinatari
                var destinatariConfig = DestinatariManager.LoadConfig();
                var trainConfig = destinatariConfig.FirstOrDefault(t => t.TrainName.Equals(trainType, StringComparison.OrdinalIgnoreCase));
                if (trainConfig != null)
                {
                    var actionConfig = trainConfig.Actions.FirstOrDefault(a => a.ActionName.Equals("Chiusura Ticket", StringComparison.OrdinalIgnoreCase));
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

                // Genera Corpo HTML
                StringBuilder htmlBuilder = new StringBuilder();
                htmlBuilder.Append("<div style='font-family: Calibri, sans-serif; font-size: 11pt; color: black;'>");
                htmlBuilder.Append($"<p style='font-size: 14pt;'>{saluto}</p>");
                htmlBuilder.Append("<p style='font-size: 14pt;'>con la presente vi invio la chiusura del ticket in oggetto.</p>");
                htmlBuilder.Append("<p style='font-size: 14pt;'>Di seguito la descrizione delle avarie segnalate dal PdC e dell'intervento effettuato:</p>");

                // Le tabelle verranno generate singolarmente per ogni avviso all'interno del ciclo

                foreach (var group in locoGroups)
                {
                    foreach (var input in group.Inputs)
                    {
                        string treno = string.Empty;
                        string loco = group.GroupLocoName ?? string.Empty;
                        string avaria = input.Avaria ?? string.Empty;
                        string intervento = input.Intervento ?? string.Empty;
                        string versioneSW = string.Empty;

                        // Ottieni treno e versione SW dal database
                        try
                        {
                            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
                            DirectoryInfo? dir = new(baseDir);
                            while (dir != null && dir.Name != "PersonalAutomationTool")
                            {
                                dir = dir.Parent;
                            }
                            if (dir != null)
                            {
                                string dbPath = Path.Combine(dir.FullName, "modules", "database", "train_software.db");
                                if (File.Exists(dbPath))
                                {
                                    using var dbManager = new PersonalAutomationTool.Modules.Database.DatabaseManager(dbPath);
                                    string locoSafe = loco.Replace("'", "''");
                                    string trainTypeSafe = trainType.Replace("'", "''");
                                    var dt = dbManager.ExecuteQuery($"SELECT treno, software FROM flotte WHERE tipo = '{trainTypeSafe}' AND loco = '{locoSafe}'");
                                    if (dt.Rows.Count > 0)
                                    {
                                        treno = dt.Rows[0]["treno"]?.ToString() ?? string.Empty;
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
                        htmlBuilder.Append($"<td style='border: 1px solid #ddd; padding: 10px; font-size: 14pt;'>{WebUtility.HtmlEncode(avaria).Replace("\n", "<br>").Replace("\r", "")}</td>");
                        htmlBuilder.Append($"<td style='border: 1px solid #ddd; padding: 10px; font-size: 14pt;'>{WebUtility.HtmlEncode(intervento).Replace("\n", "<br>").Replace("\r", "")}</td>");
                        htmlBuilder.Append($"<td style='border: 1px solid #ddd; padding: 10px; font-weight: bold;'>{WebUtility.HtmlEncode(versioneSW)}</td>");
                        htmlBuilder.Append("</tr>");

                        htmlBuilder.Append("</tbody>");
                        htmlBuilder.Append("</table>");
                        htmlBuilder.Append("<br>"); // Aggiunge ulteriore spazio netto in Outlook
                    }
                }

                htmlBuilder.Append("<p style='margin-top: 20px; font-size: 14pt;'>Cordiali saluti,</p>");
                htmlBuilder.Append("<br><br></div>");

                string bodyContent = htmlBuilder.ToString();

                if (!string.IsNullOrEmpty(signatureHtml))
                {
                    int bodyIdx = signatureHtml.IndexOf("<body", StringComparison.OrdinalIgnoreCase);
                    if (bodyIdx != -1)
                    {
                        int closingIdx = signatureHtml.IndexOf(">", bodyIdx);
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
                
                if (Directory.Exists(folderPath))
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
