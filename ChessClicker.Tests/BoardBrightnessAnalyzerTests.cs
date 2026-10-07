using ChessClicker;
using Xunit;

namespace ChessClicker.Tests;

public sealed class BoardBrightnessAnalyzerTests
{
    [Fact]
    public void FindsMoveSquaresDespiteBrightnessShiftAcrossEveryCell()
    {
        int[,] previous = FilledGrid(100);
        int[,] current = new int[8, 8];
        for (int rank = 0; rank < 8; rank++)
        {
            for (int file = 0; file < 8; file++)
            {
                int noise = (rank * 8 + file) % 5 - 2;
                current[rank, file] = 130 + noise;
            }
        }

        current[6, 4] = 170;
        current[4, 4] = 80;

        IReadOnlyList<(int Rank, int File)> changed =
            BoardBrightnessAnalyzer.FindChangedSquares(previous, current);

        Assert.Equal(new[] { (4, 4), (6, 4) }, changed.OrderBy(square => square.Rank));
        string candidates = BoardMoveDetector.CreateCandidates(changed, isWhiteView: true);
        Assert.Equal(new[] { "e2e4", "e4e2" }, candidates.Split('|').OrderBy(move => move).ToArray());
    }

    [Fact]
    public void IgnoresUniformBoardWideBrightnessShift()
    {
        int[,] previous = FilledGrid(80);
        int[,] current = FilledGrid(145);

        Assert.Empty(BoardBrightnessAnalyzer.FindChangedSquares(previous, current));
    }

    [Fact]
    public void SelectsOccupiedSquaresAfterACaptureWhenPieceCountDecreasesByOne()
    {
        double[,] scores = new double[8, 8];
        for (int index = 0; index < 31; index++)
            scores[index / 8, index % 8] = 40;
        scores[3, 7] = 10;
        scores[4, 0] = 0;

        Assert.True(PieceOccupancyAnalyzer.TrySelectOccupiedSquares(
            scores,
            minimumOccupiedSquareCount: 31,
            maximumOccupiedSquareCount: 32,
            minimumSeparation: 8,
            out bool[,] occupied));

        Assert.Equal(31, occupied.Cast<bool>().Count(value => value));
        Assert.True(occupied[3, 6]);
        Assert.False(occupied[3, 7]);
    }

    [Fact]
    public void FindsUniqueLegalMoveAmongEveryOrderedPairFromAllCells()
    {
        var allSquares = new List<(int Rank, int File)>();
        for (int rank = 0; rank < 8; rank++)
            for (int file = 0; file < 8; file++)
                allSquares.Add((rank, file));

        string[] candidates = BoardMoveDetector
            .CreateCandidates(allSquares, isWhiteView: true)
            .Split('|');

        string? move = BoardMoveDetector.FindUniqueLegalMove(
            candidates,
            candidate => candidate == "e2e4");

        Assert.Equal(64 * 63, candidates.Length);
        Assert.Equal("e2e4", move);
    }

    [Fact]
    public void DoesNotGuessWhenMoreThanOneLegalMoveMatches()
    {
        string? move = BoardMoveDetector.FindUniqueLegalMove(
            ["e2e4", "d2d4", "e4e2"],
            candidate => candidate is "e2e4" or "d2d4");

        Assert.Null(move);
    }

    [Theory]
    [InlineData(true, "d7d5")]
    [InlineData(false, "e2e4")]
    public void GeneratesCandidatesInTheCapturedBoardOrientation(bool whiteView, string expectedMove)
    {
        IReadOnlyList<(int Rank, int File)> changes = [(1, 3), (3, 3)];

        string candidates = BoardMoveDetector.CreateCandidates(changes, whiteView);

        Assert.Contains(expectedMove, candidates.Split('|'));
    }

    [Fact]
    public void SelectsBlackPawnMoveFromBlackPerspectiveWhenStartingFenSaysBlackToMove()
    {
        var board = new ChessBoard(
            "rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR b KQkq - 0 1");
        IReadOnlyList<(int Rank, int File)> changes = [(6, 4), (4, 4)];
        string[] candidates = BoardMoveDetector.CreateCandidates(changes, isWhiteView: false).Split('|');

        string? move = BoardMoveDetector.FindUniqueLegalMove(candidates, candidate =>
        {
            int fromFile = candidate[0] - 'a';
            int fromRank = 8 - (candidate[1] - '0');
            int toFile = candidate[2] - 'a';
            int toRank = 8 - (candidate[3] - '0');
            return board.ValidateMove(fromRank, fromFile, toRank, toFile);
        });

        Assert.Equal("d7d5", move);
    }

