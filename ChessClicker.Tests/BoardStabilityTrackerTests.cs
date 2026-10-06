using ChessClicker;
using Xunit;

namespace ChessClicker.Tests;

public sealed class BoardStabilityTrackerTests
{
    [Fact]
    public void RequiresThreeStableFramesBeforeReportingStability()
    {
        var tracker = new BoardStabilityTracker();
        int[,] frame = CreateFrame(100);

        Assert.False(tracker.AddFrame(frame));
        Assert.False(tracker.AddFrame(frame));
        Assert.True(tracker.AddFrame(frame));
    }

    [Fact]
    public void ChangingFrameRestartsStableFrameCount()
    {
        var tracker = new BoardStabilityTracker();
        int[,] initial = CreateFrame(100);
        int[,] changed = CreateFrame(120);

        Assert.False(tracker.AddFrame(initial));
        Assert.False(tracker.AddFrame(initial));
        Assert.False(tracker.AddFrame(changed));
        Assert.False(tracker.AddFrame(changed));
        Assert.True(tracker.AddFrame(changed));
    }

    [Fact]
    public void AllowsSmallPixelNoiseWithinTolerance()
    {
        var tracker = new BoardStabilityTracker(brightnessTolerance: 4);

        Assert.False(tracker.AddFrame(CreateFrame(100)));
        Assert.False(tracker.AddFrame(CreateFrame(104)));
        Assert.True(tracker.AddFrame(CreateFrame(103)));
    }

    [Fact]
    public void RejectsMalformedBoardFrames()
    {
        var tracker = new BoardStabilityTracker();

        Assert.Throws<ArgumentException>(() => tracker.AddFrame(new int[7, 8]));
    }

    private static int[,] CreateFrame(int value)
    {
        var frame = new int[8, 8];
        for (int rank = 0; rank < 8; rank++)
            for (int file = 0; file < 8; file++)
                frame[rank, file] = value;
        return frame;
    }
}
