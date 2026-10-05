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

            int sqWidth = boardImage.Width / 8;
            int sqHeight = boardImage.Height / 8;

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
                        // Sample a 5x5 micro-grid at the center of the square to smooth out transient mouse cursor noise
                        int centerX = (file * sqWidth) + (sqWidth / 2);
                        int centerY = (rank * sqHeight) + (sqHeight / 2);

                        long totalLuminance = 0;
                        int samples = 0;

                        for (int offsetY = -2; offsetY <= 2; offsetY++)
                        {
                            for (int offsetX = -2; offsetX <= 2; offsetX++)
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

        /// <summary>
        /// Compares the current square brightness states with the previous baseline to track state modifications.
        /// </summary>
        /// <returns>A string representation of the move (e.g. "e2e4") if a state change correlates to a valid chess move; otherwise null.</returns>
        public string? ScanForStateChanges(Bitmap currentBoard, bool isWhiteView)
        {
            if (currentBoard == null) return null;

            int[,] currentBrightness = ExtractGridBrightness(currentBoard);

            if (_previousGridBrightness == null)
            {
                _previousGridBrightness = currentBrightness;
                return null;
            }

            List<string> changedSquares = new List<string>();

            // Threshold for distinguishing genuine piece movements from subtle pixel compression artifacts
            int changeThreshold = 18;

            for (int rank = 0; rank < 8; rank++)
            {
                for (int file = 0; file < 8; file++)
                {
                    int delta = Math.Abs(currentBrightness[rank, file] - _previousGridBrightness[rank, file]);

                    if (delta > changeThreshold)
                    {
                        string squareName = ConvertGridToAlgebraic(file, rank, isWhiteView);
                        changedSquares.Add(squareName);
                    }
                }
            }

            if (changedSquares.Count == 0)
            {
                _pendingGridBrightness = null;
                _pendingStableFrames = 0;
                return null;
            }

            if (GridsAreStable(currentBrightness, _pendingGridBrightness))
            {
                _pendingStableFrames++;
            }
            else
            {
                _pendingGridBrightness = currentBrightness;
                _pendingStableFrames = 1;
            }

            if (_pendingStableFrames < 2)
                return null;

            // A stable move changes its source and destination squares.
            if (changedSquares.Count == 2)
            {
                string candidate1 = $"{changedSquares[0]}{changedSquares[1]}";
                string candidate2 = $"{changedSquares[1]}{changedSquares[0]}";
                _previousGridBrightness = currentBrightness;
                _pendingGridBrightness = null;
                _pendingStableFrames = 0;
                return $"{candidate1}|{candidate2}";
            }

            // Persistent highlights or multi-square changes are not a normal move.
            if (_pendingStableFrames >= 4)
            {
                _previousGridBrightness = currentBrightness;
                _pendingGridBrightness = null;
                _pendingStableFrames = 0;
            }

            return null;
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

        private string ConvertGridToAlgebraic(int file, int rank, bool isWhiteView)
        {
            char fileChar = isWhiteView ? (char)('a' + file) : (char)('h' - file);
            int rankNum = isWhiteView ? (8 - rank) : (1 + rank);
            return $"{fileChar}{rankNum}";
        }

        public bool DetectPlayerSideFromImage(Bitmap boardImage)
        {
            if (boardImage == null) return true;
            int sqWidth = boardImage.Width / 8;
            int sqHeight = boardImage.Height / 8;
            int targetX = sqWidth / 2;
            int targetY = (7 * sqHeight) + (sqHeight / 2);

            if (targetX >= boardImage.Width || targetY >= boardImage.Height) return true;

            Color bottomLeftColor = boardImage.GetPixel(targetX, targetY);
            return ((bottomLeftColor.R + bottomLeftColor.G + bottomLeftColor.B) / 3) <= 150;
        }
    }
}
