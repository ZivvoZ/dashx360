using XboxMetroLauncher.Models;

namespace XboxMetroLauncher.Services;

public interface IFriendsService
{
    Task<IReadOnlyList<FriendProfile>> LoadAsync();

    Task SaveAsync(IReadOnlyList<FriendProfile> friends);
}
