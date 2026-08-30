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
                string dbPath = PersonalAutomationTool.Core.AppPaths.DatabaseFile("emails.db");
                if (File.Exists(dbPath))
                {
                    using var dbManager = new DatabaseManager(dbPath);
                    var contacts = dbManager.Query("SELECT nome, email, categoria FROM indirizzi_email", static reader => new RubricaContact
                    {
                        Nome = reader.IsDBNull(0) ? "" : reader.GetValue(0)?.ToString() ?? "",
                        Email = reader.IsDBNull(1) ? "" : reader.GetValue(1)?.ToString() ?? "",
                        Categoria = reader.IsDBNull(2) ? "" : reader.GetValue(2)?.ToString() ?? ""
                    });
                    foreach (var contact in contacts)
                    {
                        Contacts.Add(contact);
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
