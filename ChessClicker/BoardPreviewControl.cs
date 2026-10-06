using System;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace ChessClicker
{
    internal sealed class BoardPreviewControl : PictureBox
    {
        private int _smoothingPercent = 50;

        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public int SmoothingPercent
        {
            get => _smoothingPercent;
            set
            {
                _smoothingPercent = Math.Clamp(value, 0, 100);
                Invalidate();
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.Clear(BackColor);
            Image? image = Image;
            if (image == null)
            {
                base.OnPaint(e);
                return;
            }

            float scale = Math.Min(
                (float)ClientSize.Width / image.Width,
                (float)ClientSize.Height / image.Height);
            int width = (int)(image.Width * scale);
            int height = (int)(image.Height * scale);
            var destination = new Rectangle(
                (ClientSize.Width - width) / 2,
                (ClientSize.Height - height) / 2,
                width,
                height);

            e.Graphics.InterpolationMode = _smoothingPercent switch
            {
                0 => InterpolationMode.NearestNeighbor,
                < 50 => InterpolationMode.Bilinear,
                < 75 => InterpolationMode.HighQualityBilinear,
                _ => InterpolationMode.HighQualityBicubic
            };
            e.Graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
            e.Graphics.DrawImage(image, destination);
        }
    }
}
