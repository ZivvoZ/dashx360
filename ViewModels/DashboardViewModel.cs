using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using XboxMetroLauncher.Input;
using XboxMetroLauncher.Models;
using XboxMetroLauncher.Services;
using XboxMetroLauncher.Utilities;
using XboxMetroLauncher.ViewModels.Tabs;

namespace XboxMetroLauncher.ViewModels;

public sealed class DashboardViewModel : ObservableObject
{
    public event EventHandler? FriendsOverlayRequested;

    private readonly IGameLibraryService _libraryService;
    private readonly IGameLaunchService _launchService;
    private readonly ISearchService _searchService;
    private readonly ISettingsService _settingsService;
    private readonly IProfileService _profileService;
    private readonly IFilePickerService _filePickerService;
    private readonly IStartupRegistrationService _startupRegistrationService;
    private readonly IAudioService _audioService;
    private readonly SocialIntegrationManager _socialIntegrationManager;
    private readonly AudioAnalysisService _audioAnalysisService = new();
    private readonly MediaPlayer _musicPlayer = new();
    private readonly DispatcherTimer _musicTimer;
    private readonly List<Brush> _accentBrushes;
    private GameLibrary _library = new();
    private DashboardTabViewModel? _currentTab;
    private GameCardViewModel? _selectedGame;
    private GameCardViewModel? _featuredGame;
    private Profile _profile = new();
    private AppSettings _settings = new();
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
    private bool _isMusicPlayerOpen;
    private bool _isMusicPlayerTransparent;
    private bool _isMusicVisualizerFullscreen;
    private bool _isMusicPlaying;
    private bool _isShuffleEnabled;
    private bool _isBooting = true;
    private string _clockText = string.Empty;
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
    private GameCardViewModel? _trayGame;
    private MusicTrackViewModel? _currentMusicTrack;
    private int _musicIndex = -1;
    private readonly Random _random = new();

    public DashboardViewModel(
        IGameLibraryService libraryService,
        IGameLaunchService launchService,
        ISearchService searchService,
        ISettingsService settingsService,
        IProfileService profileService,
        IFilePickerService filePickerService,
        IStartupRegistrationService startupRegistrationService,
        IAudioService audioService,
        SocialIntegrationManager socialIntegrationManager)
    {
        _libraryService = libraryService;
        _launchService = launchService;
        _searchService = searchService;
        _settingsService = settingsService;
        _profileService = profileService;
        _filePickerService = filePickerService;
        _startupRegistrationService = startupRegistrationService;
        _audioService = audioService;
        _socialIntegrationManager = socialIntegrationManager;
        _musicPlayer.Volume = _musicVolume;
        _musicPlayer.MediaOpened += (_, _) => RefreshMusicProgress();
        _musicPlayer.MediaEnded += (_, _) => NextMusicTrack();
        _audioAnalysisService.FrameReady += AudioAnalysis_OnFrameReady;
        _musicTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
        _musicTimer.Tick += (_, _) => RefreshMusicProgress();

        _accentBrushes =
        [
            new SolidColorBrush(Color.FromRgb(20, 156, 74)),
            new SolidColorBrush(Color.FromRgb(0, 122, 204)),
            new SolidColorBrush(Color.FromRgb(202, 80, 16)),
            new SolidColorBrush(Color.FromRgb(116, 77, 169)),
            new SolidColorBrush(Color.FromRgb(36, 161, 156)),
            new SolidColorBrush(Color.FromRgb(190, 40, 71))
        ];

        Tabs =
        [
            new BingTabViewModel(this),
            new HomeTabViewModel(this),
            new SocialTabViewModel(this),
            new MediaTabViewModel(this),
            new GamesTabViewModel(this),
            new MusicTabViewModel(this),
            new AppsTabViewModel(this),
            new SettingsTabViewModel(this)
        ];

        Games.CollectionChanged += OnGamesChanged;

        SelectGameCommand = new RelayCommand(parameter => SelectGame(parameter as GameCardViewModel));
        LaunchGameCommand = new AsyncRelayCommand(parameter => LaunchGameAsync(parameter as GameCardViewModel));
        SubmitSearchCommand = new AsyncRelayCommand(SubmitSearchAsync);
        UseTrendingSearchCommand = new RelayCommand(parameter =>
        {
            SearchQuery = parameter?.ToString() ?? string.Empty;
            _ = SubmitSearchAsync();
        });
        OpenSearchCommand = new RelayCommand(OpenSearch);
        CloseSearchCommand = new RelayCommand(() => IsSearchOverlayOpen = false);
        ShowDetailsCommand = new RelayCommand(() => IsDetailsOpen = SelectedGame is not null);
        CloseDetailsCommand = new RelayCommand(() => IsDetailsOpen = false);
        BackCommand = new RelayCommand(GoBack);
        AddGameCommand = new AsyncRelayCommand(AddGameAsync);
        EditSelectedGameCommand = new AsyncRelayCommand(EditSelectedGameAsync);
        ScanFolderCommand = new AsyncRelayCommand(ScanFolderAsync);
        ToggleFavoriteCommand = new AsyncRelayCommand(ToggleFavoriteAsync, _ => SelectedGame is not null);
        SaveSettingsCommand = new AsyncRelayCommand(SaveSettingsAsync);
        ToggleQuickMenuCommand = new RelayCommand(() => IsQuickMenuOpen = !IsQuickMenuOpen);
        OpenMyGamesCommand = new RelayCommand(OpenMyGames);
        OpenMyAppsCommand = new RelayCommand(OpenMyApps);
        OpenMyPinsCommand = new RelayCommand(OpenMyPins);
        CloseMyGamesCommand = new RelayCommand(() => IsMyGamesOpen = false);
        OpenLauncherSettingsCommand = new RelayCommand(OpenLauncherSettings);
        CloseLauncherSettingsCommand = new RelayCommand(() => IsLauncherSettingsOpen = false);
        ChooseSelectedHomeImageCommand = new AsyncRelayCommand(ChooseSelectedHomeImageAsync);
        ChooseSelectedGameMenuImageCommand = new AsyncRelayCommand(ChooseSelectedGameMenuImageAsync);
        SaveSelectedGameCommand = new AsyncRelayCommand(SaveSelectedGameAsync);
        SetOpenTrayGameCommand = new AsyncRelayCommand(SetOpenTrayGameAsync);
        RemoveSelectedGameCommand = new AsyncRelayCommand(RemoveSelectedGameAsync);
        OpenProfileEditorCommand = new RelayCommand(OpenProfileEditor);
        CloseProfileEditorCommand = new RelayCommand(() => IsProfileEditorOpen = false);
        OpenMusicPlayerCommand = new RelayCommand(parameter => OpenMusicPlayer(parameter is bool transparent && transparent));
        CloseMusicPlayerCommand = new RelayCommand(CloseMusicPlayer);
        OpenMusicVisualizerFullscreenCommand = new RelayCommand(OpenMusicVisualizerFullscreen);
        PlayPauseMusicCommand = new RelayCommand(ToggleMusicPlayback);
        StopMusicCommand = new RelayCommand(StopMusic);
        NextMusicCommand = new RelayCommand(NextMusicTrack);
        PreviousMusicCommand = new RelayCommand(PreviousMusicTrack);
        ToggleShuffleMusicCommand = new RelayCommand(() => IsShuffleEnabled = !IsShuffleEnabled);
        VolumeDownCommand = new RelayCommand(() => MusicVolume -= 0.05);
        VolumeUpCommand = new RelayCommand(() => MusicVolume += 0.05);
        PlaySelectedMusicCommand = new RelayCommand(parameter => PlayMusicTrack(parameter as MusicTrackViewModel));
        ChooseProfilePictureCommand = new AsyncRelayCommand(ChooseProfilePictureAsync);
        SaveProfileCommand = new AsyncRelayCommand(SaveProfileAsync);
        ShutdownCommand = new AsyncRelayCommand(ShutdownAsync);
        OpenYouTubeCommand = new RelayCommand(OpenYouTube);
        OpenFriendsOverlayCommand = new RelayCommand(RequestFriendsOverlay);
        SwitchTabCommand = new RelayCommand(parameter =>
        {
            if (parameter is DashboardTabViewModel tab)
            {
                CurrentTab = tab;
            }
        });

        CurrentTab = Tabs[1];
        UpdateClock();
    }

