using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using XboxMetroLauncher.Input;
using XboxMetroLauncher.ViewModels;

namespace XboxMetroLauncher.Views;

public partial class BladesGuideWindow : Window, IGuideWindow
{
    private readonly GuideViewModel _viewModel;
    private DashboardViewModel Dashboard => _viewModel.Dashboard;
    private string _page = "home";
    private string _musicSource = "hard-drive";
    private string _musicCategory = "saved-playlists";
    private bool _tracks;
    private TextBlock? _musicTitle;
    private bool _closed;
    private bool _openMusic;

    public void OpenMusicFromDashboard()
    {
        if (IsTransitioning) return;
        if (IsVisible) { MusicClick(this,new RoutedEventArgs()); return; }
        _openMusic=true;
        Open();
    }
    public bool IsTransitioning { get; private set; }
    public bool IsGuideOpen => IsVisible && !IsTransitioning;
    public event EventHandler? HiddenCompleted;

    public BladesGuideWindow(GuideViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = viewModel;
        viewModel.PropertyChanged += ViewModelChanged;
        Closed += (_, _) =>
        {
            _closed = true;
            viewModel.PropertyChanged -= ViewModelChanged;
            viewModel.Stop();
        };
        AddHandler(ButtonBase.ClickEvent, new RoutedEventHandler((_, _) => viewModel.PlaySound("guide-select")));
        AddHandler(Keyboard.GotKeyboardFocusEvent, new KeyboardFocusChangedEventHandler((_, e) =>
        {
            if (e.NewFocus is FrameworkElement element && element.ToolTip is string label &&
                (label == "Messages" || label == "Friends" || label == "Recent Players")) CommunityTitle.Text = label;
            else CommunityTitle.Text = "Community";
        }));
    }

    public bool Open(bool resetToHome = true)
    {
        if (IsTransitioning) return false;
        if (IsVisible) { Activate(); return false; }
        _viewModel.Start(resetToHome);
        _page = "home";
        PageTitle.Text = "";
        Home.Visibility = Visibility.Visible;
        Page.Visibility = Visibility.Collapsed;
        Shell.Width = 560;
        DashboardPrompt.Visibility = SignOutPrompt.Visibility = Visibility.Visible;
        Slide.X = -560;
        Home.Opacity = 0;
        Footer.Opacity = 0;
        Dimmer.Opacity = 0;
        WindowState = WindowState.Maximized;
        Show();
        Activate();
        IsTransitioning = true;
        _ = OpenAsync(resetToHome);
        return true;
    }

    private async Task OpenAsync(bool resetToHome)
    {
        try
        {
            await Task.WhenAll(Animate(Slide,TranslateTransform.XProperty,0,300), Animate(Dimmer,OpacityProperty,1,220));
            await Task.WhenAll(Animate(Home,OpacityProperty,1,140), Animate(Footer,OpacityProperty,1,140));
            if (_closed) return;
            IsTransitioning = false;
            if (_openMusic) { _openMusic=false; Dashboard.OpenBladesGuideMusicSources(); _tracks=false; await ChangePage("music","Select Music",RenderMusic); }
            else if (!resetToHome && _viewModel.IsFriendsListScreen) await ChangePage("friends", "Friends", RenderFriends);
            else if (!resetToHome && _viewModel.IsPartyScreen) await ChangePage("party", "Xbox LIVE Party", RenderParty);
            else if (!resetToHome && _viewModel.IsAchievementsScreen) await ChangePage("achievements", "Achievements", RenderAchievements);
            else SelectMusic.Focus();
        }
        catch (Exception ex) { App.LogException(ex,"BladesGuide.Open"); IsTransitioning=false; }
    }

