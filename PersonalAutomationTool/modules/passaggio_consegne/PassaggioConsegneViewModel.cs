using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using PersonalAutomationTool.Core;
using PersonalAutomationTool.Modules.Verifiche;

namespace PersonalAutomationTool.Modules.PassaggioConsegne
{
    /// <summary>
    /// ViewModel del modulo PASSAGGIO CONSEGNE: tre rapportini di turno (ETR 500, ETR 700, ETR 1000),
    /// uno per scheda del <c>TabControl</c>, ciascuno modellato sull'omonimo foglio del template
    /// Excel "rapportino di turno.xlsx".
    ///
    /// <para>
    /// <b>Dipendenze iniettabili.</b> PDF, posta e messaggi all'utente passano da tre interfacce
    /// (<see cref="IRapportinoPdfExporter"/>, <see cref="IRapportinoMailService"/>,
    /// <see cref="INotificaUtente"/>) e i dati VERIFICHE da una funzione. Il costruttore senza
    /// argomenti — quello usato dal XAML — collega le implementazioni reali; quello interno permette
    /// a xUnit di verificare l'intero flusso "Genera Mail" senza aprire Outlook, senza scrivere su
    /// disco e senza far comparire finestre modali.
    /// </para>
    ///
    /// <para>
    /// <b>Stato volatile.</b> Nessuna persistenza: il modulo riparte vuoto a ogni avvio
    /// dell'applicazione, come richiesto. Entro la sessione lo stato sopravvive alla navigazione fra
    /// moduli perché <c>MainWindow</c> tiene in cache una sola istanza per vista (§2.3, §5.8).
    /// </para>
    /// </summary>
    public sealed class PassaggioConsegneViewModel : ViewModelBase
    {
        private readonly IRapportinoPdfExporter _pdfExporter;
        private readonly IRapportinoMailService _mailService;
        private readonly INotificaUtente _notifica;
        private readonly Func<string, IReadOnlyList<VerificheModel>?> _leggiVerifiche;

        public ObservableCollection<RapportinoTurno> Rapportini { get; }

        /// <summary>I quattro turni proposti dalla ComboBox dell'intestazione.</summary>
        public IReadOnlyList<TurnoPredefinito> Turni => TurnoPredefinito.Tutti;

        private RapportinoTurno _rapportinoSelezionato;
        public RapportinoTurno RapportinoSelezionato
        {
            get => _rapportinoSelezionato;
            set => SetProperty(ref _rapportinoSelezionato, value);
        }

        private bool _isBusy;
        /// <summary>Vero durante la generazione del PDF: la vista mostra un overlay di attesa.</summary>
        public bool IsBusy
        {
            get => _isBusy;
            set => SetProperty(ref _isBusy, value);
        }

        private string _statoOperazione = string.Empty;
        public string StatoOperazione
        {
            get => _statoOperazione;
            set => SetProperty(ref _statoOperazione, value);
        }

        public ICommand GeneraMailCommand { get; }
        public ICommand AggiungiInterventoCommand { get; }
        public ICommand RimuoviInterventoCommand { get; }
        public ICommand AggiungiInterventoNonSvoltoCommand { get; }
        public ICommand RimuoviInterventoNonSvoltoCommand { get; }
        public ICommand AggiornaDaVerificheCommand { get; }
        public ICommand ResetCommand { get; }

        /// <summary>Costruttore usato dal XAML: collega le implementazioni di produzione.</summary>
        public PassaggioConsegneViewModel()
            : this(new RapportinoPdfExporter(),
                   new OutlookRapportinoMailService(),
                   new MessageBoxNotificaUtente(),
                   VerificheViewModel.GetVerificheForFleetStatic,
                   caricaVerificheAllAvvio: true)
        {
        }

