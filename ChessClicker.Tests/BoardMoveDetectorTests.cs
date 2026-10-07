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
    public void FindsUniqueLegalMoveWhenTrackedTurnIsWrong()
    {
        var position = new ChessBoard(
            "rnbqkbnr/pppppppp/8/8/4P3/8/PPPP1PPP/RNBQKBNR w KQkq - 0 1");
        string[] candidates = ["a7a6", "e4a6", "a7b6"];

        Assert.Null(BoardMoveDetector.FindUniqueLegalMove(
            candidates,
            move => IsLegalMove(position, move)));

        string? detectedMove = BoardMoveDetector.FindUniqueLegalMove(
            candidates,
            move => IsLegalIgnoringTurn(position, move));

        Assert.Equal("a7a6", detectedMove);
        Assert.True(position.MakeObservedMove(detectedMove!));
        Assert.Equal("white", position.Turn);
    }

    [Fact]
    public void RecoversMissedMoveFromCurrentBoardOccupancy()
    {
        var trackedPosition = new ChessBoard(ChessBoard.StandardStartingFen);
        Assert.True(trackedPosition.MakeMove("d2d4"));

        var displayedPosition = new ChessBoard(trackedPosition.GenerateFen());
        Assert.True(displayedPosition.MakeObservedMove("b7b6"));

        string? detectedMove = BoardMoveDetector.FindUniqueMoveMatchingPosition(
            trackedPosition, GetOccupiedSquares(displayedPosition));

        Assert.Equal("b7b6", detectedMove);
        Assert.True(trackedPosition.MakeMove(detectedMove!));
        Assert.True(GetOccupiedSquares(trackedPosition).SetEquals(
            GetOccupiedSquares(displayedPosition)));
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

    [Theory]
    [InlineData("c1h6")]
    [InlineData("f1a6")]
    [InlineData("c8h3")]
    public void DetectsLongDiagonalBishopMoves(string move)
    {
        ChessBoard position = move switch
        {
            "c1h6" => new("4k3/8/8/8/8/8/8/2B1K3 w - - 0 1"),
            "f1a6" => new("4k3/8/8/8/8/8/8/4KB2 w - - 0 1"),
            "c8h3" => new("2b1k3/8/8/8/8/8/8/4K3 b - - 0 1"),
            _ => throw new ArgumentOutOfRangeException(nameof(move))
        };

        string[] candidates = [move, "e1e3"];
        string? detectedMove = BoardMoveDetector.FindUniqueLegalMove(
            candidates, candidate => IsLegalMove(position, candidate));

        Assert.Equal(move, detectedMove);
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

    private static bool IsLegalMove(ChessBoard position, string move) =>
        position.ValidateMove(
            8 - (move[1] - '0'), move[0] - 'a',
            8 - (move[3] - '0'), move[2] - 'a');

    private static bool IsLegalIgnoringTurn(ChessBoard position, string move) =>
        position.ValidateMoveIgnoringTurn(
            8 - (move[1] - '0'), move[0] - 'a',
            8 - (move[3] - '0'), move[2] - 'a');
}
