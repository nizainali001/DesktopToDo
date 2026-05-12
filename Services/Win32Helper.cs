using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace DesktopToDo.Services
{
    public static class Win32Helper
    {
        [DllImport("user32.dll")]
        private static extern IntPtr SetWindowLong(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

        [DllImport("user32.dll")]
        private static extern IntPtr GetWindowLong(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll")]
        private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

        private const int GWL_EXSTYLE = -20;
        private const int WS_EX_TOOLWINDOW = 0x00000080;
        private const int WS_EX_APPWINDOW = 0x00040000;
        private static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);
        private static readonly IntPtr HWND_NOTOPMOST = new IntPtr(-2);
        private static readonly IntPtr HWND_TOP = new IntPtr(0);
        private static readonly IntPtr HWND_BOTTOM = new IntPtr(1);
        private const uint SWP_NOMOVE = 0x0002;
        private const uint SWP_NOSIZE = 0x0001;
        private const uint SWP_NOACTIVATE = 0x0010;

        public static void HideFromTaskbar(Window window)
        {
            var helper = new WindowInteropHelper(window);
            var exStyle = GetWindowLong(helper.Handle, GWL_EXSTYLE);
            exStyle = new IntPtr(exStyle.ToInt64() | WS_EX_TOOLWINDOW);
            exStyle = new IntPtr(exStyle.ToInt64() & ~WS_EX_APPWINDOW);
            SetWindowLong(helper.Handle, GWL_EXSTYLE, exStyle);
        }

        public static void SetWindowTopMost(Window window, bool isTopMost)
        {
            var helper = new WindowInteropHelper(window);
            SetWindowPos(helper.Handle, isTopMost ? HWND_TOPMOST : HWND_NOTOPMOST,
                0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE);
        }

        public static void SetWindowBottom(Window window)
        {
            var helper = new WindowInteropHelper(window);
            SetWindowPos(helper.Handle, HWND_BOTTOM,
                0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE);
        }
    }
}
