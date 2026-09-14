using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Forms;
using System.Windows.Forms.Integration;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Media.Media3D;
using System.Windows.Shapes;
using System.Windows.Threading;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;
using XboxMetroLauncher.Views.Tabs;
using XboxMetroLauncher.Controls;
using XboxMetroLauncher.Input;
using XboxMetroLauncher.Models;
using XboxMetroLauncher.Services;
using XboxMetroLauncher.Themes;
using XboxMetroLauncher.Utilities;
using XboxMetroLauncher.ViewModels;
using XboxMetroLauncher.ViewModels.Tabs;
using XboxMetroLauncher.Views;

namespace XboxMetroLauncher;

public partial class MainWindow : Window
{
	private readonly record struct FocusCandidate(System.Windows.Controls.Button Button, Rect Bounds);

	private readonly record struct OverlayFocusCandidate(System.Windows.Controls.Control Control, Rect Bounds);

	private readonly DashboardViewModel _viewModel;

	private readonly ControllerInputService _controllerInputService;

	private readonly GlobalHotkeyService _guideHotkeyService;

	private readonly IAudioService _audioService;

	private readonly IFriendsService _friendsService;

	private readonly SocialIntegrationManager _socialIntegrationManager;

	private readonly ISteamCommunityService _steamCommunityService;

	private readonly DispatcherTimer _clockTimer;

	private readonly DispatcherTimer _performanceDebugTimer;

	private readonly Dictionary<string, System.Windows.Controls.Button> _lastFocusedButtonByTab = new Dictionary<string, System.Windows.Controls.Button>();

	private System.Windows.Forms.WebBrowser? _bootBrowser;

	private WebView2? _youtubeTvBrowser;

	private DispatcherTimer? _bootStateTimer;

	private DateTime _bootStartedAt;

	private int _lastTabIndex = 1;

	private const string YouTubeTvUrl = "https://www.youtube.com/tv";

	private const string YouTubeTvUserAgent = "Mozilla/5.0 (SMART-TV; Linux; Tizen 6.0) AppleWebKit/537.36 (KHTML, like Gecko) SamsungBrowser/4.0 TV Safari/537.36";

	private const double FocusZoneLeft = 64.0;

	private const double FocusZoneRight = 1120.0;

	private bool _bootSkipped;

	private bool _startupInitializationComplete;

	private bool _startupSoundPlayed;

	private bool _fakeLoadingStarted;

	private bool _isFakeLoadingActive;

	private bool _isMenuFakeLoadingActive;

	private bool _isMenuTransitionActive;

	private bool _guideWarmupScheduled;

	private bool _startupPrewarmScheduled;

	private bool _bootBrowserCleanupScheduled;

	private bool _youtubeTvNavigationStarted;

	private bool _signInToastSequenceActive;

	private bool _partyInviteToastActive;

	private string _toastActionUri = string.Empty;

	private int _settingsOpenCount;

	private int _gamesOpenCount;

	private int _achievementsOpenCount;

	private int _playedGamesOpenCount;

	private int _musicOpenCount;

	private int _appsOpenCount;

	private bool _isAnimatingTab;

	private bool _isFocusUpdateQueued;

	private int _queuedTabStep;

	private object? _lastRenderedTab;

	private bool _isMouseCursorHiddenForController;

	private bool _isHandlingControllerInput;

	private Point? _lastMousePosition;

	private IGuideWindow? _guideWindow;

	private GuideViewModel? _guideViewModel;

	private UIElement? _guideReturnFocusElement;

	private bool _restoreFocusAfterGuideClose;

	private nint _guideReturnWindowHandle;

	private bool _guideRestoreExternalWindow;

	private string _appliedThemeBackgroundPath = string.Empty;

	private string _appliedBingBackgroundPath = string.Empty;

	private FrameworkElement? _activeSystemSettingsPanel;

	private int _lastGameDetailsTabAnimationIndex;

	private int _libraryFocusRequestId;

	private static readonly string BingBackgroundRelativePath = System.IO.Path.Combine("Assets", "References", "penguin_bing_background.png");

	private static readonly string PerformanceDebugLogPath = System.IO.Path.Combine(AppPaths.LogsFolder, "performance-debug.log");

	[DllImport("user32.dll")]
	private static extern nint GetForegroundWindow();

	[DllImport("psapi.dll")]
	[return: MarshalAs(UnmanagedType.Bool)]
	private static extern bool EmptyWorkingSet(nint hProcess);

	[DllImport("user32.dll")]
	[return: MarshalAs(UnmanagedType.Bool)]
	private static extern bool SetForegroundWindow(nint hWnd);

	[DllImport("user32.dll")]
	[return: MarshalAs(UnmanagedType.Bool)]
	private static extern bool BringWindowToTop(nint hWnd);

	[DllImport("user32.dll")]
	[return: MarshalAs(UnmanagedType.Bool)]
	private static extern bool IsWindow(nint hWnd);

	public MainWindow()
	{
		//IL_01a4: Unknown result type (might be due to invalid IL or missing references)
		//IL_01a9: Unknown result type (might be due to invalid IL or missing references)
		//IL_01c2: Expected O, but got Unknown
		//IL_01da: Unknown result type (might be due to invalid IL or missing references)
		//IL_01df: Unknown result type (might be due to invalid IL or missing references)
		//IL_01f8: Expected O, but got Unknown
		InitializeComponent();
		base.StateChanged += Window_OnStateChanged;
		string writableAppDataPath = GetWritableAppDataPath();
		if (!AppPaths.IsPartyLinkTestInstance)
		{
			MigrateLegacyUserData(writableAppDataPath);
		}
		JsonStore jsonStore = new JsonStore(writableAppDataPath);
		_friendsService = new FriendsService(jsonStore);
		ISettingsService settingsService = new SettingsService(jsonStore);
		LocalSocialIntegrationService localService = new LocalSocialIntegrationService(_friendsService);
		_steamCommunityService = new SteamCommunityService();
		IDashPartyLinkService dashPartyLinkService = new DashPartyLinkService(jsonStore);
		_socialIntegrationManager = new SocialIntegrationManager(_friendsService, localService, _steamCommunityService, dashPartyLinkService);
		IProfileService profileService = new ProfileService(jsonStore);
		IGameLibraryService libraryService = new JsonGameLibraryService(jsonStore);
		IImportExportService importExportService = new ImportExportService(libraryService, profileService, settingsService, writableAppDataPath);
		IRunningGameService runningGameService = new RunningGameService();
		DashboardViewModel viewModel = null;
		_viewModel = new DashboardViewModel(audioService: _audioService = new AudioService(() => viewModel?.Settings.PlayUiSounds ?? true, AudioHost, () => viewModel?.Settings.AudioOutputDeviceName ?? "Default", () => viewModel?.Settings.DashboardVolume ?? 1.0, () => viewModel?.Settings.DashboardSounds ?? "Metro"), libraryService: libraryService, launchService: new GameLaunchService(), searchService: new SearchService(), settingsService: settingsService, profileService: profileService, filePickerService: new WindowsFilePickerService(), importExportService: importExportService, steamLibraryScannerService: new SteamLibraryScannerService(), steamCommunityService: _steamCommunityService, themeService: new ThemeService(), startupRegistrationService: new RegistryStartupRegistrationService(), socialIntegrationManager: _socialIntegrationManager, runningGameService: runningGameService);
		viewModel = _viewModel;
		BladesArtworkHost.NavigationFrame += (from, to, progress, seconds, boundary) =>
		{
			BladesCenterHost.ApplyNavigationFrame(from, to, progress, boundary, BladesArtworkHost.NavigationOutgoingClip);
			BladesContentCanvas.Opacity = string.Equals(to, _viewModel.ActiveBladesBlade?.Key, StringComparison.OrdinalIgnoreCase)
				? Math.Clamp((seconds - 0.200) / 0.133, 0.0, 1.0) : 0.0;
			BladesContentCanvas.IsHitTestVisible = false;
		};
		BladesArtworkHost.NavigationCompleted += () =>
		{
			BladesCenterHost.FinishNavigation();
			BladesContentCanvas.Opacity = 1.0;
			BladesContentCanvas.IsHitTestVisible = true;
		};
		base.DataContext = _viewModel;
		_lastRenderedTab = _viewModel.CurrentTab;
		_controllerInputService = new ControllerInputService(HandleControllerInputAction, () => _viewModel.Settings.EnableControllerInput);
		_guideHotkeyService = new GlobalHotkeyService();
		_guideHotkeyService.HotkeyPressed += delegate
		{
			((DispatcherObject)this).Dispatcher.BeginInvoke((Delegate)(Action)delegate
			{
				HandleInputAction(DashboardInputAction.Guide);
			}, (DispatcherPriority)10, Array.Empty<object>());
		};
		_clockTimer = new DispatcherTimer
		{
			Interval = TimeSpan.FromSeconds(6.0)
		};
		_clockTimer.Tick += delegate
		{
			_viewModel.UpdateClock();
		};
		_performanceDebugTimer = new DispatcherTimer
		{
			Interval = TimeSpan.FromSeconds(20.0)
		};
		_performanceDebugTimer.Tick += delegate
		{
			WritePerformanceDebugReport();
		};
		_viewModel.PropertyChanged += ViewModel_OnPropertyChanged;
		_viewModel.FriendsOverlayRequested += ViewModel_OnFriendsOverlayRequested;
		_viewModel.BladesSubmenuTransition = TransitionBladesSubmenuAsync;
		_viewModel.BladesMusicRequested += ViewModel_OnBladesMusicRequested;
		_viewModel.PartyOverlayRequested += ViewModel_OnPartyOverlayRequested;
		_viewModel.AchievementOverlayRequested += ViewModel_OnAchievementOverlayRequested;
		_viewModel.ToastRequested += ViewModel_OnToastRequested;
		ApplyDisplaySettings();
	}

	private static string GetWritableAppDataPath()
	{
		return AppPaths.UserDataFolder;
	}

	private static void MigrateLegacyUserData(string targetRoot)
	{
		if (!Directory.Exists(targetRoot))
		{
			Directory.CreateDirectory(targetRoot);
		}
		if (Directory.EnumerateFiles(targetRoot, "*.json", SearchOption.TopDirectoryOnly).Any())
		{
			return;
		}
		foreach (string item in AppPaths.LegacyDataRoots())
		{
			if (!Directory.Exists(item))
			{
				continue;
			}
			List<string> list = Directory.EnumerateFiles(item, "*.json", SearchOption.TopDirectoryOnly).ToList();
			if (list.Count == 0)
			{
				continue;
			}
			{
				foreach (string item2 in list)
				{
					string text = System.IO.Path.Combine(targetRoot, System.IO.Path.GetFileName(item2));
					if (!File.Exists(text))
					{
						File.Copy(item2, text, overwrite: false);
					}
				}
				break;
			}
		}
	}

	private async void Window_OnLoaded(object sender, RoutedEventArgs e)
	{
		try
		{
			await _viewModel.LoadStartupSettingsAsync();
			_audioService.WarmUp(ShouldRunBladesRunnerStartup() ? "dash_2ndLevelClose" : "startup");
			_viewModel.RefreshAudioOutputDevices();
			_viewModel.Settings.PropertyChanged += Settings_OnPropertyChanged;
			ApplyDisplaySettings();
			_controllerInputService.Start();
			_guideHotkeyService.Register(this);
			_clockTimer.Start();
			StartBootVideo();
			await _viewModel.InitializeAsync(reloadSettings: false);
			UpdateThemeBackgroundVisual(animate: false);
			UpdateBingBackgroundVisual(animate: false);
			ApplyDisplaySettings();
			UpdateAdjacentPreviewSnapshots();
			WritePerformanceDebugReport();
			_startupInitializationComplete = true;
			if (!_viewModel.IsBooting && !StartFakeLoadingIfReady())
			{
				PlayStartupSound();
				ScheduleStartupPrewarm();
				FocusFirstButton();
				_ = RunSignInToastSequenceAsync();
			}
		}
		catch (Exception exception)
		{
			App.LogException(exception, "MainWindow.Window_OnLoaded");
		}
	}

	private void Window_OnClosing(object? sender, CancelEventArgs e)
	{
		base.StateChanged -= Window_OnStateChanged;
		base.Cursor = null;
		Mouse.OverrideCursor = null;
		_viewModel.Settings.PropertyChanged -= Settings_OnPropertyChanged;
		_viewModel.FriendsOverlayRequested -= ViewModel_OnFriendsOverlayRequested;
		_viewModel.BladesMusicRequested -= ViewModel_OnBladesMusicRequested;
		_viewModel.PartyOverlayRequested -= ViewModel_OnPartyOverlayRequested;
		_viewModel.AchievementOverlayRequested -= ViewModel_OnAchievementOverlayRequested;
		_viewModel.ToastRequested -= ViewModel_OnToastRequested;
		_guideWindow?.Close();
		_guideViewModel?.Dispose();
		_guideHotkeyService.Dispose();
		_controllerInputService.Dispose();
		_clockTimer.Stop();
		_performanceDebugTimer.Stop();
		CleanupYouTubeTvBrowser();
		CleanupBootBrowser();
		WritePerformanceDebugReport();
	}

	private void Window_OnStateChanged(object? sender, EventArgs e)
	{
		if (base.WindowState == WindowState.Minimized)
		{
			TrimResourcesForMinimizedDashboard();
		}
		else
		{
			RestoreResourcesAfterMinimizedDashboard();
		}
	}

	private void TrimResourcesForMinimizedDashboard()
	{
		try
		{
			ThemeBackgroundImage.Source = null;
			UltraWideThemeBackgroundImage.Source = null;
			BingBackgroundImage.Source = null;
			UltraWideBingBackgroundImage.Source = null;
			PreviousPreviewImage.Source = null;
			NextPreviewImage.Source = null;
			GameDetailsBackgroundImage.Source = null;
			GameDetailsGalleryImage.Source = null;
			PreviousPreviewLiveHost.Content = null;
			NextPreviewLiveHost.Content = null;
			TransitionLeftHost.Content = null;
			TransitionCenterHost.Content = null;
			TransitionRightHost.Content = null;
			ImageCacheService.ClearDecodedImages();
			_audioService.TrimCachedResources(keepGuideReady: true);
			GC.Collect(2, GCCollectionMode.Forced, blocking: true, compacting: true);
			GC.WaitForPendingFinalizers();
			GC.Collect(2, GCCollectionMode.Forced, blocking: false, compacting: true);
			EmptyWorkingSet(Process.GetCurrentProcess().Handle);
		}
		catch (Exception exception)
		{
			App.LogException(exception, "MainWindow.TrimResourcesForMinimizedDashboard");
		}
	}

	private void RestoreResourcesAfterMinimizedDashboard()
	{
		try
		{
			GameDetailsBackgroundImage.ClearValue(System.Windows.Controls.Image.SourceProperty);
			GameDetailsGalleryImage.ClearValue(System.Windows.Controls.Image.SourceProperty);
			UpdateThemeBackgroundVisual(animate: false);
			UpdateBingBackgroundVisual(animate: false);
			ScheduleGuideAudioWarmup();
			((DispatcherObject)this).Dispatcher.BeginInvoke((Delegate)(Action)FocusFirstButton, (DispatcherPriority)4, Array.Empty<object>());
		}
		catch (Exception exception)
		{
			App.LogException(exception, "MainWindow.RestoreResourcesAfterMinimizedDashboard");
		}
	}

	private void Settings_OnPropertyChanged(object? sender, PropertyChangedEventArgs e)
	{
		string propertyName = e.PropertyName;
		if ((propertyName == "DisplayResolution" || propertyName == "StartFullscreen") ? true : false)
		{
			if (propertyName == "DisplayResolution")
			{
				_viewModel.RefreshDisplayAspectRatioBindings();
			}
			ApplyDisplaySettings();
		}
		else if (propertyName == "DashboardStyle")
		{
			_viewModel.RefreshDashboardStyleBindings();
			UpdateThemeBackgroundVisual();
		}
	}

	private void ApplyDisplaySettings()
	{
		//IL_00ef: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f4: Unknown result type (might be due to invalid IL or missing references)
		var (val, val2) = _viewModel.Settings.DisplayResolution switch
		{
			"21:9" => (1720.0, 720.0), 
			_ => (1920.0, 1080.0), 
		};
		if (_viewModel.Settings.StartFullscreen)
		{
			base.WindowStyle = WindowStyle.None;
			base.ResizeMode = ResizeMode.NoResize;
			base.WindowState = WindowState.Maximized;
			return;
		}
		base.WindowState = WindowState.Normal;
		base.WindowStyle = WindowStyle.SingleBorderWindow;
		base.ResizeMode = ResizeMode.CanResize;
		Rect workArea = SystemParameters.WorkArea;
		base.Width = Math.Min(val, workArea.Width);
		base.Height = Math.Min(val2, workArea.Height);
		base.Left = workArea.Left + Math.Max(0.0, (workArea.Width - base.Width) / 2.0);
		base.Top = workArea.Top + Math.Max(0.0, (workArea.Height - base.Height) / 2.0);
	}

	private void Window_OnPreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
	{
		DashboardInputAction action;
		if (_isFakeLoadingActive)
		{
			e.Handled = true;
		}
		else if (_viewModel.IsBooting)
		{
			SkipBootIntro(userInitiated: true);
			e.Handled = true;
		}
		else if (DashboardInputRouter.TryMapKey(e, out action))
		{
			e.Handled = true;
			if (TryHandleBladesAchievementsInputEarly(action))
			{
				return;
			}
			HandleInputAction(action);
		}
	}

	private void Window_OnPreviewMouseDown(object sender, MouseButtonEventArgs e)
	{
		ShowMouseCursor(updatePosition: true);
		if (_isFakeLoadingActive)
		{
			e.Handled = true;
		}
		else if (_viewModel.IsBooting)
		{
			SkipBootIntro(userInitiated: true);
			e.Handled = true;
		}
		else
		{
		}
	}

	private void Window_OnPreviewMouseMove(object sender, System.Windows.Input.MouseEventArgs e)
	{
		//IL_0002: Unknown result type (might be due to invalid IL or missing references)
		//IL_0007: Unknown result type (might be due to invalid IL or missing references)
		//IL_0060: Unknown result type (might be due to invalid IL or missing references)
		//IL_001a: Unknown result type (might be due to invalid IL or missing references)
		//IL_001f: Unknown result type (might be due to invalid IL or missing references)
		Point position = e.GetPosition(this);
		Point? lastMousePosition = _lastMousePosition;
		if (lastMousePosition.HasValue)
		{
			Point valueOrDefault = lastMousePosition.GetValueOrDefault();
			if (Math.Abs(position.X - valueOrDefault.X) < 2.0 && Math.Abs(position.Y - valueOrDefault.Y) < 2.0)
			{
				return;
			}
		}
		_lastMousePosition = position;
		ShowMouseCursor(updatePosition: false);
	}

	private void HandleControllerInputAction(DashboardInputAction action)
	{
		HideMouseCursorForController();
		if (TryHandleBladesAchievementsInputEarly(action))
		{
			return;
		}
		_isHandlingControllerInput = true;
		try
		{
			HandleInputAction(action);
		}
		finally
		{
			_isHandlingControllerInput = false;
		}
	}

	private void HandleInputAction(DashboardInputAction action)
	{
		try
		{
			HandleInputActionCore(action);
		}
		catch (Exception exception)
		{
			App.LogException(exception, "MainWindow.HandleInputAction");
			_isAnimatingTab = false;
			_isFocusUpdateQueued = false;
		}
	}

	private bool TryHandleBladesAchievementsInputEarly(DashboardInputAction action)
	{
		try
		{
			return !_isFakeLoadingActive
				&& !_viewModel.IsBooting
				&& _viewModel.HandleBladesAchievementsSubmenuInput(action);
		}
		catch (Exception exception)
		{
			App.LogException(exception, "MainWindow.TryHandleBladesAchievementsInputEarly");
			return false;
		}
	}

	private void HideMouseCursorForController()
	{
		//IL_0012: Unknown result type (might be due to invalid IL or missing references)
		if (!_isMouseCursorHiddenForController)
		{
			_isMouseCursorHiddenForController = true;
			_lastMousePosition = Mouse.GetPosition(this);
			base.Cursor = System.Windows.Input.Cursors.None;
			Mouse.OverrideCursor = System.Windows.Input.Cursors.None;
		}
	}

	private void ShowMouseCursor(bool updatePosition)
	{
		//IL_0029: Unknown result type (might be due to invalid IL or missing references)
		//IL_000d: Unknown result type (might be due to invalid IL or missing references)
		if (!_isMouseCursorHiddenForController)
		{
			if (updatePosition)
			{
				_lastMousePosition = Mouse.GetPosition(this);
			}
			return;
		}
		_isMouseCursorHiddenForController = false;
		if (updatePosition)
		{
			_lastMousePosition = Mouse.GetPosition(this);
		}
		base.Cursor = null;
		Mouse.OverrideCursor = null;
	}

	private void HandleInputActionCore(DashboardInputAction action)
	{
		if (_isFakeLoadingActive || _isMenuFakeLoadingActive || _isMenuTransitionActive || _viewModel.IsBladesMenuTransitioning)
		{
			return;
		}
		if (action == DashboardInputAction.Activate && TryHandleActiveToastAction())
		{
			return;
		}
		if (action == DashboardInputAction.Guide)
		{
			if (TryHandleActiveToastAction())
			{
				return;
			}
			IGuideWindow? guideWindow = _guideWindow;
			if (guideWindow == null || !guideWindow.IsTransitioning)
			{
				IGuideWindow? guideWindow2 = _guideWindow;
				if (guideWindow2 != null && guideWindow2.IsGuideOpen)
				{
					HideGuide();
				}
				else
				{
					ShowGuide();
				}
			}
			return;
		}
		IGuideWindow? guideWindow3 = _guideWindow;
		if (guideWindow3 != null && guideWindow3.IsGuideOpen)
		{
			_guideWindow.HandleInput(action);
		}
		else
		{
			if (base.WindowState == WindowState.Minimized || !base.IsVisible || !base.IsActive)
			{
				return;
			}
			if (_viewModel.IsYouTubeTvOpen && HandleYouTubeTvInput(action))
			{
				return;
			}
			if (_viewModel.IsBooting)
			{
				SkipBootIntro(userInitiated: true);
				return;
			}
			if (_viewModel.IsBladesAchievementsSubmenu && _viewModel.HandleBladesAchievementsSubmenuInput(action))
			{
				return;
			}
			if (_viewModel.IsBladesDashboardStyle && _viewModel.IsBladesSubmenuOpen && !_viewModel.IsDetailsOpen && !_viewModel.IsMyGamesOpen && !_viewModel.IsLauncherSettingsOpen && !_viewModel.IsSearchOverlayOpen)
			{
				_viewModel.HandleInput(action);
				if (!_viewModel.IsBladesAchievementsSubmenu)
				{
					QueueBladesMenuFocus();
				}
				return;
			}
			if (_viewModel.IsMusicNowPlayingScreen && IsFocusInside((DependencyObject)(object)MusicPlayerLeftPane) && ((uint)action <= 3u || action == DashboardInputAction.Activate))
			{
				if ((uint)action <= 3u)
				{
					TryMoveOverlayFocus(MusicPlayerOverlay, action);
					return;
				}
				if (DashboardInputRouter.ActivateFocusedElement())
				{
					return;
				}
			}
			if (_viewModel.HandleMusicBrowserInput(action))
			{
				return;
			}
			if (action == DashboardInputAction.Back && _viewModel.IsLauncherSettingsOpen && _activeSystemSettingsPanel != null && !_viewModel.IsDashboardCustomizerOpen && !_viewModel.IsThemeCreatorOpen && !_viewModel.IsSteamSetupOpen && !_viewModel.IsSpotifySetupOpen)
			{
				_audioService.Play("menu-out");
				ShowSystemSettingsCategories();
				return;
			}
			if (action == DashboardInputAction.Back && TryBeginMenuCloseTransition())
			{
				return;
			}
			bool flag = _viewModel.IsDetailsOpen;
			if (flag)
			{
				bool flag2 = (uint)(action - 12) <= 1u;
				flag = flag2;
			}
			if (flag)
			{
				_viewModel.MoveGameDetailsTab((action != DashboardInputAction.PreviousTab) ? 1 : (-1));
				QueueGameDetailsTileFocus();
				return;
			}
			if (_viewModel.IsDetailsOpen && (uint)action <= 3u)
			{
				if (!TryMoveOverlayFocus(GameDetailsOverlay, action))
				{
					QueueGameDetailsTileFocus();
				}
				return;
			}
			if (_viewModel.IsMyGamesOpen && ((uint)action <= 3u || (uint)(action - 12) <= 1u))
			{
				TryMoveMyGamesFocus(action);
				return;
			}
			flag = IsOverlayOpen();
			if (flag)
			{
				bool flag2 = (uint)(action - 12) <= 1u;
				flag = flag2;
			}
			if (flag)
			{
				return;
			}
			flag = _isAnimatingTab;
			if (flag)
			{
				bool flag2 = (((uint)action <= 1u || (uint)(action - 12) <= 1u) ? true : false);
				flag = flag2;
			}
			if (flag)
			{
				if (!IsOverlayOpen())
				{
					flag = ((action == DashboardInputAction.MoveLeft || action == DashboardInputAction.PreviousTab) ? true : false);
					_queuedTabStep = ((!flag) ? 1 : (-1));
				}
				return;
			}
			if (_viewModel.IsBladesDashboardStyle && !IsOverlayOpen())
			{
				_viewModel.HandleInput(action);
				QueueBladesMenuFocus();
				return;
			}
			if ((uint)(action - 12) <= 1u)
			{
				RememberFocusedButton();
			}
			if ((uint)action <= 3u)
			{
				if (TryRestoreOverlayFocus())
				{
					_viewModel.HandleInput(action);
					return;
				}
				bool flag3 = TryMoveDashboardFocus(action);
				if (!flag3 && action == DashboardInputAction.MoveLeft)
				{
					if (!IsOverlayOpen())
					{
						_viewModel.MoveTab(-1);
					}
				}
				else if (!flag3 && action == DashboardInputAction.MoveRight && !IsOverlayOpen())
				{
					_viewModel.MoveTab(1);
				}
				_viewModel.HandleInput(action);
			}
			else
			{
				if (action == DashboardInputAction.Activate && TryRestoreOverlayFocus())
				{
					return;
				}
				if (action == DashboardInputAction.Activate && Keyboard.FocusedElement is System.Windows.Controls.TextBox textBox)
				{
					textBox.GetBindingExpression(System.Windows.Controls.TextBox.TextProperty)?.UpdateSource();
					if ((_viewModel.IsSearchOverlayOpen || _viewModel.CurrentTab?.Key == "bing") && _viewModel.SubmitSearchCommand.CanExecute(null))
					{
						_viewModel.SubmitSearchCommand.Execute(null);
					}
					return;
				}
				if (action == DashboardInputAction.Activate && DashboardInputRouter.ActivateFocusedElement())
				{
					_viewModel.HandleInput(action);
					return;
				}
				_viewModel.HandleInput(action);
				if (action != DashboardInputAction.Search || !_viewModel.IsSearchOverlayOpen)
				{
					return;
				}
				((DispatcherObject)this).Dispatcher.BeginInvoke((Delegate)(Action)delegate
				{
					try
					{
						SearchOverlayTextBox.Focus();
						SearchOverlayTextBox.SelectAll();
					}
					catch
					{
					}
				}, (DispatcherPriority)4, Array.Empty<object>());
			}
		}
	}

