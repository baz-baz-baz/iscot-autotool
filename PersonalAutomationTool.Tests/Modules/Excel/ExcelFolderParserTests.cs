using System;
using System.Collections.Generic;
using System.Linq;
using PersonalAutomationTool.Core.Naming;
using PersonalAutomationTool.Modules.Excel;
using Xunit;

namespace PersonalAutomationTool.Tests.Modules.Excel
{
    /// <summary>
    /// Tier 1 (funzioni pure su stringhe, nessun file system): estrazione di ticket e locomotore per
    /// il modulo EXCEL. I quattro nomi di cartella usati qui sono <b>esempi reali forniti dal
    /// committente</b>, non casi di laboratorio — è precisamente ciò che mancava per poter correggere
    /// in sicurezza la scoperta #1 dello Sprint 2 (PROJECT_MEMORY.md §6.1-bis/§6.3), rimasta bloccata
    /// per due sprint.
    /// </summary>
    public sealed class ExcelFolderParserTests
    {
        /// <summary>
        /// I <c>tipo</c> reali di <c>flotte</c>, ordinati per lunghezza decrescente come li produce
        /// <c>FlotteCache.GetDistinctTipiOrderByLengthDesc</c>. Verificati sul database spedito con
        /// l'app: si noti che la flotta Italia-Francia vi è registrata come <c>"ETR1000 I-F"</c>
        /// (con spazio e trattino) mentre sulle cartelle reali compare come <c>ETR1000IF</c>, e che
        /// la variante FH è <c>"ETR1001FH"</c>, non <c>"ETR1000FH"</c>.
        /// </summary>
        private static readonly string[] RealKnownTypes = ["ETR1000 I-F", "ETR1001FH", "ETR1000", "ETR700", "E404P"];

        // I quattro nomi reali forniti dal committente in apertura dello Sprint 3.
        private const string RealLogEtr1000 = "SR1234567 LOG ETR1000 119 02.02CR3 230826 Carlomagno";
        private const string RealDumpEtr1000 = "SR1234568 DUMP ETR1000 119 02.02CR3HR 230826 Carlomagno";
        private const string RealLogEtr1000If = "SR1234567 LOG ETR1000IF 128 BISTANDARD 230826 Carlomagno";
        private const string RealDumpEtr1000If = "SR1234567 DUMP ETR1000IF 128 BISTANDARD 230826 Carlomagno";

        [Theory]
        [InlineData(RealLogEtr1000, "1234567", "119")]
        [InlineData(RealDumpEtr1000, "1234568", "119")]
        [InlineData(RealLogEtr1000If, "1234567", "128")]
        [InlineData(RealDumpEtr1000If, "1234567", "128")]
        public void TryExtractTicketAndLoco_SuNomiReali_EstraeTicketELoco(string folderName, string expectedTicket, string expectedLoco)
        {
            var result = ExcelFolderParser.TryExtractTicketAndLoco(folderName, RealKnownTypes);

            Assert.NotNull(result);
            Assert.Equal(expectedTicket, result!.Ticket);
            Assert.Equal(expectedLoco, result.Loco);
        }

        [Theory]
        [InlineData(RealLogEtr1000, LogDumpKind.Log, "ETR1000")]
        [InlineData(RealDumpEtr1000, LogDumpKind.Dump, "ETR1000")]
        [InlineData(RealLogEtr1000If, LogDumpKind.Log, "ETR1000IF")]
        [InlineData(RealDumpEtr1000If, LogDumpKind.Dump, "ETR1000IF")]
        public void TryParse_SuNomiReali_RiconosceTipoEKind(string folderName, LogDumpKind expectedKind, string expectedTipo)
        {
            // "ETR1000IF" non compare in `flotte` (che registra "ETR1000 I-F"): il riconoscimento
            // avviene per fallback sul primo token, che in questa grammatica è esattamente il tipo.
            Assert.True(LogDumpFolderName.TryParse(folderName, RealKnownTypes, out var parsed));

            Assert.Equal(expectedKind, parsed!.Kind);
            Assert.Equal(expectedTipo, parsed.Tipo);
        }

        [Fact]
        public void TryParse_SuNomeReale_NonConfondeIlSoftwareConLaLoco()
        {
            // "02.02CR3" contiene cifre che i fallback generici (\b\d{2,4}\b) potevano catturare al
            // posto della loco: il parser posizionale le assegna al campo software, dove stanno.
            Assert.True(LogDumpFolderName.TryParse(RealLogEtr1000, RealKnownTypes, out var parsed));

            Assert.Equal("119", parsed!.Loco);
            Assert.Equal("02.02CR3", parsed.Software);
            Assert.Equal("230826", parsed.Data);
            Assert.Equal("Carlomagno", parsed.Utente);
        }

