using System.Globalization;
using DuplicateHunter.Database;
using DuplicateHunter.Models;

namespace DuplicateHunter.Services;

public class AppSettingsService : IAppSettingsService
{
    private const string ThemeKey = "Theme";
    private const string IgnoreFoldersKey = "IgnoreFolders";
    private const string IgnoreExtensionsKey = "IgnoreExtensions";
    private const string MinimumFileSizeKey = "MinimumFileSizeMB";
    private const string MaximumFileSizeKey = "MaximumFileSizeMB";

    private readonly IDuplicateHunterDatabaseService _databaseService;

    public AppSettingsService(IDuplicateHunterDatabaseService databaseService)
    {
        _databaseService = databaseService;
    }

    public async Task<ScanSettings> LoadAsync(CancellationToken cancellationToken = default)
    {
        string theme = await _databaseService.GetSettingAsync(ThemeKey, cancellationToken) ?? "System";
        string ignoreFoldersText = await _databaseService.GetSettingAsync(IgnoreFoldersKey, cancellationToken) ?? string.Empty;
        string ignoreExtensionsText = await _databaseService.GetSettingAsync(IgnoreExtensionsKey, cancellationToken) ?? string.Empty;
        double? minimumFileSizeMB = ParseNullableDouble(await _databaseService.GetSettingAsync(MinimumFileSizeKey, cancellationToken));
        double? maximumFileSizeMB = ParseNullableDouble(await _databaseService.GetSettingAsync(MaximumFileSizeKey, cancellationToken));

        return new ScanSettings
        {
            Theme = string.IsNullOrWhiteSpace(theme) ? "System" : theme,
            IgnoreFoldersText = ignoreFoldersText,
            IgnoreExtensionsText = ignoreExtensionsText,
            DefaultMinimumFileSizeMB = minimumFileSizeMB,
            DefaultMaximumFileSizeMB = maximumFileSizeMB
        };
    }

    public async Task SaveAsync(ScanSettings settings, CancellationToken cancellationToken = default)
    {
        await _databaseService.SetSettingAsync(ThemeKey, settings.Theme, cancellationToken);
        await _databaseService.SetSettingAsync(IgnoreFoldersKey, settings.IgnoreFoldersText, cancellationToken);
        await _databaseService.SetSettingAsync(IgnoreExtensionsKey, settings.IgnoreExtensionsText, cancellationToken);
        await _databaseService.SetSettingAsync(MinimumFileSizeKey, ToSettingValue(settings.DefaultMinimumFileSizeMB), cancellationToken);
        await _databaseService.SetSettingAsync(MaximumFileSizeKey, ToSettingValue(settings.DefaultMaximumFileSizeMB), cancellationToken);
    }

    private static string ToSettingValue(double? value)
    {
        return value.HasValue ? value.Value.ToString(CultureInfo.InvariantCulture) : string.Empty;
    }

    private static double? ParseNullableDouble(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        return double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out double result)
            ? result
            : null;
    }
}
