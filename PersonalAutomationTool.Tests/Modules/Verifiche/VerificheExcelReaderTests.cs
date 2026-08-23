using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ClosedXML.Excel;
using PersonalAutomationTool.Modules.Verifiche;
using Xunit;

namespace PersonalAutomationTool.Tests.Modules.Verifiche
{
    /// <summary>
    /// Tier 2 su file <c>.xlsx</c> reali generati al volo: verifica che la lettura SAX
    /// (<see cref="VerificheExcelReader"/>, intervento 3.4) produca **esattamente** lo stesso
    /// risultato del percorso ClosedXML che sostituisce.
    ///
    /// <para>
    /// <b>Perché questi test sbloccano un intervento rimandato due volte.</b> La roadmap segnalava
    /// 3.4 come non affrontabile senza un file "Verifiche" reale su cui validare la riscrittura
    /// (PROJECT_MEMORY.md §6.1-bis). Il file reale continua a non essere disponibile, ma la garanzia
    /// che serve non è "funziona su quel file": è "si comporta come l'implementazione precedente".
    /// Quella è verificabile per <b>equivalenza differenziale</b> — si esegue anche il vecchio
    /// percorso, sullo stesso file, e si confrontano gli output. I file di prova sono costruiti per
    /// riprodurre le caratteristiche note del formato reale: intestazione non sulla prima riga,
    /// righe vuote intercalate, colonne in posizione arbitraria, celle numeriche e testuali miste,
    /// celle vuote all'interno delle righe.
    /// </para>
    /// </summary>
    public sealed class VerificheExcelReaderTests : IDisposable
    {
        private readonly DirectoryInfo _root = Directory.CreateTempSubdirectory("VerificheExcelReaderTests_");

        public void Dispose()
        {
            try { _root.Delete(recursive: true); } catch { /* best-effort cleanup */ }
        }

        private string NewFilePath(string name) => Path.Combine(_root.FullName, name + ".xlsx");

        /// <summary>
        /// Replica esatta della logica di lettura ClosedXML preesistente (il corpo di
        /// <c>ParseExcelFileWithClosedXml</c>, prima della normalizzazione condivisa): è il termine
        /// di paragone contro cui viene verificata la lettura SAX.
        /// </summary>
        private static List<(string Treno, string Loco, string Avaria)> ReadWithClosedXml(string filePath)
        {
            var collection = new List<(string, string, string)>();

            using var workbook = new XLWorkbook(filePath);
            var worksheet = workbook.Worksheet(1);
            var rowsUsed = worksheet.RowsUsed().ToList();
            if (rowsUsed.Count < 2) return collection;

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
                string treno = row.Cell(trenoIdx).GetString()?.Trim() ?? string.Empty;
                string loco = row.Cell(locoIdx).GetString()?.Trim() ?? string.Empty;
                string avaria = row.Cell(avariaIdx).GetString()?.Trim() ?? string.Empty;

                if (!string.IsNullOrWhiteSpace(treno) || !string.IsNullOrWhiteSpace(loco) || !string.IsNullOrWhiteSpace(avaria))
                {
                    collection.Add((treno, loco, avaria));
                }
            }

            return collection;
        }

        private static List<(string Treno, string Loco, string Avaria)> ReadWithSax(string filePath) =>
            [.. VerificheExcelReader.Read(filePath).Select(r => (r.Treno, r.Loco, r.Avaria))];

        /// <summary>Il cuore di ogni test: le due implementazioni devono coincidere, riga per riga e nell'ordine.</summary>
        private static void AssertSaxMatchesClosedXml(string filePath)
        {
            var expected = ReadWithClosedXml(filePath);
            var actual = ReadWithSax(filePath);

            Assert.Equal(expected, actual);
        }

