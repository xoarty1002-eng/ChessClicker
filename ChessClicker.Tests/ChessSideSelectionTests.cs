using ChessClicker;
using Xunit;

namespace ChessClicker.Tests;

public sealed class ChessSideSelectionTests
{
    [Theory]
    [InlineData(true, "Top", "black")]
    [InlineData(true, "Bottom", "white")]
    [InlineData(false, "Top", "white")]
    [InlineData(false, "Bottom", "black")]
    public void SelectsCorrectColorFromScreenSide(
        bool isWhiteView,
        string engineSide,
        string expectedColor)
    {
        Assert.Equal(expectedColor == "white",
            ChessSideSelection.IsWhiteAtScreenSide(isWhiteView, engineSide));
    }

    [Theory]
    [InlineData(true, "Bottom", "white", true)]
    [InlineData(true, "Bottom", "black", false)]
    [InlineData(true, "Top", "black", true)]
    [InlineData(true, "Top", "white", false)]
    [InlineData(false, "Bottom", "black", true)]
    [InlineData(false, "Bottom", "white", false)]
    [InlineData(false, "Top", "white", true)]
    [InlineData(false, "Top", "black", false)]
    public void SoloModeEngineActsOnlyOnSelectedScreenSide(
        bool isWhiteView,
        string engineSide,
        string activeColor,
        bool expected)
    {
        Assert.Equal(expected, ChessSideSelection.ShouldEngineMove(
            "Solo", engineSide, isWhiteView, activeColor));
    }

    [Theory]
    [InlineData("white")]
    [InlineData("black")]
    public void DuoModeRunsTheEngineForEitherActiveColor(string activeColor)
    {
        Assert.True(ChessSideSelection.ShouldEngineMove(
            "Duo", "Bottom", isWhiteView: true, activeColor: activeColor));
    }

    [Theory]
    [InlineData("QKkq")]
    [InlineData("KQkqk")]
    [InlineData("KQ-")]
    public void RejectsInvalidCastlingRights(string rights)
    {
        Assert.Throws<FormatException>(() => new ChessBoard(
            $"r3k2r/8/8/8/8/8/8/R3K2R w {rights} - 0 1"));
    }
}
