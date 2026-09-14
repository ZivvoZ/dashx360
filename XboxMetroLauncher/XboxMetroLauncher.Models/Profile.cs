using XboxMetroLauncher.ViewModels;

namespace XboxMetroLauncher.Models;

public sealed class Profile : ObservableObject
{
	private string _gamertag = "Player One";

	private string _name = "(No name)";

	private string _gamerPicturePath = string.Empty;

	private int _gamerscore = 36000;

	private string _onlineStatus = "Online";

	private string _motto = "(No motto)";

	private string _location = "United States";

	private string _description = "(No bio)";

	public string Gamertag { get => _gamertag; set => SetProperty(ref _gamertag, value, nameof(Gamertag)); }

	public string Name { get => _name; set => SetProperty(ref _name, value, nameof(Name)); }

	public string GamerPicturePath { get => _gamerPicturePath; set => SetProperty(ref _gamerPicturePath, value, nameof(GamerPicturePath)); }

	public int Gamerscore { get => _gamerscore; set => SetProperty(ref _gamerscore, value, nameof(Gamerscore)); }

	public string OnlineStatus { get => _onlineStatus; set => SetProperty(ref _onlineStatus, value, nameof(OnlineStatus)); }

	public string Motto { get => _motto; set => SetProperty(ref _motto, value, nameof(Motto)); }

	public string Location { get => _location; set => SetProperty(ref _location, value, nameof(Location)); }

	public string Description { get => _description; set => SetProperty(ref _description, value, nameof(Description)); }
}
