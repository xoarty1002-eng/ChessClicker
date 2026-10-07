using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ChessClicker
{
    public readonly record struct PieceSignatureClassification(
        char Piece,
        double BestDistance,
        double SecondDistance);

    public static class PieceSignatureClassifier
    {
        public static PieceSignatureClassification Classify(
            IReadOnlyDictionary<char, double[][]> templates,
            IReadOnlyList<double> signature)
        {
            ArgumentNullException.ThrowIfNull(templates);
            ArgumentNullException.ThrowIfNull(signature);
            if (signature.Count != 64)
                throw new ArgumentException("A piece signature must contain 64 values.", nameof(signature));

            char bestPiece = ' ';
            double bestDistance = double.PositiveInfinity;
            double secondDistance = double.PositiveInfinity;
            bool hasBest = false;

            foreach ((char piece, double[][] pieceTemplates) in templates)
            {
                if (pieceTemplates.Length == 0)
                    throw new ArgumentException($"Piece '{piece}' has no appearance templates.", nameof(templates));

                double distance = pieceTemplates.Min(template =>
                    SignatureDistance(signature, template));
                if (distance >= bestDistance)
                {
                    secondDistance = Math.Min(secondDistance, distance);
                    continue;
                }

                secondDistance = bestDistance;
                bestDistance = distance;
                bestPiece = piece;
                hasBest = true;
            }

            if (!hasBest)
                throw new ArgumentException("At least one piece appearance template is required.", nameof(templates));

            return new PieceSignatureClassification(bestPiece, bestDistance, secondDistance);
        }

        public static string CreatePiecePlacement(char[,] pieces)
        {
            ArgumentNullException.ThrowIfNull(pieces);
            if (pieces.GetLength(0) != 8 || pieces.GetLength(1) != 8)
                throw new ArgumentException("Piece positions must contain an 8-by-8 board.", nameof(pieces));

            List<string> ranks = new(8);
            for (int rank = 0; rank < 8; rank++)
            {
                int emptyCount = 0;
                StringBuilder currentRank = new();
                for (int file = 0; file < 8; file++)
                {
                    char piece = pieces[rank, file];
                    if (piece == ' ')
                    {
                        emptyCount++;
                        continue;
                    }

                    if ("KQRBNPkqrbnp".IndexOf(piece, StringComparison.Ordinal) < 0)
                        throw new ArgumentException($"Unsupported piece '{piece}'.", nameof(pieces));
                    if (emptyCount > 0)
                    {
                        currentRank.Append(emptyCount);
                        emptyCount = 0;
                    }
                    currentRank.Append(piece);
                }

                if (emptyCount > 0)
                    currentRank.Append(emptyCount);
                ranks.Add(currentRank.ToString());
            }

            return string.Join('/', ranks);
        }

        private static double SignatureDistance(
            IReadOnlyList<double> first,
            IReadOnlyList<double> second)
        {
            if (second.Count != 64)
                throw new ArgumentException("Each piece appearance template must contain 64 values.", nameof(second));

            double squaredDistance = 0;
            for (int index = 0; index < first.Count; index++)
            {
                double difference = first[index] - second[index];
                squaredDistance += difference * difference;
            }

            return Math.Sqrt(squaredDistance / first.Count);
        }
    }
}
