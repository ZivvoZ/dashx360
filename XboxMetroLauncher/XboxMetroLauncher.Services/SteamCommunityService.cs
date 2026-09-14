using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Win32;
using XboxMetroLauncher.Models;
using XboxMetroLauncher.Utilities;

namespace XboxMetroLauncher.Services;

public sealed class SteamCommunityService : ISteamCommunityService
{
	private sealed class SteamFriendsResponse
	{
		[JsonPropertyName("friendslist")]
		public SteamFriendsList? FriendsList { get; set; }
	}

	private sealed class SteamFriendsList
	{
		[JsonPropertyName("friends")]
		public List<SteamFriendEntry>? Friends { get; set; }
	}

	private sealed class SteamFriendEntry
	{
		[JsonPropertyName("steamid")]
		public string SteamId { get; set; } = string.Empty;
	}

	private sealed class SteamPlayerSummariesResponse
	{
		[JsonPropertyName("response")]
		public SteamPlayerSummaries? Response { get; set; }
	}

	private sealed class SteamPlayerSummaries
	{
		[JsonPropertyName("players")]
		public List<SteamPlayerSummary>? Players { get; set; }
	}

	private sealed class SteamPlayerSummary
	{
		[JsonPropertyName("steamid")]
		public string SteamId { get; set; } = string.Empty;

		[JsonPropertyName("personaname")]
		public string PersonaName { get; set; } = string.Empty;

		[JsonPropertyName("personastate")]
		public int PersonaState { get; set; }

		[JsonPropertyName("avatar")]
		public string? Avatar { get; set; }

		[JsonPropertyName("avatarmedium")]
		public string? AvatarMedium { get; set; }

		[JsonPropertyName("avatarfull")]
		public string? AvatarFull { get; set; }

		[JsonPropertyName("gameextrainfo")]
		public string? GameExtraInfo { get; set; }

		[JsonPropertyName("gameid")]
		public string? GameId { get; set; }
	}

	private sealed class SteamAchievementsResponse
	{
		[JsonPropertyName("playerstats")]
		public SteamPlayerStats? PlayerStats { get; set; }
	}

	private sealed class SteamPlayerStats
	{
		[JsonPropertyName("success")]
		public bool Success { get; set; } = true;

		[JsonPropertyName("achievements")]
		public List<SteamAchievementResponseItem>? Achievements { get; set; }
	}

	private sealed class SteamAchievementResponseItem
	{
		[JsonPropertyName("apiname")]
		public string? ApiName { get; set; }

		[JsonPropertyName("name")]
		public string? Name { get; set; }

		[JsonPropertyName("description")]
		public string? Description { get; set; }

		[JsonPropertyName("icon")]
		public string? Icon { get; set; }

		[JsonPropertyName("icongray")]
		public string? IconGray { get; set; }

		[JsonPropertyName("achieved")]
		public int Achieved { get; set; }

		[JsonPropertyName("unlocktime")]
		public long UnlockTime { get; set; }
	}

	private sealed class SteamAchievementSchemaResponse
	{
		[JsonPropertyName("game")]
		public SteamAchievementSchemaGame? Game { get; set; }
	}

	private sealed class SteamAchievementSchemaGame
	{
		[JsonPropertyName("availableGameStats")]
		public SteamAvailableGameStats? AvailableGameStats { get; set; }
	}

	private sealed class SteamAvailableGameStats
	{
		[JsonPropertyName("achievements")]
		public List<SteamAchievementSchemaItem>? Achievements { get; set; }
	}

	private sealed class SteamAchievementSchemaItem
	{
		[JsonPropertyName("name")]
		public string? Name { get; set; }

		[JsonPropertyName("displayName")]
		public string? DisplayName { get; set; }

		[JsonPropertyName("description")]
		public string? Description { get; set; }

		[JsonPropertyName("icon")]
		public string? Icon { get; set; }

		[JsonPropertyName("icongray")]
		public string? IconGray { get; set; }
	}

	private sealed class SteamOwnedGamesResponse
	{
		[JsonPropertyName("response")]
		public SteamOwnedGamesData? Response { get; set; }
	}

	private sealed class SteamOwnedGamesData
	{
		[JsonPropertyName("games")]
		public List<SteamOwnedGame>? Games { get; set; }
	}

	private sealed class SteamOwnedGame
	{
		[JsonPropertyName("appid")]
		public int AppId { get; set; }

		[JsonPropertyName("playtime_forever")]
		public int PlaytimeForever { get; set; }
	}

	private sealed class SteamStoreEnvelope
	{
		[JsonPropertyName("success")]
		public bool Success { get; set; }

		[JsonPropertyName("data")]
		public SteamStoreAppDetails? Data { get; set; }
	}

	private sealed class SteamStoreAppDetails
	{
		[JsonPropertyName("name")]
		public string Name { get; set; } = string.Empty;

		[JsonPropertyName("short_description")]
		public string ShortDescription { get; set; } = string.Empty;

		[JsonPropertyName("genres")]
		public List<SteamStoreDescriptionItem>? Genres { get; set; }

		[JsonPropertyName("categories")]
		public List<SteamStoreDescriptionItem>? Categories { get; set; }

		[JsonPropertyName("ratings")]
		public Dictionary<string, SteamStoreRating>? Ratings { get; set; }

		[JsonPropertyName("screenshots")]
		public List<SteamStoreScreenshot>? Screenshots { get; set; }

		[JsonPropertyName("dlc")]
		public List<int>? Dlc { get; set; }

		[JsonPropertyName("price_overview")]
		public SteamStorePriceOverview? PriceOverview { get; set; }
	}

	private sealed class SteamStorePriceOverview
	{
		[JsonPropertyName("final")]
		public int Final { get; set; }

		[JsonPropertyName("final_formatted")]
		public string FinalFormatted { get; set; } = string.Empty;
	}

	private sealed class SteamStoreDescriptionItem
	{
		[JsonPropertyName("description")]
		public string Description { get; set; } = string.Empty;
	}

	private sealed class SteamStoreRating
	{
		[JsonPropertyName("rating")]
		public string Rating { get; set; } = string.Empty;
	}

