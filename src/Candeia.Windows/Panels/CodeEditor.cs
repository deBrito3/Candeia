using System.Runtime.InteropServices;
using Candeia.Languages.CSharp;
using Candeia.Scene;

namespace Candeia.Panels;

// The CANDEIA editor: the player's C#, in the amber of the old monitor. Adapted from Projeto Aurora's
// SyntaxCodeBox: colours are applied a moment after typing stops, without touching the undo history;
// the line of an error is marked over the text without changing it. New here: C# colours, applied only
// to the lines just edited (colouring the whole file took about 50 ms and held up the next key), indentation
// that follows braces, and the typing probe the measurements use.
sealed class CodeEditor : RichTextBox
{
    public static readonly Color Back = Palette.ScreenGlass;
    public static readonly Color NameColor = Color.FromArgb(232, 162, 72), KeywordColor = Color.FromArgb(255, 224, 156), TypeColor = Color.FromArgb(255, 198, 108);
    public static readonly Color TextColor = Color.FromArgb(214, 196, 122), NumberColor = Color.FromArgb(255, 186, 120), CommentColor = Color.FromArgb(138, 98, 48);
    public static readonly Color ErrorColor = Color.FromArgb(232, 84, 84);
    public const int Indent = 4;

    readonly System.Windows.Forms.Timer delay = new() { Interval = 120 };
    bool formatting, dirtyAll = true;
    int dirtyFrom = int.MaxValue, dirtyTo = -1, lastLines = 1;
    long waiting;
    int baseline = -1, caret;

    public CodeEditor()
    {
        BorderStyle = BorderStyle.None;
        BackColor = Back;
        ForeColor = NameColor;
        WordWrap = false;
        DetectUrls = false;
        AcceptsTab = true;
        ScrollBars = RichTextBoxScrollBars.Both;
        delay.Tick += (_, _) => { delay.Stop(); Highlight(); };
    }

    int LineCount => GetLineFromCharIndex(TextLength) + 1;

    // Remembers which lines an edit touched: the caret's line and its neighbours, or everything when the
    // edit added or removed several lines at once (a paste).
    void Touch()
    {
        int lines = LineCount, line = GetLineFromCharIndex(SelectionStart);
        if (Math.Abs(lines - lastLines) > 1) dirtyAll = true;
        lastLines = lines;
        dirtyFrom = Math.Min(dirtyFrom, Math.Max(0, line - 1));
        dirtyTo = Math.Max(dirtyTo, line + 1);
    }

