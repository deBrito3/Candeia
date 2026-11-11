using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace Candeia.Scene;

// An image the scene draws into with GDI+ and edits pixel by pixel: one pinned array, seen by GDI+ as a
// premultiplied 32-bit bitmap, copied to the window with a single GDI call. Rows are padded to a
// multiple of 8 pixels, the size of a light-map cell.
sealed class PixelBuffer : IDisposable
{
    readonly IntPtr address;
    Native.BitmapInfoHeader header;

    public PixelBuffer(int width, int height)
    {
        Width = Math.Max(1, width);
        Height = Math.Max(1, height);
        Stride = (Width + 7) & ~7;
        Pixels = GC.AllocateUninitializedArray<uint>(Stride * Height, pinned: true);
        address = Marshal.UnsafeAddrOfPinnedArrayElement(Pixels, 0);
        Bitmap = new Bitmap(Width, Height, Stride * 4, PixelFormat.Format32bppPArgb, address);
        header = new Native.BitmapInfoHeader
        {
            Size = Marshal.SizeOf<Native.BitmapInfoHeader>(),
            Width = Stride,
            Height = -Height, // top-down rows
            Planes = 1,
            BitCount = 32,
        };
    }

    public int Width { get; }
    public int Height { get; }
    public int Stride { get; }
    public uint[] Pixels { get; }
    public Bitmap Bitmap { get; }

    // One block copy of the whole image: measured faster than copying around the editor row by row.
    public void CopyTo(PixelBuffer other) => Pixels.AsSpan().CopyTo(other.Pixels);

    // Copies one rectangle, row by row, into another image of the same size.
    public void CopyRect(PixelBuffer other, Rectangle area)
    {
        area = Rectangle.Intersect(area, new Rectangle(0, 0, Width, Height));
        for (int y = area.Top; y < area.Bottom; y++) Array.Copy(Pixels, y * Stride + area.Left, other.Pixels, y * Stride + area.Left, area.Width);
    }

    public void Present(IntPtr hdc) =>
        Native.SetDIBitsToDevice(hdc, 0, 0, Width, Height, 0, 0, 0, Height, address, ref header, 0);

    public void Dispose() => Bitmap.Dispose();
}
