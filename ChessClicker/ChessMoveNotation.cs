using System;
using System.Linq;

namespace ChessClicker
{
    public static class ChessMoveNotation
    {
        public static string Normalize(string? input)
        {
            if (string.IsNullOrWhiteSpace(input))
                throw new ArgumentException(
                    "Enter a move such as e2e4 or a8-a6.",
                    nameof(input));

            string move = string.Concat(input.Where(character => !char.IsWhiteSpace(character)))
                .ToLowerInvariant();
            int destinationIndex = move.Length > 2 && move[2] is '-' or 'x' ? 3 : 2;
            if (move.Length < destinationIndex + 2 ||
                !IsSquare(move, 0) ||
                !IsSquare(move, destinationIndex))
            {
                throw InvalidMove(input);
            }

            int suffixIndex = destinationIndex + 2;
            if (suffixIndex < move.Length && move[suffixIndex] == '=')
            {
                suffixIndex++;
                if (suffixIndex == move.Length)
                    throw InvalidMove(input);
            }

            string suffix = move[suffixIndex..];
            if (suffix.Length > 1 || (suffix.Length == 1 && suffix[0] is not ('q' or 'r' or 'b' or 'n')))
                throw InvalidMove(input);

            return move[..2] + move.Substring(destinationIndex, 2) + suffix;
        }

        private static bool IsSquare(string move, int index) =>
            move[index] is >= 'a' and <= 'h' &&
            move[index + 1] is >= '1' and <= '8';

        private static ArgumentException InvalidMove(string input) =>
            new(
                $"Move {input} must use coordinate notation, such as e2e4, a8-a6, or e7-e8=q.",
                nameof(input));
    }
}
