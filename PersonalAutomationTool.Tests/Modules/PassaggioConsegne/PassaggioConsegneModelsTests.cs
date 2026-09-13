using System;
using System.Linq;
using PersonalAutomationTool.Modules.PassaggioConsegne;
using Xunit;

namespace PersonalAutomationTool.Tests.Modules.PassaggioConsegne
{
    /// <summary>
    /// Tier 1 (nessun WPF, nessun file system): orari dei turni, regola "Si"/"No"/vuoto e nozione di
    /// "riga compilata". Sono le tre regole di dominio del modulo PASSAGGIO CONSEGNE, tutte estratte
    /// in tipi puri proprio per poter essere verificate qui invece che a occhio su un PDF.
    /// </summary>
    public sealed class PassaggioConsegneModelsTests
    {
        // ------------------------------------------------------------------
        // Turni predefiniti — gli orari dettati dal committente
        // ------------------------------------------------------------------

        [Theory]
        [InlineData("1° Turno", "06:00", "14:00")]
        [InlineData("Turno Centrale", "08:00", "16:30")]
        [InlineData("2° Turno", "14:00", "22:00")]
        [InlineData("3° Turno", "22:00", "06:00")]
        public void TurnoPredefinito_OrariCorrispondentiAllaSpecifica(string nome, string inizio, string fine)
        {
            var turno = TurnoPredefinito.Tutti.Single(t => t.Nome == nome);

            Assert.Equal(inizio, turno.OraInizio);
            Assert.Equal(fine, turno.OraFine);
        }

        [Fact]
        public void TurnoPredefinito_QuattroTurniNellOrdineDellaComboBox()
        {
            Assert.Equal(
                ["1° Turno", "Turno Centrale", "2° Turno", "3° Turno"],
                TurnoPredefinito.Tutti.Select(t => t.Nome));
        }

        [Fact]
        public void SelezionareUnTurno_ApplicaGliOrariAlRapportino()
        {
            var rapportino = CreaRapportino();

            rapportino.TurnoSelezionato = TurnoPredefinito.Centrale;

            Assert.Equal("08:00", rapportino.OraInizio);
            Assert.Equal("16:30", rapportino.OraFine);
        }

        [Fact]
        public void GliOrariRestanoModificabiliAMano_DopoLaSelezioneDelTurno()
        {
            // La ComboBox è una scorciatoia, non un vincolo: un turno spezzato o prolungato deve
            // restare digitabile.
            var rapportino = CreaRapportino();
            rapportino.TurnoSelezionato = TurnoPredefinito.Primo;

            rapportino.OraFine = "15:30";

            Assert.Equal("06:00", rapportino.OraInizio);
            Assert.Equal("15:30", rapportino.OraFine);
        }

        // ------------------------------------------------------------------
        // Regola di stampa delle colonne a checkbox
        // ------------------------------------------------------------------

        [Theory]
        [InlineData(true, true, "Si")]
        [InlineData(false, true, "No")]
        public void SiNoCell_RigaCompilata_StampaSiOppureNo(bool spuntata, bool compilata, string atteso) =>
            Assert.Equal(atteso, SiNoCell.PerPdf(spuntata, compilata));

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void SiNoCell_RigaNonCompilata_RestaVuotaQualunqueSiaLaCheckbox(bool spuntata) =>
            Assert.Equal(string.Empty, SiNoCell.PerPdf(spuntata, rigaCompilata: false));

        // ------------------------------------------------------------------
        // Quando una riga conta come "compilata"
        // ------------------------------------------------------------------

        [Fact]
        public void DettaglioIntervento_RigaVuota_NonECompilata() =>
            Assert.False(new DettaglioInterventoRow().IsCompilata);

