using System.Text.RegularExpressions;
using Candeia.Challenges;
using Candeia.Languages.CSharp;
using Candeia.Night;

namespace Candeia.Panels;

// The lower panel of the system, with five tabs: the LEIA-ME left by the person who wrote CANDEIA; the
// SAÍDA, where each run is reported; the DICAS, which open one layer at a time (the idea, the commands, the
// skeleton), each one a sweet; the CADERNO, free, with the concepts (F1 on a word opens its page); and the
// REVISÃO of a won night. Ctrl+1 to Ctrl+5 switch between them. A double click on an output line that points
// to the code takes the editor there, or applies its fix (CORRIGIR).
sealed partial class InfoPane : Control
{
    public enum Tab { Readme, Output, Hints, Notebook, Review }

    static readonly Bilingual[] LayerNames = [new("A ideia", "The idea"), new("Os comandos", "The commands"), new("O esqueleto", "The skeleton")];

    SystemStyle style;
    readonly RichTextBox readme = Box(), output = Box(), hints = Box(), notebook = Box(), review = Box();
    IReadOnlyList<Candeia.Challenges.NotebookPage> pages = [];
    Candeia.Challenges.NotebookPage? page;
    int notebookMarks;
    NightReview? reviewed;
    (bool Available, double Cost) hintPrice = (true, 0);
    IReadOnlyList<OutputLine> lines = [];
    IReadOnlyList<Bilingual> layers = [];
    int layersShown;
    string markdown = "";

    public InfoPane(SystemStyle style)
    {
        this.style = style;
        DoubleBuffered = true;
        BackColor = SystemStyle.Glass;
        Controls.AddRange([readme, output, hints, notebook, review]);
        output.Visible = hints.Visible = notebook.Visible = review.Visible = false;
        output.MouseDoubleClick += (_, e) => ChooseOutput(LogicalLine(output, e.Location));
        notebook.MouseDoubleClick += (_, e) => ChoosePage(LogicalLine(notebook, e.Location));
    }

    // A double click on a line of the output: its fix, when the checker has one, or the code line it names.
    public void ChooseOutput(int line)
    {
        if (line < 0 || line >= lines.Count) return;
        if (lines[line] is { Line: { } code, Fix: { } fix }) FixChosen?.Invoke(code, fix);
        else if (lines[line].Line is { } at) LineChosen?.Invoke(at);
    }

    // A double click on a line of the notebook's index: in it, each page is one line after the two of the
    // heading. A secret page opens only once the marks reach it.
    public void ChoosePage(int line)
    {
        if (page is not null) return;
        line -= 2;
        if (line >= 0 && line < pages.Count && pages[line].Secret <= notebookMarks) PageChosen?.Invoke(pages[line]);
    }

    public Tab Current { get; private set; } = Tab.Readme;
    public event Action<int>? LineChosen;
    public event Action<int, string>? FixChosen;
    public event Action<Candeia.Challenges.NotebookPage>? PageChosen;
    public RichTextBox NotebookBox => notebook;
    public RichTextBox ReviewBox => review;

    // The line of the text under a point, counting only real line breaks (wrapped lines are one line).
    static int LogicalLine(RichTextBox box, Point point)
    {
        int at = box.GetCharIndexFromPosition(point);
        string text = box.Text;
        int line = 0;
        for (int i = 0; i < at && i < text.Length; i++) if (text[i] == '\n') line++;
        return line;
    }
    public RichTextBox ReadmeBox => readme;
    public RichTextBox OutputBox => output;
    public RichTextBox HintsBox => hints;

    static RichTextBox Box() => new()
    {
        ReadOnly = true,
        BorderStyle = BorderStyle.None,
        BackColor = SystemStyle.Glass,
        ForeColor = SystemStyle.Ink,
        DetectUrls = false,
        WordWrap = true,
        ScrollBars = RichTextBoxScrollBars.Vertical,
        TabStop = false,
    };

    public void Restyle(SystemStyle value)
    {
        style = value;
        Arrange();
        ShowReadme(markdown);
        ShowOutput(lines);
        ShowHints(layers, layersShown, hintPrice.Available, hintPrice.Cost);
        ShowNotebook(pages, notebookMarks, page);
        ShowReview(reviewed);
    }

