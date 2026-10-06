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
        Assert.Equal(5, ChessClickerSettings.Default.FramesPerSecond);
        Assert.Equal(18, ChessClickerSettings.Default.BrightnessThreshold);
        Assert.Equal("Green", ChessClickerSettings.Default.BoardTheme);
        Assert.False(ChessClickerSettings.Default.RandomizeStockfishSkill);
        Assert.Equal(1, ChessClickerSettings.Default.RandomSkillIntervalTurns);
    }

    [Theory]
    [InlineData(0, 18, "Green", 1)]
    [InlineData(31, 18, "Green", 1)]
    [InlineData(5, 0, "Green", 1)]
    [InlineData(5, 101, "Green", 1)]
    [InlineData(5, 18, "Purple", 1)]
    [InlineData(5, 18, "Green", 0)]
    [InlineData(5, 18, "Green", 101)]
    public void RejectsUnsupportedNewSettings(
        int framesPerSecond, int brightnessThreshold, string theme, int randomSkillInterval)
    {
        Assert.ThrowsAny<ArgumentException>(() => new ChessClickerSettings(
            1000, 20, framesPerSecond, brightnessThreshold, theme, false, randomSkillInterval));
    }
}
