using Microsoft.Extensions.DependencyInjection;
using DuplicateHunter.UI.ViewModels;

namespace DuplicateHunter.UI.Views;

public partial class ResultsView : System.Windows.Controls.UserControl
{
    public ResultsView()
    {
        InitializeComponent();

        DataContext = App.Host!.Services.GetRequiredService<DashboardViewModel>();
    }
}