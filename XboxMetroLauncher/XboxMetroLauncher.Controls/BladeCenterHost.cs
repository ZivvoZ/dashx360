using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using PathShape = System.Windows.Shapes.Path;
using XboxMetroLauncher.Utilities;

namespace XboxMetroLauncher.Controls;

public sealed class BladeCenterHost : Canvas
{
	private const double DesignWidth = 1920.0;
	private const double DesignHeight = 1080.0;
	private readonly PathShape _baseBody;
	private readonly Rectangle _topOverlay;
	private readonly Rectangle _bottomOverlay;
	private readonly Canvas _decorativeLayer;
	private readonly BladeAmbientTexture _ambientTexture;
	private readonly RadialGradientBrush _glowBrush;
	private static readonly Dictionary<string, ImageSource> _maskSourceCache = new(StringComparer.OrdinalIgnoreCase);
	private bool _animationsRunning;
	private BladeCenterHost? _outgoingCenter;
	private Canvas? _outgoingLayer;
	private readonly RectangleGeometry _navigationClip = new();
	private string? _navigationTarget;

	public void ApplyNavigationFrame(string from, string to, double progress, double boundary, Geometry? silhouette = null)
	{
		if (_navigationTarget != to)
		{
			_navigationTarget = to;
			ApplyLayout(to);
			Background = new SolidColorBrush(GetLayout(to).BaseColor);
			if (_outgoingCenter == null)
			{
				_outgoingCenter = new BladeCenterHost();
				_outgoingLayer = new Canvas { Width = DesignWidth, Height = DesignHeight, Clip = _navigationClip, IsHitTestVisible = false };
				_outgoingLayer.Children.Add(_outgoingCenter);
				SetZIndex(_outgoingLayer, 4);
				Children.Add(_outgoingLayer);
			}
			_outgoingCenter.FocusedBladeKey = from;
			_outgoingLayer!.Background = new SolidColorBrush(GetLayout(from).BaseColor);
		}
		boundary = Math.Clamp(boundary, 0, DesignWidth);
		bool forward = BladeIndex(to) > BladeIndex(from);
		_navigationClip.Rect = forward
			? new Rect(0, 0, boundary, DesignHeight)
			: new Rect(boundary, 0, DesignWidth - boundary, DesignHeight);
		_outgoingLayer!.Visibility = progress < 1 ? Visibility.Visible : Visibility.Collapsed;
		_outgoingLayer.Clip = silhouette ?? _navigationClip;
		if (progress >= 1) Background = null;
	}

	public void FinishNavigation()
	{
		if (_outgoingCenter != null)
		{
			_outgoingCenter.StopAnimations();
		}
		if (_outgoingLayer != null)
		{
			Children.Remove(_outgoingLayer);
			_outgoingLayer.Children.Clear();
			_outgoingLayer = null;
			_outgoingCenter = null;
		}
		Background = null;
		_navigationTarget = null;
	}

	private static int BladeIndex(string key) => key switch
	{
		"marketplace" => 0, "xbox live" => 1, "games" => 2, "media" => 3, "system" => 4,
		_ => 1
	};

	public static readonly DependencyProperty FocusedBladeKeyProperty = DependencyProperty.Register(
		nameof(FocusedBladeKey),
		typeof(string),
		typeof(BladeCenterHost),
		new PropertyMetadata("xbox live", OnFocusedBladeKeyChanged));

	public string FocusedBladeKey
	{
		get => (string)GetValue(FocusedBladeKeyProperty);
		set => SetValue(FocusedBladeKeyProperty, value);
	}

