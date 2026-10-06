using ChessClicker;
using System.IO;
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
        Assert.False(ChessClickerSettings.Default.CalibrateWhilePlaying);
        Assert.Equal(50, ChessClickerSettings.Default.PreviewSmoothingPercent);
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

    [Theory]
    [InlineData(-1)]
    [InlineData(101)]
    public void RejectsUnsupportedPreviewSmoothing(int smoothingPercent)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new ChessClickerSettings(
            1000, 20, previewSmoothingPercent: smoothingPercent));
    }

    [Fact]
    public void SavesAndLoadsConfiguredPreviewRate()
    {
        string path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "settings.json");
        var expected = new ChessClickerSettings(1500, 14, 12, 22, "Blue", true, 3, true, 75);

        try
        {
            expected.SaveToFile(path);
            ChessClickerSettings actual = ChessClickerSettings.LoadFromFile(path);

            Assert.Equal(expected.MoveTimeMilliseconds, actual.MoveTimeMilliseconds);
            Assert.Equal(expected.StockfishSkillLevel, actual.StockfishSkillLevel);
            Assert.Equal(expected.FramesPerSecond, actual.FramesPerSecond);
            Assert.Equal(expected.BrightnessThreshold, actual.BrightnessThreshold);
            Assert.Equal(expected.BoardTheme, actual.BoardTheme);
            Assert.Equal(expected.RandomizeStockfishSkill, actual.RandomizeStockfishSkill);
            Assert.Equal(expected.RandomSkillIntervalTurns, actual.RandomSkillIntervalTurns);
            Assert.Equal(expected.CalibrateWhilePlaying, actual.CalibrateWhilePlaying);
            Assert.Equal(expected.PreviewSmoothingPercent, actual.PreviewSmoothingPercent);
        }
        finally
        {
            string? directory = Path.GetDirectoryName(path);
            if (directory != null && Directory.Exists(directory))
                Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void LoadsOlderSettingsWithoutSmoothingAsFiftyPercent()
    {
        string path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "settings.json");

        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path,
                """{"MoveTimeMilliseconds":1000,"StockfishSkillLevel":20}""");

            ChessClickerSettings loaded = ChessClickerSettings.LoadFromFile(path);

            Assert.Equal(50, loaded.PreviewSmoothingPercent);
            Assert.False(loaded.CalibrateWhilePlaying);
        }
        finally
        {
            string? directory = Path.GetDirectoryName(path);
            if (directory != null && Directory.Exists(directory))
                Directory.Delete(directory, recursive: true);
        }
    }
}
