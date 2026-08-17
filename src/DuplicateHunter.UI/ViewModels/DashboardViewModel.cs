using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Diagnostics;
using System.IO;
using System.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DuplicateHunter.Database;
using DuplicateHunter.Models;
using DuplicateHunter.Services;
using DuplicateHunter.UI.Services;

namespace DuplicateHunter.UI.ViewModels;

public partial class DashboardViewModel : ObservableObject
{
    private readonly FolderPickerService _folderPickerService;
    private readonly FileScannerService _fileScannerService;
    private readonly ScanStatisticsService _statisticsService;
    private readonly DuplicateGroupingService _duplicateGroupingService;
    private readonly IFileOperationService _fileOperationService;
    private readonly IDialogService _dialogService;
    private readonly DuplicateManagementService _duplicateManagementService;
    private readonly IReportExportService _reportExportService;
    private readonly IDuplicateHunterDatabaseService _databaseService;
    private readonly IAppSettingsService _appSettingsService;

    private CancellationTokenSource? _cancellationTokenSource;
    private List<DuplicateFile> _allScannedFiles = new();
    private DateTime _scanStartedUtc;
    private bool _isUpdatingSelection;

    public DashboardViewModel(
        FolderPickerService folderPickerService,
        FileScannerService fileScannerService,
        ScanStatisticsService statisticsService,
        DuplicateGroupingService duplicateGroupingService,
        IFileOperationService fileOperationService,
        IDialogService dialogService,
        DuplicateManagementService duplicateManagementService,
        IReportExportService reportExportService,
        IDuplicateHunterDatabaseService databaseService,
        IAppSettingsService appSettingsService)
    {
        _folderPickerService = folderPickerService;
        _fileScannerService = fileScannerService;
        _statisticsService = statisticsService;
        _duplicateGroupingService = duplicateGroupingService;
        _fileOperationService = fileOperationService;
        _dialogService = dialogService;
        _duplicateManagementService = duplicateManagementService;
        _reportExportService = reportExportService;
        _databaseService = databaseService;
        _appSettingsService = appSettingsService;

        SelectedFileItems.CollectionChanged += OnSelectedFileItemsCollectionChanged;
    }

    // =========================
    // Properties
    // =========================

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

    [ObservableProperty]
    private DuplicateGroup? selectedDuplicateGroup;

    [ObservableProperty]
    private DuplicateFile? selectedFile;

    public ObservableCollection<DuplicateFile> SelectedFileItems { get; } = new();

    partial void OnSelectedFileChanged(DuplicateFile? value)
    {
        OpenFileCommand.NotifyCanExecuteChanged();
        OpenFolderCommand.NotifyCanExecuteChanged();
        CopyFullPathCommand.NotifyCanExecuteChanged();
        CopySha256Command.NotifyCanExecuteChanged();
        MoveToRecycleBinCommand.NotifyCanExecuteChanged();
        DeletePermanentlyCommand.NotifyCanExecuteChanged();
        DeleteAllExceptOneCommand.NotifyCanExecuteChanged();
    }

    partial void OnSelectedDuplicateGroupChanged(DuplicateGroup? value)
    {
        try
        {
            _isUpdatingSelection = true;
            SelectedFiles.Clear();
            SelectedFileItems.Clear();
            SelectedFile = null;

            if (value != null)
            {
                foreach (var file in value.Files)
                {
                    SelectedFiles.Add(file);
                }
            }
        }
        finally
        {
            _isUpdatingSelection = false;
        }

        CopySha256Command.NotifyCanExecuteChanged();
        DeleteAllExceptOneCommand.NotifyCanExecuteChanged();
        KeepNewestCommand.NotifyCanExecuteChanged();
        KeepOldestCommand.NotifyCanExecuteChanged();
        KeepLargestCommand.NotifyCanExecuteChanged();
        KeepSmallestCommand.NotifyCanExecuteChanged();
        MoveToRecycleBinCommand.NotifyCanExecuteChanged();
        DeletePermanentlyCommand.NotifyCanExecuteChanged();
    }

    // =========================
    // Phase 5 Search & Filter Properties
    // =========================

    [ObservableProperty]
    private string searchText = string.Empty;

    [ObservableProperty]
    private string filterExtension = string.Empty;

    [ObservableProperty]
    private string filterFolder = string.Empty;

    [ObservableProperty]
    private double? minFileSizeMB;

    [ObservableProperty]
    private double? maxFileSizeMB;

    [ObservableProperty]
    private string filterGroupText = string.Empty;

    [ObservableProperty]
    private string theme = "System";

    [ObservableProperty]
    private string ignoreFoldersText = string.Empty;

    [ObservableProperty]
    private string ignoreExtensionsText = string.Empty;

    [ObservableProperty]
    private double? defaultMinimumFileSizeMB;

    [ObservableProperty]
    private double? defaultMaximumFileSizeMB;

