using CommunityToolkit.Mvvm.ComponentModel;
using DuplicateHunter.Models;
using System.Collections.ObjectModel;

namespace DuplicateHunter.UI.ViewModels;

public partial class ResultsViewModel : ObservableObject
{
    public ObservableCollection<DuplicateGroup> DuplicateResults { get; }

    public ResultsViewModel(DashboardViewModel dashboardViewModel)
    {
        DuplicateResults = dashboardViewModel.DuplicateResults;
    }
}