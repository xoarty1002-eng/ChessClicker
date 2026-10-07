using System;
using System.Drawing;
using System.Windows.Forms;

namespace ChessClicker
{
    internal sealed class SettingsForm : Form
    {
        private readonly NumericUpDown _moveTimeInput;
        private readonly NumericUpDown _skillLevelInput;
        private readonly NumericUpDown _framesPerSecondInput;
        private readonly NumericUpDown _brightnessThresholdInput;
        private readonly NumericUpDown _previewSmoothingInput;
        private readonly NumericUpDown _stableBoardDurationInput;
        private readonly NumericUpDown _pieceSignatureSeparationInput;
        private readonly NumericUpDown _ambiguousMoveStableDurationInput;
        private readonly ComboBox _boardThemeInput;
        private readonly ComboBox _playModeInput;
        private readonly ComboBox _engineSideInput;
        private readonly CheckBox _randomSkillInput;
        private readonly CheckBox _calibrateWhilePlayingInput;
        private readonly CheckBox _processPossibleLegalTurnInput;
        private readonly NumericUpDown _randomSkillIntervalInput;
        private readonly TextBox _startingPositionFenInput;
        private readonly TextBox _enginePathInput;

        public ChessClickerSettings Settings => new(
            (int)_moveTimeInput.Value,
            (int)_skillLevelInput.Value,
            (int)_framesPerSecondInput.Value,
            (int)_brightnessThresholdInput.Value,
            (string)_boardThemeInput.SelectedItem!,
            _randomSkillInput.Checked,
            (int)_randomSkillIntervalInput.Value,
            _calibrateWhilePlayingInput.Checked,
            (int)_previewSmoothingInput.Value,
            _startingPositionFenInput.Text,
            _enginePathInput.Text,
            (string)_playModeInput.SelectedItem!,
            (string)_engineSideInput.SelectedItem!,
            (int)_stableBoardDurationInput.Value,
            (int)_pieceSignatureSeparationInput.Value,
            _processPossibleLegalTurnInput.Checked,
            (int)_ambiguousMoveStableDurationInput.Value);

