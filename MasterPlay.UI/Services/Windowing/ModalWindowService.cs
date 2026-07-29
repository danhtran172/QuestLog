using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;

namespace MasterPlay.UI.Services.Windowing;

public static class ModalWindowService
{
    public static bool? ShowDialog(Window owner, Window dialog)
    {
        var backdrop = CreateBackdrop(owner);
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

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeRect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }
}
