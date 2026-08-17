using DuplicateHunter.Services;
using DuplicateHunter.Database;
using DuplicateHunter.UI.Services;
using DuplicateHunter.UI.ViewModels;
using DuplicateHunter.UI.Views;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.IO;
using System.Windows;

namespace DuplicateHunter.UI;

public partial class App : System.Windows.Application
{
    public static IHost? Host { get; private set; }

    public App()
    {
        this.DispatcherUnhandledException += (s, args) =>
        {
            File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "crash.log"), args.Exception.ToString());
        };

        AppDomain.CurrentDomain.UnhandledException += (s, args) =>
        {
            File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "crash_domain.log"), args.ExceptionObject?.ToString() ?? "Unknown exception");
        };

        Host = Microsoft.Extensions.Hosting.Host
            .CreateDefaultBuilder()
            .ConfigureServices((context, services) =>
            {
                // =========================
                // Views
                // =========================

                services.AddSingleton<MainWindow>();
                services.AddSingleton<ResultsView>();

                // =========================
                // UI Services
                // =========================

                services.AddSingleton<FolderPickerService>();
                services.AddSingleton<IDialogService, WpfDialogService>();
                services.AddSingleton<IAppSettingsService, AppSettingsService>();

                // =========================
                // Core Services
                // =========================

                string databasePath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "DuplicateHunter",
                    "duplicate-hunter.db");

                services.AddSingleton(new DuplicateHunterDatabaseOptions(databasePath));
                services.AddSingleton<IDuplicateHunterDatabaseService, SqliteDuplicateHunterDatabaseService>();

                services.AddSingleton<IFileOperationService, FileOperationService>();
                services.AddSingleton<FileEnumerationService>();
                services.AddSingleton<HashService>();
                services.AddSingleton<FileScannerService>();
                services.AddSingleton<ScanStatisticsService>();
                services.AddSingleton<DuplicateGroupingService>();
                services.AddSingleton<DuplicateManagementService>();
                services.AddSingleton<IReportExportService, ReportExportService>();

                // =========================
                // ViewModels
                // =========================

                services.AddSingleton<DashboardViewModel>();
                services.AddSingleton<ResultsViewModel>();
                services.AddSingleton<MainViewModel>();

            })
            .Build();
    }

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        if (Host == null) return;

        Host.Start();

        var mainWindow = Host.Services.GetRequiredService<MainWindow>();
        MainWindow = mainWindow;
        mainWindow.Show();

        _ = InitializeServicesAsync();
    }

    private async Task InitializeServicesAsync()
    {
        try
        {
            await Host!.Services.GetRequiredService<IDuplicateHunterDatabaseService>().InitializeAsync();
            await Host.Services.GetRequiredService<DashboardViewModel>().InitializeAsync();
        }
        catch (Exception ex)
        {
            File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "startup_error.log"), ex.ToString());
        }
    }

    protected override async void OnExit(ExitEventArgs e)
    {
        if (Host != null)
            await Host.StopAsync();

        base.OnExit(e);
    }
}