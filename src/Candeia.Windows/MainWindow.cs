using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Candeia;

// The CANDEIA boot screen. Enter turns the house system on: the first night opens and this window closes
// with it.
sealed class MainWindow : Form
{
    readonly System.Windows.Forms.Timer blink = new() { Interval = BootScreen.BlinkMilliseconds };
    readonly Stopwatch clock = Stopwatch.StartNew();
    readonly Func<Form>? next;

    public MainWindow(Func<Form>? next = null)
    {
        this.next = next;
        KeyPreview = true;
        Text = GameInfo.Name;
        ClientSize = new Size(1280, 800);
        MinimumSize = new Size(1024, 640);
        StartPosition = FormStartPosition.CenterScreen;
        BackColor = Palette.Night;
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.UserPaint, true);
        blink.Tick += (_, _) => Invalidate();
        blink.Start();
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        DarkTitleBar(Handle, Palette.Night);
    }

    protected override void OnPaint(PaintEventArgs e) => BootScreen.Paint(e.Graphics, ClientSize, clock.Elapsed);

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.KeyCode == Keys.S)
        {
            Options.SoftScares = !Options.SoftScares;
            Invalidate();
            return;
        }
        if (e.KeyCode == Keys.V)
        {
            Options.Difficulty = Options.Difficulty == Night.Difficulty.Vigil ? Night.Difficulty.Night : Night.Difficulty.Vigil;
            Invalidate();
            return;
        }
        if (e.KeyCode != Keys.Enter || next is null || !Visible) return;
        var screen = next();
        screen.FormClosed += (_, _) => Close();
        screen.Show();
        Hide();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) blink.Dispose();
        base.Dispose(disposing);
    }

    // Dark title bar (Windows 10 1809+) and, on Windows 11, a caption painted in the night colour.
    // Older systems ignore the attributes and keep the default caption.
    internal static void DarkTitleBar(IntPtr handle, Color caption)
    {
        int dark = 1;
        _ = DwmSetWindowAttribute(handle, ImmersiveDarkMode, ref dark, sizeof(int));
        int colorRef = caption.R | caption.G << 8 | caption.B << 16;
        _ = DwmSetWindowAttribute(handle, CaptionColor, ref colorRef, sizeof(int));
    }

    const int ImmersiveDarkMode = 20, CaptionColor = 35;
    [DllImport("dwmapi.dll")] static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);
}
