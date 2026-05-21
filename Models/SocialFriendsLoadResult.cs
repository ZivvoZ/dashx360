namespace XboxMetroLauncher.Models;

public sealed class SocialFriendsLoadResult
{
    public required IReadOnlyList<SocialFriend> Friends { get; init; }

    public string PopupMessage { get; init; } = string.Empty;

    public bool ShowDiscordUnavailableRow { get; init; }

    public string UnavailableRowPopupMessage { get; init; } = string.Empty;
}
