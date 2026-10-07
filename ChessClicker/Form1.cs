using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using Timer = System.Windows.Forms.Timer;

namespace ChessClicker
{
    public partial class Form1 : Form
    {
        private static readonly string SettingsFilePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "ChessClicker",
            "settings.json");
        private static readonly TimeSpan LiveCalibrationInterval = TimeSpan.FromSeconds(2);

        private Rectangle _activeBoardBounds;
        private Point _topLeft = Point.Empty;
        private Point _bottomRight = Point.Empty;
        private int _calibrationStep;
        private bool _isCalibrating;
        private bool _isAutoCalibrating;
        private bool _isWhiteView = true;
        private bool _orientationDetected;
        private bool _positionInitialized;
        private bool _startingPositionResetPending;
        private bool _gameEndedAwaitingBoardReset;
        private DateTime _lastAutoCalibrationAttempt;
        private DesktopMouseHook? _desktopMouseHook;
        private ChessClickerSettings _settings = ChessClickerSettings.Default;
        private readonly ChessGameController _controller;
        private readonly Timer _timerGameLoop = new();

        public Form1()
        {
            InitializeComponent();
            _controller = new ChessGameController();
            _controller.StatusChanged += Log;
            _controller.GameEnded += Controller_GameEnded;

            calibrateButton.Click += btnCalibrate_Click;
            playButton.Click += PlayButton_Click;
            settingsButton.Click += btnSettings_Click;
            clickMoveButton.Click += ClickMoveButton_Click;
            registerOpponentMoveButton.Click += RegisterOpponentMoveButton_Click;
            scanFenButton.Click += ScanFenButton_Click;
            KeyDown += Form1_KeyDown;
            moveInputTextBox.KeyDown += MoveInputTextBox_KeyDown;

            _controller.UpdateSettings(_settings);
            LoadSettings();
            LoadBoardBounds();
            _timerGameLoop.Interval = 1000 / _settings.FramesPerSecond;
            _timerGameLoop.Tick += TimerGameLoop_Tick;
            ApplyPreviewSettings();
            UpdateStatusLabel();
        }

        private void LoadSettings()
        {
            if (!File.Exists(SettingsFilePath))
                return;

            try
            {
                _settings = ChessClickerSettings.LoadFromFile(SettingsFilePath);
                _controller.UpdateSettings(_settings);
                Log($"[Settings] Loaded. Preview rate: {_settings.FramesPerSecond} FPS.");
            }
            catch (Exception ex)
            {
                Log($"[Settings Error] Could not load settings: {ex.Message}. Defaults are active.");
            }
        }

        private void LoadBoardBounds()
        {
            string path = GetBoardBoundsPath();
            if (!File.Exists(path))
            {
                Log("[Startup] No saved board calibration. Use Manual calibrate.");
                return;
            }

            try
            {
                string[] values = File.ReadAllText(path).Split(',');
                if (values.Length != 4 ||
                    !int.TryParse(values[0], out int x) ||
                    !int.TryParse(values[1], out int y) ||
                    !int.TryParse(values[2], out int width) ||
                    !int.TryParse(values[3], out int height) ||
                    width < 40 || height < 40)
                    throw new InvalidDataException("Saved board bounds are invalid.");

                _activeBoardBounds = BoardBoundsGeometry.ToSquare(new Rectangle(x, y, width, height));
                if (_activeBoardBounds.Width != width || _activeBoardBounds.Height != height)
                    SaveBoardBounds();
                RefreshPreview();
                Log("[Startup] Loaded saved board calibration.");
            }
            catch (Exception ex)
            {
                Log($"[Calibration Error] Could not load saved board bounds: {ex.Message}");
            }
        }

        private void TimerGameLoop_Tick(object? sender, EventArgs e)
        {
            _ = UpdateBoardFrameAsync();
        }

