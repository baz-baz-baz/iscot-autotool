using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using PersonalAutomationTool.Core;
using PersonalAutomationTool.Modules.Verifiche;

namespace PersonalAutomationTool.Modules.PassaggioConsegne
{
    public class PassaggioConsegneViewModel : ViewModelBase
    {
        public RapportinoTurnoModel RapportinoEtr700 { get; set; }
        public RapportinoTurnoModel RapportinoEtr1000 { get; set; }
        public RapportinoTurnoModel RapportinoEtr500 { get; set; }

        private RapportinoTurnoModel _selectedRapportino;
        public RapportinoTurnoModel SelectedRapportino
        {
            get => _selectedRapportino;
            set
            {
                if (SetProperty(ref _selectedRapportino, value))
                {
                    OnPropertyChanged(nameof(IsEtr700Selected));
                    OnPropertyChanged(nameof(IsEtr1000Selected));
                    OnPropertyChanged(nameof(IsEtr500Selected));
                }
            }
        }

        public bool IsEtr700Selected => SelectedRapportino == RapportinoEtr700;
        public bool IsEtr1000Selected => SelectedRapportino == RapportinoEtr1000;
        public bool IsEtr500Selected => SelectedRapportino == RapportinoEtr500;

        public ICommand SelectEtr700Command { get; }
        public ICommand SelectEtr1000Command { get; }
        public ICommand SelectEtr500Command { get; }

        public ICommand SalvaDatiCommand { get; }
        public ICommand AggiungiInterventoCommand { get; }
        public ICommand RimuoviInterventoCommand { get; }
        public ICommand ResetCommand { get; }

        public ObservableCollection<string> OpzioniSiNo { get; } = new() { "", "SI", "NO" };
        public ObservableCollection<string> OpzioniTurno { get; } = new() { "", "1° Turno", "Turno CS", "2° Turno", "3° Turno" };

