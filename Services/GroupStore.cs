using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace VrchatgroupApp.clean.Services;
    public class GroupStore
    {
        private const string FileName = "groups.json";

        public async Task<List<string>> LoadAsync()
        {
            if (!File.Exists(FileName))
                return new();

            var json = await File.ReadAllTextAsync(FileName);
            return JsonSerializer.Deserialize<List<string>>(json) ?? new();
        }

        public async Task SaveAsync(List<string> groupIds)
        {
            var json = JsonSerializer.Serialize(groupIds, new JsonSerializerOptions
            {
                WriteIndented = true
            });

            await File.WriteAllTextAsync(FileName, json);
        }
    }