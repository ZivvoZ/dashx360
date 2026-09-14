using System.Collections.Generic;

namespace XboxMetroLauncher.Models;

public sealed class DashboardBackupSettings
{
	public bool StartFullscreen { get; set; }

	public bool PlayUiSounds { get; set; }

	public bool EnableControllerInput { get; set; }

	public bool LaunchOnWindowsStartup { get; set; }

	public bool MinimizeOnGameLaunch { get; set; } = true;

	public bool EnableFakeLoading { get; set; } = true;

	public string ThemeName { get; set; } = string.Empty;

	public string BingSearchBaseUrl { get; set; } = string.Empty;

	public string DisplayResolution { get; set; } = "16:9";

	public string DashboardStyle { get; set; } = "Metro";
	public string XboxGuide { get; set; } = "Metro";

	public string DashboardSounds { get; set; } = "Metro";

	public string DashboardStartup { get; set; } = "Metro";

	public string OpenTrayGameId { get; set; } = string.Empty;

	public string GameCoverFitMode { get; set; } = "Auto";

	public string DefaultAddDestination { get; set; } = "My Games";

	public string AudioOutputDeviceName { get; set; } = "Default";

	public string DashboardTileColor { get; set; } = "#FF028D02";

	public Dictionary<string, DashboardTileCustomization> DashboardTileCustomizations { get; set; } = new Dictionary<string, DashboardTileCustomization>();

	public List<DashboardBackupTileImage> DashboardTileImages { get; set; } = new List<DashboardBackupTileImage>();
}
