using System.Collections.Generic;
using XboxMetroLauncher.ViewModels;

namespace XboxMetroLauncher.Models;

public sealed class AppSettings : ObservableObject
{
	private bool _startFullscreen = true;

	private bool _playUiSounds = true;

	private bool _enableControllerInput = true;

	private bool _launchOnWindowsStartup;

	private bool _minimizeOnGameLaunch = true;

	private bool _enableFakeLoading = true;

	private string _themeName = "Xbox 360";

	private string _bingSearchBaseUrl = "https://www.bing.com/search?q=";

	private string _displayResolution = "16:9";

	private string _dashboardStyle = "Metro";
	private string _xboxGuide = "Metro";

	public string XboxGuide
	{
		get => _xboxGuide;
		set => SetProperty(ref _xboxGuide, value == "Blades" || value == "Blades BETA" ? "Blades" : "Metro", nameof(XboxGuide));
	}

	private string _dashboardSounds = "Metro";

	private string _dashboardStartup = "Metro";

	private string _openTrayGameId = string.Empty;

	private string _gameCoverFitMode = "Auto";

	private string _defaultAddDestination = "My Games";

	private string _audioOutputDeviceName = "Default";

	private double _dashboardVolume = 1.0;

	private string _spotifyClientId = string.Empty;

	private string _spotifyAccessTokenEncrypted = string.Empty;

	private string _spotifyRefreshTokenEncrypted = string.Empty;

	private string _spotifyTokenExpiresAtUtc = string.Empty;

	private string _spotifyDisplayName = string.Empty;

	private string _dashboardTileColor = "#FF028D02";

	private SocialIntegrationMode _socialIntegrationMode;

	private DiscordConnectionState _discordConnectionState;

	private string _discordUserId = string.Empty;

	private string _discordDisplayName = string.Empty;

	private string _discordAvatarPathOrUrl = string.Empty;

	private string _discordAccessTokenEncrypted = string.Empty;

	private string _discordGrantedScopes = string.Empty;

	private string _discordTokenType = string.Empty;

	public bool StartFullscreen
	{
		get
		{
			return _startFullscreen;
		}
		set
		{
			SetProperty(ref _startFullscreen, value, "StartFullscreen");
		}
	}

	public bool PlayUiSounds
	{
		get
		{
			return _playUiSounds;
		}
		set
		{
			SetProperty(ref _playUiSounds, value, "PlayUiSounds");
		}
	}

	public bool EnableControllerInput
	{
		get
		{
			return _enableControllerInput;
		}
		set
		{
			SetProperty(ref _enableControllerInput, value, "EnableControllerInput");
		}
	}

	public bool LaunchOnWindowsStartup
	{
		get
		{
			return _launchOnWindowsStartup;
		}
		set
		{
			SetProperty(ref _launchOnWindowsStartup, value, "LaunchOnWindowsStartup");
		}
	}

	public bool MinimizeOnGameLaunch
	{
		get
		{
			return _minimizeOnGameLaunch;
		}
		set
		{
			SetProperty(ref _minimizeOnGameLaunch, value, "MinimizeOnGameLaunch");
		}
	}

	public bool EnableFakeLoading
	{
		get
		{
			return _enableFakeLoading;
		}
		set
		{
			SetProperty(ref _enableFakeLoading, value, "EnableFakeLoading");
		}
	}

	public string ThemeName
	{
		get
		{
			return _themeName;
		}
		set
		{
			SetProperty(ref _themeName, value, "ThemeName");
		}
	}

	public string BingSearchBaseUrl
	{
		get
		{
			return _bingSearchBaseUrl;
		}
		set
		{
			SetProperty(ref _bingSearchBaseUrl, value, "BingSearchBaseUrl");
		}
	}

	public string DisplayResolution
	{
		get
		{
			return _displayResolution;
		}
		set
		{
			SetProperty(ref _displayResolution, value, "DisplayResolution");
		}
	}

	public string DashboardStyle
	{
		get
		{
			return _dashboardStyle;
		}
		set
		{
			SetProperty(ref _dashboardStyle, value, "DashboardStyle");
		}
	}

	public string DashboardSounds
	{
		get
		{
			return _dashboardSounds;
		}
		set
		{
			SetProperty(ref _dashboardSounds, value, "DashboardSounds");
		}
	}

	public string DashboardStartup
	{
		get
		{
			return _dashboardStartup;
		}
		set
		{
			SetProperty(ref _dashboardStartup, value, "DashboardStartup");
		}
	}

	public string OpenTrayGameId
	{
		get
		{
			return _openTrayGameId;
		}
		set
		{
			SetProperty(ref _openTrayGameId, value, "OpenTrayGameId");
		}
	}

