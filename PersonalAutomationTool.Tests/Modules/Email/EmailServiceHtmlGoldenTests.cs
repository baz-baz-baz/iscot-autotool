using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text.RegularExpressions;
using PersonalAutomationTool.Modules.Email;
using PersonalAutomationTool.Modules.Email.Dialogs;
using Xunit;

namespace PersonalAutomationTool.Tests.Modules.Email
{
    /// <summary>
    /// Golden-file test su <c>EmailService.BuildHtmlBody</c> (reso <c>internal</c> apposta, vedi
    /// AssemblyInfo.cs): congela l'HTML che finisce nel corpo delle email di chiusura ticket,
    /// il testo che arriva realmente a Hitachi/Trenitalia — se questo test rompe, è quasi
    /// certamente perché sta per rompersi qualcosa che il tecnico vede nella sua casella di posta.
    /// <para>
    /// Da quando E404P (ETR 500) è stato incluso in <c>targetTrains</c> (modifica operativa di
    /// cantiere: Chiusura Ticket e notifica Log &amp; Dump accorpate in una sola email, come già
    /// per ETR700/ETR1000), i test dedicati a E404P coprono sia il caso LOG+DUMP entrambi presenti
    /// sia i due casi con una sola cartella.
    /// </para>
    /// <para>
    /// Due scelte deliberate sulla stringa di riferimento:
    /// </para>
    /// <list type="bullet">
    /// <item>Il saluto (<c>DetermineSaluto</c>) dipende dall'ora corrente ("Buongiorno,"/"Buon
    /// pomeriggio,"/"Buonanotte,"): normalizzato prima del confronto, altrimenti il test sarebbe
    /// instabile a seconda di quando viene eseguito.</item>
    /// <item>Le locomotive di test usano matricole chiaramente inesistenti (99999, 88888) apposta:
    /// se avessero valori reali, un aggiornamento legittimo di <c>train_software.db</c> (nuova
    /// composizione flotta) farebbe fallire questo test per un motivo estraneo al suo scopo — che
    /// è verificare il TEMPLATE, non il contenuto del database.</item>
    /// </list>
    /// <para>
    /// Il valore atteso è stato catturato dall'esecuzione reale di BuildHtmlBody (non derivato a
    /// mano leggendo il codice) e poi verificato manualmente prima di essere congelato qui.
    /// </para>
    /// </summary>
    public partial class EmailServiceHtmlGoldenTests
    {
        [GeneratedRegex(@"<p style='font-size: 14pt;'>(Buongiorno,|Buon pomeriggio,|Buonanotte,)</p>")]
        private static partial Regex GreetingRegex();

        private static string NormalizeGreeting(string html) =>
            GreetingRegex().Replace(html, "<p style='font-size: 14pt;'>{SALUTO}</p>", 1);

