using System;
using System.ComponentModel;
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
            private const uint InputMouse = 0;
            private const uint MouseEventLeftDown = 0x0002;
            private const uint MouseEventLeftUp = 0x0004;

            public Point Position
            {
                get
                {
                    if (!GetCursorPos(out Point position))
                        throw new Win32Exception(Marshal.GetLastWin32Error());
                    return position;
                }
                set
                {
                    if (!SetCursorPos(value.X, value.Y))
                        throw new Win32Exception(Marshal.GetLastWin32Error());
                }
            }

            public void LeftButtonDown() => SendMouseEvent(MouseEventLeftDown);

            public void LeftButtonUp() => SendMouseEvent(MouseEventLeftUp);

            private static void SendMouseEvent(uint flags)
            {
                Input input = new()
                {
                    Type = InputMouse,
                    Data = new InputUnion
                    {
                        Mouse = new MouseInput { Flags = flags }
                    }
                };
                if (SendInput(1, [input], Marshal.SizeOf<Input>()) != 1)
                    throw new Win32Exception(Marshal.GetLastWin32Error());
            }

            [StructLayout(LayoutKind.Sequential)]
            private struct Input
            {
                public uint Type;
                public InputUnion Data;
            }

            [StructLayout(LayoutKind.Explicit)]
            private struct InputUnion
            {
                [FieldOffset(0)]
                public MouseInput Mouse;
            }

            [StructLayout(LayoutKind.Sequential)]
            private struct MouseInput
            {
                public int X;
                public int Y;
                public uint MouseData;
                public uint Flags;
                public uint Time;
                public IntPtr ExtraInfo;
            }

            [DllImport("user32.dll", SetLastError = true)]
            [return: MarshalAs(UnmanagedType.Bool)]
            private static extern bool GetCursorPos(out Point point);

            [DllImport("user32.dll", SetLastError = true)]
            [return: MarshalAs(UnmanagedType.Bool)]
            private static extern bool SetCursorPos(int x, int y);

            [DllImport("user32.dll", SetLastError = true)]
            private static extern uint SendInput(uint inputCount, [In] Input[] inputs, int inputSize);
        }
    }
}
