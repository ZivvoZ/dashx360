using System.Diagnostics;
using Microsoft.Win32;

namespace XboxMetroLauncher.Services;

public sealed class RegistryStartupRegistrationService : IStartupRegistrationService
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string AppName = "XboxMetroLauncher";

    public void SetLaunchOnStartup(bool enabled)
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: true);
        if (key is null)
        {
            return;
        }

        if (!enabled)
        {
            key.DeleteValue(AppName, throwOnMissingValue: false);
            return;
        }

        var exePath = Process.GetCurrentProcess().MainModule?.FileName;
        if (!string.IsNullOrWhiteSpace(exePath))
        {
            key.SetValue(AppName, $"\"{exePath}\"");
        }
    }
}