        private async Task UpdateBoardFrameAsync()
        {
            if (_isCalibrating || _activeBoardBounds.Width < 40 || _activeBoardBounds.Height < 40)
                return;

            try
            {
                using Bitmap currentFrame = _controller.CaptureBoard(_activeBoardBounds);
                EnsureBoardOrientationDetected(currentFrame);
                SetPreview(currentFrame);

                if (_controller.IsBusy || _isAutoCalibrating)
                    return;

                if (_settings.CalibrateWhilePlaying &&
                    DateTime.UtcNow - _lastAutoCalibrationAttempt >= LiveCalibrationInterval)
                {
                    _lastAutoCalibrationAttempt = DateTime.UtcNow;
                    _isAutoCalibrating = true;
                    try
                    {
                        Rectangle? refinedBounds = await FindRefinedBoundsAsync(_activeBoardBounds);
                        if (refinedBounds is Rectangle refined && refined != _activeBoardBounds)
                        {
                            _activeBoardBounds = BoardBoundsGeometry.ToSquare(refined);
                            SaveBoardBounds();
                            using Bitmap recalibratedFrame = _controller.CaptureBoard(_activeBoardBounds);
                            _controller.ResetBoardTracking(recalibratedFrame);
                            SetPreview(recalibratedFrame);
                            await _controller.ProcessBoardFrameAsync(
                                recalibratedFrame, _activeBoardBounds, _isWhiteView);
                            return;
                        }
                    }
                    catch (Exception ex)
                    {
                        Log($"[Live calibration] Could not refine board bounds: {ex.Message}");
                    }
                    finally
                    {
                        _isAutoCalibrating = false;
                    }
                }

                await _controller.ProcessBoardFrameAsync(currentFrame, _activeBoardBounds, _isWhiteView);
            }
            catch (Exception ex)
            {
                StopPlaying();
                Log($"[Play stopped] Board capture failed: {ex.Message}");
            }
        }

        private async Task<Rectangle?> FindRefinedBoundsAsync(Rectangle boardBounds)
        {
            int margin = Math.Max(4, Math.Min(boardBounds.Width, boardBounds.Height) / 20);
            Rectangle searchBounds = Rectangle.Inflate(boardBounds, margin, margin);
            searchBounds = Rectangle.Intersect(searchBounds, SystemInformation.VirtualScreen);
            if (searchBounds.Width < boardBounds.Width || searchBounds.Height < boardBounds.Height)
                return null;

            using Bitmap searchImage = CaptureScreen(searchBounds);
            int[,] grayscale = await Task.Run(() => new ImageScaner().ExtractGrayscale(searchImage));
            Rectangle initialBounds = new(
                boardBounds.X - searchBounds.X,
                boardBounds.Y - searchBounds.Y,
                boardBounds.Width,
                boardBounds.Height);
            Rectangle? fitted = await Task.Run(() =>
                BoardGridCalibrator.FindBestBounds(grayscale, initialBounds, margin));
            if (fitted == null)
                return null;

            return new Rectangle(
                searchBounds.X + fitted.Value.X,
                searchBounds.Y + fitted.Value.Y,
                fitted.Value.Width,
                fitted.Value.Height);
        }

        private static Bitmap CaptureScreen(Rectangle bounds)
        {
            var image = new Bitmap(bounds.Width, bounds.Height);
            try
            {
                using Graphics graphics = Graphics.FromImage(image);
                graphics.CopyFromScreen(bounds.Location, Point.Empty, bounds.Size, CopyPixelOperation.SourceCopy);
                return image;
            }
            catch
            {
                image.Dispose();
                throw;
            }
        }

        private void SetPreview(Bitmap frame)
        {
            previewPictureBox.Image?.Dispose();
            previewPictureBox.Image = (Bitmap)frame.Clone();
            UpdateStatusLabel();
        }

        private void RefreshPreview()
        {
            if (_activeBoardBounds.Width < 40 || _activeBoardBounds.Height < 40)
                return;

            try
            {
                using Bitmap frame = _controller.CaptureBoard(_activeBoardBounds);
                DetectBoardOrientation(frame);
                SetPreview(frame);
            }
            catch (Exception ex)
            {
                Log($"[Preview Error] Could not capture calibrated board: {ex.Message}");
            }
        }

