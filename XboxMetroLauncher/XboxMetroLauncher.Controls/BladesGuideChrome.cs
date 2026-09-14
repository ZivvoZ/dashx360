using System;
using System.Globalization;
using System.Windows;
using System.Windows.Media;

namespace XboxMetroLauncher.Controls;

public sealed class BladesGuideChrome : FrameworkElement
{
    protected override void OnRender(DrawingContext dc)
    {
        double w = ActualWidth;
        if (w < 70) return;
        string N(double n) => n.ToString(CultureInfo.InvariantCulture);
        var body = Geometry.Parse($"M0,0 L{N(w-16)},0 L{N(w-29)},86 Q{N(w-10)},83 {N(w-10)},98 L{N(w-24)},462 Q{N(w-22)},482 {N(w-38)},517 Q{N(w-60)},550 {N(w-16)},720 L0,720 Z");
        var gold = new LinearGradientBrush();
        gold.StartPoint = new Point(0,0); gold.EndPoint = new Point(1,0);
        gold.GradientStops.Add(new GradientStop(Color.FromRgb(244,195,29),0));
        gold.GradientStops.Add(new GradientStop(Color.FromRgb(255,216,57),0.6));
        gold.GradientStops.Add(new GradientStop(Color.FromRgb(216,169,29),1));
        dc.DrawGeometry(gold,new Pen(new SolidColorBrush(Color.FromRgb(70,67,47)),3),body);
        dc.PushClip(body);
        var patternPen = new Pen(new SolidColorBrush(Color.FromArgb(13,129,93,0)),2);
        for (int y=7; y<720; y+=44)
        for (int x=18; x<w-45; x+=57)
        {
            var p = new Point(x+((y/44)%2)*21,y);
            dc.DrawEllipse(null,patternPen,p,10,10);
            dc.DrawLine(patternPen,new Point(p.X-6,p.Y-7),new Point(p.X+6,p.Y+7));
            dc.DrawLine(patternPen,new Point(p.X+6,p.Y-7),new Point(p.X-6,p.Y+7));
        }
        var cap = new LinearGradientBrush();
        cap.StartPoint=new Point(0,0); cap.EndPoint=new Point(0,1);
        cap.GradientStops.Add(new GradientStop(Color.FromRgb(64,60,31),0));
        cap.GradientStops.Add(new GradientStop(Color.FromRgb(166,139,13),0.5));
        cap.GradientStops.Add(new GradientStop(Color.FromRgb(230,187,15),1));
        var top = Geometry.Parse($"M0,0 L{N(w-39)},0 L{N(w-55)},76 Q{N(w-57)},95 {N(w-88)},95 L0,95 Z");
        dc.DrawGeometry(cap,new Pen(new SolidColorBrush(Color.FromRgb(232,233,202)),2),top);
        dc.DrawRectangle(cap,null,new Rect(0,574,w,146));
        dc.DrawLine(new Pen(new SolidColorBrush(Color.FromRgb(227,231,199)),2),new Point(0,574),new Point(w-57,574));
        dc.Pop();
        var edge = Geometry.Parse($"M{N(w-31)},0 L{N(w-45)},84 Q{N(w-67)},250 {N(w-61)},464 Q{N(w-60)},507 {N(w-56)},552 Q{N(w-48)},638 {N(w-31)},720");
        dc.DrawGeometry(null,new Pen(new SolidColorBrush(Color.FromRgb(250,242,185)),3),edge);
        var shadow = edge.Clone();
        shadow.Transform = new TranslateTransform(-5,0);
        dc.DrawGeometry(null,new Pen(new SolidColorBrush(Color.FromRgb(91,85,46)),3),shadow);
    }
}
