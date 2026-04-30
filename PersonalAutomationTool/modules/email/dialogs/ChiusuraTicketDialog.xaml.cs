using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
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

        private ObservableCollection<string> _availableLocos = new ObservableCollection<string>();
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

        private ObservableCollection<TicketInputModel> _inputs = new ObservableCollection<TicketInputModel>();
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
        public ObservableCollection<LocoGroupModel> LocoGroups { get; set; } = new ObservableCollection<LocoGroupModel>();
        public string TipoInterventoSelezionato { get; private set; } = string.Empty;

        public ChiusuraTicketDialog(string cartella = "")
        {
            InitializeComponent();
            DataContext = this;
            PopulateLocos(cartella);
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
                                var parts = dirName.Split(new[] { " LOG " }, StringSplitOptions.None);
                                if (parts.Length > 1)
                                {
                                    string infoPart = parts[1].Trim();
                                    var infoTokens = infoPart.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                                    if (infoTokens.Length >= 2)
                                    {
                                        locos.Add(infoTokens[1]);
                                    }
                                }
                            }
                        }
                    }

                    if (locos.Count == 0)
                    {
                        var parts = cartella.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                        if (parts.Length > 1)
                        {
                            string rest = string.Join(" ", parts.Skip(1));
                            if (rest.Contains("-"))
                            {
                                var splitted = rest.Split('-');
                                foreach (var s in splitted) locos.Add(s.Trim());
                            }
                            else
                            {
                                locos.Add(rest.Trim());
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
                    AvailableLocos = new ObservableCollection<string>()
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
                    AvailableLocos = firstInput?.AvailableLocos ?? new ObservableCollection<string>(),
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

        private void BtnInserisciIntervento_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is string textToInsert)
            {
                var focusedElement = System.Windows.Input.Keyboard.FocusedElement as TextBox;
                if (focusedElement != null && focusedElement.Tag is string tagString && tagString.Contains("Dettagli intervento"))
                {
                    var insertPos = focusedElement.SelectionStart;
                    if (!string.IsNullOrEmpty(focusedElement.Text) && !focusedElement.Text.EndsWith(" ") && !focusedElement.Text.EndsWith("\n") && insertPos > 0)
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
                    if (group.Inputs.Contains(inputModel))
                    {
                        group.Inputs.Remove(inputModel);
                        
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

        private void BubbleScroll(System.Windows.Controls.ScrollViewer scv, System.Windows.Input.MouseWheelEventArgs e)
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
            DialogResult = true;
            Close();
        }

        private void BtnAnnulla_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
