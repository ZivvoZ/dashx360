namespace XboxMetroLauncher.ViewModels;

public sealed class GuideKeyboardKeyItem : ObservableObject
{
    private string _label = string.Empty;
    private bool _isSelected;

    public GuideKeyboardKeyItem(string label, Action action, bool isWide = false)
    {
        _label = label;
        Action = action;
        IsWide = isWide;
    }

    public string Label
    {
        get => _label;
        set => SetProperty(ref _label, value);
    }

    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }

    public bool IsWide { get; }

    public Action Action { get; }
}
