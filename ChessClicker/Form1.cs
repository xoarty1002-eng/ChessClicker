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

            calibrateButton.Click += btnCalibrate_Click;
            playButton.Click += PlayButton_Click;
            settingsButton.Click += btnSettings_Click;
            KeyDown += Form1_KeyDown;

            _controller.UpdateSettings(_settings);
            LoadSettings();
            LoadBoardBounds();
            _timerGameLoop.Interval = 1000 / _settings.FramesPerSecond;
            _timerGameLoop.Tick += TimerGameLoop_Tick;
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

                _activeBoardBounds = new Rectangle(x, y, width, height);
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
                            _activeBoardBounds = refined;
                            SaveBoardBounds();
                            using Bitmap recalibratedFrame = _controller.CaptureBoard(_activeBoardBounds);
                            _controller.ResetBoardTracking(recalibratedFrame);
                            SetPreview(recalibratedFrame);
                            _isWhiteView = _controller.DetectWhiteView(recalibratedFrame);
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

                _isWhiteView = _controller.DetectWhiteView(currentFrame);
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

            int screenFile = (point.X - _activeBoardBounds.X) * 8 / _activeBoardBounds.Width;
            int screenRank = (point.Y - _activeBoardBounds.Y) * 8 / _activeBoardBounds.Height;
            int file = _isWhiteView ? screenFile : 7 - screenFile;
            int rank = _isWhiteView ? 8 - screenRank : 1 + screenRank;
            _controller.RegisterSquareClick($"{(char)('a' + file)}{rank}", _activeBoardBounds, _isWhiteView);
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

            _activeBoardBounds = new Rectangle(_topLeft.X, _topLeft.Y, width, height);
            SaveBoardBounds();
            RefreshPreview();
            UpdateStatusLabel();
            Log($"[Calibration Saved] Board bounds: {_activeBoardBounds.Width}x{_activeBoardBounds.Height}.");
        }

        private void PlayButton_Click(object? sender, EventArgs e)
        {
            if (_isCalibrating)
            {
                Log("[Play] Finish or cancel manual calibration before starting.");
                return;
            }

            if (_timerGameLoop.Enabled)
            {
                StopPlaying();
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

            try
            {
                EnsureDesktopMouseHook();
                using Bitmap baseline = _controller.CaptureBoard(_activeBoardBounds);
                _isWhiteView = _controller.DetectWhiteView(baseline);
                _controller.ResetBoardTracking(baseline);
                SetPreview(baseline);
                _lastAutoCalibrationAttempt = DateTime.UtcNow;
                _timerGameLoop.Start();
                playButton.Text = "Stop (F2)";
                UpdateStatusLabel();
                Log($"[Play] Scanning continuously at {_settings.FramesPerSecond} FPS. Press F2 to stop.");
            }
            catch (Exception ex)
            {
                Log($"[Play Error] Could not start play: {ex.Message}");
            }
        }

        private void StopPlaying()
        {
            _timerGameLoop.Stop();
            playButton.Text = "Play (F2)";
            UpdateStatusLabel();
            Log("[Play] Stopped.");
        }

        private void Form1_KeyDown(object? sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.F2)
            {
                e.Handled = true;
                e.SuppressKeyPress = true;
                PlayButton_Click(playButton, EventArgs.Empty);
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

        private void btnSettings_Click(object? sender, EventArgs e)
        {
            using var settingsForm = new SettingsForm(_settings);
            if (settingsForm.ShowDialog(this) != DialogResult.OK)
                return;

            ChessClickerSettings updatedSettings = settingsForm.Settings;
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
            UpdateStatusLabel();
            Log($"[Settings] Saved. Preview rate: {_settings.FramesPerSecond} FPS; calibrate while playing: {_settings.CalibrateWhilePlaying}.");
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
            statusLabel.Text = $"Preview: {_settings.FramesPerSecond} FPS | {state} | {DateTime.Now:HH:mm:ss.fff}";
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
