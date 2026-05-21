namespace XboxMetroLauncher.Models;

public sealed class GameLibrary
{
    public List<GameMetadata> Games { get; set; } = [];
    public List<string> LibraryPaths { get; set; } = [];
}
