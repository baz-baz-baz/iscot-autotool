using System;
using System.IO;
using System.Linq;
using ClosedXML.Excel;
using PersonalAutomationTool.Modules.Verifiche;
using Xunit;

namespace PersonalAutomationTool.Tests.Modules.Verifiche
{
    /// <summary>
    /// Tier 2 su file <c>.xlsx</c> veri: l'azione "Verifica Eseguita" end-to-end — copia di backup,
    /// riga archiviata nel foglio storico, riga rimossa dal foglio principale con shift verso l'alto,
    /// file rinominato.
    ///
    /// <para>
    /// <b>I file di prova replicano la struttura reale</b> osservata nei tre workbook forniti dal
    /// committente: titolo in riga 1, intestazioni in riga 2, dati dalla riga 3, e nel foglio storico
    /// alcune <b>righe vuote ma già formattate</b> prima dei record veri — la particolarità che rende
    /// "prima riga libera" ambigua e che ha richiesto una decisione esplicita (si appende dopo
    /// l'ultima riga con dati).
    /// </para>
    /// </summary>
    public sealed class VerificheArchivioServiceTests : IDisposable
    {
        private readonly DirectoryInfo _root = Directory.CreateTempSubdirectory("VerificheArchivio_");

        public void Dispose()
        {
            try { _root.Delete(recursive: true); } catch { /* pulizia best-effort */ }
        }

        private string CartellaPrincipale
        {
            get
            {
                string p = Path.Combine(_root.FullName, "principale");
                Directory.CreateDirectory(p);
                return p;
            }
        }

        private string CartellaOld
        {
            get
            {
                string p = Path.Combine(_root.FullName, "principale", "OLD");
                Directory.CreateDirectory(p);
                return p;
            }
        }

        /// <summary>
        /// Costruisce un workbook con la struttura dei file reali.
        /// Foglio 1 (principale): r1 titolo, r2 intestazioni, r3.. dati.
        /// Foglio storico: r1 titolo, r2 intestazioni, righe vuote formattate, poi i record.
        /// </summary>
        private string CreaFile(string nome, string nomeFoglioStorico, int righeVuoteNelloStorico = 12)
        {
            string percorso = Path.Combine(CartellaPrincipale, nome);

            using var wb = new XLWorkbook();
            var principale = wb.Worksheets.Add("verifiche");
            principale.Cell(1, 1).Value = "VERIFICHE ETR700";
            principale.Cell(2, 1).Value = "TRENO";
            principale.Cell(2, 2).Value = "LOCO";
            principale.Cell(2, 3).Value = "Avaria segnalata da ING/SVI";
            principale.Cell(2, 4).Value = "Data comunicazione";

            principale.Cell(3, 1).Value = "10";
            principale.Cell(3, 2).Value = "110";
            principale.Cell(3, 3).Value = "Sostituzione scheda CPUN R";
            principale.Cell(3, 4).Value = 46258;

            principale.Cell(4, 1).Value = "9";
            principale.Cell(4, 2).Value = "809";
            principale.Cell(4, 3).Value = "Verifica DMI di banco";
            principale.Cell(4, 4).Value = 46259;

            principale.Cell(5, 1).Value = "16";
            principale.Cell(5, 2).Value = "116";
            principale.Cell(5, 3).Value = "Terza richiesta";
            principale.Cell(5, 4).Value = 46260;

            var storico = wb.Worksheets.Add(nomeFoglioStorico);
            storico.Cell(1, 1).Value = nomeFoglioStorico;
            storico.Cell(2, 1).Value = "TRENO";
            storico.Cell(2, 2).Value = "LOCO";
            storico.Cell(2, 3).Value = "note da tenere presente";
            storico.Cell(2, 4).Value = "Data comunicazione";
            storico.Cell(2, 5).Value = "Data richiesta AB";
            storico.Cell(2, 6).Value = "Tecnico";
            storico.Cell(2, 7).Value = "NOTE";
            storico.Cell(2, 8).Value = "Data verifica";

            // Righe vuote ma formattate, come nel file reale (giallo).
            for (int r = 3; r < 3 + righeVuoteNelloStorico; r++)
            {
                for (int c = 1; c <= 8; c++) storico.Cell(r, c).Style.Fill.BackgroundColor = XLColor.Yellow;
            }

            int primaRigaDati = 3 + righeVuoteNelloStorico;
            storico.Cell(primaRigaDati, 1).Value = "2";
            storico.Cell(primaRigaDati, 2).Value = "802";
            storico.Cell(primaRigaDati, 3).Value = "Record storico preesistente";
            storico.Cell(primaRigaDati, 4).Value = 46225;
            storico.Cell(primaRigaDati, 8).Value = 46232;

            wb.SaveAs(percorso);
            return percorso;
        }

