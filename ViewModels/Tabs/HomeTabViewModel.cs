namespace XboxMetroLauncher.ViewModels.Tabs;

public sealed class HomeTabViewModel : DashboardTabViewModel
{
    public HomeTabViewModel(DashboardViewModel shell)
        : base(shell, "home", "home")
    {
    }
}
