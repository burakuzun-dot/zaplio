using System.Windows.Media;
using System.Windows.Media.Imaging;
using Xunit;

namespace Zaplio.Tests;

public sealed class IntroFrameSequenceTests
{
    [Fact]
    public void Packaged_intro_keeps_transparency_at_its_original_frame_rate()
    {
        var frames = IntroFrameSequence.Load(IntroFrameSequence.PathOnDisk, animate: true);

        Assert.Equal(2.8, (double)frames.Length / IntroFrameSequence.FramesPerSecond, 3);
        Assert.All(frames, frame => Assert.True(frame.IsFrozen));
        var final = new FormatConvertedBitmap(frames[^1], PixelFormats.Bgra32, null, 0);
        var pixels = new byte[final.PixelWidth * final.PixelHeight * 4];
        final.CopyPixels(pixels, final.PixelWidth * 4, 0);
        Assert.Equal((byte)0, pixels[3]);
        Assert.Contains(Enumerable.Range(0, pixels.Length / 4), index => pixels[index * 4 + 3] == 255);
    }

    [Fact]
    public void Reduced_motion_loads_only_the_final_frame()
    {
        var frame = Assert.Single(IntroFrameSequence.Load(IntroFrameSequence.PathOnDisk, animate: false));
        var final = IntroFrameSequence.Load(IntroFrameSequence.PathOnDisk, animate: true)[^1];
        var stride = frame.PixelWidth * 4;
        var actual = new byte[stride * frame.PixelHeight];
        var expected = new byte[actual.Length];
        frame.CopyPixels(actual, stride, 0);
        final.CopyPixels(expected, stride, 0);

        Assert.Equal(expected, actual);
    }
}
