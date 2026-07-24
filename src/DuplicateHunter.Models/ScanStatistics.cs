namespace DuplicateHunter.Models;

public class ScanStatistics
{
    public int FilesScanned { get; set; }

    public int DuplicateGroups { get; set; }

    public long WastedBytes { get; set; }

    public string WastedSpace =>
        $"{WastedBytes / 1024d / 1024d:F2} MB";
}