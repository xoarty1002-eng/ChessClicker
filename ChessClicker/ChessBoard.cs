using System;
using System.Linq;
using System.Text;

namespace ChessClicker
{
    public class ChessBoard
    {
        public const string StandardStartingFen =
            "rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq - 0 1";

        private int[,] _boardMatrix = new int[8, 8];
        private string _castlingRights = "KQkq";
        private string _enPassantSquare = "-";
        private int _halfmoveClock;
        private int _fullmoveNumber = 1;

        public string Side { get; private set; } = "white";
        public string Turn { get; private set; } = "white";
        public bool Check { get; private set; }
        public bool Checkmate { get; private set; }

        public ChessBoard(string initializationParam)
        {
            CreateBoard(initializationParam);
        }

        public void CreateBoard(string parameter)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(parameter);
            string lowerParam = parameter.Trim().ToLowerInvariant();
            if (lowerParam is "white" or "black")
            {
                _boardMatrix = new int[8, 8];
                SetupStandardPieces();
                Turn = "white";
                Side = lowerParam;
                _castlingRights = "KQkq";
                _enPassantSquare = "-";
                _halfmoveClock = 0;
                _fullmoveNumber = 1;
                UpdateGameStatus();
                return;
            }

            LoadFen(parameter);
        }

        private void SetupStandardPieces()
        {
            _boardMatrix[0, 0] = -4;
            _boardMatrix[0, 7] = -4;
            _boardMatrix[0, 1] = -2;
            _boardMatrix[0, 6] = -2;
            _boardMatrix[0, 2] = -3;
            _boardMatrix[0, 5] = -3;
            _boardMatrix[0, 3] = -5;
            _boardMatrix[0, 4] = -6;
            for (int file = 0; file < 8; file++)
                _boardMatrix[1, file] = -1;

            _boardMatrix[7, 0] = 4;
            _boardMatrix[7, 7] = 4;
            _boardMatrix[7, 1] = 2;
            _boardMatrix[7, 6] = 2;
            _boardMatrix[7, 2] = 3;
            _boardMatrix[7, 5] = 3;
            _boardMatrix[7, 3] = 5;
            _boardMatrix[7, 4] = 6;
            for (int file = 0; file < 8; file++)
                _boardMatrix[6, file] = 1;
        }

        private void LoadFen(string fen)
        {
            string[] sections = fen.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (sections.Length != 6 || sections[1] is not ("w" or "b"))
                throw new FormatException("FEN must contain six fields and a valid active color.");

            int[,] parsedBoard = new int[8, 8];
            string[] ranks = sections[0].Split('/');
            if (ranks.Length != 8)
                throw new FormatException("FEN must contain exactly eight ranks.");

            for (int rank = 0; rank < 8; rank++)
            {
                int file = 0;
                foreach (char value in ranks[rank])
                {
                    if (value is >= '1' and <= '8')
                    {
                        file += value - '0';
                    }
                    else
                    {
                        int piece = MapCharToPieceCode(value);
                        if (piece == 0 || file >= 8)
                            throw new FormatException($"FEN contains an invalid rank or piece '{value}'.");
                        parsedBoard[rank, file++] = piece;
                    }

                    if (file > 8)
                        throw new FormatException("FEN rank contains more than eight squares.");
                }

                if (file != 8)
                    throw new FormatException("Each FEN rank must describe exactly eight squares.");
            }

            string castlingRights = sections[2];
            if (castlingRights != "-" &&
                (castlingRights.Length > 4 ||
                 castlingRights.Any(right => !"KQkq".Contains(right, StringComparison.Ordinal)) ||
                 castlingRights.Distinct().Count() != castlingRights.Length ||
                 !string.Equals(
                     castlingRights,
                     string.Concat(castlingRights.OrderBy(right => "KQkq".IndexOf(right))),
                     StringComparison.Ordinal)))
                throw new FormatException("FEN contains invalid castling rights.");
            if ((castlingRights.Contains('K') && (parsedBoard[7, 4] != 6 || parsedBoard[7, 7] != 4)) ||
                (castlingRights.Contains('Q') && (parsedBoard[7, 4] != 6 || parsedBoard[7, 0] != 4)) ||
                (castlingRights.Contains('k') && (parsedBoard[0, 4] != -6 || parsedBoard[0, 7] != -4)) ||
                (castlingRights.Contains('q') && (parsedBoard[0, 4] != -6 || parsedBoard[0, 0] != -4)))
                throw new FormatException("FEN castling rights do not match the king and rook positions.");

            string enPassantSquare = sections[3];
            if (enPassantSquare != "-" &&
                (enPassantSquare.Length != 2 ||
                 enPassantSquare[0] is < 'a' or > 'h' ||
                 enPassantSquare[1] != (sections[1] == "w" ? '6' : '3')))
                throw new FormatException("FEN contains an invalid en-passant square.");
            if (enPassantSquare != "-")
            {
                int file = enPassantSquare[0] - 'a';
                int targetRank = 8 - (enPassantSquare[1] - '0');
                int capturedPawnRank = targetRank + (sections[1] == "w" ? 1 : -1);
                int expectedPawn = sections[1] == "w" ? -1 : 1;
                if (parsedBoard[targetRank, file] != 0 ||
                    parsedBoard[capturedPawnRank, file] != expectedPawn)
                    throw new FormatException("FEN en-passant square does not match the board position.");
            }

            if (!int.TryParse(sections[4], out int halfmoveClock) || halfmoveClock < 0 ||
                !int.TryParse(sections[5], out int fullmoveNumber) || fullmoveNumber < 1)
                throw new FormatException("FEN contains invalid move counters.");

            if (CountKings(parsedBoard, white: true) != 1 || CountKings(parsedBoard, white: false) != 1)
                throw new FormatException("FEN must contain exactly one king of each color.");

            _boardMatrix = parsedBoard;
            Turn = sections[1] == "w" ? "white" : "black";
            Side = Turn;
            _castlingRights = castlingRights;
            _enPassantSquare = enPassantSquare;
            _halfmoveClock = halfmoveClock;
            _fullmoveNumber = fullmoveNumber;
            UpdateGameStatus();
        }

