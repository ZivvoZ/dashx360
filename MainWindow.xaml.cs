using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using XboxMetroLauncher.Input;
using XboxMetroLauncher.Services;
using XboxMetroLauncher.Utilities;
using XboxMetroLauncher.ViewModels;
using XboxMetroLauncher.Views;

namespace XboxMetroLauncher;

public partial class MainWindow : Window
{
    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool BringWindowToTop(IntPtr hWnd);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsWindow(IntPtr hWnd);

    private readonly DashboardViewModel _viewModel;
    private readonly ControllerInputService _controllerInputService;
    private readonly GlobalHotkeyService _guideHotkeyService;
    private readonly IAudioService _audioService;
    private readonly IFriendsService _friendsService;
    private readonly SocialIntegrationManager _socialIntegrationManager;
    private readonly DiscordPartyService _discordPartyService;
    private readonly DispatcherTimer _clockTimer;
    private readonly Dictionary<string, Button> _lastFocusedButtonByTab = [];
    private System.Windows.Forms.WebBrowser? _bootBrowser;
    private DispatcherTimer? _bootStateTimer;
    private DateTime _bootStartedAt;
    private int _lastTabIndex = 1;
    private const double FocusZoneLeft = 64;
    private const double FocusZoneRight = 1120;
    private bool _bootSkipped;
    private bool _isAnimatingTab;
    private bool _isFocusUpdateQueued;
    private int _queuedTabStep;
    private object? _lastRenderedTab;
    private GuideWindow? _guideWindow;
    private GuideViewModel? _guideViewModel;
    private UIElement? _guideReturnFocusElement;
    private bool _restoreFocusAfterGuideClose;
    private IntPtr _guideReturnWindowHandle;
    private bool _guideRestoreExternalWindow;

    public MainWindow()
    {
        InitializeComponent();

        var appData = GetWritableAppDataPath();

        var store = new JsonStore(appData);
        _friendsService = new FriendsService(store);
        var localSocialIntegrationService = new LocalSocialIntegrationService(_friendsService);
        _socialIntegrationManager = new SocialIntegrationManager(_friendsService, localSocialIntegrationService);
        _discordPartyService = new DiscordPartyService();
        ISettingsService settingsService = new SettingsService(store);
        DashboardViewModel? viewModel = null;
        var audioService = new AudioService(() => viewModel?.Settings.PlayUiSounds ?? true, AudioHost);
        _audioService = audioService;

        _viewModel = new DashboardViewModel(
            new JsonGameLibraryService(store),
            new GameLaunchService(),
            new SearchService(),
            settingsService,
            new ProfileService(store),
            new WindowsFilePickerService(),
            new RegistryStartupRegistrationService(),
            audioService,
            _socialIntegrationManager);

        viewModel = _viewModel;
        DataContext = _viewModel;
        _lastRenderedTab = _viewModel.CurrentTab;

        _controllerInputService = new ControllerInputService(HandleInputAction, () => _viewModel.Settings.EnableControllerInput);
        _guideHotkeyService = new GlobalHotkeyService();
        _guideHotkeyService.HotkeyPressed += (_, _) => Dispatcher.BeginInvoke(new Action(() => HandleInputAction(DashboardInputAction.Guide)), DispatcherPriority.Send);
        _clockTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(15) };
        _clockTimer.Tick += (_, _) => _viewModel.UpdateClock();
        _viewModel.PropertyChanged += ViewModel_OnPropertyChanged;
        _viewModel.FriendsOverlayRequested += ViewModel_OnFriendsOverlayRequested;