        private VerifichePercorsiRisolti Percorsi(string prefisso = "Verifiche ETR700") =>
            new(CartellaPrincipale, CartellaOld, prefisso);

        /// <summary>
        /// Riga selezionata a video. Treno/Loco sono i valori <b>mostrati</b>, SourceTreno/SourceLoco
        /// quelli <b>grezzi</b> del foglio: qui coincidono, ma non è così per la flotta 1000 — vedi
        /// <see cref="Archivia_Etr1000_TrenoNormalizzatoAVideo_NonBloccaLArchiviazione"/>.
        /// </summary>
        private static VerificheModel Riga(string file, int rigaExcel, string treno, string loco) => new()
        {
            Treno = treno,
            Loco = loco,
            SourceTreno = treno,
            SourceLoco = loco,
            Avaria = "irrilevante per l'identificazione",
            SourceFilePath = file,
            SourceRowNumber = rigaExcel,
            FleetIdentifier = "700"
        };

        // ------------------------------------------------------------------

        [Fact]
        public void Archivia_CreaLaCopiaDiSicurezzaNellaCartellaOld()
        {
            string file = CreaFile("Verifiche ETR700 240826 17_30 Ruffini.xlsx", "STORICO 2026");

            var esito = VerificheArchivioService.Archivia(
                Riga(file, 3, "10", "110"), Percorsi(), "Rossi", new DateTime(2026, 8, 24, 21, 36, 0));

            Assert.True(esito.Riuscita, esito.Messaggio);
            Assert.NotNull(esito.PercorsoBackup);
            Assert.True(File.Exists(esito.PercorsoBackup));
        }

        [Fact]
        public void Archivia_RinominaIlFileConDataOraECognome()
        {
            string file = CreaFile("Verifiche ETR700 240826 17_30 Ruffini.xlsx", "STORICO 2026");

            var esito = VerificheArchivioService.Archivia(
                Riga(file, 3, "10", "110"), Percorsi(), "Rossi", new DateTime(2026, 8, 24, 21, 36, 0));

            Assert.True(esito.Riuscita, esito.Messaggio);
            Assert.Equal("Verifiche ETR700 240826 21_36 Rossi.xlsx", Path.GetFileName(esito.NuovoPercorso));
            Assert.True(File.Exists(esito.NuovoPercorso));
            Assert.False(File.Exists(file));  // il nome precedente non deve restare
        }

        [Fact]
        public void Archivia_RimuoveLaRigaDalFoglioPrincipaleEShiftaVersoLAlto()
        {
            string file = CreaFile("Verifiche ETR700 240826 17_30 Ruffini.xlsx", "STORICO 2026");

            // Si archivia la PRIMA riga dati (riga 3): le due successive devono risalire.
            var esito = VerificheArchivioService.Archivia(
                Riga(file, 3, "10", "110"), Percorsi(), "Rossi", new DateTime(2026, 8, 24, 21, 36, 0));

            Assert.True(esito.Riuscita, esito.Messaggio);

            using var wb = new XLWorkbook(esito.NuovoPercorso);
            var principale = wb.Worksheet(1);

            Assert.Equal("9", principale.Cell(3, 1).GetString());     // era in riga 4
            Assert.Equal("809", principale.Cell(3, 2).GetString());
            Assert.Equal("16", principale.Cell(4, 1).GetString());    // era in riga 5
            Assert.True(principale.Cell(5, 1).IsEmpty());             // nessun buco in fondo
        }

