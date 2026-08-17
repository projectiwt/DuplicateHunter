using Microsoft.Extensions.DependencyInjection;
using DuplicateHunter.UI.ViewModels;

namespace DuplicateHunter.UI.Views;

public partial class ScanView : System.Windows.Controls.UserControl
{
    public ScanView()
    {
        InitializeComponent();

        DataContext = App.Host!.Services.GetRequiredService<DashboardViewModel>();
    }
}