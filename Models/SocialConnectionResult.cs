namespace XboxMetroLauncher.Models;

public sealed class SocialConnectionResult
{
    public DiscordConnectionState State { get; init; } = DiscordConnectionState.NotConnected;

    public string PopupMessage { get; init; } = string.Empty;

    public string StatusMessage { get; init; } = string.Empty;

    public string DisplayName { get; init; } = string.Empty;

    public string AvatarPathOrUrl { get; init; } = string.Empty;

    public string UserId { get; init; } = string.Empty;

    public string AccessToken { get; init; } = string.Empty;

    public string GrantedScopes { get; init; } = string.Empty;

    public string TokenTypeName { get; init; } = string.Empty;
}
