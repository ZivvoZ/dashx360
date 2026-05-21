using System.IO;
using System.Windows;
using Microsoft.Win32;

namespace XboxMetroLauncher.Services;

public sealed class WindowsFilePickerService : IFilePickerService
{
    public string? PickExecutable()
    {
        var dialog = new OpenFileDialog
        {
            Title = "Choose game executable",
            Filter = "Windows executables (*.exe)|*.exe|All files (*.*)|*.*",
            CheckFileExists = true
        };

        return ShowDialog(dialog) ? dialog.FileName : null;
    }

    public string? PickFolder()
    {
        var dialog = new OpenFolderDialog
        {
            Title = "Choose a game library folder",
            Multiselect = false
        };

        return Application.Current?.MainWindow is { } owner
            ? dialog.ShowDialog(owner) == true ? dialog.FolderName : null
            : dialog.ShowDialog() == true ? dialog.FolderName : null;
    }

    public string? PickImage(string? initialDirectory = null)
    {
        var dialog = new OpenFileDialog
        {
            Title = "Choose artwork",
            Filter = "Images (*.png;*.jpg;*.jpeg;*.bmp)|*.png;*.jpg;*.jpeg;*.bmp|All files (*.*)|*.*",
            CheckFileExists = true
        };

        if (!string.IsNullOrWhiteSpace(initialDirectory) && Directory.Exists(initialDirectory))
        {
            dialog.InitialDirectory = initialDirectory;
        }

        return ShowDialog(dialog) ? dialog.FileName : null;
    }

    private static bool ShowDialog(OpenFileDialog dialog)
        => Application.Current?.MainWindow is { } owner
            ? dialog.ShowDialog(owner) == true
            : dialog.ShowDialog() == true;
}
