using System.IO;
using System.Text.Json;
using XboxMetroLauncher.Models;
using XboxMetroLauncher.Utilities;

namespace XboxMetroLauncher.Services;

public sealed class JsonGameLibraryService : IGameLibraryService
{
    private const string LibraryFileName = "library.json";
    private readonly IJsonStore _store;

    public JsonGameLibraryService(IJsonStore store)
    {
        _store = store;
    }

    public async Task<GameLibrary> LoadAsync(CancellationToken cancellationToken = default)
    {
        var existing = await _store.ReadAsync<GameLibrary>(LibraryFileName, cancellationToken);
        if (existing is not null)
        {
            return existing;
        }

        var seedPath = AppPaths.FindFile(Path.Combine("Data", "library.seed.json"));
        if (File.Exists(seedPath))
        {
            await using var stream = File.OpenRead(seedPath);
            var seeded = await JsonSerializer.DeserializeAsync<GameLibrary>(stream, cancellationToken: cancellationToken);
            if (seeded is not null)
            {
                await SaveAsync(seeded, cancellationToken);
                return seeded;
            }
        }

        return new GameLibrary();
    }

    public Task SaveAsync(GameLibrary library, CancellationToken cancellationToken = default)
        => _store.WriteAsync(LibraryFileName, library, cancellationToken);

    public Task<IReadOnlyList<GameMetadata>> ScanFolderAsync(string folderPath, CancellationToken cancellationToken = default)
    {
        if (!Directory.Exists(folderPath))
        {
            return Task.FromResult<IReadOnlyList<GameMetadata>>([]);
        }

        var ignoredNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "UnityCrashHandler64",
            "UnityCrashHandler32",
            "CrashReportClient",
            "unins000",
            "uninstall"
        };

        var games = Directory.EnumerateFiles(folderPath, "*.exe", SearchOption.AllDirectories)
            .Where(path => !ignoredNames.Contains(Path.GetFileNameWithoutExtension(path)))
            .Select(path => new GameMetadata
            {
                Title = CleanTitle(Path.GetFileNameWithoutExtension(path)),
                ExecutablePath = path,
                WorkingDirectory = Path.GetDirectoryName(path) ?? folderPath,
                Platform = "PC",
                Genre = "Imported"
            })
            .OrderBy(game => game.Title)
            .ToList();

        return Task.FromResult<IReadOnlyList<GameMetadata>>(games);
    }

    private static string CleanTitle(string value)
        => value.Replace("_", " ").Replace("-", " ").Trim();
}
