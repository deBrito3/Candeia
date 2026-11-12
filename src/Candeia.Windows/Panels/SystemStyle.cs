namespace Candeia.Panels;

// The fonts and colours of the CANDEIA system: one monospaced face, sized with the scene.
sealed class SystemStyle : IDisposable
{
    public static readonly Color Glass = Palette.ScreenGlass;
    public static readonly Color Ink = Color.FromArgb(232, 162, 72), Bright = Color.FromArgb(255, 224, 156), Dim = Color.FromArgb(138, 98, 48);
    public static readonly Color Bar = Color.FromArgb(150, 104, 40), BarInk = Color.FromArgb(18, 12, 6);
    public static readonly Color Pass = Color.FromArgb(255, 214, 120), Fail = Color.FromArgb(236, 120, 72), Error = Color.FromArgb(232, 84, 84);
    public static readonly Color Time = Color.FromArgb(255, 198, 108), Program = Color.FromArgb(206, 192, 150), Warning = Color.FromArgb(255, 168, 64);

    // codeScale is the player's choice of editor font size (1 is the normal size).
    public SystemStyle(float scale, double codeScale = 1)
    {
        Scale = scale;
        Code = new Font("Consolas", (float)(17 * scale * codeScale), GraphicsUnit.Pixel);
        Text = new Font("Consolas", 15 * scale, GraphicsUnit.Pixel);
        TextBold = new Font("Consolas", 15 * scale, FontStyle.Bold, GraphicsUnit.Pixel);
        Title = new Font("Consolas", 15 * scale, FontStyle.Bold, GraphicsUnit.Pixel);
    }

    public float Scale { get; }
    public Font Code { get; }
    public Font Text { get; }
    public Font TextBold { get; }
    public Font Title { get; }

    public int Px(float design) => (int)Math.Round(design * Scale);

    public void Dispose()
    {
        Code.Dispose();
        Text.Dispose();
        TextBold.Dispose();
        Title.Dispose();
    }
}
