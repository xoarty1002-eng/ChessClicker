using ChessClicker;
using Xunit;

namespace ChessClicker.Tests;

public sealed class ChessMoveNotationTests
{
    [Theory]
    [InlineData("e2e4", "e2e4")]
    [InlineData(" A8-A6 ", "a8a6")]
    [InlineData("a8 x a6", "a8a6")]
    [InlineData("E7-E8=Q", "e7e8q")]
    [InlineData("e7e8n", "e7e8n")]
    public void NormalizesTypedCoordinateMove(string input, string expected)
    {
        Assert.Equal(expected, ChessMoveNotation.Normalize(input));
    }

    [Theory]
    [InlineData("")]
    [InlineData("e2")]
    [InlineData("i2i4")]
    [InlineData("e2e9")]
    [InlineData("e2--e4")]
    [InlineData("e7e8k")]
    [InlineData("e7-e8=")]
    [InlineData("e2e4extra")]
    public void RejectsInvalidMoveNotation(string input)
    {
        Assert.Throws<ArgumentException>(() => ChessMoveNotation.Normalize(input));
    }
}
