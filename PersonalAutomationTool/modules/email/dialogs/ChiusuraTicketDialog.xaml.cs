using System;
using System.Windows;

namespace PersonalAutomationTool.Modules.Email.Dialogs
{
    public partial class ChiusuraTicketDialog : Window
    {
        public string Avviso { get; private set; } = string.Empty;
        public string DataOra { get; private set; } = string.Empty;
        public string Avaria { get; private set; } = string.Empty;
        public string Intervento { get; private set; } = string.Empty;
        public string Loco { get; private set; } = string.Empty;
        public string TipoInterventoSelezionato { get; private set; } = string.Empty;

        public ChiusuraTicketDialog(string cartella = "")
        {
            InitializeComponent();
            PopulateLocos(cartella);
        }

        private void PopulateLocos(string cartella)
        {
            var locos = new System.Collections.Generic.HashSet<string>();

            try
            {
                if (!string.IsNullOrWhiteSpace(cartella))
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
                                // "SR... LOG ETR1000FH 620 ..."
                                // The loco is the token immediately following the train type.
                                // An easy way: split by " LOG ". The second part contains "TIPO LOCO SOFT DATA UTENTE".
                                var parts = dirName.Split(new[] { " LOG " }, StringSplitOptions.None);
                                if (parts.Length > 1)
                                {
                                    string infoPart = parts[1].Trim();
                                    var infoTokens = infoPart.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                                    if (infoTokens.Length >= 2)
                                    {
                                        // infoTokens[0] is typically Tipo (e.g., ETR1000FH)
                                        // infoTokens[1] is typically Loco (e.g., 620)
                                        locos.Add(infoTokens[1]);
                                    }
                                }
                            }
                        }
                    }

                    // Fallback to parsing folder name if no LOG folders found
                    if (locos.Count == 0)
                    {
                        var parts = cartella.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                        if (parts.Length > 1)
                        {
                            string rest = string.Join(" ", System.Linq.Enumerable.Skip(parts, 1));
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

            var list = new System.Collections.Generic.List<string>(locos);
            list.Sort();
            if (list.Count > 1)
            {
                list.Add(string.Join(" - ", list));
            }

            CmbLoco.ItemsSource = list;
            if (list.Count > 0) CmbLoco.SelectedIndex = 0;
        }

        private void BtnConferma_Click(object sender, RoutedEventArgs e)
        {
            Avviso = TxtAvviso.Text;
            DataOra = TxtDataOra.Text;
            Avaria = TxtAvaria.Text;
            Intervento = TxtIntervento.Text;
            Loco = CmbLoco.Text;

            if (RbNulla.IsChecked == true) TipoInterventoSelezionato = "Nulla Riscontrato";
            else if (RbNullaDati.IsChecked == true) TipoInterventoSelezionato = "Nulla Riscontrato Dati";
            else if (RbSostComponente.IsChecked == true) TipoInterventoSelezionato = "Sost. Componente";
            else if (RbSimGit.IsChecked == true) TipoInterventoSelezionato = "SIM-GIT";
            else if (RbSimGitDati.IsChecked == true) TipoInterventoSelezionato = "SIM-GIT con Dati";

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
