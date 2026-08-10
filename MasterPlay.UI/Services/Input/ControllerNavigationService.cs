using System;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace MasterPlay.UI.Services.Input;

/// <summary>
/// Adds optional XInput navigation to the currently active MasterPlay window.
/// LB + RB toggles the mode; no input is processed while another application is active.
/// </summary>
public sealed class ControllerNavigationService : IDisposable
{
    private const ushort DPadUp = 0x0001;
    private const ushort DPadDown = 0x0002;
    private const ushort DPadLeft = 0x0004;
    private const ushort DPadRight = 0x0008;
    private const ushort LeftShoulder = 0x0100;
    private const ushort RightShoulder = 0x0200;
    private const ushort ButtonA = 0x1000;
    private const ushort ButtonB = 0x2000;
    private const ushort ButtonX = 0x4000;
    private const short ThumbstickThreshold = 16_000;

    private readonly DispatcherTimer _pollTimer;
    private ushort _previousButtons;
    private bool _previousShoulderCombo;
    private ControllerDirection _lastDirection;
    private DateTime _nextDirectionRepeatAt;
    private bool _useLegacyXInput;
    private bool _xInputUnavailable;

    public ControllerNavigationService()
    {
        _pollTimer = new DispatcherTimer(DispatcherPriority.Input)
        {
            Interval = TimeSpan.FromMilliseconds(50)
        };
        _pollTimer.Tick += PollController;
    }

    public bool IsEnabled { get; private set; }

    public event EventHandler<bool>? ModeChanged;

    public void Start() => _pollTimer.Start();

    public void Dispose()
    {
        _pollTimer.Stop();
        _pollTimer.Tick -= PollController;
    }

    private void PollController(object? sender, EventArgs e)
    {
        var activeWindow = GetActiveWindow();
        if (activeWindow == null || !TryGetControllerState(out var state))
        {
            ResetInputState();
            return;
        }

        var buttons = state.Gamepad.Buttons;
        var shoulderCombo = (buttons & (LeftShoulder | RightShoulder)) == (LeftShoulder | RightShoulder);
        if (shoulderCombo && !_previousShoulderCombo)
        {
            IsEnabled = !IsEnabled;
            ModeChanged?.Invoke(this, IsEnabled);
        }

        if (IsEnabled)
        {
            if (IsPressed(buttons, ButtonA) && !IsPressed(_previousButtons, ButtonA))
                ActivateFocusedControl(activeWindow);

            if (IsPressed(buttons, ButtonB) && !IsPressed(_previousButtons, ButtonB))
                SendBack(activeWindow);

            if (IsPressed(buttons, ButtonX) && !IsPressed(_previousButtons, ButtonX))
                PlayFocusedGameCard(activeWindow);

            ProcessDirection(activeWindow, GetDirection(state));
        }
        else
        {
            _lastDirection = ControllerDirection.None;
        }

        _previousButtons = buttons;
        _previousShoulderCombo = shoulderCombo;
    }

    private void ResetInputState()
    {
        _previousButtons = 0;
        _previousShoulderCombo = false;
        _lastDirection = ControllerDirection.None;
    }

    private static Window? GetActiveWindow() => Application.Current?.Windows
        .OfType<Window>()
        .FirstOrDefault(window => window.IsVisible && window.IsActive);

    private static bool IsPressed(ushort buttons, ushort button) => (buttons & button) != 0;

    private static ControllerDirection GetDirection(XInputState state)
    {
        var buttons = state.Gamepad.Buttons;
        if (IsPressed(buttons, DPadUp) || state.Gamepad.ThumbLY >= ThumbstickThreshold)
            return ControllerDirection.Up;
        if (IsPressed(buttons, DPadDown) || state.Gamepad.ThumbLY <= -ThumbstickThreshold)
            return ControllerDirection.Down;
        if (IsPressed(buttons, DPadLeft) || state.Gamepad.ThumbLX <= -ThumbstickThreshold)
            return ControllerDirection.Left;
        if (IsPressed(buttons, DPadRight) || state.Gamepad.ThumbLX >= ThumbstickThreshold)
            return ControllerDirection.Right;
        return ControllerDirection.None;
    }

