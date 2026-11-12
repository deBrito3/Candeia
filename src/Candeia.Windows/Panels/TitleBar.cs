namespace Candeia.Panels;

// The system's header, in inverse video: the night and its piece on the left, and the presence sensors on
// the right ("PRESENÇA: QUINTAL · 04:12"). When the sensors pick him up coming closer, the place flickers
// between where he is and where he is going; when he arrives, it flashes once.
sealed class TitleBar : Control
{
    SystemStyle style;
    string title = "", presence = "", next = "";
    double flickerUntil, flashUntil;

    public TitleBar(SystemStyle style)
    {
        this.style = style;
        DoubleBuffered = true;
        SetStyle(ControlStyles.Selectable, false);
    }

    public void Restyle(SystemStyle value) { style = value; Invalidate(); }

    public void Show(string titleText, string presenceText)
    {
        if (titleText == title && presenceText == presence) return;
        title = titleText;
        presence = presenceText;
        Invalidate();
    }

    // The sensors pick him up on his way to the next station, for the next seconds.
    public void Approaching(string nextPresence, double now, double seconds)
    {
        next = nextPresence;
        flickerUntil = now + seconds;
        Invalidate();
    }

    public void Arrived(double now)
    {
        flickerUntil = 0;
        flashUntil = now + 0.25;
        Invalidate();
    }

    // Called every frame: only repaints while it flickers or flashes.
    public void Animate(double now)
    {
        if (now < flickerUntil || now < flashUntil + 0.05) Invalidate();
    }

    double Now => Environment.TickCount64 / 1000.0;

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        double now = Now;
        bool flash = now < flashUntil;
        g.Clear(flash ? SystemStyle.Bright : SystemStyle.Bar);
        int pad = style.Px(10);
        var flags = TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.NoPadding;
        TextRenderer.DrawText(g, title, style.Title, new Rectangle(pad, 0, Width - pad * 2, Height), SystemStyle.BarInk, flags | TextFormatFlags.Left | TextFormatFlags.EndEllipsis);
        // Flicker: about seven times a second between the two readings, well under the flash limit.
        string shown = now < flickerUntil && (int)(now * 7) % 2 == 1 ? next : presence;
        TextRenderer.DrawText(g, shown, style.Title, new Rectangle(pad, 0, Width - pad * 2, Height), SystemStyle.BarInk, flags | TextFormatFlags.Right);
    }
}
