namespace Candeia;

static class Program
{
    // Modes:
    //   (no arguments)        opens the game window
    //   --runner              the isolated runner the game starts for the player's code (never by hand)
    //   --shot <file.png>     renders the boot screen to an image and exits (visual review)
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
        Application.Run(new MainWindow());
        return 0;
    }
}
