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
        private readonly MoveConfirmation _moveConfirmation = new();
        private string? _pendingFromSquare;
        private bool? _lastDetectedWhiteView;
        private ChessClickerSettings _settings = ChessClickerSettings.Default;

        private static readonly TimeSpan MoveConfirmationTimeout = TimeSpan.FromSeconds(8);
        private static readonly TimeSpan MoveConfirmationPollInterval = TimeSpan.FromMilliseconds(200);
        private static readonly TimeSpan BoardStabilityTimeout = TimeSpan.FromSeconds(8);
        private int _engineTurnCount;

        public event Action<string>? StatusChanged;
        public event Action<string>? BoardChanged;

        public bool IsBusy { get; private set; }

        public void UpdateSettings(ChessClickerSettings settings)
        {
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _engineTurnCount = 0;
        }

        public Bitmap CaptureBoard(Rectangle bounds)
        {
            return _scanner.CaptureBoardRegion(bounds);
        }

        public void ResetBoardTracking(Bitmap boardImage)
        {
            _scanner.ResetStateTracking(boardImage);
        }

        public bool DetectWhiteView(Bitmap boardImage)
        {
            bool isWhiteView = _scanner.DetectPlayerSideFromImage(boardImage);
            if (_lastDetectedWhiteView != isWhiteView)
            {
                _lastDetectedWhiteView = isWhiteView;
                StatusChanged?.Invoke($"[Board orientation] Detected {(isWhiteView ? "White" : "Black")} perspective.");
            }

            return isWhiteView;
        }

        public async Task ProcessBoardFrameAsync(Bitmap boardImage, Rectangle bounds, bool isWhiteView)
        {
            if (IsBusy) return;

            IsBusy = true;
            try
            {
                string? candidates = _scanner.ScanForStateChanges(
                    boardImage, isWhiteView, _settings.BrightnessThreshold);
                if (string.IsNullOrEmpty(candidates)) return;

                if (_moveConfirmation.PendingMove is string pendingMove)
                {
                    if (ConfirmEngineMove(candidates))
                    {
                        NotifyBoardChanged();
                        StatusChanged?.Invoke($"[Move confirmed] {pendingMove} was detected on the board.");
                    }
                    else
                    {
                        _scanner.ResetStateTracking(boardImage);
                        StatusChanged?.Invoke(
                            $"[Move not synchronized] Expected {pendingMove}, but the changed squares did not uniquely confirm it. The tracked position was not changed.");
                    }
                    return;
                }

                string? appliedMove = ApplyDetectedMove(candidates);
                if (appliedMove == null)
                {
                    StatusChanged?.Invoke(
                        $"[Ignored board change] No unique legal move could be selected from the changed squares for {_board.Turn} to move.");
                    return;
                }

                NotifyBoardChanged();
                StatusChanged?.Invoke($"[State Modified] Processed legal move: {appliedMove}");
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
            if (_moveConfirmation.PendingMove != null)
            {
                StatusChanged?.Invoke(
                    $"[Move awaiting confirmation] {_moveConfirmation.PendingMove} has not yet been observed on the board.");
                return;
            }

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

        public async Task ExecuteTypedMoveAsync(string move, Rectangle bounds, bool isWhiteView)
        {
            if (IsBusy) return;
            if (_moveConfirmation.PendingMove != null)
            {
                StatusChanged?.Invoke(
                    $"[Move awaiting confirmation] {_moveConfirmation.PendingMove} has not yet been observed on the board.");
                return;
            }
            if (string.IsNullOrWhiteSpace(move))
                throw new ArgumentException("Enter a move in UCI notation, such as e2e4.", nameof(move));

            move = move.Trim().ToLowerInvariant();
            if (move.Length < 4 || !IsLegalMove(move[..4]))
                throw new ArgumentException($"Move {move} is not legal for {_board.Turn} to move.", nameof(move));

            IsBusy = true;
            try
            {
                await SendMoveAndWaitForConfirmationAsync(move, bounds, isWhiteView);
            }
            finally
            {
                IsBusy = false;
            }
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
            if (_moveConfirmation.PendingMove != null)
            {
                StatusChanged?.Invoke(
                    $"[Move awaiting confirmation] {_moveConfirmation.PendingMove} has not yet been observed on the board.");
                return;
            }

            int skillLevel = GetSkillLevelForNextTurn();
            StatusChanged?.Invoke($"[Stockfish] Calculating a move at skill level {skillLevel}/20...");

            string enginePath = await _engine.EnsureEngineInstalledAsync();
            string fen = _board.GenerateFen();
            StatusChanged?.Invoke($"[FEN Query] {fen}");

            string move = await Task.Run(() =>
                _engine.GetBestMove(enginePath, fen, _settings.MoveTimeMilliseconds, skillLevel));
            StatusChanged?.Invoke($"[Stockfish Recommendation] {move}");

            if (move.Length < 4 || move == "None" || move == "Error starting engine" ||
                move == "(none)" || move == "0000")
                return;

            await SendMoveAndWaitForConfirmationAsync(move, bounds, isWhiteView);
        }

        private async Task SendMoveAndWaitForConfirmationAsync(string move, Rectangle bounds, bool isWhiteView)
        {
            StatusChanged?.Invoke("[Board verification] Waiting for three stable captures before sending the move...");
            if (!await WaitForStableBoardAsync(bounds))
            {
                StatusChanged?.Invoke(
                    $"[Move blocked] The board did not remain stable for three captures within {BoardStabilityTimeout.TotalSeconds:0} seconds. No move was sent.");
                return;
            }

            using Bitmap baseline = CaptureBoard(bounds);
            _scanner.ResetStateTracking(baseline);
            _moveConfirmation.Expect(move);
            _clicker.ExecuteMoveOnScreen(move, bounds, isWhiteView);

            StatusChanged?.Invoke($"[Move sent] {move}. Waiting for the board to show the move...");
            DateTime confirmationDeadline = DateTime.UtcNow + MoveConfirmationTimeout;
            while (DateTime.UtcNow < confirmationDeadline)
            {
                await Task.Delay(MoveConfirmationPollInterval);
                using Bitmap currentBoard = CaptureBoard(bounds);
                string? candidates = _scanner.ScanForStateChanges(
                    currentBoard, isWhiteView, _settings.BrightnessThreshold);
                if (string.IsNullOrEmpty(candidates))
                    continue;

                if (ConfirmEngineMove(candidates))
                {
                    NotifyBoardChanged();
                    StatusChanged?.Invoke($"[Move confirmed] {move} was detected on the board.");
                    return;
                }

                _scanner.ResetStateTracking(currentBoard);
            }

            _moveConfirmation.Cancel();
            _scanner.ResetStateTracking(baseline);
            StatusChanged?.Invoke(
                $"[Move not confirmed] {move} was sent, but no matching board change appeared within {MoveConfirmationTimeout.TotalSeconds:0} seconds. The tracked position was not advanced; scanning remains armed to detect a delayed board update.");
        }

        private async Task<bool> WaitForStableBoardAsync(Rectangle bounds)
        {
            var stabilityTracker = new BoardStabilityTracker(requiredStableFrames: 3);
            DateTime deadline = DateTime.UtcNow + BoardStabilityTimeout;
            int intervalMilliseconds = Math.Max(1, 1000 / _settings.FramesPerSecond);

            while (DateTime.UtcNow < deadline)
            {
                using Bitmap currentBoard = CaptureBoard(bounds);
                if (stabilityTracker.AddFrame(_scanner.ExtractGridBrightness(currentBoard)))
                    return true;

                await Task.Delay(intervalMilliseconds);
            }

            return false;
        }

        private int GetSkillLevelForNextTurn()
        {
            if (!_settings.RandomizeStockfishSkill)
                return _settings.StockfishSkillLevel;

            _engineTurnCount++;
            if (_engineTurnCount % _settings.RandomSkillIntervalTurns == 0)
                return Random.Shared.Next(0, 21);

            return _settings.StockfishSkillLevel;
        }

        private bool ConfirmEngineMove(string candidates)
        {
            string? detectedMove = BoardMoveDetector.FindUniqueLegalMove(candidates.Split('|'), IsLegalMove);
            if (detectedMove == null ||
                !_moveConfirmation.TryConfirm(detectedMove, out string? confirmedMove) ||
                confirmedMove == null)
                return false;

            if (_board.MakeMove(confirmedMove))
                return true;

            _moveConfirmation.Expect(confirmedMove);
            StatusChanged?.Invoke(
                $"[Synchronization error] The displayed move {confirmedMove} is not legal for {_board.Turn} to move.");
            return false;
        }

        private string? ApplyDetectedMove(string candidates)
        {
            string? move = BoardMoveDetector.FindUniqueLegalMove(candidates.Split('|'), IsLegalMove);
            return move != null && _board.MakeMove(move) ? move : null;
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