        [Fact]
        public void FoglioStandard_SaxEquivalenteAClosedXml()
        {
            string path = NewFilePath("standard");
            using (var wb = new XLWorkbook())
            {
                var ws = wb.AddWorksheet("Verifiche");
                ws.Cell(1, 1).Value = "TRENO";
                ws.Cell(1, 2).Value = "LOCO";
                ws.Cell(1, 3).Value = "Avaria segnalata da ING/SVI";
                ws.Cell(2, 1).Value = 41;
                ws.Cell(2, 2).Value = 655;
                ws.Cell(2, 3).Value = "Si richiede scarico dump, per monitoraggio funzionamento macchina.";
                ws.Cell(3, 1).Value = 49;
                ws.Cell(3, 2).Value = 849;
                ws.Cell(3, 3).Value = "si richiede uno scarico Log e Dump del giorno 18.08.2026.";
                wb.SaveAs(path);
            }

            AssertSaxMatchesClosedXml(path);
            Assert.Equal(2, ReadWithSax(path).Count);
        }

        [Fact]
        public void IntestazioneNonSullaPrimaRiga_SaxEquivalenteAClosedXml()
        {
            // Caso reale: i fogli Verifiche hanno spesso titoli o loghi sopra l'intestazione.
            string path = NewFilePath("header_spostata");
            using (var wb = new XLWorkbook())
            {
                var ws = wb.AddWorksheet("Verifiche");
                ws.Cell(1, 1).Value = "REPORT VERIFICHE FLOTTA";
                ws.Cell(3, 1).Value = "Aggiornato al 18.08.2026";
                ws.Cell(5, 1).Value = "TRENO";
                ws.Cell(5, 2).Value = "LOCO";
                ws.Cell(5, 3).Value = "AVARIA";
                ws.Cell(6, 1).Value = "Y01";
                ws.Cell(6, 2).Value = 101;
                ws.Cell(6, 3).Value = "Verifica catena radio";
                wb.SaveAs(path);
            }

            AssertSaxMatchesClosedXml(path);
            var rows = ReadWithSax(path);
            Assert.Single(rows);
            Assert.Equal("Y01", rows[0].Treno);
        }

        [Fact]
        public void RigheVuoteIntercalate_NonSpostanoLIndiceIntestazione()
        {
            // RowsUsed() salta le righe interamente vuote: il conteggio delle righe dati deve
            // basarsi su quelle NON vuote, altrimenti l'intestazione risulta nel posto sbagliato.
            string path = NewFilePath("righe_vuote");
            using (var wb = new XLWorkbook())
            {
                var ws = wb.AddWorksheet("Verifiche");
                ws.Cell(2, 1).Value = "TRENO";
                ws.Cell(2, 2).Value = "LOCO";
                ws.Cell(2, 3).Value = "AVARIA";
                ws.Cell(4, 1).Value = 31;
                ws.Cell(4, 2).Value = 201;
                ws.Cell(4, 3).Value = "Prima riga dati";
                ws.Cell(8, 1).Value = 32;
                ws.Cell(8, 2).Value = 202;
                ws.Cell(8, 3).Value = "Seconda riga dati";
                wb.SaveAs(path);
            }

            AssertSaxMatchesClosedXml(path);
            Assert.Equal(2, ReadWithSax(path).Count);
        }

        [Fact]
        public void ColonneInPosizioneArbitraria_IndividuatePerNumeroDiColonna()
        {
            // Le colonne di interesse non sono le prime tre: vanno individuate per numero di colonna
            // assoluto, non per posizione fra le celle non vuote.
            string path = NewFilePath("colonne_sparse");
            using (var wb = new XLWorkbook())
            {
                var ws = wb.AddWorksheet("Verifiche");
                ws.Cell(1, 2).Value = "Data";
                ws.Cell(1, 5).Value = "TRENO";
                ws.Cell(1, 7).Value = "LOCO";
                ws.Cell(1, 10).Value = "Avaria segnalata da ING/SVI";
                ws.Cell(2, 2).Value = "18/08/2026";
                ws.Cell(2, 5).Value = 50;
                ws.Cell(2, 7).Value = 660;
                ws.Cell(2, 10).Value = "Controllo remoto";
                wb.SaveAs(path);
            }

            AssertSaxMatchesClosedXml(path);
            var rows = ReadWithSax(path);
            Assert.Single(rows);
            Assert.Equal("50", rows[0].Treno);
            Assert.Equal("660", rows[0].Loco);
            Assert.Equal("Controllo remoto", rows[0].Avaria);
        }

