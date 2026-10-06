using System;
using System.Collections.Generic;
using System.Drawing;

namespace ChessClicker
{
    public static class PieceAppearanceImageSampler
    {
        private const int SignatureSize = 8;

        public static IReadOnlyList<PieceAppearanceSample> ExtractSamples(
            Bitmap boardImage,
            string fen,
            bool isWhiteView)
        {
            ArgumentNullException.ThrowIfNull(boardImage);
            var board = new ChessBoard(fen);
            List<PieceAppearanceSample> samples = new();
            for (int screenRank = 0; screenRank < 8; screenRank++)
            {
                for (int screenFile = 0; screenFile < 8; screenFile++)
                {
                    int left = screenFile * boardImage.Width / 8;
                    int right = (screenFile + 1) * boardImage.Width / 8;
                    int top = screenRank * boardImage.Height / 8;
                    int bottom = (screenRank + 1) * boardImage.Height / 8;
                    Point center = new(left + (right - left) / 2, top + (bottom - top) / 2);
                    string square = BoardMouseCoordinates.ScreenPointToSquare(
                        center, new Rectangle(0, 0, boardImage.Width, boardImage.Height), isWhiteView);
                    char piece = board.GetPieceAt(square);
                    if (piece == ' ')
                        continue;

                    Rectangle squareBounds = Rectangle.FromLTRB(left, top, right, bottom);
                    samples.Add(new PieceAppearanceSample(piece, ExtractSignature(boardImage, squareBounds)));
                }
            }

            return samples;
        }

        private static double[] ExtractSignature(Bitmap image, Rectangle square)
        {
            int marginX = Math.Max(1, square.Width / 8);
            int marginY = Math.Max(1, square.Height / 8);
            double background = (
                Luminance(image.GetPixel(square.Left + marginX, square.Top + marginY)) +
                Luminance(image.GetPixel(square.Right - marginX - 1, square.Top + marginY)) +
                Luminance(image.GetPixel(square.Left + marginX, square.Bottom - marginY - 1)) +
                Luminance(image.GetPixel(square.Right - marginX - 1, square.Bottom - marginY - 1))) / 4;

            double[] signature = new double[SignatureSize * SignatureSize];
            for (int row = 0; row < SignatureSize; row++)
            {
                for (int column = 0; column < SignatureSize; column++)
                {
                    int x = square.Left + (column * square.Width + square.Width / 2) / SignatureSize;
                    int y = square.Top + (row * square.Height + square.Height / 2) / SignatureSize;
                    signature[row * SignatureSize + column] =
                        Math.Clamp((Luminance(image.GetPixel(x, y)) - background) / 128.0, -1, 1);
                }
            }

            return signature;
        }

        private static double Luminance(Color color) =>
            (color.R * 299 + color.G * 587 + color.B * 114) / 1000.0;
    }
}
