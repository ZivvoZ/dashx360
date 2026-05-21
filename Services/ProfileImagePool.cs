using XboxMetroLauncher.Utilities;

namespace XboxMetroLauncher.Services;

internal static class ProfileImagePool
{
    private static readonly string[] PoolFileNames =
    [
        "2000c.png",
        "20002.png",
        "20003.png",
        "20006.png",
        "20007.png",
        "20008.png",
        "20009.png",
        "2000a.png"
    ];

    private static readonly object SyncRoot = new();
    private static readonly Random Random = new();

    public static string GetDefaultAvatarPath()
    {
        var profileDefault = AppPaths.FindFile(Path.Combine("Assets", "Profile", "profilepicture.jpg"));
        if (File.Exists(profileDefault))
        {
            return profileDefault;
        }

        var artDefault = AppPaths.FindFile(Path.Combine("Assets", "Art", "profilepicture.jpg"));
        return File.Exists(artDefault) ? artDefault : profileDefault;
    }

    public static bool NeedsAssignedPoolImage(string? currentPath)
    {
        if (string.IsNullOrWhiteSpace(currentPath))
        {
            return true;
        }

        var resolvedPath = AppPaths.ResolvePath(currentPath);
        if (!File.Exists(resolvedPath))
        {
            return true;
        }

        var normalized = Path.GetFullPath(resolvedPath);
        var defaultAvatar = Path.GetFullPath(GetDefaultAvatarPath());
        if (string.Equals(normalized, defaultAvatar, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return normalized.EndsWith(Path.Combine("Assets", "Art", "profilepicture.jpg"), StringComparison.OrdinalIgnoreCase);
    }

    public static string ResolveAssignedAvatarPath(string? currentPath)
        => NeedsAssignedPoolImage(currentPath) ? GetRandomPoolAvatarPath() : AppPaths.ResolvePath(currentPath!);

    public static string GetRandomPoolAvatarPath()
    {
        var availablePaths = GetAvailablePoolPaths();
        if (availablePaths.Count == 0)
        {
            return GetDefaultAvatarPath();
        }

        lock (SyncRoot)
        {
            return availablePaths[Random.Next(availablePaths.Count)];
        }
    }

    private static List<string> GetAvailablePoolPaths()
    {
        var poolFolder = AppPaths.FindFolder(Path.Combine("Assets", "Profile", "FriendPool"));
        var availablePaths = new List<string>();
        foreach (var fileName in PoolFileNames)
        {
            var path = Path.Combine(poolFolder, fileName);
            if (File.Exists(path))
            {
                availablePaths.Add(path);
            }
        }

        return availablePaths;
    }
}
