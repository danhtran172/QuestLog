using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using MasterPlay.UI.Features.GameEditor;

namespace MasterPlay.UI.Services.Windowing;

public static class ModalWindowService
{
    public static bool? ShowDialog(Window owner, Window dialog)
    {
        var backdrop = CreateBackdrop(owner);
        using var outsideClickMonitor = new OutsideClickMonitor(owner, dialog);
        void SyncBackdrop(object? sender, EventArgs e) => PositionBackdrop(owner, backdrop);

        owner.LocationChanged += SyncBackdrop;
        owner.SizeChanged += SyncBackdrop;
        owner.StateChanged += SyncBackdrop;

        try
        {
            PositionBackdrop(owner, backdrop);
            backdrop.Owner = owner;
            backdrop.Show();
            dialog.Owner = owner;

            if (dialog is GameEditorWindow)
                dialog.Height = owner.ActualHeight * 0.9;

            return dialog.ShowDialog();
        }
        finally
        {
            owner.LocationChanged -= SyncBackdrop;
            owner.SizeChanged -= SyncBackdrop;
            owner.StateChanged -= SyncBackdrop;
            backdrop.Close();
        }
    }

    private static Window CreateBackdrop(Window owner)
    {
        return new Window
        {
            WindowStyle = WindowStyle.None,
            ResizeMode = ResizeMode.NoResize,
            AllowsTransparency = true,
            Background = new SolidColorBrush(Color.FromArgb(145, 0, 0, 0)),
            ShowInTaskbar = false,
            ShowActivated = false,
            Focusable = false,
            Topmost = owner.Topmost,
            WindowStartupLocation = WindowStartupLocation.Manual
        };
    }

    private static void PositionBackdrop(Window owner, Window backdrop)
    {
        var handle = new WindowInteropHelper(owner).Handle;
        if (handle == IntPtr.Zero || !GetWindowRect(handle, out var rectangle))
        {
            backdrop.Left = owner.Left;
            backdrop.Top = owner.Top;
            backdrop.Width = owner.ActualWidth;
            backdrop.Height = owner.ActualHeight;
            return;
        }

        var source = PresentationSource.FromVisual(owner);
        var transform = source?.CompositionTarget?.TransformFromDevice ?? Matrix.Identity;
        var topLeft = transform.Transform(new Point(rectangle.Left, rectangle.Top));
        var bottomRight = transform.Transform(new Point(rectangle.Right, rectangle.Bottom));

        backdrop.Left = topLeft.X;
        backdrop.Top = topLeft.Y;
        backdrop.Width = Math.Max(0, bottomRight.X - topLeft.X);
        backdrop.Height = Math.Max(0, bottomRight.Y - topLeft.Y);
    }

    [DllImport("user32.dll")]
    private static extern bool GetWindowRect(IntPtr windowHandle, out NativeRect rectangle);

    [DllImport("user32.dll")]
    private static extern IntPtr SetWindowsHookEx(
        int hookId,
        MouseHookProcedure procedure,
        IntPtr moduleHandle,
        uint threadId);

    [DllImport("user32.dll")]
    private static extern bool UnhookWindowsHookEx(IntPtr hookHandle);

    [DllImport("user32.dll")]
    private static extern IntPtr CallNextHookEx(
        IntPtr hookHandle,
        int code,
        IntPtr message,
        IntPtr data);

    [DllImport("kernel32.dll")]
    private static extern IntPtr GetModuleHandle(string? moduleName);

    private delegate IntPtr MouseHookProcedure(int code, IntPtr message, IntPtr data);

    private sealed class OutsideClickMonitor : IDisposable
    {
        private const int LowLevelMouseHook = 14;
        private const int LeftButtonDown = 0x0201;
        private readonly Window _owner;
        private readonly Window _dialog;
        private readonly MouseHookProcedure _procedure;
        private IntPtr _hookHandle;

        public OutsideClickMonitor(Window owner, Window dialog)
        {
            _owner = owner;
            _dialog = dialog;
            _procedure = HandleMouseEvent;
            _hookHandle = SetWindowsHookEx(
                LowLevelMouseHook,
                _procedure,
                GetModuleHandle(null),
                0);
        }

        public void Dispose()
        {
            if (_hookHandle == IntPtr.Zero)
                return;

            UnhookWindowsHookEx(_hookHandle);
            _hookHandle = IntPtr.Zero;
        }

        private IntPtr HandleMouseEvent(int code, IntPtr message, IntPtr data)
        {
            if (code >= 0 &&
                message.ToInt32() == LeftButtonDown &&
                _dialog.IsVisible &&
                _dialog.IsActive)
            {
                var mouseInput = Marshal.PtrToStructure<LowLevelMouseInput>(data);
                var ownerHandle = new WindowInteropHelper(_owner).Handle;
                var dialogHandle = new WindowInteropHelper(_dialog).Handle;

                if (ownerHandle != IntPtr.Zero &&
                    dialogHandle != IntPtr.Zero &&
                    GetWindowRect(ownerHandle, out var ownerRectangle) &&
                    GetWindowRect(dialogHandle, out var dialogRectangle) &&
                    Contains(ownerRectangle, mouseInput.Point) &&
                    !Contains(dialogRectangle, mouseInput.Point))
                {
                    _dialog.Dispatcher.BeginInvoke(_dialog.Close);
                }
            }

            return CallNextHookEx(_hookHandle, code, message, data);
        }

        private static bool Contains(NativeRect rectangle, NativePoint point)
        {
            return point.X >= rectangle.Left &&
                   point.X < rectangle.Right &&
                   point.Y >= rectangle.Top &&
                   point.Y < rectangle.Bottom;
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeRect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativePoint
    {
        public int X;
        public int Y;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct LowLevelMouseInput
    {
        public NativePoint Point;
        public uint MouseData;
        public uint Flags;
        public uint Time;
        public UIntPtr ExtraInfo;
    }
}
