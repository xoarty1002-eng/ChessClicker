using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

namespace ChessClicker
{
    internal class DesktopClicker
    {
        // Win32 API native imports for simulating physical mouse inputs
        [DllImport("user32.dll")]
        private static extern void mouse_event(uint dwFlags, uint dx, uint dy, uint dwData, int dwExtraInfo);

        private const uint MOUSEEVENTF_LEFTDOWN = 0x0002;
        private const uint MOUSEEVENTF_LEFTUP = 0x0004;

        /// <summary>
        /// Translates a recommended move (e.g. "e2e4") into screen pixel coordinates and injects physical mouse clicks.
        /// </summary>
        /// <param name="uciMove">The 4-character move string from Stockfish (e.g., "e2e4").</param>
        /// <param name="boardBounds">The calibrated desktop screen coordinates of the chessboard.</param>
        /// <param name="isWhiteView">True if White pieces are on the bottom rows; False if Black perspective rules apply.</param>
        public void ExecuteMoveOnScreen(string uciMove, Rectangle boardBounds, bool isWhiteView)
        {
            if (string.IsNullOrEmpty(uciMove) || uciMove.Length < 4) return;

            // 1. Separate the 4-character string into start and end algebraic coordinates
            string startSquare = uciMove.Substring(0, 2);
            string endSquare = uciMove.Substring(2, 2);

            // 2. Calculate the exact central pixel coordinates for both destination points
            Point startPixel = ConvertAlgebraicToPixel(startSquare, boardBounds, isWhiteView);
            Point endPixel = ConvertAlgebraicToPixel(endSquare, boardBounds, isWhiteView);

            // Save the user's current cursor position so we can restore it afterward
            Point originalMousePos = Cursor.Position;

            // 3. Click the starting square piece
            Cursor.Position = startPixel;
            Thread.Sleep(80); // Small realistic delay buffer
            mouse_event(MOUSEEVENTF_LEFTDOWN, 0, 0, 0, 0);
            Thread.Sleep(50);
            mouse_event(MOUSEEVENTF_LEFTUP, 0, 0, 0, 0);

            Thread.Sleep(150); // Pause briefly while moving across tiles to mimic natural speeds

            // 4. Click the target destination square slot
            Cursor.Position = endPixel;
            Thread.Sleep(80);
            mouse_event(MOUSEEVENTF_LEFTDOWN, 0, 0, 0, 0);
            Thread.Sleep(50);
            mouse_event(MOUSEEVENTF_LEFTUP, 0, 0, 0, 0);

            // 5. Restore the mouse back to where the user had it originally
            Cursor.Position = originalMousePos;
        }

        /// <summary>
        /// Converts an algebraic coordinate name (like "e2") into absolute pixel offsets on your monitor.
        /// </summary>
        private Point ConvertAlgebraicToPixel(string square, Rectangle boardBounds, bool isWhiteView)
        {
            int fileIndex = square[0] - 'a';       // 'a' = 0, 'b' = 1, etc.
            int rankIndex = 8 - (square[1] - '0'); // '8' = 0, '7' = 1, etc.

            // If playing from Black's perspective view, flip the indexing matrices 180 degrees
            if (!isWhiteView)
            {
                fileIndex = 7 - fileIndex;
                rankIndex = 7 - rankIndex;

            }

            double squareWidth = boardBounds.Width / 8.0;
            double squareHeight = boardBounds.Height / 8.0;

            // Calculate center offsets for target square slots
            int pixelX = boardBounds.X + (int)(fileIndex * squareWidth + (squareWidth / 2.0));
            int pixelY = boardBounds.Y + (int)(rankIndex * squareHeight + (squareHeight / 2.0));

            return new Point(pixelX, pixelY);
        }
    }
}