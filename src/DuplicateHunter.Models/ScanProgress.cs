namespace DuplicateHunter.Models;

public class ScanProgress
{
    public int FilesScanned { get; set; }

    public int TotalFiles { get; set; }

    public string CurrentFile { get; set; } = string.Empty;

    public double Percentage =>
        TotalFiles == 0
            ? 0
            : (double)FilesScanned / TotalFiles * 100;
}