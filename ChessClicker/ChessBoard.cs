using System;
using System.Text;

namespace ChessClicker
{
    public class ChessBoard
    {
        // Matrix encoding rules:
        // White: 1=Pawn, 2=Knight, 3=Bishop, 4=Rook, 5=Queen, 6=King
        // Black: -1=Pawn, -2=Knight, -3=Bishop, -4=Rook, -5=Queen, -6=King
        // 0 = Empty Space
        private int[,] _boardMatrix = new int[8, 8];

        public string Side { get; private set; } = "white";
        public string Turn { get; private set; } = "white";
        public bool Check { get; set; } = false;
        public bool Checkmate { get; set; } = false;

        public ChessBoard(string initializationParam)
        {
            CreateBoard(initializationParam);
        }

        public void CreateBoard(string parameter)
        {
            string lowerParam = parameter.Trim().ToLower();

            if (lowerParam == "white" || lowerParam == "black")
            {
                Side = lowerParam;
                Turn = "white";
                Check = false;
                Checkmate = false;
                SetupStandardPieces();
            }
            else
            {
                LoadFen(parameter);
            }
        }

        private void SetupStandardPieces()
        {
            _boardMatrix = new int[8, 8];

            // Setup Black baseline layout arrays
            _boardMatrix[0, 0] = -4; _boardMatrix[0, 7] = -4; // Rooks
            _boardMatrix[0, 1] = -2; _boardMatrix[0, 6] = -2; // Knights
            _boardMatrix[0, 2] = -3; _boardMatrix[0, 5] = -3; // Bishops
            _boardMatrix[0, 3] = -5; // Queen
            _boardMatrix[0, 4] = -6; // King
            for (int f = 0; f < 8; f++) _boardMatrix[1, f] = -1; // Pawns

            // Setup White baseline layout arrays
            _boardMatrix[7, 0] = 4; _boardMatrix[7, 7] = 4; // Rooks
            _boardMatrix[7, 1] = 2; _boardMatrix[7, 6] = 2; // Knights
            _boardMatrix[7, 2] = 3; _boardMatrix[7, 5] = 3; // Bishops
            _boardMatrix[7, 3] = 5; // Queen
            _boardMatrix[7, 4] = 6; // King
            for (int f = 0; f < 8; f++) _boardMatrix[6, f] = 1; // Pawns
        }

        private void LoadFen(string fen)
        {
            _boardMatrix = new int[8, 8];
            string[] sections = fen.Split(' ');
            string rowsData = sections[0];

            string[] ranks = rowsData.Split('/');
            for (int r = 0; r < 8; r++)
            {
                int fileIndex = 0;
                foreach (char c in ranks[r])
                {
                    if (char.IsDigit(c))
                    {
                        fileIndex += (c - '0');
                    }
                    else
                    {
                        _boardMatrix[r, fileIndex] = MapCharToPieceCode(c);
                        fileIndex++;
                    }
                }
            }

            if (sections.Length > 1)
            {
                Turn = sections[1].ToLower() == "w" ? "white" : "black";
            }
        }

        private int MapCharToPieceCode(char c)
        {
            switch (c)
            {
                case 'P': return 1;
                case 'p': return -1;
                case 'N': return 2;
                case 'n': return -2;
                case 'B': return 3;
                case 'b': return -3;
                case 'R': return 4;
                case 'r': return -4;
                case 'Q': return 5;
                case 'q': return -5;
                case 'K': return 6;
                case 'k': return -6;
                default: return 0;
            }
        }

        public void UpdateMatrix(int[,] dynamicMatrix, string calculatedSide)
        {
            if (dynamicMatrix.GetLength(0) == 8 && dynamicMatrix.GetLength(1) == 8)
            {
                _boardMatrix = (int[,])dynamicMatrix.Clone();
                Side = calculatedSide.ToLower();
            }
        }

