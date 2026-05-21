using System.IO;
using System.Media;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using XboxMetroLauncher.Utilities;

namespace XboxMetroLauncher.Services;

public sealed class AudioService : IAudioService
{
    private readonly Func<bool> _isEnabled;
    private readonly Panel? _host;
    private readonly List<MediaPlayer> _activePlayers = [];
    private readonly List<MediaElement> _activeElements = [];
    private readonly Dictionary<string, DateTimeOffset> _lastPlayTimes = new(StringComparer.OrdinalIgnoreCase);
    private const int MaxActivePlayers = 8;

    private static readonly Dictionary<string, string[]> SoundFiles = new(StringComparer.OrdinalIgnoreCase)
    {
        ["startup"] = ["02. Startup (2010).mp3", "startup.wav"],
        ["page-left"] = ["08. Page Left.mp3", "tab.wav"],
        ["page-right"] = ["09. Page Right.mp3", "tab.wav"],
        ["tab"] = ["09. Page Right.mp3", "tab.wav"],
        ["select"] = ["10. Select A.mp3", "13. Select.mp3", "select.wav"],
        ["activate"] = ["10. Select A.mp3", "13. Select.mp3", "select.wav"],
        ["back"] = ["14. Back.mp3", "15. Back 2.mp3", "back.wav"],
        ["focus"] = ["13. Select.mp3", "11. Select A (Alt).mp3", "focus.wav"]
    };

    public AudioService(Func<bool> isEnabled, Panel? host = null)
    {
        _isEnabled = isEnabled;
        _host = host;
    }

    public void Play(string soundName)
    {
        if (!_isEnabled())
        {
            return;
        }

        if (IsThrottled(soundName))
        {
            return;
        }

        var path = ResolveSoundPath(soundName);
        if (path is null)
        {
            SystemSounds.Asterisk.Play();
            return;
        }

        if (_host is not null)
        {
            PlayThroughElement(path);
            return;
        }

        if (string.Equals(Path.GetExtension(path), ".wav", StringComparison.OrdinalIgnoreCase))
        {
            using var player = new SoundPlayer(path);
            player.Play();
            return;
        }

        MediaPlayer? mediaPlayer = null;
        try
        {
            TrimActivePlayers();
            mediaPlayer = new MediaPlayer();
            mediaPlayer.MediaEnded += (_, _) => ClosePlayer(mediaPlayer);
            mediaPlayer.MediaFailed += (_, _) => ClosePlayer(mediaPlayer);
            _activePlayers.Add(mediaPlayer);
            mediaPlayer.Open(new Uri(path, UriKind.Absolute));
            mediaPlayer.Play();
        }
        catch
        {
            if (mediaPlayer is not null)
            {
                ClosePlayer(mediaPlayer);
            }
        }
    }

    private void PlayThroughElement(string path)
    {
        var dispatcher = _host?.Dispatcher ?? Application.Current?.Dispatcher;
        if (dispatcher is not null && !dispatcher.CheckAccess())
        {
            dispatcher.BeginInvoke(() => PlayThroughElement(path));
            return;
        }

        if (_host is null)
        {
            return;
        }

        try
        {
            TrimActiveElements();

            var element = new MediaElement
            {
                Width = 1,
                Height = 1,
                Opacity = 0,
                IsHitTestVisible = false,
                LoadedBehavior = MediaState.Manual,
                UnloadedBehavior = MediaState.Manual,
                Volume = 1,
                Source = new Uri(path, UriKind.Absolute)
            };

            element.MediaEnded += (_, _) => CloseElement(element);
            element.MediaFailed += (_, _) => CloseElement(element);
            _activeElements.Add(element);
            _host.Children.Add(element);
            element.Play();
        }
        catch
        {
        }
    }

    private static string? ResolveSoundPath(string soundName)
    {
        var fileNames = SoundFiles.TryGetValue(soundName, out var mappedFiles)
            ? mappedFiles
            : [$"{soundName}.mp3", $"{soundName}.wav"];

        var searchRoots = AppPaths.CandidateRoots()
            .SelectMany(root => new[]
            {
                Path.Combine(root, "Assets", "Audio", "Sounds"),
                Path.Combine(root, "sounds"),
                Path.Combine(root, "Assets", "Audio")
            })
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        foreach (var root in searchRoots)
        {
            foreach (var fileName in fileNames)
            {
                var path = Path.Combine(root, fileName);
                if (File.Exists(path))
                {
                    return path;
                }
            }
        }

        return null;
    }

    private void ClosePlayer(MediaPlayer mediaPlayer)
    {
        mediaPlayer.Close();
        _activePlayers.Remove(mediaPlayer);
    }

    private void TrimActivePlayers()
    {
        while (_activePlayers.Count >= MaxActivePlayers)
        {
            ClosePlayer(_activePlayers[0]);
        }
    }

    private void CloseElement(MediaElement element)
    {
        try
        {
            element.Stop();
            element.Source = null;
            _host?.Children.Remove(element);
            _activeElements.Remove(element);
        }
        catch
        {
        }
    }

    private void TrimActiveElements()
    {
        while (_activeElements.Count >= MaxActivePlayers)
        {
            CloseElement(_activeElements[0]);
        }
    }

    private bool IsThrottled(string soundName)
    {
        var minimumDelay = soundName switch
        {
            "focus" => TimeSpan.FromMilliseconds(110),
            "page-left" or "page-right" or "tab" => TimeSpan.FromMilliseconds(150),
            _ => TimeSpan.Zero
        };

        if (minimumDelay == TimeSpan.Zero)
        {
            return false;
        }

        var now = DateTimeOffset.UtcNow;
        if (_lastPlayTimes.TryGetValue(soundName, out var lastPlay) && now - lastPlay < minimumDelay)
        {
            return true;
        }

        _lastPlayTimes[soundName] = now;
        return false;
    }
}
