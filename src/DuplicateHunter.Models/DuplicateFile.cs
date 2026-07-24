namespace DuplicateHunter.Models;

public class DuplicateFile
{
    public string FilePath { get; set; } = string.Empty;

    public string Hash { get; set; } = string.Empty;

    public long Size { get; set; }
}