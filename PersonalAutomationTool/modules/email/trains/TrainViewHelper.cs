using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using PersonalAutomationTool.Modules.Email.Dialogs;

namespace PersonalAutomationTool.Modules.Email.Trains
{
    /// <summary>
    /// Helper statico che centralizza la logica comune a tutti i TrainView (E404P, ETR700, ETR1000, ecc.)
    /// per evitare duplicazione massiva di codice nei code-behind.
    /// </summary>
    public static partial class TrainViewHelper
    {
        [GeneratedRegex(@"\b\d{6}\b")]
        private static partial Regex SixDigitsRegex();

        [GeneratedRegex(@"\bBISTANDARD\b", RegexOptions.IgnoreCase)]
        private static partial Regex BistandardRegex();

        /// <summary>
        /// Carica le cartelle da LOG &amp; DUMP filtrate per prefisso e le assegna alla ComboBox.
        /// Seleziona automaticamente la più recente.
        /// </summary>
        public static void LoadCartelle(ComboBox cmbCartelle, params string[] prefixes)
        {
            LoadCartelle(cmbCartelle, prefixes, excludePrefixes: null);
        }

        /// <summary>
        /// Carica le cartelle da LOG &amp; DUMP filtrate per prefisso (con esclusioni opzionali).
        /// </summary>
        public static void LoadCartelle(ComboBox cmbCartelle, string[] prefixes, string[]? excludePrefixes)
        {
            string baseLogDump = Core.AppConfig.LogAndDumpFolder;
            if (!Directory.Exists(baseLogDump)) return;

            var directoryInfo = new DirectoryInfo(baseLogDump);
            var directories = directoryInfo.GetDirectories()
                .Where(d => prefixes.Any(p => d.Name.StartsWith(p, StringComparison.OrdinalIgnoreCase)))
                .ToList();

            if (excludePrefixes != null)
            {
                directories = directories
                    .Where(d => !excludePrefixes.Any(ep => d.Name.StartsWith(ep, StringComparison.OrdinalIgnoreCase)))
                    .ToList();
            }

            var filteredNames = directories.Select(d => d.Name).ToList();
            cmbCartelle.ItemsSource = filteredNames;

            var lastCreated = directories.OrderByDescending(d => d.CreationTime).FirstOrDefault();
            if (lastCreated != null)
            {
                cmbCartelle.SelectedItem = lastCreated.Name;
            }
        }

        /// <summary>
        /// Naviga indietro alla EmailView principale.
        /// </summary>
        public static void NavigateBack()
        {
            if (Application.Current.MainWindow is MainWindow mainWindow)
            {
                mainWindow.MainContentControl.Content = new EmailView();
            }
        }

        /// <summary>
        /// Apre il dialog Chiusura Ticket con la cartella e il tipo di treno selezionati.
        /// </summary>
        public static void OpenChiusuraTicketDialog(string cartella, string trainType, bool isNd, string actionType = "Chiusura Ticket")
        {
            var dialog = new ChiusuraTicketDialog(cartella, trainType, isNd, actionType)
            {
                Owner = Application.Current.MainWindow
            };
            dialog.ShowDialog();
        }