    public bool CloseGuide(bool playSound = false)
    {
        if (!IsVisible || IsTransitioning) return false;
        if (playSound) _viewModel.PlaySound("guide-close");
        IsTransitioning=true;
        _viewModel.Stop();
        _ = CloseAsync();
        return true;
    }
    private async Task CloseAsync()
    {
        try
        {
            await Task.WhenAll(Animate(Slide,TranslateTransform.XProperty,-Shell.Width,240), Animate(Dimmer,OpacityProperty,0,240));
            if (_closed) return;
            Hide();
            HiddenCompleted?.Invoke(this,EventArgs.Empty);
        }
        finally { IsTransitioning=false; }
    }
    private static Task Animate(Animatable target, DependencyProperty property, double value, int ms)
    {
        double from = (double)target.GetValue(property);
        target.BeginAnimation(property,null);
        target.SetValue(property,value);
        var completed = new TaskCompletionSource<bool>();
        var animation = new DoubleAnimation(from,value,TimeSpan.FromMilliseconds(ms))
        { EasingFunction=new CubicEase {EasingMode=EasingMode.EaseOut}, FillBehavior=FillBehavior.Stop };
        animation.Completed += (_,_) => completed.TrySetResult(true);
        target.BeginAnimation(property,animation,HandoffBehavior.SnapshotAndReplace);
        return completed.Task;
    }
    private static Task Animate(UIElement target, DependencyProperty property, double value, int ms)
    {
        double from=(double)target.GetValue(property);
        target.BeginAnimation(property,null);
        target.SetValue(property,value);
        var completed=new TaskCompletionSource<bool>();
        var animation=new DoubleAnimation(from,value,TimeSpan.FromMilliseconds(ms))
        { EasingFunction=new CubicEase {EasingMode=EasingMode.EaseOut}, FillBehavior=FillBehavior.Stop };
        animation.Completed += (_,_) => completed.TrySetResult(true);
        target.BeginAnimation(property,animation,HandoffBehavior.SnapshotAndReplace);
        return completed.Task;
    }

    private async Task ChangePage(string page, string title, Action populate)
    {
        if (IsTransitioning) return;
        IsTransitioning=true;
        try
        {
            var outgoing=_page=="home" ? (UIElement)Home : Page;
            await Animate(outgoing,OpacityProperty,0,90);
            _page=page;
            PageTitle.Text=title;
            Home.Visibility=page=="home" ? Visibility.Visible : Visibility.Collapsed;
            Page.Visibility=page=="home" ? Visibility.Collapsed : Visibility.Visible;
            DashboardPrompt.Visibility=SignOutPrompt.Visibility=page=="home" ? Visibility.Visible : Visibility.Collapsed;
            populate();
            await Animate(Shell,WidthProperty,page=="home"?560:924,330);
            await Animate(page=="home" ? (UIElement)Home : Page,OpacityProperty,1,130);
            if (_closed) return;
            if (page=="home") SelectMusic.Focus();
            else PageMenu.Children.OfType<Button>().FirstOrDefault()?.Focus();
        }
        finally { IsTransitioning=false; }
    }
    private Button Row(string text, Action click, double height=42)
    {
        var label=new TextBlock {Text=text,TextTrimming=TextTrimming.CharacterEllipsis};
        var button=new Button {Content=label,Style=(Style)FindResource("GuideRow"),Height=height};
        button.Click += (_,_) => { if (!IsTransitioning) click(); };
        return button;
    }
    private void ClearPage() { PageMenu.Children.Clear(); PageDetails.Children.Clear(); }
    private async void PersonalSettingsClick(object sender, RoutedEventArgs e)
        => await ChangePage("personal", "Personal Settings", RenderPersonalSettings);

    private void RenderPersonalSettings()
    {
        ClearPage();
        foreach (var title in new[] { "View Games", "Edit Profile", "View Rep", "Game Defaults", "Account Management", "Auto Sign-In" })
            PageMenu.Children.Add(Row(title, () =>
            {
                if (title != "Edit Profile") return;
                Dashboard.CancelBladesProfileEdit();
                Execute(Dashboard.ToggleProfileEditCommand);
                _ = ChangePage("edit-profile", "Edit Profile", RenderProfileEditor);
            }));
    }