        private void btnCalibrate_Click(object? sender, EventArgs e)
        {
            if (_controller.IsBusy)
            {
                Log("[Calibration] Wait for the current move operation to finish before recalibrating.");
                return;
            }

            if (_timerGameLoop.Enabled)
                StopPlaying();

            try
            {
                _calibrationStep = 1;
                _topLeft = Point.Empty;
                _bottomRight = Point.Empty;
                _isCalibrating = true;
                EnsureDesktopMouseHook();
                UpdateStatusLabel();
                Log("Manual calibration: click the board's top-left corner, then its bottom-right corner. Press Esc to cancel.");
            }
            catch (Exception ex)
            {
                _isCalibrating = false;
                UpdateStatusLabel();
                Log($"[Calibration Error] Could not start manual calibration: {ex.Message}");
            }
        }

        private void OnDesktopLeftClick(Point point)
        {
            if (IsDisposed || !IsHandleCreated)
                return;

            if (InvokeRequired)
            {
                BeginInvoke((Action)(() => OnDesktopLeftClick(point)));
                return;
            }

            if (_isCalibrating)
            {
                OnCalibrationClick(point);
                return;
            }

            if (!_timerGameLoop.Enabled || _controller.IsBusy ||
                !_activeBoardBounds.Contains(point))
                return;

            string square = BoardMouseCoordinates.ScreenPointToSquare(
                point, _activeBoardBounds, _isWhiteView);
            _controller.RegisterSquareClick(square);
        }

        private void OnCalibrationClick(Point point)
        {
            if (_calibrationStep == 1)
            {
                _topLeft = point;
                _calibrationStep = 2;
                Log($"[Calibration 1/2] Top-left: {point.X}, {point.Y}. Click the bottom-right corner.");
                return;
            }

            _bottomRight = point;
            _isCalibrating = false;
            _calibrationStep = 0;
            int width = _bottomRight.X - _topLeft.X;
            int height = _bottomRight.Y - _topLeft.Y;
            if (width < 40 || height < 40)
            {
                Log("[Calibration Error] The bottom-right point must be below and to the right, with board dimensions at least 40x40.");
                UpdateStatusLabel();
                return;
            }

            _activeBoardBounds = BoardBoundsGeometry.ToSquare(
                new Rectangle(_topLeft.X, _topLeft.Y, width, height));
            SaveBoardBounds();
            RefreshPreview();
            UpdateStatusLabel();
            Log($"[Calibration Saved] Board bounds: {_activeBoardBounds.Width}x{_activeBoardBounds.Height}.");
        }

