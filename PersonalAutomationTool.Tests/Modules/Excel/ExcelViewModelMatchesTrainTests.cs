using PersonalAutomationTool.Modules.Excel;
using Xunit;

namespace PersonalAutomationTool.Tests.Modules.Excel
{
    /// <summary>
    /// Tier 1 (funzione pura su stringhe): <see cref="ExcelViewModel.MatchesTrain"/>, che decide se
    /// il nome di un file "Report Interventi" appartiene alla flotta selezionata nella ComboBox del
    /// modulo EXCEL.
    ///
    /// <para>
    /// <b>Perché questo file esiste.</b> Queste asserzioni provengono dall'harness console manuale
    /// <c>TestClosedXML</c>, rimosso nello Sprint 11 (§6.1-terdecies di PROJECT_MEMORY.md): era
    /// l'unica verifica automatica di <c>MatchesTrain</c> ed è stata portata qui prima di cancellarlo,
    /// così la copertura non è andata persa insieme al progetto scratch. I 3 casi originali sono i
    /// primi tre test sotto; il resto estende la stessa logica ai casi dell'invariante §5.3-bis.
    /// </para>
    ///
    /// <para>
    /// <b>Da non confondere con <c>ExcelFolderParserTests</c>.</b> Quella suite copre la grammatica dei
    /// <i>nomi di sottocartella</i> LOG/DUMP; <c>MatchesTrain</c> è logica autonoma sui <i>nomi dei
    /// file report</i> e non delega a <c>ExcelFolderParser</c>. Sono due percorsi separati: una
    /// modifica all'uno non è coperta dai test dell'altro.
    /// </para>
    /// </summary>
    public sealed class ExcelViewModelMatchesTrainTests
    {
        // --- I 3 casi ereditati da TestClosedXML/Program.cs (Sprint 11) ---

        [Fact]
        public void E404P_RiconosceIlReportNominatoETR500()
        {
            // Il report della flotta E404P sul disco si chiama "ETR500": l'etichetta della ComboBox e
            // il nome reale del file non coincidono (invariante §5.3).
            Assert.True(ExcelViewModel.MatchesTrain("Report Interventi ETR500 230726 04_04.xlsx", "E404P"));
        }

        [Fact]
        public void Etr1000_RiconosceLAlias1001()
        {
            Assert.True(ExcelViewModel.MatchesTrain("Report 1001.xlsx", "ETR1000 / 1000FH"));
        }

        [Fact]
        public void Etr1000_EscludeLaVarianteItaliaFrancia()
        {
            // Caso critico §5.3-bis: ETR1000/1000FH e ETR1000 I-F portano a report con maxCol diverso
            // (27 contro 24) e a cartelle Hitachi diverse. Confonderli significa scrivere nel report
            // sbagliato, in silenzio.
            Assert.False(ExcelViewModel.MatchesTrain("Report 1000IF.xlsx", "ETR1000 / 1000FH"));
        }

        // --- Estensione: le altre forme con cui la variante I-F compare sui nomi reali ---

        [Theory]
        [InlineData("Report Interventi 1000IF 230726.xlsx")]
        [InlineData("Report Interventi ETR1000 Italia 230726.xlsx")]
        [InlineData("Report Interventi ETR1000 Francia 230726.xlsx")]
        [InlineData("Report Interventi ETR1000 ITA-FRA 230726.xlsx")]
        [InlineData("Report Interventi ETR1000 I-F 230726.xlsx")]
        public void Etr1000IF_RiconosceTutteLeGrafieDellaVariante(string fileName) =>
            Assert.True(ExcelViewModel.MatchesTrain(fileName, "ETR1000 I-F"));

        [Theory]
        [InlineData("Report Interventi 1000IF 230726.xlsx")]
        [InlineData("Report Interventi ETR1000 Italia 230726.xlsx")]
        [InlineData("Report Interventi ETR1000 Francia 230726.xlsx")]
        [InlineData("Report Interventi ETR1000 ITA-FRA 230726.xlsx")]
        [InlineData("Report Interventi ETR1000 I-F 230726.xlsx")]
        public void Etr1000_EscludeOgniGrafiaDellaVarianteIF(string fileName) =>
            Assert.False(ExcelViewModel.MatchesTrain(fileName, "ETR1000 / 1000FH"));

        [Theory]
        [InlineData("Report Interventi ETR1000 230726.xlsx")]
        [InlineData("Report Interventi 1000FH 230726.xlsx")]
        [InlineData("Report Interventi 1001 230726.xlsx")]
        public void Etr1000_RiconosceLeTreGrafieCondivise(string fileName) =>
            Assert.True(ExcelViewModel.MatchesTrain(fileName, "ETR1000 / 1000FH"));

        // --- Comportamento di base ---

        [Fact]
        public void FlottaSenzaRegoleDedicate_UsaIlConfrontoLetterale()
        {
            // ETR700 non ha un ramo dedicato: si ricade sul Contains dell'etichetta.
            Assert.True(ExcelViewModel.MatchesTrain("Report Interventi ETR700 270526 17_23 AlvarezE.xlsx", "ETR700"));
            Assert.False(ExcelViewModel.MatchesTrain("Report Interventi ETR700 270526 17_23 AlvarezE.xlsx", "ETR421"));
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        public void TrainTypeVuoto_NonCorrispondeMai(string? trainType) =>
            Assert.False(ExcelViewModel.MatchesTrain("Report Interventi ETR700 230726.xlsx", trainType));

        [Fact]
        public void IlConfrontoIgnoraMaiuscoleEMinuscole()
        {
            Assert.True(ExcelViewModel.MatchesTrain("report interventi etr500 230726.xlsx", "E404P"));
            Assert.True(ExcelViewModel.MatchesTrain("REPORT INTERVENTI ETR700 230726.XLSX", "ETR700"));
        }

        [Fact]
        public void ValutaSoloIlNomeDelFile_NonIlPercorsoCheLoContiene()
        {
            // Una cartella di passaggio che contiene il token della flotta non deve far scattare il
            // match: conta solo il nome del file, altrimenti un report archiviato sotto
            // "…\ETR500\…" verrebbe attribuito alla flotta sbagliata.
            Assert.False(ExcelViewModel.MatchesTrain(@"C:\LOG & DUMP\ETR500\Report Interventi ETR700 230726.xlsx", "E404P"));
            Assert.True(ExcelViewModel.MatchesTrain(@"C:\LOG & DUMP\ETR500\Report Interventi ETR700 230726.xlsx", "ETR700"));
        }
    }
}