        [Fact]
        public void Archivia_AppendeDopoLUltimaRigaConDatiDelloStorico()
        {
            // Le righe 3-14 dello storico sono vuote ma formattate e il record preesistente sta in
            // riga 15: il nuovo deve finire in riga 16, non riempire i buchi sopra.
            string file = CreaFile("Verifiche ETR700 240826 17_30 Ruffini.xlsx", "STORICO 2026", righeVuoteNelloStorico: 12);

            var esito = VerificheArchivioService.Archivia(
                Riga(file, 3, "10", "110"), Percorsi(), "Rossi", new DateTime(2026, 8, 24, 21, 36, 0));

            Assert.True(esito.Riuscita, esito.Messaggio);

            using var wb = new XLWorkbook(esito.NuovoPercorso);
            var storico = wb.Worksheet("STORICO 2026");

            Assert.Equal("2", storico.Cell(15, 1).GetString());     // record preesistente intatto
            Assert.Equal("10", storico.Cell(16, 1).GetString());    // nuovo record subito sotto
            Assert.Equal("110", storico.Cell(16, 2).GetString());
            Assert.Equal("Sostituzione scheda CPUN R", storico.Cell(16, 3).GetString());
            Assert.True(storico.Cell(3, 1).IsEmpty());              // i buchi restano buchi
        }

        [Fact]
        public void Archivia_ScriveLaDataDiChiusuraOdiernaNellaColonnaGiusta()
        {
            string file = CreaFile("Verifiche ETR700 240826 17_30 Ruffini.xlsx", "STORICO 2026");
            var momento = new DateTime(2026, 8, 24, 21, 36, 0);

            var esito = VerificheArchivioService.Archivia(
                Riga(file, 3, "10", "110"), Percorsi(), "Rossi", momento);

            Assert.True(esito.Riuscita, esito.Messaggio);

            using var wb = new XLWorkbook(esito.NuovoPercorso);
            // Colonna H = "Data verifica" nel foglio storico.
            double seriale = wb.Worksheet("STORICO 2026").Cell(16, 8).GetDouble();

            Assert.Equal(VerificheArchivioService.ASerialeExcel(momento), (int)seriale);
        }

        [Fact]
        public void Archivia_LaDataDiChiusuraEreditaIlFormatoDalleRigheGiaArchiviate()
        {
            // Regressione trovata provando il servizio sul file ETR1000 reale: la riga vuota
            // pre-formattata dello storico aveva, sulla colonna della data, un formato "generale".
            // Conservandolo, nello storico compariva il seriale grezzo "46258" invece di
            // "24/08/2026". Lo stile va preso dalle righe già archiviate, non da quella vuota.
            string percorso = Path.Combine(CartellaPrincipale, "Verifiche ETR700 240826 17_30 Ruffini.xlsx");

            using (var wb = new XLWorkbook())
            {
                var principale = wb.Worksheets.Add("verifiche");
                principale.Cell(2, 1).Value = "TRENO";
                principale.Cell(2, 2).Value = "LOCO";
                principale.Cell(3, 1).Value = "10";
                principale.Cell(3, 2).Value = "110";
                principale.Cell(3, 3).Value = "Avaria";

                var storico = wb.Worksheets.Add("STORICO 2026");
                storico.Cell(2, 8).Value = "Data verifica";

                // Record già archiviato: porta il formato data corretto.
                storico.Cell(3, 1).Value = "2";
                storico.Cell(3, 8).Value = 46232;
                storico.Cell(3, 8).Style.NumberFormat.Format = "dd/mm/yy";

                // Riga vuota successiva, formattata ma in formato generale: è la trappola.
                storico.Cell(4, 8).Style.NumberFormat.NumberFormatId = 0;
                storico.Cell(4, 1).Style.Fill.BackgroundColor = XLColor.Yellow;

                wb.SaveAs(percorso);
            }

            var esito = VerificheArchivioService.Archivia(
                Riga(percorso, 3, "10", "110"), Percorsi(), "Rossi", new DateTime(2026, 8, 24, 21, 36, 0));
            Assert.True(esito.Riuscita, esito.Messaggio);

            using var risultato = new XLWorkbook(esito.NuovoPercorso);
            var cella = risultato.Worksheet("STORICO 2026").Cell(4, 8);

            Assert.Equal("dd/mm/yy", cella.Style.NumberFormat.Format);

            // Con il formato data applicato la cella si legge come data, non come numero grezzo:
            // è esattamente la differenza fra vedere "24/08/2026" e vedere "46258" nello storico.
            Assert.Equal(new DateTime(2026, 8, 24), cella.GetDateTime());
        }

