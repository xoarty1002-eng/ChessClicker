using System.Drawing;
using ChessClicker;
using Xunit;

namespace ChessClicker.Tests;

public sealed class BoardGridCalibratorTests
{
    [Fact]
    public void FitsMisalignedCropToAlternatingEightByEightGrid()
    {
        int[,] image = new int[210, 210];
        for (int y = 0; y < 210; y++)
        {
            for (int x = 0; x < 210; x++)
            {
                bool insideBoard = x >= 20 && x < 180 && y >= 20 && y < 180;
                if (!insideBoard)
                {
                    image[y, x] = 128;
                    continue;
                }

                int file = (x - 20) / 20;
                int rank = (y - 20) / 20;
                image[y, x] = (rank + file) % 2 == 0 ? 220 : 70;
            }
        }

        Rectangle? fitted = BoardGridCalibrator.FindBestBounds(
            image, new Rectangle(25, 24, 150, 152), adjustmentPixels: 20);

        Assert.Equal(new Rectangle(20, 20, 160, 160), fitted);
    }

    [Fact]
    public void RejectsImageWithoutConfidentGridEdges()
    {
        int[,] image = new int[100, 100];
        for (int y = 0; y < 100; y++)
            for (int x = 0; x < 100; x++)
                image[y, x] = 128;

        Assert.Null(BoardGridCalibrator.FindBestBounds(image, new Rectangle(10, 10, 80, 80), 8));
    }
}