        // Apply the default/saved fullscreen intent immediately so slow startup
        // work does not leave the launcher sitting in a normal window first.
        ApplyDisplaySettings();
    }

    private static string GetWritableAppDataPath()
    {
        var candidates = new[]
        {
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "XboxMetroLauncher"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "XboxMetroLauncher"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "XboxMetroLauncherData"),
            Path.Combine(AppContext.BaseDirectory, "UserData")
        };

        foreach (var candidate in candidates.Where(path => !string.IsNullOrWhiteSpace(path)))
        {
            try
            {
                Directory.CreateDirectory(candidate);
                var probe = Path.Combine(candidate, ".write-test");
                File.WriteAllText(probe, "ok");
                File.Delete(probe);
                return candidate;
            }
            catch (UnauthorizedAccessException)
            {
            }
            catch (IOException)
            {
            }
        }

        var fallback = Path.Combine(AppContext.BaseDirectory, "UserData");
        Directory.CreateDirectory(fallback);
        return fallback;
    }

    private async void Window_OnLoaded(object sender, RoutedEventArgs e)
    {
        try
        {
            ApplyDisplaySettings();
            _controllerInputService.Start();
            _guideHotkeyService.Register(this);
            _clockTimer.Start();
            StartBootVideo();
            await _viewModel.InitializeAsync();

            _viewModel.Settings.PropertyChanged += Settings_OnPropertyChanged;
            ApplyDisplaySettings();
            FocusFirstButton();
        }
        catch (Exception ex)
        {
            App.LogException(ex, "MainWindow.Window_OnLoaded");
        }
    }

    private void Window_OnClosing(object? sender, CancelEventArgs e)
    {
        _viewModel.Settings.PropertyChanged -= Settings_OnPropertyChanged;
        _viewModel.FriendsOverlayRequested -= ViewModel_OnFriendsOverlayRequested;
        _guideWindow?.Close();
        _guideViewModel?.Dispose();
        _guideHotkeyService.Dispose();
        _controllerInputService.Dispose();
        _clockTimer.Stop();
    }

    private void Settings_OnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(_viewModel.Settings.DisplayResolution) or nameof(_viewModel.Settings.StartFullscreen))
        {
            ApplyDisplaySettings();
        }
    }

    private void ApplyDisplaySettings()
    {
        var (width, height) = _viewModel.Settings.DisplayResolution switch
        {
            "720p" => (1280d, 720d),
            "1440p" => (2560d, 1440d),
            "4K" => (3840d, 2160d),
            _ => (1920d, 1080d)
        };

        if (_viewModel.Settings.StartFullscreen)
        {
            WindowStyle = WindowStyle.None;
            ResizeMode = ResizeMode.NoResize;
            WindowState = WindowState.Maximized;
            return;
        }

        WindowState = WindowState.Normal;
        WindowStyle = WindowStyle.SingleBorderWindow;
        ResizeMode = ResizeMode.CanResize;

        var workArea = SystemParameters.WorkArea;
        Width = Math.Min(width, workArea.Width);
        Height = Math.Min(height, workArea.Height);
        Left = workArea.Left + Math.Max(0, (workArea.Width - Width) / 2);
        Top = workArea.Top + Math.Max(0, (workArea.Height - Height) / 2);
    }

    private void Window_OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (_viewModel.IsBooting)
        {
            SkipBootIntro();
            e.Handled = true;
            return;
        }

        if (!DashboardInputRouter.TryMapKey(e, out var action))
        {
            return;
        }

        e.Handled = true;
        HandleInputAction(action);
    }

    private void Window_OnPreviewMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (!_viewModel.IsBooting)
        {
            return;
        }

        SkipBootIntro();
        e.Handled = true;
    }

    private void HandleInputAction(DashboardInputAction action)
    {
        try
        {
            HandleInputActionCore(action);
        }
        catch (Exception ex)
        {
            App.LogException(ex, "MainWindow.HandleInputAction");
            _isAnimatingTab = false;
            _isFocusUpdateQueued = false;
        }
    }

    private void HandleInputActionCore(DashboardInputAction action)
    {
        if (action == DashboardInputAction.Guide)
        {
            if (_guideWindow?.IsTransitioning == true)
            {
                return;
            }

            if (_guideWindow?.IsGuideOpen == true)
            {
                HideGuide();
            }
            else
            {
                ShowGuide();
            }

            return;
        }

        if (_guideWindow?.IsGuideOpen == true)
        {
            _guideWindow.HandleInput(action);
            return;
        }

        if (WindowState == WindowState.Minimized || !IsVisible || !IsActive)
        {
            return;
        }

        if (_viewModel.IsBooting)
        {
            SkipBootIntro();
            return;
        }

        if (IsOverlayOpen()
            && action is DashboardInputAction.PreviousTab or DashboardInputAction.NextTab)
        {
            return;
        }

        if (_isAnimatingTab
            && action is DashboardInputAction.PreviousTab or DashboardInputAction.NextTab or DashboardInputAction.MoveLeft or DashboardInputAction.MoveRight)
        {
            if (!IsOverlayOpen())
            {
                _queuedTabStep = action is DashboardInputAction.PreviousTab or DashboardInputAction.MoveLeft ? -1 : 1;
            }

            return;
        }

        if (action is DashboardInputAction.PreviousTab or DashboardInputAction.NextTab)
        {
            RememberFocusedButton();
        }

        if (action is DashboardInputAction.MoveLeft or DashboardInputAction.MoveRight or DashboardInputAction.MoveUp or DashboardInputAction.MoveDown)
        {
            if (TryRestoreOverlayFocus())
            {
                _viewModel.HandleInput(action);
                return;
            }

            var moved = TryMoveDashboardFocus(action);
            if (!moved && action == DashboardInputAction.MoveLeft)
            {
                if (!IsOverlayOpen())
                {
                    _viewModel.MoveTab(-1);
                }
            }
            else if (!moved && action == DashboardInputAction.MoveRight)
            {
                if (!IsOverlayOpen())
                {
                    _viewModel.MoveTab(1);
                }
            }

            _viewModel.HandleInput(action);
            return;
        }

        if (action == DashboardInputAction.Activate && TryRestoreOverlayFocus())
        {
            return;
        }

        if (action == DashboardInputAction.Activate && Keyboard.FocusedElement is TextBox textBox)
        {
            textBox.GetBindingExpression(TextBox.TextProperty)?.UpdateSource();
            if ((_viewModel.IsSearchOverlayOpen || _viewModel.CurrentTab?.Key == "bing")
                && _viewModel.SubmitSearchCommand.CanExecute(null))
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

        if (action == DashboardInputAction.Search)
        {
            Dispatcher.BeginInvoke(() =>
            {
                try
                {
                    SearchOverlayTextBox.Focus();
                    SearchOverlayTextBox.SelectAll();
                }
                catch
                {
                }
            }, DispatcherPriority.Background);
        }
    }

    private void ShowGuide()
    {
        try
        {
            EnsureGuideWindow();
            if (_guideWindow is null || _guideWindow.IsTransitioning || _guideWindow.IsGuideOpen)
            {
                return;
            }

            RememberGuideReturnFocus();
            _guideWindow.Open();
        }
        catch (InvalidOperationException)
        {
            EnsureGuideWindow();
            if (_guideWindow is null || _guideWindow.IsTransitioning || _guideWindow.IsGuideOpen)
            {
                return;
            }

            RememberGuideReturnFocus();
            _guideWindow.Open();
        }
        catch (Exception ex)
        {
            App.LogException(ex, "MainWindow.ShowGuide");
        }
    }

    private void EnsureGuideWindow()
    {
        if (_guideWindow is not null && _guideWindow.IsLoaded && PresentationSource.FromVisual(_guideWindow) is not null)
        {
            return;
        }

        if (_guideWindow is not null)
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
        _guideViewModel = new GuideViewModel(_viewModel, this, HideGuide, _audioService, _friendsService, _socialIntegrationManager, _discordPartyService);
        _guideWindow = new GuideWindow(_guideViewModel);
        _guideWindow.HiddenCompleted += GuideWindow_OnHiddenCompleted;
        _guideWindow.Closed += GuideWindow_OnClosed;
    }

    private void HideGuide()
    {
        try
        {
            if (_guideWindow is null || _guideWindow.IsTransitioning || !_guideWindow.IsGuideOpen)
            {
                return;
            }

            _restoreFocusAfterGuideClose = _guideWindow.CloseGuide();
            if (!_guideRestoreExternalWindow)
            {
                Activate();
            }
        }
        catch (Exception ex)
        {
            App.LogException(ex, "MainWindow.HideGuide");
        }
    }

    private void ViewModel_OnFriendsOverlayRequested(object? sender, EventArgs e)
    {
        try
        {
            OpenGuideFriendsOverlay();
        }
        catch (Exception ex)
        {
            App.LogException(ex, "MainWindow.ViewModel_OnFriendsOverlayRequested");
        }
    }

    private void OpenGuideFriendsOverlay()
    {
        EnsureGuideWindow();
        if (_guideWindow is null || _guideViewModel is null || _guideWindow.IsTransitioning)
        {
            return;
        }

        RememberGuideReturnFocus();
        _guideViewModel.OpenFriendsOverlayFromDashboard();
        _guideWindow.Open();
    }

    private void RememberGuideReturnFocus()
    {
        var mainHandle = new WindowInteropHelper(this).Handle;
        var foregroundHandle = GetForegroundWindow();

        _guideRestoreExternalWindow = foregroundHandle != IntPtr.Zero && foregroundHandle != mainHandle;
        _guideReturnWindowHandle = _guideRestoreExternalWindow ? foregroundHandle : IntPtr.Zero;
        _guideReturnFocusElement = _guideRestoreExternalWindow ? null : Keyboard.FocusedElement as UIElement;
        RememberFocusedButton();
    }

    private void GuideWindow_OnHiddenCompleted(object? sender, EventArgs e)
    {
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
            return;
        }

        _restoreFocusAfterGuideClose = false;
        RestoreFocusAfterGuide();
    }

    private void GuideWindow_OnClosed(object? sender, EventArgs e)
    {
        if (_guideWindow is not null)
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
        Dispatcher.BeginInvoke(() =>
        {
            try
            {
                if (IsOverlayOpen())
                {
                    FocusFirstButton();
                    return;
                }

                if (_guideReturnFocusElement is not null && TryFocus(_guideReturnFocusElement))
                {
                    RememberFocusedButton();
                    return;
                }

                FocusFirstButton();
            }
            catch (Exception ex)
            {
                App.LogException(ex, "MainWindow.RestoreFocusAfterGuide");
            }
            finally
            {
                _guideReturnFocusElement = null;
                _guideRestoreExternalWindow = false;
                _guideReturnWindowHandle = IntPtr.Zero;
            }
        }, DispatcherPriority.Input);
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
        var bootVideoPath = AppPaths.FindFile(Path.Combine("Assets", "Boot", "Boot Screen.mp4"));
        if (!File.Exists(bootVideoPath))
        {
            SkipBootIntro();
            return;
        }

        if (!EnsureBootBrowser())
        {
            SkipBootIntro();
            return;
        }

        StartBrowserBootPlayback(bootVideoPath);
    }

    private bool EnsureBootBrowser()
    {
        if (_bootBrowser is not null)
        {
            return true;
        }

        try
        {
            var browser = new System.Windows.Forms.WebBrowser
            {
                Dock = System.Windows.Forms.DockStyle.Fill,
                AllowWebBrowserDrop = false,
                IsWebBrowserContextMenuEnabled = false,
                ScrollBarsEnabled = false,
                WebBrowserShortcutsEnabled = false
            };

            BootVideoHost.Child = browser;
            _bootBrowser = browser;
            return true;
        }
        catch
        {
            return false;
        }
    }

    private void StartBrowserBootPlayback(string bootVideoPath)
    {
        try
        {
            var videoUri = new Uri(bootVideoPath).AbsoluteUri;
            _bootBrowser!.DocumentText = $$"""
                <!doctype html>
                <html>
                <head>
                    <meta http-equiv="X-UA-Compatible" content="IE=edge" />
                    <style>
                        html, body {
                            width: 100%;
                            height: 100%;
                            margin: 0;
                            overflow: hidden;
                            background: #fff;
                        }
                        video {
                            width: 100vw;
                            height: 100vh;
                            object-fit: contain;
                            background: #fff;
                            display: block;
                        }
                    </style>
                </head>
                <body>
                    <video id="boot" src="{{videoUri}}" autoplay muted playsinline></video>
                    <script>
                        var boot = document.getElementById('boot');
                        boot.play();
                    </script>
                </body>
                </html>
                """;

            StartBootAudio();

            _bootStartedAt = DateTime.UtcNow;
            _bootStateTimer ??= new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(250) };
            _bootStateTimer.Tick -= BootStateTimer_OnTick;
            _bootStateTimer.Tick += BootStateTimer_OnTick;
            _bootStateTimer.Start();
        }
        catch
        {
            SkipBootIntro();
        }
    }

    private void StartBootAudio()
    {
        var startupSoundPath = AppPaths.FindFile(Path.Combine("Assets", "Audio", "Sounds", "02. Startup (2010).mp3"));
        if (!File.Exists(startupSoundPath))
        {
            return;
        }

        try
        {
            _audioService.Play("startup");
        }
        catch
        {
        }
    }

    private void BootStateTimer_OnTick(object? sender, EventArgs e)
    {
        try
        {
            var ended = (bool?)_bootBrowser?.Document?.InvokeScript("eval", ["document.getElementById('boot') && document.getElementById('boot').ended"]) == true;
            if (ended || DateTime.UtcNow - _bootStartedAt > TimeSpan.FromSeconds(12))
            {
                SkipBootIntro();
            }
        }
        catch
        {
            if (DateTime.UtcNow - _bootStartedAt > TimeSpan.FromSeconds(12))
            {
                SkipBootIntro();
            }
        }
    }

    private void SkipBootIntro()
    {
        if (_bootSkipped)
        {
            return;
        }

        _bootSkipped = true;
        _bootStateTimer?.Stop();
        try
        {
            _bootBrowser?.Document?.InvokeScript("eval", ["var v=document.getElementById('boot'); if(v){v.pause(); v.currentTime=0;}"]);
        }
        catch
        {
        }

        _viewModel.IsBooting = false;
        QueueFocusFirstButton();
    }

    private void MusicFullscreenHint_OnMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (_viewModel.OpenMusicVisualizerFullscreenCommand.CanExecute(null))
        {
            _viewModel.OpenMusicVisualizerFullscreenCommand.Execute(null);
            e.Handled = true;
        }
    }

    private void ViewModel_OnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(DashboardViewModel.CurrentTab))
        {
            AnimateTabChange();
            QueueFocusFirstButton();
        }
        else if (e.PropertyName is nameof(DashboardViewModel.IsMyGamesOpen) or nameof(DashboardViewModel.IsLauncherSettingsOpen) or nameof(DashboardViewModel.IsProfileEditorOpen) or nameof(DashboardViewModel.IsMusicPlayerOpen))
        {
            Dispatcher.BeginInvoke(new Action(AnimateActiveOverlayIn), DispatcherPriority.Loaded);
            QueueFocusFirstButton();
        }
    }

    private void AnimateTabChange()
    {
        if (_viewModel.Tabs.Count == 0)
        {
            return;
        }

        var newIndex = _viewModel.CurrentTab is null ? _lastTabIndex : _viewModel.Tabs.IndexOf(_viewModel.CurrentTab);
        if (newIndex < 0)
        {
            newIndex = Math.Clamp(_lastTabIndex, 0, _viewModel.Tabs.Count - 1);
        }

        var oldTab = _lastRenderedTab;
        var newTab = _viewModel.CurrentTab;
        if (ReferenceEquals(oldTab, newTab))
        {
            _lastTabIndex = newIndex;
            return;
        }

        var direction = newIndex >= _lastTabIndex ? 1 : -1;
        _lastTabIndex = newIndex;
        _lastRenderedTab = newTab;
        _isAnimatingTab = true;
        _queuedTabStep = 0;

        try
        {
            ContentSlide.BeginAnimation(TranslateTransform.XProperty, null);
            ContentHost.BeginAnimation(OpacityProperty, null);
            TabTransitionSlide.BeginAnimation(TranslateTransform.XProperty, null);
            TabTransitionLayer.BeginAnimation(OpacityProperty, null);
            AdjacentPreviewLayer.BeginAnimation(OpacityProperty, null);
            PreviousPreviewOffset.BeginAnimation(TranslateTransform.XProperty, null);
            NextPreviewOffset.BeginAnimation(TranslateTransform.XProperty, null);

            PrepareTabTransitionStrip(oldTab, newTab, direction);

            ContentHost.Opacity = 0;
            ContentSlide.X = 0;
            TabTransitionLayer.Visibility = Visibility.Visible;
            TabTransitionLayer.Opacity = 1;
            AdjacentPreviewLayer.Opacity = 0.30;
            PreviousPreviewOffset.X = direction > 0 ? 18 : 10;
            NextPreviewOffset.X = direction > 0 ? 10 : -8;

            var start = -1280.0;
            var end = direction > 0 ? -2560.0 : 0.0;
            TabTransitionSlide.X = start;

            var duration = TimeSpan.FromMilliseconds(385);
            var slide = new DoubleAnimation(start, end, duration)
            {
                EasingFunction = new QuinticEase { EasingMode = EasingMode.EaseInOut }
            };
            slide.Completed += (_, _) => FinishTabAnimation();
            TabTransitionSlide.BeginAnimation(TranslateTransform.XProperty, slide);

            AdjacentPreviewLayer.BeginAnimation(OpacityProperty, new DoubleAnimation(0.30, 0.36, duration)
            {
                EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut }
            });
            PreviousPreviewOffset.BeginAnimation(TranslateTransform.XProperty, new DoubleAnimation(0, duration)
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            });
            NextPreviewOffset.BeginAnimation(TranslateTransform.XProperty, new DoubleAnimation(0, duration)
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            });
        }
        catch
        {
            _isAnimatingTab = false;
        }
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
        if (tab is not ViewModels.Tabs.DashboardTabViewModel dashboardTab)
        {
            return null;
        }

        var index = _viewModel.Tabs.IndexOf(dashboardTab);
        var target = index + step;
        return target >= 0 && target < _viewModel.Tabs.Count ? _viewModel.Tabs[target] : null;
    }

    private void FinishTabAnimation()
    {
        _isAnimatingTab = false;
        TabTransitionLayer.Visibility = Visibility.Collapsed;
        TabTransitionLayer.Opacity = 0;
        TabTransitionSlide.X = -1280;
        PreviousPreviewOffset.X = 0;
        NextPreviewOffset.X = 0;
        TransitionLeftHost.Content = null;
        TransitionCenterHost.Content = null;
        TransitionRightHost.Content = null;
        ContentHost.Opacity = 1;
        AdjacentPreviewLayer.Opacity = 0.36;

        if (_queuedTabStep == 0 || IsOverlayOpen())
        {
            _queuedTabStep = 0;
            return;
        }

        var step = _queuedTabStep;
        _queuedTabStep = 0;
        Dispatcher.BeginInvoke(() => _viewModel.MoveTab(step), DispatcherPriority.Input);
    }

    private void AnimateActiveOverlayIn()
    {
        var overlay = GetActiveOverlay() as FrameworkElement;
        if (overlay is null || !overlay.IsVisible)
        {
            return;
        }

        AnimateOverlayIn(overlay);
    }

    private static void AnimateOverlayIn(FrameworkElement overlay)
    {
        overlay.BeginAnimation(OpacityProperty, null);

        if (overlay.RenderTransform is not TranslateTransform offset)
        {
            offset = new TranslateTransform();
            overlay.RenderTransform = offset;
        }

        overlay.Opacity = 0;
        offset.Y = 14;

        overlay.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(180))
        {
            EasingFunction = new SineEase { EasingMode = EasingMode.EaseOut }
        });

        offset.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(14, 0, TimeSpan.FromMilliseconds(240))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        });
    }

    private void QueueFocusFirstButton()
    {
        if (_isFocusUpdateQueued)
        {
            return;
        }

        _isFocusUpdateQueued = true;
        Dispatcher.BeginInvoke(() =>
        {
            try
            {
                _isFocusUpdateQueued = false;
                FocusFirstButton();
            }
            catch (Exception ex)
            {
                _isFocusUpdateQueued = false;
                App.LogException(ex, "MainWindow.QueueFocusFirstButton");
            }
        }, DispatcherPriority.Background);
    }

    private void FocusFirstButton()
    {
        if (_viewModel.IsMyGamesOpen)
        {
            var firstLibraryButton = GetLibraryGameButtons()
                .Select(candidate => candidate.Control)
                .FirstOrDefault();
            TryFocus(firstLibraryButton ?? FindFocusableControl(MyGamesOverlay));
            return;
        }

        if (_viewModel.IsLauncherSettingsOpen)
        {
            TryFocus(FindFocusableControl(LauncherSettingsOverlay));
            return;
        }

        if (_viewModel.IsProfileEditorOpen)
        {
            TryFocus(FindFocusableControl(ProfileEditorOverlay));
            return;
        }

        if (_viewModel.IsMusicPlayerOpen)
        {
            TryFocus(FindFocusableControl(MusicPlayerOverlay));
            return;
        }

        var buttons = GetDashboardFocusCandidates();

        if (buttons.Count > 0)
        {
            FocusDefaultButton(buttons);
            return;
        }

        TryFocus(FindVisualChild<Button>(ContentHost));
    }

    private bool TryMoveDashboardFocus(DashboardInputAction action)
    {
        if (Keyboard.FocusedElement is TextBox)
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

        if (_viewModel.IsMyGamesOpen)
        {
            return TryMoveMyGamesFocus(action);
        }

        if (_viewModel.IsLauncherSettingsOpen)
        {
            return SafeMoveFocus(action);
        }

        if (_viewModel.IsProfileEditorOpen)
        {
            return SafeMoveFocus(action);
        }

        var buttons = GetDashboardFocusCandidates();

        if (buttons.Count == 0)
        {
            return SafeMoveFocus(action);
        }

        if (Keyboard.FocusedElement is not Button currentButton || !buttons.Any(candidate => ReferenceEquals(candidate.Button, currentButton)))
        {
            return FocusDefaultButton(buttons);
        }

        var current = buttons.First(candidate => ReferenceEquals(candidate.Button, currentButton));
        var currentCenter = GetCenter(current.Bounds);
        var direction = action switch
        {
            DashboardInputAction.MoveLeft => new Vector(-1, 0),
            DashboardInputAction.MoveRight => new Vector(1, 0),
            DashboardInputAction.MoveUp => new Vector(0, -1),
            DashboardInputAction.MoveDown => new Vector(0, 1),
            _ => new Vector(0, 0)
        };

        var next = buttons
            .Where(candidate => !ReferenceEquals(candidate.Button, currentButton))
            .Select(candidate => new
            {
                Candidate = candidate,
                Center = GetCenter(candidate.Bounds)
            })
            .Select(item => new
            {
                item.Candidate,
                Delta = item.Center - currentCenter,
                Primary = action is DashboardInputAction.MoveLeft or DashboardInputAction.MoveRight
                    ? Math.Abs(item.Center.X - currentCenter.X)
                    : Math.Abs(item.Center.Y - currentCenter.Y),
                Secondary = action is DashboardInputAction.MoveLeft or DashboardInputAction.MoveRight
                    ? Math.Abs(item.Center.Y - currentCenter.Y)
                    : Math.Abs(item.Center.X - currentCenter.X)
            })
            .Where(item => Vector.Multiply(item.Delta, direction) > 1)
            .OrderBy(item => item.Secondary * 2.4 + item.Primary)
            .ThenBy(item => item.Primary)
            .FirstOrDefault();

        if (next is null)
        {
            RememberFocusedButton();
            return false;
        }

        if (!TryFocus(next.Candidate.Button))
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
            return FindVisualChildren<Button>(ContentHost)
                .Where(button => button.IsVisible && button.IsEnabled && button.Focusable)
                .Select(button => new FocusCandidate(button, GetElementBounds(button, ContentHost)))
                .Where(candidate => candidate.Bounds.Width > 0 && candidate.Bounds.Height > 0)
                .Where(candidate =>
                {
                    var center = GetCenter(candidate.Bounds);
                    return center.X >= FocusZoneLeft && center.X <= FocusZoneRight;
                })
                .ToList();
        }
        catch
        {
            return [];
        }
    }

    private void RememberFocusedButton()
    {
        if (_viewModel.CurrentTab is null || Keyboard.FocusedElement is not Button focusedButton)
        {
            return;
        }

        _lastFocusedButtonByTab[_viewModel.CurrentTab.Key] = focusedButton;
    }

    private bool IsOverlayOpen()
        => _viewModel.IsMyGamesOpen
           || _viewModel.IsLauncherSettingsOpen
           || _viewModel.IsProfileEditorOpen
           || _viewModel.IsMusicPlayerOpen
           || _viewModel.IsSearchOverlayOpen
           || _viewModel.IsDetailsOpen
           || _viewModel.IsQuickMenuOpen;

    private bool TryRestoreOverlayFocus()
    {
        var overlay = GetActiveOverlay();
        if (overlay is null || IsFocusInside(overlay))
        {
            return false;
        }

        FocusFirstButton();
        return true;
    }

    private DependencyObject? GetActiveOverlay()
    {
        if (_viewModel.IsMyGamesOpen)
        {
            return MyGamesOverlay;
        }

        if (_viewModel.IsLauncherSettingsOpen)
        {
            return LauncherSettingsOverlay;
        }

        if (_viewModel.IsProfileEditorOpen)
        {
            return ProfileEditorOverlay;
        }

        if (_viewModel.IsMusicPlayerOpen)
        {
            return MusicPlayerOverlay;
        }

        return null;
    }

    private static bool IsFocusInside(DependencyObject overlay)
    {
        if (Keyboard.FocusedElement is not DependencyObject focused)
        {
            return false;
        }

        for (var current = focused; current is not null; current = VisualTreeHelper.GetParent(current))
        {
            if (ReferenceEquals(current, overlay))
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
        if (_viewModel.CurrentTab is not null
            && _lastFocusedButtonByTab.TryGetValue(_viewModel.CurrentTab.Key, out var remembered)
            && buttons.Any(candidate => ReferenceEquals(candidate.Button, remembered))
            && remembered.IsVisible
            && remembered.IsEnabled)
        {
            return TryFocus(remembered);
        }

        var preferred = buttons
            .OrderBy(candidate => Math.Abs(GetCenter(candidate.Bounds).Y - 210))
            .ThenBy(candidate => Math.Abs(GetCenter(candidate.Bounds).X - 560))
            .FirstOrDefault();

        return TryFocus(preferred.Button);
    }

    private bool TryMoveOverlayFocus(FrameworkElement overlay, DashboardInputAction action)
    {
        var controls = GetOverlayFocusCandidates(overlay);
        if (controls.Count == 0)
        {
            return SafeMoveFocus(action);
        }

        if (Keyboard.FocusedElement is not Control currentControl
            || !controls.Any(candidate => ReferenceEquals(candidate.Control, currentControl)))
        {
            return TryFocus(controls[0].Control);
        }

        var current = controls.First(candidate => ReferenceEquals(candidate.Control, currentControl));
        var currentCenter = GetCenter(current.Bounds);
        var direction = action switch
        {
            DashboardInputAction.MoveLeft => new Vector(-1, 0),
            DashboardInputAction.MoveRight => new Vector(1, 0),
            DashboardInputAction.MoveUp => new Vector(0, -1),
            DashboardInputAction.MoveDown => new Vector(0, 1),
            _ => new Vector(0, 0)
        };

        var next = controls
            .Where(candidate => !ReferenceEquals(candidate.Control, currentControl))
            .Select(candidate => new
            {
                Candidate = candidate,
                Center = GetCenter(candidate.Bounds)
            })
            .Select(item => new
            {
                item.Candidate,
                Delta = item.Center - currentCenter,
                Primary = action is DashboardInputAction.MoveLeft or DashboardInputAction.MoveRight
                    ? Math.Abs(item.Center.X - currentCenter.X)
                    : Math.Abs(item.Center.Y - currentCenter.Y),
                Secondary = action is DashboardInputAction.MoveLeft or DashboardInputAction.MoveRight
                    ? Math.Abs(item.Center.Y - currentCenter.Y)
                    : Math.Abs(item.Center.X - currentCenter.X)
            })
            .Where(item => Vector.Multiply(item.Delta, direction) > 1)
            .OrderBy(item => item.Secondary * 2.2 + item.Primary)
            .ThenBy(item => item.Primary)
            .FirstOrDefault();

        if (next is null)
        {
            return SafeMoveFocus(action);
        }

        return TryFocus(next.Candidate.Control);
    }

    private bool TryMoveMyGamesFocus(DashboardInputAction action)
    {
        if (action is not (DashboardInputAction.MoveLeft or DashboardInputAction.MoveRight))
        {
            return true;
        }

        var gameButtons = GetLibraryGameButtons();

        if (gameButtons.Count == 0)
        {
            return true;
        }

        if (Keyboard.FocusedElement is not Button focusedButton
            || !gameButtons.Any(candidate => ReferenceEquals(candidate.Control, focusedButton)))
        {
            return TryFocus(gameButtons[0].Control);
        }

        var index = gameButtons.FindIndex(candidate => ReferenceEquals(candidate.Control, focusedButton));
        var nextIndex = action == DashboardInputAction.MoveRight
            ? Math.Min(index + 1, gameButtons.Count - 1)
            : Math.Max(index - 1, 0);

        return TryFocus(gameButtons[nextIndex].Control);
    }

    private List<OverlayFocusCandidate> GetLibraryGameButtons()
        => FindVisualChildren<Button>(MyGamesOverlay)
            .Where(button => button.IsVisible
                             && button.IsEnabled
                             && button.Focusable
                             && button.CommandParameter is GameCardViewModel)
            .Select(button => new OverlayFocusCandidate(button, GetElementBounds(button, MyGamesOverlay)))
            .Where(candidate => candidate.Bounds.Width > 0 && candidate.Bounds.Height > 0)
            .OrderBy(candidate => candidate.Bounds.Left)
            .ToList();

    private static List<OverlayFocusCandidate> GetOverlayFocusCandidates(FrameworkElement overlay)
    {
        try
        {
            return FindVisualChildren<Control>(overlay)
                .Where(control => control.IsVisible && control.IsEnabled && control.Focusable)
                .Select(control => new OverlayFocusCandidate(control, GetElementBounds(control, overlay)))
                .Where(candidate => candidate.Bounds.Width > 0 && candidate.Bounds.Height > 0)
                .Where(candidate =>
                {
                    var center = GetCenter(candidate.Bounds);
                    return center.X >= 0
                           && center.Y >= 0
                           && center.X <= overlay.ActualWidth
                           && center.Y <= overlay.ActualHeight;
                })
                .ToList();
        }
        catch
        {
            return [];
        }
    }

    private static Rect GetElementBounds(FrameworkElement element, Visual relativeTo)
    {
        try
        {
            var transform = element.TransformToAncestor(relativeTo);
            return transform.TransformBounds(new Rect(0, 0, element.ActualWidth, element.ActualHeight));
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
        => new(rect.Left + rect.Width / 2, rect.Top + rect.Height / 2);

    private bool TryFocus(UIElement? element)
    {
        if (element is null || !element.IsVisible || !element.Focusable)
        {
            return false;
        }

        if (element is Control { IsEnabled: false })
        {
            return false;
        }

        try
        {
            var focused = element.Focus();
            if (focused && element is Button { CommandParameter: GameCardViewModel game })
            {
                _viewModel.SelectGame(game);
            }

            return focused;
        }
        catch
        {
            return false;
        }
    }

    private static UIElement? FindFocusableControl(DependencyObject? root)
        => FindVisualChildren<UIElement>(root)
            .FirstOrDefault(element => element is Control { IsVisible: true, IsEnabled: true, Focusable: true });

    private static T? FindVisualChild<T>(DependencyObject? root)
        where T : DependencyObject
    {
        if (root is null)
        {
            return null;
        }

        int childCount;
        try
        {
            childCount = VisualTreeHelper.GetChildrenCount(root);
        }
        catch
        {
            return null;
        }

        for (var i = 0; i < childCount; i++)
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

            if (child is T typed)
            {
                return typed;
            }

            var result = FindVisualChild<T>(child);
            if (result is not null)
            {
                return result;
            }
        }

        return null;
    }

    private static IEnumerable<T> FindVisualChildren<T>(DependencyObject? root)
        where T : DependencyObject
    {
        if (root is null)
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

        for (var i = 0; i < childCount; i++)
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

            if (child is T typed)
            {
                yield return typed;
            }

            foreach (var descendant in FindVisualChildren<T>(child))
            {
                yield return descendant;
            }
        }
    }

    private readonly record struct FocusCandidate(Button Button, Rect Bounds);
    private readonly record struct OverlayFocusCandidate(Control Control, Rect Bounds);
}
