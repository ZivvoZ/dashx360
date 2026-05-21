using XboxMetroLauncher.Models;

namespace XboxMetroLauncher.Services;

public sealed class LocalSocialIntegrationService : ISocialIntegrationService
{
    private readonly IFriendsService _friendsService;

    public LocalSocialIntegrationService(IFriendsService friendsService)
    {
        _friendsService = friendsService;
    }

    public SocialFriendSource Source => SocialFriendSource.Local;

    public async Task<IReadOnlyList<SocialFriend>> LoadFriendsAsync(CancellationToken cancellationToken = default)
    {
        var friends = await _friendsService.LoadAsync().ConfigureAwait(false);
        var cacheChanged = false;
        foreach (var friend in friends)
        {
            var assignedAvatar = ProfileImagePool.ResolveAssignedAvatarPath(friend.GamerPicturePath);
            if (!string.Equals(friend.GamerPicturePath, assignedAvatar, StringComparison.OrdinalIgnoreCase))
            {
                friend.GamerPicturePath = assignedAvatar;
                cacheChanged = true;
            }
        }

        if (cacheChanged)
        {
            await _friendsService.SaveAsync(friends).ConfigureAwait(false);
        }

        return friends
            .Select(MapFromLocalFriend)
            .OrderBy(friend => friend.DisplayName, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }

    public Task<SocialPartyInviteResult> InviteToPartyAsync(SocialFriend friend, CancellationToken cancellationToken = default)
        => Task.FromResult(new SocialPartyInviteResult { AddToPartyList = true });

    public static SocialFriend MapFromLocalFriend(FriendProfile friend)
        => new()
        {
            Id = $"local:{friend.Gamertag}",
            DisplayName = friend.Gamertag,
            Source = SocialFriendSource.Local,
            AvatarPathOrUrl = friend.GamerPicturePath,
            IsOnline = IsOnline(friend.Status),
            StatusText = NormalizeOfflineStatus(friend.Status),
            ActivityText = string.Empty,
            GamerscoreText = $"{friend.Gamerscore:N0} G",
            ReputationText = NormalizeReputation(friend.Reputation),
            ZoneText = string.IsNullOrWhiteSpace(friend.Zone) ? "Recreation" : friend.Zone,
            IdentityDetailText = BuildLocalIdentityDetail(friend.Gamertag)
        };

    public static FriendProfile MapToLocalFriend(SocialFriend friend)
        => new()
        {
            Gamertag = friend.DisplayName,
            RealName = BuildLocalIdentityDetail(friend.DisplayName),
            GamerPicturePath = friend.AvatarPathOrUrl,
            Gamerscore = ParseGamerscore(friend.GamerscoreText),
            Reputation = NormalizeReputation(friend.ReputationText),
            Zone = string.IsNullOrWhiteSpace(friend.ZoneText) ? "Recreation" : friend.ZoneText,
            Status = NormalizeOfflineStatus(friend.StatusText),
            Country = "Offline"
        };

    private static string BuildLocalIdentityDetail(string gamertag)
        => "Local";

    private static int ParseGamerscore(string gamerscoreText)
    {
        if (string.IsNullOrWhiteSpace(gamerscoreText))
        {
            return 0;
        }

        var digits = new string(gamerscoreText.Where(char.IsDigit).ToArray());
        return int.TryParse(digits, out var gamerscore) ? gamerscore : 0;
    }

    private static bool IsOnline(string? status)
        => !string.IsNullOrWhiteSpace(status)
           && (status.Contains("Online", StringComparison.OrdinalIgnoreCase)
               || status.Contains("Away", StringComparison.OrdinalIgnoreCase))
           && !status.Contains("Last online", StringComparison.OrdinalIgnoreCase);

    private static string NormalizeOfflineStatus(string? status)
    {
        if (string.IsNullOrWhiteSpace(status))
        {
            return "Offline";
        }

        if (status.Contains("Last online", StringComparison.OrdinalIgnoreCase)
            || status.Contains("Away", StringComparison.OrdinalIgnoreCase)
            || status.Contains("Offline", StringComparison.OrdinalIgnoreCase))
        {
            return status;
        }

        return "Offline";
    }

    private static string NormalizeReputation(string? reputation)
    {
        if (string.IsNullOrWhiteSpace(reputation))
        {
            return "★★★★★";
        }

        var filledStars = reputation.Count(character => character is '★' or '*');
        return new string('★', Math.Clamp(filledStars == 0 ? 5 : filledStars, 1, 5));
    }
}
