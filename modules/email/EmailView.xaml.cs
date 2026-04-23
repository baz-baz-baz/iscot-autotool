using System.Windows;
using System.Windows.Controls;
using PersonalAutomationTool.Modules.Email.Trains;

namespace PersonalAutomationTool.Modules.Email
{
    public partial class EmailView : UserControl
    {
        public EmailView()
        {
            InitializeComponent();
        }

        private void Nav_E404P(object sender, RoutedEventArgs e) => NavigateTo(new E404PView());
        private void Nav_ETR700(object sender, RoutedEventArgs e) => NavigateTo(new ETR700View());
        private void Nav_ETR1000(object sender, RoutedEventArgs e) => NavigateTo(new ETR1000View());
        private void Nav_ETR1000IF(object sender, RoutedEventArgs e) => NavigateTo(new ETR1000IFView());
        private void Nav_ETR1000FH(object sender, RoutedEventArgs e) => NavigateTo(new ETR1000FHView());
        private void Nav_ETR421(object sender, RoutedEventArgs e) => NavigateTo(new ETR421View());
        private void Nav_ETR521(object sender, RoutedEventArgs e) => NavigateTo(new ETR521View());
        private void Nav_ETR522(object sender, RoutedEventArgs e) => NavigateTo(new ETR522View());

        private static void NavigateTo(UserControl view)
        {
            if (Application.Current.MainWindow is MainWindow mainWindow)
            {
                mainWindow.MainContentControl.Content = view;
            }
        }
    }
}
