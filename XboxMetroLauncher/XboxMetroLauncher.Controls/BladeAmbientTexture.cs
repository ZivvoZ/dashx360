using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace XboxMetroLauncher.Controls;

public sealed class BladeAmbientTexture : FrameworkElement
{
    public static readonly DependencyProperty TintProperty = DependencyProperty.Register(nameof(Tint), typeof(Color), typeof(BladeAmbientTexture), new PropertyMetadata(Color.FromRgb(214,147,54), OnTintChanged));
    public Color Tint { get => (Color)GetValue(TintProperty); set => SetValue(TintProperty,value); }
    private readonly WriteableBitmap _bitmap = new(384,216,96,96,PixelFormats.Pbgra32,null);
    private readonly byte[] _pixels = new byte[384*216*4];
    private bool _running;
    private double _lastFrame = -1;
    private static double _timelineSeconds;
    private readonly double[] _columnWave = new double[384];
    public BladeAmbientTexture()
    {
        IsHitTestVisible = false;
        Loaded += (_,_) => SetRunning(IsVisible);
        IsVisibleChanged += (_,_) => SetRunning(IsLoaded && IsVisible);
        Unloaded += (_,_) => SetRunning(false);
        DrawFrame(_timelineSeconds);
    }
    private static void OnTintChanged(DependencyObject target, DependencyPropertyChangedEventArgs args)
    {
        var texture = (BladeAmbientTexture)target;
        texture.DrawFrame(_timelineSeconds);
    }
    private void SetRunning(bool running)
    {
        if (_running == running) return;
        _running = running;
        if (running) CompositionTarget.Rendering += Tick;
        else CompositionTarget.Rendering -= Tick;
    }
    private void Tick(object? sender, EventArgs args)
    {
        double seconds = ((RenderingEventArgs)args).RenderingTime.TotalSeconds;
        _timelineSeconds = seconds;
        if (seconds - _lastFrame < 1.0/30.0) return;
        _lastFrame = seconds;
        DrawFrame(seconds);
    }
    private void DrawFrame(double seconds)
    {
        Color tint = Tint;
        double phase = seconds * 0.8;
        double drift = 0.11*Math.Cos(phase*0.73);
        for (int x=0; x<384; x++) _columnWave[x] = 0.12*Math.Sin((x/383.0-0.51)*6.2+phase*0.6);
        for (int y=0; y<216; y++)
        {
        double v = (y/215.0-0.49)*1.7;
        double warp = 0.14*Math.Sin(v*3+phase), b = v+drift;
        for (int x=0; x<384; x++)
        {
            double u = (x/383.0-0.51)*2;
            double a = u + warp;
            double radius = Math.Sqrt(a*a*0.83+b*b);
            double band = (radius-0.65-_columnWave[x])*4.2;
            double ribbon = Math.Exp(-band*band);
            double light = 0.69+ribbon*0.57+Math.Sin(u*3.4+v*2.8+phase*0.8)*0.08+Math.Sin(radius*9-phase)*0.045;
            int i=(y*384+x)*4;
            _pixels[i]=(byte)Math.Clamp(tint.B*light+ribbon*5,0,255);
            _pixels[i+1]=(byte)Math.Clamp(tint.G*light+ribbon*25,0,255);
            _pixels[i+2]=(byte)Math.Clamp(tint.R*light+ribbon*18,0,255);
            _pixels[i+3]=255;
        }
        }
        _bitmap.WritePixels(new Int32Rect(0,0,384,216),_pixels,384*4,0);
        InvalidateVisual();
    }
    protected override void OnRender(DrawingContext dc) => dc.DrawImage(_bitmap,new Rect(RenderSize));
}
