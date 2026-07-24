using DuplicateHunter.Services;
using DuplicateHunter.UI.Services;
using DuplicateHunter.UI.ViewModels;
using DuplicateHunter.UI.Views;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.Windows;

namespace DuplicateHunter.UI;

public partial class App : System.Windows.Application
{
    public static IHost? Host { get; private set; }

    public App()
    {
        Host = Microsoft.Extensions.Hosting.Host
            .CreateDefaultBuilder()
            .ConfigureServices((context, services) =>
            {
                // Views
                services.AddSingleton<MainWindow>();

                // UI Services
                services.AddSingleton<FolderPickerService>();

                // Core Services
                services.AddSingleton<FileEnumerationService>();
                services.AddSingleton<HashService>();
                services.AddSingleton<FileScannerService>();
                services.AddSingleton<ScanStatisticsService>();

                // ViewModels
                services.AddSingleton<DashboardViewModel>();

            })
            .Build();
    }

    protected override async void OnStartup(StartupEventArgs e)
    {
        await Host!.StartAsync();

        var mainWindow = Host.Services.GetRequiredService<MainWindow>();
        mainWindow.Show();

        base.OnStartup(e);
    }

    protected override async void OnExit(ExitEventArgs e)
    {
        if (Host != null)
            await Host.StopAsync();

        base.OnExit(e);
    }
}