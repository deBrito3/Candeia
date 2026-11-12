using System.Diagnostics;
using System.Runtime.InteropServices;
using Candeia.Challenges;
using Candeia.Progress;
using Candeia.Screens;

namespace Candeia;

// The CANDEIA window before and between the nights: the boot screen and the menu (CandeiaMenu). A night
// opens in its own window; leaving it for the menu brings this one back, and closing it closes the game.
sealed class MainWindow : Form
{
    readonly System.Windows.Forms.Timer blink = new() { Interval = BootScreen.BlinkMilliseconds };
    readonly Stopwatch clock = Stopwatch.StartNew();
    readonly Campaign? campaign;
    readonly CandeiaMenu? menu;
    readonly Action repaint;

    // Without a campaign, only the boot screen (the --shot pictures).
    public MainWindow(Campaign? campaign = null, SaveProblem? problem = null)
    {
        this.campaign = campaign;
        Text = GameInfo.Name;
        ClientSize = new Size(1280, 800);
        MinimumSize = new Size(1024, 640);
        StartPosition = FormStartPosition.CenterScreen;
        BackColor = Palette.Night;
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.UserPaint, true);
        blink.Tick += (_, _) => Invalidate();
        blink.Start();
        repaint = () => Invalidate();
        Options.Changed += repaint;
        if (campaign is null) return;
        menu = new CandeiaMenu(campaign, problem);
        menu.Play += Play;
        menu.Quit += Close;
    }

    public CandeiaMenu? CandeiaMenu => menu;

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        DarkTitleBar(Handle, Palette.Night);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        if (menu is null) BootScreen.Paint(e.Graphics, ClientSize, clock.Elapsed);
        else menu.Paint(e.Graphics, ClientSize, clock.Elapsed);
    }

    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        if (Visible && menu is not null && menu.Key(keyData))
        {
            Invalidate();
            return true;
        }
        return base.ProcessCmdKey(ref msg, keyData);
    }

    // A night in its own window. Leaving it for the menu brings this window back; closing it ends the game.
    void Play(NightDefinition night, bool fresh)
    {
        if (campaign is null) return;
        if (fresh) campaign.ForgetCode(night);
        var screen = new NightScreen(night, campaign.Nights, campaign: campaign);
        screen.FormClosed += (_, _) =>
        {
            if (!screen.ToMenu)
            {
                Close();
                return;
            }
            menu!.Show(CandeiaMenu.Page.Main);
            Show();
            Activate();
            Invalidate();
        };
        screen.Show();
        Hide();
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        campaign?.Flush();
        base.OnFormClosing(e);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            Options.Changed -= repaint;
            blink.Dispose();
        }
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
