using System.Collections.Generic;
using PersonalAutomationTool.Core.Naming;
using Xunit;

namespace PersonalAutomationTool.Tests.Core.Naming
{
    /// <summary>
    /// Tier 1 (funzioni pure, nessun file system, nessun I/O): copre <see cref="LogDumpFolderName"/>
    /// con nomi di cartella reali (per struttura) presi dalla convenzione documentata in
    /// PROJECT_MEMORY.md §5.1 e dai valori reali della colonna "tipo" di flotte.db
    /// (E404P, ETR700, ETR1000, ETR1000 I-F, ETR1001FH), più i casi limite/malformati che il
    /// parser originale gestiva (o falliva silenziosamente) in modo specifico.
    /// </summary>
    public class LogDumpFolderNameTests
    {
        /// <summary>
        /// I valori reali della colonna "tipo" di train_software.db, ordinati per lunghezza
        /// decrescente esattamente come fa <c>PdfView.GetTipiFromDbAsync</c>
        /// (<c>ORDER BY LENGTH(tipo) DESC</c>). L'ordine non è un dettaglio: è ciò che permette
        /// di distinguere "ETR1000 I-F" da "ETR1000".
        /// </summary>
        private static readonly string[] RealKnownTypes =
        [
            "ETR1000 I-F", // 11 caratteri
            "ETR1001FH",   // 9
            "ETR1000",     // 7
            "E404P",       // 5
            "ETR700"       // 6 -- volutamente non in ordine stretto fra ETR700/E404P: non ambiguo, nessuno dei due è prefisso dell'altro
        ];

        [Fact]
        public void TryParse_ParsesStandardLogFolder()
        {
            bool success = LogDumpFolderName.TryParse(
                "SR1247654 LOG ETR700 117 04.02HR 300526 Todde",
                RealKnownTypes,
                out var result);

            Assert.True(success);
            Assert.Equal("1247654", result!.Ticket);
            Assert.Equal(LogDumpKind.Log, result.Kind);
            Assert.Equal("ETR700", result.Tipo);
            Assert.Equal("117", result.Loco);
            Assert.Equal("04.02HR", result.Software);
            Assert.Equal("300526", result.Data);
            Assert.Equal("Todde", result.Utente);
        }

        [Fact]
        public void TryParse_ParsesStandardDumpFolder()
        {
            bool success = LogDumpFolderName.TryParse(
                "SR9988776 DUMP E404P 627 04.02HR 300526 Rossi",
                RealKnownTypes,
                out var result);

            Assert.True(success);
            Assert.Equal(LogDumpKind.Dump, result!.Kind);
            Assert.Equal("E404P", result.Tipo);
            Assert.Equal("627", result.Loco);
            Assert.Equal("04.02HR", result.Software);
        }

        [Fact]
        public void TryParse_DistinguishesEtr1000FromEtr1000IF_WhenLocoIsNumeric()
        {
            // Il caso critico che ha motivato questo tipo: "ETR1000" è un prefisso testuale di
            // "ETR1000 I-F", ma qui il token successivo è la loco numerica "205", non "I-F".
            // Il confronto StartsWith(candidate + " ") deve fallire per "ETR1000 I-F " (che
            // richiederebbe letteralmente "I-F" subito dopo) e riuscire solo per "ETR1000 ".
            bool success = LogDumpFolderName.TryParse(
                "SR3344556 LOG ETR1000 205 04.01 HR 210626 Neri",
                RealKnownTypes,
                out var result);

            Assert.True(success);
            Assert.Equal("ETR1000", result!.Tipo);
            Assert.Equal("205", result.Loco);
            // Software su due parole ("04.01 HR", valore reale delle opzioni combo ETR1000/1000FH
            // in ExcelViewModel): deve essere ricomposto interamente, non troncato al primo token.
            Assert.Equal("04.01 HR", result.Software);
            Assert.Equal("Neri", result.Utente);
        }

        [Fact]
        public void TryParse_ParsesTwoWordTipo_EtrIF_WithUnderscoredSoftware()
        {
            bool success = LogDumpFolderName.TryParse(
                "SR4455667 LOG ETR1000 I-F 302 02.02.0004_ELO_BL3 220626 Gialli",
                RealKnownTypes,
                out var result);

            Assert.True(success);
            Assert.Equal("ETR1000 I-F", result!.Tipo);
            Assert.Equal("302", result.Loco);
            Assert.Equal("02.02.0004_ELO_BL3", result.Software);
            Assert.Equal("220626", result.Data);
            Assert.Equal("Gialli", result.Utente);
        }

