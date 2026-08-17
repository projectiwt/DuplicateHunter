using System.Windows;
using System.Windows.Controls;
using DuplicateHunter.UI.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace DuplicateHunter.UI.Views
{
    public partial class MainWindow : Window
    {
        private readonly DashboardViewModel? _dashboardViewModel;

        public MainWindow()
        {
            InitializeComponent();

            _dashboardViewModel = App.Host?.Services.GetService<DashboardViewModel>();
            if (_dashboardViewModel != null)
            {
                _dashboardViewModel.NavigationRequested += OnNavigationRequested;
                Closed += (s, e) => _dashboardViewModel.NavigationRequested -= OnNavigationRequested;
            }

            NavigateTo("Dashboard");
        }

        private void OnNavigationRequested(string viewName)
        {
            Dispatcher.Invoke(() => NavigateTo(viewName));
        }

        public void NavigateTo(string pageName)
        {
            ResetNavHighlights();

            switch (pageName)
            {
                case "Dashboard":
                    MainContent.Content = new DashboardView();
                    DashboardButton.Tag = "Active";
                    break;
                case "Scan":
                    MainContent.Content = new ScanView();
                    ScanButton.Tag = "Active";
                    break;
                case "Results":
                    MainContent.Content = new ResultsView();
                    ResultsButton.Tag = "Active";
                    break;
                case "History":
                    MainContent.Content = new HistoryView();
                    HistoryButton.Tag = "Active";
                    break;
                case "Settings":
                    MainContent.Content = new SettingsView();
                    SettingsButton.Tag = "Active";
                    break;
                case "About":
                    MainContent.Content = new AboutView();
                    AboutButton.Tag = "Active";
                    break;
                default:
                    MainContent.Content = new DashboardView();
                    DashboardButton.Tag = "Active";
                    break;
            }
        }

        private void ResetNavHighlights()
        {
            DashboardButton.Tag = null;
            ScanButton.Tag = null;
            ResultsButton.Tag = null;
            HistoryButton.Tag = null;
            SettingsButton.Tag = null;
            AboutButton.Tag = null;
        }

        private void DashboardButton_Click(object sender, RoutedEventArgs e) => NavigateTo("Dashboard");
        private void ScanButton_Click(object sender, RoutedEventArgs e) => NavigateTo("Scan");
        private void ResultsButton_Click(object sender, RoutedEventArgs e) => NavigateTo("Results");
        private void HistoryButton_Click(object sender, RoutedEventArgs e) => NavigateTo("History");
        private void SettingsButton_Click(object sender, RoutedEventArgs e) => NavigateTo("Settings");
        private void AboutButton_Click(object sender, RoutedEventArgs e) => NavigateTo("About");
    }
}