        [Theory]
        [InlineData("ETR700 101", "")]
        [InlineData("", "Sostituzione scheda")]
        [InlineData("ETR700 101", "Sostituzione scheda")]
        public void DettaglioIntervento_TrenoLocoODescrizione_LaRendonoCompilata(string trenoLoco, string descrizione)
        {
            var riga = new DettaglioInterventoRow { TrenoLoco = trenoLoco, Descrizione = descrizione };

            Assert.True(riga.IsCompilata);
        }

        [Fact]
        public void DettaglioIntervento_SoloCheckboxSpuntate_NonBastanoARenderlaCompilata()
        {
            // Una spunta messa per sbaglio su una riga altrimenti vuota non deve produrre una riga di
            // "No" nel PDF: è il caso che la regola esiste per evitare.
            var riga = new DettaglioInterventoRow { CompilazioneOdl = true, ChiusuraTicket = true };

            Assert.False(riga.IsCompilata);
        }

        [Fact]
        public void DettaglioIntervento_SoliSpaziBianchi_NonRendonoLaRigaCompilata() =>
            Assert.False(new DettaglioInterventoRow { TrenoLoco = "   ", Descrizione = "\t" }.IsCompilata);

        [Theory]
        [InlineData("ETR500 618", "", "", "")]
        [InlineData("", "Mancanza ricambi", "", "")]
        [InlineData("", "", "14:30", "")]
        [InlineData("", "", "", "Rossi")]
        public void InterventoNonSvolto_QualsiasiCampoDiTesto_LoRendeCompilato(
            string trenoLoco, string motivazione, string oraRichiesta, string referente)
        {
            var riga = new InterventoNonSvoltoRow
            {
                TrenoLoco = trenoLoco,
                Motivazione = motivazione,
                OraRichiesta = oraRichiesta,
                Referente = referente
            };

            Assert.True(riga.IsCompilata);
        }

        [Fact]
        public void InterventoNonSvolto_RigaVuota_NonECompilata() =>
            Assert.False(new InterventoNonSvoltoRow().IsCompilata);

        // ------------------------------------------------------------------
        // Struttura iniziale: deve rispecchiare il template Excel
        // ------------------------------------------------------------------

        [Fact]
        public void NuovoRapportino_HaLeRigheDelTemplateExcel()
        {
            var rapportino = CreaRapportino();

            Assert.Equal(10, rapportino.Movimenti.Count);          // righe 6-15 del foglio
            Assert.Equal(5, rapportino.Interventi.Count);          // righe 18-22
            Assert.Equal(10, rapportino.InterventiNonSvolti.Count); // righe 25-34
        }

        [Fact]
        public void NuovoRapportino_LeTabelleNumeratePartonoDaUno()
        {
            var rapportino = CreaRapportino();

            Assert.Equal(Enumerable.Range(1, 10), rapportino.Movimenti.Select(m => m.Numero));
            Assert.Equal(Enumerable.Range(1, 10), rapportino.InterventiNonSvolti.Select(n => n.Numero));
        }

        [Fact]
        public void NuovoRapportino_LaDataEQuellaOdierna() =>
            Assert.Equal(DateTime.Today, CreaRapportino().Data);

        [Fact]
        public void PopolaDaVerifiche_PortaLeQuattroColonneDiMovimentoANo()
        {
            // Requisito esplicito: un treno estratto dalle VERIFICHE non è entrato né uscito finché il
            // tecnico non lo annota, quindi le 4 colonne nascono a "No" e non vuote.
            var riga = new MovimentoTrenoRow { Numero = 1 };

            riga.PopolaDaVerifiche("ETR700 12", "101 - 102");

            Assert.Equal("ETR700 12", riga.Treno);
            Assert.Equal("101 - 102", riga.Loco);
            Assert.Equal("No", riga.DataIngresso);
            Assert.Equal("No", riga.OraIngresso);
            Assert.Equal("No", riga.DataUscita);
            Assert.Equal("No", riga.OraUscita);
        }