        private static int MapCharToPieceCode(char value) => value switch
        {
            'P' => 1, 'p' => -1,
            'N' => 2, 'n' => -2,
            'B' => 3, 'b' => -3,
            'R' => 4, 'r' => -4,
            'Q' => 5, 'q' => -5,
            'K' => 6, 'k' => -6,
            _ => 0
        };

        public void UpdateMatrix(int[,] dynamicMatrix, string calculatedSide)
        {
            ArgumentNullException.ThrowIfNull(dynamicMatrix);
            if (dynamicMatrix.GetLength(0) != 8 || dynamicMatrix.GetLength(1) != 8)
                throw new ArgumentException("The position matrix must be exactly 8-by-8.", nameof(dynamicMatrix));
            if (calculatedSide is not ("white" or "black"))
                throw new ArgumentException("Side must be white or black.", nameof(calculatedSide));

            _boardMatrix = (int[,])dynamicMatrix.Clone();
            Turn = calculatedSide;
            Side = calculatedSide;
            _castlingRights = "-";
            _enPassantSquare = "-";
            UpdateGameStatusIfKingsPresent();
        }

        public bool ValidateMove(int fromRank, int fromFile, int toRank, int toFile) =>
            ValidateMove(fromRank, fromFile, toRank, toFile, enforceTurn: true);

        public bool ValidateMoveIgnoringTurn(int fromRank, int fromFile, int toRank, int toFile) =>
            ValidateMove(fromRank, fromFile, toRank, toFile, enforceTurn: false);

        private bool ValidateMove(int fromRank, int fromFile, int toRank, int toFile, bool enforceTurn)
        {
            if (!IsInsideBoard(fromRank, fromFile) || !IsInsideBoard(toRank, toFile) ||
                (fromRank == toRank && fromFile == toFile))
                return false;

            int piece = _boardMatrix[fromRank, fromFile];
            if (piece == 0 || (enforceTurn && !IsWhite(piece, Turn)) ||
                SameColor(piece, _boardMatrix[toRank, toFile]) ||
                Math.Abs(_boardMatrix[toRank, toFile]) == 6)
                return false;

            string movingSide = piece > 0 ? "white" : "black";
            if (!IsPseudoLegalMove(fromRank, fromFile, toRank, toFile, piece))
                return false;

            int[,] originalBoard = _boardMatrix;
            _boardMatrix = (int[,])originalBoard.Clone();
            if (Math.Abs(piece) == 1 && fromFile != toFile &&
                _boardMatrix[toRank, toFile] == 0 &&
                SquareName(toRank, toFile) == _enPassantSquare)
                _boardMatrix[toRank + (piece > 0 ? 1 : -1), toFile] = 0;
            ApplyMoveToMatrix(fromRank, fromFile, toRank, toFile, piece);
            bool legal = !IsKingInCheck(movingSide);
            _boardMatrix = originalBoard;
            return legal;
        }