        private const string ExpectedHtml =
            "<div style='font-family: Calibri, sans-serif; font-size: 11pt; color: black;'>" +
            "<p style='font-size: 14pt;'>{SALUTO}</p>" +
            "<p style='font-size: 14pt;'>con la presente vi invio la chiusura del ticket in oggetto.</p>" +
            "<p style='font-size: 14pt;'>Confermo l'inserimento in rete dei seguenti files:</p>" +
            "<p style='font-size: 14pt;'><b>SR1247654 LOG ETR700 117 04.02HR 300526 Todde</b><br><b>SR1247654 DUMP ETR700 117 04.02HR 300526 Todde</b></p>" +
            "<p style='font-size: 14pt;'>Di seguito la descrizione delle avarie segnalate dal PdC e dell'intervento effettuato:</p>" +
            "<table style='width: 100%; border-collapse: collapse; border: 1px solid #ddd; margin-top: 20px; margin-bottom: 20px;'>" +
            "<thead><tr style='background-color: #A6A6A6; color: black; font-weight: bold; text-align: center;'>" +
            "<th style='border: 1px solid white; padding: 10px;'><i>TRENO</i></th>" +
            "<th style='border: 1px solid white; padding: 10px;'><i>LOCOMOTORE</i></th>" +
            "<th style='border: 1px solid white; padding: 10px;'><i>AVARIA SEGNALATA</i></th>" +
            "<th style='border: 1px solid white; padding: 10px;'><i>DESCRIZIONE INTERVENTO</i></th>" +
            "<th style='border: 1px solid white; padding: 10px;'><i>VERSIONE SW</i></th>" +
            "</tr></thead><tbody>" +
            "<tr style='text-align: center; vertical-align: middle; border: 1px solid #ddd;'>" +
            "<td style='border: 1px solid #ddd; padding: 10px; font-weight: bold;'></td>" +
            "<td style='border: 1px solid #ddd; padding: 10px; font-weight: bold;'>99999</td>" +
            "<td style='border: 1px solid #ddd; padding: 10px; font-size: 14pt;'>Avviso=998877 Data: 01/07/2026 10:30<br><br>Oscuramento monitor cabina 1</td>" +
            "<td style='border: 1px solid #ddd; padding: 10px; font-size: 14pt;'>Sostituito modulo display, verificato funzionamento.</td>" +
            "<td style='border: 1px solid #ddd; padding: 10px; font-weight: bold;'></td>" +
            "</tr></tbody></table><br>" +
            "<table style='width: 100%; border-collapse: collapse; border: 1px solid #ddd; margin-top: 20px; margin-bottom: 20px;'>" +
            "<thead><tr style='background-color: #A6A6A6; color: black; font-weight: bold; text-align: center;'>" +
            "<th style='border: 1px solid white; padding: 10px;'><i>TRENO</i></th>" +
            "<th style='border: 1px solid white; padding: 10px;'><i>LOCOMOTORE</i></th>" +
            "<th style='border: 1px solid white; padding: 10px;'><i>AVARIA SEGNALATA</i></th>" +
            "<th style='border: 1px solid white; padding: 10px;'><i>DESCRIZIONE INTERVENTO</i></th>" +
            "<th style='border: 1px solid white; padding: 10px;'><i>VERSIONE SW</i></th>" +
            "</tr></thead><tbody>" +
            "<tr style='text-align: center; vertical-align: middle; border: 1px solid #ddd;'>" +
            "<td style='border: 1px solid #ddd; padding: 10px; font-weight: bold;'></td>" +
            "<td style='border: 1px solid #ddd; padding: 10px; font-weight: bold;'>88888</td>" +
            "<td style='border: 1px solid #ddd; padding: 10px; font-size: 14pt;'>Nulla riscontrato</td>" +
            "<td style='border: 1px solid #ddd; padding: 10px; font-size: 14pt;'>Controlli statici eseguiti con esito positivo.</td>" +
            "<td style='border: 1px solid #ddd; padding: 10px; font-weight: bold;'></td>" +
            "</tr></tbody></table><br>" +
            "<p style='margin-top: 20px; font-size: 14pt; margin-bottom: 5px;'>Cordiali saluti,</p>" +
            "<p style='font-family: \"Calibri Light\", Calibri, sans-serif; font-size: 14pt; color: #A6A6A6; font-weight: 300; margin-top: 0px;'>Ticket: 1247654, 1247655</p>" +
            "</div>";

        [Fact]
        public void BuildHtmlBody_ChiusuraTicket_MatchesFrozenTemplate()
        {
            var locoGroups = new ObservableCollection<LocoGroupModel>
            {
                new()
                {
                    GroupLocoName = "99999",
                    Inputs = new ObservableCollection<TicketInputModel>
                    {
                        new()
                        {
                            Avviso = "998877",
                            DataOra = "01/07/2026 10:30",
                            Avaria = "Oscuramento monitor cabina 1",
                            Intervento = "Sostituito modulo display, verificato funzionamento."
                        }
                    }
                },
                new()
                {
                    GroupLocoName = "88888",
                    Inputs = new ObservableCollection<TicketInputModel>
                    {
                        new()
                        {
                            Avviso = "",
                            DataOra = "",
                            Avaria = "Nulla riscontrato",
                            Intervento = "Controlli statici eseguiti con esito positivo."
                        }
                    }
                }
            };

            var logFolders = new List<string> { "SR1247654 LOG ETR700 117 04.02HR 300526 Todde" };
            var dumpFolders = new List<string> { "SR1247654 DUMP ETR700 117 04.02HR 300526 Todde" };
            var bottomTickets = new List<string> { "1247654", "1247655" };

            string actualHtml = EmailService.BuildHtmlBody("ETR700", "Chiusura Ticket", logFolders, dumpFolders, locoGroups, bottomTickets);

            Assert.Equal(ExpectedHtml, NormalizeGreeting(actualHtml));
        }