        /// <summary>
        /// Validates move coordinates according to the actual mechanical rules of the selected piece.
        /// </summary>
        public bool ValidateMove(int fromRank, int fromFile, int toRank, int toFile)
        {
            // 1. Structural window boundaries filter
            if (fromRank < 0 || fromRank > 7 || fromFile < 0 || fromFile > 7) return false;
            if (toRank < 0 || toRank > 7 || toFile < 0 || toFile > 7) return false;
            if (fromRank == toRank && fromFile == toFile) return false;

            int piece = _boardMatrix[fromRank, fromFile];
            if (piece == 0) return false;

            // 2. Enforce structural turn sequencing constraints
            if (Turn == "white" && piece < 0) return false;
            if (Turn == "black" && piece > 0) return false;

            int targetSquare = _boardMatrix[toRank, toFile];

            // 3. Absolute rule: Cannot capture an allied figure matching color bounds
            if (targetSquare != 0 && ((piece > 0 && targetSquare > 0) || (piece < 0 && targetSquare < 0)))
                return false;

            int deltaRank = toRank - fromRank;
            int deltaFile = toFile - fromFile;
            int absPiece = Math.Abs(piece);

            // 4. Unique piece mechanics evaluations
            switch (absPiece)
            {
                case 1: // PAWN MECHANICAL PATHS
                    int forwardDirection = (piece > 0) ? -1 : 1;
                    int startingRank = (piece > 0) ? 6 : 1;

                    // Standard 1-tile progression forward vector
                    if (deltaFile == 0 && deltaRank == forwardDirection && targetSquare == 0)
                        return true;

                    // Initial 2-tile burst jump rule pattern evaluation
                    if (deltaFile == 0 && fromRank == startingRank && deltaRank == (2 * forwardDirection))
                    {
                        int intermediateRank = fromRank + forwardDirection;
                        if (_boardMatrix[intermediateRank, fromFile] == 0 && targetSquare == 0)
                            return true;
                    }

                    // Traditional diagonal strike capture steps
                    if (Math.Abs(deltaFile) == 1 && deltaRank == forwardDirection && targetSquare != 0)
                        return true;

                    return false;

                case 2: // KNIGHT STEP MATRIX
                    return (Math.Abs(deltaRank) == 2 && Math.Abs(deltaFile) == 1) ||
                           (Math.Abs(deltaRank) == 1 && Math.Abs(deltaFile) == 2);

                case 3: // BISHOP DIAGONAL RAYS
                    if (Math.Abs(deltaRank) != Math.Abs(deltaFile)) return false;
                    return CheckLineOfSight(fromRank, fromFile, toRank, toFile);

                case 4: // ROOK STRAIGHT RAYS
                    if (deltaRank != 0 && deltaFile != 0) return false;
                    return CheckLineOfSight(fromRank, fromFile, toRank, toFile);

                case 5: // QUEEN HYBRID RAYS
                    if (Math.Abs(deltaRank) != Math.Abs(deltaFile) && deltaRank != 0 && deltaFile != 0) return false;
                    return CheckLineOfSight(fromRank, fromFile, toRank, toFile);

                case 6: // KING BOUNDARY RING
                    return Math.Abs(deltaRank) <= 1 && Math.Abs(deltaFile) <= 1;

                default:
                    return false;
            }
        }

        /// <summary>
        /// Validates that sliding lines of sight are not blocked by collision obstacles.
        /// </summary>
        private bool CheckLineOfSight(int fRank, int fFile, int tRank, int tFile)
        {
            int stepRank = Math.Sign(tRank - fRank);
            int stepFile = Math.Sign(tFile - fFile);

            int currentRank = fRank + stepRank;
            int currentFile = fFile + stepFile;

            while (currentRank != tRank || currentFile != tFile)
            {
                if (_boardMatrix[currentRank, currentFile] != 0)
                    return false; // Path blocked!

                currentRank += stepRank;
                currentFile += stepFile;
            }
            return true;
        }

        public bool MakeMove(string uciMove)
        {
            if (string.IsNullOrEmpty(uciMove) || uciMove.Length < 4) return false;

            int fromFile = uciMove[0] - 'a';
            int fromRank = 8 - (uciMove[1] - '0');
            int toFile = uciMove[2] - 'a';
            int toRank = 8 - (uciMove[3] - '0');

            if (!ValidateMove(fromRank, fromFile, toRank, toFile)) return false;

            _boardMatrix[toRank, toFile] = _boardMatrix[fromRank, fromFile];
            _boardMatrix[fromRank, fromFile] = 0;

            Turn = (Turn == "white") ? "black" : "white";
            return true;
        }

        public string GenerateFen()
        {
            StringBuilder fen = new StringBuilder();
            for (int r = 0; r < 8; r++)
            {
                int emptyCount = 0;
                for (int f = 0; f < 8; f++)
                {
                    int piece = _boardMatrix[r, f];
                    if (piece == 0)
                    {
                        emptyCount++;
                    }
                    else
                    {
                        if (emptyCount > 0) { fen.Append(emptyCount); emptyCount = 0; }
                        fen.Append(MapPieceCodeToChar(piece));
                    }
                }
                if (emptyCount > 0) fen.Append(emptyCount);
                if (r < 7) fen.Append("/");
            }

            fen.Append(Turn == "white" ? " w " : " b ");
            fen.Append("KQkq - 0 1");
            return fen.ToString();
        }

        private char MapPieceCodeToChar(int code)
        {
            switch (code)
            {
                case 1: return 'P';
                case -1: return 'p';
                case 2: return 'N';
                case -2: return 'n';
                case 3: return 'B';
                case -3: return 'b';
                case 4: return 'R';
                case -4: return 'r';
                case 5: return 'Q';
                case -5: return 'q';
                case 6: return 'K';
                case -6: return 'k';
                default: return ' ';
            }
        }
        public string GetDebugBoardString()
        {
            StringBuilder sb = new StringBuilder(); sb.AppendLine($"=== Player: {Side} | Turn: {Turn} ===");
            sb.AppendLine($"=== Check: {Check} | Mate: {Checkmate} ===");
            for (int r = 0; r < 8; r++)
            {
                for (int f = 0; f < 8; f++)
                {
                    int piece = _boardMatrix[r, f];
                    if (piece == 0) sb.Append("[ ] ");
                    else sb.Append($"[{MapPieceCodeToChar(piece)}] ");
                }
                sb.AppendLine();
            }
            return sb.ToString();
        }
    }
}