        [Fact]
        public void GetDiskTokens_Etr1000FH_NonIncludeLeFormeItaliaFrancia()
        {
            // Invariante §5.3: l'etichetta "ETR1000 / 1000FH" deve ESCLUDERE le forme I-F, che
            // appartengono all'etichetta separata "ETR1000 I-F" (report Excel diverso, maxCol 24
            // contro 27). Un token "1000IF" qui farebbe scrivere righe nel report sbagliato.
            var tokens = ExcelFolderParser.GetDiskTokens("ETR1000 / 1000FH");

            Assert.Contains("ETR1000", tokens);
            Assert.Contains("ETR1001FH", tokens);
            Assert.DoesNotContain(tokens, t => t.Contains("IF", StringComparison.OrdinalIgnoreCase));
            Assert.DoesNotContain(tokens, t => t.Contains("I-F", StringComparison.OrdinalIgnoreCase));
        }

        [Fact]
        public void GetDiskTokens_Etr1000IF_IncludeLaFormaAttaccataUsataSuDisco()
        {
            var tokens = ExcelFolderParser.GetDiskTokens("ETR1000 I-F");

            Assert.Contains("ETR1000IF", tokens);
            Assert.Contains("ETR1000 I-F", tokens);
        }

        [Fact]
        public void GetDiskTokens_SonoOrdinatiPerLunghezzaDecrescente()
        {
            // L'alternanza di un regex sceglie la prima alternativa che corrisponde, non la più
            // lunga: senza quest'ordine, "ETR1000" catturerebbe il prefisso di "ETR1001FH".
            foreach (var label in new[] { "ETR1000 / 1000FH", "ETR1000 I-F", "E404P", "ETR700" })
            {
                var tokens = ExcelFolderParser.GetDiskTokens(label);
                Assert.Equal(tokens.OrderByDescending(t => t.Length), tokens);
            }
        }

        [Fact]
        public void BuildLocoRegex_Etr1000FH_TrovaLaLocoDoveIlPatternOriginaleFalliva()
        {
            // Il pattern originale interpolava l'etichetta con lo slash e non poteva mai
            // corrispondere: questa è la regressione che il fix elimina.
            var regex = ExcelFolderParser.BuildLocoRegex("ETR1000 / 1000FH", minDigits: 3);

            Assert.NotNull(regex);
            var match = regex!.Match(RealLogEtr1000);
            Assert.True(match.Success);
            Assert.Equal("119", match.Groups[1].Value);
        }

        [Fact]
        public void BuildLocoRegex_Etr1000FH_NonCatturaLaLocoDiUnaCartellaItaliaFrancia()
        {
            // "ETR1000IF 128": dopo il token "ETR1000" segue "I", non un separatore né una cifra,
            // quindi la loco di una cartella I-F non viene catturata sotto l'etichetta non-I-F.
            var regex = ExcelFolderParser.BuildLocoRegex("ETR1000 / 1000FH", minDigits: 3);

            Assert.NotNull(regex);
            Assert.False(regex!.Match(RealLogEtr1000If).Success);
        }

        [Fact]
        public void BuildLocoRegex_Etr1000IF_TrovaLaLocoSullaFormaAttaccata()
        {
            var regex = ExcelFolderParser.BuildLocoRegex("ETR1000 I-F", minDigits: 3);

            Assert.NotNull(regex);
            var match = regex!.Match(RealLogEtr1000If);
            Assert.True(match.Success);
            Assert.Equal("128", match.Groups[1].Value);
        }

        [Fact]
        public void BuildLocoRegex_Etr700_ComportamentoInvariatoRispettoAlPatternOriginale()
        {
            // ETR700 ed E404P funzionavano già prima: il fix non deve cambiarne il risultato.
            var regex = ExcelFolderParser.BuildLocoRegex("ETR700", minDigits: 3);

            Assert.NotNull(regex);
            var match = regex!.Match("SR1247654 LOG ETR700 117 04.02HR 300526 Todde");
            Assert.True(match.Success);
            Assert.Equal("117", match.Groups[1].Value);
        }

        [Fact]
        public void TryExtractTicketAndLoco_NomeFuoriGrammatica_RestituisceNull()
        {
            // Il chiamante deve poter distinguere "non analizzabile" per proseguire con i fallback
            // a regex preesistenti, invece di ricevere un valore inventato.
            Assert.Null(ExcelFolderParser.TryExtractTicketAndLoco("Cartella senza formato", RealKnownTypes));
            Assert.Null(ExcelFolderParser.TryExtractTicketAndLoco("", RealKnownTypes));
            Assert.Null(ExcelFolderParser.TryExtractTicketAndLoco(null, RealKnownTypes));
        }