    public void Choose(Tab tab)
    {
        Current = tab;
        readme.Visible = tab == Tab.Readme;
        output.Visible = tab == Tab.Output;
        hints.Visible = tab == Tab.Hints;
        notebook.Visible = tab == Tab.Notebook;
        review.Visible = tab == Tab.Review;
        Invalidate();
    }

    int HeaderHeight => style.Px(26);

    protected override void OnLayout(LayoutEventArgs levent)
    {
        base.OnLayout(levent);
        Arrange();
    }

    void Arrange()
    {
        int pad = style.Px(10);
        var area = new Rectangle(pad, HeaderHeight + style.Px(4), Math.Max(10, Width - pad * 2), Math.Max(10, Height - HeaderHeight - style.Px(8)));
        readme.Bounds = output.Bounds = hints.Bounds = notebook.Bounds = review.Bounds = area;
        // Tables are laid out for the panel's width (in columns, or one block per row when too wide), so a
        // new width lays out again the texts that can hold one.
        if (area.Width == laidOutWidth) return;
        laidOutWidth = area.Width;
        ShowReadme(markdown);
        ShowHints(layers, layersShown, hintPrice.Available, hintPrice.Cost);
        ShowNotebook(pages, notebookMarks, page);
    }

    int laidOutWidth;

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        foreach (var box in new[] { readme, output, hints, notebook, review })
            try { SetWindowTheme(box.Handle, "DarkMode_Explorer", null); } catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException) { }
    }

    [System.Runtime.InteropServices.DllImport("uxtheme.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode)]
    static extern int SetWindowTheme(IntPtr hwnd, string? app, string? idList);

    readonly Rectangle[] tabs = new Rectangle[5];

    // The tabs, in inverse video when chosen, and the keys on the right.
    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.Clear(SystemStyle.Glass);
        int pad = style.Px(10), x = pad, h = HeaderHeight;
        var flags = TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.NoPadding;
        using (var rule = new Pen(SystemStyle.Dim)) g.DrawLine(rule, pad, h, Width - pad, h);
        foreach (var (tab, name) in new[] { (Tab.Readme, Options.T("LEIA-ME", "README")), (Tab.Output, Options.T("SAÍDA", "OUTPUT")), (Tab.Hints, Options.T("DICAS", "HINTS")), (Tab.Notebook, Options.T("CADERNO", "NOTEBOOK")), (Tab.Review, Options.T("REVISÃO", "REVIEW")) })
        {
            string text = $" {name} ";
            var size = TextRenderer.MeasureText(text, style.Title);
            var box = new Rectangle(x, style.Px(3), size.Width, h - style.Px(3));
            bool chosen = tab == Current;
            if (chosen) using (var bar = new SolidBrush(SystemStyle.Bar)) g.FillRectangle(bar, box);
            TextRenderer.DrawText(g, text, style.Title, box, chosen ? SystemStyle.BarInk : SystemStyle.Ink, flags);
            tabs[(int)tab] = box;
            x += size.Width + pad;
        }
        TextRenderer.DrawText(g, Options.T("F5 executar · F1 caderno · Esc pausa", "F5 run · F1 notebook · Esc pause"), style.Text, new Rectangle(x, 0, Width - x - pad, h), SystemStyle.Dim, flags | TextFormatFlags.Right | TextFormatFlags.EndEllipsis);
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        for (int i = 0; i < tabs.Length; i++) if (tabs[i].Contains(e.Location)) Choose((Tab)i);
    }

    // The output of the last run, coloured by kind.
    public void ShowOutput(IReadOnlyList<OutputLine> value)
    {
        bool added = lines.Count > 0 && value.Count > lines.Count && value.Take(lines.Count).SequenceEqual(lines);
        lines = value;
        var box = output;
        box.SuspendLayout();
        box.Clear();
        foreach (var line in value)
        {
            var ink = line.Kind switch
            {
                OutputKind.Heading or OutputKind.Win => SystemStyle.Bright,
                OutputKind.Pass => SystemStyle.Pass,
                OutputKind.Fail => SystemStyle.Fail,
                OutputKind.Error => SystemStyle.Error,
                OutputKind.Time => SystemStyle.Time,
                OutputKind.Program => SystemStyle.Program,
                OutputKind.Warning => SystemStyle.Warning,
                _ => SystemStyle.Ink,
            };
            // What the program printed goes as it is; explanations quote code between backticks, shown in
            // the code colour without the marks.
            if (line.Kind == OutputKind.Program) Append(box, "  " + line.Text + "\n", style.Text, ink);
            else if (line.Kind is OutputKind.Heading or OutputKind.Win) Append(box, Plain(line.Text) + "\n", style.TextBold, ink);
            else Inline(box, line.Text + "\n", ink);
        }
        // A new output starts at its top; lines added to it (the clock, his moves, the capture) show at
        // the bottom.
        box.SelectionStart = added ? box.TextLength : 0;
        box.ScrollToCaret();
        box.ResumeLayout();
    }

    // The night's LEIA-ME.
    public void ShowReadme(string text)
    {
        markdown = text;
        readme.Clear();
        Markdown(readme, text);
        readme.SelectionStart = 0;
        readme.ScrollToCaret();
    }

    // The hint layers opened so far, the newest in view, and the price of the next one.
    public void ShowHints(IReadOnlyList<Bilingual> all, int shown, bool available, double cost)
    {
        layers = all;
        layersShown = shown;
        hintPrice = (available, cost);
        var box = hints;
        box.Clear();
        if (shown == 0)
            Inline(box, Options.T(
                "As dicas abrem uma de cada vez: primeiro a ideia, depois os comandos e, por fim, um esqueleto com lacunas. Cada uma é um doce dele, e doce do Papa-Figo tem preço.\n\nLembrar um conceito não custa nada: o **CADERNO** (Ctrl+4, ou F1 sobre uma palavra do código) é de graça.\n",
                "The hints open one at a time: first the idea, then the commands and, last, a skeleton with blanks. Each one is a sweet of his, and the Papa-Figo's sweets have a price.\n\nRemembering a concept costs nothing: the **NOTEBOOK** (Ctrl+4, or F1 on a word of the code) is free.\n"), SystemStyle.Ink);
        int newest = 0;
        for (int i = 0; i < shown && i < all.Count; i++)
        {
            if (i > 0) Append(box, "\n", style.Text, SystemStyle.Ink);
            newest = box.TextLength;
            string name = i < LayerNames.Length ? Options.Of(LayerNames[i]) : "";
            Append(box, Options.T($"Dica {i + 1} de {all.Count} · {name}\n", $"Hint {i + 1} of {all.Count} · {name}\n"), style.TextBold, SystemStyle.Bright);
            Markdown(box, Options.Of(all[i]));
        }
        string price = $"−{(int)Math.Round(cost) / 60}:{(int)Math.Round(cost) % 60:00}";
        string next = !available && shown < all.Count ? Options.T("\nNesta noite não há doce.\n", "\nThere is no sweet tonight.\n")
            : shown >= all.Count ? ""
            : cost > 0 ? Options.T($"\n**Ctrl+3** de novo aceita o doce: a dica {shown + 1} de {all.Count}, por {price} e a marca Sem doce.\n",
                $"\n**Ctrl+3** again takes the sweet: hint {shown + 1} of {all.Count}, for {price} and the No sweet mark.\n")
            : Options.T($"\n**Ctrl+3** de novo abre a dica {shown + 1} de {all.Count}, de graça.\n", $"\n**Ctrl+3** again opens hint {shown + 1} of {all.Count}, for free.\n");
        if (next.Length > 0) Inline(box, next, SystemStyle.Warning);
        box.SelectionStart = newest;
        box.ScrollToCaret();
    }

    // The notebook: its index (a double click opens a page), or one page. The secret pages come last; each
    // shows how many marks open it until the campaign has them.
    public void ShowNotebook(IReadOnlyList<Candeia.Challenges.NotebookPage> all, int marks, Candeia.Challenges.NotebookPage? open)
    {
        pages = all;
        notebookMarks = marks;
        page = open is not null && open.Secret <= marks ? open : null;
        var box = notebook;
        box.Clear();
        if (page is null)
        {
            Append(box, Options.T("O caderno\n", "The notebook\n"), style.TextBold, SystemStyle.Bright);
            Append(box, Options.T("Duplo clique numa página para abri-la. F1 sobre uma palavra do código abre a página dela.\n", "Double click a page to open it. F1 on a word of the code opens its page.\n"), style.Text, SystemStyle.Dim);
            foreach (var p in all)
            {
                if (p.Secret == 0) Inline(box, $"• {Options.Of(p.Title)}   ({string.Join(" ", p.Keywords.Select(k => $"`{k}`"))})\n", SystemStyle.Ink);
                else if (p.Secret <= marks) Inline(box, $"✦ {Options.Of(p.Title)}\n", SystemStyle.Bright);
                else Append(box, Options.T($"✦ Página secreta: abre com {p.Secret} marcas (você tem {marks})\n", $"✦ Secret page: opens with {p.Secret} marks (you have {marks})\n"), style.Text, SystemStyle.Dim);
            }
        }
        else
        {
            Append(box, Options.Of(page.Title) + "\n", style.TextBold, SystemStyle.Bright);
            Markdown(box, Options.Of(page.Text));
            Inline(box, Options.T("\n**Ctrl+4** volta ao índice do caderno.\n", "\n**Ctrl+4** goes back to the notebook's index.\n"), SystemStyle.Dim);
        }
        box.SelectionStart = 0;
        box.ScrollToCaret();
    }

    // The review of a won night: the marks, what it taught, and the code with how many times each line ran.
    public void ShowReview(NightReview? value)
    {
        reviewed = value;
        var box = review;
        box.Clear();
        if (value is null)
        {
            Append(box, Options.T("A revisão aparece quando você vence a noite.\n", "The review appears when you win the night.\n"), style.Text, SystemStyle.Dim);
            return;
        }
        string Mark(bool got, string name) => $"{(got ? "●" : "○")} {name}";
        int left = (int)Math.Round(value.SecondsLeft);
        Append(box, Options.T("Revisão da noite\n", "Review of the night\n"), style.TextBold, SystemStyle.Bright);
        Append(box, $"{Mark(value.Marks.Unharmed, Options.T("Ileso", "Unharmed"))}   {Mark(value.Marks.NoSweet, Options.T("Sem doce", "No sweet"))}   {Mark(value.Marks.Distance, Options.T("Distância", "Distance"))}\n", style.TextBold, SystemStyle.Pass);
        Append(box, Options.T($"Sobraram {left / 60}:{left % 60:00}. Velas perdidas: {value.CandlesLost}. Tentativas: {value.Attempts}. Doces: {value.Sweets}.\n\n",
            $"Time left: {left / 60}:{left % 60:00}. Candles lost: {value.CandlesLost}. Attempts: {value.Attempts}. Sweets: {value.Sweets}.\n\n"), style.Text, SystemStyle.Ink);
        Append(box, Options.T("O que esta noite ensinou\n", "What this night taught\n"), style.TextBold, SystemStyle.Bright);
        Inline(box, Options.Of(value.Learned) + "\n\n", SystemStyle.Ink);
        Append(box, Options.T("O seu código, com quantas vezes cada linha rodou nos testes\n", "Your code, with how many times each line ran in the tests\n"), style.TextBold, SystemStyle.Bright);
        var code = value.Code.Replace("\r\n", "\n").Split('\n');
        var annotated = code.Select((text, i) => (i + 1 < value.Lines.Count && value.Lines[i + 1] > 0 ? $"{value.Lines[i + 1]}×" : "").PadLeft(6) + " │ " + text);
        Code(box, string.Join("\n", annotated));
        box.SelectionStart = 0;
        box.ScrollToCaret();
    }

    // Markdown as the nights use it: headings, paragraphs, lists, tables, code blocks, `code` and **bold**.
    void Markdown(RichTextBox box, string text)
    {
        var source = text.Replace("\r\n", "\n").Split('\n');
        for (int i = 0; i < source.Length; i++)
        {
            string line = source[i];
            if (line.StartsWith("```", StringComparison.Ordinal))
            {
                var code = new List<string>();
                for (i++; i < source.Length && !source[i].StartsWith("```", StringComparison.Ordinal); i++) code.Add(source[i]);
                Code(box, string.Join("\n", code));
                continue;
            }
            if (line.StartsWith('#'))
            {
                Append(box, line.TrimStart('#').Trim() + "\n", style.TextBold, SystemStyle.Bright);
                continue;
            }
            if (line.StartsWith('|'))
            {
                var rows = new List<string[]>();
                for (; i < source.Length && source[i].StartsWith('|'); i++)
                {
                    var cells = source[i].Trim().Trim('|').Split('|').Select(c => c.Trim()).ToArray();
                    if (cells.All(c => c.Length > 0 && c.All(ch => ch is '-' or ':'))) continue;
                    rows.Add(cells);
                }
                i--;
                Table(box, rows);
                continue;
            }
            string shown = line.StartsWith("- ", StringComparison.Ordinal) ? "• " + line[2..] : line;
            Inline(box, shown + "\n", SystemStyle.Ink);
        }
    }

    // A block of C#, indented and coloured as in the editor.
    void Code(RichTextBox box, string code)
    {
        string block = string.Join("\n", code.Split('\n').Select(l => "  " + l));
        int start = box.TextLength;
        Append(box, block + "\n", style.Text, CodeEditor.NameColor);
        foreach (var token in CSharpTokenizer.Tokenize(block))
        {
            if (token.Kind == TokenKind.Name) continue;
            box.Select(start + token.Start, token.Length);
            box.SelectionColor = CodeEditor.Colour(token.Kind);
        }
        box.Select(box.TextLength, 0);
    }

    // A table in monospaced columns; the header row is brighter.
    void Table(RichTextBox box, List<string[]> rows)
    {
        if (rows.Count == 0) return;
        int columns = rows.Max(r => r.Length);
        var widths = Enumerable.Range(0, columns).Select(c => rows.Max(r => c < r.Length ? Plain(r[c]).Length : 0)).ToArray();
        // Too wide for the panel, the columns would wrap into each other: each row becomes a short block
        // instead, its first cell on top and the others below it, under their headings.
        float letter = TextRenderer.MeasureText(new string('M', 20), style.Text).Width / 20f;
        int fits = box.ClientSize.Width > 0 ? (int)((box.ClientSize.Width - style.Px(24)) / letter) : int.MaxValue;
        if (widths.Sum() + 2 * (columns - 1) > fits && rows.Count > 1)
        {
            for (int r = 1; r < rows.Count; r++)
            {
                if (r > 1) Append(box, "\n", style.Text, SystemStyle.Ink);
                Inline(box, (rows[r].Length > 0 ? rows[r][0] : "") + "\n", SystemStyle.Bright);
                for (int c = 1; c < rows[r].Length; c++)
                    Inline(box, $"  {(c < rows[0].Length ? rows[0][c] : "")}: {rows[r][c]}\n", SystemStyle.Ink);
            }
            return;
        }
        for (int r = 0; r < rows.Count; r++)
        {
            for (int c = 0; c < columns; c++)
            {
                string cell = c < rows[r].Length ? rows[r][c] : "";
                string pad = new(' ', widths[c] - Plain(cell).Length + (c < columns - 1 ? 2 : 0));
                Inline(box, cell + pad, r == 0 ? SystemStyle.Bright : SystemStyle.Ink);
            }
            Append(box, "\n", style.Text, SystemStyle.Ink);
        }
    }

    static string Plain(string text) => text.Replace("`", "").Replace("**", "");

    void Inline(RichTextBox box, string text, Color ink)
    {
        foreach (Match m in InlinePieces().Matches(text))
        {
            if (m.Groups["code"].Success) Append(box, m.Groups["code"].Value, style.Text, CodeEditor.TypeColor);
            else if (m.Groups["bold"].Success) Append(box, m.Groups["bold"].Value, style.TextBold, SystemStyle.Bright);
            else Append(box, m.Value, style.Text, ink);
        }
    }

    static void Append(RichTextBox box, string text, Font font, Color color)
    {
        box.SelectionStart = box.TextLength;
        box.SelectionFont = font;
        box.SelectionColor = color;
        box.AppendText(text);
    }

    [GeneratedRegex(@"`(?<code>[^`]+)`|\*\*(?<bold>[^*]+)\*\*|[^`*]+|[`*]")]
    private static partial Regex InlinePieces();

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            readme.Dispose();
            output.Dispose();
            hints.Dispose();
            notebook.Dispose();
            review.Dispose();
        }
        base.Dispose(disposing);
    }
}
