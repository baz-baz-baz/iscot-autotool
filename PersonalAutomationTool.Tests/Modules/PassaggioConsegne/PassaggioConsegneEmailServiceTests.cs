using System;
using PersonalAutomationTool.Modules.PassaggioConsegne;
using Xunit;

namespace PersonalAutomationTool.Tests.Modules.PassaggioConsegne
{
    /// <summary>
    /// Tier 1: il corpo HTML dell'email di passaggio consegne
    /// (<see cref="OutlookRapportinoMailService.BuildHtmlBody"/>), la fascia oraria del saluto
    /// (<see cref="OutlookRapportinoMailService.DetermineSaluto"/>) e il colore per stato
    /// (<see cref="OutlookRapportinoMailService.ColoreStato"/>).
    ///
    /// <para>
    /// Il nome del PDF allegato ("Rapportino di Turno.pdf", fisso) è invece coperto da
    /// <c>PassaggioConsegnePdfExporterTests.IlNomeDelFileEFissoIndipendentementeDaFlottaEData</c>: è
    /// <see cref="PassaggioConsegnePdfExporter"/> a decidere quel nome, non questa classe — qui si
    /// verifica solo che l'HTML del corpo rispetti la struttura richiesta.
    /// </para>
    /// </summary>
    public sealed class PassaggioConsegneEmailServiceTests
    {
        // ------------------------------------------------------------------
        // DetermineSaluto — quattro fasce orarie
        // ------------------------------------------------------------------

        [Theory]
        [InlineData(4, "Buongiorno,")]
        [InlineData(9, "Buongiorno,")]
        [InlineData(13, "Buongiorno,")]
        [InlineData(14, "Buon pomeriggio,")]
        [InlineData(16, "Buon pomeriggio,")]
        [InlineData(17, "Buon pomeriggio,")]
        [InlineData(18, "Buonasera,")]
        [InlineData(20, "Buonasera,")]
        [InlineData(21, "Buonasera,")]
        [InlineData(22, "Buonanotte,")]
        [InlineData(23, "Buonanotte,")]
        [InlineData(0, "Buonanotte,")]
        [InlineData(3, "Buonanotte,")]
        public void DetermineSaluto_RispettaLeQuattroFasceOrarie(int ora, string salutoAtteso)
        {
            var adesso = new DateTime(2026, 8, 24, ora, 0, 0);

            Assert.Equal(salutoAtteso, OutlookRapportinoMailService.DetermineSaluto(adesso));
        }

        // ------------------------------------------------------------------
        // ColoreStato — un colore distinto per ciascuno dei 3 stati
        // ------------------------------------------------------------------

        [Theory]
        [InlineData(StatoTurno.NessunaAttivita, "#28A745")]
        [InlineData(StatoTurno.AttivitaPreviste, "#D39E00")]
        [InlineData(StatoTurno.AttivitaImminentiOInCorso, "#DC3545")]
        public void ColoreStato_UnColoreDistintoPerStato(StatoTurno stato, string coloreAtteso) =>
            Assert.Equal(coloreAtteso, OutlookRapportinoMailService.ColoreStato(stato));

        // ------------------------------------------------------------------
        // BuildHtmlBody — struttura del corpo
        // ------------------------------------------------------------------

        private static readonly DateTime Mattina = new(2026, 8, 24, 9, 0, 0);

        [Fact]
        public void BuildHtmlBody_ContieneIlSalutoDellaFasciaOraria()
        {
            string html = OutlookRapportinoMailService.BuildHtmlBody("ETR700", StatoTurno.NessunaAttivita, Mattina);

            Assert.Contains("Buongiorno,", html);
        }

        [Fact]
        public void BuildHtmlBody_ContieneLaFlottaSenzaSpazi()
        {
            string html = OutlookRapportinoMailService.BuildHtmlBody("ETR700", StatoTurno.NessunaAttivita, Mattina);

            Assert.Contains("ETR700", html);
        }

        [Fact]
        public void BuildHtmlBody_TerminaConSaluti()
        {
            string html = OutlookRapportinoMailService.BuildHtmlBody("ETR500", StatoTurno.AttivitaPreviste, Mattina);

            Assert.Contains("Saluti", html);
        }

        [Theory]
        [InlineData(StatoTurno.NessunaAttivita, "#28A745")]
        [InlineData(StatoTurno.AttivitaPreviste, "#D39E00")]
        [InlineData(StatoTurno.AttivitaImminentiOInCorso, "#DC3545")]
        public void BuildHtmlBody_ColoraLetichettaDiFlottaSecondoLoStato(StatoTurno stato, string coloreAtteso)
        {
            string html = OutlookRapportinoMailService.BuildHtmlBody("ETR1000", stato, Mattina);

            Assert.Contains($"color: {coloreAtteso};", html);
        }

        [Fact]
        public void BuildHtmlBody_NessunaAttivita_MostraLaDicituraFissa()
        {
            string html = OutlookRapportinoMailService.BuildHtmlBody("ETR700", StatoTurno.NessunaAttivita, Mattina);

            Assert.Contains("Nessuna attività in sospeso.", html);
            Assert.DoesNotContain("<br/><br/>", html);
        }

        [Theory]
        [InlineData(StatoTurno.AttivitaPreviste)]
        [InlineData(StatoTurno.AttivitaImminentiOInCorso)]
        public void BuildHtmlBody_AttivitaPrevisteOImminenti_LasciaDueRigheVuote(StatoTurno stato)
        {
            string html = OutlookRapportinoMailService.BuildHtmlBody("ETR700", stato, Mattina);

            Assert.Contains("<br/><br/>", html);
            Assert.DoesNotContain("Nessuna attività in sospeso.", html);
        }

        [Fact]
        public void BuildHtmlBody_LOrdineERispettato_SalutoFlottaCentroSaluti()
        {
            string html = OutlookRapportinoMailService.BuildHtmlBody("ETR700", StatoTurno.NessunaAttivita, Mattina);

            int idxSaluto = html.IndexOf("Buongiorno,", StringComparison.Ordinal);
            int idxFlotta = html.IndexOf("ETR700", StringComparison.Ordinal);
            int idxCentro = html.IndexOf("Nessuna attività in sospeso.", StringComparison.Ordinal);
            int idxChiusura = html.IndexOf("Saluti", StringComparison.Ordinal);

            Assert.True(idxSaluto < idxFlotta, "il saluto deve precedere l'etichetta di flotta");
            Assert.True(idxFlotta < idxCentro, "l'etichetta di flotta deve precedere il testo centrale");
            Assert.True(idxCentro < idxChiusura, "il testo centrale deve precedere la chiusura");
        }
    }
}