    // Filter Change Handlers
    partial void OnSearchTextChanged(string value) => ApplyFilters();
    partial void OnFilterExtensionChanged(string value) => ApplyFilters();
    partial void OnFilterFolderChanged(string value) => ApplyFilters();
    partial void OnMinFileSizeMBChanged(double? value) => ApplyFilters();
    partial void OnMaxFileSizeMBChanged(double? value) => ApplyFilters();
    partial void OnFilterGroupTextChanged(string value) => ApplyFilters();

    // =========================
    // Properties - History & Selection
    // =========================

    [ObservableProperty]
    private ScanHistoryRecord? selectedScanHistory;

    [ObservableProperty]
    private ReportRecord? selectedReport;

    public ObservableCollection<ScanHistoryRecord> ScanHistory { get; } = new();

    public ObservableCollection<ReportRecord> ReportHistory { get; } = new();

    public ObservableCollection<RecentFolderRecord> RecentFolders { get; } = new();

    public event Action<string>? NavigationRequested;

    [RelayCommand]
    public void Navigate(string viewName)
    {
        NavigationRequested?.Invoke(viewName);
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        var settings = await _appSettingsService.LoadAsync(cancellationToken);
        Theme = settings.Theme;
        IgnoreFoldersText = settings.IgnoreFoldersText;
        IgnoreExtensionsText = settings.IgnoreExtensionsText;
        DefaultMinimumFileSizeMB = settings.DefaultMinimumFileSizeMB;
        DefaultMaximumFileSizeMB = settings.DefaultMaximumFileSizeMB;

        await LoadHistoryAsync(cancellationToken);
    }

    public async Task LoadHistoryAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var scanHistory = await _databaseService.GetScanHistoryAsync(100, cancellationToken);
            ScanHistory.Clear();
            foreach (var record in scanHistory)
            {
                ScanHistory.Add(record);
            }

            var reports = await _databaseService.GetReportsAsync(100, cancellationToken);
            ReportHistory.Clear();
            foreach (var report in reports)
            {
                ReportHistory.Add(report);
            }