        private async void PlayButton_Click(object? sender, EventArgs e)
        {
            if (_isCalibrating)
            {
                Log("[Play] Finish or cancel manual calibration before starting.");
                return;
            }

            if (_timerGameLoop.Enabled)
            {
                StopPlaying(resetTrackedPosition: true);
                return;
            }

            if (_activeBoardBounds.Width < 40 || _activeBoardBounds.Height < 40)
            {
                MessageBox.Show(
                    "Calibrate the board before starting play.",
                    "Calibration Required",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                return;
            }

            if (_controller.IsBusy)
            {
                Log("[Play] Wait for the current move operation to finish before starting.");
                return;
            }

            try
            {
                EnsureDesktopMouseHook();
                Log($"[Play] Waiting for a stable square board image ({_settings.StableBoardDurationMilliseconds} ms)...");
                using Bitmap baseline = await _controller.CaptureStableBoardAsync(_activeBoardBounds);
                DetectBoardOrientation(baseline);
                if (!_positionInitialized || _startingPositionResetPending)
                {
                    if (!_controller.TryReconstructSingleMoveFromBoard(
                            baseline, _settings.StartingPositionFen, _isWhiteView,
                            out _, out string? reconstructionIssue))
                    {
                        if (_gameEndedAwaitingBoardReset && reconstructionIssue != null)
                        {
                            Log(
                                $"[Play blocked] The game ended, and the visible board does not match " +
                                $"the configured starting position. Reset the board in the chess game, " +
                                $"then press Play. {reconstructionIssue}");
                            return;
                        }

                        if (!_gameEndedAwaitingBoardReset)
                        {
                            _controller.ResetPosition(_settings.StartingPositionFen);
                            Log(reconstructionIssue == null
                                ? "[Position reconstruction] Board matches the configured FEN; no move was inferred."
                                : $"[Position reconstruction] {reconstructionIssue} Use Register opponent move or load the current FEN.");
                        }
                    }

                    _positionInitialized = true;
                    _startingPositionResetPending = false;
                    _gameEndedAwaitingBoardReset = false;
                }

                _controller.ResetBoardTracking(baseline);
                SetPreview(baseline);
                _lastAutoCalibrationAttempt = DateTime.UtcNow;
                _controller.StartAutomation();
                _timerGameLoop.Start();
                playButton.Text = "Stop (F2)";
                UpdateStatusLabel();
                string playDescription = _settings.PlayMode == "Solo"
                    ? $"Solo mode; engine controls the {_settings.EngineSide} side"
                    : "Duo mode; the engine controls both sides";
                Log(
                    $"[Play] {playDescription}. Scanning at {_settings.FramesPerSecond} FPS " +
                    $"({_settings.RecommendedFramesPerSecond} recommended for {_settings.StableBoardDurationMilliseconds} ms stability). Press F2 to stop.");
                _ = _controller.RequestEngineMoveAsync(_activeBoardBounds, _isWhiteView);
            }
            catch (Exception ex)
            {
                Log($"[Play Error] Could not start play: {ex.Message}");
            }
        }

        private void StopPlaying(bool resetTrackedPosition = false)
        {
            bool wasPlaying = _timerGameLoop.Enabled;
            _timerGameLoop.Stop();
            _controller.StopAutomation();
            playButton.Text = "Play (F2)";
            UpdateStatusLabel();
            Log("[Play] Stopped.");
            if (resetTrackedPosition && wasPlaying)
                _ = ResetTrackedPositionAfterStopAsync();
        }

        private async Task ResetTrackedPositionAfterStopAsync()
        {
            try
            {
                while (_controller.IsBusy && !IsDisposed)
                    await Task.Delay(50);

                if (IsDisposed)
                    return;

                _controller.ResetPosition(_settings.StartingPositionFen);
                _positionInitialized = false;
                _startingPositionResetPending = true;
                _gameEndedAwaitingBoardReset = false;
                Log("[Position reset] Loaded the configured starting FEN. The current board will be reconstructed when Play starts.");
            }
            catch (Exception ex)
            {
                Log($"[Position reset error] Could not reset the tracked position: {ex.Message}");
            }
        }

        private void Controller_GameEnded()
        {
            if (IsDisposed || !IsHandleCreated)
                return;
            if (InvokeRequired)
            {
                BeginInvoke((Action)Controller_GameEnded);
                return;
            }

            _timerGameLoop.Stop();
            playButton.Text = "Play (F2)";
            UpdateStatusLabel();
            _ = ResetTrackedPositionAfterGameEndAsync();
        }

        private async Task ResetTrackedPositionAfterGameEndAsync()
        {
            _gameEndedAwaitingBoardReset = true;
            _positionInitialized = false;
            _startingPositionResetPending = true;

            try
            {
                while (_controller.IsBusy && !IsDisposed)
                    await Task.Delay(50);

                if (IsDisposed)
                    return;

                _controller.ResetPositionForNewGame(_settings.StartingPositionFen);
                Log(
                    "[Game reset] Loaded the configured starting FEN for the next game. " +
                    "Reset the visible chess board before pressing Play.");
            }
            catch (Exception ex)
            {
                Log($"[Game reset error] Could not prepare the next game: {ex.Message}");
            }
        }

        private void EnsureBoardOrientationDetected(Bitmap boardImage)
        {
            if (!_orientationDetected)
                DetectBoardOrientation(boardImage);
        }

        private void DetectBoardOrientation(Bitmap boardImage)
        {
            _isWhiteView = _controller.DetectWhiteView(boardImage);
            _orientationDetected = true;
            UpdateStatusLabel();
        }

        private void Form1_KeyDown(object? sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.F2)
            {
                e.Handled = true;
                e.SuppressKeyPress = true;
                PlayButton_Click(playButton, EventArgs.Empty);
            }
            else if (e.KeyCode == Keys.F3)
            {
                e.Handled = true;
                e.SuppressKeyPress = true;
                _ = SubmitTypedMoveAsync();
            }
            else if (e.KeyCode == Keys.Escape && _isCalibrating)
            {
                e.Handled = true;
                _isCalibrating = false;
                _calibrationStep = 0;
                _topLeft = Point.Empty;
                _bottomRight = Point.Empty;
                UpdateStatusLabel();
                Log("[Calibration] Cancelled; the previous board bounds were kept.");
            }
        }

