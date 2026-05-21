using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using XboxMetroLauncher.Models;
using XboxMetroLauncher.Services;

namespace XboxMetroLauncher.ViewModels;

public sealed class GuideViewModel : ObservableObject, IDisposable
{
    private enum MediaFocusArea
    {
        List,
        SongRow,
        Transport,
        Submenu
    }

    private enum GuideScreen
    {
        MainMenu,
        FriendsList,
        Party,
        FriendSearch,
        FriendProfile
    }

    private enum SearchKeyboardLayout
    {
        Default,
        Symbols,
        Accents
    }

    private readonly DashboardViewModel _dashboard;
    private readonly Window _mainWindow;
    private readonly IAudioService _audioService;
    private readonly IFriendsService _friendsService;
    private readonly SocialIntegrationManager _socialIntegrationManager;
    private readonly DiscordPartyService _discordPartyService;
    private readonly DispatcherTimer _clockTimer;
    private readonly DispatcherTimer _partyRefreshTimer;
    private readonly SemaphoreSlim _friendsSaveLock = new(1, 1);
    private readonly SemaphoreSlim _partyRefreshLock = new(1, 1);
    private readonly List<FriendProfile> _friends = [];
    private readonly List<SocialFriend> _socialFriends = [];
    private readonly List<GuidePartyMember> _partyMembers = [];
    private readonly List<SocialFriend> _partyRoster = [];
    private readonly List<SocialFriend> _discordPartyMembers = [];
    private CancellationTokenSource? _pendingDiscordPartyInviteTimeoutCts;
    private bool _isInvitingPartyFriend;
    private string? _lastPartyInviteFriendId;
    private DateTimeOffset _lastPartyInviteAt = DateTimeOffset.MinValue;
    private string? _lastSuccessfulDiscordPartyInviteFriendId;
    private DateTimeOffset _lastSuccessfulDiscordPartyInviteAt = DateTimeOffset.MinValue;
    private readonly string[] _tabNames = ["Games & Apps", "Profile", "Xbox Home", "Media", "Settings"];
    private readonly string[] _defaultKeyboardLabels =
    [
        "a", "b", "c", "d", "e", "f", "g", "1", "2", "3",
        "h", "i", "j", "k", "l", "m", "n", "4", "5", "6",
        "o", "p", "q", "r", "s", "t", "u", "7", "8", "9",
        "v", "w", "x", "y", "z", ".", "@", "-", "0", "'"
    ];
    private readonly string[] _symbolKeyboardLabels =
    [
        "!", "\"", "#", "$", "%", "^", "&", "*", "(", ")",
        "[", "]", "{", "}", "<", ">", "/", "\\", "|", "~",
        "+", "=", "_", ";", ":", ",", ".", "?", "@", "-",
        "`", "'", "€", "£", "¥", "¢", "§", "°", "¬", "..."
    ];
    private readonly string[] _accentKeyboardLabels =
    [
        "á", "à", "â", "ä", "ã", "å", "æ", "ç", "é", "è",
        "ê", "ë", "í", "ì", "î", "ï", "ñ", "ó", "ò", "ô",
        "ö", "õ", "ø", "œ", "ú", "ù", "û", "ü", "ý", "ÿ",
        "Á", "É", "Í", "Ó", "Ú", "Ñ", "Ç", "¿", "¡", "."
    ];
    private readonly string[] _searchActionLabels = ["Caps", "Back", "Space", "Done"];

    private int _selectedIndex;
    private int _selectedMediaControlIndex = 2;
    private int _selectedMediaSubmenuIndex;
    private int _selectedTabIndex = 2;
    private int _selectedFriendListIndex;
    private int _selectedPartyRowIndex;
    private int _selectedSearchKeyIndex;
    private int _selectedFriendProfileActionIndex;
    private int _friendSearchCursorIndex;
    private string _clockText = string.Empty;
    private string _statusText = string.Empty;
    private string _friendSearchQuery = string.Empty;
    private bool _isMediaTransportFocused;
    private bool _isMediaSubmenuOpen;
    private bool _isSearchCapsEnabled;
    private bool _isSocialMessageOpen;
    private bool _returnToSearchOnProfileBack;
    private bool _isRefreshingFriends;
    private bool _isUsingDiscordPartyData;
    private string _partyStatusMessage = string.Empty;
    private GuideScreen _friendSearchReturnScreen = GuideScreen.FriendsList;
    private GuideScreen _profileReturnScreen = GuideScreen.FriendsList;
    private MediaFocusArea _mediaFocusArea = MediaFocusArea.List;
    private GuideScreen _screen = GuideScreen.MainMenu;
    private SearchKeyboardLayout _searchKeyboardLayout = SearchKeyboardLayout.Default;
    private int _tabTransitionDirection;
    private FriendProfile? _activeFriendProfile;
    private SocialFriend? _activeSocialFriend;
    private SocialFriend? _pendingDiscordPartyInvite;
    private string _socialMessageText = string.Empty;

    public GuideViewModel(
        DashboardViewModel dashboard,
        Window mainWindow,
        Action closeGuide,
        IAudioService audioService,
        IFriendsService friendsService,
        SocialIntegrationManager socialIntegrationManager,
        DiscordPartyService discordPartyService)
    {
        _dashboard = dashboard;
        _mainWindow = mainWindow;
        _audioService = audioService;
        _friendsService = friendsService;
        _socialIntegrationManager = socialIntegrationManager;
        _discordPartyService = discordPartyService;
        CloseGuide = closeGuide;

        Items = [];
        MediaControls = [];
        MediaSubmenuItems = [];
        FriendsListItems = [];
        PartyRows = [];
        SearchKeys = [];
        FriendProfileActions = [];

        ActivateMediaControlCommand = new RelayCommand(parameter =>
        {
            if (parameter is GuideMediaControlItem item)
            {
                SelectAndActivateMediaControl(item);
            }
        });
        OpenGuideMusicMenuCommand = new RelayCommand(OpenTransparentMusic);
        ActivateMediaSubmenuCommand = new RelayCommand(parameter =>
        {
            if (parameter is GuideMenuItem item)
            {
                SelectAndActivateMediaSubmenu(item);
            }
        });
        ActivateSearchKeyCommand = new RelayCommand(parameter =>
        {
            if (parameter is GuideKeyboardKeyItem item)
            {
                SelectAndActivateSearchKey(item);
            }
        });
        ActivateFriendProfileActionCommand = new RelayCommand(parameter =>
        {
            if (parameter is GuideMenuItem item)
            {
                SelectAndActivateFriendProfileAction(item);
            }
        });

        _dashboard.PropertyChanged += Dashboard_OnPropertyChanged;
        _clockTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(15) };
        _clockTimer.Tick += (_, _) => UpdateClock();
        _partyRefreshTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _partyRefreshTimer.Tick += async (_, _) => await RefreshPartySnapshotAsync().ConfigureAwait(true);

