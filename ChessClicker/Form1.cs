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


        // Internal instances of system automation layers
        private ImageScaner _scaner = new ImageScaner();
        private ChessBoard _board = new ChessBoard("white");
        private EngineRun _engine = new EngineRun();
        private Timer _timerGameLoop;
        private DesktopClicker _clicker = new DesktopClicker();

        public Form1()
        {
            InitializeComponent();

            // Allow the form to capture key strokes globally before they focus individual controls
            this.KeyPreview = true;
            this.KeyDown += Form1_KeyDown;

            // Configure visual asset behaviors natively
            pictureBox1.SizeMode = PictureBoxSizeMode.StretchImage;

            textBox1.Multiline = true;
            textBox1.ReadOnly = true;
            textBox1.ScrollBars = ScrollBars.Vertical;

            // Ensure button text fields look descriptive on startup
            button2.Text = "Calibrate Layout";
            button1.Text = "Get Engine Move";

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
                string binaryMoveCandidates = _scaner.ScanForStateChanges(croppedBoard, _isWhiteView);

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

        // --- BUTTON TRIGGER: INITIALIZE MOUSE HOVER CAPTURING ---
        private void btnCalibrate_Click(object sender, EventArgs e)
        {
            _isCalibrating = true;
            _calibrationStep = 1;
            _topLeft = Point.Empty;
            _bottomRight = Point.Empty;

            Log("==================================================");
            Log("🎯 Mouse Position Tracking Activated!");
            Log("1. Hover mouse pointer directly over TOP-LEFT corner of the board.");
            Log("2. Press the [SPACEBAR] key to lock the coordinate index.");
            Log("==================================================");
        }

        // --- GLOBAL KEY LISTENER: LOCK MOUSE POSITION COORDINATES ON SPACEBAR ---
        private void Form1_KeyDown(object sender, KeyEventArgs e)
        {
            if (!_isCalibrating) return;

            if (e.KeyCode == Keys.Space)
            {
                e.Handled = true;
                e.SuppressKeyPress = true; // Stop Windows from dinging or clicking background elements

                Point currentMousePos = Cursor.Position;

                if (_calibrationStep == 1)
                {
                    _topLeft = currentMousePos;
                    Log($"[Point 1 Locked] Top-Left location saved: X={_topLeft.X}, Y={_topLeft.Y}");
                    Log("👉 Now move mouse pointer over the BOTTOM-RIGHT corner and hit [SPACEBAR] again.");
                    _calibrationStep = 2;
                }
                else if (_calibrationStep == 2)
                {
                    _bottomRight = currentMousePos;
                    Log($"[Point 2 Locked] Bottom-Right location saved: X={_bottomRight.X}, Y={_bottomRight.Y}");

                    _isCalibrating = false;
                    _calibrationStep = 0;

                    // Math boundary calculations matrix
                    int x = Math.Min(_topLeft.X, _bottomRight.X);
                    int y = Math.Min(_topLeft.Y, _bottomRight.Y);
                    int w = Math.Abs(_topLeft.X - _bottomRight.X);
                    int h = Math.Abs(_topLeft.Y - _bottomRight.Y);

                    if (w < 30 || h < 30)
                    {
                        Log("[ABORTED] Selected box zone is too small to split. Try tracking again.");
                        return;
                    }

                    _activeBoardBounds = new Rectangle(x, y, w, h);

                    // Update PictureBox rendering frame
                    DisplayCroppedPreview();

                    // Write out clean layout parameters to disk configurations
                    try
                    {
                        string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "config.txt");
                        File.WriteAllText(path, $"{x},{y},{w},{h}");
                        Log($"💾 Layout configurations auto-saved to config.txt -> Dimensions: {w}x{h}px");
                    }
                    catch (Exception ex)
                    {
                        Log($"[Error] Configuration save failed: {ex.Message}");
                    }
                }
            }
        }

        // --- BUTTON TRIGGER: START / PAUSE SCAN TIMER LOOP ---
        private void btnToggleScanner_Click(object sender, EventArgs e)
        {
            if (_activeBoardBounds.Width <= 0 || _activeBoardBounds.Height <= 0)
            {
                MessageBox.Show("Please complete the mouse-hover calibration sequence first!", "Calibration Required", MessageBoxButtons.OK, MessageBoxIcon.Warning);
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
            _isEngineThinking = true;
            Log("==================================================");
            Log("🤖 Opponent move registered! Processing optimal response strategy...");

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

                if (!string.IsNullOrEmpty(recommendedMove) && recommendedMove != "None" && recommendedMove != "Error starting engine")
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
        // --- BUTTON TRIGGER: ASYNC STOCKFISH SUGGESTION PROCESSING PIPELINE ---
        private async void btnSuggestMove_ClickAsync(object sender, EventArgs e)
        {
            if (sender is Button btn)
                btn.Enabled = false;
            Log("==================================================");
            Log("🤖 Sending current matrix parameters to Stockfish engine...");
            try
            {
                // Ensure Stockfish binary deployment paths clear cleanly
                string engineExePath = await _engine.EnsureEngineInstalledAsync();// Map active perspective turns configuration mapping rules
                bool isWhiteToMove = _board.Turn == "white";
                string generatedFenString = _board.GenerateFen();
                Log($"[FEN Query]: {generatedFenString}");// Offload heavy calculation processes onto a unique background worker thread Task profile
                string recommendedMove = await Task.Run(() => _engine.GetBestMove(engineExePath, generatedFenString, 1000));
                Log($"✨ STOCKFISH STRATEGY RECOMMENDATION: {recommendedMove}");
                Log("==================================================");
            }
            catch (Exception ex)
            {
                Log($"[Engine Execution Failure]: {ex.Message}");
            }
            finally
            {
                if (sender is Button button1)
                    button1.Enabled = true;
            }
        }
        private void Log(string message)
        {
            textBox1.AppendText($"[{DateTime.Now:HH:mm:ss}] {message}{Environment.NewLine}");
        }
    }
}