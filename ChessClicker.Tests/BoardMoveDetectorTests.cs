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
