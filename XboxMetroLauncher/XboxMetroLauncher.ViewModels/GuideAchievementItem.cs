namespace XboxMetroLauncher.ViewModels;

public sealed class GuideAchievementItem : ObservableObject
{
	private bool _isSelected;

	public required string Title { get; init; }

	public string ApiName { get; init; } = string.Empty;

	public string Description { get; init; } = string.Empty;

	public bool Achieved { get; init; }

	public string StatusText { get; init; } = string.Empty;

	public long UnlockTimeUnix { get; init; }

	public string IconPath { get; init; } = string.Empty;

	public bool HasIconPath => !string.IsNullOrWhiteSpace(IconPath);

	public string IconGlyph
	{
		get
		{
			if (!Achieved)
			{
				return "\ue72e";
			}
			return "\ue7c1";
		}
	}

	public bool IsSelected
	{
		get
		{
			return _isSelected;
		}
		set
		{
			SetProperty(ref _isSelected, value, "IsSelected");
		}
	}
}
