using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Threading;
using Xunit;

namespace Zaplio.Tests;

public sealed class PlaybackViewTests
{
    [Fact]
    public async Task Moving_between_windows_preserves_the_native_video_window_and_moves_the_overlay()
    {
        var completed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(() =>
        {
            try
            {
                CheckWindowTransfers();
                completed.SetResult();
            }
            catch (Exception exception)
            {
                completed.SetException(exception);
            }
            finally
            {
                Dispatcher.CurrentDispatcher.InvokeShutdown();
            }
        });
        thread.IsBackground = true;
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        await completed.Task.WaitAsync(TimeSpan.FromSeconds(10));
    }

    private static void CheckWindowTransfers()
    {
        var overlay = new Grid();
        var view = new PlaybackView { Content = overlay };
        var mainSlot = new Border { Child = view };
        var secondSlot = new Border();
        var main = CreateWindow(mainSlot);
        var second = CreateWindow(secondSlot);
        try
        {
            main.Show();
            second.Show();
            FlushLayout();
            var host = Assert.IsAssignableFrom<HwndHost>(view.Template.FindName("PART_PlayerHost", view));
            var videoHandle = host.Handle;
            Assert.NotEqual(IntPtr.Zero, videoHandle);
            Assert.Same(main, Window.GetWindow(overlay).Owner);

            view.MoveTo(secondSlot);
            FlushLayout();
            Assert.Null(mainSlot.Child);
            Assert.Equal(videoHandle, host.Handle);
            Assert.Equal(new WindowInteropHelper(second).Handle, GetParent(videoHandle));
            Assert.Same(second, Window.GetWindow(overlay).Owner);

            view.MoveTo(mainSlot);
            second.Hide();
            FlushLayout();
            Assert.Null(secondSlot.Child);
            Assert.Equal(videoHandle, host.Handle);
            Assert.Equal(new WindowInteropHelper(main).Handle, GetParent(videoHandle));
            Assert.Same(main, Window.GetWindow(overlay).Owner);
        }
        finally
        {
            second.Close();
            main.Close();
        }
    }

    private static Window CreateWindow(Border slot) => new()
    {
        Content = slot,
        Width = 160,
        Height = 90,
        Left = -32000,
        Top = -32000,
        ShowActivated = false,
        ShowInTaskbar = false
    };

    private static void FlushLayout() => Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);

    [DllImport("user32.dll")]
    private static extern IntPtr GetParent(IntPtr window);
}