    [Fact]
    public void SelectsOnlyLegalWhiteMoveAmongTheChangedScreenSquares()
    {
        var board = new ChessBoard(ChessBoard.StandardStartingFen);
        IReadOnlyList<(int Rank, int File)> changedSquares = [(6, 4), (4, 4)];
        string[] candidates = BoardMoveDetector.CreateCandidates(changedSquares, isWhiteView: true)
            .Split('|');

        string? move = BoardMoveDetector.FindUniqueLegalMove(candidates, candidate =>
            board.ValidateMove(
                8 - (candidate[1] - '0'), candidate[0] - 'a',
                8 - (candidate[3] - '0'), candidate[2] - 'a'));

        Assert.Equal("e2e4", move);
    }

    [Fact]
    public void GeneratesMoveOriginsOnlyFromChangedSquaresContainingPieces()
    {
        var board = new ChessBoard(ChessBoard.StandardStartingFen);
        IReadOnlyList<(int Rank, int File)> changedSquares =
            [(6, 4), (4, 4), (5, 2)];

        string[] candidates = BoardMoveDetector.CreateCandidates(
            changedSquares, isWhiteView: true, board).Split('|');

        Assert.Contains("e2e4", candidates);
        Assert.DoesNotContain(candidates, move => move.StartsWith("e4", StringComparison.Ordinal));
        Assert.DoesNotContain(candidates, move => move.StartsWith("c3", StringComparison.Ordinal));
        Assert.Contains("e2c3", candidates);
    }

    [Fact]
    public void DetectsObservedMoveWhenTrackedTurnDoesNotMatchTheDisplayedMove()
    {
        var board = new ChessBoard(
            "rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR b KQkq - 0 1");
        IReadOnlyList<(int Rank, int File)> changedSquares = [(6, 4), (4, 4)];
        string[] candidates = BoardMoveDetector.CreateCandidates(changedSquares, isWhiteView: true)
            .Split('|');

        string? move = BoardMoveDetector.FindUniqueLegalMove(candidates, candidate =>
            board.ValidateMoveIgnoringTurn(
                8 - (candidate[1] - '0'), candidate[0] - 'a',
                8 - (candidate[3] - '0'), candidate[2] - 'a'));

        Assert.Equal("e2e4", move);
        Assert.True(board.MakeObservedMove(move!));
        Assert.Equal("black", board.Turn);
    }

    [Theory]
    [InlineData(true, "e1g1", 7, 4, 7, 6, 7, 7, 7, 5)]
    [InlineData(true, "e1c1", 7, 4, 7, 2, 7, 0, 7, 3)]
    [InlineData(false, "e8g8", 7, 3, 7, 1, 7, 0, 7, 2)]
    [InlineData(false, "e8c8", 7, 3, 7, 5, 7, 7, 7, 4)]
    public void DetectsCastlingAsOneCompoundMove(
        bool isWhiteView,
        string expectedMove,
        int firstRank,
        int firstFile,
        int secondRank,
        int secondFile,
        int thirdRank,
        int thirdFile,
        int fourthRank,
        int fourthFile)
    {
        IReadOnlyList<(int Rank, int File)> changedSquares =
        [
            (firstRank, firstFile),
            (secondRank, secondFile),
            (thirdRank, thirdFile),
            (fourthRank, fourthFile)
        ];

        string[] candidates = BoardMoveDetector.CreateCandidates(changedSquares, isWhiteView).Split('|');
        var board = new ChessBoard(
            $"r3k2r/8/8/8/8/8/8/R3K2R {(expectedMove[1] == '1' ? "w" : "b")} KQkq - 0 1");
        string? detectedMove = BoardMoveDetector.FindUniqueLegalMove(candidates, candidate =>
            board.ValidateMove(
                8 - (candidate[1] - '0'), candidate[0] - 'a',
                8 - (candidate[3] - '0'), candidate[2] - 'a'));

        Assert.Equal(expectedMove, detectedMove);
    }

    private static int[,] FilledGrid(int value)
    {
        int[,] grid = new int[8, 8];
        for (int rank = 0; rank < 8; rank++)
            for (int file = 0; file < 8; file++)
                grid[rank, file] = value;
        return grid;
    }
}
