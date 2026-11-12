using System.Drawing.Drawing2D;
using System.Drawing.Text;
using Candeia.Challenges;
using Candeia.Night;
using Candeia.Progress;

namespace Candeia.Screens;

// The CANDEIA menu, before and between the nights: the boot screen; the choice of difficulty in a new
// campaign; the menu (Continue, Nights, Options, Quit); the list of nights with their marks; and the
// options. It knows nothing of windows: MainWindow paints it and passes the keys, and the tests drive it
// directly.
//   ↑ ↓ choose · Enter confirm · Esc back · ← → change an option · R (in Nights) replay from the original code
sealed class CandeiaMenu
{
    public enum Page { Boot, Difficulty, Main, Nights, Options }

    // The rows of the options page, in order.
    public enum Row { Language, Difficulty, Font, Contrast, Captions, SoftScares, House, System, Creature, Music }

    readonly Campaign campaign;
    bool confirmFresh;

    public CandeiaMenu(Campaign campaign, SaveProblem? problem = null)
    {
        this.campaign = campaign;
        Notice = problem switch
        {
            SaveProblem.RecoveredFromBackup => new("O arquivo salvo estava danificado: o jogo usou a cópia de reserva.", "The save file was damaged: the game used the backup copy."),
            SaveProblem.Lost => new("O arquivo salvo estava danificado e não havia cópia boa: o jogo começa do zero.", "The save file was damaged and there was no good copy: the game starts over."),
            _ => null,
        };
    }

    public Page Current { get; private set; } = Page.Boot;
    public int Selected { get; private set; }
    // A line for the player under the menu: a save problem, or what a second R would do.
    public Bilingual? Notice { get; private set; }

    // Play a night; Fresh is true when the player chose to start from the night's original code.
    public event Action<NightDefinition, bool>? Play;
    public event Action? Quit;

    public void Show(Page page)
    {
        Current = page;
        // Night, the default difficulty, comes chosen; Vigil is one key above it.
        Selected = page switch { Page.Nights => Math.Max(0, IndexOf(campaign.Continue)), Page.Difficulty => 1, _ => 0 };
        confirmFresh = false;
    }

    int IndexOf(NightDefinition night)
    {
        for (int i = 0; i < campaign.Nights.Count; i++) if (campaign.Nights[i].Id == night.Id) return i;
        return -1;
    }

    int Count => Current switch
    {
        Page.Difficulty => 2,
        Page.Main => 4,
        Page.Nights => campaign.Nights.Count,
        Page.Options => Enum.GetValues<Row>().Length,
        _ => 0,
    };

    // A key. True when it meant something here.
    public bool Key(Keys key)
    {
        if (key != Keys.R) confirmFresh = false;
        switch (key)
        {
            case Keys.Up when Count > 0:
                Selected = (Selected + Count - 1) % Count;
                return true;
            case Keys.Down when Count > 0:
                Selected = (Selected + 1) % Count;
                return true;
            case Keys.Left when Current == Page.Options:
                Change((Row)Selected, -1);
                return true;
            case Keys.Right when Current == Page.Options:
                Change((Row)Selected, +1);
                return true;
            case Keys.S when Current == Page.Boot:
                Options.Change(Options.Settings with { SoftScares = !Options.Settings.SoftScares });
                return true;
            case Keys.R when Current == Page.Nights:
                Replay();
                return true;
            case Keys.Escape:
                if (Current is Page.Nights or Page.Options) Show(Page.Main);
                else if (Current == Page.Difficulty) Show(Page.Boot);
                return true;
            case Keys.Enter:
                Enter();
                return true;
        }
        return false;
    }

