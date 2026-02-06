using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace VrchatgroupApp.clean.Services;

public class DpapiSessionStore : ISessionStore
{
    private readonly string _filePath;

    public DpapiSessionStore()
    {
        var dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "VrchatgroupApp"
        );

        Directory.CreateDirectory(dir);
        _filePath = Path.Combine(dir, "session.dat");
    }

    public async Task SaveAsync(StoredSession session)
    {
        var json = JsonSerializer.Serialize(session);
        var plainBytes = Encoding.UTF8.GetBytes(json);

        var protectedBytes = ProtectedData.Protect(
            plainBytes,
            optionalEntropy: null,
            scope: DataProtectionScope.CurrentUser
        );

        await File.WriteAllBytesAsync(_filePath, protectedBytes);
    }

    public async Task<StoredSession?> LoadAsync()
    {
        if (!File.Exists(_filePath))
            return null;

        try
        {
            var protectedBytes = await File.ReadAllBytesAsync(_filePath);

            var plainBytes = ProtectedData.Unprotect(
                protectedBytes,
                optionalEntropy: null,
                scope: DataProtectionScope.CurrentUser
            );

            var json = Encoding.UTF8.GetString(plainBytes);
            return JsonSerializer.Deserialize<StoredSession>(json);
        }
        catch
        {
            // If file corrupt or user context changed, clear it.
            await ClearAsync();
            return null;
        }
    }

    public Task ClearAsync()
    {
        if (File.Exists(_filePath))
            File.Delete(_filePath);

        return Task.CompletedTask;
    }
}
