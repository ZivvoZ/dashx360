namespace XboxMetroLauncher.ViewModels.Tabs;

public sealed class MediaTabViewModel : DashboardTabViewModel
{
    public MediaTabViewModel(DashboardViewModel shell)
        : base(shell, "video", "media")
    {
    }
}
