using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Media;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using NAudio.Wave;
using XboxMetroLauncher.Utilities;

namespace XboxMetroLauncher.Services;

public sealed class AudioService : IAudioService
{
	private sealed record NaudioPlayback(WaveOutEvent Output, AudioFileReader Reader, string SoundName);

	private sealed class CachedWavePool : IDisposable
	{
		private readonly AudioFileReader?[] _readers;

		private readonly WaveOutEvent?[] _outputs;

		private readonly string _path;
		private readonly object _sync = new object();

		private int _nextIndex;
		private bool _disposed;

		public CachedWavePool(string path, int count)
		{
			_path = path;
			_readers = new AudioFileReader?[count];
			_outputs = new WaveOutEvent?[count];
			EnsureSlot(0);
		}

		private void EnsureSlot(int i)
		{
			if (_outputs[i] != null) return;
			AudioFileReader reader = new AudioFileReader(_path);
			WaveOutEvent output = new WaveOutEvent { DeviceNumber = -1 };
			try
			{
				output.Init(reader);
				_readers[i] = reader;
				_outputs[i] = output;
			}
			catch
			{
				output.Dispose();
				reader.Dispose();
				throw;
			}
		}

		public void Play(double volume)
		{
			lock (_sync)
			{
				ObjectDisposedException.ThrowIf(_disposed, this);
				int index = _nextIndex;
				// Reuse an idle output before allocating another overlapping voice.
				for (int i = 0; i < _outputs.Length; i++)
				{
					if (_outputs[i] == null || _outputs[i]!.PlaybackState == PlaybackState.Stopped)
					{
						index = i;
						break;
					}
				}
				EnsureSlot(index);
				_nextIndex = (index + 1) % _outputs.Length;
				WaveOutEvent output = _outputs[index]!;
				AudioFileReader reader = _readers[index]!;
				output.Stop();
				reader.Position = 0L;
				reader.Volume = (float)Math.Clamp(volume, 0.0, 1.0);
				output.Play();
			}
		}

		public void Dispose()
		{
			lock (_sync)
			{
				if (_disposed) return;
				_disposed = true;
				for (int i = 0; i < _outputs.Length; i++)
				{
					try
					{
						_outputs[i]?.Stop();
					}
					catch
					{
					}
					try
					{
						_outputs[i]?.Dispose();
					}
					catch
					{
					}
					try
					{
						_readers[i]?.Dispose();
					}
					catch
					{
					}
				}
			}
		}
	}

	private readonly Func<bool> _isEnabled;

	private readonly Func<string> _selectedOutputDeviceName;

	private readonly Func<double> _dashboardVolume;

	private readonly Func<string> _dashboardSoundTheme;

	private readonly Panel? _host;

	private readonly List<NaudioPlayback> _activeNaudioPlayers = new List<NaudioPlayback>();

	private readonly List<MediaPlayer> _activePlayers = new List<MediaPlayer>();

	private readonly List<MediaElement> _activeElements = new List<MediaElement>();

	private readonly Dictionary<MediaPlayer, string> _playerSoundNames = new Dictionary<MediaPlayer, string>();

	private readonly Dictionary<MediaElement, string> _elementSoundNames = new Dictionary<MediaElement, string>();

	private readonly Dictionary<string, DateTimeOffset> _lastPlayTimes = new Dictionary<string, DateTimeOffset>(StringComparer.OrdinalIgnoreCase);

	private readonly Dictionary<string, CachedWavePool> _cachedWavePlayers = new Dictionary<string, CachedWavePool>(StringComparer.OrdinalIgnoreCase);

	private readonly object _cachedWavePlayersLock = new object();

	private const int MaxActivePlayers = 8;

