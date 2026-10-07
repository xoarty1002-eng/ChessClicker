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
        public string StartingPositionFen { get; }
        public string EnginePath { get; }
        public string PlayMode { get; }
        public string EngineSide { get; }
        public int StableBoardDurationMilliseconds { get; }
        public int MinimumPieceSignatureSeparationPercent { get; }
        public bool ProcessPossibleLegalTurn { get; }

        [JsonConstructor]
        public ChessClickerSettings(
            int moveTimeMilliseconds,
            int stockfishSkillLevel,
            int framesPerSecond = 10,
            int brightnessThreshold = 18,
            string boardTheme = "Green",
            bool randomizeStockfishSkill = false,
            int randomSkillIntervalTurns = 1,
            bool calibrateWhilePlaying = false,
            int previewSmoothingPercent = 50,
            string startingPositionFen = ChessBoard.StandardStartingFen,
            string enginePath = "",
            string playMode = "Solo",
            string engineSide = "Bottom",
            int stableBoardDurationMilliseconds = 500,
            int minimumPieceSignatureSeparationPercent = 8,
            bool processPossibleLegalTurn = true)
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
            if (playMode is not ("Solo" or "Duo"))
                throw new ArgumentException("Play mode must be Solo or Duo.", nameof(playMode));
            if (engineSide is not ("Top" or "Bottom"))
                throw new ArgumentException("Engine side must be Top or Bottom.", nameof(engineSide));
            if (stableBoardDurationMilliseconds < 500 || stableBoardDurationMilliseconds > 10000)
                throw new ArgumentOutOfRangeException(
                    nameof(stableBoardDurationMilliseconds),
                    "Stable-board duration must be between 500 and 10000 milliseconds.");
            if (minimumPieceSignatureSeparationPercent is < 1 or > 100)
                throw new ArgumentOutOfRangeException(
                    nameof(minimumPieceSignatureSeparationPercent),
                    "Minimum piece signature separation must be between 1 and 100 percent.");
            ArgumentException.ThrowIfNullOrWhiteSpace(startingPositionFen);
            _ = new ChessBoard(startingPositionFen);

            MoveTimeMilliseconds = moveTimeMilliseconds;
            StockfishSkillLevel = stockfishSkillLevel;
            FramesPerSecond = framesPerSecond;
            BrightnessThreshold = brightnessThreshold;
            BoardTheme = boardTheme;
            RandomizeStockfishSkill = randomizeStockfishSkill;
            RandomSkillIntervalTurns = randomSkillIntervalTurns;
            CalibrateWhilePlaying = calibrateWhilePlaying;
            PreviewSmoothingPercent = previewSmoothingPercent;
            StartingPositionFen = startingPositionFen.Trim();
            EnginePath = enginePath?.Trim() ?? throw new ArgumentNullException(nameof(enginePath));
            PlayMode = playMode;
            EngineSide = engineSide;
            StableBoardDurationMilliseconds = stableBoardDurationMilliseconds;
            MinimumPieceSignatureSeparationPercent = minimumPieceSignatureSeparationPercent;
            ProcessPossibleLegalTurn = processPossibleLegalTurn;
        }

        public int RecommendedFramesPerSecond =>
            Math.Clamp(
                (5000 + StableBoardDurationMilliseconds - 1) / StableBoardDurationMilliseconds,
                1,
                30);

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
