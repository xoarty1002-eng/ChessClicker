using System;
using System.ComponentModel;
using System.Drawing;
using System.Runtime.InteropServices;

namespace ChessClicker
{
    internal sealed class DesktopMouseHook : IDisposable
    {
        private const int WhMouseLowLevel = 14;
        private const int WmLeftButtonDown = 0x0201;
        private const int WmLeftButtonUp = 0x0202;

        private readonly Func<Point, bool> _onLeftButtonDown;
        private readonly LowLevelMouseProc _hookProcedure;
        private IntPtr _hookHandle;
        private bool _swallowNextLeftUp;

        public DesktopMouseHook(Func<Point, bool> onLeftButtonDown)
        {
            _onLeftButtonDown = onLeftButtonDown;
            _hookProcedure = HookCallback;
        }

        public void Start()
        {
            if (_hookHandle != IntPtr.Zero) return;

            _hookHandle = SetWindowsHookEx(WhMouseLowLevel, _hookProcedure, IntPtr.Zero, 0);
            if (_hookHandle == IntPtr.Zero)
                throw new Win32Exception(Marshal.GetLastWin32Error());
        }

        public void Dispose()
        {
            if (_hookHandle == IntPtr.Zero) return;
            UnhookWindowsHookEx(_hookHandle);
            _hookHandle = IntPtr.Zero;
        }

        private IntPtr HookCallback(int code, IntPtr message, IntPtr data)
        {
            if (code >= 0)
            {
                int messageId = message.ToInt32();
                if (messageId == WmLeftButtonDown)
                {
                    MouseHookData mouseData = Marshal.PtrToStructure<MouseHookData>(data);
                    if (_onLeftButtonDown(mouseData.Position))
                    {
                        _swallowNextLeftUp = true;
                        return new IntPtr(1);
                    }
                }

                if (messageId == WmLeftButtonUp && _swallowNextLeftUp)
                {
                    _swallowNextLeftUp = false;
                    return new IntPtr(1);
                }
            }

            return CallNextHookEx(_hookHandle, code, message, data);
        }

        private delegate IntPtr LowLevelMouseProc(int code, IntPtr message, IntPtr data);

        [StructLayout(LayoutKind.Sequential)]
        private struct MouseHookData
        {
            public Point Position;
            public uint MouseData;
            public uint Flags;
            public uint Time;
            public IntPtr ExtraInfo;
        }

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr SetWindowsHookEx(int hookId, LowLevelMouseProc callback, IntPtr module, uint threadId);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool UnhookWindowsHookEx(IntPtr hook);

        [DllImport("user32.dll")]
        private static extern IntPtr CallNextHookEx(IntPtr hook, int code, IntPtr message, IntPtr data);
    }
}