        [Fact]
        public void Archivia_NonCompilaLaColonnaTecnico()
        {
            // Scelta esplicita del committente: il cognome resta solo nel nome del file.
            string file = CreaFile("Verifiche ETR700 240826 17_30 Ruffini.xlsx", "STORICO 2026");

            var esito = VerificheArchivioService.Archivia(
                Riga(file, 3, "10", "110"), Percorsi(), "Rossi", new DateTime(2026, 8, 24, 21, 36, 0));

            Assert.True(esito.Riuscita, esito.Messaggio);

            using var wb = new XLWorkbook(esito.NuovoPercorso);
            Assert.True(wb.Worksheet("STORICO 2026").Cell(16, 6).IsEmpty());   // F = Tecnico
        }

        [Fact]
        public void Archivia_FunzionaAncheConLoStoricoMultiAnnoDiEtr1000()
        {
            string file = CreaFile("Verifiche ETR1000 240826 10_13 Del Prete.xlsx", "STORICO 22-24-25-26");

            var esito = VerificheArchivioService.Archivia(
                Riga(file, 3, "10", "110"), Percorsi("Verifiche ETR1000"), "Del Prete", new DateTime(2026, 8, 24, 10, 13, 0));

            Assert.True(esito.Riuscita, esito.Messaggio);
            Assert.Equal("Verifiche ETR1000 240826 10_13 Del Prete.xlsx", Path.GetFileName(esito.NuovoPercorso));
        }

        [Fact]
        public void Archivia_SenzaFoglioStoricoPerLAnnoCorrente_SiFermaSenzaModificare()
        {
            string file = CreaFile("Verifiche ETR700 240826 17_30 Ruffini.xlsx", "STORICO 2019");
            var primaScrittura = File.GetLastWriteTimeUtc(file);

            var esito = VerificheArchivioService.Archivia(
                Riga(file, 3, "10", "110"), Percorsi(), "Rossi", new DateTime(2026, 8, 24, 21, 36, 0));

            Assert.False(esito.Riuscita);
            Assert.Contains("2026", esito.Messaggio);
            Assert.True(File.Exists(file));                                   // non rinominato
            Assert.Equal(primaScrittura, File.GetLastWriteTimeUtc(file));     // non modificato
        }

        [Fact]
        public void Archivia_SeLaRigaNonCorrispondePiu_SiFermaSenzaModificare()
        {
            // Simula un collega che ha cambiato il file fra la lettura a video e il click.
            string file = CreaFile("Verifiche ETR700 240826 17_30 Ruffini.xlsx", "STORICO 2026");

            var esito = VerificheArchivioService.Archivia(
                Riga(file, 3, "99", "999"), Percorsi(), "Rossi", new DateTime(2026, 8, 24, 21, 36, 0));

            Assert.False(esito.Riuscita);
            Assert.Contains("non corrisponde", esito.Messaggio);
            Assert.True(File.Exists(file));
        }

        [Fact]
        public void Archivia_RigaSenzaOrigineTracciata_VieneRifiutata()
        {
            var senzaOrigine = new VerificheModel { Treno = "10", Loco = "110", FleetIdentifier = "700" };

            var esito = VerificheArchivioService.Archivia(
                senzaOrigine, Percorsi(), "Rossi", new DateTime(2026, 8, 24, 21, 36, 0));

            Assert.False(esito.Riuscita);
            Assert.Contains("riga di origine", esito.Messaggio);
        }

        [Fact]
        public void Archivia_FileInesistente_MessaggioChiaroSenzaEccezione()
        {
            var riga = Riga(Path.Combine(CartellaPrincipale, "sparito.xlsx"), 3, "10", "110");

            var esito = VerificheArchivioService.Archivia(
                riga, Percorsi(), "Rossi", new DateTime(2026, 8, 24, 21, 36, 0));

            Assert.False(esito.Riuscita);
            Assert.Contains("non esiste più", esito.Messaggio);
        }

