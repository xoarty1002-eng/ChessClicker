using ChessClicker;
using Xunit;

namespace ChessClicker.Tests;

public sealed class ChessBoardObservedMoveTests
{
    [Fact]
    public void AllowsUniqueObservedMoveForOtherSideAndResynchronizesTurn()
    {
        var board = new ChessBoard(ChessBoard.StandardStartingFen);

        Assert.False(board.MakeMove("e7e5"));
        Assert.True(board.MakeObservedMove("e7e5"));
        Assert.Equal("white", board.Turn);
        Assert.Contains("=== Player: white | Turn: white ===", board.GetDebugBoardString());
    }

    [Fact]
    public void LoadsFenForAnInProgressPositionAndTurn()
    {
        var board = new ChessBoard("rnbqkbnr/pppp1ppp/8/4p3/4P3/8/PPPP1PPP/RNBQKBNR b KQkq - 0 1");

        Assert.Equal("black", board.Turn);
        Assert.True(board.ValidateMove(1, 3, 3, 3));
    }

    [Theory]
    [InlineData("8/8/8/8/8/8/8/8 x")]
    [InlineData("8/8/8/8/8/8/8/7X w")]
    [InlineData("8/8/8/8/8/8/8/9 w")]
    public void RejectsMalformedFen(string fen)
    {
        Assert.Throws<FormatException>(() => new ChessBoard(fen));
    }

    [Fact]
    public void TracksPositionAndFenStateThroughStandardOpeningMoves()
    {
        var board = new ChessBoard(ChessBoard.StandardStartingFen);

        Assert.True(board.MakeMove("e2e4"));
        Assert.Equal("rnbqkbnr/pppppppp/8/8/4P3/8/PPPP1PPP/RNBQKBNR b KQkq e3 0 1", board.GenerateFen());
        Assert.True(board.MakeMove("c7c5"));
        Assert.Equal("rnbqkbnr/pp1ppppp/8/2p5/4P3/8/PPPP1PPP/RNBQKBNR w KQkq c6 0 2", board.GenerateFen());
    }

    [Fact]
    public void AppliesBothKindsOfCastlingAndUpdatesRights()
    {
        var board = new ChessBoard("r3k2r/8/8/8/8/8/8/R3K2R w KQkq - 0 1");

        Assert.True(board.MakeMove("e1g1"));
        Assert.Equal("r3k2r/8/8/8/8/8/8/R4RK1 b kq - 1 1", board.GenerateFen());
        Assert.True(board.MakeMove("e8c8"));
        Assert.Equal("2kr3r/8/8/8/8/8/8/R4RK1 w - - 2 2", board.GenerateFen());
    }

    [Fact]
    public void RejectsCastlingThroughAnAttackedSquare()
    {
        var board = new ChessBoard("4kr2/8/8/8/8/8/8/4K2R w K - 0 1");

        Assert.False(board.ValidateMove(7, 4, 7, 6));
        Assert.False(board.MakeMove("e1g1"));
    }

    [Fact]
    public void AppliesEnPassantCaptureAndClearsCapturedPawn()
    {
        var board = new ChessBoard("4k3/8/8/3pP3/8/8/8/4K3 w - d6 0 2");

        Assert.True(board.MakeMove("e5d6"));

        Assert.Equal("4k3/8/3P4/8/8/8/8/4K3 b - - 0 2", board.GenerateFen());
    }

    [Fact]
    public void RejectsEnPassantThatWouldExposeTheKing()
    {
        var board = new ChessBoard("k3r3/8/8/3pP3/4K3/8/8/8 w - d6 0 1");

        Assert.False(board.ValidateMove(3, 4, 2, 3));
        Assert.False(board.MakeMove("e5d6"));
    }

    [Theory]
    [InlineData("q", 'Q')]
    [InlineData("r", 'R')]
    [InlineData("b", 'B')]
    [InlineData("n", 'N')]
    public void PromotesPawnToRequestedPiece(string promotion, char expectedPiece)
    {
        var board = new ChessBoard("4k3/P7/8/8/8/8/8/4K3 w - - 0 1");

        Assert.True(board.MakeMove($"a7a8{promotion}"));

        Assert.Equal($"{expectedPiece}3k3/8/8/8/8/8/8/4K3 b - - 0 1", board.GenerateFen());
    }

    [Fact]
    public void RejectsMovesThatExposeOwnKingAndReportsFoolsMate()
    {
        var pinnedPosition = new ChessBoard("4r1k1/8/8/8/8/8/4R3/4K3 w - - 0 1");
        Assert.False(pinnedPosition.ValidateMove(6, 4, 6, 5));

        var board = new ChessBoard(ChessBoard.StandardStartingFen);
        Assert.True(board.MakeMove("f2f3"));
        Assert.True(board.MakeMove("e7e5"));
        Assert.True(board.MakeMove("g2g4"));
        Assert.True(board.MakeMove("d8h4"));
        Assert.True(board.Check);
        Assert.True(board.Checkmate);
    }

    [Fact]
    public void RecognizesStalemateWithoutReportingCheck()
    {
        var board = new ChessBoard("7k/5Q2/6K1/8/8/8/8/8 b - - 0 1");

        Assert.False(board.Check);
        Assert.False(board.Checkmate);
        Assert.False(board.HasLegalMoveForTurn());
    }

    [Theory]
    [InlineData(1, 20)]
    [InlineData(2, 400)]
    [InlineData(3, 8902)]
    public void StandardPositionMatchesKnownLegalMoveCounts(int depth, long expectedNodes)
    {
        Assert.Equal(expectedNodes, Perft(new ChessBoard(ChessBoard.StandardStartingFen), depth));
    }

    [Fact]
    public void KiwipetePositionIncludesCastlingAndMatchesKnownMoveCount()
    {
        var board = new ChessBoard(
            "r3k2r/p1ppqpb1/bn2pnp1/3PN3/1p2P3/2N2Q1p/PPPBBPPP/R3K2R w KQkq - 0 1");

        Assert.Equal(48, Perft(board, depth: 1));
        Assert.Equal(2039, Perft(board, depth: 2));
    }

    [Theory]
    [InlineData(1, 14)]
    [InlineData(2, 191)]
    [InlineData(3, 2812)]
    public void PositionWithEnPassantAndEndgameKingsMatchesKnownMoveCounts(int depth, long expectedNodes)
    {
        var board = new ChessBoard("8/2p5/3p4/KP5r/1R3p1k/8/4P1P1/8 w - - 0 1");

        Assert.Equal(expectedNodes, Perft(board, depth));
    }

    private static long Perft(ChessBoard board, int depth)
    {
        if (depth == 0)
            return 1;

        long nodes = 0;
        for (int fromRank = 0; fromRank < 8; fromRank++)
        {
            for (int fromFile = 0; fromFile < 8; fromFile++)
            {
                for (int toRank = 0; toRank < 8; toRank++)
                {
                    for (int toFile = 0; toFile < 8; toFile++)
                    {
                        if (!board.ValidateMove(fromRank, fromFile, toRank, toFile))
                            continue;
                        string move = $"{(char)('a' + fromFile)}{8 - fromRank}" +
                                      $"{(char)('a' + toFile)}{8 - toRank}";
                        var child = new ChessBoard(board.GenerateFen());
                        Assert.True(child.MakeMove(move));
                        nodes += Perft(child, depth - 1);
                    }
                }
            }
        }

        return nodes;
    }

}
