using System;

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

        public ChessClickerSettings(
            int moveTimeMilliseconds,
            int stockfishSkillLevel,
            int framesPerSecond = 5,
            int brightnessThreshold = 18,
            string boardTheme = "Green",
            bool randomizeStockfishSkill = false,
            int randomSkillIntervalTurns = 1)
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

            MoveTimeMilliseconds = moveTimeMilliseconds;
            StockfishSkillLevel = stockfishSkillLevel;
            FramesPerSecond = framesPerSecond;
            BrightnessThreshold = brightnessThreshold;
            BoardTheme = boardTheme;
            RandomizeStockfishSkill = randomizeStockfishSkill;
            RandomSkillIntervalTurns = randomSkillIntervalTurns;
        }

        public static ChessClickerSettings Default { get; } = new(1000, 20);
    }
}
