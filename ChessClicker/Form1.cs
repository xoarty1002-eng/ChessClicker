using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using Timer = System.Windows.Forms.Timer;

namespace ChessClicker
{
    public partial class Form1 : Form
    {
        private Rectangle _activeBoardBounds;
        private Point _topLeft = Point.Empty;
        private Point _bottomRight = Point.Empty;
        private int _calibrationStep = 0;
        private bool _isCalibrating = false;
        private bool _isWhiteView = true;
        private DesktopMouseHook? _desktopMouseHook;
        private ChessClickerSettings _settings = ChessClickerSettings.Default;
        private static readonly string SettingsFilePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "ChessClicker",
            "settings.json");

        private readonly ChessGameController _controller;
        private Timer _timerGameLoop = new();

        public Form1()
        {
            InitializeComponent();
            _controller = new ChessGameController();
            _controller.StatusChanged += Log;
            _controller.BoardChanged += board => positionTextBox.Text = board;

            previewPictureBox.SizeMode = PictureBoxSizeMode.StretchImage;
            positionTextBox.Multiline = true;
            positionTextBox.ReadOnly = true;
            positionTextBox.ScrollBars = ScrollBars.Vertical;

            calibrateButton.Click += btnCalibrate_Click;
            autoCalibrateButton.Click += btnAutoCalibrate_Click;
            stopCalibrationButton.Click += btnStopCalibration_Click;
            scannerButton.Click += btnToggleScanner_Click;
            playMoveButton.Click += btnSuggestMove_ClickAsync;
            settingsButton.Click += btnSettings_Click;
            executeTypedMoveButton.Click += btnExecuteTypedMove_Click;
            moveInputTextBox.KeyDown += MoveInputTextBox_KeyDown;

            InitializeGameLoopTimer();
            LoadSettings();
            LoadStartupConfig();
            UpdatePreviewRateLabel();
        }

        private void InitializeGameLoopTimer()
        {
            _timerGameLoop.Interval = 1000 / _settings.FramesPerSecond;
            _timerGameLoop.Tick += TimerGameLoop_Tick;
        }

        private void LoadSettings()
        {
            if (!File.Exists(SettingsFilePath))
            {
                _controller.UpdateSettings(_settings);
                return;
            }

            try
            {
                _settings = ChessClickerSettings.LoadFromFile(SettingsFilePath);
                _controller.UpdateSettings(_settings);
                _timerGameLoop.Interval = 1000 / _settings.FramesPerSecond;
                Log($"[Settings] Loaded from file. Preview/scanning rate: {_settings.FramesPerSecond} FPS.");
            }
            catch (Exception ex)
            {
                _controller.UpdateSettings(_settings);
                _timerGameLoop.Interval = 1000 / _settings.FramesPerSecond;
                Log($"[Settings Error] Could not load '{SettingsFilePath}': {ex.Message}. Using defaults.");
            }
        }

