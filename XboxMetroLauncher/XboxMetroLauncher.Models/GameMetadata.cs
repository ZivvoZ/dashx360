using System;

namespace XboxMetroLauncher.Models;

public sealed class GameMetadata
{
	public string Id { get; set; } = Guid.NewGuid().ToString("N");

	public string Title { get; set; } = string.Empty;

	public string LaunchType { get; set; } = "Exe";

	public string ExecutablePath { get; set; } = string.Empty;

	public string SteamAppId { get; set; } = string.Empty;

	public string InstallPath { get; set; } = string.Empty;

	public string LaunchCommand { get; set; } = string.Empty;

	public string Arguments { get; set; } = string.Empty;

	public string WorkingDirectory { get; set; } = string.Empty;

	public string CoverArtPath { get; set; } = string.Empty;

	public string HeaderImagePath { get; set; } = string.Empty;

	public string StoreScreenshotPath { get; set; } = string.Empty;

	public string BackgroundArtPath { get; set; } = string.Empty;

	public string LogoImagePath { get; set; } = string.Empty;

	public double CoverZoom { get; set; } = 1.0;

	public double CoverOffsetX { get; set; }

	public double CoverOffsetY { get; set; }

	public string Genre { get; set; } = string.Empty;

	public string Rating { get; set; } = string.Empty;

	public string MultiplayerInfo { get; set; } = string.Empty;

	public string CoOpInfo { get; set; } = string.Empty;

	public string StoreDescription { get; set; } = string.Empty;

	public double ReviewStarRating { get; set; }

	public int ReviewCount { get; set; }

	public string Platform { get; set; } = "PC";

	public bool IsFavorite { get; set; }

	public DateTimeOffset? LastPlayed { get; set; }

	public TimeSpan Playtime { get; set; }
}
