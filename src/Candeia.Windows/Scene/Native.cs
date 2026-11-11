using System.Runtime.InteropServices;

namespace Candeia.Scene;

// The few Windows calls the scene needs: copying the frame to the window, waiting for the screen's
// refresh, a finer system timer, and capturing the window for the review images.
static class Native
{
    public const int WM_CHAR = 0x0102, WM_PAINT = 0x000F;

    [StructLayout(LayoutKind.Sequential)]
    public struct BitmapInfoHeader
    {
        public int Size;
        public int Width;
        public int Height;
        public short Planes;
        public short BitCount;
        public int Compression;
        public int SizeImage;
        public int XPelsPerMeter;
        public int YPelsPerMeter;
        public int ClrUsed;
        public int ClrImportant;
    }

    [DllImport("gdi32.dll")]
    public static extern int SetDIBitsToDevice(IntPtr hdc, int xDest, int yDest, int width, int height, int xSrc, int ySrc,
        int startScan, int scanLines, IntPtr bits, ref BitmapInfoHeader header, int colorUse);

    [DllImport("user32.dll")] public static extern IntPtr GetDC(IntPtr hwnd);
    [DllImport("user32.dll")] public static extern int ReleaseDC(IntPtr hwnd, IntPtr hdc);
    [DllImport("user32.dll")] public static extern bool GetUpdateRect(IntPtr hwnd, IntPtr rect, bool erase);
    [DllImport("user32.dll", EntryPoint = "PostMessageW")] public static extern bool PostMessage(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam);
    [DllImport("user32.dll")] public static extern bool PrintWindow(IntPtr hwnd, IntPtr hdc, int flags);
    public const int PrintFullContent = 2;

    // Blocks until the desktop compositor presents the next frame, which follows the screen's refresh.
    [DllImport("dwmapi.dll")] public static extern int DwmFlush();

    [DllImport("winmm.dll")] public static extern int timeBeginPeriod(int milliseconds);
    [DllImport("winmm.dll")] public static extern int timeEndPeriod(int milliseconds);
}