    public ObservableCollection<DashboardTabViewModel> Tabs { get; }
    public ObservableCollection<GameCardViewModel> Games { get; } = [];
    public ObservableCollection<MusicTrackViewModel> MusicTracks { get; } = [];

    public IEnumerable<GameCardViewModel> RecentGames => Games
        .OrderByDescending(game => game.Game.LastPlayed ?? DateTimeOffset.MinValue)
        .Take(8);

    public IEnumerable<GameCardViewModel> PinnedGames => Games
        .Where(game => game.Game.IsFavorite)
        .Take(8);

    public IEnumerable<GameCardViewModel> ImportedGames => Games
        .Where(game => string.Equals(game.Game.Genre, "Imported", StringComparison.OrdinalIgnoreCase));

    public IEnumerable<string> LibraryPaths => _library.LibraryPaths;
    public IEnumerable<int> BlankGameSlots => Enumerable.Range(1, 17);
    public IReadOnlyList<string> ResolutionOptions { get; } = ["720p", "1080p", "1440p", "4K"];
    public IReadOnlyList<string> GameCoverFitOptions { get; } = ["Auto", "Cover", "Fill", "Fit"];
    public IReadOnlyList<string> AddDestinationOptions { get; } = ["My Games", "My Apps"];
    public IReadOnlyList<string> SocialIntegrationOptions { get; } = ["Local"];

    public DashboardTabViewModel? CurrentTab
    {
        get => _currentTab;
        set
        {
            if (!SetProperty(ref _currentTab, value) || value is null)
            {
                return;
            }

            foreach (var tab in Tabs)
            {
                tab.IsSelected = ReferenceEquals(tab, value);
            }

            _audioService.Play(_pendingTabSound ?? "tab");
            _pendingTabSound = null;
            OnPropertyChanged(nameof(CurrentTabName));
            OnPropertyChanged(nameof(PreviousTab));
            OnPropertyChanged(nameof(NextTab));
            OnPropertyChanged(nameof(LeftPreviewContentLeft));
            OnPropertyChanged(nameof(RightPreviewContentLeft));
            OnPropertyChanged(nameof(CurrentReferenceImagePath));
            OnPropertyChanged(nameof(CurrentReferenceImageOpacity));
            OnPropertyChanged(nameof(UseLightDashboardChrome));
        }
    }

    public string CurrentTabName => CurrentTab?.Name ?? string.Empty;

    public double LeftPreviewContentLeft => CurrentTab?.Key == "settings" ? -938 : -910;

    public double RightPreviewContentLeft => CurrentTab?.Key is "bing" or "home" ? -198 : -240;

    public DashboardTabViewModel? PreviousTab
    {
        get
        {
            if (CurrentTab is null)
            {
                return null;
            }

            var index = Tabs.IndexOf(CurrentTab);
            return index > 0 ? Tabs[index - 1] : null;
        }
    }

    public DashboardTabViewModel? NextTab
    {
        get
        {
            if (CurrentTab is null)
            {
                return null;
            }

            var index = Tabs.IndexOf(CurrentTab);
            return index >= 0 && index < Tabs.Count - 1 ? Tabs[index + 1] : null;
        }
    }

    public string CurrentReferenceImagePath => string.Empty;

    public double CurrentReferenceImageOpacity => 0;

    public bool UseLightDashboardChrome => false;

    public GameCardViewModel? SelectedGame
    {
        get => _selectedGame;
        set
        {
            if (SetProperty(ref _selectedGame, value))
            {
                if (value is not null)
                {
                    FeaturedGame = value;
                    StatusMessage = value.Title;
                }

                OnPropertyChanged(nameof(SpotlightTitle));
                OnPropertyChanged(nameof(SpotlightSubtitle));
                OnPropertyChanged(nameof(MyGamesCountText));
                OnPropertyChanged(nameof(LibraryMenuCountText));
                OnPropertyChanged(nameof(SelectedCoverZoom));
                OnPropertyChanged(nameof(SelectedCoverOffsetX));
                OnPropertyChanged(nameof(SelectedCoverOffsetY));
            }
        }
    }

    public GameCardViewModel? FeaturedGame
    {
        get => _featuredGame;
        set
        {
            if (SetProperty(ref _featuredGame, value))
            {
                OnPropertyChanged(nameof(SpotlightTitle));
                OnPropertyChanged(nameof(SpotlightSubtitle));
            }
        }
    }

    public Profile Profile
    {
        get => _profile;
        set => SetProperty(ref _profile, value);
    }

