using System.Text;
using Candeia.Progress;

namespace Candeia;

// When the game breaks: the night's code and the progress are saved, a report goes to
// %LOCALAPPDATA%/Candeia/erros/, and the player is told, instead of the game closing in silence. The report
// has what a tester can send back (the version, the Windows, the error and where in the game it happened),
// and never the player's code.
static class CrashReport
{
    static int handling;

    public static string Folder => Path.Combine(SaveStore.DefaultFolder, "erros");

    // The campaign to save before closing, and what the open screens save first (the night's code).
    public static Campaign? Campaign { get; set; }
    public static event Action? Saving;

    // For the game itself; the tests and the benches keep the default, so a failure shows in their result.
    public static void Install()
    {
        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
        Application.ThreadException += (_, e) => Handle(e.Exception);
        AppDomain.CurrentDomain.UnhandledException += (_, e) => Handle(e.ExceptionObject as Exception ?? new InvalidOperationException("An error with no details."));
    }

    static void Handle(Exception error)
    {
        if (Interlocked.Exchange(ref handling, 1) == 1) return;
        string? path = null;
        try { Saving?.Invoke(); } catch (Exception) { }
        try { Campaign?.Flush(); } catch (Exception) { }
        try { path = Write(error, Folder); } catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
        string where = path ?? Folder;
        try
        {
            MessageBox.Show(Options.T(
                $"O CANDEIA encontrou um erro e precisa fechar. O seu progresso foi salvo.\n\nUm relatório ficou em:\n{where}\n\nSe puder, mande esse arquivo para quem te passou o jogo.",
                $"CANDEIA hit an error and has to close. Your progress was saved.\n\nA report was left at:\n{where}\n\nIf you can, send that file to whoever gave you the game."),
                GameInfo.Name, MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        catch (Exception) { }
        Environment.Exit(1);
    }

    // Writes the report and returns its path.
    public static string Write(Exception error, string folder, DateTime? at = null)
    {
        Directory.CreateDirectory(folder);
        var when = at ?? DateTime.Now;
        string path = Path.Combine(folder, $"erro-{when:yyyyMMdd-HHmmss}.txt");
        var text = new StringBuilder()
            .AppendLine($"{GameInfo.Name} {GameInfo.Version}")
            .AppendLine($"Quando / When: {when:yyyy-MM-dd HH:mm:ss}")
            .AppendLine($"Windows: {Environment.OSVersion.Version} {(Environment.Is64BitProcess ? "64-bit" : "32-bit")}")
            .AppendLine($".NET: {Environment.Version}")
            .AppendLine($"Processadores / Processors: {Environment.ProcessorCount}")
            .AppendLine($"Energia / Power: {SystemInformation.PowerStatus.PowerLineStatus}")
            .AppendLine()
            .AppendLine(error.ToString());
        File.WriteAllText(path, text.ToString(), new UTF8Encoding(false));
        return path;
    }
}