        internal PassaggioConsegneViewModel(
            IRapportinoPdfExporter pdfExporter,
            IRapportinoMailService mailService,
            INotificaUtente notifica,
            Func<string, IReadOnlyList<VerificheModel>?> leggiVerifiche,
            bool caricaVerificheAllAvvio)
        {
            _pdfExporter = pdfExporter;
            _mailService = mailService;
            _notifica = notifica;
            _leggiVerifiche = leggiVerifiche;

            Rapportini =
            [
                new RapportinoTurno(
                    tipoTreno: "ETR 500",
                    sottotitolo: "ETR 500 (da aggiornare durante il turno con verifica presso ufficio CT Trenitalia)",
                    fleetId: "500",
                    destinatariKey: "E404P",
                    oggettoEmail: "Passaggio Consegne IMC AV Milano ETR 500"),
                new RapportinoTurno(
                    tipoTreno: "ETR 700",
                    sottotitolo: "ETR 700 (da aggiornare durante il turno con verifica presso ufficio CT Hitachi)",
                    fleetId: "700",
                    destinatariKey: "ETR700",
                    oggettoEmail: "Passaggio Consegne IMC AV Milano ETR 700"),
                new RapportinoTurno(
                    tipoTreno: "ETR 1000",
                    sottotitolo: "ETR 1000 (da aggiornare durante il turno con verifica presso ufficio CT Hitachi)",
                    fleetId: "1000",
                    destinatariKey: "ETR1000",
                    oggettoEmail: "Passaggio Consegne IMC AV Milano ETR 1000")
            ];
            _rapportinoSelezionato = Rapportini[0];

            GeneraMailCommand = new RelayCommand(async _ => await GeneraMailAsync(), _ => !IsBusy);
            AggiungiInterventoCommand = new RelayCommand(_ =>
                RapportinoSelezionato.Interventi.Add(new DettaglioInterventoRow()));
            RimuoviInterventoCommand = new RelayCommand(RimuoviIntervento);
            AggiungiInterventoNonSvoltoCommand = new RelayCommand(_ =>
            {
                RapportinoSelezionato.InterventiNonSvolti.Add(new InterventoNonSvoltoRow());
                RapportinoSelezionato.RinumeraRighe();
            });
            RimuoviInterventoNonSvoltoCommand = new RelayCommand(RimuoviInterventoNonSvolto);
            AggiornaDaVerificheCommand = new RelayCommand(async _ => await AggiornaDaVerificheAsync());
            ResetCommand = new RelayCommand(_ => Reset());

            if (caricaVerificheAllAvvio)
            {
                // Fire-and-forget: al primo avvio VerificheViewModel.Instance è ancora null e la
                // lettura comporta la scansione di tre alberi OneDrive (criticità F, §6.4). La vista
                // si apre subito e le tabelle si popolano appena i dati sono pronti.
                _ = CaricaVerificheAsync(silenzioso: true);
            }
        }

        // ------------------------------------------------------------------
        // VERIFICHE
        // ------------------------------------------------------------------

        /// <summary>
        /// Ricarica la tabella movimenti di tutti e tre i rapportini dai file VERIFICHE.
        ///
        /// <para>
        /// <b>Non c'è alcuna sottoscrizione automatica a <c>VerificheViewModel.OnVerificheDataUpdated</c>,
        /// ed è deliberato.</b> La prima versione del modulo si agganciava a quell'evento statico e
        /// riscriveva la tabella a ogni cambiamento sui file di flotta: nel mezzo di un turno questo
        /// sovrascrive senza preavviso le date e gli orari di ingresso/uscita appena annotati a mano
        /// dal tecnico. Qui l'aggiornamento è un gesto esplicito, e la sottoscrizione mai rilasciata
        /// che era la criticità <b>D</b> di §6.4 non esiste più.
        /// </para>
        /// </summary>
        private async Task AggiornaDaVerificheAsync()
        {
            bool procedi = _notifica.Conferma(
                "I dati di TRENO e LOCO verranno ricaricati dai file VERIFICHE.\n\n" +
                "Le date e gli orari di ingresso/uscita inseriti a mano nella tabella " +
                "\"Attività richieste da ingegneria\" andranno persi.\n\nProcedere?",
                "Aggiorna da VERIFICHE");

            if (!procedi) return;

            await CaricaVerificheAsync(silenzioso: false);
        }

        private async Task CaricaVerificheAsync(bool silenzioso)
        {
            try
            {
                IsBusy = true;
                StatoOperazione = "Lettura dei file VERIFICHE in corso...";

                // La lettura può enumerare alberi OneDrive e aprire file Excel: mai sul thread UI.
                var letture = await Task.Run(() => Rapportini
                    .Select(r => (Rapportino: r, Righe: LeggiInSicurezza(r.FleetId)))
                    .ToList())
                    .ConfigureAwait(true); // si prosegue sul dispatcher: sotto si mutano ObservableCollection

                foreach (var (rapportino, righe) in letture)
                {
                    ApplicaVerifiche(rapportino, righe);
                }
            }
            catch (Exception ex)
            {
                if (!silenzioso)
                {
                    _notifica.Errore($"Impossibile leggere i dati delle VERIFICHE:\n{ex.Message}",
                        "Aggiorna da VERIFICHE");
                }
            }
            finally
            {
                IsBusy = false;
                StatoOperazione = string.Empty;
            }
        }