        [Fact]
        public void Svuota_RiportaLaRigaMovimentoAlloStatoVuoto()
        {
            var riga = new MovimentoTrenoRow { Numero = 3 };
            riga.PopolaDaVerifiche("ETR1000 55", "550");

            riga.Svuota();

            Assert.Equal(string.Empty, riga.Treno);
            Assert.Equal(string.Empty, riga.DataIngresso);
            Assert.Equal(3, riga.Numero); // la numerazione non è un dato compilabile: resta
        }

        [Fact]
        public void RinumeraRighe_ChiudeIBuchiDopoUnaRimozione()
        {
            var rapportino = CreaRapportino();
            rapportino.InterventiNonSvolti.RemoveAt(4);

            rapportino.RinumeraRighe();

            Assert.Equal(Enumerable.Range(1, 9), rapportino.InterventiNonSvolti.Select(n => n.Numero));
        }

        // ------------------------------------------------------------------
        // SpostaOra — pulsanti "-4"/"+4" sopra ORA INIZIO/FINE
        // ------------------------------------------------------------------

        [Theory]
        [InlineData("10:00", 4, "14:00")]      // caso semplice, nessun avvolgimento
        [InlineData("10:00", -4, "06:00")]
        [InlineData("06:00", -4, "02:00")]
        public void SpostaOra_CasoSempliceSenzaAttraversareLaMezzanotte(string orario, int delta, string atteso) =>
            Assert.Equal(atteso, RapportinoTurno.SpostaOra(orario, delta));

        [Theory]
        [InlineData("02:00", -4, "22:00")]     // sottrazione che scavalla la mezzanotte all'indietro
        [InlineData("01:30", -4, "21:30")]
        [InlineData("22:00", 4, "02:00")]      // addizione che scavalla la mezzanotte in avanti
        [InlineData("23:15", 4, "03:15")]
        public void SpostaOra_AvvolgeOltreLaMezzanotteInEntrambeLeDirezioni(string orario, int delta, string atteso) =>
            Assert.Equal(atteso, RapportinoTurno.SpostaOra(orario, delta));

        [Fact]
        public void SpostaOra_ConservaIMinutiInvariati()
        {
            // Solo l'ora si sposta: i minuti (es. il "16:30" del Turno Centrale) non devono cambiare.
            Assert.Equal("20:30", RapportinoTurno.SpostaOra("16:30", 4));
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData("non un orario")]
        [InlineData("25:00")]  // ora fuori range
        [InlineData("10:70")]  // minuti fuori range
        public void SpostaOra_OrarioNonValidoOVuoto_RestituisceInvariato(string? orario) =>
            Assert.Equal(orario ?? string.Empty, RapportinoTurno.SpostaOra(orario, 4));

        [Fact]
        public void SottraiQuattroOreInizioCommand_AggiornaSoloOraInizio()
        {
            var rapportino = CreaRapportino();
            rapportino.OraInizio = "10:00";
            rapportino.OraFine = "18:00";

            rapportino.SottraiQuattroOreInizioCommand.Execute(null);

            Assert.Equal("06:00", rapportino.OraInizio);
            Assert.Equal("18:00", rapportino.OraFine);
        }

        [Fact]
        public void AggiungiQuattroOreFineCommand_AggiornaSoloOraFine()
        {
            var rapportino = CreaRapportino();
            rapportino.OraInizio = "06:00";
            rapportino.OraFine = "22:00";

            rapportino.AggiungiQuattroOreFineCommand.Execute(null);

            Assert.Equal("06:00", rapportino.OraInizio);
            Assert.Equal("02:00", rapportino.OraFine);
        }

        internal static RapportinoTurno CreaRapportino() => new(
            tipoTreno: "ETR 700",
            sottotitolo: "ETR 700 (da aggiornare durante il turno con verifica presso ufficio CT Hitachi)",
            fleetId: "700",
            destinatariKey: "ETR700",
            oggettoEmail: "Passaggio Consegne IMC AV Milano ETR 700");
    }
}
