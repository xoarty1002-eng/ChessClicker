#nullable enable
namespace ChessClicker
{
    partial class Form1
    {
        private System.ComponentModel.IContainer? components = null;
        private BoardPreviewControl previewPictureBox = null!;
        private TextBox logTextBox = null!;
        private Button playButton = null!;
        private Button calibrateButton = null!;
        private Button settingsButton = null!;
        private Button scanFenButton = null!;
        private Label statusLabel = null!;
        private TextBox moveInputTextBox = null!;
        private Button clickMoveButton = null!;
        private Button registerOpponentMoveButton = null!;

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                components?.Dispose();
                previewPictureBox?.Image?.Dispose();
            }
            base.Dispose(disposing);
        }

        private void InitializeComponent()
        {
            previewPictureBox = new BoardPreviewControl();
            logTextBox = new TextBox();
            playButton = new Button();
            calibrateButton = new Button();
            settingsButton = new Button();
            scanFenButton = new Button();
            statusLabel = new Label();
            moveInputTextBox = new TextBox();
            clickMoveButton = new Button();
            registerOpponentMoveButton = new Button();
            var root = new TableLayoutPanel();
            var footer = new TableLayoutPanel();
            var buttons = new FlowLayoutPanel();
            var inputPanel = new TableLayoutPanel();
            var inputActions = new TableLayoutPanel();

            ((System.ComponentModel.ISupportInitialize)previewPictureBox).BeginInit();
            SuspendLayout();

            root.Dock = DockStyle.Fill;
            root.Padding = new Padding(8);
            root.ColumnCount = 1;
            root.RowCount = 2;
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 55));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 45));

            previewPictureBox.Dock = DockStyle.Fill;
            previewPictureBox.BackColor = Color.FromArgb(32, 32, 32);
            previewPictureBox.SizeMode = PictureBoxSizeMode.Zoom;
            previewPictureBox.SmoothingPercent = 50;
            previewPictureBox.Margin = new Padding(0, 0, 0, 6);

            footer.Dock = DockStyle.Fill;
            footer.ColumnCount = 1;
            footer.RowCount = 4;
            footer.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
            footer.RowStyles.Add(new RowStyle(SizeType.Absolute, 102));
            footer.RowStyles.Add(new RowStyle(SizeType.Absolute, 24));
            footer.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

            buttons.Dock = DockStyle.Fill;
            buttons.FlowDirection = FlowDirection.LeftToRight;
            buttons.WrapContents = true;
            buttons.Padding = new Padding(0);
            buttons.AutoScroll = true;

            ConfigureButton(calibrateButton, "Manual calibrate");
            ConfigureButton(playButton, "Play (F2)");
            ConfigureButton(settingsButton, "Settings");
            calibrateButton.Width = 105;
            playButton.Width = 82;
            settingsButton.Width = 68;
            buttons.Controls.Add(calibrateButton);
            buttons.Controls.Add(playButton);
            buttons.Controls.Add(settingsButton);

            statusLabel.Dock = DockStyle.Fill;
            statusLabel.TextAlign = ContentAlignment.MiddleLeft;
            statusLabel.AutoEllipsis = true;
            statusLabel.AutoSize = false;
            statusLabel.Text = "Bottom: unknown | 5 FPS | stopped";

            inputPanel.Dock = DockStyle.Fill;
            inputPanel.ColumnCount = 2;
            inputPanel.RowCount = 1;
            inputPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            inputPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 126));
            inputPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            inputPanel.Margin = new Padding(0, 0, 0, 4);
            moveInputTextBox.Dock = DockStyle.Fill;
            moveInputTextBox.Multiline = true;
            moveInputTextBox.ScrollBars = ScrollBars.Vertical;
            moveInputTextBox.PlaceholderText = "Enter UCI move (e2e4) or edit scanned FEN";
            inputActions.Dock = DockStyle.Fill;
            inputActions.ColumnCount = 1;
            inputActions.RowCount = 3;
            inputActions.RowStyles.Add(new RowStyle(SizeType.Percent, 34));
            inputActions.RowStyles.Add(new RowStyle(SizeType.Percent, 33));
            inputActions.RowStyles.Add(new RowStyle(SizeType.Percent, 33));
            clickMoveButton.Text = "Analyze / Click (F3)";
            clickMoveButton.Dock = DockStyle.Fill;
            clickMoveButton.Margin = new Padding(4, 0, 0, 2);
            clickMoveButton.UseVisualStyleBackColor = true;
            ConfigureButton(scanFenButton, "Scan FEN");
            scanFenButton.Dock = DockStyle.Fill;
            scanFenButton.Margin = new Padding(4, 2, 0, 0);
            ConfigureButton(registerOpponentMoveButton, "Register");
            registerOpponentMoveButton.Dock = DockStyle.Fill;
            registerOpponentMoveButton.Height = 28;
            registerOpponentMoveButton.Margin = new Padding(4, 2, 0, 0);
            inputActions.Controls.Add(clickMoveButton, 0, 0);
            inputActions.Controls.Add(scanFenButton, 0, 1);
            inputActions.Controls.Add(registerOpponentMoveButton, 0, 2);
            inputPanel.Controls.Add(moveInputTextBox, 0, 0);
            inputPanel.Controls.Add(inputActions, 1, 0);

            logTextBox.Dock = DockStyle.Fill;
            logTextBox.Multiline = true;
            logTextBox.ReadOnly = true;
            logTextBox.ScrollBars = ScrollBars.Vertical;
            logTextBox.WordWrap = true;

            footer.Controls.Add(buttons, 0, 0);
            footer.Controls.Add(inputPanel, 0, 1);
            footer.Controls.Add(statusLabel, 0, 2);
            footer.Controls.Add(logTextBox, 0, 3);
            root.Controls.Add(previewPictureBox, 0, 0);
            root.Controls.Add(footer, 0, 1);

            Controls.Add(root);
            AutoScaleDimensions = new SizeF(8F, 20F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(520, 560);
            MinimumSize = new Size(500, 500);
            KeyPreview = true;
            Name = "Form1";
            Text = "ChessClicker";
            ((System.ComponentModel.ISupportInitialize)previewPictureBox).EndInit();
            ResumeLayout(false);
        }

        private static void ConfigureButton(Button button, string text)
        {
            button.Text = text;
            button.Height = 34;
            button.Margin = new Padding(3, 2, 8, 2);
            button.UseVisualStyleBackColor = true;
        }

    }
}
