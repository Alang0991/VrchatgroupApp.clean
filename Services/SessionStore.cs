using System.IO;
using System.Text.Json;
using System.Threading.Tasks;

namespace VrchatgroupApp.clean.Services;

public class SessionStore
{
    private static readonly string FilePath =
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "VrchatgroupApp",
            "session.json"
        );

    public async Task<StoredSession?> LoadAsync()
    {
        if (!File.Exists(FilePath))
            return null;

        var json = await File.ReadAllTextAsync(FilePath);
        return JsonSerializer.Deserialize<StoredSession>(json);
    }

    public async Task SaveAsync(StoredSession session)
    {
        var dir = Path.GetDirectoryName(FilePath)!;
        Directory.CreateDirectory(dir);

        var json = JsonSerializer.Serialize(session, new JsonSerializerOptions
        {
            WriteIndented = true
        });

        await File.WriteAllTextAsync(FilePath, json);
    }

    public Task ClearAsync()
    {
        if (File.Exists(FilePath))
            File.Delete(FilePath);

        return Task.CompletedTask;
    }
}
