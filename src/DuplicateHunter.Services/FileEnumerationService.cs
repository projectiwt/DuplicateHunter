using System.IO;

namespace DuplicateHunter.Services;

public class FileEnumerationService
{
    public List<string> GetFiles(string folderPath)
    {
        var files = new List<string>();

        if (string.IsNullOrWhiteSpace(folderPath) || !Directory.Exists(folderPath))
            return files;

        try
        {
            var options = new EnumerationOptions
            {
                IgnoreInaccessible = true,
                RecurseSubdirectories = true,
                ReturnSpecialDirectories = false,
                AttributesToSkip = FileAttributes.ReparsePoint
            };

            files.AddRange(Directory.EnumerateFiles(folderPath, "*", options));
            return files;
        }
        catch
        {
            // Fallback to manual safe recursive traversal if options encounter OS-level issue
            return SafeEnumerateFilesRecursive(folderPath);
        }
    }

    private static List<string> SafeEnumerateFilesRecursive(string rootPath)
    {
        var files = new List<string>();
        var dirsToVisit = new Stack<string>();
        dirsToVisit.Push(rootPath);

        while (dirsToVisit.Count > 0)
        {
            string currentDir = dirsToVisit.Pop();

            try
            {
                files.AddRange(Directory.EnumerateFiles(currentDir));
            }
            catch
            {
                // Skip inaccessible directory files
            }

            try
            {
                foreach (string subDir in Directory.EnumerateDirectories(currentDir))
                {
                    try
                    {
                        var dirInfo = new DirectoryInfo(subDir);
                        if (!dirInfo.Attributes.HasFlag(FileAttributes.ReparsePoint))
                        {
                            dirsToVisit.Push(subDir);
                        }
                    }
                    catch
                    {
                        // Skip inaccessible sub-directory
                    }
                }
            }
            catch
            {
                // Skip sub-directory enumeration errors
            }
        }

        return files;
    }
}