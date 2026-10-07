using System;

namespace ChessClicker
{
    public static class BoardOrientationDetector
    {
        private const double MinimumContrastDifference = 0.35;

        public static bool TryDetectWhiteView(double[,] squareContrast, out bool isWhiteView)
        {
            ArgumentNullException.ThrowIfNull(squareContrast);
            if (squareContrast.GetLength(0) != 8 || squareContrast.GetLength(1) != 8)
                throw new ArgumentException("Square contrast must contain an 8-by-8 board.", nameof(squareContrast));

            double topSideScore = GetNormalizedSideContrast(squareContrast, 0);
            double bottomSideScore = GetNormalizedSideContrast(squareContrast, 6);
            double contrastDifference = bottomSideScore - topSideScore;
            if (Math.Abs(contrastDifference) < MinimumContrastDifference)
            {
                isWhiteView = false;
                return false;
            }

            isWhiteView = contrastDifference > 0;
            return true;
        }

        private static double GetNormalizedSideContrast(double[,] squareContrast, int firstRank)
        {
            double totalContrast = 0;
            double totalMagnitude = 0;
            for (int rank = firstRank; rank < firstRank + 2; rank++)
            {
                for (int file = 0; file < 8; file++)
                {
                    double contrast = squareContrast[rank, file];
                    totalContrast += contrast;
                    totalMagnitude += Math.Abs(contrast);
                }
            }

            return totalMagnitude < 1 ? 0 : totalContrast / totalMagnitude;
        }
    }
}
