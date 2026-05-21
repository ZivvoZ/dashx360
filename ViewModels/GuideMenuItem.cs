namespace XboxMetroLauncher.ViewModels;

public sealed class GuideMenuItem : ObservableObject
{
    private string _title = string.Empty;
    private string _count = string.Empty;
    private string _icon = string.Empty;

    public GuideMenuItem(string title, string icon, Action action, string count = "", bool isNoOp = false)
    {
        _title = title;
        _icon = icon;
        Count = count;
        Action = action;
        IsNoOp = isNoOp;
    }

    public string Title
    {
        get => _title;
        set => SetProperty(ref _title, value);
    }

    public string Count
    {
        get => _count;
        set => SetProperty(ref _count, value);
    }

    public string Icon
    {
        get => _icon;
        set => SetProperty(ref _icon, value);
    }

    public bool IsNoOp { get; }

    public Action Action { get; }
}