        private bool IsPseudoLegalMove(int fromRank, int fromFile, int toRank, int toFile, int piece)
        {
            int deltaRank = toRank - fromRank;
            int deltaFile = toFile - fromFile;
            int target = _boardMatrix[toRank, toFile];
            switch (Math.Abs(piece))
            {
                case 1:
                    int direction = piece > 0 ? -1 : 1;
                    int homeRank = piece > 0 ? 6 : 1;
                    if (deltaFile == 0 && target == 0 && deltaRank == direction)
                        return true;
                    if (deltaFile == 0 && target == 0 && fromRank == homeRank &&
                        deltaRank == 2 * direction && _boardMatrix[fromRank + direction, fromFile] == 0)
                        return true;
                    if (Math.Abs(deltaFile) == 1 && deltaRank == direction && target != 0)
                        return true;
                    if (Math.Abs(deltaFile) == 1 && deltaRank == direction && target == 0 &&
                        SquareName(toRank, toFile) == _enPassantSquare)
                        return true;
                    return false;
                case 2:
                    return (Math.Abs(deltaRank) == 2 && Math.Abs(deltaFile) == 1) ||
                           (Math.Abs(deltaRank) == 1 && Math.Abs(deltaFile) == 2);
                case 3:
                    return Math.Abs(deltaRank) == Math.Abs(deltaFile) &&
                           CheckLineOfSight(fromRank, fromFile, toRank, toFile);
                case 4:
                    return (deltaRank == 0 || deltaFile == 0) &&
                           CheckLineOfSight(fromRank, fromFile, toRank, toFile);
                case 5:
                    return (Math.Abs(deltaRank) == Math.Abs(deltaFile) || deltaRank == 0 || deltaFile == 0) &&
                           CheckLineOfSight(fromRank, fromFile, toRank, toFile);
                case 6:
                    if (Math.Abs(deltaRank) <= 1 && Math.Abs(deltaFile) <= 1)
                        return true;
                    return IsLegalCastle(fromRank, fromFile, toRank, toFile, piece);
                default:
                    return false;
            }
        }

        private bool IsLegalCastle(int fromRank, int fromFile, int toRank, int toFile, int king)
        {
            int homeRank = king > 0 ? 7 : 0;
            if (fromRank != homeRank || fromFile != 4 || toRank != homeRank ||
                toFile is not (2 or 6) || IsKingInCheck(king > 0 ? "white" : "black"))
                return false;

            bool white = king > 0;
            bool kingSide = toFile == 6;
            string right = white
                ? kingSide ? "K" : "Q"
                : kingSide ? "k" : "q";
            if (!_castlingRights.Contains(right, StringComparison.Ordinal))
                return false;

            int rookFile = kingSide ? 7 : 0;
            if (_boardMatrix[homeRank, rookFile] != (white ? 4 : -4))
                return false;
            int firstFile = kingSide ? 5 : 1;
            int lastFile = kingSide ? 6 : 3;
            for (int file = firstFile; file <= lastFile; file++)
                if (_boardMatrix[homeRank, file] != 0)
                    return false;

            int transitFile = kingSide ? 5 : 3;
            return !IsSquareAttacked(homeRank, transitFile, byWhite: !white) &&
                   !IsSquareAttacked(homeRank, toFile, byWhite: !white);
        }

        private bool CheckLineOfSight(int fromRank, int fromFile, int toRank, int toFile)
        {
            int stepRank = Math.Sign(toRank - fromRank);
            int stepFile = Math.Sign(toFile - fromFile);
            for (int rank = fromRank + stepRank, file = fromFile + stepFile;
                 rank != toRank || file != toFile;
                 rank += stepRank, file += stepFile)
                if (_boardMatrix[rank, file] != 0)
                    return false;
            return true;
        }

        public bool MakeMove(string uciMove) => TryMakeMove(uciMove, enforceTurn: true);

        public bool MakeObservedMove(string uciMove) => TryMakeMove(uciMove, enforceTurn: false);

