using System.Diagnostics;
using System.IO;
using XboxMetroLauncher.Models;

namespace XboxMetroLauncher.Services;

public sealed class GameLaunchService : IGameLaunchService
{
    public Task LaunchAsync(GameMetadata game, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(game.ExecutablePath))
        {
            throw new InvalidOperationException($"'{game.Title}' does not have an executable path.");
        }

        var workingDirectory = string.IsNullOrWhiteSpace(game.WorkingDirectory)
            ? Path.GetDirectoryName(game.ExecutablePath)
            : game.WorkingDirectory;

        var startInfo = new ProcessStartInfo
        {
            FileName = game.ExecutablePath,
            Arguments = game.Arguments,
            WorkingDirectory = workingDirectory ?? string.Empty,
            UseShellExecute = true
        };

        Process.Start(startInfo);
        return Task.CompletedTask;
    }
}
