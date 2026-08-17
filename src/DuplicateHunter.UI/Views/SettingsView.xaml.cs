using DuplicateHunter.UI.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace DuplicateHunter.UI.Views
{
    public partial class SettingsView : System.Windows.Controls.UserControl
    {
        public SettingsView()
        {
            InitializeComponent();

            var vm = App.Host?.Services.GetService<DashboardViewModel>();
            if (vm != null)
            {
                DataContext = vm;
            }
        }
    }
}
