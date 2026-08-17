using DuplicateHunter.Models;

namespace DuplicateHunter.Services;

public interface IReportExportService
{
    Task ExportToCsvAsync(string filePath, IEnumerable<DuplicateGroup> groups, ScanStatistics stats);
    Task ExportToJsonAsync(string filePath, IEnumerable<DuplicateGroup> groups, ScanStatistics stats);
    Task ExportToExcelAsync(string filePath, IEnumerable<DuplicateGroup> groups, ScanStatistics stats);
    Task ExportToPdfAsync(string filePath, IEnumerable<DuplicateGroup> groups, ScanStatistics stats);
}