        [Fact]
        public void CelleVuoteDentroLeRighe_ProduconoStringheVuoteNonDisallineamenti()
        {
            string path = NewFilePath("celle_vuote");
            using (var wb = new XLWorkbook())
            {
                var ws = wb.AddWorksheet("Verifiche");
                ws.Cell(1, 1).Value = "TRENO";
                ws.Cell(1, 2).Value = "LOCO";
                ws.Cell(1, 3).Value = "AVARIA";
                ws.Cell(2, 1).Value = 21;
                // LOCO mancante di proposito
                ws.Cell(2, 3).Value = "Avaria senza loco";
                ws.Cell(3, 2).Value = 777;
                // TRENO e AVARIA mancanti
                wb.SaveAs(path);
            }

            AssertSaxMatchesClosedXml(path);
            var rows = ReadWithSax(path);
            Assert.Equal(2, rows.Count);
            Assert.Equal("", rows[0].Loco);
            Assert.Equal("", rows[1].Treno);
        }

        [Fact]
        public void RigheCompletamenteVuoteNonVengonoRestituite()
        {
            string path = NewFilePath("riga_tutta_vuota");
            using (var wb = new XLWorkbook())
            {
                var ws = wb.AddWorksheet("Verifiche");
                ws.Cell(1, 1).Value = "TRENO";
                ws.Cell(1, 2).Value = "LOCO";
                ws.Cell(1, 3).Value = "AVARIA";
                ws.Cell(2, 1).Value = 37;
                ws.Cell(2, 2).Value = 300;
                ws.Cell(2, 3).Value = "Unica riga valida";
                // Una riga con solo spazi: IsNullOrWhiteSpace la scarta in entrambe le implementazioni.
                ws.Cell(3, 1).Value = "   ";
                wb.SaveAs(path);
            }

            AssertSaxMatchesClosedXml(path);
            Assert.Single(ReadWithSax(path));
        }

        [Fact]
        public void NessunaIntestazioneTreno_RipiegaSullaPrimaRigaUsataInEntrambiIPercorsi()
        {
            string path = NewFilePath("senza_header");
            using (var wb = new XLWorkbook())
            {
                var ws = wb.AddWorksheet("Verifiche");
                ws.Cell(1, 1).Value = "Colonna A";
                ws.Cell(1, 2).Value = "Colonna B";
                ws.Cell(1, 3).Value = "Colonna C";
                ws.Cell(2, 1).Value = "dato1";
                ws.Cell(2, 2).Value = "dato2";
                ws.Cell(2, 3).Value = "dato3";
                wb.SaveAs(path);
            }

            AssertSaxMatchesClosedXml(path);
        }

        [Fact]
        public void FoglioConSolaIntestazione_NessunaRigaDati()
        {
            string path = NewFilePath("solo_header");
            using (var wb = new XLWorkbook())
            {
                var ws = wb.AddWorksheet("Verifiche");
                ws.Cell(1, 1).Value = "TRENO";
                ws.Cell(1, 2).Value = "LOCO";
                ws.Cell(1, 3).Value = "AVARIA";
                wb.SaveAs(path);
            }

            AssertSaxMatchesClosedXml(path);
            Assert.Empty(ReadWithSax(path));
        }

        [Fact]
        public void TestoLungoConACapo_PreservatoIdenticamente()
        {
            string path = NewFilePath("testo_lungo");
            string avaria = "Riga uno.\nRiga due con dettagli.\nRiga tre conclusiva.";
            using (var wb = new XLWorkbook())
            {
                var ws = wb.AddWorksheet("Verifiche");
                ws.Cell(1, 1).Value = "TRENO";
                ws.Cell(1, 2).Value = "LOCO";
                ws.Cell(1, 3).Value = "AVARIA";
                ws.Cell(2, 1).Value = 41;
                ws.Cell(2, 2).Value = 655;
                ws.Cell(2, 3).Value = avaria;
                wb.SaveAs(path);
            }

            AssertSaxMatchesClosedXml(path);
            Assert.Equal(avaria, ReadWithSax(path)[0].Avaria);
        }

