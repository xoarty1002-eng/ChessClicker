using System;

namespace ChessClicker
{
    public sealed class ChessClickerSettings
    {
        public int MoveTimeMilliseconds { get; }
        public int StockfishSkillLevel { get; }

        public ChessClickerSettings(int moveTimeMilliseconds, int stockfishSkillLevel)
        {
            if (moveTimeMilliseconds < 100 || moveTimeMilliseconds > 10000)
                throw new ArgumentOutOfRangeException(nameof(moveTimeMilliseconds), "Move time must be between 100 and 10000 milliseconds.");
            if (stockfishSkillLevel < 0 || stockfishSkillLevel > 20)
                throw new ArgumentOutOfRangeException(nameof(stockfishSkillLevel), "Stockfish skill level must be between 0 and 20.");

            MoveTimeMilliseconds = moveTimeMilliseconds;
            StockfishSkillLevel = stockfishSkillLevel;
        }

        public static ChessClickerSettings Default { get; } = new(1000, 20);
    }
}