        private void MoveInputTextBox_KeyDown(object? sender, KeyEventArgs e)
        {
            if (e.KeyCode != Keys.Enter)
                return;

            e.Handled = true;
            e.SuppressKeyPress = true;
            _ = SubmitTypedMoveAsync();
        }

        private void ClickMoveButton_Click(object? sender, EventArgs e)
        {
            _ = SubmitTypedMoveAsync();
        }

        private void RegisterOpponentMoveButton_Click(object? sender, EventArgs e)
        {
            _ = RegisterOpponentMoveAsync();
        }

        private async Task RegisterOpponentMoveAsync()
        {
            ParsedChessPositionInput parsedInput;
            try
            {
                parsedInput = ChessPositionInput.Parse(moveInputTextBox.Text);
                if (parsedInput.Kind != ChessPositionInputKind.Move)
                    throw new FormatException("Enter the opponent's move in UCI notation, for example e7e5.");
            }
            catch (Exception ex)
            {
                Log($"[Opponent move] {ex.Message}");
                return;
            }

            if (_controller.IsBusy)
            {
                Log("[Opponent move] Wait for the current operation to finish.");
                return;
            }
            if (_activeBoardBounds.Width < 40 || _activeBoardBounds.Height < 40)
            {
                Log("[Opponent move] Calibrate the board before registering a move.");
                return;
            }

            try
            {
                if (!_orientationDetected)
                {
                    using Bitmap image = _controller.CaptureBoard(_activeBoardBounds);
                    DetectBoardOrientation(image);
                }

                if (!_positionInitialized || _startingPositionResetPending)
                {
                    string startFen = BoardPositionReconstructor.TryCorrectStandardStartingTurn(
                        _settings.StartingPositionFen, out string correctedFen)
                        ? correctedFen
                        : _settings.StartingPositionFen;
                    _controller.ResetPosition(startFen);
                    _positionInitialized = true;
                    _startingPositionResetPending = false;
                }

                using Bitmap currentBoard = await _controller.CaptureStableBoardAsync(_activeBoardBounds);
                string registeredMove = _controller.RegisterObservedMove(parsedInput.Value);
                _controller.ResetBoardTracking(currentBoard);
                SetPreview(currentBoard);
                moveInputTextBox.Clear();
                if (_timerGameLoop.Enabled)
                    await _controller.RequestEngineMoveAsync(_activeBoardBounds, _isWhiteView);
                Log($"[Opponent move] Registered {registeredMove}; the detected mover is based on its source piece.");
            }
            catch (Exception ex)
            {
                Log($"[Opponent move error] {ex.Message}");
            }
        }

        private async Task SubmitTypedMoveAsync()
        {
            ParsedChessPositionInput parsedInput;
            try
            {
                parsedInput = ChessPositionInput.Parse(moveInputTextBox.Text);
            }
            catch (Exception ex)
            {
                Log($"[Input] {ex.Message}");
                return;
            }

            if (_controller.IsBusy)
            {
                Log("[Input] Wait for the current operation to finish.");
                return;
            }

            if (parsedInput.Kind == ChessPositionInputKind.Fen)
            {
                await LoadFenAndSuggestAsync(parsedInput.Value);
                return;
            }

            if (_activeBoardBounds.Width < 40 || _activeBoardBounds.Height < 40)
            {
                Log("[Move input] Calibrate the board before sending a move.");
                return;
            }

            try
            {
                if (!_orientationDetected)
                {
                    using Bitmap boardImage = _controller.CaptureBoard(_activeBoardBounds);
                    DetectBoardOrientation(boardImage);
                }

                if (!_positionInitialized || _startingPositionResetPending)
                {
                    _controller.ResetPosition(_settings.StartingPositionFen);
                    _positionInitialized = true;
                    _startingPositionResetPending = false;
                }

                await _controller.ExecuteTypedMoveAsync(
                    parsedInput.Value, _activeBoardBounds, _isWhiteView);
            }
            catch (Exception ex)
            {
                Log($"[Move input] {ex.Message}");
            }
        }

