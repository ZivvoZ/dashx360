namespace XboxMetroLauncher.Models;

public sealed class SocialFriend
{
    public required string Id { get; init; }

    public required string DisplayName { get; init; }

    public SocialFriendSource Source { get; init; }

    public string AvatarPathOrUrl { get; init; } = string.Empty;

    public bool IsOnline { get; init; }

    public string StatusText { get; init; } = string.Empty;

    public string ActivityText { get; init; } = string.Empty;

    public string GamerscoreText { get; init; } = string.Empty;

    public string ReputationText { get; init; } = string.Empty;

    public string ZoneText { get; init; } = string.Empty;

    public string IdentityDetailText { get; init; } = string.Empty;

    public bool IsPartyHost { get; init; }

    public bool ShowVoiceIndicator { get; init; } = true;

    public string CurrentActivityText
        => string.IsNullOrWhiteSpace(ActivityText) ? StatusText : ActivityText;
}
