using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;

namespace Candeia.Scene;

// A point light in pixels: full strength at the centre, fading to nothing at Radius. Core sets how fast it
// falls near the centre. The colour already includes the intensity.
readonly record struct PointLight(float X, float Y, float Radius, float Core, float R, float G, float B);

// A rectangle of light with soft edges, used for the outside seen through the window.
readonly record struct AreaLight(RectangleF Bounds, float Feather, float R, float G, float B);

// The darkness of the room. Lights add into a small map, one cell per 8×8 pixels, with the vignette
// folded in; then a single pass multiplies every pixel of the room by the map, smoothly interpolated,
// adds the film grain and writes the result into the frame. The pass skips the editor, which is never
// touched.
sealed class Lighting
{
    public const int Cell = 8;
    const int Shift = 3;            // log2(Cell)
    const int GrainSize = 256;       // the grain tile is 256×256 pixels
    public const int One = 128;      // light 1.0 in the map's fixed point (1.7); the map holds up to 2.0

    readonly int stride, height, texels, mapWidth, mapHeight;
    readonly float[] red, green, blue, vignette;
    readonly short[] map, row;
    readonly sbyte[] grain;

    public Lighting(int stride, int height)
    {
        this.stride = stride;
        this.height = height;
        texels = stride / Cell;
        mapWidth = (texels + 2 + 1) & ~1; // even, so a map row is a whole number of 8-lane vectors
        mapHeight = (height + Cell - 1) / Cell + 2;
        int cells = mapWidth * mapHeight;
        red = new float[cells];
        green = new float[cells];
        blue = new float[cells];
        vignette = new float[cells];
        map = new short[cells * 4];
        row = new short[mapWidth * 4];
        grain = new sbyte[GrainSize * GrainSize * 4];

        // How much each cell darkens at the corners, from 0 in the middle to 1 at the far corners.
        float cx = stride / 2f, cy = height / 2f;
        for (int y = 0; y < mapHeight; y++)
            for (int x = 0; x < mapWidth; x++)
            {
                float nx = (x * Cell - cx) / cx, ny = (y * Cell - cy) / cy;
                float r = MathF.Sqrt(nx * nx * 0.85f + ny * ny);
                vignette[y * mapWidth + x] = SmoothStep(0.45f, 1.35f, r);
            }

        // Grain: mostly the same value on the three channels, with a little colour noise.
        var random = new Random(1234);
        for (int i = 0; i < GrainSize * GrainSize; i++)
        {
            float n = (float)(random.NextDouble() + random.NextDouble() + random.NextDouble() - 1.5) / 1.5f;
            float mono = n * 9f;
            for (int c = 0; c < 3; c++)
                grain[i * 4 + c] = (sbyte)Math.Clamp(MathF.Round(mono + (float)(random.NextDouble() - 0.5) * 2f), -24, 24);
            grain[i * 4 + 3] = 0;
        }
    }

    static float SmoothStep(float from, float to, float x)
    {
        float t = Math.Clamp((x - from) / (to - from), 0, 1);
        return t * t * (3 - 2 * t);
    }

    // Fills the light map. Strength of the vignette runs from 0 (none) to 1 (black corners).
    public void Build(ReadOnlySpan<PointLight> lights, ReadOnlySpan<AreaLight> areas, float ambientR, float ambientG, float ambientB, float vignetteStrength)
    {
        Array.Fill(red, ambientR);
        Array.Fill(green, ambientG);
        Array.Fill(blue, ambientB);
        foreach (var light in lights) AddPoint(light);
        foreach (var area in areas) AddArea(area);
        for (int i = 0; i < red.Length; i++)
        {
            float v = 1 - vignetteStrength * vignette[i];
            map[i * 4 + 0] = Fixed(blue[i] * v);
            map[i * 4 + 1] = Fixed(green[i] * v);
            map[i * 4 + 2] = Fixed(red[i] * v);
            map[i * 4 + 3] = One;
        }
    }

    static short Fixed(float value) => (short)Math.Clamp((int)(value * One + 0.5f), 0, 2 * One);

