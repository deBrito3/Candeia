using Candeia.Languages.CSharp;

namespace Candeia.Panels;

// Suggestions while typing, adapted from Projeto Aurora: after two letters, a list of the C# words, the
// members the night allows (Math, Console) and the names already in the code, each with what it does.
// ↑↓ choose, Tab or Enter insert, Esc close. Nothing is offered inside comments or strings.
sealed class CodeCompletion : IDisposable
{
    sealed class SuggestionWindow : Form
    {
        protected override bool ShowWithoutActivation => true;
        protected override CreateParams CreateParams
        {
            get
            {
                var p = base.CreateParams;
                p.ExStyle |= 0x08000000; // WS_EX_NOACTIVATE
                return p;
            }
        }
    }

    static readonly Color Back = Color.FromArgb(24, 18, 10), Chosen = Color.FromArgb(58, 40, 16), Ink = Color.FromArgb(232, 162, 72), Bright = Color.FromArgb(255, 224, 156), Note = Color.FromArgb(150, 108, 52);

    // The words of the language and of the house system, with what each one does, in each language.
    static readonly (string Word, string Pt, string En)[] Vocabulary =
    [
        ("int", "número inteiro", "whole number"), ("double", "número com casas decimais", "number with decimals"),
        ("bool", "verdadeiro ou falso", "true or false"), ("string", "texto entre aspas", "text in quotes"),
        ("char", "um caractere entre aspas simples", "one character in single quotes"), ("long", "número inteiro grande", "big whole number"),
        ("var", "o tipo vem do valor", "the type comes from the value"), ("void", "o método não devolve nada", "the method returns nothing"),
        ("true", "verdadeiro", "true"), ("false", "falso", "false"),
        ("return", "devolve o resultado do método", "hands back the method's result"), ("if", "executa só quando a condição é verdadeira", "runs only when the condition is true"),
        ("else", "senão", "otherwise"), ("while", "repete enquanto for verdade", "repeats while it is true"),
        ("for", "repete contando", "repeats, counting"), ("foreach", "percorre cada item", "goes through each item"),
        ("switch", "escolhe entre casos", "chooses between cases"), ("case", "um caso do switch", "a case of the switch"),
        ("break", "sai da repetição ou do caso", "leaves the loop or the case"), ("continue", "pula para a próxima volta", "skips to the next round"),
        ("public", "visível para o sistema da casa", "visible to the house system"), ("static", "pertence à classe, sem objeto", "belongs to the class, no object"),
        ("class", "declara uma classe", "declares a class"), ("new", "cria um objeto", "creates an object"),
        ("null", "nada", "nothing"),
        ("Math.Abs", "valor sem o sinal", "value without its sign"), ("Math.Max", "o maior de dois valores", "the larger of two values"),
        ("Math.Min", "o menor de dois valores", "the smaller of two values"), ("Math.Round", "arredonda", "rounds"),
        ("Math.Floor", "arredonda para baixo", "rounds down"), ("Math.Ceiling", "arredonda para cima", "rounds up"),
        ("Math.Sqrt", "raiz quadrada", "square root"), ("Math.Pow", "potência", "power"),
        ("Math.PI", "o número pi", "the number pi"), ("Console.WriteLine", "escreve na SAÍDA e pula a linha", "writes to the OUTPUT and ends the line"),
        ("Console.Write", "escreve na SAÍDA", "writes to the OUTPUT"),
    ];

    readonly Form owner;
    readonly CodeEditor code;
    readonly SuggestionWindow popup = new() { FormBorderStyle = FormBorderStyle.None, ShowInTaskbar = false, StartPosition = FormStartPosition.Manual };
    readonly ListBox list = new() { Dock = DockStyle.Fill, BorderStyle = BorderStyle.None, IntegralHeight = false, DrawMode = DrawMode.OwnerDrawFixed };
    bool inserting;
    int tokenStart;

    public CodeCompletion(Form owner, CodeEditor code)
    {
        this.owner = owner;
        this.code = code;
        popup.BackColor = Color.FromArgb(70, 50, 22);
        popup.Padding = new Padding(1);
        list.BackColor = Back;
        list.ForeColor = Ink;
        popup.Controls.Add(list);
        list.MouseClick += (_, _) => Accept();
        list.DrawItem += DrawItem;
        code.TextChanged += Update;
        code.SelectionChanged += Update;
        code.LostFocus += (_, _) => { if (!popup.ContainsFocus) Hide(); };
        code.VScroll += (_, _) => Hide();
        code.HScroll += (_, _) => Hide();
        owner.Move += (_, _) => Hide();
        owner.Resize += (_, _) => Hide();
        owner.Deactivate += (_, _) => Hide();
    }

    public bool Visible => popup.Visible;