        public SettingsForm(ChessClickerSettings settings)
        {
            ArgumentNullException.ThrowIfNull(settings);

            Text = "Settings";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterParent;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;
            ClientSize = new Size(560, 830);

            var layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 18,
                Padding = new Padding(12)
            };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 62));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 38));
            for (int row = 0; row < 15; row++)
                layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 72));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 54));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));

            _moveTimeInput = CreateNumber(settings.MoveTimeMilliseconds, 100, 10000, 100);
            _skillLevelInput = CreateNumber(settings.StockfishSkillLevel, 0, 20);
            _framesPerSecondInput = CreateNumber(settings.FramesPerSecond, 1, 30);
            _brightnessThresholdInput = CreateNumber(settings.BrightnessThreshold, 1, 100);
            _previewSmoothingInput = CreateNumber(settings.PreviewSmoothingPercent, 0, 100, 5);
            _stableBoardDurationInput = CreateNumber(settings.StableBoardDurationMilliseconds, 500, 10000, 100);
            _pieceSignatureSeparationInput =
                CreateNumber(settings.MinimumPieceSignatureSeparationPercent, 1, 100);
            _ambiguousMoveStableDurationInput =
                CreateNumber(settings.AmbiguousMoveStableDurationMilliseconds, 0, 10000, 100);
            _boardThemeInput = new ComboBox
            {
                Dock = DockStyle.Fill,
                DropDownStyle = ComboBoxStyle.DropDownList
            };
            _boardThemeInput.Items.AddRange(["Green", "Brown", "Blue", "Gray"]);
            _boardThemeInput.SelectedItem = settings.BoardTheme;
            _playModeInput = CreateComboBox(["Solo", "Duo"], settings.PlayMode);
            _engineSideInput = CreateComboBox(["Top", "Bottom"], settings.EngineSide);
            _randomSkillInput = new CheckBox
            {
                Text = "Random level every N turns",
                Checked = settings.RandomizeStockfishSkill,
                AutoSize = true,
                Anchor = AnchorStyles.Left
            };
            _randomSkillIntervalInput = CreateNumber(settings.RandomSkillIntervalTurns, 1, 100);
            _randomSkillInput.CheckedChanged += (_, _) =>
                _randomSkillIntervalInput.Enabled = _randomSkillInput.Checked;
            _randomSkillIntervalInput.Enabled = _randomSkillInput.Checked;

            AddSetting(layout, "Thinking time per move (ms)", _moveTimeInput, 0);
            AddSetting(layout, "Engine skill (Stockfish, 0-20)", _skillLevelInput, 1);
            AddSetting(layout, "Live preview / scan FPS", _framesPerSecondInput, 2);
            AddSetting(layout, "Square brightness sensitivity", _brightnessThresholdInput, 3);
            AddSetting(layout, "Board theme", _boardThemeInput, 4);
            layout.Controls.Add(_randomSkillInput, 0, 5);
            layout.Controls.Add(_randomSkillIntervalInput, 1, 5);
            _calibrateWhilePlayingInput = new CheckBox
            {
                Text = "Calibrate board crop while playing",
                Checked = settings.CalibrateWhilePlaying,
                AutoSize = true,
                Anchor = AnchorStyles.Left
            };
            layout.Controls.Add(_calibrateWhilePlayingInput, 0, 6);
            layout.SetColumnSpan(_calibrateWhilePlayingInput, 2);
            AddSetting(layout, "Preview smoothing (0–100%)", _previewSmoothingInput, 7);
            AddSetting(layout, "Board stable before click (ms)", _stableBoardDurationInput, 8);
            AddSetting(layout, "Play mode (Solo/Duo)", _playModeInput, 9);
            AddSetting(layout, "Engine side in Solo mode", _engineSideInput, 10);
            _enginePathInput = new TextBox
            {
                Text = settings.EnginePath,
                Dock = DockStyle.Fill
            };
            var enginePathPanel = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 1
            };
            enginePathPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            enginePathPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 72));
            var browseEngineButton = new Button { Text = "Browse...", Dock = DockStyle.Fill };
            browseEngineButton.Click += BrowseEngineButton_Click;
            enginePathPanel.Controls.Add(_enginePathInput, 0, 0);
            enginePathPanel.Controls.Add(browseEngineButton, 1, 0);
            AddSetting(layout, "UCI engine executable (blank = auto Stockfish)", enginePathPanel, 11);
            _startingPositionFenInput = new TextBox
            {
                Text = settings.StartingPositionFen,
                Dock = DockStyle.Fill,
                Multiline = true,
                WordWrap = false,
                ScrollBars = ScrollBars.Horizontal
            };
            AddSetting(layout, "Starting position FEN", _startingPositionFenInput, 12);
            AddSetting(layout, "Minimum figure signature separation (%)", _pieceSignatureSeparationInput, 13);
            AddSetting(
                layout,
                "Stable wait when multiple legal moves (ms)",
                _ambiguousMoveStableDurationInput,
                14);
            _processPossibleLegalTurnInput = new CheckBox
            {
                Text = "Process a unique legal move if tracked turn is wrong",
                Checked = settings.ProcessPossibleLegalTurn,
                AutoSize = true,
                Anchor = AnchorStyles.Left
            };
            layout.Controls.Add(_processPossibleLegalTurnInput, 0, 15);
            layout.SetColumnSpan(_processPossibleLegalTurnInput, 2);

            var note = new Label
            {
                Text = "Random levels are selected from 0-20 at the chosen turn interval. Use this only on private analysis or study boards; this app does not support fair-play detection evasion.",
                AutoSize = true,
                Dock = DockStyle.Fill
            };
            layout.Controls.Add(note, 0, 16);
            layout.SetColumnSpan(note, 2);

            var buttons = new FlowLayoutPanel
            {
                FlowDirection = FlowDirection.RightToLeft,
                Dock = DockStyle.Fill
            };
            var saveButton = new Button { Text = "Save", DialogResult = DialogResult.OK, AutoSize = true };
            var cancelButton = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel, AutoSize = true };
            buttons.Controls.Add(saveButton);
            buttons.Controls.Add(cancelButton);
            layout.Controls.Add(buttons, 0, 17);
            layout.SetColumnSpan(buttons, 2);

            Controls.Add(layout);
            AcceptButton = saveButton;
            CancelButton = cancelButton;
        }

        private static NumericUpDown CreateNumber(int value, int minimum, int maximum, int increment = 1) =>
            new()
            {
                Minimum = minimum,
                Maximum = maximum,
                Increment = increment,
                Value = value,
                Dock = DockStyle.Fill
            };

        private static ComboBox CreateComboBox(string[] values, string selectedValue)
        {
            var comboBox = new ComboBox
            {
                Dock = DockStyle.Fill,
                DropDownStyle = ComboBoxStyle.DropDownList
            };
            comboBox.Items.AddRange(values);
            comboBox.SelectedItem = selectedValue;
            return comboBox;
        }

        private void BrowseEngineButton_Click(object? sender, EventArgs e)
        {
            using var dialog = new OpenFileDialog
            {
                Title = "Select a UCI chess engine",
                Filter = "Executable files (*.exe)|*.exe|All files (*.*)|*.*",
                CheckFileExists = true
            };
            if (dialog.ShowDialog(this) == DialogResult.OK)
                _enginePathInput.Text = dialog.FileName;
        }

        private static void AddSetting(TableLayoutPanel layout, string label, Control input, int row)
        {
            layout.Controls.Add(new Label { Text = label, AutoSize = true, Anchor = AnchorStyles.Left }, 0, row);
            layout.Controls.Add(input, 1, row);
        }
    }
}
