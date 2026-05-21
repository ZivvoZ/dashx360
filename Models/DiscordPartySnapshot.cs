namespace XboxMetroLauncher.Models;

public sealed class DiscordPartySnapshot
{
    public required IReadOnlyList<SocialFriend> Members { get; init; }

    public bool UsesDiscordData { get; init; }

    public string StatusMessage { get; init; } = string.Empty;
}
