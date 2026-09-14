using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using XboxMetroLauncher.Utilities;

namespace XboxMetroLauncher.Services;

internal static class ProfileImagePool
{
	private static readonly HashSet<string> SupportedImageExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
	{
		".png",
		".jpg",
		".jpeg",
		".bmp",
		".gif"
	};

	private static readonly object SyncRoot = new object();

	private static readonly Random Random = new Random();

	public static string GetDefaultAvatarPath()
	{
		string text = AppPaths.FindFile(Path.Combine("Assets", "Profile", "profilepicture.jpg"));
		if (File.Exists(text))
		{
			return text;
		}
		string text2 = AppPaths.FindFile(Path.Combine("Assets", "Art", "profilepicture.jpg"));
		if (!File.Exists(text2))
		{
			return text;
		}
		return text2;
	}

	public static bool NeedsAssignedPoolImage(string? currentPath)
	{
		if (!TryResolveExistingAvatarPath(currentPath, out string path))
		{
			return true;
		}
		string fullPath = Path.GetFullPath(path);
		string fullPath2 = Path.GetFullPath(GetDefaultAvatarPath());
		if (string.Equals(fullPath, fullPath2, StringComparison.OrdinalIgnoreCase))
		{
			return true;
		}
		return fullPath.EndsWith(Path.Combine("Assets", "Art", "profilepicture.jpg"), StringComparison.OrdinalIgnoreCase);
	}

	public static string ResolveAssignedAvatarPath(string? currentPath)
	{
		if (!NeedsAssignedPoolImage(currentPath) && TryResolveExistingAvatarPath(currentPath, out string path))
		{
			return path;
		}
		return GetRandomPoolAvatarPath();
	}

	public static string GetRandomPoolAvatarPath()
	{
		List<string> availablePoolPaths = GetAvailablePoolPaths();
		if (availablePoolPaths.Count == 0)
		{
			return GetDefaultAvatarPath();
		}
		lock (SyncRoot)
		{
			return availablePoolPaths[Random.Next(availablePoolPaths.Count)];
		}
	}

	private static List<string> GetAvailablePoolPaths()
	{
		string path = AppPaths.FindFolder(Path.Combine("Assets", "Profile", "FriendPool"));
		if (!Directory.Exists(path))
		{
			return new List<string>();
		}
		return Directory.EnumerateFiles(path)
			.Where(file => SupportedImageExtensions.Contains(Path.GetExtension(file)))
			.OrderBy(file => Path.GetFileName(file), StringComparer.OrdinalIgnoreCase)
			.ToList();
	}

	private static bool TryResolveExistingAvatarPath(string? currentPath, out string path)
	{
		path = string.Empty;
		if (string.IsNullOrWhiteSpace(currentPath))
		{
			return false;
		}
		string resolved = AppPaths.ResolvePath(currentPath);
		if (File.Exists(resolved))
		{
			path = resolved;
			return true;
		}
		string fileName = Path.GetFileName(currentPath);
		if (!string.IsNullOrWhiteSpace(fileName))
		{
			string poolPath = Path.Combine(AppPaths.FindFolder(Path.Combine("Assets", "Profile", "FriendPool")), fileName);
			if (File.Exists(poolPath))
			{
				path = poolPath;
				return true;
			}
		}
		return false;
	}
}
