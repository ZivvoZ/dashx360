using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Windows.Media.Control;
using XboxMetroLauncher.Input;
using XboxMetroLauncher.Models;
using XboxMetroLauncher.Services;
using XboxMetroLauncher.Utilities;
using XboxMetroLauncher.ViewModels.Tabs;

namespace XboxMetroLauncher.ViewModels;

public sealed class DashboardViewModel : ObservableObject
{
	private static readonly string SteamScanDebugLogPath = Path.Combine(AppPaths.LogsFolder, "steam-scan-debug.log");

	private static readonly HttpClient SpotifyHttpClient = new HttpClient();

	private const int ShowWindowRestore = 9;

	private const double DefaultGameCoverZoom = 1.06;

	private const string SpotifyRedirectUri = "http://127.0.0.1:55432/callback/";

	private const string SpotifyScopes = "user-library-read playlist-read-private playlist-read-collaborative user-read-playback-state user-modify-playback-state user-read-currently-playing";

	private const string SpotifyMirrorScheme = "spotify-mirror://";

	private const byte VkMediaNextTrack = 176;

	private const byte VkMediaPreviousTrack = 177;

	private const byte VkMediaStop = 178;

	private const byte VkMediaPlayPause = 179;

	private const int KeyEventKeyUp = 2;

	private readonly IGameLibraryService _libraryService;

	private readonly IGameLaunchService _launchService;

	private readonly ISearchService _searchService;

	private readonly ISettingsService _settingsService;

	private readonly IProfileService _profileService;

	private readonly IFilePickerService _filePickerService;

	private readonly IImportExportService _importExportService;

	private readonly ISteamLibraryScannerService _steamLibraryScannerService;

	private readonly ISteamCommunityService _steamCommunityService;

	private readonly IThemeService _themeService;

	private readonly IStartupRegistrationService _startupRegistrationService;

	private readonly IAudioService _audioService;

	private readonly SocialIntegrationManager _socialIntegrationManager;

	private readonly IRunningGameService _runningGameService;

	private readonly AudioAnalysisService _audioAnalysisService = new AudioAnalysisService();

	private readonly MediaPlayer _musicPlayer = new MediaPlayer();

	private readonly DispatcherTimer _musicTimer;

	private readonly DispatcherTimer _dashPartyInviteTimer;

	private readonly DispatcherTimer _profileSaveTimer;

	private readonly List<System.Windows.Media.Brush> _accentBrushes;

	private GameLibrary _library = new GameLibrary();

	private DashboardTabViewModel? _currentTab;

	private GameCardViewModel? _selectedGame;

	private bool _hasSelectionSoundBaseline;

	private DateTimeOffset _lastFocusSoundAt;

	private GameCardViewModel? _featuredGame;

	private Profile _profile = new Profile();

	private bool _suppressProfileAutosave;

	private AppSettings _settings = new AppSettings();

	private string _searchQuery = string.Empty;

	private string _statusMessage = "Ready";

	private bool _isSearchOverlayOpen;

	private bool _isDetailsOpen;

	private bool _isQuickMenuOpen;

	private bool _isMyGamesOpen;

	private bool _isLibraryShowingPins;

	private bool _isLibraryShowingApps;

	private bool _isLauncherSettingsOpen;

	private bool _isProfileEditorOpen;

	private bool _isProfileMenuEditing;

	private string _editableProfileGamertag = string.Empty;

	private string _editableProfileName = string.Empty;

	private string _editableProfileMotto = string.Empty;

	private string _editableProfileLocation = string.Empty;

	private string _editableProfileDescription = string.Empty;

	private string _editableProfileGamerscore = string.Empty;

	private bool _isThemeMenuOpen;

	private bool _isThemeCreatorOpen;

	private bool _isDashboardCustomizerOpen;

	private bool _isSteamSetupOpen;

	private bool _isSpotifySetupOpen;

	private bool _isMusicPlayerOpen;

	private bool _isYouTubeTvOpen;

	private bool _isMusicPlayerTransparent;

	private bool _isMusicVisualizerFullscreen;

	private bool _isMusicPlaying;

	private bool _isRefreshingAudioOutputDevices;

	private bool _isShuffleEnabled;

	private bool _isBooting = true;

	private string _clockText = string.Empty;

	private int _topRightFriendCount;

	private int _topRightState;

	private int _selectedBladesBladeIndex = 1;

	private int _selectedBladesMenuIndex;

	private string _bladesSubmenuKey = string.Empty;

	private int _selectedBladesSubmenuIndex;

	private int _selectedBladesGameLibraryCategoryIndex = 1;

	private int _selectedBladesAchievementGameIndex;

	private int _selectedBladesAchievementIndex;

	private bool _isBladesAchievementListFocused;

	private readonly ObservableCollection<BladesAchievementGameViewModel> _bladesAchievementGameItems = new ObservableCollection<BladesAchievementGameViewModel>();

	private readonly ObservableCollection<BladesAchievementItemViewModel> _bladesAchievementItems = new ObservableCollection<BladesAchievementItemViewModel>();

	private string _musicPositionText = "0:00";

	private string _musicDurationText = "0:00";

	private double _musicProgress;

	private double _musicVolume = 0.7;

	private double _visualizerBass;

	private double _visualizerMid;

	private double _visualizerTreble;

	private double _visualizerLoudness;

	private double _visualizerPeak;

	private string? _pendingTabSound;

	private bool _suppressNextTabSound;

	private GameCardViewModel? _trayGame;

	private MusicTrackViewModel? _currentMusicTrack;

	private int _musicIndex = -1;

	private MusicTrackViewModel? _selectedMusicTrack;

	private string _musicBrowserScreen = "Source";

	private MusicBrowserMenuItemViewModel? _selectedMusicBrowserMenuItem;

	private MusicBrowserResultItemViewModel? _selectedMusicBrowserResultItem;

	private bool _isMusicBrowserResultsFocused;

	private bool _closeMusicPlayerOnBack;

	private bool _isSpotifyMusicMode;

	private bool _isSpotifyAppControlMode;

	private bool _isRefreshingSpotifyTimeline;

	private readonly Random _random = new Random();

	private DashboardTheme? _selectedTheme;

	private string _themeNameInput = string.Empty;

	private string _themeHomePreviewPath = string.Empty;

	private string _themeGamesPreviewPath = string.Empty;

	private string _themeSettingsPreviewPath = string.Empty;

	private string _themeAppsPreviewPath = string.Empty;

	private int _customizerTabIndex;

	private DashboardTileCustomizationViewModel? _selectedDashboardTile;

	private byte _customTileRed = 2;

	private byte _customTileGreen = 141;

	private byte _customTileBlue = 2;

	private string _steamSetupApiKey = string.Empty;

	private string _steamSetupSteamId64 = string.Empty;

	private string _steamSetupStatus = "Steam is not connected.";

	private string _spotifySetupClientId = string.Empty;

	private string _spotifySetupStatus = "Spotify is not connected.";

	private string _dashPartyLinkServiceUrl = string.Empty;

	private string _dashPartyLinkFriendCode = "Loading...";

	private string _dashPartyLinkDeviceId = string.Empty;

	private string _dashPartyLinkSetupStatus = "DashX360 Party Link is not configured.";

	private string _notificationToastLine1 = string.Empty;

	private string _notificationToastLine2 = string.Empty;

	private SocialFriend? _pendingDashPartyInviteFriend;

	private bool _isPollingDashPartyInvites;

	private readonly HashSet<string> _seenDashPartyInviteKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

	private int _selectedGameDetailsTabIndex;

	private bool _isGameDetailsSeeAllOpen;

	private IReadOnlyList<SteamGameDlc> _selectedGameDlc = Array.Empty<SteamGameDlc>();

	private int _libraryMenuStartIndex;

	private const int LibraryVisibleWindowSize = 6;

	private AppLibraryTileViewModel? _selectedAppLibraryTile;

	private const double LibraryMenuLeftPeekOffset = 176.0;

	public ObservableCollection<DashboardTabViewModel> Tabs { get; }

	public ObservableCollection<GameCardViewModel> Games { get; } = new ObservableCollection<GameCardViewModel>();

	public ObservableCollection<MusicTrackViewModel> MusicTracks { get; } = new ObservableCollection<MusicTrackViewModel>();

	public ObservableCollection<MusicBrowserMenuItemViewModel> MusicBrowserMenuItems { get; } = new ObservableCollection<MusicBrowserMenuItemViewModel>();

	public ObservableCollection<MusicBrowserResultItemViewModel> MusicBrowserResultItems { get; } = new ObservableCollection<MusicBrowserResultItemViewModel>();

	public ObservableCollection<DashboardTheme> AvailableThemes { get; } = new ObservableCollection<DashboardTheme>();

	public ObservableCollection<GameCardViewModel> VisibleLibraryMenuGames { get; } = new ObservableCollection<GameCardViewModel>();

	public ObservableCollection<DashboardTabCustomizationViewModel> DashboardCustomizationTabs { get; } = new ObservableCollection<DashboardTabCustomizationViewModel>();

	public ObservableCollection<string> AudioOutputDeviceOptions { get; } = new ObservableCollection<string> { "Default" };

	public IEnumerable<GameCardViewModel> RecentGames => Games.OrderByDescending((GameCardViewModel game) => game.Game.LastPlayed ?? DateTimeOffset.MinValue).Take(8);

	public IEnumerable<GameCardViewModel> PinnedGames => Games.Where((GameCardViewModel game) => game.Game.IsFavorite).Take(8);

	public IEnumerable<GameCardViewModel> ImportedGames => Games.Where((GameCardViewModel game) => string.Equals(game.Game.Genre, "Imported", StringComparison.OrdinalIgnoreCase));

	public IEnumerable<string> LibraryPaths => _library.LibraryPaths;

	public double LibraryMenuScrollOffset
	{
		get
		{
			if (_libraryMenuStartIndex <= 0)
			{
				return 0.0;
			}
			return 176.0;
		}
	}

	public IReadOnlyList<string> ResolutionOptions { get; } = new _003C_003Ez__ReadOnlyArray<string>(new string[2] { "16:9", "21:9" });

	public IReadOnlyList<string> DashboardStyleOptions { get; } = new _003C_003Ez__ReadOnlyArray<string>(new string[2] { "Metro", "Blades" });
	public IReadOnlyList<string> XboxGuideOptions { get; } = new[] { "Metro", "Blades" };

	// Reuse the catalogue and playback without opening the dashboard overlay.
	public void OpenBladesGuideMusicSources()
	{
		EnsureMusicLibraryLoaded();
		OpenMusicSourceBrowser();
	}

	public IReadOnlyList<string> DashboardSoundOptions { get; } = new _003C_003Ez__ReadOnlyArray<string>(new string[2] { "Metro", "Blades" });

	public IReadOnlyList<string> DashboardStartupOptions { get; } = new _003C_003Ez__ReadOnlyArray<string>(new string[2] { "Metro", "Blades" });

	public double DashboardAspectFrameWidth
	{
		get
		{
			return Settings.DisplayResolution switch
			{
				"21:9" => 1680.0, 
				_ => 1280.0, 
			};
		}
	}

	public double DashboardAspectFrameHeight
	{
		get
		{
			return Settings.DisplayResolution switch
			{
				"21:9" => 720.0, 
				_ => 720.0, 
			};
		}
	}

	public void RefreshDisplayAspectRatioBindings()
	{
		Settings.DisplayResolution = NormalizeDisplayAspectRatio(Settings.DisplayResolution);
		OnPropertyChanged("DashboardAspectFrameWidth");
		OnPropertyChanged("DashboardAspectFrameHeight");
	}

	public void RefreshDashboardStyleBindings()
	{
		Settings.DashboardStyle = NormalizeDashboardStyle(Settings.DashboardStyle);
		OnPropertyChanged("IsBladesDashboardStyle");
		OnPropertyChanged("CurrentThemeBackgroundPath");
	}

	public IReadOnlyList<string> GameCoverFitOptions { get; } = new _003C_003Ez__ReadOnlyArray<string>(new string[4] { "Auto", "Cover", "Fill", "Fit" });

	public IReadOnlyList<string> AddDestinationOptions { get; } = new _003C_003Ez__ReadOnlyArray<string>(new string[2] { "My Games", "My Apps" });

	public IReadOnlyList<string> SocialIntegrationOptions { get; } = new _003C_003Ez__ReadOnlySingleElementList<string>("Local");

	public ObservableCollection<GameDetailsTabViewModel> GameDetailsTabs { get; }

	public ObservableCollection<GameDetailsExtraViewModel> GameDetailsExtras { get; } = new ObservableCollection<GameDetailsExtraViewModel>();

	public ObservableCollection<GameDetailsExtraViewModel> GameDetailsPreviewExtras { get; } = new ObservableCollection<GameDetailsExtraViewModel>();

	public ObservableCollection<string> GameDetailsGalleryImages { get; } = new ObservableCollection<string>();

	public ObservableCollection<AppLibraryTileViewModel> AppLibraryTiles { get; } = new ObservableCollection<AppLibraryTileViewModel>();

	public bool IsGameDetailsOverviewTab
	{
		get
		{
			if (SelectedGameDetailsTabKey == "overview")
			{
				return !IsGameDetailsSeeAllOpen;
			}
			return false;
		}
	}

	public bool IsGameDetailsDetailsTab
	{
		get
		{
			if (SelectedGameDetailsTabKey == "details")
			{
				return !IsGameDetailsSeeAllOpen;
			}
			return false;
		}
	}

	public bool IsGameDetailsExtrasTab
	{
		get
		{
			if (SelectedGameDetailsTabKey == "extras")
			{
				return !IsGameDetailsSeeAllOpen;
			}
			return false;
		}
	}

	public bool IsGameDetailsGalleryTab
	{
		get
		{
			if (SelectedGameDetailsTabKey == "gallery")
			{
				return !IsGameDetailsSeeAllOpen;
			}
			return false;
		}
	}

	public bool IsGameDetailsSeeAllOpen
	{
		get
		{
			return _isGameDetailsSeeAllOpen;
		}
		set
		{
			if (SetProperty(ref _isGameDetailsSeeAllOpen, value, "IsGameDetailsSeeAllOpen"))
			{
				NotifyGameDetailsTabVisibility();
				OnPropertyChanged("GameDetailsSeeAllCountText");
			}
		}
	}

	public string SelectedGameDetailsTabKey
	{
		get
		{
			if (GameDetailsTabs.Count != 0)
			{
				return GameDetailsTabs[_selectedGameDetailsTabIndex].Key;
			}
			return "overview";
		}
	}

	public string GameDetailsDeveloperText
	{
		get
		{
			GameCardViewModel? selectedGame = SelectedGame;
			if (selectedGame == null || !selectedGame.IsSteamGame)
			{
				return "User added";
			}
			return "Steam";
		}
	}

	public string GameDetailsPublisherText
	{
		get
		{
			GameCardViewModel? selectedGame = SelectedGame;
			if (selectedGame == null || !selectedGame.IsSteamGame)
			{
				return "Manual";
			}
			return "Steam";
		}
	}

	public string GameDetailsGenreText => SelectedGame?.DetailsGenreText ?? "Game";

	public string GameDetailsLocalCapabilitiesText => string.Join(Environment.NewLine, BuildLocalCapabilities());

	public string GameDetailsOnlineCapabilitiesText => string.Join(Environment.NewLine, BuildOnlineCapabilities());

	public string GameDetailsNoteText => BuildGameDetailsNote();

	public string GameDetailsSeeAllCountText
	{
		get
		{
			if (GameDetailsExtras.Count != 0)
			{
				return $"{Math.Min(1, GameDetailsExtras.Count)} of {GameDetailsExtras.Count}";
			}
			return "0 of 0";
		}
	}

	public string GameDetailsGalleryImagePath => BuildGameDetailsGalleryImages().FirstOrDefault() ?? string.Empty;

	public string GameDetailsGalleryCountText
	{
		get
		{
			if (!string.IsNullOrWhiteSpace(GameDetailsGalleryImagePath))
			{
				return "1 of 1";
			}
			return "0 of 0";
		}
	}

	public string GameDetailsAddOnIconPath => AppPaths.ResolvePath(Path.Combine("Assets", "Misc", "add on icon.png"));

	public string GameDetailsSeeAllIconPath => AppPaths.ResolvePath(Path.Combine("Assets", "Misc", "see all icon.png"));

	public DashboardTabViewModel? CurrentTab
	{
		get
		{
			return _currentTab;
		}
		set
		{
			if (!SetProperty(ref _currentTab, value, "CurrentTab") || value == null)
			{
				return;
			}
			foreach (DashboardTabViewModel tab in Tabs)
			{
				tab.IsSelected = tab == value;
			}
			if (_suppressNextTabSound)
			{
				_suppressNextTabSound = false;
				_pendingTabSound = null;
			}
			else
			{
				_audioService.Play(_pendingTabSound ?? "tab");
			}
			_pendingTabSound = null;
			OnPropertyChanged("CurrentTabName");
			OnPropertyChanged("PreviousTab");
			OnPropertyChanged("NextTab");
			OnPropertyChanged("LeftPreviewContentLeft");
			OnPropertyChanged("RightPreviewContentLeft");
			OnPropertyChanged("CurrentReferenceImagePath");
			OnPropertyChanged("CurrentReferenceImageOpacity");
			OnPropertyChanged("UseLightDashboardChrome");
			OnPropertyChanged("CurrentThemeBackgroundPath");
			CloseBladesSubmenu(playSound: false);
			ResetBladesMenuSelection();
		}
	}

	public string CurrentTabName => CurrentTab?.Name ?? string.Empty;

	public double LeftPreviewContentLeft => 0.0 - GetLeftPreviewCrop(PreviousTab?.Key);

	public double RightPreviewContentLeft => 0.0 - GetRightPreviewCrop(CurrentTab?.Key, NextTab?.Key);

	private static double GetLeftPreviewCrop(string? tabKey)
	{
		return tabKey switch
		{
			"settings" => 954.0,
			"media" or "video" or "music" or "apps" => 938.0,
			"games" => 913.0,
			_ => 910.0
		};
	}

	private static double GetRightPreviewCrop(string? currentTabKey, string? tabKey)
	{
		if ((currentTabKey == "media" || currentTabKey == "video") && tabKey == "games")
		{
			return 198.0;
		}
		if (currentTabKey == "social" && (tabKey == "media" || tabKey == "video"))
		{
			return 198.0;
		}
		if (currentTabKey == "games" && tabKey == "music")
		{
			return 206.0;
		}
		if (currentTabKey == "music" && tabKey == "apps")
		{
			return 206.0;
		}
		return tabKey switch
		{
			"bing" => 198.0,
			"home" or "social" or "settings" => 224.0,
			"games" => 258.0,
			"media" or "video" or "music" or "apps" => 236.0,
			_ => 224.0
		};
	}

	public DashboardTabViewModel? PreviousTab
	{
		get
		{
			if (CurrentTab == null)
			{
				return null;
			}
			int num = Tabs.IndexOf(CurrentTab);
			if (num <= 0)
			{
				return null;
			}
			return Tabs[num - 1];
		}
	}

	public DashboardTabViewModel? NextTab
	{
		get
		{
			if (CurrentTab == null)
			{
				return null;
			}
			int num = Tabs.IndexOf(CurrentTab);
			if (num < 0 || num >= Tabs.Count - 1)
			{
				return null;
			}
			return Tabs[num + 1];
		}
	}

	public string CurrentReferenceImagePath => string.Empty;

	public double CurrentReferenceImageOpacity => 0.0;

	public bool UseLightDashboardChrome => false;

	public bool IsBladesDashboardStyle => string.Equals(Settings.DashboardStyle, "Blades", StringComparison.OrdinalIgnoreCase);

	public IReadOnlyList<BladesBladeViewModel> BladesBlades => BuildBladesBlades();

	public BladesBladeViewModel? ActiveBladesBlade
	{
		get
		{
			IReadOnlyList<BladesBladeViewModel> blades = BladesBlades;
			if (blades.Count == 0)
			{
				return null;
			}
			return blades[Math.Clamp(_selectedBladesBladeIndex, 0, blades.Count - 1)];
		}
	}

	public IReadOnlyList<BladesBladeViewModel> LeftBlades
	{
		get
		{
			IReadOnlyList<BladesBladeViewModel> blades = BladesBlades;
			int count = Math.Clamp(_selectedBladesBladeIndex, 0, blades.Count);
			return blades.Take(count).ToArray();
		}
	}

	public IReadOnlyList<BladesBladeViewModel> RightBlades
	{
		get
		{
			IReadOnlyList<BladesBladeViewModel> blades = BladesBlades;
			int startIndex = Math.Clamp(_selectedBladesBladeIndex + 1, 0, blades.Count);
			return blades.Skip(startIndex).ToArray();
		}
	}

	public System.Windows.Media.Brush ActiveBladesAccentBrush => ActiveBladesBlade?.AccentBrush ?? new SolidColorBrush(System.Windows.Media.Color.FromRgb(48, 148, 74));

	public System.Windows.Media.Brush BladesOverlayAccentBrush
	{
		get
		{
			if (IsBladesDashboardStyle && IsLauncherSettingsOpen)
			{
				return new SolidColorBrush(System.Windows.Media.Color.FromRgb(102, 54, 168));
			}
			if (IsBladesDashboardStyle && IsMusicPlayerOpen)
			{
				return new SolidColorBrush(System.Windows.Media.Color.FromRgb(0, 104, 168));
			}
			return new SolidColorBrush(System.Windows.Media.Color.FromRgb(0, 138, 0));
		}
	}

	public int SelectedBladesMenuIndex
	{
		get
		{
			return _selectedBladesMenuIndex;
		}
		set
		{
			if (SetProperty(ref _selectedBladesMenuIndex, value, "SelectedBladesMenuIndex"))
			{
				IsBladesTraySelected=false;
				OnPropertyChanged("SelectedBladesMenuItem");
				NotifyActiveBladesMenuChanged();
			}
		}
	}

	public BladesMenuItemViewModel? SelectedBladesMenuItem
	{
		get
		{
			IReadOnlyList<BladesMenuItemViewModel> bladesMenuItems = ActiveBladesMenuItems;
			if (bladesMenuItems == null || bladesMenuItems.Count == 0)
			{
				return null;
			}
			return bladesMenuItems[Math.Clamp(ActiveBladesMenuIndex, 0, bladesMenuItems.Count - 1)];
		}
	}

	public bool IsBladesSubmenuOpen => !string.IsNullOrWhiteSpace(_bladesSubmenuKey);

	public bool IsBladesGamesLibraryRootSubmenu => string.Equals(_bladesSubmenuKey, "GamesLibraryRoot", StringComparison.OrdinalIgnoreCase);

	public bool IsBladesGameLibrarySubmenu => string.Equals(_bladesSubmenuKey, "GamesLibrary", StringComparison.OrdinalIgnoreCase)
		|| string.Equals(_bladesSubmenuKey, "PlayedGames", StringComparison.OrdinalIgnoreCase);

	public bool IsBladesAchievementsSubmenu => string.Equals(_bladesSubmenuKey, "Achievements", StringComparison.OrdinalIgnoreCase);

	public string BladesGameLibraryTab0Foreground => GetBladesGameLibraryTabForeground(0);

	public string BladesGameLibraryTab1Foreground => GetBladesGameLibraryTabForeground(1);

	public string BladesGameLibraryTab2Foreground => GetBladesGameLibraryTabForeground(2);

	public string BladesGameLibraryTab3Foreground => GetBladesGameLibraryTabForeground(3);

	public string BladesGameLibraryTab4Foreground => GetBladesGameLibraryTabForeground(4);

	public ObservableCollection<BladesAchievementGameViewModel> BladesAchievementGameItems => _bladesAchievementGameItems;

	public ObservableCollection<BladesAchievementItemViewModel> BladesAchievementItems => _bladesAchievementItems;

	public int SelectedBladesAchievementGameIndex
	{
		get
		{
			return _selectedBladesAchievementGameIndex;
		}
		set
		{
			int count = Math.Max(1, BladesAchievementGameItems.Count);
			int clampedValue = Math.Clamp(value, 0, count - 1);
			if (SetProperty(ref _selectedBladesAchievementGameIndex, clampedValue, "SelectedBladesAchievementGameIndex"))
			{
				_selectedBladesAchievementIndex = 0;
				RefreshBladesAchievementItems();
				OnPropertyChanged("SelectedBladesAchievementIndex");
				NotifyBladesAchievementsChanged();
			}
		}
	}

	public int SelectedBladesAchievementIndex
	{
		get
		{
			return _selectedBladesAchievementIndex;
		}
		set
		{
			int count = Math.Max(1, BladesAchievementItems.Count);
			int clampedValue = Math.Clamp(value, 0, count - 1);
			if (SetProperty(ref _selectedBladesAchievementIndex, clampedValue, "SelectedBladesAchievementIndex"))
			{
				NotifyBladesAchievementsChanged();
			}
		}
	}

	public bool IsBladesAchievementListFocused
	{
		get
		{
			return _isBladesAchievementListFocused;
		}
		set
		{
			if (SetProperty(ref _isBladesAchievementListFocused, value, "IsBladesAchievementListFocused"))
			{
				NotifyBladesAchievementsChanged();
			}
		}
	}

	public string BladesAchievementsHeaderTitle
	{
		get
		{
			if (IsBladesAchievementListFocused && SelectedBladesAchievementItem != null)
			{
				return SelectedBladesAchievementItem.GameTitle;
			}
			return SelectedBladesAchievementGame?.Title ?? "All Games";
		}
	}

	public string BladesAchievementsHeaderCountText
	{
		get
		{
			if (IsBladesAchievementListFocused && SelectedBladesAchievementItem != null)
			{
				if (!string.IsNullOrWhiteSpace(SelectedBladesAchievementItem.Description))
				{
					return SelectedBladesAchievementItem.Description;
				}
				return SelectedBladesAchievementItem.Title;
			}
			int count = BladesAchievementItems.Count;
			return count == 1 ? "1 achievement" : $"{count} achievements";
		}
	}

	public string BladesAchievementsHeaderRightText
	{
		get
		{
			if (!IsBladesAchievementListFocused || _bladesAchievementItems.Count == 0)
			{
				return string.Empty;
			}
			return $"{Math.Clamp(_selectedBladesAchievementIndex + 1, 1, _bladesAchievementItems.Count)} / {_bladesAchievementItems.Count}";
		}
	}

	public BladesAchievementGameViewModel? SelectedBladesAchievementGame
	{
		get
		{
			IReadOnlyList<BladesAchievementGameViewModel> games = _bladesAchievementGameItems;
			if (games.Count == 0)
			{
				return null;
			}
			return games[Math.Clamp(_selectedBladesAchievementGameIndex, 0, games.Count - 1)];
		}
	}

	public BladesAchievementItemViewModel? SelectedBladesAchievementItem
	{
		get
		{
			IReadOnlyList<BladesAchievementItemViewModel> achievements = _bladesAchievementItems;
			if (achievements.Count == 0)
			{
				return null;
			}
			return achievements[Math.Clamp(_selectedBladesAchievementIndex, 0, achievements.Count - 1)];
		}
	}

	public bool IsBladesStandardSubmenu => IsBladesSubmenuOpen && !IsBladesGamesLibraryRootSubmenu && !IsBladesGameLibrarySubmenu && !IsBladesAchievementsSubmenu;

	public bool IsActiveBladesProfileVisible => IsActiveBladesGames || IsActiveBladesMedia;

	public bool IsActiveBladesMarketplace => string.Equals(ActiveBladesBlade?.Key, "marketplace", StringComparison.OrdinalIgnoreCase);

	public bool IsActiveBladesXboxLive => string.Equals(ActiveBladesBlade?.Key, "xbox live", StringComparison.OrdinalIgnoreCase);

	public bool IsActiveBladesGames => string.Equals(ActiveBladesBlade?.Key, "games", StringComparison.OrdinalIgnoreCase);

	public bool IsActiveBladesMedia => string.Equals(ActiveBladesBlade?.Key, "media", StringComparison.OrdinalIgnoreCase);

	public bool IsActiveBladesSystem => string.Equals(ActiveBladesBlade?.Key, "system", StringComparison.OrdinalIgnoreCase);

	public bool IsActiveBladesOpenTrayVisible => IsActiveBladesGames || IsActiveBladesMedia || IsActiveBladesXboxLive;

	public bool IsActiveBladesGenericMenuVisible => IsActiveBladesGames || IsActiveBladesMedia || IsActiveBladesSystem;

	public string ActiveBladesTitle
	{
		get
		{
			if (!IsBladesSubmenuOpen)
			{
				return ActiveBladesBlade?.Title ?? string.Empty;
			}
			return _bladesSubmenuKey switch
			{
				"GamesLibrary" => "Games Library",
				"GamesLibraryRoot" => "Games Library",
				"Achievements" => "Achievements",
				"PlayedGames" => "Played Games",
				"MusicSources" => "Music Sources",
				"HardDrive" => "Hard Drive",
				"Apps" => "My Apps",
				"Profile" => "Profile",
				"InsideXbox" => "Inside Xbox",
				"ConsoleSettings" => "Console Settings",
				"DashboardCustomization" => "Dashboard Customization",
				"GamesSetup" => "Games Setup",
				"Audio" => "Audio",
				"DataControl" => "Data Control",
				_ => ActiveBladesBlade?.Title ?? string.Empty
			};
		}
	}

	public IReadOnlyList<BladesMenuItemViewModel> ActiveBladesMenuItems => MarkSelectedBladesItem(IsBladesSubmenuOpen ? BuildBladesSubmenuItems() : (ActiveBladesBlade?.MenuItems ?? Array.Empty<BladesMenuItemViewModel>()), IsBladesTraySelected && !IsBladesSubmenuOpen ? -1 : ActiveBladesMenuIndex);

	public IReadOnlyList<BladesMenuItemViewModel> BladesGameLibraryVisibleMenuItems => BuildBladesGameLibraryVisibleMenuItems();

	public int ActiveBladesMenuIndex => IsBladesSubmenuOpen ? _selectedBladesSubmenuIndex : SelectedBladesMenuIndex;

	public BladesMenuItemViewModel? ActiveBladesSelectedItem => SelectedBladesMenuItem;

	public GameCardViewModel? SelectedGame
	{
		get
		{
			return _selectedGame;
		}
		set
		{
			GameCardViewModel? selectedGame = _selectedGame;
			if (SetProperty(ref _selectedGame, value, "SelectedGame"))
			{
				if (value != null && selectedGame != null && _hasSelectionSoundBaseline)
				{
					PlayFocusSound();
				}
				if (value != null)
				{
					_hasSelectionSoundBaseline = true;
					FeaturedGame = value;
					StatusMessage = value.Title;
				}
				RefreshVisibleLibraryMenuGames();
				OnPropertyChanged("SpotlightTitle");
				OnPropertyChanged("SpotlightSubtitle");
				OnPropertyChanged("MyGamesCountText");
				OnPropertyChanged("LibraryMenuCountText");
				OnPropertyChanged("LibraryMenuSelectedTitle");
				OnPropertyChanged("SelectedCoverZoom");
				OnPropertyChanged("SelectedCoverOffsetX");
				OnPropertyChanged("SelectedCoverOffsetY");
				RefreshGameDetailsPanels();
			}
		}
	}

	public GameCardViewModel? FeaturedGame
	{
		get
		{
			return _featuredGame;
		}
		set
		{
			if (SetProperty(ref _featuredGame, value, "FeaturedGame"))
			{
				OnPropertyChanged("SpotlightTitle");
				OnPropertyChanged("SpotlightSubtitle");
			}
		}
	}

	public Profile Profile
	{
		get
		{
			return _profile;
		}
		set
		{
			if (ReferenceEquals(_profile, value))
			{
				return;
			}
			if (_profile != null)
			{
				_profile.PropertyChanged -= Profile_OnPropertyChanged;
			}
			if (SetProperty(ref _profile, value, "Profile"))
			{
				if (_profile != null)
				{
					_profile.PropertyChanged += Profile_OnPropertyChanged;
				}
				RefreshEditableProfileFields();
			}
		}
	}

	public bool IsProfileMenuEditing
	{
		get
		{
			return _isProfileMenuEditing;
		}
		set
		{
			if (SetProperty(ref _isProfileMenuEditing, value, "IsProfileMenuEditing"))
			{
				OnPropertyChanged("ProfileEditMenuText");
			}
		}
	}

	public string ProfileEditMenuText => IsProfileMenuEditing ? "Save Profile" : "Edit Profile";

	public string EditableProfileGamertag
	{
		get
		{
			return _editableProfileGamertag;
		}
		set
		{
			SetProperty(ref _editableProfileGamertag, value, "EditableProfileGamertag");
		}
	}

	public string EditableProfileName
	{
		get
		{
			return _editableProfileName;
		}
		set
		{
			SetProperty(ref _editableProfileName, value, "EditableProfileName");
		}
	}

	public string EditableProfileMotto
	{
		get
		{
			return _editableProfileMotto;
		}
		set
		{
			SetProperty(ref _editableProfileMotto, value, "EditableProfileMotto");
		}
	}

	public string EditableProfileLocation
	{
		get
		{
			return _editableProfileLocation;
		}
		set
		{
			SetProperty(ref _editableProfileLocation, value, "EditableProfileLocation");
		}
	}

	public string EditableProfileDescription
	{
		get
		{
			return _editableProfileDescription;
		}
		set
		{
			SetProperty(ref _editableProfileDescription, value, "EditableProfileDescription");
		}
	}

	public string EditableProfileGamerscore
	{
		get
		{
			return _editableProfileGamerscore;
		}
		set
		{
			SetProperty(ref _editableProfileGamerscore, value, "EditableProfileGamerscore");
		}
	}

	public AppSettings Settings
	{
		get
		{
			return _settings;
		}
		set
		{
			value.GameCoverFitMode = NormalizeGameCoverFitMode(value.GameCoverFitMode);
			value.DefaultAddDestination = NormalizeAddDestination(value.DefaultAddDestination);
			value.DisplayResolution = NormalizeDisplayAspectRatio(value.DisplayResolution);
			value.DashboardStyle = NormalizeDashboardStyle(value.DashboardStyle);
			value.DashboardSounds = NormalizeDashboardThemeChoice(value.DashboardSounds);
			value.DashboardStartup = NormalizeDashboardThemeChoice(value.DashboardStartup);
			value.AudioOutputDeviceName = (string.IsNullOrWhiteSpace(value.AudioOutputDeviceName) ? "Default" : value.AudioOutputDeviceName);
			value.DashboardVolume = Math.Clamp(value.DashboardVolume, 0.0, 1.0);
			value.SocialIntegrationMode = NormalizeSocialIntegrationMode(value.SocialIntegrationMode);
			value.DashboardTileColor = NormalizeDashboardTileColor(value.DashboardTileColor);
			if (value.DashboardTileCustomizations == null)
			{
				Dictionary<string, DashboardTileCustomization> dictionary = (value.DashboardTileCustomizations = new Dictionary<string, DashboardTileCustomization>());
			}
			if (SetProperty(ref _settings, value, "Settings"))
			{
				ApplyDashboardAccentResources(value.DashboardTileColor);
				SyncDashboardCustomizerFromSettings();
				ApplyDashboardTileColorToSliders(value.DashboardTileColor);
				OnPropertyChanged("OpenTrayTitle");
				OnPropertyChanged("ResolutionOptions");
				OnPropertyChanged("DashboardStyleOptions");
				OnPropertyChanged("DashboardSoundOptions");
				OnPropertyChanged("DashboardStartupOptions");
				OnPropertyChanged("DashboardAspectFrameWidth");
				OnPropertyChanged("DashboardAspectFrameHeight");
				OnPropertyChanged("GameCoverFitMode");
				OnPropertyChanged("DefaultAddDestination");
				OnPropertyChanged("IsAddingToApps");
				OnPropertyChanged("SelectedCoverPreviewWidth");
				OnPropertyChanged("SelectedCoverPreviewHeight");
				OnPropertyChanged("AudioOutputDeviceName");
				OnPropertyChanged("AudioOutputDeviceOptions");
				OnPropertyChanged("DashboardVolume");
				OnPropertyChanged("DashboardVolumeText");
				OnPropertyChanged("SocialIntegrationModeDisplay");
				OnPropertyChanged("CurrentThemeBackgroundPath");
				OnPropertyChanged("IsBladesDashboardStyle");
				OnPropertyChanged("DashboardTileBrush");
				OnPropertyChanged("DashboardTileColorPreviewBrush");
				RefreshDashboardTileBindings();
			}
		}
	}

	public bool IsDashboardCustomizerOpen
	{
		get
		{
			return _isDashboardCustomizerOpen;
		}
		set
		{
			SetProperty(ref _isDashboardCustomizerOpen, value, "IsDashboardCustomizerOpen");
		}
	}

	public DashboardTabCustomizationViewModel? CurrentDashboardCustomizationTab
	{
		get
		{
			if (DashboardCustomizationTabs.Count != 0)
			{
				return DashboardCustomizationTabs[Math.Clamp(_customizerTabIndex, 0, DashboardCustomizationTabs.Count - 1)];
			}
			return null;
		}
	}

	public string CurrentDashboardCustomizationTabName => CurrentDashboardCustomizationTab?.Name ?? string.Empty;

	public DashboardTileCustomizationViewModel? SelectedDashboardTile
	{
		get
		{
			return _selectedDashboardTile;
		}
		set
		{
			if (_selectedDashboardTile != value)
			{
				if (_selectedDashboardTile != null)
				{
					_selectedDashboardTile.IsSelected = false;
					_selectedDashboardTile.PropertyChanged -= SelectedDashboardTile_OnPropertyChanged;
				}
				_selectedDashboardTile = value;
				if (_selectedDashboardTile != null)
				{
					_selectedDashboardTile.IsSelected = true;
					_selectedDashboardTile.PropertyChanged += SelectedDashboardTile_OnPropertyChanged;
				}
				OnPropertyChanged("SelectedDashboardTile");
				OnPropertyChanged("SelectedDashboardTileTitle");
				OnPropertyChanged("SelectedDashboardTileAllowsImageCustomization");
				OnPropertyChanged("SelectedDashboardTileAllowsLaunchCustomization");
				OnPropertyChanged("SelectedDashboardTileAllowsSecondaryTitleCustomization");
				OnPropertyChanged("SelectedDashboardTileUsesDashboardColor");
				OnPropertyChanged("SelectedDashboardTileCustomizationHint");
			}
		}
	}

	public string SelectedDashboardTileTitle => SelectedDashboardTile?.Title ?? "Select a tile";

	public bool SelectedDashboardTileAllowsImageCustomization => SelectedDashboardTile?.AllowsImageCustomization ?? false;

	public bool SelectedDashboardTileAllowsLaunchCustomization => SelectedDashboardTile?.AllowsLaunchCustomization ?? false;

	public bool SelectedDashboardTileAllowsSecondaryTitleCustomization => SelectedDashboardTile?.AllowsSecondaryTitleCustomization ?? false;

	public bool SelectedDashboardTileUsesDashboardColor => SelectedDashboardTile?.UsesDashboardColor ?? false;

	public string SelectedDashboardTileCustomizationHint => SelectedDashboardTile?.CustomizationHint ?? "Select a tile.";

	public System.Windows.Media.Brush DashboardTileBrush => CreateDashboardTileBrush(Settings.DashboardTileColor);

	public System.Windows.Media.Brush DashboardTileDarkBrush => CreateBrush(ToDarkAccentColor(Settings.DashboardTileColor));

	public System.Windows.Media.Brush DashboardTileColorPreviewBrush => CreateBrush(Settings.DashboardTileColor);

	public double CustomTileRed
	{
		get
		{
			return (int)_customTileRed;
		}
		set
		{
			byte b = ToByte(value);
			if (_customTileRed != b)
			{
				_customTileRed = b;
				OnPropertyChanged("CustomTileRed");
				UpdateDashboardTileColorFromSliders();
			}
		}
	}

	public double CustomTileGreen
	{
		get
		{
			return (int)_customTileGreen;
		}
		set
		{
			byte b = ToByte(value);
			if (_customTileGreen != b)
			{
				_customTileGreen = b;
				OnPropertyChanged("CustomTileGreen");
				UpdateDashboardTileColorFromSliders();
			}
		}
	}

	public double CustomTileBlue
	{
		get
		{
			return (int)_customTileBlue;
		}
		set
		{
			byte b = ToByte(value);
			if (_customTileBlue != b)
			{
				_customTileBlue = b;
				OnPropertyChanged("CustomTileBlue");
				UpdateDashboardTileColorFromSliders();
			}
		}
	}

	public string GameCoverFitMode
	{
		get
		{
			return Settings.GameCoverFitMode;
		}
		set
		{
			value = NormalizeGameCoverFitMode(value);
			if (!string.Equals(Settings.GameCoverFitMode, value, StringComparison.Ordinal))
			{
				Settings.GameCoverFitMode = value;
				OnPropertyChanged("GameCoverFitMode");
				SelectedGame?.Refresh();
				foreach (GameCardViewModel visibleLibraryMenuGame in VisibleLibraryMenuGames)
				{
					visibleLibraryMenuGame.Refresh();
				}
			}
		}
	}

	public string DefaultAddDestination
	{
		get
		{
			return Settings.DefaultAddDestination;
		}
		set
		{
			value = NormalizeAddDestination(value);
			if (!string.Equals(Settings.DefaultAddDestination, value, StringComparison.Ordinal))
			{
				Settings.DefaultAddDestination = value;
				OnPropertyChanged("DefaultAddDestination");
				OnPropertyChanged("IsAddingToApps");
				RefreshSetupDestinationState();
				OnPropertyChanged("SelectedCoverPreviewWidth");
				OnPropertyChanged("SelectedCoverPreviewHeight");
			}
		}
	}

	public bool IsAddingToApps => string.Equals(DefaultAddDestination, "My Apps", StringComparison.OrdinalIgnoreCase);

	public string SetupItemLabel => IsAddingToApps ? "App to edit" : "Game to edit";

	public string SetupItemTag => IsAddingToApps ? "App to Edit|Select the app whose name, icon, and launch settings you want to edit." : "Game to Edit|Select the game whose name, images, and launch settings you want to edit.";

	public string SetupCoverFitLabel => IsAddingToApps ? "My Apps Icon fit" : "My Games Cover Fit";

	public string SetupCoverFitTag => IsAddingToApps ? "My Apps Icon Fit|Choose how app icons are fit or cropped in the My Apps view." : "My Games Cover Fit|Choose how cover art is fit or cropped in the My Games view.";

	public IEnumerable<GameCardViewModel> SetupEditableItems
	{
		get
		{
			if (IsAddingToApps)
			{
				return Games.Where((GameCardViewModel game) => IsAppEntry(game.Game) && !IsBuiltInYouTubeEntry(game.Game));
			}
			return Games.Where((GameCardViewModel game) => !IsAppEntry(game.Game));
		}
	}

	public double SelectedCoverPreviewWidth => IsAddingToApps ? 102.0 : 72.0;

	public double SelectedCoverPreviewHeight => 102.0;

	public string AudioOutputDeviceName
	{
		get
		{
			return Settings.AudioOutputDeviceName;
		}
		set
		{
			if (string.IsNullOrWhiteSpace(value))
			{
				return;
			}
			value = NormalizeAudioOutputDeviceName(value);
			if (!string.Equals(Settings.AudioOutputDeviceName, value, StringComparison.Ordinal))
			{
				Settings.AudioOutputDeviceName = value;
				OnPropertyChanged("AudioOutputDeviceName");
				_ = _settingsService.SaveAsync(Settings);
			}
		}
	}

	public double DashboardVolume
	{
		get
		{
			return Settings.DashboardVolume;
		}
		set
		{
			double volume = Math.Clamp(value, 0.0, 1.0);
			if (Math.Abs(Settings.DashboardVolume - volume) > 0.0001)
			{
				Settings.DashboardVolume = volume;
				ApplyMusicPlayerVolume();
				OnPropertyChanged("DashboardVolume");
				OnPropertyChanged("DashboardVolumeText");
			}
		}
	}

	public string DashboardVolumeText => $"{Math.Round(DashboardVolume * 100.0)}%";

	public string SocialIntegrationModeDisplay
	{
		get
		{
			return ToSocialIntegrationDisplay(Settings.SocialIntegrationMode);
		}
		set
		{
			SocialIntegrationMode socialIntegrationMode = ParseSocialIntegrationMode(value);
			if (Settings.SocialIntegrationMode != socialIntegrationMode)
			{
				Settings.SocialIntegrationMode = socialIntegrationMode;
				OnPropertyChanged("SocialIntegrationModeDisplay");
			}
		}
	}

	public double SelectedCoverZoom
	{
		get
		{
			GameCardViewModel? selectedGame = SelectedGame;
			if (selectedGame == null || !(selectedGame.Game.CoverZoom > 0.0))
			{
				return 1.0;
			}
			return SelectedGame.Game.CoverZoom;
		}
		set
		{
			if (SelectedGame != null)
			{
				double num = Math.Clamp(value, 1.0, 1.8);
				if (!(Math.Abs(SelectedGame.Game.CoverZoom - num) < 0.001))
				{
					SelectedGame.Game.CoverZoom = num;
					SelectedGame.Refresh();
					OnPropertyChanged("SelectedCoverZoom");
				}
			}
		}
	}

	public double SelectedCoverOffsetX
	{
		get
		{
			return SelectedGame?.Game.CoverOffsetX ?? 0.0;
		}
		set
		{
			if (SelectedGame != null)
			{
				double num = Math.Clamp(value, -1.0, 1.0);
				if (!(Math.Abs(SelectedGame.Game.CoverOffsetX - num) < 0.001))
				{
					SelectedGame.Game.CoverOffsetX = num;
					SelectedGame.Refresh();
					OnPropertyChanged("SelectedCoverOffsetX");
				}
			}
		}
	}

	public double SelectedCoverOffsetY
	{
		get
		{
			return SelectedGame?.Game.CoverOffsetY ?? 0.0;
		}
		set
		{
			if (SelectedGame != null)
			{
				double num = Math.Clamp(value, -1.0, 1.0);
				if (!(Math.Abs(SelectedGame.Game.CoverOffsetY - num) < 0.001))
				{
					SelectedGame.Game.CoverOffsetY = num;
					SelectedGame.Refresh();
					OnPropertyChanged("SelectedCoverOffsetY");
				}
			}
		}
	}

	public string SearchQuery
	{
		get
		{
			return _searchQuery;
		}
		set
		{
			SetProperty(ref _searchQuery, value, "SearchQuery");
		}
	}

	public string StatusMessage
	{
		get
		{
			return _statusMessage;
		}
		set
		{
			SetProperty(ref _statusMessage, value, "StatusMessage");
		}
	}

	public bool IsSearchOverlayOpen
	{
		get
		{
			return _isSearchOverlayOpen;
		}
		set
		{
			SetProperty(ref _isSearchOverlayOpen, value, "IsSearchOverlayOpen");
		}
	}

	public bool IsDetailsOpen
	{
		get
		{
			return _isDetailsOpen;
		}
		set
		{
			if (SetProperty(ref _isDetailsOpen, value, "IsDetailsOpen") && value)
			{
				RefreshSelectedGameDetailsAsync();
			}
		}
	}

	public bool IsQuickMenuOpen
	{
		get
		{
			return _isQuickMenuOpen;
		}
		set
		{
			SetProperty(ref _isQuickMenuOpen, value, "IsQuickMenuOpen");
		}
	}

	public bool IsMyGamesOpen
	{
		get
		{
			return _isMyGamesOpen;
		}
		set
		{
			if (SetProperty(ref _isMyGamesOpen, value, "IsMyGamesOpen"))
			{
				OnPropertyChanged("IsDashboardContentHidden");
				OnPropertyChanged("CurrentThemeBackgroundPath");
			}
		}
	}

	public bool IsLauncherSettingsOpen
	{
		get
		{
			return _isLauncherSettingsOpen;
		}
		set
		{
			if (SetProperty(ref _isLauncherSettingsOpen, value, "IsLauncherSettingsOpen"))
			{
				OnPropertyChanged("BladesOverlayAccentBrush");
				OnPropertyChanged("IsDashboardContentHidden");
				OnPropertyChanged("CurrentThemeBackgroundPath");
			}
		}
	}

	public bool IsProfileEditorOpen
	{
		get
		{
			return _isProfileEditorOpen;
		}
		set
		{
			if (SetProperty(ref _isProfileEditorOpen, value, "IsProfileEditorOpen"))
			{
				OnPropertyChanged("CurrentThemeBackgroundPath");
			}
		}
	}

	public bool IsThemeMenuOpen
	{
		get
		{
			return _isThemeMenuOpen;
		}
		set
		{
			if (SetProperty(ref _isThemeMenuOpen, value, "IsThemeMenuOpen"))
			{
				OnPropertyChanged("ThemeMenuVisibilityTitle");
			}
		}
	}

	public bool IsThemeCreatorOpen
	{
		get
		{
			return _isThemeCreatorOpen;
		}
		set
		{
			if (SetProperty(ref _isThemeCreatorOpen, value, "IsThemeCreatorOpen"))
			{
				OnPropertyChanged("IsDashboardContentHidden");
				OnPropertyChanged("CurrentThemeBackgroundPath");
			}
		}
	}

	public bool IsDashboardContentHidden
	{
		get
		{
			if (!IsMyGamesOpen && !IsLauncherSettingsOpen)
			{
				return IsThemeCreatorOpen;
			}
			return true;
		}
	}

	public bool IsSteamSetupOpen
	{
		get
		{
			return _isSteamSetupOpen;
		}
		set
		{
			SetProperty(ref _isSteamSetupOpen, value, "IsSteamSetupOpen");
		}
	}

	public bool IsMusicPlayerOpen
	{
		get
		{
			return _isMusicPlayerOpen;
		}
		set
		{
			if (SetProperty(ref _isMusicPlayerOpen, value, "IsMusicPlayerOpen"))
			{
				EnsureAudioAnalysisState();
				OnPropertyChanged("BladesOverlayAccentBrush");
				OnPropertyChanged("CurrentThemeBackgroundPath");
			}
		}
	}

	public bool IsYouTubeTvOpen
	{
		get
		{
			return _isYouTubeTvOpen;
		}
		set
		{
			if (SetProperty(ref _isYouTubeTvOpen, value, "IsYouTubeTvOpen"))
			{
				OnPropertyChanged("CurrentThemeBackgroundPath");
			}
		}
	}

	public bool IsSpotifySetupOpen
	{
		get
		{
			return _isSpotifySetupOpen;
		}
		set
		{
			SetProperty(ref _isSpotifySetupOpen, value, "IsSpotifySetupOpen");
		}
	}

	public bool IsMusicPlayerTransparent
	{
		get
		{
			return _isMusicPlayerTransparent;
		}
		private set
		{
			SetProperty(ref _isMusicPlayerTransparent, value, "IsMusicPlayerTransparent");
		}
	}

	public bool IsMusicVisualizerFullscreen
	{
		get
		{
			return _isMusicVisualizerFullscreen;
		}
		private set
		{
			if (SetProperty(ref _isMusicVisualizerFullscreen, value, "IsMusicVisualizerFullscreen"))
			{
				OnPropertyChanged("IsMusicBrowserVisible");
				OnPropertyChanged("IsMusicNowPlayingVisible");
			}
		}
	}

	public bool IsMusicPlaying
	{
		get
		{
			return _isMusicPlaying;
		}
		set
		{
			if (SetProperty(ref _isMusicPlaying, value, "IsMusicPlaying"))
			{
				EnsureAudioAnalysisState();
				OnPropertyChanged("MusicPlayPauseText");
			}
		}
	}

	public bool IsShuffleEnabled
	{
		get
		{
			return _isShuffleEnabled;
		}
		set
		{
			if (SetProperty(ref _isShuffleEnabled, value, "IsShuffleEnabled"))
			{
				OnPropertyChanged("ShuffleText");
			}
		}
	}

	public bool IsMusicNowPlayingScreen => string.Equals(_musicBrowserScreen, "NowPlaying", StringComparison.OrdinalIgnoreCase);

	public bool IsMusicSourceBrowser => string.Equals(_musicBrowserScreen, "Source", StringComparison.OrdinalIgnoreCase);

	public bool ShowMusicBrowserMenuArrows => !IsMusicSourceBrowser;

	public bool IsMusicHardDriveBrowser => string.Equals(_musicBrowserScreen, "HardDrive", StringComparison.OrdinalIgnoreCase);

	public bool IsMusicSpotifyBrowser => string.Equals(_musicBrowserScreen, "Spotify", StringComparison.OrdinalIgnoreCase);

	public bool IsMusicFolderLinkVisible => IsMusicSourceBrowser || (IsMusicHardDriveBrowser && MusicBrowserResultItems.Count == 0);

	public bool IsMusicBrowserResultsFocused
	{
		get
		{
			return _isMusicBrowserResultsFocused;
		}
		private set
		{
			SetProperty(ref _isMusicBrowserResultsFocused, value, "IsMusicBrowserResultsFocused");
		}
	}

	public bool IsMusicBrowserVisible
	{
		get
		{
			if (!IsMusicVisualizerFullscreen)
			{
				return !IsMusicNowPlayingScreen;
			}
			return false;
		}
	}

	public bool IsMusicNowPlayingVisible
	{
		get
		{
			if (!IsMusicVisualizerFullscreen)
			{
				return IsMusicNowPlayingScreen;
			}
			return false;
		}
	}

	public string MusicPlayerHeaderText => _musicBrowserScreen switch
	{
		"HardDrive" => "Hard Drive",
		"Spotify" => "Spotify",
		"NowPlaying" => "Now Playing",
		_ => "Music Sources",
	};

	public MusicBrowserMenuItemViewModel? SelectedMusicBrowserMenuItem
	{
		get
		{
			return _selectedMusicBrowserMenuItem;
		}
		private set
		{
			if (SetProperty(ref _selectedMusicBrowserMenuItem, value, "SelectedMusicBrowserMenuItem"))
			{
				foreach (MusicBrowserMenuItemViewModel item in MusicBrowserMenuItems)
				{
					item.IsSelected = item == value;
				}
				OnPropertyChanged("MusicBrowserDetailTitle");
				OnPropertyChanged("MusicBrowserDetailDescription");
				OnPropertyChanged("MusicBrowserDetailIcon");
				OnPropertyChanged("MusicBrowserDetailIconPath");
			}
		}
	}

	public MusicBrowserResultItemViewModel? SelectedMusicBrowserResultItem
	{
		get
		{
			return _selectedMusicBrowserResultItem;
		}
		private set
		{
			if (SetProperty(ref _selectedMusicBrowserResultItem, value, "SelectedMusicBrowserResultItem"))
			{
				foreach (MusicBrowserResultItemViewModel item in MusicBrowserResultItems)
				{
					item.IsSelected = item == value;
				}
				OnPropertyChanged("MusicBrowserResultCountText");
			}
		}
	}

	public string MusicBrowserDetailTitle => SelectedMusicBrowserMenuItem?.Title ?? string.Empty;

	public string MusicBrowserDetailDescription => SelectedMusicBrowserMenuItem?.Description ?? string.Empty;

	public string MusicBrowserDetailIcon => SelectedMusicBrowserMenuItem?.Icon ?? string.Empty;

	public string MusicBrowserDetailIconPath => SelectedMusicBrowserMenuItem?.IconPath ?? string.Empty;

	public string MusicBrowserSourceFolderTitle => "Music Files";

	public string MusicBrowserSourceFolderPath => Path.Combine("Assets", "Custom Files", "Music Files");

	public bool HasMusicBrowserResults => MusicBrowserResultItems.Count > 0;

	public string MusicBrowserResultCountText
	{
		get
		{
			if (MusicBrowserResultItems.Count == 0)
			{
				return "0 of 0";
			}
			int selectedIndex = MusicBrowserResultItems.IndexOf(SelectedMusicBrowserResultItem);
			return $"{Math.Max(1, selectedIndex + 1)} of {MusicBrowserResultItems.Count}";
		}
	}

	public bool IsBooting
	{
		get
		{
			return _isBooting;
		}
		set
		{
			if (SetProperty(ref _isBooting, value, "IsBooting") && !value)
			{
				SetTopRightState(0);
			}
		}
	}

	public DashboardTheme? SelectedTheme
	{
		get
		{
			return _selectedTheme;
		}
		set
		{
			SetProperty(ref _selectedTheme, value, "SelectedTheme");
		}
	}

	public string ThemeNameInput
	{
		get
		{
			return _themeNameInput;
		}
		set
		{
			SetProperty(ref _themeNameInput, value, "ThemeNameInput");
		}
	}

	public string ThemeHomePreviewPath
	{
		get
		{
			return _themeHomePreviewPath;
		}
		set
		{
			SetProperty(ref _themeHomePreviewPath, value, "ThemeHomePreviewPath");
		}
	}

	public string ThemeGamesPreviewPath
	{
		get
		{
			return _themeGamesPreviewPath;
		}
		set
		{
			SetProperty(ref _themeGamesPreviewPath, value, "ThemeGamesPreviewPath");
		}
	}

	public string ThemeSettingsPreviewPath
	{
		get
		{
			return _themeSettingsPreviewPath;
		}
		set
		{
			SetProperty(ref _themeSettingsPreviewPath, value, "ThemeSettingsPreviewPath");
		}
	}

	public string ThemeAppsPreviewPath
	{
		get
		{
			return _themeAppsPreviewPath;
		}
		set
		{
			SetProperty(ref _themeAppsPreviewPath, value, "ThemeAppsPreviewPath");
		}
	}

	public string SteamSetupApiKey
	{
		get
		{
			return _steamSetupApiKey;
		}
		set
		{
			SetProperty(ref _steamSetupApiKey, value?.Trim() ?? string.Empty, "SteamSetupApiKey");
		}
	}

	public string SteamSetupSteamId64
	{
		get
		{
			return _steamSetupSteamId64;
		}
		set
		{
			SetProperty(ref _steamSetupSteamId64, ExtractSteamId64(value ?? string.Empty), "SteamSetupSteamId64");
		}
	}

	public string SteamSetupStatus
	{
		get
		{
			return _steamSetupStatus;
		}
		set
		{
			SetProperty(ref _steamSetupStatus, value, "SteamSetupStatus");
		}
	}

	public string SpotifySetupClientId
	{
		get
		{
			return _spotifySetupClientId;
		}
		set
		{
			SetProperty(ref _spotifySetupClientId, value?.Trim() ?? string.Empty, "SpotifySetupClientId");
		}
	}

	public string SpotifySetupStatus
	{
		get
		{
			return _spotifySetupStatus;
		}
		set
		{
			SetProperty(ref _spotifySetupStatus, value, "SpotifySetupStatus");
		}
	}

	public string SpotifySetupRedirectUri => SpotifyRedirectUri;

	public string DashPartyLinkServiceUrl
	{
		get
		{
			return _dashPartyLinkServiceUrl;
		}
		set
		{
			SetProperty(ref _dashPartyLinkServiceUrl, value?.Trim() ?? string.Empty, "DashPartyLinkServiceUrl");
		}
	}

	public string DashPartyLinkFriendCode
	{
		get
		{
			return _dashPartyLinkFriendCode;
		}
		set
		{
			SetProperty(ref _dashPartyLinkFriendCode, value, "DashPartyLinkFriendCode");
		}
	}

	public string DashPartyLinkDeviceId
	{
		get
		{
			return _dashPartyLinkDeviceId;
		}
		set
		{
			SetProperty(ref _dashPartyLinkDeviceId, value, "DashPartyLinkDeviceId");
		}
	}

	public string DashPartyLinkSetupStatus
	{
		get
		{
			return _dashPartyLinkSetupStatus;
		}
		set
		{
			SetProperty(ref _dashPartyLinkSetupStatus, value, "DashPartyLinkSetupStatus");
		}
	}

	public string NotificationToastLine1
	{
		get
		{
			if (!string.IsNullOrWhiteSpace(_notificationToastLine1))
			{
				return _notificationToastLine1;
			}
			return Profile.Gamertag + " signed";
		}
		set
		{
			SetProperty(ref _notificationToastLine1, value ?? string.Empty, "NotificationToastLine1");
		}
	}

	public string NotificationToastLine2
	{
		get
		{
			if (!string.IsNullOrWhiteSpace(_notificationToastLine2))
			{
				return _notificationToastLine2;
			}
			return "in to Xbox LIVE";
		}
		set
		{
			SetProperty(ref _notificationToastLine2, value ?? string.Empty, "NotificationToastLine2");
		}
	}

	public string ThemeMenuVisibilityTitle => SelectedTheme?.Name ?? "Xbox 360";

	public string CurrentThemeBackgroundPath
	{
		get
		{
			if (IsBladesDashboardStyle)
			{
				return string.Empty;
			}
			string text = ResolveThemeSectionKey();
			if (SelectedTheme == null || SelectedTheme.IsBuiltIn || string.IsNullOrWhiteSpace(text))
			{
				return string.Empty;
			}
			string backgroundPath = SelectedTheme.GetBackgroundPath(text);
			if (!File.Exists(AppPaths.ResolvePath(backgroundPath)))
			{
				return string.Empty;
			}
			return backgroundPath;
		}
	}

	public string ClockText
	{
		get
		{
			return _clockText;
		}
		set
		{
			SetProperty(ref _clockText, value, "ClockText");
		}
	}

	public string ProfileClockText => DateTime.Now.ToString("H:mm");

	public int TopRightFriendCount
	{
		get
		{
			return _topRightFriendCount;
		}
		private set
		{
			if (SetProperty(ref _topRightFriendCount, value, "TopRightFriendCount"))
			{
				OnPropertyChanged("TopRightFriendCountText");
			}
		}
	}

	public string TopRightFriendCountText => Math.Max(0, TopRightFriendCount).ToString(CultureInfo.InvariantCulture);

	public string TopRightGamerscoreText => Math.Max(0, Profile?.Gamerscore ?? 0).ToString(CultureInfo.InvariantCulture);

	public bool ShowTopRightGamertag => _topRightState == 0;

	public bool ShowTopRightStats => _topRightState == 1;

	public MusicTrackViewModel? CurrentMusicTrack
	{
		get
		{
			return _currentMusicTrack;
		}
		set
		{
			if (!SetProperty(ref _currentMusicTrack, value, "CurrentMusicTrack"))
			{
				return;
			}
			foreach (MusicTrackViewModel musicTrack in MusicTracks)
			{
				musicTrack.IsPlaying = musicTrack == value;
			}
			OnPropertyChanged("CurrentMusicTitle");
			OnPropertyChanged("MusicTrackCountText");
		}
	}

	public MusicTrackViewModel? SelectedMusicTrack
	{
		get
		{
			return _selectedMusicTrack;
		}
		set
		{
			if (!SetProperty(ref _selectedMusicTrack, value, "SelectedMusicTrack"))
			{
				return;
			}
			foreach (MusicTrackViewModel musicTrack in MusicTracks)
			{
				musicTrack.IsSelected = musicTrack == value;
			}
			OnPropertyChanged("SelectedMusicTrackIndex");
			OnPropertyChanged("MusicTrackCountText");
		}
	}

	public int SelectedMusicTrackIndex => MusicTracks.IndexOf(SelectedMusicTrack);

	public string CurrentMusicTitle => CurrentMusicTrack?.Title ?? "No music found";

	public string MusicTrackCountText
	{
		get
		{
			if (_isSpotifyMusicMode && MusicTracks.Count == 0)
			{
				return "Spotify";
			}
			if (MusicTracks.Count != 0)
			{
				int selectedIndex = MusicTracks.IndexOf(SelectedMusicTrack);
				int displayIndex = selectedIndex >= 0 ? selectedIndex : _musicIndex;
				return $"{Math.Max(1, displayIndex + 1)} of {MusicTracks.Count}";
			}
			return "0 of 0";
		}
	}

	public string MusicPlayPauseText
	{
		get
		{
			if (!IsMusicPlaying)
			{
				return "Play";
			}
			return "Pause";
		}
	}

	public string ShuffleText
	{
		get
		{
			if (!IsShuffleEnabled)
			{
				return "Shuffle";
			}
			return "Shuffle On";
		}
	}

	public string MusicPositionText
	{
		get
		{
			return _musicPositionText;
		}
		set
		{
			SetProperty(ref _musicPositionText, value, "MusicPositionText");
		}
	}

	public string MusicDurationText
	{
		get
		{
			return _musicDurationText;
		}
		set
		{
			SetProperty(ref _musicDurationText, value, "MusicDurationText");
		}
	}

	public double MusicProgress
	{
		get
		{
			return _musicProgress;
		}
		set
		{
			SetProperty(ref _musicProgress, value, "MusicProgress");
		}
	}

	public double MusicVolume
	{
		get
		{
			return _musicVolume;
		}
		set
		{
			double num = Math.Clamp(value, 0.0, 1.0);
			if (SetProperty(ref _musicVolume, num, "MusicVolume"))
			{
				ApplyMusicPlayerVolume();
				OnPropertyChanged("MusicVolumeText");
			}
		}
	}

	public string MusicVolumeText => $"{Math.Round(MusicVolume * 100.0)}%";

	public double VisualizerBass
	{
		get
		{
			return _visualizerBass;
		}
		private set
		{
			SetProperty(ref _visualizerBass, value, "VisualizerBass");
		}
	}

	public double VisualizerMid
	{
		get
		{
			return _visualizerMid;
		}
		private set
		{
			SetProperty(ref _visualizerMid, value, "VisualizerMid");
		}
	}

	public double VisualizerTreble
	{
		get
		{
			return _visualizerTreble;
		}
		private set
		{
			SetProperty(ref _visualizerTreble, value, "VisualizerTreble");
		}
	}

	public double VisualizerLoudness
	{
		get
		{
			return _visualizerLoudness;
		}
		private set
		{
			SetProperty(ref _visualizerLoudness, value, "VisualizerLoudness");
		}
	}

	public double VisualizerPeak
	{
		get
		{
			return _visualizerPeak;
		}
		private set
		{
			SetProperty(ref _visualizerPeak, value, "VisualizerPeak");
		}
	}

	public string SpotlightTitle => FeaturedGame?.Title ?? "DashX360";

	public string SpotlightSubtitle => FeaturedGame?.Subtitle ?? "Press Y to search or E to move across the dashboard.";

	public GameCardViewModel? TrayGame
	{
		get
		{
			return _trayGame;
		}
		set
		{
			if (SetProperty(ref _trayGame, value, "TrayGame"))
			{
				OnPropertyChanged("OpenTrayTitle");
				OnPropertyChanged("OpenTrayCoverArtPath");
			}
		}
	}

	public string OpenTrayTitle => TrayGame?.Title ?? "Open Tray";

	public string OpenTrayCoverArtPath => TrayGame?.BackgroundArtPath ?? string.Empty;

	public string MyGamesCountText
	{
		get
		{
			List<GameCardViewModel> list = Games.Where((GameCardViewModel game) => !IsAppEntry(game.Game)).ToList();
			int count = list.Count;
			if (count == 0)
			{
				return "0 of 17";
			}
			int value = ((SelectedGame == null) ? 1 : Math.Max(1, list.IndexOf(SelectedGame) + 1));
			return $"{value} of {count}";
		}
	}

	public string LibraryMenuTitle
	{
		get
		{
			if (!_isLibraryShowingPins)
			{
				if (!_isLibraryShowingApps)
				{
					return "My Games";
				}
				return "My Apps";
			}
			return "My Pins";
		}
	}

	public string LibraryMenuFilterText
	{
		get
		{
			if (!_isLibraryShowingPins)
			{
				if (!_isLibraryShowingApps)
				{
					return "all games";
				}
				return "all apps";
			}
			return "pinned games";
		}
	}

	public bool IsLibraryShowingApps => _isLibraryShowingApps;

	public bool IsLibraryShowingGameCovers => !_isLibraryShowingApps;

	public string SelectedAppLibraryTileTitle => SelectedAppLibraryTile?.Title ?? string.Empty;

	public double AppLibraryCanvasWidth
	{
		get
		{
			if (AppLibraryTiles.Count == 0)
			{
				return 1070.0;
			}
			double right = AppLibraryTiles.Max(tile => tile.Left + tile.Width + 18.0);
			return Math.Max(1070.0, right);
		}
	}

	public AppLibraryTileViewModel? SelectedAppLibraryTile
	{
		get
		{
			return _selectedAppLibraryTile;
		}
		set
		{
			if (_selectedAppLibraryTile != value)
			{
				if (_selectedAppLibraryTile != null)
				{
					_selectedAppLibraryTile.IsSelected = false;
				}
				_selectedAppLibraryTile = value;
				if (_selectedAppLibraryTile != null)
				{
					_selectedAppLibraryTile.IsSelected = true;
					StatusMessage = _selectedAppLibraryTile.Title;
				}
				OnPropertyChanged("SelectedAppLibraryTile");
				OnPropertyChanged("SelectedAppLibraryTileTitle");
				OnPropertyChanged("LibraryMenuSelectedTitle");
				OnPropertyChanged("LibraryMenuCountText");
			}
		}
	}

	public string LibraryMenuSelectedTitle => _isLibraryShowingApps ? SelectedAppLibraryTileTitle : (SelectedGame?.Title ?? string.Empty);

	public string LibraryMenuXHintText => _isLibraryShowingApps ? string.Empty : " Game Details";

	public bool HasLibraryMenuXHint => !_isLibraryShowingApps;

	public string LibraryMenuYHintText => " Pin";

	public IEnumerable<GameCardViewModel> LibraryMenuGames => GetLibraryMenuGames();

	public string LibraryMenuCountText
	{
		get
		{
			if (_isLibraryShowingApps)
			{
				int appCount = AppLibraryTiles.Count;
				if (appCount == 0)
				{
					return "0 of 0";
				}
				int selectedIndex = (SelectedAppLibraryTile == null) ? 0 : AppLibraryTiles.IndexOf(SelectedAppLibraryTile);
				return $"{Math.Max(1, selectedIndex + 1)} of {appCount}";
			}
			List<GameCardViewModel> list = LibraryMenuGames.ToList();
			if (list.Count == 0)
			{
				if (!_isLibraryShowingPins && !_isLibraryShowingApps)
				{
					return "0 of 17";
				}
				return "0 of 0";
			}
			int num = ((SelectedGame == null) ? 1 : (list.IndexOf(SelectedGame) + 1));
			if (num <= 0)
			{
				num = 1;
			}
			return $"{num} of {list.Count}";
		}
	}

	public bool HasRunningLaunchedGame => _runningGameService.HasRunningGame;

	public string RunningLaunchedGameTitle => _runningGameService.RunningGameTitle;

	public GameMetadata? RunningLaunchedGame => _runningGameService.CurrentGame;

	public string RunningGameFooterActionText => _runningGameService.State switch
	{
		RunningGameState.Launching => "Finding Game...", 
		RunningGameState.None => "No Game Running", 
		_ => "Close Game", 
	};

	public bool IsAudioAnalysisRunning => _audioAnalysisService.IsRunning;

	public bool IsMusicProgressTimerActive => _musicTimer.IsEnabled;

	public ICommand SelectGameCommand { get; }

	public ICommand LaunchGameCommand { get; }

	public ICommand LaunchAppLibraryTileCommand { get; }

	public ICommand SubmitSearchCommand { get; }

	public ICommand UseTrendingSearchCommand { get; }

	public ICommand OpenSearchCommand { get; }

	public ICommand OpenBladesMenuItemCommand { get; }

	public ICommand SwitchBladesBladeCommand { get; }

	public ICommand CloseSearchCommand { get; }

	public ICommand ShowDetailsCommand { get; }

	public ICommand CloseDetailsCommand { get; }

	public ICommand SelectGameDetailsTabCommand { get; }

	public ICommand OpenGameDetailsExtrasSeeAllCommand { get; }

	public ICommand OpenGameDetailsExtraCommand { get; }

	public ICommand BackCommand { get; }

	public ICommand AddGameCommand { get; }

	public ICommand EditSelectedGameCommand { get; }

	public ICommand ScanFolderCommand { get; }

	public ICommand ToggleFavoriteCommand { get; }

	public ICommand OpenSelectedGameStoreCommand { get; }

	public ICommand SaveSettingsCommand { get; }

	public ICommand SaveDashPartyLinkSetupCommand { get; }

	public ICommand ClearDashPartyLinkSetupCommand { get; }

	public ICommand TestDashPartyLinkSetupCommand { get; }

	public ICommand ExportDataCommand { get; }

	public ICommand ImportDataCommand { get; }

	public ICommand ScanSteamGamesCommand { get; }

	public ICommand OpenSteamSetupCommand { get; }

	public ICommand CloseSteamSetupCommand { get; }

	public ICommand OpenSpotifySetupCommand { get; }

	public ICommand CloseSpotifySetupCommand { get; }

	public ICommand SaveSpotifySetupCommand { get; }

	public ICommand ConnectSpotifyCommand { get; }

	public ICommand DisconnectSpotifyCommand { get; }

	public ICommand PasteSpotifyClientIdCommand { get; }

	public ICommand OpenSpotifyDeveloperDashboardCommand { get; }

	public ICommand SaveSteamSetupCommand { get; }

	public ICommand TestSteamSetupCommand { get; }

	public ICommand PasteSteamApiKeyCommand { get; }

	public ICommand PasteSteamIdCommand { get; }

	public ICommand OpenSteamApiKeyPageCommand { get; }

	public ICommand OpenSteamProfileHelpCommand { get; }

	public ICommand OpenThemeMenuCommand { get; }

	public ICommand CloseThemeMenuCommand { get; }

	public ICommand SelectThemeCommand { get; }

	public ICommand OpenThemeCreatorCommand { get; }

	public ICommand CloseThemeCreatorCommand { get; }

	public ICommand OpenDashboardCustomizerCommand { get; }

	public ICommand CloseDashboardCustomizerCommand { get; }

	public ICommand PreviousDashboardCustomizerTabCommand { get; }

	public ICommand NextDashboardCustomizerTabCommand { get; }

	public ICommand SelectDashboardTileCommand { get; }

	public ICommand ChooseDashboardTileImageCommand { get; }

	public ICommand ChooseDashboardTileExecutableCommand { get; }

	public ICommand ResetDashboardTileImageCommand { get; }

	public ICommand ResetSelectedCoverTransformCommand { get; }

	public ICommand ResetDashboardTileTitleCommand { get; }

	public ICommand ResetDashboardTabImagesCommand { get; }

	public ICommand OpenDashboardTileColorPickerCommand { get; }

	public ICommand ResetDashboardTileColorCommand { get; }

	public ICommand LaunchDashboardTileCommand { get; }

	public ICommand ChooseThemeHomeImageCommand { get; }

	public ICommand ChooseThemeGamesImageCommand { get; }

	public ICommand ChooseThemeSettingsImageCommand { get; }

	public ICommand ChooseThemeAppsImageCommand { get; }

	public ICommand SaveThemeCommand { get; }

	public ICommand ToggleQuickMenuCommand { get; }

	public ICommand OpenMyGamesCommand { get; }

	public ICommand OpenMyAppsCommand { get; }

	public ICommand OpenMyPinsCommand { get; }

	public ICommand CloseMyGamesCommand { get; }

	public ICommand OpenLauncherSettingsCommand { get; }

	public ICommand CloseLauncherSettingsCommand { get; }

	public ICommand ChooseSelectedHomeImageCommand { get; }

	public ICommand ChooseSelectedGameMenuImageCommand { get; }

	public ICommand SaveSelectedGameCommand { get; }

	public ICommand SetOpenTrayGameCommand { get; }

	public ICommand RemoveSelectedGameCommand { get; }

	public ICommand OpenProfileEditorCommand { get; }

	public ICommand CloseProfileEditorCommand { get; }

	public ICommand OpenMusicPlayerCommand { get; }

	public ICommand OpenMusicPlayerFromGuideCommand { get; }

	public ICommand OpenMusicNowPlayingCommand { get; }

	public ICommand OpenMusicNowPlayingFromGuideCommand { get; }

	public ICommand CloseMusicPlayerCommand { get; }

	public ICommand OpenMusicFolderCommand { get; }

	public ICommand OpenMusicVisualizerFullscreenCommand { get; }

	public ICommand PlayPauseMusicCommand { get; }

	public ICommand StopMusicCommand { get; }

	public ICommand NextMusicCommand { get; }

	public ICommand PreviousMusicCommand { get; }

	public ICommand ToggleShuffleMusicCommand { get; }

	public ICommand VolumeDownCommand { get; }

	public ICommand VolumeUpCommand { get; }

	public ICommand PlaySelectedMusicCommand { get; }

	public ICommand SelectMusicBrowserMenuItemCommand { get; }

	public ICommand OpenMusicBrowserResultCommand { get; }

	public ICommand ChooseProfilePictureCommand { get; }

	public ICommand SaveProfileCommand { get; }

	public ICommand ToggleProfileEditCommand { get; }

	public ICommand ShutdownCommand { get; }

	public ICommand OpenYouTubeCommand { get; }

	public ICommand CloseYouTubeTvCommand { get; }

	public ICommand OpenFriendsOverlayCommand { get; }

	public ICommand SwitchTabCommand { get; }

	public event EventHandler? FriendsOverlayRequested;
	public event EventHandler? BladesMusicRequested;

	public event EventHandler? PartyOverlayRequested;

	public event EventHandler<DashboardAchievementOpenRequest>? AchievementOverlayRequested;

	public event EventHandler<DashboardToastRequest>? ToastRequested;

	[DllImport("user32.dll")]
	[return: MarshalAs(UnmanagedType.Bool)]
	private static extern bool SetForegroundWindow(nint hWnd);

	[DllImport("user32.dll")]
	[return: MarshalAs(UnmanagedType.Bool)]
	private static extern bool ShowWindow(nint hWnd, int nCmdShow);

	[DllImport("user32.dll")]
	private static extern void keybd_event(byte bVk, byte bScan, int dwFlags, UIntPtr dwExtraInfo);

	public DashboardViewModel(IGameLibraryService libraryService, IGameLaunchService launchService, ISearchService searchService, ISettingsService settingsService, IProfileService profileService, IFilePickerService filePickerService, IImportExportService importExportService, ISteamLibraryScannerService steamLibraryScannerService, ISteamCommunityService steamCommunityService, IThemeService themeService, IStartupRegistrationService startupRegistrationService, IAudioService audioService, SocialIntegrationManager socialIntegrationManager, IRunningGameService runningGameService)
	{
		//IL_02df: Unknown result type (might be due to invalid IL or missing references)
		//IL_02e4: Unknown result type (might be due to invalid IL or missing references)
		//IL_02fd: Expected O, but got Unknown
		_libraryService = libraryService;
		_launchService = launchService;
		_searchService = searchService;
		_settingsService = settingsService;
		_profileService = profileService;
		_filePickerService = filePickerService;
		_importExportService = importExportService;
		_steamLibraryScannerService = steamLibraryScannerService;
		_steamCommunityService = steamCommunityService;
		_themeService = themeService;
		_startupRegistrationService = startupRegistrationService;
		_audioService = audioService;
		_socialIntegrationManager = socialIntegrationManager;
		_runningGameService = runningGameService;
		ApplyMusicPlayerVolume();
		_musicPlayer.MediaOpened += delegate
		{
			RefreshMusicProgress();
		};
		_musicPlayer.MediaEnded += delegate
		{
			NextMusicTrack();
		};
		_audioAnalysisService.FrameReady += AudioAnalysis_OnFrameReady;
		_musicTimer = new DispatcherTimer
		{
			Interval = TimeSpan.FromMilliseconds(500.0)
		};
		_musicTimer.Tick += delegate
		{
			RefreshMusicProgress();
		};
		_dashPartyInviteTimer = new DispatcherTimer
		{
			Interval = TimeSpan.FromSeconds(1.5)
		};
		_dashPartyInviteTimer.Tick += async delegate
		{
			await PollDashPartyInvitesAsync();
		};
		_dashPartyInviteTimer.Start();
		_profileSaveTimer = new DispatcherTimer
		{
			Interval = TimeSpan.FromMilliseconds(650.0)
		};
		_profileSaveTimer.Tick += async delegate
		{
			_profileSaveTimer.Stop();
			await SaveProfileSilentlyAsync();
		};
		int num = 6;
		List<System.Windows.Media.Brush> list = new List<System.Windows.Media.Brush>(num);
		CollectionsMarshal.SetCount(list, num);
		Span<System.Windows.Media.Brush> span = CollectionsMarshal.AsSpan(list);
		int num2 = 0;
		span[num2] = new SolidColorBrush(System.Windows.Media.Color.FromRgb(20, 156, 74));
		num2++;
		span[num2] = new SolidColorBrush(System.Windows.Media.Color.FromRgb(0, 122, 204));
		num2++;
		span[num2] = new SolidColorBrush(System.Windows.Media.Color.FromRgb(202, 80, 16));
		num2++;
		span[num2] = new SolidColorBrush(System.Windows.Media.Color.FromRgb(116, 77, 169));
		num2++;
		span[num2] = new SolidColorBrush(System.Windows.Media.Color.FromRgb(36, 161, 156));
		num2++;
		span[num2] = new SolidColorBrush(System.Windows.Media.Color.FromRgb(190, 40, 71));
		num2++;
		_accentBrushes = list;
		Tabs = new ObservableCollection<DashboardTabViewModel>
		{
			new BingTabViewModel(this),
			new HomeTabViewModel(this),
			new SocialTabViewModel(this),
			new MediaTabViewModel(this),
			new GamesTabViewModel(this),
			new MusicTabViewModel(this),
			new AppsTabViewModel(this),
			new SettingsTabViewModel(this)
		};
		InitializeAppLibraryTiles();
		foreach (DashboardTabCustomizationViewModel item in BuildDashboardCustomizationTabs())
		{
			DashboardCustomizationTabs.Add(item);
		}
		Games.CollectionChanged += OnGamesChanged;
		_runningGameService.StateChanged += RunningGameService_OnStateChanged;
		GameDetailsTabs = new ObservableCollection<GameDetailsTabViewModel>
		{
			new GameDetailsTabViewModel("overview", "overview"),
			new GameDetailsTabViewModel("details", "details"),
			new GameDetailsTabViewModel("extras", "extras"),
			new GameDetailsTabViewModel("gallery", "gallery")
		};
		GameDetailsTabs[0].IsSelected = true;
		SelectGameCommand = new RelayCommand(delegate(object? parameter)
		{
			SelectGame(parameter as GameCardViewModel);
		});
		LaunchGameCommand = new AsyncRelayCommand((object? parameter) => LaunchGameAsync(parameter as GameCardViewModel));
		LaunchAppLibraryTileCommand = new RelayCommand(delegate(object? parameter)
		{
			AppLibraryTileViewModel? appLibraryTileViewModel = parameter as AppLibraryTileViewModel;
			SelectedAppLibraryTile = appLibraryTileViewModel;
			if (appLibraryTileViewModel == null)
			{
				return;
			}
			if (appLibraryTileViewModel.Game != null)
			{
				_ = LaunchGameAsync(appLibraryTileViewModel.Game);
				return;
			}
			if (string.Equals(appLibraryTileViewModel.Title, "YouTube", StringComparison.OrdinalIgnoreCase))
			{
				OpenYouTube();
				return;
			}
			if (string.Equals(appLibraryTileViewModel.Title, "System Music Player", StringComparison.OrdinalIgnoreCase))
			{
				OpenMusicPlayer();
				return;
			}
			if (string.Equals(appLibraryTileViewModel.Title, "Internet Explorer", StringComparison.OrdinalIgnoreCase))
			{
				OpenDefaultBrowser();
				return;
			}
			if (string.Equals(appLibraryTileViewModel.Title, "Windows Media Center", StringComparison.OrdinalIgnoreCase))
			{
				OpenWindowsMediaPlayerLegacy();
			}
		});
		SubmitSearchCommand = new AsyncRelayCommand(SubmitSearchAsync);
		UseTrendingSearchCommand = new RelayCommand(delegate(object? parameter)
		{
			SearchQuery = parameter?.ToString() ?? string.Empty;
			SubmitSearchAsync();
		});
		OpenSearchCommand = new RelayCommand(OpenSearch);
		CloseSearchCommand = new RelayCommand((Action)(() => CloseSearchOverlay(playSound: true)));
		ShowDetailsCommand = new RelayCommand(OpenGameDetails);
		CloseDetailsCommand = new RelayCommand((Action)(() => CloseDetails(playSound: true)));
		SelectGameDetailsTabCommand = new RelayCommand(delegate(object? parameter)
		{
			SelectGameDetailsTab(parameter?.ToString() ?? string.Empty);
		});
		OpenGameDetailsExtrasSeeAllCommand = new RelayCommand(delegate
		{
			OpenGameDetailsSeeAll();
		}, null);
		OpenGameDetailsExtraCommand = new RelayCommand(OpenGameDetailsExtra);
		BackCommand = new RelayCommand(GoBack);
		AddGameCommand = new AsyncRelayCommand(AddGameAsync);
		EditSelectedGameCommand = new AsyncRelayCommand(EditSelectedGameAsync);
		ScanFolderCommand = new AsyncRelayCommand(ScanFolderAsync);
		ToggleFavoriteCommand = new AsyncRelayCommand(ToggleFavoriteAsync, (object? _) => SelectedGame != null);
		OpenSelectedGameStoreCommand = new RelayCommand(OpenSelectedGameStore);
		SaveSettingsCommand = new AsyncRelayCommand(SaveSettingsAsync);
		SaveDashPartyLinkSetupCommand = new AsyncRelayCommand(SaveDashPartyLinkSetupAsync);
		ClearDashPartyLinkSetupCommand = new AsyncRelayCommand(ClearDashPartyLinkSetupAsync);
		TestDashPartyLinkSetupCommand = new AsyncRelayCommand(TestDashPartyLinkSetupAsync);
		ExportDataCommand = new AsyncRelayCommand(ExportDataAsync);
		ImportDataCommand = new AsyncRelayCommand(ImportDataAsync);
		ScanSteamGamesCommand = new AsyncRelayCommand(ScanSteamGamesAsync);
		OpenSteamSetupCommand = new AsyncRelayCommand(OpenSteamSetupAsync);
		CloseSteamSetupCommand = new RelayCommand((Action)delegate
		{
			IsSteamSetupOpen = false;
		});
		OpenSpotifySetupCommand = new RelayCommand(OpenSpotifySetup);
		CloseSpotifySetupCommand = new RelayCommand((Action)delegate
		{
			IsSpotifySetupOpen = false;
		});
		SaveSpotifySetupCommand = new AsyncRelayCommand(SaveSpotifySetupAsync);
		ConnectSpotifyCommand = new AsyncRelayCommand(ConnectSpotifyAsync);
		DisconnectSpotifyCommand = new AsyncRelayCommand(DisconnectSpotifyAsync);
		PasteSpotifyClientIdCommand = new RelayCommand((Action)delegate
		{
			SpotifySetupClientId = GetClipboardText();
		});
		OpenSpotifyDeveloperDashboardCommand = new RelayCommand((Action)delegate
		{
			OpenExternalUrl("https://developer.spotify.com/dashboard", "Opening Spotify Developer Dashboard");
		});
		SaveSteamSetupCommand = new AsyncRelayCommand(SaveSteamSetupAsync);
		TestSteamSetupCommand = new AsyncRelayCommand(TestSteamSetupAsync);
		PasteSteamApiKeyCommand = new RelayCommand((Action)delegate
		{
			SteamSetupApiKey = GetClipboardText();
		});
		PasteSteamIdCommand = new RelayCommand((Action)delegate
		{
			SteamSetupSteamId64 = ExtractSteamId64(GetClipboardText());
		});
		OpenSteamApiKeyPageCommand = new RelayCommand((Action)delegate
		{
			OpenExternalUrl("https://steamcommunity.com/dev/apikey", "Opening Steam API key page");
		});
		OpenSteamProfileHelpCommand = new RelayCommand((Action)delegate
		{
			OpenExternalUrl("https://steamid.io/lookup", "Opening SteamID lookup");
		});
		OpenThemeMenuCommand = new RelayCommand(OpenThemeMenu);
		OpenBladesMenuItemCommand = new RelayCommand(delegate(object? parameter)
		{
			if (parameter is BladesMenuItemViewModel bladesMenuItemViewModel)
			{
				OpenBladesMenuItem(bladesMenuItemViewModel.Key);
				return;
			}
			OpenBladesMenuItem(parameter as string);
		});
		SwitchBladesBladeCommand = new RelayCommand(delegate(object? parameter)
		{
			if (parameter is BladesBladeViewModel bladesBladeViewModel)
			{
				SelectBladesBlade(bladesBladeViewModel.Key);
				return;
			}
			SelectBladesBlade(parameter as string);
		});
		CloseThemeMenuCommand = new RelayCommand((Action)delegate
		{
			IsThemeMenuOpen = false;
		});
		SelectThemeCommand = new AsyncRelayCommand(SelectThemeAsync);
		OpenThemeCreatorCommand = new RelayCommand(OpenThemeCreator);
		CloseThemeCreatorCommand = new RelayCommand(CloseThemeCreator);
		OpenDashboardCustomizerCommand = new RelayCommand(OpenDashboardCustomizer);
		CloseDashboardCustomizerCommand = new RelayCommand(CloseDashboardCustomizer);
		PreviousDashboardCustomizerTabCommand = new RelayCommand(delegate
		{
			MoveDashboardCustomizerTab(-1);
		}, null);
		NextDashboardCustomizerTabCommand = new RelayCommand(delegate
		{
			MoveDashboardCustomizerTab(1);
		}, null);
		SelectDashboardTileCommand = new RelayCommand(delegate(object? parameter)
		{
			SelectDashboardTile(parameter as DashboardTileCustomizationViewModel);
		});
		ChooseDashboardTileImageCommand = new AsyncRelayCommand(ChooseDashboardTileImageAsync);
		ChooseDashboardTileExecutableCommand = new AsyncRelayCommand(ChooseDashboardTileExecutableAsync);
		ResetDashboardTileImageCommand = new AsyncRelayCommand(ResetDashboardTileImageAsync);
		ResetSelectedCoverTransformCommand = new AsyncRelayCommand(ResetSelectedCoverTransformAsync);
		ResetDashboardTileTitleCommand = new AsyncRelayCommand(ResetDashboardTileTitleAsync);
		ResetDashboardTabImagesCommand = new AsyncRelayCommand(ResetDashboardTabImagesAsync);
		OpenDashboardTileColorPickerCommand = new RelayCommand(OpenDashboardTileColorPicker);
		ResetDashboardTileColorCommand = new AsyncRelayCommand(ResetDashboardTileColorAsync);
		ChooseThemeHomeImageCommand = new AsyncRelayCommand((object? _) => ChooseThemeSectionImageAsync("home"));
		ChooseThemeGamesImageCommand = new AsyncRelayCommand((object? _) => ChooseThemeSectionImageAsync("games"));
		ChooseThemeSettingsImageCommand = new AsyncRelayCommand((object? _) => ChooseThemeSectionImageAsync("settings"));
		ChooseThemeAppsImageCommand = new AsyncRelayCommand((object? _) => ChooseThemeSectionImageAsync("apps"));
		SaveThemeCommand = new AsyncRelayCommand(SaveThemeAsync);
		ToggleQuickMenuCommand = new RelayCommand(ToggleQuickMenu);
		LaunchDashboardTileCommand = new RelayCommand(delegate(object? parameter)
		{
			LaunchDashboardTile(parameter as string);
		});
		OpenMyGamesCommand = new RelayCommand(delegate(object? parameter)
		{
			if (parameter is string key && TryLaunchDashboardTileOverride(key))
			{
				return;
			}
			OpenMyGames();
		});
		OpenMyAppsCommand = new RelayCommand(delegate(object? parameter)
		{
			if (parameter is string key && TryLaunchDashboardTileOverride(key))
			{
				return;
			}
			OpenMyApps();
		});
		OpenMyPinsCommand = new RelayCommand(OpenMyPins);
		CloseMyGamesCommand = new RelayCommand((Action)(() => CloseLibraryMenu(playSound: true)));
		OpenLauncherSettingsCommand = new RelayCommand(OpenLauncherSettings);
		CloseLauncherSettingsCommand = new RelayCommand((Action)(() => CloseLauncherSettings(playSound: true)));
		ChooseSelectedHomeImageCommand = new AsyncRelayCommand(ChooseSelectedHomeImageAsync);
		ChooseSelectedGameMenuImageCommand = new AsyncRelayCommand(ChooseSelectedGameMenuImageAsync);
		SaveSelectedGameCommand = new AsyncRelayCommand(SaveSelectedGameAsync);
		SetOpenTrayGameCommand = new AsyncRelayCommand(SetOpenTrayGameAsync);
		RemoveSelectedGameCommand = new AsyncRelayCommand(RemoveSelectedGameAsync);
		OpenProfileEditorCommand = new RelayCommand(OpenProfileEditor);
		CloseProfileEditorCommand = new RelayCommand((Action)(() => CloseProfileEditor(playSound: true)));
		OpenMusicPlayerCommand = new RelayCommand(delegate(object? parameter)
		{
			if (parameter is string key && TryLaunchDashboardTileOverride(key))
			{
				return;
			}
			bool flag = default(bool);
			int num3;
			if (parameter is bool)
			{
				flag = (bool)parameter;
				num3 = 1;
			}
			else
			{
				num3 = 0;
			}
			OpenMusicPlayer((byte)((uint)num3 & (flag ? 1u : 0u)) != 0);
		});
		OpenMusicPlayerFromGuideCommand = new RelayCommand(delegate
		{
			OpenMusicPlayer(transparent: false, playSound: false);
		});
		OpenMusicNowPlayingCommand = new RelayCommand(delegate(object? parameter)
		{
			bool flag = default(bool);
			int num3;
			if (parameter is bool)
			{
				flag = (bool)parameter;
				num3 = 1;
			}
			else
			{
				num3 = 0;
			}
			OpenMusicNowPlaying((byte)((uint)num3 & (flag ? 1u : 0u)) != 0);
		});
		OpenMusicNowPlayingFromGuideCommand = new RelayCommand(delegate
		{
			OpenMusicNowPlaying(transparent: false, closeOnBack: true);
		});
		CloseMusicPlayerCommand = new RelayCommand(CloseMusicPlayer);
		OpenMusicFolderCommand = new RelayCommand(OpenMusicFolder);
		OpenMusicVisualizerFullscreenCommand = new RelayCommand(OpenMusicVisualizerFullscreen);
		PlayPauseMusicCommand = new RelayCommand(ToggleMusicPlayback);
		StopMusicCommand = new RelayCommand(StopMusic);
		NextMusicCommand = new RelayCommand(NextMusicTrack);
		PreviousMusicCommand = new RelayCommand(PreviousMusicTrack);
		ToggleShuffleMusicCommand = new RelayCommand((Action)delegate
		{
			IsShuffleEnabled = !IsShuffleEnabled;
		});
		VolumeDownCommand = new RelayCommand((Action)delegate
		{
			MusicVolume -= 0.05;
		});
		VolumeUpCommand = new RelayCommand((Action)delegate
		{
			MusicVolume += 0.05;
		});
		PlaySelectedMusicCommand = new RelayCommand(delegate(object? parameter)
		{
			PlayMusicTrack(parameter as MusicTrackViewModel);
		});
		SelectMusicBrowserMenuItemCommand = new RelayCommand(delegate(object? parameter)
		{
			SelectMusicBrowserMenuItem(parameter as MusicBrowserMenuItemViewModel);
		});
		OpenMusicBrowserResultCommand = new RelayCommand(delegate(object? parameter)
		{
			OpenMusicBrowserResult(parameter as MusicBrowserResultItemViewModel);
		});
		ChooseProfilePictureCommand = new AsyncRelayCommand(ChooseProfilePictureAsync);
		SaveProfileCommand = new AsyncRelayCommand(SaveProfileAsync);
		ToggleProfileEditCommand = new AsyncRelayCommand(ToggleProfileEditAsync);
		ShutdownCommand = new AsyncRelayCommand(ShutdownAsync);
		OpenYouTubeCommand = new RelayCommand(OpenYouTube);
		CloseYouTubeTvCommand = new RelayCommand(CloseYouTubeTv);
		OpenFriendsOverlayCommand = new RelayCommand(RequestFriendsOverlay);
		SwitchTabCommand = new RelayCommand(delegate(object? parameter)
		{
			if (parameter is DashboardTabViewModel currentTab)
			{
				CurrentTab = currentTab;
			}
		});
		_suppressNextTabSound = true;
		CurrentTab = Tabs[1];
		UpdateClock();
	}

	public async Task InitializeAsync()
	{
		await InitializeAsync(reloadSettings: true);
	}

	public async Task InitializeAsync(bool reloadSettings)
	{
		await ReloadSavedDataAsync(reloadSettings);
		await RefreshDashPartyLinkSetupAsync();
		_ = RefreshTopRightFriendCountAsync();
		await _settingsService.SaveAsync(Settings);
	}

	public async Task LoadStartupSettingsAsync()
	{
		Settings = await _settingsService.LoadAsync();
	}

	private async Task ReloadSavedDataAsync(bool reloadSettings)
	{
		if (reloadSettings)
		{
			Settings = await _settingsService.LoadAsync();
		}
		await LoadThemesAsync();
		_suppressProfileAutosave = true;
		Profile = await _profileService.LoadAsync();
		EnsureProfileDefaults();
		_suppressProfileAutosave = false;
		await SaveProfileSilentlyAsync();
		Settings.ThemeName = NormalizeThemeName(Settings.ThemeName);
		ApplySelectedTheme(Settings.ThemeName);
		Settings.SocialIntegrationMode = SocialIntegrationMode.LocalOnly;
		Settings.DiscordUserId = string.Empty;
		Settings.DiscordDisplayName = string.Empty;
		Settings.DiscordAvatarPathOrUrl = string.Empty;
		Settings.DiscordAccessTokenEncrypted = string.Empty;
		Settings.DiscordGrantedScopes = string.Empty;
		Settings.DiscordTokenType = string.Empty;
		_library = await _libraryService.LoadAsync();
		foreach (GameMetadata game in _library.Games)
		{
			GameMetadata gameMetadata = game;
			if (gameMetadata.Title == null)
			{
				gameMetadata.Title = string.Empty;
			}
			gameMetadata = game;
			if (gameMetadata.Platform == null)
			{
				gameMetadata.Platform = string.Empty;
			}
			gameMetadata = game;
			if (gameMetadata.Genre == null)
			{
				gameMetadata.Genre = string.Empty;
			}
			gameMetadata = game;
			if (gameMetadata.MultiplayerInfo == null)
			{
				gameMetadata.MultiplayerInfo = string.Empty;
			}
			gameMetadata = game;
			if (gameMetadata.CoOpInfo == null)
			{
				gameMetadata.CoOpInfo = string.Empty;
			}
			gameMetadata = game;
			if (gameMetadata.ExecutablePath == null)
			{
				gameMetadata.ExecutablePath = string.Empty;
			}
			gameMetadata = game;
			if (gameMetadata.Arguments == null)
			{
				gameMetadata.Arguments = string.Empty;
			}
			gameMetadata = game;
			if (gameMetadata.WorkingDirectory == null)
			{
				gameMetadata.WorkingDirectory = string.Empty;
			}
			gameMetadata = game;
			if (gameMetadata.CoverArtPath == null)
			{
				gameMetadata.CoverArtPath = string.Empty;
			}
			gameMetadata = game;
			if (gameMetadata.BackgroundArtPath == null)
			{
				gameMetadata.BackgroundArtPath = string.Empty;
			}
			game.LaunchType = (string.IsNullOrWhiteSpace(game.LaunchType) ? "Exe" : game.LaunchType);
			gameMetadata = game;
			if (gameMetadata.SteamAppId == null)
			{
				gameMetadata.SteamAppId = string.Empty;
			}
			gameMetadata = game;
			if (gameMetadata.InstallPath == null)
			{
				gameMetadata.InstallPath = string.Empty;
			}
			gameMetadata = game;
			if (gameMetadata.LaunchCommand == null)
			{
				gameMetadata.LaunchCommand = string.Empty;
			}
		}
		_library.Games = _library.Games.OrderBy<GameMetadata, string>((GameMetadata game) => game.Title, StringComparer.CurrentCultureIgnoreCase).ToList();
		SyncGamesCollectionFromLibrary();
		SelectedGame = Games.FirstOrDefault((GameCardViewModel game) => game.Game.IsFavorite) ?? Games.FirstOrDefault();
		FeaturedGame = SelectedGame;
		TrayGame = Games.FirstOrDefault((GameCardViewModel game) => string.Equals(game.Game.Id, Settings.OpenTrayGameId, StringComparison.OrdinalIgnoreCase));
		RefreshDerivedLists();
		LoadMusicLibrary();
		ResetPendingThemeDraft();
	}

	public void UpdateClock()
	{
		ClockText = DateTime.Now.ToString("h:mm tt  ddd, MMM d");
		OnPropertyChanged("ProfileClockText");
		if (!IsBooting)
		{
			SetTopRightState(_topRightState + 1);
		}
	}

	private void SetTopRightState(int state)
	{
		int normalizedState = ((state % 3) + 3) % 3;
		if (_topRightState == normalizedState)
		{
			return;
		}
		_topRightState = normalizedState;
		OnPropertyChanged("ShowTopRightGamertag");
		OnPropertyChanged("ShowTopRightStats");
	}

	private async Task RefreshTopRightFriendCountAsync()
	{
		try
		{
			SocialFriendsLoadResult socialFriendsLoadResult = await _socialIntegrationManager.LoadFriendsAsync(Settings.SocialIntegrationMode, Settings.DiscordConnectionState, Profile).ConfigureAwait(continueOnCapturedContext: true);
			TopRightFriendCount = socialFriendsLoadResult.Friends.Count;
		}
		catch (Exception exception)
		{
			App.LogException(exception, "DashboardViewModel.RefreshTopRightFriendCountAsync");
		}
	}

	public void HandleInput(DashboardInputAction action)
	{
		if (IsBladesDashboardStyle && !IsOverlayOpenForBladesInput())
		{
			if (HandleBladesInput(action))
			{
				return;
			}
		}
		switch (action)
		{
		case DashboardInputAction.PreviousTab:
			if (IsDetailsOpen)
			{
				MoveGameDetailsTab(-1);
			}
			else
			{
				MoveTab(-1);
			}
			break;
		case DashboardInputAction.NextTab:
			if (IsDetailsOpen)
			{
				MoveGameDetailsTab(1);
			}
			else
			{
				MoveTab(1);
			}
			break;
		case DashboardInputAction.Back:
			GoBack();
			break;
		case DashboardInputAction.Details:
			if (IsMusicPlayerOpen)
			{
				OpenMusicVisualizerFullscreen();
			}
			else if (IsMyGamesOpen && !_isLibraryShowingApps)
			{
				OpenGameDetails();
			}
			else if (!IsMyGamesOpen)
			{
				OpenGameDetails();
			}
			break;
		case DashboardInputAction.Search:
			if (IsDetailsOpen)
			{
				SetOpenTrayGameAsync(null);
			}
			else if (IsMyGamesOpen)
			{
				ToggleFavoriteAsync(null);
			}
			else
			{
				OpenSearch();
			}
			break;
		case DashboardInputAction.Options:
			ToggleQuickMenu();
			break;
		default:
			PlayFocusSound();
			break;
		case DashboardInputAction.Activate:
			break;
		}
	}

	public void MoveTab(int delta)
	{
		if (CurrentTab == null)
		{
			CurrentTab = Tabs[1];
			return;
		}
		int num = Tabs.IndexOf(CurrentTab);
		int num2 = Math.Clamp(num + delta, 0, Tabs.Count - 1);
		if (num2 != num)
		{
			_pendingTabSound = GetTabSwitchSoundName(num, num2, delta);
			CurrentTab = Tabs[num2];
		}
	}

	private static string GetTabSwitchSoundName(int currentIndex, int nextIndex, int delta)
	{
		int transitionIndex = Math.Min(currentIndex, nextIndex);
		return transitionIndex is >= 0 and <= 6
			? "tab-switch-" + transitionIndex.ToString(CultureInfo.InvariantCulture)
			: (delta < 0) ? "page-left" : "page-right";
	}

	public void MoveGameDetailsTab(int delta)
	{
		if (IsDetailsOpen && GameDetailsTabs.Count != 0)
		{
			if (IsGameDetailsSeeAllOpen)
			{
				IsGameDetailsSeeAllOpen = false;
			}
			int num = Math.Clamp(_selectedGameDetailsTabIndex + delta, 0, GameDetailsTabs.Count - 1);
			if (num != _selectedGameDetailsTabIndex)
			{
				SetGameDetailsTab(num);
				_audioService.Play((delta < 0) ? "page-left" : "page-right");
			}
		}
	}

	private void OpenGameDetails()
	{
		if (SelectedGame != null)
		{
			SetGameDetailsTab(0);
			IsGameDetailsSeeAllOpen = false;
			RefreshGameDetailsPanels();
			IsDetailsOpen = true;
			_audioService.Play("menu-in");
		}
	}

	public void ResetBladesMenuSelection()
	{
		_selectedBladesBladeIndex = ResolveBladesBladeIndexFromCurrentTab();
		SelectedBladesMenuIndex = 0;
		_bladesSubmenuKey = string.Empty;
		_selectedBladesSubmenuIndex = 0;
		NotifyActiveBladesMenuChanged();
	}

	private int ResolveBladesBladeIndexFromCurrentTab()
	{
		string key = CurrentTab?.Key ?? string.Empty;
		return key switch
		{
			"games" => 2,
			"video" or "music" => 3,
			"settings" or "apps" => 4,
			_ => 1
		};
	}

	private IReadOnlyList<BladesBladeViewModel> BuildBladesBlades()
	{
		System.Windows.Media.Brush marketplaceBrush = new SolidColorBrush(System.Windows.Media.Color.FromRgb(231, 94, 12));
		System.Windows.Media.Brush liveBrush = new SolidColorBrush(System.Windows.Media.Color.FromRgb(249, 183, 27));
		System.Windows.Media.Brush gamesBrush = new SolidColorBrush(System.Windows.Media.Color.FromRgb(44, 181, 42));
		System.Windows.Media.Brush mediaBrush = new SolidColorBrush(System.Windows.Media.Color.FromRgb(34, 148, 222));
		System.Windows.Media.Brush systemBrush = new SolidColorBrush(System.Windows.Media.Color.FromRgb(143, 88, 214));
		return new[]
		{
			new BladesBladeViewModel("marketplace", "Marketplace", marketplaceBrush, new[]
			{
				BladesItem("Spotlight", "\uE734", "Spotlight", "Featured Xbox LIVE Marketplace content.", key: "BladesNoAction"),
				BladesItem("New Arrivals", "\uE895", "New Arrivals", "Fresh games, videos, and downloads from Xbox LIVE Marketplace.", key: "BladesNoAction"),
				BladesItem("Game Store", "\uE7FC", "Game Store", "Browse Xbox LIVE game content.", key: "BladesNoAction"),
				BladesItem("Video Store", "\uE714", "Video Store", "Browse video marketplace content.", key: "BladesNoAction"),
				BladesItem("Redeem Code", "\uE8C7", "Redeem Code", "Redeem prepaid cards and marketplace codes.", key: "BladesNoAction"),
				BladesItem("Active Downloads", "\uE896", "Active Downloads", "View active marketplace downloads.", key: "BladesNoAction"),
				BladesItem("Account Management", "\uE77B", "Account Management", "Manage your Xbox LIVE account.", key: "BladesNoAction")
			}, _selectedBladesBladeIndex == 0),
			new BladesBladeViewModel("xbox live", "Xbox LIVE", liveBrush, new[]
			{
				BladesItem("Inside Xbox", "\uE715", "Xbox LIVE", "Games. Tournaments\nEntertainment. All the\nrewards. Endless possibilities.\nWhat are you waiting for?", key: "BladesNoAction"),
				BladesItem("Friends", "\uE716", "Xbox LIVE", "Games. Tournaments\nEntertainment. All the\nrewards. Endless possibilities.\nWhat are you waiting for?", key: "BladesOpenFriends"),
				BladesItem("Chat and IM", "\uE902", "Xbox LIVE", "Games. Tournaments\nEntertainment. All the\nrewards. Endless possibilities.\nWhat are you waiting for?", key: "BladesOpenParty")
			}, _selectedBladesBladeIndex == 1),
			new BladesBladeViewModel("games", "Games", gamesBrush, new[]
			{
				BladesItem("Games Library", "\uE7FC", "My Games", $"You have {Games.Count} games on your console. Select this option to play a game now.", key: "My Games"),
				BladesItem("Achievements", "\uECA7", "Achievements", "View achievements and progress for your games.", key: "Achievements"),
				BladesItem("Played Games", "\uE7FC", "Played Games", "View the current game's details.", key: "Played Games")
			}, _selectedBladesBladeIndex == 2),
			new BladesBladeViewModel("media", "Media", mediaBrush, new[]
			{
				BladesItem("Music", "\uE8D6", "Music", "Choose a music source and play dashboard music.", key: "Select Music"),
				BladesItem("Pictures", "\uE91B", "Pictures", "Browse pictures and media from local folders."),
				BladesItem("Videos", "\uE714", "Videos", "Open your video player.", key: "BladesNoAction"),
				BladesItem("Video Store", "\uE8B2", "Video Store", "Browse video apps and marketplace content.", key: "BladesNoAction"),
				BladesItem("Media Center", "\uE7F4", "Media Center", "Open Windows Media Player Legacy.", key: "Windows Media Center")
			}, _selectedBladesBladeIndex == 3),
			new BladesBladeViewModel("system", "System", systemBrush, new[]
			{
				BladesItem("Console Settings", "\uE115", "Console Settings", "Edit display, controller, startup, and dashboard style options."),
				BladesItem("Family Settings", "\uE716", "Family Settings", "Manage family controls for the dashboard."),
				BladesItem("Memory", "\uE958", "Memory", "Manage game saves, profiles, and storage."),
				BladesItem("Network Settings", "\uE774", "Network Settings", "Configure online and local network settings."),
				BladesItem("Computers", "\uE7F4", "Computers", "Connect to media PCs and shared libraries."),
				BladesItem("Xbox LIVE Vision", "\uE722", "Xbox LIVE Vision", "Configure camera and vision accessories."),
				BladesItem("Initial Setup", "\uE8B7", "Initial Setup", "Review setup options for DashX360.")
			}, _selectedBladesBladeIndex == 4)
		};
	}

	private bool SelectBladesBlade(string? key)
	{
		if (string.IsNullOrWhiteSpace(key))
		{
			return false;
		}
		IReadOnlyList<BladesBladeViewModel> blades = BladesBlades;
		for (int i = 0; i < blades.Count; i++)
		{
			if (string.Equals(blades[i].Key, key, StringComparison.OrdinalIgnoreCase))
			{
				if (_selectedBladesBladeIndex == i && !IsBladesSubmenuOpen)
				{
					return true;
				}
				int previousIndex = _selectedBladesBladeIndex;
				_selectedBladesBladeIndex = i;
				SelectedBladesMenuIndex = 0;
				_bladesSubmenuKey = string.Empty;
				_selectedBladesSubmenuIndex = 0;
				NotifyActiveBladesMenuChanged();
				PlayBladesBladeSwitchSound(previousIndex, i);
				return true;
			}
		}
		return false;
	}

	private bool MoveBladesBladeSelection(int delta)
	{
		IReadOnlyList<BladesBladeViewModel> blades = BladesBlades;
		if (blades.Count == 0)
		{
			return false;
		}
		int nextIndex = Math.Clamp(_selectedBladesBladeIndex + delta, 0, blades.Count - 1);
		if (nextIndex == _selectedBladesBladeIndex)
		{
			return true;
		}
		int previousIndex = _selectedBladesBladeIndex;
		_selectedBladesBladeIndex = nextIndex;
		SelectedBladesMenuIndex = 0;
		_bladesSubmenuKey = string.Empty;
		_selectedBladesSubmenuIndex = 0;
		NotifyActiveBladesMenuChanged();
		PlayBladesBladeSwitchSound(previousIndex, nextIndex);
		return true;
	}

	private void PlayBladesBladeSwitchSound(int previousIndex, int nextIndex)
	{
		_audioService.Play(GetBladesBladeSwitchSoundName(previousIndex, nextIndex));
	}

	private static string GetBladesBladeSwitchSoundName(int previousIndex, int nextIndex)
	{
		int transitionIndex = Math.Clamp(Math.Min(previousIndex, nextIndex), 0, 3);
		return "guide-blade-switch-" + (transitionIndex + 1).ToString(CultureInfo.InvariantCulture);
	}

	private bool _isBladesTraySelected;
	public bool IsBladesTraySelected { get => _isBladesTraySelected; private set => SetProperty(ref _isBladesTraySelected,value); }

	public bool MoveBladesMenuSelection(int delta)
	{
		if (IsBladesMenuTransitioning) return true;
		IReadOnlyList<BladesMenuItemViewModel> bladesMenuItems = ActiveBladesMenuItems;
		if (bladesMenuItems == null || bladesMenuItems.Count == 0)
		{
			return false;
		}
		int currentIndex = ActiveBladesMenuIndex;
		if (IsBladesTraySelected)
		{
			if (delta < 0) { IsBladesTraySelected=false; NotifyActiveBladesMenuChanged(); PlayFocusSound(); }
			return true;
		}
		if (!IsBladesSubmenuOpen && IsActiveBladesOpenTrayVisible && delta > 0 && currentIndex == bladesMenuItems.Count-1)
		{
			IsBladesTraySelected=true;
			NotifyActiveBladesMenuChanged();
			PlayFocusSound();
			return true;
		}
		int num = Math.Clamp(currentIndex + delta, 0, bladesMenuItems.Count - 1);
		if (num == currentIndex)
		{
			return true;
		}
		if (IsBladesSubmenuOpen)
		{
			_selectedBladesSubmenuIndex = num;
			NotifyActiveBladesMenuChanged();
		}
		else
		{
			SelectedBladesMenuIndex = num;
		}
		PlayFocusSound();
		return true;
	}

	public void ActivateBladesTray()
	{
		if (TrayGame != null) LaunchDashboardTile("home.open-tray");
		else StatusMessage="No disc in tray";
	}

	public void ActivateSelectedBladesMenuItem()
	{
		if (IsBladesTraySelected && !IsBladesSubmenuOpen) { ActivateBladesTray(); return; }
		OpenBladesMenuItem(ActiveBladesSelectedItem?.Key);
	}

	public bool HandleBladesAchievementsSubmenuInput(DashboardInputAction action)
	{
		if (!IsBladesAchievementsSubmenu)
		{
			return false;
		}
		return HandleBladesAchievementsInput(action);
	}

	private bool HandleBladesInput(DashboardInputAction action)
	{
		if (IsBladesMenuTransitioning) return true;
		if (IsBladesAchievementsSubmenu)
		{
			return HandleBladesAchievementsInput(action);
		}
		if (IsBladesGameLibrarySubmenu)
		{
			return HandleBladesGameLibraryInput(action);
		}
		switch (action)
		{
		case DashboardInputAction.MoveUp:
			return MoveBladesMenuSelection(-1);
		case DashboardInputAction.MoveDown:
			return MoveBladesMenuSelection(1);
		case DashboardInputAction.MoveLeft:
		case DashboardInputAction.PreviousTab:
			if (IsBladesSubmenuOpen)
			{
				CloseBladesSubmenu();
				return true;
			}
			return MoveBladesBladeSelection(-1);
		case DashboardInputAction.MoveRight:
		case DashboardInputAction.NextTab:
			if (IsBladesSubmenuOpen)
			{
				ActivateSelectedBladesMenuItem();
				return true;
			}
			return MoveBladesBladeSelection(1);
		case DashboardInputAction.Activate:
			ActivateSelectedBladesMenuItem();
			return true;
		case DashboardInputAction.Back:
			if (IsBladesSubmenuOpen)
			{
				CloseBladesSubmenu();
				return true;
			}
			return false;
		default:
			return false;
		}
	}

	private bool HandleBladesGameLibraryInput(DashboardInputAction action)
	{
		switch (action)
		{
		case DashboardInputAction.MoveUp:
			return MoveBladesMenuSelection(-1);
		case DashboardInputAction.MoveDown:
			return MoveBladesMenuSelection(1);
		case DashboardInputAction.MoveLeft:
		case DashboardInputAction.PreviousTab:
			return MoveBladesGameLibraryCategory(-1);
		case DashboardInputAction.MoveRight:
		case DashboardInputAction.NextTab:
			return MoveBladesGameLibraryCategory(1);
		case DashboardInputAction.Activate:
			ActivateSelectedBladesMenuItem();
			return true;
		case DashboardInputAction.Details:
			GameCardViewModel? game = FindGameById(ActiveBladesSelectedItem?.Key?.Replace("BladesLaunchGame:", string.Empty, StringComparison.OrdinalIgnoreCase).Replace("BladesGameDetails:", string.Empty, StringComparison.OrdinalIgnoreCase) ?? string.Empty);
			if (game != null)
			{
				SelectedGame = game;
				OpenGameDetails();
				_audioService.Play("select");
			}
			return true;
		case DashboardInputAction.Back:
			CloseBladesSubmenu();
			return true;
		default:
			return false;
		}
	}

	private bool MoveBladesGameLibraryCategory(int delta)
	{
		int nextIndex = Math.Clamp(_selectedBladesGameLibraryCategoryIndex + delta, 0, 4);
		if (nextIndex == _selectedBladesGameLibraryCategoryIndex)
		{
			return true;
		}
		_selectedBladesGameLibraryCategoryIndex = nextIndex;
		_selectedBladesSubmenuIndex = 0;
		NotifyActiveBladesMenuChanged();
		_ = RefreshBladesGameLibraryDetailsAsync();
		PlayFocusSound();
		return true;
	}

	private string GetBladesGameLibraryTabForeground(int index)
	{
		if (index == _selectedBladesGameLibraryCategoryIndex)
		{
			return "#F4FFFFFF";
		}
		return "#FF172017";
	}

	private bool HandleBladesAchievementsInput(DashboardInputAction action)
	{
		const int achievementColumns = 6;
		switch (action)
		{
		case DashboardInputAction.MoveUp:
			return MoveBladesAchievementSelection(IsBladesAchievementListFocused ? -achievementColumns : -1);
		case DashboardInputAction.MoveDown:
			return MoveBladesAchievementSelection(IsBladesAchievementListFocused ? achievementColumns : 1);
		case DashboardInputAction.MoveLeft:
		case DashboardInputAction.PreviousTab:
			if (IsBladesAchievementListFocused)
			{
				if (action == DashboardInputAction.MoveLeft && _selectedBladesAchievementIndex % achievementColumns != 0)
				{
					return MoveBladesAchievementSelection(-1);
				}
				SetBladesAchievementPaneFocus(false);
			}
			return true;
		case DashboardInputAction.MoveRight:
		case DashboardInputAction.NextTab:
			if (!IsBladesAchievementListFocused && BladesAchievementItems.Count > 0)
			{
				SetBladesAchievementPaneFocus(true);
				return true;
			}
			if (IsBladesAchievementListFocused && action == DashboardInputAction.MoveRight)
			{
				return MoveBladesAchievementSelection(1);
			}
			return true;
		case DashboardInputAction.Activate:
			OpenSelectedBladesAchievement();
			return true;
		case DashboardInputAction.Details:
			string detailsGameId = IsBladesAchievementListFocused ? (SelectedBladesAchievementItem?.GameId ?? string.Empty) : (SelectedBladesAchievementGame?.GameId ?? string.Empty);
			GameCardViewModel? game = FindGameById(detailsGameId);
			if (game != null)
			{
				SelectedGame = game;
				OpenGameDetails();
				_audioService.Play("select");
			}
			return true;
		case DashboardInputAction.Back:
			CloseBladesSubmenu();
			return true;
		default:
			return false;
		}
	}

	private void SetBladesAchievementPaneFocus(bool achievementListFocused)
	{
		if (_isBladesAchievementListFocused == achievementListFocused)
		{
			return;
		}
		IsBladesAchievementListFocused = achievementListFocused;
		PlayFocusSound();
	}

	private bool MoveBladesAchievementSelection(int delta)
	{
		if (IsBladesAchievementListFocused)
		{
			int oldIndex = _selectedBladesAchievementIndex;
			SelectedBladesAchievementIndex = Math.Clamp(_selectedBladesAchievementIndex + delta, 0, Math.Max(0, BladesAchievementItems.Count - 1));
			if (_selectedBladesAchievementIndex != oldIndex)
			{
				PlayFocusSound();
			}
			return true;
		}
		int oldGameIndex = _selectedBladesAchievementGameIndex;
		SelectedBladesAchievementGameIndex = Math.Clamp(_selectedBladesAchievementGameIndex + delta, 0, Math.Max(0, BladesAchievementGameItems.Count - 1));
		if (_selectedBladesAchievementGameIndex != oldGameIndex)
		{
			PlayFocusSound();
		}
		return true;
	}

	private void OpenSelectedBladesAchievement()
	{
		BladesAchievementItemViewModel? achievement = SelectedBladesAchievementItem;
		if (achievement == null)
		{
			return;
		}
		SelectedGame = FindGameById(achievement.GameId);
		AchievementOverlayRequested?.Invoke(this, new DashboardAchievementOpenRequest(achievement.SteamAppId, achievement.ApiName));
		_audioService.Play("select");
	}

	private void NotifyActiveBladesMenuChanged()
	{
		OnPropertyChanged("IsBladesSubmenuOpen");
		OnPropertyChanged("IsBladesGamesLibraryRootSubmenu");
		OnPropertyChanged("IsBladesGameLibrarySubmenu");
		OnPropertyChanged("IsBladesAchievementsSubmenu");
		OnPropertyChanged("IsBladesStandardSubmenu");
		OnPropertyChanged("BladesBlades");
		OnPropertyChanged("LeftBlades");
		OnPropertyChanged("RightBlades");
		OnPropertyChanged("ActiveBladesBlade");
		OnPropertyChanged("IsActiveBladesProfileVisible");
		OnPropertyChanged("IsActiveBladesMarketplace");
		OnPropertyChanged("IsActiveBladesXboxLive");
		OnPropertyChanged("IsActiveBladesGames");
		OnPropertyChanged("IsActiveBladesMedia");
		OnPropertyChanged("IsActiveBladesSystem");
		OnPropertyChanged("IsActiveBladesOpenTrayVisible");
		OnPropertyChanged("IsActiveBladesGenericMenuVisible");
		OnPropertyChanged("ActiveBladesAccentBrush");
		OnPropertyChanged("ActiveBladesTitle");
		OnPropertyChanged("ActiveBladesMenuItems");
		OnPropertyChanged("ActiveBladesMenuIndex");
		OnPropertyChanged("ActiveBladesSelectedItem");
		OnPropertyChanged("SelectedBladesMenuItem");
		OnPropertyChanged("BladesOverlayAccentBrush");
		OnPropertyChanged("BladesGameLibraryVisibleMenuItems");
		OnPropertyChanged("BladesGameLibraryTab0Foreground");
		OnPropertyChanged("BladesGameLibraryTab1Foreground");
		OnPropertyChanged("BladesGameLibraryTab2Foreground");
		OnPropertyChanged("BladesGameLibraryTab3Foreground");
		OnPropertyChanged("BladesGameLibraryTab4Foreground");
		NotifyBladesAchievementsChanged();
	}

	private void NotifyBladesAchievementsChanged()
	{
		RefreshBladesAchievementSelectionState();
		OnPropertyChanged("SelectedBladesAchievementGame");
		OnPropertyChanged("SelectedBladesAchievementItem");
		OnPropertyChanged("BladesAchievementsHeaderTitle");
		OnPropertyChanged("BladesAchievementsHeaderCountText");
		OnPropertyChanged("BladesAchievementsHeaderRightText");
	}

	private void RefreshBladesAchievementCollections()
	{
		_bladesAchievementGameItems.Clear();
		foreach (BladesAchievementGameViewModel game in BuildBladesAchievementGameItems())
		{
			_bladesAchievementGameItems.Add(game);
		}
		_selectedBladesAchievementGameIndex = Math.Clamp(_selectedBladesAchievementGameIndex, 0, Math.Max(0, _bladesAchievementGameItems.Count - 1));
		RefreshBladesAchievementItems();
		OnPropertyChanged("BladesAchievementGameItems");
		OnPropertyChanged("BladesAchievementItems");
	}

	private void RefreshBladesAchievementItems()
	{
		_bladesAchievementItems.Clear();
		foreach (BladesAchievementItemViewModel achievement in BuildBladesAchievementItems())
		{
			_bladesAchievementItems.Add(achievement);
		}
		_selectedBladesAchievementIndex = Math.Clamp(_selectedBladesAchievementIndex, 0, Math.Max(0, _bladesAchievementItems.Count - 1));
		RefreshBladesAchievementSelectionState();
		OnPropertyChanged("BladesAchievementItems");
	}

	private void RefreshBladesAchievementSelectionState()
	{
		for (int index = 0; index < _bladesAchievementGameItems.Count; index++)
		{
			_bladesAchievementGameItems[index].IsSelected = index == _selectedBladesAchievementGameIndex && !IsBladesAchievementListFocused;
		}
		for (int index = 0; index < _bladesAchievementItems.Count; index++)
		{
			_bladesAchievementItems[index].IsSelected = index == _selectedBladesAchievementIndex && IsBladesAchievementListFocused;
		}
	}

	public Func<string, Action, Task>? BladesSubmenuTransition { get; set; }
	public bool IsBladesMenuTransitioning { get; set; }

	private void OpenBladesSubmenu(string key)
	{
		if (IsBladesMenuTransitioning) return;
		if (BladesSubmenuTransition != null) _ = BladesSubmenuTransition(key, () => OpenBladesSubmenuCore(key));
		else OpenBladesSubmenuCore(key);
	}

	private void OpenBladesSubmenuCore(string key)
	{
		IsBladesTraySelected=false;
		_bladesSubmenuKey = key;
		_selectedBladesSubmenuIndex = string.Equals(key, "GamesLibraryRoot", StringComparison.OrdinalIgnoreCase) ? 1 : 0;
		if (string.Equals(key, "Achievements", StringComparison.OrdinalIgnoreCase))
		{
			_selectedBladesAchievementGameIndex = 0;
			_selectedBladesAchievementIndex = 0;
			_isBladesAchievementListFocused = false;
			RefreshBladesAchievementCollections();
			_ = RefreshBladesAchievementIconCachesAsync();
			OnPropertyChanged("SelectedBladesAchievementGameIndex");
			OnPropertyChanged("SelectedBladesAchievementIndex");
		}
		else if (string.Equals(key, "GamesLibrary", StringComparison.OrdinalIgnoreCase))
		{
			_selectedBladesGameLibraryCategoryIndex = 1;
		}
		else if (string.Equals(key, "PlayedGames", StringComparison.OrdinalIgnoreCase))
		{
			_selectedBladesGameLibraryCategoryIndex = 0;
		}
		NotifyActiveBladesMenuChanged();
		if (IsBladesGameLibrarySubmenu)
		{
			_ = RefreshBladesGameLibraryDetailsAsync();
		}
		_audioService.Play("select");
	}

	private async Task RefreshBladesAchievementIconCachesAsync()
	{
		List<string> appIds = Games
			.Where(game => !IsAppEntry(game.Game))
			.Select(game => game.Game.SteamAppId)
			.Where(appId =>
			{
				IReadOnlyList<SteamAchievementItem> achievements = ReadCachedUnlockedAchievements(appId);
				return !string.IsNullOrWhiteSpace(appId) && (achievements.Count == 0 || achievements.Any(achievement => string.IsNullOrWhiteSpace(achievement.IconUrl) && string.IsNullOrWhiteSpace(achievement.IconGrayUrl)));
			})
			.Distinct(StringComparer.OrdinalIgnoreCase)
			.ToList();
		if (appIds.Count == 0)
		{
			return;
		}
		foreach (string appId in appIds)
		{
			try
			{
				await _steamCommunityService.LoadAchievementsAsync(appId).ConfigureAwait(continueOnCapturedContext: true);
			}
			catch
			{
			}
		}
		if (IsBladesAchievementsSubmenu)
		{
			RefreshBladesAchievementCollections();
			NotifyBladesAchievementsChanged();
		}
	}

	private async Task RefreshBladesGameLibraryDetailsAsync()
	{
		List<GameCardViewModel> games = GetBladesGameLibraryItems()
			.Where(game => game.IsSteamGame && !string.IsNullOrWhiteSpace(game.Game.SteamAppId))
			.ToList();
		if (games.Count == 0)
		{
			return;
		}
		bool changed = false;
		foreach (GameCardViewModel game in games)
		{
			try
			{
				SteamGameDetails details = await _steamCommunityService.LoadGameDetailsAsync(game.Game.SteamAppId).ConfigureAwait(continueOnCapturedContext: true);
				if (details.Playtime.HasValue && details.Playtime.Value != game.Game.Playtime)
				{
					game.Game.Playtime = details.Playtime.Value;
					changed = true;
				}
				if (!string.IsNullOrWhiteSpace(details.Genre) && !string.Equals(game.Game.Genre, details.Genre, StringComparison.Ordinal))
				{
					game.Game.Genre = details.Genre;
					changed = true;
				}
				if (!string.IsNullOrWhiteSpace(details.StoreDescription) && !string.Equals(game.Game.StoreDescription, details.StoreDescription, StringComparison.Ordinal))
				{
					game.Game.StoreDescription = details.StoreDescription;
					changed = true;
				}
				game.Refresh();
			}
			catch
			{
			}
		}
		if (changed)
		{
			await PersistLibraryAsync();
			NotifyActiveBladesMenuChanged();
		}
	}

	private void CloseBladesSubmenu(bool playSound = true)
	{
		if (!playSound) { CloseBladesSubmenuCore(false); return; }
		if (IsBladesMenuTransitioning || !IsBladesSubmenuOpen) return;
		string target = _bladesSubmenuKey == "GamesLibrary" ? "GamesLibraryRoot" : string.Empty;
		Action change = () =>
		{
			if (target.Length > 0) OpenBladesSubmenuCore(target);
			else CloseBladesSubmenuCore(true);
		};
		if (BladesSubmenuTransition != null) _ = BladesSubmenuTransition(target, change);
		else change();
	}

	private void CloseBladesSubmenuCore(bool playSound)
	{
		if (!IsBladesSubmenuOpen)
		{
			return;
		}
		_bladesSubmenuKey = string.Empty;
		_selectedBladesSubmenuIndex = 0;
		NotifyActiveBladesMenuChanged();
		if (playSound)
		{
			_audioService.Play("back");
		}
	}

	private IReadOnlyList<BladesMenuItemViewModel> BuildBladesSubmenuItems()
	{
		return _bladesSubmenuKey switch
		{
			"GamesLibrary" => BuildBladesGameItems("Play Game", "Select a game to launch it now.", "BladesLaunchGame:"),
			"Achievements" => BuildBladesGameItems("Achievements", "Select a game to view achievement progress.", "BladesGameAchievements:"),
			"PlayedGames" => BuildBladesGameItems("Played Games", "Select a game to view details.", "BladesGameDetails:"),
			"GamesLibraryRoot" => new[]
			{
				BladesItem("Xbox LIVE", "\uE774", "Xbox LIVE", "Browse Xbox LIVE game content.", isEnabled: false),
				BladesItem("My Games", "\uE7FC", "My Games", $"You have {Games.Count} games on your console. Select this option to play a game now.", Games.Count.ToString(CultureInfo.InvariantCulture), key: "BladesOpenSubmenu:GamesLibrary"),
				BladesItem("Last Played Game", "\uE7FC", "Last Played Game", "Jump back to your most recently played game.", isEnabled: false),
				BladesItem("Friends Playing Now", "\uE716", "Friends Playing Now", "See which friends are playing.", isEnabled: false),
				BladesItem("Game Store", "\uE719", "Game Store", "Open game marketplace content.", key: "Game Store"),
				BladesItem("Auto Downloads", "\uE896", "Auto Downloads", "Automatic downloads are off.", "Off", isEnabled: false)
			},
			"MusicSources" => new[]
			{
				BladesItem("Hard Drive", "\uE958", "Hard Drive", "Listen to music stored in your dashboard music folder.", key: "BladesOpenSubmenu:HardDrive"),
				BladesItem("Spotify", "\uE93C", "Spotify", "Use Spotify as a dashboard music source.", key: "BladesOpenSpotify")
			},
			"HardDrive" => new[]
			{
				BladesItem("Saved Playlists", "\uE8D6", "Saved Playlists", "Choose a saved playlist from your Music Files folder.", key: "BladesOpenHardDrive"),
				BladesItem("Songs", "\uE8D6", "Songs", "Browse songs in your Music Files folder.", key: "BladesOpenHardDrive")
			},
			"Apps" => BuildBladesAppItems(),
			"Profile" => new[]
			{
				BladesItem("View Games", "\uE7FC", "View Games", "View games attached to this profile."),
				BladesItem("Edit Profile", "\uE70F", "Edit Profile", "Open the profile editor.", key: "BladesOpenProfileEditor"),
				BladesItem("View Rep", "\uECA7", "View Rep", "View profile reputation."),
				BladesItem("Game Defaults", "\uE7FC", "Game Defaults", "Game defaults are not available in Blades yet."),
				BladesItem("Sign-in Preferences", "\uE72E", "Sign-in Preferences", "Sign-in options are handled by DashX360."),
				BladesItem("Account Security", "\uE72E", "Account Security", "Account security is visual only in DashX360."),
			},
			"InsideXbox" => BuildBladesPlaceholderItems("Inside Xbox", "Xbox LIVE news and videos are not connected."),
			"ConsoleSettings" => BuildBladesSettingsItems("Console Settings"),
			"DashboardCustomization" => BuildBladesSettingsItems("Dashboard Customization"),
			"GamesSetup" => BuildBladesSettingsItems("Games Setup"),
			"Audio" => BuildBladesSettingsItems("Audio"),
			"DataControl" => BuildBladesSettingsItems("Data Control"),
			"GameStore" => BuildBladesPlaceholderItems("Game Store", "Marketplace content is not available."),
			"Pins" => BuildBladesPlaceholderItems("My Pins", "Pinned shortcuts appear on the Metro dashboard."),
			"VideoStore" => BuildBladesPlaceholderItems("Video Store", "Video marketplace content is not available."),
			_ => BuildBladesPlaceholderItems(ActiveBladesTitle, "No options are available.")
		};
	}

	private static BladesMenuItemViewModel BladesItem(string title, string iconGlyph, string detailTitle, string detailDescription, string countText = "", bool isEnabled = true, string? key = null, bool isSelected = false, string? iconPath = null, string detailMetaText = "")
	{
		return new BladesMenuItemViewModel(title, iconGlyph, detailTitle, detailDescription, countText, isEnabled, key, isSelected, iconPath ?? GetBladesIconPath(title), detailMetaText);
	}

	private static string GetBladesIconPath(string title)
	{
		return title switch
		{
			"Spotlight" => "Assets/Blades/Icons/ico_64x_Downloads.png",
			"New Arrivals" => "Assets/Blades/ProvidedIconsFit/marketplace/new arrivals.png",
			"Game Store" => "Assets/Blades/ProvidedIconsFit/marketplace/game store.png",
			"Video Store" => "Assets/Blades/ProvidedIconsFit/marketplace/video store.png",
			"Redeem Code" => "Assets/Blades/ProvidedIconsFit/marketplace/redeem code orange.png",
			"Active Downloads" => "Assets/Blades/ProvidedIconsFit/marketplace/active downloads.png",
			"Account Management" => "Assets/Blades/ProvidedIconsFit/marketplace/account management orange.png",
			"Inside Xbox" => "Assets/Blades/ProvidedIconsFit/xbox live/dashcomm__ico_64x_InsideXbox.png",
			"Friends" => "Assets/Blades/ProvidedIconsFit/xbox live/Friends.png",
			"Chat and IM" => "Assets/Blades/ProvidedIconsFit/xbox live/chat and im.png",
			"Games Library" => "Assets/Blades/ProvidedIconsFit/games/Games Library.png",
			"Achievements" => "Assets/Blades/ProvidedIconsFit/games/Achievements.png",
			"Played Games" => "Assets/Blades/ProvidedIconsFit/games/Played Games.png",
			"Music" => "Assets/Blades/ProvidedIconsFit/media/music.png",
			"Pictures" => "Assets/Blades/ProvidedIconsFit/media/pictures.png",
			"Videos" => "Assets/Blades/ProvidedIconsFit/media/videos.png",
			"Media Center" => "Assets/Blades/ProvidedIconsFit/media/media center.png",
			"Console Settings" => "Assets/Blades/ProvidedIconsFit/system/console settings.png",
			"Family Settings" => "Assets/Blades/ProvidedIconsFit/system/family settings.png",
			"Memory" => "Assets/Blades/ProvidedIconsFit/system/memory.png",
			"Network Settings" => "Assets/Blades/ProvidedIconsFit/system/network settings.png",
			"Xbox LIVE Vision" => "Assets/Blades/ProvidedIconsFit/system/XBOX LIVE Vision.png",
			"Initial Setup" => "Assets/Blades/ProvidedIconsFit/system/initial setup.png",
			_ => string.Empty
		};
	}

	private IReadOnlyList<BladesMenuItemViewModel> BuildBladesGameLibraryVisibleMenuItems()
	{
		IReadOnlyList<BladesMenuItemViewModel> items = BuildBladesSubmenuItems();
		if (items.Count == 0)
		{
			return items;
		}
		const int visibleCount = 6;
		int selectedIndex = Math.Clamp(_selectedBladesSubmenuIndex, 0, items.Count - 1);
		int startIndex = Math.Clamp(selectedIndex - 2, 0, Math.Max(0, items.Count - visibleCount));
		List<BladesMenuItemViewModel> visibleItems = items.Skip(startIndex).Take(visibleCount).ToList();
		return MarkSelectedBladesItem(visibleItems, selectedIndex - startIndex);
	}

	private static IReadOnlyList<BladesMenuItemViewModel> MarkSelectedBladesItem(IReadOnlyList<BladesMenuItemViewModel> items, int selectedIndex)
	{
		if (items.Count == 0)
		{
			return items;
		}
		int clampedIndex = selectedIndex < 0 ? -1 : Math.Clamp(selectedIndex, 0, items.Count - 1);
		return items.Select((item, index) => BladesItem(
			item.Title,
			item.IconGlyph,
			item.DetailTitle,
			item.DetailDescription,
			item.CountText,
			item.IsEnabled,
			item.Key,
			index == clampedIndex,
			item.IconPath,
			item.DetailMetaText)).ToArray();
	}

	private IReadOnlyList<BladesMenuItemViewModel> BuildBladesGameItems(string fallbackTitle, string emptyDescription, string keyPrefix)
	{
		List<GameCardViewModel> games = (string.Equals(keyPrefix, "BladesLaunchGame:", StringComparison.OrdinalIgnoreCase) || string.Equals(keyPrefix, "BladesGameDetails:", StringComparison.OrdinalIgnoreCase)
				? GetBladesGameLibraryItems()
				: Games.Where(game => !IsAppEntry(game.Game)).OrderBy(game => game.Title, StringComparer.CurrentCultureIgnoreCase))
			.ToList();
		if (string.Equals(keyPrefix, "BladesGameAchievements:", StringComparison.OrdinalIgnoreCase))
		{
			games = games
				.Where(game => CountCachedUnlockedAchievements(game.Game.SteamAppId) > 0)
				.ToList();
		}
		if (games.Count == 0)
		{
			return BuildBladesPlaceholderItems(fallbackTitle, emptyDescription);
		}
		return games.Select(game => BladesItem(
			game.Title,
			"\uE7FC",
			game.IsSteamGame ? "Steam Game" : "Manual Game",
			string.Equals(keyPrefix, "BladesGameAchievements:", StringComparison.OrdinalIgnoreCase)
				? $"{CountCachedUnlockedAchievements(game.Game.SteamAppId)} unlocked achievements"
				: BuildBladesGameLibraryDetailText(game),
			string.Equals(keyPrefix, "BladesGameAchievements:", StringComparison.OrdinalIgnoreCase)
				? string.Empty
				: BuildBladesGameAchievementProgressText(game),
			isEnabled: true,
			keyPrefix + game.Game.Id,
			iconPath: GetBladesLibraryItemIconPath(game),
			detailMetaText: string.Equals(keyPrefix, "BladesGameAchievements:", StringComparison.OrdinalIgnoreCase)
				? string.Empty
				: BuildBladesGameLibraryMetaText(game))).ToList();
	}

	private static string BuildBladesGameLibraryDetailText(GameCardViewModel game)
	{
		return BuildBladesGameDescriptionText(game);
	}

	private static string BuildBladesGameLibraryMetaText(GameCardViewModel game)
	{
		string playtimeText = game.DetailsPlaytimeText;
		string lastPlayedText = game.Game.LastPlayed.HasValue
			? "Last played: " + game.Game.LastPlayed.Value.LocalDateTime.ToString("MMM d, yyyy h:mm tt", CultureInfo.CurrentCulture)
			: "Last played: Never";
		return $"{playtimeText}{Environment.NewLine}{lastPlayedText}";
	}

	private static string BuildBladesGameDescriptionText(GameCardViewModel game)
	{
		if (!string.IsNullOrWhiteSpace(game.Game.StoreDescription))
		{
			return ShortenBladesText(game.Game.StoreDescription, 142);
		}
		string genreText = string.IsNullOrWhiteSpace(game.DetailsGenreText) || string.Equals(game.DetailsGenreText, "Game", StringComparison.OrdinalIgnoreCase)
			? "game"
			: game.DetailsGenreText.Trim().ToLowerInvariant() + " game";
		if (game.IsSteamGame)
		{
			return $"A Steam {genreText}. Store description will appear after Steam details are loaded.";
		}
		return $"A manually added {genreText} in your DashX360 library.";
	}

	private static string ShortenBladesText(string value, int maxLength)
	{
		string text = Regex.Replace(value.Trim(), "\\s+", " ");
		if (text.Length <= maxLength)
		{
			return text;
		}
		return text.Substring(0, Math.Max(0, maxLength - 3)).TrimEnd() + "...";
	}

	private static string BuildBladesGameAchievementProgressText(GameCardViewModel game)
	{
		int unlockedAchievements = CountCachedUnlockedAchievements(game.Game.SteamAppId);
		int totalAchievements = CountCachedAchievementTotal(game.Game.SteamAppId);
		return $"{unlockedAchievements}/{totalAchievements} Achievements unlocked";
	}

	private IEnumerable<GameCardViewModel> GetBladesGameLibraryItems()
	{
		IEnumerable<GameCardViewModel> games = Games;
		switch (_selectedBladesGameLibraryCategoryIndex)
		{
		case 0:
			return games
				.Where(game => !IsAppEntry(game.Game) && game.Game.LastPlayed.HasValue)
				.OrderByDescending(game => game.Game.LastPlayed ?? DateTimeOffset.MinValue);
		case 2:
			return games
				.Where(game => IsAppEntry(game.Game) && !IsBuiltInYouTubeEntry(game.Game))
				.OrderBy(game => game.Title, StringComparer.CurrentCultureIgnoreCase);
		case 3:
			return games
				.Where(game => !IsAppEntry(game.Game) && string.Equals(game.Game.LaunchType, "Steam", StringComparison.OrdinalIgnoreCase))
				.OrderBy(game => game.Title, StringComparer.CurrentCultureIgnoreCase);
		case 4:
			return games
				.Where(game => !IsAppEntry(game.Game) && !string.Equals(game.Game.LaunchType, "Steam", StringComparison.OrdinalIgnoreCase))
				.OrderBy(game => game.Title, StringComparer.CurrentCultureIgnoreCase);
		default:
			return games
				.Where(game => !IsAppEntry(game.Game))
				.OrderBy(game => game.Title, StringComparer.CurrentCultureIgnoreCase);
		}
	}

	private static string GetBladesLibraryItemIconPath(GameCardViewModel game)
	{
		if (!string.IsNullOrWhiteSpace(game.Game.LogoImagePath))
		{
			return game.Game.LogoImagePath;
		}
		if (!string.IsNullOrWhiteSpace(game.CoverArtPath))
		{
			return game.CoverArtPath;
		}
		return string.Empty;
	}

	private static int CountCachedUnlockedAchievements(string appId)
	{
		if (string.IsNullOrWhiteSpace(appId))
		{
			return 0;
		}
		try
		{
			string safeAppId = string.Concat(appId.Where(char.IsLetterOrDigit));
			if (string.IsNullOrWhiteSpace(safeAppId))
			{
				return 0;
			}
			string cachePath = Path.Combine(AppPaths.UserDataFolder, "SteamCache", "Achievements", safeAppId + ".json");
			if (!File.Exists(cachePath))
			{
				return 0;
			}
			List<SteamAchievementItem>? achievements = JsonSerializer.Deserialize<List<SteamAchievementItem>>(File.ReadAllText(cachePath));
			return achievements?.Count(achievement => achievement.Achieved) ?? 0;
		}
		catch
		{
			return 0;
		}
	}

	private static int CountCachedAchievementTotal(string appId)
	{
		if (string.IsNullOrWhiteSpace(appId))
		{
			return 0;
		}
		try
		{
			string safeAppId = string.Concat(appId.Where(char.IsLetterOrDigit));
			if (string.IsNullOrWhiteSpace(safeAppId))
			{
				return 0;
			}
			string cachePath = Path.Combine(AppPaths.UserDataFolder, "SteamCache", "Achievements", safeAppId + ".json");
			if (!File.Exists(cachePath))
			{
				return 0;
			}
			List<SteamAchievementItem>? achievements = JsonSerializer.Deserialize<List<SteamAchievementItem>>(File.ReadAllText(cachePath));
			return achievements?.Count ?? 0;
		}
		catch
		{
			return 0;
		}
	}

	private IReadOnlyList<BladesAchievementGameViewModel> BuildBladesAchievementGameItems()
	{
		List<BladesAchievementGameViewModel> games = Games
			.Where(game => !IsAppEntry(game.Game))
			.Select(game => new
			{
				Game = game,
				Achievements = ReadCachedUnlockedAchievements(game.Game.SteamAppId)
			})
			.Where(game => game.Achievements.Count > 0)
			.OrderBy(game => game.Game.Title, StringComparer.CurrentCultureIgnoreCase)
			.Select((game, index) => new BladesAchievementGameViewModel(
				game.Game.Game.Id,
				game.Game.Game.SteamAppId,
				game.Game.Title,
				string.IsNullOrWhiteSpace(game.Game.Game.LogoImagePath) ? game.Game.CoverArtPath : game.Game.Game.LogoImagePath,
				game.Achievements.Count,
				index + 1 == _selectedBladesAchievementGameIndex))
			.ToList();
		int totalUnlocked = games.Sum(game => game.UnlockedCount);
		if (totalUnlocked > 0)
		{
			games.Insert(0, new BladesAchievementGameViewModel(string.Empty, string.Empty, "All Games", "Assets/Blades/Icons/ico_64x_games.png", totalUnlocked, _selectedBladesAchievementGameIndex == 0));
		}
		return games;
	}

	private IReadOnlyList<BladesAchievementItemViewModel> BuildBladesAchievementItems()
	{
		BladesAchievementGameViewModel? selectedGame = SelectedBladesAchievementGame;
		IEnumerable<GameCardViewModel> games = Games.Where(game => !IsAppEntry(game.Game));
		if (selectedGame != null && !string.IsNullOrWhiteSpace(selectedGame.GameId))
		{
			games = games.Where(game => string.Equals(game.Game.Id, selectedGame.GameId, StringComparison.OrdinalIgnoreCase));
		}
		return games
			.SelectMany(game => ReadCachedUnlockedAchievements(game.Game.SteamAppId).Select(achievement => new
			{
				Game = game,
				Achievement = achievement
			}))
			.OrderBy(item => selectedGame != null && !string.IsNullOrWhiteSpace(selectedGame.GameId) ? item.Achievement.Name : item.Game.Title, StringComparer.CurrentCultureIgnoreCase)
			.ThenBy(item => item.Achievement.Name, StringComparer.CurrentCultureIgnoreCase)
			.Select((item, index) => new BladesAchievementItemViewModel(
				item.Game.Game.Id,
				item.Game.Game.SteamAppId,
				item.Game.Title,
				string.IsNullOrWhiteSpace(item.Achievement.ApiName) ? item.Achievement.Name : item.Achievement.ApiName,
				string.IsNullOrWhiteSpace(item.Achievement.Name) ? item.Achievement.ApiName : item.Achievement.Name,
				item.Achievement.Description,
				ResolveAchievementIconPath(item.Game.Game.SteamAppId, item.Achievement),
				index == _selectedBladesAchievementIndex))
			.ToList();
	}

	private static IReadOnlyList<SteamAchievementItem> ReadCachedUnlockedAchievements(string appId)
	{
		if (string.IsNullOrWhiteSpace(appId))
		{
			return Array.Empty<SteamAchievementItem>();
		}
		try
		{
			string safeAppId = PathSafeSteamAppId(appId);
			if (string.IsNullOrWhiteSpace(safeAppId))
			{
				return Array.Empty<SteamAchievementItem>();
			}
			string cachePath = Path.Combine(AppPaths.UserDataFolder, "SteamCache", "Achievements", safeAppId + ".json");
			if (!File.Exists(cachePath))
			{
				return Array.Empty<SteamAchievementItem>();
			}
			List<SteamAchievementItem>? achievements = JsonSerializer.Deserialize<List<SteamAchievementItem>>(File.ReadAllText(cachePath));
			return achievements?.Where(achievement => achievement.Achieved).ToList() ?? new List<SteamAchievementItem>();
		}
		catch
		{
			return Array.Empty<SteamAchievementItem>();
		}
	}

	private static string ResolveAchievementIconPath(string appId, SteamAchievementItem achievement)
	{
		if (!string.IsNullOrWhiteSpace(achievement.IconUrl))
		{
			return achievement.IconUrl;
		}
		if (!string.IsNullOrWhiteSpace(achievement.IconGrayUrl))
		{
			return achievement.IconGrayUrl;
		}
		return string.Empty;
	}

	private static string PathSafeSteamAppId(string appId)
	{
		return string.Concat(appId.Where(char.IsLetterOrDigit));
	}

	private IReadOnlyList<BladesMenuItemViewModel> BuildBladesAppItems()
	{
		if (AppLibraryTiles.Count == 0)
		{
			return BuildBladesPlaceholderItems("My Apps", "No apps are available.");
		}
		return AppLibraryTiles.Select((AppLibraryTileViewModel app, int index) => BladesItem(
			app.Title,
			string.IsNullOrWhiteSpace(app.Glyph) ? "\uE71D" : app.Glyph,
			app.Title,
			"Select this app to launch it.",
			string.Empty,
			isEnabled: true,
			"BladesLaunchApp:" + index.ToString(CultureInfo.InvariantCulture))).ToList();
	}

	private static IReadOnlyList<BladesMenuItemViewModel> BuildBladesSocialItems(string title, string description, string key)
	{
		return new[]
		{
			BladesItem("Open " + title, "\uE716", title, description, key: key)
		};
	}

	private IReadOnlyList<BladesMenuItemViewModel> BuildBladesSettingsItems(string sectionTitle)
	{
		return sectionTitle switch
		{
			"Console Settings" => new[]
			{
				BladesItem("Display", "\uE7F4", "Display", "Change aspect ratio and fullscreen settings.", key: "BladesOpenSettings"),
				BladesItem("Controller", "\uE7FC", "Controller", "Change controller input settings.", key: "BladesOpenSettings"),
				BladesItem("Dashboard Style", "\uE771", "Dashboard Style", "Switch between Metro and Blades.", key: "BladesOpenSettings"),
				BladesItem("Startup", "\uE8B7", "Startup", "Change launch and loading options.", key: "BladesOpenSettings")
			},
			"Dashboard Customization" => new[]
			{
				BladesItem("Create Theme", "\uE771", "Create Theme", "Create or edit dashboard themes.", key: "BladesOpenDashboardCustomization"),
				BladesItem("Customize Tiles", "\uE771", "Customize Tiles", "Change tile images, labels, colors, and shortcuts.", key: "BladesOpenDashboardCustomization")
			},
			"Games Setup" => new[]
			{
				BladesItem("Scan Steam Games", "\uE7FC", "Scan Steam Games", "Import installed Steam games.", key: "BladesOpenGamesSetup"),
				BladesItem("Add Executable", "\uE8A5", "Add Executable", "Add games, apps, shortcuts, and URLs.", key: "BladesOpenGamesSetup"),
				BladesItem("Edit Items", "\uE70F", "Edit Items", "Edit names, executables, and cover fit.", key: "BladesOpenGamesSetup")
			},
			"Audio" => new[]
			{
				BladesItem("Output Device Select", "\uE995", "Output Device Select", "Choose which Windows audio output plays dashboard sounds.", key: "BladesOpenAudioSettings"),
				BladesItem("Music Files", "\uE8D6", "Music Files", "Open the local music folder.", key: "BladesOpenAudioSettings"),
				BladesItem("Dashboard Volume", "\uE995", "Dashboard Volume", "Change DashX360 audio volume.", key: "BladesOpenAudioSettings")
			},
			"Data Control" => new[]
			{
				BladesItem("Import Data", "\uE8B5", "Import Data", "Restore dashboard data from a backup file.", key: "BladesOpenDataControl"),
				BladesItem("Export Data", "\uE8B5", "Export Data", "Save a dashboard backup file.", key: "BladesOpenDataControl")
			},
			_ => BuildBladesPlaceholderItems(sectionTitle, "No options are available.")
		};
	}

	private static IReadOnlyList<BladesMenuItemViewModel> BuildBladesPlaceholderItems(string title, string description)
	{
		return new[]
		{
			BladesItem(title, "\uE10F", title, description, isEnabled: false)
		};
	}

	private bool IsOverlayOpenForBladesInput()
	{
		if (IsSearchOverlayOpen || IsDetailsOpen || IsQuickMenuOpen || IsMyGamesOpen || IsLauncherSettingsOpen || IsProfileEditorOpen || IsThemeMenuOpen || IsThemeCreatorOpen)
		{
			return true;
		}
		if (IsDashboardCustomizerOpen || IsSteamSetupOpen || IsSpotifySetupOpen || IsMusicPlayerOpen || IsYouTubeTvOpen)
		{
			return true;
		}
		return false;
	}

	private void OpenBladesMenuItem(string? item)
	{
		if (string.IsNullOrWhiteSpace(item))
		{
			return;
		}
		string key = item.Trim();
		if (string.Equals(key, "BladesNoAction", StringComparison.OrdinalIgnoreCase))
		{
			return;
		}
		if (key.StartsWith("BladesOpenSubmenu:", StringComparison.OrdinalIgnoreCase))
		{
			string submenuKey = key.Substring("BladesOpenSubmenu:".Length);
			if (string.Equals(submenuKey, "MusicSources", StringComparison.OrdinalIgnoreCase))
			{
				OpenMusicPlayer(transparent: false);
				return;
			}
			if (string.Equals(submenuKey, "ConsoleSettings", StringComparison.OrdinalIgnoreCase))
			{
				OpenLauncherSettings();
				return;
			}
			OpenBladesSubmenu(submenuKey);
			return;
		}
		if (key.StartsWith("BladesLaunchGame:", StringComparison.OrdinalIgnoreCase))
		{
			GameCardViewModel? game = FindGameById(key.Substring("BladesLaunchGame:".Length));
			if (game != null)
			{
				SelectedGame = game;
				_ = LaunchGameAsync(game);
			}
			return;
		}
		if (key.StartsWith("BladesGameDetails:", StringComparison.OrdinalIgnoreCase))
		{
			GameCardViewModel? game = FindGameById(key.Substring("BladesGameDetails:".Length));
			if (game != null)
			{
				SelectedGame = game;
				OpenGameDetails();
			}
			return;
		}
		if (key.StartsWith("BladesGameAchievements:", StringComparison.OrdinalIgnoreCase))
		{
			GameCardViewModel? game = FindGameById(key.Substring("BladesGameAchievements:".Length));
			if (game != null)
			{
				SelectedGame = game;
				OpenGameDetails();
			}
			return;
		}
		if (key.StartsWith("BladesLaunchApp:", StringComparison.OrdinalIgnoreCase))
		{
			if (int.TryParse(key.Substring("BladesLaunchApp:".Length), NumberStyles.Integer, CultureInfo.InvariantCulture, out int index) && index >= 0 && index < AppLibraryTiles.Count)
			{
				LaunchAppLibraryTileCommand.Execute(AppLibraryTiles[index]);
			}
			return;
		}
		switch (key)
		{
		case "Search Bing":
		case "Recent Searches":
			OpenSearch();
			break;
		case "Internet Explorer":
			OpenDefaultBrowser();
			break;
		case "My Pins":
			OpenBladesSubmenu("Pins");
			break;
		case "Recent":
			OpenBladesSubmenu("PlayedGames");
			break;
		case "Games Library":
		case "My Games":
			OpenBladesSubmenu("GamesLibraryRoot");
			break;
		case "Game Details":
		case "Played Games":
			OpenBladesSubmenu("PlayedGames");
			break;
		case "Achievements":
			OpenBladesSubmenu("Achievements");
			break;
		case "Profile":
			OpenBladesSubmenu("Profile");
			break;
		case "Friends":
			RequestFriendsOverlay();
			break;
		case "Party":
		case "Chat and IM":
			RequestPartyOverlay();
			break;
		case "Inside Xbox":
			OpenBladesSubmenu("InsideXbox");
			break;
		case "Video Player":
		case "Movies & TV":
			OpenBladesSubmenu("VideoStore");
			break;
		case "Windows Media Center":
			OpenWindowsMediaPlayerLegacy();
			break;
		case "Game Marketplace":
		case "Game Store":
			OpenBladesSubmenu("GameStore");
			break;
		case "Music Player":
			OpenMusicNowPlaying(transparent: false);
			break;
		case "Select Music":
		case "Music":
			BladesMusicRequested?.Invoke(this, EventArgs.Empty);
			break;
		case "Hard Drive":
			OpenBladesSubmenu("HardDrive");
			break;
		case "Spotify":
			OpenBladesSubmenu("MusicSources");
			break;
		case "My Apps":
		case "Browse Apps":
			OpenBladesSubmenu("Apps");
			break;
		case "YouTube":
			OpenYouTube();
			break;
		case "Dashboard Customization":
			OpenBladesSubmenu("DashboardCustomization");
			break;
		case "Console Settings":
			OpenLauncherSettings();
			break;
		case "Games Setup":
			OpenBladesSubmenu("GamesSetup");
			break;
		case "Audio":
			OpenBladesSubmenu("Audio");
			break;
		case "Data Control":
			OpenBladesSubmenu("DataControl");
			break;
		case "BladesOpenProfileEditor":
			OpenProfileEditor();
			break;
		case "BladesOpenFriends":
			RequestFriendsOverlay();
			break;
		case "BladesOpenParty":
			RequestPartyOverlay();
			break;
		case "BladesOpenHardDrive":
			OpenMusicPlayer(transparent: false);
			OpenHardDriveBrowser();
			break;
		case "BladesOpenSpotify":
			OpenMusicPlayer(transparent: false);
			OpenSpotifyBrowser();
			break;
		case "BladesOpenSettings":
			OpenLauncherSettings();
			break;
		case "BladesOpenDashboardCustomization":
			OpenDashboardCustomizer();
			break;
		case "BladesOpenGamesSetup":
		case "BladesOpenAudioSettings":
		case "BladesOpenDataControl":
			OpenLauncherSettings();
			break;
		default:
			if (string.Equals(key, OpenTrayTitle, StringComparison.OrdinalIgnoreCase))
			{
				_ = SetOpenTrayGameAsync(null);
			}
			break;
		}
	}

	private GameCardViewModel? FindGameById(string id)
	{
		return Games.FirstOrDefault(game => string.Equals(game.Game.Id, id, StringComparison.OrdinalIgnoreCase));
	}

	private void SelectGameDetailsTab(string key)
	{
		int num = GameDetailsTabs.Select((GameDetailsTabViewModel tab, int tabIndex) => new { tab, tabIndex }).FirstOrDefault(item => string.Equals(item.tab.Key, key, StringComparison.OrdinalIgnoreCase))?.tabIndex ?? (-1);
		if (num >= 0)
		{
			IsGameDetailsSeeAllOpen = false;
			if (num != _selectedGameDetailsTabIndex)
			{
				int num2 = ((num >= _selectedGameDetailsTabIndex) ? 1 : (-1));
				SetGameDetailsTab(num);
				_audioService.Play((num2 < 0) ? "page-left" : "page-right");
			}
		}
	}

	private void SetGameDetailsTab(int index)
	{
		_selectedGameDetailsTabIndex = Math.Clamp(index, 0, GameDetailsTabs.Count - 1);
		for (int i = 0; i < GameDetailsTabs.Count; i++)
		{
			GameDetailsTabs[i].IsSelected = i == _selectedGameDetailsTabIndex;
		}
		OnPropertyChanged("SelectedGameDetailsTabKey");
		NotifyGameDetailsTabVisibility();
	}

	private void OpenGameDetailsSeeAll()
	{
		SelectGameDetailsTab("extras");
		IsGameDetailsSeeAllOpen = true;
		_audioService.Play("select");
	}

	private void NotifyGameDetailsTabVisibility()
	{
		OnPropertyChanged("IsGameDetailsOverviewTab");
		OnPropertyChanged("IsGameDetailsDetailsTab");
		OnPropertyChanged("IsGameDetailsExtrasTab");
		OnPropertyChanged("IsGameDetailsGalleryTab");
	}

	public void SelectGame(GameCardViewModel? game)
	{
		if (game != null)
		{
			SelectedGame = game;
		}
	}

	public IReadOnlyList<GameCardViewModel> GetLibraryMenuGamesForMode(bool showPins, bool showApps)
	{
		if (showPins)
		{
			return Games.Where((GameCardViewModel game) => game.Game.IsFavorite).ToList();
		}
		if (showApps)
		{
			return Games.Where((GameCardViewModel game) => IsAppEntry(game.Game)).ToList();
		}
		return Games.Where((GameCardViewModel game) => !IsAppEntry(game.Game)).ToList();
	}

	private void PlayFocusSound()
	{
		DateTimeOffset utcNow = DateTimeOffset.UtcNow;
		if (utcNow - _lastFocusSoundAt < TimeSpan.FromMilliseconds(8.0))
		{
			return;
		}
		_lastFocusSoundAt = utcNow;
		_audioService.Play("focus");
	}

	private void InitializeAppLibraryTiles()
	{
		if (AppLibraryTiles.Count != 0)
		{
			return;
		}
		RebuildAppLibraryTiles();
	}

	private void RebuildAppLibraryTiles()
	{
		const double tileSize = 198.0;
		const double tileGap = 4.0;
		const double firstX = 16.0;
		const double firstY = 8.0;
		double secondY = firstY + tileSize + tileGap;
		double step = tileSize + tileGap;
		string? selectedId = SelectedAppLibraryTile?.Game?.Game.Id;
		string? selectedTitle = SelectedAppLibraryTile?.Title;
		AppLibraryTiles.Clear();
		AppLibraryTiles.Add(new AppLibraryTileViewModel("Windows Media Center", "\uE7F4", System.Windows.Media.Color.FromRgb(0, 114, 188), tileSize, tileSize, firstX, firstY, null, "Assets/Tiles/myapps-windows-media-center.png"));
		AppLibraryTiles.Add(new AppLibraryTileViewModel("System Music Player", "\uE189", System.Windows.Media.Color.FromRgb(0, 154, 0), tileSize, tileSize, firstX + step, firstY, null, "Assets/Tiles/myapps-system-music-player.png"));
		AppLibraryTiles.Add(new AppLibraryTileViewModel("YouTube", "\uE768", Colors.White, tileSize, tileSize, firstX + step * 2.0, firstY, System.Windows.Media.Color.FromRgb(205, 24, 30), "Assets/Tiles/youtube-icon.png"));
		AppLibraryTiles.Add(new AppLibraryTileViewModel("Internet Explorer", "\uE774", System.Windows.Media.Color.FromRgb(0, 188, 222), tileSize, tileSize, firstX, secondY, null, "Assets/Tiles/myapps-internet-explorer.png"));
		AppLibraryTiles.Add(new AppLibraryTileViewModel("Microsoft Movies & TV", "\uE714", System.Windows.Media.Color.FromRgb(0, 154, 0), tileSize, tileSize, firstX + step, secondY, null, "Assets/Tiles/myapps-movies-and-tv.png"));
		foreach (GameCardViewModel app in Games.Where((GameCardViewModel game) => IsAppEntry(game.Game) && !IsBuiltInYouTubeEntry(game.Game)).OrderBy((GameCardViewModel game) => game.Title, StringComparer.CurrentCultureIgnoreCase))
		{
			int appIndex = AppLibraryTiles.Count - 5;
			int row = appIndex % 2 == 0 ? 1 : 0;
			int column = 2 + (appIndex + 1) / 2;
			double left = firstX + step * column;
			double top = firstY + step * row;
			string iconPath = app.CoverArtPath;
			AppLibraryTiles.Add(new AppLibraryTileViewModel(app.Title, "\uE7C3", System.Windows.Media.Color.FromRgb(0, 154, 0), tileSize, tileSize, left, top, null, iconPath, app));
		}
		AppLibraryTileViewModel? selectedTile = null;
		if (!string.IsNullOrWhiteSpace(selectedId))
		{
			selectedTile = AppLibraryTiles.FirstOrDefault((AppLibraryTileViewModel tile) => string.Equals(tile.Game?.Game.Id, selectedId, StringComparison.OrdinalIgnoreCase));
		}
		if (selectedTile == null && !string.IsNullOrWhiteSpace(selectedTitle))
		{
			selectedTile = AppLibraryTiles.FirstOrDefault((AppLibraryTileViewModel tile) => string.Equals(tile.Title, selectedTitle, StringComparison.OrdinalIgnoreCase));
		}
		SelectedAppLibraryTile = selectedTile ?? AppLibraryTiles.FirstOrDefault();
		OnPropertyChanged("LibraryMenuCountText");
		OnPropertyChanged("AppLibraryCanvasWidth");
	}

	private void OpenMyGames()
	{
		OpenLibraryMenu(showPins: false, showApps: false);
	}

	private void OpenMyApps()
	{
		OpenLibraryMenu(showPins: false, showApps: true);
	}

	private void OpenMyPins()
	{
		OpenLibraryMenu(showPins: true, showApps: false);
	}

	private void OpenLibraryMenu(bool showPins, bool showApps)
	{
		_isLibraryShowingPins = showPins;
		_isLibraryShowingApps = showApps;
		List<GameCardViewModel> list = GetLibraryMenuGamesForMode(showPins, showApps).ToList();
		if (showApps)
		{
			SelectedAppLibraryTile = AppLibraryTiles.FirstOrDefault();
			SelectedGame = null;
			_libraryMenuStartIndex = 0;
		}
		else if (list.Count > 0 && !showPins)
		{
			SelectedGame = list.FirstOrDefault();
			_libraryMenuStartIndex = 0;
		}
		else if (list.Count > 0 && (SelectedGame == null || !list.Contains(SelectedGame)))
		{
			SelectedGame = list.FirstOrDefault();
			_libraryMenuStartIndex = 0;
		}
		else if (list.Count == 0)
		{
			SelectedGame = null;
			_libraryMenuStartIndex = 0;
		}
		IsMyGamesOpen = true;
		IsLauncherSettingsOpen = false;
		IsProfileEditorOpen = false;
		IsThemeMenuOpen = false;
		IsThemeCreatorOpen = false;
		IsSteamSetupOpen = false;
		IsMusicPlayerOpen = false;
		IsQuickMenuOpen = false;
		IsDetailsOpen = false;
		OnPropertyChanged("LibraryMenuTitle");
		OnPropertyChanged("LibraryMenuFilterText");
		OnPropertyChanged("IsLibraryShowingApps");
		OnPropertyChanged("IsLibraryShowingGameCovers");
		OnPropertyChanged("SelectedAppLibraryTileTitle");
		OnPropertyChanged("LibraryMenuSelectedTitle");
		OnPropertyChanged("LibraryMenuGames");
		RefreshVisibleLibraryMenuGames();
		OnPropertyChanged("LibraryMenuCountText");
		OnPropertyChanged("LibraryMenuXHintText");
		OnPropertyChanged("HasLibraryMenuXHint");
		OnPropertyChanged("CurrentThemeBackgroundPath");
		_audioService.Play("menu-in");
	}

	private void OpenLauncherSettings()
	{
		CloseBladesSubmenu(playSound: false);
		EnsureSelectedSetupItem();
		IsLauncherSettingsOpen = true;
		IsMyGamesOpen = false;
		IsProfileEditorOpen = false;
		IsThemeMenuOpen = false;
		IsSteamSetupOpen = false;
		IsMusicPlayerOpen = false;
		IsQuickMenuOpen = false;
		IsDetailsOpen = false;
		OnPropertyChanged("CurrentThemeBackgroundPath");
		_audioService.Play("menu-in");
	}

	private void OpenProfileEditor()
	{
		IsProfileMenuEditing = false;
		RefreshEditableProfileFields();
		IsProfileEditorOpen = true;
		IsMyGamesOpen = false;
		IsLauncherSettingsOpen = false;
		IsThemeMenuOpen = false;
		IsThemeCreatorOpen = false;
		IsSteamSetupOpen = false;
		IsMusicPlayerOpen = false;
		IsQuickMenuOpen = false;
		IsDetailsOpen = false;
		OnPropertyChanged("CurrentThemeBackgroundPath");
		_audioService.Play("menu-in");
	}

	private void OpenThemeMenu()
	{
		IsThemeMenuOpen = true;
		IsThemeCreatorOpen = false;
		IsDashboardCustomizerOpen = false;
		IsSteamSetupOpen = false;
		IsMyGamesOpen = false;
		IsLauncherSettingsOpen = false;
		IsProfileEditorOpen = false;
		IsMusicPlayerOpen = false;
		IsQuickMenuOpen = false;
		IsDetailsOpen = false;
		_audioService.Play("menu-in");
	}

	private void OpenThemeCreator()
	{
		IsThemeCreatorOpen = true;
		IsDashboardCustomizerOpen = false;
		IsThemeMenuOpen = false;
		IsSteamSetupOpen = false;
		IsMyGamesOpen = false;
		IsProfileEditorOpen = false;
		IsMusicPlayerOpen = false;
		IsQuickMenuOpen = false;
		IsDetailsOpen = false;
		if (!IsLauncherSettingsOpen)
		{
			IsLauncherSettingsOpen = true;
		}
		ResetPendingThemeDraft();
		_audioService.Play("menu-in");
	}

	private void CloseThemeCreator()
	{
		IsThemeCreatorOpen = false;
		_audioService.Play("menu-out");
	}

	private void OpenDashboardCustomizer()
	{
		IsDashboardCustomizerOpen = true;
		IsThemeCreatorOpen = false;
		IsThemeMenuOpen = false;
		IsSteamSetupOpen = false;
		IsMyGamesOpen = false;
		IsProfileEditorOpen = false;
		IsMusicPlayerOpen = false;
		IsQuickMenuOpen = false;
		IsDetailsOpen = false;
		if (!IsLauncherSettingsOpen)
		{
			IsLauncherSettingsOpen = true;
		}
		_customizerTabIndex = Math.Clamp(_customizerTabIndex, 0, Math.Max(0, DashboardCustomizationTabs.Count - 1));
		SelectDefaultDashboardCustomizerTile();
		OnPropertyChanged("CurrentDashboardCustomizationTab");
		OnPropertyChanged("CurrentDashboardCustomizationTabName");
		_audioService.Play("menu-in");
	}

	private void CloseDashboardCustomizer()
	{
		IsDashboardCustomizerOpen = false;
		_audioService.Play("menu-out");
	}

	private void OpenMusicPlayer(bool transparent = false, bool playSound = true)
	{
		CloseBladesSubmenu(playSound: false);
		_closeMusicPlayerOnBack = false;
		IsMusicPlayerTransparent = transparent;
		IsMusicVisualizerFullscreen = false;
		OpenMusicSourceBrowser();
		IsMusicPlayerOpen = true;
		IsMyGamesOpen = false;
		IsLauncherSettingsOpen = false;
		IsProfileEditorOpen = false;
		IsThemeMenuOpen = false;
		IsThemeCreatorOpen = false;
		IsSteamSetupOpen = false;
		IsQuickMenuOpen = false;
		IsDetailsOpen = false;
		OnPropertyChanged("CurrentThemeBackgroundPath");
		_audioService.Play("menu-in");
	}

	private void OpenMusicNowPlaying(bool transparent = false, bool closeOnBack = false)
	{
		_closeMusicPlayerOnBack = closeOnBack;
		IsMusicPlayerTransparent = transparent;
		IsMusicVisualizerFullscreen = false;
		EnsureMusicLibraryLoaded();
		SetMusicBrowserScreen("NowPlaying");
		IsMusicPlayerOpen = true;
		IsMyGamesOpen = false;
		IsLauncherSettingsOpen = false;
		IsProfileEditorOpen = false;
		IsThemeMenuOpen = false;
		IsThemeCreatorOpen = false;
		IsSteamSetupOpen = false;
		IsQuickMenuOpen = false;
		IsDetailsOpen = false;
		OnPropertyChanged("CurrentThemeBackgroundPath");
		_audioService.Play("menu-in");
	}

	private void SetMusicBrowserScreen(string screen)
	{
		_musicBrowserScreen = screen;
		OnPropertyChanged("IsMusicNowPlayingScreen");
		OnPropertyChanged("IsMusicSourceBrowser");
		OnPropertyChanged("ShowMusicBrowserMenuArrows");
		OnPropertyChanged("IsMusicHardDriveBrowser");
		OnPropertyChanged("IsMusicSpotifyBrowser");
		OnPropertyChanged("IsMusicFolderLinkVisible");
		OnPropertyChanged("IsMusicBrowserVisible");
		OnPropertyChanged("IsMusicNowPlayingVisible");
		OnPropertyChanged("MusicPlayerHeaderText");
	}

	private void OpenMusicSourceBrowser()
	{
		MusicBrowserResultItems.Clear();
		MusicBrowserMenuItems.Clear();
		MusicBrowserMenuItems.Add(new MusicBrowserMenuItemViewModel("hard-drive", "Hard Drive", "\uE88F", "Listen to music stored in your dashboard music folder.", iconPath: GetMusicBrowserIconPath("Hard Drive.png")));
		MusicBrowserMenuItems.Add(new MusicBrowserMenuItemViewModel("spotify", "Spotify", "\uE189", "Show imported Spotify playlists and songs without opening Spotify.", iconPath: GetMusicBrowserIconPath("Spotify.png")));
		SelectedMusicBrowserResultItem = null;
		IsMusicBrowserResultsFocused = false;
		_isSpotifyMusicMode = false;
		_isSpotifyAppControlMode = false;
		SelectedMusicBrowserMenuItem = MusicBrowserMenuItems.FirstOrDefault();
		SetMusicBrowserScreen("Source");
		OnPropertyChanged("MusicBrowserResultCountText");
		OnPropertyChanged("HasMusicBrowserResults");
	}

	private void OpenHardDriveBrowser()
	{
		MusicBrowserResultItems.Clear();
		MusicBrowserMenuItems.Clear();
		MusicBrowserMenuItems.Add(new MusicBrowserMenuItemViewModel("saved-playlists", "Saved Playlists", "\uE142", "Choose a folder from your Music Files folder.", iconPath: GetMusicBrowserIconPath("saved playlists.png")));
		MusicBrowserMenuItems.Add(new MusicBrowserMenuItemViewModel("songs", "Songs", "\uE189", "Browse every song in Music Files, including songs inside playlist folders.", iconPath: GetMusicBrowserIconPath("songs.png")));
		SelectedMusicBrowserResultItem = null;
		IsMusicBrowserResultsFocused = false;
		SelectedMusicBrowserMenuItem = MusicBrowserMenuItems.FirstOrDefault();
		SetMusicBrowserScreen("HardDrive");
		RefreshMusicBrowserResults();
	}

	private void OpenSpotifyBrowser()
	{
		MusicBrowserResultItems.Clear();
		MusicBrowserMenuItems.Clear();
		MusicBrowserMenuItems.Add(new MusicBrowserMenuItemViewModel("spotify-now-playing", "Now Playing", "\uE189", "Control Spotify through Windows media controls without opening another setup screen.", iconPath: GetMusicBrowserIconPath("songs.png")));
		SelectedMusicBrowserResultItem = null;
		IsMusicBrowserResultsFocused = false;
		_isSpotifyMusicMode = false;
		_isSpotifyAppControlMode = false;
		SelectedMusicBrowserMenuItem = MusicBrowserMenuItems.FirstOrDefault();
		SetMusicBrowserScreen("Spotify");
		RefreshMusicBrowserResults();
	}

	private void SelectMusicBrowserMenuItem(MusicBrowserMenuItemViewModel? item)
	{
		if (item == null || !item.IsEnabled)
		{
			return;
		}
		SelectedMusicBrowserMenuItem = item;
		if (string.Equals(_musicBrowserScreen, "Source", StringComparison.OrdinalIgnoreCase))
		{
			if (string.Equals(item.Key, "hard-drive", StringComparison.OrdinalIgnoreCase))
			{
				OpenHardDriveBrowser();
				_audioService.Play("select");
			}
			else if (string.Equals(item.Key, "spotify", StringComparison.OrdinalIgnoreCase))
			{
				OpenSpotifyBrowser();
				_audioService.Play("select");
			}
			return;
		}
		if (IsMusicSpotifyBrowser && HandleSpotifyMenuItemAction(item.Key))
		{
			return;
		}
		RefreshMusicBrowserResults();
		EnterMusicBrowserResults();
		_audioService.Play("select");
	}

	private void RefreshMusicBrowserResults()
	{
		MusicBrowserResultItems.Clear();
		SelectedMusicBrowserResultItem = null;
		IsMusicBrowserResultsFocused = false;
		string key = SelectedMusicBrowserMenuItem?.Key ?? string.Empty;
		string musicFolder = GetMusicFolder();
		if (string.Equals(key, "saved-playlists", StringComparison.OrdinalIgnoreCase))
		{
			foreach (string folder in Directory.EnumerateDirectories(musicFolder).OrderBy(Path.GetFileName, StringComparer.CurrentCultureIgnoreCase))
			{
				List<string> tracks = EnumerateMusicFiles(folder, SearchOption.AllDirectories);
				if (tracks.Count == 0)
				{
					continue;
				}
				MusicBrowserResultItems.Add(new MusicBrowserResultItemViewModel(Path.GetFileName(folder), $"{tracks.Count} song{(tracks.Count == 1 ? string.Empty : "s")}", "playlist", folder, tracks));
			}
		}
		else if (string.Equals(key, "songs", StringComparison.OrdinalIgnoreCase))
		{
			foreach (string song in EnumerateMusicFiles(musicFolder, SearchOption.AllDirectories))
			{
				MusicBrowserResultItems.Add(new MusicBrowserResultItemViewModel(Path.GetFileNameWithoutExtension(song).Replace('_', ' '), GetMusicRelativeFolder(song, musicFolder), "song", song, new[] { song }));
			}
		}
		else if (key.StartsWith("spotify-", StringComparison.OrdinalIgnoreCase))
		{
			AddSpotifyBrowserResults(key);
		}
		OnPropertyChanged("MusicBrowserResultCountText");
		OnPropertyChanged("HasMusicBrowserResults");
		OnPropertyChanged("IsMusicFolderLinkVisible");
	}

	private void AddSpotifyBrowserResults(string key)
	{
		if (!key.Equals("spotify-mirror-legacy", StringComparison.OrdinalIgnoreCase))
		{
			return;
		}
		List<SpotifyMirrorPlaylist> playlists = LoadSpotifyMirrorPlaylists();
		List<SpotifyMirrorSong> songs = playlists.SelectMany((SpotifyMirrorPlaylist playlist) => playlist.Songs).ToList();
		if (string.Equals(key, "spotify-albums", StringComparison.OrdinalIgnoreCase))
		{
			foreach (IGrouping<string, SpotifyMirrorSong> album in songs.Where((SpotifyMirrorSong song) => !string.IsNullOrWhiteSpace(song.Album)).GroupBy((SpotifyMirrorSong song) => song.Album.Trim(), StringComparer.CurrentCultureIgnoreCase).OrderBy((IGrouping<string, SpotifyMirrorSong> album) => album.Key, StringComparer.CurrentCultureIgnoreCase))
			{
				AddSpotifyMirrorResult(album.Key, $"{album.Count()} song{(album.Count() == 1 ? string.Empty : "s")}", "spotify-album", album);
			}
		}
		else if (string.Equals(key, "spotify-artists", StringComparison.OrdinalIgnoreCase))
		{
			foreach (IGrouping<string, SpotifyMirrorSong> artist in songs.Where((SpotifyMirrorSong song) => !string.IsNullOrWhiteSpace(song.Artist)).GroupBy((SpotifyMirrorSong song) => song.Artist.Trim(), StringComparer.CurrentCultureIgnoreCase).OrderBy((IGrouping<string, SpotifyMirrorSong> artist) => artist.Key, StringComparer.CurrentCultureIgnoreCase))
			{
				AddSpotifyMirrorResult(artist.Key, $"{artist.Count()} song{(artist.Count() == 1 ? string.Empty : "s")}", "spotify-artist", artist);
			}
		}
		else if (string.Equals(key, "spotify-playlists", StringComparison.OrdinalIgnoreCase))
		{
			foreach (SpotifyMirrorPlaylist playlist in playlists.OrderBy((SpotifyMirrorPlaylist playlist) => playlist.Name, StringComparer.CurrentCultureIgnoreCase))
			{
				AddSpotifyMirrorResult(playlist.Name, $"{playlist.Songs.Count} song{(playlist.Songs.Count == 1 ? string.Empty : "s")}", "spotify-playlist", playlist.Songs);
			}
		}
		else if (string.Equals(key, "spotify-songs", StringComparison.OrdinalIgnoreCase))
		{
			foreach (SpotifyMirrorSong song in songs.OrderBy((SpotifyMirrorSong song) => song.DisplayTitle, StringComparer.CurrentCultureIgnoreCase))
			{
				AddSpotifyMirrorResult(song.DisplayTitle, song.Subtitle, "spotify-song", new[] { song });
			}
		}
		else if (string.Equals(key, "spotify-genres", StringComparison.OrdinalIgnoreCase))
		{
			foreach (IGrouping<string, SpotifyMirrorSong> genre in songs.Where((SpotifyMirrorSong song) => !string.IsNullOrWhiteSpace(song.Genre)).GroupBy((SpotifyMirrorSong song) => song.Genre.Trim(), StringComparer.CurrentCultureIgnoreCase).OrderBy((IGrouping<string, SpotifyMirrorSong> genre) => genre.Key, StringComparer.CurrentCultureIgnoreCase))
			{
				AddSpotifyMirrorResult(genre.Key, $"{genre.Count()} song{(genre.Count() == 1 ? string.Empty : "s")}", "spotify-genre", genre);
			}
		}
	}

	private void AddSpotifyMirrorResult(string title, string subtitle, string kind, IEnumerable<SpotifyMirrorSong> songs)
	{
		List<SpotifyMirrorSong> songList = songs.ToList();
		MusicBrowserResultItems.Add(new MusicBrowserResultItemViewModel(title, subtitle, kind, SpotifyMirrorScheme + Uri.EscapeDataString(title), songList.Select((SpotifyMirrorSong song) => song.ToTrackPath())));
	}

	private bool HandleSpotifyMenuItemAction(string key)
	{
		if (string.Equals(key, "spotify-now-playing", StringComparison.OrdinalIgnoreCase))
		{
			OpenSpotifyNowPlaying();
			_audioService.Play("select");
			return true;
		}
		return false;
	}

	private void OpenSpotifyNowPlaying()
	{
		StopMusic();
		_isSpotifyMusicMode = true;
		_isSpotifyAppControlMode = true;
		MusicTracks.Clear();
		MusicTracks.Add(new MusicTrackViewModel("Spotify", "spotify-control://now-playing"));
		_musicIndex = 0;
		CurrentMusicTrack = MusicTracks[0];
		SelectedMusicTrack = CurrentMusicTrack;
		MusicPositionText = "0:00";
		MusicDurationText = "0:00";
		MusicProgress = 0.0;
		IsMusicPlaying = false;
		_musicTimer.Start();
		SetMusicBrowserScreen("NowPlaying");
		StatusMessage = "Controlling Spotify";
		OnPropertyChanged("MusicTrackCountText");
		_ = RefreshSpotifyNowPlayingAsync(250);
	}

	private async Task AddSpotifyPlaylistShortcutFromClipboardAsync()
	{
		string clipboard = GetClipboardText().Trim();
		if (!IsSpotifyPlaylistLink(clipboard))
		{
			StatusMessage = "Copy a Spotify playlist link first";
			return;
		}
		string playlistUri = NormalizeSpotifyPlaylistUri(clipboard);
		string name = await FetchSpotifyPlaylistShortcutNameAsync(playlistUri);
		SaveSpotifyPlaylistShortcut(new SpotifyPlaylistShortcut(name, playlistUri));
		StatusMessage = "Added Spotify playlist " + name;
		RefreshMusicBrowserResults();
	}

	private static bool IsSpotifyPlaylistLink(string value)
	{
		return value.StartsWith("spotify:playlist:", StringComparison.OrdinalIgnoreCase) || value.Contains("open.spotify.com/playlist/", StringComparison.OrdinalIgnoreCase);
	}

	private static string CreateSpotifyPlaylistShortcutName(string url)
	{
		string id = url;
		int index = id.IndexOf("/playlist/", StringComparison.OrdinalIgnoreCase);
		if (index >= 0)
		{
			id = id.Substring(index + "/playlist/".Length);
		}
		else if (id.StartsWith("spotify:playlist:", StringComparison.OrdinalIgnoreCase))
		{
			id = id.Substring("spotify:playlist:".Length);
		}
		int queryIndex = id.IndexOfAny(new[] { '?', '&', '/' });
		if (queryIndex >= 0)
		{
			id = id.Substring(0, queryIndex);
		}
		if (id.Length > 8)
		{
			id = id.Substring(0, 8);
		}
		return "Spotify Playlist " + id;
	}

	private static string NormalizeSpotifyPlaylistUri(string value)
	{
		string trimmed = value.Trim();
		if (trimmed.StartsWith("spotify:playlist:", StringComparison.OrdinalIgnoreCase))
		{
			return trimmed;
		}
		int index = trimmed.IndexOf("/playlist/", StringComparison.OrdinalIgnoreCase);
		if (index < 0)
		{
			return trimmed;
		}
		string id = trimmed.Substring(index + "/playlist/".Length);
		int queryIndex = id.IndexOfAny(new[] { '?', '&', '/' });
		if (queryIndex >= 0)
		{
			id = id.Substring(0, queryIndex);
		}
		return string.IsNullOrWhiteSpace(id) ? trimmed : "spotify:playlist:" + id;
	}

	private static string ExtractSpotifyPlaylistId(string value)
	{
		string normalized = NormalizeSpotifyPlaylistUri(value);
		if (!normalized.StartsWith("spotify:playlist:", StringComparison.OrdinalIgnoreCase))
		{
			return string.Empty;
		}
		string id = normalized.Substring("spotify:playlist:".Length);
		int queryIndex = id.IndexOfAny(new[] { '?', '&', '/' });
		if (queryIndex >= 0)
		{
			id = id.Substring(0, queryIndex);
		}
		return id.Trim();
	}

	private static bool IsGeneratedSpotifyPlaylistName(string name, string uri)
	{
		return string.Equals(name.Trim(), CreateSpotifyPlaylistShortcutName(uri), StringComparison.OrdinalIgnoreCase);
	}

	private static async Task<string> FetchSpotifyPlaylistShortcutNameAsync(string playlistUri)
	{
		string fallback = CreateSpotifyPlaylistShortcutName(playlistUri);
		string id = ExtractSpotifyPlaylistId(playlistUri);
		if (string.IsNullOrWhiteSpace(id))
		{
			return fallback;
		}
		try
		{
			string openUrl = "https://open.spotify.com/playlist/" + Uri.EscapeDataString(id);
			string requestUrl = "https://open.spotify.com/oembed?url=" + Uri.EscapeDataString(openUrl);
			using HttpResponseMessage response = await SpotifyHttpClient.GetAsync(requestUrl);
			if (!response.IsSuccessStatusCode)
			{
				return fallback;
			}
			using Stream stream = await response.Content.ReadAsStreamAsync();
			using JsonDocument document = await JsonDocument.ParseAsync(stream);
			if (document.RootElement.TryGetProperty("title", out JsonElement titleElement))
			{
				string? title = titleElement.GetString();
				if (!string.IsNullOrWhiteSpace(title))
				{
					return CleanSpotifyPlaylistTitle(title);
				}
			}
		}
		catch (Exception ex)
		{
			App.LogException(ex, "DashboardViewModel.FetchSpotifyPlaylistShortcutNameAsync");
		}
		return fallback;
	}

	private static string CleanSpotifyPlaylistTitle(string title)
	{
		string cleaned = WebUtility.HtmlDecode(title).Trim();
		string[] suffixes = new[] { " - playlist by ", " playlist by " };
		foreach (string suffix in suffixes)
		{
			int index = cleaned.IndexOf(suffix, StringComparison.OrdinalIgnoreCase);
			if (index > 0)
			{
				cleaned = cleaned.Substring(0, index).Trim();
			}
		}
		return string.IsNullOrWhiteSpace(cleaned) ? "Spotify Playlist" : cleaned;
	}

	public bool HandleMusicBrowserInput(DashboardInputAction action)
	{
		if (!IsMusicPlayerOpen || IsMusicVisualizerFullscreen)
		{
			return false;
		}
		if (IsMusicNowPlayingScreen)
		{
			return HandleMusicNowPlayingInput(action);
		}
		if (action == DashboardInputAction.MoveUp || action == DashboardInputAction.MoveDown)
		{
			if (IsMusicBrowserResultsFocused)
			{
				MoveMusicBrowserResultSelection(action == DashboardInputAction.MoveDown ? 1 : -1);
			}
			else
			{
				MoveMusicBrowserMenuSelection(action == DashboardInputAction.MoveDown ? 1 : -1);
			}
			_audioService.Play("hover");
			return true;
		}
		if (action == DashboardInputAction.MoveLeft || action == DashboardInputAction.MoveRight)
		{
			return true;
		}
		if (action == DashboardInputAction.Activate)
		{
			if (IsMusicBrowserResultsFocused)
			{
				OpenMusicBrowserResult(SelectedMusicBrowserResultItem);
				return true;
			}
			if (IsMusicSourceBrowser)
			{
				if (string.Equals(SelectedMusicBrowserMenuItem?.Key, "hard-drive", StringComparison.OrdinalIgnoreCase))
				{
					OpenHardDriveBrowser();
					_audioService.Play("select");
				}
				else if (string.Equals(SelectedMusicBrowserMenuItem?.Key, "spotify", StringComparison.OrdinalIgnoreCase))
				{
					OpenSpotifyBrowser();
					_audioService.Play("select");
				}
				return true;
			}
			if (IsMusicHardDriveBrowser || IsMusicSpotifyBrowser)
			{
				EnterMusicBrowserResults();
				_audioService.Play("select");
				return true;
			}
		}
		if (action == DashboardInputAction.Back && IsMusicBrowserResultsFocused)
		{
			ExitMusicBrowserResults();
			_audioService.Play("menu-out");
			return true;
		}
		return false;
	}

	private bool HandleMusicNowPlayingInput(DashboardInputAction action)
	{
		if (action == DashboardInputAction.MoveUp || action == DashboardInputAction.MoveDown)
		{
			MoveSelectedMusicTrack(action == DashboardInputAction.MoveDown ? 1 : -1);
			_audioService.Play("hover");
			return true;
		}
		if (action == DashboardInputAction.Activate)
		{
			PlayMusicTrack(SelectedMusicTrack ?? CurrentMusicTrack ?? MusicTracks.FirstOrDefault());
			_audioService.Play("select");
			return true;
		}
		if (action == DashboardInputAction.MoveLeft || action == DashboardInputAction.MoveRight)
		{
			return false;
		}
		return false;
	}

	private void MoveSelectedMusicTrack(int delta)
	{
		if (MusicTracks.Count == 0)
		{
			SelectedMusicTrack = null;
			return;
		}
		int currentIndex = MusicTracks.IndexOf(SelectedMusicTrack);
		if (currentIndex < 0)
		{
			currentIndex = Math.Max(0, MusicTracks.IndexOf(CurrentMusicTrack));
		}
		int nextIndex = Math.Clamp(currentIndex + delta, 0, MusicTracks.Count - 1);
		SelectedMusicTrack = MusicTracks[nextIndex];
	}

	private void MoveMusicBrowserMenuSelection(int delta)
	{
		if (MusicBrowserMenuItems.Count == 0)
		{
			return;
		}
		int currentIndex = Math.Max(0, MusicBrowserMenuItems.IndexOf(SelectedMusicBrowserMenuItem));
		int nextIndex = Math.Clamp(currentIndex + delta, 0, MusicBrowserMenuItems.Count - 1);
		if (nextIndex == currentIndex)
		{
			return;
		}
		SelectedMusicBrowserMenuItem = MusicBrowserMenuItems[nextIndex];
		if (IsMusicHardDriveBrowser || IsMusicSpotifyBrowser)
		{
			RefreshMusicBrowserResults();
		}
	}

	private void EnterMusicBrowserResults()
	{
		if ((!IsMusicHardDriveBrowser && !IsMusicSpotifyBrowser) || MusicBrowserResultItems.Count == 0)
		{
			return;
		}
		IsMusicBrowserResultsFocused = true;
		SelectedMusicBrowserResultItem = MusicBrowserResultItems[0];
	}

	private void ExitMusicBrowserResults()
	{
		IsMusicBrowserResultsFocused = false;
		SelectedMusicBrowserResultItem = null;
	}

	private void MoveMusicBrowserResultSelection(int delta)
	{
		if (MusicBrowserResultItems.Count == 0)
		{
			return;
		}
		int currentIndex = MusicBrowserResultItems.IndexOf(SelectedMusicBrowserResultItem);
		if (currentIndex < 0)
		{
			currentIndex = 0;
		}
		int nextIndex = Math.Clamp(currentIndex + delta, 0, MusicBrowserResultItems.Count - 1);
		SelectedMusicBrowserResultItem = MusicBrowserResultItems[nextIndex];
	}

	private static string GetMusicRelativeFolder(string songPath, string musicFolder)
	{
		string? directoryName = Path.GetDirectoryName(songPath);
		if (string.IsNullOrWhiteSpace(directoryName) || string.Equals(directoryName, musicFolder, StringComparison.OrdinalIgnoreCase))
		{
			return "Music Files";
		}
		return Path.GetRelativePath(musicFolder, directoryName);
	}

	private void OpenMusicBrowserResult(MusicBrowserResultItemViewModel? item)
	{
		if (item == null || item.TrackPaths.Count == 0)
		{
			return;
		}
		if (string.Equals(item.Kind, "spotify-shortcut", StringComparison.OrdinalIgnoreCase))
		{
			OpenSpotifyUri(item.Path);
			StopMusic();
			_isSpotifyMusicMode = true;
			_isSpotifyAppControlMode = true;
			MusicTracks.Clear();
			MusicTracks.Add(new MusicTrackViewModel(item.Title, item.Path));
			_musicIndex = 0;
			CurrentMusicTrack = MusicTracks[0];
			SelectedMusicTrack = CurrentMusicTrack;
			MusicPositionText = "0:00";
			MusicDurationText = "0:00";
			MusicProgress = 0.0;
			IsMusicPlaying = true;
			_musicTimer.Start();
			SetMusicBrowserScreen("NowPlaying");
			StatusMessage = "Playing Spotify playlist " + item.Title;
			_audioService.Play("select");
			OnPropertyChanged("MusicTrackCountText");
			_ = RefreshSpotifyNowPlayingAsync(1200);
			return;
		}
		if (item.Kind.StartsWith("spotify-", StringComparison.OrdinalIgnoreCase))
		{
			LoadSpotifyMirrorQueue(item);
			SetMusicBrowserScreen("NowPlaying");
			StatusMessage = "Loaded Spotify mirror " + item.Title;
			_audioService.Play("select");
			return;
		}
		if (string.Equals(item.Kind, "playlist", StringComparison.OrdinalIgnoreCase))
		{
			StopMusic();
			LoadMusicQueue(item.TrackPaths);
			SetMusicBrowserScreen("NowPlaying");
			StatusMessage = "Loaded playlist " + item.Title;
			_audioService.Play("select");
			return;
		}
		LoadMusicQueue(EnumerateMusicFiles(GetMusicFolder(), SearchOption.AllDirectories), item.Path);
		SetMusicBrowserScreen("NowPlaying");
		PlayMusicTrack(MusicTracks.FirstOrDefault((MusicTrackViewModel track) => string.Equals(track.Path, item.Path, StringComparison.OrdinalIgnoreCase)));
		_audioService.Play("select");
	}

	private void LoadSpotifyMirrorQueue(MusicBrowserResultItemViewModel selectedItem)
	{
		StopMusic();
		_isSpotifyMusicMode = true;
		_isSpotifyAppControlMode = false;
		MusicTracks.Clear();
		foreach (string trackPath in selectedItem.TrackPaths)
		{
			MusicTracks.Add(CreateSpotifyMirrorTrack(trackPath));
		}
		_musicIndex = MusicTracks.Count > 0 ? 0 : -1;
		CurrentMusicTrack = MusicTracks.ElementAtOrDefault(_musicIndex);
		SelectedMusicTrack = CurrentMusicTrack;
		MusicPositionText = "0:00";
		MusicDurationText = "0:00";
		MusicProgress = 0.0;
		IsMusicPlaying = false;
		OnPropertyChanged("MusicTrackCountText");
	}

	private static MusicTrackViewModel CreateSpotifyMirrorTrack(string trackPath)
	{
		if (TryDecodeSpotifyMirrorSong(trackPath, out SpotifyMirrorSong song))
		{
			return new MusicTrackViewModel(song.DisplayTitle, trackPath);
		}
		return new MusicTrackViewModel(trackPath);
	}

	private void CloseMusicPlayer()
	{
		IsMusicVisualizerFullscreen = false;
		IsMusicPlayerOpen = false;
		IsMusicPlayerTransparent = false;
		_closeMusicPlayerOnBack = false;
		OnPropertyChanged("CurrentThemeBackgroundPath");
		_audioService.Play("menu-out");
	}

	private void OpenMusicFolder()
	{
		string musicFolder = GetMusicFolder();
		Process.Start(new ProcessStartInfo
		{
			FileName = "explorer.exe",
			Arguments = "\"" + musicFolder + "\"",
			UseShellExecute = true
		});
	}

	private void OpenMusicVisualizerFullscreen()
	{
		if (IsMusicPlayerOpen && !IsMusicVisualizerFullscreen)
		{
			IsMusicVisualizerFullscreen = true;
			_audioService.Play("select");
		}
	}

	private async Task LaunchGameAsync(GameCardViewModel? card)
	{
		if (card == null)
		{
			card = SelectedGame;
		}
		if (card == null)
		{
			StatusMessage = "No game selected";
			return;
		}
		try
		{
			SelectedGame = card;
			_runningGameService.BeginLaunch(card.Game, DateTimeOffset.UtcNow);
			if (Settings.MinimizeOnGameLaunch)
			{
				Window window = System.Windows.Application.Current?.MainWindow;
				if (window != null)
				{
					window.WindowState = WindowState.Minimized;
				}
			}
			GameLaunchResult gameLaunchResult = await _launchService.LaunchAsync(card.Game);
			_runningGameService.Track(card.Game, gameLaunchResult.TrackedProcess);
			await BringLaunchedGameToForegroundAsync(gameLaunchResult.TrackedProcess);
			card.Game.LastPlayed = DateTimeOffset.Now;
			if (card.Game.Playtime < TimeSpan.Zero)
			{
				card.Game.Playtime = TimeSpan.Zero;
			}
			await PersistLibraryAsync();
			StatusMessage = "Launching " + card.Title;
			_audioService.Play("select");
		}
		catch (Exception ex)
		{
			_runningGameService.Clear();
			StatusMessage = ex.Message;
		}
	}

	private static async Task BringLaunchedGameToForegroundAsync(Process? process)
	{
		if (process == null)
		{
			return;
		}
		for (int attempt = 0; attempt < 10; attempt++)
		{
			try
			{
				if (process.HasExited)
				{
					break;
				}
				process.Refresh();
				nint mainWindowHandle = process.MainWindowHandle;
				if (mainWindowHandle != IntPtr.Zero)
				{
					ShowWindow(mainWindowHandle, 9);
					SetForegroundWindow(mainWindowHandle);
					break;
				}
			}
			catch
			{
				break;
			}
			await Task.Delay(250);
		}
	}

	private async Task SubmitSearchAsync()
	{
		if (string.IsNullOrWhiteSpace(SearchQuery))
		{
			StatusMessage = "Type a Bing search first";
			return;
		}
		await _searchService.SearchWebAsync(SearchQuery, Settings.BingSearchBaseUrl);
		StatusMessage = "Searching Bing for " + SearchQuery;
		CloseSearchOverlay(playSound: false);
		_audioService.Play("select");
	}

	private void OpenSearch()
	{
		if (CurrentTab?.Key != "bing")
		{
			_suppressNextTabSound = true;
		}
		CurrentTab = Tabs.First((DashboardTabViewModel tab) => tab.Key == "bing");
		IsSearchOverlayOpen = true;
		_audioService.Play("menu-in");
	}

	private void RequestFriendsOverlay()
	{
		IsQuickMenuOpen = false;
		IsDetailsOpen = false;
		this.FriendsOverlayRequested?.Invoke(this, EventArgs.Empty);
	}

	private void RequestPartyOverlay()
	{
		IsQuickMenuOpen = false;
		IsDetailsOpen = false;
		this.PartyOverlayRequested?.Invoke(this, EventArgs.Empty);
	}

	public Task<RunningGameCloseResult> CloseRunningGameAsync(bool forceKill, CancellationToken cancellationToken = default(CancellationToken))
	{
		return _runningGameService.CloseAsync(forceKill, cancellationToken);
	}

	private void ToggleQuickMenu()
	{
		IsQuickMenuOpen = !IsQuickMenuOpen;
		_audioService.Play(IsQuickMenuOpen ? "menu-in" : "menu-out");
	}

	private void CloseSearchOverlay(bool playSound)
	{
		IsSearchOverlayOpen = false;
		if (playSound)
		{
			_audioService.Play("menu-out");
		}
	}

	private void CloseDetails(bool playSound)
	{
		IsDetailsOpen = false;
		if (playSound)
		{
			_audioService.Play("menu-out");
		}
	}

	private void CloseLibraryMenu(bool playSound)
	{
		IsMyGamesOpen = false;
		OnPropertyChanged("CurrentThemeBackgroundPath");
		if (playSound)
		{
			_audioService.Play("menu-out");
		}
	}

	private void CloseLauncherSettings(bool playSound)
	{
		IsLauncherSettingsOpen = false;
		OnPropertyChanged("CurrentThemeBackgroundPath");
		if (playSound)
		{
			_audioService.Play("menu-out");
		}
	}

	private void CloseProfileEditor(bool playSound)
	{
		IsProfileMenuEditing = false;
		RefreshEditableProfileFields();
		IsProfileEditorOpen = false;
		if (playSound)
		{
			_audioService.Play("menu-out");
		}
	}

	private void GoBack()
	{
		if (IsYouTubeTvOpen)
		{
			CloseYouTubeTv();
		}
		else if (IsSearchOverlayOpen)
		{
			CloseSearchOverlay(playSound: true);
		}
		else if (IsDetailsOpen)
		{
			if (IsGameDetailsSeeAllOpen)
			{
				IsGameDetailsSeeAllOpen = false;
				_audioService.Play("menu-out");
			}
			else
			{
				CloseDetails(playSound: true);
			}
		}
		else if (IsThemeCreatorOpen)
		{
			IsThemeCreatorOpen = false;
			_audioService.Play("menu-out");
		}
		else if (IsSteamSetupOpen)
		{
			IsSteamSetupOpen = false;
			_audioService.Play("menu-out");
		}
		else if (IsSpotifySetupOpen)
		{
			IsSpotifySetupOpen = false;
			_audioService.Play("menu-out");
		}
		else if (IsDashboardCustomizerOpen)
		{
			CloseDashboardCustomizer();
		}
		else if (IsThemeMenuOpen)
		{
			IsThemeMenuOpen = false;
			_audioService.Play("menu-out");
		}
		else if (IsMyGamesOpen)
		{
			CloseLibraryMenu(playSound: true);
		}
		else if (IsLauncherSettingsOpen)
		{
			CloseLauncherSettings(playSound: true);
		}
		else if (IsProfileEditorOpen)
		{
			CloseProfileEditor(playSound: true);
		}
		else if (IsMusicPlayerOpen)
		{
			if (IsMusicVisualizerFullscreen)
			{
				IsMusicVisualizerFullscreen = false;
			}
			else if (IsMusicBrowserResultsFocused)
			{
				ExitMusicBrowserResults();
			}
			else if (IsMusicNowPlayingScreen)
			{
				if (_closeMusicPlayerOnBack)
				{
					IsMusicVisualizerFullscreen = false;
					IsMusicPlayerOpen = false;
					IsMusicPlayerTransparent = false;
					_closeMusicPlayerOnBack = false;
					OnPropertyChanged("CurrentThemeBackgroundPath");
				}
				else
				{
					if (_isSpotifyMusicMode)
					{
						OpenSpotifyBrowser();
					}
					else
					{
						OpenHardDriveBrowser();
					}
				}
			}
			else if (string.Equals(_musicBrowserScreen, "HardDrive", StringComparison.OrdinalIgnoreCase) || string.Equals(_musicBrowserScreen, "Spotify", StringComparison.OrdinalIgnoreCase))
			{
				OpenMusicSourceBrowser();
			}
			else
			{
				IsMusicPlayerOpen = false;
			}
			_audioService.Play("menu-out");
		}
		else if (IsQuickMenuOpen)
		{
			ToggleQuickMenu();
		}
		else
		{
			if (CurrentTab != Tabs[1])
			{
				_suppressNextTabSound = true;
			}
			CurrentTab = Tabs[1];
			_audioService.Play("menu-out");
		}
	}

	private async Task AddGameAsync()
	{
		string text = _filePickerService.PickExecutable();
		string destination = NormalizeAddDestination(Settings.DefaultAddDestination);
		GameMetadata? game = null;
		if (!string.IsNullOrWhiteSpace(text))
		{
			game = new GameMetadata
			{
				Title = Path.GetFileNameWithoutExtension(text).Replace("_", " "),
				LaunchType = "Exe",
				ExecutablePath = text,
				WorkingDirectory = (Path.GetDirectoryName(text) ?? string.Empty),
				Platform = "PC",
				Genre = ((destination == "My Apps") ? "App" : "Manual")
			};
		}
		else
		{
			string? url = PromptForAddExecutableUrl();
			if (string.IsNullOrWhiteSpace(url))
			{
				return;
			}
			url = NormalizeLaunchUrl(url);
			game = new GameMetadata
			{
				Title = BuildUrlGameTitle(url),
				LaunchType = "Url",
				LaunchCommand = url,
				ExecutablePath = url,
				Platform = "Web",
				Genre = ((destination == "My Apps") ? "App" : "Manual")
			};
		}
		_library.Games.Add(game);
		GameCardViewModel item = new GameCardViewModel(game, _accentBrushes[Games.Count % _accentBrushes.Count]);
		Games.Add(item);
		SortGamesByTitle(game.Id);
		await PersistLibraryAsync();
		OnPropertyChanged("MyGamesCountText");
		RefreshSetupDestinationState();
		StatusMessage = "Added " + game.Title + " to " + destination;
	}

	private static string? PromptForAddExecutableUrl()
	{
		using System.Windows.Forms.Form form = new System.Windows.Forms.Form
		{
			Text = "Add URL",
			Width = 430,
			Height = 150,
			FormBorderStyle = FormBorderStyle.FixedDialog,
			StartPosition = FormStartPosition.CenterScreen,
			MinimizeBox = false,
			MaximizeBox = false
		};
		System.Windows.Forms.Label label = new System.Windows.Forms.Label
		{
			Text = "Enter a URL to add, or leave blank to cancel.",
			Left = 12,
			Top = 12,
			Width = 390
		};
		System.Windows.Forms.TextBox textBox = new System.Windows.Forms.TextBox
		{
			Left = 12,
			Top = 38,
			Width = 390
		};
		System.Windows.Forms.Button okButton = new System.Windows.Forms.Button
		{
			Text = "OK",
			Left = 246,
			Top = 74,
			Width = 75,
			DialogResult = DialogResult.OK
		};
		System.Windows.Forms.Button cancelButton = new System.Windows.Forms.Button
		{
			Text = "Cancel",
			Left = 327,
			Top = 74,
			Width = 75,
			DialogResult = DialogResult.Cancel
		};
		form.Controls.Add(label);
		form.Controls.Add(textBox);
		form.Controls.Add(okButton);
		form.Controls.Add(cancelButton);
		form.AcceptButton = okButton;
		form.CancelButton = cancelButton;
		return form.ShowDialog() == DialogResult.OK ? textBox.Text.Trim() : null;
	}

	private static string NormalizeLaunchUrl(string url)
	{
		string text = url.Trim();
		if (Uri.TryCreate(text, UriKind.Absolute, out Uri? uri) && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps))
		{
			return text;
		}
		return "https://" + text;
	}

	private static string BuildUrlGameTitle(string url)
	{
		if (Uri.TryCreate(url, UriKind.Absolute, out Uri? uri) && !string.IsNullOrWhiteSpace(uri.Host))
		{
			return uri.Host.StartsWith("www.", StringComparison.OrdinalIgnoreCase) ? uri.Host.Substring(4) : uri.Host;
		}
		return url;
	}

	private async Task EditSelectedGameAsync(object? _)
	{
		if (SelectedGame != null)
		{
			string text = _filePickerService.PickExecutable();
			if (!string.IsNullOrWhiteSpace(text))
			{
				SelectedGame.Game.ExecutablePath = text;
				SelectedGame.Game.WorkingDirectory = Path.GetDirectoryName(text) ?? string.Empty;
				await PersistLibraryAsync();
				StatusMessage = "Updated " + SelectedGame.Title;
			}
		}
	}

	private async Task ChooseSelectedHomeImageAsync(object? _)
	{
		if (SelectedGame == null)
		{
			StatusMessage = "Choose a game first";
			return;
		}
		string text = _filePickerService.PickImage(GetCustomCoverFolder("Home Screen Cover"));
		if (!string.IsNullOrWhiteSpace(text))
		{
			SelectedGame.Game.BackgroundArtPath = CopyCustomArtwork(text, "Home Screen Cover", SelectedGame.Title);
			SelectedGame.Refresh();
			if (TrayGame == SelectedGame)
			{
				OnPropertyChanged("OpenTrayCoverArtPath");
			}
			await PersistLibraryAsync();
			StatusMessage = "Updated Home image for " + SelectedGame.Title;
		}
	}

	private async Task ChooseSelectedGameMenuImageAsync(object? _)
	{
		if (SelectedGame == null)
		{
			StatusMessage = "Choose a game first";
			return;
		}
		string text = _filePickerService.PickImage(GetCustomCoverFolder("Game Menu Cover"));
		if (!string.IsNullOrWhiteSpace(text))
		{
			SelectedGame.Game.CoverArtPath = CopyCustomArtwork(text, "Game Menu Cover", SelectedGame.Title);
			SelectedGame.Refresh();
			await PersistLibraryAsync();
			StatusMessage = "Updated My Games image for " + SelectedGame.Title;
		}
	}

	private async Task SaveSelectedGameAsync(object? _)
	{
		if (SelectedGame == null)
		{
			StatusMessage = "Choose a game first";
			return;
		}
		SelectedGame.Refresh();
		if (TrayGame == SelectedGame)
		{
			OnPropertyChanged("OpenTrayTitle");
		}
		SortGamesByTitle(SelectedGame.Game.Id);
		await PersistLibraryAsync();
		StatusMessage = "Saved " + SelectedGame.Title;
	}

	private async Task SetOpenTrayGameAsync(object? _)
	{
		if (SelectedGame == null)
		{
			StatusMessage = "Choose a game first";
			return;
		}
		TrayGame = SelectedGame;
		Settings.OpenTrayGameId = SelectedGame.Game.Id;
		await SaveSettingsAsync();
		StatusMessage = SelectedGame.Title + " is now on Open Tray";
	}

	private async Task RemoveSelectedGameAsync()
	{
		if (SelectedGame == null)
		{
			StatusMessage = "Choose a game first";
			return;
		}
		GameCardViewModel removed = SelectedGame;
		_library.Games.Remove(removed.Game);
		Games.Remove(removed);
		if (string.Equals(Settings.OpenTrayGameId, removed.Game.Id, StringComparison.OrdinalIgnoreCase))
		{
			Settings.OpenTrayGameId = string.Empty;
			TrayGame = null;
			await _settingsService.SaveAsync(Settings);
		}
		SelectedGame = SetupEditableItems.FirstOrDefault() ?? Games.FirstOrDefault();
		FeaturedGame = SelectedGame;
		SortGamesByTitle(SelectedGame?.Game.Id);
		await PersistLibraryAsync();
		RefreshSetupDestinationState();
		StatusMessage = "Removed " + removed.Title + " from " + DefaultAddDestination;
	}

	private async Task ChooseProfilePictureAsync(object? _)
	{
		string text = _filePickerService.PickImage(Path.Combine(AppPaths.AppFolder, "Assets", "Profile"));
		if (!string.IsNullOrWhiteSpace(text))
		{
			Profile = new Profile
			{
				Gamertag = Profile.Gamertag,
				Name = Profile.Name,
				GamerPicturePath = text,
				Gamerscore = Profile.Gamerscore,
				OnlineStatus = Profile.OnlineStatus,
				Motto = Profile.Motto,
				Location = Profile.Location,
				Description = Profile.Description
			};
			await _profileService.SaveAsync(Profile);
			StatusMessage = "Profile picture updated";
		}
	}

	private async Task SaveProfileAsync()
	{
		EnsureProfileDefaults();
		await SaveProfileSilentlyAsync();
		OnPropertyChanged("Profile");
		OnPropertyChanged("TopRightGamerscoreText");
		StatusMessage = "Profile saved";
	}

	private void Profile_OnPropertyChanged(object? sender, PropertyChangedEventArgs e)
	{
		OnPropertyChanged("Profile");
		if (e.PropertyName == nameof(Profile.Gamerscore))
		{
			OnPropertyChanged("TopRightGamerscoreText");
		}
		if (!_suppressProfileAutosave)
		{
			_profileSaveTimer.Stop();
			_profileSaveTimer.Start();
		}
	}

	private async Task SaveProfileSilentlyAsync()
	{
		try
		{
			await _profileService.SaveAsync(Profile);
		}
		catch (Exception exception)
		{
			App.LogException(exception, "DashboardViewModel.SaveProfileSilentlyAsync");
			StatusMessage = "Profile could not be saved.";
		}
	}

	private async Task ToggleProfileEditAsync()
	{
		if (!IsProfileMenuEditing)
		{
			RefreshEditableProfileFields();
			IsProfileMenuEditing = true;
			return;
		}
		Profile.Gamertag = EditableProfileGamertag?.Trim() ?? string.Empty;
		Profile.Name = EditableProfileName?.Trim() ?? string.Empty;
		Profile.Motto = EditableProfileMotto?.Trim() ?? string.Empty;
		Profile.Location = EditableProfileLocation?.Trim() ?? string.Empty;
		Profile.Description = EditableProfileDescription?.Trim() ?? string.Empty;
		Profile.Gamerscore = ParseEditableGamerscore(EditableProfileGamerscore, Profile.Gamerscore);
		await SaveProfileAsync();
		RefreshEditableProfileFields();
		IsProfileMenuEditing = false;
	}

	public Task SaveBladesProfileEditAsync() => IsProfileMenuEditing ? ToggleProfileEditAsync() : Task.CompletedTask;

	public void CancelBladesProfileEdit()
	{
		RefreshEditableProfileFields();
		IsProfileMenuEditing = false;
	}

	private void RefreshEditableProfileFields()
	{
		EditableProfileGamertag = Profile.Gamertag ?? string.Empty;
		EditableProfileName = Profile.Name ?? string.Empty;
		EditableProfileMotto = Profile.Motto ?? string.Empty;
		EditableProfileLocation = Profile.Location ?? string.Empty;
		EditableProfileDescription = Profile.Description ?? string.Empty;
		EditableProfileGamerscore = Math.Max(0, Profile.Gamerscore).ToString(CultureInfo.InvariantCulture);
	}

	private static int ParseEditableGamerscore(string? text, int fallback)
	{
		string normalized = (text ?? string.Empty).Replace(",", string.Empty, StringComparison.Ordinal).Trim();
		if (!int.TryParse(normalized, NumberStyles.Integer, CultureInfo.InvariantCulture, out int value))
		{
			return Math.Max(0, fallback);
		}
		return Math.Clamp(value, 0, 999999999);
	}

	private async Task ShutdownAsync()
	{
		_profileSaveTimer.Stop();
		await _settingsService.SaveAsync(Settings);
		await _profileService.SaveAsync(Profile);
		System.Windows.Application.Current.Shutdown();
	}

	private async Task RefreshDashPartyLinkSetupAsync()
	{
		DashPartyLinkConfig config = await _socialIntegrationManager.GetDashPartyLinkConfigAsync();
		DashPartyLinkServiceUrl = config.ServiceUrl;
		DashPartyLinkFriendCode = string.IsNullOrWhiteSpace(config.FriendCode) ? "Not ready" : config.FriendCode;
		DashPartyLinkDeviceId = config.DeviceId;
		DashPartyLinkSetupStatus = string.IsNullOrWhiteSpace(config.ServiceUrl) ? "Paste your Party Link service URL to enable online parties." : "Party Link is configured.";
	}

	private async Task SaveDashPartyLinkSetupAsync()
	{
		DashPartyLinkConfig config = await _socialIntegrationManager.GetDashPartyLinkConfigAsync();
		config.ServiceUrl = DashPartyLinkServiceUrl;
		await _socialIntegrationManager.SaveDashPartyLinkConfigAsync(config);
		await RefreshDashPartyLinkSetupAsync();
		StatusMessage = string.IsNullOrWhiteSpace(DashPartyLinkServiceUrl) ? "DashX360 Party Link cleared" : "DashX360 Party Link saved";
		_audioService.Play("select");
	}

	private async Task ClearDashPartyLinkSetupAsync()
	{
		DashPartyLinkConfig config = await _socialIntegrationManager.GetDashPartyLinkConfigAsync();
		config.ServiceUrl = string.Empty;
		await _socialIntegrationManager.SaveDashPartyLinkConfigAsync(config);
		await RefreshDashPartyLinkSetupAsync();
		StatusMessage = "DashX360 Party Link cleared";
		_audioService.Play("back");
	}

	private async Task TestDashPartyLinkSetupAsync()
	{
		DashPartyLinkConfig config = await _socialIntegrationManager.GetDashPartyLinkConfigAsync();
		config.ServiceUrl = DashPartyLinkServiceUrl;
		await _socialIntegrationManager.SaveDashPartyLinkConfigAsync(config);
		await RefreshDashPartyLinkSetupAsync();
		DashPartyLinkSetupStatus = "Testing Party Link...";
		DashPartyLinkTestResult result = await _socialIntegrationManager.RunDashPartyLinkSelfTestAsync(Profile);
		if (result.Success)
		{
			string friendCode = await LaunchPartyLinkTestInstanceAsync(config);
			result.Message = "Party Link test passed. Test dashboard opened with friend code " + friendCode + ".";
		}
		DashPartyLinkSetupStatus = result.Message;
		StatusMessage = result.Message;
		_audioService.Play(result.Success ? "select" : "back");
	}

	public void ShowDashboardToast(string line1, string line2 = "", bool acceptPartyInviteWithGuide = false, string actionUri = "")
	{
		NotificationToastLine1 = line1;
		NotificationToastLine2 = line2;
		ToastRequested?.Invoke(this, new DashboardToastRequest
		{
			Line1 = line1,
			Line2 = line2,
			AcceptPartyInviteWithGuide = acceptPartyInviteWithGuide,
			ActionUri = actionUri
		});
	}

	public bool TryTakePendingDashPartyInvite(out SocialFriend friend)
	{
		if (_pendingDashPartyInviteFriend == null)
		{
			friend = null!;
			return false;
		}
		friend = _pendingDashPartyInviteFriend;
		_pendingDashPartyInviteFriend = null;
		return true;
	}

	private async Task PollDashPartyInvitesAsync()
	{
		if (_isPollingDashPartyInvites)
		{
			return;
		}
		_isPollingDashPartyInvites = true;
		try
		{
			IReadOnlyList<DashPartyInvite> invites = await _socialIntegrationManager.GetPendingDashPartyInvitesAsync().ConfigureAwait(continueOnCapturedContext: true);
			foreach (DashPartyInvite invite in invites)
			{
				SocialFriend? friend = CreateDashPartyInviteFriend(invite);
				if (friend == null)
				{
					continue;
				}
				string key = invite.FromDeviceId + "|" + invite.CreatedUtc.ToUnixTimeMilliseconds().ToString(CultureInfo.InvariantCulture);
				if (!_seenDashPartyInviteKeys.Add(key))
				{
					continue;
				}
				_pendingDashPartyInviteFriend = friend;
				ShowDashboardToast(friend.DisplayName, "Invited you to a party", acceptPartyInviteWithGuide: true);
			}
		}
		catch (Exception exception)
		{
			App.LogException(exception, "DashboardViewModel.PollDashPartyInvitesAsync");
		}
		finally
		{
			_isPollingDashPartyInvites = false;
		}
	}

	private static SocialFriend? CreateDashPartyInviteFriend(DashPartyInvite invite)
	{
		if (string.IsNullOrWhiteSpace(invite.FromDeviceId))
		{
			return null;
		}
		string displayName = string.IsNullOrWhiteSpace(invite.FromGamertag) ? "DashX360 Player" : invite.FromGamertag;
		return new SocialFriend
		{
			Id = "dashx360:" + invite.FromDeviceId,
			DisplayName = displayName,
			Source = SocialFriendSource.DashX360,
			IsOnline = true,
			StatusText = "Online",
			ActivityText = "Party Invite",
			ZoneText = "Party",
			IdentityDetailText = "DashX360"
		};
	}

	private async Task<string> LaunchPartyLinkTestInstanceAsync(DashPartyLinkConfig mainConfig)
	{
		string testDeviceId = "dash-test-" + RandomNumberGenerator.GetHexString(12).ToLowerInvariant();
		string testFriendCode = GenerateDashFriendCode();
		string testRoot = Path.Combine(AppPaths.UserDataFolder, "PartyLinkTest", DateTimeOffset.UtcNow.ToString("yyyyMMdd-HHmmssfff", CultureInfo.InvariantCulture));
		Directory.CreateDirectory(testRoot);
		JsonSerializerOptions jsonSerializerOptions = new JsonSerializerOptions
		{
			WriteIndented = true
		};
		DashPartyLinkConfig testConfig = new DashPartyLinkConfig
		{
			ServiceUrl = mainConfig.ServiceUrl,
			DeviceId = testDeviceId,
			FriendCode = testFriendCode,
			CreatedUtc = DateTimeOffset.UtcNow
		};
		await File.WriteAllTextAsync(Path.Combine(testRoot, "dash-party-link.json"), JsonSerializer.Serialize(testConfig, jsonSerializerOptions));
		await File.WriteAllTextAsync(Path.Combine(testRoot, "profile.json"), JsonSerializer.Serialize(new Profile
		{
			Gamertag = "DashX360 Test",
			Name = Profile.Name,
			GamerPicturePath = Profile.GamerPicturePath,
			Gamerscore = 360,
			OnlineStatus = "Online",
			Motto = "Party Link test dashboard",
			Location = Profile.Location,
			Description = "Temporary DashX360 Party Link test profile"
		}, jsonSerializerOptions));
		await File.WriteAllTextAsync(Path.Combine(testRoot, "settings.json"), JsonSerializer.Serialize(new AppSettings
		{
			StartFullscreen = false,
			PlayUiSounds = Settings.PlayUiSounds,
			EnableControllerInput = Settings.EnableControllerInput,
			LaunchOnWindowsStartup = false,
			MinimizeOnGameLaunch = false,
			EnableFakeLoading = false,
			ThemeName = Settings.ThemeName,
			DashboardTileColor = Settings.DashboardTileColor
		}, jsonSerializerOptions));
		await File.WriteAllTextAsync(Path.Combine(testRoot, "friends.json"), JsonSerializer.Serialize(new FriendsData
		{
			Friends = new List<FriendProfile>
			{
				new FriendProfile
				{
					Gamertag = Profile.Gamertag,
					GamerPicturePath = Profile.GamerPicturePath,
					Gamerscore = Profile.Gamerscore,
					Reputation = "*****",
					Zone = "Party",
					Status = "Offline",
					DashX360FriendCode = mainConfig.FriendCode
				}
			}
		}, jsonSerializerOptions));
		await _socialIntegrationManager.AddLocalFriendAsync(new SocialFriend
		{
			Id = "dashx360:" + testFriendCode,
			DisplayName = "DashX360 Test",
			Source = SocialFriendSource.DashX360,
			AvatarPathOrUrl = Profile.GamerPicturePath,
			IsOnline = false,
			StatusText = "Offline",
			GamerscoreText = "360 G",
			ReputationText = "*****",
			ZoneText = "Party",
			IdentityDetailText = "DashX360 " + testFriendCode
		});
		string fileName = Environment.ProcessPath;
		if (string.IsNullOrWhiteSpace(fileName))
		{
			fileName = Process.GetCurrentProcess().MainModule?.FileName ?? Path.Combine(AppPaths.AppFolder, "DashX360.exe");
		}
		ProcessStartInfo processStartInfo = new ProcessStartInfo
		{
			FileName = fileName,
			Arguments = "--party-link-test",
			WorkingDirectory = AppPaths.AppFolder,
			UseShellExecute = false
		};
		processStartInfo.Environment["DASHX360_USER_DATA_FOLDER"] = testRoot;
		processStartInfo.Environment["DASHX360_PARTY_LINK_TEST"] = "1";
		Process.Start(processStartInfo);
		return testFriendCode;
	}

	private static string GenerateDashFriendCode()
	{
		return "DX-" + RandomNumberGenerator.GetHexString(3).ToUpperInvariant() + "-" + RandomNumberGenerator.GetHexString(3).ToUpperInvariant();
	}

	private async Task OpenSteamSetupAsync()
	{
		SteamCommunityConfig steamCommunityConfig = await _steamCommunityService.LoadConfigAsync();
		SteamSetupApiKey = steamCommunityConfig.SteamApiKey;
		SteamSetupSteamId64 = steamCommunityConfig.SteamId64;
		SteamSetupStatus = (_steamCommunityService.IsConfigured ? "Steam is connected. Use Test Connection to check it." : "Paste your Steam Web API key and SteamID64.");
		IsSteamSetupOpen = true;
		_audioService.Play("select");
	}

	private async Task SaveSteamSetupAsync()
	{
		SteamCommunityConfig steamCommunityConfig = BuildSteamSetupConfig();
		if (string.IsNullOrWhiteSpace(steamCommunityConfig.SteamApiKey) || string.IsNullOrWhiteSpace(steamCommunityConfig.SteamId64))
		{
			SteamSetupStatus = "Steam API key and SteamID64 are both required.";
			_audioService.Play("back");
			return;
		}
		await _steamCommunityService.SaveConfigAsync(steamCommunityConfig);
		SteamSetupStatus = "Steam setup saved.";
		StatusMessage = "Steam setup saved";
		_audioService.Play("select");
	}

	private async Task TestSteamSetupAsync()
	{
		SteamSetupStatus = "Testing Steam connection...";
		SteamConnectionTestResult steamConnectionTestResult = await _steamCommunityService.TestConnectionAsync(BuildSteamSetupConfig());
		SteamSetupStatus = steamConnectionTestResult.Message;
		StatusMessage = (steamConnectionTestResult.Success ? ("Steam connected: " + steamConnectionTestResult.DisplayName) : steamConnectionTestResult.Message);
		_audioService.Play(steamConnectionTestResult.Success ? "select" : "back");
	}

	private SteamCommunityConfig BuildSteamSetupConfig()
	{
		return new SteamCommunityConfig
		{
			SteamApiKey = SteamSetupApiKey.Trim(),
			SteamId64 = ExtractSteamId64(SteamSetupSteamId64)
		};
	}

	private void OpenSpotifySetup()
	{
	}

	private void OpenSpotifyPlaylistsFolder()
	{
		string playlistsFolder = GetSpotifyPlaylistsFolder();
		Process.Start(new ProcessStartInfo
		{
			FileName = "explorer.exe",
			Arguments = "\"" + playlistsFolder + "\"",
			UseShellExecute = true
		});
		StatusMessage = "Opening Spotify Playlists folder";
	}

	private async Task SaveSpotifySetupAsync()
	{
		Settings.SpotifyClientId = SpotifySetupClientId.Trim();
		await _settingsService.SaveAsync(Settings);
		SpotifySetupStatus = string.IsNullOrWhiteSpace(Settings.SpotifyClientId) ? "Spotify Client ID cleared." : "Spotify setup saved. Redirect URI: " + SpotifyRedirectUri;
		StatusMessage = "Spotify setup saved";
		_audioService.Play("select");
	}

	private async Task DisconnectSpotifyAsync()
	{
		Settings.SpotifyAccessTokenEncrypted = string.Empty;
		Settings.SpotifyRefreshTokenEncrypted = string.Empty;
		Settings.SpotifyTokenExpiresAtUtc = string.Empty;
		Settings.SpotifyDisplayName = string.Empty;
		await _settingsService.SaveAsync(Settings);
		SpotifySetupStatus = "Spotify disconnected.";
		StatusMessage = "Spotify disconnected";
		_audioService.Play("menu-out");
	}

	private async Task ConnectSpotifyAsync()
	{
		string clientId = SpotifySetupClientId.Trim();
		if (string.IsNullOrWhiteSpace(clientId))
		{
			SpotifySetupStatus = "Spotify Client ID is required.";
			_audioService.Play("back");
			return;
		}
		string verifier = CreateSpotifyCodeVerifier();
		string challenge = CreateSpotifyCodeChallenge(verifier);
		string state = Convert.ToBase64String(RandomNumberGenerator.GetBytes(18)).TrimEnd('=').Replace('+', '-').Replace('/', '_');
		string authorizeUrl = "https://accounts.spotify.com/authorize?response_type=code&client_id=" + Uri.EscapeDataString(clientId) + "&scope=" + Uri.EscapeDataString(SpotifyScopes) + "&redirect_uri=" + Uri.EscapeDataString(SpotifyRedirectUri) + "&state=" + Uri.EscapeDataString(state) + "&code_challenge_method=S256&code_challenge=" + Uri.EscapeDataString(challenge);
		SpotifySetupStatus = "Waiting for Spotify login. Make sure this redirect URI is added: " + SpotifyRedirectUri;
		HttpListener listener = new HttpListener();
		listener.Prefixes.Add(SpotifyRedirectUri);
		try
		{
			listener.Start();
			Process.Start(new ProcessStartInfo
			{
				FileName = authorizeUrl,
				UseShellExecute = true
			});
			HttpListenerContext context = await listener.GetContextAsync();
			string? error = context.Request.QueryString["error"];
			string? code = context.Request.QueryString["code"];
			string? returnedState = context.Request.QueryString["state"];
			WriteSpotifyCallbackResponse(context, string.IsNullOrWhiteSpace(error) && !string.IsNullOrWhiteSpace(code) && string.Equals(returnedState, state, StringComparison.Ordinal) ? "Spotify connected. You can close this tab." : "Spotify connection failed. You can close this tab.");
			if (!string.IsNullOrWhiteSpace(error))
			{
				SpotifySetupStatus = "Spotify login failed: " + error;
				_audioService.Play("back");
				return;
			}
			if (string.IsNullOrWhiteSpace(code) || !string.Equals(returnedState, state, StringComparison.Ordinal))
			{
				SpotifySetupStatus = "Spotify login failed. The callback was missing or invalid.";
				_audioService.Play("back");
				return;
			}
			await ExchangeSpotifyCodeAsync(clientId, verifier, code);
			_audioService.Play("select");
		}
		catch (HttpListenerException ex)
		{
			SpotifySetupStatus = "Could not start Spotify callback listener: " + ex.Message;
			_audioService.Play("back");
		}
		catch (Exception ex)
		{
			SpotifySetupStatus = "Spotify login failed: " + ex.Message;
			_audioService.Play("back");
		}
		finally
		{
			listener.Close();
		}
	}

	private async Task ExchangeSpotifyCodeAsync(string clientId, string verifier, string code)
	{
		using FormUrlEncodedContent content = new FormUrlEncodedContent(new Dictionary<string, string>
		{
			{ "client_id", clientId },
			{ "grant_type", "authorization_code" },
			{ "code", code },
			{ "redirect_uri", SpotifyRedirectUri },
			{ "code_verifier", verifier }
		});
		using HttpResponseMessage response = await SpotifyHttpClient.PostAsync("https://accounts.spotify.com/api/token", content);
		string responseText = await response.Content.ReadAsStringAsync();
		if (!response.IsSuccessStatusCode)
		{
			throw new InvalidOperationException("Token exchange failed: " + response.StatusCode);
		}
		using JsonDocument document = JsonDocument.Parse(responseText);
		JsonElement root = document.RootElement;
		string accessToken = root.GetProperty("access_token").GetString() ?? string.Empty;
		string refreshToken = root.TryGetProperty("refresh_token", out JsonElement refreshElement) ? (refreshElement.GetString() ?? string.Empty) : string.Empty;
		int expiresIn = root.TryGetProperty("expires_in", out JsonElement expiresElement) ? expiresElement.GetInt32() : 3600;
		if (string.IsNullOrWhiteSpace(accessToken))
		{
			throw new InvalidOperationException("Spotify did not return an access token.");
		}
		Settings.SpotifyClientId = clientId;
		Settings.SpotifyAccessTokenEncrypted = SecureStringStorage.Protect(accessToken);
		if (!string.IsNullOrWhiteSpace(refreshToken))
		{
			Settings.SpotifyRefreshTokenEncrypted = SecureStringStorage.Protect(refreshToken);
		}
		Settings.SpotifyTokenExpiresAtUtc = DateTimeOffset.UtcNow.AddSeconds(Math.Max(60, expiresIn - 60)).ToString("O", CultureInfo.InvariantCulture);
		Settings.SpotifyDisplayName = await FetchSpotifyDisplayNameAsync(accessToken);
		await _settingsService.SaveAsync(Settings);
		SpotifySetupStatus = "Spotify connected as " + (string.IsNullOrWhiteSpace(Settings.SpotifyDisplayName) ? "your account" : Settings.SpotifyDisplayName) + ".";
		StatusMessage = "Spotify connected";
	}

	private static async Task<string> FetchSpotifyDisplayNameAsync(string accessToken)
	{
		using HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, "https://api.spotify.com/v1/me");
		request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);
		using HttpResponseMessage response = await SpotifyHttpClient.SendAsync(request);
		if (!response.IsSuccessStatusCode)
		{
			return string.Empty;
		}
		using JsonDocument document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
		if (document.RootElement.TryGetProperty("display_name", out JsonElement displayName))
		{
			return displayName.GetString() ?? string.Empty;
		}
		return string.Empty;
	}

	private static string CreateSpotifyCodeVerifier()
	{
		return Convert.ToBase64String(RandomNumberGenerator.GetBytes(64)).TrimEnd('=').Replace('+', '-').Replace('/', '_');
	}

	private static string CreateSpotifyCodeChallenge(string verifier)
	{
		byte[] hash = SHA256.HashData(Encoding.ASCII.GetBytes(verifier));
		return Convert.ToBase64String(hash).TrimEnd('=').Replace('+', '-').Replace('/', '_');
	}

	private static void WriteSpotifyCallbackResponse(HttpListenerContext context, string message)
	{
		byte[] bytes = Encoding.UTF8.GetBytes("<!doctype html><html><head><title>DashX360 Spotify</title></head><body style=\"font-family:Segoe UI,Arial,sans-serif;background:#111;color:#fff;padding:40px;\"><h1>" + WebUtility.HtmlEncode(message) + "</h1></body></html>");
		context.Response.ContentType = "text/html; charset=utf-8";
		context.Response.ContentLength64 = bytes.Length;
		context.Response.OutputStream.Write(bytes, 0, bytes.Length);
		context.Response.OutputStream.Close();
	}

	private void OpenYouTube()
	{
		IsYouTubeTvOpen = true;
		StatusMessage = "Opening YouTube TV";
		_audioService.Play("select");
	}

	private void OpenDefaultBrowser()
	{
		OpenExternalUrl("https://www.bing.com", "Opening default browser");
	}

	private void OpenWindowsMediaPlayerLegacy()
	{
		OpenExternalProgram("wmplayer.exe", "Opening Windows Media Player Legacy");
	}

	private void CloseYouTubeTv()
	{
		if (IsYouTubeTvOpen)
		{
			IsYouTubeTvOpen = false;
			StatusMessage = "Ready";
			_audioService.Play("back");
		}
	}

	private void OpenSelectedGameStore()
	{
		if (SelectedGame == null || !string.Equals(SelectedGame.Game.LaunchType, "Steam", StringComparison.OrdinalIgnoreCase) || string.IsNullOrWhiteSpace(SelectedGame.Game.SteamAppId))
		{
			StatusMessage = "Steam store is only available for Steam games";
		}
		else
		{
			OpenExternalUrl("steam://store/" + SelectedGame.Game.SteamAppId, "Opening " + SelectedGame.Title + " in Steam");
		}
	}

	private void OpenGameDetailsExtra(object? parameter)
	{
		if (parameter is GameDetailsExtraViewModel gameDetailsExtraViewModel && !string.IsNullOrWhiteSpace(gameDetailsExtraViewModel.SteamAppId))
		{
			OpenExternalUrl("steam://store/" + gameDetailsExtraViewModel.SteamAppId, "Opening " + gameDetailsExtraViewModel.Title + " in Steam");
			_audioService.Play("select");
		}
	}

	private void OpenExternalUrl(string url, string statusMessage)
	{
		try
		{
			Window window = System.Windows.Application.Current?.MainWindow;
			if (window != null)
			{
				window.WindowState = WindowState.Minimized;
			}
			Process.Start(new ProcessStartInfo
			{
				FileName = url,
				UseShellExecute = true
			});
			StatusMessage = statusMessage;
			_audioService.Play("select");
		}
		catch (Exception ex)
		{
			StatusMessage = ex.Message;
		}
	}

	private void OpenExternalProgram(string fileName, string statusMessage)
	{
		try
		{
			Window window = System.Windows.Application.Current?.MainWindow;
			if (window != null)
			{
				window.WindowState = WindowState.Minimized;
			}
			Process.Start(new ProcessStartInfo
			{
				FileName = fileName,
				UseShellExecute = true
			});
			StatusMessage = statusMessage;
			_audioService.Play("select");
		}
		catch (Exception ex)
		{
			StatusMessage = ex.Message;
		}
	}

	private static string GetClipboardText()
	{
		try
		{
			return System.Windows.Clipboard.ContainsText() ? System.Windows.Clipboard.GetText().Trim() : string.Empty;
		}
		catch
		{
			return string.Empty;
		}
	}

	private static string ExtractSteamId64(string value)
	{
		string text = value.Trim();
		if (text.All(char.IsDigit))
		{
			return text;
		}
		int num = text.IndexOf("/profiles/", StringComparison.OrdinalIgnoreCase);
		if (num >= 0)
		{
			int count = num + "/profiles/".Length;
			string text2 = new string(text.Skip(count).TakeWhile(char.IsDigit).ToArray());
			if (!string.IsNullOrWhiteSpace(text2))
			{
				return text2;
			}
		}
		return text;
	}

	private static string GetCustomCoverFolder(string folderName)
	{
		return EnsureDirectory(Path.Combine(AppPaths.AppFolder, "Assets", "Custom Files", "CoverArt", folderName));
	}

	private static string GetMusicFolder()
	{
		return EnsureDirectory(Path.Combine(AppPaths.AppFolder, "Assets", "Custom Files", "Music Files"));
	}

	private static string GetMusicBrowserIconPath(string fileName)
	{
		return AppPaths.ResolvePath(Path.Combine("Assets", "Misc", "MusicBrowserIcons", fileName));
	}

	private static string GetSpotifyMirrorFolder()
	{
		return EnsureDirectory(Path.Combine(AppPaths.AppFolder, "Assets", "Custom Files", "Spotify Mirror"));
	}

	private static string GetSpotifyMirrorPlaylistsFolder()
	{
		return EnsureDirectory(Path.Combine(GetSpotifyMirrorFolder(), "Playlists"));
	}

	private static string GetSpotifyPlaylistsFolder()
	{
		return EnsureDirectory(Path.Combine(AppPaths.AppFolder, "Assets", "Custom Files", "Spotify Playlists"));
	}

	private static string GetSpotifyPlaylistShortcutsFile()
	{
		return Path.Combine(GetSpotifyPlaylistsFolder(), "playlists.txt");
	}

	private static void EnsureSpotifyPlaylistShortcutFiles()
	{
		string folder = GetSpotifyPlaylistsFolder();
		string shortcutsFile = GetSpotifyPlaylistShortcutsFile();
		string readmePath = Path.Combine(folder, "README.txt");
		if (!File.Exists(readmePath))
		{
			File.WriteAllText(readmePath, "DashX360 Spotify Playlists\r\n\r\nCopy a Spotify playlist link, then use Music > Spotify > Add Playlist to save it here.\r\n\r\nYou can also edit playlists.txt manually:\r\nPlaylist Name | Spotify playlist link\r\n\r\nExample:\nMy Playlist | https://open.spotify.com/playlist/yourplaylistid\r\n", Encoding.UTF8);
		}
		if (!File.Exists(shortcutsFile))
		{
			File.WriteAllText(shortcutsFile, "# Playlist Name | Spotify playlist link\r\n", Encoding.UTF8);
		}
	}

	private static List<SpotifyPlaylistShortcut> LoadSpotifyPlaylistShortcuts()
	{
		EnsureSpotifyPlaylistShortcutFiles();
		Dictionary<string, SpotifyPlaylistShortcut> shortcuts = new Dictionary<string, SpotifyPlaylistShortcut>(StringComparer.OrdinalIgnoreCase);
		foreach (string rawLine in File.ReadLines(GetSpotifyPlaylistShortcutsFile()))
		{
			string line = rawLine.Trim();
			if (string.IsNullOrWhiteSpace(line) || line.StartsWith("#", StringComparison.Ordinal))
			{
				continue;
			}
			List<string> parts = SplitSpotifyMirrorLine(line);
			if (parts.Count == 1 && IsSpotifyPlaylistLink(parts[0]))
			{
				string playlistUri = NormalizeSpotifyPlaylistUri(parts[0]);
				shortcuts[playlistUri] = new SpotifyPlaylistShortcut(CreateSpotifyPlaylistShortcutName(playlistUri), playlistUri);
			}
			else if (parts.Count >= 2 && IsSpotifyPlaylistLink(parts[1]))
			{
				string playlistUri = NormalizeSpotifyPlaylistUri(parts[1]);
				if (!shortcuts.ContainsKey(playlistUri) || IsGeneratedSpotifyPlaylistName(shortcuts[playlistUri].Name, playlistUri))
				{
					shortcuts[playlistUri] = new SpotifyPlaylistShortcut(parts[0], playlistUri);
				}
			}
		}
		return shortcuts.Values.OrderBy((SpotifyPlaylistShortcut shortcut) => shortcut.Name, StringComparer.CurrentCultureIgnoreCase).ToList();
	}

	private static void SaveSpotifyPlaylistShortcut(SpotifyPlaylistShortcut shortcut)
	{
		List<SpotifyPlaylistShortcut> shortcuts = LoadSpotifyPlaylistShortcuts();
		int index = shortcuts.FindIndex((SpotifyPlaylistShortcut existing) => string.Equals(existing.Url, shortcut.Url, StringComparison.OrdinalIgnoreCase));
		if (index >= 0)
		{
			shortcuts[index] = shortcut;
		}
		else
		{
			shortcuts.Add(shortcut);
		}
		WriteSpotifyPlaylistShortcuts(shortcuts);
	}

	private static void WriteSpotifyPlaylistShortcuts(IEnumerable<SpotifyPlaylistShortcut> shortcuts)
	{
		EnsureSpotifyPlaylistShortcutFiles();
		StringBuilder builder = new StringBuilder("# Playlist Name | Spotify playlist link\r\n");
		foreach (SpotifyPlaylistShortcut shortcut in shortcuts.OrderBy((SpotifyPlaylistShortcut item) => item.Name, StringComparer.CurrentCultureIgnoreCase))
		{
			builder.Append(shortcut.Name.Replace("|", "-").Trim());
			builder.Append(" | ");
			builder.AppendLine(shortcut.Url);
		}
		File.WriteAllText(GetSpotifyPlaylistShortcutsFile(), builder.ToString(), Encoding.UTF8);
	}

	private async Task ResolveSpotifyPlaylistShortcutNamesAsync()
	{
		List<SpotifyPlaylistShortcut> shortcuts = LoadSpotifyPlaylistShortcuts();
		bool changed = false;
		for (int index = 0; index < shortcuts.Count; index++)
		{
			SpotifyPlaylistShortcut shortcut = shortcuts[index];
			if (!IsGeneratedSpotifyPlaylistName(shortcut.Name, shortcut.Url))
			{
				continue;
			}
			string resolvedName = await FetchSpotifyPlaylistShortcutNameAsync(shortcut.Url);
			if (!string.Equals(resolvedName, shortcut.Name, StringComparison.OrdinalIgnoreCase))
			{
				shortcuts[index] = new SpotifyPlaylistShortcut(resolvedName, shortcut.Url);
				changed = true;
			}
		}
		if (!changed)
		{
			return;
		}
		WriteSpotifyPlaylistShortcuts(shortcuts);
		await System.Windows.Application.Current.Dispatcher.InvokeAsync(delegate
		{
			if (IsMusicSpotifyBrowser && string.Equals(SelectedMusicBrowserMenuItem?.Key, "spotify-playlists", StringComparison.OrdinalIgnoreCase))
			{
				RefreshMusicBrowserResults();
			}
		});
	}

	private static void EnsureSpotifyMirrorSampleFiles()
	{
		string mirrorFolder = GetSpotifyMirrorFolder();
		string playlistsFolder = GetSpotifyMirrorPlaylistsFolder();
		string readmePath = Path.Combine(mirrorFolder, "README.txt");
		if (!File.Exists(readmePath))
		{
			File.WriteAllText(readmePath, "DashX360 Spotify Mirror\r\n\r\nPut playlist files in the Playlists folder. Supported files: .txt, .csv, .json.\r\n\r\nSimple .txt/.csv line format:\r\nSong Title | Artist | Album | Genre\r\n\r\nExample:\r\nJanice STFU | Drake | Demo Album | Hip-Hop\r\n\r\nJSON playlist format:\r\n{\r\n  \"name\": \"playlist test\",\r\n  \"songs\": [\r\n    { \"title\": \"Janice STFU\", \"artist\": \"Drake\", \"album\": \"Demo Album\", \"genre\": \"Hip-Hop\" }\r\n  ]\r\n}\r\n", Encoding.UTF8);
		}
		string samplePath = Path.Combine(playlistsFolder, "playlist test.txt");
		if (!File.Exists(samplePath) && !Directory.EnumerateFiles(playlistsFolder, "*.*", SearchOption.TopDirectoryOnly).Any())
		{
			File.WriteAllText(samplePath, "Janice STFU | Drake | Demo Album | Hip-Hop\r\nNational Treasures | Drake | Demo Album | Hip-Hop\r\nPlot Twist | Drake | Demo Album | Hip-Hop\r\nShabang | Drake | Demo Album | Hip-Hop\r\n", Encoding.UTF8);
		}
	}

	private static List<SpotifyMirrorPlaylist> LoadSpotifyMirrorPlaylists()
	{
		EnsureSpotifyMirrorSampleFiles();
		List<SpotifyMirrorPlaylist> playlists = new List<SpotifyMirrorPlaylist>();
		string playlistsFolder = GetSpotifyMirrorPlaylistsFolder();
		foreach (string file in Directory.EnumerateFiles(playlistsFolder, "*.*", SearchOption.TopDirectoryOnly).OrderBy(Path.GetFileNameWithoutExtension, StringComparer.CurrentCultureIgnoreCase))
		{
			if (!IsSupportedSpotifyMirrorFile(file))
			{
				continue;
			}
			SpotifyMirrorPlaylist? playlist = LoadSpotifyMirrorPlaylist(file);
			if (playlist != null && playlist.Songs.Count > 0)
			{
				playlists.Add(playlist);
			}
		}
		string songsFile = Directory.EnumerateFiles(GetSpotifyMirrorFolder(), "Songs.*", SearchOption.TopDirectoryOnly).FirstOrDefault(IsSupportedSpotifyMirrorFile);
		if (!string.IsNullOrWhiteSpace(songsFile))
		{
			SpotifyMirrorPlaylist? songs = LoadSpotifyMirrorPlaylist(songsFile, "Songs");
			if (songs != null && songs.Songs.Count > 0)
			{
				playlists.Add(songs);
			}
		}
		return playlists;
	}

	private static bool IsSupportedSpotifyMirrorFile(string path)
	{
		string extension = Path.GetExtension(path);
		return extension.Equals(".txt", StringComparison.OrdinalIgnoreCase) || extension.Equals(".csv", StringComparison.OrdinalIgnoreCase) || extension.Equals(".json", StringComparison.OrdinalIgnoreCase);
	}

	private static SpotifyMirrorPlaylist? LoadSpotifyMirrorPlaylist(string file, string? forcedName = null)
	{
		try
		{
			if (Path.GetExtension(file).Equals(".json", StringComparison.OrdinalIgnoreCase))
			{
				return LoadSpotifyMirrorJsonPlaylist(file, forcedName);
			}
			return LoadSpotifyMirrorTextPlaylist(file, forcedName);
		}
		catch (Exception ex)
		{
			App.LogException(ex, "DashboardViewModel.LoadSpotifyMirrorPlaylist");
			return null;
		}
	}

	private static SpotifyMirrorPlaylist LoadSpotifyMirrorTextPlaylist(string file, string? forcedName)
	{
		string playlistName = forcedName ?? Path.GetFileNameWithoutExtension(file);
		List<SpotifyMirrorSong> songs = new List<SpotifyMirrorSong>();
		string[] headers = Array.Empty<string>();
		foreach (string rawLine in File.ReadLines(file))
		{
			string line = rawLine.Trim();
			if (string.IsNullOrWhiteSpace(line) || line.StartsWith("#", StringComparison.Ordinal))
			{
				continue;
			}
			List<string> parts = SplitSpotifyMirrorLine(line);
			if (parts.Count == 0)
			{
				continue;
			}
			if (headers.Length == 0 && parts.Any((string part) => part.Equals("title", StringComparison.OrdinalIgnoreCase) || part.Equals("artist", StringComparison.OrdinalIgnoreCase)))
			{
				headers = parts.Select((string part) => part.Trim().ToLowerInvariant()).ToArray();
				continue;
			}
			SpotifyMirrorSong song = CreateSpotifyMirrorSong(parts, headers, playlistName);
			if (!string.IsNullOrWhiteSpace(song.Title))
			{
				songs.Add(song);
			}
		}
		return new SpotifyMirrorPlaylist(playlistName, songs);
	}

	private static SpotifyMirrorPlaylist LoadSpotifyMirrorJsonPlaylist(string file, string? forcedName)
	{
		using JsonDocument document = JsonDocument.Parse(File.ReadAllText(file));
		string playlistName = forcedName ?? Path.GetFileNameWithoutExtension(file);
		JsonElement songsElement = document.RootElement;
		if (document.RootElement.ValueKind == JsonValueKind.Object)
		{
			if (document.RootElement.TryGetProperty("name", out JsonElement nameElement) && nameElement.ValueKind == JsonValueKind.String)
			{
				playlistName = forcedName ?? (nameElement.GetString() ?? playlistName);
			}
			if (document.RootElement.TryGetProperty("songs", out JsonElement nestedSongs))
			{
				songsElement = nestedSongs;
			}
			else if (document.RootElement.TryGetProperty("tracks", out JsonElement nestedTracks))
			{
				songsElement = nestedTracks;
			}
		}
		List<SpotifyMirrorSong> songs = new List<SpotifyMirrorSong>();
		if (songsElement.ValueKind == JsonValueKind.Array)
		{
			foreach (JsonElement item in songsElement.EnumerateArray())
			{
				if (item.ValueKind == JsonValueKind.String)
				{
					string title = item.GetString() ?? string.Empty;
					if (!string.IsNullOrWhiteSpace(title))
					{
						songs.Add(new SpotifyMirrorSong(title, string.Empty, string.Empty, string.Empty, playlistName));
					}
					continue;
				}
				if (item.ValueKind != JsonValueKind.Object)
				{
					continue;
				}
				string title2 = GetJsonString(item, "title", "name", "track");
				if (string.IsNullOrWhiteSpace(title2))
				{
					continue;
				}
				songs.Add(new SpotifyMirrorSong(title2, GetJsonString(item, "artist", "artists"), GetJsonString(item, "album"), GetJsonString(item, "genre"), playlistName));
			}
		}
		return new SpotifyMirrorPlaylist(playlistName, songs);
	}

	private static string GetJsonString(JsonElement item, params string[] names)
	{
		foreach (string name in names)
		{
			if (!item.TryGetProperty(name, out JsonElement value))
			{
				continue;
			}
			if (value.ValueKind == JsonValueKind.String)
			{
				return value.GetString() ?? string.Empty;
			}
			if (value.ValueKind == JsonValueKind.Array)
			{
				return string.Join(", ", value.EnumerateArray().Where((JsonElement element) => element.ValueKind == JsonValueKind.String).Select((JsonElement element) => element.GetString()).Where((string? text) => !string.IsNullOrWhiteSpace(text)));
			}
		}
		return string.Empty;
	}

	private static SpotifyMirrorSong CreateSpotifyMirrorSong(IReadOnlyList<string> parts, IReadOnlyList<string> headers, string playlistName)
	{
		if (headers.Count > 0)
		{
			return new SpotifyMirrorSong(GetColumn(parts, headers, "title", "name", "track"), GetColumn(parts, headers, "artist", "artists"), GetColumn(parts, headers, "album"), GetColumn(parts, headers, "genre"), playlistName);
		}
		return new SpotifyMirrorSong(parts.ElementAtOrDefault(0) ?? string.Empty, parts.ElementAtOrDefault(1) ?? string.Empty, parts.ElementAtOrDefault(2) ?? string.Empty, parts.ElementAtOrDefault(3) ?? string.Empty, playlistName);
	}

	private static string GetColumn(IReadOnlyList<string> parts, IReadOnlyList<string> headers, params string[] names)
	{
		foreach (string name in names)
		{
			int index = headers.ToList().FindIndex((string header) => string.Equals(header, name, StringComparison.OrdinalIgnoreCase));
			if (index >= 0 && index < parts.Count)
			{
				return parts[index];
			}
		}
		return string.Empty;
	}

	private static List<string> SplitSpotifyMirrorLine(string line)
	{
		char delimiter = line.Contains('|') ? '|' : (line.Contains('\t') ? '\t' : ',');
		if (delimiter == ',' && !line.Contains(',') && line.Contains(" - "))
		{
			return line.Split(new string[] { " - " }, StringSplitOptions.None).Select((string part) => part.Trim()).Where((string part) => part.Length > 0).ToList();
		}
		List<string> parts = new List<string>();
		StringBuilder current = new StringBuilder();
		bool inQuotes = false;
		foreach (char ch in line)
		{
			if (ch == '"')
			{
				inQuotes = !inQuotes;
				continue;
			}
			if (ch == delimiter && !inQuotes)
			{
				parts.Add(current.ToString().Trim());
				current.Clear();
				continue;
			}
			current.Append(ch);
		}
		parts.Add(current.ToString().Trim());
		return parts.Where((string part) => part.Length > 0).ToList();
	}

	private static bool TryDecodeSpotifyMirrorSong(string trackPath, out SpotifyMirrorSong song)
	{
		song = new SpotifyMirrorSong(string.Empty, string.Empty, string.Empty, string.Empty, string.Empty);
		if (!trackPath.StartsWith(SpotifyMirrorScheme, StringComparison.OrdinalIgnoreCase))
		{
			return false;
		}
		try
		{
			string encoded = trackPath.Substring(SpotifyMirrorScheme.Length);
			string json = Encoding.UTF8.GetString(Convert.FromBase64String(encoded));
			SpotifyMirrorSong? decoded = JsonSerializer.Deserialize<SpotifyMirrorSong>(json);
			if (decoded == null || string.IsNullOrWhiteSpace(decoded.Title))
			{
				return false;
			}
			song = decoded;
			return true;
		}
		catch
		{
			return false;
		}
	}

	private static bool IsSupportedMusicFile(string path)
	{
		string extension = Path.GetExtension(path);
		if (extension != null)
		{
			if (!extension.Equals(".mp3", StringComparison.OrdinalIgnoreCase) && !extension.Equals(".wav", StringComparison.OrdinalIgnoreCase) && !extension.Equals(".wma", StringComparison.OrdinalIgnoreCase) && !extension.Equals(".m4a", StringComparison.OrdinalIgnoreCase))
			{
				return extension.Equals(".aac", StringComparison.OrdinalIgnoreCase);
			}
			return true;
		}
		return false;
	}

	private static List<string> EnumerateMusicFiles(string folder, SearchOption searchOption)
	{
		if (!Directory.Exists(folder))
		{
			return new List<string>();
		}
		return Directory.EnumerateFiles(folder, "*.*", searchOption)
			.Where(IsSupportedMusicFile)
			.OrderBy(Path.GetFileNameWithoutExtension, StringComparer.CurrentCultureIgnoreCase)
			.ToList();
	}

	private void AudioAnalysis_OnFrameReady(object? sender, AudioAnalysisFrame frame)
	{
		System.Windows.Application current = System.Windows.Application.Current;
		Dispatcher val = ((current != null) ? ((DispatcherObject)current).Dispatcher : null);
		if (val == null || val.CheckAccess())
		{
			ApplyAudioAnalysis(frame);
			return;
		}
		val.BeginInvoke((Delegate)(Action)delegate
		{
			ApplyAudioAnalysis(frame);
		}, (DispatcherPriority)7, Array.Empty<object>());
	}

	private void ApplyAudioAnalysis(AudioAnalysisFrame frame)
	{
		VisualizerBass = frame.Bass;
		VisualizerMid = frame.Mid;
		VisualizerTreble = frame.Treble;
		VisualizerLoudness = frame.Loudness;
		VisualizerPeak = frame.Peak;
	}

	private void EnsureAudioAnalysisState()
	{
		if (IsMusicPlaying)
		{
			_audioAnalysisService.Start();
		}
		else
		{
			_audioAnalysisService.Stop();
		}
		OnPropertyChanged("IsAudioAnalysisRunning");
	}

	private static string EnsureDirectory(string path)
	{
		Directory.CreateDirectory(path);
		return path;
	}

	private static string CopyCustomArtwork(string sourcePath, string folderName, string title)
	{
		string customCoverFolder = GetCustomCoverFolder(folderName);
		string fullPath = Path.GetFullPath(sourcePath);
		if (string.Equals(Path.GetDirectoryName(fullPath), customCoverFolder, StringComparison.OrdinalIgnoreCase))
		{
			return fullPath;
		}
		string extension = Path.GetExtension(fullPath);
		string text = MakeSafeFileName(title);
		string text2 = Path.Combine(customCoverFolder, text + extension);
		int num = 2;
		while (File.Exists(text2))
		{
			text2 = Path.Combine(customCoverFolder, $"{text} {num++}{extension}");
		}
		File.Copy(fullPath, text2);
		return text2;
	}

	private static string MakeSafeFileName(string value)
	{
		char[] invalid = Path.GetInvalidFileNameChars();
		string text = new string(value.Select((char ch) => (!invalid.Contains(ch)) ? ch : '_').ToArray()).Trim();
		if (!string.IsNullOrWhiteSpace(text))
		{
			return text;
		}
		return "cover";
	}

	private void LoadMusicLibrary()
	{
		LoadMusicQueue(EnumerateMusicFiles(GetMusicFolder(), SearchOption.AllDirectories), CurrentMusicTrack?.Path);
	}

	private void LoadMusicQueue(IEnumerable<string> paths, string? selectedPath = null)
	{
		_isSpotifyMusicMode = false;
		_isSpotifyAppControlMode = false;
		List<string> list = paths.Where(File.Exists).OrderBy(Path.GetFileNameWithoutExtension, StringComparer.CurrentCultureIgnoreCase).ToList();
		MusicTracks.Clear();
		foreach (string item in list)
		{
			MusicTracks.Add(new MusicTrackViewModel(item));
		}
		_musicIndex = ((selectedPath == null) ? (-1) : MusicTracks.ToList().FindIndex((MusicTrackViewModel track) => string.Equals(track.Path, selectedPath, StringComparison.OrdinalIgnoreCase)));
		CurrentMusicTrack = ((_musicIndex >= 0) ? MusicTracks[_musicIndex] : null);
		SelectedMusicTrack = CurrentMusicTrack ?? MusicTracks.FirstOrDefault();
		OnPropertyChanged("MusicTrackCountText");
	}

	public void EnsureMusicLibraryLoaded()
	{
		LoadMusicLibrary();
	}

	private void PlayMusicTrack(MusicTrackViewModel? track)
	{
		if (_isSpotifyMusicMode)
		{
			if (track != null)
			{
				int spotifyIndex = MusicTracks.IndexOf(track);
				if (spotifyIndex >= 0)
				{
					_musicIndex = spotifyIndex;
					CurrentMusicTrack = track;
					SelectedMusicTrack = track;
					OnPropertyChanged("MusicTrackCountText");
				}
			}
			SendMediaKey(VkMediaPlayPause);
			IsMusicPlaying = true;
			if (_isSpotifyAppControlMode)
			{
				_musicTimer.Start();
				_ = RefreshSpotifyNowPlayingAsync(700);
			}
			return;
		}
		if (track == null)
		{
			if (CurrentMusicTrack != null)
			{
				_musicPlayer.Play();
				_musicTimer.Start();
				IsMusicPlaying = true;
			}
			return;
		}
		int num = MusicTracks.IndexOf(track);
		if (num >= 0 && File.Exists(track.Path))
		{
			_musicIndex = num;
			CurrentMusicTrack = track;
			SelectedMusicTrack = track;
			_musicPlayer.Open(new Uri(track.Path, UriKind.Absolute));
			ApplyMusicPlayerVolume();
			_musicPlayer.Play();
			_musicTimer.Start();
			IsMusicPlaying = true;
			StatusMessage = "Playing " + track.Title;
			RefreshMusicProgress();
		}
	}

	private void ToggleMusicPlayback()
	{
		if (_isSpotifyMusicMode)
		{
			SendMediaKey(VkMediaPlayPause);
			IsMusicPlaying = !IsMusicPlaying;
			if (_isSpotifyAppControlMode)
			{
				_musicTimer.Start();
				_ = RefreshSpotifyNowPlayingAsync(700);
			}
			return;
		}
		if (CurrentMusicTrack == null)
		{
			if (MusicTracks.Count == 0)
			{
				LoadMusicLibrary();
			}
			PlayMusicTrack(MusicTracks.FirstOrDefault());
		}
		else if (IsMusicPlaying)
		{
			_musicPlayer.Pause();
			_musicTimer.Stop();
			IsMusicPlaying = false;
		}
		else
		{
			_musicPlayer.Play();
			_musicTimer.Start();
			IsMusicPlaying = true;
		}
	}

	private void StopMusic()
	{
		if (_isSpotifyMusicMode)
		{
			SendMediaKey(VkMediaStop);
			IsMusicPlaying = false;
			MusicProgress = 0.0;
			MusicPositionText = "0:00";
			_musicTimer.Stop();
			return;
		}
		_musicPlayer.Stop();
		_musicTimer.Stop();
		IsMusicPlaying = false;
		MusicProgress = 0.0;
		MusicPositionText = "0:00";
	}

	private void NextMusicTrack()
	{
		if (_isSpotifyMusicMode)
		{
			SendMediaKey(VkMediaNextTrack);
			MoveSpotifyQueueSelection(1);
			IsMusicPlaying = true;
			if (_isSpotifyAppControlMode)
			{
				_musicTimer.Start();
				_ = RefreshSpotifyNowPlayingAsync(900);
			}
			return;
		}
		if (MusicTracks.Count == 0)
		{
			LoadMusicLibrary();
		}
		if (MusicTracks.Count != 0)
		{
			int index = (IsShuffleEnabled ? _random.Next(MusicTracks.Count) : ((_musicIndex + 1 + MusicTracks.Count) % MusicTracks.Count));
			PlayMusicTrack(MusicTracks[index]);
		}
	}

	private void PreviousMusicTrack()
	{
		if (_isSpotifyMusicMode)
		{
			SendMediaKey(VkMediaPreviousTrack);
			MoveSpotifyQueueSelection(-1);
			IsMusicPlaying = true;
			if (_isSpotifyAppControlMode)
			{
				_musicTimer.Start();
				_ = RefreshSpotifyNowPlayingAsync(900);
			}
			return;
		}
		if (MusicTracks.Count == 0)
		{
			LoadMusicLibrary();
		}
		if (MusicTracks.Count != 0)
		{
			int index = (_musicIndex - 1 + MusicTracks.Count) % MusicTracks.Count;
			PlayMusicTrack(MusicTracks[index]);
		}
	}

	private void MoveSpotifyQueueSelection(int delta)
	{
		if (MusicTracks.Count == 0)
		{
			return;
		}
		int currentIndex = Math.Clamp(_musicIndex, 0, MusicTracks.Count - 1);
		int nextIndex = (currentIndex + delta + MusicTracks.Count) % MusicTracks.Count;
		_musicIndex = nextIndex;
		CurrentMusicTrack = MusicTracks[nextIndex];
		SelectedMusicTrack = CurrentMusicTrack;
		OnPropertyChanged("MusicTrackCountText");
	}

	private async Task RefreshSpotifyNowPlayingAsync(int delayMilliseconds = 0)
	{
		if (!_isSpotifyAppControlMode || _isRefreshingSpotifyTimeline)
		{
			return;
		}
		if (delayMilliseconds > 0)
		{
			await Task.Delay(delayMilliseconds);
		}
		if (!_isSpotifyAppControlMode || _isRefreshingSpotifyTimeline)
		{
			return;
		}
		_isRefreshingSpotifyTimeline = true;
		try
		{
			GlobalSystemMediaTransportControlsSessionManager manager = await GlobalSystemMediaTransportControlsSessionManager.RequestAsync();
			GlobalSystemMediaTransportControlsSession? session = manager.GetSessions().FirstOrDefault((GlobalSystemMediaTransportControlsSession candidate) => candidate.SourceAppUserModelId.Contains("Spotify", StringComparison.OrdinalIgnoreCase)) ?? manager.GetCurrentSession();
			if (session == null)
			{
				return;
			}
			GlobalSystemMediaTransportControlsSessionMediaProperties properties = await session.TryGetMediaPropertiesAsync();
			string title = properties.Title?.Trim() ?? string.Empty;
			string artist = properties.Artist?.Trim() ?? string.Empty;
			string displayTitle = string.IsNullOrWhiteSpace(artist) ? title : artist + " - " + title;
			GlobalSystemMediaTransportControlsSessionTimelineProperties timeline = session.GetTimelineProperties();
			TimeSpan duration = timeline.EndTime > timeline.StartTime ? timeline.EndTime - timeline.StartTime : TimeSpan.Zero;
			TimeSpan position = timeline.Position;
			if (position < TimeSpan.Zero)
			{
				position = TimeSpan.Zero;
			}
			if (duration > TimeSpan.Zero && position > duration)
			{
				position = duration;
			}
			GlobalSystemMediaTransportControlsSessionPlaybackInfo playbackInfo = session.GetPlaybackInfo();
			await System.Windows.Application.Current.Dispatcher.InvokeAsync(delegate
			{
				if (!string.IsNullOrWhiteSpace(displayTitle))
				{
					UpdateSpotifyNowPlayingTitle(displayTitle);
				}
				UpdateSpotifyTimeline(position, duration, playbackInfo.PlaybackStatus);
			});
		}
		catch (Exception ex)
		{
			App.LogException(ex, "DashboardViewModel.RefreshSpotifyNowPlayingAsync");
		}
		finally
		{
			_isRefreshingSpotifyTimeline = false;
		}
	}

	private void UpdateSpotifyTimeline(TimeSpan position, TimeSpan duration, GlobalSystemMediaTransportControlsSessionPlaybackStatus playbackStatus)
	{
		if (!_isSpotifyAppControlMode)
		{
			return;
		}
		MusicPositionText = FormatTime(position);
		if (duration > TimeSpan.Zero)
		{
			MusicDurationText = FormatTime(duration);
			MusicProgress = Math.Clamp(position.TotalSeconds / duration.TotalSeconds * 100.0, 0.0, 100.0);
		}
		else
		{
			MusicDurationText = "0:00";
			MusicProgress = 0.0;
		}
		if (playbackStatus == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing)
		{
			IsMusicPlaying = true;
		}
		else if (playbackStatus == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Paused || playbackStatus == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Stopped)
		{
			IsMusicPlaying = false;
		}
	}

	private void UpdateSpotifyNowPlayingTitle(string displayTitle)
	{
		if (!_isSpotifyMusicMode || string.IsNullOrWhiteSpace(displayTitle))
		{
			return;
		}
		string path = CurrentMusicTrack?.Path ?? "spotify-control://now-playing";
		MusicTrackViewModel track = new MusicTrackViewModel(displayTitle, path);
		if (MusicTracks.Count == 0)
		{
			MusicTracks.Add(track);
			_musicIndex = 0;
		}
		else
		{
			int index = Math.Clamp(_musicIndex, 0, MusicTracks.Count - 1);
			MusicTracks[index] = track;
			_musicIndex = index;
		}
		CurrentMusicTrack = MusicTracks[_musicIndex];
		OnPropertyChanged("MusicTrackCountText");
	}

	private static void SendMediaKey(byte virtualKey)
	{
		keybd_event(virtualKey, 0, 0, UIntPtr.Zero);
		keybd_event(virtualKey, 0, KeyEventKeyUp, UIntPtr.Zero);
	}

	private void OpenSpotifyUri(string uri)
	{
		if (string.IsNullOrWhiteSpace(uri))
		{
			uri = "spotify:";
		}
		try
		{
			Process.Start(new ProcessStartInfo
			{
				FileName = uri,
				UseShellExecute = true
			});
		}
		catch (Exception ex)
		{
			StatusMessage = "Spotify could not be opened: " + ex.Message;
		}
	}

	private void RefreshMusicProgress()
	{
		if (_isSpotifyAppControlMode)
		{
			_ = RefreshSpotifyNowPlayingAsync();
			return;
		}
		TimeSpan position = _musicPlayer.Position;
		MusicPositionText = FormatTime(position);
		if (_musicPlayer.NaturalDuration.HasTimeSpan)
		{
			TimeSpan timeSpan = _musicPlayer.NaturalDuration.TimeSpan;
			MusicDurationText = FormatTime(timeSpan);
			MusicProgress = ((timeSpan.TotalSeconds <= 0.0) ? 0.0 : Math.Clamp(position.TotalSeconds / timeSpan.TotalSeconds * 100.0, 0.0, 100.0));
		}
		else
		{
			MusicDurationText = "0:00";
			MusicProgress = 0.0;
		}
	}

	private static string FormatTime(TimeSpan value)
	{
		if (!(value.TotalHours >= 1.0))
		{
			return value.ToString("m\\:ss");
		}
		return value.ToString("h\\:mm\\:ss");
	}

	private async Task ScanFolderAsync()
	{
		string text = _filePickerService.PickFolder();
		if (string.IsNullOrWhiteSpace(text))
		{
			return;
		}
		if (!_library.LibraryPaths.Contains<string>(text, StringComparer.OrdinalIgnoreCase))
		{
			_library.LibraryPaths.Add(text);
		}
		IReadOnlyList<GameMetadata> source = await _libraryService.ScanFolderAsync(text);
		HashSet<string> knownPaths = _library.Games.Select((GameMetadata game) => game.ExecutablePath).ToHashSet<string>(StringComparer.OrdinalIgnoreCase);
		int added = 0;
		string destination = NormalizeAddDestination(Settings.DefaultAddDestination);
		foreach (GameMetadata item in source.Where((GameMetadata game) => knownPaths.Add(game.ExecutablePath)))
		{
			if (destination == "My Apps")
			{
				item.Genre = "App";
			}
			_library.Games.Add(item);
			Games.Add(new GameCardViewModel(item, _accentBrushes[Games.Count % _accentBrushes.Count]));
			added++;
		}
		SortGamesByTitle(SelectedGame?.Game.Id);
		await PersistLibraryAsync();
		OnPropertyChanged("MyGamesCountText");
		RefreshSetupDestinationState();
		StatusMessage = ((added == 1) ? ("Imported 1 item to " + destination) : $"Imported {added} items to {destination}");
	}

	private async Task ToggleFavoriteAsync(object? _)
	{
		if (SelectedGame != null)
		{
			GameCardViewModel toggledGame = SelectedGame;
			toggledGame.Game.IsFavorite = !toggledGame.Game.IsFavorite;
			await PersistLibraryAsync();
			RefreshDerivedLists();
			if (toggledGame.Game.IsFavorite)
			{
				_audioService.Play("select");
			}
			if (_isLibraryShowingPins && !toggledGame.Game.IsFavorite)
			{
				SelectedGame = LibraryMenuGames.FirstOrDefault();
			}
			StatusMessage = (toggledGame.Game.IsFavorite ? "Pinned to Home" : "Removed from pins");
		}
	}

	private async Task RefreshSelectedGameDetailsAsync()
	{
		GameCardViewModel selected = SelectedGame;
		if (selected == null || !string.Equals(selected.Game.LaunchType, "Steam", StringComparison.OrdinalIgnoreCase) || string.IsNullOrWhiteSpace(selected.Game.SteamAppId))
		{
			_selectedGameDlc = Array.Empty<SteamGameDlc>();
			selected?.Refresh();
			RefreshGameDetailsPanels();
			return;
		}
		try
		{
			SteamGameDetails steamGameDetails = await _steamCommunityService.LoadGameDetailsAsync(selected.Game.SteamAppId);
			if (selected != SelectedGame)
			{
				return;
			}
			bool flag = false;
			_selectedGameDlc = steamGameDetails.Dlc;
			TimeSpan? playtime = steamGameDetails.Playtime;
			if (playtime.HasValue)
			{
				TimeSpan valueOrDefault = playtime.GetValueOrDefault();
				if (valueOrDefault != selected.Game.Playtime)
				{
					selected.Game.Playtime = valueOrDefault;
					flag = true;
				}
			}
			if (!string.IsNullOrWhiteSpace(steamGameDetails.Genre) && !string.Equals(selected.Game.Genre, steamGameDetails.Genre, StringComparison.Ordinal))
			{
				selected.Game.Genre = steamGameDetails.Genre;
				flag = true;
			}
			if (!string.IsNullOrWhiteSpace(steamGameDetails.Rating) && !string.Equals(selected.Game.Rating, steamGameDetails.Rating, StringComparison.Ordinal))
			{
				selected.Game.Rating = steamGameDetails.Rating;
				flag = true;
			}
			if (!string.IsNullOrWhiteSpace(steamGameDetails.MultiplayerInfo) && !string.Equals(selected.Game.MultiplayerInfo, steamGameDetails.MultiplayerInfo, StringComparison.Ordinal))
			{
				selected.Game.MultiplayerInfo = steamGameDetails.MultiplayerInfo;
				flag = true;
			}
			if (!string.IsNullOrWhiteSpace(steamGameDetails.CoOpInfo) && !string.Equals(selected.Game.CoOpInfo, steamGameDetails.CoOpInfo, StringComparison.Ordinal))
			{
				selected.Game.CoOpInfo = steamGameDetails.CoOpInfo;
				flag = true;
			}
			if (!string.IsNullOrWhiteSpace(steamGameDetails.StoreDescription) && !string.Equals(selected.Game.StoreDescription, steamGameDetails.StoreDescription, StringComparison.Ordinal))
			{
				selected.Game.StoreDescription = steamGameDetails.StoreDescription;
				flag = true;
			}
			if (!string.IsNullOrWhiteSpace(steamGameDetails.StoreScreenshotPath) && !string.Equals(selected.Game.StoreScreenshotPath, steamGameDetails.StoreScreenshotPath, StringComparison.Ordinal))
			{
				selected.Game.StoreScreenshotPath = steamGameDetails.StoreScreenshotPath;
				flag = true;
			}
			if (Math.Abs(selected.Game.ReviewStarRating - steamGameDetails.ReviewStarRating) > 0.01)
			{
				selected.Game.ReviewStarRating = steamGameDetails.ReviewStarRating;
				flag = true;
			}
			if (selected.Game.ReviewCount != steamGameDetails.ReviewCount)
			{
				selected.Game.ReviewCount = steamGameDetails.ReviewCount;
				flag = true;
			}
			selected.Refresh();
			if (flag)
			{
				await PersistLibraryAsync();
			}
			RefreshGameDetailsPanels();
		}
		catch
		{
			_selectedGameDlc = Array.Empty<SteamGameDlc>();
			selected.Refresh();
			RefreshGameDetailsPanels();
		}
	}

	private void RefreshGameDetailsPanels()
	{
		GameDetailsExtras.Clear();
		foreach (GameDetailsExtraViewModel item in BuildGameDetailsExtras())
		{
			GameDetailsExtras.Add(item);
		}
		GameDetailsPreviewExtras.Clear();
		foreach (GameDetailsExtraViewModel item2 in GameDetailsExtras.Take(5))
		{
			GameDetailsPreviewExtras.Add(item2);
		}
		if (GameDetailsExtras.Count > 5)
		{
			GameDetailsPreviewExtras.Add(new GameDetailsExtraViewModel
			{
				Title = "See All",
				IconPath = GameDetailsSeeAllIconPath,
				IsSeeAll = true,
				Command = OpenGameDetailsExtrasSeeAllCommand
			});
		}
		OnPropertyChanged("GameDetailsDeveloperText");
		OnPropertyChanged("GameDetailsPublisherText");
		OnPropertyChanged("GameDetailsGenreText");
		OnPropertyChanged("GameDetailsLocalCapabilitiesText");
		OnPropertyChanged("GameDetailsOnlineCapabilitiesText");
		OnPropertyChanged("GameDetailsNoteText");
		OnPropertyChanged("GameDetailsSeeAllCountText");
		OnPropertyChanged("GameDetailsGalleryImagePath");
		OnPropertyChanged("GameDetailsGalleryCountText");
	}

	private IEnumerable<GameDetailsExtraViewModel> BuildGameDetailsExtras()
	{
		string rating = SelectedGame?.DetailsReviewStarsText;
		if (string.IsNullOrWhiteSpace(rating))
		{
			rating = "*****";
		}
		foreach (SteamGameDlc item in _selectedGameDlc.Where((SteamGameDlc dlc) => !string.IsNullOrWhiteSpace(dlc.AppId) && !string.IsNullOrWhiteSpace(dlc.Name)))
		{
			yield return new GameDetailsExtraViewModel
			{
				Title = item.Name,
				PriceText = item.PriceText,
				RatingText = rating,
				IconPath = GameDetailsAddOnIconPath,
				SteamAppId = item.AppId,
				Command = OpenGameDetailsExtraCommand
			};
		}
	}

	private IEnumerable<string> BuildGameDetailsGalleryImages()
	{
		GameMetadata gameMetadata = SelectedGame?.Game;
		if (gameMetadata == null)
		{
			yield break;
		}
		foreach (string item in new string[4] { gameMetadata.StoreScreenshotPath, gameMetadata.BackgroundArtPath, gameMetadata.HeaderImagePath, gameMetadata.CoverArtPath }.Where((string path) => !string.IsNullOrWhiteSpace(path)).Distinct<string>(StringComparer.OrdinalIgnoreCase))
		{
			yield return item;
		}
	}

	private IEnumerable<string> BuildLocalCapabilities()
	{
		yield return "Players 1";
		yield return "Hard Drive Required";
		yield return "Controller Supported";
		if (!string.IsNullOrWhiteSpace(SelectedGame?.DetailsPlaytimeText))
		{
			yield return SelectedGame.DetailsPlaytimeText.Replace("Time played: ", "Time played ");
		}
	}

	private IEnumerable<string> BuildOnlineCapabilities()
	{
		string text = SelectedGame?.DetailsMultiplayerText;
		string coOp = SelectedGame?.DetailsCoOpText;
		yield return string.IsNullOrWhiteSpace(text) ? "Online Multiplayer: None" : text;
		yield return string.IsNullOrWhiteSpace(coOp) ? "Online Co-op: None" : coOp;
	}

	private string BuildGameDetailsNote()
	{
		if (SelectedGame == null)
		{
			return string.Empty;
		}
		if (!SelectedGame.IsSteamGame)
		{
			return SelectedGame.Title + " was added manually. Steam store metadata is not available for this game.";
		}
		return SelectedGame.Title + " was imported from Steam. Store metadata is shown when available; some capabilities may depend on Steam store categories and installed game data.";
	}

	private async Task SaveSettingsAsync()
	{
		await _settingsService.SaveAsync(Settings);
		await PersistLibraryAsync();
		_startupRegistrationService.SetLaunchOnStartup(Settings.LaunchOnWindowsStartup);
		StatusMessage = "Settings saved";
	}

	private async Task ExportDataAsync()
	{
		string path = _filePickerService.PickSaveJsonFile();
		if (string.IsNullOrWhiteSpace(path))
		{
			return;
		}
		try
		{
			await _importExportService.ExportAsync(_library, Profile, Settings, path);
			StatusMessage = "Backup exported";
			System.Windows.MessageBox.Show("Dashboard data exported successfully." + Environment.NewLine + Environment.NewLine + path, "Export Complete", MessageBoxButton.OK, MessageBoxImage.Asterisk);
		}
		catch (Exception ex)
		{
			App.LogException(ex, "DashboardViewModel.ExportDataAsync");
			System.Windows.MessageBox.Show("Export failed." + Environment.NewLine + Environment.NewLine + ex.Message, "Export Failed", MessageBoxButton.OK, MessageBoxImage.Hand);
		}
	}

	private async Task ImportDataAsync()
	{
		string text = _filePickerService.PickJsonFile();
		if (!string.IsNullOrWhiteSpace(text))
		{
			DashboardImportResult result = await _importExportService.ImportAsync(text);
			if (!result.Success)
			{
				System.Windows.MessageBox.Show(result.Message, "Import Failed", MessageBoxButton.OK, MessageBoxImage.Hand);
				return;
			}
			await ReloadSavedDataAsync(reloadSettings: true);
			_startupRegistrationService.SetLaunchOnStartup(Settings.LaunchOnWindowsStartup);
			StatusMessage = "Backup imported";
			System.Windows.MessageBox.Show((result.SafetyBackupPath == null) ? result.Message : $"{result.Message}{Environment.NewLine}{Environment.NewLine}Safety backup created:{Environment.NewLine}{result.SafetyBackupPath}", "Import Complete", MessageBoxButton.OK, MessageBoxImage.Asterisk);
		}
	}

	private async Task ScanSteamGamesAsync()
	{
		_ = 1;
		try
		{
			SteamGameScanResult result = await _steamLibraryScannerService.ScanAsync(_library);
			if (!string.IsNullOrWhiteSpace(result.Message))
			{
				StatusMessage = result.Message;
			}
			if (result.Added > 0 || result.Updated > 0)
			{
				SyncGamesCollectionFromLibrary();
				RefreshDerivedLists();
				SortGamesByTitle(SelectedGame?.Game.Id);
				await PersistLibraryAsync();
			}
			WriteSteamScanDebugReport(result);
			System.Windows.MessageBox.Show(string.IsNullOrWhiteSpace(result.Message) ? $"Steam scan complete.{Environment.NewLine}{Environment.NewLine}Added: {result.Added}{Environment.NewLine}Updated: {result.Updated}{Environment.NewLine}Skipped: {result.Skipped}" : $"{result.Message}{Environment.NewLine}{Environment.NewLine}Added: {result.Added}{Environment.NewLine}Updated: {result.Updated}{Environment.NewLine}Skipped: {result.Skipped}", "Scan Steam Games", MessageBoxButton.OK, (result.Added > 0 || result.Updated > 0) ? MessageBoxImage.Asterisk : MessageBoxImage.Exclamation);
		}
		catch (Exception ex)
		{
			App.LogException(ex, "DashboardViewModel.ScanSteamGamesAsync");
			StatusMessage = "Steam scan failed";
			System.Windows.MessageBox.Show("Steam scan failed." + Environment.NewLine + Environment.NewLine + ex.Message, "Scan Steam Games", MessageBoxButton.OK, MessageBoxImage.Hand);
		}
	}

	private async Task LoadThemesAsync()
	{
		IReadOnlyList<DashboardTheme> obj = await _themeService.LoadThemesAsync();
		AvailableThemes.Clear();
		foreach (DashboardTheme item in obj)
		{
			AvailableThemes.Add(item);
		}
	}

	private static List<DashboardTabCustomizationViewModel> BuildDashboardCustomizationTabs()
	{
		int num = 5;
		List<DashboardTabCustomizationViewModel> list = new List<DashboardTabCustomizationViewModel>(num);
		CollectionsMarshal.SetCount(list, num);
		Span<DashboardTabCustomizationViewModel> span = CollectionsMarshal.AsSpan(list);
		int num2 = 0;
		span[num2] = new DashboardTabCustomizationViewModel("home", "Home", new _003C_003Ez__ReadOnlyArray<DashboardTileCustomizationViewModel>(new DashboardTileCustomizationViewModel[9]
		{
			Tile("home", "open-tray", "Open Tray", 0.0, 0.0, 177.0, 122.0, color: true, imageEditable: false, launchEditable: false),
			Tile("home", "pins", "My Pins", 0.0, 123.0, 177.0, 122.0, color: true, imageEditable: false, launchEditable: false),
			Tile("home", "recent", "Recent", 0.0, 246.0, 177.0, 122.0, color: true, imageEditable: false, launchEditable: false),
			Tile("home", "halo", "Halo 4", 179.0, 0.0, 423.0, 242.0, color: false, imageEditable: true, "Assets/Tiles/halo4home.jpg"),
			Tile("home", "dexter", "Dexter", 179.0, 244.0, 210.0, 124.0, color: false, imageEditable: true, "Assets/Tiles/dexterhome.jpg"),
			Tile("home", "maroon5", "Maroon 5", 391.0, 244.0, 211.0, 124.0, color: false, imageEditable: true, "Assets/Tiles/maroon5home.jpg"),
			Tile("home", "kinect", "Kinect Central", 604.0, 0.0, 160.0, 122.0, color: false, imageEditable: true, "Assets/Tiles/dancecentralhome.jpg"),
			Tile("home", "sports", "Sports", 604.0, 123.0, 160.0, 122.0, color: false, imageEditable: true, "Assets/Tiles/espn.jpg"),
			Tile("home", "youtube", "YouTube", 604.0, 246.0, 160.0, 122.0, color: false, imageEditable: true, "Assets/Tiles/youtubehome.jpg")
		}));
		num2++;
		span[num2] = new DashboardTabCustomizationViewModel("video", "Video", new _003C_003Ez__ReadOnlyArray<DashboardTileCustomizationViewModel>(new DashboardTileCustomizationViewModel[7]
		{
			Tile("video", "apps", "My Video Apps", 0.0, 0.0, 146.0, 148.0, color: true, imageEditable: false, launchEditable: false),
			Tile("video", "marketplace", "Video Marketplace", 0.0, 150.0, 146.0, 148.0, color: true, imageEditable: false, launchEditable: false),
			Tile("video", "feature", "The Dark Knight", 148.0, 0.0, 308.0, 301.0, color: false, imageEditable: true, "Assets/Tiles/thedarkkightvideo.jpg"),
			Tile("video", "cloudy", "Cloudy", 458.0, 0.0, 148.0, 150.0, color: false, imageEditable: true, "Assets/Tiles/cloudywithachanceofmeatballsvideo.jpg"),
			Tile("video", "recent", "Recent", 608.0, 0.0, 198.0, 150.0, color: false, imageEditable: false, "", "#FFFFFFFF", "#FF2B2B2B"),
			Tile("video", "kungfu", "Kung Fu Panda 2", 458.0, 152.0, 148.0, 149.0, color: false, imageEditable: true, "Assets/Tiles/kungfupanda2video.jpg"),
			Tile("video", "hbogo", "HBO GO", 608.0, 152.0, 198.0, 149.0, color: false, imageEditable: true, "Assets/Tiles/hbogovideo.jpg")
		}));
		num2++;
		span[num2] = new DashboardTabCustomizationViewModel("games", "Games", new _003C_003Ez__ReadOnlyArray<DashboardTileCustomizationViewModel>(new DashboardTileCustomizationViewModel[5]
		{
			Tile("games", "mygames", "My Games", 0.0, 0.0, 155.0, 153.0, color: true, imageEditable: false, launchEditable: false),
			Tile("games", "marketplace", "Game Marketplace", 0.0, 155.0, 155.0, 149.0, color: true, imageEditable: false, launchEditable: false),
			Tile("games", "forza", "Forza Horizon", 157.0, 0.0, 400.0, 303.0, color: false, imageEditable: true, "Assets/Tiles/forzahorizongames.jpg", defaultSecondaryTitle: "December IGN Pack"),
			Tile("games", "minecraft", "Minecraft", 559.0, 0.0, 200.0, 151.0, color: false, imageEditable: true, "Assets/Tiles/minecraftgames.jpg"),
			Tile("games", "blackops", "Black Ops II", 559.0, 153.0, 200.0, 150.0, color: false, imageEditable: true, "Assets/Tiles/blackops2games.jpg")
		}));
		num2++;
		span[num2] = new DashboardTabCustomizationViewModel("music", "Music", new _003C_003Ez__ReadOnlyArray<DashboardTileCustomizationViewModel>(new DashboardTileCustomizationViewModel[6]
		{
			Tile("music", "player", "Music Player", 0.0, 0.0, 148.0, 148.0, color: true, imageEditable: false, launchEditable: false),
			Tile("music", "marketplace", "Music Marketplace", 0.0, 150.0, 148.0, 148.0, color: true, imageEditable: false, launchEditable: false),
			Tile("music", "feature", "Overexposed", 150.0, 0.0, 306.0, 298.0, color: false, imageEditable: true, "Assets/Tiles/overexposedmusic.jpg", defaultSecondaryTitle: "Maroon 5 - Overexposed"),
			Tile("music", "recent", "Recent Music", 458.0, 0.0, 158.0, 148.0, color: false, imageEditable: true, "Assets/Tiles/evanescencerecent.png"),
			Tile("music", "search", "Search", 618.0, 0.0, 188.0, 148.0, color: false, imageEditable: false, "", "#FF191919"),
			Tile("music", "panchiko", "Panchiko", 458.0, 150.0, 348.0, 148.0, color: false, imageEditable: true, "Assets/Tiles/panchikomusic.jpg")
		}));
		num2++;
		span[num2] = new DashboardTabCustomizationViewModel("apps", "Apps", new _003C_003Ez__ReadOnlyArray<DashboardTileCustomizationViewModel>(new DashboardTileCustomizationViewModel[7]
		{
			Tile("apps", "myapps", "My Apps", 0.0, 0.0, 148.0, 148.0, color: true, imageEditable: false, launchEditable: false),
			Tile("apps", "browse", "Browse Apps", 0.0, 150.0, 148.0, 148.0, color: true, imageEditable: false, launchEditable: false),
			Tile("apps", "hbogo", "HBO GO", 150.0, 0.0, 306.0, 298.0, color: false, imageEditable: true, "Assets/Tiles/hbogoapps.jpg"),
			Tile("apps", "netflix", "Netflix", 458.0, 0.0, 158.0, 148.0, color: false, imageEditable: true, "Assets/Tiles/netflixapps.png"),
			Tile("apps", "youtube", "YouTube", 618.0, 0.0, 188.0, 148.0, color: false, imageEditable: true, "Assets/Tiles/youtubeapps.jpg"),
			Tile("apps", "hulu", "Hulu Plus", 458.0, 150.0, 158.0, 148.0, color: false, imageEditable: true),
			Tile("apps", "espn", "ESPN", 618.0, 150.0, 188.0, 148.0, color: false, imageEditable: true)
		}));
		num2++;
		return list;
		static DashboardTileCustomizationViewModel Tile(string tab, string id, string title, double left, double top, double width, double height, bool color, bool imageEditable, string image = "", string placeholderColor = "#FF202628", string titleColor = "#FFFFFFFF", string defaultSecondaryTitle = "", bool launchEditable = true)
		{
			return new DashboardTileCustomizationViewModel(tab + "." + id, title, tab, left, top, width, height, color, imageEditable, image, placeholderColor, titleColor, defaultSecondaryTitle, launchEditable);
		}
	}

	public DashboardTileCustomization? GetDashboardTileCustomization(string key)
	{
		if (string.IsNullOrWhiteSpace(key) || !Settings.DashboardTileCustomizations.TryGetValue(key, out DashboardTileCustomization value))
		{
			return null;
		}
		return value;
	}

	public string GetDashboardTileTitle(string key, string fallback)
	{
		if (string.IsNullOrWhiteSpace(key) || !Settings.DashboardTileCustomizations.TryGetValue(key, out DashboardTileCustomization value) || string.IsNullOrWhiteSpace(value.TitleOverride))
		{
			return fallback;
		}
		return value.TitleOverride;
	}

	public string GetDashboardTileSecondaryTitle(string key, string fallback)
	{
		if (string.IsNullOrWhiteSpace(key) || !Settings.DashboardTileCustomizations.TryGetValue(key, out DashboardTileCustomization value) || string.IsNullOrWhiteSpace(value.SecondaryTitleOverride))
		{
			return fallback;
		}
		return value.SecondaryTitleOverride;
	}

	private bool TryLaunchDashboardTileOverride(string? key)
	{
		if (string.IsNullOrWhiteSpace(key) || !AllowsDashboardTileLaunchCustomization(key) || !Settings.DashboardTileCustomizations.TryGetValue(key, out DashboardTileCustomization value))
		{
			return false;
		}
		string text = value.LaunchExecutablePath?.Trim() ?? string.Empty;
		if (!string.IsNullOrWhiteSpace(text))
		{
			try
			{
				if (IsWebLaunchTarget(text))
				{
					string url = NormalizeDashboardLaunchUrl(text);
					Process.Start(new ProcessStartInfo
					{
						FileName = url,
						UseShellExecute = true
					});
					StatusMessage = "Opening " + url;
					_audioService.Play("select");
					return true;
				}
				if (!File.Exists(text))
				{
					StatusMessage = "Executable not found";
					_audioService.Play("back");
					return true;
				}
				Process.Start(new ProcessStartInfo
				{
					FileName = text,
					UseShellExecute = true,
					WorkingDirectory = Path.GetDirectoryName(text) ?? AppPaths.AppFolder
				});
				StatusMessage = "Launching " + Path.GetFileNameWithoutExtension(text);
				_audioService.Play("select");
			}
			catch (Exception ex)
			{
				StatusMessage = ex.Message;
				_audioService.Play("back");
			}
			return true;
		}
		string text2 = value.LaunchWebAddress?.Trim() ?? string.Empty;
		if (string.IsNullOrWhiteSpace(text2))
		{
			return false;
		}
		text2 = NormalizeDashboardLaunchUrl(text2);
		try
		{
			Process.Start(new ProcessStartInfo
			{
				FileName = text2,
				UseShellExecute = true
			});
			StatusMessage = "Opening " + text2;
			_audioService.Play("select");
		}
		catch (Exception ex2)
		{
			StatusMessage = ex2.Message;
			_audioService.Play("back");
		}
		return true;
	}

	private static bool IsWebLaunchTarget(string value)
	{
		value = value.Trim();
		if (Uri.TryCreate(value, UriKind.Absolute, out Uri? uri))
		{
			return uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps;
		}
		return value.Contains('.') && !Path.IsPathRooted(value) && !value.Contains(':') && !value.Contains(Path.DirectorySeparatorChar);
	}

	private static string NormalizeDashboardLaunchUrl(string value)
	{
		string text = value.Trim();
		if (!Uri.TryCreate(text, UriKind.Absolute, out Uri? result) || (result.Scheme != Uri.UriSchemeHttp && result.Scheme != Uri.UriSchemeHttps))
		{
			text = "https://" + text;
		}
		return text;
	}

	private static bool AllowsDashboardTileLaunchCustomization(string key)
	{
		switch (key)
		{
		case "home.open-tray":
		case "home.pins":
		case "home.recent":
		case "video.apps":
		case "video.marketplace":
		case "games.mygames":
		case "games.marketplace":
		case "music.player":
		case "music.marketplace":
		case "apps.myapps":
		case "apps.browse":
			return false;
		default:
			return true;
		}
	}

	private void LaunchDashboardTile(string? key)
	{
		if (TryLaunchDashboardTileOverride(key))
		{
			return;
		}
		switch (key)
		{
		case "home.open-tray":
			_ = LaunchGameAsync(TrayGame);
			break;
		case "home.pins":
			OpenMyPins();
			break;
		case "home.recent":
			OpenGameDetails();
			break;
		case "home.youtube":
			OpenYouTube();
			break;
		case "games.mygames":
			OpenMyGames();
			break;
		case "apps.myapps":
			OpenMyApps();
			break;
		case "music.player":
			OpenMusicPlayer();
			break;
		case "music.search":
			OpenSearch();
			break;
		}
	}

	private void MoveDashboardCustomizerTab(int step)
	{
		if (DashboardCustomizationTabs.Count != 0)
		{
			_customizerTabIndex = (_customizerTabIndex + step + DashboardCustomizationTabs.Count) % DashboardCustomizationTabs.Count;
			SelectDefaultDashboardCustomizerTile();
			OnPropertyChanged("CurrentDashboardCustomizationTab");
			OnPropertyChanged("CurrentDashboardCustomizationTabName");
			_audioService.Play("tab");
		}
	}

	private void SelectDefaultDashboardCustomizerTile()
	{
		SelectedDashboardTile = CurrentDashboardCustomizationTab?.Tiles.FirstOrDefault((DashboardTileCustomizationViewModel tile) => tile.AllowsImageCustomization) ?? CurrentDashboardCustomizationTab?.Tiles.FirstOrDefault();
	}

	private void SelectDashboardTile(DashboardTileCustomizationViewModel? tile)
	{
		if (tile != null)
		{
			SelectedDashboardTile = tile;
			_audioService.Play("select");
		}
	}

	private async Task ChooseDashboardTileImageAsync(object? _)
	{
		if (SelectedDashboardTile == null || !SelectedDashboardTile.AllowsImageCustomization)
		{
			StatusMessage = "This tile uses the global tile color";
			return;
		}
		string text = _filePickerService.PickImage();
		if (!string.IsNullOrWhiteSpace(text))
		{
			SelectedDashboardTile.ImagePath = text;
			PersistSelectedDashboardTile();
			await SaveDashboardCustomizationAsync();
			StatusMessage = "Customized " + SelectedDashboardTile.Title;
		}
	}

	private async Task ChooseDashboardTileExecutableAsync(object? _)
	{
		if (SelectedDashboardTile == null)
		{
			StatusMessage = "Select a tile first";
			return;
		}
		if (!SelectedDashboardTile.AllowsLaunchCustomization)
		{
			StatusMessage = SelectedDashboardTile.Title + " already has a dashboard action";
			return;
		}
		string text = _filePickerService.PickExecutable();
		if (!string.IsNullOrWhiteSpace(text))
		{
			SelectedDashboardTile.LaunchExecutablePath = text;
			PersistSelectedDashboardTile();
			await SaveDashboardCustomizationAsync();
			StatusMessage = "Set launch app for " + SelectedDashboardTile.Title;
		}
	}

	private async Task ResetDashboardTileImageAsync(object? _)
	{
		if (SelectedDashboardTile == null || !SelectedDashboardTile.AllowsImageCustomization)
		{
			StatusMessage = "This tile uses the global tile color";
			return;
		}
		SelectedDashboardTile.ResetImage();
		PersistSelectedDashboardTile();
		RefreshDashboardTileBindings();
		await SaveDashboardCustomizationAsync();
		StatusMessage = "Reset " + SelectedDashboardTile.Title;
	}

	private async Task ResetSelectedCoverTransformAsync(object? _)
	{
		if (SelectedGame == null)
		{
			StatusMessage = "Choose a game first";
			return;
		}
		GameCoverFitMode = "Auto";
		SelectedCoverZoom = DefaultGameCoverZoom;
		SelectedGame.Game.CoverOffsetX = 0.0;
		SelectedGame.Game.CoverOffsetY = 0.0;
		SelectedGame.Refresh();
		OnPropertyChanged("SelectedCoverZoom");
		OnPropertyChanged("SelectedCoverOffsetX");
		OnPropertyChanged("SelectedCoverOffsetY");
		await PersistLibraryAsync();
		await SaveSettingsAsync();
		StatusMessage = "Cover fit reset";
	}

	private async Task ResetDashboardTileTitleAsync(object? _)
	{
		if (SelectedDashboardTile != null)
		{
			SelectedDashboardTile.ResetTitle();
			PersistSelectedDashboardTile();
			RefreshDashboardTileBindings();
			OnPropertyChanged("SelectedDashboardTileTitle");
			await SaveDashboardCustomizationAsync();
			StatusMessage = "Reset " + SelectedDashboardTile.DefaultTitle + " text";
		}
	}

	private async Task ResetDashboardTabImagesAsync(object? _)
	{
		DashboardTabCustomizationViewModel tab = CurrentDashboardCustomizationTab;
		if (tab == null)
		{
			return;
		}
		foreach (DashboardTileCustomizationViewModel tile in tab.Tiles)
		{
			Settings.DashboardTileCustomizations.Remove(tile.Key);
			tile.ResetImage();
			tile.ResetTitle();
			tile.ResetLaunchTargets();
		}
		RefreshDashboardTileBindings();
		await SaveDashboardCustomizationAsync();
		StatusMessage = tab.Name + " custom images reset";
	}

	private void OpenDashboardTileColorPicker()
	{
		System.Windows.Media.Color color = ParseColor(Settings.DashboardTileColor);
		using ColorDialog colorDialog = new ColorDialog
		{
			FullOpen = true,
			Color = System.Drawing.Color.FromArgb(color.R, color.G, color.B)
		};
		if (colorDialog.ShowDialog() == DialogResult.OK)
		{
			_customTileRed = colorDialog.Color.R;
			_customTileGreen = colorDialog.Color.G;
			_customTileBlue = colorDialog.Color.B;
			OnPropertyChanged("CustomTileRed");
			OnPropertyChanged("CustomTileGreen");
			OnPropertyChanged("CustomTileBlue");
			UpdateDashboardTileColorFromSliders();
			StatusMessage = "Dashboard tile color updated";
		}
	}

	private async Task ResetDashboardTileColorAsync(object? _)
	{
		Settings.DashboardTileColor = "#FF028D02";
		ApplyDashboardAccentResources(Settings.DashboardTileColor);
		ApplyDashboardTileColorToSliders(Settings.DashboardTileColor);
				OnPropertyChanged("DashboardTileBrush");
				OnPropertyChanged("DashboardTileDarkBrush");
				OnPropertyChanged("DashboardTileColorPreviewBrush");
		await SaveDashboardCustomizationAsync();
		StatusMessage = "Dashboard tile color reset";
	}

	private void SelectedDashboardTile_OnPropertyChanged(object? sender, PropertyChangedEventArgs e)
	{
		bool flag;
		switch (e.PropertyName)
		{
		case "Zoom":
		case "OffsetX":
		case "OffsetY":
		case "ImagePath":
		case "TitleOverride":
		case "SecondaryTitleOverride":
		case "Title":
		case "SecondaryTitle":
		case "LaunchExecutablePath":
		case "LaunchWebAddress":
			flag = true;
			break;
		default:
			flag = false;
			break;
		}
		if (flag)
		{
			PersistSelectedDashboardTile();
			RefreshDashboardTileBindings();
			OnPropertyChanged("SelectedDashboardTileTitle");
			SaveDashboardCustomizationAsync();
		}
	}

	private void PersistSelectedDashboardTile()
	{
		if (SelectedDashboardTile != null)
		{
			if (!SelectedDashboardTile.HasCustomization)
			{
				Settings.DashboardTileCustomizations.Remove(SelectedDashboardTile.Key);
				return;
			}
			Settings.DashboardTileCustomizations[SelectedDashboardTile.Key] = new DashboardTileCustomization
			{
				ImagePath = SelectedDashboardTile.ImagePath,
				TitleOverride = SelectedDashboardTile.TitleOverride,
				SecondaryTitleOverride = SelectedDashboardTile.SecondaryTitleOverride,
				LaunchExecutablePath = SelectedDashboardTile.LaunchExecutablePath,
				LaunchWebAddress = SelectedDashboardTile.LaunchWebAddress,
				Zoom = SelectedDashboardTile.Zoom,
				OffsetX = SelectedDashboardTile.OffsetX,
				OffsetY = SelectedDashboardTile.OffsetY
			};
		}
	}

	private void SyncDashboardCustomizerFromSettings()
	{
		foreach (DashboardTileCustomizationViewModel item in DashboardCustomizationTabs.SelectMany((DashboardTabCustomizationViewModel tab) => tab.Tiles))
		{
			DashboardTileCustomization value2;
			if (!item.AllowsImageCustomization)
			{
				item.ResetImage();
				if (Settings.DashboardTileCustomizations.TryGetValue(item.Key, out DashboardTileCustomization value))
				{
					item.TitleOverride = value.TitleOverride;
					item.SecondaryTitleOverride = value.SecondaryTitleOverride;
					item.LaunchExecutablePath = value.LaunchExecutablePath;
					item.LaunchWebAddress = value.LaunchWebAddress;
					if (!item.HasCustomization)
					{
						Settings.DashboardTileCustomizations.Remove(item.Key);
					}
				}
			}
			else if (Settings.DashboardTileCustomizations.TryGetValue(item.Key, out value2))
			{
				item.ImagePath = value2.ImagePath;
				item.TitleOverride = value2.TitleOverride;
				item.SecondaryTitleOverride = value2.SecondaryTitleOverride;
				item.LaunchExecutablePath = value2.LaunchExecutablePath;
				item.LaunchWebAddress = value2.LaunchWebAddress;
				item.Zoom = ((value2.Zoom <= 0.0) ? 1.0 : value2.Zoom);
				item.OffsetX = value2.OffsetX;
				item.OffsetY = value2.OffsetY;
			}
			else
			{
				item.ResetImage();
				item.ResetTitle();
				item.ResetLaunchTargets();
			}
		}
	}

	private async Task SaveDashboardCustomizationAsync()
	{
		await _settingsService.SaveAsync(Settings);
		OnPropertyChanged("DashboardTileBrush");
		OnPropertyChanged("DashboardTileDarkBrush");
		OnPropertyChanged("DashboardTileColorPreviewBrush");
	}

	private void RefreshDashboardTileBindings()
	{
		OnPropertyChanged("Settings");
		OnPropertyChanged("DashboardTileBrush");
		OnPropertyChanged("DashboardTileDarkBrush");
		OnPropertyChanged("DashboardTileColorPreviewBrush");
	}

	private void UpdateDashboardTileColorFromSliders()
	{
		Settings.DashboardTileColor = $"#FF{_customTileRed:X2}{_customTileGreen:X2}{_customTileBlue:X2}";
		ApplyDashboardAccentResources(Settings.DashboardTileColor);
		OnPropertyChanged("DashboardTileBrush");
		OnPropertyChanged("DashboardTileDarkBrush");
		OnPropertyChanged("DashboardTileColorPreviewBrush");
		SaveDashboardCustomizationAsync();
	}

	private void ApplyDashboardTileColorToSliders(string color)
	{
		System.Windows.Media.Color color2 = ParseColor(color);
		_customTileRed = color2.R;
		_customTileGreen = color2.G;
		_customTileBlue = color2.B;
		OnPropertyChanged("CustomTileRed");
		OnPropertyChanged("CustomTileGreen");
		OnPropertyChanged("CustomTileBlue");
	}

	private static void ApplyDashboardAccentResources(string color)
	{
		System.Windows.Media.Color color2 = ParseColor(color);
		System.Windows.Media.Color color3 = ToDarkAccentColor(color2);
		System.Windows.Media.Color color4 = System.Windows.Media.Color.FromArgb(204, (byte)Math.Max(0.0, (double)(int)color2.R * 0.5), (byte)Math.Max(0.0, (double)(int)color2.G * 0.5), (byte)Math.Max(0.0, (double)(int)color2.B * 0.5));
		ApplyAccentResourceSet(System.Windows.Application.Current.Resources, color2, color3, color4);
		foreach (Window window in System.Windows.Application.Current.Windows)
		{
			ApplyAccentResourceSet(window.Resources, color2, color3, color4);
		}
	}

	private static void ApplyAccentResourceSet(ResourceDictionary resources, System.Windows.Media.Color accent, System.Windows.Media.Color darkAccent, System.Windows.Media.Color pressedAccent)
	{
		resources["MetroGreenBrush"] = new SolidColorBrush(accent);
		resources["MetroGreenDarkBrush"] = new SolidColorBrush(darkAccent);
		resources["MetroTilePressedBrush"] = new SolidColorBrush(pressedAccent);
	}

	private static string NormalizeDashboardTileColor(string? color)
	{
		try
		{
			return ParseColor(color).ToString(CultureInfo.InvariantCulture);
		}
		catch
		{
			return "#FF028D02";
		}
	}

	private static string ToDarkAccentColor(string color)
	{
		return ToDarkAccentColor(ParseColor(color)).ToString(CultureInfo.InvariantCulture);
	}

	private static System.Windows.Media.Color ToDarkAccentColor(System.Windows.Media.Color color)
	{
		return System.Windows.Media.Color.FromArgb(color.A, (byte)Math.Max(0.0, (double)(int)color.R * 0.58), (byte)Math.Max(0.0, (double)(int)color.G * 0.58), (byte)Math.Max(0.0, (double)(int)color.B * 0.58));
	}

	private static SolidColorBrush CreateBrush(string color)
	{
		SolidColorBrush solidColorBrush = new SolidColorBrush(ParseColor(color));
		if (((Freezable)solidColorBrush).CanFreeze)
		{
			((Freezable)solidColorBrush).Freeze();
		}
		return solidColorBrush;
	}

	private static LinearGradientBrush CreateDashboardTileBrush(string color)
	{
		System.Windows.Media.Color accent = ParseColor(color);
		LinearGradientBrush linearGradientBrush = new LinearGradientBrush
		{
			StartPoint = new System.Windows.Point(0.0, 0.0),
			EndPoint = new System.Windows.Point(1.0, 1.0)
		};
		linearGradientBrush.GradientStops.Add(new GradientStop(AdjustColor(accent, 28), 0.0));
		linearGradientBrush.GradientStops.Add(new GradientStop(AdjustColor(accent, 10), 0.28));
		linearGradientBrush.GradientStops.Add(new GradientStop(accent, 0.58));
		linearGradientBrush.GradientStops.Add(new GradientStop(AdjustColor(accent, -16), 1.0));
		if (((Freezable)linearGradientBrush).CanFreeze)
		{
			((Freezable)linearGradientBrush).Freeze();
		}
		return linearGradientBrush;
	}

	private static System.Windows.Media.Color AdjustColor(System.Windows.Media.Color color, int delta)
	{
		return System.Windows.Media.Color.FromArgb(color.A, AdjustChannel(color.R, delta), AdjustChannel(color.G, delta), AdjustChannel(color.B, delta));
	}

	private static byte AdjustChannel(byte value, int delta)
	{
		return (byte)Math.Clamp(value + delta, 0, 255);
	}

	private static System.Windows.Media.Color ParseColor(string? color)
	{
		if (string.IsNullOrWhiteSpace(color))
		{
			return System.Windows.Media.Color.FromRgb(2, 141, 2);
		}
		return (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(color);
	}

	private static byte ToByte(double value)
	{
		return (byte)Math.Clamp((int)Math.Round(value), 0, 255);
	}

	private async Task SelectThemeAsync(object? parameter)
	{
		DashboardTheme theme = (parameter as DashboardTheme) ?? SelectedTheme ?? AvailableThemes.FirstOrDefault((DashboardTheme themeItem) => themeItem.IsBuiltIn) ?? new DashboardTheme
		{
			Name = "Xbox 360",
			IsBuiltIn = true
		};
		ApplySelectedTheme(theme.Name);
		Settings.ThemeName = theme.Name;
		await _settingsService.SaveAsync(Settings);
		IsThemeMenuOpen = false;
		OnPropertyChanged("CurrentThemeBackgroundPath");
		StatusMessage = (theme.IsBuiltIn ? "Xbox 360 theme restored" : ("Theme selected: " + theme.Name));
		_audioService.Play("select");
	}

	private async Task ChooseThemeSectionImageAsync(string sectionKey)
	{
		string text = _filePickerService.PickImage();
		if (!string.IsNullOrWhiteSpace(text))
		{
			switch (sectionKey)
			{
			case "home":
				ThemeHomePreviewPath = text;
				break;
			case "games":
				ThemeGamesPreviewPath = text;
				break;
			case "settings":
				ThemeSettingsPreviewPath = text;
				break;
			case "apps":
				ThemeAppsPreviewPath = text;
				break;
			}
			await Task.CompletedTask;
			StatusMessage = "Theme preview updated";
		}
	}

	private async Task SaveThemeAsync(object? _)
	{
		if (string.IsNullOrWhiteSpace(ThemeNameInput))
		{
			StatusMessage = "Enter a theme name first";
			return;
		}
		DashboardTheme createdTheme = await _themeService.CreateThemeAsync(ThemeNameInput, ThemeHomePreviewPath, ThemeGamesPreviewPath, ThemeSettingsPreviewPath, ThemeAppsPreviewPath);
		await LoadThemesAsync();
		ApplySelectedTheme(createdTheme.Name);
		Settings.ThemeName = createdTheme.Name;
		await _settingsService.SaveAsync(Settings);
		IsThemeCreatorOpen = false;
		OnPropertyChanged("CurrentThemeBackgroundPath");
		ResetPendingThemeDraft();
		StatusMessage = "Created theme: " + createdTheme.Name;
		_audioService.Play("select");
	}

	private void ApplySelectedTheme(string? themeName)
	{
		string normalizedName = NormalizeThemeName(themeName);
		SelectedTheme = AvailableThemes.FirstOrDefault((DashboardTheme theme) => string.Equals(theme.Name, normalizedName, StringComparison.OrdinalIgnoreCase)) ?? AvailableThemes.FirstOrDefault((DashboardTheme theme) => theme.IsBuiltIn) ?? new DashboardTheme
		{
			Name = "Xbox 360",
			IsBuiltIn = true
		};
		OnPropertyChanged("ThemeMenuVisibilityTitle");
		OnPropertyChanged("CurrentThemeBackgroundPath");
	}

	private static string NormalizeThemeName(string? themeName)
	{
		if (string.IsNullOrWhiteSpace(themeName) || string.Equals(themeName, "Metro Green", StringComparison.OrdinalIgnoreCase))
		{
			return "Xbox 360";
		}
		return themeName.Trim();
	}

	private string ResolveThemeSectionKey()
	{
		if (IsLauncherSettingsOpen)
		{
			return "settings";
		}
		if (IsMyGamesOpen)
		{
			if (!_isLibraryShowingApps)
			{
				return "games";
			}
			return "apps";
		}
		return CurrentTab?.Key switch
		{
			"bing" => "home", 
			"home" => "home", 
			"social" => "home", 
			"video" => "home", 
			"games" => "home", 
			"music" => "home", 
			"apps" => "home", 
			"settings" => "home", 
			_ => "home", 
		};
	}

	private void ResetPendingThemeDraft()
	{
		ThemeNameInput = string.Empty;
		ThemeHomePreviewPath = string.Empty;
		ThemeGamesPreviewPath = string.Empty;
		ThemeSettingsPreviewPath = string.Empty;
		ThemeAppsPreviewPath = string.Empty;
	}

	private void EnsureProfileDefaults()
	{
		string gamerPicturePath = Path.Combine("Assets", "Profile", "profilepicture.jpg");
		if (string.IsNullOrWhiteSpace(Profile.Gamertag))
		{
			Profile.Gamertag = "MetroPilot";
		}
		if (string.IsNullOrWhiteSpace(Profile.Name))
		{
			Profile.Name = "(No name)";
		}
		if (string.IsNullOrWhiteSpace(Profile.GamerPicturePath) || IsOldDefaultProfilePicture(Profile.GamerPicturePath) || IsPackagedDefaultProfilePicture(Profile.GamerPicturePath))
		{
			Profile.GamerPicturePath = gamerPicturePath;
		}
		if (string.IsNullOrWhiteSpace(Profile.OnlineStatus))
		{
			Profile.OnlineStatus = "Online";
		}
		if (string.IsNullOrWhiteSpace(Profile.Motto))
		{
			Profile.Motto = "(No motto)";
		}
		if (string.IsNullOrWhiteSpace(Profile.Location))
		{
			Profile.Location = "United States";
		}
		if (string.IsNullOrWhiteSpace(Profile.Description))
		{
			Profile.Description = "(No bio)";
		}
	}

	private static bool IsOldDefaultProfilePicture(string path)
	{
		if (path.EndsWith(Path.Combine("Assets", "Art", "profilepicture.jpg"), StringComparison.OrdinalIgnoreCase))
		{
			return !File.Exists(path);
		}
		return false;
	}

	private static bool IsPackagedDefaultProfilePicture(string path)
	{
		return Path.IsPathRooted(path) && path.EndsWith(Path.Combine("Assets", "Profile", "profilepicture.jpg"), StringComparison.OrdinalIgnoreCase);
	}

	private static string NormalizeGameCoverFitMode(string? mode)
	{
		bool flag;
		switch (mode)
		{
		case "Cover":
		case "Fill":
		case "Fit":
			flag = true;
			break;
		default:
			flag = false;
			break;
		}
		if (!flag)
		{
			return "Auto";
		}
		return mode;
	}

	private static string NormalizeAddDestination(string? destination)
	{
		if (!string.Equals(destination, "My Apps", StringComparison.OrdinalIgnoreCase))
		{
			return "My Games";
		}
		return "My Apps";
	}

	private static string NormalizeDisplayAspectRatio(string? value)
	{
		if (string.Equals(value, "21:9", StringComparison.OrdinalIgnoreCase) || string.Equals(value, "Ultrawide", StringComparison.OrdinalIgnoreCase))
		{
			return "21:9";
		}
		return "16:9";
	}

	private static string NormalizeDashboardStyle(string? value)
	{
		if (string.Equals(value, "Blades", StringComparison.OrdinalIgnoreCase))
		{
			return "Blades";
		}
		return "Metro";
	}

	private static string NormalizeDashboardThemeChoice(string? value)
	{
		if (string.Equals(value, "Blades", StringComparison.OrdinalIgnoreCase))
		{
			return "Blades";
		}
		return "Metro";
	}

	private string NormalizeAudioOutputDeviceName(string? deviceName)
	{
		return AudioOutputDeviceOptions.FirstOrDefault((string option) => string.Equals(option, deviceName, StringComparison.OrdinalIgnoreCase)) ?? "Default";
	}

	private void ApplyMusicPlayerVolume()
	{
		_musicPlayer.Volume = Math.Clamp(MusicVolume * DashboardVolume, 0.0, 1.0);
	}

	public void RefreshAudioOutputDevices()
	{
		IReadOnlyList<string> readOnlyList;
		try
		{
			readOnlyList = _audioService.GetOutputDeviceNames();
		}
		catch (Exception exception)
		{
			App.LogException(exception, "DashboardViewModel.RefreshAudioOutputDevices");
			readOnlyList = new _003C_003Ez__ReadOnlySingleElementList<string>("Default");
		}
		if (readOnlyList.Count == 0)
		{
			readOnlyList = new _003C_003Ez__ReadOnlySingleElementList<string>("Default");
		}
		string text = Settings.AudioOutputDeviceName;
		if (string.IsNullOrWhiteSpace(text))
		{
			text = "Default";
		}
		try
		{
			_isRefreshingAudioOutputDevices = true;
			AudioOutputDeviceOptions.Clear();
			foreach (string item in readOnlyList)
			{
				if (!AudioOutputDeviceOptions.Any((string option) => string.Equals(option, item, StringComparison.OrdinalIgnoreCase)))
				{
					AudioOutputDeviceOptions.Add(item);
				}
			}
		}
		finally
		{
			_isRefreshingAudioOutputDevices = false;
		}
		string text2 = AudioOutputDeviceOptions.Any((string option) => string.Equals(option, text, StringComparison.OrdinalIgnoreCase)) ? NormalizeAudioOutputDeviceName(text) : "Default";
		if (!string.Equals(Settings.AudioOutputDeviceName, text2, StringComparison.Ordinal))
		{
			Settings.AudioOutputDeviceName = text2;
			OnPropertyChanged("AudioOutputDeviceName");
		}
		else
		{
			OnPropertyChanged("AudioOutputDeviceName");
		}
	}

	private static SocialIntegrationMode NormalizeSocialIntegrationMode(SocialIntegrationMode mode)
	{
		return SocialIntegrationMode.LocalOnly;
	}

	private static string ToSocialIntegrationDisplay(SocialIntegrationMode mode)
	{
		return "Local";
	}

	private static SocialIntegrationMode ParseSocialIntegrationMode(string? mode)
	{
		return SocialIntegrationMode.LocalOnly;
	}

	private static bool IsAppEntry(GameMetadata game)
	{
		return string.Equals(game.Genre, "App", StringComparison.OrdinalIgnoreCase);
	}

	private static bool IsBuiltInYouTubeEntry(GameMetadata game)
	{
		return string.Equals(game.Title, "YouTube", StringComparison.OrdinalIgnoreCase) || game.ExecutablePath.Contains("youtube.com", StringComparison.OrdinalIgnoreCase);
	}

	private void RefreshSetupDestinationState()
	{
		OnPropertyChanged("IsAddingToApps");
		EnsureSelectedSetupItem();
		OnPropertyChanged("SetupEditableItems");
		OnPropertyChanged("SetupItemLabel");
		OnPropertyChanged("SetupItemTag");
		OnPropertyChanged("SetupCoverFitLabel");
		OnPropertyChanged("SetupCoverFitTag");
		OnPropertyChanged("SelectedCoverPreviewWidth");
		OnPropertyChanged("SelectedCoverPreviewHeight");
	}

	private void EnsureSelectedSetupItem()
	{
		List<GameCardViewModel> list = SetupEditableItems.ToList();
		if (list.Count > 0 && (SelectedGame == null || !list.Contains(SelectedGame)))
		{
			SelectedGame = list.First();
		}
	}

	private void SortGamesByTitle(string? selectedGameId = null)
	{
		if (selectedGameId == null)
		{
			selectedGameId = SelectedGame?.Game.Id;
		}
		_library.Games = _library.Games.OrderBy<GameMetadata, string>((GameMetadata game) => game.Title, StringComparer.CurrentCultureIgnoreCase).ToList();
		List<GameCardViewModel> list = Games.OrderBy<GameCardViewModel, string>((GameCardViewModel game) => game.Title, StringComparer.CurrentCultureIgnoreCase).ToList();
		Games.Clear();
		foreach (GameCardViewModel item in list)
		{
			Games.Add(item);
		}
		SelectedGame = Games.FirstOrDefault((GameCardViewModel game) => string.Equals(game.Game.Id, selectedGameId, StringComparison.OrdinalIgnoreCase)) ?? Games.FirstOrDefault();
		FeaturedGame = SelectedGame;
	}

	private async Task PersistLibraryAsync()
	{
		await _libraryService.SaveAsync(_library);
		RefreshDerivedLists();
	}

	private void SyncGamesCollectionFromLibrary()
	{
		Games.Clear();
		int num = 0;
		foreach (GameMetadata game in _library.Games)
		{
			Games.Add(new GameCardViewModel(game, _accentBrushes[num++ % _accentBrushes.Count]));
		}
	}

	private void WriteSteamScanDebugReport(SteamGameScanResult result)
	{
		try
		{
			Directory.CreateDirectory(Path.GetDirectoryName(SteamScanDebugLogPath));
			StringBuilder stringBuilder = new StringBuilder();
			stringBuilder.AppendLine("[STEAM SCAN]");
			StringBuilder stringBuilder2 = stringBuilder;
			StringBuilder stringBuilder3 = stringBuilder2;
			StringBuilder.AppendInterpolatedStringHandler handler = new StringBuilder.AppendInterpolatedStringHandler(7, 1, stringBuilder2);
			handler.AppendLiteral("added: ");
			handler.AppendFormatted(result.Added);
			stringBuilder3.AppendLine(ref handler);
			stringBuilder2 = stringBuilder;
			StringBuilder stringBuilder4 = stringBuilder2;
			handler = new StringBuilder.AppendInterpolatedStringHandler(9, 1, stringBuilder2);
			handler.AppendLiteral("updated: ");
			handler.AppendFormatted(result.Updated);
			stringBuilder4.AppendLine(ref handler);
			stringBuilder2 = stringBuilder;
			StringBuilder stringBuilder5 = stringBuilder2;
			handler = new StringBuilder.AppendInterpolatedStringHandler(9, 1, stringBuilder2);
			handler.AppendLiteral("skipped: ");
			handler.AppendFormatted(result.Skipped);
			stringBuilder5.AppendLine(ref handler);
			stringBuilder2 = stringBuilder;
			StringBuilder stringBuilder6 = stringBuilder2;
			handler = new StringBuilder.AppendInterpolatedStringHandler(9, 1, stringBuilder2);
			handler.AppendLiteral("message: ");
			handler.AppendFormatted(result.Message);
			stringBuilder6.AppendLine(ref handler);
			stringBuilder2 = stringBuilder;
			StringBuilder stringBuilder7 = stringBuilder2;
			handler = new StringBuilder.AppendInterpolatedStringHandler(26, 1, stringBuilder2);
			handler.AppendLiteral("saved library game count: ");
			handler.AppendFormatted(_library.Games.Count);
			stringBuilder7.AppendLine(ref handler);
			stringBuilder2 = stringBuilder;
			StringBuilder stringBuilder8 = stringBuilder2;
			handler = new StringBuilder.AppendInterpolatedStringHandler(25, 1, stringBuilder2);
			handler.AppendLiteral("loaded Games menu count: ");
			handler.AppendFormatted(Games.Count);
			stringBuilder8.AppendLine(ref handler);
			stringBuilder2 = stringBuilder;
			StringBuilder stringBuilder9 = stringBuilder2;
			handler = new StringBuilder.AppendInterpolatedStringHandler(24, 1, stringBuilder2);
			handler.AppendLiteral("my games visible count: ");
			handler.AppendFormatted(Games.Count((GameCardViewModel game) => !IsAppEntry(game.Game)));
			stringBuilder9.AppendLine(ref handler);
			stringBuilder2 = stringBuilder;
			StringBuilder stringBuilder10 = stringBuilder2;
			handler = new StringBuilder.AppendInterpolatedStringHandler(19, 1, stringBuilder2);
			handler.AppendLiteral("library file path: ");
			handler.AppendFormatted(Path.Combine(AppPaths.AppFolder, "UserData", "library.json"));
			stringBuilder10.AppendLine(ref handler);
			File.WriteAllText(SteamScanDebugLogPath, stringBuilder.ToString());
		}
		catch
		{
		}
	}

	private void RefreshDerivedLists()
	{
		OnPropertyChanged("RecentGames");
		OnPropertyChanged("PinnedGames");
		OnPropertyChanged("ImportedGames");
		OnPropertyChanged("LibraryPaths");
		OnPropertyChanged("MyGamesCountText");
		OnPropertyChanged("LibraryMenuGames");
		RebuildAppLibraryTiles();
		RefreshVisibleLibraryMenuGames();
		OnPropertyChanged("LibraryMenuCountText");
		OnPropertyChanged("SetupEditableItems");
	}

	private IEnumerable<GameCardViewModel> GetLibraryMenuGames()
	{
		if (!_isLibraryShowingPins)
		{
			if (!_isLibraryShowingApps)
			{
				return Games.Where((GameCardViewModel game) => !IsAppEntry(game.Game));
			}
			return Games.Where((GameCardViewModel game) => IsAppEntry(game.Game));
		}
		return Games.Where((GameCardViewModel game) => game.Game.IsFavorite);
	}

	private void RefreshVisibleLibraryMenuGames()
	{
		List<GameCardViewModel> list = GetLibraryMenuGames().ToList();
		if (list.Count == 0)
		{
			_libraryMenuStartIndex = 0;
			VisibleLibraryMenuGames.Clear();
			return;
		}
		int num = ((SelectedGame != null) ? list.IndexOf(SelectedGame) : 0);
		if (num < 0)
		{
			num = 0;
		}
		const int pageSize = 6;
		int pageStart = num / pageSize * pageSize;
		_libraryMenuStartIndex = Math.Clamp(pageStart, 0, Math.Max(0, list.Count - 1));
		int visibleStart = Math.Max(0, _libraryMenuStartIndex - 1);
		int visibleEndExclusive = Math.Min(list.Count, _libraryMenuStartIndex + pageSize + 1);
		List<GameCardViewModel> list2 = list.Skip(visibleStart).Take(visibleEndExclusive - visibleStart).ToList();
		if (VisibleLibraryMenuGames.Count == list2.Count && VisibleLibraryMenuGames.Zip(list2).All(((GameCardViewModel First, GameCardViewModel Second) pair) => pair.First == pair.Second))
		{
			OnPropertyChanged("LibraryMenuScrollOffset");
			return;
		}
		VisibleLibraryMenuGames.Clear();
		foreach (GameCardViewModel item in list2)
		{
			VisibleLibraryMenuGames.Add(item);
		}
		OnPropertyChanged("LibraryMenuScrollOffset");
	}

	private void OnGamesChanged(object? sender, NotifyCollectionChangedEventArgs e)
	{
		RefreshDerivedLists();
	}

	private void RunningGameService_OnStateChanged(object? sender, EventArgs e)
	{
		System.Windows.Application current = System.Windows.Application.Current;
		Dispatcher val = ((current != null) ? ((DispatcherObject)current).Dispatcher : null);
		if (val == null || val.CheckAccess())
		{
			HandleRunningGameStateChangedOnUiThread();
		}
		else
		{
			val.BeginInvoke((Delegate)new Action(HandleRunningGameStateChangedOnUiThread), (DispatcherPriority)4, Array.Empty<object>());
		}
	}

	private void HandleRunningGameStateChangedOnUiThread()
	{
		OnPropertyChanged("HasRunningLaunchedGame");
		OnPropertyChanged("RunningLaunchedGameTitle");
		OnPropertyChanged("RunningGameFooterActionText");
		if (!_runningGameService.ConsumePlaytimeUpdate())
		{
			return;
		}
		foreach (GameCardViewModel game in Games)
		{
			game.Refresh();
		}
		PersistLibraryAsync();
	}

	public sealed class BladesAchievementGameViewModel : ObservableObject
	{
		private bool _isSelected;

		public string GameId { get; }

		public string SteamAppId { get; }

		public string Title { get; }

		public string IconPath { get; }

		public bool HasIconPath => !string.IsNullOrWhiteSpace(IconPath);

		public int UnlockedCount { get; }

		public string CountText => UnlockedCount.ToString(CultureInfo.InvariantCulture);

		public bool IsSelected
		{
			get
			{
				return _isSelected;
			}
			set
			{
				SetProperty(ref _isSelected, value, "IsSelected");
			}
		}

		public BladesAchievementGameViewModel(string gameId, string steamAppId, string title, string iconPath, int unlockedCount, bool isSelected)
		{
			GameId = gameId;
			SteamAppId = steamAppId;
			Title = title;
			IconPath = iconPath;
			UnlockedCount = unlockedCount;
			_isSelected = isSelected;
		}
	}

	public sealed class BladesAchievementItemViewModel : ObservableObject
	{
		private bool _isSelected;

		public string GameId { get; }

		public string SteamAppId { get; }

		public string GameTitle { get; }

		public string ApiName { get; }

		public string Title { get; }

		public string Description { get; }

		public string IconPath { get; }

		public bool HasIconPath => !string.IsNullOrWhiteSpace(IconPath);

		public bool IsSelected
		{
			get
			{
				return _isSelected;
			}
			set
			{
				SetProperty(ref _isSelected, value, "IsSelected");
			}
		}

		public BladesAchievementItemViewModel(string gameId, string steamAppId, string gameTitle, string apiName, string title, string description, string iconPath, bool isSelected)
		{
			GameId = gameId;
			SteamAppId = steamAppId;
			GameTitle = gameTitle;
			ApiName = apiName;
			Title = title;
			Description = description;
			IconPath = iconPath;
			_isSelected = isSelected;
		}
	}

	public sealed class DashboardAchievementOpenRequest
	{
		public string SteamAppId { get; }

		public string AchievementApiName { get; }

		public DashboardAchievementOpenRequest(string steamAppId, string achievementApiName)
		{
			SteamAppId = steamAppId;
			AchievementApiName = achievementApiName;
		}
	}

	private sealed class SpotifyMirrorPlaylist
	{
		public string Name { get; }

		public IReadOnlyList<SpotifyMirrorSong> Songs { get; }

		public SpotifyMirrorPlaylist(string name, IReadOnlyList<SpotifyMirrorSong> songs)
		{
			Name = string.IsNullOrWhiteSpace(name) ? "Playlist" : name.Trim();
			Songs = songs;
		}
	}

	private sealed class SpotifyPlaylistShortcut
	{
		public string Name { get; }

		public string Url { get; }

		public SpotifyPlaylistShortcut(string name, string url)
		{
			Name = string.IsNullOrWhiteSpace(name) ? "Spotify Playlist" : name.Trim();
			Url = url.Trim();
		}
	}

	private sealed class SpotifyMirrorSong
	{
		public string Title { get; set; } = string.Empty;

		public string Artist { get; set; } = string.Empty;

		public string Album { get; set; } = string.Empty;

		public string Genre { get; set; } = string.Empty;

		public string Playlist { get; set; } = string.Empty;

		public string DisplayTitle => string.IsNullOrWhiteSpace(Artist) ? Title : Artist + " - " + Title;

		public string Subtitle
		{
			get
			{
				if (!string.IsNullOrWhiteSpace(Album))
				{
					return Album;
				}
				if (!string.IsNullOrWhiteSpace(Playlist))
				{
					return Playlist;
				}
				return "Spotify Mirror";
			}
		}

		public SpotifyMirrorSong()
		{
		}

		public SpotifyMirrorSong(string title, string artist, string album, string genre, string playlist)
		{
			Title = title.Trim();
			Artist = artist.Trim();
			Album = album.Trim();
			Genre = genre.Trim();
			Playlist = playlist.Trim();
		}

		public string ToTrackPath()
		{
			return SpotifyMirrorScheme + Convert.ToBase64String(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(this)));
		}
	}
}
