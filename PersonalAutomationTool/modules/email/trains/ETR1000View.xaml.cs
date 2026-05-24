using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace PersonalAutomationTool.Modules.Email.Trains
{
    public partial class ETR1000View : UserControl
    {
        public ETR1000View()
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
                    .Where(d => d.Name.StartsWith("ETR1000 ", StringComparison.OrdinalIgnoreCase))
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
            var dialog = new PersonalAutomationTool.Modules.Email.Dialogs.ChiusuraTicketDialog(cartella, "ETR1000", isNd)
            {
                Owner = Application.Current.MainWindow
            };
            
            if (dialog.ShowDialog() == true)
            {
                // MessageBox.Show("Dati confermati! Implementare la generazione email.", "Successo", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void BtnScadenza6Mesi_Click(object sender, RoutedEventArgs e)
        {
            string cartella = CmbCartelle.SelectedItem?.ToString() ?? "";
            if (string.IsNullOrWhiteSpace(cartella))
            {
                MessageBox.Show("Selezionare una cartella.", "Attenzione", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var locos = new System.Collections.Generic.HashSet<string>();
            try
            {
                string baseLogDump = PersonalAutomationTool.Core.AppConfig.LogAndDumpFolder;
                string fullPath = System.IO.Path.Combine(baseLogDump, cartella);
                if (System.IO.Directory.Exists(fullPath))
                {
                    var subDirs = System.IO.Directory.GetDirectories(fullPath);
                    foreach (var dir in subDirs)
                    {
                        string dirName = System.IO.Path.GetFileName(dir);
                        if (dirName.Contains(" LOG "))
                        {
                            var tokens = dirName.Split(new[] { " LOG " }, StringSplitOptions.None);
                            if (tokens.Length > 1)
                            {
                                var infoTokens = tokens[1].Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
                                if (infoTokens.Length >= 2)
                                {
                                    locos.Add(infoTokens[1]);
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
                string trainNumber = parts.Length > 1 ? parts[1] : "";
                if (!string.IsNullOrWhiteSpace(trainNumber))
                {
                    locoList.Add(trainNumber);
                }
            }

            var locoGroups = new System.Collections.ObjectModel.ObservableCollection<PersonalAutomationTool.Modules.Email.Dialogs.LocoGroupModel>();
            
            foreach(var loco in locoList)
            {
                var group = new PersonalAutomationTool.Modules.Email.Dialogs.LocoGroupModel { GroupLocoName = loco };
                group.Inputs.Add(new PersonalAutomationTool.Modules.Email.Dialogs.TicketInputModel
                {
                    SelectedLoco = loco,
                    Avaria = "Revisione 6 mesi",
                    Intervento = "Eseguito controlli statici per scadenza semestrale come da procedura MR20A con esito positivo. Eseguito scarico dati. Con riferimento all’SSB, il treno è conforme all’esercizio commerciale."
                });
                locoGroups.Add(group);
            }

            bool isNd = ChkPrefissoND.IsChecked == true;
            PersonalAutomationTool.Modules.Email.EmailService.GenerateChiusuraTicketEmail(cartella, "ETR1000", locoGroups, isNd, "Scadenza 6 Mesi");
        }
        private void BtnScadenza12Mesi_Click(object sender, RoutedEventArgs e)
        {
            string cartella = CmbCartelle.SelectedItem?.ToString() ?? "";
            if (string.IsNullOrWhiteSpace(cartella))
            {
                MessageBox.Show("Selezionare una cartella.", "Attenzione", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var locos = new System.Collections.Generic.HashSet<string>();
            try
            {
                string baseLogDump = PersonalAutomationTool.Core.AppConfig.LogAndDumpFolder;
                string fullPath = System.IO.Path.Combine(baseLogDump, cartella);
                if (System.IO.Directory.Exists(fullPath))
                {
                    var subDirs = System.IO.Directory.GetDirectories(fullPath);
                    foreach (var dir in subDirs)
                    {
                        string dirName = System.IO.Path.GetFileName(dir);
                        if (dirName.Contains(" LOG "))
                        {
                            var tokens = dirName.Split(new[] { " LOG " }, StringSplitOptions.None);
                            if (tokens.Length > 1)
                            {
                                var infoTokens = tokens[1].Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
                                if (infoTokens.Length >= 2)
                                {
                                    locos.Add(infoTokens[1]);
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
                string trainNumber = parts.Length > 1 ? parts[1] : "";
                if (!string.IsNullOrWhiteSpace(trainNumber))
                {
                    locoList.Add(trainNumber);
                }
            }

            var locoGroups = new System.Collections.ObjectModel.ObservableCollection<PersonalAutomationTool.Modules.Email.Dialogs.LocoGroupModel>();
            
            foreach(var loco in locoList)
            {
                var group = new PersonalAutomationTool.Modules.Email.Dialogs.LocoGroupModel { GroupLocoName = loco };
                group.Inputs.Add(new PersonalAutomationTool.Modules.Email.Dialogs.TicketInputModel
                {
                    SelectedLoco = loco,
                    Avaria = "Revisione 12 mesi",
                    Intervento = "Eseguito controlli statici per scadenza annuale come da procedura MR20A con esito positivo. Eseguito scarico dati. Con riferimento all’SSB, il treno è conforme all’esercizio commerciale."
                });
                locoGroups.Add(group);
            }

            bool isNd = ChkPrefissoND.IsChecked == true;
            PersonalAutomationTool.Modules.Email.EmailService.GenerateChiusuraTicketEmail(cartella, "ETR1000", locoGroups, isNd, "Scadenza 12 Mesi");
        }
        private void Btn3R1_Click(object sender, RoutedEventArgs e) { }

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