        [Fact]
        public void TryExtractTicketAndLoco_TicketNonNumerico_RestituisceNullPerNonCambiareComportamento()
        {
            // LogDumpFolderName accetta un ticket \S+, l'estrazione originale solo cifre: la guardia
            // evita di introdurre valori che prima venivano scartati.
            Assert.Null(ExcelFolderParser.TryExtractTicketAndLoco(
                "SR12AB45 LOG ETR1000 119 02.02CR3 230826 Carlomagno", RealKnownTypes));
        }

        [Fact]
        public void TryExtractTicketAndLoco_LocoNonNumerica_RestituisceNullPerNonCambiareComportamento()
        {
            Assert.Null(ExcelFolderParser.TryExtractTicketAndLoco(
                "SR1234567 LOG ETR1000 XYZ 02.02CR3 230826 Carlomagno", RealKnownTypes));
        }

        [Fact]
        public void TryExtractTicketAndLoco_IndipendenteDallEtichettaSelezionata()
        {
            // Il punto centrale della correzione: l'estrazione non riceve più l'etichetta UI, quindi
            // non può più fallire per una discrepanza fra etichetta e nome su disco.
            var fromEtr1000 = ExcelFolderParser.TryExtractTicketAndLoco(RealLogEtr1000, RealKnownTypes);
            var fromEtr1000If = ExcelFolderParser.TryExtractTicketAndLoco(RealLogEtr1000If, RealKnownTypes);

            Assert.Equal(new TicketLoco("1234567", "119"), fromEtr1000);
            Assert.Equal(new TicketLoco("1234567", "128"), fromEtr1000If);
        }

        // ---------------------------------------------------------------------------------------
        // ETR1000, ETR1000FH ed ETR1000IF sono TRE TRENI DISTINTI. In EXCEL le prime due
        // condividono lo stesso Report Interventi (stessa voce di ComboBox, stessa cartella
        // Hitachi, stesse opzioni del form) mentre la I-F ne ha uno proprio — ma condividere il
        // report non significa essere lo stesso rotabile: il campo ROTABILE deve riportare il
        // treno reale della cartella. Vedi PROJECT_MEMORY.md §5.3.
        //
        // NOTA sui nomi FH: il committente ha fornito nomi di cartella reali solo per ETR1000 ed
        // ETR1000IF. Il token FH usato qui è "ETR1001FH", che NON è inventato — è il valore reale
        // della colonna `tipo` in `flotte` (verificato sul .db). I test coprono anche la forma
        // alternativa "1000FH". Se le cartelle FH reali usassero un token ancora diverso, questi
        // test vanno aggiornati con quello.
        // ---------------------------------------------------------------------------------------

        private const string FhLogEtr1001Fh = "SR1234570 LOG ETR1001FH 103 02.02CR3 230826 Carlomagno";
        private const string FhLog1000Fh = "SR1234571 LOG 1000FH 803 02.02CR3 230826 Carlomagno";

        [Theory]
        [InlineData(FhLogEtr1001Fh, "ETR1001FH", "103")]
        [InlineData(FhLog1000Fh, "1000FH", "803")]
        public void TryParse_CartellaFH_RiconosceIlTipoFHDistintoDaEtr1000(string folderName, string expectedTipo, string expectedLoco)
        {
            Assert.True(LogDumpFolderName.TryParse(folderName, RealKnownTypes, out var parsed));

            Assert.Equal(expectedTipo, parsed!.Tipo);
            Assert.Equal(expectedLoco, parsed.Loco);
            Assert.NotEqual("ETR1000", parsed.Tipo);
        }

        [Fact]
        public void ResolveActualTrainType_DistingueLeTreVariantiDellaFamigliaEtr1000()
        {
            Assert.Equal("ETR1000", ExcelFolderParser.ResolveActualTrainType([RealLogEtr1000, RealDumpEtr1000], RealKnownTypes));
            Assert.Equal("ETR1001FH", ExcelFolderParser.ResolveActualTrainType([FhLogEtr1001Fh], RealKnownTypes));
            Assert.Equal("ETR1000IF", ExcelFolderParser.ResolveActualTrainType([RealLogEtr1000If, RealDumpEtr1000If], RealKnownTypes));
        }

        [Fact]
        public void ResolveActualTrainType_NessunaSottocartellaAnalizzabile_RestituisceNull()
        {
            Assert.Null(ExcelFolderParser.ResolveActualTrainType(["Cartella qualsiasi", ""], RealKnownTypes));
            Assert.Null(ExcelFolderParser.ResolveActualTrainType([], RealKnownTypes));
        }

