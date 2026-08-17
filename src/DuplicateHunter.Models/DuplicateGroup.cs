namespace DuplicateHunter.Models;

public class DuplicateGroup
{
    public string Hash { get; set; } = string.Empty;

    public List<DuplicateFile> Files { get; set; } = new();

    public long TotalSize => Files.Sum(file => file.Size);

    public int FileCount => Files.Count;

    public string ShortHash =>
        Hash.Length <= 16
            ? Hash
            : $"{Hash[..8]}...{Hash[^8..]}";

    public string FormattedSize
    {
        get
        {
            const double KB = 1024;
            const double MB = KB * 1024;
            const double GB = MB * 1024;

            if (TotalSize >= GB)
                return $"{TotalSize / GB:F2} GB";

            if (TotalSize >= MB)
                return $"{TotalSize / MB:F2} MB";

            if (TotalSize >= KB)
                return $"{TotalSize / KB:F2} KB";

            return $"{TotalSize} Bytes";
        }
    }
} 