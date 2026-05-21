using System.Windows;
using System.Windows.Media;

namespace XboxMetroLauncher.Controls;

public sealed class MusicVisualizer : FrameworkElement
{
    public static readonly DependencyProperty IsActiveProperty =
        DependencyProperty.Register(nameof(IsActive), typeof(bool), typeof(MusicVisualizer),
            new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty VolumeProperty =
        DependencyProperty.Register(nameof(Volume), typeof(double), typeof(MusicVisualizer),
            new FrameworkPropertyMetadata(0.7, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty ProgressProperty =
        DependencyProperty.Register(nameof(Progress), typeof(double), typeof(MusicVisualizer),
            new FrameworkPropertyMetadata(0d, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty BassProperty =
        DependencyProperty.Register(nameof(Bass), typeof(double), typeof(MusicVisualizer),
            new FrameworkPropertyMetadata(0d, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty MidProperty =
        DependencyProperty.Register(nameof(Mid), typeof(double), typeof(MusicVisualizer),
            new FrameworkPropertyMetadata(0d, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty TrebleProperty =
        DependencyProperty.Register(nameof(Treble), typeof(double), typeof(MusicVisualizer),
            new FrameworkPropertyMetadata(0d, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty LoudnessProperty =
        DependencyProperty.Register(nameof(Loudness), typeof(double), typeof(MusicVisualizer),
            new FrameworkPropertyMetadata(0d, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty PeakProperty =
        DependencyProperty.Register(nameof(Peak), typeof(double), typeof(MusicVisualizer),
            new FrameworkPropertyMetadata(0d, FrameworkPropertyMetadataOptions.AffectsRender));

    private readonly Random _random = new(1978);
    private readonly Particle[] _particles;
    private readonly List<Shockwave> _shockwaves = [];
    private readonly Color[] _palette =
    [
        Color.FromRgb(120, 42, 214),
        Color.FromRgb(42, 78, 190),
        Color.FromRgb(24, 192, 224),
        Color.FromRgb(208, 48, 148)
    ];

    private TimeSpan _lastFrame;
    private double _time;
    private double _bass;
    private double _mid;
    private double _treble;
    private double _energy;
    private double _burst;
    private double _cameraX;
    private double _cameraY;
    private double _cameraZoom = 1;
    private double _nextMutation;
    private double _mutation;
    private double _mutationTarget = 0.5;
    private double _lastProgress = -1;
    private double _impact;
    private double _dropEnergy;
    private double _sparkBurst;
    private double _lastLiveBass;
    private double _lastLiveLoudness;
    private double _lastKickTime = -10;

    public MusicVisualizer()
    {
        ClipToBounds = true;
        SnapsToDevicePixels = false;
        _particles = new Particle[92];

        for (var i = 0; i < _particles.Length; i++)
        {
            _particles[i] = CreateParticle(i);
        }

        Loaded += (_, _) => CompositionTarget.Rendering += OnRendering;
        Unloaded += (_, _) => CompositionTarget.Rendering -= OnRendering;
    }

    public bool IsActive
    {
        get => (bool)GetValue(IsActiveProperty);
        set => SetValue(IsActiveProperty, value);
    }

    public double Volume
    {
        get => (double)GetValue(VolumeProperty);
        set => SetValue(VolumeProperty, value);
    }

    public double Progress
    {
        get => (double)GetValue(ProgressProperty);
        set => SetValue(ProgressProperty, value);
    }

    public double Bass
    {
        get => (double)GetValue(BassProperty);
        set => SetValue(BassProperty, value);
    }

    public double Mid
    {
        get => (double)GetValue(MidProperty);
        set => SetValue(MidProperty, value);
    }

    public double Treble
    {
        get => (double)GetValue(TrebleProperty);
        set => SetValue(TrebleProperty, value);
    }

    public double Loudness
    {
        get => (double)GetValue(LoudnessProperty);
        set => SetValue(LoudnessProperty, value);
    }

    public double Peak
    {
        get => (double)GetValue(PeakProperty);
        set => SetValue(PeakProperty, value);
    }

    private void OnRendering(object? sender, EventArgs e)
    {
        if (e is not RenderingEventArgs args)
        {
            return;
        }

        var delta = _lastFrame == TimeSpan.Zero
            ? 1d / 60d
            : Math.Clamp((args.RenderingTime - _lastFrame).TotalSeconds, 0.001, 0.05);

        _lastFrame = args.RenderingTime;
        _time += delta;

        if (_time >= _nextMutation)
        {
            _mutationTarget = _random.NextDouble();
            _nextMutation = _time + 6.5 + _random.NextDouble() * 16;
        }

        _mutation = Lerp(_mutation, _mutationTarget, 1 - Math.Pow(0.07, delta));

        var volume = Math.Clamp(Volume, 0, 1);
        var progressMotion = _lastProgress < 0 ? 0 : Math.Abs(Progress - _lastProgress);
        _lastProgress = Progress;

        var activeGain = IsActive ? 1 : 0.28;
        var liveBass = Math.Clamp(Bass, 0, 1.4);
        var liveMid = Math.Clamp(Mid, 0, 1.3);
        var liveTreble = Math.Clamp(Treble, 0, 1.4);
        var liveLoudness = Math.Clamp(Loudness, 0, 1.2);
        var livePeak = Math.Clamp(Peak, 0, 1.5);
        var hasLiveAudio = liveBass + liveMid + liveTreble + liveLoudness > 0.025;
        var bassRise = liveBass - _lastLiveBass;
        var loudnessRise = liveLoudness - _lastLiveLoudness;
        var kickStrength = Math.Clamp(livePeak * 1.05 + Math.Max(0, bassRise) * 2.0 + Math.Max(0, loudnessRise) * 1.0 + Math.Max(0, liveBass - 0.2) * 0.55, 0, 1.45);
        if (hasLiveAudio && kickStrength > 0.19 && _time - _lastKickTime > 0.12)
        {
            TriggerKick(kickStrength);
        }

        _lastLiveBass = liveBass;
        _lastLiveLoudness = liveLoudness;

        var fallbackBass = SmoothPulse(_time * 1.17 + Noise(_time * 0.09) * 4);
        var fallbackMid = Noise01(_time * 0.23 + Progress * 0.013);
        var fallbackTreble = Noise01(_time * 1.61 + _mutation * 5.3);

        var bassTarget = activeGain * (hasLiveAudio
            ? 0.04 + liveBass * 0.98 + livePeak * 0.42 + liveLoudness * 0.15
            : 0.35 + volume * 0.45 + fallbackBass * 0.45);
        var midTarget = activeGain * (hasLiveAudio
            ? 0.05 + liveMid * 0.78 + liveLoudness * 0.18
            : 0.28 + volume * 0.34 + fallbackMid * 0.5);
        var trebleTarget = activeGain * (hasLiveAudio
            ? 0.04 + liveTreble * 0.88 + livePeak * 0.16
            : 0.2 + volume * 0.28 + fallbackTreble * 0.62 + progressMotion * 0.02);

        _bass = Lerp(_bass, bassTarget, bassTarget > _bass ? 1 - Math.Pow(0.00001, delta) : 1 - Math.Pow(0.012, delta));
        _mid = Lerp(_mid, midTarget, midTarget > _mid ? 1 - Math.Pow(0.0004, delta) : 1 - Math.Pow(0.02, delta));
        _treble = Lerp(_treble, trebleTarget, trebleTarget > _treble ? 1 - Math.Pow(0.0007, delta) : 1 - Math.Pow(0.045, delta));
        _energy = Lerp(_energy, Math.Clamp((_bass * 0.52 + _mid * 0.3 + _treble * 0.18 + liveLoudness * 0.22), 0, 1.35), 1 - Math.Pow(0.002, delta));
        var burstTarget = hasLiveAudio ? Math.Clamp(livePeak * 0.9 + Math.Pow(liveBass, 2.3) * 0.46, 0, 1.25) : Math.Pow(_bass, 3.2);
        _burst = Lerp(_burst, burstTarget, burstTarget > _burst ? 1 - Math.Pow(0.000001, delta) : 1 - Math.Pow(0.035, delta));
        _impact = Lerp(_impact, Math.Clamp(kickStrength * 0.78 + _burst * 0.24, 0, 1.45), kickStrength + _burst > _impact ? 1 - Math.Pow(0.000001, delta) : 1 - Math.Pow(0.018, delta));
        _dropEnergy = Lerp(_dropEnergy, Math.Clamp(liveLoudness * 0.86 + liveBass * 0.28 + liveMid * 0.16, 0, 1.18), 1 - Math.Pow(liveLoudness > _dropEnergy ? 0.001 : 0.04, delta));
        _sparkBurst = Lerp(_sparkBurst, Math.Clamp(livePeak * 0.72 + liveTreble * 0.68, 0, 1.2), 1 - Math.Pow(livePeak + liveTreble > _sparkBurst ? 0.0008 : 0.06, delta));

        var hitShake = livePeak * 0.08 + _impact * 0.04 + _burst * 0.04;
        _cameraX = Lerp(_cameraX, Noise(_time * 0.033) * (0.04 + _mid * 0.06 + hitShake), 1 - Math.Pow(0.012, delta));
        _cameraY = Lerp(_cameraY, Noise(_time * 0.041 + 12.7) * (0.036 + _mid * 0.05 + hitShake), 1 - Math.Pow(0.012, delta));
        _cameraZoom = Lerp(_cameraZoom, 1 + _impact * 0.07 + _burst * 0.09 + _bass * 0.035 + _dropEnergy * 0.04 + Noise01(_time * 0.021) * 0.02, 1 - Math.Pow(0.018, delta));

        UpdateParticles(delta);
        UpdateShockwaves(delta);
        InvalidateVisual();
    }

    protected override void OnRender(DrawingContext dc)
    {
        var width = ActualWidth;
        var height = ActualHeight;
        if (width <= 0 || height <= 0)
        {
            return;
        }

        var center = new Point(width * (0.5 + _cameraX), height * (0.52 + _cameraY));
        var scene = GetSceneWeights();

        DrawAtmosphere(dc, width, height, center);
        DrawPlasmaClouds(dc, width, height, center, scene.Plasma);
        DrawShockwaves(dc, width, height, center);
        DrawWaveField(dc, width, height, center, scene.Wave);
        DrawTunnel(dc, width, height, center, scene.Tunnel);
        DrawRibbons(dc, width, height, center, scene.Ribbon);
        DrawKaleidoscope(dc, width, height, center, scene.Bloom);
        DrawParticles(dc, width, height, center);
        DrawVignette(dc, width, height);
    }

    private void TriggerKick(double strength)
    {
        _lastKickTime = _time;
        _impact = Math.Max(_impact, strength * 0.9);
        _burst = Math.Max(_burst, strength * 0.82);
        _sparkBurst = Math.Max(_sparkBurst, strength * 0.52);

        _shockwaves.Add(new Shockwave
        {
            Age = 0,
            Strength = strength * 0.78,
            HueOffset = _random.NextDouble() * 4,
            Warp = 0.7 + _random.NextDouble() * 0.9
        });

        if (_shockwaves.Count > 8)
        {
            _shockwaves.RemoveAt(0);
        }

        var particlePush = 0.34 + strength * 0.85;
        for (var i = 0; i < _particles.Length; i += 4)
        {
            var p = _particles[i];
            var dx = p.X - 0.5;
            var dy = p.Y - 0.5;
            var length = Math.Max(0.001, Math.Sqrt(dx * dx + dy * dy));
            p.Vx += dx / length * particlePush * (0.08 + _random.NextDouble() * 0.08);
            p.Vy += dy / length * particlePush * (0.08 + _random.NextDouble() * 0.08);
            p.Alpha = Math.Min(1.08, p.Alpha + strength * 0.16);
        }
    }

    private void UpdateParticles(double delta)
    {
        for (var i = 0; i < _particles.Length; i++)
        {
            var p = _particles[i];
            var pull = 0.16 + _bass * 0.12 - _impact * 0.02;
            var swirl = 0.78 + _mid * 2.1 + _dropEnergy * 1.2 + p.Speed * 0.28;
            var noiseAngle = Noise((p.X + _time * 0.045) * 2.7 + i * 0.031) * Math.PI;
            var dx = p.X - 0.5;
            var dy = p.Y - 0.5;

            var blast = _impact * 0.36 + _burst * 0.24;
            p.Vx += (-dy * swirl + Math.Cos(noiseAngle) * (0.18 + _treble * 0.2) - dx * pull + dx * blast) * delta * 0.1;
            p.Vy += (dx * swirl + Math.Sin(noiseAngle) * (0.18 + _treble * 0.2) - dy * pull + dy * blast) * delta * 0.1;
            p.Vx *= Math.Pow(0.13, delta);
            p.Vy *= Math.Pow(0.13, delta);
            p.X += p.Vx * delta;
            p.Y += p.Vy * delta;
            p.Depth = Frac(p.Depth + delta * (0.05 + _bass * 0.08 + _dropEnergy * 0.04) * p.Speed);
            p.Alpha = Math.Max(0.18, p.Alpha - delta * 0.08);

            if (p.X < -0.14 || p.X > 1.14 || p.Y < -0.18 || p.Y > 1.18)
            {
                ResetParticle(p, i);
            }
        }
    }

    private void UpdateShockwaves(double delta)
    {
        for (var i = _shockwaves.Count - 1; i >= 0; i--)
        {
            var shockwave = _shockwaves[i];
            shockwave.Age += delta;
            if (shockwave.Age > 1.45)
            {
                _shockwaves.RemoveAt(i);
            }
            else
            {
                _shockwaves[i] = shockwave;
            }
        }
    }

    private void DrawAtmosphere(DrawingContext dc, double width, double height, Point center)
    {
        var colorA = Palette(0, 0.72);
        var colorB = Palette(1.7, 0.62);
        var back = new RadialGradientBrush
        {
            Center = new Point(center.X / width, center.Y / height),
            GradientOrigin = new Point(center.X / width, center.Y / height),
            RadiusX = 0.86 * _cameraZoom,
            RadiusY = 1.05 * _cameraZoom
        };
        back.GradientStops.Add(new GradientStop(Color.FromArgb(255, 8, 4, 19), 0));
        back.GradientStops.Add(new GradientStop(Color.FromArgb((byte)Math.Clamp(28 + _energy * 34 + _impact * 28 + _dropEnergy * 18, 0, 130), colorA.R, colorA.G, colorA.B), 0.28));
        back.GradientStops.Add(new GradientStop(Color.FromArgb((byte)Math.Clamp(14 + _mid * 24 + _bass * 16 + _dropEnergy * 14, 0, 85), colorB.R, colorB.G, colorB.B), 0.62));
        back.GradientStops.Add(new GradientStop(Color.FromRgb(1, 2, 6), 1));
        dc.DrawRectangle(back, null, new Rect(0, 0, width, height));

        for (var i = 0; i < 3; i++)
        {
            var color = Palette(i * 0.9, 0.68);
            var x = width * (0.2 + i * 0.3 + Noise(_time * (0.018 + i * 0.004) + i) * 0.05);
            var y = height * (0.34 + Noise(_time * (0.015 + i * 0.005) + 9 + i) * 0.34);
            var brush = new RadialGradientBrush();
            brush.GradientStops.Add(new GradientStop(Color.FromArgb((byte)Math.Clamp(10 + _energy * 20 + _impact * 32, 0, 82), color.R, color.G, color.B), 0));
            brush.GradientStops.Add(new GradientStop(Color.FromArgb((byte)Math.Clamp(4 + _energy * 10 + _impact * 18, 0, 45), 190, 235, 255), 0.12));
            brush.GradientStops.Add(new GradientStop(Color.FromArgb(0, 0, 0, 0), 1));
            dc.DrawEllipse(brush, null, new Point(x, y), width * (0.16 + _bass * 0.08 + _impact * 0.03), height * (0.22 + _mid * 0.06 + _impact * 0.04));
        }
    }

    private void DrawShockwaves(DrawingContext dc, double width, double height, Point center)
    {
        foreach (var shockwave in _shockwaves)
        {
            var t = Math.Clamp(shockwave.Age / 1.45, 0, 1);
            var ease = 1 - Math.Pow(1 - t, 2.5);
            var fade = Math.Pow(1 - t, 1.45);
            var color = Palette(shockwave.HueOffset + t * 1.5, 0.95);
            var radius = Math.Min(width, height) * (0.08 + ease * (0.82 + shockwave.Strength * 0.28));
            var alpha = (byte)Math.Clamp((62 + shockwave.Strength * 78) * fade, 0, 150);
            var pen = FrozenPen(Color.FromArgb(alpha, color.R, color.G, color.B), 1.2 + shockwave.Strength * 4.8 * fade);
            dc.DrawEllipse(null, pen, center, radius * (2.2 + shockwave.Warp * 0.3), radius * (0.72 + shockwave.Warp * 0.22));

            var glow = new RadialGradientBrush();
            glow.GradientStops.Add(new GradientStop(Color.FromArgb((byte)Math.Clamp(alpha * 0.42, 0, 85), color.R, color.G, color.B), 0));
            glow.GradientStops.Add(new GradientStop(Color.FromArgb((byte)Math.Clamp(alpha * 0.12, 0, 30), 220, 245, 255), 0.16));
            glow.GradientStops.Add(new GradientStop(Color.FromArgb(0, 0, 0, 0), 1));
            dc.DrawEllipse(glow, null, center, radius * 2.5, radius * 0.9);
        }
    }

    private void DrawPlasmaClouds(DrawingContext dc, double width, double height, Point center, double weight)
    {
        if (weight <= 0.02)
        {
            return;
        }

        for (var i = 0; i < 4; i++)
        {
            var angle = i * Math.PI * 2 / 4 + _time * (0.026 + i * 0.002) + Noise(_time * 0.023 + i) * 0.45;
            var radius = (0.16 + Noise01(_time * 0.027 + i * 3) * 0.24 + _burst * 0.08 + _impact * 0.05 + _dropEnergy * 0.03) * Math.Min(width, height);
            var x = center.X + Math.Cos(angle) * radius * 1.45;
            var y = center.Y + Math.Sin(angle * 1.3) * radius * 0.7;
            var color = Palette(i * 0.6 + _time * 0.035, 0.74);
            var brush = new RadialGradientBrush();
            brush.GradientStops.Add(new GradientStop(Color.FromArgb((byte)Math.Clamp(weight * (10 + _energy * 30 + _impact * 40), 0, 95), color.R, color.G, color.B), 0));
            brush.GradientStops.Add(new GradientStop(Color.FromArgb((byte)Math.Clamp(weight * (5 + _impact * 24), 0, 38), 225, 245, 255), 0.1));
            brush.GradientStops.Add(new GradientStop(Color.FromArgb(0, 0, 0, 0), 1));
            dc.DrawEllipse(brush, null, new Point(x, y), width * (0.13 + _mid * 0.04 + _impact * 0.04), height * (0.18 + _bass * 0.1 + _impact * 0.05));
        }
    }

    private void DrawWaveField(DrawingContext dc, double width, double height, Point center, double weight)
    {
        if (weight <= 0.02)
        {
            return;
        }

        var lines = 4;
        for (var i = 0; i < lines; i++)
        {
            var y = height * (0.24 + i * 0.14);
            var color = Palette(i * 0.45 + 1.2, 0.76);
            var alpha = (byte)Math.Clamp(weight * (14 + _energy * 42 + _impact * 28), 0, 95);
            var pen = FrozenPen(Color.FromArgb(alpha, color.R, color.G, color.B), 0.9 + _mid * 2.6 + _impact * 1.2);
            var geo = new StreamGeometry();
            using (var ctx = geo.Open())
            {
                ctx.BeginFigure(new Point(-20, y), false, false);
                var last = new Point(-20, y);
                for (var x = 0d; x <= width + 30; x += width / 8)
                {
                    var n = Noise(_time * 0.18 + x * 0.009 + i * 0.7 + _mutation * 3);
                    var yy = y + n * height * (0.055 + _mid * 0.1 + _dropEnergy * 0.045) + Math.Sin(x * 0.018 + _time * (1.0 + _bass * 1.1) + i) * height * (0.018 + _impact * 0.025);
                    var cp1 = new Point(last.X + width / 18, last.Y);
                    var cp2 = new Point(x - width / 18, yy);
                    var p = new Point(x, yy);
                    ctx.BezierTo(cp1, cp2, p, true, false);
                    last = p;
                }
            }

            geo.Freeze();
            dc.DrawGeometry(null, pen, geo);
        }
    }

    private void DrawTunnel(DrawingContext dc, double width, double height, Point center, double weight)
    {
        if (weight <= 0.02)
        {
            return;
        }

        var rings = 10;
        for (var i = rings; i >= 0; i--)
        {
            var depth = Frac(i / (double)rings + _time * (0.045 + _bass * 0.055 + _dropEnergy * 0.02));
            var radius = Math.Pow(depth, 1.8) * Math.Min(width, height) * (0.5 + _burst * 0.14 + _impact * 0.12);
            var color = Palette(depth * 3 + _time * 0.04, 0.82);
            var alpha = (byte)Math.Clamp(weight * depth * (18 + _energy * 52 + _impact * 58), 0, 130);
            var pen = FrozenPen(Color.FromArgb(alpha, color.R, color.G, color.B), 0.8 + depth * 2.8 + _burst * 2.2 + _impact * 2.6);
            dc.DrawEllipse(null, pen, center, radius * 2.15, radius * 0.72);
        }
    }

    private void DrawRibbons(DrawingContext dc, double width, double height, Point center, double weight)
    {
        if (weight <= 0.02)
        {
            return;
        }

        for (var layer = 0; layer < 3; layer++)
        {
            var color = Palette(layer * 0.68 + _time * 0.025, 0.9);
            for (var trail = 4; trail >= 0; trail--)
            {
                var alpha = (byte)Math.Clamp(weight * (18 + _energy * 54 + _impact * 42) * (1 - trail * 0.13), 0, 125);
                var thickness = Math.Max(0.7, (5 - trail) * (0.36 + _energy * 0.36 + _impact * 0.18));
                var pen = FrozenPen(Color.FromArgb(alpha, color.R, color.G, color.B), thickness);
                var t = _time - trail * 0.13;
                dc.DrawGeometry(null, pen, BuildRibbon(width, height, center, layer, t, 1));
                dc.DrawGeometry(null, pen, BuildRibbon(width, height, center, layer + 3, -t * 0.92, -1));
            }
        }
    }

    private Geometry BuildRibbon(double width, double height, Point center, int layer, double t, int mirror)
    {
        var geo = new StreamGeometry();
        using var ctx = geo.Open();

        var yBase = center.Y + mirror * height * (Noise(t * 0.041 + layer) * 0.16 + (layer - 2) * 0.035);
        var amp = height * (0.09 + _mid * 0.13 + _dropEnergy * 0.055 + _impact * 0.045 + Noise01(t * 0.033 + layer * 1.7) * 0.055);
        var start = new Point(-width * 0.12, yBase + Math.Sin(t + layer) * amp * 0.35);
        ctx.BeginFigure(start, false, false);

        var last = start;
        for (var i = 0; i < 5; i++)
        {
            var x = width * (0.12 + i * 0.25);
            var wobble = t * (0.76 + layer * 0.06 + _bass * 0.36) + i * 0.82 + Noise(t * 0.025 + i + layer) * (1.05 + _mid * 0.6);
            var p = new Point(
                x,
                yBase + Math.Sin(wobble) * amp + Math.Cos(wobble * 0.43 + layer) * amp * 0.42);
            var cp1 = new Point(last.X + width * 0.11, last.Y + Math.Cos(wobble) * amp * 0.35);
            var cp2 = new Point(p.X - width * 0.11, p.Y - Math.Sin(wobble * 1.2) * amp * 0.35);
            ctx.BezierTo(cp1, cp2, p, true, false);
            last = p;
        }

        geo.Freeze();
        return geo;
    }

    private void DrawKaleidoscope(DrawingContext dc, double width, double height, Point center, double weight)
    {
        if (weight <= 0.02)
        {
            return;
        }

        var spokes = 4;
        var length = Math.Min(width, height) * (0.36 + _burst * 0.18 + _impact * 0.12);
        for (var i = 0; i < spokes; i++)
        {
            var angle = i * 360d / spokes + _time * (6 + _mid * 12 + _dropEnergy * 5) + Noise(_time * 0.05 + i) * (10 + _impact * 7);
            dc.PushTransform(new RotateTransform(angle, center.X, center.Y));

            var color = Palette(i * 0.5 + _time * 0.055, 0.95);
            var alpha = (byte)Math.Clamp(weight * (14 + _treble * 54 + _sparkBurst * 34), 0, 98);
            var pen = FrozenPen(Color.FromArgb(alpha, color.R, color.G, color.B), 0.9 + _treble * 2.3 + _sparkBurst * 1.2);

            var geo = new StreamGeometry();
            using (var ctx = geo.Open())
            {
                ctx.BeginFigure(center, false, false);
                ctx.BezierTo(
                    new Point(center.X + length * 0.18, center.Y - length * 0.22),
                    new Point(center.X + length * 0.52, center.Y + length * (0.18 + Noise(_time * 0.09 + i) * 0.18)),
                    new Point(center.X + length, center.Y + Noise(_time * 0.11 + i * 2) * length * 0.12),
                    true,
                    false);
            }

            geo.Freeze();
            dc.DrawGeometry(null, pen, geo);
            dc.Pop();
        }

        var glow = new RadialGradientBrush();
        glow.GradientStops.Add(new GradientStop(Color.FromArgb((byte)Math.Clamp(weight * (28 + _burst * 80 + _impact * 72), 0, 150), 230, 250, 255), 0));
        glow.GradientStops.Add(new GradientStop(Color.FromArgb((byte)Math.Clamp(weight * (24 + _impact * 42), 0, 82), 74, 210, 238), 0.28));
        glow.GradientStops.Add(new GradientStop(Color.FromArgb(0, 0, 0, 0), 1));
        dc.DrawEllipse(glow, null, center, width * (0.08 + _burst * 0.06 + _impact * 0.04), height * (0.18 + _burst * 0.08 + _impact * 0.06));
    }

    private void DrawParticles(DrawingContext dc, double width, double height, Point center)
    {
        for (var i = 0; i < _particles.Length; i++)
        {
            if (i % 3 == 0 && _energy < 0.45 && _sparkBurst < 0.25)
            {
                continue;
            }

            var p = _particles[i];
            var parallax = 0.72 + p.Depth * 0.68;
            var x = center.X + (p.X - 0.5) * width * parallax * _cameraZoom;
            var y = center.Y + (p.Y - 0.5) * height * parallax * _cameraZoom;
            var color = Palette(p.ColorOffset + p.Depth * 2, 0.85);
            var alpha = (byte)Math.Clamp((12 + _treble * 58 + _sparkBurst * 48 + _dropEnergy * 20) * (0.36 + p.Depth) * p.Alpha, 0, 150);
            var size = p.Size * (0.55 + p.Depth * 1.55 + _burst * 1.1 + _impact * 1.25);

            var brush = new SolidColorBrush(Color.FromArgb(alpha, color.R, color.G, color.B));
            brush.Freeze();
            dc.DrawEllipse(brush, null, new Point(x, y), size, size);

            if ((_treble > 0.32 || _sparkBurst > 0.22) && p.Depth > 0.46)
            {
                var pen = FrozenPen(Color.FromArgb((byte)Math.Clamp(alpha * 0.42, 0, 82), color.R, color.G, color.B), 0.5 + _sparkBurst * 0.75);
                dc.DrawLine(pen, new Point(x - p.Vx * width * (0.35 + _impact * 0.45), y - p.Vy * height * (0.35 + _impact * 0.45)), new Point(x, y));
            }
        }
    }

    private void DrawVignette(DrawingContext dc, double width, double height)
    {
        var vignette = new RadialGradientBrush
        {
            Center = new Point(0.5, 0.5),
            GradientOrigin = new Point(0.5, 0.5),
            RadiusX = 0.7,
            RadiusY = 0.9
        };
        vignette.GradientStops.Add(new GradientStop(Color.FromArgb(0, 0, 0, 0), 0.3));
        vignette.GradientStops.Add(new GradientStop(Color.FromArgb(226, 0, 0, 0), 1));
        dc.DrawRectangle(vignette, null, new Rect(0, 0, width, height));
    }

    private SceneWeights GetSceneWeights()
    {
        var ribbon = Weight(_time, 0.017, 0.2);
        var tunnel = Weight(_time, 0.013, 1.9);
        var plasma = Weight(_time, 0.011, 3.4);
        var bloom = Weight(_time, 0.019, 5.2);
        var wave = Weight(_time, 0.015, 7.8);

        ribbon += _mid * 0.34 + _dropEnergy * 0.12;
        tunnel += _bass * 0.52 + _impact * 0.34;
        bloom += _burst * 0.34 + _impact * 0.38;
        wave += _treble * 0.2 + _mid * 0.14;
        plasma += _mutation * 0.12 + _dropEnergy * 0.22;

        var total = ribbon + tunnel + plasma + bloom + wave;
        return new SceneWeights(ribbon / total, tunnel / total, plasma / total, bloom / total, wave / total);
    }

    private static double Weight(double time, double speed, double phase)
    {
        var value = 0.5 + 0.5 * Math.Sin(time * speed + phase + Math.Sin(time * speed * 0.37 + phase) * 1.6);
        return 0.24 + SmoothStep(value) * 0.82;
    }

    private Color Palette(double offset, double saturation)
    {
        var t = _time * 0.028 + _mutation * 2.4 + offset;
        var a = _palette[(int)Math.Floor(t) % _palette.Length];
        var b = _palette[((int)Math.Floor(t) + 1) % _palette.Length];
        var mix = SmoothStep(Frac(t));
        var color = Mix(a, b, mix);
        var floor = 18 * (1 - saturation);
        return Color.FromRgb(
            (byte)Math.Clamp(color.R * saturation + floor, 0, 255),
            (byte)Math.Clamp(color.G * saturation + floor, 0, 255),
            (byte)Math.Clamp(color.B * saturation + floor, 0, 255));
    }

    private Particle CreateParticle(int index)
    {
        var angle = _random.NextDouble() * Math.PI * 2;
        var radius = Math.Sqrt(_random.NextDouble()) * 0.47;
        return new Particle
        {
            X = 0.5 + Math.Cos(angle) * radius,
            Y = 0.5 + Math.Sin(angle) * radius,
            Vx = Noise(index * 1.17) * 0.02,
            Vy = Noise(index * 2.11 + 4) * 0.02,
            Depth = _random.NextDouble(),
            Speed = 0.6 + _random.NextDouble() * 1.7,
            Size = 0.45 + _random.NextDouble() * 1.15,
            Alpha = 0.28 + _random.NextDouble() * 0.62,
            ColorOffset = _random.NextDouble() * 4
        };
    }

    private void ResetParticle(Particle particle, int index)
    {
        var replacement = CreateParticle(index);
        particle.X = replacement.X;
        particle.Y = replacement.Y;
        particle.Vx = replacement.Vx;
        particle.Vy = replacement.Vy;
        particle.Depth = replacement.Depth;
        particle.Speed = replacement.Speed;
        particle.Size = replacement.Size;
        particle.Alpha = replacement.Alpha;
        particle.ColorOffset = replacement.ColorOffset;
    }

    private static Pen FrozenPen(Color color, double thickness)
    {
        var pen = new Pen(new SolidColorBrush(color), thickness)
        {
            StartLineCap = PenLineCap.Round,
            EndLineCap = PenLineCap.Round,
            LineJoin = PenLineJoin.Round
        };
        pen.Freeze();
        return pen;
    }

    private static Color Mix(Color a, Color b, double t)
        => Color.FromRgb(
            (byte)Math.Clamp(Lerp(a.R, b.R, t), 0, 255),
            (byte)Math.Clamp(Lerp(a.G, b.G, t), 0, 255),
            (byte)Math.Clamp(Lerp(a.B, b.B, t), 0, 255));

    private static double Noise01(double value)
        => Noise(value) * 0.5 + 0.5;

    private static double Noise(double value)
    {
        var i = Math.Floor(value);
        var f = value - i;
        var u = f * f * (3 - 2 * f);
        return Lerp(Hash(i), Hash(i + 1), u);
    }

    private static double Hash(double value)
        => Frac(Math.Sin(value * 127.1 + 311.7) * 43758.5453123) * 2 - 1;

    private static double SmoothPulse(double value)
    {
        var pulse = Math.Max(0, Math.Sin(value));
        return pulse * pulse * pulse * pulse;
    }

    private static double SmoothStep(double value)
    {
        value = Math.Clamp(value, 0, 1);
        return value * value * (3 - 2 * value);
    }

    private static double Lerp(double a, double b, double t)
        => a + (b - a) * Math.Clamp(t, 0, 1);

    private static double Frac(double value)
        => value - Math.Floor(value);

    private sealed class Particle
    {
        public double X;
        public double Y;
        public double Vx;
        public double Vy;
        public double Depth;
        public double Speed;
        public double Size;
        public double Alpha;
        public double ColorOffset;
    }

    private struct Shockwave
    {
        public double Age;
        public double Strength;
        public double HueOffset;
        public double Warp;
    }

    private readonly record struct SceneWeights(double Ribbon, double Tunnel, double Plasma, double Bloom, double Wave);
}
