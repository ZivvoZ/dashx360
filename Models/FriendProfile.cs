namespace XboxMetroLauncher.Models;

public sealed class FriendProfile
{
    public string Gamertag { get; set; } = string.Empty;

    public string RealName { get; set; } = string.Empty;

    public string GamerPicturePath { get; set; } = string.Empty;

    public int Gamerscore { get; set; }

    public string Reputation { get; set; } = "★★★★★";

    public string Zone { get; set; } = "Recreation";

    public string Status { get; set; } = "Offline";

    public string Country { get; set; } = "Offline";
}
