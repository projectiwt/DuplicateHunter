using DuplicateHunter.Models;

namespace DuplicateHunter.Services;

public class DuplicateGroupingService
{
    public List<DuplicateGroup> GroupDuplicates(List<DuplicateFile> files)
    {
        return files
            .GroupBy(file => file.Hash)
            .Where(group => group.Count() > 1)
            .Select(group => new DuplicateGroup
            {
                Hash = group.Key,
                Files = group.ToList()
            })
            .OrderByDescending(group => group.FileCount)
            .ThenBy(group => group.Hash)
            .ToList();
    }
}