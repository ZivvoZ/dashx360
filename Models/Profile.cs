namespace XboxMetroLauncher.Models;

public sealed class Profile
{
    public string Gamertag { get; set; } = "Player One";
    public string GamerPicturePath { get; set; } = string.Empty;
    public int Gamerscore { get; set; } = 36000;
    public string OnlineStatus { get; set; } = "Online";
    public string Motto { get; set; } = "(No motto)";
    public string Description { get; set; } = "(No bio)";
}