    void DrawItem(object? sender, DrawItemEventArgs e)
    {
        if (e.Index < 0) return;
        bool chosen = (e.State & DrawItemState.Selected) != 0;
        using (var back = new SolidBrush(chosen ? Chosen : Back)) e.Graphics.FillRectangle(back, e.Bounds);
        string word = list.Items[e.Index]?.ToString() ?? "";
        var size = TextRenderer.MeasureText(word, list.Font);
        TextRenderer.DrawText(e.Graphics, word, list.Font, new Point(e.Bounds.Left + 6, e.Bounds.Top + 1), chosen ? Bright : Ink, TextFormatFlags.NoPadding);
        if (Describe(word) is { } text)
            TextRenderer.DrawText(e.Graphics, text, list.Font, Rectangle.FromLTRB(e.Bounds.Left + 18 + Math.Max(size.Width, list.Font.Height * 5), e.Bounds.Top, e.Bounds.Right - 4, e.Bounds.Bottom),
                Note, TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.SingleLine | TextFormatFlags.NoPadding);
    }

    public static string? Describe(string word) => Vocabulary.FirstOrDefault(v => v.Word == word) is { Word: not null } v ? Options.T(v.Pt, v.En) : null;

    // The word being typed before the caret (letters, digits, _ and dots), when it is worth completing.
    public static string Prefix(string text, int caret, out int start)
    {
        start = caret;
        if (caret < 0 || caret > text.Length || CSharpTokenizer.InCommentOrText(text, caret)) return "";
        while (start > 0 && (char.IsLetterOrDigit(text[start - 1]) || text[start - 1] is '_' or '.')) start--;
        string prefix = text[start..caret];
        return prefix.Length >= 2 && (char.IsLetter(prefix[0]) || prefix[0] == '_') ? prefix : "";
    }

    public static string[] Matches(string prefix, string text) =>
        Vocabulary.Select(v => v.Word).Concat(CSharpTokenizer.DeclaredNames(text))
            .Where(w => w.StartsWith(prefix, StringComparison.Ordinal) && w != prefix)
            .Distinct().OrderBy(w => w.Contains('.')).ThenBy(w => w, StringComparer.Ordinal).Take(8).ToArray();

    void Update(object? sender, EventArgs e)
    {
        if (inserting) return;
        if (code.ReadOnly || code.SelectionLength != 0 || !code.Focused) { Hide(); return; }
        string text = code.Text;
        var prefix = Prefix(text, code.SelectionStart, out tokenStart);
        if (prefix.Length == 0) { Hide(); return; }
        var words = Matches(prefix, text);
        if (words.Length == 0) { Hide(); return; }
        list.Font = code.Font;
        list.ItemHeight = code.Font.Height + 4;
        list.BeginUpdate();
        list.Items.Clear();
        list.Items.AddRange(words);
        list.SelectedIndex = 0;
        list.EndUpdate();
        popup.Size = new Size(code.Font.Height * 22, words.Length * list.ItemHeight + 4);
        var position = code.PointToScreen(code.GetPositionFromCharIndex(code.SelectionStart));
        position.Y += (int)(code.Font.Height * code.ZoomFactor) + 3;
        var screen = Screen.FromControl(code).WorkingArea;
        popup.Location = new Point(Math.Clamp(position.X, screen.Left, Math.Max(screen.Left, screen.Right - popup.Width)),
            Math.Clamp(position.Y, screen.Top, Math.Max(screen.Top, screen.Bottom - popup.Height)));
        if (!popup.Visible) popup.Show(owner);
    }

    // Called by the editor's owner before its own keys: true when the key was for the list.
    public bool HandleKey(Keys key)
    {
        if (!popup.Visible) return false;
        switch (key)
        {
            case Keys.Escape: Hide(); return true;
            case Keys.Up or Keys.Down:
                list.SelectedIndex = Math.Clamp(list.SelectedIndex + (key == Keys.Down ? 1 : -1), 0, list.Items.Count - 1);
                return true;
            case Keys.Tab or Keys.Enter: Accept(); return true;
            default: return false;
        }
    }

    void Accept()
    {
        if (list.SelectedItem is not string word || code.ReadOnly) { Hide(); return; }
        inserting = true;
        try
        {
            int caret = code.SelectionStart, end = caret;
            string text = code.Text;
            while (end < text.Length && (char.IsLetterOrDigit(text[end]) || text[end] == '_')) end++;
            code.Select(tokenStart, end - tokenStart);
            code.SelectedText = word;
            Hide();
            code.Focus();
        }
        finally { inserting = false; }
    }

    void Hide()
    {
        if (!popup.IsDisposed && popup.Visible) popup.Hide();
    }

    public void Dispose()
    {
        code.TextChanged -= Update;
        code.SelectionChanged -= Update;
        popup.Dispose();
    }
}