        private IReadOnlyList<VerificheModel>? LeggiInSicurezza(string fleetId)
        {
            try
            {
                return _leggiVerifiche(fleetId);
            }
            catch (Exception ex)
            {
                // Una flotta illeggibile (cartella OneDrive non sincronizzata, file aperto in Excel)
                // non deve impedire il caricamento delle altre due.
                System.Diagnostics.Debug.WriteLine($"VERIFICHE flotta {fleetId} non leggibili: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Riporta nella tabella movimenti i treni presenti nelle VERIFICHE della flotta: una riga per
        /// treno, con tutte le sue locomotive raccolte in un'unica cella, e le quattro colonne di
        /// ingresso/uscita a <c>"No"</c>.
        ///
        /// <para>
        /// Le righe che avanzano restano vuote, così la tabella conserva l'aspetto del template Excel
        /// (10 righe numerate) anche quando i treni in cantiere sono meno.
        /// </para>
        /// </summary>
        internal static void ApplicaVerifiche(RapportinoTurno rapportino, IReadOnlyList<VerificheModel>? verifiche)
        {
            var perTreno = (verifiche ?? [])
                .Where(v => !string.IsNullOrWhiteSpace(v.Treno))
                .GroupBy(v => v.Treno.Trim(), StringComparer.OrdinalIgnoreCase)
                .ToList();

            // Servono almeno le righe del template; se i treni sono di più la tabella cresce.
            int righeNecessarie = Math.Max(RapportinoTurno.RigheMovimenti, perTreno.Count);
            while (rapportino.Movimenti.Count < righeNecessarie)
                rapportino.Movimenti.Add(new MovimentoTrenoRow());

            for (int i = 0; i < rapportino.Movimenti.Count; i++)
            {
                if (i < perTreno.Count)
                {
                    var gruppo = perTreno[i];
                    string locomotive = string.Join(" - ", gruppo
                        .Select(v => v.Loco?.Trim())
                        .Where(l => !string.IsNullOrWhiteSpace(l))
                        .Distinct(StringComparer.OrdinalIgnoreCase));

                    rapportino.Movimenti[i].PopolaDaVerifiche(gruppo.Key, locomotive);
                }
                else
                {
                    rapportino.Movimenti[i].Svuota();
                }
            }

            rapportino.RinumeraRighe();
        }

        // ------------------------------------------------------------------
        // Righe delle tabelle 2 e 3
        // ------------------------------------------------------------------

        private void RimuoviIntervento(object? parametro)
        {
            var righe = RapportinoSelezionato.Interventi;
            if (parametro is DettaglioInterventoRow riga) righe.Remove(riga);
            else if (righe.Count > 0) righe.RemoveAt(righe.Count - 1);
        }

        private void RimuoviInterventoNonSvolto(object? parametro)
        {
            var righe = RapportinoSelezionato.InterventiNonSvolti;
            if (parametro is InterventoNonSvoltoRow riga) righe.Remove(riga);
            else if (righe.Count > 0) righe.RemoveAt(righe.Count - 1);
            RapportinoSelezionato.RinumeraRighe();
        }

        private void Reset()
        {
            if (!_notifica.Conferma(
                    $"Azzerare il rapportino {RapportinoSelezionato.TipoTreno}?\n\n" +
                    "Verranno svuotate tutte le tabelle e i dati dell'operatore.",
                    "Conferma reset"))
            {
                return;
            }

            var r = RapportinoSelezionato;
            r.Nome = string.Empty;
            r.Cognome = string.Empty;
            r.Data = DateTime.Today;
            r.TurnoSelezionato = null;
            r.OraInizio = string.Empty;
            r.OraFine = string.Empty;

            foreach (var m in r.Movimenti) m.Svuota();
            foreach (var i in r.Interventi) i.Svuota();
            foreach (var n in r.InterventiNonSvolti) n.Svuota();
        }

        // ------------------------------------------------------------------
        // Genera Mail
        // ------------------------------------------------------------------

        /// <summary>
        /// Esporta il PDF del rapportino selezionato e apre la bozza Outlook con l'allegato.
        ///
        /// <para>
        /// L'ordine dei passi non è casuale: lo <see cref="RapportinoSnapshot"/> è catturato sul thread
        /// UI, il PDF è disegnato su thread pool a partire da quella copia immutabile, e solo la
        /// chiamata a Outlook torna sul dispatcher (COM verso un server STA). Così la UI resta
        /// reattiva, il disegno non legge collezioni che l'utente potrebbe stare modificando, e
        /// nessuna proprietà del ViewModel viene alterata per il solo scopo di "preparare" la stampa —
        /// che era l'origine dello sfarfallio del vecchio modulo (§6.1-undecies).
        /// </para>
        /// </summary>
        internal async Task GeneraMailAsync()
        {
            if (IsBusy) return;

            var rapportino = RapportinoSelezionato;
            RapportinoSnapshot snapshot = RapportinoSnapshot.Cattura(rapportino);

            string percorsoPdf;
            try
            {
                IsBusy = true;
                StatoOperazione = "Generazione del PDF in corso...";

                percorsoPdf = await Task.Run(() => _pdfExporter.Esporta(snapshot)).ConfigureAwait(true);
            }
            catch (Exception ex)
            {
                _notifica.Errore($"Impossibile generare il PDF del rapportino:\n{ex.Message}", "Genera Mail");
                return;
            }
            finally
            {
                IsBusy = false;
                StatoOperazione = string.Empty;
            }

            try
            {
                _mailService.ApriBozza(snapshot, percorsoPdf, rapportino.DestinatariKey, rapportino.OggettoEmail);
            }
            catch (Exception ex)
            {
                // Il PDF esiste comunque: il percorso viene comunicato, così il turno non è perso
                // anche se Outlook non è disponibile.
                _notifica.Errore(
                    $"{ex.Message}\n\nIl PDF del rapportino è disponibile in:\n{percorsoPdf}",
                    "Genera Mail");
            }
        }
    }
}
