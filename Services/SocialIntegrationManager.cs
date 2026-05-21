using XboxMetroLauncher.Models;
using XboxMetroLauncher.Utilities;

namespace XboxMetroLauncher.Services;

public sealed class SocialIntegrationManager
{
    private readonly IFriendsService _friendsService;
    private readonly LocalSocialIntegrationService _localService;

    public SocialIntegrationManager(
        IFriendsService friendsService,
        LocalSocialIntegrationService localService)
    {
        _friendsService = friendsService;
        _localService = localService;
    }

    public async Task<SocialFriendsLoadResult> LoadFriendsAsync(
        SocialIntegrationMode mode,
        DiscordConnectionState discordConnectionState,
        CancellationToken cancellationToken = default)
    {
        var localFriends = await _localService.LoadFriendsAsync(cancellationToken).ConfigureAwait(false);

        return new SocialFriendsLoadResult
        {
            Friends = localFriends,
            PopupMessage = string.Empty
        };
    }

    public SocialFriend CreateLocalFriend(string gamertag)
    {
        var seed = gamertag.ToLowerInvariant().Aggregate(17, (current, character) => unchecked(current * 31 + character));
        var random = new Random(seed);
        var statuses = new[]
        {
            "Offline",
            "Away",
            $"Last online {random.Next(8, 46)} minutes ago",
            $"Last online {random.Next(1, 12)} hours ago"
        };
        var zones = new[] { "Recreation", "Family", "Pro", "Underground" };
        var repStars = BuildReputation(random.Next(3, 6));

        return new SocialFriend
        {
            Id = $"local:{gamertag}",
            DisplayName = gamertag,
            Source = SocialFriendSource.Local,
            AvatarPathOrUrl = ProfileImagePool.GetRandomPoolAvatarPath(),
            IsOnline = false,
            StatusText = statuses[random.Next(statuses.Length)],
            ActivityText = string.Empty,
            GamerscoreText = $"{random.Next(2500, 95000):N0} G",
            ReputationText = repStars,
            ZoneText = zones[random.Next(zones.Length)],
            IdentityDetailText = "Local"
        };
    }

    public Task<SocialConnectionResult> ConnectDiscordAsync(CancellationToken cancellationToken = default)
        => Task.FromResult(new SocialConnectionResult
        {
            State = DiscordConnectionState.NotImplemented,
            PopupMessage = "Discord is not available in the public build."
        });

    public async Task AddLocalFriendAsync(SocialFriend friend, CancellationToken cancellationToken = default)
    {
        var existing = await _friendsService.LoadAsync().ConfigureAwait(false);
        var updated = existing
            .Where(item => !string.Equals(item.Gamertag, friend.DisplayName, StringComparison.OrdinalIgnoreCase))
            .Append(LocalSocialIntegrationService.MapToLocalFriend(friend))
            .ToList();

        await _friendsService.SaveAsync(updated).ConfigureAwait(false);
    }

    public async Task RemoveLocalFriendAsync(SocialFriend friend, CancellationToken cancellationToken = default)
    {
        var existing = await _friendsService.LoadAsync().ConfigureAwait(false);
        var updated = existing
            .Where(item => !string.Equals(item.Gamertag, friend.DisplayName, StringComparison.OrdinalIgnoreCase))
            .ToList();

        await _friendsService.SaveAsync(updated).ConfigureAwait(false);
    }

    public Task<SocialPartyInviteResult> InviteToPartyAsync(
        SocialFriend friend,
        DiscordConnectionState discordConnectionState,
        CancellationToken cancellationToken = default)
    {
        return friend.Source switch
        {
            SocialFriendSource.Local => _localService.InviteToPartyAsync(friend, cancellationToken),
            _ => Task.FromResult(new SocialPartyInviteResult
            {
                AddToPartyList = false,
                PopupMessage = $"{GetSourceLabel(friend)} integration is not available in the public build."
            })
        };
    }

    public static string GetSourceLabel(SocialFriend friend)
        => friend.Source switch
        {
            SocialFriendSource.Discord => "Discord",
            SocialFriendSource.Steam => "Steam",
            _ => "Local"
        };

    public bool IsDiscordFriendAccessAvailable => false;

    public async Task<SocialConnectionResult> RestoreDiscordSessionAsync(
        string tokenTypeName,
        string accessToken,
        string grantedScopes,
        CancellationToken cancellationToken = default)
        => await Task.FromResult(new SocialConnectionResult
        {
            State = DiscordConnectionState.NotImplemented,
            PopupMessage = "Discord is not available in the public build."
        }).ConfigureAwait(false);

    public static string GetFriendStatusLabel(SocialFriend friend)
        => string.IsNullOrWhiteSpace(friend.StatusText)
            ? (friend.IsOnline ? "Online" : "Offline")
            : friend.StatusText;

    public static string GetFriendActivityLabel(SocialFriend friend)
        => string.IsNullOrWhiteSpace(friend.ActivityText) ? GetFriendStatusLabel(friend) : friend.ActivityText;

    public static SocialFriend BuildPartyHost(Profile profile)
        => new()
        {
            Id = $"local-host:{profile.Gamertag}",
            DisplayName = profile.Gamertag,
            Source = SocialFriendSource.Local,
            AvatarPathOrUrl = profile.GamerPicturePath,
            IsOnline = true,
            StatusText = profile.OnlineStatus,
            ActivityText = "Xbox 360 Dashboard",
            GamerscoreText = $"{profile.Gamerscore:N0} G",
            ReputationText = "★★★★★",
            ZoneText = "Party",
            IdentityDetailText = $"{profile.Gamertag}'s Profile",
            IsPartyHost = true
        };

    private static string BuildReputation(int filledStars)
        => new string('★', Math.Clamp(filledStars, 0, 5))
           + new string('☆', Math.Clamp(5 - filledStars, 0, 5));
}
