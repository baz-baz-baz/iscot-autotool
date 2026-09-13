using System;
using System.Linq;
using PersonalAutomationTool.Modules.Excel;
using Xunit;

namespace PersonalAutomationTool.Tests.Modules.Excel
{
    /// <summary>
    /// Tier 1: le opzioni ammesse nelle ComboBox del Report Interventi ETR1000/ETR1001FH.
    ///
    /// <para>
    /// <b>Il punto di queste prove non è che la lista sia "quella giusta" secondo il codice</b>, ma
    /// che coincida con la <b>convalida dati realmente applicata</b> alle righe in uso del report
    /// aziendale. Le stringhe <c>formula1</c> qui sotto sono copiate letteralmente da
    /// <c>xl/worksheets/sheet1.xml</c> del file reale (Sprint 23, §6.1-vicies-quinquies), ciascuna con
    /// l'intervallo di righe a cui si applica, e vengono interpretate con lo stesso criterio che usa
    /// <c>ExcelViewModel</c> per le liste CSV. Se un valore del catalogo divergesse da queste, la
    /// cella scritta risulterebbe non valida all'apertura del report in Excel.
    /// </para>
    ///
    /// <para>
    /// Perché le convalide storiche non bastava ignorarle "a occhio": lo stesso file ne contiene
    /// altre, applicate a righe più vecchie, che l'unione precedente mescolava alle attuali — ad
    /// esempio <c>"Pistoia,…,OMC ETR Vicenza,…"</c> per il Sito (righe 9652-16227) o cinque liste
    /// diverse per "Materiale Fornito".
    /// </para>
    /// </summary>
    public class ReportOptionsCatalogTests
    {
        private const string Flotta = ReportOptionsCatalog.Etr1000Report;

        // --- Convalide reali del Report Interventi ETR1000, quelle che coprono le righe in uso ---

        /// <summary>Colonna C, <c>sqref</c> …C17128:C20369. La virgola finale produce una voce vuota, che va scartata.</summary>
        private const string ConvalidaSito = "Pistoia,Napoli Gianturco,Milano Martesana,Roma S.Lorenzo,Piacenza,Firenze,";

        /// <summary>Colonna F, <c>sqref</c> …F10904:F65442.</summary>
        private const string ConvalidaCliente = "Hitachi,Trenitalia";

        /// <summary>Colonna L, <c>sqref</c> …L7335:L65442.</summary>
        private const string ConvalidaTipologia =
            "Mis,Extragaranzia,Upgrade,Man Programmata,Man Predittiva,Controlli Remoto,Nulla Riscontrato,Correttiva con sostit,Correttiva senza sostit";

        /// <summary>Colonna N, <c>sqref</c> N15795:N1048576. La variante storica ha "JRU" al posto di "JRU DIS".</summary>
        private const string ConvalidaCategoriaAvaria =
            "Oscuram Monitor,Verifica,Catena Radio,Catena Vigilante,Catena RSDD,JRU DIS,Data Logger,RIML,Odometria,Perdita Rid. SSB,Altro";

        /// <summary>Colonna O, <c>sqref</c> …O16988:O20369. Nota lo spazio dopo la virgola.</summary>
        private const string ConvalidaScaricoDati = "SI, NO";

        /// <summary>Colonna P, <c>sqref</c> …P16981:P20369. Nota gli spazi dopo le virgole.</summary>
        private const string ConvalidaMaterialeFornito = "HR, STS, TI";

        /// <summary>
        /// Colonna J, convalida <c>$AD$3:$AD$5</c>: la prima cella dell'intervallo è vuota e non
        /// produce una voce.
        /// </summary>
        private static readonly string[] CelleRotabile = ["", "ETR1000", "ETR1001FH"];

        /// <summary>
        /// Colonna AA, convalida <c>$AD$6:$AD$10</c>. <c>AD9</c> ha uno spazio in coda nel file
        /// reale: l'applicazione lo rimuove, come fa per qualunque voce presa da un intervallo.
        /// </summary>
        private static readonly string[] CelleVersioneSw =
            ["04.01 HR", "04.03 HR", "01.01.0000 Elo BL3", "02.02.0006 Elo BL3 ", "02.02.0007 Elo BL3"];

