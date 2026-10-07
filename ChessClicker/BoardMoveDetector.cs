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

        public static string? FindUniqueMoveMatchingOccupiedSquares(
            IEnumerable<string> candidates,
            ChessBoard position,
            IReadOnlySet<string> observedOccupiedSquares)
        {
            ArgumentNullException.ThrowIfNull(candidates);
            ArgumentNullException.ThrowIfNull(position);
            ArgumentNullException.ThrowIfNull(observedOccupiedSquares);

            string? matchingMove = null;
            string positionFen = position.GenerateFen();
            HashSet<string> distinctCandidates = new(StringComparer.OrdinalIgnoreCase);
            foreach (string candidate in candidates)
            {
                if (candidate == null ||
                    (candidate.Length != 4 && candidate.Length != 5) ||
                    !distinctCandidates.Add(candidate))
                    continue;

                ChessBoard nextPosition = new(positionFen);
                if (!nextPosition.MakeMove(candidate) && !nextPosition.MakeObservedMove(candidate))
                    continue;

                HashSet<string> predictedOccupiedSquares = GetOccupiedSquares(nextPosition);
                if (!predictedOccupiedSquares.SetEquals(observedOccupiedSquares))
                    continue;

                if (matchingMove != null)
                    return null;
                matchingMove = candidate;
            }

            return matchingMove;
        }

        public static string CreateCandidates(
            IReadOnlyList<(int Rank, int File)> changedSquares,
            bool isWhiteView,
            ChessBoard? position = null)
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

            string? castlingMove = GetCastlingMove(squares);
            if (castlingMove != null)
                return castlingMove;

            string[] orderedSquares = squares.ToArray();
            List<string> candidates = new(orderedSquares.Length * Math.Max(0, orderedSquares.Length - 1));
            for (int from = 0; from < orderedSquares.Length; from++)
            {
                if (position != null && position.GetPieceAt(orderedSquares[from]) == ' ')
                    continue;

                for (int to = 0; to < orderedSquares.Length; to++)
                    if (from != to)
                        candidates.Add(orderedSquares[from] + orderedSquares[to]);
            }

            return string.Join('|', candidates);
        }

        private static HashSet<string> GetOccupiedSquares(ChessBoard position)
        {
            HashSet<string> occupiedSquares = new(StringComparer.Ordinal);
            for (char file = 'a'; file <= 'h'; file++)
                for (char rank = '1'; rank <= '8'; rank++)
                {
                    string square = $"{file}{rank}";
                    if (position.GetPieceAt(square) != ' ')
                        occupiedSquares.Add(square);
                }

            return occupiedSquares;
        }

        private static string? GetCastlingMove(HashSet<string> changedSquares)
        {
            if (changedSquares.Count != 4)
                return null;

            if (changedSquares.SetEquals(["e1", "g1", "h1", "f1"]))
                return "e1g1";
            if (changedSquares.SetEquals(["e1", "c1", "a1", "d1"]))
                return "e1c1";
            if (changedSquares.SetEquals(["e8", "g8", "h8", "f8"]))
                return "e8g8";
            if (changedSquares.SetEquals(["e8", "c8", "a8", "d8"]))
                return "e8c8";
            return null;
        }
    }
}