	private void ShowGuide()
	{
		try
		{
			EnsureGuideWindow();
			if (_guideWindow != null && !_guideWindow.IsTransitioning && !_guideWindow.IsGuideOpen)
			{
				RememberGuideReturnFocus();
				_audioService.Play("guide-open");
				_guideWindow.Open();
			}
		}
		catch (InvalidOperationException)
		{
			EnsureGuideWindow();
			if (_guideWindow != null && !_guideWindow.IsTransitioning && !_guideWindow.IsGuideOpen)
			{
				RememberGuideReturnFocus();
				_audioService.Play("guide-open");
				_guideWindow.Open();
			}
		}
		catch (Exception exception)
		{
			App.LogException(exception, "MainWindow.ShowGuide");
		}
	}

	private void EnsureGuideWindow()
	{
		bool blades = _viewModel.Settings.XboxGuide == "Blades" || _viewModel.Settings.XboxGuide == "Blades BETA";
		if (_guideWindow != null && (_guideWindow is BladesGuideWindow) == blades)
		{
			return;
		}
		if (_guideWindow != null)
		{
			_guideWindow.HiddenCompleted -= GuideWindow_OnHiddenCompleted;
			_guideWindow.Closed -= GuideWindow_OnClosed;
			try
			{
				_guideWindow.Close();
			}
			catch
			{
			}
		}
		_guideViewModel?.Dispose();
		_guideViewModel = new GuideViewModel(_viewModel, this, HideGuide, _audioService, _friendsService, _socialIntegrationManager, _steamCommunityService);
		_guideWindow = blades ? new BladesGuideWindow(_guideViewModel) : new GuideWindow(_guideViewModel);
		_guideWindow.HiddenCompleted += GuideWindow_OnHiddenCompleted;
		_guideWindow.Closed += GuideWindow_OnClosed;
	}

	private void AcceptDashPartyInviteFromToast(SocialFriend friend)
	{
		try
		{
			EnsureGuideWindow();
			if (_guideViewModel == null || _guideWindow == null || _guideWindow.IsTransitioning)
			{
				return;
			}
			RememberGuideReturnFocus();
			_guideViewModel.AcceptDashPartyInvite(friend);
			if (!_guideWindow.IsGuideOpen)
			{
				_audioService.Play("guide-open");
				_guideWindow.Open(resetToHome: false);
			}
		}
		catch (Exception exception)
		{
			App.LogException(exception, "MainWindow.AcceptDashPartyInviteFromToast");
		}
	}

	private bool TryHandleActiveToastAction()
	{
		if (!_partyInviteToastActive)
		{
			return false;
		}
		string toastActionUri = _toastActionUri;
		if (!string.IsNullOrWhiteSpace(toastActionUri))
		{
			_toastActionUri = string.Empty;
			OpenToastActionUri(toastActionUri);
			return true;
		}
		if (_viewModel.TryTakePendingDashPartyInvite(out SocialFriend friend))
		{
			AcceptDashPartyInviteFromToast(friend);
			return true;
		}
		return false;
	}

	private static void OpenToastActionUri(string uri)
	{
		try
		{
			Process.Start(new ProcessStartInfo(uri)
			{
				UseShellExecute = true
			});
		}
		catch (Exception exception)
		{
			App.LogException(exception, "MainWindow.OpenToastActionUri");
		}
	}

	private void ScheduleGuideAudioWarmup()
	{
		if (_guideWarmupScheduled)
		{
			return;
		}
		_guideWarmupScheduled = true;
		((DispatcherObject)this).Dispatcher.BeginInvoke((Delegate)(Action)delegate
		{
			try
			{
				WarmUpGuideAudio();
			}
			catch (Exception exception)
			{
				App.LogException(exception, "MainWindow.ScheduleGuideAudioWarmup");
			}
		}, (DispatcherPriority)4, Array.Empty<object>());
	}

	private void WarmUpGuideAudio()
	{
		_audioService.WarmUp("guide-open");
		_audioService.WarmUp("guide-close");
		_audioService.WarmUp("guide-blade-open");
		_audioService.WarmUp("guide-hover");
		_audioService.WarmUp("guide-select");
		_audioService.WarmUp("guide-back");
		_audioService.WarmUp("guide-blade-switch-1");
		_audioService.WarmUp("guide-blade-switch-2");
		_audioService.WarmUp("guide-blade-switch-3");
		_audioService.WarmUp("guide-blade-switch-4");
	}

	private void HideGuide()
	{
		try
		{
			if (_guideWindow != null && !_guideWindow.IsTransitioning && _guideWindow.IsGuideOpen)
			{
				_restoreFocusAfterGuideClose = _guideWindow.CloseGuide(playSound: true);
				if (!_guideRestoreExternalWindow)
				{
					Activate();
				}
			}
		}
		catch (Exception exception)
		{
			App.LogException(exception, "MainWindow.HideGuide");
		}
	}

	private void ViewModel_OnFriendsOverlayRequested(object? sender, EventArgs e)
	{
		try
		{
			OpenGuideFriendsOverlay();
		}
		catch (Exception exception)
		{
			App.LogException(exception, "MainWindow.ViewModel_OnFriendsOverlayRequested");
		}
	}

	private async void ViewModel_OnBladesMusicRequested(object? sender, EventArgs e)
	{
		try
		{
			if (_viewModel.Settings.EnableFakeLoading && ShouldRunMenuFakeLoading("music"))
			{
				BladesLoadingTitle.Text="Music";
				await RunMenuFakeLoadingSequenceAsync();
				BladesLoadingTitle.ClearValue(TextBlock.TextProperty);
			}
			EnsureGuideWindow();
			RememberGuideReturnFocus();
			if (_guideWindow is BladesGuideWindow blades) blades.OpenMusicFromDashboard();
			else _guideViewModel?.OpenGuideMusicMenuCommand.Execute(null);
		}
		catch (Exception ex) { App.LogException(ex,"Blades.OpenMusic"); }
	}

	private void OpenGuideFriendsOverlay()
	{
		EnsureGuideWindow();
		if (_guideWindow != null && _guideViewModel != null && !_guideWindow.IsTransitioning)
		{
			RememberGuideReturnFocus();
			_guideViewModel.OpenFriendsOverlayFromDashboard();
			_audioService.Play("guide-open");
			_guideWindow.Open(resetToHome: false);
		}
	}

	private void ViewModel_OnPartyOverlayRequested(object? sender, EventArgs e)
	{
		try
		{
			OpenGuidePartyOverlay();
		}
		catch (Exception exception)
		{
			App.LogException(exception, "MainWindow.ViewModel_OnPartyOverlayRequested");
		}
	}

	private void ViewModel_OnAchievementOverlayRequested(object? sender, DashboardViewModel.DashboardAchievementOpenRequest e)
	{
		try
		{
			OpenGuideAchievementOverlay(e);
		}
		catch (Exception exception)
		{
			App.LogException(exception, "MainWindow.ViewModel_OnAchievementOverlayRequested");
		}
	}

	private void OpenGuidePartyOverlay()
	{
		EnsureGuideWindow();
		if (_guideWindow != null && _guideViewModel != null && !_guideWindow.IsTransitioning)
		{
			RememberGuideReturnFocus();
			_guideViewModel.OpenPartyOverlayFromDashboard();
			_audioService.Play("guide-open");
			_guideWindow.Open(resetToHome: false);
		}
	}

	private void OpenGuideAchievementOverlay(DashboardViewModel.DashboardAchievementOpenRequest request)
	{
		EnsureGuideWindow();
		if (_guideWindow != null && _guideViewModel != null && !_guideWindow.IsTransitioning)
		{
			RememberGuideReturnFocus();
			_guideViewModel.OpenAchievementFromDashboard(request.SteamAppId, request.AchievementApiName);
			_audioService.Play("guide-open");
			_guideWindow.Open(resetToHome: false);
		}
	}

	private void ViewModel_OnToastRequested(object? sender, DashboardToastRequest e)
	{
		((DispatcherObject)this).Dispatcher.BeginInvoke((Delegate)(Action)delegate
		{
			_ = RunDashboardToastSequenceAsync(e);
		}, DispatcherPriority.Background, Array.Empty<object>());
	}

	private async Task RunDashboardToastSequenceAsync(DashboardToastRequest request)
	{
		bool hasToastAction = !string.IsNullOrWhiteSpace(request.ActionUri);
		if (request.AcceptPartyInviteWithGuide || hasToastAction)
		{
			_partyInviteToastActive = true;
			_toastActionUri = hasToastAction ? request.ActionUri.Trim() : string.Empty;
		}
		try
		{
			await RunSignInToastSequenceAsync(request.Line1, request.Line2).ConfigureAwait(continueOnCapturedContext: true);
		}
		finally
		{
			if (request.AcceptPartyInviteWithGuide || hasToastAction)
			{
				_partyInviteToastActive = false;
				_toastActionUri = string.Empty;
			}
		}
	}

	private void RememberGuideReturnFocus()
	{
		nint handle = new WindowInteropHelper(this).Handle;
		nint foregroundWindow = GetForegroundWindow();
		_guideRestoreExternalWindow = foregroundWindow != IntPtr.Zero && foregroundWindow != handle;
		_guideReturnWindowHandle = (_guideRestoreExternalWindow ? foregroundWindow : IntPtr.Zero);
		_guideReturnFocusElement = (_guideRestoreExternalWindow ? null : (Keyboard.FocusedElement as UIElement));
		RememberFocusedButton();
	}

	private void GuideWindow_OnHiddenCompleted(object? sender, EventArgs e)
	{
		if (sender is BladesGuideWindow && WindowState == WindowState.Minimized) return;
		if (_guideRestoreExternalWindow && RestoreExternalWindowAfterGuide())
		{
			_restoreFocusAfterGuideClose = false;
			_guideRestoreExternalWindow = false;
			_guideReturnWindowHandle = IntPtr.Zero;
			_guideReturnFocusElement = null;
			return;
		}
		Activate();
		if (!_restoreFocusAfterGuideClose)
		{
			_guideRestoreExternalWindow = false;
			_guideReturnWindowHandle = IntPtr.Zero;
		}
		else
		{
			_restoreFocusAfterGuideClose = false;
			RestoreFocusAfterGuide();
		}
	}

	private void GuideWindow_OnClosed(object? sender, EventArgs e)
	{
		if (_guideWindow != null)
		{
			_guideWindow.HiddenCompleted -= GuideWindow_OnHiddenCompleted;
			_guideWindow.Closed -= GuideWindow_OnClosed;
		}
		_guideViewModel?.Dispose();
		_guideViewModel = null;
		_guideWindow = null;
	}

	private void RestoreFocusAfterGuide()
	{
		((DispatcherObject)this).Dispatcher.BeginInvoke((Delegate)(Action)delegate
		{
			try
			{
				if (IsOverlayOpen())
				{
					FocusFirstButton();
				}
				else if (_guideReturnFocusElement != null && TryFocus(_guideReturnFocusElement))
				{
					RememberFocusedButton();
				}
				else
				{
					FocusFirstButton();
				}
			}
			catch (Exception exception)
			{
				App.LogException(exception, "MainWindow.RestoreFocusAfterGuide");
			}
			finally
			{
				_guideReturnFocusElement = null;
				_guideRestoreExternalWindow = false;
				_guideReturnWindowHandle = IntPtr.Zero;
			}
		}, (DispatcherPriority)5, Array.Empty<object>());
	}

	private bool RestoreExternalWindowAfterGuide()
	{
		if (_guideReturnWindowHandle == IntPtr.Zero || !IsWindow(_guideReturnWindowHandle))
		{
			return false;
		}
		BringWindowToTop(_guideReturnWindowHandle);
		return SetForegroundWindow(_guideReturnWindowHandle);
	}

	public void PrepareGuideReturnToDashboard()
	{
		_guideRestoreExternalWindow = false;
		_guideReturnWindowHandle = IntPtr.Zero;
		_guideReturnFocusElement = null;
		_restoreFocusAfterGuideClose = false;
	}

	private void StartBootVideo()
	{
		bool useBladesStartup = ShouldUseBladesStartupVideoAudio();
		string text = AppPaths.FindFile(System.IO.Path.Combine("Assets", "Boot", useBladesStartup ? "Blades Startup.mp4" : "Boot Screen.mp4"));
		if (!File.Exists(text))
		{
			SkipBootIntro();
		}
		else if (!EnsureBootBrowser())
		{
			SkipBootIntro();
		}
		else
		{
			StartBrowserBootPlayback(text, useBladesStartup);
		}
	}

	private bool EnsureBootBrowser()
	{
		if (_bootBrowser != null)
		{
			return true;
		}
		try
		{
			System.Windows.Forms.WebBrowser webBrowser = new System.Windows.Forms.WebBrowser
			{
				Dock = DockStyle.Fill,
				AllowWebBrowserDrop = false,
				IsWebBrowserContextMenuEnabled = false,
				ScrollBarsEnabled = false,
				WebBrowserShortcutsEnabled = false
			};
			BootVideoHost.Child = webBrowser;
			_bootBrowser = webBrowser;
			return true;
		}
		catch
		{
			return false;
		}
	}

	private void StartBrowserBootPlayback(string bootVideoPath, bool useNativeAudio)
	{
		//IL_0041: Unknown result type (might be due to invalid IL or missing references)
		//IL_0046: Unknown result type (might be due to invalid IL or missing references)
		//IL_005f: Expected O, but got Unknown
		try
		{
			string absoluteUri = new Uri(bootVideoPath).AbsoluteUri;
			string mutedAttribute = useNativeAudio ? string.Empty : " muted";
			string mutedScript = useNativeAudio ? "false" : "true";
			string volumeScript = useNativeAudio ? "1" : "0";
			_bootBrowser.DocumentText = "<!doctype html>\n<html>\n<head>\n    <meta http-equiv=\"X-UA-Compatible\" content=\"IE=edge\" />\n    <style>\n        html, body {\n            width: 100%;\n            height: 100%;\n            margin: 0;\n            overflow: hidden;\n            background: #fff;\n        }\n        video {\n            width: 100vw;\n            height: 100vh;\n            object-fit: contain;\n            background: #fff;\n            display: block;\n        }\n    </style>\n</head>\n<body>\n    <video id=\"boot\" src=\"" + absoluteUri + "\" autoplay" + mutedAttribute + " playsinline></video>\n    <script>\n        var boot = document.getElementById('boot');\n        boot.muted = " + mutedScript + ";\n        boot.volume = " + volumeScript + ";\n        boot.play();\n    </script>\n</body>\n</html>";
			if (!useNativeAudio)
			{
				_ = PlayBootIntroSoundAfterBrowserStartsAsync();
			}
			_bootStartedAt = DateTime.UtcNow;
			if (_bootStateTimer == null)
			{
				_bootStateTimer = new DispatcherTimer
				{
					Interval = TimeSpan.FromMilliseconds(250.0)
				};
			}
			_bootStateTimer.Tick -= BootStateTimer_OnTick;
			_bootStateTimer.Tick += BootStateTimer_OnTick;
			_bootStateTimer.Start();
		}
		catch
		{
			SkipBootIntro();
		}
	}

	private void PlayStartupSound()
	{
		if (!_startupInitializationComplete || ShouldRunBladesRunnerStartup())
		{
			return;
		}
		if (_startupSoundPlayed)
		{
			return;
		}
		_startupSoundPlayed = true;
		try
		{
			_audioService.Play("startup");
		}
		catch
		{
		}
	}

	private void PlayNotificationSound()
	{
		_ = Task.Run(delegate
		{
			try
			{
				_audioService.Play("notify-popup");
			}
			catch
			{
			}
		});
	}

	private void ScheduleStartupPrewarm()
	{
		if (_startupPrewarmScheduled)
		{
			return;
		}
		_startupPrewarmScheduled = true;
		_ = RunStartupPrewarmLaterAsync();
	}

	private async Task RunStartupPrewarmLaterAsync()
	{
		try
		{
			await Task.Delay(5000);
			await ((DispatcherObject)this).Dispatcher.InvokeAsync((Action)PrewarmStartupUi, DispatcherPriority.ApplicationIdle);
		}
		catch (Exception exception)
		{
			App.LogException(exception, "MainWindow.RunStartupPrewarmLaterAsync");
		}
	}

	private void PrewarmStartupUi()
	{
		try
		{
			WarmUpGuideAudio();
		}
		catch (Exception exception)
		{
			App.LogException(exception, "MainWindow.PrewarmStartupUi");
		}
	}

	private async Task RunSignInToastSequenceAsync(string? line1 = null, string? line2 = null)
	{
		if (_signInToastSequenceActive)
		{
			return;
		}
		try
		{
			_signInToastSequenceActive = true;
			PrepareSignInToastText(line1, line2);
			SignInToast.Opacity = 0.0;
			SignInToastGlow.Opacity = 0.0;
			SignInToastGlowScale.ScaleX = 0.0;
			SignInToastPill.Opacity = 0.0;
			SignInToastPillScale.ScaleX = 0.0;
			SignInToastPillHighlight.Opacity = 0.0;
			SignInToastPillHighlightScale.ScaleX = 0.0;
			SignInToastIcon.Opacity = 0.0;
			SignInToastIconSphere.Opacity = 1.0;
			SignInToastIconAlert.Opacity = 0.0;
			SignInToastText.Opacity = 0.0;
			SignInToastScale.ScaleX = 1.0;
			SignInToastScale.ScaleY = 1.0;
			SignInToastTransform.Y = 0.0;
			SignInToastIconScale.ScaleX = 0.56;
			SignInToastIconScale.ScaleY = 0.56;
			await Task.Delay(120);
			CubicEase toastEase = new CubicEase
			{
				EasingMode = EasingMode.EaseOut
			};
			SignInToast.Opacity = 1.0;
			Task task = AnimateDoubleAsync(SignInToastIcon, UIElement.OpacityProperty, 0.0, 1.0, 150, toastEase);
			Task task2 = AnimateDoubleAsync(SignInToastIconScale, ScaleTransform.ScaleXProperty, 0.56, 1.04, 180, toastEase);
			Task task3 = AnimateDoubleAsync(SignInToastIconScale, ScaleTransform.ScaleYProperty, 0.56, 1.04, 180, toastEase);
			await ((DispatcherObject)this).Dispatcher.InvokeAsync((Action)delegate
			{
			}, DispatcherPriority.Render);
			((DispatcherObject)this).Dispatcher.BeginInvoke((Delegate)(Action)delegate
			{
				PlayNotificationSound();
			}, DispatcherPriority.Background, Array.Empty<object>());
			await Task.WhenAll(task, task2, task3);
			StartSignInIconBlink();
			Task task4 = AnimateDoubleAsync(SignInToastIconScale, ScaleTransform.ScaleXProperty, 1.04, 1.0, 115, toastEase);
			Task task5 = AnimateDoubleAsync(SignInToastIconScale, ScaleTransform.ScaleYProperty, 1.04, 1.0, 115, toastEase);
			await Task.WhenAll(task4, task5);
			await Task.Delay(55);
			Task task6 = AnimateDoubleAsync(SignInToastGlow, UIElement.OpacityProperty, 0.0, 0.34, 135, toastEase);
			Task task7 = AnimateDoubleAsync(SignInToastGlowScale, ScaleTransform.ScaleXProperty, 0.0, 1.0, 265, toastEase);
			Task task8 = AnimateDoubleAsync(SignInToastPill, UIElement.OpacityProperty, 0.0, 1.0, 110, toastEase);
			Task task9 = AnimateDoubleAsync(SignInToastPillScale, ScaleTransform.ScaleXProperty, 0.0, 1.0, 265, toastEase);
			Task task10 = AnimateDoubleAsync(SignInToastPillHighlight, UIElement.OpacityProperty, 0.0, 0.0, 120, toastEase);
			Task task11 = AnimateDoubleAsync(SignInToastPillHighlightScale, ScaleTransform.ScaleXProperty, 0.0, 1.0, 265, toastEase);
			await Task.WhenAll(task6, task7, task8, task9, task10, task11);
			await Task.Delay(55);
			await AnimateDoubleAsync(SignInToastText, UIElement.OpacityProperty, 0.0, 1.0, 185, null);
			await Task.Delay(2450);
			CubicEase hideEase = new CubicEase
			{
				EasingMode = EasingMode.EaseIn
			};
			await AnimateDoubleAsync(SignInToastText, UIElement.OpacityProperty, 1.0, 0.0, 185, hideEase);
			await Task.Delay(150);
			StopSignInIconBlink();
			Task task12 = AnimateDoubleAsync(SignInToastGlowScale, ScaleTransform.ScaleXProperty, 1.0, 0.0, 320, hideEase);
			Task task13 = AnimateDoubleAsync(SignInToastGlow, UIElement.OpacityProperty, 0.34, 0.0, 270, hideEase);
			Task task14 = AnimateDoubleAsync(SignInToastPillScale, ScaleTransform.ScaleXProperty, 1.0, 0.0, 320, hideEase);
			Task task15 = AnimateDoubleAsync(SignInToastPill, UIElement.OpacityProperty, 1.0, 0.0, 300, hideEase);
			Task task16 = AnimateDoubleAsync(SignInToastPillHighlightScale, ScaleTransform.ScaleXProperty, 1.0, 0.0, 300, hideEase);
			Task task17 = AnimateDoubleAsync(SignInToastPillHighlight, UIElement.OpacityProperty, 0.0, 0.0, 230, hideEase);
			Task task18 = AnimateDoubleAsync(SignInToastIconScale, ScaleTransform.ScaleXProperty, 1.0, 0.52, 210, hideEase);
			Task task19 = AnimateDoubleAsync(SignInToastIconScale, ScaleTransform.ScaleYProperty, 1.0, 0.52, 210, hideEase);
			Task task20 = AnimateDoubleAsync(SignInToastIcon, UIElement.OpacityProperty, 1.0, 0.0, 210, hideEase);
			await Task.WhenAll(task12, task13, task14, task15, task16, task17, task18, task19, task20);
			await AnimateDoubleAsync(SignInToast, UIElement.OpacityProperty, 1.0, 0.0, 80, hideEase);
		}
		catch (Exception exception)
		{
			App.LogException(exception, "MainWindow.RunSignInToastSequenceAsync");
		}
		finally
		{
			_signInToastSequenceActive = false;
			StopSignInIconBlink();
			SignInToast.Opacity = 0.0;
			SignInToastGlow.Opacity = 0.0;
			SignInToastGlowScale.ScaleX = 0.0;
			SignInToastPill.Opacity = 0.0;
			SignInToastPillScale.ScaleX = 0.0;
			SignInToastPillHighlight.Opacity = 0.0;
			SignInToastPillHighlightScale.ScaleX = 0.0;
			SignInToastIcon.Opacity = 0.0;
			SignInToastIconSphere.Opacity = 0.0;
			SignInToastIconAlert.Opacity = 1.0;
			SignInToastText.Opacity = 0.0;
			SignInToastScale.ScaleX = 1.0;
			SignInToastScale.ScaleY = 1.0;
			SignInToastTransform.Y = 0.0;
			SignInToastIconScale.ScaleX = 0.56;
			SignInToastIconScale.ScaleY = 0.56;
		}
	}

