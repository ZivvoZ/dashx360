using System.IO;
using System.Text.Json;

namespace XboxMetroLauncher.Services;

public sealed class JsonStore : IJsonStore
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    private readonly string _rootPath;

    public JsonStore(string rootPath)
    {
        _rootPath = rootPath;
        Directory.CreateDirectory(_rootPath);
    }

    public async Task<T?> ReadAsync<T>(string fileName, CancellationToken cancellationToken = default)
    {
        var path = Path.Combine(_rootPath, fileName);
        if (!File.Exists(path))
        {
            return default;
        }

        await using var stream = File.OpenRead(path);
        return await JsonSerializer.DeserializeAsync<T>(stream, SerializerOptions, cancellationToken).ConfigureAwait(false);
    }

    public async Task WriteAsync<T>(string fileName, T value, CancellationToken cancellationToken = default)
    {
        var path = Path.Combine(_rootPath, fileName);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);

        await using var stream = File.Create(path);
        await JsonSerializer.SerializeAsync(stream, value, SerializerOptions, cancellationToken).ConfigureAwait(false);
    }
}