    public AppSettings Settings
    {
        get => _settings;
        set
        {
            value.GameCoverFitMode = NormalizeGameCoverFitMode(value.GameCoverFitMode);
            value.DefaultAddDestination = NormalizeAddDestination(value.DefaultAddDestination);
            value.SocialIntegrationMode = NormalizeSocialIntegrationMode(value.SocialIntegrationMode);
            if (SetProperty(ref _settings, value))
            {
                OnPropertyChanged(nameof(OpenTrayTitle));
                OnPropertyChanged(nameof(GameCoverFitMode));
                OnPropertyChanged(nameof(DefaultAddDestination));
                OnPropertyChanged(nameof(SocialIntegrationModeDisplay));
            }
        }
    }

    public string GameCoverFitMode
    {
        get => Settings.GameCoverFitMode;
        set
        {
            value = NormalizeGameCoverFitMode(value);
            if (string.Equals(Settings.GameCoverFitMode, value, StringComparison.Ordinal))
            {
                return;
            }

            Settings.GameCoverFitMode = value;
            OnPropertyChanged();
        }
    }

    public string DefaultAddDestination
    {
        get => Settings.DefaultAddDestination;
        set
        {
            value = NormalizeAddDestination(value);
            if (string.Equals(Settings.DefaultAddDestination, value, StringComparison.Ordinal))
            {
                return;
            }

            Settings.DefaultAddDestination = value;
            OnPropertyChanged();
        }
    }

    public string SocialIntegrationModeDisplay
    {
        get => ToSocialIntegrationDisplay(Settings.SocialIntegrationMode);
        set
        {
            var normalized = ParseSocialIntegrationMode(value);
            if (Settings.SocialIntegrationMode == normalized)
            {
                return;
            }

            Settings.SocialIntegrationMode = normalized;
            OnPropertyChanged();
        }
    }

    public double SelectedCoverZoom
    {
        get => SelectedGame?.Game.CoverZoom > 0 ? SelectedGame.Game.CoverZoom : 1;
        set
        {
            if (SelectedGame is null)
            {
                return;
            }

            var zoom = Math.Clamp(value, 1, 1.8);
            if (Math.Abs(SelectedGame.Game.CoverZoom - zoom) < 0.001)
            {
                return;
            }

            SelectedGame.Game.CoverZoom = zoom;
            SelectedGame.Refresh();
            OnPropertyChanged();
        }
    }

    public double SelectedCoverOffsetX
    {
        get => SelectedGame?.Game.CoverOffsetX ?? 0;
        set
        {
            if (SelectedGame is null)
            {
                return;
            }

            var offset = Math.Clamp(value, -1, 1);
            if (Math.Abs(SelectedGame.Game.CoverOffsetX - offset) < 0.001)
            {
                return;
            }

            SelectedGame.Game.CoverOffsetX = offset;
            SelectedGame.Refresh();
            OnPropertyChanged();
        }
    }

    public double SelectedCoverOffsetY
    {
        get => SelectedGame?.Game.CoverOffsetY ?? 0;
        set
        {
            if (SelectedGame is null)
            {
                return;
            }

            var offset = Math.Clamp(value, -1, 1);
            if (Math.Abs(SelectedGame.Game.CoverOffsetY - offset) < 0.001)
            {
                return;
            }

            SelectedGame.Game.CoverOffsetY = offset;
            SelectedGame.Refresh();
            OnPropertyChanged();
        }
    }

    public string SearchQuery
    {
        get => _searchQuery;
        set => SetProperty(ref _searchQuery, value);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
    }

    public bool IsSearchOverlayOpen
    {
        get => _isSearchOverlayOpen;
        set => SetProperty(ref _isSearchOverlayOpen, value);
    }

    public bool IsDetailsOpen
    {
        get => _isDetailsOpen;
        set => SetProperty(ref _isDetailsOpen, value);
    }

    public bool IsQuickMenuOpen
    {
        get => _isQuickMenuOpen;
        set => SetProperty(ref _isQuickMenuOpen, value);
    }

    public bool IsMyGamesOpen
    {
        get => _isMyGamesOpen;
        set => SetProperty(ref _isMyGamesOpen, value);
    }

    public bool IsLauncherSettingsOpen
    {
        get => _isLauncherSettingsOpen;
        set => SetProperty(ref _isLauncherSettingsOpen, value);
    }

    public bool IsProfileEditorOpen
    {
        get => _isProfileEditorOpen;
        set => SetProperty(ref _isProfileEditorOpen, value);
    }

    public bool IsMusicPlayerOpen
    {
        get => _isMusicPlayerOpen;
        set
        {
            if (SetProperty(ref _isMusicPlayerOpen, value))
            {
                if (value)
                {
                    _audioAnalysisService.Start();
                }
                else
                {
                    _audioAnalysisService.Stop();
                }
            }
        }
    }

    public bool IsMusicPlayerTransparent
    {
        get => _isMusicPlayerTransparent;
        private set => SetProperty(ref _isMusicPlayerTransparent, value);
    }

    public bool IsMusicVisualizerFullscreen
    {
        get => _isMusicVisualizerFullscreen;
        private set => SetProperty(ref _isMusicVisualizerFullscreen, value);
    }

    public bool IsMusicPlaying
    {
        get => _isMusicPlaying;
        set
        {
            if (SetProperty(ref _isMusicPlaying, value))
            {
                OnPropertyChanged(nameof(MusicPlayPauseText));
            }
        }
    }

    public bool IsShuffleEnabled
    {
        get => _isShuffleEnabled;
        set
        {
            if (SetProperty(ref _isShuffleEnabled, value))
            {
                OnPropertyChanged(nameof(ShuffleText));
            }
        }
    }

    public bool IsBooting
    {
        get => _isBooting;
        set => SetProperty(ref _isBooting, value);
    }

    public string ClockText
    {
        get => _clockText;
        set => SetProperty(ref _clockText, value);
    }

    public MusicTrackViewModel? CurrentMusicTrack
    {
        get => _currentMusicTrack;
        set
        {
            if (SetProperty(ref _currentMusicTrack, value))
            {
                foreach (var track in MusicTracks)
                {
                    track.IsPlaying = ReferenceEquals(track, value);
                }

                OnPropertyChanged(nameof(CurrentMusicTitle));
                OnPropertyChanged(nameof(MusicTrackCountText));
            }
        }
    }

    public string CurrentMusicTitle => CurrentMusicTrack?.Title ?? "No music found";
    public string MusicTrackCountText => MusicTracks.Count == 0 ? "0 of 0" : $"{Math.Max(1, _musicIndex + 1)} of {MusicTracks.Count}";
    public string MusicPlayPauseText => IsMusicPlaying ? "Pause" : "Play";
    public string ShuffleText => IsShuffleEnabled ? "Shuffle On" : "Shuffle";

