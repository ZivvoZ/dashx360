using XboxMetroLauncher.Models;

namespace XboxMetroLauncher.Services;

public sealed class FriendsService : IFriendsService
{
    private const string FriendsFileName = "friends.json";
    private readonly IJsonStore _jsonStore;

    public FriendsService(IJsonStore jsonStore)
    {
        _jsonStore = jsonStore;
    }

    public async Task<IReadOnlyList<FriendProfile>> LoadAsync()
    {
        var data = await _jsonStore.ReadAsync<FriendsData>(FriendsFileName).ConfigureAwait(false);
        return data?.Friends ?? [];
    }

    public Task SaveAsync(IReadOnlyList<FriendProfile> friends)
        => _jsonStore.WriteAsync(FriendsFileName, new FriendsData
        {
            Friends = friends.ToList()
        });
}