        private bool TryMakeMove(string uciMove, bool enforceTurn)
        {
            string move;
            try
            {
                move = ChessMoveNotation.Normalize(uciMove);
            }
            catch (ArgumentException)
            {
                return false;
            }

            int fromFile = move[0] - 'a';
            int fromRank = 8 - (move[1] - '0');
            int toFile = move[2] - 'a';
            int toRank = 8 - (move[3] - '0');
            if (!ValidateMove(fromRank, fromFile, toRank, toFile, enforceTurn))
                return false;

            int piece = _boardMatrix[fromRank, fromFile];
            if (move.Length == 5 &&
                (Math.Abs(piece) != 1 || toRank is not (0 or 7)))
                return false;
            int capturedPiece = _boardMatrix[toRank, toFile];
            bool enPassantCapture = Math.Abs(piece) == 1 && toFile != fromFile &&
                                    capturedPiece == 0 && SquareName(toRank, toFile) == _enPassantSquare;
            if (enPassantCapture)
            {
                int capturedPawnRank = toRank + (piece > 0 ? 1 : -1);
                capturedPiece = _boardMatrix[capturedPawnRank, toFile];
                _boardMatrix[capturedPawnRank, toFile] = 0;
            }

            UpdateCastlingRights(piece, fromRank, fromFile, toRank, toFile, capturedPiece);
            _enPassantSquare = Math.Abs(piece) == 1 && Math.Abs(toRank - fromRank) == 2
                ? SquareName((toRank + fromRank) / 2, fromFile)
                : "-";

            ApplyMoveToMatrix(fromRank, fromFile, toRank, toFile, piece);
            if (Math.Abs(piece) == 1 && toRank is 0 or 7)
            {
                int promotionPiece = move.Length == 5 ? move[4] switch
                {
                    'n' => 2, 'b' => 3, 'r' => 4, _ => 5
                } : 5;
                _boardMatrix[toRank, toFile] = piece > 0 ? promotionPiece : -promotionPiece;
            }

            _halfmoveClock = Math.Abs(piece) == 1 || capturedPiece != 0 ? 0 : _halfmoveClock + 1;
            if (piece < 0)
                _fullmoveNumber++;
            Turn = piece > 0 ? "black" : "white";
            Side = Turn;
            UpdateGameStatus();
            return true;
        }

        private void ApplyMoveToMatrix(int fromRank, int fromFile, int toRank, int toFile, int piece)
        {
            _boardMatrix[toRank, toFile] = piece;
            _boardMatrix[fromRank, fromFile] = 0;
            if (Math.Abs(piece) == 6 && Math.Abs(toFile - fromFile) == 2)
            {
                int rookFromFile = toFile == 6 ? 7 : 0;
                int rookToFile = toFile == 6 ? 5 : 3;
                _boardMatrix[toRank, rookToFile] = _boardMatrix[toRank, rookFromFile];
                _boardMatrix[toRank, rookFromFile] = 0;
            }
        }

        private void UpdateCastlingRights(int piece, int fromRank, int fromFile, int toRank, int toFile, int capturedPiece)
        {
            if (Math.Abs(piece) == 6)
                _castlingRights = RemoveRights(_castlingRights, piece > 0 ? "KQ" : "kq");
            if (Math.Abs(piece) == 4)
                RemoveRookRight(piece, fromRank, fromFile);
            if (Math.Abs(capturedPiece) == 4)
                RemoveRookRight(capturedPiece, toRank, toFile);
        }

        private void RemoveRookRight(int rook, int rank, int file)
        {
            string? right = (rook, rank, file) switch
            {
                (4, 7, 0) => "Q",
                (4, 7, 7) => "K",
                (-4, 0, 0) => "q",
                (-4, 0, 7) => "k",
                _ => null
            };
            if (right != null)
                _castlingRights = RemoveRights(_castlingRights, right);
        }

        private static string RemoveRights(string rights, string remove)
        {
            string remaining = string.Concat(rights.Where(right => !remove.Contains(right)));
            return remaining.Length == 0 ? "-" : remaining;
        }

        private bool IsKingInCheck(string side)
        {
            int king = side == "white" ? 6 : -6;
            for (int rank = 0; rank < 8; rank++)
                for (int file = 0; file < 8; file++)
                    if (_boardMatrix[rank, file] == king)
                        return IsSquareAttacked(rank, file, byWhite: side != "white");
            return true;
        }

        private bool IsSquareAttacked(int targetRank, int targetFile, bool byWhite)
        {
            for (int rank = 0; rank < 8; rank++)
            {
                for (int file = 0; file < 8; file++)
                {
                    int piece = _boardMatrix[rank, file];
                    if (piece == 0 || (piece > 0) != byWhite)
                        continue;
                    int deltaRank = targetRank - rank;
                    int deltaFile = targetFile - file;
                    switch (Math.Abs(piece))
                    {
                        case 1:
                            if (deltaRank == (byWhite ? -1 : 1) && Math.Abs(deltaFile) == 1)
                                return true;
                            break;
                        case 2:
                            if ((Math.Abs(deltaRank) == 2 && Math.Abs(deltaFile) == 1) ||
                                (Math.Abs(deltaRank) == 1 && Math.Abs(deltaFile) == 2))
                                return true;
                            break;
                        case 3:
                            if (Math.Abs(deltaRank) == Math.Abs(deltaFile) &&
                                CheckLineOfSight(rank, file, targetRank, targetFile))
                                return true;
                            break;
                        case 4:
                            if ((deltaRank == 0 || deltaFile == 0) &&
                                CheckLineOfSight(rank, file, targetRank, targetFile))
                                return true;
                            break;
                        case 5:
                            if ((Math.Abs(deltaRank) == Math.Abs(deltaFile) || deltaRank == 0 || deltaFile == 0) &&
                                CheckLineOfSight(rank, file, targetRank, targetFile))
                                return true;
                            break;
                        case 6:
                            if (Math.Max(Math.Abs(deltaRank), Math.Abs(deltaFile)) == 1)
                                return true;
                            break;
                    }
                }
            }
            return false;
        }

