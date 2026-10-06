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
        private Label statusLabel = null!;
        private TextBox moveInputTextBox = null!;
        private Button clickMoveButton = null!;

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
            statusLabel = new Label();
            moveInputTextBox = new TextBox();
            clickMoveButton = new Button();
            var root = new TableLayoutPanel();
            var footer = new TableLayoutPanel();
            var buttons = new FlowLayoutPanel();
            var inputPanel = new FlowLayoutPanel();

            ((System.ComponentModel.ISupportInitialize)previewPictureBox).BeginInit();
            SuspendLayout();

            root.Dock = DockStyle.Fill;
            root.Padding = new Padding(8);
            root.ColumnCount = 1;
            root.RowCount = 2;
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 62));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 38));

            previewPictureBox.Dock = DockStyle.Fill;
            previewPictureBox.BackColor = Color.FromArgb(32, 32, 32);
            previewPictureBox.SizeMode = PictureBoxSizeMode.Zoom;
            previewPictureBox.SmoothingPercent = 50;
            previewPictureBox.Margin = new Padding(0, 0, 0, 6);

            footer.Dock = DockStyle.Fill;
            footer.ColumnCount = 1;
            footer.RowCount = 4;
            footer.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
            footer.RowStyles.Add(new RowStyle(SizeType.Absolute, 32));
            footer.RowStyles.Add(new RowStyle(SizeType.Absolute, 22));
            footer.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

            buttons.Dock = DockStyle.Fill;
            buttons.FlowDirection = FlowDirection.LeftToRight;
            buttons.WrapContents = true;
            buttons.Padding = new Padding(0);
            buttons.AutoScroll = true;

            ConfigureButton(calibrateButton, "Manual calibrate");
            ConfigureButton(playButton, "Play (F2)");
            ConfigureButton(settingsButton, "Settings");
            calibrateButton.Width = 106;
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
            inputPanel.WrapContents = false;
            inputPanel.FlowDirection = FlowDirection.LeftToRight;
            var moveLabel = new Label
            {
                Text = "Move (Enter/F3):",
                AutoSize = true,
                TextAlign = ContentAlignment.MiddleLeft,
                Margin = new Padding(2, 7, 4, 0)
            };
            moveInputTextBox.Width = 130;
            moveInputTextBox.PlaceholderText = "e2e4";
            clickMoveButton.Text = "Click move (F3)";
            clickMoveButton.Width = 104;
            clickMoveButton.Height = 26;
            clickMoveButton.Margin = new Padding(4, 1, 2, 1);
            clickMoveButton.UseVisualStyleBackColor = true;
            inputPanel.Controls.Add(moveLabel);
            inputPanel.Controls.Add(moveInputTextBox);
            inputPanel.Controls.Add(clickMoveButton);

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
            ClientSize = new Size(450, 450);
            MinimumSize = new Size(100, 100);
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