	private void PrepareSignInToastText(string? line1 = null, string? line2 = null)
	{
		string text = _viewModel.Profile?.Gamertag?.Trim() ?? string.Empty;
		if (string.IsNullOrWhiteSpace(text))
		{
			text = "Player";
		}
		_viewModel.NotificationToastLine1 = string.IsNullOrWhiteSpace(line1) ? (text + " signed") : line1;
		_viewModel.NotificationToastLine2 = string.IsNullOrWhiteSpace(line2) ? "in to Xbox LIVE" : line2;
	}

	private void PlayBootIntroSound()
	{
		try
		{
			_audioService.Play("boot-intro");
		}
		catch
		{
		}
	}

	private async Task PlayBootIntroSoundAfterBrowserStartsAsync()
	{
		try
		{
			await Task.Delay(120);
			if (_viewModel.IsBooting && !_bootSkipped)
			{
				PlayBootIntroSound();
			}
		}
		catch
		{
		}
	}

	private void BootStateTimer_OnTick(object? sender, EventArgs e)
	{
		try
		{
			if ((bool?)_bootBrowser?.Document?.InvokeScript("eval", new object[1] { "document.getElementById('boot') && document.getElementById('boot').ended" }) == true || DateTime.UtcNow - _bootStartedAt > TimeSpan.FromSeconds(12.0))
			{
				SkipBootIntro();
			}
		}
		catch
		{
			if (DateTime.UtcNow - _bootStartedAt > TimeSpan.FromSeconds(12.0))
			{
				SkipBootIntro();
			}
		}
	}

	private void SkipBootIntro(bool userInitiated = false)
	{
		if (!_bootSkipped)
		{
			_bootSkipped = true;
			DispatcherTimer? bootStateTimer = _bootStateTimer;
			if (bootStateTimer != null)
			{
				bootStateTimer.Stop();
			}
			_audioService.Stop("boot-intro");
			if (userInitiated)
			{
				_audioService.Stop("startup");
				CleanupBootBrowser();
			}
			bool num = StartFakeLoadingIfReady(bootHandoff: true);
			if (!userInitiated)
			{
				ScheduleBootBrowserCleanup();
			}
			_viewModel.IsBooting = false;
			if (!num && _startupInitializationComplete)
			{
				PlayStartupSound();
				ScheduleStartupPrewarm();
				_ = RunSignInToastSequenceAsync();
			}
		}
	}

	private void ScheduleBootBrowserCleanup()
	{
		if (_bootBrowserCleanupScheduled)
		{
			return;
		}
		_bootBrowserCleanupScheduled = true;
		_ = CleanupBootBrowserLaterAsync();
	}

	private async Task CleanupBootBrowserLaterAsync()
	{
		try
		{
			await Task.Delay(2000);
			await ((DispatcherObject)this).Dispatcher.InvokeAsync((Action)CleanupBootBrowser, DispatcherPriority.ApplicationIdle);
		}
		catch (Exception exception)
		{
			App.LogException(exception, "MainWindow.CleanupBootBrowserLaterAsync");
		}
	}

	private void CleanupBootBrowser()
	{
		try
		{
			_audioService.Stop("boot-intro");
		}
		catch
		{
		}
		DispatcherTimer? bootStateTimer = _bootStateTimer;
		if (bootStateTimer != null)
		{
			bootStateTimer.Stop();
		}
		if (_bootStateTimer != null)
		{
			_bootStateTimer.Tick -= BootStateTimer_OnTick;
		}
		if (_bootBrowser == null)
		{
			BootVideoHost.Child = null;
			return;
		}
		try
		{
			_bootBrowser.Stop();
		}
		catch
		{
		}
		try
		{
			_bootBrowser.DocumentText = "<html><body></body></html>";
		}
		catch
		{
		}
		try
		{
			BootVideoHost.Child = null;
		}
		catch
		{
		}
		try
		{
			_bootBrowser.Dispose();
		}
		catch
		{
		}
		_bootBrowser = null;
	}

	private async void UpdateYouTubeTvHost()
	{
		if (_viewModel.IsYouTubeTvOpen)
		{
			await EnsureYouTubeTvBrowserAsync();
			_youtubeTvBrowser?.Focus();
		}
		else
		{
			CleanupYouTubeTvBrowser();
		}
	}

	private async Task EnsureYouTubeTvBrowserAsync()
	{
		try
		{
			if (_youtubeTvBrowser == null)
			{
				_youtubeTvBrowser = new WebView2
				{
					DefaultBackgroundColor = System.Drawing.Color.FromArgb(5, 5, 5)
				};
				YouTubeTvHost.Content = _youtubeTvBrowser;
			}
			if (_youtubeTvBrowser.CoreWebView2 == null)
			{
				await _youtubeTvBrowser.EnsureCoreWebView2Async();
				_youtubeTvBrowser.CoreWebView2.Settings.AreDefaultContextMenusEnabled = false;
				_youtubeTvBrowser.CoreWebView2.Settings.AreDevToolsEnabled = false;
				_youtubeTvBrowser.CoreWebView2.Settings.IsStatusBarEnabled = false;
				_youtubeTvBrowser.CoreWebView2.Settings.IsZoomControlEnabled = false;
				_youtubeTvBrowser.CoreWebView2.Settings.UserAgent = YouTubeTvUserAgent;
				_youtubeTvBrowser.CoreWebView2.NavigationStarting += YouTubeTvBrowser_OnNavigationStarting;
			}
			if (!_youtubeTvNavigationStarted)
			{
				_youtubeTvNavigationStarted = true;
				_youtubeTvBrowser.Source = new Uri(YouTubeTvUrl);
			}
		}
		catch (Exception exception)
		{
			App.LogException(exception, "MainWindow.EnsureYouTubeTvBrowserAsync");
			_viewModel.IsYouTubeTvOpen = false;
			_viewModel.StatusMessage = "YouTube TV could not open";
		}
	}

	private void YouTubeTvBrowser_OnNavigationStarting(object? sender, CoreWebView2NavigationStartingEventArgs e)
	{
		if (!_viewModel.IsYouTubeTvOpen || string.IsNullOrWhiteSpace(e.Uri))
		{
			return;
		}
		if (!Uri.TryCreate(e.Uri, UriKind.Absolute, out Uri? uri))
		{
			return;
		}
		bool isYouTube = uri.Host.Equals("youtube.com", StringComparison.OrdinalIgnoreCase) || uri.Host.Equals("www.youtube.com", StringComparison.OrdinalIgnoreCase);
		bool isTvPath = uri.AbsolutePath.StartsWith("/tv", StringComparison.OrdinalIgnoreCase);
		if (isYouTube && !isTvPath)
		{
			e.Cancel = true;
			if (_youtubeTvBrowser?.CoreWebView2 != null)
			{
				_youtubeTvBrowser.CoreWebView2.Navigate(YouTubeTvUrl);
			}
		}
	}

	private void CleanupYouTubeTvBrowser()
	{
		_youtubeTvNavigationStarted = false;
		if (_youtubeTvBrowser == null)
		{
			YouTubeTvHost.Content = null;
			return;
		}
		try
		{
			if (_youtubeTvBrowser.CoreWebView2 != null)
			{
				_youtubeTvBrowser.CoreWebView2.NavigationStarting -= YouTubeTvBrowser_OnNavigationStarting;
			}
			_youtubeTvBrowser.CoreWebView2?.Navigate("about:blank");
		}
		catch
		{
		}
		try
		{
			YouTubeTvHost.Content = null;
		}
		catch
		{
		}
		try
		{
			_youtubeTvBrowser.Dispose();
		}
		catch
		{
		}
		_youtubeTvBrowser = null;
	}

	private bool HandleYouTubeTvInput(DashboardInputAction action)
	{
		if (action == DashboardInputAction.Back)
		{
			_ = HandleYouTubeTvBackAsync();
			return true;
		}
		if (action == DashboardInputAction.Details)
		{
			_ = HandleYouTubeTvSearchBackspaceAsync();
			return true;
		}
		string? key = action switch
		{
			DashboardInputAction.MoveLeft => "ArrowLeft",
			DashboardInputAction.MoveRight => "ArrowRight",
			DashboardInputAction.MoveUp => "ArrowUp",
			DashboardInputAction.MoveDown => "ArrowDown",
			DashboardInputAction.Activate => "Enter",
			DashboardInputAction.Search => "/",
			_ => null
		};
		if (key != null)
		{
			_ = DispatchYouTubeTvKeyAsync(key);
		}
		return true;
	}

	private async Task HandleYouTubeTvSearchBackspaceAsync()
	{
		try
		{
			await EnsureYouTubeTvBrowserAsync();
			WebView2? youtubeTvBrowser = _youtubeTvBrowser;
			CoreWebView2? coreWebView = youtubeTvBrowser?.CoreWebView2;
			if (youtubeTvBrowser == null || coreWebView == null)
			{
				return;
			}
			youtubeTvBrowser.Focus();
			if (await IsYouTubeTvSearchKeyboardOpenAsync(coreWebView))
			{
				if (!await ClickYouTubeTvSearchBackspaceAsync(coreWebView))
				{
					await DispatchYouTubeTvKeyAsync("Backspace");
				}
			}
		}
		catch (Exception exception)
		{
			App.LogException(exception, "MainWindow.HandleYouTubeTvSearchBackspaceAsync");
		}
	}

	private async Task HandleYouTubeTvBackAsync()
	{
		try
		{
			await EnsureYouTubeTvBrowserAsync();
			WebView2? youtubeTvBrowser = _youtubeTvBrowser;
			CoreWebView2? coreWebView = youtubeTvBrowser?.CoreWebView2;
			if (youtubeTvBrowser == null || coreWebView == null)
			{
				return;
			}
			youtubeTvBrowser.Focus();
			if (await IsYouTubeTvHomeAsync(coreWebView))
			{
				_viewModel.CloseYouTubeTvCommand.Execute(null);
				return;
			}
			if (coreWebView.CanGoBack)
			{
				coreWebView.GoBack();
				return;
			}
			await DispatchYouTubeTvKeyAsync("Escape");
		}
		catch (Exception exception)
		{
			App.LogException(exception, "MainWindow.HandleYouTubeTvBackAsync");
		}
	}

	private static async Task<bool> IsYouTubeTvHomeAsync(CoreWebView2 coreWebView)
	{
		string script = "(() => { const path = (location.pathname || '').toLowerCase(); const hash = (location.hash || '').toLowerCase(); const text = document.body && document.body.innerText ? document.body.innerText : ''; const rootHash = hash === '' || hash === '#' || hash === '#/' || hash === '#/home' || hash.indexOf('home') >= 0; const signedOutHome = text.indexOf('Make YouTube your own') >= 0 || text.indexOf('Try searching to get started') >= 0; return path.startsWith('/tv') && (rootHash || signedOutHome); })();";
		string result = await coreWebView.ExecuteScriptAsync(script);
		return string.Equals(result, "true", StringComparison.OrdinalIgnoreCase);
	}

	private static async Task<bool> IsYouTubeTvSearchKeyboardOpenAsync(CoreWebView2 coreWebView)
	{
		string script = "(() => { const href = (location.href || '').toLowerCase(); const text = document.body && document.body.innerText ? document.body.innerText : ''; const hasKeyboard = text.indexOf('SPACE') >= 0 && text.indexOf('CLEAR') >= 0 && text.indexOf('&123') >= 0; return href.indexOf('search') >= 0 && hasKeyboard; })();";
		string result = await coreWebView.ExecuteScriptAsync(script);
		return string.Equals(result, "true", StringComparison.OrdinalIgnoreCase);
	}

	private static async Task<bool> ClickYouTubeTvSearchBackspaceAsync(CoreWebView2 coreWebView)
	{
		string script = "(() => { const isVisible = (element) => { const rect = element.getBoundingClientRect(); const style = getComputedStyle(element); return rect.width > 8 && rect.height > 8 && style.visibility !== 'hidden' && style.display !== 'none' && rect.bottom > 0 && rect.right > 0 && rect.top < innerHeight && rect.left < innerWidth; }; const explicit = Array.from(document.querySelectorAll('[aria-label], [title], button, div, span')).find((element) => { if (!isVisible(element)) return false; const text = ((element.getAttribute('aria-label') || '') + ' ' + (element.getAttribute('title') || '') + ' ' + (element.textContent || '')).toLowerCase(); return text.includes('backspace') || text.includes('delete'); }); const clickElement = (element) => { const rect = element.getBoundingClientRect(); const x = rect.left + rect.width / 2; const y = rect.top + rect.height / 2; const target = document.elementFromPoint(x, y) || element; for (const type of ['pointerdown', 'mousedown', 'pointerup', 'mouseup', 'click']) target.dispatchEvent(new MouseEvent(type, { bubbles: true, cancelable: true, clientX: x, clientY: y, view: window })); return true; }; if (explicit) return clickElement(explicit); const candidates = Array.from(document.querySelectorAll('button, [role=\"button\"], div, span')).filter((element) => { if (!isVisible(element)) return false; const rect = element.getBoundingClientRect(); const centerX = rect.left + rect.width / 2; const centerY = rect.top + rect.height / 2; return centerX > innerWidth * 0.56 && centerX < innerWidth * 0.72 && centerY > innerHeight * 0.11 && centerY < innerHeight * 0.25 && rect.width < innerWidth * 0.09 && rect.height < innerHeight * 0.09; }).sort((a, b) => b.getBoundingClientRect().left - a.getBoundingClientRect().left); return candidates.length > 0 ? clickElement(candidates[0]) : false; })();";
		string result = await coreWebView.ExecuteScriptAsync(script);
		return string.Equals(result, "true", StringComparison.OrdinalIgnoreCase);
	}

	private async Task DispatchYouTubeTvKeyAsync(string key)
	{
		try
		{
			await EnsureYouTubeTvBrowserAsync();
			WebView2? youtubeTvBrowser = _youtubeTvBrowser;
			if (youtubeTvBrowser?.CoreWebView2 == null)
			{
				return;
			}
			youtubeTvBrowser.Focus();
			int keyCode = key switch
			{
				"Backspace" => 8,
				"Enter" => 13,
				"Escape" => 27,
				"ArrowLeft" => 37,
				"ArrowUp" => 38,
				"ArrowRight" => 39,
				"ArrowDown" => 40,
				"/" => 191,
				_ => 0
			};
			string code = key == "/" ? "Slash" : key;
			string script = "(() => { const target = document.activeElement || document.body || document; const eventInit = { key: '" + key + "', code: '" + code + "', keyCode: " + keyCode + ", which: " + keyCode + ", bubbles: true, cancelable: true }; target.dispatchEvent(new KeyboardEvent('keydown', eventInit)); target.dispatchEvent(new KeyboardEvent('keyup', eventInit)); })();";
			await youtubeTvBrowser.CoreWebView2.ExecuteScriptAsync(script);
		}
		catch (Exception exception)
		{
			App.LogException(exception, "MainWindow.DispatchYouTubeTvKeyAsync");
		}
	}

	private bool StartFakeLoadingIfReady(bool bootHandoff = false)
	{
		if (_fakeLoadingStarted || !_startupInitializationComplete || (_viewModel.IsBooting && !bootHandoff) || (!_viewModel.Settings.EnableFakeLoading && !ShouldRunBladesRunnerStartup()))
		{
			return false;
		}
		_fakeLoadingStarted = true;
		RunFakeLoadingSequenceAsync(bootHandoff);
		return true;
	}

	private async Task RunFakeLoadingSequenceAsync(bool bootHandoff)
	{
		_ = 16;
		try
		{
			_isFakeLoadingActive = true;
			BladesLoadingChrome.Visibility = Visibility.Collapsed;
			FakeLoadingOverlay.Visibility = Visibility.Visible;
			FakeLoadingOverlay.Opacity = (bootHandoff ? 1 : 0);
			FakeLoadingIndicator.Opacity = 1.0;
			DashboardContentHost.Opacity = 0.0;
			DashboardStartupScale.ScaleX = 0.965;
			DashboardStartupScale.ScaleY = 0.965;
			DashboardStartupTranslate.Y = 18.0;
			PrepareSignInToastText();
			SignInToast.Opacity = 0.0;
			SignInToastGlow.Opacity = 0.0;
			SignInToastGlowScale.ScaleX = 0.0;
			SignInToastPill.Opacity = 0.0;
			SignInToastPillScale.ScaleX = 0.0;
			SignInToastPillHighlight.Opacity = 0.0;
			SignInToastPillHighlightScale.ScaleX = 0.0;
			SignInToastIcon.Opacity = 0.0;
			SignInToastIconSphere.Opacity = 1.0;
			SignInToastIconAlert.Opacity = 0.0;
			SignInToastText.Opacity = 0.0;
			SignInToastScale.ScaleX = 1.0;
			SignInToastScale.ScaleY = 1.0;
			SignInToastTransform.Y = 0.0;
			SignInToastIconScale.ScaleX = 0.56;
			SignInToastIconScale.ScaleY = 0.56;
			StartFakeLoadingRingAnimation();
			PrewarmStartupUi();
			if (!_viewModel.Settings.EnableFakeLoading) FakeLoadingOverlay.Visibility=Visibility.Collapsed;
			if (!bootHandoff && _viewModel.Settings.EnableFakeLoading)
			{
				await AnimateDoubleAsync(FakeLoadingOverlay, UIElement.OpacityProperty, 0.0, 1.0, 260, null);
			}
			if (_viewModel.Settings.EnableFakeLoading)
			{
				await Task.Delay(1150);
				await AnimateDoubleAsync(FakeLoadingIndicator, UIElement.OpacityProperty, 1.0, 0.0, 120, null);
				await Task.Delay(40);
			}
			CubicEase easing = new CubicEase
			{
				EasingMode = EasingMode.EaseOut
			};
			StopFakeLoadingRingAnimation();
			if (ShouldRunBladesRunnerStartup())
			{
				PrepareBladesRunnerStartup();
				Task loadingFade = AnimateDoubleAsync(FakeLoadingOverlay, UIElement.OpacityProperty, 1.0, 0.0, 140, easing);
				Task opening = AnimateBladesRunnerStartupAsync(easing);
				await Task.WhenAll(loadingFade, opening);
				FakeLoadingOverlay.Visibility = Visibility.Collapsed;
			}
			else
			{
				PlayStartupSound();
				Task task = AnimateDoubleAsync(DashboardContentHost, UIElement.OpacityProperty, 0.0, 1.0, 470, easing);
				Task task2 = AnimateDoubleAsync(DashboardStartupScale, ScaleTransform.ScaleXProperty, 0.965, 1.0, 470, easing);
				Task task3 = AnimateDoubleAsync(DashboardStartupScale, ScaleTransform.ScaleYProperty, 0.965, 1.0, 470, easing);
				Task task4 = AnimateDoubleAsync(DashboardStartupTranslate, TranslateTransform.YProperty, 18.0, 0.0, 470, easing);
				Task task5 = AnimateDoubleAsync(FakeLoadingOverlay, UIElement.OpacityProperty, 1.0, 0.0, 520, easing);
				await Task.WhenAll(task, task2, task3, task4, task5);
			}
			FakeLoadingOverlay.Visibility = Visibility.Collapsed;
			_viewModel.IsBooting = false;
			_isFakeLoadingActive = false;
			QueueFocusFirstButton();
			await Task.Delay(120);
			CubicEase toastEase = new CubicEase
			{
				EasingMode = EasingMode.EaseOut
			};
			SignInToast.Opacity = 1.0;
			Task task6 = AnimateDoubleAsync(SignInToastIcon, UIElement.OpacityProperty, 0.0, 1.0, 150, toastEase);
			Task task7 = AnimateDoubleAsync(SignInToastIconScale, ScaleTransform.ScaleXProperty, 0.56, 1.04, 180, toastEase);
			Task task8 = AnimateDoubleAsync(SignInToastIconScale, ScaleTransform.ScaleYProperty, 0.56, 1.04, 180, toastEase);
			await ((DispatcherObject)this).Dispatcher.InvokeAsync((Action)delegate
			{
			}, DispatcherPriority.Render);
			((DispatcherObject)this).Dispatcher.BeginInvoke((Delegate)(Action)delegate
			{
				PlayNotificationSound();
			}, DispatcherPriority.Background, Array.Empty<object>());
			await Task.WhenAll(task6, task7, task8);
			StartSignInIconBlink();
			Task task9 = AnimateDoubleAsync(SignInToastIconScale, ScaleTransform.ScaleXProperty, 1.04, 1.0, 115, toastEase);
			Task task10 = AnimateDoubleAsync(SignInToastIconScale, ScaleTransform.ScaleYProperty, 1.04, 1.0, 115, toastEase);
			await Task.WhenAll(task9, task10);
			await Task.Delay(55);
			Task task11 = AnimateDoubleAsync(SignInToastGlow, UIElement.OpacityProperty, 0.0, 0.34, 135, toastEase);
			Task task12 = AnimateDoubleAsync(SignInToastGlowScale, ScaleTransform.ScaleXProperty, 0.0, 1.0, 265, toastEase);
			Task task13 = AnimateDoubleAsync(SignInToastPill, UIElement.OpacityProperty, 0.0, 1.0, 110, toastEase);
			Task task14 = AnimateDoubleAsync(SignInToastPillScale, ScaleTransform.ScaleXProperty, 0.0, 1.0, 265, toastEase);
			Task task15 = AnimateDoubleAsync(SignInToastPillHighlight, UIElement.OpacityProperty, 0.0, 0.0, 120, toastEase);
			Task task16 = AnimateDoubleAsync(SignInToastPillHighlightScale, ScaleTransform.ScaleXProperty, 0.0, 1.0, 265, toastEase);
			await Task.WhenAll(task11, task12, task13, task14, task15, task16);
			await Task.Delay(55);
			await AnimateDoubleAsync(SignInToastText, UIElement.OpacityProperty, 0.0, 1.0, 185, null);
			await Task.Delay(2450);
			CubicEase hideEase = new CubicEase
			{
				EasingMode = EasingMode.EaseIn
			};
			await AnimateDoubleAsync(SignInToastText, UIElement.OpacityProperty, 1.0, 0.0, 185, hideEase);
			await Task.Delay(150);
			StopSignInIconBlink();
			Task task17 = AnimateDoubleAsync(SignInToastGlowScale, ScaleTransform.ScaleXProperty, 1.0, 0.0, 320, hideEase);
			Task task18 = AnimateDoubleAsync(SignInToastGlow, UIElement.OpacityProperty, 0.34, 0.0, 270, hideEase);
			Task task19 = AnimateDoubleAsync(SignInToastPillScale, ScaleTransform.ScaleXProperty, 1.0, 0.0, 320, hideEase);
			Task task20 = AnimateDoubleAsync(SignInToastPill, UIElement.OpacityProperty, 1.0, 0.0, 300, hideEase);
			Task task21 = AnimateDoubleAsync(SignInToastPillHighlightScale, ScaleTransform.ScaleXProperty, 1.0, 0.0, 300, hideEase);
			Task task22 = AnimateDoubleAsync(SignInToastPillHighlight, UIElement.OpacityProperty, 0.0, 0.0, 230, hideEase);
			Task task23 = AnimateDoubleAsync(SignInToastIconScale, ScaleTransform.ScaleXProperty, 1.0, 0.52, 210, hideEase);
			Task task24 = AnimateDoubleAsync(SignInToastIconScale, ScaleTransform.ScaleYProperty, 1.0, 0.52, 210, hideEase);
			Task task25 = AnimateDoubleAsync(SignInToastIcon, UIElement.OpacityProperty, 1.0, 0.0, 210, hideEase);
			await Task.WhenAll(task17, task18, task19, task20, task21, task22, task23, task24, task25);
			await AnimateDoubleAsync(SignInToast, UIElement.OpacityProperty, 1.0, 0.0, 80, hideEase);
			ScheduleGuideAudioWarmup();
		}
		catch (Exception exception)
		{
			App.LogException(exception, "MainWindow.RunFakeLoadingSequenceAsync");
		}
		finally
		{
			StopFakeLoadingRingAnimation();
			StopSignInIconBlink();
			FakeLoadingIndicator.Opacity = 0.0;
			DashboardContentHost.Opacity = 1.0;
			DashboardStartupScale.ScaleX = 1.0;
			DashboardStartupScale.ScaleY = 1.0;
			DashboardStartupTranslate.Y = 0.0;
			ResetBladesRunnerStartup();
			SignInToast.Opacity = 0.0;
			SignInToastGlow.Opacity = 0.0;
			SignInToastGlowScale.ScaleX = 0.0;
			SignInToastPill.Opacity = 0.0;
			SignInToastPillScale.ScaleX = 0.0;
			SignInToastPillHighlight.Opacity = 0.0;
			SignInToastPillHighlightScale.ScaleX = 0.0;
			SignInToastIcon.Opacity = 0.0;
			SignInToastIconSphere.Opacity = 0.0;
			SignInToastIconAlert.Opacity = 1.0;
			SignInToastText.Opacity = 0.0;
			SignInToastScale.ScaleX = 1.0;
			SignInToastScale.ScaleY = 1.0;
			SignInToastTransform.Y = 0.0;
			SignInToastIconScale.ScaleX = 0.56;
			SignInToastIconScale.ScaleY = 0.56;
			FakeLoadingOverlay.Opacity = 0.0;
			FakeLoadingOverlay.Visibility = Visibility.Collapsed;
			_isFakeLoadingActive = false;
			QueueFocusFirstButton();
		}
	}