	private static readonly Dictionary<string, string[]> SoundFiles = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
	{
		["boot-intro"] = new string[1] { "02. Startup (2010).mp3" },
		["startup"] = new string[3] { "startup-after-loading.wav", "startup.wav", "02. Startup (2010).mp3" },
		["notify-popup"] = new string[1] { "notify-popup.wav" },
		["page-left"] = new string[4] { "09. Page Right.mp3", "swipe-left.wav", "08. Page Left.mp3", "tab.wav" },
		["page-right"] = new string[4] { "08. Page Left.mp3", "swipe-right.wav", "09. Page Right.mp3", "tab.wav" },
		["tab"] = new string[2] { "09. Page Right.mp3", "tab.wav" },
		["tab-switch-0"] = new string[2] { "09. Page Right.mp3", "tab.wav" },
		["tab-switch-1"] = new string[2] { "09. Page Right.mp3", "tab.wav" },
		["tab-switch-2"] = new string[2] { "09. Page Right.mp3", "tab.wav" },
		["tab-switch-3"] = new string[2] { "09. Page Right.mp3", "tab.wav" },
		["tab-switch-4"] = new string[2] { "09. Page Right.mp3", "tab.wav" },
		["tab-switch-5"] = new string[2] { "09. Page Right.mp3", "tab.wav" },
		["tab-switch-6"] = new string[2] { "09. Page Right.mp3", "tab.wav" },
		["select"] = new string[3] { "10. Select A.mp3", "13. Select.mp3", "select.wav" },
		["settings-box"] = new string[2] { "11. Select A (Alt).mp3", "10. Select A.mp3" },
		["menu-in"] = new string[4] { "select-into-menu.wav", "select-into-alt.wav", "10. Select A.mp3", "select.wav" },
		["guide-music-sources-load"] = new string[1] { "select-into-menu.wav" },
		["menu-out"] = new string[3] { "select-out-menu.wav", "14. Back.mp3", "back.wav" },
		["activate"] = new string[3] { "10. Select A.mp3", "13. Select.mp3", "select.wav" },
		["back"] = new string[3] { "14. Back.mp3", "15. Back 2.mp3", "back.wav" },
		["focus"] = new string[4] { "tile-hover.wav", "13. Select.mp3", "11. Select A (Alt).mp3", "focus.wav" },
		["hover"] = new string[4] { "tile-hover.wav", "13. Select.mp3", "11. Select A (Alt).mp3", "focus.wav" },
		["guide-open"] = new string[2] { "hud-open.wav", "10. Select A.mp3" },
		["guide-close"] = new string[2] { "hud-close.wav", "14. Back.mp3" },
		["guide-blade-open"] = new string[2] { "blade-open.wav", "hud-open.wav" },
		["guide-blade-switch-1"] = new string[2] { "blade-switch-1.wav", "09. Page Right.mp3" },
		["guide-blade-switch-2"] = new string[2] { "blade-switch-2.wav", "09. Page Right.mp3" },
		["guide-blade-switch-3"] = new string[2] { "blade-switch-3.wav", "09. Page Right.mp3" },
		["guide-blade-switch-4"] = new string[2] { "blade-switch-4.wav", "09. Page Right.mp3" },
		["guide-hover"] = new string[2] { "guide-hover.wav", "13. Select.mp3" },
		["guide-select"] = new string[2] { "guide-select.wav", "10. Select A.mp3" },
		["guide-type-1"] = new string[1] { "8 (xwb).wav" },
		["guide-type-2"] = new string[1] { "29 (xwb).wav" },
		["guide-type-3"] = new string[1] { "31 (xwb).wav" },
		["guide-type-backspace"] = new string[1] { "7 (xwb).wav" },
		["guide-back"] = new string[2] { "guide-back.wav", "14. Back.mp3" }
	};

