using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using PersonalAutomationTool.Modules.PassaggioConsegne;
using PersonalAutomationTool.Modules.Verifiche;
using Xunit;

namespace PersonalAutomationTool.Tests.Modules.PassaggioConsegne
{
    /// <summary>
    /// Tier 1: il ViewModel del modulo PASSAGGIO CONSEGNE, con PDF, Outlook e finestre di dialogo
    /// sostituiti da implementazioni finte.
    ///
    /// <para>
    /// <b>Perché il flusso "Genera Mail" è verificabile qui.</b> Nella prima versione del modulo
    /// l'esportazione partiva da un gestore <c>Click</c> nel code-behind che catturava la vista WPF:
    /// non c'era modo di provarla senza aprire una finestra e Outlook. Ora PDF, posta e notifiche
    /// passano da tre interfacce iniettate, quindi la sequenza completa — cattura dello snapshot,
    /// generazione del PDF, apertura della bozza con oggetto e destinatari giusti — si verifica in
    /// memoria, in millisecondi.
    /// </para>
    /// </summary>
    public sealed class PassaggioConsegneViewModelTests
    {
        // ------------------------------------------------------------------
        // Struttura del modulo
        // ------------------------------------------------------------------

        [Fact]
        public void EspoleTreSchede_UnaPerFlotta()
        {
            var vm = CreaViewModel();

            Assert.Equal(["ETR 500", "ETR 700", "ETR 1000"], vm.Rapportini.Select(r => r.TipoTreno));
        }

        [Fact]
        public void AllApertura_LaPrimaSchedaEQuellaSelezionata() =>
            Assert.Equal("ETR 500", CreaViewModel().RapportinoSelezionato.TipoTreno);

        [Theory]
        [InlineData("ETR 500", "500", "E404P", "Passaggio Consegne IMC AV Milano ETR 500")]
        [InlineData("ETR 700", "700", "ETR700", "Passaggio Consegne IMC AV Milano ETR 700")]
        [InlineData("ETR 1000", "1000", "ETR1000", "Passaggio Consegne IMC AV Milano ETR 1000")]
        public void OgniSchedaHaFlottaDestinatariEOggettoCorretti(
            string tipoTreno, string fleetId, string destinatariKey, string oggetto)
        {
            var rapportino = CreaViewModel().Rapportini.Single(r => r.TipoTreno == tipoTreno);

            Assert.Equal(fleetId, rapportino.FleetId);
            Assert.Equal(destinatariKey, rapportino.DestinatariKey);
            Assert.Equal(oggetto, rapportino.OggettoEmail);
        }

        [Fact]
        public void LaSchedaEtr500UsaLaChiaveDestinatariE404P()
        {
            // La flotta si chiama "ETR 500" nell'interfaccia ma "E404P" in destinatari.json: è lo
            // scostamento di nomi descritto in §5.3 di PROJECT_MEMORY.md, e sbagliarlo significa
            // mandare il rapportino a nessuno.
            var etr500 = CreaViewModel().Rapportini.First();

            Assert.Equal("E404P", etr500.DestinatariKey);
        }

        // ------------------------------------------------------------------
        // Popolamento da VERIFICHE
        // ------------------------------------------------------------------

        [Fact]
        public void ApplicaVerifiche_UnaRigaPerTreno_ConLeLocoRaggruppate()
        {
            var rapportino = PassaggioConsegneModelsTests.CreaRapportino();
            var verifiche = new List<VerificheModel>
            {
                new() { Treno = "ETR700 12", Loco = "101", Avaria = "SSB" },
                new() { Treno = "ETR700 12", Loco = "102", Avaria = "Altro" },
                new() { Treno = "ETR700 15", Loco = "150", Avaria = "SSB" }
            };

            PassaggioConsegneViewModel.ApplicaVerifiche(rapportino, verifiche);

            Assert.Equal("ETR700 12", rapportino.Movimenti[0].Treno);
            Assert.Equal("101 - 102", rapportino.Movimenti[0].Loco);
            Assert.Equal("ETR700 15", rapportino.Movimenti[1].Treno);
            Assert.Equal("150", rapportino.Movimenti[1].Loco);
        }