        [Fact]
        public void Archivia_DueVolteDiSeguito_NonSovrascriveIlBackupPrecedente()
        {
            string file = CreaFile("Verifiche ETR700 240826 17_30 Ruffini.xlsx", "STORICO 2026");

            var primo = VerificheArchivioService.Archivia(
                Riga(file, 3, "10", "110"), Percorsi(), "Rossi", new DateTime(2026, 8, 24, 21, 36, 0));
            Assert.True(primo.Riuscita, primo.Messaggio);

            // Seconda archiviazione sul file appena rinominato: ora la prima riga dati è "9"/"809".
            var secondo = VerificheArchivioService.Archivia(
                Riga(primo.NuovoPercorso!, 3, "9", "809"), Percorsi(), "Rossi", new DateTime(2026, 8, 24, 21, 40, 0));
            Assert.True(secondo.Riuscita, secondo.Messaggio);

            Assert.Equal(2, Directory.GetFiles(CartellaOld, "*.xlsx").Length);
        }

        [Fact]
        public void Archivia_IlFileRestaLeggibileDalModuloVerifiche()
        {
            // Regressione strutturale: dopo la modifica il workbook deve restare un pacchetto valido
            // che il lettore SAX del modulo sa ancora aprire, altrimenti la tabella si svuota.
            string file = CreaFile("Verifiche ETR700 240826 17_30 Ruffini.xlsx", "STORICO 2026");

            var esito = VerificheArchivioService.Archivia(
                Riga(file, 3, "10", "110"), Percorsi(), "Rossi", new DateTime(2026, 8, 24, 21, 36, 0));
            Assert.True(esito.Riuscita, esito.Messaggio);

            using var wb = new XLWorkbook(esito.NuovoPercorso);
            var righe = wb.Worksheet(1).RowsUsed().ToList();

            Assert.Equal(4, righe.Count);   // titolo + intestazioni + 2 righe dati rimaste
        }


        /// <summary>
        /// Regressione del difetto segnalato dal committente: su ETR1000 ogni archiviazione veniva
        /// rifiutata con "La riga 3 non corrisponde più alla verifica selezionata — atteso treno '31',
        /// trovato 'ETR1000'".
        ///
        /// <para>
        /// Causa: per la flotta 1000 il ViewModel sostituisce il generico "ETR1000" letto dal foglio
        /// con il numero di treno reale risolto dal database tramite la loco. La guardia confrontava
        /// quel valore <b>normalizzato per la UI</b> con il contenuto grezzo della cella, e non
        /// potevano che essere diversi. Ora il confronto avviene fra i valori grezzi conservati in
        /// SourceTreno/SourceLoco e quelli riletti dal file.
        /// </para>
        /// </summary>
        [Fact]
        public void Archivia_Etr1000_TrenoNormalizzatoAVideo_NonBloccaLArchiviazione()
        {
            string percorso = Path.Combine(CartellaPrincipale, "Verifiche ETR1000 240826 10_13 Del Prete.xlsx");
            using (var wb = new XLWorkbook())
            {
                var principale = wb.Worksheets.Add("verifiche");
                principale.Cell(2, 1).Value = "TRENO";
                principale.Cell(2, 2).Value = "LOCO";
                principale.Cell(3, 1).Value = "ETR1000";      // valore grezzo nel foglio
                principale.Cell(3, 2).Value = "831";
                principale.Cell(3, 3).Value = "Sostituire piastra pneumatica superiore";

                var storico = wb.Worksheets.Add("STORICO 22-24-25-26");
                storico.Cell(2, 8).Value = "Data chiusura Verifica";
                storico.Cell(3, 1).Value = "45";
                storico.Cell(3, 8).Value = 44604;
                wb.SaveAs(percorso);
            }

            var riga = new VerificheModel
            {
                Treno = "31",              // ciò che il tecnico vede, risolto dal database
                Loco = "831",
                SourceTreno = "ETR1000",   // ciò che è scritto nella cella
                SourceLoco = "831",
                SourceFilePath = percorso,
                SourceRowNumber = 3,
                FleetIdentifier = "1000"
            };

            var esito = VerificheArchivioService.Archivia(
                riga, Percorsi("Verifiche ETR1000"), "Del Prete", new DateTime(2026, 8, 24, 10, 13, 0));

            Assert.True(esito.Riuscita, esito.Messaggio);
            Assert.DoesNotContain("non corrisponde", esito.Messaggio);

            using var risultato = new XLWorkbook(esito.NuovoPercorso);
            Assert.Equal("ETR1000", risultato.Worksheet("STORICO 22-24-25-26").Cell(4, 1).GetString());
            Assert.True(risultato.Worksheet(1).Cell(3, 1).IsEmpty());   // riga rimossa dal principale
        }

