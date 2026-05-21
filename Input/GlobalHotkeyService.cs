using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;

namespace XboxMetroLauncher.Input;

public sealed class GlobalHotkeyService : IDisposable
{
    private const int HotkeyId = 0x360;
    private const uint ModShift = 0x0004;
    private const uint ModWin = 0x0008;
    private const int WmHotkey = 0x0312;
    private const int WhKeyboardLl = 13;
    private const int WmKeyDown = 0x0100;
    private const int WmKeyUp = 0x0101;
    private const int WmSysKeyDown = 0x0104;
    private const int WmSysKeyUp = 0x0105;
    private const int VkLShift = 0xA0;
    private const int VkLControl = 0xA2;
    private const int VkLWin = 0x5B;
    private const int VkRWin = 0x5C;

    private HwndSource? _source;
    private LowLevelKeyboardProc? _keyboardProc;
    private IntPtr _keyboardHook;
    private IntPtr _handle;
    private bool _registered;
    private bool _leftShiftDown;
    private bool _leftCtrlDown;
    private bool _winDown;
    private bool _hotkeyDown;
    private DateTimeOffset _lastHotkeyRaised = DateTimeOffset.MinValue;

    public event EventHandler? HotkeyPressed;

    public void Register(Window window)
    {
        if (_registered)
        {
            return;
        }

        _handle = new WindowInteropHelper(window).Handle;
        if (_handle == IntPtr.Zero)
        {
            return;
        }

        _source = HwndSource.FromHwnd(_handle);
        _source?.AddHook(WndProc);

        var key = (uint)KeyInterop.VirtualKeyFromKey(Key.LeftCtrl);
        _registered = RegisterHotKey(_handle, HotkeyId, ModWin | ModShift, key);
        InstallKeyboardHook();
    }

    public void Dispose()
    {
        if (_registered)
        {
            UnregisterHotKey(_handle, HotkeyId);
            _registered = false;
        }

        _source?.RemoveHook(WndProc);
        _source = null;

        if (_keyboardHook != IntPtr.Zero)
        {
            UnhookWindowsHookEx(_keyboardHook);
            _keyboardHook = IntPtr.Zero;
        }
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WmHotkey && wParam.ToInt32() == HotkeyId)
        {
            handled = true;
            RaiseHotkeyPressed();
        }

        return IntPtr.Zero;
    }

    private void InstallKeyboardHook()
    {
        if (_keyboardHook != IntPtr.Zero)
        {
            return;
        }

        _keyboardProc = KeyboardHookCallback;
        _keyboardHook = SetWindowsHookEx(WhKeyboardLl, _keyboardProc, GetModuleHandle(null), 0);
    }

    private IntPtr KeyboardHookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0)
        {
            var message = wParam.ToInt32();
            var isDown = message is WmKeyDown or WmSysKeyDown;
            var isUp = message is WmKeyUp or WmSysKeyUp;

            if (isDown || isUp)
            {
                var info = Marshal.PtrToStructure<Kbdllhookstruct>(lParam);
                UpdateModifierState(info.VkCode, isDown);
            }
        }

        return CallNextHookEx(_keyboardHook, nCode, wParam, lParam);
    }

    private void UpdateModifierState(int virtualKey, bool isDown)
    {
        switch (virtualKey)
        {
            case VkLShift:
                _leftShiftDown = isDown;
                break;
            case VkLControl:
                _leftCtrlDown = isDown;
                break;
            case VkLWin:
            case VkRWin:
                _winDown = isDown;
                break;
        }

        if (_leftShiftDown && _leftCtrlDown && _winDown)
        {
            if (_hotkeyDown)
            {
                return;
            }

            _hotkeyDown = true;
            Application.Current?.Dispatcher.BeginInvoke(RaiseHotkeyPressed);
            return;
        }

        _hotkeyDown = false;
    }

    private void RaiseHotkeyPressed()
    {
        var now = DateTimeOffset.UtcNow;
        if (now - _lastHotkeyRaised < TimeSpan.FromMilliseconds(350))
        {
            return;
        }

        _lastHotkeyRaised = now;
        HotkeyPressed?.Invoke(this, EventArgs.Empty);
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    private delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);

    [StructLayout(LayoutKind.Sequential)]
    private struct Kbdllhookstruct
    {
        public int VkCode;
        public int ScanCode;
        public int Flags;
        public int Time;
        public IntPtr ExtraInfo;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnhookWindowsHookEx(IntPtr hhk);

    [DllImport("user32.dll")]
    private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern IntPtr GetModuleHandle(string? lpModuleName);
}
