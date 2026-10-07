using ChessClicker;
using Xunit;

namespace ChessClicker.Tests;

public sealed class BoardMoveDetectorTests
{
    [Fact]
    public void UsesPieceOccupancyToDisambiguateNoisySquaresAndDetectTheOtherSide()
    {
        var position = new ChessBoard(ChessBoard.StandardStartingFen);
        const string expectedMove = "h7h6";
        var observedPosition = new ChessBoard(position.GenerateFen());
        Assert.True(observedPosition.MakeObservedMove(expectedMove));

        string? detectedMove = BoardMoveDetector.FindUniqueMoveMatchingOccupiedSquares(
            ["h7h6", "g7g6", "e2e4", "h7g6"],
            position,
            GetOccupiedSquares(observedPosition));

        Assert.Equal(expectedMove, detectedMove);
    }

    [Fact]
    public void DoesNotGuessWhenNoCandidateMatchesTheObservedOccupancy()
    {
        var position = new ChessBoard(ChessBoard.StandardStartingFen);
        var observedPosition = new ChessBoard(position.GenerateFen());
        Assert.True(observedPosition.MakeMove("g1f3"));

        string? detectedMove = BoardMoveDetector.FindUniqueMoveMatchingOccupiedSquares(
            ["b1c3", "c2c4"],
            position,
            GetOccupiedSquares(observedPosition));

        Assert.Null(detectedMove);
    }

    [Fact]
    public void DetectsUniqueCheckmateDespiteOtherChangedSquareCandidates()
    {
        var position = new ChessBoard(ChessBoard.StandardStartingFen);
        Assert.True(position.MakeMove("f2f3"));
        Assert.True(position.MakeMove("e7e5"));
        Assert.True(position.MakeMove("g2g4"));

        string? detectedMove = BoardMoveDetector.FindUniqueCheckmatingMove(
            ["d8h4", "d8g5", "e8e7", "a7a6"],
            position);

        Assert.Equal("d8h4", detectedMove);
        Assert.False(position.Checkmate);

        Assert.True(position.MakeMove(detectedMove!));
        Assert.True(position.Checkmate);
    }

    [Fact]
    public void DetectsTheRookMateShownInTheWindowsScreenshot()
    {
        var position = new ChessBoard(
            "r2q1bnr/1p2p1kp/n1p5/p3pP1Q/3PN3/7P/7P/4K2R w - - 0 1");

        string? detectedMove = BoardMoveDetector.FindUniqueCheckmatingMove(
            ["h1g1", "d4d5", "e4f6", "h5h6"],
            position);

        Assert.Equal("h1g1", detectedMove);
        Assert.True(position.MakeMove(detectedMove!));
        Assert.True(position.Checkmate);
    }

    private static HashSet<string> GetOccupiedSquares(ChessBoard position)
    {
        HashSet<string> occupiedSquares = new(StringComparer.Ordinal);
        for (char file = 'a'; file <= 'h'; file++)
            for (char rank = '1'; rank <= '8'; rank++)
            {
                string square = $"{file}{rank}";
                if (position.GetPieceAt(square) != ' ')
                    occupiedSquares.Add(square);
            }

        return occupiedSquares;
    }
}
