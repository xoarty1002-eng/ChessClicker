#nullable enable
namespace ChessClicker
{
    partial class Form1
    {
        private System.ComponentModel.IContainer? components = null;
        private PictureBox previewPictureBox = null!;
        private TextBox positionTextBox = null!;
        private TextBox logTextBox = null!;
        private TextBox moveInputTextBox = null!;
        private Button playMoveButton = null!;
        private Button scannerButton = null!;
        private Button calibrateButton = null!;
        private Button autoCalibrateButton = null!;
        private Button settingsButton = null!;
        private Button executeTypedMoveButton = null!;
        private Label fpsLabel = null!;

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
            previewPictureBox = new PictureBox();
            positionTextBox = new TextBox();
            logTextBox = new TextBox();
            moveInputTextBox = new TextBox();
            playMoveButton = new Button();
            scannerButton = new Button();
            calibrateButton = new Button();
            autoCalibrateButton = new Button();
            settingsButton = new Button();
            executeTypedMoveButton = new Button();
            fpsLabel = new Label();
            var root = new TableLayoutPanel();
            var rightPanel = new FlowLayoutPanel();
            var commandLayout = new TableLayoutPanel();
            ((System.ComponentModel.ISupportInitialize)previewPictureBox).BeginInit();
            SuspendLayout();

            root.ColumnCount = 2;
            root.RowCount = 2;
            root.Dock = DockStyle.Fill;
            root.Padding = new Padding(8);
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 68));
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 32));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 72));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 28));

            previewPictureBox.Dock = DockStyle.Fill;
            previewPictureBox.BackColor = Color.FromArgb(32, 32, 32);
            previewPictureBox.SizeMode = PictureBoxSizeMode.Zoom;
            previewPictureBox.Margin = new Padding(0, 0, 8, 8);

            rightPanel.Dock = DockStyle.Fill;
            rightPanel.FlowDirection = FlowDirection.TopDown;
            rightPanel.WrapContents = false;
            rightPanel.AutoScroll = true;
            rightPanel.Padding = new Padding(4);

            fpsLabel.Text = "Preview: stopped";
            fpsLabel.AutoSize = true;
            fpsLabel.Margin = new Padding(4, 4, 4, 12);

            ConfigureButton(calibrateButton, "Manual Calibrate: select corners");
            ConfigureButton(autoCalibrateButton, "Auto-calibrate full screen");
            ConfigureButton(scannerButton, "Start Scanning");
            ConfigureButton(playMoveButton, "Get Stockfish Move");
            ConfigureButton(settingsButton, "Settings");
            ConfigureButton(executeTypedMoveButton, "Click Typed Move");

            var moveLabel = new Label
            {
                Text = "Move in UCI notation (for example e2e4):",
                AutoSize = true,
                Margin = new Padding(4, 14, 4, 2)
            };
            moveInputTextBox.Width = 300;
            moveInputTextBox.PlaceholderText = "e2e4";
            moveInputTextBox.Margin = new Padding(4, 2, 4, 2);
            executeTypedMoveButton.Width = 300;

            var positionLabel = new Label
            {
                Text = "Tracked position",
                AutoSize = true,
                Margin = new Padding(4, 14, 4, 2)
            };
            positionTextBox.Multiline = true;
            positionTextBox.ReadOnly = true;
            positionTextBox.ScrollBars = ScrollBars.Vertical;
            positionTextBox.WordWrap = false;
            positionTextBox.Width = 300;
            positionTextBox.Height = 180;
            positionTextBox.Margin = new Padding(4, 2, 4, 4);

            rightPanel.Controls.Add(fpsLabel);
            rightPanel.Controls.Add(calibrateButton);
            rightPanel.Controls.Add(autoCalibrateButton);
            rightPanel.Controls.Add(scannerButton);
            rightPanel.Controls.Add(playMoveButton);
            rightPanel.Controls.Add(settingsButton);
            rightPanel.Controls.Add(moveLabel);
            rightPanel.Controls.Add(moveInputTextBox);
            rightPanel.Controls.Add(executeTypedMoveButton);
            rightPanel.Controls.Add(positionLabel);
            rightPanel.Controls.Add(positionTextBox);

            commandLayout.Dock = DockStyle.Fill;
            commandLayout.ColumnCount = 1;
            commandLayout.RowCount = 1;
            commandLayout.Padding = new Padding(4);
            commandLayout.Controls.Add(logTextBox, 0, 0);
            logTextBox.Dock = DockStyle.Fill;
            logTextBox.Multiline = true;
            logTextBox.ReadOnly = true;
            logTextBox.ScrollBars = ScrollBars.Vertical;
            logTextBox.WordWrap = false;

            root.Controls.Add(previewPictureBox, 0, 0);
            root.Controls.Add(rightPanel, 1, 0);
            root.Controls.Add(commandLayout, 0, 1);
            root.SetColumnSpan(commandLayout, 2);

            Controls.Add(root);
            AutoScaleDimensions = new SizeF(8F, 20F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(1180, 820);
            MinimumSize = new Size(950, 680);
            Name = "Form1";
            Text = "ChessClicker - Private Analysis";
            ((System.ComponentModel.ISupportInitialize)previewPictureBox).EndInit();
            ResumeLayout(false);
        }

        private static void ConfigureButton(Button button, string text)
        {
            button.Text = text;
            button.Width = 300;
            button.Height = 38;
            button.Margin = new Padding(4);
            button.UseVisualStyleBackColor = true;
        }
    }
}
