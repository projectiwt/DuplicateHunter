using System.Threading;
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

    private CancellationTokenSource? _cancellationTokenSource;

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

    [ObservableProperty]
    private double scanProgress;

    [ObservableProperty]
    private string currentFile = "Ready";

    [ObservableProperty]
    private string scanStatus = "Idle";

    [RelayCommand]
    private async Task StartScanAsync()
    {
        var folder = _folderPickerService.PickFolder();

        if (string.IsNullOrWhiteSpace(folder))
            return;

        SelectedFolder = folder;

        IsScanning = true;

        ScanProgress = 0;
        CurrentFile = "Preparing...";
        ScanStatus = "Starting scan...";

        _cancellationTokenSource = new CancellationTokenSource();

        try
        {
            var files = await _fileScannerService.ScanAsync(
                folder,
                new Progress<DuplicateHunter.Models.ScanProgress>(progress =>
                {
                    ScanProgress = progress.Percentage;
                    CurrentFile = progress.CurrentFile;
                    ScanStatus = $"Scanning {progress.FilesScanned} of {progress.TotalFiles}";
                }),
                _cancellationTokenSource.Token);

            var statistics = _statisticsService.Calculate(files);

            FilesScanned = statistics.FilesScanned;
            DuplicateGroups = statistics.DuplicateGroups;
            WastedSpace = statistics.WastedSpace;

            ScanProgress = 100;
            ScanStatus = "Scan Complete";
            CurrentFile = "Ready";
        }
        catch (OperationCanceledException)
        {
            ScanStatus = "Scan Cancelled";
            CurrentFile = "Ready";
        }
        finally
        {
            IsScanning = false;

            _cancellationTokenSource?.Dispose();
            _cancellationTokenSource = null;
        }
    }

    [RelayCommand]
    private void StopScan()
    {
        _cancellationTokenSource?.Cancel();
    }
}