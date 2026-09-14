using System;
using System.Windows;
using System.Windows.Media;

namespace XboxMetroLauncher.Controls;

public sealed class BladesLoadingIndicator : FrameworkElement
{
    private bool _running;
    private double _seconds;
    public BladesLoadingIndicator()
    {
        IsHitTestVisible = false;
        Loaded += (_, _) => Run(IsVisible);
        IsVisibleChanged += (_, _) => Run(IsLoaded && IsVisible);
        Unloaded += (_, _) => Run(false);
    }
    private void Run(bool value)
    {
        if (_running == value) return;
        _running = value;
        if (value) CompositionTarget.Rendering += Tick;
        else CompositionTarget.Rendering -= Tick;
    }
    private void Tick(object? sender, EventArgs args)
    {
        _seconds = ((RenderingEventArgs)args).RenderingTime.TotalSeconds;
        InvalidateVisual();
    }
    protected override void OnRender(DrawingContext dc)
    {
        double scale = Math.Min(ActualWidth, ActualHeight) / 100;
        dc.PushTransform(new ScaleTransform(scale,scale));
        for (int i=0; i<16; i++)
        {
            double angle = i*Math.PI/8 - Math.PI/2;
            double behind = ((_seconds*1.7*16-i)%16+16)%16;
            byte alpha = (byte)(35+180*Math.Exp(-behind*0.85));
            dc.DrawEllipse(new SolidColorBrush(Color.FromArgb(alpha,230,244,231)),null,
                new Point(50+36*Math.Cos(angle),50+36*Math.Sin(angle)),3.5,3.5);
        }
        // The stationary waisted console silhouette remains inside the orbiting dots.
        var console = Geometry.Parse("M40,29 L60,29 Q55,50 60,71 L40,71 Q45,50 40,29 Z");
        dc.DrawGeometry(new SolidColorBrush(Color.FromArgb(150,33,56,56)),null,console);
        dc.DrawEllipse(new SolidColorBrush(Color.FromRgb(189,225,155)),null,new Point(50,61),2,2);
        dc.Pop();
    }
}
