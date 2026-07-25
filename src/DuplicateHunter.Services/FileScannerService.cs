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
    IProgress<ScanProgress>? progress = null)

    {
        var results = new List<DuplicateFile>();

        var files = _fileEnumerationService.GetFiles(folderPath);

        foreach (var file in files)
        {
            try
            {
                var hash = await _hashService.ComputeHashAsync(file);

                results.Add(new DuplicateFile
                {
                    FilePath = file,
                    Hash = hash,
                    Size = new FileInfo(file).Length
                });


                progress?.Report(new ScanProgress
                {
                    FilesScanned = results.Count,
                    TotalFiles = files.Count,
                    CurrentFile = Path.GetFileName(file)
                });
            }
            catch
            {
                // Skip unreadable files
            }
        }

        return results;
    }
}