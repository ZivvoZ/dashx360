using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace XboxMetroLauncher.Controls;

public sealed class BladesArtworkHost : Canvas
{
	private const double DesignWidth = 1920.0;
	private const double DesignHeight = 1080.0;
	private const string AssetRoot = "Assets/Blades/Separated";

	private static readonly string[] StateKeys =
	{
		"marketplace",
		"xbox live",
		"games",
		"media",
		"system"
	};

	private static readonly LayerDefinition[] Layers =
	{
		new("MarketplaceBladeImage", "Marketplace", "marketplace"),
		new("XboxLiveBladeImage", "Xbox LIVE", "xbox live"),
		new("GamesBladeImage", "Games", "games"),
		new("MediaBladeImage", "Media", "media"),
		new("SystemBladeImage", "System", "system"),
		new("LeftRunnerImage", "Left runner", "left runner"),
		new("RightRunnerImage", "Right runner", "right runner")
	};

	private readonly Dictionary<string, Image> _images = new(StringComparer.OrdinalIgnoreCase);
	private readonly Dictionary<string, TranslateTransform> _transforms = new(StringComparer.OrdinalIgnoreCase);
	private readonly Dictionary<string, RectangleGeometry> _clips = new(StringComparer.OrdinalIgnoreCase);
	private readonly Dictionary<string, Image> _incomingImages = new(StringComparer.OrdinalIgnoreCase);
	private readonly Dictionary<string, TranslateTransform> _incomingTransforms = new(StringComparer.OrdinalIgnoreCase);
	private readonly Dictionary<string, RectangleGeometry> _incomingClips = new(StringComparer.OrdinalIgnoreCase);
	private readonly Dictionary<string, SourceInfo> _sourceCache = new(StringComparer.OrdinalIgnoreCase);
	private readonly IReadOnlyDictionary<string, BladeStateDefinition> _states;
	private string _currentStateKey = "xbox live";
	private string? _queuedStateKey;
	private EventHandler? _navigationRendering;
	private bool _isAnimating;
	public event Action<string, string, double, double, double>? NavigationFrame;
	public event Action? NavigationCompleted;
	public Geometry? NavigationOutgoingClip { get; private set; }
	private readonly Dictionary<string, Geometry> _boundaryCache = new();

	public static readonly DependencyProperty FocusedBladeKeyProperty = DependencyProperty.Register(
		nameof(FocusedBladeKey),
		typeof(string),
		typeof(BladesArtworkHost),
		new PropertyMetadata("xbox live", OnFocusedBladeKeyChanged));

	public static readonly DependencyProperty StartupOpenProgressProperty = DependencyProperty.Register(
		nameof(StartupOpenProgress),
		typeof(double),
		typeof(BladesArtworkHost),
		new PropertyMetadata(1.0, OnStartupOpenProgressChanged));

	public static readonly DependencyProperty StartupElapsedSecondsProperty = DependencyProperty.Register(
		nameof(StartupElapsedSeconds),
		typeof(double),
		typeof(BladesArtworkHost),
		new PropertyMetadata(1.167, OnStartupElapsedSecondsChanged));

	public string FocusedBladeKey
	{
		get => (string)GetValue(FocusedBladeKeyProperty);
		set => SetValue(FocusedBladeKeyProperty, value);
	}

	public double StartupOpenProgress
	{
		get => (double)GetValue(StartupOpenProgressProperty);
		set => SetValue(StartupOpenProgressProperty, value);
	}

	public double StartupElapsedSeconds
	{
		get => (double)GetValue(StartupElapsedSecondsProperty);
		set => SetValue(StartupElapsedSecondsProperty, value);
	}

	public static readonly DependencyProperty SubmenuExpansionProperty = DependencyProperty.Register(
		nameof(SubmenuExpansion), typeof(double), typeof(BladesArtworkHost),
		new PropertyMetadata(0.0, (d,e) => ((BladesArtworkHost)d).ApplySubmenuExpansion((double)e.NewValue)));

	public double SubmenuExpansion { get => (double)GetValue(SubmenuExpansionProperty); set => SetValue(SubmenuExpansionProperty,value); }

	private void ApplySubmenuExpansion(double progress)
	{
		int focused = StateIndexFromKey(FocusedBladeKey);
		foreach (var layer in Layers)
		{
			bool left = layer.Key == "left runner" || (layer.Key != "right runner" && StateIndexFromKey(layer.Key) <= focused);
			_transforms[layer.Key].X = (left ? -800 : 800) * progress;
		}
	}