    // The line marked as an error (1-based), or 0 for none. Editing clears it.
    public int MarkedLine { get; private set; }
    public event Action? MarkChanged;

    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public TypingProbe? Probe { get; private set; }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        try { SetWindowTheme(Handle, "DarkMode_Explorer", null); } catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException) { }
        // RichEdit selects whole words when dragging by default; code is selected by character.
        Send(Handle, 0x44D, (IntPtr)3, unchecked((IntPtr)~1)); // EM_SETOPTIONS, ECOOP_AND, ~ECO_AUTOWORDSELECTION
        Highlight();
    }

    protected override void OnTextChanged(EventArgs e)
    {
        if (formatting) return;
        if (MarkedLine > 0) MarkLine(0);
        base.OnTextChanged(e);
        Touch();
        delay.Stop();
        delay.Start();
    }

    // A new font resets the formatting of the whole text: colour it all again.
    protected override void OnFontChanged(EventArgs e)
    {
        base.OnFontChanged(e);
        dirtyAll = true;
        Highlight();
    }

    protected override void OnSelectionChanged(EventArgs e)
    {
        if (!formatting) base.OnSelectionChanged(e);
    }

    // Puts the code in the editor as the player's starting point: no undo back to empty.
    public void Load(string code)
    {
        Text = code.Replace("\r\n", "\n");
        ClearUndo();
        Select(0, 0);
        dirtyAll = true;
        Highlight();
    }

    // Colours the tokens of the lines touched since last time, or of the whole text after a load, a paste,
    // or while a block comment is open somewhere (it may span lines). TOM's undo suspension keeps the
    // colours out of the player's undo history, and redraw is off while it runs, so nothing flickers.
    public void Highlight()
    {
        if (!IsHandleCreated || IsDisposed || formatting) return;
        string text = Text;
        int lines = LineCount;
        bool all = dirtyAll || text.Contains("/*", StringComparison.Ordinal) || dirtyFrom >= lines;
        int from = all ? 0 : Math.Max(0, GetFirstCharIndexFromLine(dirtyFrom));
        int to = all || dirtyTo + 1 >= lines ? text.Length : Math.Max(from, GetFirstCharIndexFromLine(dirtyTo + 1));
        dirtyAll = false;
        dirtyFrom = int.MaxValue;
        dirtyTo = -1;
        lastLines = lines;
        if (to <= from && !all) return;
        GetOle(Handle, 0x43C, IntPtr.Zero, out var ole); // EM_GETOLEINTERFACE
        if (ole == IntPtr.Zero) return;
        IntPtr document = IntPtr.Zero;
        try
        {
            var iid = new Guid("8CC497C0-A1DF-11CE-8098-00AA0047BE5D"); // ITextDocument
            if (Marshal.QueryInterface(ole, in iid, out document) != 0) return;
            var undo = Marshal.GetDelegateForFunctionPointer<UndoMethod>(Marshal.ReadIntPtr(Marshal.ReadIntPtr(document), 22 * IntPtr.Size));
            int start = SelectionStart, length = SelectionLength;
            bool modified = Modified;
            var scroll = Point.Empty;
            Scroll(Handle, 0x4DD, IntPtr.Zero, ref scroll); // EM_GETSCROLLPOS
            if (undo(document, -9999995, IntPtr.Zero) < 0) return; // tomSuspend
            formatting = true;
            Send(Handle, 0xB, IntPtr.Zero, IntPtr.Zero); // WM_SETREDRAW off
            try
            {
                Select(from, to - from);
                SelectionColor = NameColor;
                foreach (var token in CSharpTokenizer.Tokenize(text[from..to]))
                {
                    if (token.Kind == TokenKind.Name) continue;
                    Select(from + token.Start, token.Length);
                    SelectionColor = Colour(token.Kind);
                }
            }
            finally
            {
                Select(start, length);
                if (length == 0) SelectionColor = NameColor;
                Modified = modified;
                Scroll(Handle, 0x4DE, IntPtr.Zero, ref scroll); // EM_SETSCROLLPOS
                undo(document, -9999994, IntPtr.Zero); // tomResume
                Send(Handle, 0xB, 1, IntPtr.Zero);
                formatting = false;
                if (all) Invalidate();
                else
                {
                    int top = GetPositionFromCharIndex(from).Y, height = (int)Math.Ceiling(Font.Height * ZoomFactor);
                    int bottom = (to < text.Length ? GetPositionFromCharIndex(to).Y : GetPositionFromCharIndex(Math.Max(0, to - 1)).Y + height) + height;
                    Invalidate(new Rectangle(0, top, ClientSize.Width, Math.Max(height, bottom - top)));
                }
            }
        }
        finally
        {
            if (document != IntPtr.Zero) Marshal.Release(document);
            Marshal.Release(ole);
        }
    }

    public static Color Colour(TokenKind kind) => kind switch
    {
        TokenKind.Keyword => KeywordColor,
        TokenKind.Type => TypeColor,
        TokenKind.Text => TextColor,
        TokenKind.Number => NumberColor,
        TokenKind.Comment => CommentColor,
        _ => NameColor,
    };

    // Marks a line as the place of an error and brings it into view; 0 clears the mark.
    public void MarkLine(int line)
    {
        if (MarkedLine == line) return;
        MarkedLine = line;
        MarkChanged?.Invoke();
        if (line > 0 && LineBounds(line) is { } bounds && (bounds.Top < 0 || bounds.Bottom > ClientSize.Height))
        {
            int at = GetFirstCharIndexFromLine(line - 1);
            if (at >= 0)
            {
                int keep = SelectionStart;
                Select(at, 0);
                ScrollToCaret();
                Select(keep, 0);
            }
        }
        Invalidate();
    }

    Rectangle? LineBounds(int line)
    {
        int start = GetFirstCharIndexFromLine(line - 1);
        if (start < 0) return null;
        int height = (int)Math.Ceiling(Font.Height * ZoomFactor);
        return new Rectangle(0, GetPositionFromCharIndex(start).Y, ClientSize.Width, height);
    }

    // Enter keeps the indentation, one step more after an opening brace; Tab is four spaces; a closing
    // brace typed at the start of a line moves one step back.
    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Handled || ReadOnly) return;
        if (e.KeyCode == Keys.Enter && e.Modifiers == Keys.None)
        {
            NewLine();
            e.Handled = e.SuppressKeyPress = true;
        }
        else if (e.KeyCode == Keys.Tab && e.Modifiers == Keys.None && SelectionLength == 0)
        {
            InsertTab();
            e.Handled = e.SuppressKeyPress = true;
        }
    }

    protected override void OnKeyPress(KeyPressEventArgs e)
    {
        if (e.KeyChar == '}' && !ReadOnly && CloseBrace())
        {
            e.Handled = true;
            return;
        }
        base.OnKeyPress(e);
    }

    public void NewLine()
    {
        string line = CurrentLineBeforeCaret();
        string indent = new(' ', line.Length - line.TrimStart(' ').Length);
        if (line.TrimEnd().EndsWith('{')) indent += new string(' ', Indent);
        SelectedText = "\n" + indent;
    }

    public void InsertTab()
    {
        int column = CurrentLineBeforeCaret().Length;
        SelectedText = new string(' ', Indent - column % Indent);
    }

    // A closing brace at the start of a line moves one step back. False when the brace goes in as typed.
    public bool CloseBrace()
    {
        string line = CurrentLineBeforeCaret();
        if (SelectionLength != 0 || line.Length < Indent || line.Trim().Length != 0) return false;
        Select(SelectionStart - Indent, Indent);
        SelectedText = "}";
        return true;
    }

    string CurrentLineBeforeCaret()
    {
        int at = SelectionStart;
        int start = GetFirstCharIndexFromLine(GetLineFromCharIndex(at));
        return start >= 0 && at >= start ? Text.Substring(start, at - start) : "";
    }

    // Measurement: letters posted by the probe are timed until they show; afterwards the text is put back.
    public void Attach(TypingProbe probe, int position)
    {
        baseline = TextLength;
        caret = position;
        Select(caret, 0);
        Probe = probe;
        probe.Start(this);
    }

    public void Detach()
    {
        Probe?.Dispose();
        Probe = null;
        RestoreText();
    }

    public void RestoreText()
    {
        if (baseline < 0 || TextLength <= baseline) return;
        Select(caret, TextLength - baseline);
        SelectedText = "";
        Select(caret, 0);
    }

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == Native.WM_CHAR && Probe is { } probe && TypingProbe.IsProbe(m.LParam))
        {
            long postedAt = probe.InFlight;
            base.WndProc(ref m);
            // The text box usually draws the new letter at once; if it left a repaint pending, the letter
            // only counts as shown once that paint is done.
            if (Native.GetUpdateRect(Handle, IntPtr.Zero, false)) waiting = postedAt;
            else probe.Shown(postedAt);
            return;
        }
        // The error mark is painted over the region being repainted only, so its colour never stacks.
        NativeRect dirty = default;
        bool painting = m.Msg == Native.WM_PAINT && MarkedLine > 0 && GetUpdateRect(Handle, out dirty, false);
        base.WndProc(ref m);
        if (m.Msg == Native.WM_PAINT && waiting != 0 && Probe is { } painter)
        {
            painter.Shown(waiting);
            waiting = 0;
        }
        if (!painting || LineBounds(MarkedLine) is not { } bounds) return;
        using var g = Graphics.FromHwnd(Handle);
        g.SetClip(Rectangle.FromLTRB(dirty.Left, dirty.Top, dirty.Right, dirty.Bottom));
        using var fill = new SolidBrush(Color.FromArgb(48, ErrorColor));
        using var edge = new SolidBrush(ErrorColor);
        g.FillRectangle(fill, bounds);
        g.FillRectangle(edge, bounds.X, bounds.Y, 3, bounds.Height);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            delay.Dispose();
            Probe?.Dispose();
        }
        base.Dispose(disposing);
    }

    [StructLayout(LayoutKind.Sequential)] struct NativeRect { public int Left, Top, Right, Bottom; }
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] delegate int UndoMethod(IntPtr self, int count, IntPtr actual);
    [DllImport("user32.dll")] static extern bool GetUpdateRect(IntPtr hwnd, out NativeRect rect, bool erase);
    [DllImport("user32.dll", EntryPoint = "SendMessageW")] static extern IntPtr Send(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam);
    [DllImport("user32.dll", EntryPoint = "SendMessageW")] static extern IntPtr GetOle(IntPtr hwnd, int msg, IntPtr wParam, out IntPtr lParam);
    [DllImport("user32.dll", EntryPoint = "SendMessageW")] static extern IntPtr Scroll(IntPtr hwnd, int msg, IntPtr wParam, ref Point lParam);
    [DllImport("uxtheme.dll", CharSet = CharSet.Unicode)] static extern int SetWindowTheme(IntPtr hwnd, string? app, string? idList);
}