    void AddPoint(in PointLight light)
    {
        int x0 = Math.Max(0, (int)((light.X - light.Radius) / Cell)), x1 = Math.Min(mapWidth - 1, (int)((light.X + light.Radius) / Cell) + 1);
        int y0 = Math.Max(0, (int)((light.Y - light.Radius) / Cell)), y1 = Math.Min(mapHeight - 1, (int)((light.Y + light.Radius) / Cell) + 1);
        float radius2 = light.Radius * light.Radius, inverseRadius2 = 1 / radius2, inverseCore2 = 1 / (light.Core * light.Core);
        for (int y = y0; y <= y1; y++)
        {
            float dy = y * Cell - light.Y, dy2 = dy * dy;
            int line = y * mapWidth;
            for (int x = x0; x <= x1; x++)
            {
                float dx = x * Cell - light.X, d2 = dx * dx + dy2;
                if (d2 >= radius2) continue;
                float q = 1 - d2 * inverseRadius2;
                float f = q * q / (1 + d2 * inverseCore2);
                red[line + x] += light.R * f;
                green[line + x] += light.G * f;
                blue[line + x] += light.B * f;
            }
        }
    }

    void AddArea(in AreaLight area)
    {
        var b = area.Bounds;
        int x0 = Math.Max(0, (int)((b.Left - area.Feather) / Cell)), x1 = Math.Min(mapWidth - 1, (int)((b.Right + area.Feather) / Cell) + 1);
        int y0 = Math.Max(0, (int)((b.Top - area.Feather) / Cell)), y1 = Math.Min(mapHeight - 1, (int)((b.Bottom + area.Feather) / Cell) + 1);
        float feather = Math.Max(1, area.Feather) * 2;
        for (int y = y0; y <= y1; y++)
        {
            float py = y * Cell;
            float fy = Math.Clamp((py - b.Top + feather / 2) / feather, 0, 1) * Math.Clamp((b.Bottom - py + feather / 2) / feather, 0, 1);
            if (fy <= 0) continue;
            int line = y * mapWidth;
            for (int x = x0; x <= x1; x++)
            {
                float px = x * Cell;
                float f = fy * Math.Clamp((px - b.Left + feather / 2) / feather, 0, 1) * Math.Clamp((b.Right - px + feather / 2) / feather, 0, 1);
                red[line + x] += area.R * f;
                green[line + x] += area.G * f;
                blue[line + x] += area.B * f;
            }
        }
    }

    static readonly Vector128<short> Weights01 = Vector128.Create((short)0, 0, 0, 0, 1, 1, 1, 1);
    static readonly Vector128<short> Weights23 = Vector128.Create((short)2, 2, 2, 2, 3, 3, 3, 3);
    static readonly Vector128<short> Weights45 = Vector128.Create((short)4, 4, 4, 4, 5, 5, 5, 5);
    static readonly Vector128<short> Weights67 = Vector128.Create((short)6, 6, 6, 6, 7, 7, 7, 7);

