namespace XboxMetroLauncher.ViewModels.Tabs;

public abstract class DashboardTabViewModel : ObservableObject
{
    private bool _isSelected;

    protected DashboardTabViewModel(DashboardViewModel shell, string name, string key)
    {
        Shell = shell;
        Name = name;
        Key = key;
    }

    public DashboardViewModel Shell { get; }
    public string Name { get; }
    public string Key { get; }

    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }
}
