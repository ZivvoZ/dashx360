using XboxMetroLauncher.ViewModels;

namespace XboxMetroLauncher.Models;

public sealed class AppSettings : ObservableObject
{
    private bool _startFullscreen = true;
    private bool _playUiSounds = true;
    private bool _enableControllerInput = true;
    private bool _launchOnWindowsStartup;
    private string _themeName = "Metro Green";
    private string _bingSearchBaseUrl = "https://www.bing.com/search?q=";
    private string _displayResolution = "1080p";
    private string _openTrayGameId = string.Empty;
    private string _gameCoverFitMode = "Auto";
    private string _defaultAddDestination = "My Games";
    private SocialIntegrationMode _socialIntegrationMode = SocialIntegrationMode.LocalOnly;
    private DiscordConnectionState _discordConnectionState = DiscordConnectionState.NotConnected;
    private string _discordUserId = string.Empty;
    private string _discordDisplayName = string.Empty;
    private string _discordAvatarPathOrUrl = string.Empty;
    private string _discordAccessTokenEncrypted = string.Empty;
    private string _discordGrantedScopes = string.Empty;
    private string _discordTokenType = string.Empty;

    public bool StartFullscreen
    {
        get => _startFullscreen;
        set => SetProperty(ref _startFullscreen, value);
    }

    public bool PlayUiSounds
    {
        get => _playUiSounds;
        set => SetProperty(ref _playUiSounds, value);
    }

    public bool EnableControllerInput
    {
        get => _enableControllerInput;
        set => SetProperty(ref _enableControllerInput, value);
    }

    public bool LaunchOnWindowsStartup
    {
        get => _launchOnWindowsStartup;
        set => SetProperty(ref _launchOnWindowsStartup, value);
    }

    public string ThemeName
    {
        get => _themeName;
        set => SetProperty(ref _themeName, value);
    }

    public string BingSearchBaseUrl
    {
        get => _bingSearchBaseUrl;
        set => SetProperty(ref _bingSearchBaseUrl, value);
    }

    public string DisplayResolution
    {
        get => _displayResolution;
        set => SetProperty(ref _displayResolution, value);
    }

    public string OpenTrayGameId
    {
        get => _openTrayGameId;
        set => SetProperty(ref _openTrayGameId, value);
    }

    public string GameCoverFitMode
    {
        get => _gameCoverFitMode;
        set => SetProperty(ref _gameCoverFitMode, value);
    }

    public string DefaultAddDestination
    {
        get => _defaultAddDestination;
        set => SetProperty(ref _defaultAddDestination, value);
    }

    public SocialIntegrationMode SocialIntegrationMode
    {
        get => _socialIntegrationMode;
        set => SetProperty(ref _socialIntegrationMode, value);
    }

    public DiscordConnectionState DiscordConnectionState
    {
        get => _discordConnectionState;
        set => SetProperty(ref _discordConnectionState, value);
    }

    public bool DiscordConnected
    {
        get => DiscordConnectionState == DiscordConnectionState.Connected;
        set => DiscordConnectionState = value ? DiscordConnectionState.Connected : DiscordConnectionState.NotConnected;
    }

    public string DiscordUserId
    {
        get => _discordUserId;
        set => SetProperty(ref _discordUserId, value);
    }

    public string DiscordDisplayName
    {
        get => _discordDisplayName;
        set => SetProperty(ref _discordDisplayName, value);
    }

    public string DiscordAvatarPathOrUrl
    {
        get => _discordAvatarPathOrUrl;
        set => SetProperty(ref _discordAvatarPathOrUrl, value);
    }

    public string DiscordAccessTokenEncrypted
    {
        get => _discordAccessTokenEncrypted;
        set => SetProperty(ref _discordAccessTokenEncrypted, value);
    }

    public string DiscordGrantedScopes
    {
        get => _discordGrantedScopes;
        set => SetProperty(ref _discordGrantedScopes, value);
    }

    public string DiscordTokenType
    {
        get => _discordTokenType;
        set => SetProperty(ref _discordTokenType, value);
    }
}