            var recent = await _databaseService.GetRecentFoldersAsync(10, cancellationToken);
            RecentFolders.Clear();
            foreach (var folder in recent)
            {
                RecentFolders.Add(folder);
            }
        }
        catch
        {
            // Database read fallback
        }
    }

    [RelayCommand]
    private async Task RefreshHistoryAsync()
    {
        await LoadHistoryAsync();
        _dialogService.ShowInformation("History records have been refreshed.", "History Refreshed");
    }

    [RelayCommand]
    private async Task ClearHistoryAsync()
    {
        if (!_dialogService.Confirm("Are you sure you want to clear all scan history and export reports from the database?", "Clear History"))
            return;

        await _databaseService.ClearScanHistoryAsync();
        await _databaseService.ClearReportsAsync();
        ScanHistory.Clear();
        ReportHistory.Clear();
        _dialogService.ShowInformation("Scan and report history have been cleared.", "History Cleared");
    }

    [RelayCommand]
    private void OpenReport(ReportRecord? report)
    {
        var target = report ?? SelectedReport;
        if (target != null && File.Exists(target.ReportPath))
        {
            _fileOperationService.OpenFile(target.ReportPath);
        }
        else
        {
            _dialogService.ShowWarning("Report file could not be found at path.", "File Not Found");
        }
    }

    [RelayCommand]
    private void OpenReportLocation(ReportRecord? report)
    {
        var target = report ?? SelectedReport;
        if (target != null && File.Exists(target.ReportPath))
        {
            _fileOperationService.OpenFolder(target.ReportPath);
        }
        else
        {
            _dialogService.ShowWarning("Report folder could not be found.", "Folder Not Found");
        }
    }

    [RelayCommand]
    private void SelectRecentFolder(RecentFolderRecord? folder)
    {
        if (folder != null && !string.IsNullOrWhiteSpace(folder.FolderPath))
        {
            SelectedFolder = folder.FolderPath;
            Navigate("Scan");
        }
    }

    // =========================
    // Collections
    // =========================

    public ObservableCollection<DuplicateGroup> DuplicateResults { get; } = new();

    public ObservableCollection<DuplicateFile> SelectedFiles { get; } = new();

    // =========================
    // Commands - Scanning & Folder
    // =========================

    [RelayCommand]
    private void BrowseFolder()
    {
        var folder = _folderPickerService.PickFolder();
        if (!string.IsNullOrWhiteSpace(folder))
        {
            SelectedFolder = folder;
        }
    }

    [RelayCommand]
    private async Task StartScanAsync()
    {
        string folder = SelectedFolder;
        if (string.IsNullOrWhiteSpace(folder) || folder == "No folder selected" || !Directory.Exists(folder))
        {
            var picked = _folderPickerService.PickFolder();
            if (string.IsNullOrWhiteSpace(picked))
                return;

            folder = picked;
            SelectedFolder = folder;
        }

        _scanStartedUtc = DateTime.UtcNow;

        await _databaseService.AddRecentFolderAsync(folder);

        IsScanning = true;
        ScanProgress = 0;
        CurrentFile = "Preparing...";
        ScanStatus = "Starting scan...";

        DuplicateResults.Clear();
        SelectedFiles.Clear();
        SelectedFileItems.Clear();
        SelectedFile = null;
        SelectedDuplicateGroup = null;
        _allScannedFiles.Clear();

        _cancellationTokenSource = new CancellationTokenSource();

        try
        {
            _allScannedFiles = await _fileScannerService.ScanAsync(
                folder,
                new Progress<ScanProgress>(progress =>
                {
                    ScanProgress = progress.Percentage;
                    CurrentFile = progress.CurrentFile;
                    ScanStatus = $"Scanning {progress.FilesScanned} of {progress.TotalFiles}";
                }),
                _cancellationTokenSource.Token,
                BuildScanSettings());

            RefreshResultsFromFiles();

            ScanProgress = 100;
            ScanStatus = "Scan Complete";
            CurrentFile = "Ready";

            await PersistScanHistoryAsync("Completed");
        }
        catch (OperationCanceledException)
        {
            ScanStatus = "Scan Cancelled";
            CurrentFile = "Ready";
            await PersistScanHistoryAsync("Cancelled");
        }
        catch (Exception ex)
        {
            ScanStatus = "Scan Failed";
            CurrentFile = "Ready";
            _dialogService.ShowError($"Scan failed:\n{ex.Message}", "Scan Error");
            await PersistScanHistoryAsync("Failed", ex.Message);
        }
        finally
        {
            IsScanning = false;

            _cancellationTokenSource?.Dispose();
            _cancellationTokenSource = null;

            await LoadHistoryAsync();
        }
    }

    [RelayCommand]
    private void StopScan()
    {
        _cancellationTokenSource?.Cancel();
    }

    [RelayCommand]
    private void RefreshResults()
    {
        RefreshResultsFromFiles();
    }

    // =========================
    // Commands - Phase 6 Reports & Exporting
    // =========================

    [RelayCommand]
    private async Task ExportCsvAsync()
    {
        if (DuplicateResults.Count == 0)
        {
            _dialogService.ShowWarning("No duplicate results to export. Please run a scan first.", "Export Warning");
            return;
        }

        string? filePath = _dialogService.SaveFilePicker("DuplicateHunter_Report.csv", "CSV Files (*.csv)|*.csv|All Files (*.*)|*.*");
        if (string.IsNullOrWhiteSpace(filePath)) return;

        var stats = BuildStatisticsFromGroups(DuplicateResults);
        await _reportExportService.ExportToCsvAsync(filePath, DuplicateResults, stats);
        await PersistReportAsync(filePath, "csv", stats);
        await LoadHistoryAsync();
        _dialogService.ShowInformation($"CSV report successfully exported to:\n{filePath}", "Export Successful");
    }

    [RelayCommand]
    private async Task ExportJsonAsync()
    {
        if (DuplicateResults.Count == 0)
        {
            _dialogService.ShowWarning("No duplicate results to export. Please run a scan first.", "Export Warning");
            return;
        }

        string? filePath = _dialogService.SaveFilePicker("DuplicateHunter_Report.json", "JSON Files (*.json)|*.json|All Files (*.*)|*.*");
        if (string.IsNullOrWhiteSpace(filePath)) return;

        var stats = BuildStatisticsFromGroups(DuplicateResults);
        await _reportExportService.ExportToJsonAsync(filePath, DuplicateResults, stats);
        await PersistReportAsync(filePath, "json", stats);
        await LoadHistoryAsync();
        _dialogService.ShowInformation($"JSON report successfully exported to:\n{filePath}", "Export Successful");
    }

    [RelayCommand]
    private async Task ExportExcelAsync()
    {
        if (DuplicateResults.Count == 0)
        {
            _dialogService.ShowWarning("No duplicate results to export. Please run a scan first.", "Export Warning");
            return;
        }

        string? filePath = _dialogService.SaveFilePicker("DuplicateHunter_Report.xlsx", "Excel Files (*.xlsx)|*.xlsx|All Files (*.*)|*.*");
        if (string.IsNullOrWhiteSpace(filePath)) return;

        var stats = BuildStatisticsFromGroups(DuplicateResults);
        await _reportExportService.ExportToExcelAsync(filePath, DuplicateResults, stats);
        await PersistReportAsync(filePath, "xlsx", stats);
        await LoadHistoryAsync();
        _dialogService.ShowInformation($"Excel spreadsheet report successfully exported to:\n{filePath}", "Export Successful");
    }

    [RelayCommand]
    private async Task ExportPdfAsync()
    {
        if (DuplicateResults.Count == 0)
        {
            _dialogService.ShowWarning("No duplicate results to export. Please run a scan first.", "Export Warning");
            return;
        }

        string? filePath = _dialogService.SaveFilePicker("DuplicateHunter_Report.pdf", "PDF Files (*.pdf)|*.pdf|All Files (*.*)|*.*");
        if (string.IsNullOrWhiteSpace(filePath)) return;

        var stats = BuildStatisticsFromGroups(DuplicateResults);
        await _reportExportService.ExportToPdfAsync(filePath, DuplicateResults, stats);
        await PersistReportAsync(filePath, "pdf", stats);
        await LoadHistoryAsync();
        _dialogService.ShowInformation($"PDF summary report successfully exported to:\n{filePath}", "Export Successful");
    }

    // =========================
    // Commands - Phase 5 Search & Filter
    // =========================

    [RelayCommand]
    private void ClearFilters()
    {
        SearchText = string.Empty;
        FilterExtension = string.Empty;
        FilterFolder = string.Empty;
        MinFileSizeMB = null;
        MaxFileSizeMB = null;
        FilterGroupText = string.Empty;
        ApplyFilters();
    }

    [RelayCommand]
    private async Task SaveSettingsAsync()
    {
        await _appSettingsService.SaveAsync(BuildScanSettings());
        _dialogService.ShowInformation("Settings saved successfully.", "Settings Saved");
    }

    [RelayCommand]
    private async Task ResetSettingsAsync()
    {
        Theme = "System";
        IgnoreFoldersText = string.Empty;
        IgnoreExtensionsText = string.Empty;
        DefaultMinimumFileSizeMB = null;
        DefaultMaximumFileSizeMB = null;

        await SaveSettingsAsync();
    }

    // =========================
    // Commands - File & Clipboard Operations (Phase 2)
    // =========================

    [RelayCommand(CanExecute = nameof(CanOpenSelectedFile))]
    private void OpenFile()
    {
        var file = GetPrimarySelectedFile();
        if (file == null)
            return;

        _fileOperationService.OpenFile(file.FilePath);
    }

    [RelayCommand(CanExecute = nameof(CanOpenSelectedFile))]
    private void OpenFolder()
    {
        var file = GetPrimarySelectedFile();
        if (file == null)
            return;

        _fileOperationService.OpenFolder(file.FilePath);
    }

    [RelayCommand(CanExecute = nameof(CanOpenSelectedFile))]
    private void CopyFullPath()
    {
        var files = GetSelectedFilesForAction().ToList();
        if (files.Count == 0)
            return;

        if (files.Count == 1)
        {
            _fileOperationService.CopyToClipboard(files[0].FilePath);
            return;
        }

        _fileOperationService.CopyToClipboard(string.Join(Environment.NewLine, files.Select(file => file.FilePath)));
    }

    [RelayCommand(CanExecute = nameof(CanCopySha256))]
    private void CopySha256()
    {
        var files = GetSelectedFilesForAction().ToList();

        if (files.Count > 0)
        {
            if (files.Count == 1)
            {
                _fileOperationService.CopyToClipboard(files[0].Hash);
                return;
            }

            _fileOperationService.CopyToClipboard(string.Join(Environment.NewLine, files.Select(file => file.Hash)));
            return;
        }

        string? hash = SelectedDuplicateGroup?.Hash;
        if (!string.IsNullOrEmpty(hash))
        {
            _fileOperationService.CopyToClipboard(hash);
        }
    }

    // =========================
    // Commands - Phase 4 Selection Improvements
    // =========================

    [RelayCommand]
    private void SelectAll()
    {
        var targetFiles = SelectedFiles.Count > 0
            ? SelectedFiles
            : DuplicateResults.SelectMany(g => g.Files);

        foreach (var file in targetFiles)
        {
            file.IsSelected = true;
        }

        ReplaceSelectedFileItems(targetFiles);
    }

    [RelayCommand]
    private void DeselectAll()
    {
        foreach (var group in DuplicateResults)
        {
            foreach (var file in group.Files)
            {
                file.IsSelected = false;
            }
        }

        try
        {
            _isUpdatingSelection = true;
            SelectedFileItems.Clear();
            SelectedFile = null;
        }
        finally
        {
            _isUpdatingSelection = false;
        }

        OpenFileCommand.NotifyCanExecuteChanged();
        OpenFolderCommand.NotifyCanExecuteChanged();
        CopyFullPathCommand.NotifyCanExecuteChanged();
        CopySha256Command.NotifyCanExecuteChanged();
        MoveToRecycleBinCommand.NotifyCanExecuteChanged();
        DeletePermanentlyCommand.NotifyCanExecuteChanged();
        DeleteAllExceptOneCommand.NotifyCanExecuteChanged();
    }

    [RelayCommand]
    private void SelectByFolder()
    {
        string? input = _dialogService.PromptInput(
            "Enter folder path or keyword to select files from (e.g., Downloads, Temp):",
            "Select Files by Folder");

        if (string.IsNullOrWhiteSpace(input))
            return;

        string filter = input.Trim().ToLowerInvariant();
        int count = 0;

        foreach (var group in DuplicateResults)
        {
            foreach (var file in group.Files)
            {
                if (file.DirectoryName.ToLowerInvariant().Contains(filter) ||
                    file.FilePath.ToLowerInvariant().Contains(filter))
                {
                    file.IsSelected = true;
                    count++;
                }
            }
        }

        AddSelectedFileItems(DuplicateResults
            .SelectMany(g => g.Files)
            .Where(file => file.DirectoryName.ToLowerInvariant().Contains(filter) || file.FilePath.ToLowerInvariant().Contains(filter)));

        _dialogService.ShowInformation($"Selected {count} file(s) matching folder filter '{input}'.", "Selection Complete");
    }

    [RelayCommand]
    private void SelectByExtension()
    {
        string? input = _dialogService.PromptInput(
            "Enter file extension to select (e.g., .jpg, .pdf, .tmp, .txt):",
            "Select Files by Extension");

        if (string.IsNullOrWhiteSpace(input))
            return;

        string ext = input.Trim().ToLowerInvariant();
        if (!ext.StartsWith("."))
            ext = "." + ext;

        int count = 0;

        foreach (var group in DuplicateResults)
        {
            foreach (var file in group.Files)
            {
                if (file.Extension == ext)
                {
                    file.IsSelected = true;
                    count++;
                }
            }
        }

        AddSelectedFileItems(DuplicateResults.SelectMany(g => g.Files).Where(file => file.Extension == ext));

        _dialogService.ShowInformation($"Selected {count} file(s) with extension '{ext}'.", "Selection Complete");
    }

    [RelayCommand]
    private void DeleteSelected()
    {
        var selectedFiles = GetSelectedFilesForDeletion().ToList();

        if (selectedFiles.Count == 0)
        {
            _dialogService.ShowWarning(
                "No files selected. Use row selection or checkboxes to choose the file(s) you wish to delete.",
                "No Selection");
            return;
        }

        long totalSizeBytes = selectedFiles.Sum(f => f.Size);
        string formattedSize = FormatByteSize(totalSizeBytes);

        if (!_dialogService.Confirm(
            $"Are you sure you want to move {selectedFiles.Count} selected file(s) ({formattedSize}) to the Recycle Bin?",
            "Move Selected Files to Recycle Bin"))
        {
            return;
        }

        int deletedCount = 0;
        foreach (var file in selectedFiles)
        {
            if (_duplicateManagementService.DeleteFile(file, permanent: false))
            {
                deletedCount++;
                _allScannedFiles.RemoveAll(f => f.FilePath == file.FilePath);
            }
        }

        _dialogService.ShowInformation($"{deletedCount} file(s) moved to Recycle Bin.", "Operation Complete");
        RefreshResultsFromFiles();
    }

    // =========================
    // Commands - Safe Duplicate Management (Phase 3)
    // =========================

    [RelayCommand(CanExecute = nameof(CanDeleteSelectedFile))]
    private void MoveToRecycleBin()
    {
        if (SelectedFile == null)
            return;

        if (!_dialogService.Confirm(
            $"Are you sure you want to move this file to the Recycle Bin?\n\n{SelectedFile.FilePath}",
            "Move to Recycle Bin"))
        {
            return;
        }

        if (_duplicateManagementService.DeleteFile(SelectedFile, permanent: false))
        {
            RemoveFileFromResults(SelectedFile);
        }
        else
        {
            _dialogService.ShowError("Could not move file to Recycle Bin.", "Operation Failed");
        }
    }

    [RelayCommand(CanExecute = nameof(CanDeleteSelectedFile))]
    private void DeletePermanently()
    {
        if (SelectedFile == null)
            return;

        if (!_dialogService.Confirm(
            $"WARNING: Are you sure you want to PERMANENTLY delete this file?\nThis operation CANNOT be undone!\n\n{SelectedFile.FilePath}",
            "Permanent Delete Warning"))
        {
            return;
        }

        if (_duplicateManagementService.DeleteFile(SelectedFile, permanent: true))
        {
            RemoveFileFromResults(SelectedFile);
        }
        else
        {
            _dialogService.ShowError("Could not permanently delete file.", "Operation Failed");
        }
    }

    [RelayCommand(CanExecute = nameof(CanDeleteAllExceptOne))]
    private void DeleteAllExceptOne()
    {
        if (SelectedDuplicateGroup == null || SelectedFile == null)
        {
            _dialogService.ShowWarning("Please select a specific file in a duplicate group to keep.", "Selection Required");
            return;
        }

        if (!_dialogService.Confirm(
            $"Keep this file:\n{SelectedFile.FilePath}\n\nAnd move all other {SelectedDuplicateGroup.Files.Count - 1} duplicate file(s) in this group to the Recycle Bin?",
            "Keep Selected File Only"))
        {
            return;
        }

        int deletedCount = _duplicateManagementService.DeleteAllExceptOne(SelectedDuplicateGroup, SelectedFile, permanent: false);
        _dialogService.ShowInformation($"{deletedCount} duplicate file(s) moved to Recycle Bin.", "Operation Complete");
        RefreshResultsFromFiles();
    }

    [RelayCommand(CanExecute = nameof(CanManageSelectedGroup))]
    private void KeepNewest()
    {
        if (SelectedDuplicateGroup == null)
            return;

        if (!_dialogService.Confirm(
            $"Keep the NEWEST file in group (Hash: {SelectedDuplicateGroup.ShortHash}) and move all other duplicate files to the Recycle Bin?",
            "Keep Newest File"))
        {
            return;
        }

        int count = _duplicateManagementService.KeepNewest(SelectedDuplicateGroup, permanent: false);
        _dialogService.ShowInformation($"{count} duplicate file(s) moved to Recycle Bin.", "Operation Complete");
        RefreshResultsFromFiles();
    }

    [RelayCommand(CanExecute = nameof(CanManageSelectedGroup))]
    private void KeepOldest()
    {
        if (SelectedDuplicateGroup == null)
            return;

        if (!_dialogService.Confirm(
            $"Keep the OLDEST file in group (Hash: {SelectedDuplicateGroup.ShortHash}) and move all other duplicate files to the Recycle Bin?",
            "Keep Oldest File"))
        {
            return;
        }

        int count = _duplicateManagementService.KeepOldest(SelectedDuplicateGroup, permanent: false);
        _dialogService.ShowInformation($"{count} duplicate file(s) moved to Recycle Bin.", "Operation Complete");
        RefreshResultsFromFiles();
    }

    [RelayCommand(CanExecute = nameof(CanManageSelectedGroup))]
    private void KeepLargest()
    {
        if (SelectedDuplicateGroup == null)
            return;

        if (!_dialogService.Confirm(
            $"Keep the LARGEST file in group (Hash: {SelectedDuplicateGroup.ShortHash}) and move all other duplicate files to the Recycle Bin?",
            "Keep Largest File"))
        {
            return;
        }

        int count = _duplicateManagementService.KeepLargest(SelectedDuplicateGroup, permanent: false);
        _dialogService.ShowInformation($"{count} duplicate file(s) moved to Recycle Bin.", "Operation Complete");
        RefreshResultsFromFiles();
    }

    [RelayCommand(CanExecute = nameof(CanManageSelectedGroup))]
    private void KeepSmallest()
    {
        if (SelectedDuplicateGroup == null)
            return;

        if (!_dialogService.Confirm(
            $"Keep the SMALLEST file in group (Hash: {SelectedDuplicateGroup.ShortHash}) and move all other duplicate files to the Recycle Bin?",
            "Keep Smallest File"))
        {
            return;
        }

        int count = _duplicateManagementService.KeepSmallest(SelectedDuplicateGroup, permanent: false);
        _dialogService.ShowInformation($"{count} duplicate file(s) moved to Recycle Bin.", "Operation Complete");
        RefreshResultsFromFiles();
    }

    // =========================
    // Selection & Refresh Logic
    // =========================

    private void RemoveFileFromResults(DuplicateFile file)
    {
        _allScannedFiles.RemoveAll(f => f.FilePath == file.FilePath);

        try
        {
            _isUpdatingSelection = true;
            SelectedFiles.Remove(file);
            SelectedFileItems.Remove(file);

            if (SelectedDuplicateGroup != null)
            {
                SelectedDuplicateGroup.Files.RemoveAll(f => f.FilePath == file.FilePath);

                if (SelectedDuplicateGroup.Files.Count <= 1)
                {
                    DuplicateResults.Remove(SelectedDuplicateGroup);
                    SelectedDuplicateGroup = null;
                    SelectedFiles.Clear();
                    SelectedFile = null;
                }
            }
        }
        finally
        {
            _isUpdatingSelection = false;
        }

        RefreshStatistics();
    }

    private void RefreshResultsFromFiles()
    {
        // Re-check existing scanned files for existence
        _allScannedFiles.RemoveAll(f => !System.IO.File.Exists(f.FilePath));

        try
        {
            _isUpdatingSelection = true;
            SelectedFileItems.Clear();
            SelectedFile = null;
        }
        finally
        {
            _isUpdatingSelection = false;
        }

        ApplyFilters();
    }

    private void ApplyFilters()
    {
        IEnumerable<DuplicateFile> filteredFiles = _allScannedFiles;

        // 1. Search text filter (FileName or FilePath)
        if (!string.IsNullOrWhiteSpace(SearchText))
        {
            string search = SearchText.Trim().ToLowerInvariant();
            filteredFiles = filteredFiles.Where(f =>
                f.FileName.ToLowerInvariant().Contains(search) ||
                f.FilePath.ToLowerInvariant().Contains(search));
        }

        // 2. Extension filter
        if (!string.IsNullOrWhiteSpace(FilterExtension))
        {
            string ext = FilterExtension.Trim().ToLowerInvariant();
            if (!ext.StartsWith(".")) ext = "." + ext;
            filteredFiles = filteredFiles.Where(f => f.Extension == ext);
        }

        // 3. Folder filter
        if (!string.IsNullOrWhiteSpace(FilterFolder))
        {
            string folder = FilterFolder.Trim().ToLowerInvariant();
            filteredFiles = filteredFiles.Where(f =>
                f.DirectoryName.ToLowerInvariant().Contains(folder) ||
                f.FilePath.ToLowerInvariant().Contains(folder));
        }

        // 4. Min size filter (MB)
        if (MinFileSizeMB.HasValue && MinFileSizeMB.Value > 0)
        {
            long minBytes = (long)(MinFileSizeMB.Value * 1024 * 1024);
            filteredFiles = filteredFiles.Where(f => f.Size >= minBytes);
        }

        // 5. Max size filter (MB)
        if (MaxFileSizeMB.HasValue && MaxFileSizeMB.Value > 0)
        {
            long maxBytes = (long)(MaxFileSizeMB.Value * 1024 * 1024);
            filteredFiles = filteredFiles.Where(f => f.Size <= maxBytes);
        }

        var list = filteredFiles.ToList();
        var duplicateGroups = _duplicateGroupingService.GroupDuplicates(list);

        if (!string.IsNullOrWhiteSpace(FilterGroupText))
        {
            string groupFilter = FilterGroupText.Trim().ToLowerInvariant();

            if (int.TryParse(groupFilter, out int fileCountFilter))
            {
                duplicateGroups = duplicateGroups.Where(group => group.FileCount == fileCountFilter).ToList();
            }
            else
            {
                duplicateGroups = duplicateGroups.Where(group =>
                    group.Hash.ToLowerInvariant().Contains(groupFilter) ||
                    group.ShortHash.ToLowerInvariant().Contains(groupFilter) ||
                    group.FileCount.ToString().Contains(groupFilter)).ToList();
            }
        }

        var statistics = BuildStatisticsFromGroups(duplicateGroups);

        DuplicateResults.Clear();
        SelectedFiles.Clear();
        SelectedFileItems.Clear();
        SelectedFile = null;
        SelectedDuplicateGroup = null;

        foreach (var group in duplicateGroups)
        {
            DuplicateResults.Add(group);
        }

        FilesScanned = statistics.FilesScanned;
        DuplicateGroups = statistics.DuplicateGroups;
        WastedSpace = statistics.WastedSpace;
    }

    private void RefreshStatistics()
    {
        var statistics = BuildStatisticsFromGroups(DuplicateResults);
        FilesScanned = statistics.FilesScanned;
        DuplicateGroups = statistics.DuplicateGroups;
        WastedSpace = statistics.WastedSpace;
    }

    private async Task PersistScanHistoryAsync(string status, string? errorMessage = null)
    {
        if (string.IsNullOrWhiteSpace(SelectedFolder) || SelectedFolder == "No folder selected")
            return;

        var statistics = _statisticsService.Calculate(_allScannedFiles);

        await _databaseService.SaveScanHistoryAsync(new ScanHistoryRecord(
            SelectedFolder,
            _scanStartedUtc == default ? DateTime.UtcNow : _scanStartedUtc,
            DateTime.UtcNow,
            status,
            statistics.FilesScanned,
            statistics.DuplicateGroups,
            statistics.WastedBytes,
            errorMessage));
    }

    private async Task PersistReportAsync(string reportPath, string format, ScanStatistics statistics)
    {
        if (string.IsNullOrWhiteSpace(SelectedFolder) || SelectedFolder == "No folder selected")
            return;

        await _databaseService.SaveReportAsync(new ReportRecord(
            SelectedFolder,
            reportPath,
            format,
            DateTime.UtcNow,
            statistics.FilesScanned,
            statistics.DuplicateGroups,
            statistics.WastedBytes));
    }

    private ScanSettings BuildScanSettings()
    {
        return new ScanSettings
        {
            Theme = Theme,
            IgnoreFoldersText = IgnoreFoldersText,
            IgnoreExtensionsText = IgnoreExtensionsText,
            DefaultMinimumFileSizeMB = DefaultMinimumFileSizeMB,
            DefaultMaximumFileSizeMB = DefaultMaximumFileSizeMB
        };
    }

    private static ScanStatistics BuildStatisticsFromGroups(IEnumerable<DuplicateGroup> groups)
    {
        var duplicateGroups = groups.ToList();

        long wastedBytes = duplicateGroups.Sum(group =>
        {
            if (group.FileCount <= 1)
                return 0L;

            return (group.FileCount - 1L) * group.Files[0].Size;
        });

        return new ScanStatistics
        {
            FilesScanned = duplicateGroups.Sum(group => group.FileCount),
            DuplicateGroups = duplicateGroups.Count,
            WastedBytes = wastedBytes
        };
    }

    private static string FormatByteSize(long bytes)
    {
        const double KB = 1024;
        const double MB = KB * 1024;
        const double GB = MB * 1024;

        if (bytes >= GB) return $"{bytes / GB:F2} GB";
        if (bytes >= MB) return $"{bytes / MB:F2} MB";
        if (bytes >= KB) return $"{bytes / KB:F2} KB";
        return $"{bytes} Bytes";
    }

    private bool CanOpenSelectedFile() => SelectedFileItems.Count > 0 || SelectedFile != null;

    private bool CanCopySha256() => SelectedFileItems.Count > 0 || SelectedFile != null || SelectedDuplicateGroup != null;

    private bool CanDeleteSelectedFile() => SelectedFileItems.Count > 0 || SelectedFile != null;

    private bool CanManageSelectedGroup() => SelectedDuplicateGroup != null && SelectedDuplicateGroup.Files.Count > 1;

    private bool CanDeleteAllExceptOne() =>
        SelectedDuplicateGroup != null &&
        SelectedFile != null &&
        SelectedDuplicateGroup.Files.Any(file => file.FilePath == SelectedFile.FilePath);

    private void OnSelectedFileItemsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (_isUpdatingSelection)
            return;

        try
        {
            _isUpdatingSelection = true;
            var primary = SelectedFileItems.LastOrDefault();
            if (SelectedFile != primary)
            {
                SelectedFile = primary;
            }

            OpenFileCommand.NotifyCanExecuteChanged();
            OpenFolderCommand.NotifyCanExecuteChanged();
            CopyFullPathCommand.NotifyCanExecuteChanged();
            CopySha256Command.NotifyCanExecuteChanged();
            MoveToRecycleBinCommand.NotifyCanExecuteChanged();
            DeletePermanentlyCommand.NotifyCanExecuteChanged();
            DeleteAllExceptOneCommand.NotifyCanExecuteChanged();
        }
        finally
        {
            _isUpdatingSelection = false;
        }
    }

    private DuplicateFile? GetPrimarySelectedFile()
    {
        return SelectedFileItems.LastOrDefault() ?? SelectedFile;
    }

    private IEnumerable<DuplicateFile> GetSelectedFilesForAction()
    {
        return SelectedFileItems.Count > 0
            ? SelectedFileItems.DistinctBy(file => file.FilePath)
            : SelectedFile != null
                ? new[] { SelectedFile }
                : Enumerable.Empty<DuplicateFile>();
    }

    private IEnumerable<DuplicateFile> GetSelectedFilesForDeletion()
    {
        var checkedFiles = DuplicateResults.SelectMany(group => group.Files).Where(file => file.IsSelected).ToList();
        if (checkedFiles.Count > 0)
        {
            return checkedFiles.DistinctBy(file => file.FilePath);
        }

        return SelectedFileItems.Count > 0
            ? SelectedFileItems.DistinctBy(file => file.FilePath)
            : SelectedFile != null
                ? new[] { SelectedFile }
                : Enumerable.Empty<DuplicateFile>();
    }

    private void ReplaceSelectedFileItems(IEnumerable<DuplicateFile> files)
    {
        if (_isUpdatingSelection)
            return;

        try
        {
            _isUpdatingSelection = true;
            SelectedFileItems.Clear();

            foreach (var file in files.DistinctBy(item => item.FilePath))
            {
                SelectedFileItems.Add(file);
            }

            SelectedFile = SelectedFileItems.LastOrDefault();
        }
        finally
        {
            _isUpdatingSelection = false;
        }

        OpenFileCommand.NotifyCanExecuteChanged();
        OpenFolderCommand.NotifyCanExecuteChanged();
        CopyFullPathCommand.NotifyCanExecuteChanged();
        CopySha256Command.NotifyCanExecuteChanged();
        MoveToRecycleBinCommand.NotifyCanExecuteChanged();
        DeletePermanentlyCommand.NotifyCanExecuteChanged();
        DeleteAllExceptOneCommand.NotifyCanExecuteChanged();
    }

    private void AddSelectedFileItems(IEnumerable<DuplicateFile> files)
    {
        if (_isUpdatingSelection)
            return;

        try
        {
            _isUpdatingSelection = true;
            var existingPaths = SelectedFileItems.Select(file => file.FilePath).ToHashSet(StringComparer.OrdinalIgnoreCase);

            foreach (var file in files)
            {
                if (existingPaths.Add(file.FilePath))
                {
                    SelectedFileItems.Add(file);
                }
            }

            SelectedFile = SelectedFileItems.LastOrDefault();
        }
        finally
        {
            _isUpdatingSelection = false;
        }

        OpenFileCommand.NotifyCanExecuteChanged();
        OpenFolderCommand.NotifyCanExecuteChanged();
        CopyFullPathCommand.NotifyCanExecuteChanged();
        CopySha256Command.NotifyCanExecuteChanged();
        MoveToRecycleBinCommand.NotifyCanExecuteChanged();
        DeletePermanentlyCommand.NotifyCanExecuteChanged();
        DeleteAllExceptOneCommand.NotifyCanExecuteChanged();
    }
}