using System;
using System.IO;
using PersonalAutomationTool.Modules.Email;
using Xunit;

namespace PersonalAutomationTool.Tests.Modules.Email
{
    /// <summary>
    /// Tier 2 (cartelle vere su disco): <c>EmailService.BuildSubject</c>, l'oggetto della bozza
    /// Outlook di chiusura ticket. Non aveva copertura xUnit (elencata come debito residuo in
    /// PROJECT_MEMORY.md §6.5) finché non è servita a diagnosticare il difetto qui sotto.
    ///
    /// <para>
    /// <b>Il difetto segnalato dal committente.</b> Con due ticket (due cartelle SR distinte),
    /// l'oggetto ripeteva il software una volta per ciascuna locomotiva —
    /// "SR1234567 - SR1234568 E404P 650 04.02HR - 651 04.02HR IMC AV Milano…" — invece di comparire
    /// una sola volta: "650 - 651 04.02HR". Causa: fra tipo treno e data la grammatica di cartella
    /// (§5.1) prevede due campi distinti, loco e software, ma il codice li prendeva insieme come un
    /// unico "campo loco" per ciascuna cartella — con due cartelle, due copie del software.
    /// </para>
    /// </summary>
    public sealed class EmailServiceBuildSubjectTests : IDisposable
    {
        private readonly string _cartella;

        public EmailServiceBuildSubjectTests()
        {
            _cartella = Path.Combine(Path.GetTempPath(), "PatTests_BuildSubject_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_cartella);
        }

        public void Dispose()
        {
            try { if (Directory.Exists(_cartella)) Directory.Delete(_cartella, true); }
            catch { /* pulizia best-effort */ }
        }

        private void CreaSottocartella(string nome) => Directory.CreateDirectory(Path.Combine(_cartella, nome));

        [Fact]
        public void DueTicket_IlSoftwareComparUnaSolaVoltaDopoEntrambeLeLoco()
        {
            // Esattamente il caso dello screenshot del committente.
            CreaSottocartella("SR1234567 LOG E404P 650 04.02HR 300826 Bassetto");
            CreaSottocartella("SR1234567 DUMP E404P 650 04.02HR 300826 Bassetto");
            CreaSottocartella("SR1234568 LOG E404P 651 04.02HR 300826 Bassetto");
            CreaSottocartella("SR1234568 DUMP E404P 651 04.02HR 300826 Bassetto");

            string subject = EmailService.BuildSubject(
                "E404P 12", "E404P", "Chiusura Ticket", isNdPrefix: false, logFolders: [], folderPath: _cartella);

            Assert.Equal(
                "CHIUSURA TICKET SR1234567 - SR1234568 E404P 650 - 651 04.02HR IMC AV Milano 300826 Bassetto",
                subject);
        }

        [Fact]
        public void UnSoloTicket_IlSoftwareComparUnaVoltaComePrima()
        {
            // Comportamento invariato: con un solo ticket il difetto non si manifestava già prima
            // (una sola cartella → un solo "campo loco" combinato, per coincidenza già corretto).
            CreaSottocartella("SR1234567 LOG E404P 650 04.02HR 300826 Bassetto");
            CreaSottocartella("SR1234567 DUMP E404P 650 04.02HR 300826 Bassetto");

            string subject = EmailService.BuildSubject(
                "E404P 12", "E404P", "Chiusura Ticket", isNdPrefix: false, logFolders: [], folderPath: _cartella);

            Assert.Equal(
                "CHIUSURA TICKET SR1234567 E404P 650 04.02HR IMC AV Milano 300826 Bassetto",
                subject);
        }

        [Fact]
        public void TrenoIF_IlTokenAggiuntivoNonSfasaLEstrazioneDiLocoESoftware()
        {
            // Per I-F/FH la grammatica ha un token in più prima del loco (§5.2): locoStartIndex
            // passa da 3 a 4. Un errore qui romperebbe silenziosamente solo le flotte I-F/FH.
            CreaSottocartella("SR1234567 LOG ETR1000 I-F 117 04.02HR 300826 Bassetto");
            CreaSottocartella("SR1234567 DUMP ETR1000 I-F 117 04.02HR 300826 Bassetto");

            string subject = EmailService.BuildSubject(
                "ETR1000 I-F 12", "ETR1000IF", "Chiusura Ticket", isNdPrefix: false, logFolders: [], folderPath: _cartella);

            Assert.Equal(
                "CHIUSURA TICKET SR1234567 ETR1000IF 117 04.02HR IMC AV Milano 300826 Bassetto",
                subject);
        }

        [Fact]
        public void LogDumpE404P_DueTicket_IlSoftwareComparUnaSolaVolta()
        {
            // Stesso difetto, ramo diverso: "Log Dump" su E404P ha un formato di oggetto proprio,
            // ma costruisce locosStr dalla stessa lista — corretta una sola volta per entrambi i rami.
            CreaSottocartella("SR1234567 LOG E404P 650 04.02HR 300826 Bassetto");
            CreaSottocartella("SR1234568 LOG E404P 651 04.02HR 300826 Bassetto");

            string subject = EmailService.BuildSubject(
                "E404P 12", "E404P", "Log Dump", isNdPrefix: false, logFolders: [], folderPath: _cartella);

            Assert.Equal(
                "SR1234567 - SR1234568 LOG E DUMP in rete E404P 650 - 651 04.02HR IMC AV Milano 300826 Bassetto",
                subject);
        }

        [Fact]
        public void IsNdPrefix_AggiungeNDInTesta()
        {
            CreaSottocartella("SR1234567 LOG E404P 650 04.02HR 300826 Bassetto");

            string subject = EmailService.BuildSubject(
                "E404P 12", "E404P", "Chiusura Ticket", isNdPrefix: true, logFolders: [], folderPath: _cartella);

            Assert.StartsWith("ND CHIUSURA TICKET", subject);
        }

        [Fact]
        public void CartellaSenzaSottocartelleValide_RipiegaSulNomeDellaCartella()
        {
            string subject = EmailService.BuildSubject(
                "E404P 12", "E404P", "Chiusura Ticket", isNdPrefix: false, logFolders: [], folderPath: _cartella);

            Assert.Equal("CHIUSURA TICKET E404P 12", subject);
        }
    }
}