        [Theory]
        [InlineData("ETR1001FH", true)]
        [InlineData("1000FH", true)]
        [InlineData("ETR1000", false)]
        [InlineData("ETR1000IF", false)]
        public void IsFhType_RiconosceSoloLaVarianteFH(string tipo, bool expected) =>
            Assert.Equal(expected, ExcelFolderParser.IsFhType(tipo));

        [Theory]
        [InlineData("ETR1000IF", true)]
        [InlineData("ETR1000 I-F", true)]
        [InlineData("ETR1000", false)]
        [InlineData("ETR1001FH", false)]
        public void IsItaliaFranciaType_RiconosceSoloLaVarianteIF(string tipo, bool expected) =>
            Assert.Equal(expected, ExcelFolderParser.IsItaliaFranciaType(tipo));

        /// <summary>Opzioni ROTABILE di un foglio che distingue le tre varianti.</summary>
        private static readonly string[] RotabileOptionsConVarianti = ["ETR 500", "ETR 700", "ETR 1000", "ETR 1000 FH", "ETR 1000 ITA-FRA"];

        [Fact]
        public void SelectRotabileOption_CartellaFH_ScegliePropriaOpzioneNonQuellaEtr1000()
        {
            // È la regressione corretta: prima una cartella FH otteneva "ETR 1000".
            Assert.Equal("ETR 1000 FH", ExcelFolderParser.SelectRotabileOption(RotabileOptionsConVarianti, "ETR1001FH"));
        }

        [Fact]
        public void SelectRotabileOption_CartellaEtr1000Pura_NonScegliePerErroreLaVarianteFH()
        {
            // Simmetrico: "ETR 1000 FH" contiene la sottostringa "ETR 1000", quindi un confronto
            // per sola sottostringa poteva assegnarla a una cartella ETR1000 pura.
            Assert.Equal("ETR 1000", ExcelFolderParser.SelectRotabileOption(RotabileOptionsConVarianti, "ETR1000"));
        }

        [Fact]
        public void SelectRotabileOption_CartellaItaliaFrancia_ScegliePropriaOpzione()
        {
            Assert.Equal("ETR 1000 ITA-FRA", ExcelFolderParser.SelectRotabileOption(RotabileOptionsConVarianti, "ETR1000IF"));
        }

        [Fact]
        public void SelectRotabileOption_FoglioSenzaVariante_RestituisceNullPerNonRegredire()
        {
            // Un report che elenca solo "ETR 1000" non permette di fare meglio: restituendo null il
            // chiamante ricade sulla selezione preesistente, quindi nessun cambiamento rispetto a oggi.
            string[] senzaVarianti = ["ETR 500", "ETR 700", "ETR 1000"];

            Assert.Null(ExcelFolderParser.SelectRotabileOption(senzaVarianti, "ETR1001FH"));
            Assert.Equal("ETR 1000", ExcelFolderParser.SelectRotabileOption(senzaVarianti, "ETR1000"));
        }

        [Fact]
        public void SelectRotabileOption_SenzaTipoRealeOSenzaOpzioni_RestituisceNull()
        {
            Assert.Null(ExcelFolderParser.SelectRotabileOption(RotabileOptionsConVarianti, null));
            Assert.Null(ExcelFolderParser.SelectRotabileOption(RotabileOptionsConVarianti, ""));
            Assert.Null(ExcelFolderParser.SelectRotabileOption(null, "ETR1000"));
            Assert.Null(ExcelFolderParser.SelectRotabileOption([], "ETR1000"));
        }

        [Fact]
        public void GetDiskTokens_Etr1000FH_CopreEntrambeLeFlotteCheCondividonoIlReport()
        {
            // ETR1000 ed ETR1001FH sono treni distinti ma condividono il Report Interventi, quindi
            // stanno sotto la stessa voce di ComboBox: entrambi i token devono essere cercati.
            var tokens = ExcelFolderParser.GetDiskTokens("ETR1000 / 1000FH");

            Assert.Contains("ETR1000", tokens);
            Assert.Contains("ETR1001FH", tokens);
            Assert.Contains("1000FH", tokens);
        }

        [Fact]
        public void BuildLocoRegex_Etr1000FH_TrovaLaLocoAncheSuUnaCartellaFH()
        {
            var regex = ExcelFolderParser.BuildLocoRegex("ETR1000 / 1000FH", minDigits: 3);

            Assert.NotNull(regex);
            var match = regex!.Match(FhLogEtr1001Fh);
            Assert.True(match.Success);
            Assert.Equal("103", match.Groups[1].Value);
        }
    }
}
