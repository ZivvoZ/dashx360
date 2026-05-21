using XboxMetroLauncher.Models;

namespace XboxMetroLauncher.Services;

public interface ISocialIntegrationService
{
    SocialFriendSource Source { get; }

    Task<IReadOnlyList<SocialFriend>> LoadFriendsAsync(CancellationToken cancellationToken = default);

    Task<SocialPartyInviteResult> InviteToPartyAsync(SocialFriend friend, CancellationToken cancellationToken = default);
}