    private void ProcessDirection(Window activeWindow, ControllerDirection direction)
    {
        if (direction == ControllerDirection.None)
        {
            _lastDirection = ControllerDirection.None;
            return;
        }

        var now = DateTime.UtcNow;
        var isNewDirection = direction != _lastDirection;
        if (!isNewDirection && now < _nextDirectionRepeatAt)
            return;

        MoveFocus(activeWindow, direction);
        _lastDirection = direction;
        _nextDirectionRepeatAt = now + (isNewDirection
            ? TimeSpan.FromMilliseconds(350)
            : TimeSpan.FromMilliseconds(120));
    }

    private static void MoveFocus(Window activeWindow, ControllerDirection direction)
    {
        var focusedElement = GetFocusedElement(activeWindow);
        if (focusedElement == null)
        {
            FocusFirstControl(activeWindow);
            return;
        }

        var comboBox = FindAncestor<ComboBox>(focusedElement);
        if (comboBox?.IsDropDownOpen == true)
        {
            MoveComboBoxSelection(comboBox, direction);
            return;
        }

        var focusDirection = direction switch
        {
            ControllerDirection.Up => FocusNavigationDirection.Up,
            ControllerDirection.Down => FocusNavigationDirection.Down,
            ControllerDirection.Left => FocusNavigationDirection.Left,
            _ => FocusNavigationDirection.Right
        };

        if (focusedElement.MoveFocus(new TraversalRequest(focusDirection)))
            return;

        var fallbackDirection = direction is ControllerDirection.Up or ControllerDirection.Left
            ? FocusNavigationDirection.Previous
            : FocusNavigationDirection.Next;
        focusedElement.MoveFocus(new TraversalRequest(fallbackDirection));
    }

    private static void MoveComboBoxSelection(ComboBox comboBox, ControllerDirection direction)
    {
        if (comboBox.Items.Count == 0)
            return;

        var change = direction is ControllerDirection.Up or ControllerDirection.Left ? -1 : 1;
        var selectedIndex = comboBox.SelectedIndex < 0 ? 0 : comboBox.SelectedIndex;
        comboBox.SelectedIndex = Math.Clamp(selectedIndex + change, 0, comboBox.Items.Count - 1);
    }

