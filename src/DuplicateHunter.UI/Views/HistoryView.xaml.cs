using DuplicateHunter.UI.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace DuplicateHunter.UI.Views
{
    public partial class HistoryView : System.Windows.Controls.UserControl
    {
        public HistoryView()
        {
            InitializeComponent();

            var vm = App.Host?.Services.GetService<DashboardViewModel>();
            if (vm != null)
            {
                DataContext = vm;
                _ = vm.LoadHistoryAsync();
            }
        }
    }
}