        private async Task LoadFenAndSuggestAsync(string fen)
        {
            try
            {
                if (_timerGameLoop.Enabled)
                    StopPlaying();

                _controller.ResetPosition(fen);
                _positionInitialized = true;
                _startingPositionResetPending = false;
                if (_activeBoardBounds.Width >= 40 && _activeBoardBounds.Height >= 40)
                {
                    using Bitmap frame = await _controller.CaptureStableBoardAsync(_activeBoardBounds);
                    _controller.ResetBoardTracking(frame);
                    SetPreview(frame);
                }

                string? suggestion = await _controller.GetEngineSuggestionAsync();
                if (suggestion != null)
                    Log($"[FEN suggestion] {suggestion}. This is a suggestion; no board click was sent.");
            }
            catch (Exception ex)
            {
                Log($"[FEN Error] {ex.Message}");
            }
        }

        private async void ScanFenButton_Click(object? sender, EventArgs e)
        {
            if (_controller.IsBusy)
            {
                Log("[FEN scan] Wait for the current operation to finish.");
                return;
            }
            if (_activeBoardBounds.Width < 40 || _activeBoardBounds.Height < 40)
            {
                Log("[FEN scan] Calibrate the board before scanning piece appearances.");
                return;
            }

            try
            {
                if (string.IsNullOrWhiteSpace(moveInputTextBox.Text))
                {
                    Log("[FEN scan] Enter the exact FEN for the currently displayed board. A starting FEN is not assumed.");
                    return;
                }

                ParsedChessPositionInput input = ChessPositionInput.Parse(moveInputTextBox.Text);
                if (input.Kind != ChessPositionInputKind.Fen)
                {
                    Log("[FEN scan] Enter the exact current FEN in the input box first.");
                    return;
                }

                Log($"[FEN scan] Waiting for a square board image stable for {_settings.StableBoardDurationMilliseconds} ms...");
                using Bitmap image = await _controller.CaptureStableBoardAsync(_activeBoardBounds);
                SetPreview(image);
                DetectBoardOrientation(image);
                IReadOnlyList<PieceAppearanceSample> samples =
                    PieceAppearanceImageSampler.ExtractSamples(image, input.Value, _isWhiteView);
                PieceAppearanceReport report = PieceAppearanceAnalyzer.Analyze(
                    samples, _settings.MinimumPieceSignatureSeparationPercent);

                if (report.MissingPieces.Count > 0)
                {
                    string missing = string.Join(", ", report.MissingPieces.Select(FormatPieceLabel));
                    Log($"[FEN scan incomplete] Position lacks samples for: {missing}. Use a position containing both colors of all six piece types.");
                    return;
                }

                string first = FormatPieceLabel(report.FirstClosestPiece);
                string second = FormatPieceLabel(report.SecondClosestPiece);
                Log(report.IsSeparable
                    ? $"[FEN scan passed] All 12 piece classes are represented. Closest contrast/shape signatures: {first} vs {second}, {report.MinimumSeparationPercent:0.0}% (minimum {_settings.MinimumPieceSignatureSeparationPercent}%)."
                    : $"[FEN scan failed] {first} and {second} signatures are only {report.MinimumSeparationPercent:0.0}% apart; minimum is {_settings.MinimumPieceSignatureSeparationPercent}%. Adjust calibration, theme, or the settings threshold.");
                foreach ((char piece, int count) in report.SampleCounts.OrderBy(pair => pair.Key))
                    Log($"[FEN scan sample] {FormatPieceLabel(piece)}: {count} figure(s).");
            }
            catch (Exception ex)
            {
                Log($"[FEN scan error] {ex.Message}");
            }
        }

        private static string FormatPieceLabel(char piece)
        {
            string color = char.IsUpper(piece) ? "White" : "Black";
            string name = char.ToUpperInvariant(piece) switch
            {
                'K' => "King",
                'Q' => "Queen",
                'R' => "Rook",
                'B' => "Bishop",
                'N' => "Knight",
                'P' => "Pawn",
                _ => "Unknown"
            };
            return $"{color} {name}";
        }