        [Fact]
        public void ApplicaVerifiche_LeQuattroColonneDiMovimentoPartonoDaNo()
        {
            var rapportino = PassaggioConsegneModelsTests.CreaRapportino();

            PassaggioConsegneViewModel.ApplicaVerifiche(rapportino,
                [new VerificheModel { Treno = "ETR700 12", Loco = "101" }]);

            var riga = rapportino.Movimenti[0];
            Assert.Equal("No", riga.DataIngresso);
            Assert.Equal("No", riga.OraIngresso);
            Assert.Equal("No", riga.DataUscita);
            Assert.Equal("No", riga.OraUscita);
        }

        [Fact]
        public void ApplicaVerifiche_LeRigheInEccessoRestanoCompletamenteVuote()
        {
            // Le righe non usate non devono riempirsi di "No": nel PDF apparirebbero come attività
            // esistenti e mai movimentate.
            var rapportino = PassaggioConsegneModelsTests.CreaRapportino();

            PassaggioConsegneViewModel.ApplicaVerifiche(rapportino,
                [new VerificheModel { Treno = "ETR700 12", Loco = "101" }]);

            foreach (var riga in rapportino.Movimenti.Skip(1))
            {
                Assert.Equal(string.Empty, riga.Treno);
                Assert.Equal(string.Empty, riga.DataIngresso);
            }
        }

        [Fact]
        public void ApplicaVerifiche_MantieneLeDieciRigheDelTemplateAncheConPochiTreni()
        {
            var rapportino = PassaggioConsegneModelsTests.CreaRapportino();

            PassaggioConsegneViewModel.ApplicaVerifiche(rapportino,
                [new VerificheModel { Treno = "ETR700 12", Loco = "101" }]);

            Assert.Equal(10, rapportino.Movimenti.Count);
        }

        [Fact]
        public void ApplicaVerifiche_LaTabellaCresceSeITreniSuperanoLeRigheDelTemplate()
        {
            var rapportino = PassaggioConsegneModelsTests.CreaRapportino();
            var verifiche = Enumerable.Range(1, 14)
                .Select(i => new VerificheModel { Treno = $"ETR700 {i}", Loco = $"{100 + i}" })
                .ToList();

            PassaggioConsegneViewModel.ApplicaVerifiche(rapportino, verifiche);

            Assert.Equal(14, rapportino.Movimenti.Count);
            Assert.Equal(Enumerable.Range(1, 14), rapportino.Movimenti.Select(m => m.Numero));
        }

        [Fact]
        public void ApplicaVerifiche_TreniSenzaNomeVengonoIgnorati()
        {
            var rapportino = PassaggioConsegneModelsTests.CreaRapportino();

            PassaggioConsegneViewModel.ApplicaVerifiche(rapportino,
            [
                new VerificheModel { Treno = "   ", Loco = "999" },
                new VerificheModel { Treno = "ETR700 12", Loco = "101" }
            ]);

            Assert.Equal("ETR700 12", rapportino.Movimenti[0].Treno);
        }

        [Fact]
        public void ApplicaVerifiche_LocoDuplicateVengonoRaccolteUnaVoltaSola()
        {
            var rapportino = PassaggioConsegneModelsTests.CreaRapportino();

            PassaggioConsegneViewModel.ApplicaVerifiche(rapportino,
            [
                new VerificheModel { Treno = "ETR700 12", Loco = "101" },
                new VerificheModel { Treno = "ETR700 12", Loco = "101" }
            ]);

            Assert.Equal("101", rapportino.Movimenti[0].Loco);
        }

        [Fact]
        public void ApplicaVerifiche_ElencoNullo_NonSollevaEccezioni()
        {
            var rapportino = PassaggioConsegneModelsTests.CreaRapportino();

            PassaggioConsegneViewModel.ApplicaVerifiche(rapportino, null);

            Assert.Equal(10, rapportino.Movimenti.Count);
            Assert.All(rapportino.Movimenti, m => Assert.Equal(string.Empty, m.Treno));
        }

        // ------------------------------------------------------------------
        // Genera Mail
        // ------------------------------------------------------------------

