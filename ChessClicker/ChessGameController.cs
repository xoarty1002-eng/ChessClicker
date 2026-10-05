using System;
using System.Drawing;
using System.Threading.Tasks;

namespace ChessClicker
{
    internal sealed class ChessGameController
    {
        private readonly ChessBoard _board = new ChessBoard("white");
        private readonly ImageScaner _scanner = new ImageScaner();
        private readonly EngineRun _engine = new EngineRun();
        private readonly DesktopClicker _clicker = new DesktopClicker();
        private string? _pendingFromSquare;

        public event Action<string>? StatusChanged;
        public event Action<string>? BoardChanged;

        public bool IsBusy { get; private set; }

        public Bitmap CaptureBoard(Rectangle bounds)
        {
            return _scanner.CaptureBoardRegion(bounds);
        }

        public bool DetectWhiteView(Bitmap boardImage)
        {
            return _scanner.DetectPlayerSideFromImage(boardImage);
        }

        public async Task ProcessBoardFrameAsync(Bitmap boardImage, Rectangle bounds, bool isWhiteView)
        {
            if (IsBusy) return;

            IsBusy = true;
            try
            {
                string? candidates = _scanner.ScanForStateChanges(boardImage, isWhiteView);
                if (string.IsNullOrEmpty(candidates)) return;

                string[] moves = candidates.Split('|');
                if (moves.Length != 2) return;

                string? appliedMove = ApplyDetectedMove(moves[0], moves[1]);
                if (appliedMove == null)
                {
                    StatusChanged?.Invoke($"[Ignored board change] {moves[0]} / {moves[1]} is not legal for {_board.Turn} to move.");
                    return;
                }

                StatusChanged?.Invoke($"[State Modified] Processed legal move: {appliedMove}");
                NotifyBoardChanged();
                await RequestEngineMoveCoreAsync(bounds, isWhiteView);
            }
            catch (Exception ex)
            {
                StatusChanged?.Invoke($"[Automation Pipeline Failure] {ex.Message}");
            }
            finally
            {
                IsBusy = false;
            }
        }

        public void RegisterSquareClick(string square, Rectangle bounds, bool isWhiteView)
        {
            if (IsBusy) return;

            if (_pendingFromSquare == null)
            {
                _pendingFromSquare = square;
                return;
            }

            string move = _pendingFromSquare + square;
            _pendingFromSquare = null;
            if (!IsLegalMove(move))
            {
                _pendingFromSquare = square;
                return;
            }

            _ = ProcessClickedMoveAsync(move, bounds, isWhiteView);
        }

        public async Task RequestEngineMoveAsync(Rectangle bounds, bool isWhiteView)
        {
            if (IsBusy) return;

            IsBusy = true;
            try
            {
                await RequestEngineMoveCoreAsync(bounds, isWhiteView);
            }
            catch (Exception ex)
            {
                StatusChanged?.Invoke($"[Engine Execution Failure] {ex.Message}");
            }
            finally
            {
                IsBusy = false;
            }
        }

        public string GetDebugBoardString()
        {
            return _board.GetDebugBoardString();
        }

        private async Task ProcessClickedMoveAsync(string move, Rectangle bounds, bool isWhiteView)
        {
            IsBusy = true;
            try
            {
                await Task.Delay(350);
                if (!_board.MakeMove(move))
                {
                    StatusChanged?.Invoke($"[Mouse move rejected] {move} no longer matches the tracked position.");
                    return;
                }

                NotifyBoardChanged();
                StatusChanged?.Invoke($"[Mouse move detected] {move}");
                await RequestEngineMoveCoreAsync(bounds, isWhiteView);
            }
            catch (Exception ex)
            {
                StatusChanged?.Invoke($"[Mouse move error] {ex.Message}");
            }
            finally
            {
                IsBusy = false;
            }
        }

        private async Task RequestEngineMoveCoreAsync(Rectangle bounds, bool isWhiteView)
        {
            StatusChanged?.Invoke("[Stockfish] Calculating a move...");

            string enginePath = await _engine.EnsureEngineInstalledAsync();
            string fen = _board.GenerateFen();
            StatusChanged?.Invoke($"[FEN Query] {fen}");

            string move = await Task.Run(() => _engine.GetBestMove(enginePath, fen, 1000));
            StatusChanged?.Invoke($"[Stockfish Recommendation] {move}");

            if (move.Length < 4 || move == "None" || move == "Error starting engine" ||
                move == "(none)" || move == "0000")
                return;

            _clicker.ExecuteMoveOnScreen(move, bounds, isWhiteView);
            if (_board.MakeMove(move))
            {
                StatusChanged?.Invoke($"[Move played] {move}");
                NotifyBoardChanged();
            }
        }

        private string? ApplyDetectedMove(string firstMove, string secondMove)
        {
            if (_board.MakeMove(firstMove)) return firstMove;
            if (_board.MakeMove(secondMove)) return secondMove;
            return null;
        }

        private bool IsLegalMove(string move)
        {
            int fromFile = move[0] - 'a';
            int fromRank = 8 - (move[1] - '0');
            int toFile = move[2] - 'a';
            int toRank = 8 - (move[3] - '0');
            return _board.ValidateMove(fromRank, fromFile, toRank, toFile);
        }

        private void NotifyBoardChanged()
        {
            BoardChanged?.Invoke(_board.GetDebugBoardString());
        }
    }
}