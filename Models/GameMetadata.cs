namespace XboxMetroLauncher.Models;

public sealed class GameMetadata
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Title { get; set; } = string.Empty;
    public string ExecutablePath { get; set; } = string.Empty;
    public string Arguments { get; set; } = string.Empty;
    public string WorkingDirectory { get; set; } = string.Empty;
    public string CoverArtPath { get; set; } = string.Empty;
    public string BackgroundArtPath { get; set; } = string.Empty;
    public double CoverZoom { get; set; } = 1;
    public double CoverOffsetX { get; set; }
    public double CoverOffsetY { get; set; }
    public string Genre { get; set; } = string.Empty;
    public string Platform { get; set; } = "PC";
    public bool IsFavorite { get; set; }
    public DateTimeOffset? LastPlayed { get; set; }
    public TimeSpan Playtime { get; set; }
}
