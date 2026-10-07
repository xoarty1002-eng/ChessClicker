using System;
using System.Drawing;
using System.Windows.Forms;

namespace ChessClicker
{
    internal sealed class PositionTroubleshooterForm : Form
    {
        private readonly TextBox _fenInput;

        public string CurrentFen => _fenInput.Text.Trim();

        public PositionTroubleshooterForm(string startingFen, string reason)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(startingFen);
            ArgumentException.ThrowIfNullOrWhiteSpace(reason);

            Text = "Position troubleshooter";
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;
            ClientSize = new Size(580, 250);

            var layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(12),
                ColumnCount = 1,
                RowCount = 3
            };
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 56));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));

            var explanation = new Label
            {
                Dock = DockStyle.Fill,
                Text = $"{reason}{Environment.NewLine}Check board calibration and orientation. If more than one move was played, enter the exact current six-field FEN below.",
                AutoEllipsis = true
            };
            _fenInput = new TextBox
            {
                Dock = DockStyle.Fill,
                Multiline = true,
                WordWrap = false,
                ScrollBars = ScrollBars.Horizontal,
                Text = startingFen
            };

            var buttons = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.RightToLeft
            };
            var loadButton = new Button
            {
                Text = "Load FEN and start",
                AutoSize = true
            };
            var cancelButton = new Button
            {
                Text = "Cancel Play",
                DialogResult = DialogResult.Cancel,
                AutoSize = true
            };
            loadButton.Click += LoadButton_Click;
            buttons.Controls.Add(loadButton);
            buttons.Controls.Add(cancelButton);

            layout.Controls.Add(explanation, 0, 0);
            layout.Controls.Add(_fenInput, 0, 1);
            layout.Controls.Add(buttons, 0, 2);
            Controls.Add(layout);

            AcceptButton = loadButton;
            CancelButton = cancelButton;
        }

        private void LoadButton_Click(object? sender, EventArgs e)
        {
            try
            {
                ParsedChessPositionInput input = ChessPositionInput.Parse(_fenInput.Text);
                if (input.Kind != ChessPositionInputKind.Fen)
                    throw new FormatException("Enter a complete six-field FEN, not a move.");

                _fenInput.Text = input.Value;
                DialogResult = DialogResult.OK;
                Close();
            }
            catch (Exception ex) when (ex is ArgumentException or FormatException)
            {
                MessageBox.Show(
                    this,
                    ex.Message,
                    "Invalid FEN",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
            }
        }
    }
}