    public string MusicPositionText
    {
        get => _musicPositionText;
        set => SetProperty(ref _musicPositionText, value);
    }

    public string MusicDurationText
    {
        get => _musicDurationText;
        set => SetProperty(ref _musicDurationText, value);
    }

    public double MusicProgress
    {
        get => _musicProgress;
        set => SetProperty(ref _musicProgress, value);
    }

    public double MusicVolume
    {
        get => _musicVolume;
        set
        {
            var volume = Math.Clamp(value, 0, 1);
            if (SetProperty(ref _musicVolume, volume))
            {
                _musicPlayer.Volume = volume;
                OnPropertyChanged(nameof(MusicVolumeText));
            }
        }
    }

    public string MusicVolumeText => $"{Math.Round(MusicVolume * 100)}%";

    public double VisualizerBass
    {
        get => _visualizerBass;
        private set => SetProperty(ref _visualizerBass, value);
    }

    public double VisualizerMid
    {
        get => _visualizerMid;
        private set => SetProperty(ref _visualizerMid, value);
    }

    public double VisualizerTreble
    {
        get => _visualizerTreble;
        private set => SetProperty(ref _visualizerTreble, value);
    }

    public double VisualizerLoudness
    {
        get => _visualizerLoudness;
        private set => SetProperty(ref _visualizerLoudness, value);
    }

    public double VisualizerPeak
    {
        get => _visualizerPeak;
        private set => SetProperty(ref _visualizerPeak, value);
    }

    public string SpotlightTitle => FeaturedGame?.Title ?? "Xbox Metro Launcher";
    public string SpotlightSubtitle => FeaturedGame?.Subtitle ?? "Press Y to search or E to move across the dashboard.";
    public GameCardViewModel? TrayGame
    {
        get => _trayGame;
        set
        {
            if (SetProperty(ref _trayGame, value))
            {
                OnPropertyChanged(nameof(OpenTrayTitle));
                OnPropertyChanged(nameof(OpenTrayCoverArtPath));
            }
        }
    }

    public string OpenTrayTitle => TrayGame?.Title ?? "Open Tray";
    public string OpenTrayCoverArtPath => TrayGame?.BackgroundArtPath ?? string.Empty;
    public string MyGamesCountText
    {
        get
        {
            var games = Games.Where(game => !IsAppEntry(game.Game)).ToList();
            var count = games.Count;
            if (count == 0)
            {
                return "0 of 17";
            }

            var selected = SelectedGame is null ? 1 : Math.Max(1, games.IndexOf(SelectedGame) + 1);
            return $"{selected} of {count}";
        }
    }

    public string LibraryMenuTitle => _isLibraryShowingPins ? "My Pins" : _isLibraryShowingApps ? "My Apps" : "My Games";

    public string LibraryMenuFilterText => _isLibraryShowingPins ? "pinned games" : _isLibraryShowingApps ? "all apps" : "all games";

    public string LibraryMenuXHintText => " Pin";

    public IEnumerable<GameCardViewModel> LibraryMenuGames
        => _isLibraryShowingPins
            ? Games.Where(game => game.Game.IsFavorite)
            : _isLibraryShowingApps
                ? Games.Where(game => IsAppEntry(game.Game))
                : Games.Where(game => !IsAppEntry(game.Game));

    public string LibraryMenuCountText
    {
        get
        {
            var visibleGames = LibraryMenuGames.ToList();
            if (visibleGames.Count == 0)
            {
                return _isLibraryShowingPins || _isLibraryShowingApps ? "0 of 0" : "0 of 17";
            }

            var selected = SelectedGame is null ? 1 : visibleGames.IndexOf(SelectedGame) + 1;
            if (selected <= 0)
            {
                selected = 1;
            }

            return $"{selected} of {visibleGames.Count}";
        }
    }

    public ICommand SelectGameCommand { get; }
    public ICommand LaunchGameCommand { get; }
    public ICommand SubmitSearchCommand { get; }
    public ICommand UseTrendingSearchCommand { get; }
    public ICommand OpenSearchCommand { get; }
    public ICommand CloseSearchCommand { get; }
    public ICommand ShowDetailsCommand { get; }
    public ICommand CloseDetailsCommand { get; }
    public ICommand BackCommand { get; }
    public ICommand AddGameCommand { get; }
    public ICommand EditSelectedGameCommand { get; }
    public ICommand ScanFolderCommand { get; }
    public ICommand ToggleFavoriteCommand { get; }
    public ICommand SaveSettingsCommand { get; }
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
    public ICommand CloseMusicPlayerCommand { get; }
    public ICommand OpenMusicVisualizerFullscreenCommand { get; }
    public ICommand PlayPauseMusicCommand { get; }
    public ICommand StopMusicCommand { get; }
    public ICommand NextMusicCommand { get; }
    public ICommand PreviousMusicCommand { get; }
    public ICommand ToggleShuffleMusicCommand { get; }
    public ICommand VolumeDownCommand { get; }
    public ICommand VolumeUpCommand { get; }
    public ICommand PlaySelectedMusicCommand { get; }
    public ICommand ChooseProfilePictureCommand { get; }
    public ICommand SaveProfileCommand { get; }
    public ICommand ShutdownCommand { get; }
    public ICommand OpenYouTubeCommand { get; }
    public ICommand OpenFriendsOverlayCommand { get; }
    public ICommand SwitchTabCommand { get; }

    public async Task InitializeAsync()
    {
        Settings = await _settingsService.LoadAsync();
        Profile = await _profileService.LoadAsync();
        EnsureProfileDefaults();
        Settings.SocialIntegrationMode = SocialIntegrationMode.LocalOnly;
        Settings.DiscordUserId = string.Empty;
        Settings.DiscordDisplayName = string.Empty;
        Settings.DiscordAvatarPathOrUrl = string.Empty;
        Settings.DiscordAccessTokenEncrypted = string.Empty;
        Settings.DiscordGrantedScopes = string.Empty;
        Settings.DiscordTokenType = string.Empty;
        _library = await _libraryService.LoadAsync();

        _library.Games = _library.Games
            .OrderBy(game => game.Title, StringComparer.CurrentCultureIgnoreCase)
            .ToList();

        Games.Clear();
        var index = 0;
        foreach (var game in _library.Games)
        {
            Games.Add(new GameCardViewModel(game, _accentBrushes[index++ % _accentBrushes.Count]));
        }

        SelectedGame = Games.FirstOrDefault(game => game.Game.IsFavorite) ?? Games.FirstOrDefault();
        FeaturedGame = SelectedGame;
        TrayGame = Games.FirstOrDefault(game => string.Equals(game.Game.Id, Settings.OpenTrayGameId, StringComparison.OrdinalIgnoreCase));

        await _settingsService.SaveAsync(Settings);

    }

