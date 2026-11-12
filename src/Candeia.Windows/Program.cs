using Candeia.Challenges;
using Candeia.Screens;

namespace Candeia;

static class Program
{
    // Modes:
    //   (no arguments)              the CANDEIA boot screen and menu, with the save in %LOCALAPPDATA%/Candeia
    //   --night <id>                opens a night directly, without the save, for example --night act1-02-system-battery
    //   --runner                    the isolated runner the game starts for the player's code (never by hand)
    //   --shot <file.png>           renders the boot screen to an image and exits (visual review)
    //   --night-images <folder>     plays the moments of night 1 by itself and saves one image of each
    //   --night-bench <report.md>   measures typing, frame rate and processor on the night screen
    //                [--fps 30]     holding a frame rate, whatever the power
    //   --ui-test <report.txt>      checks the editor and the night's keys in real windows
    //   --menu-images <folder>      saves one image of each page of the CANDEIA menu, without the save
    //   --scene                     opens the rendering prototype: the room, the candles, the Papa-Figo
    //   --scene-bench <report.md>   measures the prototype at 1920×1080 and writes the report and images
    //   --scene-images <folder>     saves one image of the prototype per station
    //   --scene-profile <file.md>   renders each station in memory and writes the cost of each layer
    [STAThread]
    static int Main(string[] args)
    {
        // First, before anything else touches the console or loads the window toolkit.
        if (args is [Execution.Runner.Argument]) return Execution.Runner.Main(Console.OpenStandardInput(), Console.OpenStandardOutput());
        ApplicationConfiguration.Initialize();
        int shot = Array.IndexOf(args, "--shot");
        if (shot >= 0)
        {
            if (shot + 1 >= args.Length) return 2;
            BootScreen.SaveShot(args[shot + 1], new Size(1280, 800));
            return 0;
        }
        switch (args)
        {
            case ["--scene"]:
                Application.Run(new Scene.ScenePreviewWindow(benchmark: false));
                return 0;
            case ["--scene-bench", var report]:
                return Scene.SceneBench.Run(report, imagesOnly: false);
            case ["--scene-images", var folder]:
                return Scene.SceneBench.Run(Path.Combine(folder, "imagens.md"), imagesOnly: true);
            case ["--scene-profile", var profile]:
                return Scene.SceneBench.Profile(profile);
        }

        var nights = Nights();
        if (nights.Count == 0) return 1;
        switch (args)
        {
            case ["--night", var id]:
                var night = nights.FirstOrDefault(n => n.Id == id);
                if (night is null)
                {
                    MessageBox.Show($"Noite não encontrada: {id}", GameInfo.Name);
                    return 2;
                }
                CrashReport.Install();
                Application.Run(new NightScreen(night, nights));
                return 0;
            case ["--night-images", var folder]:
                return NightBench.Run(nights, folder, images: true);
            case ["--night-bench", var report, "--fps", var fps] when double.TryParse(fps, System.Globalization.CultureInfo.InvariantCulture, out double held):
                return NightBench.Run(nights, report, images: false, held);
            case ["--night-bench", var report]:
                return NightBench.Run(nights, report, images: false);
            case ["--ui-test", var report]:
                return UiTests.Run(nights, report);
            case ["--menu-images", var folder]:
                return CandeiaMenu.SaveShots(nights, folder);
        }
        // Arguments the game does not know never open it with the real save: a mistyped test command
        // fails instead of starting a campaign.
        if (args.Length > 0)
        {
            Console.Error.WriteLine($"Unknown arguments: {string.Join(" ", args)}");
            return 2;
        }
        var (campaign, problem) = Progress.Campaign.Load(nights, new Progress.SaveStore(Progress.SaveStore.DefaultFolder));
        Options.Use(campaign);
        CrashReport.Campaign = campaign;
        CrashReport.Install();
        Application.Run(new MainWindow(campaign, problem));
        return 0;
    }

    // Every night in content/, in campaign order. A night that fails to load is reported and left out.
    static List<NightDefinition> Nights()
    {
        var loaded = NightLoader.LoadAll();
        var broken = loaded.Where(l => !l.Ok).ToList();
        if (broken.Count > 0)
            MessageBox.Show("Noites com problema, deixadas de fora:\n" + string.Join("\n", broken.Select(b => $"{Path.GetFileName(b.Folder)}: {string.Join("; ", b.Errors)}")), GameInfo.Name);
        var nights = loaded.Where(l => l.Ok).Select(l => l.Night!).OrderBy(n => n.Act).ThenBy(n => n.Order).ToList();
        if (nights.Count == 0) MessageBox.Show("Nenhuma noite encontrada na pasta content.", GameInfo.Name);
        return nights;
    }
}
