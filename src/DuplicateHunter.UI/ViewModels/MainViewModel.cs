using CommunityToolkit.Mvvm.ComponentModel;

namespace DuplicateHunter.UI.ViewModels;

public partial class MainViewModel : ObservableObject
{
    [ObservableProperty]
    private object? currentView;

    public MainViewModel(DashboardViewModel dashboardViewModel)
    {
        CurrentView = dashboardViewModel;
    }
}