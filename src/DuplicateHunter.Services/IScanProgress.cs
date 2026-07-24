using DuplicateHunter.Models;

namespace DuplicateHunter.Services;

public interface IScanProgress
{
    void Report(ScanProgress progress);
}