	private bool ShouldRunBladesRunnerStartup()
	{
		return _viewModel != null
			&& string.Equals(_viewModel.Settings.DashboardStyle, "Blades", StringComparison.OrdinalIgnoreCase);
	}

	private bool ShouldUseBladesStartupVideoAudio()
	{
		return _viewModel != null
			&& string.Equals(_viewModel.Settings.DashboardStartup, "Blades", StringComparison.OrdinalIgnoreCase);
	}

	private void PrepareBladesRunnerStartup()
	{
		DashboardContentHost.BeginAnimation(UIElement.OpacityProperty, null);
		DashboardContentHost.Opacity = 1.0;
		DashboardStartupScale.BeginAnimation(ScaleTransform.ScaleXProperty, null);
		DashboardStartupScale.BeginAnimation(ScaleTransform.ScaleYProperty, null);
		DashboardStartupTranslate.BeginAnimation(TranslateTransform.YProperty, null);
		DashboardStartupScale.ScaleX = 1.0;
		DashboardStartupScale.ScaleY = 1.0;
		DashboardStartupTranslate.Y = 0.0;

		BladesBootRunnerOverlay.Visibility = Visibility.Visible;
		BladesBootRunnerOverlay.Opacity = 1.0;
		BladesStartupGreenReveal.Visibility = Visibility.Visible;
		BladesStartupGreenReveal.BeginAnimation(UIElement.OpacityProperty, null);
		BladesStartupGreenReveal.Opacity = 1.0;
		BladesBootLeftRunnerImage.Opacity = 1.0;
		BladesBootRightRunnerImage.Opacity = 1.0;
		BladesBootLeftRunnerOpenImage.Opacity = 0.0;
		BladesBootRightRunnerOpenImage.Opacity = 0.0;
		BladesBootLeftRunnerTranslate.BeginAnimation(TranslateTransform.XProperty, null);
		BladesBootRightRunnerTranslate.BeginAnimation(TranslateTransform.XProperty, null);
		BladesBootLeftRunnerTranslate.X = 714.0 - 739.0;
		BladesBootRightRunnerTranslate.X = 1228.0 - 1203.0;

		BladesArtworkHost.BeginAnimation(UIElement.OpacityProperty, null);
		BladesContentCanvas.BeginAnimation(UIElement.OpacityProperty, null);
		BladesCenterHost.BeginAnimation(UIElement.OpacityProperty, null);
		BladesCenterHost.Opacity = 0.0;
		BladesArtworkHost.StartupElapsedSeconds = 0.0;
		BladesArtworkHost.StartupOpenProgress = 0.0;
		BladesArtworkHost.Opacity = 1.0;
		BladesContentCanvas.Opacity = 0.0;
	}

	private async Task AnimateBladesRunnerStartupAsync(IEasingFunction easing)
	{
		await ((DispatcherObject)this).Dispatcher.InvokeAsync((Action)delegate
		{
		}, DispatcherPriority.Render);

		await RunBladesStartupTimelineAsync();
		BladesArtworkHost.StartupElapsedSeconds = 1.167;
		BladesArtworkHost.StartupOpenProgress = 1.0;
		ResetBladesRunnerStartup();
	}

	private async Task RunBladesStartupTimelineAsync()
	{
		_audioService.Play("dash_2ndLevelClose");
		(TimeSpan Time, double LeftEdge, double RightEdge)[] runnerKeys =
		{
			(TimeSpan.FromSeconds(0.000), 714.0, 1228.0),
			(TimeSpan.FromSeconds(1.0 / 30.0), 691.0, 1250.0),
			(TimeSpan.FromSeconds(2.0 / 30.0), 649.0, 1290.0),
			(TimeSpan.FromSeconds(3.0 / 30.0), 610.0, 1328.0),
			(TimeSpan.FromSeconds(4.0 / 30.0), 571.0, 1365.0),
			(TimeSpan.FromSeconds(5.0 / 30.0), 533.0, 1402.0),
			(TimeSpan.FromSeconds(6.0 / 30.0), 497.0, 1437.0),
			(TimeSpan.FromSeconds(7.0 / 30.0), 448.0, 1484.0),
			(TimeSpan.FromSeconds(8.0 / 30.0), 429.0, 1502.0),
			(TimeSpan.FromSeconds(9.0 / 30.0), 396.0, 1534.0),
			(TimeSpan.FromSeconds(10.0 / 30.0), 363.0, 1565.0),
			(TimeSpan.FromSeconds(11.0 / 30.0), 332.0, 1595.0),
			(TimeSpan.FromSeconds(12.0 / 30.0), 301.0, 1624.0),
			(TimeSpan.FromSeconds(13.0 / 30.0), 271.0, 1653.0),
			(TimeSpan.FromSeconds(14.0 / 30.0), 242.0, 1681.0),
			(TimeSpan.FromSeconds(15.0 / 30.0), 213.0, 1708.0),
			(TimeSpan.FromSeconds(16.0 / 30.0), 185.0, 1734.0),
			(TimeSpan.FromSeconds(17.0 / 30.0), 177.0, 1739.0),
			(TimeSpan.FromSeconds(20.0 / 30.0), 175.0, 1739.0),
			(TimeSpan.FromSeconds(23.0 / 30.0), 169.0, 1741.0),
			(TimeSpan.FromSeconds(26.0 / 30.0), 160.0, 1743.0),
			(TimeSpan.FromSeconds(29.0 / 30.0), 148.0, 1746.0),
			(TimeSpan.FromSeconds(32.0 / 30.0), 128.0, 1749.0),
			(TimeSpan.FromSeconds(1.167), 105.0, 1751.0),
		};

		TimeSpan totalDuration = TimeSpan.FromSeconds(1.167);
		TimeSpan? firstRenderTime = null;
		TimeSpan? previousRenderTime = null;
		TaskCompletionSource completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
		EventHandler handler = (_, args) =>
		{
			if (args is not RenderingEventArgs rendering || previousRenderTime == rendering.RenderingTime)
			{
				return;
			}
			previousRenderTime = rendering.RenderingTime;
			firstRenderTime ??= rendering.RenderingTime;
			TimeSpan elapsed = rendering.RenderingTime - firstRenderTime.Value;
			if (elapsed > totalDuration) elapsed = totalDuration;
			try
			{
				(double leftEdge, double rightEdge) = SampleRunnerEdges(runnerKeys, elapsed);
				ApplyBladesStartupFrame(elapsed.TotalSeconds, leftEdge, rightEdge);
				if (elapsed >= totalDuration) completion.TrySetResult();
			}
			catch (Exception exception)
			{
				completion.TrySetException(exception);
			}
		};
		CompositionTarget.Rendering += handler;
		try
		{
			await completion.Task;
		}
		finally
		{
			CompositionTarget.Rendering -= handler;
		}
	}

	private void ApplyBladesStartupFrame(double seconds, double leftEdge, double rightEdge)
	{
		const double leftRunnerInnerEdge = 739.0;
		const double rightRunnerInnerEdge = 1203.0;
		BladesBootLeftRunnerTranslate.X = leftEdge - leftRunnerInnerEdge;
		BladesBootRightRunnerTranslate.X = rightEdge - rightRunnerInnerEdge;

		double centerProgress = SmoothStep((seconds - 0.36) / 0.30);
		double greenProgress = SampleCurve(seconds, (0.000, 0.18), (0.100, 0.26), (0.200, 0.36), (0.300, 0.55), (0.400, 0.72), (0.500, 0.92), (0.567, 1.0));
		double sideProgress = SmoothStep((seconds - 0.533) / 0.634);
		double contentProgress = SampleCurve(seconds, (0.967, 0.0), (1.000, 0.12), (1.033, 0.36), (1.067, 0.57), (1.100, 0.83), (1.133, 1.0));

		BladesCenterHost.Opacity = centerProgress;
		BladesStartupGreenReveal.Opacity = 1.0 - greenProgress;
		BladesArtworkHost.StartupElapsedSeconds = seconds;
		BladesArtworkHost.StartupOpenProgress = sideProgress;
		BladesContentCanvas.Opacity = contentProgress;
		// Keep the curved shutters in front while the intact blades emerge.
		// The settled runners are already covered by the stack before this fade.
		BladesBootRunnerOverlay.Opacity = 1.0 - SmoothStep((seconds - 1.033) / 0.134);

		BladesBootLeftRunnerImage.Opacity = 1.0;
		BladesBootRightRunnerImage.Opacity = 1.0;
		BladesBootLeftRunnerOpenImage.Opacity = 0.0;
		BladesBootRightRunnerOpenImage.Opacity = 0.0;
	}

	private static (double LeftEdge, double RightEdge) SampleRunnerEdges(
		(TimeSpan Time, double LeftEdge, double RightEdge)[] keys,
		TimeSpan elapsed)
	{
		if (elapsed <= keys[0].Time)
		{
			return (keys[0].LeftEdge, keys[0].RightEdge);
		}

		for (int i = 1; i < keys.Length; i++)
		{
			if (elapsed <= keys[i].Time)
			{
				double local = (elapsed - keys[i - 1].Time).TotalSeconds / (keys[i].Time - keys[i - 1].Time).TotalSeconds;
				return (
					Lerp(keys[i - 1].LeftEdge, keys[i].LeftEdge, local),
					Lerp(keys[i - 1].RightEdge, keys[i].RightEdge, local));
			}
		}

		return (keys[^1].LeftEdge, keys[^1].RightEdge);
	}

	private static double SmoothStep(double value)
	{
		value = Math.Clamp(value, 0.0, 1.0);
		return value * value * (3.0 - (2.0 * value));
	}

	private static double Lerp(double from, double to, double progress)
	{
		return from + ((to - from) * progress);
	}

	private static double SampleCurve(double seconds, params (double Time, double Value)[] samples)
	{
		if (samples.Length == 0)
		{
			return 0.0;
		}

		if (seconds <= samples[0].Time)
		{
			return samples[0].Value;
		}

		for (int i = 1; i < samples.Length; i++)
		{
			if (seconds <= samples[i].Time)
			{
				double span = samples[i].Time - samples[i - 1].Time;
				double local = span <= 0.0 ? 1.0 : (seconds - samples[i - 1].Time) / span;
				return Lerp(samples[i - 1].Value, samples[i].Value, local);
			}
		}

		return samples[^1].Value;
	}

	private void ResetBladesRunnerStartup()
	{
		BladesBootLeftRunnerTranslate.BeginAnimation(TranslateTransform.XProperty, null);
		BladesBootRightRunnerTranslate.BeginAnimation(TranslateTransform.XProperty, null);
		BladesBootRunnerOverlay.BeginAnimation(UIElement.OpacityProperty, null);
		BladesStartupGreenReveal.BeginAnimation(UIElement.OpacityProperty, null);
		BladesCenterHost.BeginAnimation(UIElement.OpacityProperty, null);
		BladesContentCanvas.BeginAnimation(UIElement.OpacityProperty, null);
		BladesArtworkHost.BeginAnimation(UIElement.OpacityProperty, null);
		BladesBootLeftRunnerTranslate.X = 0.0;
		BladesBootRightRunnerTranslate.X = 0.0;
		BladesBootLeftRunnerImage.Opacity = 1.0;
		BladesBootRightRunnerImage.Opacity = 1.0;
		BladesBootLeftRunnerOpenImage.Opacity = 0.0;
		BladesBootRightRunnerOpenImage.Opacity = 0.0;
		BladesBootRunnerOverlay.Opacity = 0.0;
		BladesBootRunnerOverlay.Visibility = Visibility.Collapsed;
		BladesStartupGreenReveal.Opacity = 0.0;
		BladesStartupGreenReveal.Visibility = Visibility.Collapsed;
		BladesCenterHost.Opacity = 1.0;
		BladesArtworkHost.StartupElapsedSeconds = 1.167;
		BladesArtworkHost.StartupOpenProgress = 1.0;
		BladesArtworkHost.Opacity = 1.0;
		BladesContentCanvas.Opacity = 1.0;
	}

	private void StartFakeLoadingRingAnimation()
	{
		DoubleAnimation animation = new DoubleAnimation(0.0, 360.0, TimeSpan.FromMilliseconds(760.0))
		{
			RepeatBehavior = RepeatBehavior.Forever
		};
		FakeLoadingRingRotate.BeginAnimation(RotateTransform.AngleProperty, animation);
	}

	private void StopFakeLoadingRingAnimation()
	{
		FakeLoadingRingRotate.BeginAnimation(RotateTransform.AngleProperty, null);
	}

	private async Task TransitionBladesSubmenuAsync(string target, Action change)
	{
		if (_viewModel.IsBladesMenuTransitioning) return;
		_viewModel.IsBladesMenuTransitioning = true;
		bool wasOpen = _viewModel.IsBladesSubmenuOpen;
		bool opening = target.Length > 0;
		bool enteringFromRoot = !wasOpen && opening;
		try
		{
			BladesContentCanvas.IsHitTestVisible = false;
			await AnimateDoubleAsync(BladesContentCanvas, UIElement.OpacityProperty, 1, 0, 100, null);
			if (wasOpen != opening)
			{
				BladesArtworkHost.Visibility = Visibility.Visible;
				if (opening) change();
				await AnimateDoubleAsync(BladesArtworkHost, Controls.BladesArtworkHost.SubmenuExpansionProperty,
					opening ? 0 : 1, opening ? 1 : 0, 300, new CubicEase { EasingMode=EasingMode.EaseOut });
				if (!opening) change();
			}
			else { await Task.Delay(100); change(); }
			if (enteringFromRoot && _viewModel.Settings.EnableFakeLoading && ShouldRunBladesSubmenuFakeLoading(target))
			{
				BladesLoadingTitle.Text=target=="GamesLibrary" ? "My Games" : _viewModel.ActiveBladesTitle;
				await RunMenuFakeLoadingSequenceAsync();
				BladesLoadingTitle.ClearValue(TextBlock.TextProperty);
			}
			await AnimateDoubleAsync(BladesContentCanvas, UIElement.OpacityProperty, 0, 1, 180, null);
		}
		catch (Exception ex) { App.LogException(ex,"Blades.SubmenuTransition"); }
		finally
		{
			BladesArtworkHost.ClearValue(UIElement.VisibilityProperty);
			BladesArtworkHost.SubmenuExpansion = _viewModel.IsBladesSubmenuOpen ? 1 : 0;
			BladesArtworkHost.BeginAnimation(Controls.BladesArtworkHost.SubmenuExpansionProperty,null);
			BladesContentCanvas.BeginAnimation(UIElement.OpacityProperty,null);
			BladesContentCanvas.Opacity=1;
			BladesContentCanvas.IsHitTestVisible=true;
			_viewModel.IsBladesMenuTransitioning=false;
			QueueBladesMenuFocus();
		}
	}

	private bool ShouldRunBladesSubmenuFakeLoading(string target)
	{
		string kind = target switch
		{
			"GamesLibraryRoot" or "GamesLibrary" => "games",
			"Achievements" => "achievements",
			"PlayedGames" => "played-games",
			_ => string.Empty
		};
		return kind.Length > 0 && ShouldRunMenuFakeLoading(kind);
	}

	private bool MaybeRunMenuFakeLoading(string propertyName)
	{
		if (!_viewModel.Settings.EnableFakeLoading)
		{
			return false;
		}
		string kind;
		if (propertyName == "IsLauncherSettingsOpen" && _viewModel.IsLauncherSettingsOpen)
		{
			kind = "settings";
		}
		else if (propertyName == "IsMyGamesOpen" && _viewModel.IsMyGamesOpen)
		{
			if (string.Equals(_viewModel.LibraryMenuTitle, "My Apps", StringComparison.OrdinalIgnoreCase))
			{
				kind = "apps";
			}
			else if (string.Equals(_viewModel.LibraryMenuTitle, "My Games", StringComparison.OrdinalIgnoreCase))
			{
				kind = "games";
			}
			else
			{
				return false;
			}
		}
		else
		{
			return false;
		}
		if (!ShouldRunMenuFakeLoading(kind))
		{
			return false;
		}
		((DispatcherObject)this).Dispatcher.BeginInvoke((Delegate)(Action)delegate
		{
			if (GetActiveOverlay() is FrameworkElement frameworkElement)
			{
				frameworkElement.BeginAnimation(UIElement.OpacityProperty, null);
				frameworkElement.Opacity = 0.0;
			}
			_ = RunMenuFakeLoadingSequenceAsync();
		}, (DispatcherPriority)6, Array.Empty<object>());
		return true;
	}

	private bool ShouldRunMenuFakeLoading(string kind)
	{
		switch (kind)
		{
		case "settings":
			_settingsOpenCount++;
			return _settingsOpenCount % 4 == 0;
		case "games":
			_gamesOpenCount++;
			return _gamesOpenCount % 4 == 0;
		case "achievements":
			_achievementsOpenCount++;
			return _achievementsOpenCount % 4 == 0;
		case "played-games":
			_playedGamesOpenCount++;
			return _playedGamesOpenCount % 4 == 0;
		case "music":
			_musicOpenCount++;
			return _musicOpenCount % 4 == 0;
		case "apps":
			_appsOpenCount++;
			return _appsOpenCount % 4 == 0;
		default:
			return false;
		}
	}

	private async Task RunMenuFakeLoadingSequenceAsync()
	{
		if (_isFakeLoadingActive || _isMenuFakeLoadingActive || _viewModel.IsBooting)
		{
			return;
		}
		try
		{
			_isMenuFakeLoadingActive = true;
			BladesLoadingChrome.Visibility = Visibility.Visible;
			FakeLoadingOverlay.BeginAnimation(UIElement.OpacityProperty, null);
			FakeLoadingIndicator.BeginAnimation(UIElement.OpacityProperty, null);
			FakeLoadingOverlay.Visibility = Visibility.Visible;
			FakeLoadingOverlay.Opacity = 1.0;
			FakeLoadingIndicator.Opacity = 1.0;
			StartFakeLoadingRingAnimation();
			await Task.Delay(1080);
			CubicEase easing = new CubicEase
			{
				EasingMode = EasingMode.EaseOut
			};
			Task indicatorFade = AnimateDoubleAsync(FakeLoadingIndicator, UIElement.OpacityProperty, 1.0, 0.0, 180, easing);
			Task overlayFade = AnimateDoubleAsync(FakeLoadingOverlay, UIElement.OpacityProperty, 1.0, 0.0, 320, easing);
			await Task.WhenAll(indicatorFade, overlayFade);
			AnimateActiveOverlayIn();
		}
		catch (Exception exception)
		{
			App.LogException(exception, "MainWindow.RunMenuFakeLoadingSequenceAsync");
		}
		finally
		{
			StopFakeLoadingRingAnimation();
			FakeLoadingIndicator.Opacity = 0.0;
			FakeLoadingOverlay.Opacity = 0.0;
			FakeLoadingOverlay.Visibility = Visibility.Collapsed;
			_isMenuFakeLoadingActive = false;
		}
	}

