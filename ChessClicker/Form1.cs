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
                if (_activeBoardBounds.Width <= 0 || _activeBoardBounds.Height <= 0) return;

                // Fetch a quick, clean capture snapshot to render into the UI image box layout context
                Bitmap cropped = _controller.CaptureBoard(_activeBoardBounds);
                pictureBox1.Image?.Dispose();
                pictureBox1.Image = cropped;
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
                Log("Calibration clicks are intercepted so they do not move a chess piece.");
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
                return true;
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
                Log($"[Top-left captured] X={point.X}, Y={point.Y}. Click the bottom-right outer corner.");
                return;
            }

            _bottomRight = point;
            _isCalibrating = false;
            _calibrationStep = 0;

            int x = Math.Min(_topLeft.X, _bottomRight.X);
            int y = Math.Min(_topLeft.Y, _bottomRight.Y);
            int width = Math.Abs(_topLeft.X - _bottomRight.X);
            int height = Math.Abs(_topLeft.Y - _bottomRight.Y);

            if (width < 30 || height < 30)
            {
                Log("[Calibration Aborted] Board area is too small. Start calibration again.");
                return;
            }

            _activeBoardBounds = new Rectangle(x, y, width, height);
            DisplayCroppedPreview();

            try
            {
                string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "config.txt");
                File.WriteAllText(path, $"{x},{y},{width},{height}");
                Log($"[Calibration Saved] Board bounds: {width}x{height}px.");
            }
            catch (Exception ex)
            {
                Log($"[Calibration Error] Could not save coordinates: {ex.Message}");
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