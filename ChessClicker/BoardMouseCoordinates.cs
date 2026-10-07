using System;
using System.Drawing;
using System.Threading;

namespace ChessClicker
{
    public static class BoardBoundsGeometry
    {
        public static Rectangle ToSquare(Rectangle bounds)
        {
            if (bounds.Width <= 0 || bounds.Height <= 0)
                throw new ArgumentException("The chessboard bounds must have positive width and height.", nameof(bounds));

            int side = Math.Min(bounds.Width, bounds.Height);
            return new Rectangle(
                bounds.X + (bounds.Width - side) / 2,
                bounds.Y + (bounds.Height - side) / 2,
                side,
                side);
        }

        public static bool IsSquare(Rectangle bounds) =>
            bounds.Width > 0 && bounds.Height > 0 && bounds.Width == bounds.Height;
    }

    public static class BoardMouseCoordinates
    {
        public static string ScreenCellToSquare(int screenFile, int screenRank, bool isWhiteView)
        {
            ValidateScreenCell(screenFile, screenRank);
            int file = isWhiteView ? screenFile : 7 - screenFile;
            int rank = isWhiteView ? 8 - screenRank : screenRank + 1;
            return $"{(char)('a' + file)}{rank}";
        }

        public static (int File, int Rank) SquareToScreenCell(string square, bool isWhiteView)
        {
            ValidateSquare(square, nameof(square));
            int file = square[0] - 'a';
            int rank = 8 - (square[1] - '0');
            if (!isWhiteView)
            {
                file = 7 - file;
                rank = 7 - rank;
            }

            return (file, rank);
        }

        public static Point SquareCenter(string square, Rectangle boardBounds, bool isWhiteView)
        {
            ValidateBoardBounds(boardBounds);
            (int fileIndex, int rankIndex) = SquareToScreenCell(square, isWhiteView);

            double squareWidth = boardBounds.Width / 8.0;
            double squareHeight = boardBounds.Height / 8.0;
            int pixelX = boardBounds.X + (int)(fileIndex * squareWidth + squareWidth / 2.0);
            int pixelY = boardBounds.Y + (int)(rankIndex * squareHeight + squareHeight / 2.0);
            return new Point(pixelX, pixelY);
        }

        public static string ScreenPointToSquare(Point point, Rectangle boardBounds, bool isWhiteView)
        {
            ValidateBoardBounds(boardBounds);
            if (!boardBounds.Contains(point))
                throw new ArgumentOutOfRangeException(nameof(point), "The point must be inside the chessboard.");

            int screenFile = (point.X - boardBounds.X) * 8 / boardBounds.Width;
            int screenRank = (point.Y - boardBounds.Y) * 8 / boardBounds.Height;
            return ScreenCellToSquare(screenFile, screenRank, isWhiteView);
        }

        private static void ValidateScreenCell(int screenFile, int screenRank)
        {
            if (screenFile is < 0 or > 7)
                throw new ArgumentOutOfRangeException(
                    nameof(screenFile), "Screen column must be within the board.");
            if (screenRank is < 0 or > 7)
                throw new ArgumentOutOfRangeException(
                    nameof(screenRank), "Screen row must be within the board.");
        }

        internal static void ValidateSquare(string square, string parameterName)
        {
            if (square == null || square.Length != 2 ||
                square[0] < 'a' || square[0] > 'h' ||
                square[1] < '1' || square[1] > '8')
            {
                throw new ArgumentException("A square must use algebraic notation from a1 through h8.", parameterName);
            }
        }

        internal static void ValidateBoardBounds(Rectangle boardBounds)
        {
            if (boardBounds.Width <= 0 || boardBounds.Height <= 0)
                throw new ArgumentException("The chessboard bounds must have positive width and height.", nameof(boardBounds));
        }
    }

    public interface IMouseInput
    {
        Point Position { get; set; }
        void LeftButtonDown();
        void LeftButtonUp();
    }

    public sealed class MouseMoveExecutor
    {
        private readonly IMouseInput _mouseInput;
        private readonly Action<TimeSpan> _delay;

        public MouseMoveExecutor(IMouseInput mouseInput)
            : this(mouseInput, delay => Thread.Sleep(delay))
        {
        }

        public MouseMoveExecutor(IMouseInput mouseInput, Action<TimeSpan> delay)
        {
            _mouseInput = mouseInput ?? throw new ArgumentNullException(nameof(mouseInput));
            _delay = delay ?? throw new ArgumentNullException(nameof(delay));
        }

        public void ExecuteMove(string uciMove, Rectangle boardBounds, bool isWhiteView)
        {
            uciMove = ChessMoveNotation.Normalize(uciMove);
            string fromSquare = uciMove[..2];
            string toSquare = uciMove.Substring(2, 2);

            Point from = BoardMouseCoordinates.SquareCenter(fromSquare, boardBounds, isWhiteView);
            Point to = BoardMouseCoordinates.SquareCenter(toSquare, boardBounds, isWhiteView);
            Point originalPosition = _mouseInput.Position;

            try
            {
                ClickSquare(from, TimeSpan.FromMilliseconds(80));
                _delay(TimeSpan.FromMilliseconds(300));
                ClickSquare(to, TimeSpan.FromMilliseconds(80));
            }
            finally
            {
                _mouseInput.Position = originalPosition;
            }
        }

        private void ClickSquare(Point square, TimeSpan cursorDelay)
        {
            _mouseInput.Position = square;
            _delay(cursorDelay);
            _mouseInput.LeftButtonDown();
            try
            {
                _delay(TimeSpan.FromMilliseconds(50));
            }
            finally
            {
                _mouseInput.LeftButtonUp();
            }
        }
    }
}
