using System.Drawing.Drawing2D;

namespace Candeia.Scene;

// The room is designed on a 1920×1080 canvas. A window of any size shows it scaled to fit and centred,
// with the wall and the desk continuing into the extra margins.
sealed class SceneLayout
{
    public const float DesignWidth = 1920, DesignHeight = 1080;

    public SceneLayout(Size size)
    {
        Size = size;
        Scale = Math.Min(size.Width / DesignWidth, size.Height / DesignHeight);
        OffsetX = (size.Width - DesignWidth * Scale) / 2;
        OffsetY = (size.Height - DesignHeight * Scale) / 2;
    }

    public Size Size { get; }
    public float Scale { get; }
    public float OffsetX { get; }
    public float OffsetY { get; }

    public Matrix Transform() => new(Scale, 0, 0, Scale, OffsetX, OffsetY);
    public PointF ToPixels(PointF p) => new(OffsetX + p.X * Scale, OffsetY + p.Y * Scale);
    public RectangleF ToPixels(RectangleF r) => new(OffsetX + r.X * Scale, OffsetY + r.Y * Scale, r.Width * Scale, r.Height * Scale);
    public Rectangle EditorBounds() => Rectangle.Round(ToPixels(Editor));

    // Places, in design coordinates.
    public static readonly RectangleF WindowCasing = RectangleF.FromLTRB(60, 150, 440, 790);
    public static readonly RectangleF WindowOpening = RectangleF.FromLTRB(90, 180, 410, 770);
    public static readonly RectangleF Transom = RectangleF.FromLTRB(102, 190, 398, 268);
    // The six lower panes of the window, left leaf then right leaf, top to bottom.
    public static readonly RectangleF[] LowerPanes =
    [
        RectangleF.FromLTRB(102, 292, 242, 442), RectangleF.FromLTRB(102, 452, 242, 604), RectangleF.FromLTRB(102, 614, 242, 758),
        RectangleF.FromLTRB(258, 292, 398, 442), RectangleF.FromLTRB(258, 452, 398, 604), RectangleF.FromLTRB(258, 614, 398, 758),
    ];

    public static readonly RectangleF DoorCasing = RectangleF.FromLTRB(1530, 120, 1860, 1000);
    public static readonly RectangleF Doorway = RectangleF.FromLTRB(1556, 150, 1834, 1000);
    public static readonly RectangleF DoorGap = RectangleF.FromLTRB(1556, 234, 1632, 1000);
    public static readonly RectangleF DoorLeaf = RectangleF.FromLTRB(1642, 234, 1834, 1000);

    public static readonly RectangleF Monitor = RectangleF.FromLTRB(470, 70, 1450, 905);
    public static readonly RectangleF Screen = RectangleF.FromLTRB(545, 135, 1375, 790);
    public const float ScreenRadius = 36;
    public static readonly RectangleF Editor = RectangleF.FromLTRB(567, 157, 1353, 768);
    public static readonly PointF Led = new(1392, 852);

    public const float DeskTop = 930;
    public static readonly RectangleF Keyboard = RectangleF.FromLTRB(640, 968, 1280, 1066);

    public static GraphicsPath RoundedRect(RectangleF r, float radius)
    {
        var path = new GraphicsPath();
        float d = radius * 2;
        path.AddArc(r.Left, r.Top, d, d, 180, 90);
        path.AddArc(r.Right - d, r.Top, d, d, 270, 90);
        path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
        path.AddArc(r.Left, r.Bottom - d, d, d, 90, 90);
        path.CloseFigure();
        return path;
    }

    public static GraphicsPath LowerGlass()
    {
        var path = new GraphicsPath();
        foreach (var pane in LowerPanes) path.AddRectangle(pane);
        return path;
    }

    public static GraphicsPath AllGlass()
    {
        var path = LowerGlass();
        path.AddRectangle(Transom);
        return path;
    }
}
