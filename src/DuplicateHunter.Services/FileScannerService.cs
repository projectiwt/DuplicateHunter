using System.Threading;
using DuplicateHunter.Models;

namespace DuplicateHunter.Services;

public class FileScannerService
{
    private readonly FileEnumerationService _fileEnumerationService;
    private readonly HashService _hashService;

    public FileScannerService(
        FileEnumerationService fileEnumerationService,
        HashService hashService)
    {
        _fileEnumerationService = fileEnumerationService;
        _hashService = hashService;
    }

    public async Task<List<DuplicateFile>> ScanAsync(
        string folderPath,
        IProgress<ScanProgress>? progress = null,
        CancellationToken cancellationToken = default,
        ScanSettings? settings = null)
    {
        var files = _fileEnumerationService.GetFiles(folderPath);

        var results = new List<DuplicateFile>(files.Count);

     
        ScanFilter? filter = settings is null ? null : ScanFilter.Create(settings);

        foreach (var file in files)
        {
            // Stop immediately if cancellation was requested
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                long? fileSize = null;
                FileInfo? fileInfo = null;

                if (filter != null)
                {
                    if (filter.MatchesIgnoredFolder(file) || filter.MatchesIgnoredExtension(file))
                        continue;

                    if (filter.RequiresSizeCheck)
                    {
                        fileInfo = new FileInfo(file);
                        fileSize = fileInfo.Length;

                        if (!filter.IsWithinSizeBounds(fileSize.Value))
                            continue;
                    }
                }

                var hash = await _hashService.ComputeHashAsync(file, cancellationToken);

                fileInfo ??= new FileInfo(file);
                fileSize ??= fileInfo.Length;

                results.Add(new DuplicateFile
                {
                    FilePath = file,
                    Hash = hash,
                    Size = fileSize.Value
                });

                progress?.Report(new ScanProgress
                {
                    FilesScanned = results.Count,
                    TotalFiles = files.Count,
                    CurrentFile = Path.GetFileName(file)
                });
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch
            {
                // Skip inaccessible, locked, or unreadable files gracefully
            }
        }

        return results;
    }

    private sealed class ScanFilter
    {
        private readonly HashSet<string> _ignoredExtensions;
        private readonly string[] _ignoredFolders;
        private readonly long? _minimumSizeBytes;
        private readonly long? _maximumSizeBytes;

        private ScanFilter(string[] ignoredFolders, HashSet<string> ignoredExtensions, long? minimumSizeBytes, long? maximumSizeBytes)
        {
            _ignoredFolders = ignoredFolders;
            _ignoredExtensions = ignoredExtensions;
            _minimumSizeBytes = minimumSizeBytes;
            _maximumSizeBytes = maximumSizeBytes;
        }

        public bool RequiresSizeCheck => _minimumSizeBytes.HasValue || _maximumSizeBytes.HasValue;

        public static ScanFilter Create(ScanSettings settings)
        {
            var ignoredFolders = SplitSettingValues(settings.IgnoreFoldersText).ToArray();
            var ignoredExtensions = SplitSettingValues(settings.IgnoreExtensionsText)
                .Select(value => value.StartsWith('.') ? value : "." + value)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            long? minimumSizeBytes = settings.DefaultMinimumFileSizeMB is > 0
                ? (long)(settings.DefaultMinimumFileSizeMB.Value * 1024 * 1024)
                : null;

            long? maximumSizeBytes = settings.DefaultMaximumFileSizeMB is > 0
                ? (long)(settings.DefaultMaximumFileSizeMB.Value * 1024 * 1024)
                : null;

            return new ScanFilter(ignoredFolders, ignoredExtensions, minimumSizeBytes, maximumSizeBytes);
        }

        public bool MatchesIgnoredFolder(string filePath)
        {
            if (_ignoredFolders.Length == 0)
                return false;

            return _ignoredFolders.Any(folder => filePath.Contains(folder, StringComparison.OrdinalIgnoreCase));
        }

        public bool MatchesIgnoredExtension(string filePath)
        {
            if (_ignoredExtensions.Count == 0)
                return false;

            string extension = Path.GetExtension(filePath);
            return _ignoredExtensions.Contains(extension);
        }

        public bool IsWithinSizeBounds(long sizeBytes)
        {
            if (_minimumSizeBytes.HasValue && sizeBytes < _minimumSizeBytes.Value)
                return false;

            if (_maximumSizeBytes.HasValue && sizeBytes > _maximumSizeBytes.Value)
                return false;

            return true;
        }
    }

    private static IEnumerable<string> SplitSettingValues(string text)
    {
        return text.Split(new[] { ',', ';', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(value => !string.IsNullOrWhiteSpace(value));
    }
}