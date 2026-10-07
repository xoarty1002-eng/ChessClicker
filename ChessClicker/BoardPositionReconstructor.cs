using System;
using System.Collections.Generic;

namespace ChessClicker
{
    public static class BoardPositionReconstructor
    {
        public static bool TryCorrectStandardStartingTurn(string fen, out string correctedFen)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(fen);
            ChessBoard position = new(fen);
            string[] fields = fen.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            correctedFen = fen;
            if (position.Turn != "black" || fields.Length != 6)
                return false;

            ChessBoard standardStart = new(ChessBoard.StandardStartingFen);
            for (char file = 'a'; file <= 'h'; file++)
            {
                for (char rank = '1'; rank <= '8'; rank++)
                {
                    string square = $"{file}{rank}";
                    if (position.GetPieceAt(square) != standardStart.GetPieceAt(square))
                        return false;
                }
            }

            fields[1] = "w";
            correctedFen = string.Join(' ', fields);
            return true;
        }

        public static string? FindSingleQuietMove(
            ChessBoard startingPosition,
            IReadOnlySet<string> observedOccupiedSquares)
        {
            ArgumentNullException.ThrowIfNull(startingPosition);
            ArgumentNullException.ThrowIfNull(observedOccupiedSquares);

            HashSet<string> initialOccupiedSquares = new(StringComparer.Ordinal);
            for (char file = 'a'; file <= 'h'; file++)
            {
                for (char rank = '1'; rank <= '8'; rank++)
                {
                    string square = $"{file}{rank}";
                    if (startingPosition.GetPieceAt(square) != ' ')
                        initialOccupiedSquares.Add(square);
                }
            }

            if (observedOccupiedSquares.Count != initialOccupiedSquares.Count)
                return null;

            string? uniqueMove = null;
            for (char fromFile = 'a'; fromFile <= 'h'; fromFile++)
            {
                for (char fromRank = '1'; fromRank <= '8'; fromRank++)
                {
                    string from = $"{fromFile}{fromRank}";
                    if (startingPosition.GetPieceAt(from) == ' ')
                        continue;

                    for (char toFile = 'a'; toFile <= 'h'; toFile++)
                    {
                        for (char toRank = '1'; toRank <= '8'; toRank++)
                        {
                            string to = $"{toFile}{toRank}";
                            if (initialOccupiedSquares.Contains(to))
                                continue;

                            string candidate = from + to;
                            if (!startingPosition.ValidateMove(
                                    8 - (fromRank - '0'), fromFile - 'a',
                                    8 - (toRank - '0'), toFile - 'a'))
                                continue;

                            HashSet<string> expectedOccupiedSquares =
                                new(initialOccupiedSquares, StringComparer.Ordinal);
                            expectedOccupiedSquares.Remove(from);
                            expectedOccupiedSquares.Add(to);
                            if (!expectedOccupiedSquares.SetEquals(observedOccupiedSquares))
                                continue;

                            if (uniqueMove != null)
                                return null;
                            uniqueMove = candidate;
                        }
                    }
                }
            }

            return uniqueMove;
        }
    }
}
