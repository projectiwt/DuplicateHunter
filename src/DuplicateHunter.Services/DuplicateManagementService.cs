using System.IO;
using DuplicateHunter.Models;

namespace DuplicateHunter.Services;

public class DuplicateManagementService
{
    private readonly IFileOperationService _fileOperationService;

    public DuplicateManagementService(IFileOperationService fileOperationService)
    {
        _fileOperationService = fileOperationService;
    }

    public bool DeleteFile(DuplicateFile file, bool permanent)
    {
        if (file == null || string.IsNullOrWhiteSpace(file.FilePath))
            return false;

        return permanent
            ? _fileOperationService.DeletePermanently(file.FilePath)
            : _fileOperationService.MoveToRecycleBin(file.FilePath);
    }

    public int KeepNewest(DuplicateGroup group, bool permanent)
    {
        if (group == null || group.Files.Count <= 1)
            return 0;

        var targetFile = group.Files
            .OrderByDescending(f => GetLastWriteTimeSafe(f.FilePath))
            .FirstOrDefault();

        if (targetFile == null)
            return 0;

        return DeleteAllExceptOne(group, targetFile, permanent);
    }

    public int KeepOldest(DuplicateGroup group, bool permanent)
    {
        if (group == null || group.Files.Count <= 1)
            return 0;

        var targetFile = group.Files
            .OrderBy(f => GetLastWriteTimeSafe(f.FilePath))
            .FirstOrDefault();

        if (targetFile == null)
            return 0;

        return DeleteAllExceptOne(group, targetFile, permanent);
    }

    public int KeepLargest(DuplicateGroup group, bool permanent)
    {
        if (group == null || group.Files.Count <= 1)
            return 0;

        var targetFile = group.Files
            .OrderByDescending(f => f.Size)
            .ThenBy(f => f.FilePath)
            .FirstOrDefault();

        if (targetFile == null)
            return 0;

        return DeleteAllExceptOne(group, targetFile, permanent);
    }

    public int KeepSmallest(DuplicateGroup group, bool permanent)
    {
        if (group == null || group.Files.Count <= 1)
            return 0;

        var targetFile = group.Files
            .OrderBy(f => f.Size)
            .ThenBy(f => f.FilePath)
            .FirstOrDefault();

        if (targetFile == null)
            return 0;

        return DeleteAllExceptOne(group, targetFile, permanent);
    }

    public int DeleteAllExceptOne(DuplicateGroup group, DuplicateFile keepFile, bool permanent)
    {
        if (group == null || keepFile == null || group.Files.Count <= 1)
            return 0;

        if (!group.Files.Any(file => file.FilePath == keepFile.FilePath))
            return 0;

        int deletedCount = 0;
        var filesToDelete = group.Files.Where(f => f != keepFile && f.FilePath != keepFile.FilePath).ToList();

        foreach (var file in filesToDelete)
        {
            if (DeleteFile(file, permanent))
            {
                deletedCount++;
            }
        }

        return deletedCount;
    }

    private static DateTime GetLastWriteTimeSafe(string filePath)
    {
        try
        {
            return File.GetLastWriteTime(filePath);
        }
        catch
        {
            return DateTime.MinValue;
        }
    }
}
