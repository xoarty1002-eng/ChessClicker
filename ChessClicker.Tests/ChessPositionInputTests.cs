using ChessClicker;
using Xunit;

namespace ChessClicker.Tests;

public sealed class ChessPositionInputTests
{
    [Theory]
    [InlineData("e2e4", ChessPositionInputKind.Move, "e2e4")]
    [InlineData("a8-a6", ChessPositionInputKind.Move, "a8a6")]
    [InlineData(ChessBoard.StandardStartingFen, ChessPositionInputKind.Fen, ChessBoard.StandardStartingFen)]
    public void DetectsMoveVersusFenInput(
        string input,
        ChessPositionInputKind expectedKind,
        string expectedValue)
    {
        ParsedChessPositionInput parsed = ChessPositionInput.Parse(input);

        Assert.Equal(expectedKind, parsed.Kind);
        Assert.Equal(expectedValue, parsed.Value);
    }

    [Theory]
    [InlineData("rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR x KQkq - 0 1")]
    [InlineData("this/is/not/a/fen")]
    public void RejectsMalformedFenInsteadOfTreatingItAsAMove(string input)
    {
        Assert.Throws<FormatException>(() => ChessPositionInput.Parse(input));
    }
}
