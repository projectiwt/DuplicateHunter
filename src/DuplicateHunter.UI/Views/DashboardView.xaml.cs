using Microsoft.Extensions.DependencyInjection;
using DuplicateHunter.UI.ViewModels;

namespace DuplicateHunter.UI.Views;

public partial class DashboardView : System.Windows.Controls.UserControl
{
    public DashboardView()
    {
        InitializeComponent();

        DataContext = App.Host!
            .Services
            .GetRequiredService<DashboardViewModel>();
    }
}