        [Fact]
        public void Archivia_LaGuardiaNonAccettaUnaCorrispondenzaParziale()
        {
            // Con il confronto per suffisso che c'era prima, il treno "1" avrebbe combaciato con il
            // "31" presente nella cella: la guardia avrebbe lasciato archiviare la riga sbagliata.
            string percorso = Path.Combine(CartellaPrincipale, "Verifiche ETR700 240826 17_30 Ruffini.xlsx");
            using (var wb = new XLWorkbook())
            {
                var principale = wb.Worksheets.Add("verifiche");
                principale.Cell(2, 1).Value = "TRENO";
                principale.Cell(2, 2).Value = "LOCO";
                principale.Cell(3, 1).Value = "31";
                principale.Cell(3, 2).Value = "831";
                var storico = wb.Worksheets.Add("STORICO 2026");
                storico.Cell(2, 8).Value = "Data verifica";
                storico.Cell(3, 1).Value = "2";
                wb.SaveAs(percorso);
            }

            var esito = VerificheArchivioService.Archivia(
                Riga(percorso, 3, "1", "831"), Percorsi(), "Rossi", new DateTime(2026, 8, 24, 21, 36, 0));

            Assert.False(esito.Riuscita);
            Assert.Contains("non corrisponde", esito.Messaggio);
        }

