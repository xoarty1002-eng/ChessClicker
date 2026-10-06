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

        private readonly ChessGameController _controller;
        private Timer _timerGameLoop;

        public Form1()
        {
            InitializeComponent();
            _controller = new ChessGameController();
            _controller.StatusChanged += Log;
            _controller.BoardChanged += board => textBox1.Text = board;

            // Configure visual asset behaviors natively
            pictureBox1.SizeMode = PictureBoxSizeMode.StretchImage;

            textBox1.Multiline = true;
            textBox1.ReadOnly = true;
            textBox1.ScrollBars = ScrollBars.Vertical;

            // Ensure button text fields look descriptive on startup
            button2.Text = "Calibrate";
            button1.Text = "Play Move";
            button3.Text = "Settings";

            LoadStartupConfig();
            InitializeGameLoopTimer();
        }

        private void InitializeGameLoopTimer()
        {
            _timerGameLoop = new Timer();
            _timerGameLoop.Interval = 200; // 200ms = 5 frames per second scanning speed
            _timerGameLoop.Tick += TimerGameLoop_Tick;
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

                pictureBox1.Image?.Dispose();
                pictureBox1.Image = (Bitmap)cropped.Clone();
                Log($"[Preview] Displaying calibrated board: {cropped.Width}x{cropped.Height}px.");
            }
            catch (Exception ex)
            {
                Log($"[UI Error] Failed initializing preview snapshot: {ex.Message}");
            }
        }

        // --- THE 5 FRAMES PER SECOND AUTOMATED STATE SCANNER TICK LOOP ---
        private async void TimerGameLoop_Tick(object sender, EventArgs e)
        {
            if (_isCalibrating || _controller.IsBusy) return;

            try
            {
                // 1. Capture ONLY the dedicated chessboard screen real estate box parameters
                using Bitmap croppedBoard = _controller.CaptureBoard(_activeBoardBounds);

                pictureBox1.Image?.Dispose();
                pictureBox1.Image = (Bitmap)croppedBoard.Clone();

                // 2. Discover perspective view alignments from the screen pixels automatically
                _isWhiteView = _controller.DetectWhiteView(croppedBoard);
                await _controller.ProcessBoardFrameAsync(croppedBoard, _activeBoardBounds, _isWhiteView);
            }
            catch (Exception ex)
            {
                _timerGameLoop.Stop();
                Log($"[CRITICAL] Background loop auto-suspended: {ex.Message}");
                button2.Text = "Start Scanner Loop";
            }
        }

        // --- BUTTON TRIGGER: CAPTURE BOARD CORNERS FROM DESKTOP CLICKS ---
        private void btnCalibrate_Click(object sender, EventArgs e)
        {
            try
            {
                _calibrationStep = 1;
                _topLeft = Point.Empty;
                _bottomRight = Point.Empty;
                _isCalibrating = true;
                EnsureDesktopMouseHook();
                Log("Click the board's top-left outer corner, then its bottom-right outer corner.");
                Log("Calibration clicks are forwarded to the board.");
            }
            catch (Exception ex)
            {
                _isCalibrating = false;
                Log($"[Calibration Error] Could not capture mouse clicks: {ex.Message}");
            }
        }

        private bool OnDesktopLeftClick(Point point)
        {
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
                int x = Math.Min(_topLeft.X, _bottomRight.X);
                int y = Math.Min(_topLeft.Y, _bottomRight.Y);
                int width = _bottomRight.X - _topLeft.X;
                int height = _bottomRight.Y - _topLeft.Y;

                if (width <= 0 || height <= 0)
                {
                    Log($"[Calibration Error] Invalid board dimensions: width={width}px, height={height}px. Click top-left first, then bottom-right.");
                    return;
                }

                if (width < 30 || height < 30)
                {
                    Log($"[Calibration Error] Board dimensions are too small: width={width}px, height={height}px. Start calibration again.");
                    return;
                }

                _activeBoardBounds = new Rectangle(x, y, width, height);
                DisplayCroppedPreview();

                try
                {
                    string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "config.txt");
                    File.WriteAllText(path, $"{x},{y},{width},{height}");
                    Log($"[Calibration Saved] Board bounds: X={x}, Y={y}, width={width}px, height={height}px.");
                }
                catch (Exception ex)
                {
                    Log($"[Calibration Error] Could not save coordinates: {ex.Message}");
                }
            }
        }

        private void EnsureDesktopMouseHook()
        {
            _desktopMouseHook ??= new DesktopMouseHook(OnDesktopLeftClick);
            _desktopMouseHook.Start();
        }

        private string ScreenPointToSquare(Point point)
        {
            return BoardMouseCoordinates.ScreenPointToSquare(point, _activeBoardBounds, _isWhiteView);
        }

        // --- BUTTON TRIGGER: START / PAUSE SCAN TIMER LOOP ---
        private void btnToggleScanner_Click(object sender, EventArgs e)
        {
            if (_activeBoardBounds.Width <= 0 || _activeBoardBounds.Height <= 0)
            {
                MessageBox.Show("Calibrate the board by clicking its top-left and bottom-right corners first.", "Calibration Required", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (_timerGameLoop.Enabled)
            {
                _timerGameLoop.Stop();
                ((Button)sender).Text = "Start Scanner Loop";
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
                ((Button)sender).Text = "Pause Scanner Loop";
                Log("Live scanning active (5 FPS). Click a piece, then its destination to register a move.");
            }
        }

        private void btnSettings_Click(object sender, EventArgs e)
        {
            using var settingsForm = new SettingsForm(_settings);
            if (settingsForm.ShowDialog(this) != DialogResult.OK)
                return;

            _settings = settingsForm.Settings;
            _controller.UpdateSettings(_settings);
            Log($"[Settings] Move time: {_settings.MoveTimeMilliseconds} ms; Stockfish skill: {_settings.StockfishSkillLevel}/20.");
        }

        private void btnAutoCalibrate_Click(object sender, EventArgs e)
        {
            if (_activeBoardBounds.Width < 40 || _activeBoardBounds.Height < 40)
            {
                MessageBox.Show("Calibrate an approximate board area first.", "Calibration Required",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            try
            {
                int paddingX = Math.Max(8, _activeBoardBounds.Width / 10);
                int paddingY = Math.Max(8, _activeBoardBounds.Height / 10);
                Rectangle searchBounds = Rectangle.Inflate(_activeBoardBounds, paddingX, paddingY);
                searchBounds = Rectangle.Intersect(searchBounds, SystemInformation.VirtualScreen);
                using Bitmap image = _controller.CaptureBoard(searchBounds);

                int[,] grayscale = new int[image.Height, image.Width];
                for (int y = 0; y < image.Height; y++)
                {
                    for (int x = 0; x < image.Width; x++)
                    {
                        Color color = image.GetPixel(x, y);
                        grayscale[y, x] = (color.R + color.G + color.B) / 3;
                    }
                }

                Rectangle initialBounds = new(
                    _activeBoardBounds.X - searchBounds.X,
                    _activeBoardBounds.Y - searchBounds.Y,
                    _activeBoardBounds.Width,
                    _activeBoardBounds.Height);
                int adjustment = Math.Max(paddingX, paddingY);
                Rectangle? fittedBounds = BoardGridCalibrator.FindBestBounds(grayscale, initialBounds, adjustment);
                if (fittedBounds == null)
                {
                    Log("[Auto-calibration] Could not find a confident 8x8 grid. Existing calibration was kept.");
                    return;
                }

                _activeBoardBounds = new Rectangle(
                    fittedBounds.Value.X + searchBounds.X,
                    fittedBounds.Value.Y + searchBounds.Y,
                    fittedBounds.Value.Width,
                    fittedBounds.Value.Height);
                DisplayCroppedPreview();

                string configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "config.txt");
                File.WriteAllText(configPath,
                    $"{_activeBoardBounds.X},{_activeBoardBounds.Y},{_activeBoardBounds.Width},{_activeBoardBounds.Height}");
                Log($"[Auto-calibration] Fitted an 8x8 grid at X={_activeBoardBounds.X}, Y={_activeBoardBounds.Y}, width={_activeBoardBounds.Width}, height={_activeBoardBounds.Height}.");
            }
            catch (Exception ex)
            {
                Log($"[Auto-calibration error] {ex.Message}");
            }
        }

        // --- BUTTON TRIGGER: PLAY STOCKFISH'S RECOMMENDED MOVE ---
        private async void btnSuggestMove_ClickAsync(object sender, EventArgs e)
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
            textBox1.AppendText($"[{DateTime.Now:HH:mm:ss}] {message}{Environment.NewLine}");
        }
    }
}