        private void btnSettings_Click(object? sender, EventArgs e)
        {
            if (_controller.IsBusy)
            {
                Log("[Settings] Wait for the current move operation to finish before changing settings.");
                return;
            }

            using var settingsForm = new SettingsForm(_settings);
            if (settingsForm.ShowDialog(this) != DialogResult.OK)
                return;

            ChessClickerSettings updatedSettings;
            try
            {
                updatedSettings = settingsForm.Settings;
            }
            catch (Exception ex)
            {
                Log($"[Settings Error] {ex.Message}");
                MessageBox.Show(ex.Message, "Invalid Settings", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            bool startingPositionChanged =
                !string.Equals(_settings.StartingPositionFen, updatedSettings.StartingPositionFen, StringComparison.Ordinal);
            bool engineSideChanged =
                !string.Equals(_settings.EngineSide, updatedSettings.EngineSide, StringComparison.Ordinal);
            bool isPlaying = _timerGameLoop.Enabled;
            try
            {
                updatedSettings.SaveToFile(SettingsFilePath);
            }
            catch (Exception ex)
            {
                Log($"[Settings Error] Could not save settings: {ex.Message}");
                return;
            }

            _settings = updatedSettings;
            _controller.UpdateSettings(_settings);
            _timerGameLoop.Interval = 1000 / _settings.FramesPerSecond;
            ApplyPreviewSettings();
            UpdateStatusLabel();
            if (startingPositionChanged || engineSideChanged)
            {
                _startingPositionResetPending = true;
                if (isPlaying)
                {
                    Log("[Settings] New position/engine side will be loaded when Play is started again.");
                }
                else
                {
                    Log("[Settings] The configured FEN and active color will load when Play starts.");
                }
            }

            Log(
                $"[Settings] Saved. Preview rate: {_settings.FramesPerSecond} FPS; " +
                $"smoothing: {_settings.PreviewSmoothingPercent}%; " +
                $"calibrate while playing: {_settings.CalibrateWhilePlaying}; " +
                $"process unique legal move on turn mismatch: {_settings.ProcessPossibleLegalTurn}.");
        }

        private void ApplyPreviewSettings()
        {
            previewPictureBox.SmoothingPercent = _settings.PreviewSmoothingPercent;
        }

        private void EnsureDesktopMouseHook()
        {
            _desktopMouseHook ??= new DesktopMouseHook(point =>
            {
                OnDesktopLeftClick(point);
                return false;
            });
            _desktopMouseHook.Start();
        }

        private void UpdateStatusLabel()
        {
            string state = _isCalibrating
                ? "calibrating"
                : _timerGameLoop.Enabled ? "playing" : "stopped";
            string bottomSide = !_orientationDetected
                ? "Bottom: unknown"
                : $"Bottom: {(_isWhiteView ? "White" : "Black")}";
            statusLabel.Text = $"{bottomSide} | {_settings.FramesPerSecond} FPS | {state}";
        }

        private static string GetBoardBoundsPath() =>
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "config.txt");

        private void SaveBoardBounds()
        {
            string path = GetBoardBoundsPath();
            try
            {
                File.WriteAllText(path,
                    $"{_activeBoardBounds.X},{_activeBoardBounds.Y},{_activeBoardBounds.Width},{_activeBoardBounds.Height}");
            }
            catch (Exception ex)
            {
                Log($"[Calibration Error] Could not save board bounds: {ex.Message}");
            }
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            _desktopMouseHook?.Dispose();
            _timerGameLoop.Stop();
            _timerGameLoop.Dispose();
            _controller.StopAutomation();
            base.OnFormClosed(e);
        }

        private void Log(string message)
        {
            if (IsDisposed || !IsHandleCreated)
                return;
            if (InvokeRequired)
            {
                BeginInvoke((Action)(() => Log(message)));
                return;
            }

            logTextBox.AppendText($"[{DateTime.Now:HH:mm:ss}] {message}{Environment.NewLine}");
        }
    }
}
