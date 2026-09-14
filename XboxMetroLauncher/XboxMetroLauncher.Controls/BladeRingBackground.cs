using System;
using System.Windows;
using System.Windows.Media;

namespace XboxMetroLauncher.Controls;

public sealed class BladeRingBackground : FrameworkElement
{
    public static readonly DependencyProperty ShowGlareProperty = DependencyProperty.Register(nameof(ShowGlare), typeof(bool), typeof(BladeRingBackground), new FrameworkPropertyMetadata(false,FrameworkPropertyMetadataOptions.AffectsRender));
    public bool ShowGlare { get => (bool)GetValue(ShowGlareProperty); set => SetValue(ShowGlareProperty,value); }
    private bool _running;
    private double _seconds;
    public BladeRingBackground()
    {
        IsHitTestVisible=false; ClipToBounds=true;
        Loaded += (_,_) => SetRunning(IsVisible);
        IsVisibleChanged += (_,_) => SetRunning(IsLoaded && IsVisible);
        Unloaded += (_,_) => SetRunning(false);
    }
    private void SetRunning(bool running)
    {
        if (_running==running) return;
        _running=running;
        if (running) CompositionTarget.Rendering+=Tick;
        else CompositionTarget.Rendering-=Tick;
    }
    private void Tick(object? sender,EventArgs args)
    {
        double seconds=((RenderingEventArgs)args).RenderingTime.TotalSeconds;
        if (seconds-_seconds<1.0/30.0) return;
        _seconds=seconds; InvalidateVisual();
    }
    protected override void OnRender(DrawingContext dc)
    {
        double h=ActualHeight,w=ActualWidth;
        if (h<=0 || w<=0) return;
        double baseRadius=Math.Min(h*0.29,54);
        for (int i=0;i<4;i++)
        {
            double cycle=(_seconds/2.4+i/4.0)%1;
            double radius=baseRadius+cycle*Math.Min(h*0.55,70);
            byte alpha=(byte)(185*Math.Sin(Math.PI*cycle));
            dc.DrawEllipse(null,new Pen(new SolidColorBrush(Color.FromArgb(alpha,181,204,40)),1.8),new Point(-baseRadius*0.32,h*0.5),radius,radius);
        }
        if (!ShowGlare) return;
        var edge=new LinearGradientBrush(Color.FromArgb(125,255,255,255),Colors.Transparent,0);
        dc.DrawRectangle(edge,null,new Rect(0,0,12,h));
        double x=(_seconds%5.0)/2.3*(w+120)-100;
        var glare=new LinearGradientBrush();
        glare.GradientStops.Add(new GradientStop(Colors.Transparent,0));
        glare.GradientStops.Add(new GradientStop(Color.FromArgb(58,255,255,255),0.5));
        glare.GradientStops.Add(new GradientStop(Colors.Transparent,1));
        dc.PushTransform(new SkewTransform(-12,0));
        dc.DrawRectangle(glare,null,new Rect(x,0,80,h));
        dc.Pop();
    }
}
