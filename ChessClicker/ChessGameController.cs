using System;
using System.Drawing;
using System.Threading;
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
        private CancellationTokenSource _automationCancellation = new();
        private bool _automationActive;
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

        public void StartAutomation()
        {
            if (_automationCancellation.IsCancellationRequested)
            {
                _automationCancellation.Dispose();
                _automationCancellation = new CancellationTokenSource();
            }
            _automationActive = true;
        }

        public void StopAutomation()
        {
            _automationActive = false;
            _automationCancellation.Cancel();
        }

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

        public void ResetPosition(string fen)
        {
            if (IsBusy)
                throw new InvalidOperationException("Cannot reset the tracked position while a move is being processed.");

            _board.CreateBoard(fen);
            _pendingFromSquare = null;
            _moveConfirmation.Cancel();
            NotifyBoardChanged();
            StatusChanged?.Invoke($"[Position loaded] {_board.Turn} to move.");
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
            CancellationToken cancellationToken = _automationCancellation.Token;
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
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
                        $"[Ignored board change] No unique legal move could be selected from the changed squares for {_board.Turn} to move. Check the board calibration and tracked starting position.");
                    return;
                }

                NotifyBoardChanged();
                StatusChanged?.Invoke($"[State Modified] Processed legal move: {appliedMove}");
                if (ShouldEngineMove(isWhiteView))
                    await RequestEngineMoveCoreAsync(bounds, isWhiteView, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                StatusChanged?.Invoke("[Play] Automation stopped.");
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
            CancellationToken cancellationToken = _automationCancellation.Token;
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (ShouldEngineMove(isWhiteView))
                    await RequestEngineMoveCoreAsync(bounds, isWhiteView, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                StatusChanged?.Invoke("[Play] Automation stopped.");
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

        public async Task<string?> GetEngineSuggestionAsync()
        {
            if (IsBusy)
                throw new InvalidOperationException("Wait for the current operation before requesting a suggestion.");

            IsBusy = true;
            try
            {
                return await GetEngineMoveSuggestionAsync(CancellationToken.None);
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
            move = ChessMoveNotation.Normalize(move);

            IsBusy = true;
            try
            {
                await SendMoveAndWaitForConfirmationAsync(
                    move,
                    bounds,
                    isWhiteView,
                    _automationActive ? _automationCancellation.Token : CancellationToken.None,
                    continueGame: _automationActive);
            }
            finally
            {
                IsBusy = false;
            }
        }

        private async Task ProcessClickedMoveAsync(string move, Rectangle bounds, bool isWhiteView)
        {
            IsBusy = true;
            CancellationToken cancellationToken = _automationCancellation.Token;
            try
            {
                await Task.Delay(350, cancellationToken);
                if (!_board.MakeMove(move))
                {
                    StatusChanged?.Invoke($"[Mouse move rejected] {move} no longer matches the tracked position.");
                    return;
                }

                NotifyBoardChanged();
                StatusChanged?.Invoke($"[Mouse move detected] {move}");
                if (ShouldEngineMove(isWhiteView))
                    await RequestEngineMoveCoreAsync(bounds, isWhiteView, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                StatusChanged?.Invoke("[Play] Automation stopped.");
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

        private async Task RequestEngineMoveCoreAsync(
            Rectangle bounds,
            bool isWhiteView,
            CancellationToken cancellationToken)
        {
            if (_moveConfirmation.PendingMove != null)
            {
                StatusChanged?.Invoke(
                    $"[Move awaiting confirmation] {_moveConfirmation.PendingMove} has not yet been observed on the board.");
                return;
            }

            string? move = await GetEngineMoveSuggestionAsync(cancellationToken);
            if (move == null)
                return;

            await SendMoveAndWaitForConfirmationAsync(
                move, bounds, isWhiteView, cancellationToken, continueGame: true);
        }

        private async Task<string?> GetEngineMoveSuggestionAsync(CancellationToken cancellationToken)
        {
            int skillLevel = GetSkillLevelForNextTurn();
            if (_board.Checkmate || !_board.HasLegalMoveForTurn())
            {
                StatusChanged?.Invoke(_board.Checkmate
                    ? "[Game over] Checkmate."
                    : "[Game over] The side to move has no legal moves (stalemate).");
                return null;
            }

            StatusChanged?.Invoke($"[Engine] Calculating a move at skill level {skillLevel}/20...");
            string enginePath = await _engine.EnsureEngineInstalledAsync(_settings.EnginePath);
            string fen = _board.GenerateFen();
            StatusChanged?.Invoke($"[FEN Query] {fen}");

            string move = await _engine.GetBestMoveAsync(
                enginePath, fen, _settings.MoveTimeMilliseconds, skillLevel, cancellationToken);
            if (move is "None" or "Error starting engine" or "(none)" or "0000")
            {
                StatusChanged?.Invoke("[Engine] No move is available for this position.");
                return null;
            }

            try
            {
                move = ChessMoveNotation.Normalize(move);
            }
            catch (ArgumentException ex)
            {
                StatusChanged?.Invoke($"[Engine error] The configured engine returned invalid move notation: {ex.Message}");
                return null;
            }

            if (!_board.ValidateMove(
                    8 - (move[1] - '0'), move[0] - 'a',
                    8 - (move[3] - '0'), move[2] - 'a'))
            {
                StatusChanged?.Invoke($"[Engine error] The configured engine returned an illegal move: {move}.");
                return null;
            }

            StatusChanged?.Invoke($"[Engine suggestion] {move} (not clicked).");
            return move;
        }

        private async Task SendMoveAndWaitForConfirmationAsync(
            string move,
            Rectangle bounds,
            bool isWhiteView,
            CancellationToken cancellationToken,
            bool continueGame)
        {
            StatusChanged?.Invoke(
                $"[Board verification] Waiting for the board to stay stable for at least {_settings.StableBoardDurationMilliseconds} ms...");
            if (!await WaitForStableBoardAsync(bounds, cancellationToken))
            {
                StatusChanged?.Invoke(
                    $"[Move blocked] The board did not stay stable for the configured duration within {BoardStabilityTimeout.TotalSeconds + _settings.StableBoardDurationMilliseconds / 1000.0:0.#} seconds. No move was sent.");
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
                await Task.Delay(MoveConfirmationPollInterval, cancellationToken);
                using Bitmap currentBoard = CaptureBoard(bounds);
                string? candidates = _scanner.ScanForStateChanges(
                    currentBoard, isWhiteView, _settings.BrightnessThreshold);
                if (string.IsNullOrEmpty(candidates))
                    continue;

                if (ConfirmEngineMove(candidates))
                {
                    NotifyBoardChanged();
                    StatusChanged?.Invoke($"[Move confirmed] {move} was detected on the board.");
                    if (continueGame && ShouldEngineMove(isWhiteView))
                        await RequestEngineMoveCoreAsync(bounds, isWhiteView, cancellationToken);
                    return;
                }

                _scanner.ResetStateTracking(currentBoard);
            }

            _moveConfirmation.Cancel();
            _scanner.ResetStateTracking(baseline);
            StatusChanged?.Invoke(
                $"[Move not confirmed] {move} was sent, but no matching board change appeared within {MoveConfirmationTimeout.TotalSeconds:0} seconds. The tracked position was not advanced; scanning remains armed to detect a delayed board update.");
        }

        private async Task<bool> WaitForStableBoardAsync(Rectangle bounds, CancellationToken cancellationToken)
        {
            TimeSpan stabilityTimeout = BoardStabilityTimeout +
                TimeSpan.FromMilliseconds(_settings.StableBoardDurationMilliseconds);
            var stabilityTracker = new BoardStabilityTracker(
                requiredStableFrames: 2,
                requiredStableDuration: TimeSpan.FromMilliseconds(_settings.StableBoardDurationMilliseconds));
            DateTime deadline = DateTime.UtcNow + stabilityTimeout;
            int intervalMilliseconds = Math.Max(1, 1000 / _settings.FramesPerSecond);

            while (DateTime.UtcNow < deadline)
            {
                cancellationToken.ThrowIfCancellationRequested();
                using Bitmap currentBoard = CaptureBoard(bounds);
                if (stabilityTracker.AddFrame(
                        _scanner.ExtractGridBrightness(currentBoard),
                        DateTimeOffset.UtcNow))
                    return true;

                await Task.Delay(intervalMilliseconds, cancellationToken);
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

        private bool ShouldEngineMove(bool isWhiteView)
            => ChessSideSelection.ShouldEngineMove(
                _settings.PlayMode, _settings.EngineSide, isWhiteView, _board.Turn);

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
            string[] moves = candidates.Split('|');
            string? move = BoardMoveDetector.FindUniqueLegalMove(moves, IsLegalMove);
            if (move != null)
                return _board.MakeMove(move) ? move : null;

            string? observedMove = BoardMoveDetector.FindUniqueLegalMove(moves, IsLegalMoveIgnoringTurn);
            if (observedMove == null || IsLegalMove(observedMove) ||
                !_board.MakeObservedMove(observedMove))
                return null;

            StatusChanged?.Invoke(
                $"[Turn resynchronized] Detected {observedMove} as a legal move for the other side; tracked turn corrected to {_board.Turn}.");
            return observedMove;
        }

        private bool IsLegalMove(string move)
        {
            return IsLegalMove(move, enforceTurn: true);
        }

        private bool IsLegalMoveIgnoringTurn(string move)
        {
            return IsLegalMove(move, enforceTurn: false);
        }

        private bool IsLegalMove(string move, bool enforceTurn)
        {
            if (move.Length < 4 ||
                move[0] is < 'a' or > 'h' ||
                move[1] is < '1' or > '8' ||
                move[2] is < 'a' or > 'h' ||
                move[3] is < '1' or > '8' ||
                move[3] is < '1' or > '8')
                return false;

            int fromFile = move[0] - 'a';
            int fromRank = 8 - (move[1] - '0');
            int toFile = move[2] - 'a';
            int toRank = 8 - (move[3] - '0');
            return enforceTurn
                ? _board.ValidateMove(fromRank, fromFile, toRank, toFile)
                : _board.ValidateMoveIgnoringTurn(fromRank, fromFile, toRank, toFile);
        }

        private void NotifyBoardChanged()
        {
            BoardChanged?.Invoke(_board.GetDebugBoardString());
        }
    }
}