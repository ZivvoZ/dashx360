using System.IO;
using XboxMetroLauncher.Models;
using XboxMetroLauncher.Utilities;

namespace XboxMetroLauncher.Services;

public sealed class ProfileService : IProfileService
{
    private const string ProfileFileName = "profile.json";
    private static readonly string DefaultGamerPicturePath = Path.Combine(AppPaths.AppFolder, "Assets", "Profile", "profilepicture.jpg");
    private readonly IJsonStore _store;

    public ProfileService(IJsonStore store)
    {
        _store = store;
    }

    public async Task<Profile> LoadAsync(CancellationToken cancellationToken = default)
    {
        var profile = await _store.ReadAsync<Profile>(ProfileFileName, cancellationToken).ConfigureAwait(false);
        if (profile is not null)
        {
            return profile;
        }

        profile = new Profile
        {
            Gamertag = "MetroPilot",
            GamerPicturePath = DefaultGamerPicturePath,
            Gamerscore = 36000,
            OnlineStatus = "Online",
            Motto = "(No motto)",
            Description = "(No bio)"
        };
        await SaveAsync(profile, cancellationToken).ConfigureAwait(false);
        return profile;
    }

    public Task SaveAsync(Profile profile, CancellationToken cancellationToken = default)
        => _store.WriteAsync(ProfileFileName, profile, cancellationToken);
}
