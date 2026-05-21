namespace XboxMetroLauncher.Services;

public interface IJsonStore
{
    Task<T?> ReadAsync<T>(string fileName, CancellationToken cancellationToken = default);
    Task WriteAsync<T>(string fileName, T value, CancellationToken cancellationToken = default);
}