        /// <summary>Interpreta una lista CSV come fa <c>ExcelViewModel</c> per le convalide in linea.</summary>
        private static string[] DaListaCsv(string formula1) =>
            [.. formula1.Trim('"').Split([',', ';'], StringSplitOptions.RemoveEmptyEntries).Select(s => s.Trim())];

        /// <summary>Interpreta le celle di un intervallo come fa <c>ExcelViewModel</c>.</summary>
        private static string[] DaIntervallo(string[] celle) =>
            [.. celle.Where(c => !string.IsNullOrWhiteSpace(c)).Select(c => c.Trim())];

        // -----------------------------------------------------------------------------------
        // 1. Ogni lista coincide con la convalida dati reale
        // -----------------------------------------------------------------------------------

        [Theory]
        [InlineData("Sito", ConvalidaSito)]
        [InlineData("Cliente", ConvalidaCliente)]
        [InlineData("TIPOLOGIA INTERVENTO", ConvalidaTipologia)]
        [InlineData("CATEGORIA AVARIA ", ConvalidaCategoriaAvaria)]
        [InlineData("Scarico Dati Locale", ConvalidaScaricoDati)]
        [InlineData("Materiale Fornito", ConvalidaMaterialeFornito)]
        public void LeOpzioniCoincidonoConLaConvalidaDatiDelReport(string intestazione, string formula1)
        {
            var attese = DaListaCsv(formula1);

            var opzioni = ReportOptionsCatalog.GetOptions(Flotta, intestazione);

            Assert.NotNull(opzioni);
            Assert.Equal(attese, opzioni!);
        }

        [Fact]
        public void Rotabile_CoincideConLIntervalloDiConvalida()
        {
            var opzioni = ReportOptionsCatalog.GetOptions(Flotta, "ROTABILE");

            Assert.NotNull(opzioni);
            Assert.Equal(DaIntervallo(CelleRotabile), opzioni!);
            // Le due flotte che condividono questo report, e nient'altro.
            Assert.Equal(["ETR1000", "ETR1001FH"], opzioni!);
        }

        [Fact]
        public void VersioneSw_CoincideConLIntervalloDiConvalida()
        {
            var opzioni = ReportOptionsCatalog.GetOptions(Flotta, "VERSIONE SW PRESENTE");

            Assert.NotNull(opzioni);
            Assert.Equal(DaIntervallo(CelleVersioneSw), opzioni!);
        }

        // -----------------------------------------------------------------------------------
        // 2. Le liste sono esattamente quelle richieste, senza voci in più
        // -----------------------------------------------------------------------------------

        [Fact]
        public void Sito_NonContieneLeSediDelleConvalideStoriche()
        {
            var opzioni = ReportOptionsCatalog.GetOptions(Flotta, "Sito")!;

            Assert.Equal(
                ["Pistoia", "Napoli Gianturco", "Milano Martesana", "Roma S.Lorenzo", "Piacenza", "Firenze"],
                opzioni);

            // Erano finite nel menu: la prima dalla convalida storica del file, la seconda da una
            // lista fissa nel codice che non distingueva la flotta.
            Assert.DoesNotContain("OMC ETR Vicenza", opzioni);
            Assert.DoesNotContain("IMC AV Mestre", opzioni);
        }

        [Fact]
        public void ScaricoDatiLocale_SoloMaiuscole_SenzaLaVarianteStorica()
        {
            var opzioni = ReportOptionsCatalog.GetOptions(Flotta, "Scarico Dati Locale")!;

            Assert.Equal(["SI", "NO"], opzioni);
            // L'unione con la convalida storica "Si,No" produceva quattro voci invece di due,
            // perché il confronto fra stringhe distingue le maiuscole.
            Assert.DoesNotContain("Si", opzioni);
            Assert.DoesNotContain("No", opzioni);
        }