	private static readonly Dictionary<string, string[]> BladesSoundFiles = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
	{
		["page-left"] = new string[1] { "blades-switch-live-to-games.wav" },
		["page-right"] = new string[1] { "blades-switch-games-to-media.wav" },
		["tab"] = new string[1] { "blades-switch-live-to-games.wav" },
		["tab-switch-0"] = new string[1] { "blades-switch-bing-to-home.wav" },
		["tab-switch-1"] = new string[1] { "blades-switch-home-to-social.wav" },
		["tab-switch-2"] = new string[1] { "blades-switch-social-to-media.wav" },
		["tab-switch-3"] = new string[1] { "blades-switch-media-to-games.wav" },
		["tab-switch-4"] = new string[1] { "blades-switch-games-to-music.wav" },
		["tab-switch-5"] = new string[1] { "blades-switch-music-to-apps.wav" },
		["tab-switch-6"] = new string[1] { "blades-switch-apps-to-settings.wav" },
		["select"] = new string[1] { "blades-select.wav" },
		["settings-box"] = new string[1] { "blades-select.wav" },
		["menu-in"] = new string[1] { "blades-menu-open.wav" },
		["guide-music-sources-load"] = new string[1] { "blades-menu-open.wav" },
		["menu-out"] = new string[1] { "blades-back.wav" },
		["activate"] = new string[1] { "blades-select.wav" },
		["back"] = new string[1] { "blades-back.wav" },
		["focus"] = new string[1] { "blades-hover.wav" },
		["hover"] = new string[1] { "blades-hover.wav" },
		["guide-open"] = new string[1] { "blades-guide-open.wav" },
		["guide-close"] = new string[1] { "blades-guide-close.wav" },
		["guide-hover"] = new string[1] { "blades-hover.wav" },
		["guide-select"] = new string[1] { "blades-select.wav" },
		["guide-back"] = new string[1] { "blades-back.wav" },
		["guide-blade-open"] = new string[1] { "blades-menu-open.wav" },
		["guide-blade-switch-1"] = new string[1] { "blades-switch-marketplace-to-live.wav" },
		["guide-blade-switch-2"] = new string[1] { "blades-switch-live-to-games.wav" },
		["guide-blade-switch-3"] = new string[1] { "blades-switch-games-to-media.wav" },
		["guide-blade-switch-4"] = new string[1] { "blades-switch-media-to-system.wav" }
	};

	public AudioService(Func<bool> isEnabled, Panel? host = null, Func<string>? selectedOutputDeviceName = null, Func<double>? dashboardVolume = null, Func<string>? dashboardSoundTheme = null)
	{
		_isEnabled = isEnabled;
		_host = host;
		_selectedOutputDeviceName = selectedOutputDeviceName ?? ((Func<string>)(() => "Default"));
		_dashboardVolume = dashboardVolume ?? ((Func<double>)(() => 1.0));
		_dashboardSoundTheme = dashboardSoundTheme ?? ((Func<string>)(() => "Metro"));
		PreloadLowLatencyWave("focus");
		PreloadLowLatencyWave("startup");
		PreloadLowLatencyWave("notify-popup");
		PreloadLowLatencyWave("guide-type-1");
		PreloadLowLatencyWave("guide-type-2");
		PreloadLowLatencyWave("guide-type-3");
		PreloadLowLatencyWave("guide-type-backspace");
	}

	public IReadOnlyList<string> GetOutputDeviceNames()
	{
		List<string> list = new List<string> { "Default" };
		try
		{
			Dictionary<string, int> dictionary = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
			int count = WaveInterop.waveOutGetNumDevs();
			int capsSize = Marshal.SizeOf<WaveOutCapabilities>();
			for (int i = 0; i < count; i++)
			{
				WaveOutCapabilities capabilities = default(WaveOutCapabilities);
				if (WaveInterop.waveOutGetDevCaps(new IntPtr(i), out capabilities, capsSize) != 0)
				{
					continue;
				}
				string text = capabilities.ProductName?.Trim() ?? string.Empty;
				if (string.IsNullOrWhiteSpace(text))
				{
					continue;
				}
				dictionary.TryGetValue(text, out var value);
				dictionary[text] = value + 1;
				list.Add((value == 0) ? text : $"{text} ({value + 1})");
			}
		}
		catch (Exception exception)
		{
			App.LogException(exception, "AudioService.GetOutputDeviceNames");
		}
		return list;
	}

	public void Play(string soundName)
	{
		if (!_isEnabled() || IsThrottled(soundName))
		{
			return;
		}
		string text = ResolveSoundPath(soundName);
		if (text == null)
		{
			return;
		}
		else
		{
			if (!IsDefaultOutputDevice(_selectedOutputDeviceName()) && TryPlayThroughNaudio(text, soundName))
			{
				return;
			}
			if (string.Equals(Path.GetExtension(text), ".wav", StringComparison.OrdinalIgnoreCase) && TryPlayCachedWave(text))
			{
				return;
			}
			if (_host == null)
			{
				if (string.Equals(Path.GetExtension(text), ".wav", StringComparison.OrdinalIgnoreCase))
				{
					using (SoundPlayer soundPlayer = new SoundPlayer(text))
					{
						soundPlayer.Play();
						return;
					}
				}
				MediaPlayer mediaPlayer = null;
				try
				{
					TrimActivePlayers();
					mediaPlayer = new MediaPlayer();
					mediaPlayer.MediaEnded += delegate
					{
						ClosePlayer(mediaPlayer);
					};
					mediaPlayer.MediaFailed += delegate
					{
						ClosePlayer(mediaPlayer);
					};
					_activePlayers.Add(mediaPlayer);
					_playerSoundNames[mediaPlayer] = soundName;
					mediaPlayer.Open(new Uri(text, UriKind.Absolute));
					mediaPlayer.Volume = GetDashboardVolume();
					mediaPlayer.Play();
					return;
				}
				catch
				{
					if (mediaPlayer != null)
					{
						ClosePlayer(mediaPlayer);
					}
					return;
				}
			}
			PlayThroughElement(text, soundName);
		}
	}

