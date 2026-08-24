using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using PersonalAutomationTool.Core;

namespace PersonalAutomationTool.Modules.PassaggioConsegne
{
    /// <summary>
    /// Un turno predefinito con i suoi orari di inizio e fine. Selezionandolo dalla ComboBox
    /// dell'intestazione, <see cref="RapportinoTurno.TurnoSelezionato"/> applica automaticamente
    /// <see cref="OraInizio"/> e <see cref="OraFine"/> ai due campi corrispondenti.
    /// <para>
    /// Tipo immutabile e senza dipendenze da WPF: gli orari sono verificati da xUnit
    /// (<c>TurnoPredefinitoTests</c>) invece di essere sparsi in uno <c>switch</c> dentro un setter,
    /// com'era nella prima versione del modulo.
    /// </para>
    /// </summary>
    public sealed record TurnoPredefinito(string Nome, string OraInizio, string OraFine)
    {
        public static readonly TurnoPredefinito Primo = new("1° Turno", "06:00", "14:00");
        public static readonly TurnoPredefinito Centrale = new("Turno Centrale", "08:00", "16:30");
        public static readonly TurnoPredefinito Secondo = new("2° Turno", "14:00", "22:00");
        public static readonly TurnoPredefinito Terzo = new("3° Turno", "22:00", "06:00");

        /// <summary>I quattro turni, nell'ordine in cui compaiono nella ComboBox.</summary>
        public static IReadOnlyList<TurnoPredefinito> Tutti { get; } =
            [Primo, Centrale, Secondo, Terzo];
    }

    /// <summary>
    /// Regola di stampa delle colonne a checkbox, condivisa fra le tabelle "Dettaglio interventi" e
    /// "Interventi non svolti".
    ///
    /// <para>
    /// <b>Perché è una funzione pura e non un <c>IValueConverter</c>.</b> Il PDF non è più una
    /// fotografia della finestra (<c>RenderTargetBitmap</c>) ma è disegnato in grafica vettoriale da
    /// <see cref="PassaggioConsegnePdfExporter"/>: non c'è alcun binding XAML di mezzo nel percorso di
    /// esportazione, quindi la conversione non deve passare per il motore di binding di WPF. Tenerla
    /// qui la rende anche verificabile da xUnit senza WPF, che è il motivo per cui la regola è coperta
    /// da test invece che ispezionata a occhio su un PDF.
    /// </para>
    /// </summary>
    public static class SiNoCell
    {
        /// <summary>
        /// Testo da stampare nel PDF per una colonna a checkbox: <c>"Si"</c>/<c>"No"</c> se la riga è
        /// compilata, <b>stringa vuota</b> se la riga non lo è — una riga mai usata non deve
        /// riempirsi di "No" in tutte le colonne booleane.
        /// </summary>
        public static string PerPdf(bool spuntata, bool rigaCompilata) =>
            rigaCompilata ? (spuntata ? "Si" : "No") : string.Empty;
    }

    /// <summary>
    /// Riga della tabella "ATTIVITA' RICHIESTE DA INGEGNERIA HR STS" (righe 6-15 del template Excel).
    /// Treno e Loco arrivano dai file VERIFICHE; le quattro colonne di ingresso/uscita nascono a
    /// <c>"No"</c> e restano modificabili a mano se il treno entra o esce durante il turno.
    /// </summary>
    public sealed class MovimentoTrenoRow : ViewModelBase
    {
        /// <summary>Valore iniziale delle 4 colonne data/ora sulle righe popolate da VERIFICHE.</summary>
        public const string NonMovimentato = "No";

        private int _numero;
        public int Numero
        {
            get => _numero;
            set => SetProperty(ref _numero, value);
        }

        private string _treno = string.Empty;
        public string Treno
        {
            get => _treno;
            set => SetProperty(ref _treno, value);
        }

        private string _loco = string.Empty;
        public string Loco
        {
            get => _loco;
            set => SetProperty(ref _loco, value);
        }

