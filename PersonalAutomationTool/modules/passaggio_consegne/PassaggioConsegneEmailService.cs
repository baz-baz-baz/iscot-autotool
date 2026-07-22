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
            try
            {
                Type? outlookType = Type.GetTypeFromProgID("Outlook.Application");
                if (outlookType == null)
                {
                    MessageBox.Show($"Il PDF del rapportino è stato generato in:\n{pdfPath}\n\nMicrosoft Outlook non è stato rilevato per l'apertura della bozza email.", "PDF Generato", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                dynamic outlookApp = Activator.CreateInstance(outlookType)!;
                dynamic mailItem = outlookApp.CreateItem(0); // 0 = olMailItem

                var inspector = mailItem.GetInspector;
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

                System.Runtime.InteropServices.Marshal.ReleaseComObject(inspector);
                System.Runtime.InteropServices.Marshal.ReleaseComObject(mailItem);
                System.Runtime.InteropServices.Marshal.ReleaseComObject(outlookApp);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Si è verificato un errore nell'apertura della bozza email:\n{ex.Message}", "Errore Email", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
