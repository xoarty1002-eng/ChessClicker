using System;
using System.Drawing;
using System.IO;
using System.Threading.Tasks;
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
        private bool _isEngineThinking = false; 
        private bool _isWhiteView = true;
        private CalibrationMouseHook? _calibrationMouseHook;


        // Internal instances of system automation layers
        private ImageScaner _scaner = new ImageScaner();
        private ChessBoard _board = new ChessBoard("white");
        private EngineRun _engine = new EngineRun();
        private Timer _timerGameLoop;
        private DesktopClicker _clicker = new DesktopClicker();

        public Form1()
        {
            InitializeComponent();

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
                Bitmap cropped = _scaner.CaptureBoardRegion(_activeBoardBounds);
                pictureBox1.Image?.Dispose();
                pictureBox1.Image = cropped;
            }
            catch (Exception ex)
            {
                Log($"[UI Error] Failed initializing preview snapshot: {ex.Message}");
            }
        }

        // --- THE 5 FRAMES PER SECOND AUTOMATED STATE SCANNER TICK LOOP ---
        private void TimerGameLoop_Tick(object sender, EventArgs e)
        {
            if (_isCalibrating || _isEngineThinking) return;

            try
            {
                // 1. Capture ONLY the dedicated chessboard screen real estate box parameters
                using Bitmap croppedBoard = _scaner.CaptureBoardRegion(_activeBoardBounds);

                pictureBox1.Image?.Dispose();
                pictureBox1.Image = (Bitmap)croppedBoard.Clone();

                // 2. Discover perspective view alignments from the screen pixels automatically
                _isWhiteView = _scaner.DetectPlayerSideFromImage(croppedBoard);

                // 3. Scan for layout modifications and extract binary orientation strings
                string? binaryMoveCandidates = _scaner.ScanForStateChanges(croppedBoard, _isWhiteView);

                if (!string.IsNullOrEmpty(binaryMoveCandidates))
                {
                    // Split apart move combinations (e.g., "e2e4|e4e2")
                    string[] possibilities = binaryMoveCandidates.Split('|');
                    if (possibilities.Length != 2)
                        return;

                    string moveDirectionA = possibilities[0];
                    string moveDirectionB = possibilities[1];

                    // 4. Test directional movements through our internal matrix validation layer rules
                    bool moveApplied = false;
                    if (_board.MakeMove(moveDirectionA))
                    {
                        Log($"[State Modified] Automatically processed legal move: {moveDirectionA}");
                        moveApplied = true;
                    }
                    else if (_board.MakeMove(moveDirectionB))
                    {
                        Log($"[State Modified] Automatically processed legal move: {moveDirectionB}");
                        moveApplied = true;
                    }

                    if (moveApplied)
                    {
                        textBox1.Text = _board.GetDebugBoardString();
                        _ = AutoRequestEngineMoveAsync();
                    }
                }
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
                _calibrationMouseHook ??= new CalibrationMouseHook(OnCalibrationClick);
                _calibrationMouseHook.Start();
                Log("Click the board's top-left outer corner, then its bottom-right outer corner.");
                Log("Calibration clicks are intercepted so they do not move a chess piece.");
            }
            catch (Exception ex)
            {
                _isCalibrating = false;
                Log($"[Calibration Error] Could not capture mouse clicks: {ex.Message}");
            }
        }

        private void OnCalibrationClick(Point point)
        {
            if (!_isCalibrating) return;

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
            _calibrationMouseHook?.Stop();

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
                _timerGameLoop.Start();
                ((Button)sender).Text = "Pause Scanner Loop";
                Log("Live scanning loop active (Speed: 5 FPS). Tracking frame changes...");
            }
        }

        // --- THE FULLY AUTONOMOUS BACKGROUND EVALUATION & CLICK PIPELINE ---
        private async Task AutoRequestEngineMoveAsync()
        {
            if (_isEngineThinking || _activeBoardBounds.Width <= 0 || _activeBoardBounds.Height <= 0)
                return;

            _isEngineThinking = true;
            Log("==================================================");
            Log("🤖 Asking Stockfish for a move...");

            try
            {
                string engineExePath = await _engine.EnsureEngineInstalledAsync();

                string generatedFenString = _board.GenerateFen();
                Log($"[FEN Query]: {generatedFenString}");

                // Calculate response via background threads
                string recommendedMove = await Task.Run(() =>
                    _engine.GetBestMove(engineExePath, generatedFenString, 1000)
                );

                Log($"✨ STOCKFISH RECOMMENDATION: {recommendedMove}");

                if (recommendedMove.Length >= 4 && recommendedMove != "None" &&
                    recommendedMove != "Error starting engine" && recommendedMove != "(none)" &&
                    recommendedMove != "0000")
                {
                    Log($"🎯 Executing move click injection sequence: {recommendedMove}");

                    // 1. INJECT PHYSICAL CLICKS TO SIMULATE MOVE ON DESKTOP
                    _clicker.ExecuteMoveOnScreen(recommendedMove, _activeBoardBounds, _isWhiteView);
                    // 2. COMMIT THE SUGGESTED PIECE MOVE INTO INTERNAL BOARD MATRIX MEMORY
                    if (_board.MakeMove(recommendedMove))
                    {
                        Log($"✅ Successfully committed suggested move into internal memory array.");

                        // Force update text layout diagnostics fields
                        this.Invoke((MethodInvoker)delegate {
                            textBox1.Text = _board.GetDebugBoardString();
                        });
                    }
                }
                Log("==================================================");
            }
            catch (Exception ex)
            {
                Log($"[Automation Pipeline Failure]: {ex.Message}");
            }
            finally
            {
                _isEngineThinking = false;
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
                using Bitmap boardImage = _scaner.CaptureBoardRegion(_activeBoardBounds);
                _isWhiteView = _scaner.DetectPlayerSideFromImage(boardImage);
                await AutoRequestEngineMoveAsync();
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
            _calibrationMouseHook?.Dispose();
            _timerGameLoop?.Stop();
            base.OnFormClosed(e);
        }
        private void Log(string message)
        {
            textBox1.AppendText($"[{DateTime.Now:HH:mm:ss}] {message}{Environment.NewLine}");
        }
    }
}