	private void PreloadLowLatencyWave(string soundName)
	{
		Task.Run(delegate
		{
			try
			{
				string text = ResolveSoundPath(soundName);
				if (!string.IsNullOrWhiteSpace(text) && string.Equals(Path.GetExtension(text), ".wav", StringComparison.OrdinalIgnoreCase))
				{
					GetCachedWavePlayer(text);
				}
			}
			catch
			{
			}
		});
	}

	private bool TryPlayCachedWave(string path)
	{
		try
		{
			if (TryGetCachedWavePlayer(path, out CachedWavePool? soundPlayer))
			{
				soundPlayer.Play(GetDashboardVolume());
				return true;
			}
			Task.Run(delegate
			{
				try
				{
					GetCachedWavePlayer(path).Play(GetDashboardVolume());
				}
				catch
				{
				}
			});
			return true;
		}
		catch
		{
			return false;
		}
	}

	public void WarmUp(string soundName)
	{
		PreloadLowLatencyWave(soundName);
	}

	public void TrimCachedResources(bool keepGuideReady)
	{
		lock (_cachedWavePlayersLock)
		{
			foreach (KeyValuePair<string, CachedWavePool> pair in _cachedWavePlayers.ToList())
			{
				string fileName = Path.GetFileName(pair.Key);
				if (keepGuideReady && IsGuideSoundFile(fileName))
				{
					continue;
				}
				pair.Value.Dispose();
				_cachedWavePlayers.Remove(pair.Key);
			}
		}
	}

	private bool TryGetCachedWavePlayer(string path, out CachedWavePool? soundPlayer)
	{
		lock (_cachedWavePlayersLock)
		{
			return _cachedWavePlayers.TryGetValue(path, out soundPlayer);
		}
	}

	private CachedWavePool GetCachedWavePlayer(string path)
	{
		lock (_cachedWavePlayersLock)
		{
			if (!_cachedWavePlayers.TryGetValue(path, out CachedWavePool soundPlayer))
			{
				soundPlayer = null;
			}
			else
			{
				return soundPlayer;
			}
		}
		CachedWavePool createdPlayer = new CachedWavePool(path, GetCachedWavePoolSize(path));
		lock (_cachedWavePlayersLock)
		{
			if (_cachedWavePlayers.TryGetValue(path, out CachedWavePool existingPlayer))
			{
				createdPlayer.Dispose();
				return existingPlayer;
			}
			_cachedWavePlayers[path] = createdPlayer;
			return createdPlayer;
		}
	}

	private static int GetCachedWavePoolSize(string path)
	{
		string fileName = Path.GetFileName(path);
		if (string.Equals(fileName, "tile-hover.wav", StringComparison.OrdinalIgnoreCase) || string.Equals(fileName, "focus.wav", StringComparison.OrdinalIgnoreCase))
		{
			return 10;
		}
		if (string.Equals(fileName, "guide-hover.wav", StringComparison.OrdinalIgnoreCase))
		{
			return 4;
		}
		if (fileName.StartsWith("blade-switch-", StringComparison.OrdinalIgnoreCase))
		{
			return 3;
		}
		return 2;
	}

	private static bool IsGuideSoundFile(string fileName)
	{
		return string.Equals(fileName, "hud-open.wav", StringComparison.OrdinalIgnoreCase)
			|| string.Equals(fileName, "hud-close.wav", StringComparison.OrdinalIgnoreCase)
			|| string.Equals(fileName, "guide-hover.wav", StringComparison.OrdinalIgnoreCase)
			|| string.Equals(fileName, "guide-select.wav", StringComparison.OrdinalIgnoreCase)
			|| string.Equals(fileName, "guide-back.wav", StringComparison.OrdinalIgnoreCase)
			|| string.Equals(fileName, "blade-open.wav", StringComparison.OrdinalIgnoreCase)
			|| fileName.StartsWith("blade-switch-", StringComparison.OrdinalIgnoreCase);
	}

