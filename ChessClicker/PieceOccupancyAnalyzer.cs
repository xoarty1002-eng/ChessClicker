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
            out bool[,] occupiedSquares) =>
            TrySelectOccupiedSquares(
                scores, occupiedSquareCount, occupiedSquareCount,
                minimumSeparation, out occupiedSquares);

        public static bool TrySelectOccupiedSquares(
            double[,] scores,
            int minimumOccupiedSquareCount,
            int maximumOccupiedSquareCount,
            double minimumSeparation,
            out bool[,] occupiedSquares)
        {
            ArgumentNullException.ThrowIfNull(scores);
            if (scores.GetLength(0) != 8 || scores.GetLength(1) != 8)
                throw new ArgumentException("Occupancy scores must contain an 8-by-8 grid.", nameof(scores));
            if (minimumOccupiedSquareCount is < 1 or > 64)
                throw new ArgumentOutOfRangeException(nameof(minimumOccupiedSquareCount));
            if (maximumOccupiedSquareCount < minimumOccupiedSquareCount ||
                maximumOccupiedSquareCount > 64)
                throw new ArgumentOutOfRangeException(nameof(maximumOccupiedSquareCount));
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
            if (minimumOccupiedSquareCount == 64 && maximumOccupiedSquareCount == 64)
            {
                for (int rank = 0; rank < 8; rank++)
                    for (int file = 0; file < 8; file++)
                        occupiedSquares[rank, file] = true;
                return true;
            }

            int selectedCount = 0;
            double selectedSeparation = double.NegativeInfinity;
            bool tiedBestSeparation = false;
            int largestCount = Math.Min(maximumOccupiedSquareCount, rankedSquares.Length - 1);
            for (int count = minimumOccupiedSquareCount; count <= largestCount; count++)
            {
                double separation =
                    rankedSquares[count - 1].Score - rankedSquares[count].Score;
                if (separation < minimumSeparation)
                    continue;

                if (separation > selectedSeparation)
                {
                    selectedCount = count;
                    selectedSeparation = separation;
                    tiedBestSeparation = false;
                }
                else if (separation == selectedSeparation)
                {
                    tiedBestSeparation = true;
                }
            }

            if (selectedCount == 0 || tiedBestSeparation)
                return false;

            for (int index = 0; index < selectedCount; index++)
            {
                (_, int rank, int file) = rankedSquares[index];
                occupiedSquares[rank, file] = true;
            }

            return true;
        }
    }
}
