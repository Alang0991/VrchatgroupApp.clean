using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace VrchatgroupApp.clean.Services;

public static class SecureStorage
{
    private static readonly string BasePath =
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "VrchatgroupApp"
        );

    public static void Save(string key, string data)
    {
        Directory.CreateDirectory(BasePath);

        var bytes = Encoding.UTF8.GetBytes(data);
        var encrypted = ProtectedData.Protect(bytes, null, DataProtectionScope.CurrentUser);

        File.WriteAllBytes(Path.Combine(BasePath, key), encrypted);
    }

    public static string? Load(string key)
    {
        var file = Path.Combine(BasePath, key);
        if (!File.Exists(file)) return null;

        var encrypted = File.ReadAllBytes(file);
        var decrypted = ProtectedData.Unprotect(encrypted, null, DataProtectionScope.CurrentUser);

        return Encoding.UTF8.GetString(decrypted);
    }

    public static void Delete(string key)
    {
        var file = Path.Combine(BasePath, key);
        if (File.Exists(file)) File.Delete(file);
    }
}