	public void Stop(string soundName)
	{
		foreach (MediaPlayer item in (from pair in _playerSoundNames
			where string.Equals(pair.Value, soundName, StringComparison.OrdinalIgnoreCase)
			select pair.Key).ToList())
		{
			ClosePlayer(item);
		}
		foreach (MediaElement item2 in (from pair in _elementSoundNames
			where string.Equals(pair.Value, soundName, StringComparison.OrdinalIgnoreCase)
			select pair.Key).ToList())
		{
			CloseElement(item2);
		}
		foreach (NaudioPlayback item3 in _activeNaudioPlayers.Where((NaudioPlayback playback) => string.Equals(playback.SoundName, soundName, StringComparison.OrdinalIgnoreCase)).ToList())
		{
			CloseNaudioPlayback(item3);
		}
	}

	private bool TryPlayThroughNaudio(string path, string soundName)
	{
		AudioFileReader audioFileReader = null;
		WaveOutEvent waveOutEvent = null;
		try
		{
			TrimActiveNaudioPlayers();
			audioFileReader = new AudioFileReader(path);
			audioFileReader.Volume = (float)GetDashboardVolume();
			waveOutEvent = new WaveOutEvent
			{
				DeviceNumber = ResolveOutputDeviceNumber(_selectedOutputDeviceName())
			};
			NaudioPlayback playback = new NaudioPlayback(waveOutEvent, audioFileReader, soundName);
			waveOutEvent.PlaybackStopped += delegate
			{
				CloseNaudioPlayback(playback);
			};
			waveOutEvent.Init(audioFileReader);
			_activeNaudioPlayers.Add(playback);
			waveOutEvent.Play();
			return true;
		}
		catch (Exception exception)
		{
			App.LogException(exception, "AudioService.NAudio");
			try
			{
				waveOutEvent?.Dispose();
				audioFileReader?.Dispose();
			}
			catch
			{
			}
			return false;
		}
	}

	private static bool IsDefaultOutputDevice(string? deviceName)
	{
		if (!string.IsNullOrWhiteSpace(deviceName))
		{
			return string.Equals(deviceName, "Default", StringComparison.OrdinalIgnoreCase);
		}
		return true;
	}

	private int ResolveOutputDeviceNumber(string? deviceName)
	{
		if (string.IsNullOrWhiteSpace(deviceName) || string.Equals(deviceName, "Default", StringComparison.OrdinalIgnoreCase))
		{
			return -1;
		}
		string[] names = GetOutputDeviceNames().Skip(1).ToArray();
		for (int i = 0; i < names.Length; i++)
		{
			if (string.Equals(names[i], deviceName, StringComparison.OrdinalIgnoreCase))
			{
				return i;
			}
		}
		return -1;
	}

	private void PlayThroughElement(string path, string soundName)
	{
		Panel? host = _host;
		object obj = ((host != null) ? ((DispatcherObject)host).Dispatcher : null);
		if (obj == null)
		{
			Application current = Application.Current;
			obj = ((current != null) ? ((DispatcherObject)current).Dispatcher : null);
		}
		Dispatcher val = (Dispatcher)obj;
		if (val != null && !val.CheckAccess())
		{
			val.BeginInvoke((Delegate)(Action)delegate
			{
				PlayThroughElement(path, soundName);
			}, Array.Empty<object>());
		}
		else
		{
			if (_host == null)
			{
				return;
			}
			try
			{
				TrimActiveElements();
				MediaElement element = new MediaElement
				{
					Width = 1.0,
					Height = 1.0,
					Opacity = 0.0,
					IsHitTestVisible = false,
					LoadedBehavior = MediaState.Manual,
					UnloadedBehavior = MediaState.Manual,
					Volume = GetDashboardVolume(),
					Source = new Uri(path, UriKind.Absolute)
				};
				element.MediaEnded += delegate
				{
					CloseElement(element);
				};
				element.MediaFailed += delegate
				{
					CloseElement(element);
				};
				_activeElements.Add(element);
				_elementSoundNames[element] = soundName;
				_host.Children.Add(element);
				element.Play();
			}
			catch
			{
			}
		}
	}

