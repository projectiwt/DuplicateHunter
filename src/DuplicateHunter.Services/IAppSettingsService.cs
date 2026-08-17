using DuplicateHunter.Models;

namespace DuplicateHunter.Services;

public interface IAppSettingsService
{
    Task<ScanSettings> LoadAsync(CancellationToken cancellationToken = default);
    Task SaveAsync(ScanSettings settings, CancellationToken cancellationToken = default);
}
