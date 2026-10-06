using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

namespace ChessClicker
{
    internal sealed class DesktopClicker
    {
        private readonly MouseMoveExecutor _executor = new(new WindowsMouseInput());

        public void ExecuteMoveOnScreen(string uciMove, Rectangle boardBounds, bool isWhiteView)
        {
            _executor.ExecuteMove(uciMove, boardBounds, isWhiteView);
        }

        private sealed class WindowsMouseInput : IMouseInput
        {
            private const uint MouseEventLeftDown = 0x0002;
            private const uint MouseEventLeftUp = 0x0004;

            public Point Position
            {
                get => Cursor.Position;
                set => Cursor.Position = value;
            }

            public void LeftButtonDown()
            {
                mouse_event(MouseEventLeftDown, 0, 0, 0, 0);
            }

            public void LeftButtonUp()
            {
                mouse_event(MouseEventLeftUp, 0, 0, 0, 0);
            }

            [DllImport("user32.dll")]
            private static extern void mouse_event(uint flags, uint dx, uint dy, uint data, int extraInfo);
        }
    }
}