    public void UpdateClock()
        => ClockText = DateTime.Now.ToString("h:mm tt  ddd, MMM d");

    public void HandleInput(DashboardInputAction action)
    {
        switch (action)
        {
            case DashboardInputAction.PreviousTab:
                MoveTab(-1);
                break;
            case DashboardInputAction.NextTab:
                MoveTab(1);
                break;
            case DashboardInputAction.Back:
                GoBack();
                break;
            case DashboardInputAction.Details:
                if (IsMusicPlayerOpen)
                {
                    OpenMusicVisualizerFullscreen();
                }
                else if (IsMyGamesOpen)
                {
                    _ = ToggleFavoriteAsync(null);
                }
                else
                {
                    IsDetailsOpen = SelectedGame is not null;
                    _audioService.Play("select");
                }
                break;
            case DashboardInputAction.Search:
                OpenSearch();
                break;
            case DashboardInputAction.Options:
                IsQuickMenuOpen = !IsQuickMenuOpen;
                break;
            case DashboardInputAction.Activate:
                break;
            default:
                _audioService.Play("focus");
                break;
        }
    }

    public void MoveTab(int delta)
    {
        if (CurrentTab is null)
        {
            CurrentTab = Tabs[1];
            return;
        }

        var index = Tabs.IndexOf(CurrentTab);
        var next = Math.Clamp(index + delta, 0, Tabs.Count - 1);
        if (next == index)
        {
            return;
        }

        _pendingTabSound = delta < 0 ? "page-left" : "page-right";
        CurrentTab = Tabs[next];
    }

    public void SelectGame(GameCardViewModel? game)
    {
        if (game is null)
        {
            return;
        }

        SelectedGame = game;
        _audioService.Play("focus");
    }

    private void OpenMyGames()
        => OpenLibraryMenu(showPins: false, showApps: false);

    private void OpenMyApps()
        => OpenLibraryMenu(showPins: false, showApps: true);

    private void OpenMyPins()
        => OpenLibraryMenu(showPins: true, showApps: false);

    private void OpenLibraryMenu(bool showPins, bool showApps)
    {
        _isLibraryShowingPins = showPins;
        _isLibraryShowingApps = showApps;

        var visibleGames = LibraryMenuGames.ToList();
        if (visibleGames.Count > 0 && (SelectedGame is null || !visibleGames.Contains(SelectedGame)))
        {
            SelectedGame = visibleGames.FirstOrDefault();
        }

        IsMyGamesOpen = true;
        IsLauncherSettingsOpen = false;
        IsProfileEditorOpen = false;
        IsMusicPlayerOpen = false;
        IsQuickMenuOpen = false;
        IsDetailsOpen = false;
        OnPropertyChanged(nameof(LibraryMenuTitle));
        OnPropertyChanged(nameof(LibraryMenuFilterText));
        OnPropertyChanged(nameof(LibraryMenuGames));
        OnPropertyChanged(nameof(LibraryMenuCountText));
        OnPropertyChanged(nameof(LibraryMenuXHintText));
        _audioService.Play("select");
    }

    private void OpenLauncherSettings()
    {
        IsLauncherSettingsOpen = true;
        IsMyGamesOpen = false;
        IsProfileEditorOpen = false;
        IsMusicPlayerOpen = false;
        IsQuickMenuOpen = false;
        IsDetailsOpen = false;
        _audioService.Play("select");
    }

    private void OpenProfileEditor()
    {
        IsProfileEditorOpen = true;
        IsMyGamesOpen = false;
        IsLauncherSettingsOpen = false;
        IsMusicPlayerOpen = false;
        IsQuickMenuOpen = false;
        IsDetailsOpen = false;
        _audioService.Play("select");
    }

    private void OpenMusicPlayer(bool transparent = false)
    {
        IsMusicPlayerTransparent = transparent;
        IsMusicVisualizerFullscreen = false;
        LoadMusicLibrary();
        _audioAnalysisService.Start();
        IsMusicPlayerOpen = true;
        IsMyGamesOpen = false;
        IsLauncherSettingsOpen = false;
        IsProfileEditorOpen = false;
        IsQuickMenuOpen = false;
        IsDetailsOpen = false;
        _audioService.Play("select");

        if (CurrentMusicTrack is null && MusicTracks.Count > 0)
        {
            PlayMusicTrack(MusicTracks[0]);
        }
    }

    private void CloseMusicPlayer()
    {
        IsMusicVisualizerFullscreen = false;
        IsMusicPlayerOpen = false;
        IsMusicPlayerTransparent = false;
        _audioAnalysisService.Stop();
        _audioService.Play("back");
    }

    private void OpenMusicVisualizerFullscreen()
    {
        if (!IsMusicPlayerOpen || IsMusicVisualizerFullscreen)
        {
            return;
        }

        IsMusicVisualizerFullscreen = true;
        _audioService.Play("select");
    }