        [Fact]
        public void TryParse_ParsesEtr1001FH()
        {
            bool success = LogDumpFolderName.TryParse(
                "SR5566778 DUMP ETR1001FH 410 04.03HR 230626 Blu",
                RealKnownTypes,
                out var result);

            Assert.True(success);
            Assert.Equal(LogDumpKind.Dump, result!.Kind);
            Assert.Equal("ETR1001FH", result.Tipo);
            Assert.Equal("410", result.Loco);
        }

        [Fact]
        public void TryParse_UtenteWithMultipleWords_TakesEverythingAfterDate()
        {
            bool success = LogDumpFolderName.TryParse(
                "SR6677889 LOG E404P 601 04.02HR 240626 Mario Rossi",
                RealKnownTypes,
                out var result);

            Assert.True(success);
            Assert.Equal("Mario Rossi", result!.Utente);
        }

        [Fact]
        public void TryParse_HandlesEmptySoftwareCausingDoubleSpace_ProducesEmptySoftwareAndCorrectLoco()
        {
            // Se il campo software è vuoto in CartelleView, l'interpolazione
            // $"...{loco} {software} {data}..." lascia uno spazio doppio nel nome finale
            // (il .Trim() del writer rimuove solo gli spazi ai due estremi della stringa
            // intera, non quelli interni). Questo è un artefatto reale che può comparire su
            // disco, non un caso di laboratorio.
            bool success = LogDumpFolderName.TryParse(
                "SR1247654 LOG ETR700 117  300526 Todde", // doppio spazio fra "117" e "300526"
                RealKnownTypes,
                out var result);

            Assert.True(success);
            Assert.Equal("117", result!.Loco);
            Assert.Equal("", result.Software);
            Assert.Equal("300526", result.Data);
        }

        [Fact]
        public void TryParse_FallsBackToFirstTokenAsTipo_WhenTypeIsUnknown()
        {
            // "XYZ999" non è nell'elenco dei tipi noti: il parser originale ricadeva sul primo
            // token dopo il prefisso, esattamente come qui.
            bool success = LogDumpFolderName.TryParse(
                "SR1234567 LOG XYZ999 205 04.02HR 260626 Utente",
                RealKnownTypes,
                out var result);

            Assert.True(success);
            Assert.Equal("XYZ999", result!.Tipo);
            Assert.Equal("205", result.Loco);
        }

        [Fact]
        public void TryParse_FallsBackToFirstTokenAsTipo_WhenKnownTypesIsEmpty()
        {
            bool success = LogDumpFolderName.TryParse(
                "SR1234567 LOG ETR700 205 04.02HR 260626 Utente",
                [],
                out var result);

            Assert.True(success);
            Assert.Equal("ETR700", result!.Tipo);
            Assert.Equal("205", result.Loco);
        }

        [Fact]
        public void TryParse_FallsBackToFirstTokenAsTipo_WhenNoLocoPresentBetweenTipoAndDate()
        {
            // Cartella malformata: dopo il tipo non c'è alcuna loco prima della data.
            bool success = LogDumpFolderName.TryParse(
                "SR1234567 LOG ETR700 300526 Utente",
                RealKnownTypes,
                out var result);

            Assert.True(success);
            Assert.Equal("ETR700", result!.Tipo);
            Assert.Equal("", result.Loco);
            Assert.Equal("", result.Software);
        }

        [Fact]
        public void TryParse_RequiresCallerToOrderKnownTypesLongestFirst_OtherwiseAmbiguousPrefixWins()
        {
            // Documenta il contratto del parametro knownTypes: se l'ordine è invertito
            // (più corto prima), "ETR1000" vince per primo su "ETR1000 I-F" e la loco reale
            // finisce interpretata come "I-F". Non è un difetto di TryParse: è la stessa
            // ambiguità che il parser originale aveva già, risolta unicamente dall'ordine con cui
            // PdfView.GetTipiFromDbAsync interroga il database (ORDER BY LENGTH(tipo) DESC).
            string[] shortestFirst = ["ETR1000", "ETR1000 I-F"];

            bool success = LogDumpFolderName.TryParse(
                "SR4455667 LOG ETR1000 I-F 302 04.02HR 220626 Gialli",
                shortestFirst,
                out var result);

            Assert.True(success);
            Assert.Equal("ETR1000", result!.Tipo);
            Assert.Equal("I-F", result.Loco); // esito scorretto, atteso: dimostra la landmine
        }

