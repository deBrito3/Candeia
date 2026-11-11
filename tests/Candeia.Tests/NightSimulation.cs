using System.Globalization;
using Candeia.Challenges;
using Candeia.Creature;
using Candeia.Night;

namespace Candeia.Tests;

// Plays scripted nights through the real clock and creature director, a quarter of a second at a time,
// and prints what the player would live through. The runs are described, not compiled, so the timeline
// shows the rules alone.
static class NightSimulation
{
    enum Act { Run, Crash, CompileError, AcceptSweet, DeclineSweet }

    sealed record Step(double At, Act Action, int[]? Lamps = null);

    sealed record Scenario(string Title, int ActNumber, Difficulty Difficulty, int Lamps, Step[] Steps, IReadOnlyList<NightEvent>? Events = null, string? Concept = null);

    public static int Run(TextWriter output)
    {
        var rules = NightRules.Load();
        int[] Upto(int count) => Enumerable.Range(0, count).ToArray();
        var scenarios = new Scenario[]
        {
            new("Noite 1, A luz do quarto: a primeira lâmpada, o vulto no quintal e a vitória", 1, Difficulty.Night, 4,
                [new(25, Act.Run, [0]), new(70, Act.CompileError), new(95, Act.Run, [0, 1]), new(140, Act.Run, Upto(4))],
                [new NightEvent("first-new-lamp", "silhouette", "yard", 0.6)]),
            new("Noite 2, A bateria do sistema: progresso aos poucos", 1, Difficulty.Night, 13,
                [new(50, Act.Run, [0, 1]), new(100, Act.Run, [0, 1, 4, 5, 6]), new(140, Act.Run, [0, 1, 4, 5, 6]), new(190, Act.Run, Upto(10)), new(240, Act.Run, Upto(13))]),
            new("Noite 2: alguém que erra três vezes seguidas e recusa o doce", 1, Difficulty.Night, 13,
                [new(40, Act.Run, []), new(70, Act.Run, []), new(72, Act.DeclineSweet), new(100, Act.Run, []), new(150, Act.Run, [0, 1, 2]), new(230, Act.Run, Upto(13))]),
            new("Noite 2: o código trava duas vezes e o doce é aceito", 1, Difficulty.Night, 13,
                [new(60, Act.Crash), new(85, Act.Crash), new(88, Act.AcceptSweet), new(160, Act.Run, Upto(13))]),
            new("Noite 2: alguém que trava e não executa mais nada, até a captura", 1, Difficulty.Night, 13, []),
            new("A mesma noite na dificuldade Vigília: mais tempo, quatro velas, doce de graça", 1, Difficulty.Vigil, 13, [new(200, Act.AcceptSweet)]),
            new("Ato 2: três erros seguidos trazem o avanço extra", 2, Difficulty.Night, 10, [new(30, Act.Run, []), new(60, Act.Run, []), new(62, Act.DeclineSweet), new(90, Act.Run, [])]),
        };
        foreach (var scenario in scenarios) Simulate(rules, scenario, output);
        return 0;
    }

    static void Simulate(NightRules rules, Scenario scenario, TextWriter output)
    {
        var penalties = new PenaltyRules(rules, scenario.ActNumber, scenario.Difficulty);
        var clock = new NightClock(penalties, scenario.Lamps);
        var director = new CreatureDirector(penalties, scenario.Events);
        output.WriteLine($"## {scenario.Title}");
        output.WriteLine();
        output.WriteLine($"Ato {scenario.ActNumber} · {Name(scenario.Difficulty)} · {Clock(clock.StartSeconds)} no relógio · {penalties.Candles} velas · {scenario.Lamps} lâmpadas");
        output.WriteLine();
        double elapsed = 0;
        void Log(IReadOnlyList<ClockEvent> events, IReadOnlyList<CreatureCue> cues)
        {
            foreach (var e in events)
                if (Describe(e) is { } text) output.WriteLine($"- `{Clock(elapsed)}` {text} (resta {Clock(clock.Remaining)}, {StationName(director.State.Station)})");
            foreach (var cue in cues)
                if (Describe(cue, director.State.Visible) is { } text) output.WriteLine($"- `{Clock(elapsed)}` {text}");
        }
        var start = clock.BeginCoding();
        Log(start, director.Update(clock, start));
        var pending = new Queue<Step>(scenario.Steps.OrderBy(s => s.At));
        while (elapsed < 3600 && clock.State is not (ClockState.Captured or ClockState.Won))
        {
            while (pending.Count > 0 && pending.Peek().At <= elapsed + 1e-9)
            {
                var step = pending.Dequeue();
                IReadOnlyList<ClockEvent> events;
                switch (step.Action)
                {
                    case Act.CompileError: events = clock.Listened(); break;
                    case Act.AcceptSweet: events = clock.AcceptSweet(); break;
                    case Act.DeclineSweet: clock.DeclineSweet(); output.WriteLine($"- `{Clock(elapsed)}` recusou o doce"); events = []; break;
                    default:
                        var passed = Enumerable.Range(0, scenario.Lamps).Select(i => step.Lamps?.Contains(i) == true).ToList();
                        clock.BeginRun();
                        output.WriteLine($"- `{Clock(elapsed)}` executa o código" + (step.Action == Act.Crash ? ", que trava" : $": {passed.Count(p => p)} de {scenario.Lamps} testes passam"));
                        events = clock.ApplyRun(new RunOutcome(passed, step.Action == Act.Crash, []));
                        break;
                }
                Log(events, director.Update(clock, events));
            }
            var tick = clock.Advance(0.25);
            elapsed += 0.25;
            Log(tick, director.Update(clock, tick));
        }
        var marks = clock.Marks;
        output.WriteLine();
        output.WriteLine(clock.State == ClockState.Won
            ? $"**Resultado:** venceu aos {Clock(elapsed)}, com {Clock(clock.Remaining)} de sobra. Marcas: Ileso {Yes(marks.Unharmed)}, Sem doce {Yes(marks.NoSweet)}, Distância {Yes(marks.Distance)}."
            : $"**Resultado:** capturado aos {Clock(elapsed)}. A noite recomeça com o código mantido e uma página nova do caderno.");
        output.WriteLine();
    }

