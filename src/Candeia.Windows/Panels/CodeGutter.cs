namespace Candeia.Panels;

// Line numbers beside the editor, adapted from Projeto Aurora. Numbers follow the editor's scrolling, the
// caret's line is brighter, and the line of an error is red, matching the "Linha N" of the output.
sealed class CodeGutter : Control
{
    public static readonly Color Back = Color.FromArgb(20, 15, 9), Number = Color.FromArgb(112, 80, 36), Current = Color.FromArgb(214, 150, 64);
    readonly CodeEditor editor;
    readonly List<int> starts = [0];
    int caretLine = -1;

    public CodeGutter(CodeEditor editor)
    {
        this.editor = editor;
        DoubleBuffered = true;
        BackColor = Back;
        // Repaints only when what it shows changes: the number of lines, or the caret's line.
        editor.TextChanged += (_, _) => { int before = starts.Count; Recount(); if (starts.Count != before) Invalidate(); };
        // RichEdit normalizes line endings when its handle is created: count again then.
        editor.HandleCreated += (_, _) => editor.BeginInvoke(() => { if (!IsDisposed) { Recount(); Invalidate(); } });
        editor.VScroll += (_, _) => Invalidate();
        editor.Resize += (_, _) => Invalidate();
        editor.SelectionChanged += (_, _) =>
        {
            int line = editor.GetLineFromCharIndex(editor.SelectionStart);
            if (line == caretLine) return;
            caretLine = line;
            Invalidate();
        };
        editor.FontChanged += (_, _) => Invalidate();
        editor.MarkChanged += Invalidate;
        Recount();
    }

    // The width the numbers need for the editor's font.
    public int PreferredWidth()
    {
        int digits = Math.Max(2, starts.Count.ToString().Length);
        return TextRenderer.MeasureText(new string('8', digits), editor.Font).Width + (int)(editor.Font.Height * 0.6f);
    }

    void Recount()
    {
        starts.Clear();
        starts.Add(0);
        string text = editor.Text;
        for (int i = 0; i < text.Length; i++) if (text[i] == '\n') starts.Add(i + 1);
    }

    int LineHeight => (int)Math.Ceiling(editor.Font.Height * editor.ZoomFactor);

    int TopOf(int line)
    {
        int start = starts[line];
        if (start < editor.TextLength || start == 0) return editor.GetPositionFromCharIndex(start).Y;
        return editor.GetPositionFromCharIndex(start - 1).Y + LineHeight; // an empty last line
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        if (!editor.IsHandleCreated) return;
        var g = e.Graphics;
        int height = LineHeight;
        int low = 0, high = starts.Count - 1;
        while (low < high)
        {
            int mid = (low + high) / 2;
            if (TopOf(mid) + height < 0) low = mid + 1;
            else high = mid;
        }
        int caret = editor.SelectionStart;
        int caretLine = starts.BinarySearch(caret) is var found && found >= 0 ? found : ~found - 1;
        // The editor's text starts a few pixels below its top: the numbers follow it.
        int offset = editor.Top - Top;
        for (int line = low; line < starts.Count; line++)
        {
            int top = TopOf(line) + offset;
            if (top > ClientSize.Height) break;
            var color = line + 1 == editor.MarkedLine ? CodeEditor.ErrorColor : line == caretLine ? Current : Number;
            TextRenderer.DrawText(g, (line + 1).ToString(), editor.Font, new Rectangle(0, top, ClientSize.Width - editor.Font.Height / 3, height), color,
                TextFormatFlags.Right | TextFormatFlags.Top | TextFormatFlags.NoPadding | TextFormatFlags.SingleLine);
        }
        using var edge = new Pen(Color.FromArgb(54, 38, 18));
        g.DrawLine(edge, ClientSize.Width - 1, 0, ClientSize.Width - 1, ClientSize.Height);
    }
}
