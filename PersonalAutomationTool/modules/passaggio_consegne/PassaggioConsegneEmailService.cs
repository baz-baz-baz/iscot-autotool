using System;
using System.IO;
using System.Windows;
using PersonalAutomationTool.Modules.DestinatariMail;

namespace PersonalAutomationTool.Modules.PassaggioConsegne
{
    public static class PassaggioConsegneEmailService
    {
        public static void OpenDraftEmail(RapportinoTurnoModel rapportino, string pdfPath)
        {
            // Riferimenti COM dichiarati fuori dal try: il finally deve poterli rilasciare anche
            // quando la generazione fallisce a metà, altrimenti OUTLOOK.EXE resta agganciato.
            dynamic? outlookApp = null;
            dynamic? mailItem = null;
            dynamic? inspector = null;

            try
            {
                Type? outlookType = Type.GetTypeFromProgID("Outlook.Application");
                if (outlookType == null)
                {
                    MessageBox.Show($"Il PDF del rapportino è stato generato in:\n{pdfPath}\n\nMicrosoft Outlook non è stato rilevato per l'apertura della bozza email.", "PDF Generato", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                outlookApp = Activator.CreateInstance(outlookType)!;
                mailItem = outlookApp.CreateItem(0); // 0 = olMailItem

                inspector = mailItem.GetInspector;
                string signatureHtml = mailItem.HTMLBody ?? string.Empty;

                string subject = $"Passaggio di consegne - Rapportino Turno {rapportino.TipoTreno} - {rapportino.Data}";
                mailItem.Subject = subject;

                var recipientConfig = DestinatariManager.GetRecipients(rapportino.TipoTreno, "Passaggio di consegne")
                                   ?? DestinatariManager.GetRecipients(rapportino.TipoTreno, "Chiusura Ticket");

                if (recipientConfig != null)
                {
                    if (!string.IsNullOrEmpty(recipientConfig.ToRecipients)) mailItem.To = recipientConfig.ToRecipients;
                    if (!string.IsNullOrEmpty(recipientConfig.CcRecipients)) mailItem.CC = recipientConfig.CcRecipients;
                }

                string bodyHtml = $@"
<p style='font-family: Calibri, Arial, sans-serif; font-size: 15px;'>
    Buongiorno,<br><br>
    In allegato il <b>Rapportino di Turno ({rapportino.TipoTreno})</b> per il passaggio di consegne relativo al <b>{rapportino.Data}</b>.<br><br>
    <b>Operatore:</b> {rapportino.Nome} {rapportino.Cognome}<br>
    <b>Turno:</b> {rapportino.OraInizio} - {rapportino.OraFine}<br><br>
    Cordiali saluti.
</p><br>";

                if (!string.IsNullOrEmpty(signatureHtml))
                {
                    mailItem.HTMLBody = bodyHtml + signatureHtml;
                }
                else
                {
                    mailItem.HTMLBody = bodyHtml;
                }

                if (File.Exists(pdfPath))
                {
                    mailItem.Attachments.Add(pdfPath);
                }

                mailItem.Display(false);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Si è verificato un errore nell'apertura della bozza email:\n{ex.Message}", "Errore Email", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                ReleaseCom(inspector);
                ReleaseCom(mailItem);
                ReleaseCom(outlookApp);
            }
        }

        /// <summary>
        /// Rilascia un riferimento COM ignorando eventuali errori, così che il rilascio dei
        /// riferimenti successivi non venga mai saltato.
        /// </summary>
        private static void ReleaseCom(object? comObject)
        {
            if (comObject == null) return;
            try
            {
                System.Runtime.InteropServices.Marshal.ReleaseComObject(comObject);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"ReleaseComObject fallita: {ex.Message}");
            }
        }
    }
}
