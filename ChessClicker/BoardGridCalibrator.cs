using System;
using System.Drawing;

namespace ChessClicker
{
    public static class BoardGridCalibrator
    {
        private const double MinimumGridEdgeContrast = 8;
        private static readonly double[] CellSampleFractions = [0.25, 0.75];

        public static Rectangle? FindBestBounds(int[,] grayscale, Rectangle initialBounds, int adjustmentPixels)
        {
            ArgumentNullException.ThrowIfNull(grayscale);
            if (grayscale.GetLength(0) < 8 || grayscale.GetLength(1) < 8)
                throw new ArgumentException("The calibration image must be at least 8-by-8 pixels.", nameof(grayscale));
            if (initialBounds.Width < 40 || initialBounds.Height < 40 ||
                initialBounds.Left < 0 || initialBounds.Top < 0 ||
                initialBounds.Right > grayscale.GetLength(1) ||
                initialBounds.Bottom > grayscale.GetLength(0))
                throw new ArgumentException("The initial board bounds must be at least 40-by-40 pixels and inside the image.", nameof(initialBounds));
            if (adjustmentPixels < 0)
                throw new ArgumentOutOfRangeException(nameof(adjustmentPixels));

            Rectangle best = initialBounds;
            double bestScore = ScoreGridEdges(grayscale, best);
            int adjustment = Math.Min(adjustmentPixels, Math.Min(initialBounds.Width, initialBounds.Height) / 5);
            const int step = 1;

            for (int pass = 0; pass < 2; pass++)
            {
                (best, bestScore) = FindBestHorizontalBounds(grayscale, best, adjustment, step, bestScore);
                (best, bestScore) = FindBestVerticalBounds(grayscale, best, adjustment, step, bestScore);
            }

            return bestScore >= MinimumGridEdgeContrast ? best : null;
        }

        private static (Rectangle Bounds, double Score) FindBestHorizontalBounds(
            int[,] grayscale, Rectangle current, int adjustment, int step, double bestScore)
        {
            Rectangle best = current;
            for (int left = Math.Max(0, current.Left - adjustment);
                 left <= Math.Min(grayscale.GetLength(1) - 40, current.Left + adjustment);
                 left += step)
            {
                for (int right = Math.Max(left + 40, current.Right - adjustment);
                     right <= Math.Min(grayscale.GetLength(1), current.Right + adjustment);
                     right += step)
                {
                    Rectangle candidate = Rectangle.FromLTRB(left, current.Top, right, current.Bottom);
                    double score = ScoreGridEdges(grayscale, candidate);
                    if (score > bestScore)
                    {
                        best = candidate;
                        bestScore = score;
                    }
                }
            }

            return (best, bestScore);
        }

        private static (Rectangle Bounds, double Score) FindBestVerticalBounds(
            int[,] grayscale, Rectangle current, int adjustment, int step, double bestScore)
        {
            Rectangle best = current;
            for (int top = Math.Max(0, current.Top - adjustment);
                 top <= Math.Min(grayscale.GetLength(0) - 40, current.Top + adjustment);
                 top += step)
            {
                for (int bottom = Math.Max(top + 40, current.Bottom - adjustment);
                     bottom <= Math.Min(grayscale.GetLength(0), current.Bottom + adjustment);
                     bottom += step)
                {
                    Rectangle candidate = Rectangle.FromLTRB(current.Left, top, current.Right, bottom);
                    double score = ScoreGridEdges(grayscale, candidate);
                    if (score > bestScore)
                    {
                        best = candidate;
                        bestScore = score;
                    }
                }
            }

            return (best, bestScore);
        }

        private static double ScoreGridEdges(int[,] grayscale, Rectangle bounds)
        {
            double totalContrast = 0;
            int samples = 0;

            for (int line = 1; line < 8; line++)
            {
                int x = bounds.Left + line * bounds.Width / 8;
                int y = bounds.Top + line * bounds.Height / 8;
                for (int cell = 0; cell < 8; cell++)
                {
                    int cellTop = bounds.Top + cell * bounds.Height / 8;
                    int cellBottom = bounds.Top + (cell + 1) * bounds.Height / 8;
                    int cellLeft = bounds.Left + cell * bounds.Width / 8;
                    int cellRight = bounds.Left + (cell + 1) * bounds.Width / 8;

                    foreach (double fraction in CellSampleFractions)
                    {
                        int sampleY = cellTop + (int)((cellBottom - cellTop) * fraction);
                        int sampleX = cellLeft + (int)((cellRight - cellLeft) * fraction);
                        totalContrast += Math.Abs(grayscale[sampleY, x] - grayscale[sampleY, x - 1]);
                        totalContrast += Math.Abs(grayscale[y, sampleX] - grayscale[y - 1, sampleX]);
                        samples += 2;
                    }
                }
            }

            return samples == 0 ? 0 : totalContrast / samples;
        }
    }
}