        /// <summary>
        /// Regressione del difetto segnalato dal committente con uno screenshot dello storico
        /// ETR1000: la riga archiviata compariva <b>bianca e senza bordi</b>, mentre quella sopra era
        /// grigia e bordata, e la data comunicazione mostrava il seriale grezzo.
        ///
        /// <para>
        /// Erano due difetti insieme. Lo stile veniva preso dalla riga <i>preesistente vuota</i>
        /// invece che dall'ultima archiviata; e soprattutto veniva applicato alle sole colonne che il
        /// servizio scriveva (A-D più la data): le colonne intermedie non venivano nemmeno create, e
        /// da E in poi la riga restava priva di sfondo e bordi. Ora si veste l'intera riga sul
        /// modello di quella precedente, colonne vuote comprese.
        /// </para>
        /// </summary>
        [Fact]
        public void Archivia_LaRigaArchiviataHaLoStessoAspettoDiQuellaSopra_SuTutteLeColonne()
        {
            string percorso = Path.Combine(CartellaPrincipale, "Verifiche ETR1000 240826 10_13 Del Prete.xlsx");

            using (var wb = new XLWorkbook())
            {
                var principale = wb.Worksheets.Add("verifiche");
                principale.Cell(2, 1).Value = "TRENO";
                principale.Cell(2, 2).Value = "LOCO";
                principale.Cell(3, 1).Value = "ETR1000";
                principale.Cell(3, 2).Value = "831";
                principale.Cell(3, 3).Value = "Sostituire piastra pneumatica superiore";
                principale.Cell(3, 4).Value = 46202;

                var storico = wb.Worksheets.Add("STORICO 22-24-25-26");
                storico.Cell(2, 8).Value = "Data chiusura Verifica";

                // Riga già archiviata: grigia, bordata, font dedicato su TUTTE le 8 colonne —
                // comprese quelle che restano vuote (5 "Data richiesta", 6 "Tecnico", 7 "NOTE").
                for (int c = 1; c <= 8; c++)
                {
                    var cella = storico.Cell(3, c);
                    cella.Style.Fill.BackgroundColor = XLColor.FromHtml("#A6A6A6");
                    cella.Style.Border.BottomBorder = XLBorderStyleValues.Thin;
                    cella.Style.Border.TopBorder = XLBorderStyleValues.Thin;
                    cella.Style.Font.FontName = "Calibri";
                    cella.Style.Font.Bold = true;
                }
                storico.Cell(3, 1).Value = "45";
                storico.Cell(3, 2).Value = "145";
                storico.Cell(3, 4).Value = 44600;
                storico.Cell(3, 8).Value = 44604;
                storico.Cell(3, 8).Style.NumberFormat.Format = "dd/mm/yy";

                wb.SaveAs(percorso);
            }

            var riga = new VerificheModel
            {
                Treno = "31", Loco = "831", SourceTreno = "ETR1000", SourceLoco = "831",
                SourceFilePath = percorso, SourceRowNumber = 3, FleetIdentifier = "1000"
            };

            var esito = VerificheArchivioService.Archivia(
                riga, Percorsi("Verifiche ETR1000"), "Rossi", new DateTime(2026, 8, 24, 21, 36, 0));
            Assert.True(esito.Riuscita, esito.Messaggio);

            using var risultato = new XLWorkbook(esito.NuovoPercorso);
            var foglio = risultato.Worksheet("STORICO 22-24-25-26");
            var sopra = foglio.Row(3);
            var nuova = foglio.Row(4);

            for (int c = 1; c <= 8; c++)
            {
                Assert.Equal(sopra.Cell(c).Style.Fill.BackgroundColor, nuova.Cell(c).Style.Fill.BackgroundColor);
                Assert.Equal(sopra.Cell(c).Style.Border.BottomBorder, nuova.Cell(c).Style.Border.BottomBorder);
                Assert.Equal(sopra.Cell(c).Style.Font.FontName, nuova.Cell(c).Style.Font.FontName);
                Assert.Equal(sopra.Cell(c).Style.Font.Bold, nuova.Cell(c).Style.Font.Bold);
            }

            // La data comunicazione copiata non deve restare un numero grezzo.
            Assert.Equal(sopra.Cell(4).Style.NumberFormat.Format, nuova.Cell(4).Style.NumberFormat.Format);
            Assert.Equal(new DateTime(2026, 8, 24), nuova.Cell(8).GetDateTime());
        }
        // ------------------------------------------------------------------
        // Utilità di conversione
        // ------------------------------------------------------------------

        /// <summary>
        /// I valori attesi sono presi dai file reali: nel foglio ETR700 la cella "Data comunicazione"
        /// del 24-08-2026 vale <c>46258</c>.
        ///
        /// <para>
        /// Non è coperto gennaio-febbraio 1900, dove l'epoca 30/12/1899 e i seriali di Excel
        /// divergono di un giorno a causa del noto bug dell'anno bisestile 1900. È deliberato: la
        /// conversione serve per date di lavoro, e allinearla su quel caso limite la
        /// disallineerebbe su tutte le date moderne.
        /// </para>
        /// </summary>
        [Theory]
        [InlineData(24, 8, 2026, 46258)]
        [InlineData(1, 3, 1900, 61)]
        [InlineData(31, 12, 2025, 46022)]
        public void ASerialeExcel_ConvertecorrettamenteLeDate(int giorno, int mese, int anno, int atteso) =>
            Assert.Equal(atteso, VerificheArchivioService.ASerialeExcel(new DateTime(anno, mese, giorno)));

        [Theory]
        [InlineData("A1", 1)]
        [InlineData("D15", 4)]
        [InlineData("H1833", 8)]
        [InlineData("AA3", 27)]
        public void NumeroColonna_LeggeLaParteAlfabetica(string riferimento, int atteso) =>
            Assert.Equal(atteso, VerificheArchivioService.NumeroColonna(riferimento));

        [Theory]
        [InlineData(1, "A")]
        [InlineData(4, "D")]
        [InlineData(8, "H")]
        [InlineData(27, "AA")]
        public void LettereColonna_EInversaDiNumeroColonna(int colonna, string atteso)
        {
            Assert.Equal(atteso, VerificheArchivioService.LettereColonna(colonna));
            Assert.Equal(colonna, VerificheArchivioService.NumeroColonna(atteso + "1"));
        }
    }
}