        [Fact]
        public void TryParse_ReturnsFalse_WhenMissingSrPrefix()
        {
            bool success = LogDumpFolderName.TryParse(
                "1234567 LOG ETR700 117 04.02HR 300526 Todde",
                RealKnownTypes,
                out var result);

            Assert.False(success);
            Assert.Null(result);
        }

        [Fact]
        public void TryParse_ReturnsFalse_WhenMissingLogOrDumpToken()
        {
            bool success = LogDumpFolderName.TryParse(
                "SR1234567 ETR700 117 04.02HR 300526 Todde",
                RealKnownTypes,
                out var result);

            Assert.False(success);
        }

        [Fact]
        public void TryParse_ReturnsFalse_WhenKindTokenIsLowercase()
        {
            // Fedeltà all'originale: il parser era (ed è rimasto) case-sensitive su "LOG"/"DUMP".
            // Non è stata aggiunta tolleranza al case non richiesta da questo sprint.
            bool success = LogDumpFolderName.TryParse(
                "SR1234567 log ETR700 117 04.02HR 300526 Todde",
                RealKnownTypes,
                out var result);

            Assert.False(success);
        }

        [Fact]
        public void TryParse_ReturnsFalse_WhenDateIsNotSixDigits()
        {
            bool success = LogDumpFolderName.TryParse(
                "SR1234567 LOG ETR700 117 04.02HR 30052 Todde", // 5 cifre
                RealKnownTypes,
                out var result);

            Assert.False(success);
        }

        [Fact]
        public void TryParse_ReturnsFalse_WhenUtenteIsEmptyAndTrailingSpaceWasTrimmed()
        {
            // Limite preesistente del formato, non introdotto da questo tipo: se l'utente è
            // vuoto, CartelleView.BtnCrea_Click applica .Trim() all'intera stringa interpolata,
            // rimuovendo lo spazio separatore che precederebbe l'utente. Il nome risultante su
            // disco termina esattamente sulla data, senza lo spazio che la grammatica richiede
            // dopo di essa: sia il parser originale (PdfView.ParseLogFolderName) sia questo
            // avrebbero fallito nel riconoscerlo. Candidato per la validazione preventiva (roadmap
            // §6.1, intervento 1.2): avvisare l'utente se lascia vuoto il campo "Utente" in
            // CARTELLE, perché rende la cartella silenziosamente non analizzabile a valle.
            bool success = LogDumpFolderName.TryParse(
                "SR7788990 LOG ETR700 205 04.02HR 250626", // nessuno spazio/utente dopo la data
                RealKnownTypes,
                out var result);

            Assert.False(success);
        }

        [Fact]
        public void TryParse_ReturnsFalse_ForNullOrEmptyInput()
        {
            Assert.False(LogDumpFolderName.TryParse(null, RealKnownTypes, out var r1));
            Assert.Null(r1);
            Assert.False(LogDumpFolderName.TryParse("", RealKnownTypes, out var r2));
            Assert.Null(r2);
        }

        [Fact]
        public void Format_ReproducesOriginalWriterOutput_ForStandardCase()
        {
            var name = new LogDumpFolderName
            {
                Ticket = "1247654",
                Kind = LogDumpKind.Log,
                Tipo = "ETR700",
                Loco = "117",
                Software = "04.02HR",
                Data = "300526",
                Utente = "Todde"
            };

            Assert.Equal("SR1247654 LOG ETR700 117 04.02HR 300526 Todde", name.Format());
            Assert.Equal(name.Format(), name.ToString());
        }

        [Fact]
        public void Format_ThenTryParse_RoundTrips_ForWellFormedNames()
        {
            // Non testato per Utente vuoto: quel caso, per costruzione, non è round-trippabile
            // (vedi TryParse_ReturnsFalse_WhenUtenteIsEmptyAndTrailingSpaceWasTrimmed) — non è
            // un difetto di questo test, è lo stesso limite del formato su disco.
            var original = new LogDumpFolderName
            {
                Ticket = "4455667",
                Kind = LogDumpKind.Dump,
                Tipo = "ETR1000 I-F",
                Loco = "302",
                Software = "02.02.0004_ELO_BL3",
                Data = "220626",
                Utente = "Gialli"
            };

            string formatted = original.Format();
            bool success = LogDumpFolderName.TryParse(formatted, RealKnownTypes, out var reparsed);

            Assert.True(success);
            Assert.Equal(original, reparsed);
        }
    }
}
