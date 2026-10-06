using System;
using System.Collections.Generic;

namespace ChessClicker
{
    public static class BoardBrightnessAnalyzer
    {
        public static IReadOnlyList<(int Rank, int File)> FindChangedSquares(
            int[,] previous,
            int[,] current,
            int threshold = 18)
        {
            ValidateGrid(previous, nameof(previous));
            ValidateGrid(current, nameof(current));
            if (threshold < 0) throw new ArgumentOutOfRangeException(nameof(threshold));

            int[] deltas = new int[64];
            int index = 0;
            for (int rank = 0; rank < 8; rank++)
                for (int file = 0; file < 8; file++)
                    deltas[index++] = current[rank, file] - previous[rank, file];

            Array.Sort(deltas);
            double medianDelta = (deltas[31] + deltas[32]) / 2.0;
            List<(int Rank, int File)> changedSquares = new();
            for (int rank = 0; rank < 8; rank++)
            {
                for (int file = 0; file < 8; file++)
                {
                    double localDelta = current[rank, file] - previous[rank, file];
                    if (Math.Abs(localDelta - medianDelta) > threshold)
                        changedSquares.Add((rank, file));
                }
            }

            return changedSquares;
        }

        private static void ValidateGrid(int[,] grid, string parameterName)
        {
            ArgumentNullException.ThrowIfNull(grid);
            if (grid.GetLength(0) != 8 || grid.GetLength(1) != 8)
                throw new ArgumentException("Brightness data must contain an 8-by-8 grid.", parameterName);
        }
    }
}
