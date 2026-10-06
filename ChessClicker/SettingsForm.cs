using System;
using System.Drawing;
using System.Windows.Forms;

namespace ChessClicker
{
    internal sealed class SettingsForm : Form
    {
        private readonly NumericUpDown _moveTimeInput;
        private readonly NumericUpDown _skillLevelInput;

        public ChessClickerSettings Settings =>
            new((int)_moveTimeInput.Value, (int)_skillLevelInput.Value);

        public SettingsForm(ChessClickerSettings settings)
        {
            ArgumentNullException.ThrowIfNull(settings);

            Text = "Analysis Settings";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterParent;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;
            ClientSize = new Size(380, 205);

            var layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 4,
                Padding = new Padding(12)
            };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 65));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 35));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));

            _moveTimeInput = new NumericUpDown
            {
                Minimum = 100,
                Maximum = 10000,
                Increment = 100,
                Value = settings.MoveTimeMilliseconds,
                Dock = DockStyle.Fill
            };
            _skillLevelInput = new NumericUpDown
            {
                Minimum = 0,
                Maximum = 20,
                Value = settings.StockfishSkillLevel,
                Dock = DockStyle.Fill
            };

            layout.Controls.Add(new Label { Text = "Thinking time per move (ms)", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 0);
            layout.Controls.Add(_moveTimeInput, 1, 0);
            layout.Controls.Add(new Label { Text = "Stockfish skill (0-20)", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 1);
            layout.Controls.Add(_skillLevelInput, 1, 1);
            layout.Controls.Add(new Label
            {
                Text = "For private analysis and study boards only. This app does not support concealment or fair-play detection evasion.",
                AutoSize = true,
                Dock = DockStyle.Fill
            }, 0, 2);
            layout.SetColumnSpan(layout.GetControlFromPosition(0, 2)!, 2);

            var buttons = new FlowLayoutPanel
            {
                FlowDirection = FlowDirection.RightToLeft,
                Dock = DockStyle.Fill
            };
            var saveButton = new Button { Text = "Save", DialogResult = DialogResult.OK, AutoSize = true };
            var cancelButton = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel, AutoSize = true };
            buttons.Controls.Add(saveButton);
            buttons.Controls.Add(cancelButton);
            layout.Controls.Add(buttons, 0, 3);
            layout.SetColumnSpan(buttons, 2);

            Controls.Add(layout);
            AcceptButton = saveButton;
            CancelButton = cancelButton;
        }
    }
}
