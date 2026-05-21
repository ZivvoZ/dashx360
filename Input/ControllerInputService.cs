using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Threading;

namespace XboxMetroLauncher.Input;

public sealed class ControllerInputService : IDisposable
{
    private const int ErrorSuccess = 0;
    private const ushort XinputGamepadDpadUp = 0x0001;
    private const ushort XinputGamepadDpadDown = 0x0002;
    private const ushort XinputGamepadDpadLeft = 0x0004;
    private const ushort XinputGamepadDpadRight = 0x0008;
    private const ushort XinputGamepadStart = 0x0010;
    private const ushort XinputGamepadBack = 0x0020;
    private const ushort XinputGamepadLeftShoulder = 0x0100;
    private const ushort XinputGamepadRightShoulder = 0x0200;
    private const ushort XinputGamepadA = 0x1000;
    private const ushort XinputGamepadB = 0x2000;
    private const ushort XinputGamepadX = 0x4000;
    private const ushort XinputGamepadY = 0x8000;
    private const short ThumbDeadzone = 16000;
    private const byte TriggerThreshold = 120;

    private readonly Timer _timer;
    private readonly Action<DashboardInputAction> _onAction;
    private readonly Func<bool> _isEnabled;
    private readonly Dictionary<DashboardInputAction, DateTimeOffset> _lastMoveTimes = [];
    private ushort _previousButtons;
    private XInputState _previousState;
    private DashboardInputAction? _previousStickAction;
    private DateTimeOffset _lastStickMove = DateTimeOffset.MinValue;
    private static readonly TimeSpan MoveRepeatDelay = TimeSpan.FromMilliseconds(185);
    private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(95);
    private bool _isDispatching;
    private int _isPolling;
    private bool _guideButtonDown;

    public ControllerInputService(Action<DashboardInputAction> onAction, Func<bool>? isEnabled = null)
    {
        _onAction = onAction;
        _isEnabled = isEnabled ?? (() => true);
        _timer = new Timer(OnTick, null, Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
    }

    public void Start() => _timer.Change(TimeSpan.Zero, PollInterval);

    public void Dispose() => _timer.Dispose();

    private void OnTick(object? state)
    {
        if (Interlocked.Exchange(ref _isPolling, 1) == 1)
        {
            return;
        }

        try
        {
            PollController();
        }
        catch (Exception ex)
        {
            App.LogException(ex, "ControllerInputService.OnTick");
            ResetInputState();
        }
        finally
        {
            Interlocked.Exchange(ref _isPolling, 0);
        }
    }

    private void PollController()
    {
        if (!_isEnabled())
        {
            ResetInputState();
            return;
        }

        XInputState state;
        try
        {
            if (XInputGetState(0, out state) != ErrorSuccess)
            {
                ResetInputState();
                return;
            }
        }
        catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException or BadImageFormatException or SEHException)
        {
            App.LogException(ex, "ControllerInputService.XInput");
            _timer.Change(Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
            return;
        }

        var buttons = state.Gamepad.Buttons;
        var guideCombo = (ushort)(XinputGamepadBack | XinputGamepadStart);
        var guideDown = (buttons & guideCombo) == guideCombo;
        if (guideDown)
        {
            if (!_guideButtonDown)
            {
                DispatchAction(DashboardInputAction.Guide);
            }

            _guideButtonDown = true;
            _previousStickAction = null;
            _previousButtons = buttons;
            _previousState = state;
            return;
        }

        _guideButtonDown = false;
        var handledMove = FireDpadMove(buttons);
        FireOnPress(buttons, XinputGamepadA, DashboardInputAction.Activate);
        FireOnPress(buttons, XinputGamepadB, DashboardInputAction.Back);
        FireOnPress(buttons, XinputGamepadX, DashboardInputAction.Details);
        FireOnPress(buttons, XinputGamepadY, DashboardInputAction.Search);
        FireOnPress(buttons, XinputGamepadStart, DashboardInputAction.Options);
        FireOnPress(buttons, XinputGamepadBack, DashboardInputAction.Back);
        FireOnPress(buttons, XinputGamepadLeftShoulder, DashboardInputAction.PreviousTab);
        FireOnPress(buttons, XinputGamepadRightShoulder, DashboardInputAction.NextTab);
        FireOnTriggerPress(state.Gamepad.LeftTrigger, _previousState.Gamepad.LeftTrigger, DashboardInputAction.LeftTrigger);
        FireOnTriggerPress(state.Gamepad.RightTrigger, _previousState.Gamepad.RightTrigger, DashboardInputAction.RightTrigger);

        var stickAction = GetStickAction(state.Gamepad.LeftThumbX, state.Gamepad.LeftThumbY);
        if (!handledMove && stickAction is not null && ShouldFireStick(stickAction.Value))
        {
            DispatchAction(stickAction.Value);
        }

        _previousStickAction = stickAction;
        _previousButtons = buttons;
        _previousState = state;
    }

    private bool FireDpadMove(ushort buttons)
    {
        var action = GetDpadAction(buttons);
        if (action is null)
        {
            return false;
        }

        var mask = GetButtonMask(action.Value);
        FireMoveWhileDown(buttons, mask, action.Value);
        return true;
    }

    private void FireOnPress(ushort buttons, ushort mask, DashboardInputAction action)
    {
        if ((buttons & mask) != 0 && (_previousButtons & mask) == 0)
        {
            DispatchAction(action);
        }
    }

    private void FireOnTriggerPress(byte value, byte previousValue, DashboardInputAction action)
    {
        if (value >= TriggerThreshold && previousValue < TriggerThreshold)
        {
            DispatchAction(action);
        }
    }

    private void FireMoveWhileDown(ushort buttons, ushort mask, DashboardInputAction action)
    {
        if ((buttons & mask) == 0)
        {
            return;
        }

        var isNewPress = (_previousButtons & mask) == 0;
        if (isNewPress)
        {
            _lastMoveTimes[action] = DateTimeOffset.UtcNow;
        }
        else if (!ShouldRepeat(action))
        {
            return;
        }

        DispatchAction(action);
    }

    private bool ShouldRepeat(DashboardInputAction action)
    {
        var now = DateTimeOffset.UtcNow;
        if (_lastMoveTimes.TryGetValue(action, out var lastMove) && now - lastMove < MoveRepeatDelay)
        {
            return false;
        }

        _lastMoveTimes[action] = now;
        return true;
    }

    private bool ShouldFireStick(DashboardInputAction action)
    {
        var now = DateTimeOffset.UtcNow;
        if (_previousStickAction != action || now - _lastStickMove >= MoveRepeatDelay)
        {
            _lastStickMove = now;
            return true;
        }

        return false;
    }

    private void DispatchAction(DashboardInputAction action)
    {
        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher is not null && !dispatcher.CheckAccess())
        {
            dispatcher.BeginInvoke(new Action(() => DispatchActionOnUiThread(action)), DispatcherPriority.Send);
            return;
        }

        DispatchActionOnUiThread(action);
    }

