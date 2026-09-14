using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using XboxMetroLauncher.Models;
using XboxMetroLauncher.Utilities;

namespace XboxMetroLauncher.Services;

public sealed class ImportExportService : IImportExportService
{
	private const string BackupFilePrefix = "DashX360_Backup";

	private static readonly JsonSerializerOptions SerializerOptions = new JsonSerializerOptions
	{
		WriteIndented = true,
		PropertyNameCaseInsensitive = true,
		AllowTrailingCommas = true,
		ReadCommentHandling = JsonCommentHandling.Skip
	};

	private readonly IGameLibraryService _libraryService;

	private readonly IProfileService _profileService;

	private readonly ISettingsService _settingsService;

	private readonly string _dataRoot;

	private readonly string _themesRoot;

	public ImportExportService(IGameLibraryService libraryService, IProfileService profileService, ISettingsService settingsService, string dataRoot)
	{
		_libraryService = libraryService;
		_profileService = profileService;
		_settingsService = settingsService;
		_dataRoot = dataRoot;
		_themesRoot = AppPaths.FindFolder(Path.Combine("Assets", "Custom Files", "Themes"));
		Directory.CreateDirectory(_themesRoot);
	}

	public async Task ExportAsync(GameLibrary library, Profile profile, AppSettings settings, string filePath, CancellationToken cancellationToken = default(CancellationToken))
	{
		DashboardBackup dashboardBackup = new DashboardBackup();
		DashboardBackup dashboardBackup2 = dashboardBackup;
		dashboardBackup2.Settings = await BuildSettingsBackupAsync(settings, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
		DashboardBackup dashboardBackup3 = dashboardBackup;
		dashboardBackup3.Profile = await BuildProfileBackupAsync(profile, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
		dashboardBackup.Library = CloneLibrary(library);
		DashboardBackup dashboardBackup4 = dashboardBackup;
		dashboardBackup4.CustomThemes = await BuildThemesBackupAsync(cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
		await WriteBackupAsync(dashboardBackup, filePath, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
	}

	public async Task<DashboardImportResult> ImportAsync(string filePath, CancellationToken cancellationToken = default(CancellationToken))
	{
		_ = 10;
		ImportedDashboardData importedData;
		try
		{
			importedData = await ReadAndValidateBackupAsync(filePath, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
		}
		catch (JsonException)
		{
			return new DashboardImportResult
			{
				Success = false,
				Message = "The selected backup file is not valid JSON."
			};
		}
		catch (InvalidDataException ex)
		{
			return new DashboardImportResult
			{
				Success = false,
				Message = ex.Message
			};
		}
		try
		{
			DashboardBackup backup = importedData.Backup;
			GameLibrary currentLibrary = await _libraryService.LoadAsync(cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
			Profile currentProfile = await _profileService.LoadAsync(cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
			AppSettings currentSettings = await _settingsService.LoadAsync(cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
			string safetyBackupPath = await CreateSafetyBackupAsync(currentLibrary, currentProfile, currentSettings, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
			AppSettings updatedSettings = importedData.IncludesSettings ? await MergeSettingsAsync(currentSettings, backup.Settings, cancellationToken).ConfigureAwait(continueOnCapturedContext: false) : currentSettings;
			Profile updatedProfile = importedData.IncludesProfile ? await MergeProfileAsync(currentProfile, backup.Profile, cancellationToken).ConfigureAwait(continueOnCapturedContext: false) : currentProfile;
			GameLibrary updatedLibrary = importedData.IncludesLibrary ? NormalizeLibrary(backup.Library) : currentLibrary;
			if (importedData.IncludesThemes)
			{
				await RestoreThemesAsync(backup.CustomThemes, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
			}
			await _settingsService.SaveAsync(updatedSettings, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
			await _profileService.SaveAsync(updatedProfile, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
			await _libraryService.SaveAsync(updatedLibrary, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
			return new DashboardImportResult
			{
				Success = true,
				Message = "Dashboard data imported successfully.",
				SafetyBackupPath = safetyBackupPath
			};
		}
		catch (JsonException ex2)
		{
			App.LogException(ex2, "ImportExportService.ImportAsync");
			return new DashboardImportResult
			{
				Success = false,
				Message = "Import failed while reading existing dashboard data: " + ex2.Message
			};
		}
		catch (InvalidDataException ex3)
		{
			return new DashboardImportResult
			{
				Success = false,
				Message = ex3.Message
			};
		}
		catch (Exception ex4)
		{
			App.LogException(ex4, "ImportExportService.ImportAsync");
			return new DashboardImportResult
			{
				Success = false,
				Message = "Import failed: " + ex4.Message
			};
		}
	}

	private async Task<string> CreateSafetyBackupAsync(GameLibrary library, Profile profile, AppSettings settings, CancellationToken cancellationToken)
	{
		string text = Path.Combine(_dataRoot, "Backups");
		Directory.CreateDirectory(text);
		string path = $"{"DashX360_Backup"}_PreImport_{DateTime.Now:yyyyMMdd_HHmmss}.json";
		string backupPath = Path.Combine(text, path);
		await ExportAsync(library, profile, settings, backupPath, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
		return backupPath;
	}

	private static async Task<DashboardBackupSettings> BuildSettingsBackupAsync(AppSettings settings, CancellationToken cancellationToken)
	{
		DashboardBackupSettings backup = new DashboardBackupSettings
		{
			StartFullscreen = settings.StartFullscreen,
			PlayUiSounds = settings.PlayUiSounds,
			EnableControllerInput = settings.EnableControllerInput,
			LaunchOnWindowsStartup = settings.LaunchOnWindowsStartup,
			MinimizeOnGameLaunch = settings.MinimizeOnGameLaunch,
			EnableFakeLoading = settings.EnableFakeLoading,
			ThemeName = settings.ThemeName,
			BingSearchBaseUrl = settings.BingSearchBaseUrl,
			DisplayResolution = settings.DisplayResolution,
			DashboardStyle = settings.DashboardStyle,
			XboxGuide = settings.XboxGuide,
			DashboardSounds = settings.DashboardSounds,
			DashboardStartup = settings.DashboardStartup,
			OpenTrayGameId = settings.OpenTrayGameId,
			GameCoverFitMode = settings.GameCoverFitMode,
			DefaultAddDestination = settings.DefaultAddDestination,
			AudioOutputDeviceName = settings.AudioOutputDeviceName,
			DashboardTileColor = settings.DashboardTileColor,
			DashboardTileCustomizations = settings.DashboardTileCustomizations.ToDictionary<KeyValuePair<string, DashboardTileCustomization>, string, DashboardTileCustomization>((KeyValuePair<string, DashboardTileCustomization> keyValuePair) => keyValuePair.Key, (KeyValuePair<string, DashboardTileCustomization> keyValuePair) => new DashboardTileCustomization
			{
				ImagePath = keyValuePair.Value.ImagePath,
				TitleOverride = keyValuePair.Value.TitleOverride,
				SecondaryTitleOverride = keyValuePair.Value.SecondaryTitleOverride,
				LaunchExecutablePath = keyValuePair.Value.LaunchExecutablePath,
				LaunchWebAddress = keyValuePair.Value.LaunchWebAddress,
				Zoom = keyValuePair.Value.Zoom,
				OffsetX = keyValuePair.Value.OffsetX,
				OffsetY = keyValuePair.Value.OffsetY
			}, StringComparer.OrdinalIgnoreCase)
		};
		foreach (KeyValuePair<string, DashboardTileCustomization> pair in settings.DashboardTileCustomizations)
		{
			cancellationToken.ThrowIfCancellationRequested();
			string imagePath = pair.Value.ImagePath;
			if (!string.IsNullOrWhiteSpace(imagePath) && File.Exists(imagePath))
			{
				byte[] inArray = await File.ReadAllBytesAsync(imagePath, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
				backup.DashboardTileImages.Add(new DashboardBackupTileImage
				{
					TileKey = pair.Key,
					FileName = Path.GetFileName(imagePath),
					ImageBase64 = Convert.ToBase64String(inArray)
				});
			}
		}
		return backup;
	}

	private async Task<AppSettings> MergeSettingsAsync(AppSettings current, DashboardBackupSettings imported, CancellationToken cancellationToken)
	{
		current.StartFullscreen = imported.StartFullscreen;
		current.PlayUiSounds = imported.PlayUiSounds;
		current.EnableControllerInput = imported.EnableControllerInput;
		current.LaunchOnWindowsStartup = imported.LaunchOnWindowsStartup;
		current.MinimizeOnGameLaunch = imported.MinimizeOnGameLaunch;
		current.EnableFakeLoading = imported.EnableFakeLoading;
		current.ThemeName = (string.IsNullOrWhiteSpace(imported.ThemeName) ? current.ThemeName : imported.ThemeName);
		current.BingSearchBaseUrl = (string.IsNullOrWhiteSpace(imported.BingSearchBaseUrl) ? current.BingSearchBaseUrl : imported.BingSearchBaseUrl);
		current.DisplayResolution = (string.IsNullOrWhiteSpace(imported.DisplayResolution) ? current.DisplayResolution : imported.DisplayResolution);
		current.DashboardStyle = (string.IsNullOrWhiteSpace(imported.DashboardStyle) ? current.DashboardStyle : imported.DashboardStyle);
		current.XboxGuide = imported.XboxGuide;
		current.DashboardSounds = (string.IsNullOrWhiteSpace(imported.DashboardSounds) ? current.DashboardSounds : imported.DashboardSounds);
		current.DashboardStartup = (string.IsNullOrWhiteSpace(imported.DashboardStartup) ? current.DashboardStartup : imported.DashboardStartup);
		current.OpenTrayGameId = imported.OpenTrayGameId ?? string.Empty;
		current.GameCoverFitMode = (string.IsNullOrWhiteSpace(imported.GameCoverFitMode) ? current.GameCoverFitMode : imported.GameCoverFitMode);
		current.DefaultAddDestination = (string.IsNullOrWhiteSpace(imported.DefaultAddDestination) ? current.DefaultAddDestination : imported.DefaultAddDestination);
		current.AudioOutputDeviceName = (string.IsNullOrWhiteSpace(imported.AudioOutputDeviceName) ? current.AudioOutputDeviceName : imported.AudioOutputDeviceName);
		current.DashboardTileColor = (string.IsNullOrWhiteSpace(imported.DashboardTileColor) ? current.DashboardTileColor : imported.DashboardTileColor);
		current.DashboardTileCustomizations = await RestoreDashboardTileCustomizationsAsync(imported.DashboardTileCustomizations, imported.DashboardTileImages, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
		return current;
	}

	private async Task<Dictionary<string, DashboardTileCustomization>> RestoreDashboardTileCustomizationsAsync(Dictionary<string, DashboardTileCustomization>? importedCustomizations, IEnumerable<DashboardBackupTileImage>? importedImages, CancellationToken cancellationToken)
	{
		Dictionary<string, DashboardTileCustomization> restored = new Dictionary<string, DashboardTileCustomization>(StringComparer.OrdinalIgnoreCase);
		if (importedCustomizations == null)
		{
			return restored;
		}
		foreach (KeyValuePair<string, DashboardTileCustomization> importedCustomization in importedCustomizations)
		{
			restored[importedCustomization.Key] = new DashboardTileCustomization
			{
				ImagePath = (importedCustomization.Value.ImagePath ?? string.Empty),
				TitleOverride = (importedCustomization.Value.TitleOverride ?? string.Empty),
				SecondaryTitleOverride = (importedCustomization.Value.SecondaryTitleOverride ?? string.Empty),
				LaunchExecutablePath = (importedCustomization.Value.LaunchExecutablePath ?? string.Empty),
				LaunchWebAddress = (importedCustomization.Value.LaunchWebAddress ?? string.Empty),
				Zoom = ((importedCustomization.Value.Zoom <= 0.0) ? 1.0 : importedCustomization.Value.Zoom),
				OffsetX = importedCustomization.Value.OffsetX,
				OffsetY = importedCustomization.Value.OffsetY
			};
		}
		if (importedImages == null)
		{
			return restored;
		}
		string tileImageFolder = Path.Combine(_dataRoot, "ImportedAssets", "DashboardTiles");
		Directory.CreateDirectory(tileImageFolder);
		foreach (DashboardBackupTileImage importedImage in importedImages)
		{
			cancellationToken.ThrowIfCancellationRequested();
			if (!string.IsNullOrWhiteSpace(importedImage.TileKey) && !string.IsNullOrWhiteSpace(importedImage.ImageBase64) && restored.TryGetValue(importedImage.TileKey, out DashboardTileCustomization customization))
			{
				string text = (string.IsNullOrWhiteSpace(importedImage.FileName) ? (MakeSafeFileName(importedImage.TileKey) + ".png") : MakeSafeFileName(importedImage.FileName));
				string destination = Path.Combine(tileImageFolder, text);
				if (File.Exists(destination))
				{
					destination = Path.Combine(tileImageFolder, $"{Path.GetFileNameWithoutExtension(text)}-{DateTime.Now:yyyyMMddHHmmss}{Path.GetExtension(text)}");
				}
				byte[] bytes = Convert.FromBase64String(importedImage.ImageBase64);
				await File.WriteAllBytesAsync(destination, bytes, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
				customization.ImagePath = destination;
				customization = null;
			}
		}
		return restored;
	}

	private static async Task<DashboardBackupProfile> BuildProfileBackupAsync(Profile profile, CancellationToken cancellationToken)
	{
		DashboardBackupProfile backup = new DashboardBackupProfile
		{
			Gamertag = profile.Gamertag,
			Name = profile.Name,
			GamerPicturePath = profile.GamerPicturePath,
			Gamerscore = profile.Gamerscore,
			OnlineStatus = profile.OnlineStatus,
			Motto = profile.Motto,
			Location = profile.Location,
			Description = profile.Description
		};
		if (!string.IsNullOrWhiteSpace(profile.GamerPicturePath) && File.Exists(profile.GamerPicturePath))
		{
			backup.GamerPictureFileName = Path.GetFileName(profile.GamerPicturePath);
			backup.GamerPictureBase64 = Convert.ToBase64String(await File.ReadAllBytesAsync(profile.GamerPicturePath, cancellationToken).ConfigureAwait(continueOnCapturedContext: false));
		}
		return backup;
	}

	private async Task<Profile> MergeProfileAsync(Profile current, DashboardBackupProfile imported, CancellationToken cancellationToken)
	{
		current.Gamertag = (string.IsNullOrWhiteSpace(imported.Gamertag) ? current.Gamertag : imported.Gamertag);
		current.Name = string.IsNullOrWhiteSpace(imported.Name) ? current.Name : imported.Name;
		current.Gamerscore = ((imported.Gamerscore > 0) ? imported.Gamerscore : current.Gamerscore);
		current.OnlineStatus = (string.IsNullOrWhiteSpace(imported.OnlineStatus) ? current.OnlineStatus : imported.OnlineStatus);
		current.Motto = imported.Motto ?? string.Empty;
		current.Location = string.IsNullOrWhiteSpace(imported.Location) ? current.Location : imported.Location;
		current.Description = imported.Description ?? string.Empty;
		if (!string.IsNullOrWhiteSpace(imported.GamerPictureBase64))
		{
			current.GamerPicturePath = await RestoreProfilePictureAsync(imported, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
		}
		else if (!string.IsNullOrWhiteSpace(imported.GamerPicturePath) && File.Exists(imported.GamerPicturePath))
		{
			current.GamerPicturePath = imported.GamerPicturePath;
		}
		return current;
	}

	private async Task<string> RestoreProfilePictureAsync(DashboardBackupProfile imported, CancellationToken cancellationToken)
	{
		string text = Path.Combine(_dataRoot, "ImportedAssets", "Profile");
		Directory.CreateDirectory(text);
		string text2 = (string.IsNullOrWhiteSpace(imported.GamerPictureFileName) ? "profile-import.png" : MakeSafeFileName(imported.GamerPictureFileName));
		string destination = Path.Combine(text, text2);
		if (File.Exists(destination))
		{
			destination = Path.Combine(text, $"{Path.GetFileNameWithoutExtension(text2)}-{DateTime.Now:yyyyMMddHHmmss}{Path.GetExtension(text2)}");
		}
		byte[] bytes = Convert.FromBase64String(imported.GamerPictureBase64);
		await File.WriteAllBytesAsync(destination, bytes, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
		return destination;
	}

	private async Task<List<DashboardBackupTheme>> BuildThemesBackupAsync(CancellationToken cancellationToken)
	{
		List<DashboardBackupTheme> themes = new List<DashboardBackupTheme>();
		foreach (string folderPath in Directory.EnumerateDirectories(_themesRoot).OrderBy<string, string>((string result) => result, StringComparer.OrdinalIgnoreCase))
		{
			cancellationToken.ThrowIfCancellationRequested();
			string path = Path.Combine(folderPath, "theme.json");
			DashboardThemeManifest manifest;
			if (File.Exists(path))
			{
				await using FileStream stream = File.OpenRead(path);
				manifest = (await JsonSerializer.DeserializeAsync<DashboardThemeManifest>(stream, SerializerOptions, cancellationToken).ConfigureAwait(continueOnCapturedContext: false)) ?? new DashboardThemeManifest();
			}
			else
			{
				manifest = new DashboardThemeManifest
				{
					Name = Path.GetFileName(folderPath)
				};
			}
			List<DashboardBackupTheme> list = themes;
			DashboardBackupTheme dashboardBackupTheme = new DashboardBackupTheme
			{
				Name = (string.IsNullOrWhiteSpace(manifest.Name) ? Path.GetFileName(folderPath) : manifest.Name),
				FolderName = Path.GetFileName(folderPath),
				HomeImageFileName = (string.IsNullOrWhiteSpace(manifest.HomeImage) ? "home.png" : manifest.HomeImage)
			};
			DashboardBackupTheme dashboardBackupTheme2 = dashboardBackupTheme;
			dashboardBackupTheme2.HomeImageBase64 = await ReadThemeImageBase64Async(folderPath, manifest.HomeImage, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
			dashboardBackupTheme.GamesImageFileName = (string.IsNullOrWhiteSpace(manifest.GamesImage) ? "games.png" : manifest.GamesImage);
			DashboardBackupTheme dashboardBackupTheme3 = dashboardBackupTheme;
			dashboardBackupTheme3.GamesImageBase64 = await ReadThemeImageBase64Async(folderPath, manifest.GamesImage, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
			dashboardBackupTheme.SettingsImageFileName = (string.IsNullOrWhiteSpace(manifest.SettingsImage) ? "settings.png" : manifest.SettingsImage);
			DashboardBackupTheme dashboardBackupTheme4 = dashboardBackupTheme;
			dashboardBackupTheme4.SettingsImageBase64 = await ReadThemeImageBase64Async(folderPath, manifest.SettingsImage, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
			dashboardBackupTheme.AppsImageFileName = (string.IsNullOrWhiteSpace(manifest.AppsImage) ? "apps.png" : manifest.AppsImage);
			DashboardBackupTheme dashboardBackupTheme5 = dashboardBackupTheme;
			dashboardBackupTheme5.AppsImageBase64 = await ReadThemeImageBase64Async(folderPath, manifest.AppsImage, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
			list.Add(dashboardBackupTheme);
			manifest = null;
		}
		return themes;
	}

	private async Task RestoreThemesAsync(IEnumerable<DashboardBackupTheme>? themes, CancellationToken cancellationToken)
	{
		if (themes == null)
		{
			return;
		}
		foreach (DashboardBackupTheme theme in themes)
		{
			cancellationToken.ThrowIfCancellationRequested();
			string text = (string.IsNullOrWhiteSpace(theme.FolderName) ? MakeSafeFolderName(theme.Name) : MakeSafeFolderName(theme.FolderName));
			string folderPath = Path.Combine(_themesRoot, text);
			Directory.CreateDirectory(folderPath);
			DashboardThemeManifest manifest = new DashboardThemeManifest
			{
				Name = (string.IsNullOrWhiteSpace(theme.Name) ? text : theme.Name),
				HomeImage = (string.IsNullOrWhiteSpace(theme.HomeImageFileName) ? "home.png" : MakeSafeFileName(theme.HomeImageFileName)),
				GamesImage = (string.IsNullOrWhiteSpace(theme.GamesImageFileName) ? "games.png" : MakeSafeFileName(theme.GamesImageFileName)),
				SettingsImage = (string.IsNullOrWhiteSpace(theme.SettingsImageFileName) ? "settings.png" : MakeSafeFileName(theme.SettingsImageFileName)),
				AppsImage = (string.IsNullOrWhiteSpace(theme.AppsImageFileName) ? "apps.png" : MakeSafeFileName(theme.AppsImageFileName))
			};
			await WriteThemeImageAsync(folderPath, manifest.HomeImage, theme.HomeImageBase64, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
			await WriteThemeImageAsync(folderPath, manifest.GamesImage, theme.GamesImageBase64, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
			await WriteThemeImageAsync(folderPath, manifest.SettingsImage, theme.SettingsImageBase64, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
			await WriteThemeImageAsync(folderPath, manifest.AppsImage, theme.AppsImageBase64, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
			await using FileStream stream = File.Create(Path.Combine(folderPath, "theme.json"));
			await JsonSerializer.SerializeAsync(stream, manifest, SerializerOptions, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
		}
	}

	private static async Task<string> ReadThemeImageBase64Async(string folderPath, string fileName, CancellationToken cancellationToken)
	{
		if (string.IsNullOrWhiteSpace(fileName))
		{
			return string.Empty;
		}
		string path = Path.Combine(folderPath, fileName);
		if (!File.Exists(path))
		{
			return string.Empty;
		}
		return Convert.ToBase64String(await File.ReadAllBytesAsync(path, cancellationToken).ConfigureAwait(continueOnCapturedContext: false));
	}

	private static async Task WriteThemeImageAsync(string folderPath, string fileName, string base64, CancellationToken cancellationToken)
	{
		string path = Path.Combine(folderPath, fileName);
		if (string.IsNullOrWhiteSpace(base64))
		{
			DeleteIfExists(path);
			return;
		}
		byte[] bytes = Convert.FromBase64String(base64);
		await File.WriteAllBytesAsync(path, bytes, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
	}

	private static GameLibrary CloneLibrary(GameLibrary library)
	{
		return NormalizeLibrary(new GameLibrary
		{
			LibraryPaths = library.LibraryPaths.ToList(),
			Games = library.Games.Select((GameMetadata game) => new GameMetadata
			{
				Id = game.Id,
				Title = game.Title,
				LaunchType = game.LaunchType,
				ExecutablePath = game.ExecutablePath,
				SteamAppId = game.SteamAppId,
				InstallPath = game.InstallPath,
				LaunchCommand = game.LaunchCommand,
				Arguments = game.Arguments,
				WorkingDirectory = game.WorkingDirectory,
				CoverArtPath = game.CoverArtPath,
				HeaderImagePath = game.HeaderImagePath,
				StoreScreenshotPath = game.StoreScreenshotPath,
				BackgroundArtPath = game.BackgroundArtPath,
				LogoImagePath = game.LogoImagePath,
				CoverZoom = game.CoverZoom,
				CoverOffsetX = game.CoverOffsetX,
				CoverOffsetY = game.CoverOffsetY,
				Genre = game.Genre,
				Rating = game.Rating,
				MultiplayerInfo = game.MultiplayerInfo,
				CoOpInfo = game.CoOpInfo,
				ReviewStarRating = game.ReviewStarRating,
				ReviewCount = game.ReviewCount,
				Platform = game.Platform,
				IsFavorite = game.IsFavorite,
				LastPlayed = game.LastPlayed,
				Playtime = game.Playtime
			}).ToList()
		});
	}

	private static GameLibrary NormalizeLibrary(GameLibrary? library)
	{
		if (library == null)
		{
			library = new GameLibrary();
		}
		GameLibrary gameLibrary = library;
		if (gameLibrary.LibraryPaths == null)
		{
			List<string> list = (gameLibrary.LibraryPaths = new List<string>());
		}
		gameLibrary = library;
		if (gameLibrary.Games == null)
		{
			List<GameMetadata> list3 = (gameLibrary.Games = new List<GameMetadata>());
		}
		foreach (GameMetadata game in library.Games)
		{
			game.Id = (string.IsNullOrWhiteSpace(game.Id) ? Guid.NewGuid().ToString("N") : game.Id);
			GameMetadata gameMetadata = game;
			if (gameMetadata.Title == null)
			{
				string text = (gameMetadata.Title = string.Empty);
			}
			game.LaunchType = (string.IsNullOrWhiteSpace(game.LaunchType) ? "Exe" : game.LaunchType);
			gameMetadata = game;
			if (gameMetadata.ExecutablePath == null)
			{
				string text = (gameMetadata.ExecutablePath = string.Empty);
			}
			gameMetadata = game;
			if (gameMetadata.SteamAppId == null)
			{
				string text = (gameMetadata.SteamAppId = string.Empty);
			}
			gameMetadata = game;
			if (gameMetadata.InstallPath == null)
			{
				string text = (gameMetadata.InstallPath = string.Empty);
			}
			gameMetadata = game;
			if (gameMetadata.LaunchCommand == null)
			{
				string text = (gameMetadata.LaunchCommand = string.Empty);
			}
			gameMetadata = game;
			if (gameMetadata.Arguments == null)
			{
				string text = (gameMetadata.Arguments = string.Empty);
			}
			gameMetadata = game;
			if (gameMetadata.WorkingDirectory == null)
			{
				string text = (gameMetadata.WorkingDirectory = string.Empty);
			}
			gameMetadata = game;
			if (gameMetadata.CoverArtPath == null)
			{
				string text = (gameMetadata.CoverArtPath = string.Empty);
			}
			gameMetadata = game;
			if (gameMetadata.HeaderImagePath == null)
			{
				string text = (gameMetadata.HeaderImagePath = string.Empty);
			}
			gameMetadata = game;
			if (gameMetadata.StoreScreenshotPath == null)
			{
				string text = (gameMetadata.StoreScreenshotPath = string.Empty);
			}
			gameMetadata = game;
			if (gameMetadata.BackgroundArtPath == null)
			{
				string text = (gameMetadata.BackgroundArtPath = string.Empty);
			}
			gameMetadata = game;
			if (gameMetadata.LogoImagePath == null)
			{
				string text = (gameMetadata.LogoImagePath = string.Empty);
			}
			gameMetadata = game;
			if (gameMetadata.Genre == null)
			{
				string text = (gameMetadata.Genre = string.Empty);
			}
			gameMetadata = game;
			if (gameMetadata.Rating == null)
			{
				string text = (gameMetadata.Rating = string.Empty);
			}
			gameMetadata = game;
			if (gameMetadata.MultiplayerInfo == null)
			{
				string text = (gameMetadata.MultiplayerInfo = string.Empty);
			}
			gameMetadata = game;
			if (gameMetadata.CoOpInfo == null)
			{
				string text = (gameMetadata.CoOpInfo = string.Empty);
			}
			gameMetadata = game;
			if (gameMetadata.Platform == null)
			{
				string text = (gameMetadata.Platform = string.Empty);
			}
		}
		return library;
	}

	private static async Task WriteBackupAsync(DashboardBackup backup, string filePath, CancellationToken cancellationToken)
	{
		string directoryName = Path.GetDirectoryName(filePath);
		if (!string.IsNullOrWhiteSpace(directoryName))
		{
			Directory.CreateDirectory(directoryName);
		}
		await using FileStream stream = File.Create(filePath);
		await JsonSerializer.SerializeAsync(stream, backup, SerializerOptions, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
	}

	private static async Task<ImportedDashboardData> ReadAndValidateBackupAsync(string filePath, CancellationToken cancellationToken)
	{
		if (!File.Exists(filePath))
		{
			throw new InvalidDataException("The selected backup file could not be found.");
		}
		await using FileStream stream = File.OpenRead(filePath);
		using JsonDocument document = await JsonDocument.ParseAsync(stream, new JsonDocumentOptions
		{
			AllowTrailingCommas = true,
			CommentHandling = JsonCommentHandling.Skip
		}, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
		JsonElement root = document.RootElement;
		if (root.ValueKind == JsonValueKind.Object && IsFullBackupJson(root))
		{
			DashboardBackup dashboardBackup = root.Deserialize<DashboardBackup>(SerializerOptions) ?? throw new InvalidDataException("The selected backup file is empty or unreadable.");
			return NormalizeImportedBackup(dashboardBackup, includesSettings: true, includesProfile: true, includesLibrary: true, includesThemes: true);
		}
		if (root.ValueKind == JsonValueKind.Object && IsSettingsJson(root))
		{
			AppSettings settings = root.Deserialize<AppSettings>(SerializerOptions) ?? throw new InvalidDataException("The selected settings file is empty or unreadable.");
			return NormalizeImportedBackup(new DashboardBackup
			{
				Settings = await BuildSettingsBackupAsync(settings, cancellationToken).ConfigureAwait(continueOnCapturedContext: false)
			}, includesSettings: true, includesProfile: false, includesLibrary: false, includesThemes: false);
		}
		if (root.ValueKind == JsonValueKind.Object && IsProfileJson(root))
		{
			Profile profile = root.Deserialize<Profile>(SerializerOptions) ?? throw new InvalidDataException("The selected profile file is empty or unreadable.");
			return NormalizeImportedBackup(new DashboardBackup
			{
				Profile = await BuildProfileBackupAsync(profile, cancellationToken).ConfigureAwait(continueOnCapturedContext: false)
			}, includesSettings: false, includesProfile: true, includesLibrary: false, includesThemes: false);
		}
		if (root.ValueKind == JsonValueKind.Object && IsLibraryJson(root))
		{
			GameLibrary library = root.Deserialize<GameLibrary>(SerializerOptions) ?? throw new InvalidDataException("The selected library file is empty or unreadable.");
			return NormalizeImportedBackup(new DashboardBackup
			{
				Library = library
			}, includesSettings: false, includesProfile: false, includesLibrary: true, includesThemes: false);
		}
		if (root.ValueKind == JsonValueKind.Array)
		{
			List<GameMetadata> games = root.Deserialize<List<GameMetadata>>(SerializerOptions) ?? new List<GameMetadata>();
			return NormalizeImportedBackup(new DashboardBackup
			{
				Library = new GameLibrary
				{
					Games = games
				}
			}, includesSettings: false, includesProfile: false, includesLibrary: true, includesThemes: false);
		}
		throw new InvalidDataException("The selected JSON file is not a DashX360 backup, settings, profile, or library file.");
	}

	private static ImportedDashboardData NormalizeImportedBackup(DashboardBackup dashboardBackup, bool includesSettings, bool includesProfile, bool includesLibrary, bool includesThemes)
	{
		if (dashboardBackup.Settings == null)
		{
			dashboardBackup.Settings = new DashboardBackupSettings();
		}
		if (dashboardBackup.Profile == null)
		{
			dashboardBackup.Profile = new DashboardBackupProfile();
		}
		dashboardBackup.Library = NormalizeLibrary(dashboardBackup.Library);
		if (dashboardBackup.CustomThemes == null)
		{
			dashboardBackup.CustomThemes = new List<DashboardBackupTheme>();
		}
		return new ImportedDashboardData(dashboardBackup, includesSettings, includesProfile, includesLibrary, includesThemes);
	}

	private static bool IsFullBackupJson(JsonElement root)
	{
		return root.TryGetProperty("ExportVersion", out _) || root.TryGetProperty("Settings", out _) || root.TryGetProperty("Profile", out _) || root.TryGetProperty("Library", out _) || root.TryGetProperty("CustomThemes", out _);
	}

	private static bool IsSettingsJson(JsonElement root)
	{
		return root.TryGetProperty("DashboardTileCustomizations", out _) || root.TryGetProperty("DashboardTileColor", out _) || root.TryGetProperty("ThemeName", out _) || root.TryGetProperty("StartFullscreen", out _);
	}

	private static bool IsProfileJson(JsonElement root)
	{
		return root.TryGetProperty("Gamertag", out _) || root.TryGetProperty("GamerPicturePath", out _) || root.TryGetProperty("Gamerscore", out _);
	}

	private static bool IsLibraryJson(JsonElement root)
	{
		return root.TryGetProperty("Games", out _) || root.TryGetProperty("LibraryPaths", out _);
	}

	private sealed record ImportedDashboardData(DashboardBackup Backup, bool IncludesSettings, bool IncludesProfile, bool IncludesLibrary, bool IncludesThemes);

	private static string MakeSafeFileName(string value)
	{
		char[] invalid = Path.GetInvalidFileNameChars();
		string text = new string(value.Select((char ch) => (!invalid.Contains(ch)) ? ch : '_').ToArray()).Trim();
		if (!string.IsNullOrWhiteSpace(text))
		{
			return text;
		}
		return "profile-import.png";
	}

	private static string MakeSafeFolderName(string value)
	{
		char[] invalid = Path.GetInvalidFileNameChars();
		string text = new string((from ch in value
			where !invalid.Contains(ch)
			select (!char.IsWhiteSpace(ch)) ? ch : '_').ToArray()).Trim('_');
		if (!string.IsNullOrWhiteSpace(text))
		{
			return text;
		}
		return "Custom_Theme";
	}

	private static void DeleteIfExists(string path)
	{
		try
		{
			if (File.Exists(path))
			{
				File.Delete(path);
			}
		}
		catch
		{
		}
	}
}
