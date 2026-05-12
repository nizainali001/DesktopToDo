using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace DesktopToDo.Services
{
    public static class WallpaperModeService
    {
        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr FindWindowEx(IntPtr hP, IntPtr hC, string? sC, string? sW);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

        private static readonly IntPtr HWND_TOP = IntPtr.Zero;
        private const uint SWP_NOMOVE = 0x0002;
        private const uint SWP_NOSIZE = 0x0001;
        private const uint SWP_NOACTIVATE = 0x0010;
        private const uint SWP_SHOWWINDOW = 0x0040;

        private static IntPtr _originalOwner = IntPtr.Zero;
        private static IntPtr _desktopWindow = IntPtr.Zero;

        public static void EnableWallpaperMode(Window window)
        {
            try
            {
                var interop = new WindowInteropHelper(window);
                interop.EnsureHandle();

                // 1. 找到 Progman
                IntPtr progman = FindWindowEx(IntPtr.Zero, IntPtr.Zero, "Progman", null);

                // 2. 找到 SHELLDLL_DefView
                _desktopWindow = FindWindowEx(progman, IntPtr.Zero, "SHELLDLL_DefView", null);

                // 如果没找到，可能在 WorkerW 下面（Win10/11）
                if (_desktopWindow == IntPtr.Zero)
                {
                    _desktopWindow = FindDesktopWindowViaEnum();
                }

                if (_desktopWindow != IntPtr.Zero)
                {
                    // 3. 设置 Owner
                    _originalOwner = interop.Owner;
                    interop.Owner = _desktopWindow;

                    // 4. 设置位置
                    SetWindowPos(interop.Handle, HWND_TOP, 0, 0, 0, 0,
                        SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE | SWP_SHOWWINDOW);
                }
            }
            catch { }
        }

        public static void DisableWallpaperMode(Window window)
        {
            try
            {
                var interop = new WindowInteropHelper(window);
                interop.Owner = _originalOwner;
                _desktopWindow = IntPtr.Zero;
            }
            catch { }
        }

        public static void ForceKeepVisible(Window window, bool isUserHidden)
        {
            if (!isUserHidden && _desktopWindow != IntPtr.Zero)
            {
                SetBottom(window);
            }
        }

        public static void SetBottom(Window window)
        {
            try
            {
                var interop = new WindowInteropHelper(window);
                SetWindowPos(interop.Handle, HWND_TOP, 0, 0, 0, 0,
                    SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE | SWP_SHOWWINDOW);
            }
            catch { }
        }

        private static IntPtr FindDesktopWindowViaEnum()
        {
            IntPtr result = IntPtr.Zero;

            // 枚举找 WorkerW
            EnumWindows((hWnd, lParam) =>
            {
                IntPtr shellDll = FindWindowEx(hWnd, IntPtr.Zero, "SHELLDLL_DefView", null);
                if (shellDll != IntPtr.Zero)
                {
                    result = hWnd;
                    return false;
                }
                return true;
            }, IntPtr.Zero);

            return result;
        }

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);
        private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);
    }
}