    void Enter()
    {
        switch (Current)
        {
            case Page.Boot:
                Notice = null;
                Show(campaign.Started ? Page.Main : Page.Difficulty);
                break;
            case Page.Difficulty:
                campaign.Begin(Selected == 0 ? Difficulty.Vigil : Difficulty.Night);
                Options.Use(campaign);
                Show(Page.Main);
                Play?.Invoke(campaign.Continue, false);
                break;
            case Page.Main:
                switch (Selected)
                {
                    case 0: Play?.Invoke(campaign.Continue, false); break;
                    case 1: Show(Page.Nights); break;
                    case 2: Show(Page.Options); break;
                    default: Quit?.Invoke(); break;
                }
                break;
            case Page.Nights:
                var night = campaign.Nights[Selected];
                if (campaign.Unlocked(night)) Play?.Invoke(night, false);
                break;
            case Page.Options:
                Change((Row)Selected, +1);
                break;
        }
    }

    // R once warns; R again plays the night from its original code, letting the player's code go.
    void Replay()
    {
        var night = campaign.Nights[Selected];
        if (!campaign.Unlocked(night)) return;
        if (!confirmFresh)
        {
            confirmFresh = true;
            Notice = new("R de novo começa esta noite do código original, e o seu código dela vai embora.", "R again starts this night from its original code, and your code for it is let go.");
            return;
        }
        confirmFresh = false;
        Notice = null;
        Play?.Invoke(night, true);
    }

    static readonly double[] Steps = [0, 0.1, 0.2, 0.3, 0.4, 0.5, 0.6, 0.7, 0.8, 0.9, 1];

    static double Step(double value, int by)
    {
        int at = Array.FindIndex(Steps, s => Math.Abs(s - value) < 0.05);
        if (at < 0) at = Steps.Length - 1;
        return Steps[Math.Clamp(at + by, 0, Steps.Length - 1)];
    }

    void Change(Row row, int by)
    {
        var s = Options.Settings;
        Options.Change(row switch
        {
            Row.Language => s with { Language = s.English ? "pt" : "en" },
            Row.Difficulty => s with { Difficulty = s.Difficulty == Difficulty.Vigil ? Difficulty.Night : Difficulty.Vigil },
            Row.Font => s with { FontStep = Math.Clamp(s.FontStep + by, 0, Settings.FontScales.Length - 1) },
            Row.Contrast => s with { HighContrast = !s.HighContrast },
            Row.Captions => s with { Captions = !s.Captions },
            Row.SoftScares => s with { SoftScares = !s.SoftScares },
            Row.House => s with { HouseVolume = Step(s.HouseVolume, by) },
            Row.System => s with { SystemVolume = Step(s.SystemVolume, by) },
            Row.Creature => s with { CreatureVolume = Step(s.CreatureVolume, by) },
            _ => s with { MusicVolume = Step(s.MusicVolume, by) },
        });
    }

