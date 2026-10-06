using ChessClicker;
using Xunit;

namespace ChessClicker.Tests;

public sealed class MoveConfirmationTests
{
    [Theory]
    [InlineData("e2e4|e4e2")]
    [InlineData("e4e2|e2e4")]
    public void ConfirmsExpectedMoveRegardlessOfDetectedCandidateOrder(string candidates)
    {
        var confirmation = new MoveConfirmation();
        confirmation.Expect("e2e4");

        Assert.True(confirmation.TryConfirm(candidates, out string? confirmedMove));
        Assert.Equal("e2e4", confirmedMove);
        Assert.Null(confirmation.PendingMove);
    }

    [Fact]
    public void DoesNotConfirmAnUnrelatedBoardChange()
    {
        var confirmation = new MoveConfirmation();
        confirmation.Expect("e2e4");

        Assert.False(confirmation.TryConfirm("d2d4|d4d2", out _));
        Assert.Equal("e2e4", confirmation.PendingMove);
    }

    [Fact]
    public void ConfirmsPromotionUsingTheObservedFromAndToSquares()
    {
        var confirmation = new MoveConfirmation();
        confirmation.Expect("e7e8q");

        Assert.True(confirmation.TryConfirm("e7e8|e8e7", out string? confirmedMove));
        Assert.Equal("e7e8q", confirmedMove);
    }

    [Fact]
    public void RejectsInvalidExpectedMove()
    {
        var confirmation = new MoveConfirmation();

        Assert.Throws<ArgumentException>(() => confirmation.Expect("e2"));
        Assert.Null(confirmation.PendingMove);
    }

    [Fact]
    public void CancelClearsAnUnconfirmedMoveSoScanningCanResume()
    {
        var confirmation = new MoveConfirmation();
        confirmation.Expect("e2e4");

        confirmation.Cancel();

        Assert.Null(confirmation.PendingMove);
        Assert.False(confirmation.TryConfirm("e2e4", out _));
    }
}