        [Fact]
        public async Task GeneraMail_EsportaIlPdfEApreLaBozza()
        {
            var pdf = new PdfExporterFinto();
            var posta = new MailServiceFinto();
            var vm = CreaViewModel(pdf, posta);

            await vm.GeneraMailAsync();

            Assert.Equal(1, pdf.Chiamate);
            Assert.Equal(1, posta.Chiamate);
            Assert.Equal(pdf.PercorsoRestituito, posta.UltimoPercorsoPdf);
        }

        [Fact]
        public async Task GeneraMail_UsaOggettoEDestinatariDellaSchedaSelezionata()
        {
            var posta = new MailServiceFinto();
            var vm = CreaViewModel(mailService: posta);
            vm.RapportinoSelezionato = vm.Rapportini.Single(r => r.TipoTreno == "ETR 1000");

            await vm.GeneraMailAsync();

            Assert.Equal("Passaggio Consegne IMC AV Milano ETR 1000", posta.UltimoOggetto);
            Assert.Equal("ETR1000", posta.UltimaChiaveDestinatari);
        }

        [Fact]
        public async Task GeneraMail_LoSnapshotRiflettteIDatiCompilati()
        {
            var posta = new MailServiceFinto();
            var vm = CreaViewModel(mailService: posta);
            var rapportino = vm.RapportinoSelezionato;
            rapportino.Nome = "Alessio";
            rapportino.Cognome = "Bassetto";
            rapportino.TurnoSelezionato = TurnoPredefinito.Primo;
            rapportino.Interventi[0].TrenoLoco = "E404P 618";
            rapportino.Interventi[0].CompilazioneOdl = true;

            await vm.GeneraMailAsync();

            var snapshot = posta.UltimoSnapshot!;
            Assert.Equal("Alessio", snapshot.Nome);
            Assert.Equal("06:00", snapshot.OraInizio);
            Assert.Equal("14:00", snapshot.OraFine);
            Assert.Equal("Si", snapshot.Interventi[0].CompilazioneOdl);
            Assert.Equal("No", snapshot.Interventi[0].ChiusuraTicket);
        }

        [Fact]
        public async Task GeneraMail_LeRigheNonCompilateRestanoVuoteNelloSnapshot()
        {
            var posta = new MailServiceFinto();
            var vm = CreaViewModel(mailService: posta);
            // Una spunta su una riga senza TRENO-LOCO né DESCRIZIONE: nel PDF non deve produrre "Si".
            vm.RapportinoSelezionato.Interventi[2].CompReport = true;

            await vm.GeneraMailAsync();

            var riga = posta.UltimoSnapshot!.Interventi[2];
            Assert.Equal(string.Empty, riga.CompReport);
            Assert.Equal(string.Empty, riga.CompilazioneOdl);
        }

        [Fact]
        public async Task GeneraMail_ChiedeLoStatoEDoLoPassaAllaMail()
        {
            var posta = new MailServiceFinto();
            var statoDialog = new StatoTurnoDialogServiceFinto { Risposta = StatoTurno.AttivitaImminentiOInCorso };
            var vm = CreaViewModel(mailService: posta, statoTurnoDialog: statoDialog);

            await vm.GeneraMailAsync();

            Assert.Equal(1, statoDialog.Chiamate);
            Assert.Equal(StatoTurno.AttivitaImminentiOInCorso, posta.UltimoStato);
        }

        [Fact]
        public async Task GeneraMail_SeIlDialogDiStatoVieneAnnullato_NonGeneraNePdfNeEmail()
        {
            var pdf = new PdfExporterFinto();
            var posta = new MailServiceFinto();
            var statoDialog = new StatoTurnoDialogServiceFinto { Risposta = null };
            var vm = CreaViewModel(pdf, posta, statoTurnoDialog: statoDialog);

            await vm.GeneraMailAsync();

            Assert.Equal(1, statoDialog.Chiamate);
            Assert.Equal(0, pdf.Chiamate);
            Assert.Equal(0, posta.Chiamate);
        }

