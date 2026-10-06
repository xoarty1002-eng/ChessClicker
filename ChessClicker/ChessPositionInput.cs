using System;

namespace ChessClicker
{
    public enum ChessPositionInputKind
    {
        Move,
        Fen
    }

    public sealed record ParsedChessPositionInput(ChessPositionInputKind Kind, string Value);

    public static class ChessPositionInput
    {
        public static ParsedChessPositionInput Parse(string? input)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(input);
            string value = input.Trim();
            string[] fields = value.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (value.Contains('/') || fields.Length == 6)
            {
                _ = new ChessBoard(value);
                return new ParsedChessPositionInput(ChessPositionInputKind.Fen, value);
            }

            return new ParsedChessPositionInput(
                ChessPositionInputKind.Move,
                ChessMoveNotation.Normalize(value));
        }
    }
}