        [Fact]
        public void MaterialeFornito_NonContieneLeCinqueListeStoriche()
        {
            var opzioni = ReportOptionsCatalog.GetOptions(Flotta, "Materiale Fornito")!;

            Assert.Equal(["HR", "STS", "TI"], opzioni);
            foreach (var storica in new[] { "ASTS", "ANSALDOBREDA", "HITACHI", "HITACHIRail", "HR-STS" })
            {
                Assert.DoesNotContain(storica, opzioni);
            }
        }

        [Fact]
        public void CategoriaAvaria_UsaJruDis_NonLaVarianteStoricaJru()
        {
            var opzioni = ReportOptionsCatalog.GetOptions(Flotta, "CATEGORIA AVARIA ")!;

            Assert.Contains("JRU DIS", opzioni);
            Assert.DoesNotContain("JRU", opzioni);
            Assert.Equal(11, opzioni.Count);
        }

        [Fact]
        public void VersioneSw_NonContieneLeVersioniStoriche()
        {
            var opzioni = ReportOptionsCatalog.GetOptions(Flotta, "VERSIONE SW PRESENTE")!;

            Assert.Equal(
                ["04.01 HR", "04.03 HR", "01.01.0000 Elo BL3", "02.02.0006 Elo BL3", "02.02.0007 Elo BL3"],
                opzioni);
            foreach (var storica in new[] { "04.00.34CR", "04.00.35A1", "04.00.35HR", "04.00.36HR", "04.01.0002HR" })
            {
                Assert.DoesNotContain(storica, opzioni);
            }
        }

        // -----------------------------------------------------------------------------------
        // 3. Confini: campi che NON devono essere vincolati, e altre flotte
        // -----------------------------------------------------------------------------------

        [Theory]
        [InlineData("TECNICO Cliente")]  // contiene "Cliente" ma non è il campo Cliente
        [InlineData("TECNICO HRSTS")]
        [InlineData("AVARIA SEGNALATA")] // contiene "Avaria" ma non è la Categoria Avaria
        [InlineData("Descrizione intervento effettuato")]
        [InlineData("NOTE")]
        [InlineData("SN")]
        [InlineData("DATA CHIAMATA")]
        public void CampiNonVincolati_NonRicevonoAlcunaLista(string intestazione) =>
            Assert.Null(ReportOptionsCatalog.GetOptions(Flotta, intestazione));

        [Theory]
        [InlineData("ETR700")]
        [InlineData("E404P")]
        [InlineData("ETR1000 I-F")]
        [InlineData("")]
        [InlineData(null)]
        public void AltreFlotte_NonSonoToccate(string? flotta)
        {
            // Il catalogo vincola il solo report ETR1000/ETR1001FH: per tutte le altre flotte il
            // chiamante deve continuare a usare ciò che ha ricavato dal file.
            foreach (var campo in new[] { "Sito", "Cliente", "ROTABILE", "TIPOLOGIA INTERVENTO", "CATEGORIA AVARIA ", "Scarico Dati Locale", "Materiale Fornito", "VERSIONE SW PRESENTE", "Descrizione LRU" })
            {
                Assert.Null(ReportOptionsCatalog.GetOptions(flotta, campo));
            }
        }

        [Fact]
        public void LEtichettaDiFlottaEConfrontataInModoEsatto()
        {
            // "ETR1000" da solo non è l'etichetta del report: quella è "ETR1000 / 1000FH" (§5.3-bis).
            Assert.Null(ReportOptionsCatalog.GetOptions("ETR1000", "Sito"));
            Assert.Null(ReportOptionsCatalog.GetOptions("ETR1001FH", "Sito"));
            Assert.NotNull(ReportOptionsCatalog.GetOptions("ETR1000 / 1000FH", "Sito"));
        }

        [Fact]
        public void NomeCampoVuotoONullo_NonProduceLista()
        {
            Assert.Null(ReportOptionsCatalog.GetOptions(Flotta, null));
            Assert.Null(ReportOptionsCatalog.GetOptions(Flotta, "   "));
        }

        // -----------------------------------------------------------------------------------
        // 4. "Descrizione LRU" (colonne R e U, stessa convalida $AE$4:$AE$106): 103 componenti
        // -----------------------------------------------------------------------------------

