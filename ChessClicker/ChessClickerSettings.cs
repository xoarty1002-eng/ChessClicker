using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ChessClicker
{
    public sealed class ChessClickerSettings
    {
        public int MoveTimeMilliseconds { get; }
        public int StockfishSkillLevel { get; }
        public int FramesPerSecond { get; }
        public int BrightnessThreshold { get; }
        public string BoardTheme { get; }
        public bool RandomizeStockfishSkill { get; }
        public int RandomSkillIntervalTurns { get; }
        public bool CalibrateWhilePlaying { get; }
        public int PreviewSmoothingPercent { get; }

        [JsonConstructor]
        public ChessClickerSettings(
            int moveTimeMilliseconds,
            int stockfishSkillLevel,
            int framesPerSecond = 5,
            int brightnessThreshold = 18,
            string boardTheme = "Green",
            bool randomizeStockfishSkill = false,
            int randomSkillIntervalTurns = 1,
            bool calibrateWhilePlaying = false,
            int previewSmoothingPercent = 50)
        {
            if (moveTimeMilliseconds < 100 || moveTimeMilliseconds > 10000)
                throw new ArgumentOutOfRangeException(nameof(moveTimeMilliseconds), "Move time must be between 100 and 10000 milliseconds.");
            if (stockfishSkillLevel < 0 || stockfishSkillLevel > 20)
                throw new ArgumentOutOfRangeException(nameof(stockfishSkillLevel), "Stockfish skill level must be between 0 and 20.");
            if (framesPerSecond < 1 || framesPerSecond > 30)
                throw new ArgumentOutOfRangeException(nameof(framesPerSecond), "Preview frame rate must be between 1 and 30 FPS.");
            if (brightnessThreshold < 1 || brightnessThreshold > 100)
                throw new ArgumentOutOfRangeException(nameof(brightnessThreshold), "Brightness threshold must be between 1 and 100.");
            if (boardTheme is not ("Green" or "Brown" or "Blue" or "Gray"))
                throw new ArgumentException("Choose a supported board theme.", nameof(boardTheme));
            if (randomSkillIntervalTurns < 1 || randomSkillIntervalTurns > 100)
                throw new ArgumentOutOfRangeException(nameof(randomSkillIntervalTurns), "Random skill interval must be between 1 and 100 turns.");
            if (previewSmoothingPercent < 0 || previewSmoothingPercent > 100)
                throw new ArgumentOutOfRangeException(nameof(previewSmoothingPercent), "Preview smoothing must be between 0 and 100 percent.");

            MoveTimeMilliseconds = moveTimeMilliseconds;
            StockfishSkillLevel = stockfishSkillLevel;
            FramesPerSecond = framesPerSecond;
            BrightnessThreshold = brightnessThreshold;
            BoardTheme = boardTheme;
            RandomizeStockfishSkill = randomizeStockfishSkill;
            RandomSkillIntervalTurns = randomSkillIntervalTurns;
            CalibrateWhilePlaying = calibrateWhilePlaying;
            PreviewSmoothingPercent = previewSmoothingPercent;
        }

        public static ChessClickerSettings Default { get; } = new(1000, 20);

        public static ChessClickerSettings LoadFromFile(string path)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(path);
            using FileStream stream = File.OpenRead(path);
            return JsonSerializer.Deserialize<ChessClickerSettings>(stream)
                ?? throw new InvalidDataException("The settings file does not contain valid settings.");
        }

        public void SaveToFile(string path)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(path);
            string fullPath = Path.GetFullPath(path);
            Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
            using FileStream stream = File.Create(fullPath);
            JsonSerializer.Serialize(stream, this, new JsonSerializerOptions { WriteIndented = true });
        }
    }
}
