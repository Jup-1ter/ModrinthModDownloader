using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ModDownloader
{
    static class Program
    {
        static async Task Main(string[] args)
        {
            if (args.Length < 3)
            {
                Console.WriteLine("Использование: <загрузчик> <версия> <ID мода>[ <ID мода 2> ...]");
                return;
            }

            Directory.CreateDirectory(args[0]);
            using HttpClient client = new();
            HashSet<Task> tasks = [];

            foreach (var modId in args.Skip(2))
                tasks.Add(DownloadMod(client, args[0], args[1], modId));
            await Task.WhenAll(tasks);
        }

        static async Task DownloadMod(HttpClient client, string loader, string version, string modId)
        {
            try
            {
                Console.WriteLine($"Скачивание последней версии мода {modId} для загрузчика {loader} {version}...");

                string response;
                try
                {
                    response = await client.GetStringAsync($"https://api.modrinth.com/v2/project/{modId}/version");
                }
                catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
                {
                    Console.WriteLine($"Мод {modId} не найден.");
                    return;
                }

                var versions = JsonSerializer.Deserialize<ModrinthVersion[]>(response);
                if (versions == null || versions.Length == 0)
                {
                    Console.WriteLine($"Не удалось найти ни одной версии для мода {modId}.");
                    return;
                }

                var targetVersion = versions
                    .Where(v => v.Loaders.Contains(loader) && v.GameVersions.Contains(version))
                    //.OrderByDescending(v => v.DatePublished)
                    .OrderByDescending(v => v.VersionNumber)
                    .FirstOrDefault();
                if (targetVersion == null)
                {
                    Console.WriteLine($"Не удалось найти версию мода {modId} для {loader} {version}.");
                    return;
                }

                var downloadUrl = targetVersion.Files.FirstOrDefault()?.Url;
                if (string.IsNullOrEmpty(downloadUrl))
                {
                    Console.WriteLine($"Не удалось найти ссылку для скачивания мода {modId}.");
                    return;
                }

                string fileName = targetVersion.Files.First().Filename;
                Console.WriteLine($"Скачивание {fileName} с {downloadUrl}...");
                var fileData = await client.GetByteArrayAsync(downloadUrl);
                await File.WriteAllBytesAsync($"{loader}\\{fileName}", fileData);

                Console.WriteLine($"Мод {modId} успешно скачан: {fileName}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Произошла ошибка при скачивании мода {modId}: {ex.Message}");
            }
        }
    }

    public class ModrinthVersion
    {
        [JsonPropertyName("id")]
        public required string Id { get; set; }
        [JsonPropertyName("game_versions")]
        public required string[] GameVersions { get; set; }
        [JsonPropertyName("version_number")]
        public required string VersionNumber { get; set; }
        [JsonPropertyName("loaders")]
        public required string[] Loaders { get; set; }
        [JsonPropertyName("files")]
        public required ModrinthFile[] Files { get; set; }
    }

    public class ModrinthFile
    {
        [JsonPropertyName("url")]
        public required string Url { get; set; }
        [JsonPropertyName("filename")]
        public required string Filename { get; set; }
    }
}