using System;

namespace ChessClicker
{
    public static class BoardOrientationDetector
    {
        private const double MinimumContrastDifference = 8;

        public static bool TryDetectWhiteView(double[,] squareContrast, out bool isWhiteView)
        {
            ArgumentNullException.ThrowIfNull(squareContrast);
            if (squareContrast.GetLength(0) != 8 || squareContrast.GetLength(1) != 8)
                throw new ArgumentException("Square contrast must contain an 8-by-8 board.", nameof(squareContrast));

            double topSideScore = 0;
            double bottomSideScore = 0;
            for (int file = 0; file < 8; file++)
            {
                topSideScore += squareContrast[0, file] + squareContrast[1, file];
                bottomSideScore += squareContrast[6, file] + squareContrast[7, file];
            }

            double contrastDifference = (bottomSideScore - topSideScore) / 16;
            if (Math.Abs(contrastDifference) < MinimumContrastDifference)
            {
                isWhiteView = false;
                return false;
            }

            isWhiteView = contrastDifference > 0;
            return true;
        }
    }
}
