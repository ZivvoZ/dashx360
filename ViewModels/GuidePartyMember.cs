namespace XboxMetroLauncher.ViewModels;

public sealed class GuidePartyMember
{
    public required string Gamertag { get; init; }

    public required string AvatarPath { get; init; }

    public required string ActivityText { get; init; }

    public required string ActivityIcon { get; init; }

    public bool ShowVoiceIcon { get; init; }

    public bool IsHost { get; init; }
}
