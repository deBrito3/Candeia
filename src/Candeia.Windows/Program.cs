namespace Candeia;

static class Program
{
    // Modes:
    //   (no arguments)              opens the game window
    //   --runner                    the isolated runner the game starts for the player's code (never by hand)
    //   --shot <file.png>           renders the boot screen to an image and exits (visual review)
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
        Application.Run(new MainWindow());
        return 0;
    }
}
