using System.IO;

namespace SpotifyTaskbarWidget;

/// <summary>
/// Diagnóstico mínimo para falhas silenciosas: escreve UMA vez por causa no
/// errors.log (o mesmo do handler global), para os utilizadores afetados
/// poderem colar o conteúdo num report. Em inglês — é o que viaja no Reddit.
/// </summary>
internal static class Diag
{
    private static readonly HashSet<string> Seen = new();

    public static void Once(string key, string message)
    {
        lock (Seen)
        {
            if (!Seen.Add(key)) return;
        }
        try
        {
            var dir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "SpotifyTaskbarWidget");
            Directory.CreateDirectory(dir);
            File.AppendAllText(Path.Combine(dir, "errors.log"),
                $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}\n");
        }
        catch { }
    }
}
