using DuplicateHunter.Models;

namespace DuplicateHunter.Services;

public class ScanStatisticsService
{
    public ScanStatistics Calculate(List<DuplicateFile> files)
    {
        var statistics = new ScanStatistics();

        statistics.FilesScanned = files.Count;

        var duplicateGroups = files
            .GroupBy(f => f.Hash)
            .Where(g => g.Count() > 1)
            .ToList();

        statistics.DuplicateGroups = duplicateGroups.Count;

        statistics.WastedBytes = duplicateGroups.Sum(group =>
            (group.Count() - 1) * group.First().Size);

        return statistics;
    }
}