	private sealed class SteamStoreScreenshot
	{
		[JsonPropertyName("path_thumbnail")]
		public string PathThumbnail { get; set; } = string.Empty;

		[JsonPropertyName("path_full")]
		public string PathFull { get; set; } = string.Empty;
	}

	private sealed class SteamReviewResponse
	{
		[JsonPropertyName("query_summary")]
		public SteamReviewSummary? QuerySummary { get; set; }
	}

	private sealed class SteamReviewSummary
	{
		[JsonPropertyName("total_reviews")]
		public int TotalReviews { get; set; }

		[JsonPropertyName("total_positive")]
		public int TotalPositive { get; set; }

		[JsonPropertyName("review_score")]
		public int ReviewScore { get; set; }
	}

	private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
	{
		PropertyNameCaseInsensitive = true,
		WriteIndented = true
	};

	private static readonly TimeSpan FriendsCacheAge = TimeSpan.FromMinutes(5.0);

	private static readonly TimeSpan AchievementsCacheAge = TimeSpan.FromMinutes(15.0);

	private static readonly TimeSpan GameDetailsCacheAge = TimeSpan.FromHours(12.0);

	private static readonly HttpClient Http = new HttpClient
	{
		Timeout = TimeSpan.FromSeconds(8.0)
	};

	private readonly string _configPath;

	private readonly string _cacheFolder;

	public string LastStatusMessage { get; private set; } = string.Empty;

	public bool IsConfigured
	{
		get
		{
			try
			{
				if (!File.Exists(_configPath))
				{
					return false;
				}
				SteamCommunityConfig steamCommunityConfig = JsonSerializer.Deserialize<SteamCommunityConfig>(File.ReadAllText(_configPath), JsonOptions);
				return steamCommunityConfig != null && HasCredentials(steamCommunityConfig);
			}
			catch
			{
				return false;
			}
		}
	}

	public SteamCommunityService()
	{
		_configPath = Path.Combine(AppPaths.UserDataFolder, "steam-web-config.json");
		_cacheFolder = Path.Combine(AppPaths.UserDataFolder, "SteamCache");
		Directory.CreateDirectory(_cacheFolder);
		EnsureConfigExample();
	}

