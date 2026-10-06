using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace ChessClicker
{
    public class ImageScaner
    {
        // Stores the previous frame's matrix grid brightness values to compare against
        private int[,]? _previousGridBrightness;
        private int[,]? _pendingGridBrightness;
        private int _pendingStableFrames;
        private DateTimeOffset? _pendingStableSince;

        /// <summary>
        /// Screens only a specific calibrated part of the desktop monitor directly.
        /// </summary>
        public Bitmap CaptureBoardRegion(Rectangle boardBounds)
        {
            if (boardBounds.Width <= 0 || boardBounds.Height <= 0)
                throw new ArgumentException("Invalid board coordinates. Please calibrate first.");

            Bitmap croppedBmp = new Bitmap(boardBounds.Width, boardBounds.Height, PixelFormat.Format32bppArgb);
            using (Graphics g = Graphics.FromImage(croppedBmp))
            {
                g.CopyFromScreen(boardBounds.X, boardBounds.Y, 0, 0, boardBounds.Size, CopyPixelOperation.SourceCopy);
            }
            return croppedBmp;
        }

        /// <summary>
        /// Calculates the central average brightness for each of the 64 squares.
        /// </summary>
        public int[,] ExtractGridBrightness(Bitmap boardImage)
        {
            int[,] brightnessMatrix = new int[8, 8];
            if (boardImage == null) return brightnessMatrix;

            if (boardImage.Width < 8 || boardImage.Height < 8)
                throw new ArgumentException("The captured board must be at least 8-by-8 pixels.", nameof(boardImage));

            BitmapData bitmapData = boardImage.LockBits(
                new Rectangle(0, 0, boardImage.Width, boardImage.Height),
                ImageLockMode.ReadOnly, PixelFormat.Format32bppRgb
            );

            try
            {
                int bytesCount = Math.Abs(bitmapData.Stride) * boardImage.Height;
                byte[] rgbValues = new byte[bytesCount];
                Marshal.Copy(bitmapData.Scan0, rgbValues, 0, bytesCount);

                int bytesPerPixel = 4;

                for (int rank = 0; rank < 8; rank++)
                {
                    for (int file = 0; file < 8; file++)
                    {
                        int left = file * boardImage.Width / 8;
                        int right = (file + 1) * boardImage.Width / 8;
                        int top = rank * boardImage.Height / 8;
                        int bottom = (rank + 1) * boardImage.Height / 8;
                        int centerX = (left + right) / 2;
                        int centerY = (top + bottom) / 2;
                        int radiusX = Math.Min(2, Math.Max(0, (right - left - 1) / 2));
                        int radiusY = Math.Min(2, Math.Max(0, (bottom - top - 1) / 2));

                        long totalLuminance = 0;
                        int samples = 0;

                        for (int offsetY = -radiusY; offsetY <= radiusY; offsetY++)
                        {
                            for (int offsetX = -radiusX; offsetX <= radiusX; offsetX++)
                            {
                                int sampleX = centerX + offsetX;
                                int sampleY = centerY + offsetY;
                                int byteIndex = (sampleY * bitmapData.Stride) + (sampleX * bytesPerPixel);

                                if (byteIndex + 2 < rgbValues.Length && byteIndex >= 0)
                                {
                                    int b = rgbValues[byteIndex];
                                    int g = rgbValues[byteIndex + 1];
                                    int r = rgbValues[byteIndex + 2];
                                    totalLuminance += (r + g + b) / 3;
                                    samples++;
                                }
                            }
                        }

                        brightnessMatrix[rank, file] = samples > 0 ? (int)(totalLuminance / samples) : 0;
                    }
                }
            }
            finally
            {
                boardImage.UnlockBits(bitmapData);
            }

            return brightnessMatrix;
        }

        public int[,] ExtractGrayscale(Bitmap image)
        {
            ArgumentNullException.ThrowIfNull(image);
            var grayscale = new int[image.Height, image.Width];
            BitmapData bitmapData = image.LockBits(
                new Rectangle(0, 0, image.Width, image.Height),
                ImageLockMode.ReadOnly, PixelFormat.Format32bppRgb);
            try
            {
                int rowBytes = image.Width * 4;
                byte[] row = new byte[rowBytes];
                for (int y = 0; y < image.Height; y++)
                {
                    IntPtr rowStart = IntPtr.Add(bitmapData.Scan0, y * bitmapData.Stride);
                    Marshal.Copy(rowStart, row, 0, rowBytes);
                    for (int x = 0; x < image.Width; x++)
                    {
                        int offset = x * 4;
                        int blue = row[offset];
                        int green = row[offset + 1];
                        int red = row[offset + 2];
                        grayscale[y, x] = (red * 299 + green * 587 + blue * 114) / 1000;
                    }
                }
            }
            finally
            {
                image.UnlockBits(bitmapData);
            }

            return grayscale;
        }

        /// <summary>
        /// Compares the current square brightness states with the previous baseline to track state modifications.
        /// </summary>
        /// <returns>A string representation of the move (e.g. "e2e4") if a state change correlates to a valid chess move; otherwise null.</returns>
        public string? ScanForStateChanges(
            Bitmap currentBoard,
            bool isWhiteView,
            int brightnessThreshold = 18,
            int minimumStableDurationMilliseconds = 0)
        {
            if (currentBoard == null) return null;
            if (minimumStableDurationMilliseconds < 0)
                throw new ArgumentOutOfRangeException(nameof(minimumStableDurationMilliseconds));

            int[,] currentBrightness = ExtractGridBrightness(currentBoard);

            if (_previousGridBrightness == null)
            {
                _previousGridBrightness = currentBrightness;
                return null;
            }

            IReadOnlyList<(int Rank, int File)> changedSquares =
                BoardBrightnessAnalyzer.FindChangedSquares(_previousGridBrightness, currentBrightness, brightnessThreshold);

            if (changedSquares.Count == 0)
            {
                if (_pendingGridBrightness == null)
                {
                    _previousGridBrightness = currentBrightness;
                    return null;
                }

                UpdatePendingStability(currentBrightness);
                if (PendingChangeHasSettled(minimumStableDurationMilliseconds))
                {
                    _previousGridBrightness = currentBrightness;
                    ResetPendingChange();
                }
                return null;
            }

            UpdatePendingStability(currentBrightness);
            if (!PendingChangeHasSettled(minimumStableDurationMilliseconds))
                return null;

            string candidates = BoardMoveDetector.CreateCandidates(changedSquares, isWhiteView);
            _previousGridBrightness = currentBrightness;
            ResetPendingChange();
            if (!string.IsNullOrEmpty(candidates))
                return candidates;

            return null;
        }

        public void ResetStateTracking(Bitmap currentBoard)
        {
            ArgumentNullException.ThrowIfNull(currentBoard);
            _previousGridBrightness = ExtractGridBrightness(currentBoard);
            ResetPendingChange();
        }

        private void UpdatePendingStability(int[,] currentBrightness)
        {
            if (GridsAreStable(currentBrightness, _pendingGridBrightness))
            {
                _pendingStableFrames++;
            }
            else
            {
                _pendingGridBrightness = currentBrightness;
                _pendingStableFrames = 1;
                _pendingStableSince = DateTimeOffset.UtcNow;
            }
        }

        private bool PendingChangeHasSettled(int minimumStableDurationMilliseconds) =>
            _pendingStableFrames >= 2 &&
            _pendingStableSince != null &&
            DateTimeOffset.UtcNow - _pendingStableSince.Value >=
                TimeSpan.FromMilliseconds(minimumStableDurationMilliseconds);

        private void ResetPendingChange()
        {
            _pendingGridBrightness = null;
            _pendingStableFrames = 0;
            _pendingStableSince = null;
        }

        private static bool GridsAreStable(int[,] current, int[,]? previous)
        {
            if (previous == null) return false;

            for (int rank = 0; rank < 8; rank++)
            {
                for (int file = 0; file < 8; file++)
                {
                    if (Math.Abs(current[rank, file] - previous[rank, file]) > 8)
                        return false;
                }
            }

            return true;
        }

        public bool DetectPlayerSideFromImage(Bitmap boardImage)
        {
            ArgumentNullException.ThrowIfNull(boardImage);
            if (boardImage.Width < 8 || boardImage.Height < 8)
                throw new ArgumentException("The captured board must be at least 8-by-8 pixels.", nameof(boardImage));

            double[,] squareContrast = new double[8, 8];
            for (int rank = 0; rank < 8; rank++)
            {
                if (rank > 1 && rank < 6) continue;
                for (int file = 0; file < 8; file++)
                {
                    Rectangle square = new(
                        file * boardImage.Width / 8,
                        rank * boardImage.Height / 8,
                        (file + 1) * boardImage.Width / 8 - file * boardImage.Width / 8,
                        (rank + 1) * boardImage.Height / 8 - rank * boardImage.Height / 8);

                    double centerLuminance = AverageLuminance(boardImage, GetRelativeRegion(square, 0.25, 0.25, 0.75, 0.75));
                    double backgroundLuminance =
                        (AverageLuminance(boardImage, GetRelativeRegion(square, 0.12, 0.12, 0.28, 0.28)) +
                         AverageLuminance(boardImage, GetRelativeRegion(square, 0.72, 0.12, 0.88, 0.28)) +
                         AverageLuminance(boardImage, GetRelativeRegion(square, 0.12, 0.72, 0.28, 0.88)) +
                         AverageLuminance(boardImage, GetRelativeRegion(square, 0.72, 0.72, 0.88, 0.88))) / 4;

                    squareContrast[rank, file] = centerLuminance - backgroundLuminance;
                }
            }

            if (BoardOrientationDetector.TryDetectWhiteView(squareContrast, out bool isWhiteView))
                return isWhiteView;

            throw new InvalidOperationException(
                "Could not determine board orientation from the pieces. Calibrate a full board with visible pieces on the first or last two ranks.");
        }

        private static Rectangle GetRelativeRegion(Rectangle square, double left, double top, double right, double bottom)
        {
            int x = square.X + (int)(square.Width * left);
            int y = square.Y + (int)(square.Height * top);
            int width = Math.Max(1, (int)(square.Width * (right - left)));
            int height = Math.Max(1, (int)(square.Height * (bottom - top)));
            return new Rectangle(x, y, width, height);
        }

        private static double AverageLuminance(Bitmap image, Rectangle region)
        {
            double total = 0;
            int samples = 0;
            int stepX = Math.Max(1, region.Width / 12);
            int stepY = Math.Max(1, region.Height / 12);

            for (int y = region.Top; y < region.Bottom && y < image.Height; y += stepY)
            {
                for (int x = region.Left; x < region.Right && x < image.Width; x += stepX)
                {
                    Color color = image.GetPixel(x, y);
                    total += (color.R + color.G + color.B) / 3.0;
                    samples++;
                }
            }

            return samples == 0 ? 0 : total / samples;
        }
    }
}