        private string _dataIngresso = string.Empty;
        public string DataIngresso
        {
            get => _dataIngresso;
            set => SetProperty(ref _dataIngresso, value);
        }

        private string _oraIngresso = string.Empty;
        public string OraIngresso
        {
            get => _oraIngresso;
            set => SetProperty(ref _oraIngresso, value);
        }

        private string _dataUscita = string.Empty;
        public string DataUscita
        {
            get => _dataUscita;
            set => SetProperty(ref _dataUscita, value);
        }

        private string _oraUscita = string.Empty;
        public string OraUscita
        {
            get => _oraUscita;
            set => SetProperty(ref _oraUscita, value);
        }

        /// <summary>Svuota la riga riportandola allo stato "mai compilata".</summary>
        public void Svuota()
        {
            Treno = string.Empty;
            Loco = string.Empty;
            DataIngresso = string.Empty;
            OraIngresso = string.Empty;
            DataUscita = string.Empty;
            OraUscita = string.Empty;
        }

        /// <summary>
        /// Popola la riga con i dati di VERIFICHE, portando le 4 colonne data/ora al valore iniziale
        /// <see cref="NonMovimentato"/>.
        /// </summary>
        public void PopolaDaVerifiche(string treno, string loco)
        {
            Treno = treno;
            Loco = loco;
            DataIngresso = NonMovimentato;
            OraIngresso = NonMovimentato;
            DataUscita = NonMovimentato;
            OraUscita = NonMovimentato;
        }
    }

    /// <summary>
    /// Riga della tabella "DETTAGLIO INTERVENTI (CORRETIVA, PREVENTIVA, RICHIESTE INGEGNERIA)"
    /// (righe 18-22 del template Excel). Le 5 colonne booleane sono checkbox a schermo e
    /// <c>"Si"</c>/<c>"No"</c>/vuoto nel PDF, secondo <see cref="SiNoCell.PerPdf"/>.
    /// </summary>
    public sealed class DettaglioInterventoRow : ViewModelBase
    {
        private string _trenoLoco = string.Empty;
        public string TrenoLoco
        {
            get => _trenoLoco;
            set
            {
                if (SetProperty(ref _trenoLoco, value)) OnPropertyChanged(nameof(IsCompilata));
            }
        }

        private string _descrizione = string.Empty;
        public string Descrizione
        {
            get => _descrizione;
            set
            {
                if (SetProperty(ref _descrizione, value)) OnPropertyChanged(nameof(IsCompilata));
            }
        }

        private bool _compilazioneOdl;
        public bool CompilazioneOdl
        {
            get => _compilazioneOdl;
            set => SetProperty(ref _compilazioneOdl, value);
        }

        private bool _chiusuraTicket;
        public bool ChiusuraTicket
        {
            get => _chiusuraTicket;
            set => SetProperty(ref _chiusuraTicket, value);
        }

        private bool _compReport;
        public bool CompReport
        {
            get => _compReport;
            set => SetProperty(ref _compReport, value);
        }

        private bool _emailIngegneria;
        public bool EmailIngegneria
        {
            get => _emailIngegneria;
            set => SetProperty(ref _emailIngegneria, value);
        }

        private bool _aggiornareVerifiche;
        public bool AggiornareVerifiche
        {
            get => _aggiornareVerifiche;
            set => SetProperty(ref _aggiornareVerifiche, value);
        }

        /// <summary>
        /// Una riga conta come compilata quando ha un TRENO-LOCO <b>o</b> una DESCRIZIONE. Le sole
        /// checkbox non bastano: spuntarne una per sbaglio su una riga altrimenti vuota non deve
        /// far comparire una riga di "No" nel PDF.
        /// </summary>
        public bool IsCompilata =>
            !string.IsNullOrWhiteSpace(TrenoLoco) || !string.IsNullOrWhiteSpace(Descrizione);

        public void Svuota()
        {
            TrenoLoco = string.Empty;
            Descrizione = string.Empty;
            CompilazioneOdl = false;
            ChiusuraTicket = false;
            CompReport = false;
            EmailIngegneria = false;
            AggiornareVerifiche = false;
        }
    }

