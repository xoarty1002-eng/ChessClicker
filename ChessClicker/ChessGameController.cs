using System;
using System.Collections.Generic;
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
        private string? _pendingClickedMove;
        private DateTimeOffset _pendingClickedMoveAt;

        private static readonly TimeSpan MoveConfirmationTimeout = TimeSpan.FromSeconds(8);
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

        public async Task<Bitmap> CaptureStableBoardAsync(
            Rectangle bounds,
            CancellationToken cancellationToken = default)
        {
            if (!BoardBoundsGeometry.IsSquare(bounds))
                throw new ArgumentException("Board capture bounds must be square.", nameof(bounds));
            if (IsBusy)
                throw new InvalidOperationException("Wait for the current operation before scanning the board.");

            IsBusy = true;
            try
            {
                if (!await WaitForStableBoardAsync(bounds, cancellationToken))
                    throw new TimeoutException("The board did not remain stable long enough to scan.");
                return CaptureBoard(bounds);
            }
            finally
            {
                IsBusy = false;
            }
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
            _pendingClickedMove = null;
            _moveConfirmation.Cancel();
            NotifyBoardChanged();
            StatusChanged?.Invoke($"[Position loaded] {_board.Turn} to move.");
        }

        public bool TryReconstructSingleMoveFromBoard(
            Bitmap boardImage,
            string startingFen,
            bool isWhiteView,
            out string? inferredMove)
        {
            ArgumentNullException.ThrowIfNull(boardImage);
            ArgumentException.ThrowIfNullOrWhiteSpace(startingFen);
            if (IsBusy)
                throw new InvalidOperationException("Cannot reconstruct the position while a move is being processed.");

            ChessBoard startingPosition = new(startingFen);
            if (!PieceAppearanceImageSampler.TryExtractOccupiedSquares(
                    boardImage, startingPosition, isWhiteView, out IReadOnlySet<string> occupiedSquares))
            {
                inferredMove = null;
                StatusChanged?.Invoke(
                    "[Position reconstruction] Piece/empty-square contrast was not clear enough to infer a move safely.");
                return false;
            }

            inferredMove = BoardPositionReconstructor.FindSingleQuietMove(
                startingPosition, occupiedSquares);
            if (inferredMove == null)
                return false;

            ResetPosition(startingFen);
            if (!_board.MakeMove(inferredMove))
                throw new InvalidOperationException($"Could not apply reconstructed move {inferredMove}.");

            NotifyBoardChanged();
            StatusChanged?.Invoke(
                $"[Position reconstructed] Detected the opening move {inferredMove}; {_board.Turn} to move.");
            return true;
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
                    boardImage,
                    isWhiteView,
                    _settings.BrightnessThreshold,
                    _settings.StableBoardDurationMilliseconds);
                if (string.IsNullOrEmpty(candidates))
                {
                    ExpirePendingClickHint();
                    return;
                }

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
                        _moveConfirmation.Cancel();
                        string? observedMove = ApplyDetectedMove(candidates);
                        if (observedMove == null)
                        {
                            _moveConfirmation.Expect(pendingMove);
                            StatusChanged?.Invoke(
                                $"[Move not synchronized] Expected {pendingMove}, but the changed squares did not uniquely confirm a legal move. The tracked position was not changed.");
                            return;
                        }

                        NotifyBoardChanged();
                        StatusChanged?.Invoke(
                            $"[Move confirmed] The board shows {observedMove}, not the expected engine move {pendingMove}; tracked the displayed move.");
                        if (_automationActive && ShouldEngineMove(isWhiteView))
                            await RequestEngineMoveCoreAsync(bounds, isWhiteView, cancellationToken);
                    }
                    return;
                }

                string? appliedMove = ApplyDetectedMove(candidates);
                string? clickedMove = _pendingClickedMove;
                _pendingClickedMove = null;
                if (appliedMove == null)
                {
                    StatusChanged?.Invoke(
                        $"[Ignored board change] Changed squares did not uniquely confirm a legal move for {_board.Turn} to move. Check the board crop and tracked FEN.");
                    return;
                }

                NotifyBoardChanged();
                StatusChanged?.Invoke(clickedMove == null
                    ? $"[Move confirmed] Detected {appliedMove} from the stable board image."
                    : string.Equals(clickedMove, appliedMove, StringComparison.OrdinalIgnoreCase)
                        ? $"[Move confirmed] Clicked move {appliedMove} appeared on the stable board."
                        : $"[Move confirmed] Board shows {appliedMove}, not clicked move {clickedMove}; tracked the displayed move.");
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

        public void RegisterSquareClick(string square)
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

            _pendingClickedMove = move;
            _pendingClickedMoveAt = DateTimeOffset.UtcNow;
            StatusChanged?.Invoke($"[Click observed] Waiting for the board to confirm {move}.");
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

            if (!ShouldEngineMove(isWhiteView))
            {
                StatusChanged?.Invoke(
                    $"[Play] Waiting for {_board.Turn} to move; the engine controls the other side.");
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
            try
            {
                _clicker.ExecuteMoveOnScreen(move, bounds, isWhiteView);
            }
            catch
            {
                _moveConfirmation.Cancel();
                throw;
            }

            StatusChanged?.Invoke($"[Move sent] {move}. Waiting for the board to show the move...");
            DateTime confirmationDeadline = DateTime.UtcNow + MoveConfirmationTimeout;
            int pollIntervalMilliseconds = Math.Max(1, 1000 / _settings.FramesPerSecond);
            while (DateTime.UtcNow < confirmationDeadline)
            {
                // Once clicks are sent, finish verification even if Play is stopped.
                // Otherwise the tracked position can remain behind the visible board.
                await Task.Delay(pollIntervalMilliseconds);
                using Bitmap currentBoard = CaptureBoard(bounds);
                string? candidates = _scanner.ScanForStateChanges(
                    currentBoard,
                    isWhiteView,
                    _settings.BrightnessThreshold,
                    _settings.StableBoardDurationMilliseconds);
                if (string.IsNullOrEmpty(candidates))
                    continue;

                if (ConfirmEngineMove(candidates))
                {
                    NotifyBoardChanged();
                    StatusChanged?.Invoke($"[Move confirmed] {move} was detected on the board.");
                    if (continueGame && _automationActive && ShouldEngineMove(isWhiteView))
                        await RequestEngineMoveCoreAsync(bounds, isWhiteView, cancellationToken);
                    return;
                }

                _moveConfirmation.Cancel();
                string? observedMove = ApplyDetectedMove(candidates);
                if (observedMove != null)
                {
                    NotifyBoardChanged();
                    StatusChanged?.Invoke(
                        $"[Move confirmed] The board shows {observedMove}, not the expected engine move {move}; tracked the displayed move.");
                    if (continueGame && _automationActive && ShouldEngineMove(isWhiteView))
                        await RequestEngineMoveCoreAsync(bounds, isWhiteView, cancellationToken);
                    return;
                }

                _moveConfirmation.Expect(move);
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

            if (Array.Exists(moves, IsLegalMove))
                return null;

            if (!CandidatesContainExactlyTwoSquares(moves))
                return null;

            move = BoardMoveDetector.FindUniqueLegalMove(moves, IsLegalMoveIgnoringTurn);
            return move != null && _board.MakeObservedMove(move) ? move : null;
        }

        private bool IsLegalMove(string move)
        {
            if (move.Length < 4 ||
                move[0] is < 'a' or > 'h' ||
                move[1] is < '1' or > '8' ||
                move[2] is < 'a' or > 'h' ||
                move[3] is < '1' or > '8')
                return false;

            int fromFile = move[0] - 'a';
            int fromRank = 8 - (move[1] - '0');
            int toFile = move[2] - 'a';
            int toRank = 8 - (move[3] - '0');
            return _board.ValidateMove(fromRank, fromFile, toRank, toFile);
        }

        private bool IsLegalMoveIgnoringTurn(string move)
        {
            if (move.Length < 4 ||
                move[0] is < 'a' or > 'h' ||
                move[1] is < '1' or > '8' ||
                move[2] is < 'a' or > 'h' ||
                move[3] is < '1' or > '8')
                return false;

            int fromFile = move[0] - 'a';
            int fromRank = 8 - (move[1] - '0');
            int toFile = move[2] - 'a';
            int toRank = 8 - (move[3] - '0');
            return _board.ValidateMoveIgnoringTurn(fromRank, fromFile, toRank, toFile);
        }

        private static bool CandidatesContainExactlyTwoSquares(IEnumerable<string> moves)
        {
            HashSet<string> squares = new(StringComparer.Ordinal);
            foreach (string move in moves)
            {
                if (move.Length < 4)
                    continue;

                squares.Add(move[..2]);
                squares.Add(move.Substring(2, 2));
                if (squares.Count > 2)
                    return false;
            }

            return squares.Count == 2;
        }

        private void ExpirePendingClickHint()
        {
            if (_pendingClickedMove == null ||
                DateTimeOffset.UtcNow - _pendingClickedMoveAt <
                    TimeSpan.FromMilliseconds(_settings.StableBoardDurationMilliseconds * 4))
                return;

            StatusChanged?.Invoke(
                $"[Click not confirmed] {_pendingClickedMove} did not appear on the board; the tracked position was not advanced.");
            _pendingClickedMove = null;
        }

        private void NotifyBoardChanged()
        {
            BoardChanged?.Invoke(_board.GetDebugBoardString());
        }
    }
}