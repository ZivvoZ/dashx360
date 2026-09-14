using System;
using XboxMetroLauncher.Input;

namespace XboxMetroLauncher.Views;

public interface IGuideWindow
{
    bool IsGuideOpen { get; }
    bool IsTransitioning { get; }
    event EventHandler? HiddenCompleted;
    event EventHandler? Closed;
    bool Open(bool resetToHome = true);
    bool CloseGuide(bool playSound = false);
    bool HandleInput(DashboardInputAction action);
    void Close();
}