    // The lines of the current page, as the player reads them, the selected one marked by Selected.
    public IReadOnlyList<string> Lines()
    {
        static string T(string pt, string en) => Options.T(pt, en);
        string Yes(bool on) => on ? T("sim", "yes") : T("não", "no");
        string Percent(double v) => $"{Math.Round(v * 100)}%";
        var s = Options.Settings;
        switch (Current)
        {
            case Page.Difficulty:
                return
                [
                    T("Vigília · para quem nunca programou: mais tempo, quatro velas, dicas de graça, e a primeira noite explica do zero",
                      "Vigil · for people who never programmed: more time, four candles, free hints, and the first night explains from zero"),
                    T("Noite · para quem já começou a programar: o jogo como foi pensado", "Night · for people who have started programming: the game as it was meant"),
                ];
            case Page.Main:
                var next = campaign.Continue;
                string go = campaign.AllWon
                    ? T($"Continuar · o Ato 1 terminou: rever a noite {next.Order}", $"Continue · Act 1 is over: replay night {next.Order}")
                    : T($"Continuar · Noite {next.Order}, {next.Title.Pt}", $"Continue · Night {next.Order}, {next.Title.En}");
                return [go, T("Noites", "Nights"), T("Opções", "Options"), T("Sair", "Quit")];
            case Page.Nights:
                return campaign.Nights.Select(n =>
                {
                    string title = $"{n.Order}  {Options.Of(n.Title)}";
                    if (!campaign.Unlocked(n)) return $"{title}  ·  {T("vença a noite anterior", "win the night before")}";
                    if (campaign.Record(n).Best is not { } best) return $"{title}  ·  {T("aberta", "open")}";
                    int left = (int)Math.Round(best.SecondsLeft);
                    return $"{title}  ·  {Mark(best.Unharmed)} {Mark(best.NoSweet)} {Mark(best.Distance)}  ·  {T("sobraram", "left")} {left / 60}:{left % 60:00}";
                }).ToList();
            case Page.Options:
                return
                [
                    $"{T("Idioma", "Language")}: {(s.English ? "English" : "Português")}",
                    $"{T("Dificuldade", "Difficulty")}: {(s.Difficulty == Difficulty.Vigil ? T("Vigília", "Vigil") : T("Noite", "Night"))}  ({T("vale a partir da próxima noite", "from the next night on")})",
                    $"{T("Fonte do editor", "Editor font")}: {Percent(s.FontScale)}  (Ctrl+= Ctrl+-)",
                    $"{T("Alto contraste no editor", "High contrast editor")}: {Yes(s.HighContrast)}",
                    $"{T("Legendas dos sons", "Sound captions")}: {Yes(s.Captions)}",
                    $"{T("Sustos suaves, sem clarões", "Soft scares, no flashes")}: {Yes(s.SoftScares)}",
                    $"{T("Volume da casa", "House volume")}: {Percent(s.HouseVolume)}",
                    $"{T("Volume do sistema", "System volume")}: {Percent(s.SystemVolume)}",
                    $"{T("Volume dele", "His volume")}: {Percent(s.CreatureVolume)}",
                    $"{T("Volume da trilha", "Score volume")}: {Percent(s.MusicVolume)}",
                ];
        }
        return [];
    }

    static string Mark(bool got) => got ? "●" : "○";

    // A line about the page: the keys, and for the nights, the marks.
    string Help() => Current switch
    {
        Page.Difficulty => Options.T("↑ ↓ escolhe · Enter começa · a dificuldade muda depois nas Opções", "↑ ↓ choose · Enter begins · the difficulty can change later in Options"),
        Page.Main => Options.T("↑ ↓ escolhe · Enter confirma", "↑ ↓ choose · Enter confirms"),
        Page.Nights => Options.T($"Marcas: {campaign.Marks} de {campaign.MarksPossible} · Enter joga · R recomeça do código original · Esc volta",
                                 $"Marks: {campaign.Marks} of {campaign.MarksPossible} · Enter plays · R starts from the original code · Esc back"),
        Page.Options => Options.T("↑ ↓ escolhe · ← → muda · Esc volta", "↑ ↓ choose · ← → change · Esc back"),
        _ => "",
    };

    string Heading() => Current switch
    {
        Page.Difficulty => Options.T("Como você quer passar as noites?", "How do you want to spend the nights?"),
        Page.Main => Options.T("Ato 1 · A casa acorda", "Act 1 · The house wakes"),
        Page.Nights => Options.T("As noites", "The nights"),
        Page.Options => Options.T("Opções", "Options"),
        _ => "",
    };

