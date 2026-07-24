using System.Windows;
using DuplicateHunter.UI.ViewModels;

namespace DuplicateHunter.UI.Views;

public partial class MainWindow : Window
{
    public MainWindow(DashboardViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}