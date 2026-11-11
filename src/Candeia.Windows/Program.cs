namespace Candeia;

static class Program
{
    // Modes:
    //   (no arguments)        opens the game window
    //   --shot <file.png>     renders the boot screen to an image and exits (visual review)
    [STAThread]
    static int Main(string[] args)
    {
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
