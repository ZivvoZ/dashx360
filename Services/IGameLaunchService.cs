using XboxMetroLauncher.Models;

namespace XboxMetroLauncher.Services;

public interface IGameLaunchService
{
    Task LaunchAsync(GameMetadata game, CancellationToken cancellationToken = default);
}