        [Fact]
        public void BuildHtmlBody_ChiusuraTicket_E404P_MatchesFrozenTemplate()
        {
            // Modifica operativa di cantiere: la Chiusura Ticket E404P (ETR 500) ora accorpa anche
            // la notifica Log & Dump nella stessa email, esattamente come già avveniva per
            // ETR700/ETR1000 sopra — stesso ramo "Confermo l'inserimento in rete dei seguenti
            // files:", non più solo il ramo separato "Log Dump" testato più sotto.
            var locoGroups = new ObservableCollection<LocoGroupModel>
            {
                new()
                {
                    GroupLocoName = "99999",
                    Inputs = new ObservableCollection<TicketInputModel>
                    {
                        new()
                        {
                            Avviso = "112233",
                            DataOra = "05/09/2026 08:15",
                            Avaria = "Anomalia SSB cabina 2",
                            Intervento = "Sostituita scheda diagnostica, verificato funzionamento."
                        }
                    }
                }
            };

            var logFolders = new List<string> { "SR1300001 LOG E404P 650 04.02HR 010726 Bassetto" };
            var dumpFolders = new List<string> { "SR1300001 DUMP E404P 650 04.02HR 010726 Bassetto" };
            var bottomTickets = new List<string> { "1300001" };

            string actualHtml = EmailService.BuildHtmlBody("E404P", "Chiusura Ticket", logFolders, dumpFolders, locoGroups, bottomTickets);

            const string expectedHtml =
                "<div style='font-family: Calibri, sans-serif; font-size: 11pt; color: black;'>" +
                "<p style='font-size: 14pt;'>{SALUTO}</p>" +
                "<p style='font-size: 14pt;'>con la presente vi invio la chiusura del ticket in oggetto.</p>" +
                "<p style='font-size: 14pt;'>Confermo l'inserimento in rete dei seguenti files:</p>" +
                "<p style='font-size: 14pt;'><b>SR1300001 LOG E404P 650 04.02HR 010726 Bassetto</b><br><b>SR1300001 DUMP E404P 650 04.02HR 010726 Bassetto</b></p>" +
                "<p style='font-size: 14pt;'>Di seguito la descrizione delle avarie segnalate dal PdC e dell'intervento effettuato:</p>" +
                "<table style='width: 100%; border-collapse: collapse; border: 1px solid #ddd; margin-top: 20px; margin-bottom: 20px;'>" +
                "<thead><tr style='background-color: #A6A6A6; color: black; font-weight: bold; text-align: center;'>" +
                "<th style='border: 1px solid white; padding: 10px;'><i>TRENO</i></th>" +
                "<th style='border: 1px solid white; padding: 10px;'><i>LOCOMOTORE</i></th>" +
                "<th style='border: 1px solid white; padding: 10px;'><i>AVARIA SEGNALATA</i></th>" +
                "<th style='border: 1px solid white; padding: 10px;'><i>DESCRIZIONE INTERVENTO</i></th>" +
                "<th style='border: 1px solid white; padding: 10px;'><i>VERSIONE SW</i></th>" +
                "</tr></thead><tbody>" +
                "<tr style='text-align: center; vertical-align: middle; border: 1px solid #ddd;'>" +
                "<td style='border: 1px solid #ddd; padding: 10px; font-weight: bold;'></td>" +
                "<td style='border: 1px solid #ddd; padding: 10px; font-weight: bold;'>99999</td>" +
                "<td style='border: 1px solid #ddd; padding: 10px; font-size: 14pt;'>Avviso=112233 Data: 05/09/2026 08:15<br><br>Anomalia SSB cabina 2</td>" +
                "<td style='border: 1px solid #ddd; padding: 10px; font-size: 14pt;'>Sostituita scheda diagnostica, verificato funzionamento.</td>" +
                "<td style='border: 1px solid #ddd; padding: 10px; font-weight: bold;'></td>" +
                "</tr></tbody></table><br>" +
                "<p style='margin-top: 20px; font-size: 14pt; margin-bottom: 5px;'>Cordiali saluti,</p>" +
                "<p style='font-family: \"Calibri Light\", Calibri, sans-serif; font-size: 14pt; color: #A6A6A6; font-weight: 300; margin-top: 0px;'>Ticket: 1300001</p>" +
                "</div>";

            Assert.Equal(expectedHtml, NormalizeGreeting(actualHtml));
        }

        [Fact]
        public void BuildHtmlBody_ChiusuraTicket_E404P_OnlyLogFolder_ListsJustThatFolder()
        {
            // Se per il treno esiste solo la cartella LOG (nessun DUMP), l'elenco deve indicare
            // solo quella rilevata, non un'assenza silenziosa della sezione.
            var locoGroups = new ObservableCollection<LocoGroupModel>
            {
                new()
                {
                    GroupLocoName = "99999",
                    Inputs = new ObservableCollection<TicketInputModel> { new() { Avaria = "Test", Intervento = "Test" } }
                }
            };
            var logFolders = new List<string> { "SR2 LOG E404P 601 04.02HR 010726 Utente" };
            var dumpFolders = new List<string>();

            string html = EmailService.BuildHtmlBody("E404P", "Chiusura Ticket", logFolders, dumpFolders, locoGroups, []);

            Assert.Contains("Confermo l'inserimento in rete dei seguenti files:", html);
            Assert.Contains("<b>SR2 LOG E404P 601 04.02HR 010726 Utente</b>", html);
            Assert.DoesNotContain("DUMP", html);
        }