        [Fact]
        public void DescrizioneLru_HaEsattamente103ComponentiSenzaDuplicati()
        {
            var opzioni = ReportOptionsCatalog.GetOptions(Flotta, "Descrizione LRU")!;

            Assert.NotNull(opzioni);
            Assert.Equal(103, opzioni.Count);
            Assert.Equal(opzioni.Count, opzioni.Distinct().Count());
        }

        /// <summary>
        /// Le due colonne "Descrizione LRU" (R e U) condividono la stessa convalida <c>$AE$4:$AE$106</c>:
        /// il campo è riconosciuto dal nome, non dalla lettera di colonna, quindi entrambe ricevono
        /// lo stesso catalogo.
        /// </summary>
        [Fact]
        public void DescrizioneLru_StessaListaPerEntrambeLeColonne()
        {
            var perColonnaR = ReportOptionsCatalog.GetOptions(Flotta, "Descrizione LRU");
            var perColonnaU = ReportOptionsCatalog.GetOptions(Flotta, "Descrizione LRU");

            Assert.Equal(perColonnaR, perColonnaU);
        }

        [Theory]
        [InlineData("ARMADIO ALA B61A.000014")]     // nessuno spazio iniziale: caso comune
        [InlineData("BTM N B62A.000002")]
        [InlineData("Elo SWITCH CS2 9001.0101301")]
        public void DescrizioneLru_ContieneIComponentiComuni(string componente)
        {
            var opzioni = ReportOptionsCatalog.GetOptions(Flotta, "Descrizione LRU")!;
            Assert.Contains(componente, opzioni);
        }

        /// <summary>
        /// Sette voci del file reale hanno uno spazio <b>iniziale</b> significativo, marcato con
        /// <c>xml:space="preserve"</c> in <c>sharedStrings.xml</c> — non un refuso di trascrizione.
        /// Se il catalogo le avesse "ripulite" col <c>.Trim()</c> che l'app applica ad altri campi
        /// (Rotabile, Versione SW), il valore scritto in cella non corrisponderebbe più a nessuna
        /// voce della convalida.
        /// </summary>
        [Theory]
        [InlineData(" CPUE ALM N B61C.0100003")]
        [InlineData(" CPUE ALM R B61C.0100004")]
        [InlineData(" WDRS N B61D.000005")]
        [InlineData(" Edor 109A.0101709")]
        public void DescrizioneLru_PreservaLoSpazioInizialeSignificativo(string componenteConSpazio)
        {
            var opzioni = ReportOptionsCatalog.GetOptions(Flotta, "Descrizione LRU")!;

            Assert.Contains(componenteConSpazio, opzioni);
            Assert.StartsWith(" ", componenteConSpazio); // il caso di prova stesso deve avere lo spazio
        }

        [Fact]
        public void DescrizioneLru_PreservaGliSpaziDoppiInterni()
        {
            var opzioni = ReportOptionsCatalog.GetOptions(Flotta, "Descrizione LRU")!;

            Assert.Contains("SUONERIA CAB.A  B64A.000002", opzioni);
            Assert.Contains("ANTENNA_RSDD  N FM9136300101", opzioni);
        }

        [Theory]
        [InlineData("Descrizione LRU")]
        public void DescrizioneLru_EMarcataComeCampoDigitabileConRicerca(string intestazione) =>
            Assert.True(ReportOptionsCatalog.IsSearchableComponentField(intestazione));

        [Theory]
        [InlineData("Sito")]
        [InlineData("Cliente")]
        [InlineData("ROTABILE")]
        [InlineData("TIPOLOGIA INTERVENTO")]
        [InlineData("CATEGORIA AVARIA ")]
        [InlineData("Scarico Dati Locale")]
        [InlineData("Materiale Fornito")]
        [InlineData("VERSIONE SW PRESENTE")]
        [InlineData("LRU Rimossa")]   // contiene "LRU" ma non è "Descrizione LRU": nessuna convalida nel file reale
        [InlineData("LRU Installata")]
        [InlineData(null)]
        public void SoloDescrizioneLru_EMarcataComeCampoDigitabile(string? intestazione) =>
            Assert.False(ReportOptionsCatalog.IsSearchableComponentField(intestazione));
    }
}
