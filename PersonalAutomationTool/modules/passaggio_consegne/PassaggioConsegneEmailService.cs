using System;
using System.IO;
using System.Runtime.InteropServices;
using PersonalAutomationTool.Modules.DestinatariMail;
using PersonalAutomationTool.Modules.Email;

namespace PersonalAutomationTool.Modules.PassaggioConsegne
{
    /// <summary>
    /// Crea in Outlook la bozza del passaggio di consegne, con il PDF del rapportino allegato.
    ///
    /// <para>
    /// Segue gli stessi due vincoli di <see cref="EmailService"/>, entrambi documentati in §5.5 di
    /// PROJECT_MEMORY.md: <c>GetInspector</c> va invocato per costringere Outlook a generare la firma,
    /// e il corpo va inserito <b>dentro</b> l'HTML della firma (tramite
    /// <see cref="EmailService.ComponiCorpoConFirma"/>) e non concatenato prima, altrimenti la firma
    /// finisce in testa al messaggio. <c>Display(false)</c> mostra la bozza senza inviarla: l'invio
    /// resta un gesto manuale del tecnico.
    /// </para>
    /// </summary>
    public sealed class OutlookRapportinoMailService : IRapportinoMailService
    {
        /// <summary>
        /// Azione con cui i destinatari del passaggio di consegne sono registrati in
        /// <c>destinatari.json</c>, gestibile dalla schermata DESTINATARI MAIL.
        ///
        /// <para>
        /// ⚠️ <b>Il nome deve restare esattamente questo.</b> È la voce già presente da sempre nel
        /// <c>destinatari.json</c> installato sulle macchine dei tecnici, con gli indirizzi reali e
        /// personalizzati a mano. Cambiarlo — anche solo in "Passaggio Consegne", senza il "di" — non
        /// rinomina la voce esistente: ne fa comparire una <b>seconda</b>, vuota o con i valori di
        /// default, lasciando quella buona inutilizzata. È esattamente l'errore commesso alla prima
        /// stesura di questo modulo e segnalato dal committente.
        /// </para>
        /// </summary>
        public const string AzioneDestinatari = "Passaggio di consegne";

        /// <summary>
        /// Se per una flotta non risultassero destinatari di "Passaggio di consegne" si ripiega su
        /// quelli della chiusura ticket, che hanno la stessa platea: meglio una bozza con i
        /// destinatari plausibili già compilati che una bozza vuota da riempire a mano a fine turno.
        /// </summary>
        private const string AzioneRipiego = "Chiusura Ticket";

        public void ApriBozza(RapportinoSnapshot rapportino, string percorsoPdf, string destinatariKey, string oggetto)
        {
            ArgumentNullException.ThrowIfNull(rapportino);

            // Riferimenti COM dichiarati fuori dal try: il finally deve poterli rilasciare anche
            // quando la generazione fallisce a metà, altrimenti OUTLOOK.EXE resta agganciato (§4.4).
            dynamic? outlookApp = null;
            dynamic? mailItem = null;
            dynamic? inspector = null;

            try
            {
                Type? outlookType = Type.GetTypeFromProgID("Outlook.Application");
                if (outlookType == null)
                {
                    throw new InvalidOperationException(
                        $"Microsoft Outlook non è stato rilevato su questa macchina.\n\n" +
                        $"Il PDF del rapportino è comunque stato generato in:\n{percorsoPdf}");
                }

                outlookApp = Activator.CreateInstance(outlookType)!;
                mailItem = outlookApp.CreateItem(0); // 0 = olMailItem

                // Forza Outlook a generare la firma dentro HTMLBody prima di leggerla.
                inspector = mailItem.GetInspector;
                string firmaHtml = mailItem.HTMLBody ?? string.Empty;

                mailItem.Subject = oggetto;

                var destinatari = DestinatariManager.GetRecipients(destinatariKey, AzioneDestinatari)
                                  ?? DestinatariManager.GetRecipients(destinatariKey, AzioneRipiego);
                if (destinatari != null)
                {
                    if (!string.IsNullOrWhiteSpace(destinatari.ToRecipients)) mailItem.To = destinatari.ToRecipients;
                    if (!string.IsNullOrWhiteSpace(destinatari.CcRecipients)) mailItem.CC = destinatari.CcRecipients;
                }

                mailItem.HTMLBody = EmailService.ComponiCorpoConFirma(ComponiCorpo(rapportino), firmaHtml);

                if (File.Exists(percorsoPdf)) mailItem.Attachments.Add(percorsoPdf);

                mailItem.Display(false);
            }
            finally
            {
                ReleaseCom(inspector);
                ReleaseCom(mailItem);
                ReleaseCom(outlookApp);
            }
        }

        /// <summary>
        /// Corpo del messaggio. Volutamente breve: il contenuto del turno sta nel PDF allegato, qui
        /// serve solo il minimo per capire di quale turno e di quale operatore si tratta senza aprire
        /// l'allegato.
        /// </summary>
        internal static string ComponiCorpo(RapportinoSnapshot r)
        {
            string operatore = $"{r.Nome} {r.Cognome}".Trim();
            string orario = string.IsNullOrWhiteSpace(r.OraInizio) && string.IsNullOrWhiteSpace(r.OraFine)
                ? string.Empty
                : $"{r.OraInizio} - {r.OraFine}".Trim(' ', '-');

            var corpo = new System.Text.StringBuilder();
            corpo.Append("<p style=\"font-family: Calibri, Arial, sans-serif; font-size: 15px;\">");
            corpo.Append("Buongiorno,<br><br>");
            corpo.Append($"in allegato il rapportino di turno <b>{Escape(r.TipoTreno)}</b> ");
            corpo.Append($"relativo al <b>{Escape(r.Data)}</b>.<br><br>");

            if (!string.IsNullOrWhiteSpace(operatore))
                corpo.Append($"<b>Operatore:</b> {Escape(operatore)}<br>");
            if (!string.IsNullOrWhiteSpace(orario))
                corpo.Append($"<b>Turno:</b> {Escape(orario)}<br>");

            corpo.Append("<br>Cordiali saluti.</p><br>");
            return corpo.ToString();
        }

        /// <summary>
        /// Neutralizza i caratteri che romperebbero l'HTML del messaggio. I campi provengono da
        /// digitazione libera del tecnico: una "&amp;" in un cognome non deve produrre markup rotto.
        /// </summary>
        private static string Escape(string? valore) =>
            System.Net.WebUtility.HtmlEncode(valore ?? string.Empty);

        /// <summary>
        /// Rilascia un riferimento COM ignorando eventuali errori, così che il rilascio dei
        /// riferimenti successivi non venga mai saltato (§4.4).
        /// </summary>
        private static void ReleaseCom(object? comObject)
        {
            if (comObject == null) return;
            try
            {
                Marshal.ReleaseComObject(comObject);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"ReleaseComObject fallita: {ex.Message}");
            }
        }
    }
}