        public PassaggioConsegneViewModel()
        {
            RapportinoEtr700 = new RapportinoTurnoModel(
                "ETR 700",
                "ETR 700 (da aggiornare durante il turno con verifica presso ufficio CT Hitachi)"
            );

            RapportinoEtr1000 = new RapportinoTurnoModel(
                "ETR 1000",
                "ETR 1000 (da aggiornare durante il turno con verifica presso ufficio CT Hitachi)"
            );

            RapportinoEtr500 = new RapportinoTurnoModel(
                "ETR 500",
                "ETR 500 (da aggiornare durante il turno con verifica presso ufficio CT Trenitalia)"
            );

            _selectedRapportino = RapportinoEtr700;

            SelectEtr700Command = new RelayCommand(_ => SelectedRapportino = RapportinoEtr700);
            SelectEtr1000Command = new RelayCommand(_ => SelectedRapportino = RapportinoEtr1000);
            SelectEtr500Command = new RelayCommand(_ => SelectedRapportino = RapportinoEtr500);

            SalvaDatiCommand = new RelayCommand(_ => SalvaDati(true));

            AggiungiInterventoCommand = new RelayCommand(_ =>
            {
                SelectedRapportino.Interventi.Add(new DettaglioInterventoRow());
            });

            RimuoviInterventoCommand = new RelayCommand(param =>
            {
                if (param is DettaglioInterventoRow riga)
                {
                    SelectedRapportino.Interventi.Remove(riga);
                }
                else if (SelectedRapportino.Interventi.Count > 0)
                {
                    SelectedRapportino.Interventi.RemoveAt(SelectedRapportino.Interventi.Count - 1);
                }
            });

            ResetCommand = new RelayCommand(_ =>
            {
                if (MessageBox.Show("Sei sicuro di voler resettare il rapportino corrente?", "Conferma Reset", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
                {
                    SelectedRapportino.Nome = string.Empty;
                    SelectedRapportino.Cognome = string.Empty;
                    SelectedRapportino.OraInizio = string.Empty;
                    SelectedRapportino.OraFine = string.Empty;
                    SelectedRapportino.Data = DateTime.Now.ToString("dd/MM/yyyy");

                    foreach (var m in SelectedRapportino.Movimenti)
                    {
                        m.Treno = string.Empty;
                        m.Loco = string.Empty;
                        m.DataIngresso = string.Empty;
                        m.OraIngresso = string.Empty;
                        m.DataUscita = string.Empty;
                        m.OraUscita = string.Empty;
                    }

                    SelectedRapportino.Interventi.Clear();

                    foreach (var n in SelectedRapportino.InterventiNonSvolti)
                    {
                        n.TrenoLoco = string.Empty;
                        n.Motivazione = string.Empty;
                        n.OraRichiesta = string.Empty;
                        n.Referente = string.Empty;
                        n.InviataEmail = string.Empty;
                        n.PassaggioConsegna = string.Empty;
                    }

                    AutoCompilaTreniDaVerifiche();
                }
            });

            CaricaDati();
            AutoCompilaTreniDaVerifiche();

            VerificheViewModel.OnVerificheDataUpdated += () =>
            {
                Application.Current?.Dispatcher.InvokeAsync(() =>
                {
                    AutoCompilaTreniDaVerifiche();
                });
            };
        }

        public void AutoCompilaTreniDaVerifiche()
        {
            AutoCompilaRapportino(RapportinoEtr700, "700");
            AutoCompilaRapportino(RapportinoEtr1000, "1000");
            AutoCompilaRapportino(RapportinoEtr500, "500");
        }

        private static void AutoCompilaRapportino(RapportinoTurnoModel rapportino, string fleetIdentifier)
        {
            var rawList = VerificheViewModel.GetVerificheForFleetStatic(fleetIdentifier);
            if (rawList == null || rawList.Count == 0) return;

            var grouped = rawList
                .Where(v => !string.IsNullOrWhiteSpace(v.Treno))
                .GroupBy(v => v.Treno.Trim(), StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (grouped.Count == 0) return;

            for (int i = 0; i < grouped.Count; i++)
            {
                var grp = grouped[i];
                string trenoName = grp.Key;

                var locos = grp.Select(v => v.Loco?.Trim())
                               .Where(l => !string.IsNullOrWhiteSpace(l))
                               .Distinct(StringComparer.OrdinalIgnoreCase)
                               .ToList();

                string combinedLoco = string.Join(" - ", locos);

                if (i >= rapportino.Movimenti.Count)
                {
                    rapportino.Movimenti.Add(new MovimentoTrenoRow { Numero = i + 1 });
                }

                rapportino.Movimenti[i].Treno = trenoName;
                rapportino.Movimenti[i].Loco = combinedLoco;
            }
        }

        public void SalvaDati(bool showNotification = true)
        {
            try
            {
                string folder = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "data");
                System.IO.Directory.CreateDirectory(folder);
                string file = System.IO.Path.Combine(folder, "passaggio_consegne.json");

                var container = new RapportiniDataContainer
                {
                    Etr700 = RapportinoEtr700,
                    Etr1000 = RapportinoEtr1000,
                    Etr500 = RapportinoEtr500
                };

                string json = System.Text.Json.JsonSerializer.Serialize(container, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
                System.IO.File.WriteAllText(file, json);

                if (showNotification)
                {
                    MessageBox.Show("Dati del rapportino salvati con successo!", "Salvataggio Dati", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                if (showNotification)
                {
                    MessageBox.Show($"Errore durante il salvataggio dei dati:\n{ex.Message}", "Errore Salvataggio", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void CaricaDati()
        {
            try
            {
                string file = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "data", "passaggio_consegne.json");
                if (System.IO.File.Exists(file))
                {
                    string json = System.IO.File.ReadAllText(file);
                    var container = System.Text.Json.JsonSerializer.Deserialize<RapportiniDataContainer>(json);
                    if (container != null)
                    {
                        if (container.Etr700 != null) RapportinoEtr700 = container.Etr700;
                        if (container.Etr1000 != null) RapportinoEtr1000 = container.Etr1000;
                        if (container.Etr500 != null) RapportinoEtr500 = container.Etr500;
                        SelectedRapportino = RapportinoEtr700;

                        PulisciInviataEmail(RapportinoEtr700);
                        PulisciInviataEmail(RapportinoEtr1000);
                        PulisciInviataEmail(RapportinoEtr500);
                    }
                }
            }
            catch { }
        }

        private static void PulisciInviataEmail(RapportinoTurnoModel? model)
        {
            if (model?.InterventiNonSvolti == null) return;
            foreach (var item in model.InterventiNonSvolti)
            {
                if (item.InviataEmail == "NO")
                {
                    item.InviataEmail = string.Empty;
                }
            }
        }
    }

    public class RapportiniDataContainer
    {
        public RapportinoTurnoModel? Etr700 { get; set; }
        public RapportinoTurnoModel? Etr1000 { get; set; }
        public RapportinoTurnoModel? Etr500 { get; set; }
    }
}
