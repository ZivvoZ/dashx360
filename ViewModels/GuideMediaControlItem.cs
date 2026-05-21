namespace XboxMetroLauncher.ViewModels;

public sealed class GuideMediaControlItem : ObservableObject
{
    private string _title = string.Empty;
    private string _icon = string.Empty;
    private bool _isSelected;

    public GuideMediaControlItem(string title, string icon, Action action)
    {
        _title = title;
        _icon = icon;
        Action = action;
    }

    public string Title
    {
        get => _title;
        set => SetProperty(ref _title, value);
    }

    public string Icon
    {
        get => _icon;
        set => SetProperty(ref _icon, value);
    }

    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }

    public Action Action { get; }
}
