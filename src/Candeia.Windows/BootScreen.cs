using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;

namespace Candeia;

// The empty CANDEIA screen shown while the game has no scene yet: the system name, its version and a
// blinking prompt. Painting is a pure function of the size and the elapsed time, so the window and
// --shot draw exactly the same picture.
static class BootScreen
{
    public const int BlinkMilliseconds = 530;

    public static bool CursorVisible(TimeSpan elapsed) => (long)(elapsed.TotalMilliseconds / BlinkMilliseconds) % 2 == 0;

    public static void Paint(Graphics g, Size size, TimeSpan elapsed)
    {
        g.Clear(Palette.Night);
        if (size.Width <= 0 || size.Height <= 0) return;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;

        float unit = Math.Min(size.Width, size.Height * 1.6f) / 100f;
        using var title = new Font("Consolas", unit * 9f, FontStyle.Bold, GraphicsUnit.Pixel);
        using var small = new Font("Consolas", unit * 1.8f, FontStyle.Regular, GraphicsUnit.Pixel);
        using var amber = new SolidBrush(Palette.Amber);
        using var dim = new SolidBrush(Palette.AmberDim);

        var titleSize = g.MeasureString(GameInfo.SystemName, title);
        float cx = size.Width / 2f, cy = size.Height / 2f;
        g.DrawString(GameInfo.SystemName, title, amber, cx - titleSize.Width / 2f, cy - titleSize.Height * 0.75f);

        string subtitle = $"sistema da casa · versão {GameInfo.Version}";
        var subtitleSize = g.MeasureString(subtitle, small);
        g.DrawString(subtitle, small, dim, cx - subtitleSize.Width / 2f, cy + titleSize.Height * 0.3f);

        // The prompt: "> " and a block cursor that blinks.
        string prompt = "> ";
        var promptSize = g.MeasureString(prompt, small);
        float py = cy + titleSize.Height * 0.3f + subtitleSize.Height * 2f;
        float px = cx - promptSize.Width;
        g.DrawString(prompt, small, amber, px, py);
        if (CursorVisible(elapsed))
            g.FillRectangle(amber, px + promptSize.Width, py + subtitleSize.Height * 0.15f, unit * 1.0f, subtitleSize.Height * 0.75f);
        string hint = "Enter liga a casa";
        var hintSize = g.MeasureString(hint, small);
        g.DrawString(hint, small, dim, cx - hintSize.Width / 2f, py + subtitleSize.Height * 2.2f);

        // The photosensitivity warning, and the soft scares.
        string[] warning =
        [
            "Aviso: este jogo tem clarões de relâmpago, sustos e sons altos de repente.",
            $"S liga os sustos suaves, sem clarões: {(Options.SoftScares ? "LIGADOS" : "desligados")}.",
        ];
        float wy = size.Height - unit * 9;
        foreach (var line in warning)
        {
            var lineSize = g.MeasureString(line, small);
            g.DrawString(line, small, line.StartsWith('S') ? amber : dim, cx - lineSize.Width / 2f, wy);
            wy += lineSize.Height * 1.2f;
        }
    }

    public static void SaveShot(string path, Size size)
    {
        string? folder = Path.GetDirectoryName(Path.GetFullPath(path));
        if (folder is not null) Directory.CreateDirectory(folder);
        using var image = new Bitmap(size.Width, size.Height, PixelFormat.Format32bppArgb);
        using (var g = Graphics.FromImage(image)) Paint(g, size, TimeSpan.Zero);
        image.Save(path, ImageFormat.Png);
    }
}
