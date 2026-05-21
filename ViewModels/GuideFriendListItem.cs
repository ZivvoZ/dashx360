namespace XboxMetroLauncher.ViewModels;

public sealed class GuideFriendListItem : ObservableObject
{
    private string _gamertag = string.Empty;
    private string _friendId = string.Empty;
    private string _subtitle = string.Empty;
    private string _status = string.Empty;
    private string _avatarPath = string.Empty;
    private bool _isAddFriend;

    public string Gamertag
    {
        get => _gamertag;
        set => SetProperty(ref _gamertag, value);
    }

    public string FriendId
    {
        get => _friendId;
        set => SetProperty(ref _friendId, value);
    }

    public string Subtitle
    {
        get => _subtitle;
        set => SetProperty(ref _subtitle, value);
    }

    public string Status
    {
        get => _status;
        set => SetProperty(ref _status, value);
    }

    public string AvatarPath
    {
        get => _avatarPath;
        set => SetProperty(ref _avatarPath, value);
    }

    public bool IsAddFriend
    {
        get => _isAddFriend;
        set => SetProperty(ref _isAddFriend, value);
    }
}
