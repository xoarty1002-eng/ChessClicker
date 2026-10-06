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
            var root = new TableLayoutPanel();
            var footer = new TableLayoutPanel();
            var buttons = new FlowLayoutPanel();

            ((System.ComponentModel.ISupportInitialize)previewPictureBox).BeginInit();
            SuspendLayout();

            root.Dock = DockStyle.Fill;
            root.Padding = new Padding(8);
            root.ColumnCount = 1;
            root.RowCount = 2;
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 76));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 24));

            previewPictureBox.Dock = DockStyle.Fill;
            previewPictureBox.BackColor = Color.FromArgb(32, 32, 32);
            previewPictureBox.SizeMode = PictureBoxSizeMode.Zoom;
            previewPictureBox.SmoothingPercent = 50;
            previewPictureBox.Margin = new Padding(0, 0, 0, 6);

            footer.Dock = DockStyle.Fill;
            footer.ColumnCount = 1;
            footer.RowCount = 2;
            footer.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
            footer.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

            buttons.Dock = DockStyle.Fill;
            buttons.FlowDirection = FlowDirection.LeftToRight;
            buttons.WrapContents = true;
            buttons.Padding = new Padding(0);
            buttons.AutoScroll = true;

            ConfigureButton(calibrateButton, "Manual calibrate");
            ConfigureButton(playButton, "Play (F2)");
            ConfigureButton(settingsButton, "Settings");
            calibrateButton.Width = 112;
            playButton.Width = 84;
            settingsButton.Width = 72;
            buttons.Controls.Add(calibrateButton);
            buttons.Controls.Add(playButton);
            buttons.Controls.Add(settingsButton);

            statusLabel.Dock = DockStyle.Fill;
            statusLabel.TextAlign = ContentAlignment.MiddleRight;
            statusLabel.AutoEllipsis = true;
            statusLabel.Text = "Preview: 5 FPS (stopped)";
            buttons.Controls.Add(statusLabel);

            logTextBox.Dock = DockStyle.Fill;
            logTextBox.Multiline = true;
            logTextBox.ReadOnly = true;
            logTextBox.ScrollBars = ScrollBars.Vertical;
            logTextBox.WordWrap = true;

            footer.Controls.Add(buttons, 0, 0);
            footer.Controls.Add(logTextBox, 0, 1);
            root.Controls.Add(previewPictureBox, 0, 0);
            root.Controls.Add(footer, 0, 1);

            Controls.Add(root);
            AutoScaleDimensions = new SizeF(8F, 20F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(300, 300);
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
