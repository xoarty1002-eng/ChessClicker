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
}
