using System.Windows.Media;
using XboxMetroLauncher.Models;

namespace XboxMetroLauncher.ViewModels;

public sealed class GameCardViewModel : ObservableObject
{
    public GameCardViewModel(GameMetadata game, Brush accentBrush)
    {
        Game = game;
        AccentBrush = accentBrush;
    }

    public GameMetadata Game { get; }
    public string Title => Game.Title;
    public string Subtitle => string.IsNullOrWhiteSpace(Game.Genre) ? Game.Platform : $"{Game.Platform} - {Game.Genre}";
    public string CoverArtPath => Game.CoverArtPath;
    public double CoverZoom => Game.CoverZoom;
    public double CoverOffsetX => Game.CoverOffsetX;
    public double CoverOffsetY => Game.CoverOffsetY;
    public string BackgroundArtPath => Game.BackgroundArtPath;
    public bool IsFavorite => Game.IsFavorite;
    public Brush AccentBrush { get; }

    public void Refresh()
    {
        OnPropertyChanged(nameof(Title));
        OnPropertyChanged(nameof(Subtitle));
        OnPropertyChanged(nameof(CoverArtPath));
        OnPropertyChanged(nameof(CoverZoom));
        OnPropertyChanged(nameof(CoverOffsetX));
        OnPropertyChanged(nameof(CoverOffsetY));
        OnPropertyChanged(nameof(BackgroundArtPath));
        OnPropertyChanged(nameof(IsFavorite));
    }
}
