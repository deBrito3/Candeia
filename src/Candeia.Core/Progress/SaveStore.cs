using System.Text;
using System.Text.Json;

namespace Candeia.Progress;

// What went wrong reading the save, for the player: the backup copy was used, or nothing could be read.
public enum SaveProblem { RecoveredFromBackup, Lost }

// The save file, %LOCALAPPDATA%/Candeia/save.json, written safely as in Projeto Aurora: the new content
// goes to a temporary file, flushed to the disk, and replaces the save; the save it replaces becomes the
// backup copy (save.json.bak), unless it was damaged, in which case it is kept aside and the good backup
// stays. Reading falls back to the backup when the save cannot be trusted.
public sealed class SaveStore
{
    readonly object gate = new();
    long issued, written;
    Task pending = Task.CompletedTask;

    public SaveStore(string folder)
    {
        Folder = folder;
        SavePath = Path.Combine(folder, "save.json");
    }

    public static string DefaultFolder => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Candeia");

    public string Folder { get; }
    public string SavePath { get; }
    public string BackupPath => SavePath + ".bak";

    // The save; or the backup when the save is missing or damaged; or a new game. Problem says what went
    // wrong, for the player, when something did.
    public (SaveData Data, SaveProblem? Problem) Load()
    {
        bool damaged = false;
        if (File.Exists(SavePath))
        {
            try { return (Read(SavePath), null); }
            catch (Exception ex) when (IsFileError(ex)) { damaged = true; }
        }
        if (File.Exists(BackupPath))
        {
            try { return (Read(BackupPath), damaged ? SaveProblem.RecoveredFromBackup : null); }
            catch (Exception ex) when (IsFileError(ex)) { damaged = true; }
        }
        return (new SaveData(), damaged ? SaveProblem.Lost : null);
    }

    // Writes now. False when the disk refused (the game goes on; the next save tries again).
    public bool Write(string json)
    {
        lock (gate)
        {
            string temporary = SavePath + ".tmp";
            try
            {
                SaveData.FromJson(json);
                Directory.CreateDirectory(Folder);
                byte[] bytes = new UTF8Encoding(false).GetBytes(json);
                using (var file = new FileStream(temporary, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    file.Write(bytes);
                    file.Flush(true);
                }
                if (File.Exists(SavePath))
                {
                    bool valid = false;
                    try { Read(SavePath); valid = true; }
                    catch (Exception ex) when (IsFileError(ex)) { }
                    // Never replace a good backup with a damaged save.
                    File.Replace(temporary, SavePath, valid ? BackupPath : SavePath + ".invalid-" + DateTime.UtcNow.ToString("yyyyMMddHHmmssfff"));
                }
                else File.Move(temporary, SavePath);
                return true;
            }
            catch (Exception ex) when (IsFileError(ex)) { return false; }
        }
    }

    // Writes off the calling thread, so the game never waits for the disk. A snapshot taken earlier never
    // overwrites a later one.
    public void WriteInBackground(string json)
    {
        long number = Interlocked.Increment(ref issued);
        var task = Task.Run(() =>
        {
            lock (gate)
            {
                if (number < written) return;
                Write(json);
                written = number;
            }
        });
        lock (gate) pending = Task.WhenAll(pending, task);
    }

    // Waits for the writes under way, as the game closes.
    public void Flush(int milliseconds = 3000)
    {
        Task all;
        lock (gate) all = pending;
        all.Wait(milliseconds);
    }

    static SaveData Read(string path)
    {
        if (new FileInfo(path).Length > 16_000_000) throw new InvalidDataException("save too large");
        return SaveData.FromJson(File.ReadAllText(path));
    }

    static bool IsFileError(Exception ex) => ex is IOException or InvalidDataException or UnauthorizedAccessException or JsonException or NotSupportedException;
}
