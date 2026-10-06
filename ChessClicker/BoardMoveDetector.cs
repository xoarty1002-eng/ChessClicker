using System;
using System.Collections.Generic;
using System.Linq;

namespace ChessClicker
{
    public static class BoardMoveDetector
    {
        public static string? FindUniqueLegalMove(IEnumerable<string> candidates, Predicate<string> isLegalMove)
        {
            ArgumentNullException.ThrowIfNull(candidates);
            ArgumentNullException.ThrowIfNull(isLegalMove);

            string[] legalMoves = candidates
                .Where(candidate => candidate != null && candidate.Length == 4)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Where(candidate => isLegalMove(candidate))
                .Take(2)
                .ToArray();

            return legalMoves.Length == 1 ? legalMoves[0] : null;
        }

        public static string CreateCandidates(IReadOnlyList<(int Rank, int File)> changedSquares, bool isWhiteView)
        {
            ArgumentNullException.ThrowIfNull(changedSquares);
            HashSet<string> squares = new(StringComparer.Ordinal);
            foreach ((int rank, int file) in changedSquares)
            {
                if (rank < 0 || rank > 7 || file < 0 || file > 7)
                    throw new ArgumentOutOfRangeException(nameof(changedSquares), "Changed square coordinates must be within the board.");

                char fileChar = isWhiteView ? (char)('a' + file) : (char)('h' - file);
                int rankNumber = isWhiteView ? 8 - rank : 1 + rank;
                squares.Add($"{fileChar}{rankNumber}");
            }

            string[] orderedSquares = squares.ToArray();
            List<string> candidates = new(orderedSquares.Length * Math.Max(0, orderedSquares.Length - 1));
            for (int from = 0; from < orderedSquares.Length; from++)
                for (int to = 0; to < orderedSquares.Length; to++)
                    if (from != to)
                        candidates.Add(orderedSquares[from] + orderedSquares[to]);

            return string.Join('|', candidates);
        }
    }
}
