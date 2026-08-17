namespace DuplicateHunter.Models;

public sealed record ScanSettings
{
    public string Theme { get; init; } = "System";

    public string IgnoreFoldersText { get; init; } = string.Empty;

    public string IgnoreExtensionsText { get; init; } = string.Empty;

    public double? DefaultMinimumFileSizeMB { get; init; }

    public double? DefaultMaximumFileSizeMB { get; init; }
}
