using ChessClicker;
using Xunit;

namespace ChessClicker.Tests;

public sealed class BoardPositionReconstructorTests
{
    [Fact]
    public void ReconstructsOneOpeningPawnMoveFromOccupiedSquares()
    {
        var startingPosition = new ChessBoard(ChessBoard.StandardStartingFen);
        HashSet<string> observed = GetOccupiedSquares(startingPosition);
        observed.Remove("e2");
        observed.Add("e4");

        Assert.Equal(
            "e2e4",
            BoardPositionReconstructor.FindSingleQuietMove(startingPosition, observed));
    }

    [Fact]
    public void ReconstructsOneMoveForBlackWhenBlackIsToMove()
    {
        var startingPosition = new ChessBoard(
            "rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR b KQkq - 0 1");
        HashSet<string> observed = GetOccupiedSquares(startingPosition);
        observed.Remove("e7");
        observed.Add("e5");

        Assert.Equal(
            "e7e5",
            BoardPositionReconstructor.FindSingleQuietMove(startingPosition, observed));
    }

    [Fact]
    public void DoesNotGuessWhenObservedPositionContainsMoreThanOneMove()
    {
        var startingPosition = new ChessBoard(ChessBoard.StandardStartingFen);
        HashSet<string> observed = GetOccupiedSquares(startingPosition);
        observed.Remove("e2");
        observed.Add("e4");
        observed.Remove("d2");
        observed.Add("d4");

        Assert.Null(
            BoardPositionReconstructor.FindSingleQuietMove(startingPosition, observed));
    }

    [Fact]
    public void SelectsTheExpectedNumberOfSquaresOnlyWhenTheScoreGapIsClear()
    {
        double[,] scores = new double[8, 8];
        for (int rank = 0; rank < 8; rank++)
            for (int file = 0; file < 8; file++)
                scores[rank, file] = 4;
        for (int index = 0; index < 32; index++)
            scores[index / 8, index % 8] = 30;

        Assert.True(PieceOccupancyAnalyzer.TrySelectOccupiedSquares(
            scores, occupiedSquareCount: 32, minimumSeparation: 8, out bool[,] occupied));
        Assert.Equal(32, CountOccupied(occupied));

        scores[4, 0] = 25;
        Assert.False(PieceOccupancyAnalyzer.TrySelectOccupiedSquares(
            scores, occupiedSquareCount: 32, minimumSeparation: 8, out _));
    }

    private static HashSet<string> GetOccupiedSquares(ChessBoard board)
    {
        HashSet<string> occupied = new(StringComparer.Ordinal);
        for (char file = 'a'; file <= 'h'; file++)
            for (char rank = '1'; rank <= '8'; rank++)
            {
                string square = $"{file}{rank}";
                if (board.GetPieceAt(square) != ' ')
                    occupied.Add(square);
            }

        return occupied;
    }

    private static int CountOccupied(bool[,] occupied)
    {
        int count = 0;
        for (int rank = 0; rank < 8; rank++)
            for (int file = 0; file < 8; file++)
                if (occupied[rank, file])
                    count++;
        return count;
    }
}
