using System.Collections.ObjectModel;
using PersonalAutomationTool.Core;

namespace PersonalAutomationTool.Modules.PassaggioConsegne
{
    public class MovimentoTrenoRow : ViewModelBase
    {
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
    }

    public class DettaglioInterventoRow : ViewModelBase
    {
        private string _trenoLoco = string.Empty;
        public string TrenoLoco
        {
            get => _trenoLoco;
            set => SetProperty(ref _trenoLoco, value);
        }

        private string _descrizione = string.Empty;
        public string Descrizione
        {
            get => _descrizione;
            set => SetProperty(ref _descrizione, value);
        }

        private bool _compilazioneOdlBool;
        public bool CompilazioneOdlBool
        {
            get => _compilazioneOdlBool;
            set
            {
                if (SetProperty(ref _compilazioneOdlBool, value))
                    OnPropertyChanged(nameof(CompilazioneOdl));
            }
        }
        public string CompilazioneOdl
        {
            get => CompilazioneOdlBool ? "SI" : "NO";
            set => CompilazioneOdlBool = (value == "SI");
        }

        private bool _chiusuraTicketBool;
        public bool ChiusuraTicketBool
        {
            get => _chiusuraTicketBool;
            set
            {
                if (SetProperty(ref _chiusuraTicketBool, value))
                    OnPropertyChanged(nameof(ChiusuraTicket));
            }
        }
        public string ChiusuraTicket
        {
            get => ChiusuraTicketBool ? "SI" : "NO";
            set => ChiusuraTicketBool = (value == "SI");
        }

        private bool _compReportBool;
        public bool CompReportBool
        {
            get => _compReportBool;
            set
            {
                if (SetProperty(ref _compReportBool, value))
                    OnPropertyChanged(nameof(CompReport));
            }
        }
        public string CompReport
        {
            get => CompReportBool ? "SI" : "NO";
            set => CompReportBool = (value == "SI");
        }

        private bool _emailIngegneriaBool;
        public bool EmailIngegneriaBool
        {
            get => _emailIngegneriaBool;
            set
            {
                if (SetProperty(ref _emailIngegneriaBool, value))
                    OnPropertyChanged(nameof(EmailIngegneria));
            }
        }
        public string EmailIngegneria
        {
            get => EmailIngegneriaBool ? "SI" : "NO";
            set => EmailIngegneriaBool = (value == "SI");
        }

        private bool _aggiornareVerificheBool;
        public bool AggiornareVerificheBool
        {
            get => _aggiornareVerificheBool;
            set
            {
                if (SetProperty(ref _aggiornareVerificheBool, value))
                    OnPropertyChanged(nameof(AggiornareVerifiche));
            }
        }
        public string AggiornareVerifiche
        {
            get => AggiornareVerificheBool ? "SI" : "NO";
            set => AggiornareVerificheBool = (value == "SI");
        }
    }

    public class InterventoNonSvoltoRow : ViewModelBase
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
            set => SetProperty(ref _trenoLoco, value);
        }

        private string _motivazione = string.Empty;
        public string Motivazione
        {
            get => _motivazione;
            set => SetProperty(ref _motivazione, value);
        }

        private string _oraRichiesta = string.Empty;
        public string OraRichiesta
        {
            get => _oraRichiesta;
            set => SetProperty(ref _oraRichiesta, value);
        }

        private string _referente = string.Empty;
        public string Referente
        {
            get => _referente;
            set => SetProperty(ref _referente, value);
        }

        private bool _inviataEmailBool;
        public bool InviataEmailBool
        {
            get => _inviataEmailBool;
            set
            {
                if (SetProperty(ref _inviataEmailBool, value))
                    OnPropertyChanged(nameof(InviataEmail));
            }
        }
        public string InviataEmail
        {
            get => InviataEmailBool ? "SI" : "NO";
            set => InviataEmailBool = (value == "SI");
        }

        private string _passaggioConsegna = string.Empty;
        public string PassaggioConsegna
        {
            get => _passaggioConsegna;
            set => SetProperty(ref _passaggioConsegna, value);
        }
    }

    public class RapportinoTurnoModel : ViewModelBase
    {
        private string _tipoTreno = string.Empty; // "ETR 700", "ETR 1000", "ETR 500"
        public string TipoTreno
        {
            get => _tipoTreno;
            set => SetProperty(ref _tipoTreno, value);
        }

        private string _notaUfficioCt = string.Empty;
        public string NotaUfficioCt
        {
            get => _notaUfficioCt;
            set => SetProperty(ref _notaUfficioCt, value);
        }

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

        private string _oraInizio = string.Empty;
        public string OraInizio
        {
            get => _oraInizio;
            set
            {
                if (SetProperty(ref _oraInizio, value))
                    OnPropertyChanged(nameof(OraInizioFine));
            }
        }

        private string _oraFine = string.Empty;
        public string OraFine
        {
            get => _oraFine;
            set
            {
                if (SetProperty(ref _oraFine, value))
                    OnPropertyChanged(nameof(OraInizioFine));
            }
        }

        public string OraInizioFine
        {
            get => string.IsNullOrEmpty(OraInizio) && string.IsNullOrEmpty(OraFine) ? string.Empty : $"{OraInizio} - {OraFine}".Trim(' ', '-');
            set
            {
                if (string.IsNullOrWhiteSpace(value))
                {
                    OraInizio = string.Empty;
                    OraFine = string.Empty;
                    return;
                }
                var parts = value.Split('-');
                if (parts.Length > 0) OraInizio = parts[0].Trim();
                if (parts.Length > 1) OraFine = parts[1].Trim();
            }
        }

        private string _data = System.DateTime.Now.ToString("dd/MM/yyyy");
        public string Data
        {
            get => _data;
            set => SetProperty(ref _data, value);
        }

        public ObservableCollection<MovimentoTrenoRow> Movimenti { get; set; } = new();
        public ObservableCollection<DettaglioInterventoRow> Interventi { get; set; } = new();
        public ObservableCollection<InterventoNonSvoltoRow> InterventiNonSvolti { get; set; } = new();

        public RapportinoTurnoModel(string tipoTreno, string notaUfficioCt)
        {
            TipoTreno = tipoTreno;
            NotaUfficioCt = notaUfficioCt;

            // Inizializza 10 righe vuote per la tabella movimenti
            for (int i = 1; i <= 10; i++)
            {
                Movimenti.Add(new MovimentoTrenoRow { Numero = i });
            }

            // Inizializza 10 righe vuote per la tabella interventi non svolti
            for (int i = 1; i <= 10; i++)
            {
                InterventiNonSvolti.Add(new InterventoNonSvoltoRow { Numero = i });
            }
        }
    }
}
