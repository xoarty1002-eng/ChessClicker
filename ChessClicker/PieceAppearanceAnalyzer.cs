using System;
using System.Collections.Generic;
using System.Linq;

namespace ChessClicker
{
    public sealed record PieceAppearanceSample(char Piece, IReadOnlyList<double> Signature);

    public sealed record PieceAppearanceReport(
        bool IsSeparable,
        double MinimumSeparationPercent,
        char FirstClosestPiece,
        char SecondClosestPiece,
        IReadOnlyList<char> MissingPieces,
        IReadOnlyDictionary<char, int> SampleCounts);

    public static class PieceAppearanceAnalyzer
    {
        private const int SignatureSize = 8;
        private const string PieceTypes = "KQRBNP";

        public static PieceAppearanceReport Analyze(
            IEnumerable<PieceAppearanceSample> samples,
            double minimumSeparationPercent)
        {
            ArgumentNullException.ThrowIfNull(samples);
            if (minimumSeparationPercent is < 0 or > 100)
                throw new ArgumentOutOfRangeException(nameof(minimumSeparationPercent));

            Dictionary<char, double[]> prototypes = new();
            Dictionary<char, int> counts = new();
            foreach (PieceAppearanceSample sample in samples)
            {
                ArgumentNullException.ThrowIfNull(sample);
                char label = sample.Piece;
                if ("KQRBNPkqrbnp".IndexOf(label, StringComparison.Ordinal) < 0)
                    throw new ArgumentException($"Unsupported piece label '{label}'.", nameof(samples));
                if (sample.Signature.Count != SignatureSize * SignatureSize ||
                    sample.Signature.Any(value => !double.IsFinite(value) || value is < -1 or > 1))
                    throw new ArgumentException("Each piece signature must contain 64 normalized brightness values.", nameof(samples));

                if (!prototypes.TryGetValue(label, out double[]? prototype))
                {
                    prototype = new double[SignatureSize * SignatureSize];
                    prototypes.Add(label, prototype);
                    counts.Add(label, 0);
                }

                for (int index = 0; index < prototype.Length; index++)
                    prototype[index] += sample.Signature[index];
                counts[label]++;
            }

            foreach ((char label, double[] prototype) in prototypes)
                for (int index = 0; index < prototype.Length; index++)
                    prototype[index] /= counts[label];

            char closestFirst = '\0';
            char closestSecond = '\0';
            double minimumSeparation = double.PositiveInfinity;
            char[] labels = prototypes.Keys.Order().ToArray();
            for (int first = 0; first < labels.Length; first++)
            {
                for (int second = first + 1; second < labels.Length; second++)
                {
                    double distance = SignatureDistancePercent(
                        prototypes[labels[first]], prototypes[labels[second]]);
                    if (distance >= minimumSeparation)
                        continue;
                    minimumSeparation = distance;
                    closestFirst = labels[first];
                    closestSecond = labels[second];
                }
            }

            List<char> missingPieces = new();
            foreach (char color in new[] { 'w', 'b' })
                foreach (char type in PieceTypes)
                {
                    char label = color == 'w' ? type : char.ToLowerInvariant(type);
                    if (!prototypes.ContainsKey(label))
                        missingPieces.Add(label);
                }

            double reportedSeparation = double.IsPositiveInfinity(minimumSeparation)
                ? 0
                : minimumSeparation;
            return new PieceAppearanceReport(
                missingPieces.Count == 0 && reportedSeparation >= minimumSeparationPercent,
                reportedSeparation,
                closestFirst,
                closestSecond,
                missingPieces,
                counts);
        }

        private static double SignatureDistancePercent(double[] first, double[] second)
        {
            double squaredDistance = 0;
            for (int index = 0; index < first.Length; index++)
            {
                double difference = first[index] - second[index];
                squaredDistance += difference * difference;
            }

            return Math.Sqrt(squaredDistance / first.Length) * 50;
        }
    }
}
