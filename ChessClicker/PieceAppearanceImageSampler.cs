using System;
using System.Collections.Generic;
using System.Drawing;

namespace ChessClicker
{
    public static class PieceAppearanceImageSampler
    {
        private const int SignatureSize = 8;

        public static bool TryExtractOccupiedSquares(
            Bitmap boardImage,
            ChessBoard startingPosition,
            bool isWhiteView,
            out IReadOnlySet<string> occupiedSquares)
        {
            ArgumentNullException.ThrowIfNull(boardImage);
            ArgumentNullException.ThrowIfNull(startingPosition);
            if (boardImage.Width != boardImage.Height)
                throw new ArgumentException("Board occupancy scanning requires a square board capture.", nameof(boardImage));
            if (boardImage.Width < 8)
                throw new ArgumentException("The board capture must be at least 8-by-8 pixels.", nameof(boardImage));

            double[,] scores = ExtractOccupancyScores(boardImage);
            int pieceCount = 0;
            for (char file = 'a'; file <= 'h'; file++)
                for (char rank = '1'; rank <= '8'; rank++)
                    if (startingPosition.GetPieceAt($"{file}{rank}") != ' ')
                        pieceCount++;

            if (pieceCount == 0)
            {
                occupiedSquares = new HashSet<string>(StringComparer.Ordinal);
                return false;
            }

            int minimumPieceCount = Math.Max(1, pieceCount - 1);
            if (!PieceOccupancyAnalyzer.TrySelectOccupiedSquares(
                    scores, minimumPieceCount, pieceCount,
                    minimumSeparation: 8, out bool[,] screenOccupancy))
            {
                occupiedSquares = new HashSet<string>(StringComparer.Ordinal);
                return false;
            }

            HashSet<string> detectedSquares = new(StringComparer.Ordinal);
            for (int screenRank = 0; screenRank < 8; screenRank++)
            {
                for (int screenFile = 0; screenFile < 8; screenFile++)
                {
                    if (!screenOccupancy[screenRank, screenFile])
                        continue;

                    int left = screenFile * boardImage.Width / 8;
                    int right = (screenFile + 1) * boardImage.Width / 8;
                    int top = screenRank * boardImage.Height / 8;
                    int bottom = (screenRank + 1) * boardImage.Height / 8;
                    Point center = new(
                        left + (right - left) / 2,
                        top + (bottom - top) / 2);
                    detectedSquares.Add(BoardMouseCoordinates.ScreenPointToSquare(
                        center, new Rectangle(0, 0, boardImage.Width, boardImage.Height), isWhiteView));
                }
            }

            occupiedSquares = detectedSquares;
            return true;
        }

        public static IReadOnlyList<PieceAppearanceSample> ExtractSamples(
            Bitmap boardImage,
            string fen,
            bool isWhiteView)
        {
            ArgumentNullException.ThrowIfNull(boardImage);
            var board = new ChessBoard(fen);
            List<PieceAppearanceSample> samples = new();
            IReadOnlyDictionary<string, double[]> signatures =
                ExtractSquareSignatures(boardImage, isWhiteView);
            foreach ((string square, double[] signature) in signatures)
            {
                char piece = board.GetPieceAt(square);
                if (piece != ' ')
                    samples.Add(new PieceAppearanceSample(piece, signature));
            }

            return samples;
        }

        public static IReadOnlyDictionary<string, double[]> ExtractSquareSignatures(
            Bitmap boardImage,
            bool isWhiteView)
        {
            ArgumentNullException.ThrowIfNull(boardImage);
            if (boardImage.Width != boardImage.Height)
                throw new ArgumentException("Piece scanning requires a square board capture.", nameof(boardImage));
            if (boardImage.Width < 8)
                throw new ArgumentException("The board capture must be at least 8-by-8 pixels.", nameof(boardImage));

            Dictionary<string, double[]> signatures = new(StringComparer.Ordinal);
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
                    Rectangle squareBounds = Rectangle.FromLTRB(left, top, right, bottom);
                    signatures.Add(square, ExtractSignature(boardImage, squareBounds));
                }
            }

            return signatures;
        }

        private static double[,] ExtractOccupancyScores(Bitmap image)
        {
            double[,] scores = new double[8, 8];
            for (int rank = 0; rank < 8; rank++)
            {
                for (int file = 0; file < 8; file++)
                {
                    int left = file * image.Width / 8;
                    int right = (file + 1) * image.Width / 8;
                    int top = rank * image.Height / 8;
                    int bottom = (rank + 1) * image.Height / 8;
                    int marginX = Math.Max(1, (right - left) / 8);
                    int marginY = Math.Max(1, (bottom - top) / 8);
                    double background = (
                        Luminance(image.GetPixel(left + marginX, top + marginY)) +
                        Luminance(image.GetPixel(right - marginX - 1, top + marginY)) +
                        Luminance(image.GetPixel(left + marginX, bottom - marginY - 1)) +
                        Luminance(image.GetPixel(right - marginX - 1, bottom - marginY - 1))) / 4;

                    double totalDifference = 0;
                    for (int row = 0; row < SignatureSize; row++)
                    {
                        for (int column = 0; column < SignatureSize; column++)
                        {
                            int x = left + (column * (right - left) + (right - left) / 2) / SignatureSize;
                            int y = top + (row * (bottom - top) + (bottom - top) / 2) / SignatureSize;
                            totalDifference += Math.Abs(Luminance(image.GetPixel(x, y)) - background);
                        }
                    }

                    scores[rank, file] = totalDifference / (SignatureSize * SignatureSize);
                }
            }

            return scores;
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
