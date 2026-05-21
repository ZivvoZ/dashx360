using System.IO;

namespace XboxMetroLauncher.ViewModels;

public sealed class MusicTrackViewModel : ObservableObject
{
    private bool _isPlaying;

    public MusicTrackViewModel(string path)
    {
        Path = path;
        Title = System.IO.Path.GetFileNameWithoutExtension(path).Replace('_', ' ');
    }

    public string Path { get; }
    public string Title { get; }

    public bool IsPlaying
    {
        get => _isPlaying;
        set => SetProperty(ref _isPlaying, value);
    }
}
