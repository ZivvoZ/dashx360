using System.Collections.ObjectModel;
using System.Windows.Input;

namespace XboxMetroLauncher.ViewModels.Tabs;

public sealed class BingTabViewModel : DashboardTabViewModel
{
    public BingTabViewModel(DashboardViewModel shell)
        : base(shell, "bing", "bing")
    {
        TrendingSearches =
        [
            "Halo Reach",
            "Forza Horizon",
            "Game Pass PC",
            "Local co-op games",
            "Xbox 360 dashboard"
        ];
    }

    public ObservableCollection<string> TrendingSearches { get; }
    public ICommand SubmitSearchCommand => Shell.SubmitSearchCommand;
    public ICommand UseTrendingSearchCommand => Shell.UseTrendingSearchCommand;
}
