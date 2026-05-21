using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace XboxMetroLauncher.Controls;

public partial class MetroTile : UserControl
{
    public static readonly DependencyProperty TitleProperty =
        DependencyProperty.Register(nameof(Title), typeof(string), typeof(MetroTile), new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty SubtitleProperty =
        DependencyProperty.Register(nameof(Subtitle), typeof(string), typeof(MetroTile), new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty ImagePathProperty =
        DependencyProperty.Register(nameof(ImagePath), typeof(string), typeof(MetroTile), new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty TileBrushProperty =
        DependencyProperty.Register(nameof(TileBrush), typeof(Brush), typeof(MetroTile), new PropertyMetadata(Brushes.SeaGreen));

    public static readonly DependencyProperty TileWidthProperty =
        DependencyProperty.Register(nameof(TileWidth), typeof(double), typeof(MetroTile), new PropertyMetadata(250d));

    public static readonly DependencyProperty TileHeightProperty =
        DependencyProperty.Register(nameof(TileHeight), typeof(double), typeof(MetroTile), new PropertyMetadata(140d));

    public static readonly DependencyProperty CommandProperty =
        DependencyProperty.Register(nameof(Command), typeof(ICommand), typeof(MetroTile), new PropertyMetadata(null));

    public static readonly DependencyProperty CommandParameterProperty =
        DependencyProperty.Register(nameof(CommandParameter), typeof(object), typeof(MetroTile), new PropertyMetadata(null));

    public static readonly DependencyProperty FocusCommandProperty =
        DependencyProperty.Register(nameof(FocusCommand), typeof(ICommand), typeof(MetroTile), new PropertyMetadata(null));

    public MetroTile()
    {
        InitializeComponent();
    }

    public string Title
    {
        get => (string)GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    public string Subtitle
    {
        get => (string)GetValue(SubtitleProperty);
        set => SetValue(SubtitleProperty, value);
    }

    public string ImagePath
    {
        get => (string)GetValue(ImagePathProperty);
        set => SetValue(ImagePathProperty, value);
    }

    public Brush TileBrush
    {
        get => (Brush)GetValue(TileBrushProperty);
        set => SetValue(TileBrushProperty, value);
    }

    public double TileWidth
    {
        get => (double)GetValue(TileWidthProperty);
        set => SetValue(TileWidthProperty, value);
    }

    public double TileHeight
    {
        get => (double)GetValue(TileHeightProperty);
        set => SetValue(TileHeightProperty, value);
    }

    public ICommand? Command
    {
        get => (ICommand?)GetValue(CommandProperty);
        set => SetValue(CommandProperty, value);
    }

    public object? CommandParameter
    {
        get => GetValue(CommandParameterProperty);
        set => SetValue(CommandParameterProperty, value);
    }

    public ICommand? FocusCommand
    {
        get => (ICommand?)GetValue(FocusCommandProperty);
        set => SetValue(FocusCommandProperty, value);
    }

    private void TileButton_OnGotKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        if (FocusCommand?.CanExecute(CommandParameter) == true)
        {
            FocusCommand.Execute(CommandParameter);
        }
    }
}
