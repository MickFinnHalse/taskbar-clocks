using System;
using System.Drawing;
using System.Runtime.InteropServices;

namespace TaskbarClocks
{
    /// <summary>
    /// Win32 glue. Windows 11 removed the deskband API, so nothing can be hosted *inside*
    /// the taskbar any more. Instead we locate the notification area and float a topmost
    /// tool window immediately to its left, which reads as though it were in the taskbar.
    /// </summary>
    internal static class Native
    {
        [StructLayout(LayoutKind.Sequential)]
        public struct RECT { public int Left, Top, Right, Bottom; }

        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        private static extern IntPtr FindWindow(string lpClassName, string lpWindowName);

        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        private static extern IntPtr FindWindowEx(IntPtr parent, IntPtr after, string lpClassName, string lpWindowName);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter,
                                                int X, int Y, int cx, int cy, uint uFlags);

        [DllImport("user32.dll")]
        private static extern bool SetProcessDPIAware();

        [DllImport("user32.dll")]
        private static extern bool SetProcessDpiAwarenessContext(IntPtr value);

        private static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);
        private static readonly IntPtr DPI_PER_MONITOR_AWARE_V2 = new IntPtr(-4);

        private const uint SWP_NOSIZE     = 0x0001;
        private const uint SWP_NOMOVE     = 0x0002;
        private const uint SWP_NOACTIVATE = 0x0010;

        /// <summary>
        /// Must run before any window exists, or GetWindowRect returns virtualised
        /// coordinates on a scaled display and the clocks land in the wrong place.
        /// </summary>
        public static void EnableDpiAwareness()
        {
            try { if (SetProcessDpiAwarenessContext(DPI_PER_MONITOR_AWARE_V2)) return; } catch { }
            try { SetProcessDPIAware(); } catch { }
        }

        private static Rectangle RectOf(IntPtr hWnd)
        {
            RECT r;
            if (hWnd == IntPtr.Zero || !GetWindowRect(hWnd, out r)) return Rectangle.Empty;
            return Rectangle.FromLTRB(r.Left, r.Top, r.Right, r.Bottom);
        }

        /// <summary>Screen rect of the primary taskbar, or empty if it cannot be found.</summary>
        public static Rectangle GetTaskbarRect()
        {
            return RectOf(FindWindow("Shell_TrayWnd", null));
        }

        /// <summary>
        /// Screen rect of the notification area (clock, chevron, wifi, volume, battery).
        /// Empty if the shell layout has changed and we can no longer find it.
        /// </summary>
        public static Rectangle GetTrayRect()
        {
            IntPtr tray = FindWindow("Shell_TrayWnd", null);
            if (tray == IntPtr.Zero) return Rectangle.Empty;
            IntPtr notify = FindWindowEx(tray, IntPtr.Zero, "TrayNotifyWnd", null);
            return RectOf(notify);
        }

        /// <summary>
        /// Re-assert topmost. The taskbar is itself a topmost window, and z-order among
        /// topmost windows follows activation, so a single TopMost=true at startup is not
        /// enough to stay above it for the life of the process.
        /// </summary>
        public static void BringToTop(IntPtr hWnd)
        {
            if (hWnd == IntPtr.Zero) return;
            SetWindowPos(hWnd, HWND_TOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE);
        }
    }
}
