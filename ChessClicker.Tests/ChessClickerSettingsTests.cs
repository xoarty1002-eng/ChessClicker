using ChessClicker;
using Xunit;

namespace ChessClicker.Tests;

public sealed class ChessClickerSettingsTests
{
    [Theory]
    [InlineData(99, 10)]
    [InlineData(10001, 10)]
    public void RejectsMoveTimesOutsideSupportedRange(int moveTime, int skillLevel)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new ChessClickerSettings(moveTime, skillLevel));
    }

    [Theory]
    [InlineData(100, -1)]
    [InlineData(100, 21)]
    public void RejectsStockfishSkillOutsideSupportedRange(int moveTime, int skillLevel)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new ChessClickerSettings(moveTime, skillLevel));
    }

    [Fact]
    public void DefaultSettingsUseSupportedValues()
    {
        Assert.Equal(1000, ChessClickerSettings.Default.MoveTimeMilliseconds);
        Assert.Equal(20, ChessClickerSettings.Default.StockfishSkillLevel);
    }
}
