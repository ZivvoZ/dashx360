using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Controls.Primitives;

namespace XboxMetroLauncher.Input;

public static class DashboardInputRouter
{
    public static bool TryMapKey(KeyEventArgs args, out DashboardInputAction action)
    {
        action = default;

        if (IsTextEditingKey(args))
        {
            return false;
        }

        action = args.Key switch
        {
            Key.Left => DashboardInputAction.MoveLeft,
            Key.A => DashboardInputAction.MoveLeft,
            Key.Right => DashboardInputAction.MoveRight,
            Key.D => DashboardInputAction.MoveRight,
            Key.Up => DashboardInputAction.MoveUp,
            Key.W => DashboardInputAction.MoveUp,
            Key.Down => DashboardInputAction.MoveDown,
            Key.S => DashboardInputAction.MoveDown,
            Key.Enter => DashboardInputAction.Activate,
            Key.Space => DashboardInputAction.Activate,
            Key.Escape => DashboardInputAction.Back,
            Key.X => DashboardInputAction.Details,
            Key.Y => DashboardInputAction.Search,
            Key.F => DashboardInputAction.Search,
            Key.Q => DashboardInputAction.PreviousTab,
            Key.E => DashboardInputAction.NextTab,
            Key.PageUp => DashboardInputAction.PreviousTab,
            Key.PageDown => DashboardInputAction.NextTab,
            Key.F10 => DashboardInputAction.Options,
            _ => default
        };

        return args.Key is Key.Left or Key.A or Key.Right or Key.D or Key.Up or Key.W or Key.Down or Key.S or Key.Enter or Key.Space or Key.Escape
            or Key.X or Key.Y or Key.F or Key.Q or Key.E or Key.PageUp or Key.PageDown or Key.F10;
    }

    public static bool MoveFocus(DashboardInputAction action)
    {
        var direction = action switch
        {
            DashboardInputAction.MoveLeft => FocusNavigationDirection.Left,
            DashboardInputAction.MoveRight => FocusNavigationDirection.Right,
            DashboardInputAction.MoveUp => FocusNavigationDirection.Up,
            DashboardInputAction.MoveDown => FocusNavigationDirection.Down,
            _ => (FocusNavigationDirection?)null
        };

        if (direction is null || Keyboard.FocusedElement is not UIElement element)
        {
            return false;
        }

        try
        {
            return element.MoveFocus(new TraversalRequest(direction.Value));
        }
        catch (InvalidOperationException)
        {
            return false;
        }
        catch (ArgumentException)
        {
            return false;
        }
        catch
        {
            return false;
        }
    }

    public static bool ActivateFocusedElement()
    {
        if (Keyboard.FocusedElement is Button button && button.Command?.CanExecute(button.CommandParameter) == true)
        {
            try
            {
                button.Command.Execute(button.CommandParameter);
                return true;
            }
            catch
            {
                return false;
            }
        }

        if (Keyboard.FocusedElement is CheckBox checkBox)
        {
            checkBox.IsChecked = !(checkBox.IsChecked ?? false);
            return true;
        }

        if (Keyboard.FocusedElement is ComboBox comboBox)
        {
            comboBox.IsDropDownOpen = !comboBox.IsDropDownOpen;
            return true;
        }

        if (Keyboard.FocusedElement is Slider slider)
        {
            slider.Value = Math.Clamp(slider.Value + slider.SmallChange, slider.Minimum, slider.Maximum);
            return true;
        }

        if (Keyboard.FocusedElement is TextBox textBox)
        {
            var binding = textBox.GetBindingExpression(TextBox.TextProperty);
            binding?.UpdateSource();
            return true;
        }

        return false;
    }

    public static bool TryAdjustFocusedSetting(DashboardInputAction action)
    {
        if (Keyboard.FocusedElement is Slider slider && action is DashboardInputAction.MoveLeft or DashboardInputAction.MoveRight)
        {
            var delta = action == DashboardInputAction.MoveRight ? slider.SmallChange : -slider.SmallChange;
            slider.Value = Math.Clamp(slider.Value + delta, slider.Minimum, slider.Maximum);
            return true;
        }

        if (Keyboard.FocusedElement is ComboBox comboBox && action is DashboardInputAction.MoveLeft or DashboardInputAction.MoveRight)
        {
            if (comboBox.Items.Count == 0)
            {
                return false;
            }

            var delta = action == DashboardInputAction.MoveRight ? 1 : -1;
            var next = Math.Clamp(comboBox.SelectedIndex + delta, 0, comboBox.Items.Count - 1);
            if (next == comboBox.SelectedIndex)
            {
                return true;
            }

            comboBox.SelectedIndex = next;
            return true;
        }

        return false;
    }

    private static bool IsTextEditingKey(KeyEventArgs args)
    {
        if (Keyboard.FocusedElement is not TextBox)
        {
            return false;
        }

        return args.Key is not (Key.Enter or Key.Escape or Key.PageUp or Key.PageDown);
    }
}