	private string? ResolveSoundPath(string soundName)
	{
		string[] value;
		Dictionary<string, string[]> soundFiles = IsBladesSoundTheme() ? BladesSoundFiles : SoundFiles;
		string[] array = (soundFiles.TryGetValue(soundName, out value) || SoundFiles.TryGetValue(soundName, out value) ? value : new string[2]
		{
			soundName + ".mp3",
			soundName + ".wav"
		});
		foreach (string item in AppPaths.CandidateRoots().SelectMany((string root) => new string[3]
		{
			Path.Combine(root, "Assets", "Audio", "Sounds"),
			Path.Combine(root, "sounds"),
			Path.Combine(root, "Assets", "Audio")
		}).Distinct<string>(StringComparer.OrdinalIgnoreCase)
			.ToList())
		{
			string[] array2 = array;
			foreach (string path in array2)
			{
				string text = Path.Combine(item, path);
				if (File.Exists(text))
				{
					return text;
				}
			}
		}
		return null;
	}

	private bool IsBladesSoundTheme()
	{
		try
		{
			return string.Equals(_dashboardSoundTheme(), "Blades", StringComparison.OrdinalIgnoreCase);
		}
		catch
		{
			return false;
		}
	}

	private void ClosePlayer(MediaPlayer mediaPlayer)
	{
		mediaPlayer.Close();
		_activePlayers.Remove(mediaPlayer);
		_playerSoundNames.Remove(mediaPlayer);
	}

	private void TrimActivePlayers()
	{
		while (_activePlayers.Count >= 8)
		{
			ClosePlayer(_activePlayers[0]);
		}
	}

	private double GetDashboardVolume()
	{
		try
		{
			return Math.Clamp(_dashboardVolume(), 0.0, 1.0);
		}
		catch
		{
			return 1.0;
		}
	}

	private void CloseNaudioPlayback(NaudioPlayback playback)
	{
		if (_activeNaudioPlayers.Remove(playback))
		{
			try
			{
				playback.Output.Stop();
			}
			catch
			{
			}
			playback.Output.Dispose();
			playback.Reader.Dispose();
		}
	}

	private void TrimActiveNaudioPlayers()
	{
		while (_activeNaudioPlayers.Count >= 8)
		{
			CloseNaudioPlayback(_activeNaudioPlayers[0]);
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
			_elementSoundNames.Remove(element);
		}
		catch
		{
		}
	}

	private void TrimActiveElements()
	{
		while (_activeElements.Count >= 8)
		{
			CloseElement(_activeElements[0]);
		}
	}

	private bool IsThrottled(string soundName)
	{
		TimeSpan timeSpan;
		switch (soundName)
		{
		case "select":
			timeSpan = TimeSpan.FromMilliseconds(80.0);
			break;
		case "menu-in":
		case "menu-out":
			timeSpan = TimeSpan.FromMilliseconds(120.0);
			break;
		case "guide-open":
		case "guide-close":
		case "guide-blade-open":
		case "guide-select":
		case "guide-back":
			timeSpan = TimeSpan.FromMilliseconds(60.0);
			break;
		case "focus":
		case "guide-hover":
		case "guide-blade-switch-1":
		case "guide-blade-switch-2":
		case "guide-blade-switch-3":
		case "guide-blade-switch-4":
			timeSpan = TimeSpan.Zero;
			break;
		case "page-left":
		case "page-right":
		case "tab":
		case "tab-switch-0":
		case "tab-switch-1":
		case "tab-switch-2":
		case "tab-switch-3":
		case "tab-switch-4":
		case "tab-switch-5":
		case "tab-switch-6":
			timeSpan = TimeSpan.FromMilliseconds(150.0);
			break;
		default:
			timeSpan = TimeSpan.Zero;
			break;
		}
		TimeSpan timeSpan2 = timeSpan;
		if (timeSpan2 == TimeSpan.Zero)
		{
			return false;
		}
		DateTimeOffset utcNow = DateTimeOffset.UtcNow;
		if (_lastPlayTimes.TryGetValue(soundName, out var value) && utcNow - value < timeSpan2)
		{
			return true;
		}
		_lastPlayTimes[soundName] = utcNow;
		return false;
	}
}