        BuildSearchKeys();
        BuildItems();
        BuildMediaSubmenu();
        UpdateClock();
        _ = RefreshAsync(showPopup: false);
    }

    public ObservableCollection<GuideMenuItem> Items { get; }

    public ObservableCollection<GuideMediaControlItem> MediaControls { get; }

    public ObservableCollection<GuideMenuItem> MediaSubmenuItems { get; }

    public ObservableCollection<GuideFriendListItem> FriendsListItems { get; }

    public ObservableCollection<GuidePartyRowItem> PartyRows { get; }

    public ObservableCollection<GuideKeyboardKeyItem> SearchKeys { get; }

    public ObservableCollection<GuideMenuItem> FriendProfileActions { get; }

    public IEnumerable<GuideKeyboardKeyItem> SearchMainKeys => SearchKeys.Take(40);

    public IEnumerable<GuideKeyboardKeyItem> SearchActionKeys => SearchKeys.Skip(40);

    public Action CloseGuide { get; }

    public ICommand ActivateMediaControlCommand { get; }

    public ICommand OpenGuideMusicMenuCommand { get; }

    public ICommand ActivateMediaSubmenuCommand { get; }

    public ICommand ActivateSearchKeyCommand { get; }

    public ICommand ActivateFriendProfileActionCommand { get; }

    public int SelectedIndex
    {
        get => _selectedIndex;
        set => SetProperty(ref _selectedIndex, Math.Clamp(value, 0, Math.Max(0, Items.Count - 1)));
    }

    public int SelectedFriendListIndex
    {
        get => _selectedFriendListIndex;
        set
        {
            if (SetProperty(ref _selectedFriendListIndex, Math.Clamp(value, 0, Math.Max(0, FriendsListItems.Count - 1))))
            {
                OnPropertyChanged(nameof(FriendsSelectionCountText));
            }
        }
    }

    public int SelectedPartyRowIndex
    {
        get => _selectedPartyRowIndex;
        set => SetProperty(ref _selectedPartyRowIndex, Math.Clamp(value, 0, Math.Max(0, PartyRows.Count - 1)));
    }

    public int SelectedSearchKeyIndex
    {
        get => _selectedSearchKeyIndex;
        set
        {
            if (SetProperty(ref _selectedSearchKeyIndex, Math.Clamp(value, 0, Math.Max(0, SearchKeys.Count - 1))))
            {
                RefreshSearchKeySelection();
            }
        }
    }

    public int SelectedFriendProfileActionIndex
    {
        get => _selectedFriendProfileActionIndex;
        set => SetProperty(ref _selectedFriendProfileActionIndex, Math.Clamp(value, 0, Math.Max(0, FriendProfileActions.Count - 1)));
    }

    public string Gamertag => _dashboard.Profile.Gamertag;

    public string GamerPicturePath => _dashboard.Profile.GamerPicturePath;

    public string ClockText
    {
        get => _clockText;
        private set => SetProperty(ref _clockText, value);
    }

    public string StatusText
    {
        get => _statusText;
        private set
        {
            if (SetProperty(ref _statusText, value))
            {
                OnPropertyChanged(nameof(ShowStatusText));
            }
        }
    }

    public string CurrentTabTitle => GetTabText(_selectedTabIndex);

    public int TabTransitionDirection
    {
        get => _tabTransitionDirection;
        private set => SetProperty(ref _tabTransitionDirection, value);
    }

    public string LeftOuterTabText => GetTabText(_selectedTabIndex - 2);

    public string LeftInnerTabText => GetTabText(_selectedTabIndex - 1);

    public string RightInnerTabText => GetTabText(_selectedTabIndex + 1);

    public string RightOuterTabText => GetTabText(_selectedTabIndex + 2);

    public bool IsHomeTab => _selectedTabIndex == 2;

    public bool IsMediaTab => _selectedTabIndex == 3 && IsMainGuideScreen;

    public bool IsMainGuideScreen => _screen == GuideScreen.MainMenu;

    public bool IsFriendsListScreen => _screen == GuideScreen.FriendsList;

    public bool IsPartyScreen => _screen == GuideScreen.Party;

    public bool IsFriendSearchScreen => _screen == GuideScreen.FriendSearch;

    public bool IsFriendProfileScreen => _screen == GuideScreen.FriendProfile;

    public bool IsFriendOverlayScreen
        => _screen is GuideScreen.FriendsList or GuideScreen.Party or GuideScreen.FriendSearch or GuideScreen.FriendProfile;

    public bool IsMediaSongRowFocused => IsMediaTab && _mediaFocusArea == MediaFocusArea.SongRow;

    public bool IsMediaSubmenuOpen
    {
        get => _isMediaSubmenuOpen;
        private set => SetProperty(ref _isMediaSubmenuOpen, value);
    }

    public bool IsMediaTransportFocused
    {
        get => _isMediaTransportFocused;
        private set
        {
            if (SetProperty(ref _isMediaTransportFocused, value))
            {
                RefreshMediaControlSelection();
            }
        }
    }

    public int SelectedMediaControlIndex
    {
        get => _selectedMediaControlIndex;
        private set
        {
            if (SetProperty(ref _selectedMediaControlIndex, Math.Clamp(value, 0, Math.Max(0, MediaControls.Count - 1))))
            {
                RefreshMediaControlSelection();
            }
        }
    }

    public int SelectedMediaSubmenuIndex
    {
        get => _selectedMediaSubmenuIndex;
        set => SetProperty(ref _selectedMediaSubmenuIndex, Math.Clamp(value, 0, Math.Max(0, MediaSubmenuItems.Count - 1)));
    }

    public string GuideMediaPlaybackLabel
        => _dashboard.IsMusicPlaying && _dashboard.CurrentMusicTrack is not null
            ? _dashboard.CurrentMusicTitle
            : "Select Music";

    public string FriendSearchQuery
    {
        get => _friendSearchQuery;
        set
        {
            if (SetProperty(ref _friendSearchQuery, value))
            {
                OnPropertyChanged(nameof(FriendSearchDisplayText));
            }
        }
    }

    public string FriendSearchDisplayText
        => FriendSearchQuery.Insert(Math.Clamp(_friendSearchCursorIndex, 0, FriendSearchQuery.Length), "|");

    public string FriendSearchHeaderText => "Add Recipient";

    public string FriendSearchInstructionText => "Enter a recipient's gamertag.";

    public string SearchSymbolsButtonText => _searchKeyboardLayout == SearchKeyboardLayout.Symbols ? "ABC" : "Symbols";

    public string SearchAccentsButtonText => _searchKeyboardLayout == SearchKeyboardLayout.Accents ? "ABC" : "Accents";

    public string FriendsHeaderText => $"Friends ({_socialFriends.Count(friend => friend.IsOnline)} Online)";

    public string FriendsCommunityHeaderText => "Community";

    public string FriendsTotalCountText => _socialFriends.Count.ToString();

    public string FriendsMessageCountText => "0";

    public string FriendsGameInviteCountText => "0";

    public string FriendsFooterText => "Sorted by online status";

    public string FriendsSelectionCountText
    {
        get
        {
            if (_socialFriends.Count == 0
                || SelectedFriendListIndex < 0
                || SelectedFriendListIndex >= FriendsListItems.Count)
            {
                return string.Empty;
            }

            var selectedItem = FriendsListItems[SelectedFriendListIndex];
            if (selectedItem.IsAddFriend)
            {
                return string.Empty;
            }

            var realFriendPosition = FriendsListItems
                .Take(SelectedFriendListIndex + 1)
                .Count(item => !item.IsAddFriend);

            return realFriendPosition <= 0 ? string.Empty : $"{realFriendPosition} of {_socialFriends.Count}";
        }
    }

    public string PartyHeaderText => $"Xbox LIVE Party ({PartyMemberCount})";

    public string PartyFriendCountText => _socialFriends.Count.ToString();

    public string PartyMessageCountText => "0";

    public string PartyGameCountText => "0";

    public int PartyMemberCount => _partyMembers.Count;

    public bool ShowStatusText => !IsFriendOverlayScreen && !_isSocialMessageOpen && !string.IsNullOrWhiteSpace(StatusText);

    public double FooterPromptTop => _screen switch
    {
        GuideScreen.FriendsList => 574,
        GuideScreen.Party => 574,
        GuideScreen.FriendSearch => 530,
        GuideScreen.FriendProfile => 562,
        _ => 646
    };

    public string ActiveFriendGamertag => _activeSocialFriend?.DisplayName ?? _activeFriendProfile?.Gamertag ?? string.Empty;

    public string ActiveFriendPicturePath => _activeSocialFriend?.AvatarPathOrUrl ?? _activeFriendProfile?.GamerPicturePath ?? GamerPicturePath;

    public string ActiveFriendGamerscore => _activeSocialFriend?.GamerscoreText
        ?? (_activeFriendProfile is null ? string.Empty : $"{_activeFriendProfile.Gamerscore:N0} G");

    public string ActiveFriendReputation => _activeSocialFriend?.ReputationText ?? NormalizeReputation(_activeFriendProfile?.Reputation);

    public string ActiveFriendZone => _activeSocialFriend?.ZoneText ?? _activeFriendProfile?.Zone ?? string.Empty;

    public string ActiveFriendStatus => _activeSocialFriend is not null
        ? SocialIntegrationManager.GetFriendActivityLabel(_activeSocialFriend)
        : _activeFriendProfile?.Status ?? string.Empty;

    public string ActiveFriendCountry => _activeSocialFriend is null
        ? NormalizeOfflineCountry(_activeFriendProfile?.Country)
        : (_activeSocialFriend.Source == SocialFriendSource.Local
            ? NormalizeOfflineCountry(_activeFriendProfile?.Country)
            : string.IsNullOrWhiteSpace(_activeFriendProfile?.Country)
                ? "Offline"
                : _activeFriendProfile.Country);

    public string ActiveFriendSourceLabel => _activeSocialFriend is null
        ? string.Empty
        : SocialIntegrationManager.GetSourceLabel(_activeSocialFriend);

    public bool IsSocialMessageOpen
    {
        get => _isSocialMessageOpen;
        private set
        {
            if (SetProperty(ref _isSocialMessageOpen, value))
            {
                OnPropertyChanged(nameof(ShowStatusText));
            }
        }
    }

    public string SocialMessageTitle => "Community";

    public string SocialMessageText
    {
        get => _socialMessageText;
        private set => SetProperty(ref _socialMessageText, value);
    }

    public GuideKeyboardKeyItem? SearchCapsKey => SearchKeys.ElementAtOrDefault(40);

    public GuideKeyboardKeyItem? SearchBackspaceKey => SearchKeys.ElementAtOrDefault(41);

    public GuideKeyboardKeyItem? SearchSpaceKey => SearchKeys.ElementAtOrDefault(42);

    public GuideKeyboardKeyItem? SearchDoneKey => SearchKeys.ElementAtOrDefault(43);

    public string FooterXActionText
        => _screen switch
        {
            GuideScreen.FriendsList => string.Empty,
            GuideScreen.Party => "Leave Party",
            GuideScreen.FriendSearch => "Backspace",
            GuideScreen.FriendProfile => string.Empty,
            _ => "Sign Out"
        };

    public string FooterYActionText
        => _screen switch
        {
            GuideScreen.FriendsList => "Change Sort",
            GuideScreen.Party => string.Empty,
            GuideScreen.FriendSearch => "Space",
            GuideScreen.FriendProfile => string.Empty,
            _ => "Minimize Dashboard"
        };

    public bool ShowFooterXAction
        => _screen is GuideScreen.MainMenu or GuideScreen.FriendSearch or GuideScreen.Party;

    public bool ShowFooterYAction => _screen is not GuideScreen.FriendProfile and not GuideScreen.Party;

    public void Start()
    {
        _ = RefreshAsync(showPopup: false);
        _clockTimer.Start();
    }

    public void Stop() => _clockTimer.Stop();

    public void Move(int delta)
    {
        if (IsSocialMessageOpen)
        {
            return;
        }

        switch (_screen)
        {
            case GuideScreen.FriendsList:
                MoveFriendList(delta);
                return;
            case GuideScreen.Party:
                MovePartyRows(delta);
                return;
            case GuideScreen.FriendSearch:
                MoveSearchKeyVertical(delta);
                return;
            case GuideScreen.FriendProfile:
                MoveFriendProfileActions(delta);
                return;
        }

        if (Items.Count == 0)
        {
            return;
        }

        if (IsMediaTab && IsMediaSubmenuOpen)
        {
            var oldSubmenuIndex = SelectedMediaSubmenuIndex;
            SelectedMediaSubmenuIndex = Math.Clamp(SelectedMediaSubmenuIndex + delta, 0, MediaSubmenuItems.Count - 1);
            if (SelectedMediaSubmenuIndex != oldSubmenuIndex)
            {
                _audioService.Play("focus");
            }

            return;
        }

        if (IsMediaTab && delta < 0 && _mediaFocusArea == MediaFocusArea.Transport)
        {
            SetMediaFocus(MediaFocusArea.SongRow);
            _audioService.Play("focus");
            return;
        }

        if (IsMediaTab && _mediaFocusArea == MediaFocusArea.SongRow)
        {
            SetMediaFocus(delta < 0 ? MediaFocusArea.List : MediaFocusArea.Transport);
            if (delta < 0)
            {
                SelectedIndex = Items.Count - 1;
            }

            _audioService.Play("focus");
            return;
        }

        if (IsMediaTab && delta > 0 && _mediaFocusArea == MediaFocusArea.List && SelectedIndex >= Items.Count - 1)
        {
            SetMediaFocus(MediaFocusArea.SongRow);
            _audioService.Play("focus");
            return;
        }

        if (IsMediaTab && _mediaFocusArea == MediaFocusArea.Transport)
        {
            return;
        }

        var oldIndex = SelectedIndex;
        SelectedIndex = Math.Clamp(SelectedIndex + delta, 0, Items.Count - 1);
        if (SelectedIndex != oldIndex)
        {
            SetMediaFocus(MediaFocusArea.List);
            _audioService.Play("focus");
        }
    }

    public bool TryHandleHorizontal(int delta)
    {
        if (IsSocialMessageOpen)
        {
            return true;
        }

        return _screen switch
        {
            GuideScreen.FriendSearch => TryMoveSearchKeyHorizontal(delta),
            GuideScreen.FriendsList or GuideScreen.Party or GuideScreen.FriendProfile => true,
            _ => TryMoveMediaTransport(delta)
        };
    }

    public void MoveTab(int delta)
    {
        if (!IsMainGuideScreen)
        {
            return;
        }

        var next = Math.Clamp(_selectedTabIndex + delta, 0, _tabNames.Length - 1);
        if (next == _selectedTabIndex)
        {
            return;
        }

        _selectedTabIndex = next;
        TabTransitionDirection = Math.Sign(delta);
        BuildItems();
        SelectedIndex = 0;
        CloseMediaSubmenu();
        SetMediaFocus(MediaFocusArea.List);
        StatusText = string.Empty;
        _audioService.Play(delta < 0 ? "page-left" : "page-right");
        NotifyTabStateChanged();
    }

    public void SelectTab(int index)
    {
        if (!IsMainGuideScreen)
        {
            return;
        }

        var next = Math.Clamp(index, 0, _tabNames.Length - 1);
        if (next == _selectedTabIndex)
        {
            return;
        }

        var sound = next < _selectedTabIndex ? "page-left" : "page-right";
        TabTransitionDirection = Math.Sign(next - _selectedTabIndex);
        _selectedTabIndex = next;
        BuildItems();
        SelectedIndex = 0;
        CloseMediaSubmenu();
        SetMediaFocus(MediaFocusArea.List);
        StatusText = string.Empty;
        _audioService.Play(sound);
        NotifyTabStateChanged();
    }

    public void SelectRelativeTab(int offset)
    {
        if (!IsMainGuideScreen)
        {
            return;
        }

        var next = _selectedTabIndex + offset;
        if (next < 0 || next >= _tabNames.Length)
        {
            return;
        }

        SelectTab(next);
    }

    public void ActivateSelected()
    {
        if (IsSocialMessageOpen)
        {
            DismissSocialMessage();
            _audioService.Play("select");
            return;
        }

        switch (_screen)
        {
            case GuideScreen.FriendsList:
                ActivateSelectedFriendListItem();
                return;
            case GuideScreen.Party:
                ActivateSelectedPartyRow();
                return;
            case GuideScreen.FriendSearch:
                ActivateSelectedSearchKey();
                return;
            case GuideScreen.FriendProfile:
                ActivateSelectedFriendProfileAction();
                return;
        }

        if (IsMediaTab && IsMediaSubmenuOpen)
        {
            ActivateSelectedMediaSubmenu();
            return;
        }

        if (IsMediaTab && _mediaFocusArea == MediaFocusArea.SongRow)
        {
            _audioService.Play("select");
            OpenTransparentMusic();
            return;
        }

        if (IsMediaTab && _mediaFocusArea == MediaFocusArea.Transport)
        {
            ActivateSelectedMediaControl();
            return;
        }

        if (SelectedIndex < 0 || SelectedIndex >= Items.Count)
        {
            return;
        }

        if (Items[SelectedIndex].IsNoOp)
        {
            return;
        }

        _audioService.Play("select");
        Items[SelectedIndex].Action();
    }

    public bool HandleBack()
    {
        if (IsSocialMessageOpen)
        {
            DismissSocialMessage();
            return true;
        }

        if (_screen == GuideScreen.FriendProfile)
        {
            if (_returnToSearchOnProfileBack)
            {
                OpenFriendSearch(_profileReturnScreen);
            }
            else
            {
                if (_profileReturnScreen == GuideScreen.Party)
                {
                    OpenParty();
                }
                else
                {
                    OpenFriendsList();
                }
            }

            _audioService.Play("back");
            return true;
        }

        if (_screen == GuideScreen.FriendSearch)
        {
            if (_friendSearchReturnScreen == GuideScreen.Party)
            {
                OpenParty();
            }
            else
            {
                OpenFriendsList();
            }
            _audioService.Play("back");
            return true;
        }

        if (_screen == GuideScreen.FriendsList)
        {
            SetScreen(GuideScreen.MainMenu);
            StatusText = string.Empty;
            _audioService.Play("back");
            return true;
        }

        if (_screen == GuideScreen.Party)
        {
            SetScreen(GuideScreen.MainMenu);
            StatusText = string.Empty;
            _audioService.Play("back");
            return true;
        }

        if (!IsMediaSubmenuOpen)
        {
            return false;
        }

        CloseMediaSubmenu();
        SetMediaFocus(MediaFocusArea.SongRow);
        _audioService.Play("back");
        return true;
    }

    public void HandleFooterX()
    {
        if (IsSocialMessageOpen)
        {
            return;
        }

        if (_screen == GuideScreen.FriendSearch)
        {
            BackspaceFriendSearch();
            _audioService.Play("select");
            return;
        }

        if (_screen == GuideScreen.FriendsList)
        {
            return;
        }

        if (_screen == GuideScreen.Party)
        {
            LeaveParty();
            _audioService.Play("select");
            return;
        }

        SignOut();
    }

    public void HandleFooterY()
    {
        if (IsSocialMessageOpen)
        {
            return;
        }

        if (_screen == GuideScreen.FriendSearch)
        {
            AppendToFriendSearch(" ");
            _audioService.Play("select");
            return;
        }

        if (_screen == GuideScreen.FriendsList)
        {
            SortFriendsByStatus();
            BuildFriendsListItems();
            _audioService.Play("select");
            return;
        }

        if (_screen == GuideScreen.Party)
        {
            return;
        }

        MinimizeDashboard();
    }

    public void PlaySound(string soundName)
        => _audioService.Play(soundName);

    public void ActivateFriendListItem(GuideFriendListItem item)
    {
        var index = FriendsListItems.IndexOf(item);
        if (index < 0)
        {
            return;
        }

        SelectedFriendListIndex = index;
        ActivateSelectedFriendListItem();
    }

    public void ActivatePartyRowItem(GuidePartyRowItem item)
    {
        var index = PartyRows.IndexOf(item);
        if (index < 0)
        {
            return;
        }

        SelectedPartyRowIndex = index;
        ActivateSelectedPartyRow();
    }

    public void ActivateSearchKey(GuideKeyboardKeyItem item)
    {
        var index = SearchKeys.IndexOf(item);
        if (index < 0)
        {
            return;
        }

        SelectedSearchKeyIndex = index;
        ActivateSelectedSearchKey();
    }

    public void ActivateFriendProfileAction(GuideMenuItem item)
    {
        var index = FriendProfileActions.IndexOf(item);
        if (index < 0)
        {
            return;
        }

        SelectedFriendProfileActionIndex = index;
        ActivateSelectedFriendProfileAction();
    }

    public void AppendFriendSearchCharacter(string value)
    {
        if (!IsFriendSearchScreen || string.IsNullOrEmpty(value))
        {
            return;
        }

        AppendToFriendSearch(value);
    }

    public void BackspaceFriendSearchFromKeyboard()
    {
        if (!IsFriendSearchScreen)
        {
            return;
        }

        BackspaceFriendSearch();
    }

    public void ConfirmFriendSearch()
    {
        if (!IsFriendSearchScreen)
        {
            return;
        }

        CompleteFriendSearch();
    }

    public void MoveFriendSearchCursor(int delta)
    {
        if (!IsFriendSearchScreen || delta == 0)
        {
            return;
        }

        var next = Math.Clamp(_friendSearchCursorIndex + delta, 0, FriendSearchQuery.Length);
        if (next == _friendSearchCursorIndex)
        {
            return;
        }

        _friendSearchCursorIndex = next;
        OnPropertyChanged(nameof(FriendSearchDisplayText));
        _audioService.Play("focus");
    }

    public void SwitchToSymbolKeyboard()
    {
        if (!IsFriendSearchScreen)
        {
            return;
        }

        SetSearchKeyboardLayout(_searchKeyboardLayout == SearchKeyboardLayout.Symbols ? SearchKeyboardLayout.Default : SearchKeyboardLayout.Symbols);
        _audioService.Play("select");
    }

    public void SwitchToAccentKeyboard()
    {
        if (!IsFriendSearchScreen)
        {
            return;
        }

        SetSearchKeyboardLayout(_searchKeyboardLayout == SearchKeyboardLayout.Accents ? SearchKeyboardLayout.Default : SearchKeyboardLayout.Accents);
        _audioService.Play("select");
    }

    public bool TryMoveMediaTransport(int delta)
    {
        if (IsMediaSubmenuOpen)
        {
            return true;
        }

        if (!IsMediaTab || !IsMediaTransportFocused || MediaControls.Count == 0)
        {
            return false;
        }

        var oldIndex = SelectedMediaControlIndex;
        SelectedMediaControlIndex = Math.Clamp(SelectedMediaControlIndex + delta, 0, MediaControls.Count - 1);
        if (SelectedMediaControlIndex != oldIndex)
        {
            _audioService.Play("focus");
        }

        return true;
    }

    public void SignOut()
    {
        _dashboard.Profile.OnlineStatus = "Offline";
        if (_dashboard.SaveProfileCommand.CanExecute(null))
        {
            _dashboard.SaveProfileCommand.Execute(null);
        }

        StatusText = "Signed out";
        OnPropertyChanged(nameof(Gamertag));
        OnPropertyChanged(nameof(GamerPicturePath));
        NotifySideTabTextChanged();
    }

    public void MinimizeDashboard()
    {
        _mainWindow.WindowState = WindowState.Minimized;
        StatusText = "Dashboard minimized";
    }

    public void Dispose()
    {
        _dashboard.PropertyChanged -= Dashboard_OnPropertyChanged;
        _clockTimer.Stop();
        _partyRefreshTimer.Stop();
        ClearPendingDiscordPartyInvite();
        _partyRefreshLock.Dispose();
        _friendsSaveLock.Dispose();
    }

    private async Task RefreshAsync(bool showPopup)
    {
        if (_isRefreshingFriends)
        {
            return;
        }

        _isRefreshingFriends = true;
        try
        {
            await LoadFriendsAsync(showPopup);
        }
        catch
        {
            // Keep the Guide responsive even if social or local friend loading fails.
        }
        finally
        {
            _isRefreshingFriends = false;
        }

        if (_screen == GuideScreen.Party || _isUsingDiscordPartyData)
        {
            await RefreshPartySnapshotAsync(forceRebuild: true).ConfigureAwait(true);
        }

        BuildItems();
        BuildMediaControls();
        BuildFriendsListItems();
        BuildPartyRows();
        OnPropertyChanged(nameof(Gamertag));
        OnPropertyChanged(nameof(GamerPicturePath));
        OnPropertyChanged(nameof(CurrentTabTitle));
        NotifySideTabTextChanged();
        OnPropertyChanged(nameof(GuideMediaPlaybackLabel));
        OnPropertyChanged(nameof(FriendsHeaderText));
        OnPropertyChanged(nameof(FooterXActionText));
        OnPropertyChanged(nameof(FooterYActionText));
        UpdateClock();
    }

    private void BuildItems()
    {
        Items.Clear();

        switch (_selectedTabIndex)
        {
            case 0:
                Items.Add(new GuideMenuItem("My Games", "\uE7FC", OpenMyGames));
                Items.Add(new GuideMenuItem("My Apps", "\uECAA", OpenMyApps));
                Items.Add(new GuideMenuItem("Game Marketplace", "\uE719", () => ShowPlaceholder("Game Marketplace")));
                Items.Add(new GuideMenuItem("App Marketplace", "\uE719", () => ShowPlaceholder("App Marketplace")));
                break;
            case 1:
                Items.Add(new GuideMenuItem("Achievements", string.Empty, () => ShowPlaceholder("Achievements")));
                Items.Add(new GuideMenuItem("Awards", string.Empty, () => ShowPlaceholder("Awards")));
                Items.Add(new GuideMenuItem("Recent", string.Empty, () => ShowPlaceholder("Recent")));
                Items.Add(new GuideMenuItem("My Games", string.Empty, OpenMyGames));
                Items.Add(new GuideMenuItem("Active Downloads", string.Empty, () => ShowPlaceholder("Active Downloads")));
                Items.Add(new GuideMenuItem("Redeem Code", string.Empty, () => ShowPlaceholder("Redeem Code")));
                break;
            case 3:
                Items.Add(new GuideMenuItem("Video Player", string.Empty, DoNothing, isNoOp: true));
                Items.Add(new GuideMenuItem("Music Player", string.Empty, OpenMusic));
                Items.Add(new GuideMenuItem("Picture Viewer", string.Empty, DoNothing, isNoOp: true));
                Items.Add(new GuideMenuItem("Windows Media Center", string.Empty, DoNothing, isNoOp: true));
                break;
            case 4:
                Items.Add(new GuideMenuItem("System Settings", "\uE713", OpenSettings));
                Items.Add(new GuideMenuItem("Profile", "\uE77B", OpenProfile));
                Items.Add(new GuideMenuItem("Preferences", "\uE115", OpenSettings));
                Items.Add(new GuideMenuItem("Turn Off", "\uE7E8", () => Application.Current.Shutdown()));
                break;
            default:
                Items.Add(new GuideMenuItem("Xbox Home", string.Empty, OpenXboxHome));
                Items.Add(new GuideMenuItem("Friends", "\uE13D", OpenFriends, _socialFriends.Count.ToString()));
                Items.Add(new GuideMenuItem("Party", "\uE716", OpenParty, PartyMemberCount.ToString()));
                Items.Add(new GuideMenuItem("Messages", "\uE119", () => ShowPlaceholder("Messages"), "0"));
                Items.Add(new GuideMenuItem("Minimize", "\uE8BB", CloseGuide));
                Items.Add(new GuideMenuItem("Chat", "\uE15F", () => ShowPlaceholder("Chat")));
                Items.Add(new GuideMenuItem(_dashboard.TrayGame?.Title ?? string.Empty, "\uE958", OpenTray));
                break;
        }

        BuildMediaControls();
    }

    private string GetTabText(int index)
    {
        if (index < 0 || index >= _tabNames.Length)
        {
            return string.Empty;
        }

        if (index == 1)
        {
            return Gamertag;
        }

        if (index == 2 && _selectedTabIndex == 1)
        {
            return Gamertag;
        }

        return _tabNames[index];
    }

    private void NotifyTabStateChanged()
    {
        OnPropertyChanged(nameof(CurrentTabTitle));
        NotifySideTabTextChanged();
        OnPropertyChanged(nameof(IsHomeTab));
        OnPropertyChanged(nameof(IsMediaTab));
        OnPropertyChanged(nameof(IsMediaSongRowFocused));
    }

    private void NotifySideTabTextChanged()
    {
        OnPropertyChanged(nameof(LeftOuterTabText));
        OnPropertyChanged(nameof(LeftInnerTabText));
        OnPropertyChanged(nameof(RightInnerTabText));
        OnPropertyChanged(nameof(RightOuterTabText));
    }

    private void BuildMediaSubmenu()
    {
        MediaSubmenuItems.Clear();
        MediaSubmenuItems.Add(new GuideMenuItem("Now Playing", string.Empty, () => ShowPlaceholder(GuideMediaPlaybackLabel)));
        MediaSubmenuItems.Add(new GuideMenuItem("All Songs", string.Empty, () => ShowPlaceholder("All Songs")));
        MediaSubmenuItems.Add(new GuideMenuItem("Playlists", string.Empty, () => ShowPlaceholder("Playlists")));
        MediaSubmenuItems.Add(new GuideMenuItem("Artists", string.Empty, () => ShowPlaceholder("Artists")));
        MediaSubmenuItems.Add(new GuideMenuItem("Albums", string.Empty, () => ShowPlaceholder("Albums")));
        MediaSubmenuItems.Add(new GuideMenuItem("Genres", string.Empty, () => ShowPlaceholder("Genres")));
        MediaSubmenuItems.Add(new GuideMenuItem("Search", string.Empty, () => ShowPlaceholder("Search")));
        MediaSubmenuItems.Add(new GuideMenuItem("Random All Songs", string.Empty, PlayRandomAllSongs));
    }

    private void BuildMediaControls()
    {
        MediaControls.Clear();
        MediaControls.Add(new GuideMediaControlItem("Previous", "\uE100", ExecutePreviousTrack));
        MediaControls.Add(new GuideMediaControlItem(_dashboard.IsMusicPlaying ? "Pause" : "Play", _dashboard.IsMusicPlaying ? "\uE103" : "\uE102", ExecutePlayPause));
        MediaControls.Add(new GuideMediaControlItem("Stop", "\uE15B", ExecuteStop));
        MediaControls.Add(new GuideMediaControlItem("Next", "\uE101", ExecuteNextTrack));
        MediaControls.Add(new GuideMediaControlItem(_dashboard.ShuffleText, "\uE8B1", ExecuteShuffle));
        MediaControls.Add(new GuideMediaControlItem("Volume", "\uE995", ExecuteVolumeUp));
        RefreshMediaControlSelection();
    }

    private void SetMediaFocus(MediaFocusArea area)
    {
        _mediaFocusArea = area;
        IsMediaTransportFocused = area == MediaFocusArea.Transport;
        OnPropertyChanged(nameof(IsMediaSongRowFocused));
    }

    private void OpenGuideMusicMenu()
    {
        BuildMediaSubmenu();
        IsMediaSubmenuOpen = true;
        SetMediaFocus(MediaFocusArea.Submenu);
        SelectedMediaSubmenuIndex = 0;
        StatusText = string.Empty;
    }

    private void CloseMediaSubmenu()
    {
        if (!IsMediaSubmenuOpen)
        {
            return;
        }

        IsMediaSubmenuOpen = false;
    }

    private void ActivateSelectedMediaSubmenu()
    {
        if (SelectedMediaSubmenuIndex < 0 || SelectedMediaSubmenuIndex >= MediaSubmenuItems.Count)
        {
            return;
        }

        _audioService.Play("select");
        MediaSubmenuItems[SelectedMediaSubmenuIndex].Action();
    }

    private void SelectAndActivateMediaSubmenu(GuideMenuItem item)
    {
        var index = MediaSubmenuItems.IndexOf(item);
        if (index < 0)
        {
            return;
        }

        SelectedMediaSubmenuIndex = index;
        ActivateSelectedMediaSubmenu();
    }

    private void PlayRandomAllSongs()
    {
        ExecuteShuffle();
        if (!_dashboard.IsMusicPlaying)
        {
            ExecutePlayPause();
        }

        StatusText = "Random all songs";
    }

    private void RefreshMediaControlSelection()
    {
        for (var index = 0; index < MediaControls.Count; index++)
        {
            MediaControls[index].IsSelected = IsMediaTransportFocused && index == SelectedMediaControlIndex;
        }
    }

    private void ActivateSelectedMediaControl()
    {
        if (SelectedMediaControlIndex < 0 || SelectedMediaControlIndex >= MediaControls.Count)
        {
            return;
        }

        _audioService.Play("select");
        MediaControls[SelectedMediaControlIndex].Action();
    }

    private void SelectAndActivateMediaControl(GuideMediaControlItem item)
    {
        var index = MediaControls.IndexOf(item);
        if (index < 0)
        {
            return;
        }

        SetMediaFocus(MediaFocusArea.Transport);
        SelectedMediaControlIndex = index;
        ActivateSelectedMediaControl();
    }

    private void UpdateClock()
        => ClockText = DateTime.Now.ToString("h:mm  tt");

    private void Dashboard_OnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(DashboardViewModel.Profile)
            or nameof(DashboardViewModel.TrayGame)
            or nameof(DashboardViewModel.OpenTrayTitle)
            or nameof(DashboardViewModel.SocialIntegrationModeDisplay))
        {
            _ = RefreshAsync(showPopup: false);
        }
        else if (e.PropertyName is nameof(DashboardViewModel.CurrentMusicTrack)
                 or nameof(DashboardViewModel.CurrentMusicTitle)
                 or nameof(DashboardViewModel.IsMusicPlaying)
                 or nameof(DashboardViewModel.MusicPlayPauseText)
                 or nameof(DashboardViewModel.ShuffleText))
        {
            BuildMediaControls();
            BuildMediaSubmenu();
            OnPropertyChanged(nameof(GuideMediaPlaybackLabel));
        }
    }

    private void ExecutePlayPause()
    {
        if (_dashboard.PlayPauseMusicCommand.CanExecute(null))
        {
            _dashboard.PlayPauseMusicCommand.Execute(null);
        }
    }

    private void ExecuteStop()
    {
        if (_dashboard.StopMusicCommand.CanExecute(null))
        {
            _dashboard.StopMusicCommand.Execute(null);
        }
    }

    private void ExecuteNextTrack()
    {
        if (_dashboard.NextMusicCommand.CanExecute(null))
        {
            _dashboard.NextMusicCommand.Execute(null);
        }
    }

    private void ExecutePreviousTrack()
    {
        if (_dashboard.PreviousMusicCommand.CanExecute(null))
        {
            _dashboard.PreviousMusicCommand.Execute(null);
        }
    }

    private void ExecuteShuffle()
    {
        if (_dashboard.ToggleShuffleMusicCommand.CanExecute(null))
        {
            _dashboard.ToggleShuffleMusicCommand.Execute(null);
        }
    }

    private void ExecuteVolumeUp()
    {
        if (_dashboard.VolumeUpCommand.CanExecute(null))
        {
            _dashboard.VolumeUpCommand.Execute(null);
        }
    }

    private void OpenXboxHome()
    {
        PrepareGuideReturnToDashboard();
        CloseGuide();
        RestoreMainWindow();
    }

    private void OpenTray()
    {
        if (_dashboard.TrayGame is null)
        {
            StatusText = "Open Tray is empty";
            return;
        }

        CloseGuide();
        if (_dashboard.LaunchGameCommand.CanExecute(_dashboard.TrayGame))
        {
            _dashboard.LaunchGameCommand.Execute(_dashboard.TrayGame);
        }
    }

    private void ShowPlaceholder(string pageName)
        => StatusText = $"{pageName} is not connected yet";

    private void OpenMyGames()
    {
        PrepareGuideReturnToDashboard();
        CloseGuide();
        if (_dashboard.OpenMyGamesCommand.CanExecute(null))
        {
            _dashboard.OpenMyGamesCommand.Execute(null);
        }

        RestoreMainWindow();
    }

    private void OpenMyApps()
    {
        PrepareGuideReturnToDashboard();
        CloseGuide();
        if (_dashboard.OpenMyAppsCommand.CanExecute(null))
        {
            _dashboard.OpenMyAppsCommand.Execute(null);
        }

        RestoreMainWindow();
    }

    private void OpenFriends()
    {
        if (!CanUseFriendsOverlay())
        {
            return;
        }

        OpenFriendsList();
        _audioService.Play("select");
    }

    public void OpenFriendsOverlayFromDashboard()
    {
        if (!CanUseFriendsOverlay())
        {
            return;
        }

        _selectedTabIndex = 2;
        BuildItems();
        NotifyTabStateChanged();
        OpenFriendsList();
    }

    private void OpenFriendsList()
    {
        if (!CanUseFriendsOverlay())
        {
            return;
        }

        DismissSocialMessage();
        BuildFriendsListItems();
        SetScreen(GuideScreen.FriendsList);
        SelectedFriendListIndex = 0;
        StatusText = string.Empty;
        _ = RefreshAsync(showPopup: true);
    }

    private void OpenParty()
    {
        if (!CanUseFriendsOverlay())
        {
            return;
        }

        DismissSocialMessage();
        BuildPartyRows();
        SetScreen(GuideScreen.Party);
        SelectedPartyRowIndex = FindNextSelectablePartyIndex(0, 1);
        StatusText = string.Empty;
        _ = RefreshAsync(showPopup: true);
        _ = RefreshPartySnapshotAsync(forceRebuild: true);
        _audioService.Play("select");
    }

    private void BuildFriendsListItems()
    {
        FriendsListItems.Clear();
        FriendsListItems.Add(new GuideFriendListItem
        {
            FriendId = "action:add-friend",
            Gamertag = "Add Friend",
            Subtitle = string.Empty,
            Status = string.Empty,
            AvatarPath = string.Empty,
            IsAddFriend = true
        });

        foreach (var friend in SortSocialFriends(_socialFriends))
        {
            FriendsListItems.Add(new GuideFriendListItem
            {
                FriendId = friend.Id,
                Gamertag = friend.DisplayName,
                Subtitle = SocialIntegrationManager.GetSourceLabel(friend),
                Status = SocialIntegrationManager.GetFriendActivityLabel(friend),
                AvatarPath = friend.AvatarPathOrUrl
            });
        }

        OnPropertyChanged(nameof(FriendsHeaderText));
        OnPropertyChanged(nameof(FriendsTotalCountText));
        OnPropertyChanged(nameof(FriendsSelectionCountText));
    }

    private void MoveFriendList(int delta)
    {
        if (FriendsListItems.Count == 0)
        {
            return;
        }

        var oldIndex = SelectedFriendListIndex;
        SelectedFriendListIndex = Math.Clamp(SelectedFriendListIndex + delta, 0, FriendsListItems.Count - 1);
        if (SelectedFriendListIndex != oldIndex)
        {
            _audioService.Play("focus");
        }
    }

    private void BuildPartyRows()
    {
        BuildPartyMembers();
        PartyRows.Clear();
        PartyRows.Add(new GuidePartyRowItem
        {
            RowKind = "Invite",
            Title = "Invite Players to Party",
            IsSelectable = true
        });
        PartyRows.Add(new GuidePartyRowItem
        {
            RowKind = "Disabled",
            Title = "Invite Party to Game",
            IsSelectable = false
        });
        PartyRows.Add(new GuidePartyRowItem
        {
            RowKind = "Options",
            Title = "Party Options:  Party Chat, Invite Only",
            IsSelectable = false
        });

        foreach (var member in _partyMembers)
        {
            PartyRows.Add(new GuidePartyRowItem
            {
                RowKind = "Member",
                Title = member.Gamertag,
                AvatarPath = member.AvatarPath,
                ActivityText = member.ActivityText,
                ActivityIcon = member.ActivityIcon,
                ShowVoiceIcon = member.ShowVoiceIcon,
                IsHost = member.IsHost,
                IsSelectable = true
            });
        }

        OnPropertyChanged(nameof(PartyHeaderText));
        OnPropertyChanged(nameof(PartyFriendCountText));
        OnPropertyChanged(nameof(PartyMessageCountText));
        OnPropertyChanged(nameof(PartyGameCountText));
        OnPropertyChanged(nameof(PartyMemberCount));

        var partyMenuItem = Items.FirstOrDefault(item => string.Equals(item.Title, "Party", StringComparison.OrdinalIgnoreCase));
        if (partyMenuItem is not null)
        {
            partyMenuItem.Count = PartyMemberCount.ToString();
        }

    }

    private void BuildPartyMembers()
    {
        _partyMembers.Clear();

        if (_isUsingDiscordPartyData)
        {
            foreach (var friend in _discordPartyMembers)
            {
                _partyMembers.Add(new GuidePartyMember
                {
                    Gamertag = friend.DisplayName,
                    AvatarPath = friend.AvatarPathOrUrl,
                    ActivityText = SocialIntegrationManager.GetFriendActivityLabel(friend),
                    ActivityIcon = "\uE7FC",
                    ShowVoiceIcon = friend.ShowVoiceIndicator,
                    IsHost = friend.IsPartyHost
                });
            }

            if (_pendingDiscordPartyInvite is not null
                && !_discordPartyMembers.Any(existing => string.Equals(existing.Id, _pendingDiscordPartyInvite.Id, StringComparison.OrdinalIgnoreCase)))
            {
                _partyMembers.Add(new GuidePartyMember
                {
                    Gamertag = _pendingDiscordPartyInvite.DisplayName,
                    AvatarPath = _pendingDiscordPartyInvite.AvatarPathOrUrl,
                    ActivityText = "Ringing...",
                    ActivityIcon = "\uE717",
                    ShowVoiceIcon = false,
                    IsHost = false
                });
            }

            return;
        }

        var partyFriends = _partyRoster.Count > 0
            ? _partyRoster
            : _socialFriends.Take(1).ToList();

        var host = SocialIntegrationManager.BuildPartyHost(_dashboard.Profile);
        _partyMembers.Add(new GuidePartyMember
        {
            Gamertag = host.DisplayName,
            AvatarPath = host.AvatarPathOrUrl,
            ActivityText = host.ActivityText,
            ActivityIcon = "\uE7FC",
            ShowVoiceIcon = host.ShowVoiceIndicator,
            IsHost = true
        });

        foreach (var friend in partyFriends)
        {
            _partyMembers.Add(new GuidePartyMember
            {
                Gamertag = friend.DisplayName,
                AvatarPath = friend.AvatarPathOrUrl,
                ActivityText = SocialIntegrationManager.GetFriendActivityLabel(friend),
                ActivityIcon = "\uE7FC",
                ShowVoiceIcon = friend.ShowVoiceIndicator,
                IsHost = friend.IsPartyHost
            });
        }
    }

    private bool ShouldUseDiscordPartyData()
        => false;

    private void UpdatePartyRefreshState()
    {
        if (_screen == GuideScreen.Party && ShouldUseDiscordPartyData())
        {
            if (!_partyRefreshTimer.IsEnabled)
            {
                _partyRefreshTimer.Start();
            }
        }
        else
        {
            _partyRefreshTimer.Stop();
        }
    }

    private async Task RefreshPartySnapshotAsync(bool forceRebuild = false)
    {
        if (!await _partyRefreshLock.WaitAsync(0).ConfigureAwait(true))
        {
            return;
        }

        try
        {
            if (!ShouldUseDiscordPartyData())
            {
                var hadDiscordPartyState = _isUsingDiscordPartyData || _discordPartyMembers.Count > 0 || !string.IsNullOrWhiteSpace(_partyStatusMessage);
                _isUsingDiscordPartyData = false;
                _partyStatusMessage = string.Empty;
                _discordPartyMembers.Clear();
                ClearPendingDiscordPartyInvite();

                if (forceRebuild || hadDiscordPartyState)
                {
                    BuildPartyRows();
                    if (_screen == GuideScreen.Party)
                    {
                        var selectedIndex = Math.Clamp(SelectedPartyRowIndex, 0, Math.Max(0, PartyRows.Count - 1));
                        SelectedPartyRowIndex = FindNextSelectablePartyIndex(selectedIndex, 1);
                    }
                }

                return;
            }

            var snapshot = await _discordPartyService
                .GetCurrentPartySnapshotAsync(_dashboard.Profile, _dashboard.Settings.DiscordConnectionState)
                .ConfigureAwait(true);

            ResolvePendingDiscordPartyInvite(snapshot.Members);
            var membersChanged = !PartyMembersEqual(_discordPartyMembers, snapshot.Members);
            var shouldRebuild = forceRebuild
                || membersChanged
                || _isUsingDiscordPartyData != snapshot.UsesDiscordData
                || !string.Equals(_partyStatusMessage, snapshot.StatusMessage, StringComparison.Ordinal);

            _isUsingDiscordPartyData = snapshot.UsesDiscordData;
            _partyStatusMessage = snapshot.StatusMessage;
            _discordPartyMembers.Clear();
            foreach (var member in snapshot.Members)
            {
                _discordPartyMembers.Add(member);
            }

            if (shouldRebuild)
            {
                BuildPartyRows();
                if (_screen == GuideScreen.Party)
                {
                    var selectedIndex = Math.Clamp(SelectedPartyRowIndex, 0, Math.Max(0, PartyRows.Count - 1));
                    SelectedPartyRowIndex = FindNextSelectablePartyIndex(selectedIndex, 1);
                }
            }
        }
        finally
        {
            _partyRefreshLock.Release();
        }
    }

    private static bool PartyMembersEqual(IReadOnlyList<SocialFriend> left, IReadOnlyList<SocialFriend> right)
    {
        if (ReferenceEquals(left, right))
        {
            return true;
        }

        if (left.Count != right.Count)
        {
            return false;
        }

        for (var index = 0; index < left.Count; index++)
        {
            var leftMember = left[index];
            var rightMember = right[index];

            if (!string.Equals(leftMember.Id, rightMember.Id, StringComparison.OrdinalIgnoreCase)
                || !string.Equals(leftMember.DisplayName, rightMember.DisplayName, StringComparison.Ordinal)
                || !string.Equals(leftMember.AvatarPathOrUrl, rightMember.AvatarPathOrUrl, StringComparison.Ordinal)
                || !string.Equals(leftMember.StatusText, rightMember.StatusText, StringComparison.Ordinal)
                || !string.Equals(leftMember.ActivityText, rightMember.ActivityText, StringComparison.Ordinal)
                || leftMember.IsOnline != rightMember.IsOnline
                || leftMember.ShowVoiceIndicator != rightMember.ShowVoiceIndicator
                || leftMember.IsPartyHost != rightMember.IsPartyHost)
            {
                return false;
            }
        }

        return true;
    }

    private void MovePartyRows(int delta)
    {
        if (PartyRows.Count == 0 || delta == 0)
        {
            return;
        }

        var oldIndex = SelectedPartyRowIndex;
        var candidate = Math.Clamp(SelectedPartyRowIndex + delta, 0, PartyRows.Count - 1);
        SelectedPartyRowIndex = FindNextSelectablePartyIndex(candidate, delta);

        if (SelectedPartyRowIndex != oldIndex)
        {
            _audioService.Play("focus");
        }
    }

    private int FindNextSelectablePartyIndex(int candidate, int direction)
    {
        if (PartyRows.Count == 0)
        {
            return 0;
        }

        var step = direction < 0 ? -1 : 1;
        var index = Math.Clamp(candidate, 0, PartyRows.Count - 1);

        while (index >= 0 && index < PartyRows.Count)
        {
            if (PartyRows[index].IsSelectable)
            {
                return index;
            }

            index += step;
        }

        return SelectedPartyRowIndex >= 0 && SelectedPartyRowIndex < PartyRows.Count
            ? SelectedPartyRowIndex
            : 0;
    }

    private void ActivateSelectedPartyRow()
    {
        if (SelectedPartyRowIndex < 0 || SelectedPartyRowIndex >= PartyRows.Count)
        {
            return;
        }

        var row = PartyRows[SelectedPartyRowIndex];
        if (!row.IsSelectable)
        {
            return;
        }

        _audioService.Play("select");

        if (row.RowKind == "Invite")
        {
            OpenFriendSearch(GuideScreen.Party);
            return;
        }

        StatusText = row.RowKind == "Member"
            ? $"{row.Title} is in party"
            : string.Empty;
    }

    private void LeaveParty()
    {
        if (_isUsingDiscordPartyData)
        {
            _discordPartyService.LeaveCurrentPartyAsync().GetAwaiter().GetResult();
            _discordPartyMembers.Clear();
            _isUsingDiscordPartyData = false;
            _partyStatusMessage = string.Empty;
            ClearPendingDiscordPartyInvite();
        }
        else
        {
            _partyRoster.Clear();
        }

        BuildPartyRows();
        SetScreen(GuideScreen.MainMenu);
        StatusText = "Left party";
    }

    private void ActivateSelectedFriendListItem()
    {
        if (SelectedFriendListIndex < 0 || SelectedFriendListIndex >= FriendsListItems.Count)
        {
            return;
        }

        var item = FriendsListItems[SelectedFriendListIndex];
        _audioService.Play("select");

        if (item.IsAddFriend)
        {
            OpenFriendSearch();
            return;
        }

        var friend = _socialFriends.FirstOrDefault(profile => string.Equals(profile.Id, item.FriendId, StringComparison.OrdinalIgnoreCase))
            ?? _socialFriends.FirstOrDefault(profile => string.Equals(profile.DisplayName, item.Gamertag, StringComparison.OrdinalIgnoreCase));
        if (friend is null)
        {
            return;
        }

        _returnToSearchOnProfileBack = false;
        _profileReturnScreen = GuideScreen.FriendsList;
        SetActiveFriend(friend);
        BuildFriendProfileActions();
        SetScreen(GuideScreen.FriendProfile);
        SelectedFriendProfileActionIndex = 0;
    }

    private void OpenFriendSearch(GuideScreen returnScreen = GuideScreen.FriendsList)
    {
        if (!CanUseFriendsOverlay())
        {
            return;
        }

        _friendSearchReturnScreen = returnScreen;
        FriendSearchQuery = string.Empty;
        _friendSearchCursorIndex = 0;
        _isSearchCapsEnabled = false;
        _searchKeyboardLayout = SearchKeyboardLayout.Default;
        BuildSearchKeys();
        SetScreen(GuideScreen.FriendSearch);
        SelectedSearchKeyIndex = 0;
        StatusText = string.Empty;
    }

    private void BuildSearchKeys()
    {
        SearchKeys.Clear();
        foreach (var label in GetActiveSearchKeyboardLabels().Concat(_searchActionLabels))
        {
            var isWide = label is "Caps" or "Back" or "Space" or "Done";
            SearchKeys.Add(new GuideKeyboardKeyItem(label, () => HandleSearchKey(label), isWide));
        }

        RefreshSearchKeySelection();
        OnPropertyChanged(nameof(SearchMainKeys));
        OnPropertyChanged(nameof(SearchActionKeys));
        OnPropertyChanged(nameof(SearchCapsKey));
        OnPropertyChanged(nameof(SearchBackspaceKey));
        OnPropertyChanged(nameof(SearchSpaceKey));
        OnPropertyChanged(nameof(SearchDoneKey));
        OnPropertyChanged(nameof(SearchSymbolsButtonText));
        OnPropertyChanged(nameof(SearchAccentsButtonText));
    }

    private void HandleSearchKey(string label)
    {
        switch (label)
        {
            case "Caps":
                _isSearchCapsEnabled = !_isSearchCapsEnabled;
                break;
            case "Back":
                BackspaceFriendSearch();
                break;
            case "Space":
                AppendToFriendSearch(" ");
                break;
            case "Done":
                CompleteFriendSearch();
                break;
            default:
                AppendToFriendSearch(_isSearchCapsEnabled ? label.ToUpperInvariant() : label);
                break;
        }
    }

    private void BackspaceFriendSearch()
    {
        if (string.IsNullOrEmpty(FriendSearchQuery) || _friendSearchCursorIndex <= 0)
        {
            return;
        }

        FriendSearchQuery = FriendSearchQuery.Remove(_friendSearchCursorIndex - 1, 1);
        _friendSearchCursorIndex--;
        OnPropertyChanged(nameof(FriendSearchDisplayText));
    }

    private void AppendToFriendSearch(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return;
        }

        FriendSearchQuery = FriendSearchQuery.Insert(_friendSearchCursorIndex, value);
        _friendSearchCursorIndex += value.Length;
        OnPropertyChanged(nameof(FriendSearchDisplayText));
    }

    private void CompleteFriendSearch()
    {
        var gamertag = FriendSearchQuery.Trim();
        if (string.IsNullOrWhiteSpace(gamertag))
        {
            return;
        }

        var existing = _socialFriends.FirstOrDefault(friend => string.Equals(friend.DisplayName, gamertag, StringComparison.OrdinalIgnoreCase));
        _returnToSearchOnProfileBack = true;
        _profileReturnScreen = _friendSearchReturnScreen;
        SetActiveFriend(existing ?? _socialIntegrationManager.CreateLocalFriend(gamertag));
        BuildFriendProfileActions();
        SetScreen(GuideScreen.FriendProfile);
        SelectedFriendProfileActionIndex = 0;
        StatusText = string.Empty;
    }

    private bool TryMoveSearchKeyHorizontal(int delta)
    {
        if (!IsFriendSearchScreen || SearchKeys.Count == 0)
        {
            return false;
        }

        var oldIndex = SelectedSearchKeyIndex;
        var nextIndex = SelectedSearchKeyIndex switch
        {
            40 => delta > 0 ? 41 : 40,
            41 => delta > 0 ? 42 : 40,
            42 => delta > 0 ? 43 : 41,
            43 => delta < 0 ? 42 : 43,
            _ => MoveSearchKeyHorizontalFromGrid(delta)
        };

        SelectedSearchKeyIndex = nextIndex;
        if (SelectedSearchKeyIndex != oldIndex)
        {
            _audioService.Play("focus");
        }

        return true;
    }

    private void MoveSearchKeys(int delta)
    {
        if (SearchKeys.Count == 0)
        {
            return;
        }

        var oldIndex = SelectedSearchKeyIndex;
        SelectedSearchKeyIndex = Math.Clamp(SelectedSearchKeyIndex + delta, 0, SearchKeys.Count - 1);
        if (SelectedSearchKeyIndex != oldIndex)
        {
            _audioService.Play("focus");
        }
    }

    private void MoveSearchKeyVertical(int delta)
    {
        if (!IsFriendSearchScreen || SearchKeys.Count == 0 || delta == 0)
        {
            return;
        }

        var oldIndex = SelectedSearchKeyIndex;
        if (SelectedSearchKeyIndex >= 40)
        {
            SelectedSearchKeyIndex = SelectedSearchKeyIndex switch
            {
                40 => 30,
                41 => 34,
                42 => 37,
                43 => 39,
                _ => SelectedSearchKeyIndex
            };
        }
        else
        {
            var row = SelectedSearchKeyIndex / 10;
            var column = SelectedSearchKeyIndex % 10;

            if (delta < 0)
            {
                SelectedSearchKeyIndex = row == 0
                    ? SelectedSearchKeyIndex
                    : SelectedSearchKeyIndex - 10;
            }
            else
            {
                SelectedSearchKeyIndex = row switch
                {
                    <= 2 => SelectedSearchKeyIndex + 10,
                    _ => column switch
                    {
                        0 => 40,
                        <= 4 => 41,
                        <= 8 => 42,
                        _ => 43
                    }
                };
            }
        }

        if (SelectedSearchKeyIndex != oldIndex)
        {
            _audioService.Play("focus");
        }
    }

    private int MoveSearchKeyHorizontalFromGrid(int delta)
    {
        var row = SelectedSearchKeyIndex / 10;
        var column = SelectedSearchKeyIndex % 10;
        var nextColumn = Math.Clamp(column + delta, 0, 9);
        return row * 10 + nextColumn;
    }

    private void RefreshSearchKeySelection()
    {
        for (var index = 0; index < SearchKeys.Count; index++)
        {
            SearchKeys[index].IsSelected = index == SelectedSearchKeyIndex;
        }
    }

    private void ActivateSelectedSearchKey()
    {
        if (SelectedSearchKeyIndex < 0 || SelectedSearchKeyIndex >= SearchKeys.Count)
        {
            return;
        }

        _audioService.Play("select");
        SearchKeys[SelectedSearchKeyIndex].Action();
    }

    private void SelectAndActivateSearchKey(GuideKeyboardKeyItem item)
    {
        var index = SearchKeys.IndexOf(item);
        if (index < 0)
        {
            return;
        }

        SelectedSearchKeyIndex = index;
        ActivateSelectedSearchKey();
    }

    private void SetActiveFriend(SocialFriend friend)
    {
        _activeSocialFriend = friend;
        _activeFriendProfile = BuildFriendProfileSnapshot(friend);
        OnPropertyChanged(nameof(ActiveFriendGamertag));
        OnPropertyChanged(nameof(ActiveFriendPicturePath));
        OnPropertyChanged(nameof(ActiveFriendGamerscore));
        OnPropertyChanged(nameof(ActiveFriendReputation));
        OnPropertyChanged(nameof(ActiveFriendZone));
        OnPropertyChanged(nameof(ActiveFriendStatus));
        OnPropertyChanged(nameof(ActiveFriendCountry));
        OnPropertyChanged(nameof(ActiveFriendSourceLabel));
    }

    private void BuildFriendProfileActions()
    {
        FriendProfileActions.Clear();
        var source = _activeSocialFriend?.Source;
        var isLocalFriend = source == SocialFriendSource.Local;
        var isDiscordFriend = source == SocialFriendSource.Discord;
        var isSavedFriend = isLocalFriend
            && _activeSocialFriend is not null
            && _friends.Any(friend => string.Equals(friend.Gamertag, _activeSocialFriend.DisplayName, StringComparison.OrdinalIgnoreCase));
        var primaryTitle = isDiscordFriend
            ? "Remove Friend"
            : isSavedFriend ? "Remove Friend" : "Send Friend Request";
        Action primaryAction = isDiscordFriend
            ? RemoveFriend
            : isLocalFriend
                ? (isSavedFriend ? RemoveFriend : SendFriendRequest)
                : DoNothing;

        FriendProfileActions.Add(new GuideMenuItem(primaryTitle, string.Empty, primaryAction));
        FriendProfileActions.Add(new GuideMenuItem("Invite to Game", string.Empty, DoNothing));
        FriendProfileActions.Add(new GuideMenuItem("Invite to Party", string.Empty, InviteActiveFriendToParty));
        FriendProfileActions.Add(new GuideMenuItem("Send Message", string.Empty, DoNothing));
        FriendProfileActions.Add(new GuideMenuItem("Compare Games", string.Empty, DoNothing));
        FriendProfileActions.Add(new GuideMenuItem("Submit Player Review", string.Empty, DoNothing));
        FriendProfileActions.Add(new GuideMenuItem("File Complaint", string.Empty, DoNothing));
        FriendProfileActions.Add(new GuideMenuItem("Mute", string.Empty, DoNothing));
    }

    private void MoveFriendProfileActions(int delta)
    {
        if (FriendProfileActions.Count == 0)
        {
            return;
        }

        var oldIndex = SelectedFriendProfileActionIndex;
        SelectedFriendProfileActionIndex = Math.Clamp(SelectedFriendProfileActionIndex + delta, 0, FriendProfileActions.Count - 1);
        if (SelectedFriendProfileActionIndex != oldIndex)
        {
            _audioService.Play("focus");
        }
    }

    private void ActivateSelectedFriendProfileAction()
    {
        if (SelectedFriendProfileActionIndex < 0 || SelectedFriendProfileActionIndex >= FriendProfileActions.Count)
        {
            return;
        }

        _audioService.Play("select");
        FriendProfileActions[SelectedFriendProfileActionIndex].Action();
    }

    private void SelectAndActivateFriendProfileAction(GuideMenuItem item)
    {
        var index = FriendProfileActions.IndexOf(item);
        if (index < 0)
        {
            return;
        }

        SelectedFriendProfileActionIndex = index;
        ActivateSelectedFriendProfileAction();
    }

    private void SendFriendRequest()
    {
        if (_activeSocialFriend is null)
        {
            return;
        }

        if (_activeSocialFriend.Source is not SocialFriendSource.Local)
        {
            StatusText = $"{_activeSocialFriend.DisplayName} is managed through {SocialIntegrationManager.GetSourceLabel(_activeSocialFriend)}";
            return;
        }

        if (_friends.Any(friend => string.Equals(friend.Gamertag, _activeSocialFriend.DisplayName, StringComparison.OrdinalIgnoreCase)))
        {
            StatusText = $"{_activeSocialFriend.DisplayName} is already on your friends list";
            return;
        }

        var localFriend = NormalizeOfflineFriend(LocalSocialIntegrationService.MapToLocalFriend(_activeSocialFriend));
        _friends.Add(localFriend);
        if (!_socialFriends.Any(friend => string.Equals(friend.Id, _activeSocialFriend.Id, StringComparison.OrdinalIgnoreCase)))
        {
            _socialFriends.Add(LocalSocialIntegrationService.MapFromLocalFriend(localFriend));
            _socialFriends.Sort((left, right) => CompareSocialFriends(left, right));
        }

        QueueFriendsSave();
        BuildFriendsListItems();
        BuildFriendProfileActions();
        StatusText = $"{_activeSocialFriend.DisplayName} added to friends";
    }

    private void RemoveFriend()
    {
        if (_activeSocialFriend is null)
        {
            return;
        }

        if (_activeSocialFriend.Source == SocialFriendSource.Discord)
        {
            ShowSocialMessage("Discord friends cannot be removed from here.");
            return;
        }

        if (_activeSocialFriend.Source is not SocialFriendSource.Local)
        {
            StatusText = $"{_activeSocialFriend.DisplayName} is managed through {SocialIntegrationManager.GetSourceLabel(_activeSocialFriend)}";
            return;
        }

        var friend = _friends.FirstOrDefault(existing => string.Equals(existing.Gamertag, _activeSocialFriend.DisplayName, StringComparison.OrdinalIgnoreCase));
        if (friend is null)
        {
            StatusText = $"{_activeSocialFriend.DisplayName} is not on your friends list";
            return;
        }

        _friends.Remove(friend);
        _socialFriends.RemoveAll(existing => string.Equals(existing.Id, _activeSocialFriend.Id, StringComparison.OrdinalIgnoreCase)
            || (existing.Source == SocialFriendSource.Local
                && string.Equals(existing.DisplayName, _activeSocialFriend.DisplayName, StringComparison.OrdinalIgnoreCase)));
        QueueFriendsSave();
        BuildFriendsListItems();
        BuildFriendProfileActions();
        SelectedFriendProfileActionIndex = 0;
        StatusText = $"{_activeSocialFriend.DisplayName} removed from friends";
    }

    private async void InviteActiveFriendToParty()
    {
        if (_activeSocialFriend is null)
        {
            return;
        }

        if (_isInvitingPartyFriend)
        {
            StatusText = $"Inviting {_activeSocialFriend.DisplayName} to party...";
            return;
        }

        if (_activeSocialFriend.Source == SocialFriendSource.Discord
            && string.Equals(_lastPartyInviteFriendId, _activeSocialFriend.Id, StringComparison.OrdinalIgnoreCase)
            && DateTimeOffset.UtcNow - _lastPartyInviteAt < TimeSpan.FromSeconds(5))
        {
            StatusText = $"Calling {_activeSocialFriend.DisplayName}...";
            return;
        }

        _isInvitingPartyFriend = true;
        _lastPartyInviteFriendId = _activeSocialFriend.Id;
        _lastPartyInviteAt = DateTimeOffset.UtcNow;

        try
        {
            StatusText = $"Inviting {_activeSocialFriend.DisplayName} to party...";

            var result = await _socialIntegrationManager
                .InviteToPartyAsync(_activeSocialFriend, _dashboard.Settings.DiscordConnectionState)
                .ConfigureAwait(true);

            if (result.AddToPartyList)
            {
                AddToPartyRoster(_activeSocialFriend);
            }
            else if (_activeSocialFriend.Source == SocialFriendSource.Discord && string.IsNullOrWhiteSpace(result.PopupMessage))
            {
                SetPendingDiscordPartyInvite(_activeSocialFriend);
                _lastSuccessfulDiscordPartyInviteFriendId = _activeSocialFriend.Id;
                _lastSuccessfulDiscordPartyInviteAt = DateTimeOffset.UtcNow;
            }

            BuildPartyRows();
            _ = RefreshPartySnapshotAsync(forceRebuild: true);

            var suppressDuplicateDiscordFailure = _activeSocialFriend.Source == SocialFriendSource.Discord
                && string.Equals(result.PopupMessage, "Discord party call is not available yet.", StringComparison.Ordinal)
                && string.Equals(_lastSuccessfulDiscordPartyInviteFriendId, _activeSocialFriend.Id, StringComparison.OrdinalIgnoreCase)
                && DateTimeOffset.UtcNow - _lastSuccessfulDiscordPartyInviteAt < TimeSpan.FromSeconds(8);

            if (!string.IsNullOrWhiteSpace(result.PopupMessage) && !suppressDuplicateDiscordFailure)
            {
                ClearPendingDiscordPartyInvite();
                ShowSocialMessage(result.PopupMessage);
            }
            else
            {
                StatusText = $"{_activeSocialFriend.DisplayName} invited to party";
                _ = TriggerPartyRefreshBurstAsync();
            }
        }
        catch (Exception)
        {
            ClearPendingDiscordPartyInvite();
            ShowSocialMessage("Discord party call failed.");
        }
        finally
        {
            _isInvitingPartyFriend = false;
        }
    }

    private async Task TriggerPartyRefreshBurstAsync()
    {
        for (var attempt = 0; attempt < 6; attempt++)
        {
            try
            {
                await Task.Delay(750).ConfigureAwait(true);
                await RefreshPartySnapshotAsync(forceRebuild: attempt == 0).ConfigureAwait(true);
            }
            catch
            {
                break;
            }
        }
    }

    private void SetPendingDiscordPartyInvite(SocialFriend friend)
    {
        ClearPendingDiscordPartyInvite();
        _pendingDiscordPartyInvite = new SocialFriend
        {
            Id = friend.Id,
            DisplayName = friend.DisplayName,
            Source = friend.Source,
            AvatarPathOrUrl = friend.AvatarPathOrUrl,
            IsOnline = friend.IsOnline,
            StatusText = friend.StatusText,
            ActivityText = friend.ActivityText,
            GamerscoreText = friend.GamerscoreText,
            ReputationText = friend.ReputationText,
            ZoneText = friend.ZoneText,
            IdentityDetailText = friend.IdentityDetailText,
            IsPartyHost = false,
            ShowVoiceIndicator = false
        };

        _pendingDiscordPartyInviteTimeoutCts = new CancellationTokenSource();
        _ = ClearPendingDiscordPartyInviteAfterDelayAsync(_pendingDiscordPartyInviteTimeoutCts.Token);
    }

    private void ResolvePendingDiscordPartyInvite(IReadOnlyList<SocialFriend> resolvedMembers)
    {
        if (_pendingDiscordPartyInvite is null)
        {
            return;
        }

        var pendingInviteResolved = resolvedMembers.Any(member =>
            string.Equals(member.Id, _pendingDiscordPartyInvite.Id, StringComparison.OrdinalIgnoreCase)
            || string.Equals(member.DisplayName, _pendingDiscordPartyInvite.DisplayName, StringComparison.OrdinalIgnoreCase));

        if (pendingInviteResolved)
        {
            ClearPendingDiscordPartyInvite();
        }
    }

    private async Task ClearPendingDiscordPartyInviteAfterDelayAsync(CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(TimeSpan.FromSeconds(20), cancellationToken).ConfigureAwait(true);
        }
        catch (TaskCanceledException)
        {
            return;
        }

        if (_pendingDiscordPartyInvite is null || cancellationToken.IsCancellationRequested)
        {
            return;
        }

        ClearPendingDiscordPartyInvite();
        BuildPartyRows();
    }

    private void ClearPendingDiscordPartyInvite()
    {
        _pendingDiscordPartyInviteTimeoutCts?.Cancel();
        _pendingDiscordPartyInviteTimeoutCts?.Dispose();
        _pendingDiscordPartyInviteTimeoutCts = null;
        _pendingDiscordPartyInvite = null;
    }

    private void AddToPartyRoster(SocialFriend friend)
    {
        if (_partyRoster.Any(existing => string.Equals(existing.Id, friend.Id, StringComparison.OrdinalIgnoreCase)))
        {
            return;
        }

        _partyRoster.Add(new SocialFriend
        {
            Id = friend.Id,
            DisplayName = friend.DisplayName,
            Source = friend.Source,
            AvatarPathOrUrl = friend.AvatarPathOrUrl,
            IsOnline = friend.IsOnline,
            StatusText = friend.StatusText,
            ActivityText = string.IsNullOrWhiteSpace(friend.ActivityText) ? "Joined Party" : friend.ActivityText,
            GamerscoreText = friend.GamerscoreText,
            ReputationText = friend.ReputationText,
            ZoneText = friend.ZoneText,
            IdentityDetailText = friend.IdentityDetailText,
            IsPartyHost = false,
            ShowVoiceIndicator = friend.ShowVoiceIndicator
        });
    }

    private void ShowSocialMessage(string message)
    {
        SocialMessageText = message;
        IsSocialMessageOpen = true;
        StatusText = string.Empty;
    }

    private void DismissSocialMessage()
    {
        SocialMessageText = string.Empty;
        IsSocialMessageOpen = false;
    }

    private FriendProfile BuildFriendProfileSnapshot(SocialFriend friend)
        => friend.Source == SocialFriendSource.Local
            ? NormalizeOfflineFriend(LocalSocialIntegrationService.MapToLocalFriend(friend))
            : new FriendProfile
            {
                Gamertag = friend.DisplayName,
                RealName = SocialIntegrationManager.GetSourceLabel(friend),
                GamerPicturePath = friend.AvatarPathOrUrl,
                Gamerscore = ParseGamerscore(friend.GamerscoreText),
                Reputation = NormalizeReputation(friend.ReputationText),
                Zone = string.IsNullOrWhiteSpace(friend.ZoneText) ? "Party" : friend.ZoneText,
                Status = SocialIntegrationManager.GetFriendActivityLabel(friend),
                Country = SocialIntegrationManager.GetSourceLabel(friend)
            };

    private async Task LoadFriendsAsync(bool showPopup)
    {
        _friends.Clear();
        var loadedFriends = (await _friendsService.LoadAsync().ConfigureAwait(true)).Select(NormalizeOfflineFriend).ToList();
        var avatarsUpdated = false;
        foreach (var friend in loadedFriends)
        {
            var assignedAvatar = ProfileImagePool.ResolveAssignedAvatarPath(friend.GamerPicturePath);
            if (!string.Equals(friend.GamerPicturePath, assignedAvatar, StringComparison.OrdinalIgnoreCase))
            {
                friend.GamerPicturePath = assignedAvatar;
                avatarsUpdated = true;
            }
        }

        _friends.AddRange(loadedFriends);
        SortFriendsByStatus();
        if (avatarsUpdated)
        {
            QueueFriendsSave();
        }

        var result = await _socialIntegrationManager
            .LoadFriendsAsync(_dashboard.Settings.SocialIntegrationMode, _dashboard.Settings.DiscordConnectionState)
            .ConfigureAwait(true);

        _socialFriends.Clear();
        _socialFriends.AddRange(SortSocialFriends(result.Friends));
        if (showPopup && !string.IsNullOrWhiteSpace(result.PopupMessage))
        {
            ShowSocialMessage(result.PopupMessage);
        }
        else
        {
            DismissSocialMessage();
        }

        OnPropertyChanged(nameof(FriendsHeaderText));
        OnPropertyChanged(nameof(FriendsTotalCountText));
        OnPropertyChanged(nameof(FriendsSelectionCountText));
        OnPropertyChanged(nameof(PartyFriendCountText));
    }

    private void QueueFriendsSave()
        => _ = SaveFriendsAsync();

    private async Task SaveFriendsAsync()
    {
        var snapshot = _friends.Select(NormalizeOfflineFriend).ToList();

        await _friendsSaveLock.WaitAsync().ConfigureAwait(false);
        try
        {
            await _friendsService.SaveAsync(snapshot).ConfigureAwait(false);
        }
        catch
        {
            // Keep the Guide responsive even if the local friends file cannot be written.
        }
        finally
        {
            _friendsSaveLock.Release();
        }
    }

    private void SortFriendsByStatus()
    {
        var sorted = SortFriends(_friends).ToList();
        _friends.Clear();
        _friends.AddRange(sorted);
    }

    private static IEnumerable<SocialFriend> SortSocialFriends(IEnumerable<SocialFriend> friends)
        => friends
            .OrderBy(friend => friend.IsOnline ? 0 : 1)
            .ThenBy(friend => friend.DisplayName, StringComparer.CurrentCultureIgnoreCase)
            .ToList();

    private static int CompareSocialFriends(SocialFriend left, SocialFriend right)
    {
        var onlineComparison = (left.IsOnline ? 0 : 1).CompareTo(right.IsOnline ? 0 : 1);
        if (onlineComparison != 0)
        {
            return onlineComparison;
        }

        return StringComparer.CurrentCultureIgnoreCase.Compare(left.DisplayName, right.DisplayName);
    }

    private static IEnumerable<FriendProfile> SortFriends(IEnumerable<FriendProfile> friends)
        => friends
            .OrderBy(friend => GetStatusSortOrder(friend.Status))
            .ThenBy(friend => friend.Gamertag, StringComparer.CurrentCultureIgnoreCase);

    private static int GetStatusSortOrder(string status)
    {
        if (status.Contains("Online", StringComparison.OrdinalIgnoreCase))
        {
            return 0;
        }

        if (status.Contains("Away", StringComparison.OrdinalIgnoreCase))
        {
            return 1;
        }

        if (status.Contains("Last online", StringComparison.OrdinalIgnoreCase))
        {
            return 2;
        }

        return 3;
    }

    private static bool IsOnlineStatus(string status)
        => status.Contains("Away", StringComparison.OrdinalIgnoreCase)
           || (status.Contains("Online", StringComparison.OrdinalIgnoreCase)
               && !status.Contains("Last online", StringComparison.OrdinalIgnoreCase));

    private FriendProfile CreateOfflineFriend(string gamertag)
    {
        var seed = gamertag.ToLowerInvariant().Aggregate(17, (current, character) => unchecked(current * 31 + character));
        var random = new Random(seed);
        var statuses = new[]
        {
            "Offline",
            "Away",
            $"Last online {random.Next(8, 46)} minutes ago",
            $"Last online {random.Next(1, 12)} hours ago"
        };
        var zones = new[] { "Recreation", "Family", "Pro", "Underground" };
        var countries = new[] { "United States", "Canada", "United Kingdom", "Offline" };
        var repStars = BuildReputation(random.Next(3, 6));

        return new FriendProfile
        {
            Gamertag = gamertag,
            RealName = $"{gamertag} profile",
            GamerPicturePath = GamerPicturePath,
            Gamerscore = random.Next(2500, 95000),
            Reputation = repStars,
            Zone = zones[random.Next(zones.Length)],
            Status = NormalizeOfflineStatus(statuses[random.Next(statuses.Length)]),
            Country = countries[random.Next(countries.Length)]
        };
    }

    private static int ParseGamerscore(string gamerscoreText)
    {
        if (string.IsNullOrWhiteSpace(gamerscoreText))
        {
            return 0;
        }

        var digits = new string(gamerscoreText.Where(char.IsDigit).ToArray());
        return int.TryParse(digits, out var gamerscore) ? gamerscore : 0;
    }

    private static FriendProfile CloneFriend(FriendProfile friend)
        => new()
        {
            Gamertag = friend.Gamertag,
            RealName = friend.RealName,
            GamerPicturePath = friend.GamerPicturePath,
            Gamerscore = friend.Gamerscore,
            Reputation = friend.Reputation,
            Zone = friend.Zone,
            Status = friend.Status,
            Country = friend.Country
        };

    private static FriendProfile NormalizeOfflineFriend(FriendProfile friend)
    {
        var normalized = CloneFriend(friend);
        normalized.Reputation = NormalizeReputation(normalized.Reputation);
        normalized.Status = NormalizeOfflineStatus(normalized.Status);
        normalized.Zone = string.IsNullOrWhiteSpace(normalized.Zone) ? "Recreation" : normalized.Zone;
        normalized.Country = NormalizeOfflineCountry(normalized.Country);
        return normalized;
    }

    private static string NormalizeOfflineStatus(string? status)
    {
        if (string.IsNullOrWhiteSpace(status))
        {
            return "Offline";
        }

        if (status.Contains("Last online", StringComparison.OrdinalIgnoreCase)
            || status.Contains("Away", StringComparison.OrdinalIgnoreCase)
            || status.Contains("Offline", StringComparison.OrdinalIgnoreCase))
        {
            return status;
        }

        return "Offline";
    }

    private static string NormalizeReputation(string? reputation)
    {
        if (string.IsNullOrWhiteSpace(reputation))
        {
            return BuildReputation(5);
        }

        var filledStars = reputation.Count(character => character is '★' or '*');
        if (filledStars == 0 && reputation.Contains("â", StringComparison.Ordinal))
        {
            filledStars = 5;
        }

        return BuildReputation(filledStars == 0 ? 5 : Math.Clamp(filledStars, 1, 5));
    }

    private static string BuildReputation(int filledStars)
        => new string('★', Math.Clamp(filledStars, 0, 5))
           + new string('☆', Math.Clamp(5 - filledStars, 0, 5));

    private static string NormalizeOfflineCountry(string? country)
    {
        if (string.IsNullOrWhiteSpace(country))
        {
            return "Offline";
        }

        if (country.StartsWith("Offline", StringComparison.OrdinalIgnoreCase))
        {
            return "Offline";
        }

        return country;
    }

    private IEnumerable<string> GetActiveSearchKeyboardLabels()
        => _searchKeyboardLayout switch
        {
            SearchKeyboardLayout.Symbols => _symbolKeyboardLabels,
            SearchKeyboardLayout.Accents => _accentKeyboardLabels,
            _ => _defaultKeyboardLabels
        };

    private void SetSearchKeyboardLayout(SearchKeyboardLayout layout)
    {
        if (_searchKeyboardLayout == layout)
        {
            return;
        }

        _searchKeyboardLayout = layout;
        BuildSearchKeys();
    }

    private void SetScreen(GuideScreen screen)
    {
        _screen = screen;
        OnPropertyChanged(nameof(IsMainGuideScreen));
        OnPropertyChanged(nameof(IsFriendsListScreen));
        OnPropertyChanged(nameof(IsPartyScreen));
        OnPropertyChanged(nameof(IsFriendSearchScreen));
        OnPropertyChanged(nameof(IsFriendProfileScreen));
        OnPropertyChanged(nameof(IsFriendOverlayScreen));
        OnPropertyChanged(nameof(IsMediaTab));
        OnPropertyChanged(nameof(IsMediaSongRowFocused));
        OnPropertyChanged(nameof(ShowStatusText));
        OnPropertyChanged(nameof(FooterPromptTop));
        OnPropertyChanged(nameof(FooterXActionText));
        OnPropertyChanged(nameof(FooterYActionText));
        OnPropertyChanged(nameof(ShowFooterXAction));
        OnPropertyChanged(nameof(ShowFooterYAction));
        OnPropertyChanged(nameof(FriendsTotalCountText));
        OnPropertyChanged(nameof(FriendsMessageCountText));
        OnPropertyChanged(nameof(FriendsGameInviteCountText));
        OnPropertyChanged(nameof(FriendsSelectionCountText));
        OnPropertyChanged(nameof(PartyHeaderText));
        OnPropertyChanged(nameof(PartyFriendCountText));
        OnPropertyChanged(nameof(PartyMessageCountText));
        OnPropertyChanged(nameof(PartyGameCountText));
        OnPropertyChanged(nameof(PartyMemberCount));

        if (_screen != GuideScreen.MainMenu)
        {
            CloseMediaSubmenu();
        }

        UpdatePartyRefreshState();
    }

    private static void DoNothing()
    {
    }

    private bool CanUseFriendsOverlay() => true;

    private void OpenMusic()
        => OpenMusic(transparent: false);

    private void OpenTransparentMusic()
        => OpenMusic(transparent: true);

    private void OpenMusic(bool transparent)
    {
        PrepareGuideReturnToDashboard();
        CloseGuide();
        if (_dashboard.OpenMusicPlayerCommand.CanExecute(null))
        {
            _dashboard.OpenMusicPlayerCommand.Execute(transparent);
        }

        RestoreMainWindow();
    }

    private void OpenSettings()
    {
        PrepareGuideReturnToDashboard();
        CloseGuide();
        if (_dashboard.OpenLauncherSettingsCommand.CanExecute(null))
        {
            _dashboard.OpenLauncherSettingsCommand.Execute(null);
        }

        RestoreMainWindow();
    }

    private void OpenProfile()
    {
        PrepareGuideReturnToDashboard();
        CloseGuide();
        if (_dashboard.OpenProfileEditorCommand.CanExecute(null))
        {
            _dashboard.OpenProfileEditorCommand.Execute(null);
        }

        RestoreMainWindow();
    }

    private void RestoreMainWindow()
    {
        _mainWindow.Show();
        if (_mainWindow.WindowState == WindowState.Minimized)
        {
            _mainWindow.WindowState = _dashboard.Settings.StartFullscreen ? WindowState.Maximized : WindowState.Normal;
        }

        _mainWindow.Activate();
    }

    private void PrepareGuideReturnToDashboard()
    {
        if (_mainWindow is MainWindow mainWindow)
        {
            mainWindow.PrepareGuideReturnToDashboard();
        }
    }
}