    private async Task LaunchGameAsync(GameCardViewModel? card)
    {
        card ??= SelectedGame;
        if (card is null)
        {
            StatusMessage = "No game selected";
            return;
        }

        try
        {
            SelectedGame = card;

            if (Application.Current?.MainWindow is { } window)
            {
                window.WindowState = WindowState.Minimized;
            }

            await _launchService.LaunchAsync(card.Game);
            card.Game.LastPlayed = DateTimeOffset.Now;
            await PersistLibraryAsync();
            StatusMessage = $"Launching {card.Title}";
            _audioService.Play("select");
        }
        catch (Exception ex)
        {
            StatusMessage = ex.Message;
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
        StatusMessage = $"Searching Bing for {SearchQuery}";
        IsSearchOverlayOpen = false;
        _audioService.Play("select");
    }

    private void OpenSearch()
    {
        CurrentTab = Tabs.First(tab => tab.Key == "bing");
        IsSearchOverlayOpen = true;
        _audioService.Play("select");
    }

    private void RequestFriendsOverlay()
    {
        IsQuickMenuOpen = false;
        IsDetailsOpen = false;
        FriendsOverlayRequested?.Invoke(this, EventArgs.Empty);
    }

    private void GoBack()
    {
        if (IsSearchOverlayOpen)
        {
            IsSearchOverlayOpen = false;
            _audioService.Play("back");
            return;
        }

        if (IsDetailsOpen)
        {
            IsDetailsOpen = false;
            _audioService.Play("back");
            return;
        }

        if (IsMyGamesOpen)
        {
            IsMyGamesOpen = false;
            _audioService.Play("back");
            return;
        }

        if (IsLauncherSettingsOpen)
        {
            IsLauncherSettingsOpen = false;
            _audioService.Play("back");
            return;
        }

        if (IsProfileEditorOpen)
        {
            IsProfileEditorOpen = false;
            _audioService.Play("back");
            return;
        }

        if (IsMusicPlayerOpen)
        {
            if (IsMusicVisualizerFullscreen)
            {
                IsMusicVisualizerFullscreen = false;
            }
            else
            {
                IsMusicPlayerOpen = false;
            }

            _audioService.Play("back");
            return;
        }

        if (IsQuickMenuOpen)
        {
            IsQuickMenuOpen = false;
            _audioService.Play("back");
            return;
        }

        CurrentTab = Tabs[1];
        _audioService.Play("back");
    }

    private async Task AddGameAsync()
    {
        var executable = _filePickerService.PickExecutable();
        if (string.IsNullOrWhiteSpace(executable))
        {
            return;
        }

        var destination = NormalizeAddDestination(Settings.DefaultAddDestination);
        var game = new GameMetadata
        {
            Title = Path.GetFileNameWithoutExtension(executable).Replace("_", " "),
            ExecutablePath = executable,
            WorkingDirectory = Path.GetDirectoryName(executable) ?? string.Empty,
            Platform = "PC",
            Genre = destination == "My Apps" ? "App" : "Manual"
        };

        _library.Games.Add(game);
        var card = new GameCardViewModel(game, _accentBrushes[Games.Count % _accentBrushes.Count]);
        Games.Add(card);
        SortGamesByTitle(game.Id);
        await PersistLibraryAsync();
        OnPropertyChanged(nameof(MyGamesCountText));
        StatusMessage = $"Added {game.Title} to {destination}";
    }

    private async Task EditSelectedGameAsync(object? _)
    {
        if (SelectedGame is null)
        {
            return;
        }

        var executable = _filePickerService.PickExecutable();
        if (string.IsNullOrWhiteSpace(executable))
        {
            return;
        }

        SelectedGame.Game.ExecutablePath = executable;
        SelectedGame.Game.WorkingDirectory = Path.GetDirectoryName(executable) ?? string.Empty;
        await PersistLibraryAsync();
        StatusMessage = $"Updated {SelectedGame.Title}";
    }

    private async Task ChooseSelectedHomeImageAsync(object? _)
    {
        if (SelectedGame is null)
        {
            StatusMessage = "Choose a game first";
            return;
        }

        var image = _filePickerService.PickImage(GetCustomCoverFolder("Home Screen Cover"));
        if (string.IsNullOrWhiteSpace(image))
        {
            return;
        }

        SelectedGame.Game.BackgroundArtPath = CopyCustomArtwork(image, "Home Screen Cover", SelectedGame.Title);
        SelectedGame.Refresh();
        if (ReferenceEquals(TrayGame, SelectedGame))
        {
            OnPropertyChanged(nameof(OpenTrayCoverArtPath));
        }

        await PersistLibraryAsync();
        StatusMessage = $"Updated Home image for {SelectedGame.Title}";
    }

    private async Task ChooseSelectedGameMenuImageAsync(object? _)
    {
        if (SelectedGame is null)
        {
            StatusMessage = "Choose a game first";
            return;
        }

        var image = _filePickerService.PickImage(GetCustomCoverFolder("Game Menu Cover"));
        if (string.IsNullOrWhiteSpace(image))
        {
            return;
        }

        SelectedGame.Game.CoverArtPath = CopyCustomArtwork(image, "Game Menu Cover", SelectedGame.Title);
        SelectedGame.Refresh();

        await PersistLibraryAsync();
        StatusMessage = $"Updated My Games image for {SelectedGame.Title}";
    }

    private async Task SaveSelectedGameAsync(object? _)
    {
        if (SelectedGame is null)
        {
            StatusMessage = "Choose a game first";
            return;
        }

        SelectedGame.Refresh();
        if (ReferenceEquals(TrayGame, SelectedGame))
        {
            OnPropertyChanged(nameof(OpenTrayTitle));
        }

        SortGamesByTitle(SelectedGame.Game.Id);
        await PersistLibraryAsync();
        StatusMessage = $"Saved {SelectedGame.Title}";
    }

    private async Task SetOpenTrayGameAsync(object? _)
    {
        if (SelectedGame is null)
        {
            StatusMessage = "Choose a game first";
            return;
        }

        TrayGame = SelectedGame;
        Settings.OpenTrayGameId = SelectedGame.Game.Id;
        await SaveSettingsAsync();
        StatusMessage = $"{SelectedGame.Title} is now on Open Tray";
    }

    private async Task RemoveSelectedGameAsync()
    {
        if (SelectedGame is null)
        {
            StatusMessage = "Choose a game first";
            return;
        }

        var removed = SelectedGame;
        _library.Games.Remove(removed.Game);
        Games.Remove(removed);

        if (string.Equals(Settings.OpenTrayGameId, removed.Game.Id, StringComparison.OrdinalIgnoreCase))
        {
            Settings.OpenTrayGameId = string.Empty;
            TrayGame = null;
            await _settingsService.SaveAsync(Settings);
        }

        SelectedGame = Games.FirstOrDefault();
        FeaturedGame = SelectedGame;
        SortGamesByTitle(SelectedGame?.Game.Id);
        await PersistLibraryAsync();
        StatusMessage = $"Removed {removed.Title} from My Games";
    }

    private async Task ChooseProfilePictureAsync(object? _)
    {
        var image = _filePickerService.PickImage(Path.Combine(AppPaths.AppFolder, "Assets", "Profile"));
        if (string.IsNullOrWhiteSpace(image))
        {
            return;
        }

        Profile = new Profile
        {
            Gamertag = Profile.Gamertag,
            GamerPicturePath = image,
            Gamerscore = Profile.Gamerscore,
            OnlineStatus = Profile.OnlineStatus,
            Motto = Profile.Motto,
            Description = Profile.Description
        };
        await _profileService.SaveAsync(Profile);
        StatusMessage = "Profile picture updated";
    }

    private async Task SaveProfileAsync()
    {
        EnsureProfileDefaults();
        await _profileService.SaveAsync(Profile);
        OnPropertyChanged(nameof(Profile));
        StatusMessage = "Profile saved";
    }

    private async Task ShutdownAsync()
    {
        await _settingsService.SaveAsync(Settings);
        await _profileService.SaveAsync(Profile);
        Application.Current.Shutdown();
    }

    private void OpenYouTube()
    {
        try
        {
            if (Application.Current?.MainWindow is { } window)
            {
                window.WindowState = WindowState.Minimized;
            }

            Process.Start(new ProcessStartInfo
            {
                FileName = "https://www.youtube.com",
                UseShellExecute = true
            });
            StatusMessage = "Opening YouTube";
            _audioService.Play("select");
        }
        catch (Exception ex)
        {
            StatusMessage = ex.Message;
        }
    }

    private static string GetCustomCoverFolder(string folderName)
        => EnsureDirectory(Path.Combine(AppPaths.AppFolder, "Assets", "Custom Files", "CoverArt", folderName));

    private static string GetMusicFolder()
        => EnsureDirectory(AppPaths.FindFolder(
            Path.Combine("Assets", "Custom Files", "Music Files"),
            folder => Directory.EnumerateFiles(folder).Any(IsSupportedMusicFile)));

    private static bool IsSupportedMusicFile(string path)
        => Path.GetExtension(path) is { } extension
           && (extension.Equals(".mp3", StringComparison.OrdinalIgnoreCase)
               || extension.Equals(".wav", StringComparison.OrdinalIgnoreCase)
               || extension.Equals(".wma", StringComparison.OrdinalIgnoreCase)
               || extension.Equals(".m4a", StringComparison.OrdinalIgnoreCase)
               || extension.Equals(".aac", StringComparison.OrdinalIgnoreCase));

    private void AudioAnalysis_OnFrameReady(object? sender, AudioAnalysisFrame frame)
    {
        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher is null || dispatcher.CheckAccess())
        {
            ApplyAudioAnalysis(frame);
            return;
        }

        dispatcher.BeginInvoke(new Action(() => ApplyAudioAnalysis(frame)), DispatcherPriority.Render);
    }