        [Fact]
        public void PiuFogli_VieneLettoIlPrimoComeConClosedXml()
        {
            string path = NewFilePath("piu_fogli");
            using (var wb = new XLWorkbook())
            {
                var ws1 = wb.AddWorksheet("Verifiche");
                ws1.Cell(1, 1).Value = "TRENO";
                ws1.Cell(1, 2).Value = "LOCO";
                ws1.Cell(1, 3).Value = "AVARIA";
                ws1.Cell(2, 1).Value = 41;
                ws1.Cell(2, 2).Value = 655;
                ws1.Cell(2, 3).Value = "Dal primo foglio";

                var ws2 = wb.AddWorksheet("Altro");
                ws2.Cell(1, 1).Value = "TRENO";
                ws2.Cell(1, 2).Value = "LOCO";
                ws2.Cell(1, 3).Value = "AVARIA";
                ws2.Cell(2, 1).Value = 99;
                ws2.Cell(2, 2).Value = 999;
                ws2.Cell(2, 3).Value = "Dal secondo foglio: NON deve comparire";

                wb.SaveAs(path);
            }

            AssertSaxMatchesClosedXml(path);
            var rows = ReadWithSax(path);
            Assert.Single(rows);
            Assert.Equal("Dal primo foglio", rows[0].Avaria);
        }

        [Fact]
        public void ValoriRipetuti_RisoltiCorrettamenteDallaTabellaStringheCondivise()
        {
            // Excel deduplica le stringhe ripetute nella SharedStringTable: un errore di
            // risoluzione degli indici si manifesta proprio qui, con valori scambiati fra righe.
            string path = NewFilePath("shared_strings");
            using (var wb = new XLWorkbook())
            {
                var ws = wb.AddWorksheet("Verifiche");
                ws.Cell(1, 1).Value = "TRENO";
                ws.Cell(1, 2).Value = "LOCO";
                ws.Cell(1, 3).Value = "AVARIA";
                for (int i = 0; i < 10; i++)
                {
                    ws.Cell(2 + i, 1).Value = "Y0" + (i % 3);
                    ws.Cell(2 + i, 2).Value = 600 + (i % 4);
                    ws.Cell(2 + i, 3).Value = i % 2 == 0 ? "Avaria ricorrente A" : "Avaria ricorrente B";
                }
                wb.SaveAs(path);
            }

            AssertSaxMatchesClosedXml(path);
            var rows = ReadWithSax(path);
            Assert.Equal(10, rows.Count);
            Assert.Equal("Avaria ricorrente A", rows[0].Avaria);
            Assert.Equal("Avaria ricorrente B", rows[1].Avaria);
        }

        [Fact]
        public void FoglioAmpio_SaxEquivalenteAClosedXml()
        {
            // Il caso che motiva l'intervento: molte righe. Verifica che l'equivalenza regga anche
            // quando il file supera la manciata di righe dei test precedenti.
            string path = NewFilePath("ampio");
            using (var wb = new XLWorkbook())
            {
                var ws = wb.AddWorksheet("Verifiche");
                ws.Cell(1, 1).Value = "TRENO";
                ws.Cell(1, 2).Value = "LOCO";
                ws.Cell(1, 3).Value = "Avaria segnalata da ING/SVI";
                for (int i = 0; i < 500; i++)
                {
                    ws.Cell(2 + i, 1).Value = 30 + (i % 20);
                    ws.Cell(2 + i, 2).Value = 600 + i;
                    ws.Cell(2 + i, 3).Value = $"Avaria numero {i} con descrizione di lunghezza ragionevole.";
                }
                wb.SaveAs(path);
            }

            AssertSaxMatchesClosedXml(path);
            Assert.Equal(500, ReadWithSax(path).Count);
        }

        [Theory]
        [InlineData("A1", 1)]
        [InlineData("B2", 2)]
        [InlineData("Z100", 26)]
        [InlineData("AA1", 27)]
        [InlineData("AB1", 28)]
        [InlineData("BC12", 55)]
        [InlineData(null, 0)]
        [InlineData("", 0)]
        [InlineData("123", 0)]
        public void GetColumnNumber_ConvertePosizioniDiCella(string? cellReference, int expected) =>
            Assert.Equal(expected, VerificheExcelReader.GetColumnNumber(cellReference));
    }
}