    private void RenderProfileEditor()
    {
        ClearPage();
        foreach (var field in new[] { ("Gamertag", "EditableProfileGamertag"), ("Gamerscore", "EditableProfileGamerscore"), ("Motto", "EditableProfileMotto"), ("Name", "EditableProfileName"), ("Location", "EditableProfileLocation"), ("Bio", "EditableProfileDescription") })
        {
            PageDetails.Children.Add(new TextBlock { Text=field.Item1, FontSize=18, Margin=new Thickness(0,6,0,2) });
            var editor=new TextBox { FontSize=20, Padding=new Thickness(5), Background=new SolidColorBrush(Color.FromArgb(180,255,255,230)), Foreground=Brushes.Black };
            editor.SetBinding(TextBox.TextProperty,new Binding(field.Item2) { Source=Dashboard, Mode=BindingMode.TwoWay, UpdateSourceTrigger=UpdateSourceTrigger.PropertyChanged });
            PageDetails.Children.Add(editor);
        }
        PageMenu.Children.Add(Row("Save", async () =>
        {
            await Dashboard.SaveBladesProfileEditAsync();
            await ChangePage("personal", "Personal Settings", RenderPersonalSettings);
        }));
        PageMenu.Children.Add(Row("Cancel", () => { Dashboard.CancelBladesProfileEdit(); _ = ChangePage("personal", "Personal Settings", RenderPersonalSettings); }));
        PageMenu.Children.Add(Row("Gamer Picture", () => Execute(Dashboard.ChooseProfilePictureCommand)));
    }
    private void Detail(string text) => PageDetails.Children.Add(new TextBlock {Text=text, TextWrapping=TextWrapping.Wrap, Margin=new Thickness(4,6,4,10),FontSize=22});