        private void LoadStartupConfig()
        {
            string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "config.txt");
            if (File.Exists(path))
            {
                try
                {
                    string[] parts = File.ReadAllText(path).Split(',');
                    if (parts.Length == 4)
                    {
                        _activeBoardBounds = new Rectangle(
                            int.Parse(parts[0]),
                            int.Parse(parts[1]),
                            int.Parse(parts[2]),
                            int.Parse(parts[3])
                        );
                        Log("[Startup] Configuration file loaded successfully from disk.");
                        DisplayCroppedPreview();
                    }
                }
                catch
                {
                    Log("[Startup] Configuration load error. Please calibrate coordinates layout profile.");
                }
            }
            else
            {
                Log("[Startup] No configuration text file found. Please run calibration tracking.");
            }
        }

        private void DisplayCroppedPreview()
        {
            try
            {
                if (_activeBoardBounds.Width <= 0 || _activeBoardBounds.Height <= 0)
                {
                    Log($"[Preview Error] Invalid board size: {_activeBoardBounds.Width}x{_activeBoardBounds.Height}px.");
                    return;
                }

                using Bitmap cropped = _controller.CaptureBoard(_activeBoardBounds);
                if (cropped.Width <= 0 || cropped.Height <= 0)
                {
                    Log($"[Preview Error] Captured image has invalid size: {cropped.Width}x{cropped.Height}px.");
                    return;
                }

                previewPictureBox.Image?.Dispose();
                previewPictureBox.Image = (Bitmap)cropped.Clone();
                Log($"[Preview] Displaying calibrated board: {cropped.Width}x{cropped.Height}px.");
            }
            catch (Exception ex)
            {
                Log($"[UI Error] Failed initializing preview snapshot: {ex.Message}");
            }
        }

        // --- AUTOMATED STATE SCANNER TICK LOOP ---
        private async void TimerGameLoop_Tick(object? sender, EventArgs e)
        {
            if (_isCalibrating || _controller.IsBusy) return;

            try
            {
                // 1. Capture ONLY the dedicated chessboard screen real estate box parameters
                using Bitmap croppedBoard = _controller.CaptureBoard(_activeBoardBounds);

                previewPictureBox.Image?.Dispose();
                previewPictureBox.Image = (Bitmap)croppedBoard.Clone();

                // 2. Discover perspective view alignments from the screen pixels automatically
                _isWhiteView = _controller.DetectWhiteView(croppedBoard);
                await _controller.ProcessBoardFrameAsync(croppedBoard, _activeBoardBounds, _isWhiteView);
            }
            catch (Exception ex)
            {
                _timerGameLoop.Stop();
                Log($"[CRITICAL] Background loop auto-suspended: {ex.Message}");
                scannerButton.Text = "Start Scanner Loop";
                UpdatePreviewRateLabel();
            }
        }

        // --- BUTTON TRIGGER: CAPTURE BOARD CORNERS FROM DESKTOP CLICKS ---
        private void btnCalibrate_Click(object? sender, EventArgs e)
        {
            try
            {
                _timerGameLoop.Stop();
                scannerButton.Text = "Start Scanner Loop";
                _calibrationStep = 1;
                _topLeft = Point.Empty;
                _bottomRight = Point.Empty;
                _isCalibrating = true;
                stopCalibrationButton.Enabled = true;
                scannerButton.Enabled = false;
                UpdatePreviewRateLabel();
                EnsureDesktopMouseHook();
                Log("Click the board's top-left outer corner, then its bottom-right outer corner.");
                Log("Calibration clicks are forwarded to the board.");
            }
            catch (Exception ex)
            {
                _isCalibrating = false;
                stopCalibrationButton.Enabled = false;
                scannerButton.Enabled = true;
                UpdatePreviewRateLabel();
                Log($"[Calibration Error] Could not capture mouse clicks: {ex.Message}");
            }
        }

        private async void btnAutoCalibrate_Click(object? sender, EventArgs e)
        {
            if (_controller.IsBusy)
            {
                Log("[Auto-calibration] Wait for the current board operation to finish.");
                return;
            }

            bool wasScanning = _timerGameLoop.Enabled;
            if (wasScanning)
            {
                _timerGameLoop.Stop();
                scannerButton.Text = "Start Scanner Loop";
                UpdatePreviewRateLabel();
            }

            bool wasVisible = Visible;
            try
            {
                Log("[Auto-calibration] Hiding this window and searching the desktop for an 8-by-8 grid...");
                Hide();
                await Task.Delay(200);

                Rectangle virtualScreen = SystemInformation.VirtualScreen;
                using Bitmap screenImage = CaptureVirtualScreen(virtualScreen);
                int[,] grayscale = await Task.Run(() => new ImageScaner().ExtractGrayscale(screenImage));
                int estimatedSize = GetAutoCalibrationBoardSize(virtualScreen.Size);
                Size boardSize = new(estimatedSize, estimatedSize);
                Rectangle? localBounds = await Task.Run(() =>
                    BoardGridCalibrator.FindBestPosition(grayscale, boardSize, positionStep: 4));

                if (localBounds == null)
                {
                    Log("[Auto-calibration Failed] No confident 8-by-8 grid was found. Use manual corner calibration.");
                    return;
                }

                _activeBoardBounds = new Rectangle(
                    virtualScreen.X + localBounds.Value.X,
                    virtualScreen.Y + localBounds.Value.Y,
                    localBounds.Value.Width,
                    localBounds.Value.Height);
                SaveBoardBounds();
                DisplayCroppedPreview();
                Log($"[Auto-calibration] Found board bounds at X={_activeBoardBounds.X}, Y={_activeBoardBounds.Y}, size={_activeBoardBounds.Width}x{_activeBoardBounds.Height}.");
            }
            catch (Exception ex)
            {
                Log($"[Auto-calibration Error] {ex.Message}");
            }
            finally
            {
                if (wasVisible) Show();
                if (wasScanning && !_isCalibrating)
                {
                    _timerGameLoop.Start();
                    scannerButton.Text = "Pause Scanner Loop";
                    UpdatePreviewRateLabel();
                }
            }
        }

        private static Bitmap CaptureVirtualScreen(Rectangle bounds)
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

        private int GetAutoCalibrationBoardSize(Size screenSize)
        {
            int maximum = Math.Min(screenSize.Width, screenSize.Height);
            if (_activeBoardBounds.Width >= 40 &&
                _activeBoardBounds.Width <= maximum &&
                _activeBoardBounds.Height >= 40 &&
                _activeBoardBounds.Height <= maximum)
            {
                return Math.Min(_activeBoardBounds.Width, _activeBoardBounds.Height);
            }

            return Math.Max(40, (int)(maximum * 0.62));
        }

        private void btnStopCalibration_Click(object? sender, EventArgs e)
        {
            if (!_isCalibrating)
            {
                Log("[Calibration] No corner selection is in progress.");
                return;
            }

            _isCalibrating = false;
            _calibrationStep = 0;
            _topLeft = Point.Empty;
            _bottomRight = Point.Empty;
            stopCalibrationButton.Enabled = false;
            scannerButton.Enabled = true;
            UpdatePreviewRateLabel();
            Log("[Calibration] Corner selection stopped; existing board bounds were kept.");
        }

        private void btnSettings_Click(object? sender, EventArgs e)
        {
            using var settingsForm = new SettingsForm(_settings);
            if (settingsForm.ShowDialog(this) != DialogResult.OK) return;

            _settings = settingsForm.Settings;
            _controller.UpdateSettings(_settings);
            _timerGameLoop.Interval = 1000 / _settings.FramesPerSecond;
            UpdatePreviewRateLabel();
            try
            {
                _settings.SaveToFile(SettingsFilePath);
                Log($"[Settings] Saved to '{SettingsFilePath}'.");
            }
            catch (Exception ex)
            {
                Log($"[Settings Error] Could not save '{SettingsFilePath}': {ex.Message}");
            }
            Log($"[Settings] Updated move time={_settings.MoveTimeMilliseconds}ms, skill={_settings.StockfishSkillLevel}, FPS={_settings.FramesPerSecond}, threshold={_settings.BrightnessThreshold}, theme={_settings.BoardTheme}.");
        }

        private void btnExecuteTypedMove_Click(object? sender, EventArgs e)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(moveInputTextBox.Text))
                    throw new ArgumentException("Enter a move in UCI notation, such as e2e4.");

                _ = _controller.ExecuteTypedMoveAsync(moveInputTextBox.Text, _activeBoardBounds, _isWhiteView);
            }
            catch (Exception ex)
            {
                Log($"[Move input] {ex.Message}");
            }
        }

        private void MoveInputTextBox_KeyDown(object? sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                e.Handled = true;
                e.SuppressKeyPress = true;
                btnExecuteTypedMove_Click(sender, e);
            }
        }

        private bool OnDesktopLeftClick(Point point)
        {
            if (InvokeRequired)
            {
                if (!IsDisposed && IsHandleCreated)
                    BeginInvoke((Action)(() => OnDesktopLeftClick(point)));
                return false;
            }

            if (_isCalibrating)
            {
                OnCalibrationClick(point);
                return false;
            }

            if (!_timerGameLoop.Enabled || _controller.IsBusy ||
                !_activeBoardBounds.Contains(point))
                return false;

            string square = ScreenPointToSquare(point);
            _controller.RegisterSquareClick(square, _activeBoardBounds, _isWhiteView);
            return false;
        }

        private void OnCalibrationClick(Point point)
        {
            if (_calibrationStep == 1)
            {
                _topLeft = point;
                _calibrationStep = 2;
                Log($"[Calibration 1/2] Top-left cursor position: X={point.X}, Y={point.Y}.");
                Log("Click the board's bottom-right outer corner.");
            }
            else
            {
                _bottomRight = point;
                Log($"[Calibration 2/2] Bottom-right cursor position: X={point.X}, Y={point.Y}.");
                _isCalibrating = false;
                _calibrationStep = 0;
                stopCalibrationButton.Enabled = false;
                scannerButton.Enabled = true;
                UpdatePreviewRateLabel();
                int x = _topLeft.X;
                int y = _topLeft.Y;
                int width = _bottomRight.X - _topLeft.X;
                int height = _bottomRight.Y - _topLeft.Y;

                if (width <= 0 || height <= 0)
                {
                    Log($"[Calibration Error] The second point must be below and to the right of the first point. Measured width={width}px, height={height}px.");
                    return;
                }

                if (width < 30 || height < 30)
                {
                    Log($"[Calibration Error] Board dimensions are too small: width={width}px, height={height}px. Start calibration again.");
                    return;
                }

                _activeBoardBounds = new Rectangle(x, y, width, height);
                bool saved = SaveBoardBounds();
                DisplayCroppedPreview();
                UpdatePreviewRateLabel();
                if (saved)
                    Log($"[Calibration Saved] Board bounds: X={x}, Y={y}, width={width}px, height={height}px.");
            }
        }

        private bool SaveBoardBounds()
        {
            string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "config.txt");
            try
            {
                File.WriteAllText(path,
                    $"{_activeBoardBounds.X},{_activeBoardBounds.Y},{_activeBoardBounds.Width},{_activeBoardBounds.Height}");
                return true;
            }
            catch (Exception ex)
            {
                Log($"[Calibration Error] Could not save board bounds to '{path}': {ex.Message}");
                return false;
            }
        }

        private void EnsureDesktopMouseHook()
        {
            _desktopMouseHook ??= new DesktopMouseHook(OnDesktopLeftClick);
            _desktopMouseHook.Start();
        }

        private string ScreenPointToSquare(Point point)
        {
            int screenFile = (point.X - _activeBoardBounds.X) * 8 / _activeBoardBounds.Width;
            int screenRank = (point.Y - _activeBoardBounds.Y) * 8 / _activeBoardBounds.Height;
            int file = _isWhiteView ? screenFile : 7 - screenFile;
            int rank = _isWhiteView ? 8 - screenRank : 1 + screenRank;
            return $"{(char)('a' + file)}{rank}";
        }

        // --- BUTTON TRIGGER: START / PAUSE SCAN TIMER LOOP ---
        private void btnToggleScanner_Click(object? sender, EventArgs e)
        {
            if (_activeBoardBounds.Width <= 0 || _activeBoardBounds.Height <= 0)
            {
                MessageBox.Show("Calibrate the board by clicking its top-left and bottom-right corners first.", "Calibration Required", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (_timerGameLoop.Enabled)
            {
                _timerGameLoop.Stop();
                scannerButton.Text = "Start Scanner Loop";
                UpdatePreviewRateLabel();
                Log("Live scanning loop paused.");
            }
            else
            {
                try
                {
                    EnsureDesktopMouseHook();
                }
                catch (Exception ex)
                {
                    Log($"[Mouse tracking unavailable] {ex.Message}");
                }

                _timerGameLoop.Start();
                scannerButton.Text = "Pause Scanner Loop";
                UpdatePreviewRateLabel();
                Log($"Live scanning active ({_settings.FramesPerSecond} FPS). Click a piece, then its destination to register a move.");
            }
        }

        private void UpdatePreviewRateLabel()
        {
            string state = _isCalibrating ? "calibrating" : _timerGameLoop?.Enabled == true ? "running" : "stopped";
            fpsLabel.Text = $"Preview: {_settings.FramesPerSecond} FPS ({state})";
        }

        // --- BUTTON TRIGGER: PLAY STOCKFISH'S RECOMMENDED MOVE ---
        private async void btnSuggestMove_ClickAsync(object? sender, EventArgs e)
        {
            if (_activeBoardBounds.Width <= 0 || _activeBoardBounds.Height <= 0)
            {
                MessageBox.Show("Calibrate the board before requesting a move.", "Calibration Required", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (sender is Button button)
                button.Enabled = false;

            try
            {
                using Bitmap boardImage = _controller.CaptureBoard(_activeBoardBounds);
                _isWhiteView = _controller.DetectWhiteView(boardImage);
                await _controller.RequestEngineMoveAsync(_activeBoardBounds, _isWhiteView);
            }
            catch (Exception ex)
            {
                Log($"[Engine Execution Failure] {ex.Message}");
            }
            finally
            {
                if (sender is Button playButton)
                    playButton.Enabled = true;
            }
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            _desktopMouseHook?.Dispose();
            _timerGameLoop?.Stop();
            base.OnFormClosed(e);
        }
        private void Log(string message)
        {
            if (logTextBox != null)
            {
                logTextBox.AppendText($"[{DateTime.Now:HH:mm:ss}] {message}{Environment.NewLine}");
            }
        }
    }
}