        [Fact]
        public async Task GeneraMail_SeIlPdfFallisce_NonSiApreAlcunaBozzaEVieneSegnalato()
        {
            var posta = new MailServiceFinto();
            var notifica = new NotificaFinta();
            var vm = CreaViewModel(new PdfExporterFinto { Eccezione = new InvalidOperationException("disco pieno") },
                posta, notifica);

            await vm.GeneraMailAsync();

            Assert.Equal(0, posta.Chiamate);
            Assert.Single(notifica.Errori);
            Assert.Contains("disco pieno", notifica.Errori[0]);
        }

        [Fact]
        public async Task GeneraMail_SeOutlookFallisce_IlPercorsoDelPdfVieneComunicato()
        {
            // Il turno non deve andare perso solo perché Outlook non è disponibile: il PDF esiste già
            // e il tecnico deve sapere dove trovarlo.
            var pdf = new PdfExporterFinto();
            var notifica = new NotificaFinta();
            var vm = CreaViewModel(pdf, new MailServiceFinto { Eccezione = new InvalidOperationException("Outlook assente") }, notifica);

            await vm.GeneraMailAsync();

            Assert.Single(notifica.Errori);
            Assert.Contains("Outlook assente", notifica.Errori[0]);
            Assert.Contains(pdf.PercorsoRestituito, notifica.Errori[0]);
        }

        [Fact]
        public async Task GeneraMail_AlTermineIlModuloNonRestaOccupato()
        {
            var vm = CreaViewModel();

            await vm.GeneraMailAsync();

            Assert.False(vm.IsBusy);
            Assert.Equal(string.Empty, vm.StatoOperazione);
        }

        [Fact]
        public async Task GeneraMail_NonModificaLoStatoDelRapportino()
        {
            // Nessun flag "modalità esportazione" da attivare sulla UI: è la proprietà che rende
            // impossibile lo sfarfallio inseguito nella prima versione del modulo (§6.1-undecies).
            var vm = CreaViewModel();
            var rapportino = vm.RapportinoSelezionato;
            rapportino.Interventi[0].TrenoLoco = "E404P 618";
            rapportino.Interventi[0].ChiusuraTicket = true;

            await vm.GeneraMailAsync();

            Assert.Equal("E404P 618", rapportino.Interventi[0].TrenoLoco);
            Assert.True(rapportino.Interventi[0].ChiusuraTicket);
        }

        // ------------------------------------------------------------------
        // Righe e reset
        // ------------------------------------------------------------------

        [Fact]
        public void AggiungiIntervento_AggiungeUnaRigaAllaSchedaSelezionata()
        {
            var vm = CreaViewModel();
            int prima = vm.RapportinoSelezionato.Interventi.Count;

            vm.AggiungiInterventoCommand.Execute(null);

            Assert.Equal(prima + 1, vm.RapportinoSelezionato.Interventi.Count);
        }

        [Fact]
        public void RimuoviIntervento_ConParametro_RimuoveLaRigaIndicata()
        {
            var vm = CreaViewModel();
            var riga = vm.RapportinoSelezionato.Interventi[1];
            riga.TrenoLoco = "da rimuovere";

            vm.RimuoviInterventoCommand.Execute(riga);

            Assert.DoesNotContain(riga, vm.RapportinoSelezionato.Interventi);
        }

        [Fact]
        public void AggiungiERimuoviInterventoNonSvolto_MantieneLaNumerazioneSenzaBuchi()
        {
            var vm = CreaViewModel();

            vm.AggiungiInterventoNonSvoltoCommand.Execute(null);
            vm.RimuoviInterventoNonSvoltoCommand.Execute(vm.RapportinoSelezionato.InterventiNonSvolti[3]);

            Assert.Equal(
                Enumerable.Range(1, vm.RapportinoSelezionato.InterventiNonSvolti.Count),
                vm.RapportinoSelezionato.InterventiNonSvolti.Select(n => n.Numero));
        }

        [Fact]
        public void Reset_Confermato_SvuotaLaSchedaERiportaLaDataAOggi()
        {
            var vm = CreaViewModel(notifica: new NotificaFinta { RispostaConferma = true });
            var r = vm.RapportinoSelezionato;
            r.Nome = "Alessio";
            r.Data = new DateTime(2020, 1, 1);
            r.Interventi[0].TrenoLoco = "E404P 618";

            vm.ResetCommand.Execute(null);

            Assert.Equal(string.Empty, r.Nome);
            Assert.Equal(DateTime.Today, r.Data);
            Assert.Equal(string.Empty, r.Interventi[0].TrenoLoco);
        }