    // Writes source × light map + grain into destination (both the size of the frame; they may be the
    // same array). grainX is rounded to a multiple of 8. Pixels inside skip (the editor) are left alone,
    // whole 8-pixel cells at a time.
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public void Apply(uint[] source, uint[] destination, Rectangle skip, int grainX, int grainY, bool withGrain)
    {
        if (!Vector128.IsHardwareAccelerated) { ApplyScalar(source, destination, skip, grainX, grainY, withGrain); return; }
        ref byte pixels = ref MemoryMarshal.GetReference(MemoryMarshal.AsBytes(source.AsSpan()));
        ref byte output = ref MemoryMarshal.GetReference(MemoryMarshal.AsBytes(destination.AsSpan()));
        ref short mapStart = ref map[0];
        ref short rowStart = ref row[0];
        ref sbyte grainStart = ref grain[0];
        int lanes = mapWidth * 4;
        int skipFrom = (skip.Left + Cell - 1) / Cell, skipTo = skip.Right / Cell;
        grainX &= ~(Cell - 1);

        for (int y = 0; y < height; y++)
        {
            int j = y >> Shift;
            var k = Vector128.Create((short)(y & (Cell - 1)));
            for (int i = 0; i < lanes; i += 8)
            {
                var a = Vector128.LoadUnsafe(ref mapStart, (nuint)(j * lanes + i));
                var b = Vector128.LoadUnsafe(ref mapStart, (nuint)((j + 1) * lanes + i));
                (a + Vector128.ShiftRightArithmetic((b - a) * k, Shift)).StoreUnsafe(ref rowStart, (nuint)i);
            }

            bool skipping = y >= skip.Top && y < skip.Bottom && skipTo > skipFrom;
            int grainRow = ((y + grainY) & (GrainSize - 1)) * GrainSize * 4;
            nuint line = (nuint)(y * stride * 4);
            for (int t = 0; t < texels; t++)
            {
                if (skipping && t == skipFrom) { t = skipTo - 1; continue; }
                var here = Vector128.Create(Unsafe.ReadUnaligned<ulong>(ref Unsafe.As<short, byte>(ref Unsafe.Add(ref rowStart, t * 4)))).AsInt16();
                var next = Vector128.Create(Unsafe.ReadUnaligned<ulong>(ref Unsafe.As<short, byte>(ref Unsafe.Add(ref rowStart, t * 4 + 4)))).AsInt16();
                var step = next - here;
                var l0 = (here + Vector128.ShiftRightArithmetic(step * Weights01, Shift)).AsUInt16();
                var l1 = (here + Vector128.ShiftRightArithmetic(step * Weights23, Shift)).AsUInt16();
                var l2 = (here + Vector128.ShiftRightArithmetic(step * Weights45, Shift)).AsUInt16();
                var l3 = (here + Vector128.ShiftRightArithmetic(step * Weights67, Shift)).AsUInt16();

                nuint at = line + (nuint)(t * Cell * 4);
                var v0 = Vector128.LoadUnsafe(ref pixels, at);
                var v1 = Vector128.LoadUnsafe(ref pixels, at + 16);
                var p0 = Vector128.ShiftRightLogical(Vector128.WidenLower(v0) * l0, 7).AsInt16();
                var p1 = Vector128.ShiftRightLogical(Vector128.WidenUpper(v0) * l1, 7).AsInt16();
                var p2 = Vector128.ShiftRightLogical(Vector128.WidenLower(v1) * l2, 7).AsInt16();
                var p3 = Vector128.ShiftRightLogical(Vector128.WidenUpper(v1) * l3, 7).AsInt16();
                if (withGrain)
                {
                    nuint g = (nuint)(grainRow + ((t * Cell + grainX) & (GrainSize - 1)) * 4);
                    var g0 = Vector128.LoadUnsafe(ref grainStart, g);
                    var g1 = Vector128.LoadUnsafe(ref grainStart, g + 16);
                    p0 += Vector128.WidenLower(g0);
                    p1 += Vector128.WidenUpper(g0);
                    p2 += Vector128.WidenLower(g1);
                    p3 += Vector128.WidenUpper(g1);
                }
                Pack(p0, p1).StoreUnsafe(ref output, at);
                Pack(p2, p3).StoreUnsafe(ref output, at + 16);
            }
        }
    }

    static Vector128<byte> Pack(Vector128<short> low, Vector128<short> high)
    {
        if (Sse2.IsSupported) return Sse2.PackUnsignedSaturate(low, high);
        var max = Vector128.Create((short)255);
        return Vector128.Narrow(Vector128.Min(Vector128.Max(low, Vector128<short>.Zero), max).AsUInt16(), Vector128.Min(Vector128.Max(high, Vector128<short>.Zero), max).AsUInt16());
    }

    // The same pass, one value at a time. It is the reference the fast pass is checked against, and the
    // fallback on processors without vector instructions.
    public void ApplyScalar(uint[] source, uint[] destination, Rectangle skip, int grainX, int grainY, bool withGrain)
    {
        int lanes = mapWidth * 4;
        int skipFrom = (skip.Left + Cell - 1) / Cell, skipTo = skip.Right / Cell;
        grainX &= ~(Cell - 1);
        for (int y = 0; y < height; y++)
        {
            int j = y >> Shift, k = y & (Cell - 1);
            for (int i = 0; i < lanes; i++)
            {
                int a = map[j * lanes + i], b = map[(j + 1) * lanes + i];
                row[i] = (short)(a + (((b - a) * k) >> Shift));
            }
            bool skipping = y >= skip.Top && y < skip.Bottom && skipTo > skipFrom;
            int grainRow = ((y + grainY) & (GrainSize - 1)) * GrainSize * 4;
            for (int t = 0; t < texels; t++)
            {
                if (skipping && t >= skipFrom && t < skipTo) continue;
                for (int p = 0; p < Cell; p++)
                {
                    int x = t * Cell + p;
                    uint color = source[y * stride + x];
                    uint result = 0;
                    for (int c = 0; c < 4; c++)
                    {
                        int here = row[t * 4 + c], next = row[t * 4 + 4 + c];
                        int light = here + (((next - here) * p) >> Shift);
                        int value = (int)((color >> (c * 8)) & 0xFF) * light >> 7;
                        if (withGrain) value += grain[grainRow + ((x + grainX) & (GrainSize - 1)) * 4 + c];
                        result |= (uint)Math.Clamp(value, 0, 255) << (c * 8);
                    }
                    destination[y * stride + x] = result;
                }
            }
        }
    }
}
