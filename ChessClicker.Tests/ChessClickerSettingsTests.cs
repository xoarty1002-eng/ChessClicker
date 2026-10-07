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
        Assert.Equal(10, ChessClickerSettings.Default.FramesPerSecond);
        Assert.Equal(10, ChessClickerSettings.Default.RecommendedFramesPerSecond);
        Assert.Equal(18, ChessClickerSettings.Default.BrightnessThreshold);
        Assert.Equal("Green", ChessClickerSettings.Default.BoardTheme);
        Assert.False(ChessClickerSettings.Default.RandomizeStockfishSkill);
        Assert.Equal(1, ChessClickerSettings.Default.RandomSkillIntervalTurns);
        Assert.False(ChessClickerSettings.Default.CalibrateWhilePlaying);
        Assert.Equal(50, ChessClickerSettings.Default.PreviewSmoothingPercent);
        Assert.Equal(ChessBoard.StandardStartingFen, ChessClickerSettings.Default.StartingPositionFen);
        Assert.Equal("", ChessClickerSettings.Default.EnginePath);
        Assert.Equal("Solo", ChessClickerSettings.Default.PlayMode);
        Assert.Equal("Bottom", ChessClickerSettings.Default.EngineSide);
        Assert.Equal(500, ChessClickerSettings.Default.StableBoardDurationMilliseconds);
        Assert.Equal(8, ChessClickerSettings.Default.MinimumPieceSignatureSeparationPercent);
        Assert.True(ChessClickerSettings.Default.ProcessPossibleLegalTurn);
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
    public void RejectsInvalidStartingFen()
    {
        Assert.Throws<FormatException>(() => new ChessClickerSettings(
            1000, 20, startingPositionFen: "not a fen"));
    }

    [Fact]
    public void SavesAndLoadsConfiguredPreviewRate()
    {
        string path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "settings.json");
        const string midgameFen = "rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR b KQkq - 0 1";
        var expected = new ChessClickerSettings(
            1500, 14, 12, 22, "Blue", true, 3, true, 75, midgameFen,
            "/engines/example-engine", "Duo", "Top", 1200, 17, false);

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
            Assert.Equal(expected.StartingPositionFen, actual.StartingPositionFen);
            Assert.Equal(expected.EnginePath, actual.EnginePath);
            Assert.Equal(expected.PlayMode, actual.PlayMode);
            Assert.Equal(expected.EngineSide, actual.EngineSide);
            Assert.Equal(expected.StableBoardDurationMilliseconds, actual.StableBoardDurationMilliseconds);
            Assert.Equal(expected.MinimumPieceSignatureSeparationPercent, actual.MinimumPieceSignatureSeparationPercent);
            Assert.Equal(expected.ProcessPossibleLegalTurn, actual.ProcessPossibleLegalTurn);
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
            Assert.Equal("Solo", loaded.PlayMode);
            Assert.Equal("Bottom", loaded.EngineSide);
            Assert.Equal(500, loaded.StableBoardDurationMilliseconds);
            Assert.Equal(10, loaded.FramesPerSecond);
            Assert.Equal(8, loaded.MinimumPieceSignatureSeparationPercent);
            Assert.True(loaded.ProcessPossibleLegalTurn);
        }
        finally
        {
            string? directory = Path.GetDirectoryName(path);
            if (directory != null && Directory.Exists(directory))
                Directory.Delete(directory, recursive: true);
        }
    }

    [Theory]
    [InlineData(500, 10)]
    [InlineData(1000, 5)]
    [InlineData(10000, 1)]
    public void RecommendsFiveBoardSamplesDuringTheStabilityWindow(int stableMilliseconds, int expectedFps)
    {
        var settings = new ChessClickerSettings(
            1000,
            20,
            stableBoardDurationMilliseconds: stableMilliseconds);

        Assert.Equal(expectedFps, settings.RecommendedFramesPerSecond);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(101)]
    public void RejectsUnsupportedPieceSignatureSeparation(int separationPercent)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new ChessClickerSettings(
            1000, 20, minimumPieceSignatureSeparationPercent: separationPercent));
    }

    [Theory]
    [InlineData("Invalid", "Bottom", 500)]
    [InlineData("Solo", "Left", 500)]
    [InlineData("Solo", "Bottom", -1)]
    [InlineData("Solo", "Bottom", 499)]
    [InlineData("Duo", "Top", 10001)]
    public void RejectsInvalidGameAndStabilitySettings(string mode, string side, int stableMilliseconds)
    {
        Assert.ThrowsAny<ArgumentException>(() => new ChessClickerSettings(
            1000, 20, playMode: mode, engineSide: side,
            stableBoardDurationMilliseconds: stableMilliseconds));
    }
}