	private void StartSignInIconBlink()
	{
		DoubleAnimationUsingKeyFrames doubleAnimationUsingKeyFrames = new DoubleAnimationUsingKeyFrames
		{
			Duration = TimeSpan.FromMilliseconds(2160.0),
			RepeatBehavior = RepeatBehavior.Forever,
			FillBehavior = FillBehavior.HoldEnd
		};
		doubleAnimationUsingKeyFrames.KeyFrames.Add(new LinearDoubleKeyFrame(1.0, KeyTime.FromTimeSpan(TimeSpan.Zero)));
		doubleAnimationUsingKeyFrames.KeyFrames.Add(new LinearDoubleKeyFrame(1.0, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(960.0))));
		doubleAnimationUsingKeyFrames.KeyFrames.Add(new LinearDoubleKeyFrame(0.0, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(1080.0))));
		doubleAnimationUsingKeyFrames.KeyFrames.Add(new LinearDoubleKeyFrame(0.0, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(2040.0))));
		doubleAnimationUsingKeyFrames.KeyFrames.Add(new LinearDoubleKeyFrame(1.0, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(2160.0))));
		DoubleAnimationUsingKeyFrames doubleAnimationUsingKeyFrames2 = new DoubleAnimationUsingKeyFrames
		{
			Duration = TimeSpan.FromMilliseconds(2160.0),
			RepeatBehavior = RepeatBehavior.Forever,
			FillBehavior = FillBehavior.HoldEnd
		};
		doubleAnimationUsingKeyFrames2.KeyFrames.Add(new LinearDoubleKeyFrame(0.0, KeyTime.FromTimeSpan(TimeSpan.Zero)));
		doubleAnimationUsingKeyFrames2.KeyFrames.Add(new LinearDoubleKeyFrame(0.0, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(960.0))));
		doubleAnimationUsingKeyFrames2.KeyFrames.Add(new LinearDoubleKeyFrame(1.0, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(1080.0))));
		doubleAnimationUsingKeyFrames2.KeyFrames.Add(new LinearDoubleKeyFrame(1.0, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(2040.0))));
		doubleAnimationUsingKeyFrames2.KeyFrames.Add(new LinearDoubleKeyFrame(0.0, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(2160.0))));
		SignInToastIconSphere.BeginAnimation(UIElement.OpacityProperty, doubleAnimationUsingKeyFrames);
		SignInToastIconAlert.BeginAnimation(UIElement.OpacityProperty, doubleAnimationUsingKeyFrames2);
	}

	private void StopSignInIconBlink()
	{
		SignInToastIconSphere.BeginAnimation(UIElement.OpacityProperty, null);
		SignInToastIconAlert.BeginAnimation(UIElement.OpacityProperty, null);
		SignInToastIconSphere.Opacity = 0.0;
		SignInToastIconAlert.Opacity = 1.0;
	}

	private static Task AnimateDoubleAsync(IAnimatable target, DependencyProperty property, double from, double to, int milliseconds, IEasingFunction? easing)
	{
		TaskCompletionSource completion = new TaskCompletionSource();
		DoubleAnimation doubleAnimation = new DoubleAnimation(from, to, TimeSpan.FromMilliseconds(milliseconds))
		{
			EasingFunction = easing,
			FillBehavior = FillBehavior.HoldEnd
		};
		doubleAnimation.Completed += delegate
		{
			completion.TrySetResult();
		};
		target.BeginAnimation(property, doubleAnimation);
		return completion.Task;
	}

	private static async Task DelayedAnimateDoubleAsync(IAnimatable target, DependencyProperty property, double from, double to, int milliseconds, IEasingFunction? easing, int delayMilliseconds)
	{
		if (delayMilliseconds > 0)
		{
			await Task.Delay(delayMilliseconds);
		}

		await AnimateDoubleAsync(target, property, from, to, milliseconds, easing);
	}

	private void MusicFullscreenHint_OnMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
	{
		if (_viewModel.OpenMusicVisualizerFullscreenCommand.CanExecute(null))
		{
			_viewModel.OpenMusicVisualizerFullscreenCommand.Execute(null);
			e.Handled = true;
		}
	}

	private void MusicFolderLink_OnPreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
	{
		if (_viewModel.OpenMusicFolderCommand.CanExecute(null))
		{
			_viewModel.OpenMusicFolderCommand.Execute(null);
			e.Handled = true;
		}
	}

	private void SettingsOption_OnFocusOrMouseEnter(object sender, RoutedEventArgs e)
	{
		if (sender is FrameworkElement { Tag: string tag } && !string.IsNullOrWhiteSpace(tag))
		{
			if (tag.Count((char character) => character == '|') >= 2)
			{
				string[] array = tag.Split('|', 3);
				UpdateSettingsDescription(array[1], array[2]);
			}
			else
			{
				string[] array2 = tag.Split('|', 2);
				UpdateSettingsDescription(array2[0], (array2.Length > 1) ? array2[1] : string.Empty);
			}
		}
	}

	private void AudioOutputDeviceComboBox_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
	{
		if (sender is System.Windows.Controls.ComboBox { SelectedItem: string selectedDevice } && !string.IsNullOrWhiteSpace(selectedDevice))
		{
			_viewModel.AudioOutputDeviceName = selectedDevice;
		}
	}

	private void SettingsCategory_OnClick(object sender, RoutedEventArgs e)
	{
		if (!(sender is FrameworkElement { Tag: string tag }))
		{
			return;
		}
		string[] array = tag.Split('|', 3);
		if (array.Length < 3)
		{
			return;
		}
		FrameworkElement frameworkElement2 = array[0] switch
		{
			"console" => SystemSettingsConsolePanel, 
			"dashboard" => SystemSettingsDashboardPanel, 
			"games" => SystemSettingsGamesPanel, 
			"audio" => SystemSettingsAudioPanel, 
			"data" => SystemSettingsDataPanel, 
			_ => null, 
		};
		if (frameworkElement2 != null)
		{
			_audioService.Play("settings-box");
			if (string.Equals(array[0], "audio", StringComparison.OrdinalIgnoreCase))
			{
				_viewModel.RefreshAudioOutputDevices();
			}
			OpenSystemSettingsPanel(frameworkElement2, array[1], array[2]);
		}
	}

	private void SettingsBackToCategories_OnClick(object sender, RoutedEventArgs e)
	{
		_audioService.Play("menu-out");
		ShowSystemSettingsCategories();
	}

	private void OpenSystemSettingsPanel(FrameworkElement panel, string title, string description)
	{
		SystemSettingsCategoryPanel.Visibility = Visibility.Collapsed;
		foreach (FrameworkElement systemSettingsPanel in GetSystemSettingsPanels())
		{
			systemSettingsPanel.Visibility = ((systemSettingsPanel != panel) ? Visibility.Collapsed : Visibility.Visible);
		}
		_activeSystemSettingsPanel = panel;
		SettingsHeaderTitle.Text = title;
		UpdateSettingsDescription(title, description);
		panel.BeginAnimation(UIElement.OpacityProperty, null);
		panel.Opacity = 1.0;
		EnsureOverlayTranslateTransform(panel).X = 0.0;
		((DispatcherObject)this).Dispatcher.BeginInvoke((Delegate)(Action)delegate
		{
			TryFocus(FindFocusableControl((DependencyObject?)(object)panel));
		}, (DispatcherPriority)4, Array.Empty<object>());
	}

	private void ShowSystemSettingsCategories()
	{
		foreach (FrameworkElement systemSettingsPanel in GetSystemSettingsPanels())
		{
			systemSettingsPanel.Visibility = Visibility.Collapsed;
		}
		_activeSystemSettingsPanel = null;
		SystemSettingsCategoryPanel.Visibility = Visibility.Visible;
		SettingsHeaderTitle.Text = "System Settings";
		UpdateSettingsDescription("System Settings", "Choose a settings category.");
		SystemSettingsCategoryPanel.BeginAnimation(UIElement.OpacityProperty, null);
		SystemSettingsCategoryPanel.Opacity = 1.0;
		EnsureOverlayTranslateTransform(SystemSettingsCategoryPanel).X = 0.0;
		((DispatcherObject)this).Dispatcher.BeginInvoke((Delegate)(Action)delegate
		{
			TryFocus(FindFocusableControl((DependencyObject?)(object)SystemSettingsCategoryPanel));
		}, (DispatcherPriority)4, Array.Empty<object>());
	}

	private IEnumerable<FrameworkElement> GetSystemSettingsPanels()
	{
		yield return SystemSettingsConsolePanel;
		yield return SystemSettingsDashboardPanel;
		yield return SystemSettingsGamesPanel;
		yield return SystemSettingsAudioPanel;
		yield return SystemSettingsDataPanel;
	}

	private void UpdateSettingsDescription(string title, string description)
	{
		SettingsDescriptionTitle.Text = title;
		SettingsDescriptionText.Text = description;
	}

	private void ViewModel_OnPropertyChanged(object? sender, PropertyChangedEventArgs e)
	{
		if (e.PropertyName == "IsBladesSubmenuOpen" && !_viewModel.IsBladesSubmenuOpen && !_viewModel.IsBladesMenuTransitioning)
		{
			BladesArtworkHost.BeginAnimation(Controls.BladesArtworkHost.SubmenuExpansionProperty,null);
			BladesArtworkHost.SubmenuExpansion=0;
		}
		if (e.PropertyName == "CurrentTab")
		{
			AnimateTabChange();
			UpdateThemeBackgroundVisual();
			UpdateBingBackgroundVisual();
			QueueFocusFirstButton();
			return;
		}
		if (e.PropertyName == "SelectedBladesMenuIndex" || e.PropertyName == "ActiveBladesMenuIndex" || e.PropertyName == "ActiveBladesMenuItems")
		{
			if (!_viewModel.IsBladesAchievementsSubmenu)
			{
				QueueBladesMenuFocus();
			}
			return;
		}
		if (e.PropertyName == "IsBladesAchievementsSubmenu" || e.PropertyName == "IsBladesAchievementListFocused" || e.PropertyName == "SelectedBladesAchievementGameIndex" || e.PropertyName == "SelectedBladesAchievementIndex" || e.PropertyName == "BladesAchievementGameItems" || e.PropertyName == "BladesAchievementItems")
		{
			QueueBladesAchievementsFocus();
			return;
		}
		if (e.PropertyName == "IsYouTubeTvOpen")
		{
			UpdateYouTubeTvHost();
			UpdateThemeBackgroundVisual();
			UpdateBingBackgroundVisual();
			return;
		}
		bool flag;
		switch (e.PropertyName)
		{
		case "IsDetailsOpen":
		case "IsMyGamesOpen":
		case "IsLauncherSettingsOpen":
		case "IsProfileEditorOpen":
		case "IsThemeMenuOpen":
		case "IsThemeCreatorOpen":
		case "IsDashboardCustomizerOpen":
		case "IsSteamSetupOpen":
		case "IsMusicPlayerOpen":
			flag = true;
			break;
		default:
			flag = false;
			break;
		}
		if (flag)
		{
			if (e.PropertyName == "IsLauncherSettingsOpen" && _viewModel.IsLauncherSettingsOpen && !_viewModel.IsDashboardCustomizerOpen && !_viewModel.IsThemeCreatorOpen && !_viewModel.IsSteamSetupOpen)
			{
				ShowSystemSettingsCategories();
			}
			if (!MaybeRunMenuFakeLoading(e.PropertyName))
			{
				((DispatcherObject)this).Dispatcher.BeginInvoke((Delegate)new Action(AnimateActiveOverlayIn), (DispatcherPriority)6, Array.Empty<object>());
			}
			UpdateThemeBackgroundVisual();
			UpdateBingBackgroundVisual();
			QueueFocusFirstButton();
		}
		else if (e.PropertyName == "CurrentThemeBackgroundPath")
		{
			UpdateThemeBackgroundVisual();
		}
		else if (e.PropertyName == "SelectedGameDetailsTabKey")
		{
			AnimateGameDetailsTabChange();
			QueueGameDetailsTileFocus();
		}
		else if (e.PropertyName == "SelectedMusicBrowserResultItem")
		{
			((DispatcherObject)this).Dispatcher.BeginInvoke((Delegate)(Action)ScrollMusicBrowserResultIntoView, (DispatcherPriority)6, Array.Empty<object>());
		}
		else if (e.PropertyName == "CurrentMusicTrack")
		{
			((DispatcherObject)this).Dispatcher.BeginInvoke((Delegate)(Action)ScrollCurrentMusicTrackIntoView, (DispatcherPriority)6, Array.Empty<object>());
		}
		else if (e.PropertyName == "SelectedMusicTrack")
		{
			((DispatcherObject)this).Dispatcher.BeginInvoke((Delegate)(Action)ScrollCurrentMusicTrackIntoView, (DispatcherPriority)6, Array.Empty<object>());
		}
	}

	private void UpdateThemeBackgroundVisual(bool animate = true)
	{
		try
		{
			string currentThemeBackgroundPath = _viewModel.CurrentThemeBackgroundPath;
			if (string.Equals(_appliedThemeBackgroundPath, currentThemeBackgroundPath, StringComparison.OrdinalIgnoreCase) && ThemeBackgroundLayer.Visibility == Visibility.Visible == !string.IsNullOrWhiteSpace(currentThemeBackgroundPath))
			{
				return;
			}
			ThemeBackgroundLayer.BeginAnimation(UIElement.OpacityProperty, null);
			UltraWideThemeBackgroundLayer.BeginAnimation(UIElement.OpacityProperty, null);
			if (string.IsNullOrWhiteSpace(currentThemeBackgroundPath))
			{
				_appliedThemeBackgroundPath = string.Empty;
				if (!animate)
				{
					ThemeBackgroundLayer.Opacity = 0.0;
					ThemeBackgroundLayer.Visibility = Visibility.Collapsed;
					ThemeBackgroundImage.Source = null;
					UltraWideThemeBackgroundLayer.Opacity = 0.0;
					UltraWideThemeBackgroundLayer.Visibility = Visibility.Collapsed;
					UltraWideThemeBackgroundImage.Source = null;
					return;
				}
				DoubleAnimation doubleAnimation = new DoubleAnimation(ThemeBackgroundLayer.Opacity, 0.0, TimeSpan.FromMilliseconds(260.0))
				{
					EasingFunction = new SineEase
					{
						EasingMode = EasingMode.EaseOut
					}
				};
				doubleAnimation.Completed += delegate
				{
					ThemeBackgroundLayer.Visibility = Visibility.Collapsed;
					ThemeBackgroundImage.Source = null;
					UltraWideThemeBackgroundLayer.Visibility = Visibility.Collapsed;
					UltraWideThemeBackgroundImage.Source = null;
				};
				ThemeBackgroundLayer.BeginAnimation(UIElement.OpacityProperty, doubleAnimation);
				UltraWideThemeBackgroundLayer.BeginAnimation(UIElement.OpacityProperty, doubleAnimation.Clone());
				return;
			}
			BitmapSource decodedImage = ImageCacheService.GetDecodedImage(AppPathResolver.Resolve(currentThemeBackgroundPath), 1920);
			if (decodedImage == null)
			{
				_appliedThemeBackgroundPath = string.Empty;
				ThemeBackgroundLayer.Opacity = 0.0;
				ThemeBackgroundLayer.Visibility = Visibility.Collapsed;
				ThemeBackgroundImage.Source = null;
				UltraWideThemeBackgroundLayer.Opacity = 0.0;
				UltraWideThemeBackgroundLayer.Visibility = Visibility.Collapsed;
				UltraWideThemeBackgroundImage.Source = null;
				return;
			}
			_appliedThemeBackgroundPath = currentThemeBackgroundPath;
			ThemeBackgroundLayer.Visibility = Visibility.Visible;
			ThemeBackgroundImage.Source = decodedImage;
			UltraWideThemeBackgroundLayer.Visibility = Visibility.Visible;
			UltraWideThemeBackgroundImage.Source = decodedImage;
			if (!animate)
			{
				ThemeBackgroundLayer.Opacity = 1.0;
				UltraWideThemeBackgroundLayer.Opacity = 1.0;
				return;
			}
			ThemeBackgroundLayer.Opacity = 0.0;
			UltraWideThemeBackgroundLayer.Opacity = 0.0;
			ThemeBackgroundLayer.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(0.0, 1.0, TimeSpan.FromMilliseconds(320.0))
			{
				EasingFunction = new SineEase
				{
					EasingMode = EasingMode.EaseOut
				}
			});
			UltraWideThemeBackgroundLayer.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(0.0, 1.0, TimeSpan.FromMilliseconds(320.0))
			{
				EasingFunction = new SineEase
				{
					EasingMode = EasingMode.EaseOut
				}
			});
		}
		catch (Exception exception)
		{
			App.LogException(exception, "MainWindow.UpdateThemeBackgroundVisual");
		}
	}

	private void UpdateBingBackgroundVisual(bool animate = true)
	{
		try
		{
			bool flag = string.Equals(_viewModel.CurrentTab?.Key, "bing", StringComparison.OrdinalIgnoreCase);
			if (!flag && string.IsNullOrEmpty(_appliedBingBackgroundPath) && BingBackgroundLayer.Visibility != Visibility.Visible)
			{
				return;
			}
			string text = (flag ? AppPaths.ResolvePath(BingBackgroundRelativePath) : string.Empty);
			bool flag2 = BingBackgroundLayer.Visibility == Visibility.Visible;
			if (string.Equals(_appliedBingBackgroundPath, text, StringComparison.OrdinalIgnoreCase) && flag2 == flag)
			{
				return;
			}
			BingBackgroundLayer.BeginAnimation(UIElement.OpacityProperty, null);
			UltraWideBingBackgroundLayer.BeginAnimation(UIElement.OpacityProperty, null);
			if (string.IsNullOrWhiteSpace(text))
			{
				_appliedBingBackgroundPath = string.Empty;
				if (!animate)
				{
					BingBackgroundLayer.Opacity = 0.0;
					BingBackgroundLayer.Visibility = Visibility.Collapsed;
					BingBackgroundImage.Source = null;
					UltraWideBingBackgroundLayer.Opacity = 0.0;
					UltraWideBingBackgroundLayer.Visibility = Visibility.Collapsed;
					UltraWideBingBackgroundImage.Source = null;
					return;
				}
				DoubleAnimation doubleAnimation = new DoubleAnimation(BingBackgroundLayer.Opacity, 0.0, TimeSpan.FromMilliseconds(300.0))
				{
					EasingFunction = new SineEase
					{
						EasingMode = EasingMode.EaseOut
					}
				};
				doubleAnimation.Completed += delegate
				{
					BingBackgroundLayer.Visibility = Visibility.Collapsed;
					BingBackgroundImage.Source = null;
					UltraWideBingBackgroundLayer.Visibility = Visibility.Collapsed;
					UltraWideBingBackgroundImage.Source = null;
				};
				BingBackgroundLayer.BeginAnimation(UIElement.OpacityProperty, doubleAnimation);
				UltraWideBingBackgroundLayer.BeginAnimation(UIElement.OpacityProperty, doubleAnimation.Clone());
				return;
			}
			BitmapSource decodedImage = ImageCacheService.GetDecodedImage(text, 1920);
			if (decodedImage == null)
			{
				_appliedBingBackgroundPath = string.Empty;
				BingBackgroundLayer.Opacity = 0.0;
				BingBackgroundLayer.Visibility = Visibility.Collapsed;
				BingBackgroundImage.Source = null;
				UltraWideBingBackgroundLayer.Opacity = 0.0;
				UltraWideBingBackgroundLayer.Visibility = Visibility.Collapsed;
				UltraWideBingBackgroundImage.Source = null;
				return;
			}
			_appliedBingBackgroundPath = text;
			BingBackgroundLayer.Visibility = Visibility.Visible;
			BingBackgroundImage.Source = decodedImage;
			UltraWideBingBackgroundLayer.Visibility = Visibility.Visible;
			UltraWideBingBackgroundImage.Source = decodedImage;
			if (!animate)
			{
				BingBackgroundLayer.Opacity = 1.0;
				UltraWideBingBackgroundLayer.Opacity = 1.0;
				return;
			}
			BingBackgroundLayer.Opacity = 0.0;
			UltraWideBingBackgroundLayer.Opacity = 0.0;
			BingBackgroundLayer.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(0.0, 1.0, TimeSpan.FromMilliseconds(320.0))
			{
				EasingFunction = new SineEase
				{
					EasingMode = EasingMode.EaseOut
				}
			});
			UltraWideBingBackgroundLayer.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(0.0, 1.0, TimeSpan.FromMilliseconds(320.0))
			{
				EasingFunction = new SineEase
				{
					EasingMode = EasingMode.EaseOut
				}
			});
		}
		catch (Exception exception)
		{
			App.LogException(exception, "MainWindow.UpdateBingBackgroundVisual");
		}
	}

	private void AnimateTabChange()
	{
		if (_viewModel.Tabs.Count == 0)
		{
			return;
		}
		int num = ((_viewModel.CurrentTab == null) ? _lastTabIndex : _viewModel.Tabs.IndexOf(_viewModel.CurrentTab));
		if (num < 0)
		{
			num = Math.Clamp(_lastTabIndex, 0, _viewModel.Tabs.Count - 1);
		}
		object lastRenderedTab = _lastRenderedTab;
		DashboardTabViewModel currentTab = _viewModel.CurrentTab;
		if (lastRenderedTab == currentTab)
		{
			_lastTabIndex = num;
			return;
		}
		int num2 = ((num >= _lastTabIndex) ? 1 : (-1));
		_lastTabIndex = num;
		_lastRenderedTab = currentTab;
		_isAnimatingTab = true;
		_queuedTabStep = 0;
		try
		{
			ContentSlide.BeginAnimation(TranslateTransform.XProperty, null);
			ContentHost.BeginAnimation(UIElement.OpacityProperty, null);
			TabTransitionSlide.BeginAnimation(TranslateTransform.XProperty, null);
			TabTransitionLayer.BeginAnimation(UIElement.OpacityProperty, null);
			AdjacentPreviewLayer.BeginAnimation(UIElement.OpacityProperty, null);
			PreviousPreviewOffset.BeginAnimation(TranslateTransform.XProperty, null);
			NextPreviewOffset.BeginAnimation(TranslateTransform.XProperty, null);
			UpdateAdjacentPreviewSnapshots();
			PrepareLiveAdjacentPreviews();
			PrepareTabTransitionStrip(lastRenderedTab, currentTab, num2);
			ContentHost.Opacity = 0.0;
			ContentSlide.X = 0.0;
			TabTransitionLayer.Visibility = Visibility.Visible;
			TabTransitionLayer.Opacity = 1.0;
			AdjacentPreviewLayer.Visibility = Visibility.Visible;
			AdjacentPreviewLayer.Opacity = 0.36;
			BeginAdjacentPreviewOffsetAnimation();
			double num3 = -1280.0;
			double toValue = ((num2 > 0) ? (-2560.0) : 0.0);
			TabTransitionSlide.X = num3;
			TimeSpan timeSpan = TimeSpan.FromMilliseconds(420.0);
			DoubleAnimation doubleAnimation = new DoubleAnimation(num3, toValue, timeSpan)
			{
				EasingFunction = new QuarticEase
				{
					EasingMode = EasingMode.EaseInOut
				}
			};
			doubleAnimation.Completed += delegate
			{
				FinishTabAnimation();
			};
			TabTransitionSlide.BeginAnimation(TranslateTransform.XProperty, doubleAnimation);
		}
		catch
		{
			_isAnimatingTab = false;
		}
	}

	private void BeginAdjacentPreviewOffsetAnimation()
	{
		double fromLeft = -104.0;
		double fromRight = 104.0;
		TimeSpan duration = TimeSpan.FromMilliseconds(420.0);
		IEasingFunction easingFunction = new QuarticEase
		{
			EasingMode = EasingMode.EaseInOut
		};
		PreviousPreviewOffset.X = fromLeft;
		NextPreviewOffset.X = fromRight;
		PreviousPreviewOffset.BeginAnimation(TranslateTransform.XProperty, new DoubleAnimation(fromLeft, 0.0, duration)
		{
			EasingFunction = easingFunction
		});
		NextPreviewOffset.BeginAnimation(TranslateTransform.XProperty, new DoubleAnimation(fromRight, 0.0, duration)
		{
			EasingFunction = easingFunction
		});
	}

	private void PrepareTabTransitionStrip(object? oldTab, object? newTab, int direction)
	{
		if (direction > 0)
		{
			TransitionLeftHost.Content = null;
			TransitionCenterHost.Content = oldTab;
			TransitionRightHost.Content = newTab;
		}
		else
		{
			TransitionLeftHost.Content = newTab;
			TransitionCenterHost.Content = oldTab;
			TransitionRightHost.Content = null;
		}
	}

	private object? GetTabNear(object? tab, int step)
	{
		if (!(tab is DashboardTabViewModel item))
		{
			return null;
		}
		int num = _viewModel.Tabs.IndexOf(item) + step;
		if (num < 0 || num >= _viewModel.Tabs.Count)
		{
			return null;
		}
		return _viewModel.Tabs[num];
	}

	private void FinishTabAnimation()
	{
		_isAnimatingTab = false;
		TabTransitionLayer.Visibility = Visibility.Collapsed;
		TabTransitionLayer.Opacity = 0.0;
		TabTransitionSlide.X = -1280.0;
		PreviousPreviewOffset.X = 0.0;
		NextPreviewOffset.X = 0.0;
		TransitionLeftHost.Content = null;
		TransitionCenterHost.Content = null;
		TransitionRightHost.Content = null;
		ContentHost.Opacity = 1.0;
		ContentSlide.X = 0.0;
		UpdateAdjacentPreviewSnapshots();
		PrepareLiveAdjacentPreviews();
		AdjacentPreviewLayer.Visibility = Visibility.Visible;
		AdjacentPreviewLayer.Opacity = 0.36;
		if (IsVideoTabCurrent() && !IsOverlayOpen())
		{
			_lastFocusedButtonByTab.Remove(_viewModel.CurrentTab.Key);
			QueueFocusFirstButton();
		}
		if (_queuedTabStep == 0 || IsOverlayOpen())
		{
			_queuedTabStep = 0;
			return;
		}
		int step = _queuedTabStep;
		_queuedTabStep = 0;
		((DispatcherObject)this).Dispatcher.BeginInvoke((Delegate)(Action)delegate
		{
			_viewModel.MoveTab(step);
		}, (DispatcherPriority)5, Array.Empty<object>());
	}

	private void FadeAdjacentPreviewLayer(double toOpacity, double milliseconds)
	{
		DoubleAnimation doubleAnimation = new DoubleAnimation(toOpacity, TimeSpan.FromMilliseconds(milliseconds))
		{
			EasingFunction = new SineEase
			{
				EasingMode = EasingMode.EaseOut
			}
		};
		AdjacentPreviewLayer.BeginAnimation(UIElement.OpacityProperty, doubleAnimation);
	}

	private void AnimateActiveOverlayIn()
	{
		if (GetActiveOverlay() is FrameworkElement { IsVisible: not false } frameworkElement)
		{
			if (frameworkElement == MyGamesOverlay)
			{
				AnimateLibraryMenuIn();
			}
			else if (frameworkElement == LauncherSettingsOverlay)
			{
				AnimateSettingsMenuIn();
			}
			else if (frameworkElement == MusicPlayerOverlay)
			{
				AnimateMusicPlayerIn();
			}
			else if (frameworkElement == GameDetailsOverlay)
			{
				AnimateGameDetailsIn();
			}
			else if (frameworkElement == ThemeMenuOverlay)
			{
				AnimateThemeMenuIn();
			}
			else if (frameworkElement == ProfileEditorOverlay)
			{
				AnimateProfileMenuIn();
			}
			else
			{
				AnimateOverlayIn(frameworkElement);
			}
		}
	}

	private static void AnimateOverlayIn(FrameworkElement overlay)
	{
		overlay.BeginAnimation(UIElement.OpacityProperty, null);
		TranslateTransform translateTransform = EnsureOverlayTranslateTransform(overlay);
		translateTransform.BeginAnimation(TranslateTransform.XProperty, null);
		translateTransform.BeginAnimation(TranslateTransform.YProperty, null);
		overlay.Opacity = 0.0;
		translateTransform.X = 48.0;
		translateTransform.Y = 0.0;
		overlay.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(0.0, 1.0, TimeSpan.FromMilliseconds(135.0))
		{
			EasingFunction = new SineEase
			{
				EasingMode = EasingMode.EaseOut
			}
		});
		translateTransform.BeginAnimation(TranslateTransform.XProperty, new DoubleAnimation(0.0, TimeSpan.FromMilliseconds(215.0))
		{
			EasingFunction = new QuarticEase
			{
				EasingMode = EasingMode.EaseOut
			}
		});
	}

	private void AnimateLibraryMenuIn()
	{
		BeginLayerAnimation(MyGamesOverlay, EnsureOverlayTranslateTransform(MyGamesOverlay), 1.0, 0.0, 0.0, 1.0, 120, 0);
		BeginLayerAnimation(MyGamesBackdrop, null, 0.0, 0.0, 0.26, 0.26, 260, 0);
		BeginLayerAnimation(MyGamesFilterChrome, MyGamesFilterChromeTranslate, -24.0, 0.0, 0.0, 1.0, 240, 40);
		BeginLayerAnimation(MyGamesHeaderChrome, MyGamesHeaderChromeTranslate, 42.0, 0.0, 0.0, 1.0, 260, 60);
		BeginLayerAnimation(MyGamesStrip, MyGamesStripTranslate, 168.0, 0.0, 0.0, 1.0, 360, 35);
		BeginLayerAnimation(MyGamesFooterChrome, MyGamesFooterChromeTranslate, 28.0, 0.0, 0.0, 1.0, 210, 145);
	}

	private void AnimateSettingsMenuIn()
	{
		BeginLayerAnimation(LauncherSettingsOverlay, EnsureOverlayTranslateTransform(LauncherSettingsOverlay), 1.0, 0.0, 0.0, 1.0, 120, 0);
		BeginLayerAnimation(SettingsHeaderTitle, SettingsHeaderTitleTranslate, 36.0, 0.0, 0.0, 1.0, 250, 35);
		BeginLayerAnimation(SettingsPanelFrame, SettingsPanelFrameTranslate, 132.0, 0.0, 0.0, 1.0, 360, 20);
		SystemSettingsCategoryPanel.BeginAnimation(UIElement.OpacityProperty, null);
		SystemSettingsCategoryPanel.Opacity = 1.0;
		EnsureOverlayTranslateTransform(SystemSettingsCategoryPanel).X = 0.0;
		BeginLayerAnimation(SettingsFooterChrome, SettingsFooterChromeTranslate, 26.0, 0.0, 0.0, 1.0, 210, 160);
	}

	private void AnimateMusicPlayerIn()
	{
		BeginLayerAnimation(MusicPlayerOverlay, EnsureOverlayTranslateTransform(MusicPlayerOverlay), 1.0, 0.0, 0.0, 1.0, 130, 0);
		BeginLayerAnimation(MusicPlayerHeader, MusicPlayerHeaderTranslate, 34.0, 0.0, 0.0, 1.0, 240, 35);
		BeginLayerAnimation(MusicPlayerContentGrid, MusicPlayerContentTranslate, 112.0, 0.0, 0.0, 1.0, 335, 20);
		BeginLayerAnimation(MusicPlayerLeftPane, MusicPlayerLeftPaneTranslate, 30.0, 0.0, 0.0, 1.0, 250, 105);
		BeginLayerAnimation(MusicPlayerTrackPane, MusicPlayerTrackPaneTranslate, 54.0, 0.0, 0.0, 1.0, 270, 135);
		BeginLayerAnimation(MusicPlayerFooterChrome, MusicPlayerFooterChromeTranslate, 24.0, 0.0, 0.0, 1.0, 205, 170);
	}

	private void AnimateGameDetailsIn()
	{
		BeginLayerAnimation(GameDetailsOverlay, EnsureOverlayTranslateTransform(GameDetailsOverlay), 1.0, 0.0, 0.0, 1.0, 120, 0);
		BeginLayerAnimation(GameDetailsTintLayer, null, 0.0, 0.0, 0.0, 1.0, 210, 0);
		BeginLayerAnimation(GameDetailsBackgroundImage, null, 0.0, 0.0, 0.0, 0.46, 270, 25);
		BeginLayerAnimation(GameDetailsShadeLayer, null, 0.0, 0.0, 0.0, 1.0, 250, 40);
		BeginLayerAnimation(GameDetailsGradientLayer, null, 0.0, 0.0, 0.0, 1.0, 250, 55);
		BeginLayerAnimation(GameDetailsTabsChrome, GameDetailsTabsChromeTranslate, -42.0, 0.0, 0.0, 1.0, 270, 55);
		BeginLayerAnimation(GameDetailsTitleChrome, GameDetailsTitleChromeTranslate, 48.0, 0.0, 0.0, 1.0, 280, 80);
		AnimateVisibleGameDetailsPanel(GameDetailsOverviewPanel, GameDetailsOverviewPanelTranslate, 118.0, 95);
		AnimateVisibleGameDetailsPanel(GameDetailsDetailsPanel, GameDetailsDetailsPanelTranslate, 90.0, 95);
		AnimateVisibleGameDetailsPanel(GameDetailsExtrasPanel, GameDetailsExtrasPanelTranslate, 90.0, 95);
		AnimateVisibleGameDetailsPanel(GameDetailsGalleryPanel, GameDetailsGalleryPanelTranslate, 90.0, 95);
		BeginLayerAnimation(GameDetailsInfoPanel, GameDetailsInfoPanelTranslate, 58.0, 0.0, 0.0, 1.0, 285, 145);
		BeginLayerAnimation(GameDetailsFooterChrome, GameDetailsFooterChromeTranslate, 26.0, 0.0, 0.0, 1.0, 210, 190);
	}

	private void AnimateThemeMenuIn()
	{
		BeginLayerAnimation(ThemeMenuOverlay, EnsureOverlayTranslateTransform(ThemeMenuOverlay), 0.0, 0.0, 0.0, 1.0, 95, 0);
		BeginLayerAnimation(ThemeMenuBackdrop, null, 0.0, 0.0, 0.0, 1.0, 180, 0);
		BeginLayerAnimation(ThemeMenuPanel, ThemeMenuPanelTranslate, 64.0, 0.0, 0.0, 1.0, 285, 15);
		BeginLayerAnimation(ThemeMenuFooterChrome, ThemeMenuFooterChromeTranslate, 24.0, 0.0, 0.0, 1.0, 205, 110);
	}

	private void AnimateProfileMenuIn()
	{
		BeginLayerAnimation(ProfileEditorOverlay, EnsureOverlayTranslateTransform(ProfileEditorOverlay), 0.0, 0.0, 0.0, 1.0, 95, 0);
		BeginLayerAnimation(ProfileHeaderTitle, ProfileHeaderTitleTranslate, 34.0, 0.0, 0.0, 1.0, 240, 35);
		BeginLayerAnimation(ProfileTopGamerPictureButton, ProfileTopGamerPictureTranslate, 0.0, 0.0, 0.0, 1.0, 210, 45);
		BeginLayerAnimation(ProfileClockChrome, ProfileClockChromeTranslate, 34.0, 0.0, 0.0, 1.0, 240, 55);
		BeginLayerAnimation(ProfilePanelFrame, ProfilePanelFrameTranslate, 118.0, 0.0, 0.0, 1.0, 340, 20);
		BeginLayerAnimation(ProfileFooterChrome, ProfileFooterChromeTranslate, 24.0, 0.0, 0.0, 1.0, 205, 145);
	}

	private static void AnimateVisibleGameDetailsPanel(FrameworkElement panel, TranslateTransform translateTransform, double fromX, int delayMs)
	{
		if (panel.Visibility != Visibility.Visible)
		{
			panel.BeginAnimation(UIElement.OpacityProperty, null);
			panel.Opacity = 1.0;
			translateTransform.X = 0.0;
			return;
		}
		BeginLayerAnimation(panel, translateTransform, fromX, 0.0, 0.0, 1.0, 340, delayMs);
	}

	private static void BeginLayerAnimation(FrameworkElement element, TranslateTransform? translateTransform, double fromX, double toX, double fromOpacity, double toOpacity, int durationMs, int delayMs)
	{
		element.BeginAnimation(UIElement.OpacityProperty, null);
		element.Opacity = fromOpacity;
		TimeSpan duration = TimeSpan.FromMilliseconds(durationMs);
		DoubleAnimation opacityAnimation = new DoubleAnimation(fromOpacity, toOpacity, duration)
		{
			BeginTime = TimeSpan.FromMilliseconds(delayMs),
			EasingFunction = new SineEase
			{
				EasingMode = EasingMode.EaseOut
			}
		};
		element.BeginAnimation(UIElement.OpacityProperty, opacityAnimation);
		if (translateTransform == null)
		{
			return;
		}
		translateTransform.BeginAnimation(TranslateTransform.XProperty, null);
		translateTransform.BeginAnimation(TranslateTransform.YProperty, null);
		translateTransform.X = fromX;
		translateTransform.Y = 0.0;
		translateTransform.BeginAnimation(TranslateTransform.XProperty, new DoubleAnimation(fromX, toX, duration)
		{
			BeginTime = TimeSpan.FromMilliseconds(delayMs),
			EasingFunction = new QuarticEase
			{
				EasingMode = EasingMode.EaseOut
			}
		});
	}

	private bool TryBeginMenuCloseTransition()
	{
		FrameworkElement overlay;
		if (_viewModel.IsMyGamesOpen)
		{
			overlay = MyGamesOverlay;
		}
		else if (_viewModel.IsLauncherSettingsOpen && !_viewModel.IsDashboardCustomizerOpen && !_viewModel.IsThemeCreatorOpen && !_viewModel.IsSteamSetupOpen)
		{
			overlay = LauncherSettingsOverlay;
		}
		else
		{
			return false;
		}
		_isMenuTransitionActive = true;
		if (overlay == MyGamesOverlay)
		{
			AnimateLibraryMenuOut(FinishMenuCloseTransition);
		}
		else
		{
			AnimateSettingsMenuOut(FinishMenuCloseTransition);
		}
		return true;

		void FinishMenuCloseTransition()
		{
			try
			{
				_viewModel.BackCommand.Execute(null);
			}
			finally
			{
				_isMenuTransitionActive = false;
				overlay.Opacity = 1.0;
				EnsureOverlayTranslateTransform(overlay).X = 0.0;
			}
		}
	}

	private void BackHint_OnMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
	{
		e.Handled = true;
		if (_viewModel.IsLauncherSettingsOpen && _activeSystemSettingsPanel != null && !_viewModel.IsDashboardCustomizerOpen && !_viewModel.IsThemeCreatorOpen && !_viewModel.IsSteamSetupOpen && !_viewModel.IsSpotifySetupOpen)
		{
			_audioService.Play("menu-out");
			ShowSystemSettingsCategories();
			return;
		}
		if (TryBeginMenuCloseTransition())
		{
			return;
		}
		if (_viewModel.BackCommand.CanExecute(null))
		{
			_viewModel.BackCommand.Execute(null);
		}
	}

	private void AnimateLibraryMenuOut(Action completed)
	{
		BeginLayerAnimation(MyGamesFilterChrome, MyGamesFilterChromeTranslate, 0.0, -34.0, 1.0, 0.0, 145, 0);
		BeginLayerAnimation(MyGamesHeaderChrome, MyGamesHeaderChromeTranslate, 0.0, 48.0, 1.0, 0.0, 160, 0);
		BeginLayerAnimation(MyGamesStrip, MyGamesStripTranslate, 0.0, 150.0, 1.0, 0.0, 210, 0);
		BeginLayerAnimation(MyGamesFooterChrome, MyGamesFooterChromeTranslate, 0.0, 24.0, 1.0, 0.0, 130, 0);
		BeginLayerAnimation(MyGamesBackdrop, null, 0.0, 0.0, 0.26, 0.0, 190, 0);
		AnimateOverlayOut(MyGamesOverlay, completed);
	}

	private void AnimateSettingsMenuOut(Action completed)
	{
		BeginLayerAnimation(SettingsHeaderTitle, SettingsHeaderTitleTranslate, 0.0, 42.0, 1.0, 0.0, 145, 0);
		BeginLayerAnimation(SettingsPanelFrame, SettingsPanelFrameTranslate, 0.0, 136.0, 1.0, 0.0, 215, 0);
		BeginLayerAnimation(SettingsFooterChrome, SettingsFooterChromeTranslate, 0.0, 24.0, 1.0, 0.0, 130, 0);
		AnimateOverlayOut(LauncherSettingsOverlay, completed);
	}

	private static void AnimateOverlayOut(FrameworkElement overlay, Action completed)
	{
		overlay.BeginAnimation(UIElement.OpacityProperty, null);
		TranslateTransform translateTransform = EnsureOverlayTranslateTransform(overlay);
		translateTransform.BeginAnimation(TranslateTransform.XProperty, null);
		translateTransform.BeginAnimation(TranslateTransform.YProperty, null);
		translateTransform.X = 0.0;
		translateTransform.Y = 0.0;
		DoubleAnimation doubleAnimation = new DoubleAnimation(1.0, 0.0, TimeSpan.FromMilliseconds(150.0))
		{
			EasingFunction = new SineEase
			{
				EasingMode = EasingMode.EaseIn
			}
		};
		doubleAnimation.Completed += delegate
		{
			completed();
		};
		overlay.BeginAnimation(UIElement.OpacityProperty, doubleAnimation);
		translateTransform.BeginAnimation(TranslateTransform.XProperty, new DoubleAnimation(112.0, TimeSpan.FromMilliseconds(210.0))
		{
			EasingFunction = new CubicEase
			{
				EasingMode = EasingMode.EaseIn
			}
		});
	}

	private static TranslateTransform EnsureOverlayTranslateTransform(FrameworkElement overlay)
	{
		if (overlay.RenderTransform is TransformGroup group)
		{
			TranslateTransform translate = group.Children.OfType<TranslateTransform>().FirstOrDefault();
			if (translate != null)
			{
				return translate;
			}
		}
		if (overlay.RenderTransform is TranslateTransform existingTranslate)
		{
			return existingTranslate;
		}
		TranslateTransform translateTransform = new TranslateTransform();
		overlay.RenderTransform = translateTransform;
		return translateTransform;
	}

	private void UpdateAdjacentPreviewSnapshots()
	{
		PreviousPreviewImage.Source = ((_viewModel.PreviousTab == null) ? null : CreatePreviewSnapshot(_viewModel.PreviousTab, 0.0 - _viewModel.LeftPreviewContentLeft));
		NextPreviewImage.Source = ((_viewModel.NextTab == null) ? null : CreatePreviewSnapshot(_viewModel.NextTab, 0.0 - _viewModel.RightPreviewContentLeft));
	}

	private void PrepareLiveAdjacentPreviews()
	{
		PreviousPreviewLiveHost.Content = null;
		NextPreviewLiveHost.Content = null;
		NextPreviewLiveHost.ClearValue(Canvas.LeftProperty);
		PreviousPreviewLiveLayer.Visibility = Visibility.Collapsed;
		NextPreviewLiveLayer.Visibility = Visibility.Collapsed;
		bool hasPrevious = _viewModel.PreviousTab != null && PreviousPreviewImage.Source != null;
		bool hasNext = _viewModel.NextTab != null && NextPreviewImage.Source != null;
		PreviousPreviewImage.Visibility = hasPrevious ? Visibility.Visible : Visibility.Collapsed;
		NextPreviewImage.Visibility = hasNext ? Visibility.Visible : Visibility.Collapsed;
	}

	private static bool IsHeavyPreviewTab(object? tab)
	{
		return tab?.GetType().Name == "AppsTabViewModel";
	}

	private BitmapSource? CreatePreviewSnapshot(object? tab, double cropX)
	{
		//IL_0043: Unknown result type (might be due to invalid IL or missing references)
		//IL_0072: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d7: Unknown result type (might be due to invalid IL or missing references)
		//IL_010d: Unknown result type (might be due to invalid IL or missing references)
		//IL_014c: Unknown result type (might be due to invalid IL or missing references)
		if (tab == null)
		{
			return null;
		}
		try
		{
			ContentPresenter contentPresenter = new ContentPresenter();
			contentPresenter.Content = tab;
			contentPresenter.Width = 1280.0;
			contentPresenter.Height = 502.0;
			contentPresenter.Measure(new Size(1280.0, 502.0));
			contentPresenter.Arrange(new Rect(0.0, 0.0, 1280.0, 502.0));
			contentPresenter.UpdateLayout();
			VisualBrush brush = new VisualBrush(contentPresenter)
			{
				Stretch = Stretch.Fill,
				AlignmentX = AlignmentX.Left,
				AlignmentY = AlignmentY.Top,
				ViewboxUnits = BrushMappingMode.Absolute,
				Viewbox = new Rect(Math.Clamp(cropX, 0.0, 1176.0), 0.0, 104.0, 502.0),
				ViewportUnits = BrushMappingMode.Absolute,
				Viewport = new Rect(0.0, 0.0, 104.0, 502.0)
			};
			DrawingVisual drawingVisual = new DrawingVisual();
			using (DrawingContext drawingContext = drawingVisual.RenderOpen())
			{
				drawingContext.DrawRectangle(brush, null, new Rect(0.0, 0.0, 104.0, 502.0));
			}
			RenderTargetBitmap renderTargetBitmap = new RenderTargetBitmap(104, 502, 96.0, 96.0, PixelFormats.Pbgra32);
			renderTargetBitmap.Render(drawingVisual);
			((Freezable)renderTargetBitmap).Freeze();
			return renderTargetBitmap;
		}
		catch (Exception exception)
		{
			App.LogException(exception, "MainWindow.CreatePreviewSnapshot");
			return null;
		}
	}

	private void QueueFocusFirstButton()
	{
		if (_isFocusUpdateQueued)
		{
			return;
		}
		_isFocusUpdateQueued = true;
		((DispatcherObject)this).Dispatcher.BeginInvoke((Delegate)(Action)delegate
		{
			try
			{
				_isFocusUpdateQueued = false;
				FocusFirstButton();
			}
			catch (Exception exception)
			{
				_isFocusUpdateQueued = false;
				App.LogException(exception, "MainWindow.QueueFocusFirstButton");
			}
		}, (DispatcherPriority)4, Array.Empty<object>());
	}

	private void QueueGameDetailsTileFocus()
	{
		((DispatcherObject)this).Dispatcher.BeginInvoke((Delegate)(Action)delegate
		{
			if (_viewModel.IsDetailsOpen)
			{
				TryFocus(GetFirstVisibleDetailsButton() ?? FindFocusableControl((DependencyObject?)(object)GameDetailsOverviewPanel) ?? FindFocusableControl((DependencyObject?)(object)GameDetailsOverlay));
			}
		}, (DispatcherPriority)6, Array.Empty<object>());
	}

	private void FocusFirstButton()
	{
		if (_viewModel.IsDetailsOpen)
		{
			TryFocus(GetFirstVisibleDetailsButton() ?? FindFocusableControl((DependencyObject?)(object)GameDetailsOverlay));
		}
		else if (_viewModel.IsMyGamesOpen)
		{
			FocusLibraryGameButton(_viewModel.SelectedGame);
		}
		else if (_viewModel.IsLauncherSettingsOpen)
		{
			if (_viewModel.IsDashboardCustomizerOpen)
			{
				TryFocus(DashboardCustomizerPreviousTabButton ?? FindFocusableControl((DependencyObject?)(object)DashboardCustomizerOverlay));
			}
			else if (_viewModel.IsThemeCreatorOpen)
			{
				TryFocus(ChooseThemeHomeBackgroundButton ?? FindFocusableControl((DependencyObject?)(object)ThemeCreatorOverlay));
			}
			else
			{
				TryFocus(FindFocusableControl((DependencyObject?)(object)LauncherSettingsOverlay));
			}
		}
		else if (_viewModel.IsThemeMenuOpen)
		{
			TryFocus(FindThemeMenuFocusButton() ?? FindFocusableControl((DependencyObject?)(object)ThemeMenuOverlay));
		}
		else if (_viewModel.IsProfileEditorOpen)
		{
			TryFocus(ProfileMenuViewGamesButton ?? FindFocusableControl((DependencyObject?)(object)ProfileEditorOverlay));
		}
		else if (_viewModel.IsMusicPlayerOpen)
		{
			TryFocus(FindFocusableControl((DependencyObject?)(object)MusicPlayerOverlay));
		}
		else if (_viewModel.IsBladesDashboardStyle)
		{
			TryFocusBladesMenuButton();
		}
		else
		{
			List<FocusCandidate> dashboardFocusCandidates = GetDashboardFocusCandidates();
			if (dashboardFocusCandidates.Count > 0)
			{
				FocusDefaultButton(dashboardFocusCandidates);
			}
			else
			{
				TryFocus(FindVisualChild<System.Windows.Controls.Button>((DependencyObject?)(object)ContentHost));
			}
		}
	}

	private void GameDetailsTileButton_OnFocusOrMouseEnter(object sender, RoutedEventArgs e)
	{
		if (sender is System.Windows.Controls.Button button)
		{
			System.Windows.Controls.Panel.SetZIndex(button, 12);
			AnimateGameDetailsTileLift(button, 1.13, -7.0, -8.0, 145);
		}
	}

	private void GameDetailsTileButton_OnFocusOrMouseLeave(object sender, RoutedEventArgs e)
	{
		if (sender is System.Windows.Controls.Button button && (e is System.Windows.Input.MouseEventArgs || (!button.IsKeyboardFocusWithin && !button.IsMouseOver)))
		{
			System.Windows.Controls.Panel.SetZIndex(button, 0);
			AnimateGameDetailsTileLift(button, 1.0, 0.0, 0.0, 120);
		}
	}

	private static void AnimateGameDetailsTileLift(System.Windows.Controls.Button button, double scale, double x, double y, int milliseconds)
	{
		//IL_0073: Unknown result type (might be due to invalid IL or missing references)
		TransformGroup transformGroup = button.RenderTransform as TransformGroup;
		if (transformGroup == null)
		{
			transformGroup = new TransformGroup();
			transformGroup.Children.Add(new ScaleTransform(1.0, 1.0));
			transformGroup.Children.Add(new TranslateTransform(0.0, 0.0));
			button.RenderTransform = transformGroup;
			button.RenderTransformOrigin = new Point(0.5, 0.5);
		}
		ScaleTransform scaleTransform = transformGroup.Children.OfType<ScaleTransform>().FirstOrDefault();
		TranslateTransform translateTransform = transformGroup.Children.OfType<TranslateTransform>().FirstOrDefault();
		if (scaleTransform != null && translateTransform != null)
		{
			CubicEase easingFunction = new CubicEase
			{
				EasingMode = EasingMode.EaseOut
			};
			scaleTransform.BeginAnimation(ScaleTransform.ScaleXProperty, new DoubleAnimation(scale, TimeSpan.FromMilliseconds(milliseconds))
			{
				EasingFunction = easingFunction
			});
			scaleTransform.BeginAnimation(ScaleTransform.ScaleYProperty, new DoubleAnimation(scale, TimeSpan.FromMilliseconds(milliseconds))
			{
				EasingFunction = easingFunction
			});
			translateTransform.BeginAnimation(TranslateTransform.XProperty, new DoubleAnimation(x, TimeSpan.FromMilliseconds(milliseconds))
			{
				EasingFunction = easingFunction
			});
			translateTransform.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(y, TimeSpan.FromMilliseconds(milliseconds))
			{
				EasingFunction = easingFunction
			});
		}
	}

	private bool TryMoveDashboardFocus(DashboardInputAction action)
	{
		//IL_0193: Unknown result type (might be due to invalid IL or missing references)
		//IL_0198: Unknown result type (might be due to invalid IL or missing references)
		//IL_019d: Unknown result type (might be due to invalid IL or missing references)
		//IL_01d5: Unknown result type (might be due to invalid IL or missing references)
		//IL_01da: Unknown result type (might be due to invalid IL or missing references)
		//IL_01f0: Unknown result type (might be due to invalid IL or missing references)
		//IL_01f5: Unknown result type (might be due to invalid IL or missing references)
		//IL_020b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0210: Unknown result type (might be due to invalid IL or missing references)
		//IL_0226: Unknown result type (might be due to invalid IL or missing references)
		//IL_022b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0249: Unknown result type (might be due to invalid IL or missing references)
		//IL_024b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0241: Unknown result type (might be due to invalid IL or missing references)
		//IL_0246: Unknown result type (might be due to invalid IL or missing references)
		if (Keyboard.FocusedElement is System.Windows.Controls.TextBox)
		{
			return false;
		}
		if (_viewModel.IsLauncherSettingsOpen && DashboardInputRouter.TryAdjustFocusedSetting(action))
		{
			return true;
		}
		if (_viewModel.IsMusicPlayerOpen)
		{
			return TryMoveOverlayFocus(MusicPlayerOverlay, action);
		}
		if (_viewModel.IsDetailsOpen)
		{
			return TryMoveOverlayFocus(GameDetailsOverlay, action);
		}
		if (_viewModel.IsMyGamesOpen)
		{
			return TryMoveMyGamesFocus(action);
		}
		if (_viewModel.IsLauncherSettingsOpen)
		{
			if (_viewModel.IsDashboardCustomizerOpen)
			{
				return TryMoveOverlayFocus(DashboardCustomizerOverlay, action);
			}
			if (_viewModel.IsThemeCreatorOpen)
			{
				return TryMoveOverlayFocus(ThemeCreatorOverlay, action);
			}
			return TryMoveOverlayFocus(LauncherSettingsOverlay, action);
		}
		if (_viewModel.IsProfileEditorOpen)
		{
			return TryMoveProfileMenuFocus(action);
		}
		if (_viewModel.IsThemeMenuOpen)
		{
			return TryMoveThemeMenuFocus(action);
		}
		List<FocusCandidate> dashboardFocusCandidates = GetDashboardFocusCandidates();
		if (dashboardFocusCandidates.Count == 0)
		{
			return SafeMoveFocus(action);
		}
		IInputElement focusedElement = Keyboard.FocusedElement;
		System.Windows.Controls.Button currentButton = focusedElement as System.Windows.Controls.Button;
		if (currentButton == null || !dashboardFocusCandidates.Any((FocusCandidate candidate) => candidate.Button == currentButton))
		{
			return FocusDefaultButton(dashboardFocusCandidates);
		}
		Point currentCenter = GetCenter(dashboardFocusCandidates.First((FocusCandidate candidate) => candidate.Button == currentButton).Bounds);
		Vector direction = (Vector)(action switch
		{
			DashboardInputAction.MoveLeft => new Vector(-1.0, 0.0), 
			DashboardInputAction.MoveRight => new Vector(1.0, 0.0), 
			DashboardInputAction.MoveUp => new Vector(0.0, -1.0), 
			DashboardInputAction.MoveDown => new Vector(0.0, 1.0), 
			_ => new Vector(0.0, 0.0), 
		});
		var anon = (from item in (from candidate in dashboardFocusCandidates
				where candidate.Button != currentButton
				select new
				{
					Candidate = candidate,
					Center = GetCenter(candidate.Bounds)
				}).Select(item =>
			{
				//IL_0008: Unknown result type (might be due to invalid IL or missing references)
				//IL_000e: Unknown result type (might be due to invalid IL or missing references)
				//IL_0013: Unknown result type (might be due to invalid IL or missing references)
				//IL_0018: Unknown result type (might be due to invalid IL or missing references)
				//IL_0052: Unknown result type (might be due to invalid IL or missing references)
				//IL_0057: Unknown result type (might be due to invalid IL or missing references)
				//IL_0030: Unknown result type (might be due to invalid IL or missing references)
				//IL_0035: Unknown result type (might be due to invalid IL or missing references)
				//IL_0088: Unknown result type (might be due to invalid IL or missing references)
				//IL_00b1: Unknown result type (might be due to invalid IL or missing references)
				//IL_00b6: Unknown result type (might be due to invalid IL or missing references)
				//IL_008f: Unknown result type (might be due to invalid IL or missing references)
				//IL_0094: Unknown result type (might be due to invalid IL or missing references)
				FocusCandidate candidate = item.Candidate;
				Vector delta = item.Center - currentCenter;
				DashboardInputAction dashboardInputAction = action;
				Point center;
				double num;
				if ((uint)dashboardInputAction > 1u)
				{
					center = item.Center;
					num = Math.Abs(center.Y - currentCenter.Y);
				}
				else
				{
					center = item.Center;
					num = Math.Abs(center.X - currentCenter.X);
				}
				double primary = num;
				dashboardInputAction = action;
				double secondary;
				if (!((uint)dashboardInputAction <= 1u))
				{
					center = item.Center;
					secondary = Math.Abs(center.X - currentCenter.X);
				}
				else
				{
					center = item.Center;
					secondary = Math.Abs(center.Y - currentCenter.Y);
				}
				return new
				{
					Candidate = candidate,
					Delta = delta,
					Primary = primary,
					Secondary = secondary
				};
			})
			where Vector.Multiply(item.Delta, direction) > 1.0
			orderby item.Secondary * 2.4 + item.Primary, item.Primary
			select item).FirstOrDefault();
		if (anon == null)
		{
			RememberFocusedButton();
			return false;
		}
		if (!TryFocus(anon.Candidate.Button))
		{
			return false;
		}
		RememberFocusedButton();
		return true;
	}

	private List<FocusCandidate> GetDashboardFocusCandidates()
	{
		try
		{
			return (from button in FindVisualChildren<System.Windows.Controls.Button>((DependencyObject?)(object)ContentHost)
				where button.IsVisible && button.IsEnabled && button.Focusable
				select new FocusCandidate(button, GetElementBounds(button, ContentHost))).Where(delegate(FocusCandidate candidate)
			{
				//IL_0002: Unknown result type (might be due to invalid IL or missing references)
				//IL_0007: Unknown result type (might be due to invalid IL or missing references)
				//IL_001c: Unknown result type (might be due to invalid IL or missing references)
				//IL_0021: Unknown result type (might be due to invalid IL or missing references)
				Rect bounds = candidate.Bounds;
				if (bounds.Width > 0.0)
				{
					bounds = candidate.Bounds;
					return bounds.Height > 0.0;
				}
				return false;
			}).Where(delegate(FocusCandidate candidate)
			{
				//IL_0002: Unknown result type (might be due to invalid IL or missing references)
				//IL_0007: Unknown result type (might be due to invalid IL or missing references)
				//IL_000c: Unknown result type (might be due to invalid IL or missing references)
				Point center = GetCenter(candidate.Bounds);
				return center.X >= 64.0 && center.X <= 1120.0;
			}).ToList();
		}
		catch
		{
			return new List<FocusCandidate>();
		}
	}

	private void RememberFocusedButton()
	{
		if (_viewModel.CurrentTab != null && Keyboard.FocusedElement is System.Windows.Controls.Button value)
		{
			if (IsVideoTabCurrent())
			{
				FocusCandidate focusCandidate = GetDashboardFocusCandidates().FirstOrDefault((FocusCandidate candidate) => candidate.Button == value);
				if (focusCandidate.Button == null || !IsLeftRailDashboardCandidate(focusCandidate))
				{
					_lastFocusedButtonByTab.Remove(_viewModel.CurrentTab.Key);
					return;
				}
			}
			_lastFocusedButtonByTab[_viewModel.CurrentTab.Key] = value;
		}
	}

	private void QueueBladesMenuFocus()
	{
		((DispatcherObject)this).Dispatcher.BeginInvoke((Delegate)(Action)delegate
		{
			TryFocusBladesMenuButton();
		}, (DispatcherPriority)6, Array.Empty<object>());
	}

	private void QueueBladesAchievementsFocus()
	{
		((DispatcherObject)this).Dispatcher.BeginInvoke((Delegate)(Action)delegate
		{
			TryFocusBladesAchievementsPane();
		}, (DispatcherPriority)6, Array.Empty<object>());
	}

	private bool TryFocusBladesAchievementsPane()
	{
		try
		{
			if (!_viewModel.IsBladesAchievementsSubmenu)
			{
				return false;
			}
			if (_viewModel.IsBladesAchievementListFocused)
			{
				if (_viewModel.SelectedBladesAchievementIndex >= 0 && _viewModel.SelectedBladesAchievementIndex < BladesAchievementListBox.Items.Count)
				{
					BladesAchievementListBox.ScrollIntoView(BladesAchievementListBox.Items[_viewModel.SelectedBladesAchievementIndex]);
				}
				BladesAchievementListBox.UpdateLayout();
				return TryFocus(BladesAchievementListBox);
			}
			if (_viewModel.SelectedBladesAchievementGameIndex >= 0 && _viewModel.SelectedBladesAchievementGameIndex < BladesAchievementGameListBox.Items.Count)
			{
				BladesAchievementGameListBox.ScrollIntoView(BladesAchievementGameListBox.Items[_viewModel.SelectedBladesAchievementGameIndex]);
			}
			BladesAchievementGameListBox.UpdateLayout();
			return TryFocus(BladesAchievementGameListBox);
		}
		catch (Exception exception)
		{
			App.LogException(exception, "MainWindow.TryFocusBladesAchievementsPane");
			return false;
		}
	}

	private void BladesTrayClick(object sender, RoutedEventArgs e) => _viewModel.ActivateBladesTray();

	private bool TryFocusBladesMenuButton()
	{
		try
		{
			if (_viewModel.IsBladesTraySelected && !_viewModel.IsBladesSubmenuOpen) return TryFocus(BladesTrayButton);
			List<System.Windows.Controls.Button> buttons = FindVisualChildren<System.Windows.Controls.Button>((DependencyObject?)(object)BladesDashboardOverlay)
				.Where((System.Windows.Controls.Button button) => button.IsVisible && string.Equals(button.Tag as string, "BladesMenuOption", StringComparison.OrdinalIgnoreCase))
				.ToList();
			if (buttons.Count == 0)
			{
				return false;
			}
			int index = Math.Clamp(_viewModel.ActiveBladesMenuIndex, 0, buttons.Count - 1);
			var selected=buttons.FirstOrDefault(button => button.DataContext is BladesMenuItemViewModel item && item.IsSelected) ?? buttons[index];
			selected.BringIntoView();
			return TryFocus(selected);
		}
		catch (Exception exception)
		{
			App.LogException(exception, "MainWindow.TryFocusBladesMenuButton");
			return false;
		}
	}

	private bool IsVideoTabCurrent()
	{
		DashboardTabViewModel? currentTab = _viewModel.CurrentTab;
		return string.Equals(currentTab?.Key, "media", StringComparison.OrdinalIgnoreCase)
			|| string.Equals(currentTab?.Key, "video", StringComparison.OrdinalIgnoreCase)
			|| string.Equals(currentTab?.Name, "video", StringComparison.OrdinalIgnoreCase);
	}

	private static bool IsLeftRailDashboardCandidate(FocusCandidate candidate)
	{
		Point center = GetCenter(candidate.Bounds);
		return center.X < 360.0;
	}

	private bool IsOverlayOpen()
	{
		if (!_viewModel.IsMyGamesOpen && !_viewModel.IsLauncherSettingsOpen && !_viewModel.IsProfileEditorOpen && !_viewModel.IsThemeMenuOpen && !_viewModel.IsThemeCreatorOpen && !_viewModel.IsDashboardCustomizerOpen && !_viewModel.IsSteamSetupOpen && !_viewModel.IsMusicPlayerOpen && !_viewModel.IsYouTubeTvOpen && !_viewModel.IsSearchOverlayOpen && !_viewModel.IsDetailsOpen)
		{
			return _viewModel.IsQuickMenuOpen;
		}
		return true;
	}

	private bool TryRestoreOverlayFocus()
	{
		DependencyObject activeOverlay = GetActiveOverlay();
		if (activeOverlay == null || IsFocusInside(activeOverlay))
		{
			return false;
		}
		FocusFirstButton();
		return true;
	}

	private void ScrollMusicBrowserResultIntoView()
	{
		int selectedIndex = _viewModel.MusicBrowserResultItems.IndexOf(_viewModel.SelectedMusicBrowserResultItem);
		ScrollItemIntoView(MusicBrowserResultsScrollViewer, selectedIndex, 48.0);
	}

	private void ScrollCurrentMusicTrackIntoView()
	{
		int selectedIndex = _viewModel.MusicTracks.IndexOf(_viewModel.SelectedMusicTrack ?? _viewModel.CurrentMusicTrack);
		ScrollItemIntoView(MusicPlayerTracksScrollViewer, selectedIndex, 48.0);
	}

	private static void ScrollItemIntoView(ScrollViewer scrollViewer, int selectedIndex, double itemHeight)
	{
		if (selectedIndex < 0 || itemHeight <= 0.0)
		{
			return;
		}
		double itemTop = selectedIndex * itemHeight;
		double itemBottom = itemTop + itemHeight;
		double viewportTop = scrollViewer.VerticalOffset;
		double viewportBottom = viewportTop + scrollViewer.ViewportHeight;
		if (itemTop < viewportTop)
		{
			scrollViewer.ScrollToVerticalOffset(itemTop);
		}
		else if (itemBottom > viewportBottom)
		{
			scrollViewer.ScrollToVerticalOffset(itemBottom - scrollViewer.ViewportHeight);
		}
	}

	private DependencyObject? GetActiveOverlay()
	{
		if (_viewModel.IsDetailsOpen)
		{
			return (DependencyObject?)(object)GameDetailsOverlay;
		}
		if (_viewModel.IsMyGamesOpen)
		{
			return (DependencyObject?)(object)MyGamesOverlay;
		}
		if (_viewModel.IsLauncherSettingsOpen)
		{
			if (_viewModel.IsDashboardCustomizerOpen)
			{
				return (DependencyObject?)(object)DashboardCustomizerOverlay;
			}
			if (_viewModel.IsThemeCreatorOpen)
			{
				return (DependencyObject?)(object)ThemeCreatorOverlay;
			}
			if (_viewModel.IsSteamSetupOpen)
			{
				return (DependencyObject?)(object)SteamSetupOverlay;
			}
			return (DependencyObject?)(object)LauncherSettingsOverlay;
		}
		if (_viewModel.IsProfileEditorOpen)
		{
			return (DependencyObject?)(object)ProfileEditorOverlay;
		}
		if (_viewModel.IsThemeMenuOpen)
		{
			return (DependencyObject?)(object)ThemeMenuOverlay;
		}
		if (_viewModel.IsMusicPlayerOpen)
		{
			return (DependencyObject?)(object)MusicPlayerOverlay;
		}
		if (_viewModel.IsYouTubeTvOpen)
		{
			return (DependencyObject?)(object)YouTubeTvOverlay;
		}
		return null;
	}

	private System.Windows.Controls.Button? GetFirstVisibleDetailsButton()
	{
		if (IsElementVisible(SteamDetailsLaunchButton))
		{
			return SteamDetailsLaunchButton;
		}
		if (IsElementVisible(ManualDetailsLaunchButton))
		{
			return ManualDetailsLaunchButton;
		}
		return null;
	}

	private static bool IsElementVisible(FrameworkElement? element)
	{
		if (element != null && element.IsVisible)
		{
			return element.Visibility == Visibility.Visible;
		}
		return false;
	}

	private static bool IsFocusInside(DependencyObject overlay)
	{
		IInputElement focusedElement = Keyboard.FocusedElement;
		DependencyObject val = (DependencyObject)((focusedElement is DependencyObject) ? focusedElement : null);
		if (val == null)
		{
			return false;
		}
		for (DependencyObject val2 = val; val2 != null; val2 = VisualTreeHelper.GetParent(val2))
		{
			if (val2 == overlay)
			{
				return true;
			}
		}
		return false;
	}

	private static bool SafeMoveFocus(DashboardInputAction action)
	{
		try
		{
			return DashboardInputRouter.MoveFocus(action);
		}
		catch
		{
			return false;
		}
	}

	private bool FocusDefaultButton(IReadOnlyCollection<FocusCandidate> buttons)
	{
		if (_viewModel.CurrentTab != null && _lastFocusedButtonByTab.TryGetValue(_viewModel.CurrentTab.Key, out System.Windows.Controls.Button remembered) && remembered.IsVisible && remembered.IsEnabled)
		{
			FocusCandidate rememberedCandidate = buttons.FirstOrDefault((FocusCandidate candidate) => candidate.Button == remembered);
			if (rememberedCandidate.Button != null && ShouldUseRememberedDashboardFocus(rememberedCandidate))
			{
				return TryFocus(remembered);
			}
		}
		IEnumerable<FocusCandidate> focusOrder;
	if (IsVideoTabCurrent())
	{
		focusOrder = buttons.OrderBy(delegate(FocusCandidate candidate)
		{
			return IsLeftRailDashboardCandidate(candidate) ? 0 : 1;
		}).ThenBy(delegate(FocusCandidate candidate)
		{
			Point center = GetCenter(candidate.Bounds);
			return Math.Abs(center.Y - 210.0);
			}).ThenBy(delegate(FocusCandidate candidate)
			{
				Point center = GetCenter(candidate.Bounds);
				return Math.Abs(center.X - 120.0);
			});
		}
		else
		{
			focusOrder = buttons.OrderBy(delegate(FocusCandidate candidate)
			{
				Point center = GetCenter(candidate.Bounds);
				return Math.Abs(center.Y - 210.0);
			}).ThenBy(delegate(FocusCandidate candidate)
			{
				Point center = GetCenter(candidate.Bounds);
				return Math.Abs(center.X - 560.0);
			});
		}
		return TryFocus(focusOrder.FirstOrDefault().Button);
	}

	private bool ShouldUseRememberedDashboardFocus(FocusCandidate candidate)
	{
		if (!IsVideoTabCurrent())
		{
			return true;
		}
		return IsLeftRailDashboardCandidate(candidate);
	}

		private bool TryMoveOverlayFocus(FrameworkElement overlay, DashboardInputAction action)
		{
			//IL_007d: Unknown result type (might be due to invalid IL or missing references)
			//IL_0082: Unknown result type (might be due to invalid IL or missing references)
			//IL_0087: Unknown result type (might be due to invalid IL or missing references)
			//IL_00bf: Unknown result type (might be due to invalid IL or missing references)
			//IL_00c4: Unknown result type (might be due to invalid IL or missing references)
			//IL_00da: Unknown result type (might be due to invalid IL or missing references)
			//IL_00df: Unknown result type (might be due to invalid IL or missing references)
			//IL_00f5: Unknown result type (might be due to invalid IL or missing references)
			//IL_00fa: Unknown result type (might be due to invalid IL or missing references)
			//IL_0110: Unknown result type (might be due to invalid IL or missing references)
			//IL_0115: Unknown result type (might be due to invalid IL or missing references)
			//IL_0133: Unknown result type (might be due to invalid IL or missing references)
			//IL_0135: Unknown result type (might be due to invalid IL or missing references)
			//IL_012b: Unknown result type (might be due to invalid IL or missing references)
			//IL_0130: Unknown result type (might be due to invalid IL or missing references)
			List<OverlayFocusCandidate> overlayFocusCandidates = GetOverlayFocusCandidates(overlay);
			if (overlayFocusCandidates.Count == 0)
			{
				return SafeMoveFocus(action);
			}
			System.Windows.Controls.Control currentControl = GetFocusedOverlayControl(overlay);
			if (currentControl == null || !overlayFocusCandidates.Any((OverlayFocusCandidate candidate) => candidate.Control == currentControl))
			{
				return TryFocus(overlayFocusCandidates[0].Control);
			}
			Point currentCenter = GetCenter(overlayFocusCandidates.First((OverlayFocusCandidate candidate) => candidate.Control == currentControl).Bounds);
			Vector direction = (Vector)(action switch
			{
				DashboardInputAction.MoveLeft => new Vector(-1.0, 0.0), 
				DashboardInputAction.MoveRight => new Vector(1.0, 0.0), 
				DashboardInputAction.MoveUp => new Vector(0.0, -1.0), 
				DashboardInputAction.MoveDown => new Vector(0.0, 1.0), 
				_ => new Vector(0.0, 0.0), 
			});
			var anon = (from item in (from candidate in overlayFocusCandidates
					where candidate.Control != currentControl
					select new
					{
						Candidate = candidate,
						Center = GetCenter(candidate.Bounds)
					}).Select(item =>
				{
					//IL_0008: Unknown result type (might be due to invalid IL or missing references)
					//IL_000e: Unknown result type (might be due to invalid IL or missing references)
					//IL_0013: Unknown result type (might be due to invalid IL or missing references)
					//IL_0018: Unknown result type (might be due to invalid IL or missing references)
					//IL_0052: Unknown result type (might be due to invalid IL or missing references)
					//IL_0057: Unknown result type (might be due to invalid IL or missing references)
					//IL_0030: Unknown result type (might be due to invalid IL or missing references)
					//IL_0035: Unknown result type (might be due to invalid IL or missing references)
					//IL_0088: Unknown result type (might be due to invalid IL or missing references)
					//IL_00b1: Unknown result type (might be due to invalid IL or missing references)
					//IL_00b6: Unknown result type (might be due to invalid IL or missing references)
					//IL_008f: Unknown result type (might be due to invalid IL or missing references)
					//IL_0094: Unknown result type (might be due to invalid IL or missing references)
					OverlayFocusCandidate candidate = item.Candidate;
					Vector delta = item.Center - currentCenter;
					DashboardInputAction dashboardInputAction = action;
					Point center;
					double num;
					if ((uint)dashboardInputAction > 1u)
					{
						center = item.Center;
						num = Math.Abs(center.Y - currentCenter.Y);
					}
					else
					{
						center = item.Center;
						num = Math.Abs(center.X - currentCenter.X);
					}
					double primary = num;
					dashboardInputAction = action;
					double secondary;
					if (!((uint)dashboardInputAction <= 1u))
					{
						center = item.Center;
						secondary = Math.Abs(center.X - currentCenter.X);
					}
					else
					{
						center = item.Center;
						secondary = Math.Abs(center.Y - currentCenter.Y);
					}
					return new
					{
						Candidate = candidate,
						Delta = delta,
						Primary = primary,
						Secondary = secondary
					};
				})
				where Vector.Multiply(item.Delta, direction) > 1.0
				orderby item.Secondary * 2.2 + item.Primary, item.Primary
				select item).FirstOrDefault();
			if (anon == null)
			{
				return SafeMoveFocus(action);
			}
			return TryFocus(anon.Candidate.Control);
		}

		private static System.Windows.Controls.Control? GetFocusedOverlayControl(FrameworkElement overlay)
		{
			IInputElement focusedElement = Keyboard.FocusedElement;
			DependencyObject val = (DependencyObject)((focusedElement is DependencyObject) ? focusedElement : null);
			if (val == null)
			{
				return null;
			}
			for (DependencyObject val2 = val; val2 != null; val2 = GetParentObject(val2))
			{
				if ((object)val2 == overlay)
				{
					return null;
				}
				if (val2 is System.Windows.Controls.Control result)
				{
					return result;
				}
			}
			return null;
		}

		private bool TryMoveMyGamesFocus(DashboardInputAction action)
		{
			if (_viewModel.IsLibraryShowingApps)
			{
				return TryMoveAppLibraryFocus(action);
			}
			DashboardInputAction dashboardInputAction = action;
			if ((uint)dashboardInputAction > 1u && (uint)(dashboardInputAction - 12) > 1u)
			{
				return true;
			}
			List<GameCardViewModel> list = _viewModel.LibraryMenuGames.ToList();
			if (list.Count == 0)
			{
				return true;
			}
			GameCardViewModel gameCardViewModel = _viewModel.SelectedGame ?? list.FirstOrDefault();
			if (gameCardViewModel == null)
			{
				_viewModel.SelectGame(list[0]);
				((DispatcherObject)this).Dispatcher.BeginInvoke((Delegate)(Action)delegate
				{
					FocusLibraryGameButton(_viewModel.SelectedGame);
				}, (DispatcherPriority)7, Array.Empty<object>());
				return true;
			}
			int num = list.IndexOf(gameCardViewModel);
			if (num < 0)
			{
				num = 0;
			}
			bool isPageStep = action == DashboardInputAction.PreviousTab || action == DashboardInputAction.NextTab;
			bool movingRight = action == DashboardInputAction.MoveRight || action == DashboardInputAction.NextTab;
			int num2;
			if (isPageStep)
			{
				const int pageSize = 6;
				int pageStart = num / pageSize * pageSize;
				int lastPageStart = (list.Count - 1) / pageSize * pageSize;
				num2 = Math.Clamp(pageStart + (movingRight ? pageSize : (-pageSize)), 0, lastPageStart);
			}
			else
			{
				num2 = (movingRight ? Math.Min(num + 1, list.Count - 1) : Math.Max(num - 1, 0));
			}
			if (num2 == num)
			{
				return true;
			}
			List<GameCardViewModel> list2 = _viewModel.VisibleLibraryMenuGames.ToList();
			GameCardViewModel nextGame = list[num2];
			_viewModel.SelectGame(nextGame);
			if (isPageStep)
			{
				_audioService.Play(movingRight ? "page-right" : "page-left");
			}
			if (list2.Count != _viewModel.VisibleLibraryMenuGames.Count || !list2.Zip(_viewModel.VisibleLibraryMenuGames).All(((GameCardViewModel First, GameCardViewModel Second) pair) => pair.First == pair.Second))
			{
				((DispatcherObject)this).Dispatcher.BeginInvoke((Delegate)(Action)delegate
				{
					AnimateMyGamesPageShift(movingRight ? DashboardInputAction.MoveRight : DashboardInputAction.MoveLeft);
				}, (DispatcherPriority)7, Array.Empty<object>());
			}
			((DispatcherObject)this).Dispatcher.BeginInvoke((Delegate)(Action)delegate
			{
				FocusLibraryGameButton(nextGame);
			}, (DispatcherPriority)7, Array.Empty<object>());
			return true;
		}

		private bool TryMoveAppLibraryFocus(DashboardInputAction action)
		{
			if ((uint)action > 3u)
			{
				return true;
			}
			List<AppLibraryTileViewModel> list = _viewModel.AppLibraryTiles.ToList();
			if (list.Count == 0)
			{
				return true;
			}
			AppLibraryTileViewModel appLibraryTileViewModel = _viewModel.SelectedAppLibraryTile ?? list[0];
			int num = list.IndexOf(appLibraryTileViewModel);
			if (num < 0)
			{
				num = 0;
			}
			int num2 = FindNearestAppLibraryTileIndex(list, num, action);
			AppLibraryTileViewModel appLibraryTileViewModel2 = list[num2];
			if (_viewModel.SelectedAppLibraryTile != appLibraryTileViewModel2)
			{
				_viewModel.SelectedAppLibraryTile = appLibraryTileViewModel2;
			}
			((DispatcherObject)this).Dispatcher.BeginInvoke((Delegate)(Action)delegate
			{
				FocusAppLibraryTileButton(appLibraryTileViewModel2);
			}, (DispatcherPriority)7, Array.Empty<object>());
			return true;
		}

		private static int FindNearestAppLibraryTileIndex(IReadOnlyList<AppLibraryTileViewModel> tiles, int currentIndex, DashboardInputAction action)
		{
			if (currentIndex < 0 || currentIndex >= tiles.Count)
			{
				return 0;
			}
			AppLibraryTileViewModel current = tiles[currentIndex];
			double currentCenterX = current.Left + current.Width / 2.0;
			double currentCenterY = current.Top + current.Height / 2.0;
			int bestIndex = currentIndex;
			double bestScore = double.MaxValue;
			for (int i = 0; i < tiles.Count; i++)
			{
				if (i == currentIndex)
				{
					continue;
				}
				AppLibraryTileViewModel candidate = tiles[i];
				double candidateCenterX = candidate.Left + candidate.Width / 2.0;
				double candidateCenterY = candidate.Top + candidate.Height / 2.0;
				double deltaX = candidateCenterX - currentCenterX;
				double deltaY = candidateCenterY - currentCenterY;
				bool isCandidate = action switch
				{
					DashboardInputAction.MoveLeft => deltaX < -1.0,
					DashboardInputAction.MoveRight => deltaX > 1.0,
					DashboardInputAction.MoveUp => deltaY < -1.0,
					DashboardInputAction.MoveDown => deltaY > 1.0,
					_ => false,
				};
				if (!isCandidate)
				{
					continue;
				}
				double primaryDistance = action == DashboardInputAction.MoveLeft || action == DashboardInputAction.MoveRight ? Math.Abs(deltaX) : Math.Abs(deltaY);
				double crossAxisDistance = action == DashboardInputAction.MoveLeft || action == DashboardInputAction.MoveRight ? Math.Abs(deltaY) : Math.Abs(deltaX);
				double score = primaryDistance * 1000.0 + crossAxisDistance;
				if (score < bestScore)
				{
					bestScore = score;
					bestIndex = i;
				}
			}
			return bestIndex;
		}

		private void AnimateMyGamesPageShift(DashboardInputAction action)
		{
			if (MyGamesStripTranslate != null && MyGamesStrip != null)
			{
				try
				{
					MyGamesScrollViewer.ScrollToHorizontalOffset(_viewModel.LibraryMenuScrollOffset);
				}
				catch
				{
				}
				double x = ((action == DashboardInputAction.MoveRight) ? 120.0 : (-120.0));
				MyGamesStripTranslate.BeginAnimation(TranslateTransform.XProperty, null);
				MyGamesStrip.BeginAnimation(UIElement.OpacityProperty, null);
				MyGamesStripTranslate.X = x;
				MyGamesStrip.Opacity = 0.86;
				TimeSpan timeSpan = TimeSpan.FromMilliseconds(230.0);
				CubicEase easingFunction = new CubicEase
				{
					EasingMode = EasingMode.EaseOut
				};
				MyGamesStripTranslate.BeginAnimation(TranslateTransform.XProperty, new DoubleAnimation(0.0, timeSpan)
				{
					EasingFunction = easingFunction
				});
				MyGamesStrip.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(1.0, TimeSpan.FromMilliseconds(150.0))
				{
					EasingFunction = new SineEase
					{
						EasingMode = EasingMode.EaseOut
					}
				});
			}
		}

		private void AnimateGameDetailsTabChange()
		{
			if (!_viewModel.IsDetailsOpen || GameDetailsOverlay == null)
			{
				_lastGameDetailsTabAnimationIndex = GetGameDetailsTabIndex(_viewModel.SelectedGameDetailsTabKey);
				return;
			}
			int gameDetailsTabIndex = GetGameDetailsTabIndex(_viewModel.SelectedGameDetailsTabKey);
			double x = ((gameDetailsTabIndex >= _lastGameDetailsTabAnimationIndex) ? 46.0 : (-46.0));
			_lastGameDetailsTabAnimationIndex = gameDetailsTabIndex;
			TimeSpan timeSpan = TimeSpan.FromMilliseconds(180.0);
			foreach (FrameworkElement item in from element in GameDetailsOverlay.Children.OfType<FrameworkElement>().Skip(4)
				where element.IsVisible
				select element)
			{
				TranslateTransform translateTransform = item.RenderTransform as TranslateTransform;
				if (translateTransform == null)
				{
					translateTransform = (TranslateTransform)(item.RenderTransform = new TranslateTransform());
				}
				translateTransform.BeginAnimation(TranslateTransform.XProperty, null);
				item.BeginAnimation(UIElement.OpacityProperty, null);
				translateTransform.X = x;
				item.Opacity = 0.96;
				translateTransform.BeginAnimation(TranslateTransform.XProperty, new DoubleAnimation(0.0, timeSpan)
				{
					EasingFunction = new CubicEase
					{
						EasingMode = EasingMode.EaseOut
					}
				});
				item.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(1.0, TimeSpan.FromMilliseconds(130.0))
				{
					EasingFunction = new SineEase
					{
						EasingMode = EasingMode.EaseOut
					}
				});
			}
		}

		private static int GetGameDetailsTabIndex(string key)
		{
			return key switch
			{
				"details" => 1, 
				"extras" => 2, 
				"gallery" => 3, 
				_ => 0, 
			};
		}

		private void MyGamesScrollViewer_OnRequestBringIntoView(object sender, RequestBringIntoViewEventArgs e)
		{
			e.Handled = true;
		}

	private void FocusLibraryGameButton(GameCardViewModel? game, int remainingRetries = 2, int requestId = 0)
	{
		if (_viewModel.IsDetailsOpen)
		{
			return;
		}
		if (game == null)
		{
			return;
		}
			if (requestId == 0)
			{
				requestId = ++_libraryFocusRequestId;
			}
			else if (requestId != _libraryFocusRequestId)
			{
				return;
			}
			try
			{
				MyGamesScrollViewer.ScrollToHorizontalOffset(_viewModel.LibraryMenuScrollOffset);
			}
			catch
			{
			}
			System.Windows.Controls.Button element = GetLibraryGameButtonForItem(game);
			if (TryFocus(element))
			{
				return;
			}
			if (remainingRetries > 0)
			{
				((DispatcherObject)this).Dispatcher.BeginInvoke((Delegate)(Action)delegate
				{
					FocusLibraryGameButton(game, remainingRetries - 1, requestId);
				}, (DispatcherPriority)7, Array.Empty<object>());
			}
			else if (!TryFocus(element))
			{
				FocusFirstButton();
			}
		}

		private System.Windows.Controls.Button? GetLibraryGameButtonForItem(GameCardViewModel game)
		{
			DependencyObject container = MyGamesItemsControl?.ItemContainerGenerator.ContainerFromItem(game) as DependencyObject;
			if (container == null)
			{
				return null;
			}
			return FindVisualChildren<System.Windows.Controls.Button>(container).FirstOrDefault((System.Windows.Controls.Button button) => button.CommandParameter == game);
		}

		private void FocusAppLibraryTileButton(AppLibraryTileViewModel? tile, int remainingRetries = 2)
		{
			if (tile == null)
			{
				return;
			}
			ScrollAppLibraryTileIntoView(tile);
			System.Windows.Controls.Button element = GetAppLibraryTileButtonForItem(tile);
			if (TryFocus(element))
			{
				return;
			}
			if (remainingRetries > 0)
			{
				((DispatcherObject)this).Dispatcher.BeginInvoke((Delegate)(Action)delegate
				{
					FocusAppLibraryTileButton(tile, remainingRetries - 1);
				}, (DispatcherPriority)7, Array.Empty<object>());
			}
		}

		private System.Windows.Controls.Button? GetAppLibraryTileButtonForItem(AppLibraryTileViewModel tile)
		{
			DependencyObject container = MyAppsItemsControl?.ItemContainerGenerator.ContainerFromItem(tile) as DependencyObject;
			if (container == null)
			{
				return null;
			}
			return FindVisualChildren<System.Windows.Controls.Button>(container).FirstOrDefault((System.Windows.Controls.Button button) => button.CommandParameter == tile);
		}

		private void MyAppsScrollViewer_OnPreviewMouseWheel(object sender, MouseWheelEventArgs e)
		{
			if (sender is ScrollViewer scrollViewer)
			{
				scrollViewer.ScrollToHorizontalOffset(scrollViewer.HorizontalOffset - e.Delta);
				e.Handled = true;
			}
		}

		private void AppLibraryTile_OnMouseEnter(object sender, System.Windows.Input.MouseEventArgs e)
		{
			if (sender is System.Windows.Controls.Button { CommandParameter: AppLibraryTileViewModel tile })
			{
				_viewModel.SelectedAppLibraryTile = tile;
				ScrollAppLibraryTileIntoView(tile);
			}
		}

		private void ScrollAppLibraryTileIntoView(AppLibraryTileViewModel tile)
		{
			try
			{
				if (MyAppsScrollViewer == null)
				{
					return;
				}
				double viewportLeft = MyAppsScrollViewer.HorizontalOffset;
				double viewportRight = viewportLeft + MyAppsScrollViewer.ViewportWidth;
				const double margin = 26.0;
				if (tile.Left < viewportLeft + margin)
				{
					MyAppsScrollViewer.ScrollToHorizontalOffset(Math.Max(0.0, tile.Left - margin));
				}
				else if (tile.Right > viewportRight - margin)
				{
					MyAppsScrollViewer.ScrollToHorizontalOffset(tile.Right - MyAppsScrollViewer.ViewportWidth + margin);
				}
			}
			catch (Exception exception)
			{
				App.LogException(exception, "MainWindow.ScrollAppLibraryTileIntoView");
			}
		}

		private List<OverlayFocusCandidate> GetLibraryGameButtons()
		{
			return (from button in FindVisualChildren<System.Windows.Controls.Button>((DependencyObject?)(object)MyGamesOverlay)
				where button.IsVisible && button.IsEnabled && button.Focusable && button.CommandParameter is GameCardViewModel
				select new OverlayFocusCandidate(button, GetElementBounds(button, MyGamesOverlay))).Where(delegate(OverlayFocusCandidate candidate)
			{
				//IL_0002: Unknown result type (might be due to invalid IL or missing references)
				//IL_0007: Unknown result type (might be due to invalid IL or missing references)
				//IL_001c: Unknown result type (might be due to invalid IL or missing references)
				//IL_0021: Unknown result type (might be due to invalid IL or missing references)
				Rect bounds = candidate.Bounds;
				if (bounds.Width > 0.0)
				{
					bounds = candidate.Bounds;
					return bounds.Height > 0.0;
				}
				return false;
			}).OrderBy(delegate(OverlayFocusCandidate candidate)
			{
				//IL_0002: Unknown result type (might be due to invalid IL or missing references)
				//IL_0007: Unknown result type (might be due to invalid IL or missing references)
				Rect bounds = candidate.Bounds;
				return bounds.Left;
			}).ToList();
		}

		private static List<OverlayFocusCandidate> GetOverlayFocusCandidates(FrameworkElement overlay)
		{
			try
			{
				return (from control in FindVisualChildren<System.Windows.Controls.Control>((DependencyObject?)(object)overlay)
					where control.IsVisible && control.IsEnabled && control.Focusable
					select new OverlayFocusCandidate(control, GetElementBounds(control, overlay))).Where(delegate(OverlayFocusCandidate candidate)
				{
					//IL_0002: Unknown result type (might be due to invalid IL or missing references)
					//IL_0007: Unknown result type (might be due to invalid IL or missing references)
					//IL_001c: Unknown result type (might be due to invalid IL or missing references)
					//IL_0021: Unknown result type (might be due to invalid IL or missing references)
					Rect bounds = candidate.Bounds;
					if (bounds.Width > 0.0)
					{
						bounds = candidate.Bounds;
						return bounds.Height > 0.0;
					}
					return false;
				}).Where(delegate(OverlayFocusCandidate candidate)
				{
					//IL_0002: Unknown result type (might be due to invalid IL or missing references)
					//IL_0007: Unknown result type (might be due to invalid IL or missing references)
					//IL_000c: Unknown result type (might be due to invalid IL or missing references)
					//IL_003f: Unknown result type (might be due to invalid IL or missing references)
					//IL_0044: Unknown result type (might be due to invalid IL or missing references)
					//IL_0059: Unknown result type (might be due to invalid IL or missing references)
					//IL_005e: Unknown result type (might be due to invalid IL or missing references)
					Point center = GetCenter(candidate.Bounds);
					if (center.X >= -200.0 && center.X <= overlay.ActualWidth + 200.0)
					{
						Rect bounds = candidate.Bounds;
						if (bounds.Bottom >= -200.0)
						{
							bounds = candidate.Bounds;
							return bounds.Top <= overlay.ActualHeight + 1200.0;
						}
					}
					return false;
				}).ToList();
			}
			catch
			{
				return new List<OverlayFocusCandidate>();
			}
		}

		private static Rect GetElementBounds(FrameworkElement element, Visual relativeTo)
		{
			//IL_0025: Unknown result type (might be due to invalid IL or missing references)
			//IL_002a: Unknown result type (might be due to invalid IL or missing references)
			//IL_002f: Unknown result type (might be due to invalid IL or missing references)
			//IL_0033: Unknown result type (might be due to invalid IL or missing references)
			//IL_0038: Unknown result type (might be due to invalid IL or missing references)
			//IL_003c: Unknown result type (might be due to invalid IL or missing references)
			//IL_0041: Unknown result type (might be due to invalid IL or missing references)
			//IL_0045: Unknown result type (might be due to invalid IL or missing references)
			//IL_004a: Unknown result type (might be due to invalid IL or missing references)
			//IL_004d: Unknown result type (might be due to invalid IL or missing references)
			try
			{
				return element.TransformToAncestor(relativeTo).TransformBounds(new Rect(0.0, 0.0, element.ActualWidth, element.ActualHeight));
			}
			catch (InvalidOperationException)
			{
				return Rect.Empty;
			}
			catch (ArgumentException)
			{
				return Rect.Empty;
			}
			catch
			{
				return Rect.Empty;
			}
		}

		private static Point GetCenter(Rect rect)
		{
			//IL_0032: Unknown result type (might be due to invalid IL or missing references)
			return new Point(rect.Left + rect.Width / 2.0, rect.Top + rect.Height / 2.0);
		}

		private bool ShouldBringFocusedElementIntoView(FrameworkElement element)
		{
			if (!IsOverlayOpen() && IsDescendantOf(element, ContentHost))
			{
				return false;
			}
			return true;
		}

		private static bool IsDescendantOf(DependencyObject child, DependencyObject ancestor)
		{
			DependencyObject? dependencyObject = child;
			while (dependencyObject != null)
			{
				if (dependencyObject == ancestor)
				{
					return true;
				}
				dependencyObject = GetParentObject(dependencyObject);
			}
			return false;
		}

		private bool TryFocus(UIElement? element)
		{
			if (element == null || !element.IsVisible || !element.Focusable)
			{
				return false;
			}
			if (element is System.Windows.Controls.Control && !element.IsEnabled)
			{
				return false;
			}
			try
			{
				bool num = element.Focus();
				bool flag = _viewModel.IsMyGamesOpen && element is System.Windows.Controls.Button button && button.CommandParameter is GameCardViewModel;
				if (num && element is FrameworkElement frameworkElement && !flag && ShouldBringFocusedElementIntoView(frameworkElement))
				{
					frameworkElement.BringIntoView();
				}
				if (num && !_viewModel.IsDetailsOpen && element is System.Windows.Controls.Button { CommandParameter: GameCardViewModel commandParameter })
				{
					_viewModel.SelectGame(commandParameter);
				}
				return num;
			}
			catch
			{
				return false;
			}
		}

		private bool TryMoveThemeMenuFocus(DashboardInputAction action)
		{
			List<System.Windows.Controls.Button> themeButtons = FindVisualChildren<System.Windows.Controls.Button>((DependencyObject?)(object)ThemeMenuOverlay)
				.Where((System.Windows.Controls.Button button) => button.IsVisible && button.IsEnabled && button.Focusable && button.Command == _viewModel.SelectThemeCommand)
				.ToList();
			if (themeButtons.Count == 0)
			{
				return false;
			}
			if (action != DashboardInputAction.MoveUp && action != DashboardInputAction.MoveDown)
			{
				return TryFocus(themeButtons.Contains(Keyboard.FocusedElement as System.Windows.Controls.Button) ? (System.Windows.Controls.Button)Keyboard.FocusedElement : themeButtons[0]);
			}
			System.Windows.Controls.Button? currentButton = Keyboard.FocusedElement as System.Windows.Controls.Button;
			int currentIndex = currentButton == null ? -1 : themeButtons.IndexOf(currentButton);
			int nextIndex = currentIndex < 0 ? 0 : currentIndex + (action == DashboardInputAction.MoveDown ? 1 : -1);
			nextIndex = Math.Max(0, Math.Min(themeButtons.Count - 1, nextIndex));
			return TryFocus(themeButtons[nextIndex]);
		}

		private bool TryMoveProfileMenuFocus(DashboardInputAction action)
		{
			List<System.Windows.Controls.Button> menuButtons = FindVisualChildren<System.Windows.Controls.Button>((DependencyObject?)(object)ProfileEditorOverlay)
				.Where((System.Windows.Controls.Button button) => button.IsVisible && button.IsEnabled && button.Focusable && string.Equals(button.Tag as string, "ProfileMenuOption", StringComparison.OrdinalIgnoreCase))
				.ToList();
			List<System.Windows.Controls.TextBox> editFields = FindVisualChildren<System.Windows.Controls.TextBox>((DependencyObject?)(object)ProfileEditorOverlay)
				.Where((System.Windows.Controls.TextBox textBox) => textBox.IsVisible && textBox.IsEnabled && textBox.Focusable && string.Equals(textBox.Tag as string, "ProfileEditField", StringComparison.OrdinalIgnoreCase))
				.ToList();
			if (menuButtons.Count == 0)
			{
				return false;
			}
			System.Windows.Controls.TextBox? currentTextBox = Keyboard.FocusedElement as System.Windows.Controls.TextBox;
			if (currentTextBox != null && editFields.Contains(currentTextBox))
			{
				if (action == DashboardInputAction.MoveLeft)
				{
					return TryFocus(ProfileMenuEditButton ?? menuButtons[Math.Min(1, menuButtons.Count - 1)]);
				}
				if (action == DashboardInputAction.MoveUp || action == DashboardInputAction.MoveDown)
				{
					int currentFieldIndex = editFields.IndexOf(currentTextBox);
					int nextFieldIndex = currentFieldIndex + (action == DashboardInputAction.MoveDown ? 1 : -1);
					nextFieldIndex = Math.Max(0, Math.Min(editFields.Count - 1, nextFieldIndex));
					return TryFocus(editFields[nextFieldIndex]);
				}
				return TryFocus(currentTextBox);
			}
			if (_viewModel.IsProfileMenuEditing && action == DashboardInputAction.MoveRight && editFields.Count > 0)
			{
				return TryFocus(editFields[0]);
			}
			if (action != DashboardInputAction.MoveUp && action != DashboardInputAction.MoveDown)
			{
				return TryFocus(menuButtons.Contains(Keyboard.FocusedElement as System.Windows.Controls.Button) ? (System.Windows.Controls.Button)Keyboard.FocusedElement : menuButtons[0]);
			}
			System.Windows.Controls.Button? currentButton = Keyboard.FocusedElement as System.Windows.Controls.Button;
			int currentIndex = currentButton == null ? -1 : menuButtons.IndexOf(currentButton);
			int nextIndex = currentIndex < 0 ? 0 : currentIndex + (action == DashboardInputAction.MoveDown ? 1 : -1);
			nextIndex = Math.Max(0, Math.Min(menuButtons.Count - 1, nextIndex));
			return TryFocus(menuButtons[nextIndex]);
		}

		private static UIElement? FindFocusableControl(DependencyObject? root)
		{
			return FindVisualChildren<UIElement>(root).FirstOrDefault((UIElement element) => element is System.Windows.Controls.Control && element.IsVisible && element.IsEnabled && element.Focusable);
		}

		private System.Windows.Controls.Button? FindThemeMenuFocusButton()
		{
			List<System.Windows.Controls.Button> themeButtons = FindVisualChildren<System.Windows.Controls.Button>((DependencyObject?)(object)ThemeMenuOverlay)
				.Where((System.Windows.Controls.Button button) => button.IsVisible && button.IsEnabled && button.Focusable && button.Command == _viewModel.SelectThemeCommand)
				.ToList();
			if (themeButtons.Count == 0)
			{
				return null;
			}
			if (_viewModel.SelectedTheme != null)
			{
				System.Windows.Controls.Button? selectedButton = themeButtons.FirstOrDefault((System.Windows.Controls.Button button) => button.CommandParameter is DashboardTheme theme && string.Equals(theme.Name, _viewModel.SelectedTheme.Name, StringComparison.OrdinalIgnoreCase));
				if (selectedButton != null)
				{
					return selectedButton;
				}
			}
			return themeButtons[0];
		}

		private static T? FindVisualChild<T>(DependencyObject? root) where T : DependencyObject
		{
			if (root == null)
			{
				return default(T);
			}
			int childrenCount;
			try
			{
				childrenCount = VisualTreeHelper.GetChildrenCount(root);
			}
			catch
			{
				return default(T);
			}
			for (int i = 0; i < childrenCount; i++)
			{
				DependencyObject child;
				try
				{
					child = VisualTreeHelper.GetChild(root, i);
				}
				catch
				{
					continue;
				}
				T val = (T)(object)((child is T) ? child : null);
				if (val != null)
				{
					return val;
				}
				T val2 = FindVisualChild<T>(child);
				if (val2 != null)
				{
					return val2;
				}
			}
			return default(T);
		}

		private static DependencyObject? GetParentObject(DependencyObject current)
		{
			if (current is Visual || current is Visual3D)
			{
				return VisualTreeHelper.GetParent(current);
			}
			if (current is FrameworkContentElement frameworkContentElement)
			{
				return frameworkContentElement.Parent;
			}
			return null;
		}

		private static IEnumerable<T> FindVisualChildren<T>(DependencyObject? root) where T : DependencyObject
		{
			if (root == null)
			{
				yield break;
			}
			int childCount;
			try
			{
				childCount = VisualTreeHelper.GetChildrenCount(root);
			}
			catch
			{
				yield break;
			}
			for (int i = 0; i < childCount; i++)
			{
				DependencyObject child;
				try
				{
					child = VisualTreeHelper.GetChild(root, i);
				}
				catch
				{
					continue;
				}
				T val = (T)(object)((child is T) ? child : null);
				if (val != null)
				{
					yield return val;
				}
				foreach (T item in FindVisualChildren<T>(child))
				{
					yield return item;
				}
			}
		}

		private void WritePerformanceDebugReport()
		{
			try
			{
				ImageCacheService.ImageCacheSnapshot snapshot = ImageCacheService.GetSnapshot();
				Process currentProcess = Process.GetCurrentProcess();
				bool num = _clockTimer.IsEnabled;
				DispatcherTimer? bootStateTimer = _bootStateTimer;
				int value = (num ? 1 : 0) + ((bootStateTimer != null && bootStateTimer.IsEnabled) ? 1 : 0) + (_performanceDebugTimer.IsEnabled ? 1 : 0) + (_viewModel.IsMusicProgressTimerActive ? 1 : 0) + (_controllerInputService.IsRunning ? 1 : 0) + (_guideViewModel?.ActiveTimerCount ?? 0);
				string[] contents = new string[13]
				{
					"[PERFORMANCE]",
					$"timestamp: {DateTime.Now:O}",
					$"ram working set: {(double)currentProcess.WorkingSet64 / 1024.0 / 1024.0:0.0} MB",
					$"ram private bytes: {(double)currentProcess.PrivateMemorySize64 / 1024.0 / 1024.0:0.0} MB",
					$"loaded image count: {snapshot.LoadedImageCount}",
					$"loaded cover count: {snapshot.LoadedCoverCount}",
					$"visible my games tiles: {_viewModel.VisibleLibraryMenuGames.Count}",
					$"largest cached image: {snapshot.LargestPixelWidth}x{snapshot.LargestPixelHeight}",
					$"active timers: {value}",
					"visualizer running: " + ((MusicVisualizer.ActiveRendererCount > 0) ? "yes" : "no"),
					$"visualizer instances active: {MusicVisualizer.ActiveRendererCount}",
					"audio analysis running: " + (_viewModel.IsAudioAnalysisRunning ? "yes" : "no"),
					"music playing: " + (_viewModel.IsMusicPlaying ? "yes" : "no")
				};
				Directory.CreateDirectory(System.IO.Path.GetDirectoryName(PerformanceDebugLogPath));
				File.WriteAllLines(PerformanceDebugLogPath, contents);
			}
			catch (Exception exception)
			{
				App.LogException(exception, "MainWindow.WritePerformanceDebugReport");
			}
		}
	}