        [Fact]
        public void Reset_Annullato_NonToccaNulla()
        {
            var vm = CreaViewModel(notifica: new NotificaFinta { RispostaConferma = false });
            vm.RapportinoSelezionato.Nome = "Alessio";

            vm.ResetCommand.Execute(null);

            Assert.Equal("Alessio", vm.RapportinoSelezionato.Nome);
        }

        [Fact]
        public void Reset_AgisceSoloSullaSchedaSelezionata()
        {
            var vm = CreaViewModel(notifica: new NotificaFinta { RispostaConferma = true });
            vm.Rapportini[0].Nome = "Primo";
            vm.Rapportini[1].Nome = "Secondo";
            vm.RapportinoSelezionato = vm.Rapportini[0];

            vm.ResetCommand.Execute(null);

            Assert.Equal(string.Empty, vm.Rapportini[0].Nome);
            Assert.Equal("Secondo", vm.Rapportini[1].Nome);
        }

        // ------------------------------------------------------------------
        // Supporto
        // ------------------------------------------------------------------

        private static PassaggioConsegneViewModel CreaViewModel(
            IRapportinoPdfExporter? pdfExporter = null,
            IRapportinoMailService? mailService = null,
            INotificaUtente? notifica = null,
            IStatoTurnoDialogService? statoTurnoDialog = null,
            Func<string, IReadOnlyList<VerificheModel>?>? leggiVerifiche = null) =>
            new(pdfExporter ?? new PdfExporterFinto(),
                mailService ?? new MailServiceFinto(),
                notifica ?? new NotificaFinta(),
                statoTurnoDialog ?? new StatoTurnoDialogServiceFinto(),
                leggiVerifiche ?? (_ => null),
                caricaVerificheAllAvvio: false);

        private sealed class PdfExporterFinto : IRapportinoPdfExporter
        {
            public string PercorsoRestituito { get; init; } = @"C:\temp\rapportino.pdf";
            public Exception? Eccezione { get; init; }
            public int Chiamate { get; private set; }

            public string Esporta(RapportinoSnapshot rapportino)
            {
                Chiamate++;
                if (Eccezione != null) throw Eccezione;
                return PercorsoRestituito;
            }
        }

        private sealed class MailServiceFinto : IRapportinoMailService
        {
            public Exception? Eccezione { get; init; }
            public int Chiamate { get; private set; }
            public RapportinoSnapshot? UltimoSnapshot { get; private set; }
            public string? UltimoPercorsoPdf { get; private set; }
            public string? UltimaChiaveDestinatari { get; private set; }
            public string? UltimoOggetto { get; private set; }
            public StatoTurno? UltimoStato { get; private set; }

            public void ApriBozza(RapportinoSnapshot rapportino, string percorsoPdf, string destinatariKey, string oggetto, StatoTurno stato)
            {
                Chiamate++;
                UltimoSnapshot = rapportino;
                UltimoPercorsoPdf = percorsoPdf;
                UltimaChiaveDestinatari = destinatariKey;
                UltimoOggetto = oggetto;
                UltimoStato = stato;
                if (Eccezione != null) throw Eccezione;
            }
        }

        private sealed class StatoTurnoDialogServiceFinto : IStatoTurnoDialogService
        {
            /// <summary>Risposta simulata; <c>null</c> riproduce "Annulla" o la chiusura del dialog.</summary>
            public StatoTurno? Risposta { get; init; } = StatoTurno.NessunaAttivita;
            public int Chiamate { get; private set; }

            public StatoTurno? ChiediStato()
            {
                Chiamate++;
                return Risposta;
            }
        }

        private sealed class NotificaFinta : INotificaUtente
        {
            public bool RispostaConferma { get; init; } = true;
            public List<string> Errori { get; } = [];
            public List<string> Informazioni { get; } = [];

            public void Errore(string messaggio, string titolo) => Errori.Add(messaggio);
            public void Informazione(string messaggio, string titolo) => Informazioni.Add(messaggio);
            public bool Conferma(string messaggio, string titolo) => RispostaConferma;
        }
    }
}
