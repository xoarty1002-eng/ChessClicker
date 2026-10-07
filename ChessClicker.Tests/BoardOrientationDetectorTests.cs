using ChessClicker;
using Xunit;

namespace ChessClicker.Tests;

public sealed class BoardOrientationDetectorTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void DetectsOrientationFromPieceContrastRatherThanSquareColor(bool whiteView)
    {
        double[,] contrast = new double[8, 8];
        double whitePieceContrast = 42;
        double blackPieceContrast = -36;

        int whiteHomeRank = whiteView ? 7 : 0;
        int whitePawnRank = whiteView ? 6 : 1;
        int blackHomeRank = whiteView ? 0 : 7;
        int blackPawnRank = whiteView ? 1 : 6;
        for (int file = 0; file < 8; file++)
        {
            contrast[whiteHomeRank, file] = whitePieceContrast;
            contrast[whitePawnRank, file] = whitePieceContrast;
            contrast[blackHomeRank, file] = blackPieceContrast;
            contrast[blackPawnRank, file] = blackPieceContrast;
        }

        Assert.True(BoardOrientationDetector.TryDetectWhiteView(contrast, out bool detectedView));
        Assert.Equal(whiteView, detectedView);
    }

    [Fact]
    public void ReportsInsufficientEvidenceInsteadOfGuessingFromAnEmptyBoard()
    {
        Assert.False(BoardOrientationDetector.TryDetectWhiteView(new double[8, 8], out _));
    }

    [Fact]
    public void DetectsSideFromPieceColorEvenWhenOneSideHasFewerPieces()
    {
        double[,] contrast = new double[8, 8];
        contrast[7, 4] = 42;
        for (int file = 0; file < 8; file++)
            contrast[0, file] = -36;

        Assert.True(BoardOrientationDetector.TryDetectWhiteView(contrast, out bool whiteView));
        Assert.True(whiteView);
    }

    [Fact]
    public void DoesNotChooseOrientationWhenBothEndsHaveTheSamePieceColorEvidence()
    {
        double[,] contrast = new double[8, 8];
        contrast[0, 0] = -35;
        contrast[7, 7] = -35;

        Assert.False(BoardOrientationDetector.TryDetectWhiteView(contrast, out _));
    }

    [Fact]
    public void RejectsNonBoardContrastMatrix()
    {
        Assert.Throws<ArgumentException>(() =>
            BoardOrientationDetector.TryDetectWhiteView(new double[7, 8], out _));
    }
}
