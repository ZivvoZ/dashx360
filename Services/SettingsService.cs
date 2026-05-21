using XboxMetroLauncher.Models;

namespace XboxMetroLauncher.Services;

public sealed class SettingsService : ISettingsService
{
    private const string SettingsFileName = "settings.json";
    private readonly IJsonStore _store;

    public SettingsService(IJsonStore store)
    {
        _store = store;
    }

    public async Task<AppSettings> LoadAsync(CancellationToken cancellationToken = default)
        => await _store.ReadAsync<AppSettings>(SettingsFileName, cancellationToken) ?? new AppSettings();

    public Task SaveAsync(AppSettings settings, CancellationToken cancellationToken = default)
        => _store.WriteAsync(SettingsFileName, settings, cancellationToken);
}