        [Fact]
        public void BuildHtmlBody_ChiusuraTicket_E404P_OnlyDumpFolder_ListsJustThatFolder()
        {
            // Speculare al test precedente: solo DUMP presente, nessun LOG.
            var locoGroups = new ObservableCollection<LocoGroupModel>
            {
                new()
                {
                    GroupLocoName = "99999",
                    Inputs = new ObservableCollection<TicketInputModel> { new() { Avaria = "Test", Intervento = "Test" } }
                }
            };
            var logFolders = new List<string>();
            var dumpFolders = new List<string> { "SR3 DUMP E404P 602 04.02HR 010726 Utente" };

            string html = EmailService.BuildHtmlBody("E404P", "Chiusura Ticket", logFolders, dumpFolders, locoGroups, []);

            Assert.Contains("Confermo l'inserimento in rete dei seguenti files:", html);
            Assert.Contains("<b>SR3 DUMP E404P 602 04.02HR 010726 Utente</b>", html);
            Assert.DoesNotContain(" LOG ", html);
        }

        [Fact]
        public void BuildHtmlBody_ChiusuraTicket_Etr1001FH_IncludeConfermaInserimentoInRete()
        {
            // ETR1001FH (rename da "ETR1000FH", il valore reale della colonna `tipo` in flotte.db,
            // PROJECT_MEMORY.md §5.3-bis) deve restare nel ramo "targetTrains" di EmailService: senza
            // la voce aggiornata nell'array, la mail di chiusura ticket per questa flotta perderebbe
            // silenziosamente la conferma di inserimento in rete dei file LOG/DUMP.
            var locoGroups = new ObservableCollection<LocoGroupModel>
            {
                new()
                {
                    GroupLocoName = "410",
                    Inputs = new ObservableCollection<TicketInputModel>
                    {
                        new() { Avaria = "Test", Intervento = "Test" }
                    }
                }
            };

            var logFolders = new List<string> { "SR5566778 LOG ETR1001FH 410 04.03HR 230626 Blu" };
            var dumpFolders = new List<string> { "SR5566778 DUMP ETR1001FH 410 04.03HR 230626 Blu" };

            string html = EmailService.BuildHtmlBody("ETR1001FH", "Chiusura Ticket", logFolders, dumpFolders, locoGroups, []);

            Assert.Contains("Confermo l'inserimento in rete dei seguenti files:", html);
            Assert.Contains("<b>SR5566778 LOG ETR1001FH 410 04.03HR 230626 Blu</b>", html);
            Assert.Contains("<b>SR5566778 DUMP ETR1001FH 410 04.03HR 230626 Blu</b>", html);
        }

        [Fact]
        public void BuildHtmlBody_LogDump_E404P_UsesBulletListInsteadOfTable_ForFolderNames()
        {
            // Ramo diverso dal precedente: actionType "Log Dump" + trainType "E404P" produce un
            // elenco puntato dei file invece della frase "Confermo l'inserimento in rete...".
            // Non un confronto byte-per-byte come sopra: verifica solo che il ramo giusto scatti,
            // a fronte di un secondo golden-file completo con più manutenzione senza beneficio
            // proporzionato (i due rami condividono comunque la parte tabellare già coperta sopra).
            var locoGroups = new ObservableCollection<LocoGroupModel>
            {
                new()
                {
                    GroupLocoName = "99999",
                    Inputs = new ObservableCollection<TicketInputModel> { new() { Avaria = "Test", Intervento = "Test" } }
                }
            };
            var logFolders = new List<string> { "SR1 LOG E404P 601 04.02HR 010726 Utente" };
            var dumpFolders = new List<string> { "SR1 DUMP E404P 601 04.02HR 010726 Utente" };

            string html = EmailService.BuildHtmlBody("E404P", "Log Dump", logFolders, dumpFolders, locoGroups, []);

            Assert.Contains("Con la presente comunica l'inserimento in rete", html);
            Assert.Contains("<ul>", html);
            Assert.DoesNotContain("Confermo l'inserimento in rete dei seguenti files", html);
        }
    }
}
