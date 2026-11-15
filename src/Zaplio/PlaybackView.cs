using System.Windows.Controls;
using LibVLCSharp.WPF;

namespace Zaplio;

public sealed class PlaybackView : VideoView
{
    internal void MoveTo(Decorator destination)
    {
        // WPF reparents the existing HwndHost, keeping the HWND that VLC's video output is using.
        if (Parent is Decorator current) current.Child = null;
        destination.Child = this;
    }
}
