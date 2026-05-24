using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;

namespace PersonalAutomationTool.Modules.Email.Dialogs
{
    public class TicketInputModel : INotifyPropertyChanged
    {
        private string _selectedLoco = string.Empty;
        public string SelectedLoco
        {
            get => _selectedLoco;
            set { _selectedLoco = value; OnPropertyChanged(nameof(SelectedLoco)); }
        }

        private ObservableCollection<string> _availableLocos = [];
        public ObservableCollection<string> AvailableLocos
        {
            get => _availableLocos;
            set { _availableLocos = value; OnPropertyChanged(nameof(AvailableLocos)); }
        }

        private string _avviso = string.Empty;
        public string Avviso
        {
            get => _avviso;
            set { _avviso = value; OnPropertyChanged(nameof(Avviso)); }
        }

        private string _dataOra = string.Empty;
        public string DataOra
        {
            get => _dataOra;
            set { _dataOra = value; OnPropertyChanged(nameof(DataOra)); }
        }

        private string _avaria = string.Empty;
        public string Avaria
        {
            get => _avaria;
            set { _avaria = value; OnPropertyChanged(nameof(Avaria)); }
        }

        private string _intervento = string.Empty;
        public string Intervento
        {
            get => _intervento;
            set { _intervento = value; OnPropertyChanged(nameof(Intervento)); }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    public class LocoGroupModel : INotifyPropertyChanged
    {
        private string _groupLocoName = string.Empty;
        public string GroupLocoName
        {
            get => _groupLocoName;
            set { _groupLocoName = value; OnPropertyChanged(nameof(GroupLocoName)); }
        }

        private bool _isCopyFromFirstVisible = false;
        public bool IsCopyFromFirstVisible
        {
            get => _isCopyFromFirstVisible;
            set { _isCopyFromFirstVisible = value; OnPropertyChanged(nameof(IsCopyFromFirstVisible)); }
        }

        private ObservableCollection<TicketInputModel> _inputs = [];
        public ObservableCollection<TicketInputModel> Inputs
        {
            get => _inputs;
            set { _inputs = value; OnPropertyChanged(nameof(Inputs)); }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    public partial class ChiusuraTicketDialog : Window
    {
        [System.Text.RegularExpressions.GeneratedRegex(@"\b\d{6}\b")]
        private static partial System.Text.RegularExpressions.Regex SixDigitsRegex();

        public ObservableCollection<LocoGroupModel> LocoGroups { get; set; } = [];
        public ObservableCollection<string> TrainShortcuts { get; set; } = [];
        public string TipoInterventoSelezionato { get; private set; } = string.Empty;
        private readonly string _cartella;
        private readonly string _trainType;
        private readonly bool _isNd;
        private readonly string _actionType;

        public ChiusuraTicketDialog(string cartella = "", string trainType = "", bool isNd = false, string actionType = "Chiusura Ticket")
        {
            InitializeComponent();
            _cartella = cartella;
            _trainType = trainType;
            _isNd = isNd;
            _actionType = actionType;
            
            LoadCacheOrPopulate(cartella);
            
            DataContext = this;
            PopulateShortcuts(trainType);
        }

        private void LoadCacheOrPopulate(string cartella)
        {
            if (_actionType == "Log Dump")
            {
                try
                {
                    string baseLogDump = PersonalAutomationTool.Core.AppConfig.LogAndDumpFolder;
                    string fullPath = Path.Combine(baseLogDump, cartella);
                    string cacheFile = Path.Combine(fullPath, "info_ticket.json");

                    if (File.Exists(cacheFile))
                    {
                        string json = File.ReadAllText(cacheFile);
                        var cachedGroups = JsonSerializer.Deserialize<ObservableCollection<LocoGroupModel>>(json);
                        if (cachedGroups != null && cachedGroups.Count > 0)
                        {
                            LocoGroups = cachedGroups;
                            return;
                        }
                    }
                }
                catch { }
            }
            
            PopulateLocos(cartella);
        }

        private void PopulateShortcuts(string trainType)
        {
            var shortcuts = ShortcutsManager.GetShortcutsForTrain(trainType);
            TrainShortcuts.Clear();
            foreach (var s in shortcuts)
            {
                TrainShortcuts.Add(s);
            }
        }

        private void PopulateLocos(string cartella)
        {
            var locos = new HashSet<string>();

            try
            {
                if (!string.IsNullOrWhiteSpace(cartella))
                {
                    string baseLogDump = PersonalAutomationTool.Core.AppConfig.LogAndDumpFolder;
                    string fullPath = Path.Combine(baseLogDump, cartella);

                    if (Directory.Exists(fullPath))
                    {
                        var subDirs = Directory.GetDirectories(fullPath);
                        foreach (var dir in subDirs)
                        {
                            string dirName = Path.GetFileName(dir);
                            if (dirName.Contains(" LOG "))
                            {
                                var parts = dirName.Split(" LOG ", StringSplitOptions.None);
                                if (parts.Length > 1)
                                {
                                var dateMatch = SixDigitsRegex().Match(dirName);
                                var infoParts = dirName.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                                if (dateMatch.Success)
                                {
                                    int dateIndex = Array.IndexOf(infoParts, dateMatch.Value);
                                    int locoStartIndex = 3;
                                    if (infoParts.Length > 3 && (infoParts[3].Equals("I-F", StringComparison.OrdinalIgnoreCase) || infoParts[3].Equals("FH", StringComparison.OrdinalIgnoreCase)))
                                    {
                                        locoStartIndex = 4;
                                    }
                                    
                                    if (dateIndex > locoStartIndex)
                                    {
                                        string locoString = string.Join(" ", infoParts.Skip(locoStartIndex).Take(dateIndex - locoStartIndex));
                                        var splittedLocos = locoString.Split('-', StringSplitOptions.RemoveEmptyEntries);
                                        foreach(var s in splittedLocos) 
                                        {
                                            string cleanLoco = s.Trim();
                                            cleanLoco = System.Text.RegularExpressions.Regex.Replace(cleanLoco, @"\bBISTANDARD\b", "", System.Text.RegularExpressions.RegexOptions.IgnoreCase).Trim();
                                            if (!string.IsNullOrEmpty(cleanLoco))
                                            {
                                                locos.Add(cleanLoco);
                                            }
                                        }
                                    }
                                }
                                }
                            }
                        }
                    }

                    if (locos.Count == 0)
                    {
                        var parts = cartella.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                        var dateMatch = SixDigitsRegex().Match(cartella);
                        string trainNumber = "";
                        
                        if (dateMatch.Success)
                        {
                            int dateIndex = Array.IndexOf(parts, dateMatch.Value);
                            int locoStartIndex = 1;
                            if (parts.Length > 1 && (parts[1].Equals("I-F", StringComparison.OrdinalIgnoreCase) || parts[1].Equals("FH", StringComparison.OrdinalIgnoreCase)))
                                locoStartIndex = 2;
                            
                            if (dateIndex > locoStartIndex)
                            {
                                trainNumber = string.Join(" ", parts.Skip(locoStartIndex).Take(dateIndex - locoStartIndex));
                            }
                        }
                        else 
                        {
                            int locoStartIndex = 1;
                            if (parts.Length > 1 && (parts[1].Equals("I-F", StringComparison.OrdinalIgnoreCase) || parts[1].Equals("FH", StringComparison.OrdinalIgnoreCase)))
                                locoStartIndex = 2;
                            if (parts.Length > locoStartIndex)
                                trainNumber = parts[locoStartIndex];
                        }
                        
                        if (!string.IsNullOrEmpty(trainNumber))
                        {
                            if (trainNumber.Contains('-'))
                            {
                                var splitted = trainNumber.Split('-');
                                foreach (var s in splitted) locos.Add(s.Trim());
                            }
                            else
                            {
                                locos.Add(trainNumber.Trim());
                            }
                        }
                    }
                }
            }
            catch (Exception) { }

            var list = locos.ToList();
            list.Sort();

            if (list.Count > 0)
            {
                bool isFirst = true;
                foreach (var loco in list)
                {
                    var group = new LocoGroupModel { 
                        GroupLocoName = loco,
                        IsCopyFromFirstVisible = !isFirst
                    };
                    isFirst = false;
                    var model = new TicketInputModel
                    {
                        AvailableLocos = new ObservableCollection<string>(list),
                        SelectedLoco = loco
                    };
                    group.Inputs.Add(model);
                    LocoGroups.Add(group);
                }
            }
            else
            {
                var group = new LocoGroupModel { GroupLocoName = "Sconosciuta" };
                group.Inputs.Add(new TicketInputModel
                {
                    AvailableLocos = []
                });
                LocoGroups.Add(group);
            }
        }

        private void BtnAddSpecificSection_Click(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement element && element.DataContext is LocoGroupModel groupModel)
            {
                var firstInput = groupModel.Inputs.FirstOrDefault();
                var newModel = new TicketInputModel
                {
                    AvailableLocos = firstInput?.AvailableLocos ?? [],
                    SelectedLoco = groupModel.GroupLocoName
                };
                groupModel.Inputs.Add(newModel);
            }
        }

        private void BtnCopyFromFirst_Click(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement element && element.DataContext is LocoGroupModel currentGroup)
            {
                var firstGroup = LocoGroups.FirstOrDefault();
                if (firstGroup != null && firstGroup != currentGroup)
                {
                    currentGroup.Inputs.Clear();
                    foreach (var input in firstGroup.Inputs)
                    {
                        var newModel = new TicketInputModel
                        {
                            AvailableLocos = input.AvailableLocos,
                            SelectedLoco = currentGroup.GroupLocoName,
                            Avviso = input.Avviso,
                            DataOra = input.DataOra,
                            Avaria = input.Avaria,
                            Intervento = input.Intervento
                        };
                        currentGroup.Inputs.Add(newModel);
                    }
                }
            }
        }

        private static string GetMacroText(string macroName, string trainType)
        {
            if (trainType == "E404P")
            {
                switch (macroName)
                {
                    case "Nulla Riscontrato":
                        return "Dai controlli statici effettuati non si riscontrano anomalie al SSB. Eseguite prove con esito positivo come da check-list allegata.";
                    case "Nulla Riscontrato Dati":
                        return "Dai controlli statici effettuati non si riscontrano anomalie al SSB. Eseguito scarico dati diagnostici per analisi da parte dell'ingegneria. Eseguite prove con esito positivo come da check-list allegata.";
                    case "Sost. Componente":
                        return "Dai controlli Statici effettuati si rende necessaria la sostituzione XXX. Dopo la sostituzione non emergono ulteriori anomalie al SSB. Eseguito scarico dati per analisi da parte dell'ingegneria HR-STS. Eseguite prove con esito positivo come da Check List allegata";
                    case "SIM-GIT":
                        return "Dai controlli statici effettuati non si riscontrano anomalie al SSB. Eseguiti controlli con SIM-GIT con esito positivo. Eseguiti controlli con esito positivo come da Checklist allegata";
                    case "SIM-GIT con Dati":
                        return "Dai controlli statici effettuati non si riscontrano anomalie al SSB. Eseguiti controlli con SIM-GIT con esito positivo. Eseguito scarico dati diagnostici per ingegneria.  Eseguiti controlli con esito positivo come da Checklist allegata.";
                }
            }
            else if (trainType == "700")
            {
                switch (macroName)
                {
                    case "Nulla Riscontrato":
                        return "Dai controlli statici effettuati non si riscontrano anomalie al SSB. Con riferimento al SSB il treno è conforme all'esercizio commerciale";
                    case "Nulla Riscontrato Dati":
                        return "Dai controlli statici effettuati non si riscontrano anomalie al SSB. Eseguito scarico dati. Con riferimento al SSB, il treno è conforme all'esercizio commerciale.";
                    case "Sost. Componente":
                        return "Dai controlli Statici effettuati si rende necessaria la sostituzione XXX. Dopo la sostituzione non emergono ulteriori anomalie al SSB. Eseguito scarico dati. Con riferimento al SSB, il treno è conforme all'esercizio commerciale.";
                    case "SIM-GIT":
                        return "Dai controlli statici effettuati non si riscontrano anomalie al SSB. Eseguiti controlli con SIM-GIT con esito positivo. Con riferimento al SSB il treno è conforme all'esercizio commerciale.";
                    case "SIM-GIT con Dati":
                        return "Dai controlli statici effettuati non si riscontrano anomalie al SSB. Eseguiti controlli con SIM-GIT con esito positivo. Eseguito scarico dati diagnostici per ingegneria. Con ferimento al SSB il treno è conforme all'esercizio commerciale.";
                }
            }
            else if (trainType == "1000")
            {
                switch (macroName)
                {
                    case "Nulla Riscontrato":
                        return "Dai controlli statici effettuati non si riscontrano anomalie al SSB. Con riferimento al SSB il treno è conforme all'esercizio commerciale";
                    case "Nulla Riscontrato Dati":
                        return "Dai controlli statici effettuati non si riscontrano anomalie al SSB. Eseguito scarico dati. Con riferimento al SSB, il treno è conforme all'esercizio commerciale.";
                    case "Sost. Componente":
                        return "Dai controlli Statici effettuati si rende necessaria la sostituzione XXX. Dopo la sostituzione non emergono ulteriori anomalie al SSB. Eseguito scarico dati. Con riferimento al SSB, il treno è conforme all'esercizio commerciale.";
                    case "SIM-GIT":
                        return "Dai controlli statici effettuati non si riscontrano anomalie al SSB. Eseguiti controlli con SIM-GIT con esito positivo. Con riferimento al SSB il treno è conforme all'esercizio commerciale.";
                    case "SIM-GIT con Dati":
                        return "Dai controlli statici effettuati non si riscontrano anomalie al SSB. Eseguiti controlli con SIM-GIT con esito positivo. Eseguito scarico dati diagnostici per ingegneria. Con ferimento al SSB il treno è conforme all'esercizio commerciale.";
                }
            }
            else if (trainType == "1000IF")
            {
                switch (macroName)
                {
                    case "Nulla Riscontrato":
                        return "Dai controlli statici effettuati non si riscontrano anomalie al SSB. Con riferimento al SSB il treno è conforme all'esercizio commerciale";
                    case "Nulla Riscontrato Dati":
                        return "Dai controlli statici effettuati non si riscontrano anomalie al SSB. Eseguito scarico dati. Con riferimento al SSB, il treno è conforme all'esercizio commerciale.";
                    case "Sost. Componente":
                        return "Dai controlli Statici effettuati si rende necessaria la sostituzione XXX. Dopo la sostituzione non emergono ulteriori anomalie al SSB. Eseguito scarico dati. Con riferimento al SSB, il treno è conforme all'esercizio commerciale.";
                    case "SIM-GIT":
                        return "Dai controlli statici effettuati non si riscontrano anomalie al SSB. Eseguiti controlli con SIM-GIT con esito positivo. Con riferimento al SSB il treno è conforme all'esercizio commerciale.";
                    case "SIM-GIT con Dati":
                        return "Dai controlli statici effettuati non si riscontrano anomalie al SSB. Eseguiti controlli con SIM-GIT con esito positivo. Eseguito scarico dati diagnostici per ingegneria. Con ferimento al SSB il treno è conforme all'esercizio commerciale.";
                }
            }
            else if (trainType == "1000FH")
            {
                switch (macroName)
                {
                    case "Nulla Riscontrato":
                        return "Dai controlli statici effettuati non si riscontrano anomalie al SSB. Con riferimento al SSB il treno è conforme all'esercizio commerciale";
                    case "Nulla Riscontrato Dati":
                        return "Dai controlli statici effettuati non si riscontrano anomalie al SSB. Eseguito scarico dati. Con riferimento al SSB, il treno è conforme all'esercizio commerciale.";
                    case "Sost. Componente":
                        return "Dai controlli Statici effettuati si rende necessaria la sostituzione XXX. Dopo la sostituzione non emergono ulteriori anomalie al SSB. Eseguito scarico dati. Con riferimento al SSB, il treno è conforme all'esercizio commerciale.";
                    case "SIM-GIT":
                        return "Dai controlli statici effettuati non si riscontrano anomalie al SSB. Eseguiti controlli con SIM-GIT con esito positivo. Con riferimento al SSB il treno è conforme all'esercizio commerciale.";
                    case "SIM-GIT con Dati":
                        return "Dai controlli statici effettuati non si riscontrano anomalie al SSB. Eseguiti controlli con SIM-GIT con esito positivo. Eseguito scarico dati diagnostici per ingegneria. Con ferimento al SSB il treno è conforme all'esercizio commerciale.";
                }
            }
            return macroName;
        }

        private void BtnInserisciIntervento_Click(object sender, RoutedEventArgs e)
        {
            // Il bottone ora ha come DataContext direttamente la stringa (grazie all'ItemsControl)
            string textToInsert = "";
            if (sender is Button btn)
            {
                if (btn.Tag is string t)
                {
                    textToInsert = t;
                }
                else if (btn.DataContext is string d)
                {
                    textToInsert = GetMacroText(d, _trainType);
                }
            }

            if (!string.IsNullOrEmpty(textToInsert))
            {
                if (System.Windows.Input.Keyboard.FocusedElement is TextBox focusedElement && focusedElement.Tag is string tagString && tagString.Contains("Dettagli intervento"))
                {
                    var insertPos = focusedElement.SelectionStart;
                    if (!string.IsNullOrEmpty(focusedElement.Text) && !focusedElement.Text.EndsWith(' ') && !focusedElement.Text.EndsWith('\n') && insertPos > 0)
                    {
                        textToInsert = " " + textToInsert;
                    }
                    focusedElement.Text = focusedElement.Text.Insert(insertPos, textToInsert);
                    focusedElement.SelectionStart = insertPos + textToInsert.Length;
                    focusedElement.Focus();
                }
                else
                {
                    MessageBox.Show("Seleziona prima la casella di testo 'Descrizione Intervento' in cui vuoi inserire il testo.", "Attenzione", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
        }

        private void BtnRemoveSection_Click(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement element && element.DataContext is TicketInputModel inputModel)
            {
                foreach (var group in LocoGroups.ToList())
                {
                    if (group.Inputs.Remove(inputModel))
                    {
                        // Se era l'ultimo riquadro per questa locomotiva, eliminiamo l'intera riga
                        if (group.Inputs.Count == 0)
                        {
                            LocoGroups.Remove(group);
                        }
                        break;
                    }
                }
            }
        }

        private void InnerScrollViewer_PreviewMouseWheel(object sender, System.Windows.Input.MouseWheelEventArgs e)
        {
            if (sender is System.Windows.Controls.ScrollViewer scv)
            {
                if (System.Windows.Input.Keyboard.Modifiers == System.Windows.Input.ModifierKeys.Shift)
                {
                    scv.ScrollToHorizontalOffset(scv.HorizontalOffset - e.Delta);
                    e.Handled = true;
                    return;
                }

                if (scv.ScrollableWidth > 0)
                {
                    // Se andiamo verso sinistra (Delta > 0) e siamo già all'inizio, facciamo scroll verticale
                    if (e.Delta > 0 && scv.HorizontalOffset == 0)
                    {
                        BubbleScroll(scv, e);
                        return;
                    }
                    // Se andiamo verso destra (Delta < 0) e siamo già alla fine, facciamo scroll verticale
                    if (e.Delta < 0 && scv.HorizontalOffset >= scv.ScrollableWidth)
                    {
                        BubbleScroll(scv, e);
                        return;
                    }

                    // Altrimenti trasformiamo lo scroll verticale in orizzontale
                    // Molto comodo per scorrere tra gli avvisi usando la rotellina classica o le gesture verticali
                    scv.ScrollToHorizontalOffset(scv.HorizontalOffset - e.Delta);
                    e.Handled = true;
                }
                else
                {
                    // Nessuno spazio orizzontale, scroll verticale classico
                    BubbleScroll(scv, e);
                }
            }
        }

        private static void BubbleScroll(System.Windows.Controls.ScrollViewer scv, System.Windows.Input.MouseWheelEventArgs e)
        {
            e.Handled = true;
            var eventArg = new System.Windows.Input.MouseWheelEventArgs(e.MouseDevice, e.Timestamp, e.Delta)
            {
                RoutedEvent = UIElement.MouseWheelEvent,
                Source = scv
            };
            var parent = ((FrameworkElement)scv).Parent as UIElement;
            parent?.RaiseEvent(eventArg);
        }

        private void BtnConferma_Click(object sender, RoutedEventArgs e)
        {
            SaveCache();
            PersonalAutomationTool.Modules.Email.EmailService.GenerateChiusuraTicketEmail(_cartella, _trainType, LocoGroups, _isNd, _actionType);
            DialogResult = true;
            Close();
        }

        public void SaveCache()
        {
            try
            {
                string baseLogDump = PersonalAutomationTool.Core.AppConfig.LogAndDumpFolder;
                string fullPath = Path.Combine(baseLogDump, _cartella);
                string cacheFile = Path.Combine(fullPath, "info_ticket.json");

                string json = JsonSerializer.Serialize(LocoGroups);
                File.WriteAllText(cacheFile, json);
            }
            catch { }
        }

        private void BtnAnnulla_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
