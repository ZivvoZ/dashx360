namespace XboxMetroLauncher.Services;

public interface IFilePickerService
{
    string? PickExecutable();
    string? PickFolder();
    string? PickImage(string? initialDirectory = null);
}