    /// <summary>
    /// Riga della tabella "INTERVENTI RICHIESTI DA INGEGNERIA NON SVOLTI SU TRENI IN CANTIERE"
    /// (righe 25-34 del template Excel).
    /// </summary>
    public sealed class InterventoNonSvoltoRow : ViewModelBase
    {
        private int _numero;
        public int Numero
        {
            get => _numero;
            set => SetProperty(ref _numero, value);
        }

        private string _trenoLoco = string.Empty;
        public string TrenoLoco
        {
            get => _trenoLoco;
            set
            {
                if (SetProperty(ref _trenoLoco, value)) OnPropertyChanged(nameof(IsCompilata));
            }
        }

        private string _motivazione = string.Empty;
        public string Motivazione
        {
            get => _motivazione;
            set
            {
                if (SetProperty(ref _motivazione, value)) OnPropertyChanged(nameof(IsCompilata));
            }
        }

        private string _oraRichiesta = string.Empty;
        public string OraRichiesta
        {
            get => _oraRichiesta;
            set
            {
                if (SetProperty(ref _oraRichiesta, value)) OnPropertyChanged(nameof(IsCompilata));
            }
        }

        private string _referente = string.Empty;
        public string Referente
        {
            get => _referente;
            set
            {
                if (SetProperty(ref _referente, value)) OnPropertyChanged(nameof(IsCompilata));
            }
        }

        private bool _inviataEmailIngegneria;
        public bool InviataEmailIngegneria
        {
            get => _inviataEmailIngegneria;
            set => SetProperty(ref _inviataEmailIngegneria, value);
        }

        private bool _passaggioConsegna;
        public bool PassaggioConsegna
        {
            get => _passaggioConsegna;
            set => SetProperty(ref _passaggioConsegna, value);
        }

        /// <summary>
        /// Compilata se <b>uno qualsiasi</b> dei quattro campi di testo è valorizzato — non solo
        /// TRENO-LOCO e MOTIVAZIONE. Interpretazione deliberatamente più larga di quella della
        /// tabella "Dettaglio interventi": qui una riga con solo ORA RICHIESTA e REFERENTE compilati
        /// è chiaramente in uso, e stamparne vuote le due colonne booleane sembrerebbe un difetto.
        /// </summary>
        public bool IsCompilata =>
            !string.IsNullOrWhiteSpace(TrenoLoco) ||
            !string.IsNullOrWhiteSpace(Motivazione) ||
            !string.IsNullOrWhiteSpace(OraRichiesta) ||
            !string.IsNullOrWhiteSpace(Referente);

        public void Svuota()
        {
            TrenoLoco = string.Empty;
            Motivazione = string.Empty;
            OraRichiesta = string.Empty;
            Referente = string.Empty;
            InviataEmailIngegneria = false;
            PassaggioConsegna = false;
        }
    }

    /// <summary>
    /// Il rapportino di una singola flotta: una scheda del TabControl, un foglio del template Excel.
    ///
    /// <para>
    /// <b>Stato volatile per scelta.</b> Nessun salvataggio su disco: il rapportino vive per la durata
    /// della sessione e riparte vuoto a ogni avvio dell'applicazione. La prima versione del modulo
    /// serializzava tutto in <c>data\passaggio_consegne.json</c>, e da lì nasceva il difetto della
    /// data "congelata" all'ultimo salvataggio (§6.1-undecies di PROJECT_MEMORY.md): senza
    /// persistenza quel difetto non è più possibile per costruzione.
    /// </para>
    /// </summary>
    public sealed class RapportinoTurno : ViewModelBase
    {
        /// <summary>Righe della tabella movimenti nel template Excel (righe 6-15).</summary>
        public const int RigheMovimenti = 10;

        /// <summary>Righe della tabella dettaglio interventi nel template Excel (righe 18-22).</summary>
        public const int RigheInterventi = 5;

