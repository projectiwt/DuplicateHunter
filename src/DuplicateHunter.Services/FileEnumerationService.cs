namespace DuplicateHunter.Services;

public class FileEnumerationService
{
    public List<string> GetFiles(string folderPath)
    {
        var files = new List<string>();

        if (!Directory.Exists(folderPath))
            return files;

        try
        {
            files.AddRange(
                Directory.EnumerateFiles(
                    folderPath,
                    "*",
                    SearchOption.AllDirectories));
        }
        catch
        {
            // Ignore folders that cannot be accessed.
        }

        return files;
    }
}