    public void Paint(Graphics g, Size size, TimeSpan elapsed)
    {
        if (Current == Page.Boot)
        {
            BootScreen.Paint(g, size, elapsed);
            if (Notice is { } boot) DrawNotice(g, size, Options.Of(boot));
            return;
        }
        g.Clear(Palette.Night);
        if (size.Width <= 0 || size.Height <= 0) return;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
        float unit = Math.Min(size.Width, size.Height * 1.6f) / 100f;
        using var title = new Font("Consolas", unit * 4.2f, FontStyle.Bold, GraphicsUnit.Pixel);
        using var text = new Font("Consolas", unit * 1.8f, FontStyle.Regular, GraphicsUnit.Pixel);
        using var bold = new Font("Consolas", unit * 1.8f, FontStyle.Bold, GraphicsUnit.Pixel);
        using var amber = new SolidBrush(Palette.Amber);
        using var dim = new SolidBrush(Palette.AmberDim);

        float left = size.Width * 0.12f, y = size.Height * 0.12f, width = size.Width * 0.76f;
        g.DrawString(GameInfo.SystemName, title, amber, left, y);
        y += g.MeasureString(GameInfo.SystemName, title).Height * 1.1f;
        g.DrawString(Heading(), bold, dim, left, y);
        y += unit * 5;
        var lines = Lines();
        var format = new StringFormat { Trimming = StringTrimming.EllipsisCharacter };
        for (int i = 0; i < lines.Count; i++)
        {
            bool chosen = i == Selected;
            bool locked = Current == Page.Nights && !campaign.Unlocked(campaign.Nights[i]);
            string line = (chosen ? "> " : "  ") + lines[i];
            var box = new RectangleF(left, y, width, unit * 6);
            var measured = g.MeasureString(line, chosen ? bold : text, (int)width, format);
            g.DrawString(line, chosen ? bold : text, locked ? dim : chosen ? amber : dim, box with { Height = measured.Height + 2 }, format);
            y += measured.Height + unit * 1.2f;
        }
        string help = Help();
        var helpSize = g.MeasureString(help, text);
        g.DrawString(help, text, dim, size.Width / 2f - helpSize.Width / 2f, size.Height - unit * 6);
        if (Notice is { } notice) DrawNotice(g, size, Options.Of(notice));
    }

    // One picture of each page, for visual review, with a campaign in memory that has won two nights.
    public static int SaveShots(IReadOnlyList<NightDefinition> nights, string folder)
    {
        Directory.CreateDirectory(folder);
        var campaign = new Campaign(nights, store: null);
        campaign.Begin(Difficulty.Night);
        campaign.RecordWin(nights[0], new NightMarks(true, true, true), 201, 1);
        if (nights.Count > 1) campaign.RecordWin(nights[1], new NightMarks(true, false, false), 64, 2);
        Options.Use(campaign);
        var menu = new CandeiaMenu(new Campaign(nights, store: null));
        var size = new Size(1600, 900);
        void Shot(CandeiaMenu which, string name)
        {
            using var image = new Bitmap(size.Width, size.Height);
            using (var g = Graphics.FromImage(image)) which.Paint(g, size, TimeSpan.Zero);
            image.Save(Path.Combine(folder, name), System.Drawing.Imaging.ImageFormat.Png);
        }
        Shot(menu, "1-inicio.png");
        menu.Key(Keys.Enter);
        Shot(menu, "2-dificuldade.png");
        var played = new CandeiaMenu(campaign);
        played.Show(Page.Main);
        Shot(played, "3-menu.png");
        played.Show(Page.Nights);
        Shot(played, "4-noites.png");
        played.Show(Page.Options);
        Shot(played, "5-opcoes.png");
        Options.Change(Options.Settings with { Language = "en" });
        Shot(played, "6-options-en.png");
        played.Show(Page.Nights);
        Shot(played, "7-nights-en.png");
        Options.Change(Options.Settings with { Language = "pt" });
        return 0;
    }

    static void DrawNotice(Graphics g, Size size, string notice)
    {
        float unit = Math.Min(size.Width, size.Height * 1.6f) / 100f;
        using var font = new Font("Consolas", unit * 1.8f, FontStyle.Bold, GraphicsUnit.Pixel);
        using var brush = new SolidBrush(Palette.Amber);
        var measured = g.MeasureString(notice, font);
        g.DrawString(notice, font, brush, size.Width / 2f - measured.Width / 2f, size.Height - unit * 10);
    }
}
