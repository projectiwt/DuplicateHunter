namespace DuplicateHunter.Services;

public interface IFileOperationService
{
    void OpenFile(string filePath);
    void OpenFolder(string filePath);
    void CopyToClipboard(string text);
    bool MoveToRecycleBin(string filePath);
    bool DeletePermanently(string filePath);
}
