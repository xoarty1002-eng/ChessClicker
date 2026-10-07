using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
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
        public event Action? GameEnded;

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
            StopIfGameOver();
        }

        public bool TryReconstructSingleMoveFromBoard(
            Bitmap boardImage,
            string startingFen,
            bool isWhiteView,
            out string? inferredMove,
            out string? reconstructionIssue)
        {
            ArgumentNullException.ThrowIfNull(boardImage);
            ArgumentException.ThrowIfNullOrWhiteSpace(startingFen);
            if (IsBusy)
                throw new InvalidOperationException("Cannot reconstruct the position while a move is being processed.");

            inferredMove = null;
            reconstructionIssue = null;
            string activeStartingFen = startingFen;
            bool correctedStartingTurn =
                BoardPositionReconstructor.TryCorrectStandardStartingTurn(startingFen, out string whiteToMoveFen);
            if (correctedStartingTurn)
                activeStartingFen = whiteToMoveFen;

            ChessBoard startingPosition = new(activeStartingFen);
            if (!PieceAppearanceImageSampler.TryExtractOccupiedSquares(
                    boardImage, startingPosition, isWhiteView, out IReadOnlySet<string> occupiedSquares))
            {
                reconstructionIssue =
                    "The board image did not provide a clear enough distinction between pieces and empty squares.";
                return false;
            }

            inferredMove = BoardPositionReconstructor.FindSingleQuietMove(
                startingPosition, occupiedSquares);
            if (inferredMove == null)
            {
                HashSet<string> configuredOccupiedSquares = new(StringComparer.Ordinal);
                for (char file = 'a'; file <= 'h'; file++)
                    for (char rank = '1'; rank <= '8'; rank++)
                    {
                        string square = $"{file}{rank}";
                        if (startingPosition.GetPieceAt(square) != ' ')
                            configuredOccupiedSquares.Add(square);
                    }

                if (!configuredOccupiedSquares.SetEquals(occupiedSquares))
                    reconstructionIssue =
                        "The observed board differs from the configured starting FEN, but no single legal quiet move could be identified.";
                if (correctedStartingTurn && reconstructionIssue == null)
                {
                    ResetPosition(activeStartingFen);
                    StatusChanged?.Invoke(
                    "[Position corrected] The standard starting layout is visible, so White moves first.");
                    return true;
                }
                return false;
            }

            ResetPosition(activeStartingFen);
            if (!_board.MakeMove(inferredMove))
                throw new InvalidOperationException($"Could not apply reconstructed move {inferredMove}.");

            NotifyBoardChanged();
            StatusChanged?.Invoke(
                $"[Position reconstructed] Detected the opening move {inferredMove}; {_board.Turn} to move.");
            StopIfGameOver();
            return true;
        }

        public bool DetectWhiteView(Bitmap boardImage)
        {
            bool isWhiteView = _scanner.DetectPlayerSideFromImage(boardImage);
            if (_lastDetectedWhiteView != isWhiteView)
            {
                _lastDetectedWhiteView = isWhiteView;
                StatusChanged?.Invoke(isWhiteView
                    ? "[Board orientation] White is at the bottom; files run a-h from left to right."
                    : "[Board orientation] Black is at the bottom; files run h-a from left to right.");
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
                    out IReadOnlyList<(int Rank, int File)> detectedSquares,
                    _settings.BrightnessThreshold,
                    _settings.StableBoardDurationMilliseconds,
                    _board);
                if (string.IsNullOrEmpty(candidates))
                {
                    if (detectedSquares.Count > 0)
                    {
                        StatusChanged?.Invoke(
                            $"[Board detection] Tracked turn: {_board.Turn}; bottom: " +
                            $"{(isWhiteView ? "White" : "Black")}; changed screen cells: " +
                            $"{FormatDetectedSquares(detectedSquares, isWhiteView)}; " +
                            "no move candidates could be generated.");
                    }
                    ExpirePendingClickHint();
                    return;
                }

                string detectionSummary = DescribeBoardDetection(
                    detectedSquares, candidates, isWhiteView);
                StatusChanged?.Invoke($"[Board detection] {detectionSummary}");

                if (BoardImageStillMatchesTrackedPosition(boardImage, isWhiteView))
                {
                    _scanner.ResetStateTracking(boardImage);
                    StatusChanged?.Invoke(
                        "[Move ignored] Square highlights changed, but piece occupancy still matches the tracked position.");
                    return;
                }

                if (_moveConfirmation.PendingMove is string pendingMove)
                {
                    if (ConfirmEngineMove(candidates))
                    {
                        NotifyBoardChanged();
                        StatusChanged?.Invoke($"[Move confirmed] {pendingMove} was detected on the board.");
                        if (StopIfGameOver())
                            return;
                    }
                    else
                    {
                        _scanner.ResetStateTracking(boardImage);
                        _moveConfirmation.Cancel();
                        string? observedMove = ApplyDetectedMove(candidates, boardImage, isWhiteView);
                        if (observedMove == null)
                        {
                            _moveConfirmation.Expect(pendingMove);
                            StatusChanged?.Invoke(
                                $"[Move not synchronized] Expected {pendingMove}, but no unique move matched. {detectionSummary} The tracked position was not changed.");
                            return;
                        }

                        NotifyBoardChanged();
                        StatusChanged?.Invoke(
                            $"[Move confirmed] The board shows {observedMove}, not the expected engine move {pendingMove}; tracked the displayed move.");
                        if (StopIfGameOver())
                            return;
                        if (_automationActive && ShouldEngineMove(isWhiteView))
                            await RequestEngineMoveCoreAsync(bounds, isWhiteView, cancellationToken);
                    }
                    return;
                }

                string? appliedMove = ApplyDetectedMove(candidates, boardImage, isWhiteView);
                string? clickedMove = _pendingClickedMove;
                _pendingClickedMove = null;
                if (appliedMove == null)
                {
                    StatusChanged?.Invoke(
                        $"[Ignored board change] No unique move matched. {detectionSummary}");
                    return;
                }

                NotifyBoardChanged();
                StatusChanged?.Invoke(clickedMove == null
                    ? $"[Move confirmed] Detected {appliedMove} from the stable board image."
                    : string.Equals(clickedMove, appliedMove, StringComparison.OrdinalIgnoreCase)
                        ? $"[Move confirmed] Clicked move {appliedMove} appeared on the stable board."
                        : $"[Move confirmed] Board shows {appliedMove}, not clicked move {clickedMove}; tracked the displayed move.");
                if (StopIfGameOver())
                    return;
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

        public string RegisterObservedMove(string move)
        {
            if (IsBusy)
                throw new InvalidOperationException("Wait for the current operation before registering an opponent move.");

            move = ChessMoveNotation.Normalize(move);
            char piece = _board.GetPieceAt(move[..2]);
            if (piece == ' ')
                throw new InvalidOperationException($"There is no piece on {move[..2]}.");

            bool applied = _board.MakeMove(move) || _board.MakeObservedMove(move);
            if (!applied)
                throw new InvalidOperationException(
                    $"Move {move} is not legal in the tracked position {_board.Turn} to move.");

            _pendingFromSquare = null;
            _pendingClickedMove = null;
            _moveConfirmation.Cancel();
            string mover = char.IsUpper(piece) ? "White" : "Black";
            NotifyBoardChanged();
            StatusChanged?.Invoke(
                $"[Opponent move registered] {mover} played {move}; {_board.Turn} to move.");
            StopIfGameOver();
            return move;
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
                bool engineIsWhite = ChessSideSelection.IsWhiteAtScreenSide(
                    isWhiteView, _settings.EngineSide);
                StatusChanged?.Invoke(
                    $"[Play] Waiting for {_board.Turn} to move; the engine controls " +
                    $"{(engineIsWhite ? "White" : "Black")} ({_settings.EngineSide.ToLowerInvariant()}, " +
                    $"{(isWhiteView ? "White" : "Black")} at bottom).");
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
            if (StopIfGameOver())
                return;

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
            if (StopIfGameOver())
                return null;

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
            Point fromPoint = BoardMouseCoordinates.SquareCenter(move[..2], bounds, isWhiteView);
            Point toPoint = BoardMouseCoordinates.SquareCenter(move.Substring(2, 2), bounds, isWhiteView);
            StatusChanged?.Invoke(
                $"[Click targets] {move[..2]}=({fromPoint.X},{fromPoint.Y}), " +
                $"{move.Substring(2, 2)}=({toPoint.X},{toPoint.Y}); board crop " +
                $"({bounds.X},{bounds.Y},{bounds.Width}x{bounds.Height}), " +
                $"bottom {(isWhiteView ? "White" : "Black")}; tracked {_board.Turn} to move.");
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
                    out IReadOnlyList<(int Rank, int File)> detectedSquares,
                    _settings.BrightnessThreshold,
                    _settings.StableBoardDurationMilliseconds,
                    _board);
                if (string.IsNullOrEmpty(candidates))
                {
                    if (detectedSquares.Count > 0)
                    {
                        StatusChanged?.Invoke(
                            $"[Board detection] Tracked turn: {_board.Turn}; bottom: " +
                            $"{(isWhiteView ? "White" : "Black")}; changed screen cells: " +
                            $"{FormatDetectedSquares(detectedSquares, isWhiteView)}; " +
                            "no move candidates could be generated.");
                    }
                    continue;
                }

                string detectionSummary = DescribeBoardDetection(
                    detectedSquares, candidates, isWhiteView);
                StatusChanged?.Invoke($"[Board detection] {detectionSummary}");

                if (BoardImageStillMatchesTrackedPosition(currentBoard, isWhiteView))
                {
                    _scanner.ResetStateTracking(currentBoard);
                    continue;
                }

                if (ConfirmEngineMove(candidates))
                {
                    NotifyBoardChanged();
                    StatusChanged?.Invoke($"[Move confirmed] {move} was detected on the board.");
                    if (StopIfGameOver())
                        return;
                    if (continueGame && _automationActive && ShouldEngineMove(isWhiteView))
                        await RequestEngineMoveCoreAsync(bounds, isWhiteView, cancellationToken);
                    return;
                }

                _moveConfirmation.Cancel();
                string? observedMove = ApplyDetectedMove(candidates, currentBoard, isWhiteView);
                if (observedMove != null)
                {
                    NotifyBoardChanged();
                    StatusChanged?.Invoke(
                        $"[Move confirmed] The board shows {observedMove}, not the expected engine move {move}; tracked the displayed move.");
                    if (StopIfGameOver())
                        return;
                    if (continueGame && _automationActive && ShouldEngineMove(isWhiteView))
                        await RequestEngineMoveCoreAsync(bounds, isWhiteView, cancellationToken);
                    return;
                }

                _moveConfirmation.Expect(move);
                _scanner.ResetStateTracking(currentBoard);
            }

            _moveConfirmation.Cancel();
            _scanner.ResetStateTracking(baseline);
            using Bitmap finalBoard = CaptureBoard(bounds);
            IReadOnlyList<(int Rank, int File)> changedSquares =
                BoardBrightnessAnalyzer.FindChangedSquares(
                    _scanner.ExtractGridBrightness(baseline),
                    _scanner.ExtractGridBrightness(finalBoard),
                    _settings.BrightnessThreshold);
            string changedSquareList = FormatDetectedSquares(changedSquares, isWhiteView);
            string finalCandidates = BoardMoveDetector.CreateCandidates(
                changedSquares, isWhiteView, _board);
            string timeoutDetectionSummary = string.IsNullOrEmpty(finalCandidates)
                ? "No move candidates could be generated."
                : DescribeBoardDetection(changedSquares, finalCandidates, isWhiteView);
            StatusChanged?.Invoke(
                $"[Move not confirmed] {move} was sent, but no matching move appeared within " +
                $"{MoveConfirmationTimeout.TotalSeconds:0} seconds. Changed crop squares: " +
                $"{(changedSquareList.Length == 0 ? "none" : changedSquareList)}. " +
                $"{timeoutDetectionSummary} Verify the crop and click targets in the log. " +
                $"The tracked position ({_board.Turn} to move) was not advanced.");
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

        private string? ApplyDetectedMove(string candidates, Bitmap boardImage, bool isWhiteView)
        {
            string[] moves = candidates.Split('|');
            string? move = BoardMoveDetector.FindUniqueCheckmatingMove(moves, _board);
            if (move != null)
                return _board.MakeMove(move) || _board.MakeObservedMove(move) ? move : null;

            move = BoardMoveDetector.FindUniqueLegalMove(moves, IsLegalMove);
            if (move != null)
                return _board.MakeMove(move) ? move : null;

            if (!PieceAppearanceImageSampler.TryExtractOccupiedSquares(
                    boardImage, _board, isWhiteView, out IReadOnlySet<string> observedOccupiedSquares))
                return null;

            move = BoardMoveDetector.FindUniqueMoveMatchingOccupiedSquares(
                moves, _board, observedOccupiedSquares);
            if (move == null)
                return null;

            return _board.MakeMove(move) || _board.MakeObservedMove(move) ? move : null;
        }

        private string DescribeBoardDetection(
            IReadOnlyList<(int Rank, int File)> detectedSquares,
            string candidates,
            bool isWhiteView)
        {
            string[] moves = candidates.Split('|', StringSplitOptions.RemoveEmptyEntries);
            string legalForTurn = FormatMoveList(moves.Where(IsLegalMove));
            string legalIgnoringTurn = FormatMoveList(moves.Where(IsLegalMoveIgnoringTurn));
            return $"Tracked turn: {_board.Turn}; bottom: {(isWhiteView ? "White" : "Black")}; " +
                $"changed screen cells: {FormatDetectedSquares(detectedSquares, isWhiteView)}; " +
                $"legal for turn: {legalForTurn}; legal ignoring turn: {legalIgnoringTurn}.";
        }

        private static string FormatDetectedSquares(
            IReadOnlyList<(int Rank, int File)> changedSquares,
            bool isWhiteView)
        {
            if (changedSquares.Count == 0)
                return "none";

            return string.Join(", ", changedSquares.Select(square =>
                $"r{square.Rank + 1}c{square.File + 1}=" +
                BoardMouseCoordinates.ScreenCellToSquare(
                    square.File, square.Rank, isWhiteView)));
        }

        private static string FormatMoveList(IEnumerable<string> candidateMoves)
        {
            string[] moves = candidateMoves
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Take(10)
                .ToArray();
            return moves.Length switch
            {
                0 => "none",
                10 => $"{string.Join(", ", moves.Take(9))} (more...)",
                _ => string.Join(", ", moves)
            };
        }

        private bool BoardImageStillMatchesTrackedPosition(Bitmap boardImage, bool isWhiteView)
        {
            if (!PieceAppearanceImageSampler.TryExtractOccupiedSquares(
                    boardImage, _board, isWhiteView, out IReadOnlySet<string> observedSquares))
                return false;

            HashSet<string> trackedSquares = new(StringComparer.Ordinal);
            for (char file = 'a'; file <= 'h'; file++)
            {
                for (char rank = '1'; rank <= '8'; rank++)
                {
                    string square = $"{file}{rank}";
                    if (_board.GetPieceAt(square) != ' ')
                        trackedSquares.Add(square);
                }
            }

            return trackedSquares.SetEquals(observedSquares);
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

        private bool StopIfGameOver()
        {
            if (!_board.Checkmate && _board.HasLegalMoveForTurn())
                return false;

            StopAutomation();
            StatusChanged?.Invoke(_board.Checkmate
                ? "[Game over] Checkmate; Play stopped."
                : "[Game over] Stalemate; Play stopped.");
            GameEnded?.Invoke();
            return true;
        }
    }
}