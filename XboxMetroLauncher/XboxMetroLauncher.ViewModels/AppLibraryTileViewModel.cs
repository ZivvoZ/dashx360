using System.Windows.Media;

namespace XboxMetroLauncher.ViewModels;

public sealed class AppLibraryTileViewModel : ObservableObject
{
	private bool _isSelected;

	public string Title { get; }

	public string Glyph { get; }

	public Brush TileBrush { get; }

	public Brush ForegroundBrush { get; }

	public string IconPath { get; }

	public bool HasIconPath => !string.IsNullOrWhiteSpace(IconPath);

	public GameCardViewModel? Game { get; }

	public double Width { get; }

	public double Height { get; }

	public double Left { get; }

	public double Top { get; }

	public double Right => Left + Width;

	public int ZIndex => IsSelected ? 10 : 0;

	public bool IsSelected
	{
		get
		{
			return _isSelected;
		}
		set
		{
			if (SetProperty(ref _isSelected, value, "IsSelected"))
			{
				OnPropertyChanged("ZIndex");
			}
		}
	}

	public AppLibraryTileViewModel(string title, string glyph, Color tileColor, double width = 198.0, double height = 148.0, double left = 0.0, double top = 0.0, Color? foreground = null, string iconPath = "", GameCardViewModel? game = null)
	{
		Title = title;
		Glyph = glyph;
		IconPath = iconPath;
		Game = game;
		Width = width;
		Height = height;
		Left = left;
		Top = top;
		SolidColorBrush solidColorBrush = new SolidColorBrush(tileColor);
		if (solidColorBrush.CanFreeze)
		{
			solidColorBrush.Freeze();
		}
		TileBrush = solidColorBrush;
		SolidColorBrush solidColorBrush2 = new SolidColorBrush(foreground ?? Colors.White);
		if (solidColorBrush2.CanFreeze)
		{
			solidColorBrush2.Freeze();
		}
		ForegroundBrush = solidColorBrush2;
	}
}
