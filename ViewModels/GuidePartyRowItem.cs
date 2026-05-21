namespace XboxMetroLauncher.ViewModels;

public sealed class GuidePartyRowItem : ObservableObject
{
    private string _rowKind = string.Empty;
    private string _title = string.Empty;
    private string _activityText = string.Empty;
    private string _avatarPath = string.Empty;
    private string _activityIcon = string.Empty;
    private bool _isSelectable;
    private bool _showVoiceIcon;
    private bool _isHost;

    public string RowKind
    {
        get => _rowKind;
        set => SetProperty(ref _rowKind, value);
    }

    public string Title
    {
        get => _title;
        set => SetProperty(ref _title, value);
    }

    public string ActivityText
    {
        get => _activityText;
        set => SetProperty(ref _activityText, value);
    }

    public string AvatarPath
    {
        get => _avatarPath;
        set => SetProperty(ref _avatarPath, value);
    }

    public string ActivityIcon
    {
        get => _activityIcon;
        set => SetProperty(ref _activityIcon, value);
    }

    public bool IsSelectable
    {
        get => _isSelectable;
        set => SetProperty(ref _isSelectable, value);
    }

    public bool ShowVoiceIcon
    {
        get => _showVoiceIcon;
        set => SetProperty(ref _showVoiceIcon, value);
    }

    public bool IsHost
    {
        get => _isHost;
        set => SetProperty(ref _isHost, value);
    }
}