	public BladesArtworkHost()
	{
		Width = DesignWidth;
		Height = DesignHeight;
		ClipToBounds = false;
		IsHitTestVisible = false;
		HorizontalAlignment = HorizontalAlignment.Left;
		VerticalAlignment = VerticalAlignment.Top;
		RenderTransformOrigin = new Point(0, 0);
		SnapsToDevicePixels = true;
		UseLayoutRounding = true;
		RenderOptions.SetBitmapScalingMode(this, BitmapScalingMode.HighQuality);

		_states = BuildStates();
		foreach (LayerDefinition layer in Layers)
		{
			TranslateTransform transform = new();
			Image image = CreateLayerImage(layer.ImageName, transform);
			RectangleGeometry clip = new(new Rect(0, 0, DesignWidth, DesignHeight));
			image.Clip = clip;
			_images[layer.Key] = image;
			_transforms[layer.Key] = transform;
			_clips[layer.Key] = clip;
			Children.Add(image);
		}

		foreach (LayerDefinition layer in Layers)
		{
			TranslateTransform transform = new();
			Image image = CreateLayerImage(layer.ImageName + "Incoming", transform);
			RectangleGeometry clip = new(new Rect(0, 0, DesignWidth, DesignHeight));
			image.Clip = clip;
			image.Opacity = 1;
			image.Visibility = Visibility.Collapsed;
			_incomingImages[layer.Key] = image;
			_incomingTransforms[layer.Key] = transform;
			_incomingClips[layer.Key] = clip;
			Children.Add(image);
		}

		Loaded += async (_, _) =>
		{
			if (!_isAnimating) ApplyStaticState(StateIndexFromKey(FocusedBladeKey));
			foreach (BladeStateDefinition state in _states.Values)
			{
				foreach (string source in state.LayerSources.Values)
				{
					if (_sourceCache.ContainsKey(source)) continue;
					if (!IsLoaded) return;
					try
					{
						SourceInfo info = await System.Threading.Tasks.Task.Run(() => DecodeSourceInfo(source));
						_sourceCache.TryAdd(source, info);
					}
					catch (IOException exception)
					{
						Debug.WriteLine($"Blades asset preload: {exception.Message}");
					}
				}
			}
		};
		Unloaded += (_, _) =>
		{
			StopNavigationRendering();
			if (_isAnimating) NavigationCompleted?.Invoke();
			_isAnimating = false;
			_queuedStateKey = null;
		};
		ApplyStaticState(StateIndexFromKey(FocusedBladeKey));
	}

	private static Image CreateLayerImage(string name, TranslateTransform transform)
	{
		Image image = new()
		{
			Name = name,
			Width = DesignWidth,
			Height = DesignHeight,
			Stretch = Stretch.None,
			HorizontalAlignment = HorizontalAlignment.Left,
			VerticalAlignment = VerticalAlignment.Top,
			IsHitTestVisible = false,
			RenderTransformOrigin = new Point(0, 0),
			RenderTransform = transform
		};

		RenderOptions.SetBitmapScalingMode(image, BitmapScalingMode.HighQuality);
		SetLeft(image, 0);
		SetTop(image, 0);
		return image;
	}

	private static void OnFocusedBladeKeyChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs e)
	{
		if (dependencyObject is BladesArtworkHost host)
		{
			host.GoToState(StateIndexFromKey(e.NewValue as string));
		}
	}

	private static void OnStartupOpenProgressChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs e)
	{
		if (dependencyObject is BladesArtworkHost host)
		{
			host.ApplyStartupOpenProgress((double)e.NewValue);
		}
	}

	private static void OnStartupElapsedSecondsChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs e)
	{
		if (dependencyObject is BladesArtworkHost host)
		{
			host.ApplyStartupOpenProgress(host.StartupOpenProgress);
		}
	}

	private void GoToState(int stateIndex)
	{
		string targetStateKey = StateKeys[stateIndex];
		if (!IsLoaded)
		{
			ApplyStaticState(stateIndex);
			return;
		}
		if (_isAnimating)
		{
			_queuedStateKey = targetStateKey;
			return;
		}
		if (string.Equals(targetStateKey, _currentStateKey, StringComparison.OrdinalIgnoreCase)) return;

		AnimateToState(targetStateKey);
	}

	private void ApplyStaticState(int stateIndex)
	{
		StopNavigationRendering();
		string stateKey = StateKeys[stateIndex];
		_currentStateKey = stateKey;
		BladeStateDefinition state = _states[stateKey];

		foreach (LayerDefinition layer in Layers)
		{
			if (!state.LayerSources.TryGetValue(layer.Key, out string? source))
			{
				throw new InvalidOperationException($"Missing separated blade asset mapping for state '{stateKey}', layer '{layer.Key}'.");
			}

			SourceInfo sourceInfo = LoadSourceInfo(source);
			Image image = _images[layer.Key];
			TranslateTransform transform = _transforms[layer.Key];
			RectangleGeometry clip = _clips[layer.Key];
			transform.BeginAnimation(TranslateTransform.XProperty, null);
			transform.BeginAnimation(TranslateTransform.YProperty, null);
			image.BeginAnimation(OpacityProperty, null);
			clip.BeginAnimation(RectangleGeometry.RectProperty, null);
			image.Source = sourceInfo.Artwork;
			image.Opacity = 1;
			image.Visibility = Visibility.Visible;
			clip.Rect = new Rect(0, 0, DesignWidth, DesignHeight);
			SetLeft(image, 0);
			SetTop(image, 0);
			SetZIndex(image, state.LayerZOrder[layer.Key]);
			transform.X = 0;
			transform.Y = 0;

			Image incomingImage = _incomingImages[layer.Key];
			TranslateTransform incomingTransform = _incomingTransforms[layer.Key];
			RectangleGeometry incomingClip = _incomingClips[layer.Key];
			incomingTransform.BeginAnimation(TranslateTransform.XProperty, null);
			incomingTransform.BeginAnimation(TranslateTransform.YProperty, null);
			incomingImage.BeginAnimation(OpacityProperty, null);
			incomingClip.BeginAnimation(RectangleGeometry.RectProperty, null);
			incomingImage.Source = null;
			incomingImage.Opacity = 1;
			incomingImage.Visibility = Visibility.Collapsed;
			incomingClip.Rect = new Rect(0, 0, DesignWidth, DesignHeight);
			SetZIndex(incomingImage, state.LayerZOrder[layer.Key]);
			incomingTransform.X = 0;
			incomingTransform.Y = 0;

			WriteDiagnostic(stateKey, layer, source, sourceInfo.Bitmap, image, transform);
		}

		ApplyStartupOpenProgress(StartupOpenProgress);
	}

	private void ApplyStartupOpenProgress(double progress)
	{
		progress = Math.Clamp(progress, 0.0, 1.0);
		double seconds = Math.Clamp(StartupElapsedSeconds, 0.0, 1.167);
		double gamesOffset = SampleCurve(
			seconds,
			(0.567, 210.0),
			(0.600, 205.0),
			(0.633, 203.0),
			(0.667, 194.0),
			(0.700, 167.0),
			(0.733, 138.0),
			(0.767, 111.0),
			(0.800, 97.0),
			(0.833, 87.0),
			(0.867, 78.0),
			(0.900, 68.0),
			(0.933, 59.0),
			(0.967, 49.0),
			(1.000, 39.0),
			(1.033, 30.0),
			(1.067, 20.0),
			(1.100, 12.0),
			(1.133, 3.0),
			(1.167, 0.0));
		// Full PNGs emerge behind the foreground shutters; adjacent blades occlude
		// one another. A rectangular width wipe exposes transparent gaps and cuts labels.
		double mediaOffset = Math.Max(gamesOffset - 71.0,
			SampleCurve(seconds, (0.833, 20.0), (1.000, 12.0), (1.067, 7.0), (1.100, 4.0), (1.133, 1.0), (1.167, 0.0)));
		double systemOffset = SampleCurve(seconds, (0.900, 80.0), (1.000, 68.0), (1.067, 30.0), (1.133, 7.0), (1.167, 0.0));
		double xboxLiveOffset = SampleCurve(seconds, (0.600, -110.0), (0.700, -85.0), (0.767, -38.0), (0.800, -15.0), (0.833, 0.0));
		double marketplaceOffset = SampleCurve(seconds, (0.800, -60.0), (1.000, -42.0), (1.067, -26.0), (1.133, -9.0), (1.167, 0.0));

		foreach (LayerDefinition layer in Layers)
		{
			if (!_images.TryGetValue(layer.Key, out Image? image) ||
				!_transforms.TryGetValue(layer.Key, out TranslateTransform? transform) ||
				!_clips.TryGetValue(layer.Key, out RectangleGeometry? clip))
			{
				continue;
			}

			if (progress >= 1.0)
			{
				image.Visibility = Visibility.Visible;
				image.Opacity = 1.0;
				clip.Rect = FullClipRect();
				transform.X = 0.0;
				transform.Y = 0.0;
				SetZIndex(image, _states[_currentStateKey].LayerZOrder[layer.Key]);
				continue;
			}

			image.Opacity = 1.0;
			clip.Rect = FullClipRect();
			switch (layer.Key)
			{
				case "left runner":
					image.Opacity = SampleCurve(seconds, (0.767, 0.0), (0.800, 1.0));
					clip.Rect = FullClipRect();
					SetZIndex(image, 5);
					transform.X = 0.0;
					transform.Y = 0.0;
					break;
				case "right runner":
					image.Opacity = SampleCurve(seconds, (0.767, 0.0), (0.800, 1.0));
					clip.Rect = FullClipRect();
					SetZIndex(image, 5);
					transform.X = 0.0;
					transform.Y = 0.0;
					break;
				case "xbox live":
					image.Opacity = 1.0;
					clip.Rect = FullClipRect();
					SetZIndex(image, 90);
					transform.X = xboxLiveOffset;
					transform.Y = 0.0;
					break;
				case "games":
					image.Opacity = 1.0;
					clip.Rect = FullClipRect();
					SetZIndex(image, 89);
					transform.X = gamesOffset;
					transform.Y = 0.0;
					break;
				case "marketplace":
					image.Opacity = 1.0;
					clip.Rect = FullClipRect();
					SetZIndex(image, 70);
					transform.X = marketplaceOffset;
					transform.Y = 0.0;
					break;
				case "media":
					image.Opacity = 1.0;
					clip.Rect = FullClipRect();
					SetZIndex(image, 69);
					transform.X = mediaOffset;
					transform.Y = 0.0;
					break;
				case "system":
					image.Opacity = 1.0;
					clip.Rect = FullClipRect();
					SetZIndex(image, 68);
					transform.X = systemOffset;
					transform.Y = 0.0;
					break;
			}
		}
	}

	private static double SampleCurve(double seconds, params (double Time, double Value)[] samples)
	{
		if (samples.Length == 0)
		{
			return 0.0;
		}

		if (seconds <= samples[0].Time)
		{
			return samples[0].Value;
		}

		for (int i = 1; i < samples.Length; i++)
		{
			if (seconds <= samples[i].Time)
			{
				double span = samples[i].Time - samples[i - 1].Time;
				double local = span <= 0.0 ? 1.0 : (seconds - samples[i - 1].Time) / span;
				return Lerp(samples[i - 1].Value, samples[i].Value, local);
			}
		}

		return samples[^1].Value;
	}

	private double HiddenBehindX(string layerKey, string frontLayerKey, bool alignRightEdges)
	{
		SourceInfo layerInfo = LoadSourceInfo(_states[_currentStateKey].LayerSources[layerKey]);
		SourceInfo frontInfo = LoadSourceInfo(_states[_currentStateKey].LayerSources[frontLayerKey]);
		return alignRightEdges
			? (frontInfo.VisibleBounds.X + frontInfo.VisibleBounds.Width) - (layerInfo.VisibleBounds.X + layerInfo.VisibleBounds.Width)
			: frontInfo.VisibleBounds.X - layerInfo.VisibleBounds.X;
	}

	private double CurrentLeftEdge(string layerKey, double transformX)
	{
		SourceInfo layerInfo = LoadSourceInfo(_states[_currentStateKey].LayerSources[layerKey]);
		return layerInfo.VisibleBounds.X + transformX;
	}

	private static double Lerp(double from, double to, double progress)
	{
		progress = Math.Clamp(progress, 0.0, 1.0);
		return from + ((to - from) * progress);
	}

	private void AnimateToState(string targetStateKey)
	{
		string fromStateKey = _currentStateKey;
		BladeStateDefinition fromState = _states[fromStateKey];
		BladeStateDefinition targetState = _states[targetStateKey];
		int fromStateIndex = StateIndexFromKey(fromStateKey);
		int targetStateIndex = StateIndexFromKey(targetStateKey);
		int direction = Math.Sign(targetStateIndex - fromStateIndex);
		if (direction == 0)
		{
			return;
		}

		_isAnimating = true;
		StopNavigationRendering();
		string movingKey = direction > 0 ? targetStateKey : fromStateKey;
		SourceInfo movingFrom = LoadSourceInfo(fromState.LayerSources[movingKey]);
		SourceInfo movingTo = LoadSourceInfo(targetState.LayerSources[movingKey]);
		double edgeFrom = movingFrom.VisibleBounds.X + 40.0;
		double edgeTo = movingTo.VisibleBounds.X + 40.0;
		var boundaryTransform = new TranslateTransform(movingFrom.VisibleBounds.X - movingTo.VisibleBounds.X,0);
		var boundaryGeometry = GetBoundaryGeometry(targetState.LayerSources[movingKey],movingTo,direction>0).Clone();
		boundaryGeometry.Transform = boundaryTransform;
		NavigationOutgoingClip = boundaryGeometry;
		NavigationFrame?.Invoke(fromStateKey, targetStateKey, 0.0, 0.0, edgeFrom);
		List<Action<double>> updates = new();

		foreach (LayerDefinition layer in Layers)
		{
			string fromSource = fromState.LayerSources[layer.Key];
			string targetSource = targetState.LayerSources[layer.Key];
			SourceInfo fromInfo = LoadSourceInfo(fromSource);
			SourceInfo targetInfo = LoadSourceInfo(targetSource);
			Image image = _images[layer.Key];
			TranslateTransform transform = _transforms[layer.Key];
			RectangleGeometry clip = _clips[layer.Key];
			Image incomingImage = _incomingImages[layer.Key];
			TranslateTransform incomingTransform = _incomingTransforms[layer.Key];
			RectangleGeometry incomingClip = _incomingClips[layer.Key];
			double incomingStartX = fromInfo.VisibleBounds.X - targetInfo.VisibleBounds.X;
			double outgoingEndX = targetInfo.VisibleBounds.X - fromInfo.VisibleBounds.X;

			transform.BeginAnimation(TranslateTransform.XProperty, null);
			transform.BeginAnimation(TranslateTransform.YProperty, null);
			image.BeginAnimation(OpacityProperty, null);
			clip.BeginAnimation(RectangleGeometry.RectProperty, null);
			image.Source = fromInfo.Artwork;
			image.Opacity = 1;
			image.Visibility = Visibility.Visible;
			clip.Rect = FullClipRect();
			SetLeft(image, 0);
			SetTop(image, 0);
			SetZIndex(image, BuildTransitionZOrder(layer.Key, fromStateKey, false));
			transform.X = 0;
			transform.Y = 0;

			incomingTransform.BeginAnimation(TranslateTransform.XProperty, null);
			incomingTransform.BeginAnimation(TranslateTransform.YProperty, null);
			incomingImage.BeginAnimation(OpacityProperty, null);
			incomingClip.BeginAnimation(RectangleGeometry.RectProperty, null);
			incomingImage.Source = targetInfo.Artwork;
			incomingImage.Opacity = 1;
			incomingImage.Visibility = Visibility.Visible;
			incomingClip.Rect = FullClipRect();
			SetLeft(incomingImage, 0);
			SetTop(incomingImage, 0);
			SetZIndex(incomingImage, BuildTransitionZOrder(layer.Key, targetStateKey, true));
			incomingTransform.X = incomingStartX;
			incomingTransform.Y = 0;

			bool runner = IsRunner(layer.Key);
			int z = runner ? 5 : layer.Key == movingKey ? 100 : 60 - Math.Abs(StateIndexFromKey(layer.Key) - targetStateIndex);
			SetZIndex(image, z * 2);
			SetZIndex(incomingImage, z * 2 + 1);
			// The destination artwork travels as one opaque surface. Crossfading
			// handed variants duplicates their labels and makes the silver translucent.
			image.Visibility = Visibility.Collapsed;
			incomingImage.Opacity = 1;
			updates.Add(progress =>
			{
				transform.X = runner ? 0 : outgoingEndX * progress;
				incomingTransform.X = runner ? 0 : incomingStartX * (1.0 - progress);
				incomingImage.Opacity = 1;
			});

			WriteDiagnostic(fromStateKey + " -> " + targetStateKey, layer, fromSource, fromInfo.Bitmap, image, transform);
		}

		TimeSpan? start = null, previous = null;
		_navigationRendering = (_, args) =>
		{
			TimeSpan now = ((RenderingEventArgs)args).RenderingTime;
			if (previous == now) return;
			previous = now;
			start ??= now;
			double seconds = (now - start.Value).TotalSeconds;
			double progress = EaseOutCubic(seconds / 0.200);
			foreach (Action<double> update in updates) update(progress);
			boundaryTransform.X = (movingFrom.VisibleBounds.X - movingTo.VisibleBounds.X)*(1-progress);
			NavigationFrame?.Invoke(fromStateKey, targetStateKey, progress, seconds, Lerp(edgeFrom, edgeTo, progress));
			if (seconds >= 0.333) FinishAnimation(targetStateKey);
		};
		CompositionTarget.Rendering += _navigationRendering;
	}

	private void StopNavigationRendering()
	{
		if (_navigationRendering != null) CompositionTarget.Rendering -= _navigationRendering;
		_navigationRendering = null;
	}

	private Geometry GetBoundaryGeometry(string source, SourceInfo info, bool forward)
	{
		string key=source+":"+forward;
		if (_boundaryCache.TryGetValue(key,out var cached)) return cached;
		byte[] pixels=new byte[1920*1080*4];
		info.Bitmap.CopyPixels(pixels,1920*4,(info.PixelBounds.Y*1920+info.PixelBounds.X)*4);
		var geometry=new StreamGeometry();
		using(var context=geometry.Open())
		{
			double outside=forward ? -1920 : 3840;
			context.BeginFigure(new Point(outside,0),true,true);
			double previous=forward?0:1920;
			for(int y=0;y<=1080;y+=4)
			{
				int row=Math.Min(y,1079);
				int silverLeft=1920, silverRight=-1;
				for(int x=0;x<1920;x++)
				{
					int p=(row*1920+x)*4;
					int low=Math.Min(pixels[p],Math.Min(pixels[p+1],pixels[p+2]));
					int high=Math.Max(pixels[p],Math.Max(pixels[p+1],pixels[p+2]));
					if(pixels[p+3]>240 && low>110 && high-low<12)
					{ silverLeft=Math.Min(silverLeft,x); silverRight=x; }
				}
				for(int i=0;i<1920;i++)
				{
					int x=forward?1919-i:i;
					if(pixels[(row*1920+x)*4+3]>=96) { previous=x; break; }
				}
				// Keep the color join inside opaque silver, not its translucent colored lip.
				if(silverRight>=silverLeft) previous=(silverLeft+silverRight)/2.0;
				context.LineTo(new Point(previous,y),true,false);
			}
			context.LineTo(new Point(outside,1080),true,false);
		}
		geometry.Freeze();
		_boundaryCache[key]=geometry;
		return geometry;
	}

	private static Rect FullClipRect()
	{
		return new Rect(0, 0, DesignWidth, DesignHeight);
	}

	private static double EaseOutCubic(double value)
	{
		value = Math.Clamp(value, 0.0, 1.0);
		double inverse = 1.0 - value;
		return 1.0 - (inverse * inverse * inverse);
	}

	private void FinishAnimation(string targetStateKey)
	{
		StopNavigationRendering();
		_isAnimating = false;
		ApplyStaticState(StateIndexFromKey(targetStateKey));
		NavigationCompleted?.Invoke();

		if (_queuedStateKey is { } queuedStateKey &&
			!string.Equals(queuedStateKey, _currentStateKey, StringComparison.OrdinalIgnoreCase))
		{
			_queuedStateKey = null;
			AnimateToState(queuedStateKey);
		}
		else
		{
			_queuedStateKey = null;
		}
	}

	private SourceInfo LoadSourceInfo(string relativePath)
	{
		if (_sourceCache.TryGetValue(relativePath, out SourceInfo? cached))
		{
			return cached;
		}
		SourceInfo sourceInfo = DecodeSourceInfo(relativePath);
		_sourceCache[relativePath] = sourceInfo;
		return sourceInfo;
	}

	private static SourceInfo DecodeSourceInfo(string relativePath)
	{
		string path = Path.Combine(AppContext.BaseDirectory, relativePath.Replace('/', Path.DirectorySeparatorChar));
		var compact = XboxMetroLauncher.Utilities.CompactBitmap.Load(path);
		return new SourceInfo(compact.Bitmap, compact.VisibleBounds, compact.PixelBounds, compact.Artwork);
	}

	private static IReadOnlyDictionary<string, BladeStateDefinition> BuildStates()
	{
		Dictionary<string, BladeStateDefinition> states = new(StringComparer.OrdinalIgnoreCase);
		foreach (string stateKey in StateKeys)
		{
			states[stateKey] = BuildState(stateKey);
		}

		return states;
	}

	private static BladeStateDefinition BuildState(string focusedKey)
	{
		string folder = focusedKey + " focused";
		Dictionary<string, string> sources = new(StringComparer.OrdinalIgnoreCase)
		{
			["marketplace"] = AssetPath(folder, focusedKey == "media" ? "marketplace.png" : "marketplace blade.png"),
			["xbox live"] = AssetPath(folder, "xbox live blade.png"),
			["games"] = AssetPath(folder, "games blade.png"),
			["media"] = AssetPath(folder, "media blade.png"),
			["system"] = AssetPath(folder, "system blade.png"),
			["left runner"] = AssetPath(folder, folder + " left runner.png"),
			["right runner"] = AssetPath(folder, folder + " right runner.png")
		};

		Dictionary<string, int> zOrder = BuildStaticZOrder(focusedKey);
		return new BladeStateDefinition(sources, zOrder);
	}

	private static Dictionary<string, int> BuildStaticZOrder(string focusedKey)
	{
		int focusedIndex = StateIndexFromKey(focusedKey);
		Dictionary<string, int> zOrder = new(StringComparer.OrdinalIgnoreCase)
		{
			["left runner"] = 5,
			["right runner"] = 5
		};

		for (int i = 0; i < StateKeys.Length; i++)
		{
			string layerKey = StateKeys[i];
			int distance = Math.Abs(i - focusedIndex);
			zOrder[layerKey] = i == focusedIndex ? 70 : 60 - distance;
		}

		return zOrder;
	}

	private static int BuildTransitionZOrder(string layerKey, string stateKey, bool isIncoming)
	{
		if (IsRunner(layerKey))
		{
			return 5;
		}

		int focusedIndex = StateIndexFromKey(stateKey);
		int layerIndex = StateIndexFromKey(layerKey);
		int baseZ = layerIndex == focusedIndex ? 80 : 60 - Math.Abs(layerIndex - focusedIndex);
		return isIncoming ? baseZ + 2 : baseZ;
	}

	private static bool IsRunner(string layerKey)
	{
		return string.Equals(layerKey, "left runner", StringComparison.OrdinalIgnoreCase) ||
			string.Equals(layerKey, "right runner", StringComparison.OrdinalIgnoreCase);
	}

	private static string AssetPath(string folder, string fileName)
	{
		return $"{AssetRoot}/{folder}/{fileName}";
	}

	private static int StateIndexFromKey(string? key)
	{
		if (!string.IsNullOrWhiteSpace(key))
		{
			for (int i = 0; i < StateKeys.Length; i++)
			{
				if (string.Equals(StateKeys[i], key, StringComparison.OrdinalIgnoreCase))
				{
					return i;
				}
			}
		}

		throw new InvalidOperationException($"Unknown Blades focused state '{key}'.");
	}

	private static void WriteDiagnostic(
		string stateKey,
		LayerDefinition layer,
		string source,
		BitmapSource bitmap,
		Image image,
		TranslateTransform transform)
	{
		Debug.WriteLine(
			$"[BladesArtworkHost] state='{stateKey}' layer='{layer.DisplayName}' source='{source}' " +
			$"bitmap={bitmap.PixelWidth}x{bitmap.PixelHeight} actual={image.ActualWidth:0.##}x{image.ActualHeight:0.##} " +
			$"translate=({transform.X:0.##},{transform.Y:0.##}) z={GetZIndex(image)}");
	}

	private sealed record LayerDefinition(string ImageName, string DisplayName, string Key);

	private sealed record BladeStateDefinition(
		IReadOnlyDictionary<string, string> LayerSources,
		IReadOnlyDictionary<string, int> LayerZOrder);

	private sealed record SourceInfo(BitmapSource Bitmap, Int32Rect VisibleBounds, Int32Rect PixelBounds, DrawingImage Artwork);
}
