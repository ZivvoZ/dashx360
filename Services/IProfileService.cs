using XboxMetroLauncher.Models;

namespace XboxMetroLauncher.Services;

public interface IProfileService
{
    Task<Profile> LoadAsync(CancellationToken cancellationToken = default);
    Task SaveAsync(Profile profile, CancellationToken cancellationToken = default);
}
