using DuplicateHunter.Database;
using DuplicateHunter.Models;
using DuplicateHunter.Services;
using Xunit;

namespace DuplicateHunter.Tests;

public class AppSettingsServiceTests
{
    [Fact]
    public async Task AppSettingsService_LoadsDefaultsWhenEmpty()
    {
        string tempDirectory = Path.Combine(Path.GetTempPath(), "DuplicateHunter.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDirectory);
        string databasePath = Path.Combine(tempDirectory, "test_settings_1.db");

        using var dbService = new SqliteDuplicateHunterDatabaseService(new DuplicateHunterDatabaseOptions(databasePath));
        await dbService.InitializeAsync();

        var settingsService = new AppSettingsService(dbService);

        try
        {
            // Act
            var settings = await settingsService.LoadAsync();

            // Assert
            Assert.Equal("System", settings.Theme);
            Assert.Empty(settings.IgnoreFoldersText);
            Assert.Empty(settings.IgnoreExtensionsText);
            Assert.Null(settings.DefaultMinimumFileSizeMB);
            Assert.Null(settings.DefaultMaximumFileSizeMB);
        }
        finally
        {
            dbService.Dispose();
            if (Directory.Exists(tempDirectory))
                Directory.Delete(tempDirectory, recursive: true);
        }
    }

    [Fact]
    public async Task AppSettingsService_SavesAndReloadsConfiguredSettings()
    {
        string tempDirectory = Path.Combine(Path.GetTempPath(), "DuplicateHunter.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDirectory);
        string databasePath = Path.Combine(tempDirectory, "test_settings_2.db");

        using var dbService = new SqliteDuplicateHunterDatabaseService(new DuplicateHunterDatabaseOptions(databasePath));
        await dbService.InitializeAsync();

        var settingsService = new AppSettingsService(dbService);

        var originalSettings = new ScanSettings
        {
            Theme = "Dark",
            IgnoreFoldersText = "node_modules, .git, AppData",
            IgnoreExtensionsText = ".dll, .exe, .tmp",
            DefaultMinimumFileSizeMB = 1.5,
            DefaultMaximumFileSizeMB = 500.0
        };

        try
        {
            // Act
            await settingsService.SaveAsync(originalSettings);
            var reloadedSettings = await settingsService.LoadAsync();

            // Assert
            Assert.Equal("Dark", reloadedSettings.Theme);
            Assert.Equal("node_modules, .git, AppData", reloadedSettings.IgnoreFoldersText);
            Assert.Equal(".dll, .exe, .tmp", reloadedSettings.IgnoreExtensionsText);
            Assert.Equal(1.5, reloadedSettings.DefaultMinimumFileSizeMB);
            Assert.Equal(500.0, reloadedSettings.DefaultMaximumFileSizeMB);
        }
        finally
        {
            dbService.Dispose();
            if (Directory.Exists(tempDirectory))
                Directory.Delete(tempDirectory, recursive: true);
        }
    }
}
