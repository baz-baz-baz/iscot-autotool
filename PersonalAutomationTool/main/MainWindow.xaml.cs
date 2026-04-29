using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

using PersonalAutomationTool.Modules.Home;
using PersonalAutomationTool.Modules.Cartelle;
using PersonalAutomationTool.Modules.Pdf;
using PersonalAutomationTool.Modules.Email;
using PersonalAutomationTool.Modules.Excel;
using PersonalAutomationTool.Modules.Database;
using PersonalAutomationTool.Modules.DestinatariMail;

namespace PersonalAutomationTool
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();

            // Inizializza il FileSystemWatcher per gli aggiornamenti in tempo reale
            Core.AppWatcher.Initialize();

            // Carica la Home all'avvio
            MainContentControl.Content = new HomeView();
        }

        private void Nav_Home(object sender, RoutedEventArgs e) => MainContentControl.Content = new HomeView();
        private void Nav_Cartelle(object sender, RoutedEventArgs e) => MainContentControl.Content = new CartelleView();
        private void Nav_Pdf(object sender, RoutedEventArgs e) => MainContentControl.Content = new PdfView();
        private void Nav_Email(object sender, RoutedEventArgs e) => MainContentControl.Content = new EmailView();
        private void Nav_Excel(object sender, RoutedEventArgs e) => MainContentControl.Content = new ExcelView();
        private void Nav_DestinatariMail(object sender, RoutedEventArgs e) => MainContentControl.Content = new DestinatariMailView();
        private void Nav_Database(object sender, RoutedEventArgs e) => MainContentControl.Content = new DatabaseView();
    }
}