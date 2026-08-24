using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;

using PersonalAutomationTool.Modules.Home;
using PersonalAutomationTool.Modules.Cartelle;
using PersonalAutomationTool.Modules.Pdf;
using PersonalAutomationTool.Modules.Email;
using PersonalAutomationTool.Modules.Excel;
using PersonalAutomationTool.Modules.Database;
using PersonalAutomationTool.Modules.DestinatariMail;
using PersonalAutomationTool.Modules.Verifiche;
using PersonalAutomationTool.Modules.PassaggioConsegne;

namespace PersonalAutomationTool
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private readonly Dictionary<Type, UserControl> _viewCache = new();

        public MainWindow()
        {
            InitializeComponent();

            // Inizializza il FileSystemWatcher per gli aggiornamenti in tempo reale
            Core.AppWatcher.Initialize();

            // Carica la Home all'avvio
            NavigateTo<HomeView>();
        }

        /// <summary>
        /// Naviga verso una View, riutilizzando l'istanza già creata se presente in cache.
        /// Evita la ricreazione inutile delle View e preserva lo stato dell'utente.
        /// </summary>
        private void NavigateTo<T>() where T : UserControl, new()
        {
            if (!_viewCache.TryGetValue(typeof(T), out var view))
            {
                view = new T();
                _viewCache[typeof(T)] = view;
            }
            MainContentControl.Content = view;
        }

        private void Nav_Home(object sender, RoutedEventArgs e) => NavigateTo<HomeView>();
        private void Nav_Cartelle(object sender, RoutedEventArgs e) => NavigateTo<CartelleView>();
        private void Nav_Pdf(object sender, RoutedEventArgs e) => NavigateTo<PdfView>();
        private void Nav_Email(object sender, RoutedEventArgs e) => NavigateTo<EmailView>();
        private void Nav_Excel(object sender, RoutedEventArgs e) => NavigateTo<ExcelView>();
        private void Nav_DestinatariMail(object sender, RoutedEventArgs e) => NavigateTo<DestinatariMailView>();
        private void Nav_Database(object sender, RoutedEventArgs e) => NavigateTo<DatabaseView>();
        private void Nav_Verifiche(object sender, RoutedEventArgs e) => NavigateTo<VerificheView>();
        private void Nav_PassaggioConsegne(object sender, RoutedEventArgs e) => NavigateTo<PassaggioConsegneView>();
    }
}