    private async void MusicClick(object sender,RoutedEventArgs e)
    {
        if (IsTransitioning) return;
        Dashboard.OpenBladesGuideMusicSources();
        _tracks=false;
        await ChangePage("music","Select Music",RenderMusic);
    }
    private void RenderMusic()
    {
        ClearPage();
        PageTitle.Text=Dashboard.IsMusicSourceBrowser ? "Select Music" : _musicSource=="spotify" ? "Spotify" : "Hard Drive";
        if (_tracks)
        {
            PageTitle.Text="Music";
            if (_musicSource!="spotify")
            {
                PageMenu.Children.Add(Row("Saved Playlists",()=> { _musicCategory="saved-playlists"; RestoreCategory(); RenderMusic(); FocusMenu(); }));
                PageMenu.Children.Add(Row("Songs",()=> { _musicCategory="songs"; RestoreCategory(); RenderMusic(); FocusMenu(); }));
            }
            PageMenu.Children.Add(Row("Select Music",()=> { Dashboard.OpenBladesGuideMusicSources(); _tracks=false; RenderMusic(); FocusMenu(); }));
            _musicTitle=new TextBlock {Text=Dashboard.CurrentMusicTrack?.Title ?? "Songs", TextWrapping=TextWrapping.Wrap,FontSize=22,Margin=new Thickness(4,6,4,10)};
            PageDetails.Children.Add(_musicTitle);
            var transport=new UniformGrid {Columns=4,Margin=new Thickness(0,0,0,8)};
            foreach(var control in new (string Glyph,string Label,ICommand Command)[]
            {("\uE768","Play / Pause",Dashboard.PlayPauseMusicCommand),("\uE892","Previous",Dashboard.PreviousMusicCommand),("\uE893","Next",Dashboard.NextMusicCommand),("\uE71A","Stop",Dashboard.StopMusicCommand)})
                transport.Children.Add(new Button {Content=control.Glyph,ToolTip=control.Label,Command=control.Command,FontFamily=new FontFamily("Segoe MDL2 Assets"),Style=(Style)FindResource("GuideTool")});
            PageDetails.Children.Add(transport);
            foreach (var track in Dashboard.MusicTracks)
                PageDetails.Children.Add(Row(track.Title,()=>Execute(Dashboard.PlaySelectedMusicCommand,track),48));
            return;
        }
        foreach (var item in Dashboard.MusicBrowserMenuItems)
        {
            PageMenu.Children.Add(Row(item.Title,()=>
            {
                bool source=Dashboard.IsMusicSourceBrowser;
                if(source) _musicSource=item.Key; else _musicCategory=item.Key;
                Execute(Dashboard.SelectMusicBrowserMenuItemCommand,item);
                _tracks=Dashboard.IsMusicNowPlayingScreen;
                RenderMusic();
                if(source) FocusMenu(); else PageDetails.Children.OfType<Button>().FirstOrDefault()?.Focus();
            }));
        }
        if (Dashboard.IsMusicSourceBrowser)
        {
            Detail("Select Music");
            return;
        }
        foreach (var result in Dashboard.MusicBrowserResultItems)
        {
            PageDetails.Children.Add(Row(result.Title,()=>
            {
                Execute(Dashboard.OpenMusicBrowserResultCommand,result);
                _tracks=Dashboard.IsMusicNowPlayingScreen;
                RenderMusic(); FocusMenu();
            },48));
        }
        if (Dashboard.MusicBrowserResultItems.Count==0) Detail("No music found");
    }
    private void RestoreCategory()
    {
        Dashboard.OpenBladesGuideMusicSources();
        var source=Dashboard.MusicBrowserMenuItems.FirstOrDefault(i=>i.Key==_musicSource);
        if(source!=null) Execute(Dashboard.SelectMusicBrowserMenuItemCommand,source);
        var category=Dashboard.MusicBrowserMenuItems.FirstOrDefault(i=>i.Key==_musicCategory);
        if(category!=null) Execute(Dashboard.SelectMusicBrowserMenuItemCommand,category);
        _tracks=false;
    }
    private void FocusMenu() => PageMenu.Children.OfType<Button>().FirstOrDefault()?.Focus();
    private static void Execute(ICommand command,object? parameter=null) { if(command.CanExecute(parameter)) command.Execute(parameter); }
    private async void FriendsClick(object sender,RoutedEventArgs e)
    {
        if(IsTransitioning) return;
        _viewModel.OpenFriendsOverlayFromDashboard();
        await ChangePage("friends","Friends",RenderFriends);
    }
    private void RenderFriends()
    {
        ClearPage();
        foreach(var friend in _viewModel.FriendsListItems.Where(f=>!f.IsAddFriend))
            PageMenu.Children.Add(Row(friend.Gamertag,()=>
            {
                PageDetails.Children.Clear();
                Detail(friend.Gamertag); Detail(friend.Status); Detail(friend.Subtitle);
            },50));
        if(PageMenu.Children.Count==0) Detail(string.IsNullOrWhiteSpace(_viewModel.StatusText)?"No friends online":_viewModel.StatusText);
        else Detail(_viewModel.FriendsHeaderText);
    }
    private async void MessagesClick(object sender,RoutedEventArgs e) => await ChangePage("messages","Messages",RenderMessages);
    private void RenderMessages()
    {
        ClearPage();
        foreach(var message in _viewModel.BladesMessages)
            PageMenu.Children.Add(Row(message.FromGamertag,()=> {PageDetails.Children.Clear();Detail(message.FromGamertag);Detail(message.Message);},50));
        if(PageMenu.Children.Count==0) Detail("No messages");
    }
    private void RenderParty()
    {
        ClearPage();
        foreach(var row in _viewModel.PartyRows)
            PageMenu.Children.Add(Row(row.Title,()=> { _viewModel.ActivatePartyRowItem(row); RenderParty(); },48));
        Detail(_viewModel.StatusText);
    }
    private void RenderAchievements()
    {
        ClearPage();
        if(_viewModel.IsAchievementGameList)
            foreach(var game in _viewModel.AchievementGameItems)
                PageMenu.Children.Add(Row(game.Title,()=> { _viewModel.ActivateAchievementGameItem(game); RenderAchievements(); },48));
        else
            foreach(var achievement in _viewModel.AchievementItems)
                PageMenu.Children.Add(Row(achievement.Title,()=> { _viewModel.SelectAchievementItem(achievement); PageDetails.Children.Clear();Detail(_viewModel.SelectedAchievementDescription); },48));
        Detail(_viewModel.AchievementsGameTitle);
    }
    private void ViewModelChanged(object? sender,PropertyChangedEventArgs e)
    {
        if(!IsVisible || IsTransitioning) return;
        if(_page=="messages" && e.PropertyName=="FriendsMessageCountText") RenderMessages();
        if(_page=="music" && _tracks && _musicTitle!=null) _musicTitle.Text=Dashboard.CurrentMusicTrack?.Title ?? "Songs";
    }
    private void VolumeClick(object sender,RoutedEventArgs e)
    {
        Volume.Visibility=Volume.Visibility==Visibility.Visible ? Visibility.Collapsed : Visibility.Visible;
        if(Volume.IsVisible) Volume.Focus();
    }
    private void DashboardClick(object sender,RoutedEventArgs e) => GoDashboard();
    private void GoDashboard()
    {
        _viewModel.ResetToXboxHome();
        _viewModel.SelectedIndex=0;
        _viewModel.ActivateSelected();
    }
    private async void Back()
    {
        if (_page=="edit-profile") { Dashboard.CancelBladesProfileEdit(); await ChangePage("personal","Personal Settings",RenderPersonalSettings); return; }
        if(Volume.IsVisible) {Volume.Visibility=Visibility.Collapsed;SelectMusic.Focus();return;}
        if(_page=="music" && !Dashboard.IsMusicSourceBrowser)
        {
            if(_tracks) RestoreCategory();
            else Dashboard.OpenBladesGuideMusicSources();
            RenderMusic(); FocusMenu(); return;
        }
        if(_page!="home")
        {
            _viewModel.ResetToXboxHome();
            await ChangePage("home","",()=>{});
        }
        else CloseGuide(true);
    }
    public bool HandleInput(DashboardInputAction action)
    {
        if(IsTransitioning) return true;
        switch(action)
        {
            case DashboardInputAction.Back: Back(); break;
            case DashboardInputAction.Guide: CloseGuide(true); break;
            case DashboardInputAction.Activate:
                if(Keyboard.FocusedElement is Button b && b.IsEnabled)
                {
                    if (b.Command != null) Execute(b.Command,b.CommandParameter);
                    b.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
                }
                break;
            case DashboardInputAction.Details: if(_page=="home") _viewModel.HandleFooterX(); break;
            case DashboardInputAction.Search: if(_page=="home") { _viewModel.MinimizeDashboard(); CloseGuide(); } break;
            case DashboardInputAction.MoveUp: MoveFocus(FocusNavigationDirection.Up); break;
            case DashboardInputAction.MoveDown: MoveFocus(FocusNavigationDirection.Down); break;
            case DashboardInputAction.MoveLeft:
                if(Volume.IsKeyboardFocusWithin) Volume.Value=Math.Max(0,Volume.Value-.05);
                else MoveFocus(FocusNavigationDirection.Left); break;
            case DashboardInputAction.MoveRight:
                if(Volume.IsKeyboardFocusWithin) Volume.Value=Math.Min(1,Volume.Value+.05);
                else MoveFocus(FocusNavigationDirection.Right); break;
        }
        return true;
    }
    private void MoveFocus(FocusNavigationDirection direction)
    {
        if(Keyboard.FocusedElement is UIElement element) element.MoveFocus(new TraversalRequest(direction));
        else if(_page=="home") SelectMusic.Focus(); else FocusMenu();
        if (Keyboard.FocusedElement is FrameworkElement focused) focused.BringIntoView();
        _viewModel.PlaySound("guide-hover");
    }
    private void OnKeyDown(object sender,KeyEventArgs e)
    {
        if (Keyboard.FocusedElement is TextBox && e.Key != Key.Escape) return;
        DashboardInputAction? action=e.Key switch
        {
            Key.Escape or Key.Back=>DashboardInputAction.Back,
            Key.Up=>DashboardInputAction.MoveUp, Key.Down=>DashboardInputAction.MoveDown,
            Key.Left=>DashboardInputAction.MoveLeft, Key.Right=>DashboardInputAction.MoveRight,
            Key.Enter or Key.Space=>DashboardInputAction.Activate,
            Key.X=>DashboardInputAction.Details, Key.Y=>DashboardInputAction.Search,
            _=>null
        };
        if(action.HasValue) {HandleInput(action.Value);e.Handled=true;}
    }
}
