using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DuplicateHunter.Services;
using DuplicateHunter.UI.Services;

namespace DuplicateHunter.UI.ViewModels;

public partial class DashboardViewModel : ObservableObject
{
    private readonly FolderPickerService _folderPickerService;
    private readonly FileScannerService _fileScannerService;
    private readonly ScanStatisticsService _statisticsService;

    public DashboardViewModel(
        FolderPickerService folderPickerService,
        FileScannerService fileScannerService,
        ScanStatisticsService statisticsService)
    {
        _folderPickerService = folderPickerService;
        _fileScannerService = fileScannerService;
        _statisticsService = statisticsService;
    }

    [ObservableProperty]
    private string selectedFolder = "No folder selected";

    [ObservableProperty]
    private int filesScanned;

    [ObservableProperty]
    private int duplicateGroups;

    [ObservableProperty]
    private string wastedSpace = "0 MB";

    [ObservableProperty]
    private bool isScanning;

    [RelayCommand]
    private async Task StartScanAsync()
    {
        var folder = _folderPickerService.PickFolder();

        if (string.IsNullOrWhiteSpace(folder))
            return;

        SelectedFolder = folder;

        IsScanning = true;

        var files = await _fileScannerService.ScanAsync(folder);

        var statistics = _statisticsService.Calculate(files);

        FilesScanned = statistics.FilesScanned;
        DuplicateGroups = statistics.DuplicateGroups;
        WastedSpace = statistics.WastedSpace;

        IsScanning = false;
    }
}