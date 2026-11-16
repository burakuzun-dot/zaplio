using Windows.Media;
using Xunit;

namespace Zaplio.Tests;

public sealed class MediaSessionTests
{
    [Theory]
    [InlineData(SystemMediaTransportControlsButton.Play, (int)MediaCommand.Play)]
    [InlineData(SystemMediaTransportControlsButton.Pause, (int)MediaCommand.Pause)]
    [InlineData(SystemMediaTransportControlsButton.Stop, (int)MediaCommand.Stop)]
    [InlineData(SystemMediaTransportControlsButton.Next, (int)MediaCommand.Next)]
    [InlineData(SystemMediaTransportControlsButton.Previous, (int)MediaCommand.Previous)]
    [InlineData(SystemMediaTransportControlsButton.Record, null)]
    [InlineData(SystemMediaTransportControlsButton.FastForward, null)]
    [InlineData(SystemMediaTransportControlsButton.Rewind, null)]
    public void Maps_supported_windows_commands(SystemMediaTransportControlsButton button, int? expected)
    {
        Assert.Equal((MediaCommand?)expected, MediaSession.Map(button));
    }
}