    private static void ActivateFocusedControl(Window activeWindow)
    {
        var focusedElement = GetFocusedElement(activeWindow);
        if (focusedElement == null)
        {
            FocusFirstControl(activeWindow);
            return;
        }

        if (ExecuteGameCardCommand(focusedElement, ControllerNavigationProperties.GetSelectCommand))
            return;

        if (FindAncestor<ComboBox>(focusedElement) is { } comboBox)
        {
            comboBox.IsDropDownOpen = !comboBox.IsDropDownOpen;
            return;
        }

        if (FindAncestor<ButtonBase>(focusedElement) is { IsEnabled: true } button)
        {
            button.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent, button));
            return;
        }

        if (FindAncestor<ListBoxItem>(focusedElement) is { IsEnabled: true } listBoxItem)
            listBoxItem.IsSelected = true;
    }

    private static void PlayFocusedGameCard(Window activeWindow)
    {
        var focusedElement = GetFocusedElement(activeWindow);
        if (focusedElement != null)
            ExecuteGameCardCommand(focusedElement, ControllerNavigationProperties.GetPlayCommand);
    }

    private static bool ExecuteGameCardCommand(
        UIElement focusedElement,
        Func<DependencyObject, ICommand?> commandSelector)
    {
        var element = FindGameCard(focusedElement, commandSelector);
        var command = element == null ? null : commandSelector(element);
        if (command == null || !command.CanExecute(element.DataContext))
            return false;

        command.Execute(element.DataContext);
        return true;
    }

    private static FrameworkElement? FindGameCard(
        DependencyObject? element,
        Func<DependencyObject, ICommand?> commandSelector)
    {
        while (element != null)
        {
            if (element is FrameworkElement frameworkElement && commandSelector(frameworkElement) != null)
                return frameworkElement;

            element = element switch
            {
                Visual or System.Windows.Media.Media3D.Visual3D => VisualTreeHelper.GetParent(element),
                _ => LogicalTreeHelper.GetParent(element)
            };
        }

        return null;
    }

    private static void SendBack(Window activeWindow)
    {
        var target = GetFocusedElement(activeWindow) ?? activeWindow;
        var source = PresentationSource.FromVisual(activeWindow);
        if (source == null)
            return;

        var previewEvent = new KeyEventArgs(Keyboard.PrimaryDevice, source, Environment.TickCount, Key.Escape)
        {
            RoutedEvent = Keyboard.PreviewKeyDownEvent
        };
        target.RaiseEvent(previewEvent);

        if (previewEvent.Handled)
            return;

        var keyDownEvent = new KeyEventArgs(Keyboard.PrimaryDevice, source, Environment.TickCount, Key.Escape)
        {
            RoutedEvent = Keyboard.KeyDownEvent
        };
        target.RaiseEvent(keyDownEvent);
    }

    private static UIElement? GetFocusedElement(Window activeWindow)
    {
        if (Keyboard.FocusedElement is not DependencyObject focusedElement)
            return null;

        var window = Window.GetWindow(focusedElement);
        if (window != activeWindow)
            return null;

        return FindAncestor<UIElement>(focusedElement);
    }

    private static void FocusFirstControl(Window window)
    {
        var control = FindFocusableDescendant(window);
        if (control != null)
            Keyboard.Focus(control);
    }

    private static UIElement? FindFocusableDescendant(DependencyObject root)
    {
        var childCount = VisualTreeHelper.GetChildrenCount(root);
        for (var index = 0; index < childCount; index++)
        {
            var child = VisualTreeHelper.GetChild(root, index);
            if (child is UIElement { IsVisible: true, IsEnabled: true, Focusable: true } element)
                return element;

            var descendant = FindFocusableDescendant(child);
            if (descendant != null)
                return descendant;
        }

        return null;
    }

    private static T? FindAncestor<T>(DependencyObject? element) where T : DependencyObject
    {
        while (element != null)
        {
            if (element is T match)
                return match;

            element = element switch
            {
                Visual or System.Windows.Media.Media3D.Visual3D => VisualTreeHelper.GetParent(element),
                _ => LogicalTreeHelper.GetParent(element)
            };
        }

        return null;
    }

    private bool TryGetControllerState(out XInputState state)
    {
        state = default;
        if (_xInputUnavailable)
            return false;

        for (uint userIndex = 0; userIndex < 4; userIndex++)
        {
            if (GetState(userIndex, out state) == 0)
                return true;
        }

        return false;
    }

    private uint GetState(uint userIndex, out XInputState state)
    {
        try
        {
            return _useLegacyXInput
                ? XInputGetState13(userIndex, out state)
                : XInputGetState14(userIndex, out state);
        }
        catch (DllNotFoundException)
        {
            _useLegacyXInput = true;
        }
        catch (EntryPointNotFoundException)
        {
            _useLegacyXInput = true;
        }

        try
        {
            return XInputGetState13(userIndex, out state);
        }
        catch (DllNotFoundException)
        {
            _xInputUnavailable = true;
            state = default;
            return 1;
        }
        catch (EntryPointNotFoundException)
        {
            _xInputUnavailable = true;
            state = default;
            return 1;
        }
    }

    [DllImport("xinput1_4.dll", EntryPoint = "XInputGetState")]
    private static extern uint XInputGetState14(uint userIndex, out XInputState state);

    [DllImport("xinput1_3.dll", EntryPoint = "XInputGetState")]
    private static extern uint XInputGetState13(uint userIndex, out XInputState state);

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
        public short ThumbLX;
        public short ThumbLY;
        public short ThumbRX;
        public short ThumbRY;
    }

    private enum ControllerDirection
    {
        None,
        Up,
        Down,
        Left,
        Right
    }
}
