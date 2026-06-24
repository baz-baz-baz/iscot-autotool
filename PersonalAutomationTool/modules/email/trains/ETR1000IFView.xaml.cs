using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace PersonalAutomationTool.Modules.Email.Trains
{
    public partial class ETR1000IFView : UserControl
    {
        [System.Text.RegularExpressions.GeneratedRegex(@"\b\d{6}\b")]
        private static partial System.Text.RegularExpressions.Regex DateRegex();

        [System.Text.RegularExpressions.GeneratedRegex(@"\bBISTANDARD\b", System.Text.RegularExpressions.RegexOptions.IgnoreCase)]
        private static partial System.Text.RegularExpressions.Regex BistandardRegex();

        public ETR1000IFView()
        {
            InitializeComponent();
            LoadCartelle();
        }

        private void LoadCartelle()
        {
            string baseLogDump = PersonalAutomationTool.Core.AppConfig.LogAndDumpFolder;
            if (Directory.Exists(baseLogDump))
            {
                var directoryInfo = new DirectoryInfo(baseLogDump);
                var directories = directoryInfo.GetDirectories()
                    .Where(d => d.Name.StartsWith("ETR1000IF", StringComparison.OrdinalIgnoreCase) ||
                                d.Name.StartsWith("ETR1000 I-F", StringComparison.OrdinalIgnoreCase))
                    .ToList();

                var filteredNames = directories.Select(d => d.Name).ToList();
                CmbCartelle.ItemsSource = filteredNames;

                var lastCreated = directories.OrderByDescending(d => d.CreationTime).FirstOrDefault();
                if (lastCreated != null)
                {
                    CmbCartelle.SelectedItem = lastCreated.Name;
                }
            }
        }

        private void BtnBack_Click(object sender, RoutedEventArgs e)
        {
            if (Application.Current.MainWindow is MainWindow mainWindow)
            {
                mainWindow.MainContentControl.Content = new EmailView();
            }
        }

        private void BtnChiusuraTicket_Click(object sender, RoutedEventArgs e)
        {
            string cartella = CmbCartelle.SelectedItem?.ToString() ?? "";
            bool isNd = ChkPrefissoND.IsChecked == true;
            var dialog = new PersonalAutomationTool.Modules.Email.Dialogs.ChiusuraTicketDialog(cartella, "ETR1000IF", isNd)
            {
                Owner = Application.Current.MainWindow
            };
            
            if (dialog.ShowDialog() == true)
            {
                // MessageBox.Show("Dati confermati! Implementare la generazione email.", "Successo", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }
        private void BtnScadenza6Mesi_Click(object sender, RoutedEventArgs e) { }
        private void BtnScadenza12Mesi_Click(object sender, RoutedEventArgs e) { }
        
        private void BtnScadenzeFrancesi_Click(object sender, RoutedEventArgs e)
        {
            string cartella = CmbCartelle.SelectedItem?.ToString() ?? "";
            if (string.IsNullOrWhiteSpace(cartella))
            {
                MessageBox.Show("Selezionare una cartella.", "Attenzione", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            string baseLogDump = PersonalAutomationTool.Core.AppConfig.LogAndDumpFolder;
            string fullPath = System.IO.Path.Combine(baseLogDump, cartella);
            
            string scadenzaName = "I0"; // Fallback
            if (System.IO.Directory.Exists(fullPath))
            {
                var txtFiles = System.IO.Directory.GetFiles(fullPath, "*.txt");
                if (txtFiles.Length > 0)
                {
                    scadenzaName = System.IO.Path.GetFileNameWithoutExtension(txtFiles[0]);
                }
            }

            var locos = new System.Collections.Generic.HashSet<string>();
            try
            {
                if (System.IO.Directory.Exists(fullPath))
                {
                    var subDirs = System.IO.Directory.GetDirectories(fullPath);
                    foreach (var dir in subDirs)
                    {
                        string dirName = System.IO.Path.GetFileName(dir);
                        if (dirName.Contains(" LOG "))
                        {
                            var dateMatch = DateRegex().Match(dirName);
                            var parts = dirName.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                            if (dateMatch.Success)
                            {
                                int dateIndex = Array.IndexOf(parts, dateMatch.Value);
                                int locoStartIndex = 3;
                                if (parts.Length > 3 && (parts[3].Equals("I-F", StringComparison.OrdinalIgnoreCase) || parts[3].Equals("FH", StringComparison.OrdinalIgnoreCase)))
                                {
                                    locoStartIndex = 4;
                                }
                                
                                if (dateIndex > locoStartIndex)
                                {
                                    string locoString = string.Join(" ", parts.Skip(locoStartIndex).Take(dateIndex - locoStartIndex));
                                    var splittedLocos = locoString.Split('-', StringSplitOptions.RemoveEmptyEntries);
                                    foreach(var s in splittedLocos) 
                                    {
                                        string cleanLoco = s.Trim();
                                        cleanLoco = BistandardRegex().Replace(cleanLoco, "").Trim();
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
            } catch { }

            var locoList = System.Linq.Enumerable.ToList(locos);
            locoList.Sort();

            if (locoList.Count == 0) 
            {
                var parts = cartella.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                var dateMatch = DateRegex().Match(cartella);
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

                if (!string.IsNullOrWhiteSpace(trainNumber))
                {
                    if (trainNumber.Contains('-'))
                    {
                        var splitted = trainNumber.Split('-');
                        foreach (var s in splitted) locoList.Add(s.Trim());
                    }
                    else
                    {
                        locoList.Add(trainNumber.Trim());
                    }
                }
            }

            var locoGroups = new System.Collections.ObjectModel.ObservableCollection<PersonalAutomationTool.Modules.Email.Dialogs.LocoGroupModel>();
            
            foreach(var loco in locoList)
            {
                var group = new PersonalAutomationTool.Modules.Email.Dialogs.LocoGroupModel { GroupLocoName = loco };
                group.Inputs.Add(new PersonalAutomationTool.Modules.Email.Dialogs.TicketInputModel
                {
                    SelectedLoco = loco,
                    Avaria = $"Revisione {scadenzaName}",
                    Intervento = $"Eseguito controlli statici per scadenza {scadenzaName} come da PdM con esito positivo."
                });
                locoGroups.Add(group);
            }

            bool isNd = ChkPrefissoND.IsChecked == true;
            PersonalAutomationTool.Modules.Email.EmailService.GenerateChiusuraTicketEmail(cartella, "ETR1000IF", locoGroups, isNd, "Scadenze Francesi");
        }

        private void ChkPrefissoND_Checked(object sender, RoutedEventArgs e)
        {
            if (TxtPrefissoND != null)
            {
                TxtPrefissoND.Text = "ON";
                TxtPrefissoND.Foreground = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#3B82F6"));
            }
        }

        private void ChkPrefissoND_Unchecked(object sender, RoutedEventArgs e)
        {
            if (TxtPrefissoND != null)
            {
                TxtPrefissoND.Text = "OFF";
                TxtPrefissoND.Foreground = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#7F8C8D"));
            }
        }
    }
}
