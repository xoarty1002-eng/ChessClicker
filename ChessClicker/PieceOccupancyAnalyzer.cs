using System;
using System.Linq;

namespace ChessClicker
{
    public static class PieceOccupancyAnalyzer
    {
        public static bool TrySelectOccupiedSquares(
            double[,] scores,
            int occupiedSquareCount,
            double minimumSeparation,
            out bool[,] occupiedSquares)
        {
            ArgumentNullException.ThrowIfNull(scores);
            if (scores.GetLength(0) != 8 || scores.GetLength(1) != 8)
                throw new ArgumentException("Occupancy scores must contain an 8-by-8 grid.", nameof(scores));
            if (occupiedSquareCount is < 1 or > 64)
                throw new ArgumentOutOfRangeException(nameof(occupiedSquareCount));
            if (!double.IsFinite(minimumSeparation) || minimumSeparation < 0)
                throw new ArgumentOutOfRangeException(nameof(minimumSeparation));
            if (scores.Cast<double>().Any(score => !double.IsFinite(score) || score < 0))
                throw new ArgumentException("Occupancy scores must be finite and non-negative.", nameof(scores));

            (double Score, int Rank, int File)[] rankedSquares =
                (from rank in Enumerable.Range(0, 8)
                 from file in Enumerable.Range(0, 8)
                 select (scores[rank, file], rank, file))
                .OrderByDescending(square => square.Item1)
                .ToArray();

            occupiedSquares = new bool[8, 8];
            if (occupiedSquareCount < rankedSquares.Length &&
                rankedSquares[occupiedSquareCount - 1].Score -
                rankedSquares[occupiedSquareCount].Score < minimumSeparation)
                return false;

            for (int index = 0; index < occupiedSquareCount; index++)
            {
                (_, int rank, int file) = rankedSquares[index];
                occupiedSquares[rank, file] = true;
            }

            return true;
        }
    }
}
