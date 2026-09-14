using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
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
		MusicPicker,
		FriendsList,
		Party,
		FriendSearch,
		FriendProfile,
		Achievements
	}

	private enum SearchKeyboardLayout
	{
		Default,
		Symbols,
		Accents
	}

	private enum FriendSearchMode
	{
		AddFriend
	}

	private const int AchievementGridColumns = 7;

	private readonly DashboardViewModel _dashboard;

	private readonly Window _mainWindow;

	private readonly IAudioService _audioService;

	private readonly IFriendsService _friendsService;

	private readonly SocialIntegrationManager _socialIntegrationManager;

	private readonly ISteamCommunityService _steamCommunityService;

	private readonly DispatcherTimer _clockTimer;

	private readonly DispatcherTimer _partyRefreshTimer;

	private readonly SemaphoreSlim _friendsSaveLock = new SemaphoreSlim(1, 1);

	private readonly SemaphoreSlim _partyRefreshLock = new SemaphoreSlim(1, 1);

	private readonly List<FriendProfile> _friends = new List<FriendProfile>();

	private readonly List<SocialFriend> _socialFriends = new List<SocialFriend>();

	private readonly List<GuidePartyMember> _partyMembers = new List<GuidePartyMember>();

	private readonly List<SocialFriend> _partyRoster = new List<SocialFriend>();

	private bool _isInvitingPartyFriend;

	private string? _lastPartyInviteFriendId;

	private DateTimeOffset _lastPartyInviteAt = DateTimeOffset.MinValue;

	private int _dashUnreadMessageCount;

	private int _bladeSwitchSoundIndex;

	private int _keyboardTypingSoundIndex;

	private readonly string[] _tabNames = new string[5] { "Games & Apps", "Profile", "Xbox Home", "Media", "Settings" };

	private readonly string[] _defaultKeyboardLabels = new string[40]
	{
		"1", "2", "3", "4", "5", "6", "7", "8", "9", "0",
		"q", "w", "e", "r", "t", "y", "u", "i", "o", "p",
		"a", "s", "d", "f", "g", "h", "j", "k", "l", "-",
		"z", "x", "c", "v", "b", "n", "m", "_", "@", "."
	};

	private readonly string[] _symbolKeyboardLabels = new string[40]
	{
		"!", "\"", "#", "$", "%", "^", "&", "*", "(", ")",
		"[", "]", "{", "}", "<", ">", "/", "\\", "|", "~",
		"+", "=", "_", ";", ":", ",", ".", "?", "@", "-",
		"`", "'", "€", "£", "¥", "¢", "§", "°", "¬", "..."
	};

	private readonly string[] _accentKeyboardLabels = new string[40]
	{
		"á", "à", "â", "ä", "ã", "å", "æ", "ç", "é", "è",
		"ê", "ë", "í", "ì", "î", "ï", "ñ", "ó", "ò", "ô",
		"ö", "õ", "ø", "œ", "ú", "ù", "û", "ü", "ý", "ÿ",
		"Á", "É", "Í", "Ó", "Ú", "Ñ", "Ç", "¿", "¡", "."
	};

	private readonly string[] _searchActionLabels = new string[4] { "Caps", "Back", "Space", "Done" };

	private int _selectedIndex;

	private int _selectedMediaControlIndex = 2;

	private int _selectedMediaSubmenuIndex;

	private int _selectedTabIndex = 2;

	private int _selectedFriendListIndex = -1;

	private bool _isRebuildingFriendsListItems;

	private string _friendsListItemsSignature = string.Empty;

	private int _selectedPartyRowIndex;

	private int _selectedGuideMusicTrackIndex;

	private int _selectedSearchKeyIndex;

	private int _selectedFriendProfileActionIndex;

	private int _selectedAchievementGameIndex;

	private int _selectedAchievementIndex;

	private int _friendSearchCursorIndex;

	private string _clockText = string.Empty;

	private string _statusText = string.Empty;

	private string _friendSearchQuery = string.Empty;

	private bool _isMediaTransportFocused;

	private bool _isMediaSubmenuOpen;

	private bool _isSearchCapsEnabled;

	private bool _isSocialMessageOpen;

	private bool _returnToSearchOnProfileBack;

	private bool _restoreMediaSubmenuAfterMusicPicker;

	private bool _isRefreshingFriends;

	private bool _isUsingDiscordPartyData;

	private bool _isOpeningCommunityOverlayFromMainGuide;

	private bool _isOpeningCommunityOverlay;

	private bool _isOpeningAchievementsOverlayFromMainGuide;

	private bool _isOpeningAchievementsOverlay;

	private bool _isOpeningGuideMusicPickerFromMainGuide;

	private bool _isOpeningGuideMusicPicker;

	private bool _runningGameForceClosePending;

	private string _partyStatusMessage = string.Empty;

	private GuideScreen _friendSearchReturnScreen = GuideScreen.FriendsList;

	private GuideScreen _musicPickerReturnScreen;

	private GuideScreen _profileReturnScreen = GuideScreen.FriendsList;

	private MediaFocusArea _mediaFocusArea;

	private MediaFocusArea _musicPickerReturnMediaFocus = MediaFocusArea.SongRow;

	private GuideScreen _screen;

	private SearchKeyboardLayout _searchKeyboardLayout;

	private FriendSearchMode _friendSearchMode;

	private int _tabTransitionDirection;

	private FriendProfile? _activeFriendProfile;

	private SocialFriend? _activeSocialFriend;

	private string _socialMessageText = string.Empty;

	private DateTimeOffset _runningGameForceCloseRequestedAt = DateTimeOffset.MinValue;

	public ObservableCollection<GuideMenuItem> Items { get; }
	public ObservableCollection<DashPartyTextMessage> BladesMessages { get; } = new();

	public ObservableCollection<GuideMediaControlItem> MediaControls { get; }

	public ObservableCollection<GuideMenuItem> MediaSubmenuItems { get; }

	public ObservableCollection<GuideFriendListItem> FriendsListItems { get; }

	public ObservableCollection<GuidePartyRowItem> PartyRows { get; }

	public ObservableCollection<GuideKeyboardKeyItem> SearchKeys { get; }

	public GuideKeyboardKeyItem SearchCursorLeftKey { get; }

	public GuideKeyboardKeyItem SearchCursorRightKey { get; }

	public GuideKeyboardKeyItem SearchSymbolsKey { get; }

	public GuideKeyboardKeyItem SearchAccentsKey { get; }

	public ObservableCollection<GuideMenuItem> FriendProfileActions { get; }

	public ObservableCollection<GuideAchievementGameItem> AchievementGameItems { get; }

	public ObservableCollection<GuideAchievementItem> AchievementItems { get; }

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
		get
		{
			return _selectedIndex;
		}
		set
		{
			if (SetProperty(ref _selectedIndex, Math.Clamp(value, 0, Math.Max(0, Items.Count - 1)), "SelectedIndex"))
			{
				OnPropertyChanged("GuideMenuVisualSelectedIndex");
			}
		}
	}

	public int GuideMenuVisualSelectedIndex
	{
		get
		{
			if (!IsGuideMenuSelectionActive)
			{
				return -1;
			}
			return SelectedIndex;
		}
		set
		{
			if (value >= 0)
			{
				SelectedIndex = value;
			}
		}
	}

	public int SelectedFriendListIndex
	{
		get
		{
			return _selectedFriendListIndex;
		}
		set
		{
			if (value >= 0 || (!_isRebuildingFriendsListItems && (FriendsListItems.Count <= 0 || _selectedFriendListIndex < 0)))
			{
				int selectedIndex = ((FriendsListItems.Count == 0) ? (-1) : Math.Clamp(value, 0, FriendsListItems.Count - 1));
				if (SetProperty(ref _selectedFriendListIndex, selectedIndex, "SelectedFriendListIndex"))
				{
					OnPropertyChanged("FriendsSelectionCountText");
				}
			}
		}
	}

	public int SelectedPartyRowIndex
	{
		get
		{
			return _selectedPartyRowIndex;
		}
		set
		{
			SetProperty(ref _selectedPartyRowIndex, Math.Clamp(value, 0, Math.Max(0, PartyRows.Count - 1)), "SelectedPartyRowIndex");
		}
	}

	public int SelectedGuideMusicTrackIndex
	{
		get
		{
			return _selectedGuideMusicTrackIndex;
		}
		set
		{
			SetProperty(ref _selectedGuideMusicTrackIndex, Math.Clamp(value, 0, Math.Max(0, GuideMusicTracks.Count - 1)), "SelectedGuideMusicTrackIndex");
		}
	}

	public int SelectedSearchKeyIndex
	{
		get
		{
			return _selectedSearchKeyIndex;
		}
		set
		{
			if (SetProperty(ref _selectedSearchKeyIndex, Math.Clamp(value, 0, Math.Max(0, SearchKeys.Count - 1)), "SelectedSearchKeyIndex"))
			{
				RefreshSearchKeySelection();
			}
		}
	}

	public int SelectedFriendProfileActionIndex
	{
		get
		{
			return _selectedFriendProfileActionIndex;
		}
		set
		{
			SetProperty(ref _selectedFriendProfileActionIndex, Math.Clamp(value, 0, Math.Max(0, FriendProfileActions.Count - 1)), "SelectedFriendProfileActionIndex");
		}
	}

	public int SelectedAchievementGameIndex
	{
		get
		{
			return _selectedAchievementGameIndex;
		}
		set
		{
			if (SetProperty(ref _selectedAchievementGameIndex, Math.Clamp(value, 0, Math.Max(0, AchievementGameItems.Count - 1)), "SelectedAchievementGameIndex"))
			{
				RefreshAchievementGameSelection();
				OnPropertyChanged("AchievementsGameTitle");
				OnPropertyChanged("AchievementsCountText");
				OnPropertyChanged("AchievementsUnlockedText");
			}
		}
	}

	public int SelectedAchievementIndex
	{
		get
		{
			return _selectedAchievementIndex;
		}
		set
		{
			if (SetProperty(ref _selectedAchievementIndex, Math.Clamp(value, 0, Math.Max(0, AchievementItems.Count - 1)), "SelectedAchievementIndex"))
			{
				RefreshAchievementSelection();
				NotifySelectedAchievementChanged();
			}
		}
	}

	public string Gamertag => _dashboard.Profile.Gamertag;

	public string GamerPicturePath => _dashboard.Profile.GamerPicturePath;

	public string ClockText
	{
		get
		{
			return _clockText;
		}
		private set
		{
			SetProperty(ref _clockText, value, "ClockText");
		}
	}

	public string StatusText
	{
		get
		{
			return _statusText;
		}
		private set
		{
			if (SetProperty(ref _statusText, value, "StatusText"))
			{
				OnPropertyChanged("ShowStatusText");
			}
		}
	}

	public string CurrentTabTitle => GetTabText(_selectedTabIndex);

	public int TabTransitionDirection
	{
		get
		{
			return _tabTransitionDirection;
		}
		private set
		{
			SetProperty(ref _tabTransitionDirection, value, "TabTransitionDirection");
		}
	}

	public string LeftOuterTabText => GetTabText(_selectedTabIndex - 2);

	public string LeftInnerTabText => GetTabText(_selectedTabIndex - 1);

	public string RightInnerTabText => GetTabText(_selectedTabIndex + 1);

	public string RightOuterTabText => GetTabText(_selectedTabIndex + 2);

	public DashboardViewModel Dashboard => _dashboard;

	public bool IsHomeTab => _selectedTabIndex == 2;

	public bool IsMediaTab
	{
		get
		{
			if (_selectedTabIndex == 3)
			{
				return IsMainGuideScreen;
			}
			return false;
		}
	}

	public bool IsGuideBladeScreen
	{
		get
		{
			if ((uint)_screen <= 1u)
			{
				return true;
			}
			return false;
		}
	}

	public bool IsMainGuideScreen => _screen == GuideScreen.MainMenu;

	public bool IsGuideMusicPickerScreen => _screen == GuideScreen.MusicPicker;

	public bool IsFriendsListScreen => _screen == GuideScreen.FriendsList;

	public bool IsPartyScreen => _screen == GuideScreen.Party;

	public bool IsFriendSearchScreen => _screen == GuideScreen.FriendSearch;

	public bool IsFriendProfileScreen => _screen == GuideScreen.FriendProfile;

	public bool IsAchievementsScreen => _screen == GuideScreen.Achievements;

	public bool IsFriendOverlayScreen
	{
		get
		{
			if ((uint)(_screen - 2) <= 4u)
			{
				return true;
			}
			return false;
		}
	}

	public bool IsMediaSongRowFocused
	{
		get
		{
			if (IsMediaTab)
			{
				return _mediaFocusArea == MediaFocusArea.SongRow;
			}
			return false;
		}
	}

	public bool IsGuideMenuSelectionActive
	{
		get
		{
			if (IsMediaTab)
			{
				return _mediaFocusArea == MediaFocusArea.List;
			}
			return true;
		}
	}

	public bool IsMediaSubmenuOpen
	{
		get
		{
			return _isMediaSubmenuOpen;
		}
		private set
		{
			SetProperty(ref _isMediaSubmenuOpen, value, "IsMediaSubmenuOpen");
		}
	}

	public bool IsMediaTransportFocused
	{
		get
		{
			return _isMediaTransportFocused;
		}
		private set
		{
			if (SetProperty(ref _isMediaTransportFocused, value, "IsMediaTransportFocused"))
			{
				RefreshMediaControlSelection();
			}
		}
	}

	public int SelectedMediaControlIndex
	{
		get
		{
			return _selectedMediaControlIndex;
		}
		private set
		{
			if (SetProperty(ref _selectedMediaControlIndex, Math.Clamp(value, 0, Math.Max(0, MediaControls.Count - 1)), "SelectedMediaControlIndex"))
			{
				RefreshMediaControlSelection();
			}
		}
	}

	public int SelectedMediaSubmenuIndex
	{
		get
		{
			return _selectedMediaSubmenuIndex;
		}
		set
		{
			SetProperty(ref _selectedMediaSubmenuIndex, Math.Clamp(value, 0, Math.Max(0, MediaSubmenuItems.Count - 1)), "SelectedMediaSubmenuIndex");
		}
	}

	public string GuideMediaPlaybackLabel
	{
		get
		{
			if (!_dashboard.IsMusicPlaying || _dashboard.CurrentMusicTrack == null)
			{
				return "Select Music";
			}
			return _dashboard.CurrentMusicTitle;
		}
	}

	public ObservableCollection<MusicTrackViewModel> GuideMusicTracks => _dashboard.MusicTracks;

	public string GuideMusicPickerHeaderText => "Select Music";

	public string GuideMusicPickerCurrentTitle => _dashboard.CurrentMusicTitle;

	public string GuideMusicPickerCountText
	{
		get
		{
			if (GuideMusicTracks.Count == 0)
			{
				return "0 of 0";
			}
			int value = ((_dashboard.CurrentMusicTrack == null) ? SelectedGuideMusicTrackIndex : GuideMusicTracks.IndexOf(_dashboard.CurrentMusicTrack));
			value = Math.Clamp(value, 0, GuideMusicTracks.Count - 1);
			return $"{value + 1} of {GuideMusicTracks.Count}";
		}
	}

	public string FriendSearchQuery
	{
		get
		{
			return _friendSearchQuery;
		}
		set
		{
			if (SetProperty(ref _friendSearchQuery, value, "FriendSearchQuery"))
			{
				OnPropertyChanged("FriendSearchDisplayText");
			}
		}
	}

	public string FriendSearchDisplayText => FriendSearchQuery.Insert(Math.Clamp(_friendSearchCursorIndex, 0, FriendSearchQuery.Length), "|");

	public string FriendSearchHeaderText => "Add Recipient";

	public string FriendSearchInstructionText => "Enter a recipient's gamertag.";

	public string SearchSymbolsButtonText
	{
		get
		{
			if (_searchKeyboardLayout != SearchKeyboardLayout.Symbols)
			{
				return "Symbols";
			}
			return "ABC";
		}
	}

	public string SearchAccentsButtonText
	{
		get
		{
			if (_searchKeyboardLayout != SearchKeyboardLayout.Accents)
			{
				return "Accents";
			}
			return "ABC";
		}
	}

	public string FriendsHeaderText => $"Friends ({_socialFriends.Count((SocialFriend friend) => friend.IsOnline)} Online)";

	public string FriendsCommunityHeaderText => "Community";

	public string FriendsTotalCountText => _socialFriends.Count.ToString();

	public string FriendsMessageCountText => _dashUnreadMessageCount.ToString();

	public string FriendsGameInviteCountText => "0";

	public string FriendsFooterText => "Sorted by online status";

	public string AchievementsHeaderText => "Achievements";

	public bool IsAchievementGameList
	{
		get
		{
			if (IsAchievementsScreen)
			{
				return AchievementItems.Count == 0;
			}
			return false;
		}
	}

	public bool IsAchievementDetail
	{
		get
		{
			if (IsAchievementsScreen)
			{
				return AchievementItems.Count > 0;
			}
			return false;
		}
	}

	public string AchievementsGameTitle
	{
		get
		{
			if (IsAchievementDetail && SelectedAchievementGameIndex >= 0 && SelectedAchievementGameIndex < AchievementGameItems.Count)
			{
				return AchievementGameItems[SelectedAchievementGameIndex].Title;
			}
			if (AchievementGameItems.Count != 0)
			{
				return "Steam Games";
			}
			return "No Steam games found";
		}
	}

	public string AchievementsCountText
	{
		get
		{
			if (AchievementItems.Count == 0)
			{
				if (AchievementGameItems.Count != 0)
				{
					return $"{Math.Clamp(SelectedAchievementGameIndex + 1, 1, AchievementGameItems.Count)} of {AchievementGameItems.Count}";
				}
				return "0 of 0";
			}
			return $"{Math.Clamp(SelectedAchievementIndex + 1, 1, AchievementItems.Count)} of {AchievementItems.Count}";
		}
	}

	public string AchievementsUnlockedText
	{
		get
		{
			if (AchievementItems.Count == 0)
			{
				if (AchievementGameItems.Count != 0)
				{
					return "Select a game to view achievements.";
				}
				return "Scan Steam games in Settings to view achievements.";
			}
			if (SelectedAchievementIndex < 0 || SelectedAchievementIndex >= AchievementItems.Count)
			{
				int value = AchievementItems.Count((GuideAchievementItem item) => item.Achieved);
				return $"{value} of {AchievementItems.Count} unlocked";
			}
			string value2 = (AchievementItems[SelectedAchievementIndex].Achieved ? "unlocked" : "locked");
			return $"{SelectedAchievementIndex + 1} of {AchievementItems.Count} {value2}";
		}
	}

	public string SelectedAchievementTitle
	{
		get
		{
			if (SelectedAchievementIndex < 0 || SelectedAchievementIndex >= AchievementItems.Count)
			{
				return string.Empty;
			}
			return AchievementItems[SelectedAchievementIndex].Title;
		}
	}

	public string SelectedAchievementDescription
	{
		get
		{
			if (SelectedAchievementIndex < 0 || SelectedAchievementIndex >= AchievementItems.Count)
			{
				return string.Empty;
			}
			return AchievementItems[SelectedAchievementIndex].Description;
		}
	}

	public string SelectedAchievementStatusText
	{
		get
		{
			if (SelectedAchievementIndex < 0 || SelectedAchievementIndex >= AchievementItems.Count)
			{
				return string.Empty;
			}
			return AchievementItems[SelectedAchievementIndex].StatusText;
		}
	}

	public string SelectedAchievementDateText
	{
		get
		{
			if (SelectedAchievementIndex < 0 || SelectedAchievementIndex >= AchievementItems.Count)
			{
				return string.Empty;
			}
			GuideAchievementItem guideAchievementItem = AchievementItems[SelectedAchievementIndex];
			if (!guideAchievementItem.Achieved || guideAchievementItem.UnlockTimeUnix <= 0)
			{
				return string.Empty;
			}
			return DateTimeOffset.FromUnixTimeSeconds(guideAchievementItem.UnlockTimeUnix).LocalDateTime.ToString("M/d/yyyy", CultureInfo.CurrentCulture);
		}
	}

	public string FriendsSelectionCountText
	{
		get
		{
			if (_socialFriends.Count == 0 || SelectedFriendListIndex < 0 || SelectedFriendListIndex >= FriendsListItems.Count)
			{
				return string.Empty;
			}
			if (FriendsListItems[SelectedFriendListIndex].IsAddFriend)
			{
				return string.Empty;
			}
			int num = FriendsListItems.Take(SelectedFriendListIndex + 1).Count((GuideFriendListItem item) => !item.IsAddFriend);
			if (num > 0)
			{
				return $"{num} of {_socialFriends.Count}";
			}
			return string.Empty;
		}
	}

	public string PartyHeaderText => $"Xbox LIVE Party ({PartyMemberCount})";

	public int ActiveTimerCount => (_clockTimer.IsEnabled ? 1 : 0) + (_partyRefreshTimer.IsEnabled ? 1 : 0);

	public string PartyFriendCountText => _socialFriends.Count.ToString();

	public string PartyMessageCountText => _dashUnreadMessageCount.ToString();

	public string PartyGameCountText => "0";

	public int PartyMemberCount => _partyMembers.Count;

	public bool ShowStatusText
	{
		get
		{
			if (!IsFriendOverlayScreen && !_isSocialMessageOpen)
			{
				return !string.IsNullOrWhiteSpace(StatusText);
			}
			return false;
		}
	}

	public double FooterPromptTop => _screen switch
	{
		GuideScreen.FriendsList => 574, 
		GuideScreen.Party => 574, 
		GuideScreen.FriendSearch => 530, 
		GuideScreen.FriendProfile => 562, 
		GuideScreen.Achievements => 574, 
		_ => 646, 
	};

	public string ActiveFriendGamertag => _activeSocialFriend?.DisplayName ?? _activeFriendProfile?.Gamertag ?? string.Empty;

	public string ActiveFriendPicturePath => _activeSocialFriend?.AvatarPathOrUrl ?? _activeFriendProfile?.GamerPicturePath ?? GamerPicturePath;

	public string ActiveFriendGamerscore
	{
		get
		{
			SocialFriend activeSocialFriend = _activeSocialFriend;
			if (activeSocialFriend == null)
			{
				if (_activeFriendProfile != null)
				{
					return $"{_activeFriendProfile.Gamerscore:N0} G";
				}
				return string.Empty;
			}
			return GetSocialFriendGamerscoreText(activeSocialFriend);
		}
	}

	public string ActiveFriendReputation
	{
		get
		{
			SocialFriend activeSocialFriend = _activeSocialFriend;
			if (activeSocialFriend == null)
			{
				return NormalizeReputation(_activeFriendProfile?.Reputation);
			}
			return GetSocialFriendReputationText(activeSocialFriend);
		}
	}

	public string ActiveFriendZone
	{
		get
		{
			SocialFriend activeSocialFriend = _activeSocialFriend;
			object obj;
			if (activeSocialFriend == null)
			{
				obj = _activeFriendProfile?.Zone;
				if (obj == null)
				{
					return string.Empty;
				}
			}
			else
			{
				obj = GetSocialFriendZoneText(activeSocialFriend);
			}
			return (string)obj;
		}
	}

	public string ActiveFriendStatus
	{
		get
		{
			object obj;
			if (_activeSocialFriend == null)
			{
				obj = _activeFriendProfile?.Status;
				if (obj == null)
				{
					return string.Empty;
				}
			}
			else
			{
				obj = GetActiveFriendProfileStatus(_activeSocialFriend);
			}
			return (string)obj;
		}
	}

	public string ActiveFriendGameTitle
	{
		get
		{
			SocialFriend activeSocialFriend = _activeSocialFriend;
			if (activeSocialFriend == null || activeSocialFriend.Source != SocialFriendSource.Steam)
			{
				return string.Empty;
			}
			return _activeSocialFriend.ActivityText;
		}
	}

	public string ActiveFriendGameIconPath => ResolveActiveFriendGameIconPath();

	public string ActiveFriendCountry
	{
		get
		{
			if (_activeSocialFriend != null)
			{
				if (_activeSocialFriend.Source != SocialFriendSource.Local)
				{
					if (!string.IsNullOrWhiteSpace(_activeFriendProfile?.Country))
					{
						return _activeFriendProfile.Country;
					}
					return "Offline";
				}
				return NormalizeOfflineCountry(_activeFriendProfile?.Country);
			}
			return NormalizeOfflineCountry(_activeFriendProfile?.Country);
		}
	}

	public string ActiveFriendSourceLabel
	{
		get
		{
			if (_activeSocialFriend != null)
			{
				return SocialIntegrationManager.GetSourceLabel(_activeSocialFriend);
			}
			return string.Empty;
		}
	}

	public bool IsSocialMessageOpen
	{
		get
		{
			return _isSocialMessageOpen;
		}
		private set
		{
			if (SetProperty(ref _isSocialMessageOpen, value, "IsSocialMessageOpen"))
			{
				OnPropertyChanged("ShowStatusText");
			}
		}
	}

	public string SocialMessageTitle => "Community";

	public string SocialMessageText
	{
		get
		{
			return _socialMessageText;
		}
		private set
		{
			SetProperty(ref _socialMessageText, value, "SocialMessageText");
		}
	}

	public GuideKeyboardKeyItem? SearchCapsKey => SearchKeys.ElementAtOrDefault(40);

	public GuideKeyboardKeyItem? SearchBackspaceKey => SearchKeys.ElementAtOrDefault(41);

	public GuideKeyboardKeyItem? SearchSpaceKey => SearchKeys.ElementAtOrDefault(42);

	public GuideKeyboardKeyItem? SearchDoneKey => SearchKeys.ElementAtOrDefault(43);

	public string FooterXActionText
	{
		get
		{
			switch (_screen)
			{
			case GuideScreen.FriendsList:
				return string.Empty;
			case GuideScreen.Party:
				return "Leave Party";
			case GuideScreen.FriendSearch:
				return "Backspace";
			case GuideScreen.FriendProfile:
				return string.Empty;
			case GuideScreen.Achievements:
				if (IsAchievementDetail)
				{
					return "Share Achievement";
				}
				break;
			}
			return _dashboard.RunningGameFooterActionText;
		}
	}

	public string FooterYActionText => _screen switch
	{
		GuideScreen.FriendsList => "Change Sort", 
		GuideScreen.Party => string.Empty, 
		GuideScreen.FriendSearch => "Space", 
		GuideScreen.FriendProfile => string.Empty, 
		_ => "Minimize Dashboard", 
	};

	public bool ShowFooterXAction
	{
		get
		{
			GuideScreen screen = _screen;
			if (screen != GuideScreen.MainMenu && (uint)(screen - 3) > 1u)
			{
				if (_screen == GuideScreen.Achievements)
				{
					return IsAchievementDetail;
				}
				return false;
			}
			return true;
		}
	}

	public bool ShowFooterYAction
	{
		get
		{
			GuideScreen screen = _screen;
			if (screen != GuideScreen.FriendProfile)
			{
				return screen != GuideScreen.Party;
			}
			return false;
		}
	}

	public GuideViewModel(DashboardViewModel dashboard, Window mainWindow, Action closeGuide, IAudioService audioService, IFriendsService friendsService, SocialIntegrationManager socialIntegrationManager, ISteamCommunityService steamCommunityService)
	{
		//IL_070d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0712: Unknown result type (might be due to invalid IL or missing references)
		//IL_072b: Expected O, but got Unknown
		//IL_0743: Unknown result type (might be due to invalid IL or missing references)
		//IL_0748: Unknown result type (might be due to invalid IL or missing references)
		//IL_0761: Expected O, but got Unknown
		_dashboard = dashboard;
		_mainWindow = mainWindow;
		_audioService = audioService;
		_friendsService = friendsService;
		_socialIntegrationManager = socialIntegrationManager;
		_steamCommunityService = steamCommunityService;
		CloseGuide = closeGuide;
		Items = new ObservableCollection<GuideMenuItem>();
		MediaControls = new ObservableCollection<GuideMediaControlItem>();
		MediaSubmenuItems = new ObservableCollection<GuideMenuItem>();
		FriendsListItems = new ObservableCollection<GuideFriendListItem>();
		PartyRows = new ObservableCollection<GuidePartyRowItem>();
		SearchKeys = new ObservableCollection<GuideKeyboardKeyItem>();
		SearchCursorLeftKey = new GuideKeyboardKeyItem("Left", delegate
		{
			MoveFriendSearchCursor(-1, playSound: false);
		});
		SearchCursorRightKey = new GuideKeyboardKeyItem("Right", delegate
		{
			MoveFriendSearchCursor(1, playSound: false);
		});
		SearchSymbolsKey = new GuideKeyboardKeyItem("Symbols", SwitchToSymbolKeyboard);
		SearchAccentsKey = new GuideKeyboardKeyItem("Accents", SwitchToAccentKeyboard);
		FriendProfileActions = new ObservableCollection<GuideMenuItem>();
		AchievementGameItems = new ObservableCollection<GuideAchievementGameItem>();
		AchievementItems = new ObservableCollection<GuideAchievementItem>();
		ActivateMediaControlCommand = new RelayCommand(delegate(object? parameter)
		{
			if (parameter is GuideMediaControlItem item)
			{
				SelectAndActivateMediaControl(item);
			}
		});
		OpenGuideMusicMenuCommand = new RelayCommand(OpenGuideMusicPicker);
		ActivateMediaSubmenuCommand = new RelayCommand(delegate(object? parameter)
		{
			if (parameter is GuideMenuItem item)
			{
				SelectAndActivateMediaSubmenu(item);
			}
		});
		ActivateSearchKeyCommand = new RelayCommand(delegate(object? parameter)
		{
			if (parameter is GuideKeyboardKeyItem item)
			{
				SelectAndActivateSearchKey(item);
			}
		});
		ActivateFriendProfileActionCommand = new RelayCommand(delegate(object? parameter)
		{
			if (parameter is GuideMenuItem item)
			{
				SelectAndActivateFriendProfileAction(item);
			}
		});
		_dashboard.PropertyChanged += Dashboard_OnPropertyChanged;
		_clockTimer = new DispatcherTimer
		{
			Interval = TimeSpan.FromSeconds(15.0)
		};
		_clockTimer.Tick += delegate
		{
			UpdateClock();
		};
		_partyRefreshTimer = new DispatcherTimer
		{
			Interval = TimeSpan.FromSeconds(1.0)
		};
		_partyRefreshTimer.Tick += async delegate
		{
			await RefreshPartySnapshotAsync().ConfigureAwait(continueOnCapturedContext: true);
		};
		BuildSearchKeys();
		BuildItems();
		BuildMediaSubmenu();
		UpdateClock();
		RefreshAsync(showPopup: false);
	}

	public void Start(bool resetToHome = true)
	{
		if (resetToHome)
		{
			ResetToXboxHome();
		}
		_clockTimer.Start();
	}

	public void ResetToXboxHome()
	{
		DismissSocialMessage();
		SetScreen(GuideScreen.MainMenu);
		_selectedTabIndex = 2;
		BuildItems();
		SelectedIndex = 0;
		CloseMediaSubmenu();
		SetMediaFocus(MediaFocusArea.List);
		TabTransitionDirection = 0;
		StatusText = string.Empty;
		NotifyTabStateChanged();
	}

	public void Stop()
	{
		_clockTimer.Stop();
	}

	public void Move(int delta)
	{
		if (IsSocialMessageOpen)
		{
			return;
		}
		switch (_screen)
		{
		case GuideScreen.MusicPicker:
			MoveGuideMusicTracks(delta);
			return;
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
		case GuideScreen.Achievements:
			MoveAchievements(delta);
			return;
		}
		if (Items.Count == 0)
		{
			return;
		}
		if (IsMediaTab && IsMediaSubmenuOpen)
		{
			int selectedMediaSubmenuIndex = SelectedMediaSubmenuIndex;
			SelectedMediaSubmenuIndex = Math.Clamp(SelectedMediaSubmenuIndex + delta, 0, MediaSubmenuItems.Count - 1);
			if (SelectedMediaSubmenuIndex != selectedMediaSubmenuIndex)
			{
				_audioService.Play("guide-hover");
			}
		}
		else if (IsMediaTab && delta < 0 && _mediaFocusArea == MediaFocusArea.Transport)
		{
			SetMediaFocus(MediaFocusArea.SongRow);
			_audioService.Play("guide-hover");
		}
		else if (IsMediaTab && _mediaFocusArea == MediaFocusArea.SongRow)
		{
			SetMediaFocus((delta >= 0) ? MediaFocusArea.Transport : MediaFocusArea.List);
			if (delta < 0)
			{
				SelectedIndex = Items.Count - 1;
			}
			_audioService.Play("guide-hover");
		}
		else if (IsMediaTab && delta > 0 && _mediaFocusArea == MediaFocusArea.List && SelectedIndex >= Items.Count - 1)
		{
			SetMediaFocus(MediaFocusArea.SongRow);
			_audioService.Play("guide-hover");
		}
		else if (!IsMediaTab || _mediaFocusArea != MediaFocusArea.Transport)
		{
			int selectedIndex = SelectedIndex;
			SelectedIndex = Math.Clamp(SelectedIndex + delta, 0, Items.Count - 1);
			if (SelectedIndex != selectedIndex)
			{
				SetMediaFocus(MediaFocusArea.List);
				_audioService.Play("guide-hover");
			}
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
			GuideScreen.MusicPicker => true, 
			GuideScreen.FriendSearch => TryMoveSearchKeyHorizontal(delta), 
			GuideScreen.Achievements => TryMoveAchievementsHorizontal(delta), 
			GuideScreen.FriendsList => true, 
			GuideScreen.Party => true, 
			GuideScreen.FriendProfile => true, 
			_ => TryMoveMediaTransport(delta), 
		};
	}

	public bool TryHandleKeyboardHorizontal(int delta)
	{
		if (_screen == GuideScreen.FriendsList || _screen == GuideScreen.Party)
		{
			return TrySwitchCommunityOverlayPage(-delta);
		}
		return TryHandleHorizontal(delta);
	}

	public bool SwitchCommunityTab(int delta)
	{
		if (_screen == GuideScreen.FriendsList && delta < 0)
		{
			return TrySwitchCommunityOverlayPage(1);
		}
		if (_screen == GuideScreen.Party && delta > 0)
		{
			return TrySwitchCommunityOverlayPage(-1);
		}
		if ((uint)(_screen - 2) <= 1u)
		{
			return true;
		}
		return false;
	}

	private bool TrySwitchCommunityOverlayPage(int delta)
	{
		if (_screen == GuideScreen.FriendsList && delta > 0)
		{
			TabTransitionDirection = 1;
			OpenParty();
			OnPropertyChanged("CurrentTabTitle");
			_audioService.Play("page-right");
			return true;
		}
		if (_screen == GuideScreen.Party && delta < 0)
		{
			TabTransitionDirection = -1;
			OpenFriendsList();
			OnPropertyChanged("CurrentTabTitle");
			_audioService.Play("page-left");
			return true;
		}
		return true;
	}

	public void MoveTab(int delta)
	{
		if (IsMainGuideScreen)
		{
			int num = Math.Clamp(_selectedTabIndex + delta, 0, _tabNames.Length - 1);
			if (num != _selectedTabIndex)
			{
				_selectedTabIndex = num;
				TabTransitionDirection = Math.Sign(delta);
				BuildItems();
				SelectedIndex = 0;
				CloseMediaSubmenu();
				SetMediaFocus(MediaFocusArea.List);
				StatusText = string.Empty;
				PlayBladeSwitchSound();
				NotifyTabStateChanged();
			}
		}
	}

	public void SelectTab(int index)
	{
		if (IsMainGuideScreen)
		{
			int num = Math.Clamp(index, 0, _tabNames.Length - 1);
			if (num != _selectedTabIndex)
			{
				TabTransitionDirection = Math.Sign(num - _selectedTabIndex);
				_selectedTabIndex = num;
				BuildItems();
				SelectedIndex = 0;
				CloseMediaSubmenu();
				SetMediaFocus(MediaFocusArea.List);
				StatusText = string.Empty;
				PlayBladeSwitchSound();
				NotifyTabStateChanged();
			}
		}
	}

	public void EnsureFriendListSelection()
	{
		if (FriendsListItems.Count == 0)
		{
			if (_selectedFriendListIndex != -1)
			{
				SetProperty(ref _selectedFriendListIndex, -1, "SelectedFriendListIndex");
				OnPropertyChanged("FriendsSelectionCountText");
			}
		}
		else
		{
			SelectedFriendListIndex = Math.Clamp(SelectedFriendListIndex, 0, FriendsListItems.Count - 1);
		}
	}

	private void SelectAddFriendRow()
	{
		if (_selectedFriendListIndex != -1)
		{
			SetProperty(ref _selectedFriendListIndex, -1, "SelectedFriendListIndex");
		}
		SelectedFriendListIndex = 0;
	}

	private void PlayBladeSwitchSound()
	{
		int soundNumber = _bladeSwitchSoundIndex % 4 + 1;
		_bladeSwitchSoundIndex = (_bladeSwitchSoundIndex + 1) % 4;
		_audioService.Play("guide-blade-switch-" + soundNumber.ToString(CultureInfo.InvariantCulture));
	}

	public void SelectRelativeTab(int offset)
	{
		if (IsMainGuideScreen)
		{
			int num = _selectedTabIndex + offset;
			if (num >= 0 && num < _tabNames.Length)
			{
				SelectTab(num);
			}
		}
	}

	public void ActivateSelected()
	{
		if (IsSocialMessageOpen)
		{
			DismissSocialMessage();
			_audioService.Play("guide-select");
			return;
		}
		switch (_screen)
		{
		case GuideScreen.MusicPicker:
			ActivateSelectedGuideMusicTrack();
			return;
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
		case GuideScreen.Achievements:
			ActivateSelectedAchievementScreenItem();
			return;
		}
		if (IsMediaTab && IsMediaSubmenuOpen)
		{
			ActivateSelectedMediaSubmenu();
		}
		else if (IsMediaTab && _mediaFocusArea == MediaFocusArea.SongRow)
		{
			_audioService.Play("guide-select");
			OpenGuideMusicPicker();
		}
		else if (IsMediaTab && _mediaFocusArea == MediaFocusArea.Transport)
		{
			ActivateSelectedMediaControl();
		}
		else if (SelectedIndex >= 0 && SelectedIndex < Items.Count && !Items[SelectedIndex].IsNoOp)
		{
			_audioService.Play("guide-select");
			Items[SelectedIndex].Action();
		}
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
			else if (_profileReturnScreen == GuideScreen.Party)
			{
				OpenParty();
			}
			else
			{
				OpenFriendsList();
			}
			_audioService.Play("guide-back");
			return true;
		}
		if (_screen == GuideScreen.Achievements)
		{
			if (IsAchievementDetail)
			{
				AchievementItems.Clear();
				RefreshAchievementSelection();
				OnPropertyChanged("IsAchievementGameList");
				OnPropertyChanged("IsAchievementDetail");
				OnPropertyChanged("AchievementsGameTitle");
				OnPropertyChanged("AchievementsCountText");
				OnPropertyChanged("AchievementsUnlockedText");
				StatusText = "Select a game to view achievements.";
				_audioService.Play("guide-back");
				return true;
			}
			SetScreen(GuideScreen.MainMenu);
			StatusText = string.Empty;
			_audioService.Play("guide-back");
			return true;
		}
		if (_screen == GuideScreen.MusicPicker)
		{
			RestoreGuideMusicPickerReturnState();
			_audioService.Play("guide-back");
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
			_audioService.Play("guide-back");
			return true;
		}
		if (_screen == GuideScreen.FriendsList)
		{
			SetScreen(GuideScreen.MainMenu);
			StatusText = string.Empty;
			_audioService.Play("guide-back");
			return true;
		}
		if (_screen == GuideScreen.Party)
		{
			SetScreen(GuideScreen.MainMenu);
			StatusText = string.Empty;
			_audioService.Play("guide-back");
			return true;
		}
		if (!IsMediaSubmenuOpen)
		{
			return false;
		}
		CloseMediaSubmenu();
		SetMediaFocus(MediaFocusArea.SongRow);
		_audioService.Play("guide-back");
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
			_audioService.Play("guide-select");
		}
		else if (_screen != GuideScreen.FriendsList)
		{
			if (_screen == GuideScreen.Party)
			{
				LeaveParty();
				_audioService.Play("guide-select");
			}
			else
			{
				CloseRunningGameFromGuideAsync();
			}
		}
	}

	public void HandleFooterY()
	{
		if (!IsSocialMessageOpen)
		{
			if (_screen == GuideScreen.FriendSearch)
			{
				AppendToFriendSearch(" ");
				_audioService.Play("guide-select");
			}
			else if (_screen == GuideScreen.FriendsList)
			{
				SortFriendsByStatus();
				BuildFriendsListItems();
				_audioService.Play("guide-select");
			}
			else if (_screen != GuideScreen.Party)
			{
				MinimizeDashboard();
			}
		}
	}

	public void PlaySound(string soundName)
	{
		_audioService.Play(soundName);
	}

	public void ActivateFriendListItem(GuideFriendListItem item)
	{
		int num = FriendsListItems.IndexOf(item);
		if (num >= 0)
		{
			SelectedFriendListIndex = num;
			ActivateSelectedFriendListItem();
		}
	}

	public void ActivatePartyRowItem(GuidePartyRowItem item)
	{
		int num = PartyRows.IndexOf(item);
		if (num >= 0)
		{
			SelectedPartyRowIndex = num;
			ActivateSelectedPartyRow();
		}
	}

	public void ActivateAchievementGameItem(GuideAchievementGameItem item)
	{
		int num = AchievementGameItems.IndexOf(item);
		if (num >= 0)
		{
			SelectedAchievementGameIndex = num;
			ActivateSelectedAchievementScreenItem();
		}
	}

	public void SelectAchievementItem(GuideAchievementItem item)
	{
		int num = AchievementItems.IndexOf(item);
		if (num >= 0)
		{
			SelectedAchievementIndex = num;
			_audioService.Play("guide-select");
		}
	}

	public void ActivateSearchKey(GuideKeyboardKeyItem item)
	{
		int num = SearchKeys.IndexOf(item);
		if (num >= 0)
		{
			SelectedSearchKeyIndex = num;
		}
		ActivateSearchKeyItem(item);
	}

	public void ActivateFriendProfileAction(GuideMenuItem item)
	{
		int num = FriendProfileActions.IndexOf(item);
		if (num >= 0)
		{
			SelectedFriendProfileActionIndex = num;
			ActivateSelectedFriendProfileAction();
		}
	}

	public void AppendFriendSearchCharacter(string value)
	{
		if (IsFriendSearchScreen && !string.IsNullOrEmpty(value))
		{
			AppendToFriendSearch(value);
			PlaySearchTypingSound();
		}
	}

	public void BackspaceFriendSearchFromKeyboard()
	{
		if (IsFriendSearchScreen)
		{
			ActivateSearchBackspaceKey();
		}
	}

	public void ConfirmFriendSearch()
	{
		if (IsFriendSearchScreen)
		{
			ActivateSearchDoneKey();
		}
	}

	public void ActivateSearchCursorLeftKey()
	{
		if (IsFriendSearchScreen)
		{
			ActivateSearchKeyItem(SearchCursorLeftKey);
		}
	}

	public void ActivateSearchCursorRightKey()
	{
		if (IsFriendSearchScreen)
		{
			ActivateSearchKeyItem(SearchCursorRightKey);
		}
	}

	public void ActivateSearchSymbolsKey()
	{
		if (IsFriendSearchScreen)
		{
			ActivateSearchKeyItem(SearchSymbolsKey, flash: false);
		}
	}

	public void ActivateSearchAccentsKey()
	{
		if (IsFriendSearchScreen)
		{
			ActivateSearchKeyItem(SearchAccentsKey, flash: false);
		}
	}

	public void ActivateSearchCapsKey()
	{
		if (IsFriendSearchScreen)
		{
			GuideKeyboardKeyItem item = SearchCapsKey;
			if (item != null)
			{
				ActivateSearchKeyItem(item);
			}
		}
	}

	public void ActivateSearchBackspaceKey()
	{
		if (IsFriendSearchScreen)
		{
			GuideKeyboardKeyItem item = SearchBackspaceKey;
			if (item != null)
			{
				ActivateSearchKeyItem(item);
			}
		}
	}

	public void ActivateSearchDoneKey()
	{
		if (IsFriendSearchScreen)
		{
			GuideKeyboardKeyItem item = SearchDoneKey;
			if (item != null)
			{
				ActivateSearchKeyItem(item);
			}
		}
	}

	public void MoveFriendSearchCursor(int delta)
	{
		MoveFriendSearchCursor(delta, playSound: true);
	}

	private void MoveFriendSearchCursor(int delta, bool playSound)
	{
		if (!IsFriendSearchScreen || delta == 0)
		{
			return;
		}
		int num = Math.Clamp(_friendSearchCursorIndex + delta, 0, FriendSearchQuery.Length);
		if (num != _friendSearchCursorIndex)
		{
			_friendSearchCursorIndex = num;
			OnPropertyChanged("FriendSearchDisplayText");
			if (playSound)
			{
				_audioService.Play("guide-hover");
			}
		}
	}

	public void SwitchToSymbolKeyboard()
	{
		if (IsFriendSearchScreen)
		{
			SetSearchKeyboardLayout((_searchKeyboardLayout != SearchKeyboardLayout.Symbols) ? SearchKeyboardLayout.Symbols : SearchKeyboardLayout.Default);
			SearchSymbolsKey.Label = SearchSymbolsButtonText;
			SearchAccentsKey.Label = SearchAccentsButtonText;
		}
	}

	public void SwitchToAccentKeyboard()
	{
		if (IsFriendSearchScreen)
		{
			SetSearchKeyboardLayout((_searchKeyboardLayout != SearchKeyboardLayout.Accents) ? SearchKeyboardLayout.Accents : SearchKeyboardLayout.Default);
			SearchSymbolsKey.Label = SearchSymbolsButtonText;
			SearchAccentsKey.Label = SearchAccentsButtonText;
		}
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
		int selectedMediaControlIndex = SelectedMediaControlIndex;
		SelectedMediaControlIndex = Math.Clamp(SelectedMediaControlIndex + delta, 0, MediaControls.Count - 1);
		if (SelectedMediaControlIndex != selectedMediaControlIndex)
		{
			_audioService.Play("guide-hover");
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
		OnPropertyChanged("Gamertag");
		OnPropertyChanged("GamerPicturePath");
		NotifySideTabTextChanged();
	}

	public void MinimizeDashboard()
	{
		_mainWindow.WindowState = WindowState.Minimized;
		StatusText = "Dashboard minimized";
	}

	private async Task CloseRunningGameFromGuideAsync()
	{
		if (!_dashboard.HasRunningLaunchedGame)
		{
			_runningGameForceClosePending = false;
			StatusText = "No game running";
			OnPropertyChanged("FooterXActionText");
			return;
		}
		bool forceKill = _runningGameForceClosePending && DateTimeOffset.UtcNow - _runningGameForceCloseRequestedAt < TimeSpan.FromSeconds(6.0);
		RunningGameCloseResult runningGameCloseResult = await _dashboard.CloseRunningGameAsync(forceKill).ConfigureAwait(continueOnCapturedContext: true);
		if (runningGameCloseResult.Success)
		{
			_runningGameForceClosePending = false;
			StatusText = runningGameCloseResult.Message;
		}
		else if (runningGameCloseResult.RequiresForceConfirmation)
		{
			_runningGameForceClosePending = true;
			_runningGameForceCloseRequestedAt = DateTimeOffset.UtcNow;
			StatusText = runningGameCloseResult.Message;
		}
		else
		{
			_runningGameForceClosePending = false;
			StatusText = runningGameCloseResult.Message;
		}
		OnPropertyChanged("FooterXActionText");
		OnPropertyChanged("ShowFooterXAction");
	}

	public void Dispose()
	{
		_dashboard.PropertyChanged -= Dashboard_OnPropertyChanged;
		_clockTimer.Stop();
		_partyRefreshTimer.Stop();
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
		}
		finally
		{
			_isRefreshingFriends = false;
		}
		if (_screen == GuideScreen.Party || _isUsingDiscordPartyData)
		{
			await RefreshPartySnapshotAsync(forceRebuild: true).ConfigureAwait(continueOnCapturedContext: true);
		}
		BuildItems();
		BuildMediaControls();
		BuildFriendsListItems();
		BuildPartyRows();
		OnPropertyChanged("Gamertag");
		OnPropertyChanged("GamerPicturePath");
		OnPropertyChanged("CurrentTabTitle");
		NotifySideTabTextChanged();
		OnPropertyChanged("GuideMediaPlaybackLabel");
		OnPropertyChanged("GuideMusicPickerCurrentTitle");
		OnPropertyChanged("GuideMusicPickerCountText");
		OnPropertyChanged("FriendsHeaderText");
		OnPropertyChanged("FooterXActionText");
		OnPropertyChanged("FooterYActionText");
		UpdateClock();
	}

	private void BuildItems()
	{
		Items.Clear();
		switch (_selectedTabIndex)
		{
		case 0:
			Items.Add(new GuideMenuItem("My Games", "\ue7fc", OpenMyGames));
			Items.Add(new GuideMenuItem("My Apps", "\uecaa", OpenMyApps));
			Items.Add(new GuideMenuItem("Game Marketplace", "\ue719", delegate
			{
				ShowPlaceholder("Game Marketplace");
			}));
			Items.Add(new GuideMenuItem("App Marketplace", "\ue719", delegate
			{
				ShowPlaceholder("App Marketplace");
			}));
			break;
		case 1:
			Items.Add(new GuideMenuItem("Achievements", string.Empty, OpenAchievements));
			Items.Add(new GuideMenuItem("Awards", string.Empty, delegate
			{
				ShowPlaceholder("Awards");
			}));
			Items.Add(new GuideMenuItem("Recent", string.Empty, delegate
			{
				ShowPlaceholder("Recent");
			}));
			Items.Add(new GuideMenuItem("My Games", string.Empty, OpenMyGames));
			Items.Add(new GuideMenuItem("Active Downloads", string.Empty, delegate
			{
				ShowPlaceholder("Active Downloads");
			}));
			Items.Add(new GuideMenuItem("Redeem Code", string.Empty, delegate
			{
				ShowPlaceholder("Redeem Code");
			}));
			break;
		case 3:
			Items.Add(new GuideMenuItem("Video Player", string.Empty, DoNothing, "", isNoOp: true));
			Items.Add(new GuideMenuItem("Music Player", string.Empty, OpenMusicPlayer));
			Items.Add(new GuideMenuItem("Picture Viewer", string.Empty, DoNothing, "", isNoOp: true));
			Items.Add(new GuideMenuItem("Windows Media Center", string.Empty, DoNothing, "", isNoOp: true));
			break;
		case 4:
			Items.Add(new GuideMenuItem("System Settings", "\ue713", OpenSettings));
			Items.Add(new GuideMenuItem("Profile", "\ue77b", OpenProfile));
			Items.Add(new GuideMenuItem("Preferences", "\ue115", OpenSettings));
			Items.Add(new GuideMenuItem("Turn Off", "\ue7e8", delegate
			{
				Application.Current.Shutdown();
			}));
			break;
		default:
			Items.Add(new GuideMenuItem("Xbox Home", string.Empty, OpenXboxHome));
			Items.Add(new GuideMenuItem("Friends", "\ue13d", OpenFriends, _socialFriends.Count.ToString()));
			Items.Add(new GuideMenuItem("Party", "\ue716", OpenParty, PartyMemberCount.ToString()));
			Items.Add(new GuideMenuItem("Inside Xbox", "\ue119", delegate
			{
				ShowPlaceholder("Inside Xbox");
			}, "0"));
			Items.Add(new GuideMenuItem("Minimize", "\ue8bb", CloseGuide));
			Items.Add(new GuideMenuItem("Chat and IM", "\ue15f", OpenParty));
			Items.Add(new GuideMenuItem(_dashboard.TrayGame?.Title ?? string.Empty, "\ue958", OpenTray));
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
		switch (index)
		{
		case 1:
			return Gamertag;
		case 2:
			if (_selectedTabIndex == 1)
			{
				return Gamertag;
			}
			break;
		}
		return _tabNames[index];
	}

	private void NotifyTabStateChanged()
	{
		OnPropertyChanged("CurrentTabTitle");
		NotifySideTabTextChanged();
		OnPropertyChanged("IsGuideBladeScreen");
		OnPropertyChanged("IsHomeTab");
		OnPropertyChanged("IsMediaTab");
		OnPropertyChanged("IsGuideMenuSelectionActive");
		OnPropertyChanged("GuideMenuVisualSelectedIndex");
		OnPropertyChanged("IsGuideMusicPickerScreen");
		OnPropertyChanged("IsMediaSongRowFocused");
	}

	private void NotifySideTabTextChanged()
	{
		OnPropertyChanged("LeftOuterTabText");
		OnPropertyChanged("LeftInnerTabText");
		OnPropertyChanged("RightInnerTabText");
		OnPropertyChanged("RightOuterTabText");
	}

	private void BuildMediaSubmenu()
	{
		MediaSubmenuItems.Clear();
		MediaSubmenuItems.Add(new GuideMenuItem("Now Playing", string.Empty, delegate
		{
			ShowPlaceholder(GuideMediaPlaybackLabel);
		}));
		MediaSubmenuItems.Add(new GuideMenuItem("All Songs", string.Empty, delegate
		{
			ShowPlaceholder("All Songs");
		}));
		MediaSubmenuItems.Add(new GuideMenuItem("Playlists", string.Empty, delegate
		{
			ShowPlaceholder("Playlists");
		}));
		MediaSubmenuItems.Add(new GuideMenuItem("Artists", string.Empty, delegate
		{
			ShowPlaceholder("Artists");
		}));
		MediaSubmenuItems.Add(new GuideMenuItem("Albums", string.Empty, delegate
		{
			ShowPlaceholder("Albums");
		}));
		MediaSubmenuItems.Add(new GuideMenuItem("Genres", string.Empty, delegate
		{
			ShowPlaceholder("Genres");
		}));
		MediaSubmenuItems.Add(new GuideMenuItem("Search", string.Empty, delegate
		{
			ShowPlaceholder("Search");
		}));
		MediaSubmenuItems.Add(new GuideMenuItem("Random All Songs", string.Empty, PlayRandomAllSongs));
	}

	private async void OpenGuideMusicPicker()
	{
		if (_screen == GuideScreen.MainMenu)
		{
			await OpenMusicSourcesFromGuideAsync().ConfigureAwait(continueOnCapturedContext: true);
			return;
		}
		OpenMusicSourcesFromGuide();
	}

	private async Task OpenMusicSourcesFromGuideAsync()
	{
		if (_isOpeningGuideMusicPicker)
		{
			return;
		}
		_isOpeningGuideMusicPicker = true;
		try
		{
			await Task.Delay(360).ConfigureAwait(continueOnCapturedContext: true);
			OpenMusicSourcesFromGuide(playLoadSound: false);
			await Task.Delay(120).ConfigureAwait(continueOnCapturedContext: true);
			_audioService.Play("guide-music-sources-load");
		}
		finally
		{
			_isOpeningGuideMusicPicker = false;
		}
	}

	private void OpenMusicSourcesFromGuide(bool playLoadSound = true)
	{
		PrepareGuideReturnToDashboard();
		CloseGuide();
		if (_dashboard.OpenMusicPlayerFromGuideCommand.CanExecute(null))
		{
			_dashboard.OpenMusicPlayerFromGuideCommand.Execute(null);
			if (playLoadSound)
			{
				_audioService.Play("guide-music-sources-load");
			}
		}
		RestoreMainWindow();
	}

	private void OpenGuideMusicPickerImmediate()
	{
		_musicPickerReturnScreen = _screen;
		_musicPickerReturnMediaFocus = _mediaFocusArea;
		_restoreMediaSubmenuAfterMusicPicker = IsMediaSubmenuOpen;
		CloseMediaSubmenu();
		SyncGuideMusicPickerSelection();
		SetScreen(GuideScreen.MusicPicker);
		StatusText = string.Empty;
	}

	private void BuildMediaControls()
	{
		MediaControls.Clear();
		MediaControls.Add(new GuideMediaControlItem("Previous", "\ue100", ExecutePreviousTrack));
		MediaControls.Add(new GuideMediaControlItem(_dashboard.IsMusicPlaying ? "Pause" : "Play", _dashboard.IsMusicPlaying ? "\ue103" : "\ue102", ExecutePlayPause));
		MediaControls.Add(new GuideMediaControlItem("Stop", "\ue15b", ExecuteStop));
		MediaControls.Add(new GuideMediaControlItem("Next", "\ue101", ExecuteNextTrack));
		MediaControls.Add(new GuideMediaControlItem(_dashboard.ShuffleText, "\ue8b1", ExecuteShuffle));
		MediaControls.Add(new GuideMediaControlItem("Volume", "\ue995", ExecuteVolumeUp));
		RefreshMediaControlSelection();
	}

	private async void OpenAchievements()
	{
		if (_screen == GuideScreen.MainMenu)
		{
			await OpenAchievementsOverlayFromMainGuideAsync().ConfigureAwait(continueOnCapturedContext: true);
			return;
		}
		OpenAchievementsImmediate();
	}

	private async Task OpenAchievementsOverlayFromMainGuideAsync()
	{
		if (_isOpeningAchievementsOverlay)
		{
			return;
		}
		_isOpeningAchievementsOverlay = true;
		try
		{
			await Task.Delay(125).ConfigureAwait(continueOnCapturedContext: true);
			_audioService.Play("menu-in");
			await Task.Delay(75).ConfigureAwait(continueOnCapturedContext: true);
			_isOpeningAchievementsOverlayFromMainGuide = true;
			OpenAchievementsImmediate();
		}
		finally
		{
			_isOpeningAchievementsOverlay = false;
		}
	}

	private void OpenAchievementsImmediate()
	{
		SetScreen(GuideScreen.Achievements);
		BuildAchievementGameList();
		SelectedAchievementGameIndex = 0;
		SelectedAchievementIndex = 0;
		AchievementItems.Clear();
		RefreshAchievementGameSelection();
		RefreshAchievementSelection();
		StatusText = ((AchievementGameItems.Count == 0) ? "Scan Steam games in Settings to view achievements." : "Select a game to view achievements.");
		OnPropertyChanged("IsAchievementGameList");
		OnPropertyChanged("IsAchievementDetail");
		OnPropertyChanged("AchievementsGameTitle");
		OnPropertyChanged("AchievementsCountText");
		OnPropertyChanged("AchievementsUnlockedText");
	}

	public async void OpenAchievementFromDashboard(string steamAppId, string achievementApiName)
	{
		SetScreen(GuideScreen.Achievements);
		BuildAchievementGameList();
		int gameIndex = AchievementGameItems.Select((GuideAchievementGameItem item, int index) => new
		{
			item,
			index
		}).FirstOrDefault(candidate => string.Equals(candidate.item.SteamAppId, steamAppId, StringComparison.OrdinalIgnoreCase))?.index ?? 0;
		SelectedAchievementGameIndex = gameIndex;
		SelectedAchievementIndex = 0;
		AchievementItems.Clear();
		RefreshAchievementGameSelection();
		RefreshAchievementSelection();
		OnPropertyChanged("IsAchievementGameList");
		OnPropertyChanged("IsAchievementDetail");
		OnPropertyChanged("AchievementsGameTitle");
		OnPropertyChanged("AchievementsCountText");
		OnPropertyChanged("AchievementsUnlockedText");
		await LoadSelectedAchievementGameAsync().ConfigureAwait(continueOnCapturedContext: true);
		int achievementIndex = AchievementItems.Select((GuideAchievementItem item, int index) => new
		{
			item,
			index
		}).FirstOrDefault(candidate => string.Equals(candidate.item.ApiName, achievementApiName, StringComparison.OrdinalIgnoreCase))?.index ?? -1;
		if (achievementIndex < 0)
		{
			achievementIndex = AchievementItems.Select((GuideAchievementItem item, int index) => new
			{
				item,
				index
			}).FirstOrDefault(candidate => string.Equals(candidate.item.Title, achievementApiName, StringComparison.CurrentCultureIgnoreCase))?.index ?? 0;
		}
		SelectedAchievementIndex = Math.Clamp(achievementIndex, 0, Math.Max(0, AchievementItems.Count - 1));
		RefreshAchievementSelection();
		NotifySelectedAchievementChanged();
	}

	private void BuildAchievementGameList()
	{
		string selectedAppId = GetAchievementGame()?.SteamAppId ?? string.Empty;
		List<GameMetadata> list = (from @group in _dashboard.Games.Select((GameCardViewModel card) => card.Game).Where(IsSteamAchievementGame).GroupBy<GameMetadata, string>((GameMetadata game) => game.SteamAppId, StringComparer.OrdinalIgnoreCase)
			select @group.OrderByDescending((GameMetadata game) => game.LastPlayed ?? DateTimeOffset.MinValue).ThenBy<GameMetadata, string>((GameMetadata game) => game.Title, StringComparer.CurrentCultureIgnoreCase).First() into game
			orderby game.LastPlayed ?? DateTimeOffset.MinValue descending
			select game).ThenBy<GameMetadata, string>((GameMetadata game) => game.Title, StringComparer.CurrentCultureIgnoreCase).ToList();
		AchievementGameItems.Clear();
		foreach (GameMetadata item in list)
		{
			AchievementGameItems.Add(new GuideAchievementGameItem
			{
				Title = item.Title,
				SteamAppId = item.SteamAppId,
				CoverArtPath = item.CoverArtPath
			});
		}
		int selectedAchievementGameIndex = AchievementGameItems.Select((GuideAchievementGameItem item2, int index) => new
		{
			item = item2,
			index = index
		}).FirstOrDefault(candidate => string.Equals(candidate.item.SteamAppId, selectedAppId, StringComparison.OrdinalIgnoreCase))?.index ?? 0;
		SelectedAchievementGameIndex = selectedAchievementGameIndex;
	}

	private async void ActivateSelectedAchievementScreenItem()
	{
		if (IsAchievementDetail)
		{
			_audioService.Play("guide-select");
		}
		else if (SelectedAchievementGameIndex >= 0 && SelectedAchievementGameIndex < AchievementGameItems.Count)
		{
			await LoadSelectedAchievementGameAsync().ConfigureAwait(continueOnCapturedContext: true);
		}
	}

	private async Task LoadSelectedAchievementGameAsync()
	{
		if (SelectedAchievementGameIndex < 0 || SelectedAchievementGameIndex >= AchievementGameItems.Count)
		{
			return;
		}
		GuideAchievementGameItem game = AchievementGameItems[SelectedAchievementGameIndex];
		try
		{
			StatusText = "Loading achievements...";
			_audioService.Play("guide-select");
			IReadOnlyList<SteamAchievementItem> obj = await _steamCommunityService.LoadAchievementsAsync(game.SteamAppId).ConfigureAwait(continueOnCapturedContext: true);
			AchievementItems.Clear();
			foreach (SteamAchievementItem item in obj)
			{
				AchievementItems.Add(new GuideAchievementItem
				{
					Title = (string.IsNullOrWhiteSpace(item.Name) ? item.ApiName : item.Name),
					ApiName = item.ApiName,
					Description = item.Description,
					Achieved = item.Achieved,
					StatusText = (item.Achieved ? "Unlocked" : "Locked"),
					UnlockTimeUnix = item.UnlockTimeUnix,
					IconPath = item.Achieved ? ResolveAchievementIconPath(item) : string.Empty
				});
			}
			game.UnlockedCount = AchievementItems.Count((GuideAchievementItem guideAchievementItem) => guideAchievementItem.Achieved);
			game.TotalCount = AchievementItems.Count;
			game.StatusText = ((AchievementItems.Count > 0) ? game.CountText : "No achievements exposed");
			SelectedAchievementIndex = 0;
			RefreshAchievementSelection();
			NotifySelectedAchievementChanged();
			StatusText = (string.IsNullOrWhiteSpace(_steamCommunityService.LastStatusMessage) ? string.Empty : _steamCommunityService.LastStatusMessage);
		}
		catch (Exception exception)
		{
			App.LogException(exception, "GuideViewModel.OpenAchievements");
			StatusText = "Achievements could not be loaded.";
		}
		finally
		{
			OnPropertyChanged("IsAchievementGameList");
			OnPropertyChanged("IsAchievementDetail");
			OnPropertyChanged("AchievementsGameTitle");
			OnPropertyChanged("AchievementsCountText");
			OnPropertyChanged("AchievementsUnlockedText");
		}
	}

	private static string ResolveAchievementIconPath(SteamAchievementItem achievement)
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

	private GameMetadata? GetAchievementGame()
	{
		GameMetadata runningLaunchedGame = _dashboard.RunningLaunchedGame;
		if (IsSteamAchievementGame(runningLaunchedGame))
		{
			return runningLaunchedGame;
		}
		GameMetadata gameMetadata = _dashboard.SelectedGame?.Game;
		if (IsSteamAchievementGame(gameMetadata))
		{
			return gameMetadata;
		}
		GameMetadata gameMetadata2 = _dashboard.TrayGame?.Game;
		if (IsSteamAchievementGame(gameMetadata2))
		{
			return gameMetadata2;
		}
		return (from game in _dashboard.Games.Select((GameCardViewModel card) => card.Game).Where(IsSteamAchievementGame)
			orderby game.LastPlayed ?? DateTimeOffset.MinValue descending
			select game).ThenBy<GameMetadata, string>((GameMetadata game) => game.Title, StringComparer.CurrentCultureIgnoreCase).FirstOrDefault();
	}

	private static bool IsSteamAchievementGame(GameMetadata? game)
	{
		if (game != null)
		{
			return !string.IsNullOrWhiteSpace(game.SteamAppId);
		}
		return false;
	}

	private void MoveAchievements(int delta)
	{
		if (IsAchievementGameList)
		{
			if (AchievementGameItems.Count != 0)
			{
				int selectedAchievementGameIndex = SelectedAchievementGameIndex;
				SelectedAchievementGameIndex = Math.Clamp(SelectedAchievementGameIndex + delta, 0, AchievementGameItems.Count - 1);
				if (SelectedAchievementGameIndex != selectedAchievementGameIndex)
				{
					_audioService.Play("guide-hover");
				}
			}
		}
		else if (AchievementItems.Count != 0)
		{
			int selectedAchievementIndex = SelectedAchievementIndex;
			int num = SelectedAchievementIndex % 7;
			int num2 = SelectedAchievementIndex + delta * 7;
			if (num2 >= AchievementItems.Count)
			{
				num2 = Math.Min((AchievementItems.Count - 1) / 7 * 7 + num, AchievementItems.Count - 1);
			}
			else if (num2 < 0)
			{
				num2 = num;
			}
			SelectedAchievementIndex = Math.Clamp(num2, 0, AchievementItems.Count - 1);
			if (SelectedAchievementIndex != selectedAchievementIndex)
			{
				_audioService.Play("guide-hover");
			}
		}
	}

	private bool TryMoveAchievementsHorizontal(int delta)
	{
		if (!IsAchievementDetail || AchievementItems.Count == 0)
		{
			return true;
		}
		int selectedAchievementIndex = SelectedAchievementIndex;
		SelectedAchievementIndex = Math.Clamp(SelectedAchievementIndex + delta, 0, AchievementItems.Count - 1);
		if (SelectedAchievementIndex != selectedAchievementIndex)
		{
			_audioService.Play("guide-hover");
		}
		return true;
	}

	private void RefreshAchievementSelection()
	{
		for (int i = 0; i < AchievementItems.Count; i++)
		{
			AchievementItems[i].IsSelected = i == SelectedAchievementIndex;
		}
	}

	private void NotifySelectedAchievementChanged()
	{
		OnPropertyChanged("AchievementsCountText");
		OnPropertyChanged("AchievementsUnlockedText");
		OnPropertyChanged("SelectedAchievementTitle");
		OnPropertyChanged("SelectedAchievementDescription");
		OnPropertyChanged("SelectedAchievementStatusText");
		OnPropertyChanged("SelectedAchievementDateText");
	}

	private void RefreshAchievementGameSelection()
	{
		for (int i = 0; i < AchievementGameItems.Count; i++)
		{
			AchievementGameItems[i].IsSelected = i == SelectedAchievementGameIndex;
		}
	}

	private void SetMediaFocus(MediaFocusArea area)
	{
		_mediaFocusArea = area;
		IsMediaTransportFocused = area == MediaFocusArea.Transport;
		OnPropertyChanged("IsGuideMenuSelectionActive");
		OnPropertyChanged("GuideMenuVisualSelectedIndex");
		OnPropertyChanged("IsMediaSongRowFocused");
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
		if (IsMediaSubmenuOpen)
		{
			IsMediaSubmenuOpen = false;
		}
	}

	private void ActivateSelectedMediaSubmenu()
	{
		if (SelectedMediaSubmenuIndex >= 0 && SelectedMediaSubmenuIndex < MediaSubmenuItems.Count)
		{
			_audioService.Play("guide-select");
			MediaSubmenuItems[SelectedMediaSubmenuIndex].Action();
		}
	}

	private void MoveGuideMusicTracks(int delta)
	{
		if (GuideMusicTracks.Count != 0)
		{
			int selectedGuideMusicTrackIndex = SelectedGuideMusicTrackIndex;
			SelectedGuideMusicTrackIndex = Math.Clamp(SelectedGuideMusicTrackIndex + delta, 0, GuideMusicTracks.Count - 1);
			if (SelectedGuideMusicTrackIndex != selectedGuideMusicTrackIndex)
			{
				_audioService.Play("guide-hover");
				OnPropertyChanged("GuideMusicPickerCountText");
			}
		}
	}

	private void ActivateSelectedGuideMusicTrack()
	{
		if (GuideMusicTracks.Count == 0 || SelectedGuideMusicTrackIndex < 0 || SelectedGuideMusicTrackIndex >= GuideMusicTracks.Count)
		{
			StatusText = "No music found";
			return;
		}
		MusicTrackViewModel parameter = GuideMusicTracks[SelectedGuideMusicTrackIndex];
		if (_dashboard.PlaySelectedMusicCommand.CanExecute(parameter))
		{
			_audioService.Play("guide-select");
			_dashboard.PlaySelectedMusicCommand.Execute(parameter);
			OnPropertyChanged("GuideMediaPlaybackLabel");
			OnPropertyChanged("GuideMusicPickerCurrentTitle");
			OnPropertyChanged("GuideMusicPickerCountText");
		}
	}

	private void RestoreGuideMusicPickerReturnState()
	{
		SetScreen(_musicPickerReturnScreen);
		if (_musicPickerReturnScreen == GuideScreen.MainMenu)
		{
			if (_restoreMediaSubmenuAfterMusicPicker)
			{
				OpenGuideMusicMenu();
			}
			else
			{
				SetMediaFocus(_musicPickerReturnMediaFocus);
			}
		}
		_restoreMediaSubmenuAfterMusicPicker = false;
	}

	private void SyncGuideMusicPickerSelection()
	{
		if (GuideMusicTracks.Count == 0)
		{
			SelectedGuideMusicTrackIndex = 0;
			OnPropertyChanged("GuideMusicPickerCountText");
		}
		else
		{
			SelectedGuideMusicTrackIndex = ((_dashboard.CurrentMusicTrack != null) ? Math.Max(0, GuideMusicTracks.IndexOf(_dashboard.CurrentMusicTrack)) : 0);
			OnPropertyChanged("GuideMusicPickerCountText");
		}
	}

	private void SelectAndActivateMediaSubmenu(GuideMenuItem item)
	{
		int num = MediaSubmenuItems.IndexOf(item);
		if (num >= 0)
		{
			SelectedMediaSubmenuIndex = num;
			ActivateSelectedMediaSubmenu();
		}
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
		for (int i = 0; i < MediaControls.Count; i++)
		{
			MediaControls[i].IsSelected = IsMediaTransportFocused && i == SelectedMediaControlIndex;
		}
	}

	private void ActivateSelectedMediaControl()
	{
		if (SelectedMediaControlIndex >= 0 && SelectedMediaControlIndex < MediaControls.Count)
		{
			_audioService.Play("guide-select");
			MediaControls[SelectedMediaControlIndex].Action();
		}
	}

	private void SelectAndActivateMediaControl(GuideMediaControlItem item)
	{
		int num = MediaControls.IndexOf(item);
		if (num >= 0)
		{
			SetMediaFocus(MediaFocusArea.Transport);
			SelectedMediaControlIndex = num;
			ActivateSelectedMediaControl();
		}
	}

	private void UpdateClock()
	{
		ClockText = DateTime.Now.ToString("h:mm  tt");
	}

	private void Dashboard_OnPropertyChanged(object? sender, PropertyChangedEventArgs e)
	{
		bool flag;
		switch (e.PropertyName)
		{
		case "Profile":
		case "TrayGame":
		case "OpenTrayTitle":
		case "SocialIntegrationModeDisplay":
			flag = true;
			break;
		default:
			flag = false;
			break;
		}
		if (flag)
		{
			RefreshAsync(showPopup: false);
			return;
		}
		switch (e.PropertyName)
		{
		case "HasRunningLaunchedGame":
		case "RunningLaunchedGameTitle":
		case "RunningGameFooterActionText":
			flag = true;
			break;
		default:
			flag = false;
			break;
		}
		if (flag)
		{
			OnPropertyChanged("FooterXActionText");
			OnPropertyChanged("ShowFooterXAction");
			return;
		}
		switch (e.PropertyName)
		{
		case "CurrentMusicTrack":
		case "CurrentMusicTitle":
		case "IsMusicPlaying":
		case "MusicPlayPauseText":
		case "ShuffleText":
			flag = true;
			break;
		default:
			flag = false;
			break;
		}
		if (flag)
		{
			BuildMediaControls();
			BuildMediaSubmenu();
			OnPropertyChanged("GuideMediaPlaybackLabel");
			OnPropertyChanged("GuideMusicPickerCurrentTitle");
			OnPropertyChanged("GuideMusicPickerCountText");
			SyncGuideMusicPickerSelection();
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
		if (_dashboard.TrayGame == null)
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
	{
		StatusText = pageName + " is not connected yet";
	}

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

	private async void OpenFriends()
	{
		if (CanUseFriendsOverlay())
		{
			if (_screen == GuideScreen.MainMenu)
			{
				await OpenCommunityOverlayFromMainGuideAsync(OpenFriendsList).ConfigureAwait(continueOnCapturedContext: true);
			}
			else
			{
				OpenFriendsList();
			}
		}
	}

	public void OpenFriendsOverlayFromDashboard()
	{
		if (CanUseFriendsOverlay())
		{
			_selectedTabIndex = 2;
			BuildItems();
			NotifyTabStateChanged();
			OpenFriendsList();
		}
	}

	public void OpenPartyOverlayFromDashboard()
	{
		if (CanUseFriendsOverlay())
		{
			_selectedTabIndex = 2;
			BuildItems();
			NotifyTabStateChanged();
			OpenPartyImmediate();
		}
	}

	private void OpenFriendsList()
	{
		if (CanUseFriendsOverlay())
		{
			DismissSocialMessage();
			BuildFriendsListItems();
			SetScreen(GuideScreen.FriendsList);
			SelectAddFriendRow();
			StatusText = string.Empty;
			RefreshAsync(showPopup: true);
		}
	}

	private async void OpenParty()
	{
		if (CanUseFriendsOverlay())
		{
			if (_screen == GuideScreen.MainMenu)
			{
				await OpenCommunityOverlayFromMainGuideAsync(OpenPartyImmediate).ConfigureAwait(continueOnCapturedContext: true);
			}
			else
			{
				OpenPartyImmediate();
			}
		}
	}

	private async Task OpenCommunityOverlayFromMainGuideAsync(Action openAction)
	{
		if (_isOpeningCommunityOverlay)
		{
			return;
		}
		_isOpeningCommunityOverlay = true;
		try
		{
			await Task.Delay(125).ConfigureAwait(continueOnCapturedContext: true);
			_audioService.Play("menu-in");
			await Task.Delay(75).ConfigureAwait(continueOnCapturedContext: true);
			_isOpeningCommunityOverlayFromMainGuide = true;
			openAction();
		}
		finally
		{
			_isOpeningCommunityOverlay = false;
		}
	}

	private void OpenPartyImmediate()
	{
		if (CanUseFriendsOverlay())
		{
			DismissSocialMessage();
			BuildPartyRows();
			SetScreen(GuideScreen.Party);
			SelectedPartyRowIndex = FindNextSelectablePartyIndex(0, 1);
			StatusText = string.Empty;
			RefreshAsync(showPopup: true);
			RefreshPartySnapshotAsync(forceRebuild: true);
		}
	}

	private void BuildFriendsListItems()
	{
		List<SocialFriend> sortedFriends = SortSocialFriends(_socialFriends).ToList();
		string friendsListItemsSignature = string.Join("\n", sortedFriends.Select((SocialFriend friend) => string.Join("\t", friend.Id, friend.DisplayName, SocialIntegrationManager.GetSourceLabel(friend), SocialIntegrationManager.GetFriendActivityLabel(friend), friend.AvatarPathOrUrl)));
		if (FriendsListItems.Count > 0 && string.Equals(_friendsListItemsSignature, friendsListItemsSignature, StringComparison.Ordinal))
		{
			EnsureFriendListSelection();
			OnPropertyChanged("FriendsHeaderText");
			OnPropertyChanged("FriendsTotalCountText");
			OnPropertyChanged("FriendsSelectionCountText");
			return;
		}
		int selectedIndex = _selectedFriendListIndex;
		_isRebuildingFriendsListItems = true;
		try
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
			foreach (SocialFriend item in sortedFriends)
			{
				FriendsListItems.Add(new GuideFriendListItem
				{
					FriendId = item.Id,
					Gamertag = item.DisplayName,
					Subtitle = SocialIntegrationManager.GetSourceLabel(item),
					Status = SocialIntegrationManager.GetFriendActivityLabel(item),
					AvatarPath = item.AvatarPathOrUrl
				});
			}
			_friendsListItemsSignature = friendsListItemsSignature;
		}
		finally
		{
			_isRebuildingFriendsListItems = false;
		}
		SelectedFriendListIndex = selectedIndex;
		EnsureFriendListSelection();
		OnPropertyChanged("FriendsListItems");
		OnPropertyChanged("SelectedFriendListIndex");
		OnPropertyChanged("FriendsHeaderText");
		OnPropertyChanged("FriendsTotalCountText");
		OnPropertyChanged("FriendsSelectionCountText");
	}

	private void MoveFriendList(int delta)
	{
		if (FriendsListItems.Count != 0)
		{
			EnsureFriendListSelection();
			int selectedFriendListIndex = SelectedFriendListIndex;
			SelectedFriendListIndex = Math.Clamp(SelectedFriendListIndex + delta, 0, FriendsListItems.Count - 1);
			if (SelectedFriendListIndex != selectedFriendListIndex)
			{
				_audioService.Play("guide-hover");
			}
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
		foreach (GuidePartyMember partyMember in _partyMembers)
		{
			PartyRows.Add(new GuidePartyRowItem
			{
				RowKind = "Member",
				Title = partyMember.Gamertag,
				AvatarPath = partyMember.AvatarPath,
				ActivityText = partyMember.ActivityText,
				ActivityIcon = partyMember.ActivityIcon,
				ShowVoiceIcon = partyMember.ShowVoiceIcon,
				IsHost = partyMember.IsHost,
				IsSelectable = true
			});
		}
		OnPropertyChanged("PartyHeaderText");
		OnPropertyChanged("PartyFriendCountText");
		OnPropertyChanged("PartyMessageCountText");
		OnPropertyChanged("PartyGameCountText");
		OnPropertyChanged("PartyMemberCount");
		GuideMenuItem guideMenuItem = Items.FirstOrDefault((GuideMenuItem item) => string.Equals(item.Title, "Party", StringComparison.OrdinalIgnoreCase));
		if (guideMenuItem != null)
		{
			guideMenuItem.Count = PartyMemberCount.ToString();
		}
	}

	private void BuildPartyMembers()
	{
		_partyMembers.Clear();
		SocialFriend socialFriend = SocialIntegrationManager.BuildPartyHost(_dashboard.Profile);
		_partyMembers.Add(new GuidePartyMember
		{
			Gamertag = socialFriend.DisplayName,
			AvatarPath = socialFriend.AvatarPathOrUrl,
			ActivityText = socialFriend.ActivityText,
			ActivityIcon = "\ue7fc",
			ShowVoiceIcon = socialFriend.ShowVoiceIndicator,
			IsHost = true
		});
		foreach (SocialFriend item in _partyRoster)
		{
			_partyMembers.Add(new GuidePartyMember
			{
				Gamertag = item.DisplayName,
				AvatarPath = item.AvatarPathOrUrl,
				ActivityText = SocialIntegrationManager.GetFriendActivityLabel(item),
				ActivityIcon = "\ue7fc",
				ShowVoiceIcon = item.ShowVoiceIndicator,
				IsHost = item.IsPartyHost
			});
		}
	}

	private bool ShouldUseDiscordPartyData()
	{
		return false;
	}

	private void UpdatePartyRefreshState()
	{
		if (_screen == GuideScreen.Party || _screen == GuideScreen.FriendsList || (_screen == GuideScreen.Party && ShouldUseDiscordPartyData()))
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
		if (!(await _partyRefreshLock.WaitAsync(0).ConfigureAwait(continueOnCapturedContext: true)))
		{
			return;
		}
		try
		{
			if (_screen == GuideScreen.FriendsList)
			{
				await LoadFriendsAsync(showPopup: false).ConfigureAwait(continueOnCapturedContext: true);
				BuildFriendsListItems();
			}
			if (ShouldUseDiscordPartyData())
			{
				return;
			}
			bool dashPartyChanged = await RefreshDashPartyLinkAsync().ConfigureAwait(continueOnCapturedContext: true);
			bool flag = _isUsingDiscordPartyData || !string.IsNullOrWhiteSpace(_partyStatusMessage);
			_isUsingDiscordPartyData = false;
			_partyStatusMessage = string.Empty;
			if (forceRebuild || flag || dashPartyChanged)
			{
				BuildPartyRows();
				if (_screen == GuideScreen.Party)
				{
					int candidate = Math.Clamp(SelectedPartyRowIndex, 0, Math.Max(0, PartyRows.Count - 1));
					SelectedPartyRowIndex = FindNextSelectablePartyIndex(candidate, 1);
				}
			}
		}
		finally
		{
			_partyRefreshLock.Release();
		}
	}

	private async Task<bool> RefreshDashPartyLinkAsync()
	{
		bool changed = false;
		IReadOnlyList<DashPartyTextMessage> messages = await _socialIntegrationManager.GetDashPartyTextMessagesAsync().ConfigureAwait(continueOnCapturedContext: true);
		if (messages.Count > 0)
		{
			_dashUnreadMessageCount += messages.Count;
			foreach (var message in messages) BladesMessages.Add(message);
			DashPartyTextMessage latest = messages[messages.Count - 1];
			string sender = (string.IsNullOrWhiteSpace(latest.FromGamertag) ? "DashX360 Player" : latest.FromGamertag);
			ShowSocialMessage(sender + ": " + latest.Message);
			OnPropertyChanged("FriendsMessageCountText");
			OnPropertyChanged("PartyMessageCountText");
			changed = true;
		}
		return changed;
	}

	private static bool PartyMembersEqual(IReadOnlyList<SocialFriend> left, IReadOnlyList<SocialFriend> right)
	{
		if (left == right)
		{
			return true;
		}
		if (left.Count != right.Count)
		{
			return false;
		}
		for (int i = 0; i < left.Count; i++)
		{
			SocialFriend socialFriend = left[i];
			SocialFriend socialFriend2 = right[i];
			if (!string.Equals(socialFriend.Id, socialFriend2.Id, StringComparison.OrdinalIgnoreCase) || !string.Equals(socialFriend.DisplayName, socialFriend2.DisplayName, StringComparison.Ordinal) || !string.Equals(socialFriend.AvatarPathOrUrl, socialFriend2.AvatarPathOrUrl, StringComparison.Ordinal) || !string.Equals(socialFriend.StatusText, socialFriend2.StatusText, StringComparison.Ordinal) || !string.Equals(socialFriend.ActivityText, socialFriend2.ActivityText, StringComparison.Ordinal) || socialFriend.IsOnline != socialFriend2.IsOnline || socialFriend.ShowVoiceIndicator != socialFriend2.ShowVoiceIndicator || socialFriend.IsPartyHost != socialFriend2.IsPartyHost)
			{
				return false;
			}
		}
		return true;
	}

	private void MovePartyRows(int delta)
	{
		if (PartyRows.Count != 0 && delta != 0)
		{
			int selectedPartyRowIndex = SelectedPartyRowIndex;
			int candidate = Math.Clamp(SelectedPartyRowIndex + delta, 0, PartyRows.Count - 1);
			SelectedPartyRowIndex = FindNextSelectablePartyIndex(candidate, delta);
			if (SelectedPartyRowIndex != selectedPartyRowIndex)
			{
				_audioService.Play("guide-hover");
			}
		}
	}

	private int FindNextSelectablePartyIndex(int candidate, int direction)
	{
		if (PartyRows.Count == 0)
		{
			return 0;
		}
		int num = ((direction >= 0) ? 1 : (-1));
		for (int i = Math.Clamp(candidate, 0, PartyRows.Count - 1); i >= 0 && i < PartyRows.Count; i += num)
		{
			if (PartyRows[i].IsSelectable)
			{
				return i;
			}
		}
		if (SelectedPartyRowIndex < 0 || SelectedPartyRowIndex >= PartyRows.Count)
		{
			return 0;
		}
		return SelectedPartyRowIndex;
	}

	private void ActivateSelectedPartyRow()
	{
		if (SelectedPartyRowIndex < 0 || SelectedPartyRowIndex >= PartyRows.Count)
		{
			return;
		}
		GuidePartyRowItem guidePartyRowItem = PartyRows[SelectedPartyRowIndex];
		if (guidePartyRowItem.IsSelectable)
		{
			_audioService.Play("guide-select");
			if (guidePartyRowItem.RowKind == "Invite")
			{
				OpenFriendSearch(GuideScreen.Party);
			}
			else
			{
				StatusText = ((guidePartyRowItem.RowKind == "Member") ? (guidePartyRowItem.Title + " is in party") : string.Empty);
			}
		}
	}

	private void LeaveParty()
	{
		_partyRoster.Clear();
		_isUsingDiscordPartyData = false;
		_partyStatusMessage = string.Empty;
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
		GuideFriendListItem item = FriendsListItems[SelectedFriendListIndex];
		_audioService.Play("guide-select");
		if (item.IsAddFriend)
		{
			OpenFriendSearch();
			return;
		}
		SocialFriend socialFriend = _socialFriends.FirstOrDefault((SocialFriend profile) => string.Equals(profile.Id, item.FriendId, StringComparison.OrdinalIgnoreCase)) ?? _socialFriends.FirstOrDefault((SocialFriend profile) => string.Equals(profile.DisplayName, item.Gamertag, StringComparison.OrdinalIgnoreCase));
		if (socialFriend != null)
		{
			_returnToSearchOnProfileBack = false;
			_profileReturnScreen = GuideScreen.FriendsList;
			SetActiveFriend(socialFriend);
			BuildFriendProfileActions();
			SetScreen(GuideScreen.FriendProfile);
			SelectedFriendProfileActionIndex = 0;
		}
	}

	private void OpenFriendSearch(GuideScreen returnScreen = GuideScreen.FriendsList)
	{
		if (CanUseFriendsOverlay())
		{
			_friendSearchMode = FriendSearchMode.AddFriend;
			_friendSearchReturnScreen = returnScreen;
			FriendSearchQuery = string.Empty;
			_friendSearchCursorIndex = 0;
			_isSearchCapsEnabled = false;
			_searchKeyboardLayout = SearchKeyboardLayout.Default;
			BuildSearchKeys();
			SearchSymbolsKey.Label = SearchSymbolsButtonText;
			SearchAccentsKey.Label = SearchAccentsButtonText;
			OnPropertyChanged("FriendSearchHeaderText");
			OnPropertyChanged("FriendSearchInstructionText");
			SetScreen(GuideScreen.FriendSearch);
			SelectedSearchKeyIndex = 0;
			StatusText = string.Empty;
		}
	}

	private void BuildSearchKeys()
	{
		SearchKeys.Clear();
		foreach (string label in GetActiveSearchKeyboardLabels().Concat(_searchActionLabels))
		{
			bool flag;
			switch (label)
			{
			case "Caps":
			case "Back":
			case "Space":
			case "Done":
				flag = true;
				break;
			default:
				flag = false;
				break;
			}
			bool isWide = flag;
			SearchKeys.Add(new GuideKeyboardKeyItem(label, delegate
			{
				HandleSearchKey(label);
			}, isWide));
		}
		RefreshSearchKeySelection();
		OnPropertyChanged("SearchMainKeys");
		OnPropertyChanged("SearchActionKeys");
		OnPropertyChanged("SearchCapsKey");
		OnPropertyChanged("SearchBackspaceKey");
		OnPropertyChanged("SearchSpaceKey");
		OnPropertyChanged("SearchDoneKey");
		OnPropertyChanged("SearchSymbolsButtonText");
		OnPropertyChanged("SearchAccentsButtonText");
	}

	private void HandleSearchKey(string label)
	{
		switch (label)
		{
		case "Caps":
			_isSearchCapsEnabled = !_isSearchCapsEnabled;
			BuildSearchKeys();
			break;
		case "Back":
			BackspaceFriendSearch();
			_audioService.Play("guide-type-backspace");
			break;
		case "Space":
			AppendToFriendSearch(" ");
			break;
		case "Done":
			CompleteFriendSearch();
			break;
		default:
			AppendToFriendSearch(label);
			PlaySearchTypingSound();
			break;
		}
	}

	private void PlaySearchTypingSound()
	{
		_keyboardTypingSoundIndex = _keyboardTypingSoundIndex % 3 + 1;
		_audioService.Play("guide-type-" + _keyboardTypingSoundIndex.ToString(CultureInfo.InvariantCulture));
	}

	private void BackspaceFriendSearch()
	{
		if (!string.IsNullOrEmpty(FriendSearchQuery) && _friendSearchCursorIndex > 0)
		{
			FriendSearchQuery = FriendSearchQuery.Remove(_friendSearchCursorIndex - 1, 1);
			_friendSearchCursorIndex--;
			OnPropertyChanged("FriendSearchDisplayText");
		}
	}

	private void AppendToFriendSearch(string value)
	{
		if (!string.IsNullOrEmpty(value))
		{
			FriendSearchQuery = FriendSearchQuery.Insert(_friendSearchCursorIndex, value);
			_friendSearchCursorIndex += value.Length;
			OnPropertyChanged("FriendSearchDisplayText");
		}
	}

	private void CompleteFriendSearch()
	{
		string gamertag = FriendSearchQuery.Trim();
		if (!string.IsNullOrWhiteSpace(gamertag))
		{
			string friendCode;
			SocialFriend socialFriend = (TryFormatDashFriendCode(gamertag, out friendCode) ? (_socialFriends.FirstOrDefault((SocialFriend friend) => friend.Source == SocialFriendSource.DashX360 && friend.IdentityDetailText.Contains(friendCode, StringComparison.OrdinalIgnoreCase)) ?? CreateDashFriendCodeCandidate(friendCode)) : (_socialFriends.FirstOrDefault((SocialFriend friend) => string.Equals(friend.DisplayName, gamertag, StringComparison.OrdinalIgnoreCase)) ?? _socialIntegrationManager.CreateLocalFriend(gamertag)));
			_returnToSearchOnProfileBack = true;
			_profileReturnScreen = _friendSearchReturnScreen;
			SetActiveFriend(socialFriend);
			BuildFriendProfileActions();
			SetScreen(GuideScreen.FriendProfile);
			SelectedFriendProfileActionIndex = 0;
			StatusText = string.Empty;
		}
	}

	private FriendProfile? FindFriendProfile(string gamertag)
	{
		return _friends.FirstOrDefault((FriendProfile friend) => string.Equals(friend.Gamertag, gamertag, StringComparison.OrdinalIgnoreCase));
	}

	private static bool TryFormatDashFriendCode(string input, out string friendCode)
	{
		string text = new string((input ?? string.Empty).Where(char.IsLetterOrDigit).ToArray()).ToUpperInvariant();
		if (text.Length == 8 && text.StartsWith("DX", StringComparison.OrdinalIgnoreCase))
		{
			friendCode = text.Insert(2, "-");
			return true;
		}
		friendCode = string.Empty;
		return false;
	}

	private static SocialFriend CreateDashFriendCodeCandidate(string friendCode)
	{
		return new SocialFriend
		{
			Id = "dashx360:" + friendCode,
			DisplayName = "DashX360 " + friendCode,
			Source = SocialFriendSource.DashX360,
			AvatarPathOrUrl = ProfileImagePool.GetRandomPoolAvatarPath(),
			IsOnline = false,
			StatusText = "Offline",
			ActivityText = string.Empty,
			GamerscoreText = "0 G",
			ReputationText = "*****",
			ZoneText = "DashX360",
			IdentityDetailText = "DashX360 " + friendCode
		};
	}

	private bool TryMoveSearchKeyHorizontal(int delta)
	{
		if (!IsFriendSearchScreen || SearchKeys.Count == 0)
		{
			return false;
		}
		int selectedSearchKeyIndex = SelectedSearchKeyIndex;
		SelectedSearchKeyIndex = SelectedSearchKeyIndex switch
		{
			40 => (delta > 0) ? 41 : 40, 
			41 => (delta > 0) ? 42 : 40, 
			42 => (delta > 0) ? 43 : 41, 
			43 => (delta < 0) ? 42 : 43, 
			_ => MoveSearchKeyHorizontalFromGrid(delta), 
		};
		if (SelectedSearchKeyIndex != selectedSearchKeyIndex)
		{
			_audioService.Play("guide-hover");
		}
		return true;
	}

	private void MoveSearchKeys(int delta)
	{
		if (SearchKeys.Count != 0)
		{
			int selectedSearchKeyIndex = SelectedSearchKeyIndex;
			SelectedSearchKeyIndex = Math.Clamp(SelectedSearchKeyIndex + delta, 0, SearchKeys.Count - 1);
			if (SelectedSearchKeyIndex != selectedSearchKeyIndex)
			{
				_audioService.Play("guide-hover");
			}
		}
	}

	private void MoveSearchKeyVertical(int delta)
	{
		if (!IsFriendSearchScreen || SearchKeys.Count == 0 || delta == 0)
		{
			return;
		}
		int selectedSearchKeyIndex = SelectedSearchKeyIndex;
		if (SelectedSearchKeyIndex >= 40)
		{
			SelectedSearchKeyIndex = SelectedSearchKeyIndex switch
			{
				40 => 30, 
				41 => 34, 
				42 => 37, 
				43 => 39, 
				_ => SelectedSearchKeyIndex, 
			};
		}
		else
		{
			int num = SelectedSearchKeyIndex / 10;
			int num2 = SelectedSearchKeyIndex % 10;
			if (delta < 0)
			{
				SelectedSearchKeyIndex = ((num == 0) ? SelectedSearchKeyIndex : (SelectedSearchKeyIndex - 10));
			}
			else
			{
				int selectedSearchKeyIndex2 = ((num > 2) ? ((num2 > 4) ? ((num2 > 8) ? 43 : 42) : ((num2 != 0) ? 41 : 40)) : (SelectedSearchKeyIndex + 10));
				SelectedSearchKeyIndex = selectedSearchKeyIndex2;
			}
		}
		if (SelectedSearchKeyIndex != selectedSearchKeyIndex)
		{
			_audioService.Play("guide-hover");
		}
	}

	private int MoveSearchKeyHorizontalFromGrid(int delta)
	{
		int num = SelectedSearchKeyIndex / 10;
		int num2 = Math.Clamp(SelectedSearchKeyIndex % 10 + delta, 0, 9);
		return num * 10 + num2;
	}

	private void RefreshSearchKeySelection()
	{
		for (int i = 0; i < SearchKeys.Count; i++)
		{
			SearchKeys[i].IsSelected = i == SelectedSearchKeyIndex;
		}
	}

	private void ActivateSelectedSearchKey()
	{
		if (SelectedSearchKeyIndex >= 0 && SelectedSearchKeyIndex < SearchKeys.Count)
		{
			ActivateSearchKeyItem(SearchKeys[SelectedSearchKeyIndex]);
		}
	}

	private void ActivateSearchKeyItem(GuideKeyboardKeyItem item, bool flash = true)
	{
		if (flash && item != SearchSymbolsKey && item != SearchAccentsKey)
		{
			FlashSearchKeyAsync(item);
		}
		int num = SearchKeys.IndexOf(item);
		bool num2 = num >= 0 && num < 40;
		bool isBackspaceKey = item == SearchBackspaceKey;
		if (!num2 && !isBackspaceKey)
		{
			_audioService.Play("guide-select");
		}
		item.Action();
	}

	private static async Task FlashSearchKeyAsync(GuideKeyboardKeyItem item)
	{
		int version = ++item.PressVisualVersion;
		item.IsPressedVisual = true;
		try
		{
			await Task.Delay(220);
		}
		finally
		{
			if (item.PressVisualVersion == version)
			{
				item.IsPressedVisual = false;
			}
		}
	}

	private void SelectAndActivateSearchKey(GuideKeyboardKeyItem item)
	{
		int num = SearchKeys.IndexOf(item);
		if (num >= 0)
		{
			SelectedSearchKeyIndex = num;
		}
		ActivateSearchKeyItem(item);
	}

	private void SetActiveFriend(SocialFriend friend)
	{
		_activeSocialFriend = friend;
		_activeFriendProfile = BuildFriendProfileSnapshot(friend);
		OnPropertyChanged("ActiveFriendGamertag");
		OnPropertyChanged("ActiveFriendPicturePath");
		OnPropertyChanged("ActiveFriendGamerscore");
		OnPropertyChanged("ActiveFriendReputation");
		OnPropertyChanged("ActiveFriendZone");
		OnPropertyChanged("ActiveFriendStatus");
		OnPropertyChanged("ActiveFriendCountry");
		OnPropertyChanged("ActiveFriendSourceLabel");
		OnPropertyChanged("ActiveFriendGameTitle");
		OnPropertyChanged("ActiveFriendGameIconPath");
	}

	private void BuildFriendProfileActions()
	{
		FriendProfileActions.Clear();
		SocialFriendSource? socialFriendSource = _activeSocialFriend?.Source;
		int num;
		if (socialFriendSource != SocialFriendSource.Local)
		{
			num = ((socialFriendSource == SocialFriendSource.DashX360) ? 1 : 0);
			if (num == 0)
			{
				goto IL_0078;
			}
		}
		else
		{
			num = 1;
		}
		if (_activeSocialFriend == null)
		{
			goto IL_0078;
		}
		int num2 = (_friends.Any((FriendProfile friend) => string.Equals(friend.Gamertag, _activeSocialFriend.DisplayName, StringComparison.OrdinalIgnoreCase)) ? 1 : 0);
		goto IL_0079;
		IL_0078:
		num2 = 0;
		goto IL_0079;
		IL_0079:
		bool flag2 = (byte)num2 != 0;
		bool isSteamFriend = socialFriendSource == SocialFriendSource.Steam;
		string title = ((flag2 || isSteamFriend) ? "Remove Friend" : "Send Friend Request");
		Action action = ((num == 0) ? new Action(DoNothing) : (flag2 ? new Action(RemoveFriend) : new Action(SendFriendRequest)));
		FriendProfileActions.Add(new GuideMenuItem(title, string.Empty, action));
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
		if (FriendProfileActions.Count != 0)
		{
			int selectedFriendProfileActionIndex = SelectedFriendProfileActionIndex;
			SelectedFriendProfileActionIndex = Math.Clamp(SelectedFriendProfileActionIndex + delta, 0, FriendProfileActions.Count - 1);
			if (SelectedFriendProfileActionIndex != selectedFriendProfileActionIndex)
			{
				_audioService.Play("guide-hover");
			}
		}
	}

	private void ActivateSelectedFriendProfileAction()
	{
		if (SelectedFriendProfileActionIndex >= 0 && SelectedFriendProfileActionIndex < FriendProfileActions.Count)
		{
			_audioService.Play("guide-select");
			FriendProfileActions[SelectedFriendProfileActionIndex].Action();
		}
	}

	private void SelectAndActivateFriendProfileAction(GuideMenuItem item)
	{
		int num = FriendProfileActions.IndexOf(item);
		if (num >= 0)
		{
			SelectedFriendProfileActionIndex = num;
			ActivateSelectedFriendProfileAction();
		}
	}

	private void SendFriendRequest()
	{
		if (_activeSocialFriend == null)
		{
			return;
		}
		if (_activeSocialFriend.Source != SocialFriendSource.Local && _activeSocialFriend.Source != SocialFriendSource.DashX360)
		{
			StatusText = _activeSocialFriend.DisplayName + " is managed through " + SocialIntegrationManager.GetSourceLabel(_activeSocialFriend);
			return;
		}
		if (_friends.Any((FriendProfile friend) => string.Equals(friend.Gamertag, _activeSocialFriend.DisplayName, StringComparison.OrdinalIgnoreCase)))
		{
			StatusText = _activeSocialFriend.DisplayName + " is already on your friends list";
			return;
		}
		FriendProfile friendProfile = NormalizeOfflineFriend(LocalSocialIntegrationService.MapToLocalFriend(_activeSocialFriend));
		_friends.Add(friendProfile);
		if (!_socialFriends.Any((SocialFriend friend) => string.Equals(friend.Id, _activeSocialFriend.Id, StringComparison.OrdinalIgnoreCase)))
		{
			_socialFriends.Add(LocalSocialIntegrationService.MapFromLocalFriend(friendProfile));
			_socialFriends.Sort((SocialFriend left, SocialFriend right) => CompareSocialFriends(left, right));
		}
		QueueFriendsSave();
		BuildFriendsListItems();
		BuildFriendProfileActions();
		StatusText = _activeSocialFriend.DisplayName + " added to friends";
	}

	private void RemoveFriend()
	{
		if (_activeSocialFriend == null)
		{
			return;
		}
		if (_activeSocialFriend.Source != SocialFriendSource.Local && _activeSocialFriend.Source != SocialFriendSource.DashX360)
		{
			StatusText = _activeSocialFriend.DisplayName + " is managed through " + SocialIntegrationManager.GetSourceLabel(_activeSocialFriend);
			return;
		}
		FriendProfile friendProfile = _friends.FirstOrDefault((FriendProfile existing) => string.Equals(existing.Gamertag, _activeSocialFriend.DisplayName, StringComparison.OrdinalIgnoreCase));
		if (friendProfile == null)
		{
			StatusText = _activeSocialFriend.DisplayName + " is not on your friends list";
			return;
		}
		_friends.Remove(friendProfile);
		_socialFriends.RemoveAll((SocialFriend existing) => string.Equals(existing.Id, _activeSocialFriend.Id, StringComparison.OrdinalIgnoreCase) || ((existing.Source == SocialFriendSource.Local || existing.Source == SocialFriendSource.DashX360) && string.Equals(existing.DisplayName, _activeSocialFriend.DisplayName, StringComparison.OrdinalIgnoreCase)));
		QueueFriendsSave();
		BuildFriendsListItems();
		BuildFriendProfileActions();
		SelectedFriendProfileActionIndex = 0;
		StatusText = _activeSocialFriend.DisplayName + " removed from friends";
	}

	private async void InviteActiveFriendToParty()
	{
		if (_activeSocialFriend == null)
		{
			return;
		}
		if (_isInvitingPartyFriend)
		{
			StatusText = "Inviting " + _activeSocialFriend.DisplayName + " to party...";
			return;
		}
		_isInvitingPartyFriend = true;
		_lastPartyInviteFriendId = _activeSocialFriend.Id;
		_lastPartyInviteAt = DateTimeOffset.UtcNow;
		try
		{
			StatusText = "Inviting " + _activeSocialFriend.DisplayName + " to party...";
			SocialPartyInviteResult socialPartyInviteResult = await _socialIntegrationManager.InviteToPartyAsync(_dashboard.Profile, _activeSocialFriend, _dashboard.Settings.DiscordConnectionState).ConfigureAwait(continueOnCapturedContext: true);
			BuildPartyRows();
			RefreshPartySnapshotAsync(forceRebuild: true);
			if (!string.IsNullOrWhiteSpace(socialPartyInviteResult.PopupMessage))
			{
				ShowSocialMessage(socialPartyInviteResult.PopupMessage);
				StatusText = socialPartyInviteResult.PopupMessage;
			}
			else
			{
				StatusText = "Invite Sent";
				_dashboard.ShowDashboardToast("Invite Sent");
				TriggerPartyRefreshBurstAsync();
			}
		}
		catch (Exception)
		{
			ShowSocialMessage("Party invite failed.");
			StatusText = "Party invite failed.";
		}
		finally
		{
			_isInvitingPartyFriend = false;
		}
	}

	private async Task TriggerPartyRefreshBurstAsync()
	{
		for (int attempt = 0; attempt < 6; attempt++)
		{
			try
			{
				await Task.Delay(750).ConfigureAwait(continueOnCapturedContext: true);
				await RefreshPartySnapshotAsync(attempt == 0).ConfigureAwait(continueOnCapturedContext: true);
			}
			catch
			{
				break;
			}
		}
	}

	private void AddToPartyRoster(SocialFriend friend)
	{
		if (!_partyRoster.Any((SocialFriend existing) => string.Equals(existing.Id, friend.Id, StringComparison.OrdinalIgnoreCase)))
		{
			_partyRoster.Add(new SocialFriend
			{
				Id = friend.Id,
				DisplayName = friend.DisplayName,
				Source = friend.Source,
				AvatarPathOrUrl = friend.AvatarPathOrUrl,
				IsOnline = friend.IsOnline,
				StatusText = friend.StatusText,
				ActivityText = (string.IsNullOrWhiteSpace(friend.ActivityText) ? "Joined Party" : friend.ActivityText),
				ActivityAppId = friend.ActivityAppId,
				GamerscoreText = friend.GamerscoreText,
				ReputationText = friend.ReputationText,
				ZoneText = friend.ZoneText,
				IdentityDetailText = friend.IdentityDetailText,
				IsPartyHost = false,
				ShowVoiceIndicator = friend.ShowVoiceIndicator
			});
		}
	}

	public void AcceptDashPartyInvite(SocialFriend friend)
	{
		DismissSocialMessage();
		AddToPartyRoster(friend);
		BuildPartyRows();
		OpenParty();
		StatusText = "Joined " + friend.DisplayName + "'s party";
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
	{
		if (friend.Source != SocialFriendSource.Local)
		{
			return new FriendProfile
			{
				Gamertag = friend.DisplayName,
				RealName = SocialIntegrationManager.GetSourceLabel(friend),
				GamerPicturePath = friend.AvatarPathOrUrl,
				Gamerscore = ParseGamerscore(GetSocialFriendGamerscoreText(friend)),
				Reputation = NormalizeReputation(GetSocialFriendReputationText(friend)),
				Zone = GetSocialFriendZoneText(friend),
				Status = GetActiveFriendProfileStatus(friend),
				Country = SocialIntegrationManager.GetSourceLabel(friend)
			};
		}
		return NormalizeOfflineFriend(LocalSocialIntegrationService.MapToLocalFriend(friend));
	}

	private string ResolveActiveFriendGameIconPath()
	{
		SocialFriendSource? socialFriendSource = _activeSocialFriend?.Source;
		if (!socialFriendSource.HasValue || socialFriendSource != SocialFriendSource.Steam)
		{
			return string.Empty;
		}
		string appId = _activeSocialFriend.ActivityAppId;
		string activity = _activeSocialFriend.ActivityText;
		GameMetadata gameMetadata = _dashboard.Games.Select((GameCardViewModel item) => item.Game).FirstOrDefault((GameMetadata candidate) => string.Equals(candidate.LaunchType, "Steam", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(appId) && string.Equals(candidate.SteamAppId, appId, StringComparison.OrdinalIgnoreCase)) ?? _dashboard.Games.Select((GameCardViewModel item) => item.Game).FirstOrDefault((GameMetadata candidate) => string.Equals(candidate.LaunchType, "Steam", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(activity) && string.Equals(candidate.Title, activity, StringComparison.CurrentCultureIgnoreCase));
		return gameMetadata?.CoverArtPath ?? gameMetadata?.HeaderImagePath ?? gameMetadata?.LogoImagePath ?? string.Empty;
	}

	private static string GetActiveFriendProfileStatus(SocialFriend friend)
	{
		if (friend.Source == SocialFriendSource.Steam)
		{
			if (!string.IsNullOrWhiteSpace(friend.StatusText))
			{
				return friend.StatusText;
			}
			if (!friend.IsOnline)
			{
				return "Offline";
			}
			return "Online";
		}
		return SocialIntegrationManager.GetFriendActivityLabel(friend);
	}

	private static string GetSocialFriendGamerscoreText(SocialFriend friend)
	{
		if (!string.IsNullOrWhiteSpace(friend.GamerscoreText))
		{
			return friend.GamerscoreText;
		}
		return BuildSocialFriendProfileStats(friend).GamerscoreText;
	}

	private static string GetSocialFriendReputationText(SocialFriend friend)
	{
		if (!string.IsNullOrWhiteSpace(friend.ReputationText))
		{
			return friend.ReputationText;
		}
		return BuildSocialFriendProfileStats(friend).ReputationText;
	}

	private static string GetSocialFriendZoneText(SocialFriend friend)
	{
		if (friend.Source != SocialFriendSource.Steam && !string.IsNullOrWhiteSpace(friend.ZoneText))
		{
			return friend.ZoneText;
		}
		if (friend.Source == SocialFriendSource.Steam && !string.IsNullOrWhiteSpace(friend.ZoneText) && !string.Equals(friend.ZoneText, "Steam", StringComparison.OrdinalIgnoreCase))
		{
			return friend.ZoneText;
		}
		return BuildSocialFriendProfileStats(friend).ZoneText;
	}

	private static (string GamerscoreText, string ReputationText, string ZoneText) BuildSocialFriendProfileStats(SocialFriend friend)
	{
		Random random = new Random((string.IsNullOrWhiteSpace(friend.Id) ? friend.DisplayName : friend.Id).Aggregate(23, (int current, char character) => current * 31 + character));
		string[] array = new string[4] { "Recreation", "Family", "Pro", "Underground" };
		int value = random.Next(2500, 125000);
		int num = random.Next(3, 6);
		string item = new string('★', num) + new string('☆', 5 - num);
		return (GamerscoreText: $"{value:N0} G", ReputationText: item, ZoneText: array[random.Next(array.Length)]);
	}

	private async Task LoadFriendsAsync(bool showPopup)
	{
		_friends.Clear();
		List<FriendProfile> list = (await _friendsService.LoadAsync().ConfigureAwait(continueOnCapturedContext: true)).Select(NormalizeOfflineFriend).ToList();
		bool flag = false;
		foreach (FriendProfile item in list)
		{
			string text = ProfileImagePool.ResolveAssignedAvatarPath(item.GamerPicturePath);
			if (!string.Equals(item.GamerPicturePath, text, StringComparison.OrdinalIgnoreCase))
			{
				item.GamerPicturePath = text;
				flag = true;
			}
		}
		_friends.AddRange(list);
		SortFriendsByStatus();
		if (flag)
		{
			QueueFriendsSave();
		}
		SocialFriendsLoadResult socialFriendsLoadResult = await _socialIntegrationManager.LoadFriendsAsync(_dashboard.Settings.SocialIntegrationMode, _dashboard.Settings.DiscordConnectionState, _dashboard.Profile).ConfigureAwait(continueOnCapturedContext: true);
		_socialFriends.Clear();
		_socialFriends.AddRange(SortSocialFriends(socialFriendsLoadResult.Friends));
		if (showPopup && !string.IsNullOrWhiteSpace(socialFriendsLoadResult.PopupMessage))
		{
			ShowSocialMessage(socialFriendsLoadResult.PopupMessage);
		}
		else
		{
			DismissSocialMessage();
		}
		OnPropertyChanged("FriendsHeaderText");
		OnPropertyChanged("FriendsTotalCountText");
		OnPropertyChanged("FriendsSelectionCountText");
		OnPropertyChanged("PartyFriendCountText");
	}

	private void QueueFriendsSave()
	{
		SaveFriendsAsync();
	}

	private async Task SaveFriendsAsync()
	{
		List<FriendProfile> snapshot = _friends.Select(NormalizeOfflineFriend).ToList();
		await _friendsSaveLock.WaitAsync().ConfigureAwait(continueOnCapturedContext: false);
		try
		{
			await _friendsService.SaveAsync(snapshot).ConfigureAwait(continueOnCapturedContext: false);
		}
		catch
		{
		}
		finally
		{
			_friendsSaveLock.Release();
		}
	}

	private void SortFriendsByStatus()
	{
		List<FriendProfile> collection = SortFriends(_friends).ToList();
		_friends.Clear();
		_friends.AddRange(collection);
	}

	private static IEnumerable<SocialFriend> SortSocialFriends(IEnumerable<SocialFriend> friends)
	{
		return friends.OrderBy((SocialFriend friend) => (!friend.IsOnline) ? 1 : 0).ThenBy<SocialFriend, string>((SocialFriend friend) => friend.DisplayName, StringComparer.CurrentCultureIgnoreCase).ToList();
	}

	private static int CompareSocialFriends(SocialFriend left, SocialFriend right)
	{
		int num = ((!left.IsOnline) ? 1 : 0).CompareTo((!right.IsOnline) ? 1 : 0);
		if (num != 0)
		{
			return num;
		}
		return StringComparer.CurrentCultureIgnoreCase.Compare(left.DisplayName, right.DisplayName);
	}

	private static IEnumerable<FriendProfile> SortFriends(IEnumerable<FriendProfile> friends)
	{
		return friends.OrderBy((FriendProfile friend) => GetStatusSortOrder(friend.Status)).ThenBy<FriendProfile, string>((FriendProfile friend) => friend.Gamertag, StringComparer.CurrentCultureIgnoreCase);
	}

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
	{
		if (!status.Contains("Away", StringComparison.OrdinalIgnoreCase))
		{
			if (status.Contains("Online", StringComparison.OrdinalIgnoreCase))
			{
				return !status.Contains("Last online", StringComparison.OrdinalIgnoreCase);
			}
			return false;
		}
		return true;
	}

	private FriendProfile CreateOfflineFriend(string gamertag)
	{
		Random random = new Random(gamertag.ToLowerInvariant().Aggregate(17, (int current, char character) => current * 31 + character));
		string[] array = new string[4]
		{
			"Offline",
			"Away",
			$"Last online {random.Next(8, 46)} minutes ago",
			$"Last online {random.Next(1, 12)} hours ago"
		};
		string[] array2 = new string[4] { "Recreation", "Family", "Pro", "Underground" };
		string[] array3 = new string[4] { "United States", "Canada", "United Kingdom", "Offline" };
		string reputation = BuildReputation(random.Next(3, 6));
		return new FriendProfile
		{
			Gamertag = gamertag,
			RealName = gamertag + " profile",
			GamerPicturePath = GamerPicturePath,
			Gamerscore = random.Next(2500, 95000),
			Reputation = reputation,
			Zone = array2[random.Next(array2.Length)],
			Status = NormalizeOfflineStatus(array[random.Next(array.Length)]),
			Country = array3[random.Next(array3.Length)]
		};
	}

	private static int ParseGamerscore(string gamerscoreText)
	{
		if (string.IsNullOrWhiteSpace(gamerscoreText))
		{
			return 0;
		}
		if (!int.TryParse(new string(gamerscoreText.Where(char.IsDigit).ToArray()), out var result))
		{
			return 0;
		}
		return result;
	}

	private static FriendProfile CloneFriend(FriendProfile friend)
	{
		return new FriendProfile
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
	}

	private static FriendProfile NormalizeOfflineFriend(FriendProfile friend)
	{
		FriendProfile friendProfile = CloneFriend(friend);
		friendProfile.Reputation = NormalizeReputation(friendProfile.Reputation);
		friendProfile.Status = NormalizeOfflineStatus(friendProfile.Status);
		friendProfile.Zone = (string.IsNullOrWhiteSpace(friendProfile.Zone) ? "Recreation" : friendProfile.Zone);
		friendProfile.Country = NormalizeOfflineCountry(friendProfile.Country);
		return friendProfile;
	}

	private static string NormalizeOfflineStatus(string? status)
	{
		if (string.IsNullOrWhiteSpace(status))
		{
			return "Offline";
		}
		if (status.Contains("Last online", StringComparison.OrdinalIgnoreCase) || status.Contains("Away", StringComparison.OrdinalIgnoreCase) || status.Contains("Offline", StringComparison.OrdinalIgnoreCase))
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
		int num = reputation.Count((char character) => (character == '*' || character == '★') ? true : false);
		if (num == 0 && reputation.Contains("â", StringComparison.Ordinal))
		{
			num = 5;
		}
		return BuildReputation((num == 0) ? 5 : Math.Clamp(num, 1, 5));
	}

	private static string BuildReputation(int filledStars)
	{
		return new string('★', Math.Clamp(filledStars, 0, 5)) + new string('☆', Math.Clamp(5 - filledStars, 0, 5));
	}

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
	{
		IEnumerable<string> labels = _searchKeyboardLayout switch
		{
			SearchKeyboardLayout.Symbols => _symbolKeyboardLabels, 
			SearchKeyboardLayout.Accents => _accentKeyboardLabels, 
			_ => _defaultKeyboardLabels, 
		};
		if (!_isSearchCapsEnabled)
		{
			return labels;
		}
		return labels.Select(ApplySearchCapsToLabel);
	}

	private static string ApplySearchCapsToLabel(string label)
	{
		if (label.Length == 1 && label.Any(char.IsLetter))
		{
			return label.ToUpperInvariant();
		}
		return label;
	}

	private void SetSearchKeyboardLayout(SearchKeyboardLayout layout)
	{
		if (_searchKeyboardLayout != layout)
		{
			_searchKeyboardLayout = layout;
			BuildSearchKeys();
		}
	}

	private void SetScreen(GuideScreen screen)
	{
		GuideScreen screen2 = _screen;
		_screen = screen;
		if (IsPartyFriendsBladeSwitch(screen2, screen))
		{
			PlayBladeSwitchSound();
		}
		OnPropertyChanged("IsGuideBladeScreen");
		OnPropertyChanged("IsMainGuideScreen");
		OnPropertyChanged("IsGuideMusicPickerScreen");
		OnPropertyChanged("IsFriendsListScreen");
		OnPropertyChanged("IsPartyScreen");
		OnPropertyChanged("IsFriendSearchScreen");
		OnPropertyChanged("IsFriendProfileScreen");
		OnPropertyChanged("IsAchievementsScreen");
		OnPropertyChanged("IsFriendOverlayScreen");
		OnPropertyChanged("IsMediaTab");
		OnPropertyChanged("IsMediaSongRowFocused");
		OnPropertyChanged("ShowStatusText");
		OnPropertyChanged("FooterPromptTop");
		OnPropertyChanged("FooterXActionText");
		OnPropertyChanged("FooterYActionText");
		OnPropertyChanged("ShowFooterXAction");
		OnPropertyChanged("ShowFooterYAction");
		OnPropertyChanged("FriendsTotalCountText");
		OnPropertyChanged("FriendsMessageCountText");
		OnPropertyChanged("FriendsGameInviteCountText");
		OnPropertyChanged("FriendsSelectionCountText");
		OnPropertyChanged("PartyHeaderText");
		OnPropertyChanged("PartyFriendCountText");
		OnPropertyChanged("PartyMessageCountText");
		OnPropertyChanged("PartyGameCountText");
		OnPropertyChanged("PartyMemberCount");
		OnPropertyChanged("IsAchievementGameList");
		OnPropertyChanged("IsAchievementDetail");
		OnPropertyChanged("AchievementsGameTitle");
		OnPropertyChanged("AchievementsCountText");
		OnPropertyChanged("AchievementsUnlockedText");
		OnPropertyChanged("SelectedAchievementTitle");
		OnPropertyChanged("SelectedAchievementDescription");
		OnPropertyChanged("SelectedAchievementStatusText");
		OnPropertyChanged("SelectedAchievementDateText");
		if (_screen != GuideScreen.MainMenu)
		{
			CloseMediaSubmenu();
		}
		UpdatePartyRefreshState();
	}

	public bool ConsumeCommunityOverlayOpenFromMainGuide()
	{
		bool isOpeningCommunityOverlayFromMainGuide = _isOpeningCommunityOverlayFromMainGuide;
		_isOpeningCommunityOverlayFromMainGuide = false;
		return isOpeningCommunityOverlayFromMainGuide;
	}

	public bool ConsumeAchievementsOverlayOpenFromMainGuide()
	{
		bool isOpeningAchievementsOverlayFromMainGuide = _isOpeningAchievementsOverlayFromMainGuide;
		_isOpeningAchievementsOverlayFromMainGuide = false;
		return isOpeningAchievementsOverlayFromMainGuide;
	}

	public bool ConsumeGuideMusicPickerOpenFromMainGuide()
	{
		bool isOpeningGuideMusicPickerFromMainGuide = _isOpeningGuideMusicPickerFromMainGuide;
		_isOpeningGuideMusicPickerFromMainGuide = false;
		return isOpeningGuideMusicPickerFromMainGuide;
	}

	private static bool IsPartyFriendsBladeSwitch(GuideScreen from, GuideScreen to)
	{
		if (from != GuideScreen.FriendsList || to != GuideScreen.Party)
		{
			if (from == GuideScreen.Party)
			{
				return to == GuideScreen.FriendsList;
			}
			return false;
		}
		return true;
	}

	private static bool IsCommunityOverlayMenuItem(GuideMenuItem item)
	{
		if (!string.Equals(item.Title, "Friends", StringComparison.OrdinalIgnoreCase))
		{
			return string.Equals(item.Title, "Party", StringComparison.OrdinalIgnoreCase);
		}
		return true;
	}

	private static void DoNothing()
	{
	}

	private bool CanUseFriendsOverlay()
	{
		return true;
	}

	private void OpenMusic()
	{
		OpenGuideMusicPicker();
	}

	private void OpenMusicPlayer()
	{
		PrepareGuideReturnToDashboard();
		CloseGuide();
		if (_dashboard.OpenMusicNowPlayingFromGuideCommand.CanExecute(null))
		{
			_dashboard.OpenMusicNowPlayingFromGuideCommand.Execute(null);
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
			_mainWindow.WindowState = (_dashboard.Settings.StartFullscreen ? WindowState.Maximized : WindowState.Normal);
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
