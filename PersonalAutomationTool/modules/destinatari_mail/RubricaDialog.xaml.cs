using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using PersonalAutomationTool.Modules.Database;

namespace PersonalAutomationTool.Modules.DestinatariMail
{
    public class RubricaContact : System.ComponentModel.INotifyPropertyChanged
    {
        private bool _isSelected;
        public bool IsSelected
        {
            get => _isSelected;
            set { _isSelected = value; OnPropertyChanged(nameof(IsSelected)); }
        }
        public string Nome { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Categoria { get; set; } = string.Empty;

        public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged(string name) => PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(name));
    }

    public partial class RubricaDialog : Window
    {
        public ObservableCollection<RubricaContact> Contacts { get; set; } = new();

        public RubricaDialog()
        {
            InitializeComponent();
            LoadContactsFromDatabase();
            ContactsGrid.ItemsSource = Contacts;
        }

        private void LoadContactsFromDatabase()
        {
            try
            {
                string baseDir = AppDomain.CurrentDomain.BaseDirectory;
                DirectoryInfo? dir = new(baseDir);
                while (dir != null && dir.Name != "PersonalAutomationTool")
                {
                    dir = dir.Parent;
                }

                if (dir != null)
                {
                    string dbPath = Path.Combine(dir.FullName, "modules", "database", "emails.db");
                    if (File.Exists(dbPath))
                    {
                        using var dbManager = new DatabaseManager(dbPath);
                        var data = dbManager.ExecuteQuery("SELECT nome, email, categoria FROM indirizzi_email");
                        foreach (System.Data.DataRow row in data.Rows)
                        {
                            if (data.Columns.Contains("Errore") && row["Errore"] != DBNull.Value)
                                continue;

                            Contacts.Add(new RubricaContact
                            {
                                Nome = row["nome"]?.ToString() ?? "",
                                Email = row["email"]?.ToString() ?? "",
                                Categoria = row["categoria"]?.ToString() ?? ""
                            });
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Errore caricamento rubrica: {ex.Message}", "Errore", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        public string GetSelectedEmails()
        {
            var selected = new List<string>();
            foreach (var contact in Contacts)
            {
                if (contact.IsSelected && !string.IsNullOrWhiteSpace(contact.Email))
                {
                    selected.Add(contact.Email);
                }
            }
            return string.Join("; ", selected);
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

        private void SearchTextBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            if (ContactsGrid.ItemsSource != null)
            {
                var view = System.Windows.Data.CollectionViewSource.GetDefaultView(ContactsGrid.ItemsSource);
                if (view != null)
                {
                    string filterText = SearchTextBox.Text.Trim().ToLower();
                    if (string.IsNullOrEmpty(filterText))
                    {
                        view.Filter = null;
                    }
                    else
                    {
                        view.Filter = item =>
                        {
                            if (item is RubricaContact contact)
                            {
                                return (contact.Nome?.ToLower().Contains(filterText) == true) ||
                                       (contact.Email?.ToLower().Contains(filterText) == true) ||
                                       (contact.Categoria?.ToLower().Contains(filterText) == true);
                            }
                            return false;
                        };
                    }
                }
            }
        }
    }
}