    private void ApplyAudioAnalysis(AudioAnalysisFrame frame)
    {
        VisualizerBass = frame.Bass;
        VisualizerMid = frame.Mid;
        VisualizerTreble = frame.Treble;
        VisualizerLoudness = frame.Loudness;
        VisualizerPeak = frame.Peak;
    }

    private static string EnsureDirectory(string path)
    {
        Directory.CreateDirectory(path);
        return path;
    }

    private static string CopyCustomArtwork(string sourcePath, string folderName, string title)
    {
        var folder = GetCustomCoverFolder(folderName);
        var fullSource = Path.GetFullPath(sourcePath);
        if (string.Equals(Path.GetDirectoryName(fullSource), folder, StringComparison.OrdinalIgnoreCase))
        {
            return fullSource;
        }

        var extension = Path.GetExtension(fullSource);
        var fileName = MakeSafeFileName(title);
        var destination = Path.Combine(folder, $"{fileName}{extension}");
        var count = 2;
        while (File.Exists(destination))
        {
            destination = Path.Combine(folder, $"{fileName} {count++}{extension}");
        }

        File.Copy(fullSource, destination);
        return destination;
    }

    private static string MakeSafeFileName(string value)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var safe = new string(value.Select(ch => invalid.Contains(ch) ? '_' : ch).ToArray()).Trim();
        return string.IsNullOrWhiteSpace(safe) ? "cover" : safe;
    }

    private void LoadMusicLibrary()
    {
        var folder = GetMusicFolder();
        var extensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".mp3",
            ".wav",
            ".wma",
            ".m4a",
            ".aac"
        };

        var selectedPath = CurrentMusicTrack?.Path;
        var files = Directory.EnumerateFiles(folder)
            .Where(path => extensions.Contains(Path.GetExtension(path)))
            .OrderBy(Path.GetFileNameWithoutExtension, StringComparer.CurrentCultureIgnoreCase)
            .ToList();

        MusicTracks.Clear();
        foreach (var file in files)
        {
            MusicTracks.Add(new MusicTrackViewModel(file));
        }

        _musicIndex = selectedPath is null
            ? -1
            : MusicTracks.ToList().FindIndex(track => string.Equals(track.Path, selectedPath, StringComparison.OrdinalIgnoreCase));
        CurrentMusicTrack = _musicIndex >= 0 ? MusicTracks[_musicIndex] : null;
        OnPropertyChanged(nameof(MusicTrackCountText));
    }

    private void PlayMusicTrack(MusicTrackViewModel? track)
    {
        if (track is null)
        {
            if (CurrentMusicTrack is not null)
            {
                _musicPlayer.Play();
                _musicTimer.Start();
                IsMusicPlaying = true;
            }

            return;
        }

        var index = MusicTracks.IndexOf(track);
        if (index < 0 || !File.Exists(track.Path))
        {
            return;
        }

        _musicIndex = index;
        CurrentMusicTrack = track;
        _musicPlayer.Open(new Uri(track.Path, UriKind.Absolute));
        _musicPlayer.Volume = MusicVolume;
        _musicPlayer.Play();
        _musicTimer.Start();
        IsMusicPlaying = true;
        StatusMessage = $"Playing {track.Title}";
        RefreshMusicProgress();
    }

    private void ToggleMusicPlayback()
    {
        if (CurrentMusicTrack is null)
        {
            if (MusicTracks.Count == 0)
            {
                LoadMusicLibrary();
            }

            PlayMusicTrack(MusicTracks.FirstOrDefault());
            return;
        }

        if (IsMusicPlaying)
        {
            _musicPlayer.Pause();
            _musicTimer.Stop();
            IsMusicPlaying = false;
            return;
        }

        _musicPlayer.Play();
        _musicTimer.Start();
        IsMusicPlaying = true;
    }

    private void StopMusic()
    {
        _musicPlayer.Stop();
        _musicTimer.Stop();
        IsMusicPlaying = false;
        MusicProgress = 0;
        MusicPositionText = "0:00";
    }

    private void NextMusicTrack()
    {
        if (MusicTracks.Count == 0)
        {
            LoadMusicLibrary();
        }

        if (MusicTracks.Count == 0)
        {
            return;
        }

        var next = IsShuffleEnabled
            ? _random.Next(MusicTracks.Count)
            : (_musicIndex + 1 + MusicTracks.Count) % MusicTracks.Count;
        PlayMusicTrack(MusicTracks[next]);
    }

    private void PreviousMusicTrack()
    {
        if (MusicTracks.Count == 0)
        {
            LoadMusicLibrary();
        }

        if (MusicTracks.Count == 0)
        {
            return;
        }

        var previous = (_musicIndex - 1 + MusicTracks.Count) % MusicTracks.Count;
        PlayMusicTrack(MusicTracks[previous]);
    }

    private void RefreshMusicProgress()
    {
        var position = _musicPlayer.Position;
        MusicPositionText = FormatTime(position);

        if (_musicPlayer.NaturalDuration.HasTimeSpan)
        {
            var duration = _musicPlayer.NaturalDuration.TimeSpan;
            MusicDurationText = FormatTime(duration);
            MusicProgress = duration.TotalSeconds <= 0 ? 0 : Math.Clamp(position.TotalSeconds / duration.TotalSeconds * 100, 0, 100);
        }
        else
        {
            MusicDurationText = "0:00";
            MusicProgress = 0;
        }
    }

    private static string FormatTime(TimeSpan value)
        => value.TotalHours >= 1
            ? value.ToString(@"h\:mm\:ss")
            : value.ToString(@"m\:ss");

    private async Task ScanFolderAsync()
    {
        var folder = _filePickerService.PickFolder();
        if (string.IsNullOrWhiteSpace(folder))
        {
            return;
        }

        if (!_library.LibraryPaths.Contains(folder, StringComparer.OrdinalIgnoreCase))
        {
            _library.LibraryPaths.Add(folder);
        }

        var scanned = await _libraryService.ScanFolderAsync(folder);
        var knownPaths = _library.Games
            .Select(game => game.ExecutablePath)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var added = 0;
        var destination = NormalizeAddDestination(Settings.DefaultAddDestination);
        foreach (var game in scanned.Where(game => knownPaths.Add(game.ExecutablePath)))
        {
            if (destination == "My Apps")
            {
                game.Genre = "App";
            }

            _library.Games.Add(game);
            Games.Add(new GameCardViewModel(game, _accentBrushes[Games.Count % _accentBrushes.Count]));
            added++;
        }

        SortGamesByTitle(SelectedGame?.Game.Id);
        await PersistLibraryAsync();
        OnPropertyChanged(nameof(MyGamesCountText));
        StatusMessage = added == 1 ? $"Imported 1 item to {destination}" : $"Imported {added} items to {destination}";
    }

    private async Task ToggleFavoriteAsync(object? _)
    {
        if (SelectedGame is null)
        {
            return;
        }

        var toggledGame = SelectedGame;
        toggledGame.Game.IsFavorite = !toggledGame.Game.IsFavorite;
        await PersistLibraryAsync();
        RefreshDerivedLists();

        if (toggledGame.Game.IsFavorite)
        {
            _audioService.Play("select");
        }

        if (_isLibraryShowingPins && toggledGame.Game.IsFavorite == false)
        {
            SelectedGame = LibraryMenuGames.FirstOrDefault();
        }

        StatusMessage = toggledGame.Game.IsFavorite ? "Pinned to Home" : "Removed from pins";
    }

    private async Task SaveSettingsAsync()
    {
        await _settingsService.SaveAsync(Settings);
        await PersistLibraryAsync();
        _startupRegistrationService.SetLaunchOnStartup(Settings.LaunchOnWindowsStartup);
        StatusMessage = "Settings saved";
    }

    private void EnsureProfileDefaults()
    {
        var defaultPicturePath = Path.Combine(AppPaths.AppFolder, "Assets", "Profile", "profilepicture.jpg");

        if (string.IsNullOrWhiteSpace(Profile.Gamertag))
        {
            Profile.Gamertag = "MetroPilot";
        }

        if (string.IsNullOrWhiteSpace(Profile.GamerPicturePath) || IsOldDefaultProfilePicture(Profile.GamerPicturePath))
        {
            Profile.GamerPicturePath = defaultPicturePath;
        }

        if (string.IsNullOrWhiteSpace(Profile.OnlineStatus))
        {
            Profile.OnlineStatus = "Online";
        }

        if (string.IsNullOrWhiteSpace(Profile.Motto))
        {
            Profile.Motto = "(No motto)";
        }

        if (string.IsNullOrWhiteSpace(Profile.Description))
        {
            Profile.Description = "(No bio)";
        }
    }

    private static bool IsOldDefaultProfilePicture(string path)
        => path.EndsWith(Path.Combine("Assets", "Art", "profilepicture.jpg"), StringComparison.OrdinalIgnoreCase)
           && !File.Exists(path);

    private static string NormalizeGameCoverFitMode(string? mode)
        => mode is "Cover" or "Fill" or "Fit" ? mode : "Auto";

    private static string NormalizeAddDestination(string? destination)
        => string.Equals(destination, "My Apps", StringComparison.OrdinalIgnoreCase) ? "My Apps" : "My Games";

    private static SocialIntegrationMode NormalizeSocialIntegrationMode(SocialIntegrationMode mode)
        => SocialIntegrationMode.LocalOnly;

    private static string ToSocialIntegrationDisplay(SocialIntegrationMode mode)
        => "Local";

    private static SocialIntegrationMode ParseSocialIntegrationMode(string? mode)
        => SocialIntegrationMode.LocalOnly;

    private static bool IsAppEntry(GameMetadata game)
        => string.Equals(game.Genre, "App", StringComparison.OrdinalIgnoreCase);

    private void SortGamesByTitle(string? selectedGameId = null)
    {
        selectedGameId ??= SelectedGame?.Game.Id;

        _library.Games = _library.Games
            .OrderBy(game => game.Title, StringComparer.CurrentCultureIgnoreCase)
            .ToList();

        var sortedCards = Games
            .OrderBy(game => game.Title, StringComparer.CurrentCultureIgnoreCase)
            .ToList();

        Games.Clear();
        foreach (var game in sortedCards)
        {
            Games.Add(game);
        }

        SelectedGame = Games.FirstOrDefault(game => string.Equals(game.Game.Id, selectedGameId, StringComparison.OrdinalIgnoreCase))
            ?? Games.FirstOrDefault();
        FeaturedGame = SelectedGame;
    }

    private async Task PersistLibraryAsync()
    {
        await _libraryService.SaveAsync(_library);
        RefreshDerivedLists();
    }

    private void RefreshDerivedLists()
    {
        OnPropertyChanged(nameof(RecentGames));
        OnPropertyChanged(nameof(PinnedGames));
        OnPropertyChanged(nameof(ImportedGames));
        OnPropertyChanged(nameof(LibraryPaths));
        OnPropertyChanged(nameof(MyGamesCountText));
        OnPropertyChanged(nameof(LibraryMenuGames));
        OnPropertyChanged(nameof(LibraryMenuCountText));
    }

    private void OnGamesChanged(object? sender, NotifyCollectionChangedEventArgs e)
        => RefreshDerivedLists();
}