    private void DispatchActionOnUiThread(DashboardInputAction action)
    {
        if (_isDispatching)
        {
            return;
        }

        try
        {
            _isDispatching = true;
            _onAction(action);
        }
        catch (Exception ex)
        {
            App.LogException(ex, "ControllerInputService.DispatchAction");
            // Controller polling must never tear down the WPF shell.
        }
        finally
        {
            _isDispatching = false;
        }
    }

    private static DashboardInputAction? GetDpadAction(ushort buttons)
    {
        var left = (buttons & XinputGamepadDpadLeft) != 0;
        var right = (buttons & XinputGamepadDpadRight) != 0;
        var up = (buttons & XinputGamepadDpadUp) != 0;
        var down = (buttons & XinputGamepadDpadDown) != 0;

        if (left == right && up == down)
        {
            return null;
        }

        if (left != right)
        {
            return left ? DashboardInputAction.MoveLeft : DashboardInputAction.MoveRight;
        }

        return up ? DashboardInputAction.MoveUp : DashboardInputAction.MoveDown;
    }

    private static ushort GetButtonMask(DashboardInputAction action)
        => action switch
        {
            DashboardInputAction.MoveLeft => XinputGamepadDpadLeft,
            DashboardInputAction.MoveRight => XinputGamepadDpadRight,
            DashboardInputAction.MoveUp => XinputGamepadDpadUp,
            DashboardInputAction.MoveDown => XinputGamepadDpadDown,
            _ => 0
        };

    private void ResetInputState()
    {
        _previousButtons = 0;
        _previousState = default;
        _previousStickAction = null;
        _guideButtonDown = false;
        _lastMoveTimes.Clear();
    }

    private static DashboardInputAction? GetStickAction(short x, short y)
    {
        if (Math.Abs(x) < ThumbDeadzone && Math.Abs(y) < ThumbDeadzone)
        {
            return null;
        }

        if (Math.Abs(x) > Math.Abs(y))
        {
            return x > 0 ? DashboardInputAction.MoveRight : DashboardInputAction.MoveLeft;
        }

        return y > 0 ? DashboardInputAction.MoveUp : DashboardInputAction.MoveDown;
    }

    [DllImport("xinput1_4.dll", EntryPoint = "XInputGetState")]
    private static extern int XInputGetState(int dwUserIndex, out XInputState pState);

    [StructLayout(LayoutKind.Sequential)]
    private struct XInputState
    {
        public uint PacketNumber;
        public XInputGamepad Gamepad;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct XInputGamepad
    {
        public ushort Buttons;
        public byte LeftTrigger;
        public byte RightTrigger;
        public short LeftThumbX;
        public short LeftThumbY;
        public short RightThumbX;
        public short RightThumbY;
    }
}
