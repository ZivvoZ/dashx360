using XboxMetroLauncher.Models;

namespace XboxMetroLauncher.Services;

public interface IGameLibraryService
{
    Task<GameLibrary> LoadAsync(CancellationToken cancellationToken = default);
    Task SaveAsync(GameLibrary library, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<GameMetadata>> ScanFolderAsync(string folderPath, CancellationToken cancellationToken = default);
}
