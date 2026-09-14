using System;
using System.Collections.Generic;

namespace XboxMetroLauncher.Models;

public sealed class SteamGameDetails
{
	public TimeSpan? Playtime { get; set; }

	public string Genre { get; set; } = string.Empty;

	public string Rating { get; set; } = string.Empty;

	public string MultiplayerInfo { get; set; } = string.Empty;

	public string CoOpInfo { get; set; } = string.Empty;

	public string StoreDescription { get; set; } = string.Empty;

	public string StoreScreenshotPath { get; set; } = string.Empty;

	public double ReviewStarRating { get; set; }

	public int ReviewCount { get; set; }

	public IReadOnlyList<SteamGameDlc> Dlc { get; set; } = Array.Empty<SteamGameDlc>();
}