	public string GameCoverFitMode
	{
		get
		{
			return _gameCoverFitMode;
		}
		set
		{
			SetProperty(ref _gameCoverFitMode, value, "GameCoverFitMode");
		}
	}

	public string DefaultAddDestination
	{
		get
		{
			return _defaultAddDestination;
		}
		set
		{
			SetProperty(ref _defaultAddDestination, value, "DefaultAddDestination");
		}
	}

	public string AudioOutputDeviceName
	{
		get
		{
			return _audioOutputDeviceName;
		}
		set
		{
			SetProperty(ref _audioOutputDeviceName, value, "AudioOutputDeviceName");
		}
	}

	public double DashboardVolume
	{
		get
		{
			return _dashboardVolume;
		}
		set
		{
			SetProperty(ref _dashboardVolume, System.Math.Clamp(value, 0.0, 1.0), "DashboardVolume");
		}
	}

	public string SpotifyClientId
	{
		get
		{
			return _spotifyClientId;
		}
		set
		{
			SetProperty(ref _spotifyClientId, value, "SpotifyClientId");
		}
	}

	public string SpotifyAccessTokenEncrypted
	{
		get
		{
			return _spotifyAccessTokenEncrypted;
		}
		set
		{
			SetProperty(ref _spotifyAccessTokenEncrypted, value, "SpotifyAccessTokenEncrypted");
		}
	}

	public string SpotifyRefreshTokenEncrypted
	{
		get
		{
			return _spotifyRefreshTokenEncrypted;
		}
		set
		{
			SetProperty(ref _spotifyRefreshTokenEncrypted, value, "SpotifyRefreshTokenEncrypted");
		}
	}

	public string SpotifyTokenExpiresAtUtc
	{
		get
		{
			return _spotifyTokenExpiresAtUtc;
		}
		set
		{
			SetProperty(ref _spotifyTokenExpiresAtUtc, value, "SpotifyTokenExpiresAtUtc");
		}
	}

	public string SpotifyDisplayName
	{
		get
		{
			return _spotifyDisplayName;
		}
		set
		{
			SetProperty(ref _spotifyDisplayName, value, "SpotifyDisplayName");
		}
	}

	public string DashboardTileColor
	{
		get
		{
			return _dashboardTileColor;
		}
		set
		{
			SetProperty(ref _dashboardTileColor, value, "DashboardTileColor");
		}
	}

	public Dictionary<string, DashboardTileCustomization> DashboardTileCustomizations { get; set; } = new Dictionary<string, DashboardTileCustomization>();

	public SocialIntegrationMode SocialIntegrationMode
	{
		get
		{
			return _socialIntegrationMode;
		}
		set
		{
			SetProperty(ref _socialIntegrationMode, value, "SocialIntegrationMode");
		}
	}

	public DiscordConnectionState DiscordConnectionState
	{
		get
		{
			return _discordConnectionState;
		}
		set
		{
			SetProperty(ref _discordConnectionState, value, "DiscordConnectionState");
		}
	}

	public bool DiscordConnected
	{
		get
		{
			return DiscordConnectionState == DiscordConnectionState.Connected;
		}
		set
		{
			DiscordConnectionState = (value ? DiscordConnectionState.Connected : DiscordConnectionState.NotConnected);
		}
	}

	public string DiscordUserId
	{
		get
		{
			return _discordUserId;
		}
		set
		{
			SetProperty(ref _discordUserId, value, "DiscordUserId");
		}
	}

	public string DiscordDisplayName
	{
		get
		{
			return _discordDisplayName;
		}
		set
		{
			SetProperty(ref _discordDisplayName, value, "DiscordDisplayName");
		}
	}

	public string DiscordAvatarPathOrUrl
	{
		get
		{
			return _discordAvatarPathOrUrl;
		}
		set
		{
			SetProperty(ref _discordAvatarPathOrUrl, value, "DiscordAvatarPathOrUrl");
		}
	}

	public string DiscordAccessTokenEncrypted
	{
		get
		{
			return _discordAccessTokenEncrypted;
		}
		set
		{
			SetProperty(ref _discordAccessTokenEncrypted, value, "DiscordAccessTokenEncrypted");
		}
	}

	public string DiscordGrantedScopes
	{
		get
		{
			return _discordGrantedScopes;
		}
		set
		{
			SetProperty(ref _discordGrantedScopes, value, "DiscordGrantedScopes");
		}
	}

	public string DiscordTokenType
	{
		get
		{
			return _discordTokenType;
		}
		set
		{
			SetProperty(ref _discordTokenType, value, "DiscordTokenType");
		}
	}
}
