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

        public void ApriBozza(RapportinoSnapshot rapportino, string percorsoPdf, string destinatariKey, string oggetto, StatoTurno stato)
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

                string flotta = (rapportino.TipoTreno ?? string.Empty).Replace(" ", string.Empty);
                mailItem.HTMLBody = EmailService.ComponiCorpoConFirma(BuildHtmlBody(flotta, stato, DateTime.Now), firmaHtml);

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
        /// Saluto in base alla fascia oraria corrente, sullo stesso principio già consolidato in
        /// <c>EmailService.DetermineSaluto</c> (§4.5 di PROJECT_MEMORY.md) ma con le quattro fasce
        /// richieste dal corpo del passaggio di consegne invece delle tre del modulo EMAIL. Non è la
        /// stessa funzione — e non lo diventa modificando quella — perché estendere
        /// <c>EmailService.DetermineSaluto</c> a quattro fasce cambierebbe il testo delle email di
        /// chiusura ticket già in uso, cosa non richiesta qui.
        /// <para>
        /// <c>internal</c> e parametrizzata su <paramref name="adesso"/> (non <c>DateTime.Now</c>
        /// letto internamente) apposta per essere testabile in modo deterministico, senza dipendere
        /// dall'ora in cui gira la suite.
        /// </para>
        /// </summary>
        internal static string DetermineSaluto(DateTime adesso)
        {
            int ora = adesso.Hour;
            if (ora >= 4 && ora < 14) return "Buongiorno,";
            if (ora >= 14 && ora < 18) return "Buon pomeriggio,";
            if (ora >= 18 && ora < 22) return "Buonasera,";
            return "Buonanotte,";
        }

        /// <summary>Colore HTML dell'etichetta di flotta, condizionato dallo stato scelto nel pop-up.</summary>
        internal static string ColoreStato(StatoTurno stato) => stato switch
        {
            StatoTurno.NessunaAttivita => "#28A745",           // verde
            StatoTurno.AttivitaPreviste => "#D39E00",           // ambra scuro, leggibile su bianco
            StatoTurno.AttivitaImminentiOInCorso => "#C00000",  // rosso
            _ => "#000000"
        };

        /// <summary>
        /// Corpo del messaggio, secondo la struttura fissa richiesta: saluto per fascia oraria,
        /// etichetta di flotta colorata in base allo stato del turno, testo centrale (dicitura fissa
        /// se non ci sono attività, due righe vuote da compilare a mano altrimenti) e chiusura
        /// "Saluti". Il dettaglio del turno resta nel PDF allegato, non nel corpo.
        /// </summary>
        internal static string BuildHtmlBody(string flotta, StatoTurno stato, DateTime adesso)
        {
            string saluto = DetermineSaluto(adesso);
            string colore = ColoreStato(stato);
            string centro = stato == StatoTurno.NessunaAttivita
                ? "<p style=\"margin: 0 0 12px 0;\">Nessuna attività in sospeso.</p>"
                : "<br/><br/>";

            var corpo = new System.Text.StringBuilder();
            corpo.Append("<div style=\"font-family: Calibri, Arial, sans-serif; font-size: 21px; color: #000000;\">");
            corpo.Append($"<p style=\"margin: 0 0 12px 0;\">{Escape(saluto)}</p>");
            corpo.Append($"<p style=\"margin: 0 0 12px 0; font-size: 42px; font-weight: bold; color: {colore};\">{Escape(flotta)}</p>");
            corpo.Append(centro);
            corpo.Append("<p style=\"margin: 12px 0 0 0;\">Saluti</p>");
            corpo.Append("</div>");
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
