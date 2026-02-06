using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace VrchatgroupApp.clean.Services
{
    public class SecureSessionStore
    {
        private static readonly string FilePath =
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "VrchatgroupApp",
                "session.dat"
            );

        private record SessionData(string Auth, string? TwoFactor);

        public async Task SaveAsync(string auth, string? twoFactor)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);

            var json = JsonSerializer.Serialize(new SessionData(auth, twoFactor));
            var bytes = Encoding.UTF8.GetBytes(json);

            var encrypted = ProtectedData.Protect(
                bytes,
                null,
                DataProtectionScope.CurrentUser
            );

            await File.WriteAllBytesAsync(FilePath, encrypted);
        }

        public async Task<(string auth, string? twoFactor)?> LoadAsync()
        {
            if (!File.Exists(FilePath))
                return null;

            try
            {
                var encrypted = await File.ReadAllBytesAsync(FilePath);

                var decrypted = ProtectedData.Unprotect(
                    encrypted,
                    null,
                    DataProtectionScope.CurrentUser
                );

                var json = Encoding.UTF8.GetString(decrypted);
                var data = JsonSerializer.Deserialize<SessionData>(json);

                return data is null ? null : (data.Auth, data.TwoFactor);
            }
            catch
            {
                return null;
            }
        }

        public Task ClearAsync()
        {
            if (File.Exists(FilePath))
                File.Delete(FilePath);

            return Task.CompletedTask;
        }
    }
}
