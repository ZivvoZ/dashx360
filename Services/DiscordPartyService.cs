using XboxMetroLauncher.Models;

namespace XboxMetroLauncher.Services;

public sealed class DiscordPartyService
{
    public DiscordPartyService()
    {
    }

    public Task<DiscordPartySnapshot> GetCurrentPartySnapshotAsync(
        Profile profile,
        DiscordConnectionState connectionState,
        CancellationToken cancellationToken = default)
        => Task.FromResult(BuildSoloSnapshot(profile, "Party data is local only in the public build."));

    public Task LeaveCurrentPartyAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

    public static DiscordPartySnapshot BuildSoloSnapshot(Profile profile, string statusMessage = "")
    {
        var self = SocialIntegrationManager.BuildPartyHost(profile);
        return new DiscordPartySnapshot
        {
            Members =
            [
                new SocialFriend
                {
                    Id = self.Id,
                    DisplayName = self.DisplayName,
                    Source = self.Source,
                    AvatarPathOrUrl = self.AvatarPathOrUrl,
                    IsOnline = self.IsOnline,
                    StatusText = self.StatusText,
                    ActivityText = self.ActivityText,
                    GamerscoreText = self.GamerscoreText,
                    ReputationText = self.ReputationText,
                    ZoneText = self.ZoneText,
                    IdentityDetailText = self.IdentityDetailText,
                    IsPartyHost = true,
                    ShowVoiceIndicator = self.ShowVoiceIndicator
                }
            ],
            UsesDiscordData = false,
            StatusMessage = statusMessage
        };
    }
}