	public async Task<IReadOnlyList<SocialFriend>> LoadFriendsAsync(CancellationToken cancellationToken = default(CancellationToken))
	{
		LastStatusMessage = string.Empty;
		SteamCommunityConfig config = await LoadConfigAsync(cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
		if (!HasCredentials(config))
		{
			LastStatusMessage = "Steam friends need UserData\\steam-web-config.json";
			return Array.Empty<SocialFriend>();
		}
		string cachePath = Path.Combine(_cacheFolder, "friends.json");
		List<SocialFriend> list = await ReadFreshCacheAsync<List<SocialFriend>>(cachePath, FriendsCacheAge, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
		if (list != null && list.Any(HasStaleSteamFriendCache))
		{
			list = null;
		}
		if (list != null)
		{
			return list.Select(NormalizeSteamFriendDisplay).ToList();
		}
		try
		{
			List<string> list2 = (from id in (await GetJsonAsync<SteamFriendsResponse>($"https://api.steampowered.com/ISteamUser/GetFriendList/v0001/?key={Uri.EscapeDataString(config.SteamApiKey)}&steamid={Uri.EscapeDataString(config.SteamId64)}&relationship=friend", cancellationToken).ConfigureAwait(continueOnCapturedContext: false))?.FriendsList?.Friends?.Select((SteamFriendEntry friend) => friend.SteamId)
				where !string.IsNullOrWhiteSpace(id)
				select id).Distinct<string>(StringComparer.OrdinalIgnoreCase).Take(100).ToList() ?? new List<string>();
			if (list2.Count == 0)
			{
				await WriteCacheAsync(cachePath, new List<SocialFriend>(), cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
				return Array.Empty<SocialFriend>();
			}
			List<SteamPlayerSummary> source = (await GetJsonAsync<SteamPlayerSummariesResponse>("https://api.steampowered.com/ISteamUser/GetPlayerSummaries/v0002/?key=" + Uri.EscapeDataString(config.SteamApiKey) + "&steamids=" + Uri.EscapeDataString(string.Join(',', list2)), cancellationToken).ConfigureAwait(continueOnCapturedContext: false))?.Response?.Players ?? new List<SteamPlayerSummary>();
			List<SocialFriend> mapped = (from friend in source.Select(MapPlayer).Select(NormalizeSteamFriendDisplay)
				orderby friend.IsOnline descending
				select friend).ThenBy<SocialFriend, string>((SocialFriend friend) => friend.DisplayName, StringComparer.CurrentCultureIgnoreCase).ToList();
			mapped = await CacheSteamAvatarsAsync(mapped, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
			await WriteCacheAsync(cachePath, mapped, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
			return mapped;
		}
		catch (Exception ex)
		{
			LastStatusMessage = "Steam friends unavailable: " + ex.Message;
			return ((await ReadCacheAsync<List<SocialFriend>>(cachePath, cancellationToken).ConfigureAwait(continueOnCapturedContext: false)) ?? new List<SocialFriend>()).Select(NormalizeSteamFriendDisplay).ToList();
		}
	}

	public async Task SaveConfigAsync(SteamCommunityConfig config, CancellationToken cancellationToken = default(CancellationToken))
	{
		Directory.CreateDirectory(Path.GetDirectoryName(_configPath));
		await using FileStream stream = File.Create(_configPath);
		await JsonSerializer.SerializeAsync(stream, config, JsonOptions, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
	}

	public async Task<SteamConnectionTestResult> TestConnectionAsync(SteamCommunityConfig config, CancellationToken cancellationToken = default(CancellationToken))
	{
		if (!HasCredentials(config))
		{
			return new SteamConnectionTestResult
			{
				Success = false,
				Message = "Enter a Steam Web API key and SteamID64 first."
			};
		}
		try
		{
			SteamPlayerSummary steamPlayerSummary = (await GetJsonAsync<SteamPlayerSummariesResponse>("https://api.steampowered.com/ISteamUser/GetPlayerSummaries/v0002/?key=" + Uri.EscapeDataString(config.SteamApiKey) + "&steamids=" + Uri.EscapeDataString(config.SteamId64), cancellationToken).ConfigureAwait(continueOnCapturedContext: false))?.Response?.Players?.FirstOrDefault();
			if (steamPlayerSummary == null)
			{
				return new SteamConnectionTestResult
				{
					Success = false,
					Message = "Steam did not find that profile. Check the SteamID64."
				};
			}
			string text = (string.IsNullOrWhiteSpace(steamPlayerSummary.PersonaName) ? steamPlayerSummary.SteamId : steamPlayerSummary.PersonaName);
			return new SteamConnectionTestResult
			{
				Success = true,
				DisplayName = text,
				Message = "Connected as " + text + "."
			};
		}
		catch (Exception ex)
		{
			return new SteamConnectionTestResult
			{
				Success = false,
				Message = "Steam connection failed: " + ex.Message
			};
		}
	}

	public async Task<IReadOnlyList<SteamAchievementItem>> LoadAchievementsAsync(string appId, CancellationToken cancellationToken = default(CancellationToken))
	{
		LastStatusMessage = string.Empty;
		if (string.IsNullOrWhiteSpace(appId))
		{
			LastStatusMessage = "Select a Steam game first.";
			return Array.Empty<SteamAchievementItem>();
		}
		SteamCommunityConfig config = await LoadConfigAsync(cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
		if (!HasCredentials(config))
		{
			LastStatusMessage = "Steam achievements need UserData\\steam-web-config.json";
			return Array.Empty<SteamAchievementItem>();
		}
		string safeAppId = new string(appId.Where(char.IsDigit).ToArray());
		if (string.IsNullOrWhiteSpace(safeAppId))
		{
			LastStatusMessage = "This game does not have a Steam AppID.";
			return Array.Empty<SteamAchievementItem>();
		}
		string cachePath = Path.Combine(_cacheFolder, "Achievements", safeAppId + ".json");
		List<SteamAchievementItem> list = await ReadFreshCacheAsync<List<SteamAchievementItem>>(cachePath, AchievementsCacheAge, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
		if (list != null && ShouldUseCachedAchievements(list))
		{
			return list;
		}
		try
		{
			SteamAchievementsResponse steamAchievementsResponse = await GetJsonAsync<SteamAchievementsResponse>($"https://api.steampowered.com/ISteamUserStats/GetPlayerAchievements/v0001/?key={Uri.EscapeDataString(config.SteamApiKey)}&steamid={Uri.EscapeDataString(config.SteamId64)}&appid={Uri.EscapeDataString(safeAppId)}&l=en", cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
			if (steamAchievementsResponse != null && steamAchievementsResponse.PlayerStats?.Success == false)
			{
				return await LoadAchievementSchemaFallbackAsync(config, safeAppId, cachePath, "Steam did not return unlock status for this game.", cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
			}
			List<SteamAchievementItem> achievements = (from item in steamAchievementsResponse?.PlayerStats?.Achievements?.Select((SteamAchievementResponseItem item) => new SteamAchievementItem
				{
					ApiName = (item.ApiName ?? string.Empty),
					Name = (string.IsNullOrWhiteSpace(item.Name) ? (item.ApiName ?? "Achievement") : item.Name),
					Description = (item.Description ?? string.Empty),
					IconUrl = (item.Icon ?? string.Empty),
					IconGrayUrl = (item.IconGray ?? string.Empty),
					Achieved = (item.Achieved > 0),
					UnlockTimeUnix = item.UnlockTime
				})
				orderby item.Achieved
				select item).ThenBy<SteamAchievementItem, string>((SteamAchievementItem item) => item.Name, StringComparer.CurrentCultureIgnoreCase).ToList() ?? new List<SteamAchievementItem>();
			// Unlock status and artwork are returned by separate Steam endpoints.
			try
			{
				var schema = await GetJsonAsync<SteamAchievementSchemaResponse>($"https://api.steampowered.com/ISteamUserStats/GetSchemaForGame/v2/?key={Uri.EscapeDataString(config.SteamApiKey)}&appid={Uri.EscapeDataString(safeAppId)}&l=en", cancellationToken).ConfigureAwait(false);
				foreach (var achievement in achievements)
				{
					var metadata = schema?.Game?.AvailableGameStats?.Achievements?.FirstOrDefault(item => item.Name == achievement.ApiName);
					var cached = list?.FirstOrDefault(item => item.ApiName == achievement.ApiName);
					achievement.IconUrl = metadata?.Icon ?? cached?.IconUrl ?? achievement.IconUrl;
					achievement.IconGrayUrl = metadata?.IconGray ?? cached?.IconGrayUrl ?? achievement.IconGrayUrl;
					if (!string.IsNullOrWhiteSpace(metadata?.DisplayName)) achievement.Name = metadata.DisplayName;
					if (!string.IsNullOrWhiteSpace(metadata?.Description)) achievement.Description = metadata.Description;
				}
			}
			catch (OperationCanceledException) { throw; }
			catch (Exception) { /* Keep genuine unlock status when artwork is unavailable. */ }
			await WriteCacheAsync(cachePath, achievements, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
			return achievements;
		}
		catch (Exception ex)
		{
			return await LoadAchievementSchemaFallbackAsync(config, safeAppId, cachePath, "Steam unlock status unavailable: " + FriendlySteamError(ex), cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
		}
	}

	private static bool ShouldUseCachedAchievements(IReadOnlyCollection<SteamAchievementItem> achievements)
	{
		return !achievements.Any(achievement => achievement.Achieved && string.IsNullOrWhiteSpace(achievement.IconUrl) && string.IsNullOrWhiteSpace(achievement.IconGrayUrl));
	}

	public async Task<SteamGameDetails> LoadGameDetailsAsync(string appId, CancellationToken cancellationToken = default(CancellationToken))
	{
		string safeAppId = new string((appId ?? string.Empty).Where(char.IsDigit).ToArray());
		if (string.IsNullOrWhiteSpace(safeAppId))
		{
			return new SteamGameDetails();
		}
		TimeSpan? playtime = await LoadSteamPlaytimeAsync(safeAppId, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
		SteamGameDetails steamGameDetails = await LoadSteamStoreDetailsAsync(safeAppId, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
		return new SteamGameDetails
		{
			Playtime = playtime,
			Genre = steamGameDetails.Genre,
			Rating = steamGameDetails.Rating,
			MultiplayerInfo = steamGameDetails.MultiplayerInfo,
			CoOpInfo = steamGameDetails.CoOpInfo,
			StoreDescription = steamGameDetails.StoreDescription,
			StoreScreenshotPath = steamGameDetails.StoreScreenshotPath,
			ReviewStarRating = steamGameDetails.ReviewStarRating,
			ReviewCount = steamGameDetails.ReviewCount,
			Dlc = steamGameDetails.Dlc
		};
	}

	private async Task<TimeSpan?> LoadSteamPlaytimeAsync(string safeAppId, CancellationToken cancellationToken)
	{
		SteamCommunityConfig config = await LoadConfigAsync(cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
		if (!HasCredentials(config))
		{
			return await LoadLocalSteamPlaytimeAsync(safeAppId, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
		}
		string cachePath = Path.Combine(_cacheFolder, "owned-games.json");
		SteamOwnedGamesResponse ownedGames = await ReadFreshCacheAsync<SteamOwnedGamesResponse>(cachePath, GameDetailsCacheAge, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
		if (ownedGames == null)
		{
			try
			{
				ownedGames = await GetJsonAsync<SteamOwnedGamesResponse>($"https://api.steampowered.com/IPlayerService/GetOwnedGames/v0001/?key={Uri.EscapeDataString(config.SteamApiKey)}&steamid={Uri.EscapeDataString(config.SteamId64)}&include_played_free_games=1&format=json", cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
				if (ownedGames != null)
				{
					await WriteCacheAsync(cachePath, ownedGames, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
				}
			}
			catch
			{
				ownedGames = await ReadCacheAsync<SteamOwnedGamesResponse>(cachePath, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
			}
		}
		SteamOwnedGame steamOwnedGame = ownedGames?.Response?.Games?.FirstOrDefault((SteamOwnedGame candidate) => candidate.AppId.ToString(CultureInfo.InvariantCulture) == safeAppId);
		if (steamOwnedGame != null && steamOwnedGame.PlaytimeForever > 0)
		{
			return TimeSpan.FromMinutes(steamOwnedGame.PlaytimeForever);
		}
		return await LoadLocalSteamPlaytimeAsync(safeAppId, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
	}

	private static async Task<TimeSpan?> LoadLocalSteamPlaytimeAsync(string safeAppId, CancellationToken cancellationToken)
	{
		string text = FindSteamPath();
		if (string.IsNullOrWhiteSpace(text))
		{
			return null;
		}
		string path = Path.Combine(text, "userdata");
		if (!Directory.Exists(path))
		{
			return null;
		}
		int bestMinutes = 0;
		foreach (string item in Directory.EnumerateFiles(path, "localconfig.vdf", SearchOption.AllDirectories))
		{
			cancellationToken.ThrowIfCancellationRequested();
			string input;
			try
			{
				input = await File.ReadAllTextAsync(item, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
			}
			catch
			{
				continue;
			}
			Match match = Regex.Match(input, "\"" + Regex.Escape(safeAppId) + "\"\\s*\\{(?<body>.*?)\\n\\s*\\}", RegexOptions.IgnoreCase | RegexOptions.Singleline);
			if (match.Success)
			{
				Match match2 = Regex.Match(match.Groups["body"].Value, "\"playtime\"\\s*\"(?<minutes>\\d+)\"", RegexOptions.IgnoreCase);
				if (match2.Success && int.TryParse(match2.Groups["minutes"].Value, out var result) && result > bestMinutes)
				{
					bestMinutes = result;
				}
			}
		}
		return (bestMinutes > 0) ? new TimeSpan?(TimeSpan.FromMinutes(bestMinutes)) : ((TimeSpan?)null);
	}

	private async Task<SteamGameDetails> LoadSteamStoreDetailsAsync(string safeAppId, CancellationToken cancellationToken)
	{
		string cachePath = Path.Combine(_cacheFolder, "StoreDetails", safeAppId + ".json");
		SteamStoreAppDetails cached = await ReadFreshCacheAsync<SteamStoreAppDetails>(cachePath, GameDetailsCacheAge, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
		if (cached != null && (cached.Dlc == null || string.IsNullOrWhiteSpace(cached.ShortDescription)))
		{
			cached = null;
		}
		if (cached == null)
		{
			try
			{
				Dictionary<string, SteamStoreEnvelope> dictionary = await GetJsonAsync<Dictionary<string, SteamStoreEnvelope>>("https://store.steampowered.com/api/appdetails?appids=" + Uri.EscapeDataString(safeAppId) + "&filters=basic,genres,categories,ratings,screenshots,dlc,short_description", cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
				cached = ((dictionary != null && dictionary.TryGetValue(safeAppId, out var value) && value.Success) ? value.Data : null);
				if (cached != null)
				{
					await WriteCacheAsync(cachePath, cached, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
				}
			}
			catch
			{
				cached = await ReadCacheAsync<SteamStoreAppDetails>(cachePath, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
			}
		}
		if (cached == null)
		{
			return new SteamGameDetails();
		}
		List<string> categories = (from value2 in cached.Categories?.Select((SteamStoreDescriptionItem category) => category.Description)
			where !string.IsNullOrWhiteSpace(value2)
			select value2).ToList() ?? new List<string>();
		List<string> genres = (from value2 in cached.Genres?.Select((SteamStoreDescriptionItem genre) => genre.Description)
			where !string.IsNullOrWhiteSpace(value2)
			select value2).Take(2).ToList() ?? new List<string>();
		(double Stars, int Count) reviewSummary = await LoadSteamReviewSummaryAsync(safeAppId, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
		string screenshotPath = await CacheStoreScreenshotAsync(safeAppId, cached.Screenshots, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
		IReadOnlyList<SteamGameDlc> dlc = await LoadSteamDlcAsync(safeAppId, cached.Dlc, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
		SteamGameDetails steamGameDetails = new SteamGameDetails();
		steamGameDetails.Genre = ((genres.Count == 0) ? string.Empty : string.Join(" & ", genres));
		steamGameDetails.Rating = BuildRatingLabel(cached.Ratings);
		steamGameDetails.MultiplayerInfo = BuildCategoryLine(categories, "Multiplayer", new string[4] { "Multi-player", "MMO", "PvP", "Online PvP" });
		steamGameDetails.CoOpInfo = BuildCategoryLine(categories, "Co-op", new string[4] { "Co-op", "Online Co-op", "Shared/Split Screen Co-op", "LAN Co-op" });
		steamGameDetails.StoreDescription = CleanSteamHtmlText(cached.ShortDescription);
		steamGameDetails.StoreScreenshotPath = screenshotPath;
		steamGameDetails.ReviewStarRating = reviewSummary.Stars;
		steamGameDetails.ReviewCount = reviewSummary.Count;
		steamGameDetails.Dlc = dlc;
		return steamGameDetails;
	}

	private async Task<IReadOnlyList<SteamGameDlc>> LoadSteamDlcAsync(string safeAppId, IReadOnlyList<int>? dlcAppIds, CancellationToken cancellationToken)
	{
		List<int> ids = dlcAppIds?.Where((int id) => id > 0).Distinct().Take(60)
			.ToList() ?? new List<int>();
		if (ids.Count == 0)
		{
			return await LoadSteamDlcPageFallbackAsync(safeAppId, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
		}
		string cachePath = Path.Combine(_cacheFolder, "StoreDetails", safeAppId + "-dlc.json");
		List<SteamGameDlc> list = await ReadFreshCacheAsync<List<SteamGameDlc>>(cachePath, GameDetailsCacheAge, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
		if (list != null && list.Count > 0)
		{
			return list;
		}
		try
		{
			string text = string.Join(',', ids);
			Dictionary<string, SteamStoreEnvelope> response = await GetJsonAsync<Dictionary<string, SteamStoreEnvelope>>("https://store.steampowered.com/api/appdetails?appids=" + text + "&filters=basic,price_overview", cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
			List<SteamGameDlc> mapped = (from item in ids.Select(delegate(int id)
				{
					string text2 = id.ToString(CultureInfo.InvariantCulture);
					SteamStoreEnvelope value;
					return (response == null || !response.TryGetValue(text2, out value) || !value.Success || value.Data == null || string.IsNullOrWhiteSpace(value.Data.Name)) ? null : new SteamGameDlc
					{
						AppId = text2,
						Name = value.Data.Name.Trim(),
						PriceText = BuildPriceText(value.Data.PriceOverview)
					};
				})
				where item != null
				select item).Cast<SteamGameDlc>().ToList();
			if (mapped.Count == 0)
			{
				return await LoadSteamDlcPageFallbackAsync(safeAppId, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
			}
			await WriteCacheAsync(cachePath, mapped, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
			return mapped;
		}
		catch
		{
			IReadOnlyList<SteamGameDlc> readOnlyList = await ReadCacheAsync<List<SteamGameDlc>>(cachePath, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
			if (readOnlyList == null)
			{
				readOnlyList = await LoadSteamDlcPageFallbackAsync(safeAppId, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
			}
			return readOnlyList;
		}
	}

	private async Task<IReadOnlyList<SteamGameDlc>> LoadSteamDlcPageFallbackAsync(string safeAppId, CancellationToken cancellationToken)
	{
		string cachePath = Path.Combine(_cacheFolder, "StoreDetails", safeAppId + "-dlc-page.json");
		List<SteamGameDlc> list = await ReadFreshCacheAsync<List<SteamGameDlc>>(cachePath, GameDetailsCacheAge, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
		if (list != null)
		{
			return list;
		}
		try
		{
			string input = await Http.GetStringAsync("https://store.steampowered.com/dlc/" + Uri.EscapeDataString(safeAppId) + "/", cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
			List<SteamGameDlc> items = new List<SteamGameDlc>();
			HashSet<string> hashSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
			foreach (Match item in Regex.Matches(input, "href=\"https://store\\.steampowered\\.com/app/(?<id>\\d+)/[^\"]*\"(?<body>.*?)</a>", RegexOptions.IgnoreCase | RegexOptions.Singleline))
			{
				string value = item.Groups["id"].Value;
				if (!string.Equals(value, safeAppId, StringComparison.OrdinalIgnoreCase) && hashSet.Add(value))
				{
					string value2 = item.Groups["body"].Value;
					string value3 = Regex.Match(value2, "class=\"tab_item_name\"[^>]*>(?<name>.*?)</", RegexOptions.IgnoreCase | RegexOptions.Singleline).Groups["name"].Value;
					if (string.IsNullOrWhiteSpace(value3))
					{
						value3 = Regex.Match(value2, "data-ds-appid=\"\\d+\"[^>]*>(?<name>.*?)</", RegexOptions.IgnoreCase | RegexOptions.Singleline).Groups["name"].Value;
					}
					value3 = CleanSteamHtmlText(value3);
					if (!string.IsNullOrWhiteSpace(value3))
					{
						string value4 = Regex.Match(value2, "class=\"(?:discount_final_price|col search_price)[^\"]*\"[^>]*>(?<price>.*?)</", RegexOptions.IgnoreCase | RegexOptions.Singleline).Groups["price"].Value;
						items.Add(new SteamGameDlc
						{
							AppId = value,
							Name = value3,
							PriceText = CleanSteamHtmlText(value4)
						});
					}
				}
			}
			await WriteCacheAsync(cachePath, items, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
			return items;
		}
		catch
		{
			return (await ReadCacheAsync<List<SteamGameDlc>>(cachePath, cancellationToken).ConfigureAwait(continueOnCapturedContext: false)) ?? new List<SteamGameDlc>();
		}
	}

	private static string CleanSteamHtmlText(string value)
	{
		if (string.IsNullOrWhiteSpace(value))
		{
			return string.Empty;
		}
		return Regex.Replace(WebUtility.HtmlDecode(Regex.Replace(value, "<.*?>", " ", RegexOptions.Singleline)), "\\s+", " ").Trim();
	}

	private static string BuildPriceText(SteamStorePriceOverview? price)
	{
		if (price == null)
		{
			return string.Empty;
		}
		if (price.Final <= 0)
		{
			return "Free";
		}
		if (!string.IsNullOrWhiteSpace(price.FinalFormatted))
		{
			return price.FinalFormatted;
		}
		return ((double)price.Final / 100.0).ToString("C", CultureInfo.CurrentCulture);
	}

	private async Task<(double Stars, int Count)> LoadSteamReviewSummaryAsync(string safeAppId, CancellationToken cancellationToken)
	{
		string cachePath = Path.Combine(_cacheFolder, "StoreReviews", safeAppId + ".json");
		SteamReviewResponse cached = await ReadFreshCacheAsync<SteamReviewResponse>(cachePath, GameDetailsCacheAge, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
		if (cached == null)
		{
			try
			{
				cached = await GetJsonAsync<SteamReviewResponse>("https://store.steampowered.com/appreviews/" + Uri.EscapeDataString(safeAppId) + "?json=1&purchase_type=all&num_per_page=0&language=all", cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
				if (cached != null)
				{
					await WriteCacheAsync(cachePath, cached, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
				}
			}
			catch
			{
				cached = await ReadCacheAsync<SteamReviewResponse>(cachePath, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
			}
		}
		SteamReviewSummary steamReviewSummary = cached?.QuerySummary;
		if (steamReviewSummary == null || steamReviewSummary.TotalReviews <= 0)
		{
			return (Stars: 0.0, Count: 0);
		}
		return (Stars: Math.Clamp(((steamReviewSummary.TotalPositive > 0) ? ((double)steamReviewSummary.TotalPositive / (double)steamReviewSummary.TotalReviews) : ((double)steamReviewSummary.ReviewScore / 9.0)) * 5.0, 0.0, 5.0), Count: steamReviewSummary.TotalReviews);
	}

	private async Task<string> CacheStoreScreenshotAsync(string safeAppId, IReadOnlyList<SteamStoreScreenshot>? screenshots, CancellationToken cancellationToken)
	{
		string text = screenshots?.Select((SteamStoreScreenshot screenshot) => (!string.IsNullOrWhiteSpace(screenshot.PathThumbnail)) ? screenshot.PathThumbnail : screenshot.PathFull).FirstOrDefault((string value) => !string.IsNullOrWhiteSpace(value));
		if (string.IsNullOrWhiteSpace(text))
		{
			return string.Empty;
		}
		string text2 = Path.Combine(_cacheFolder, "StoreImages", safeAppId);
		string text3 = Path.GetExtension(new Uri(text).AbsolutePath);
		if (!string.Equals(text3, ".png", StringComparison.OrdinalIgnoreCase) && !string.Equals(text3, ".jpg", StringComparison.OrdinalIgnoreCase) && !string.Equals(text3, ".jpeg", StringComparison.OrdinalIgnoreCase))
		{
			text3 = ".jpg";
		}
		string path = Path.Combine(text2, "screenshot" + text3);
		if (File.Exists(path))
		{
			return path;
		}
		try
		{
			Directory.CreateDirectory(text2);
			using (HttpResponseMessage response = await Http.GetAsync(text, cancellationToken).ConfigureAwait(continueOnCapturedContext: false))
			{
				if (!response.IsSuccessStatusCode)
				{
					return string.Empty;
				}
				if (!(response.Content.Headers.ContentType?.MediaType ?? string.Empty).StartsWith("image/", StringComparison.OrdinalIgnoreCase))
				{
					return string.Empty;
				}
				string result;
				await using (Stream source = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(continueOnCapturedContext: false))
				{
					string text4;
					await using (FileStream destination = File.Create(path))
					{
						await source.CopyToAsync(destination, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
						text4 = path;
					}
					result = text4;
				}
				return result;
			}
			IL_0512:;
		}
		catch
		{
			return File.Exists(path) ? path : string.Empty;
		}
		string result2;
		return result2;
	}

	private static string BuildRatingLabel(Dictionary<string, SteamStoreRating>? ratings)
	{
		if (ratings == null || ratings.Count == 0)
		{
			return string.Empty;
		}
		string[] array = new string[5] { "esrb", "pegi", "usk", "oflc", "dejus" };
		foreach (string key in array)
		{
			if (ratings.TryGetValue(key, out SteamStoreRating value) && !string.IsNullOrWhiteSpace(value.Rating))
			{
				return value.Rating.Trim().ToUpperInvariant();
			}
		}
		return ratings.Values.Select((SteamStoreRating steamStoreRating) => steamStoreRating.Rating).FirstOrDefault((string value2) => !string.IsNullOrWhiteSpace(value2))?.Trim().ToUpperInvariant() ?? string.Empty;
	}

	private static string? FindSteamPath()
	{
		string text = Registry.GetValue("HKEY_CURRENT_USER\\Software\\Valve\\Steam", "SteamPath", null) as string;
		if (!string.IsNullOrWhiteSpace(text) && Directory.Exists(text))
		{
			return text;
		}
		string text2 = (Registry.GetValue("HKEY_LOCAL_MACHINE\\SOFTWARE\\WOW6432Node\\Valve\\Steam", "InstallPath", null) as string) ?? (Registry.GetValue("HKEY_LOCAL_MACHINE\\SOFTWARE\\Valve\\Steam", "InstallPath", null) as string);
		if (!string.IsNullOrWhiteSpace(text2) && Directory.Exists(text2))
		{
			return text2;
		}
		return new string[2] { "C:\\Program Files (x86)\\Steam", "C:\\Program Files\\Steam" }.FirstOrDefault(Directory.Exists);
	}

	private static string BuildCategoryLine(IReadOnlyCollection<string> categories, string label, string[] matches)
	{
		List<string> list = categories.Where((string category) => matches.Any((string match) => category.Contains(match, StringComparison.OrdinalIgnoreCase))).Take(2).ToList();
		if (list.Count != 0)
		{
			return label + ": " + string.Join(" & ", list);
		}
		return label + ": None";
	}

	private async Task<IReadOnlyList<SteamAchievementItem>> LoadAchievementSchemaFallbackAsync(SteamCommunityConfig config, string safeAppId, string cachePath, string reason, CancellationToken cancellationToken)
	{
		List<SteamAchievementItem> cached = await ReadCacheAsync<List<SteamAchievementItem>>(cachePath, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
		try
		{
			List<SteamAchievementItem> achievements = (from item in (await GetJsonAsync<SteamAchievementSchemaResponse>($"https://api.steampowered.com/ISteamUserStats/GetSchemaForGame/v2/?key={Uri.EscapeDataString(config.SteamApiKey)}&appid={Uri.EscapeDataString(safeAppId)}&l=en", cancellationToken).ConfigureAwait(continueOnCapturedContext: false))?.Game?.AvailableGameStats?.Achievements?.Select((SteamAchievementSchemaItem item) => new SteamAchievementItem
				{
					ApiName = (item.Name ?? string.Empty),
					Name = (string.IsNullOrWhiteSpace(item.DisplayName) ? (item.Name ?? "Achievement") : item.DisplayName),
					Description = (item.Description ?? string.Empty),
					IconUrl = (item.Icon ?? string.Empty),
					IconGrayUrl = (item.IconGray ?? string.Empty),
					Achieved = false
				})
				where !string.IsNullOrWhiteSpace(item.Name)
				select item).OrderBy<SteamAchievementItem, string>((SteamAchievementItem item) => item.Name, StringComparer.CurrentCultureIgnoreCase).ToList() ?? new List<SteamAchievementItem>();
			if (achievements.Count > 0)
			{
				LastStatusMessage = reason + " Showing the public achievement list instead.";
				await WriteCacheAsync(cachePath, achievements, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
				return achievements;
			}
		}
		catch (Exception ex)
		{
			LastStatusMessage = reason + " Public achievement list unavailable: " + FriendlySteamError(ex);
			return cached ?? new List<SteamAchievementItem>();
		}
		LastStatusMessage = ((cached != null && cached.Count > 0) ? (reason + " Showing cached achievements.") : "Steam does not expose achievements for this game.");
		return cached ?? new List<SteamAchievementItem>();
	}

	private static string FriendlySteamError(Exception ex)
	{
		if (ex is HttpRequestException { StatusCode: not null } ex2)
		{
			return $"Steam returned {ex2.StatusCode.Value} {ex2.StatusCode.Value}.";
		}
		return ex.Message;
	}

	private static SocialFriend MapPlayer(SteamPlayerSummary player)
	{
		bool flag = player.PersonaState > 0;
		string activityText = (string.IsNullOrWhiteSpace(player.GameExtraInfo) ? string.Empty : player.GameExtraInfo.Trim());
		(string, string, string) tuple = BuildSteamProfileStats(player.SteamId, player.PersonaName);
		return new SocialFriend
		{
			Id = "steam:" + player.SteamId,
			DisplayName = (string.IsNullOrWhiteSpace(player.PersonaName) ? "Steam Friend" : player.PersonaName),
			Source = SocialFriendSource.Steam,
			AvatarPathOrUrl = (player.AvatarFull ?? player.AvatarMedium ?? player.Avatar ?? string.Empty),
			IsOnline = flag,
			StatusText = (flag ? "Online" : "Offline"),
			ActivityText = activityText,
			ActivityAppId = (player.GameId ?? string.Empty),
			GamerscoreText = tuple.Item1,
			ReputationText = tuple.Item2,
			ZoneText = tuple.Item3,
			IdentityDetailText = "Steam"
		};
	}

	private static (string GamerscoreText, string ReputationText, string ZoneText) BuildSteamProfileStats(string steamId, string personaName)
	{
		Random random = new Random((string.IsNullOrWhiteSpace(steamId) ? personaName : steamId).Aggregate(23, (int current, char character) => current * 31 + character));
		string[] array = new string[4] { "Recreation", "Family", "Pro", "Underground" };
		int value = random.Next(2500, 125000);
		int num = random.Next(3, 6);
		string item = new string('★', num) + new string('☆', 5 - num);
		return (GamerscoreText: $"{value:N0} G", ReputationText: item, ZoneText: array[random.Next(array.Length)]);
	}

	private static SocialFriend NormalizeSteamFriendDisplay(SocialFriend friend)
	{
		if (friend.Source != SocialFriendSource.Steam)
		{
			return friend;
		}
		return new SocialFriend
		{
			Id = friend.Id,
			DisplayName = friend.DisplayName,
			Source = friend.Source,
			AvatarPathOrUrl = friend.AvatarPathOrUrl,
			IsOnline = friend.IsOnline,
			StatusText = (friend.IsOnline ? "Online" : "Offline"),
			ActivityText = friend.ActivityText,
			ActivityAppId = friend.ActivityAppId,
			GamerscoreText = friend.GamerscoreText,
			ReputationText = friend.ReputationText,
			ZoneText = friend.ZoneText,
			IdentityDetailText = friend.IdentityDetailText,
			IsPartyHost = friend.IsPartyHost,
			ShowVoiceIndicator = friend.ShowVoiceIndicator
		};
	}

	private static bool HasBlankSteamAvatar(SocialFriend friend)
	{
		if (friend.Source == SocialFriendSource.Steam)
		{
			return string.IsNullOrWhiteSpace(friend.AvatarPathOrUrl);
		}
		return false;
	}

	private static bool HasStaleSteamFriendCache(SocialFriend friend)
	{
		if (friend.Source == SocialFriendSource.Steam)
		{
			if (!string.IsNullOrWhiteSpace(friend.AvatarPathOrUrl))
			{
				return string.Equals(friend.StatusText, "Away", StringComparison.OrdinalIgnoreCase);
			}
			return true;
		}
		return false;
	}

	private async Task<List<SocialFriend>> CacheSteamAvatarsAsync(IReadOnlyList<SocialFriend> friends, CancellationToken cancellationToken)
	{
		string avatarFolder = Path.Combine(_cacheFolder, "FriendAvatars");
		Directory.CreateDirectory(avatarFolder);
		SemaphoreSlim gate = new SemaphoreSlim(6);
		try
		{
			return (await Task.WhenAll(friends.Select((SocialFriend friend) => CacheSteamAvatarAsync(friend, avatarFolder, gate, cancellationToken))).ConfigureAwait(continueOnCapturedContext: false)).ToList();
		}
		finally
		{
			if (gate != null)
			{
				((IDisposable)gate).Dispose();
			}
		}
	}

	private static async Task<SocialFriend> CacheSteamAvatarAsync(SocialFriend friend, string avatarFolder, SemaphoreSlim gate, CancellationToken cancellationToken)
	{
		if (friend.Source != SocialFriendSource.Steam || !TryCreateHttpUri(friend.AvatarPathOrUrl, out Uri uri))
		{
			return friend;
		}
		string text = SanitizeFileName(friend.Id.Replace("steam:", string.Empty));
		string avatarExtension = GetAvatarExtension(uri);
		string localPath = Path.Combine(avatarFolder, text + avatarExtension);
		if (!File.Exists(localPath))
		{
			await gate.WaitAsync(cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
			try
			{
				using HttpResponseMessage response = await Http.GetAsync(uri, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
				if (!response.IsSuccessStatusCode)
				{
					return friend;
				}
				if (!(response.Content.Headers.ContentType?.MediaType ?? string.Empty).StartsWith("image/", StringComparison.OrdinalIgnoreCase))
				{
					return friend;
				}
				await using Stream stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
				await using FileStream file = File.Create(localPath);
				await stream.CopyToAsync(file, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
			}
			catch
			{
				return friend;
			}
			finally
			{
				gate.Release();
			}
		}
		return CopySteamFriend(friend, localPath);
	}

	private static SocialFriend CopySteamFriend(SocialFriend friend, string avatarPath)
	{
		return new SocialFriend
		{
			Id = friend.Id,
			DisplayName = friend.DisplayName,
			Source = friend.Source,
			AvatarPathOrUrl = avatarPath,
			IsOnline = friend.IsOnline,
			StatusText = friend.StatusText,
			ActivityText = friend.ActivityText,
			ActivityAppId = friend.ActivityAppId,
			GamerscoreText = friend.GamerscoreText,
			ReputationText = friend.ReputationText,
			ZoneText = friend.ZoneText,
			IdentityDetailText = friend.IdentityDetailText,
			IsPartyHost = friend.IsPartyHost,
			ShowVoiceIndicator = friend.ShowVoiceIndicator
		};
	}

	private static bool TryCreateHttpUri(string path, out Uri uri)
	{
		if (Uri.TryCreate(path, UriKind.Absolute, out uri) && (uri.Scheme.Equals(Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase) || uri.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)))
		{
			return true;
		}
		uri = null;
		return false;
	}

	private static string GetAvatarExtension(Uri uri)
	{
		string extension = Path.GetExtension(uri.AbsolutePath);
		if (!extension.Equals(".png", StringComparison.OrdinalIgnoreCase) && !extension.Equals(".jpg", StringComparison.OrdinalIgnoreCase) && !extension.Equals(".jpeg", StringComparison.OrdinalIgnoreCase))
		{
			return ".jpg";
		}
		return extension;
	}

	private static string SanitizeFileName(string value)
	{
		char[] invalid = Path.GetInvalidFileNameChars();
		string text = new string(value.Select((char ch) => (!invalid.Contains(ch)) ? ch : '_').ToArray());
		if (!string.IsNullOrWhiteSpace(text))
		{
			return text;
		}
		return "steam-friend";
	}

	public async Task<SteamCommunityConfig> LoadConfigAsync(CancellationToken cancellationToken = default(CancellationToken))
	{
		if (!File.Exists(_configPath))
		{
			return new SteamCommunityConfig();
		}
		SteamCommunityConfig result;
		await using (FileStream stream = File.OpenRead(_configPath))
		{
			result = (await JsonSerializer.DeserializeAsync<SteamCommunityConfig>(stream, JsonOptions, cancellationToken).ConfigureAwait(continueOnCapturedContext: false)) ?? new SteamCommunityConfig();
		}
		return result;
	}

	private static bool HasCredentials(SteamCommunityConfig config)
	{
		if (!string.IsNullOrWhiteSpace(config.SteamApiKey))
		{
			return !string.IsNullOrWhiteSpace(config.SteamId64);
		}
		return false;
	}

	private static async Task<T?> GetJsonAsync<T>(string uri, CancellationToken cancellationToken)
	{
		T result;
		await using (Stream stream = await Http.GetStreamAsync(uri, cancellationToken).ConfigureAwait(continueOnCapturedContext: false))
		{
			result = await JsonSerializer.DeserializeAsync<T>(stream, JsonOptions, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
		}
		return result;
	}

	private async Task<T?> ReadFreshCacheAsync<T>(string path, TimeSpan maxAge, CancellationToken cancellationToken)
	{
		if (!File.Exists(path) || DateTimeOffset.UtcNow - File.GetLastWriteTimeUtc(path) > maxAge)
		{
			return default(T);
		}
		return await ReadCacheAsync<T>(path, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
	}

	private static async Task<T?> ReadCacheAsync<T>(string path, CancellationToken cancellationToken)
	{
		if (!File.Exists(path))
		{
			return default(T);
		}
		T result;
		await using (FileStream stream = File.OpenRead(path))
		{
			result = await JsonSerializer.DeserializeAsync<T>(stream, JsonOptions, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
		}
		return result;
	}

	private static async Task WriteCacheAsync<T>(string path, T value, CancellationToken cancellationToken)
	{
		Directory.CreateDirectory(Path.GetDirectoryName(path));
		await using FileStream stream = File.Create(path);
		await JsonSerializer.SerializeAsync(stream, value, JsonOptions, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
	}

	private void EnsureConfigExample()
	{
		string path = Path.Combine(AppPaths.UserDataFolder, "steam-web-config.example.json");
		if (!File.Exists(path))
		{
			SteamCommunityConfig value = new SteamCommunityConfig
			{
				SteamApiKey = "paste-your-steam-web-api-key-here",
				SteamId64 = "paste-your-steamid64-here"
			};
			File.WriteAllText(path, JsonSerializer.Serialize(value, JsonOptions));
		}
	}

	private static string SteamPersonaStateLabel(int state)
	{
		return state switch
		{
			1 => "Online", 
			2 => "Busy", 
			3 => "Away", 
			4 => "Snooze", 
			5 => "Looking to trade", 
			6 => "Looking to play", 
			_ => "Offline", 
		};
	}
}
