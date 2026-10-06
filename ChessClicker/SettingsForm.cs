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
        private readonly ComboBox _boardThemeInput;
        private readonly CheckBox _randomSkillInput;
        private readonly CheckBox _calibrateWhilePlayingInput;
        private readonly NumericUpDown _randomSkillIntervalInput;

        public ChessClickerSettings Settings => new(
            (int)_moveTimeInput.Value,
            (int)_skillLevelInput.Value,
            (int)_framesPerSecondInput.Value,
            (int)_brightnessThresholdInput.Value,
            (string)_boardThemeInput.SelectedItem!,
            _randomSkillInput.Checked,
            (int)_randomSkillIntervalInput.Value,
            _calibrateWhilePlayingInput.Checked,
            (int)_previewSmoothingInput.Value);

        public SettingsForm(ChessClickerSettings settings)
        {
            ArgumentNullException.ThrowIfNull(settings);

            Text = "Settings";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterParent;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;
            ClientSize = new Size(470, 460);

            var layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 10,
                Padding = new Padding(12)
            };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 62));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 38));
            for (int row = 0; row < 8; row++)
                layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));

            _moveTimeInput = CreateNumber(settings.MoveTimeMilliseconds, 100, 10000, 100);
            _skillLevelInput = CreateNumber(settings.StockfishSkillLevel, 0, 20);
            _framesPerSecondInput = CreateNumber(settings.FramesPerSecond, 1, 30);
            _brightnessThresholdInput = CreateNumber(settings.BrightnessThreshold, 1, 100);
            _previewSmoothingInput = CreateNumber(settings.PreviewSmoothingPercent, 0, 100, 5);
            _boardThemeInput = new ComboBox
            {
                Dock = DockStyle.Fill,
                DropDownStyle = ComboBoxStyle.DropDownList
            };
            _boardThemeInput.Items.AddRange(["Green", "Brown", "Blue", "Gray"]);
            _boardThemeInput.SelectedItem = settings.BoardTheme;
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
            AddSetting(layout, "Stockfish skill (0-20)", _skillLevelInput, 1);
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

            var note = new Label
            {
                Text = "Random levels are selected from 0-20 at the chosen turn interval. Use this only on private analysis or study boards; this app does not support fair-play detection evasion.",
                AutoSize = true,
                Dock = DockStyle.Fill
            };
            layout.Controls.Add(note, 0, 8);
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
            layout.Controls.Add(buttons, 0, 9);
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

        private static void AddSetting(TableLayoutPanel layout, string label, Control input, int row)
        {
            layout.Controls.Add(new Label { Text = label, AutoSize = true, Anchor = AnchorStyles.Left }, 0, row);
            layout.Controls.Add(input, 1, row);
        }
    }
}