        /// <summary>
        /// Gestisce il toggle del checkbox ND (ON/OFF con colore).
        /// </summary>
        public static void SetNdCheckboxState(TextBlock? txtPrefissoND, bool isChecked)
        {
            if (txtPrefissoND == null) return;
            txtPrefissoND.Text = isChecked ? "ON" : "OFF";
            string color = isChecked ? "#3B82F6" : "#7F8C8D";
            txtPrefissoND.Foreground = new System.Windows.Media.SolidColorBrush(
                (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(color));
        }

        /// <summary>
        /// Estrae le locomotive dalle sottocartelle LOG di una cartella di lavoro.
        /// Logica condivisa tra ETR1000, ETR1000IF, ETR1000FH e le scadenze.
        /// </summary>
        public static List<string> ExtractLocosFromFolder(string cartella, bool useBistandardClean = false)
        {
            var locos = new HashSet<string>();
            try
            {
                string baseLogDump = Core.AppConfig.LogAndDumpFolder;
                string fullPath = Path.Combine(baseLogDump, cartella);
                if (Directory.Exists(fullPath))
                {
                    var subDirs = Directory.GetDirectories(fullPath);
                    foreach (var dir in subDirs)
                    {
                        string dirName = Path.GetFileName(dir);
                        if (dirName.Contains(" LOG "))
                        {
                            if (useBistandardClean)
                            {
                                ExtractLocosAdvanced(dirName, locos);
                            }
                            else
                            {
                                ExtractLocosSimple(dirName, locos);
                            }
                        }
                    }
                }
            }
            catch { }

            var locoList = locos.ToList();
            locoList.Sort();

            // Fallback: estrai dal nome della cartella stessa
            if (locoList.Count == 0)
            {
                ExtractLocosFromFolderName(cartella, locoList);
            }

            return locoList;
        }

        /// <summary>
        /// Parsing semplice delle locomotive: prende il secondo token dopo " LOG ".
        /// Usato da ETR1000View per le scadenze 6/12 mesi.
        /// </summary>
        private static void ExtractLocosSimple(string dirName, HashSet<string> locos)
        {
            var tokens = dirName.Split(" LOG ", StringSplitOptions.None);
            if (tokens.Length > 1)
            {
                var infoTokens = tokens[1].Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (infoTokens.Length >= 2)
                {
                    locos.Add(infoTokens[1]);
                }
            }
        }

        /// <summary>
        /// Parsing avanzato delle locomotive con gestione date, I-F/FH e BISTANDARD.
        /// Usato da ETR1000IFView (ChiusuraTicketDialog usa lo stesso pattern).
        /// </summary>
        private static void ExtractLocosAdvanced(string dirName, HashSet<string> locos)
        {
            var dateMatch = SixDigitsRegex().Match(dirName);
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
                    foreach (var s in splittedLocos)
                    {
                        string cleanLoco = s.Trim();
                        cleanLoco = BistandardRegex().Replace(cleanLoco, "").Trim();
                        if (cleanLoco.Contains(' '))
                        {
                            cleanLoco = cleanLoco.Split(' ', StringSplitOptions.RemoveEmptyEntries)[0];
                        }
                        if (!string.IsNullOrEmpty(cleanLoco))
                        {
                            locos.Add(cleanLoco);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Fallback: estrai loco dal nome della cartella stessa se non trovate sottocartelle LOG.
        /// </summary>
        private static void ExtractLocosFromFolderName(string cartella, List<string> locoList)
        {
            var parts = cartella.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var dateMatch = SixDigitsRegex().Match(cartella);
            string trainNumber = "";

            int locoStartIndex = 1;
            if (parts.Length > 1 && (parts[1].Equals("I-F", StringComparison.OrdinalIgnoreCase) || parts[1].Equals("FH", StringComparison.OrdinalIgnoreCase)))
                locoStartIndex = 2;

            if (dateMatch.Success)
            {
                int dateIndex = Array.IndexOf(parts, dateMatch.Value);
                if (dateIndex > locoStartIndex)
                {
                    trainNumber = string.Join(" ", parts.Skip(locoStartIndex).Take(dateIndex - locoStartIndex));
                }
            }
            else
            {
                if (parts.Length > locoStartIndex)
                    trainNumber = parts[locoStartIndex];
            }

            if (!string.IsNullOrWhiteSpace(trainNumber))
            {
                if (trainNumber.Contains('-'))
                {
                    var splitted = trainNumber.Split('-');
                    foreach (var s in splitted)
                    {
                        string cleanLoco = s.Trim();
                        if (cleanLoco.Contains(' '))
                            cleanLoco = cleanLoco.Split(' ', StringSplitOptions.RemoveEmptyEntries)[0];
                        if (!string.IsNullOrEmpty(cleanLoco))
                            locoList.Add(cleanLoco);
                    }
                }
                else
                {
                    string cleanLoco = trainNumber.Trim();
                    if (cleanLoco.Contains(' '))
                        cleanLoco = cleanLoco.Split(' ', StringSplitOptions.RemoveEmptyEntries)[0];
                    if (!string.IsNullOrEmpty(cleanLoco))
                        locoList.Add(cleanLoco);
                }
            }
        }

        /// <summary>
        /// Genera un'email di scadenza (6 mesi, 12 mesi, VI, VT, ecc.) con gestione cache opzionale.
        /// Per E404P usa il dialog per caching; per ETR1000/IF usa generazione diretta.
        /// </summary>
        public static void GenerateScadenzaEmailWithDialog(string cartella, string trainType, string actionType,
            string avariaText, string interventoText, bool isNd = false)
        {
            if (string.IsNullOrWhiteSpace(cartella))
            {
                MessageBox.Show("Selezionare una cartella.", "Attenzione", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var dialog = new ChiusuraTicketDialog(cartella, trainType, isNd, actionType);
            foreach (var group in dialog.LocoGroups)
            {
                foreach (var input in group.Inputs)
                {
                    input.Avviso = "";
                    input.DataOra = "";
                    input.Avaria = avariaText;
                    input.Intervento = interventoText;
                }
            }
            dialog.SaveCache();
            EmailService.GenerateChiusuraTicketEmail(cartella, trainType, dialog.LocoGroups, isNd, actionType);
        }

        /// <summary>
        /// Genera un'email di scadenza con generazione diretta dei LocoGroups (senza dialog/caching).
        /// Usato da ETR1000View per le scadenze 6/12 mesi.
        /// </summary>
        public static void GenerateScadenzaEmailDirect(string cartella, string trainType, string actionType,
            string avariaText, string interventoText, bool isNd = false, bool useBistandardClean = false)
        {
            if (string.IsNullOrWhiteSpace(cartella))
            {
                MessageBox.Show("Selezionare una cartella.", "Attenzione", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var locoList = ExtractLocosFromFolder(cartella, useBistandardClean);
            var locoGroups = new ObservableCollection<LocoGroupModel>();

            foreach (var loco in locoList)
            {
                var group = new LocoGroupModel { GroupLocoName = loco };
                group.Inputs.Add(new TicketInputModel
                {
                    SelectedLoco = loco,
                    Avaria = avariaText,
                    Intervento = interventoText
                });
                locoGroups.Add(group);
            }

            EmailService.GenerateChiusuraTicketEmail(cartella, trainType, locoGroups, isNd, actionType);
        }
    }
}