	public BladeCenterHost()
	{
		Width = DesignWidth;
		Height = DesignHeight;
		ClipToBounds = false;
		IsHitTestVisible = false;
		SnapsToDevicePixels = true;
		UseLayoutRounding = true;

		_baseBody = CreatePath(0);
		Children.Add(_baseBody);

		_glowBrush = new RadialGradientBrush
		{
			Center = new Point(0.50, 0.34),
			GradientOrigin = new Point(0.48, 0.30),
			RadiusX = 0.88,
			RadiusY = 0.76
		};
		_glowBrush.GradientStops.Add(new GradientStop(Color.FromArgb(24, 255, 255, 255), 0));
		_glowBrush.GradientStops.Add(new GradientStop(Color.FromArgb(10, 255, 255, 255), 0.46));
		_glowBrush.GradientStops.Add(new GradientStop(Color.FromArgb(0, 255, 255, 255), 1));

		_decorativeLayer = new Canvas { Width = DesignWidth, Height = DesignHeight, Opacity = 0.94, IsHitTestVisible = false };
		SetZIndex(_decorativeLayer, 1);
		Children.Add(_decorativeLayer);
		_ambientTexture = new BladeAmbientTexture { Width = DesignWidth, Height = DesignHeight };
		_decorativeLayer.Children.Add(_ambientTexture);

		Rectangle glow = new()
		{
			Width = DesignWidth,
			Height = DesignHeight,
			Fill = _glowBrush,
			IsHitTestVisible = false
		};
		_decorativeLayer.Children.Add(glow);

		_topOverlay = CreateMaskedOverlay(3);
		_bottomOverlay = CreateMaskedOverlay(3);
		Children.Add(_topOverlay);
		Children.Add(_bottomOverlay);

		Loaded += (_, _) =>
		{
			ApplyLayout();
			StartAnimations();
		};
		IsVisibleChanged += (_, _) =>
		{
			if (IsVisible)
			{
				StartAnimations();
			}
			else
			{
				StopAnimations();
			}
		};
		Unloaded += (_, _) => StopAnimations();
		ApplyLayout();
	}

	private static PathShape CreatePath(int zIndex)
	{
		PathShape path = new()
		{
			Stretch = Stretch.None,
			SnapsToDevicePixels = true,
			UseLayoutRounding = true,
			IsHitTestVisible = false
		};
		SetZIndex(path, zIndex);
		return path;
	}

	private static void OnFocusedBladeKeyChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs e)
	{
		if (dependencyObject is BladeCenterHost host)
		{
			if (host._navigationTarget == null) host.ApplyLayout();
		}
	}

	private void ApplyLayout(string? stateKey = null)
	{
		stateKey ??= FocusedBladeKey;
		BladeCenterLayout layout = GetLayout(stateKey);
		Color color = layout.BaseColor;
		_ambientTexture.Tint = color;
		Geometry baseGeometry = CreateStateCenterGeometry(layout);
		Clip = null;
		Opacity = 1.0;

		_baseBody.Data = baseGeometry;
		_baseBody.Fill = CreateBodyBrush(color);
		_baseBody.Stroke = Brushes.Transparent;
		_baseBody.StrokeThickness = 0;

		(string topOverlayPath, string bottomOverlayPath) = GetFocusedOverlaySources(stateKey);
		_topOverlay.Fill = CreateHeaderBrush(color);
		_topOverlay.Opacity = 0.52;
		_topOverlay.OpacityMask = CreateMaskBrush(topOverlayPath);
		_bottomOverlay.Fill = CreateFooterBrush(color);
		_bottomOverlay.Opacity = 0.48;
		_bottomOverlay.OpacityMask = CreateMaskBrush(bottomOverlayPath);

		_decorativeLayer.Clip = baseGeometry;

		UpdateGlowTint(color);
	}

	private static Rectangle CreateMaskedOverlay(int zIndex)
	{
		Rectangle rectangle = new()
		{
			Width = DesignWidth,
			Height = DesignHeight,
			IsHitTestVisible = false,
			SnapsToDevicePixels = true,
			UseLayoutRounding = true
		};

		SetLeft(rectangle, 0);
		SetTop(rectangle, 0);
		SetZIndex(rectangle, zIndex);
		return rectangle;
	}

	private ImageBrush CreateMaskBrush(string relativePath)
	{
		ImageBrush brush = new(LoadMaskSource(relativePath))
		{
			Stretch = Stretch.Fill,
			AlignmentX = AlignmentX.Left,
			AlignmentY = AlignmentY.Top
		};
		RenderOptions.SetBitmapScalingMode(brush, BitmapScalingMode.HighQuality);
		return brush;
	}

	private ImageSource LoadMaskSource(string relativePath)
	{
		lock (_maskSourceCache)
		{
			if (_maskSourceCache.TryGetValue(relativePath, out ImageSource? cachedSource))
			{
				return cachedSource;
			}

			var source = CompactBitmap.Load(System.IO.Path.Combine(AppContext.BaseDirectory,relativePath)).Artwork;
			_maskSourceCache[relativePath] = source;
			return source;
		}
	}

	private static (string Top, string Bottom) GetFocusedOverlaySources(string? key)
	{
		const string root = "Assets/Blades/FocusedOverlays";
		return (key ?? string.Empty).ToLowerInvariant() switch
		{
			"marketplace" => ($"{root}/MarketplaceFocusedTop.png", $"{root}/MarketplaceFocusedBottom.png"),
			"games" => ($"{root}/GamesFocusedTop.png", $"{root}/GamesFocusedBottom.png"),
			"media" => ($"{root}/MediaFocusedTop.png", $"{root}/MediaFocusedBottom.png"),
			"system" => ($"{root}/SystemFocusedTop.png", $"{root}/SystemFocusedBottom.png"),
			_ => ($"{root}/XboxLiveFocusedTop.png", $"{root}/XboxLiveFocusedBottom.png")
		};
	}

    private static Geometry CreateStateCenterGeometry(BladeCenterLayout layout)
    {
        BladeShellGeometry shell = layout.Shell;
        double bodyLeft = GetPlainBodyLeft(shell);
        return new RectangleGeometry(new Rect(bodyLeft, 0, shell.BodyRight - bodyLeft, DesignHeight));
    }

    private static double GetPlainBodyLeft(BladeShellGeometry shell)
    {
        // Cover beneath the side artwork; the header curve is not the middle's edge.
        return Math.Max(0, shell.BodyLeft - 112);
    }

	private static Brush CreateBodyBrush(Color color)
	{
		LinearGradientBrush brush = new() { StartPoint = new Point(0, 0), EndPoint = new Point(0, 1) };
		brush.GradientStops.Add(new GradientStop(Blend(color, Colors.White, 0.18), 0));
		brush.GradientStops.Add(new GradientStop(Blend(color, Colors.White, 0.06), 0.36));
		brush.GradientStops.Add(new GradientStop(color, 0.70));
		brush.GradientStops.Add(new GradientStop(Blend(color, Colors.Black, 0.12), 1));
		return brush;
	}

	private static Brush CreateHeaderBrush(Color color)
	{
		LinearGradientBrush brush = new() { StartPoint = new Point(0, 0), EndPoint = new Point(0, 1) };
		brush.GradientStops.Add(new GradientStop(Blend(color, Colors.Black, 0.18), 0));
		brush.GradientStops.Add(new GradientStop(Blend(color, Colors.Black, 0.12), 0.48));
		brush.GradientStops.Add(new GradientStop(Blend(color, Colors.Black, 0.24), 1));
		return brush;
	}

	private static Brush CreateFooterBrush(Color color)
	{
		LinearGradientBrush brush = new() { StartPoint = new Point(0, 0), EndPoint = new Point(0, 1) };
		brush.GradientStops.Add(new GradientStop(Blend(color, Colors.Black, 0.12), 0));
		brush.GradientStops.Add(new GradientStop(Blend(color, Colors.Black, 0.22), 0.50));
		brush.GradientStops.Add(new GradientStop(Blend(color, Colors.Black, 0.34), 1));
		return brush;
	}

	private void UpdateGlowTint(Color color)
	{
		Color tint = Blend(color, Colors.White, 0.48);
		_glowBrush.GradientStops[0].Color = Color.FromArgb(22, tint.R, tint.G, tint.B);
		_glowBrush.GradientStops[1].Color = Color.FromArgb(9, 255, 255, 255);
	}

	private void StartAnimations()
	{
		if (_animationsRunning || !IsVisible)
		{
			return;
		}
		_animationsRunning = true;

		_decorativeLayer.Opacity = 0.94;
		_glowBrush.BeginAnimation(RadialGradientBrush.CenterProperty, new PointAnimation(new Point(0.47, 0.24), new Point(0.57, 0.34), TimeSpan.FromSeconds(42)) { AutoReverse = true, RepeatBehavior = RepeatBehavior.Forever, EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut } });
		_glowBrush.BeginAnimation(RadialGradientBrush.GradientOriginProperty, new PointAnimation(new Point(0.43, 0.22), new Point(0.54, 0.28), TimeSpan.FromSeconds(37)) { AutoReverse = true, RepeatBehavior = RepeatBehavior.Forever, EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut } });
	}

	private void StopAnimations()
	{
		_animationsRunning = false;
		_decorativeLayer.BeginAnimation(OpacityProperty, null);
		_glowBrush.BeginAnimation(RadialGradientBrush.CenterProperty, null);
		_glowBrush.BeginAnimation(RadialGradientBrush.GradientOriginProperty, null);
	}

	private static Color Blend(Color color, Color target, double amount)
	{
		byte r = (byte)Math.Round(color.R + (target.R - color.R) * amount);
		byte g = (byte)Math.Round(color.G + (target.G - color.G) * amount);
		byte b = (byte)Math.Round(color.B + (target.B - color.B) * amount);
		return Color.FromArgb(255, r, g, b);
	}

	private static BladeCenterLayout GetLayout(string? key)
        {
            return (key ?? string.Empty).ToLowerInvariant() switch
            {
                "marketplace" => new BladeCenterLayout(
        new Rect(80, 0, 1412, 1072),
                    new Rect(306, 152, 1228, 738),
                    new Rect(306, 152, 612, 610),
                    new Rect(870, 152, 556, 426),
                    new Rect(870, 250, 556, 150),
                    new Rect(306, 790, 1182, 84),
                    Color.FromRgb(222, 91, 15),
        new BladeShellGeometry(132, 1518, 132, 152, 196, 1401, 1502, 52, 895, 156, 196, 1449, 1487, 56),
                    56,
                    34,
                    46,
                    12,
                    1490),
                "games" => new BladeCenterLayout(
        new Rect(188, 0, 1390, 1080),
                    new Rect(584, 146, 934, 734),
                    new Rect(584, 150, 612, 610),
                    new Rect(1010, 150, 510, 420),
                    new Rect(594, 615, 588, 138),
                    new Rect(584, 790, 956, 84),
                    Color.FromRgb(47, 178, 47),
        new BladeShellGeometry(258, 1644, 132, 278, 326, 1575, 1629, 52, 895, 282, 326, 1583, 1615, 56),
                    42,
                    36,
                    52,
                    0,
                    1626),
			"media" => new BladeCenterLayout(
				new Rect(360, 0, 1270, 1080),
				new Rect(650, 146, 884, 734),
				new Rect(650, 150, 612, 610),
				new Rect(1040, 150, 504, 420),
				new Rect(0, 0, 0, 0),
				new Rect(650, 790, 900, 84),
				Color.FromRgb(37, 150, 220),
				new BladeShellGeometry(372, 1715, 132, 389, 451, 1594, 1699, 52, 895, 395, 423, 1661, 1688, 56),
				38,
				34,
				50,
                    0,
                    1698),
                "system" => new BladeCenterLayout(
        new Rect(340, 0, 1438, 1074),
                    new Rect(522, 142, 1074, 720),
                    new Rect(522, 162, 612, 560),
                    new Rect(1152, 162, 420, 600),
                    new Rect(0, 0, 0, 0),
                    new Rect(0, 0, 0, 0),
                    Color.FromRgb(144, 91, 212),
        new BladeShellGeometry(384, 1784, 132, 400, 432, 1672, 1770, 52, 895, 402, 432, 1702, 1766, 56),
                    38,
                    30,
                    44,
                    6,
        1784),
        _ => new BladeCenterLayout(
        new Rect(170, 0, 1370, 1080),
                    new Rect(494, 146, 1000, 734),
                    new Rect(494, 150, 612, 610),
                    new Rect(936, 150, 510, 420),
                    new Rect(494, 610, 588, 144),
                    new Rect(494, 790, 956, 84),
                    Color.FromRgb(218, 151, 57),
        new BladeShellGeometry(184, 1584, 132, 204, 232, 1516, 1570, 52, 895, 208, 242, 1523, 1556, 56),
                    42,
                    36,
                    52,
				0,
				1560),
		};
	}

	private sealed record BladeCenterLayout(
		Rect BaseBounds,
		Rect ContentClipBounds,
		Rect LeftContentBounds,
		Rect RightContentBounds,
		Rect DecorativeRingBounds,
		Rect OpenTrayBounds,
		Color BaseColor,
		BladeShellGeometry Shell,
		double LeftCurveInset,
		double RightCurveInset,
		double RightWaistInset,
		double BottomLift,
		double CapRight);

	private sealed record BladeShellGeometry(
		double BodyLeft,
		double BodyRight,
		double HeaderBottom,
		double HeaderLeftCurveStart,
		double HeaderLeftCurveEnd,
		double HeaderRightCurveStart,
		double HeaderRightCurveEnd,
		double HeaderCurveDepth,
		double FooterTop,
		double FooterLeftCurveStart,
		double FooterLeftCurveEnd,
		double FooterRightCurveStart,
		double FooterRightCurveEnd,
		double FooterCurveDepth);
}