    static string? Describe(ClockEvent e) => e.Kind switch
    {
        ClockEventKind.Started => "a noite começa",
        ClockEventKind.LampsLit => e.Count == 1 ? "acende 1 lâmpada nova" : $"acendem {e.Count} lâmpadas novas",
        ClockEventKind.Bonus => $"o relógio ganha {Clock(e.Seconds)}",
        ClockEventKind.NoProgressCost => $"execução sem lâmpada nova, a {e.Count}ª seguida: perde {Clock(-e.Seconds)}",
        ClockEventKind.CrashCost => $"o código travou: perde {Clock(-e.Seconds)}",
        ClockEventKind.StreakAdvanceCost => $"a insistência faz barulho e ele avança mais: perde {Clock(-e.Seconds)}",
        ClockEventKind.SweetCost => e.Seconds == 0 ? "aceita o doce, de graça nesta dificuldade" : $"aceita o doce: perde {Clock(-e.Seconds)} e a marca Sem doce",
        ClockEventKind.Listened => "erro de compilação: ele escuta, mas não se mexe",
        ClockEventKind.ConceptsMissing => $"todas acesas, mas ainda falta {e.Detail}",
        ClockEventKind.SweetOffered => "um doce aparece no parapeito",
        ClockEventKind.NearCapture => $"o tempo acaba: uma vela apaga (restam {e.Count}), ele recua e há 5 s de respiro",
        ClockEventKind.RespiteOver => "fim do respiro, o relógio volta a correr",
        ClockEventKind.Captured => "a última vela apaga: **captura**",
        ClockEventKind.Won => "todas as lâmpadas acesas: **ele foge**",
        _ => null,
    };

    static string? Describe(CreatureCue cue, bool visible) => cue.Kind switch
    {
        CueKind.Approaching => $"som: ele está chegando {To(cue.Station)}",
        CueKind.Arrived => $"ele chega {To(cue.Station)}" + (visible ? "" : " (só se ouve, não se vê)"),
        CueKind.Retreated => $"ele recua {To(cue.Station)}",
        CueKind.Scripted => $"susto roteirizado: {(cue.Effect == "silhouette" ? "o vulto aparece" : "um som")} {StationName(cue.Where ?? cue.Station)} por {cue.Seconds.ToString("0.#", CultureInfo.InvariantCulture)} s",
        _ => null,
    };

    static string StationName(Station station) => station switch
    {
        Station.Street => "na rua", Station.Gate => "no portão", Station.Yard => "no quintal",
        Station.Window => "na janela", Station.Door => "na porta do quarto", _ => "ao seu lado",
    };

    static string To(Station station) => station switch
    {
        Station.Street => "para a rua", Station.Gate => "ao portão", Station.Yard => "ao quintal",
        Station.Window => "à janela", Station.Door => "à porta do quarto", _ => "ao seu lado",
    };

    static string Name(Difficulty difficulty) => difficulty switch { Difficulty.Vigil => "Vigília", Difficulty.Night => "Noite", _ => "Pesadelo" };

    static string Yes(bool value) => value ? "sim" : "não";

    static string Clock(double seconds)
    {
        int total = (int)Math.Round(Math.Max(0, seconds));
        return $"{total / 60}:{total % 60:00}";
    }
}