        /// <summary>Righe della tabella interventi non svolti nel template Excel (righe 25-34).</summary>
        public const int RigheInterventiNonSvolti = 10;

        /// <summary>Etichetta della scheda e della flotta, es. <c>"ETR 500"</c>.</summary>
        public string TipoTreno { get; }

        /// <summary>Riga 4 del foglio Excel, sotto l'intestazione della prima tabella.</summary>
        public string Sottotitolo { get; }

        /// <summary>
        /// Identificatore accettato da <c>VerificheViewModel.GetVerificheForFleetStatic</c>:
        /// <c>"500"</c>, <c>"700"</c> o <c>"1000"</c>.
        /// </summary>
        public string FleetId { get; }

        /// <summary>
        /// Chiave con cui cercare i destinatari in <c>destinatari.json</c> via
        /// <c>DestinatariManager.GetRecipients</c>.
        /// </summary>
        public string DestinatariKey { get; }

        /// <summary>Oggetto della bozza Outlook generata dal pulsante "Genera Mail".</summary>
        public string OggettoEmail { get; }

        private string _nome = string.Empty;
        public string Nome
        {
            get => _nome;
            set => SetProperty(ref _nome, value);
        }

        private string _cognome = string.Empty;
        public string Cognome
        {
            get => _cognome;
            set => SetProperty(ref _cognome, value);
        }

        private DateTime _data = DateTime.Today;
        public DateTime Data
        {
            get => _data;
            set => SetProperty(ref _data, value);
        }

        private TurnoPredefinito? _turnoSelezionato;
        /// <summary>
        /// Selezionare un turno riscrive <see cref="OraInizio"/> e <see cref="OraFine"/>. Gli orari
        /// restano comunque modificabili a mano dopo la selezione: la ComboBox è una scorciatoia,
        /// non un vincolo.
        /// </summary>
        public TurnoPredefinito? TurnoSelezionato
        {
            get => _turnoSelezionato;
            set
            {
                if (SetProperty(ref _turnoSelezionato, value) && value != null)
                {
                    OraInizio = value.OraInizio;
                    OraFine = value.OraFine;
                }
            }
        }

        private string _oraInizio = string.Empty;
        public string OraInizio
        {
            get => _oraInizio;
            set => SetProperty(ref _oraInizio, value);
        }

        private string _oraFine = string.Empty;
        public string OraFine
        {
            get => _oraFine;
            set => SetProperty(ref _oraFine, value);
        }

        public ObservableCollection<MovimentoTrenoRow> Movimenti { get; } = [];
        public ObservableCollection<DettaglioInterventoRow> Interventi { get; } = [];
        public ObservableCollection<InterventoNonSvoltoRow> InterventiNonSvolti { get; } = [];

        public RapportinoTurno(string tipoTreno, string sottotitolo, string fleetId, string destinatariKey, string oggettoEmail)
        {
            TipoTreno = tipoTreno;
            Sottotitolo = sottotitolo;
            FleetId = fleetId;
            DestinatariKey = destinatariKey;
            OggettoEmail = oggettoEmail;

            for (int i = 1; i <= RigheMovimenti; i++)
                Movimenti.Add(new MovimentoTrenoRow { Numero = i });

            for (int i = 0; i < RigheInterventi; i++)
                Interventi.Add(new DettaglioInterventoRow());

            for (int i = 1; i <= RigheInterventiNonSvolti; i++)
                InterventiNonSvolti.Add(new InterventoNonSvoltoRow { Numero = i });
        }

        /// <summary>
        /// Rinumera la colonna N° delle due tabelle numerate dopo un'aggiunta o una rimozione, così
        /// che la numerazione resti 1..N senza buchi.
        /// </summary>
        public void RinumeraRighe()
        {
            for (int i = 0; i < Movimenti.Count; i++) Movimenti[i].Numero = i + 1;
            for (int i = 0; i < InterventiNonSvolti.Count; i++) InterventiNonSvolti[i].Numero = i + 1;
        }
    }
}