        private bool HasAnyLegalMove(string side)
        {
            bool white = side == "white";
            for (int fromRank = 0; fromRank < 8; fromRank++)
            {
                for (int fromFile = 0; fromFile < 8; fromFile++)
                {
                    int piece = _boardMatrix[fromRank, fromFile];
                    if (piece == 0 || (piece > 0) != white)
                        continue;
                    for (int toRank = 0; toRank < 8; toRank++)
                        for (int toFile = 0; toFile < 8; toFile++)
                            if (ValidateMove(fromRank, fromFile, toRank, toFile, enforceTurn: false))
                                return true;
                }
            }
            return false;
        }

        public bool HasLegalMoveForTurn() => HasAnyLegalMove(Turn);

        public char GetPieceAt(string square)
        {
            BoardMouseCoordinates.ValidateSquare(square, nameof(square));
            int rank = 8 - (square[1] - '0');
            int file = square[0] - 'a';
            return MapPieceCodeToChar(_boardMatrix[rank, file]);
        }

        private void UpdateGameStatus()
        {
            Check = IsKingInCheck(Turn);
            Checkmate = Check && !HasAnyLegalMove(Turn);
        }

        private void UpdateGameStatusIfKingsPresent()
        {
            if (CountKings(_boardMatrix, white: true) == 1 && CountKings(_boardMatrix, white: false) == 1)
                UpdateGameStatus();
            else
            {
                Check = false;
                Checkmate = false;
            }
        }

        private static bool IsInsideBoard(int rank, int file) =>
            rank is >= 0 and < 8 && file is >= 0 and < 8;

        private static bool IsWhite(int piece, string side) => (piece > 0) == (side == "white");
        private static bool SameColor(int first, int second) => first != 0 && second != 0 && (first > 0) == (second > 0);
        private static string SquareName(int rank, int file) => $"{(char)('a' + file)}{8 - rank}";

        private static int CountKings(int[,] board, bool white)
        {
            int count = 0;
            int king = white ? 6 : -6;
            foreach (int piece in board)
                if (piece == king)
                    count++;
            return count;
        }

        public string GenerateFen()
        {
            StringBuilder fen = new();
            for (int rank = 0; rank < 8; rank++)
            {
                int emptyCount = 0;
                for (int file = 0; file < 8; file++)
                {
                    int piece = _boardMatrix[rank, file];
                    if (piece == 0)
                        emptyCount++;
                    else
                    {
                        if (emptyCount > 0)
                        {
                            fen.Append(emptyCount);
                            emptyCount = 0;
                        }
                        fen.Append(MapPieceCodeToChar(piece));
                    }
                }
                if (emptyCount > 0)
                    fen.Append(emptyCount);
                if (rank < 7)
                    fen.Append('/');
            }

            fen.Append(Turn == "white" ? " w " : " b ");
            fen.Append(_castlingRights).Append(' ').Append(_enPassantSquare)
                .Append(' ').Append(_halfmoveClock).Append(' ').Append(_fullmoveNumber);
            return fen.ToString();
        }

        private static char MapPieceCodeToChar(int code) => code switch
        {
            1 => 'P', -1 => 'p',
            2 => 'N', -2 => 'n',
            3 => 'B', -3 => 'b',
            4 => 'R', -4 => 'r',
            5 => 'Q', -5 => 'q',
            6 => 'K', -6 => 'k',
            _ => ' '
        };

        public string GetDebugBoardString()
        {
            StringBuilder builder = new();
            builder.AppendLine($"=== Player: {Side} | Turn: {Turn} ===");
            builder.AppendLine($"=== Check: {Check} | Mate: {Checkmate} ===");
            for (int rank = 0; rank < 8; rank++)
            {
                for (int file = 0; file < 8; file++)
                {
                    int piece = _boardMatrix[rank, file];
                    builder.Append(piece == 0 ? "[ ] " : $"[{MapPieceCodeToChar(piece)}] ");
                }
                builder.AppendLine();
            }
            return builder.ToString();
        }
    }
}
