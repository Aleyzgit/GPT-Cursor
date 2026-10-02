using System.Net;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace GPTCursor;

internal sealed record AppUpdate(Version Version, Uri Download, string Sha256);

internal sealed class UpdateService : IDisposable
{
    internal const string Repository = "https://github.com/Aleyzgit/GPT-Cursor";
    internal static Version CurrentVersion { get; } = new(Assembly.GetExecutingAssembly().GetName().Version!.ToString(3));
    private readonly HttpClient client;
    internal UpdateService(HttpMessageHandler? handler = null)
    {
        client = handler == null ? new HttpClient() : new HttpClient(handler);
        client.Timeout = TimeSpan.FromMinutes(5);
        client.DefaultRequestHeaders.UserAgent.ParseAdd($"GPT-Cursor/{CurrentVersion}");
        client.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
    }
    internal async Task<AppUpdate?> CheckAsync(CancellationToken cancellation)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
        timeout.CancelAfter(TimeSpan.FromSeconds(20));
        using var response = await client.GetAsync("https://api.github.com/repos/Aleyzgit/GPT-Cursor/releases/latest", timeout.Token);
        if (response.StatusCode == HttpStatusCode.NotFound) return null;
        response.EnsureSuccessStatusCode();
        return Parse(await response.Content.ReadAsStringAsync(timeout.Token), CurrentVersion);
    }
    internal static AppUpdate? Parse(string json, Version current)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        if (root.GetProperty("draft").GetBoolean() || root.GetProperty("prerelease").GetBoolean()) return null;
        string tag = root.GetProperty("tag_name").GetString() ?? "";
        if (!Regex.IsMatch(tag, @"^v\d+\.\d+\.\d+$") || !Version.TryParse(tag[1..], out var version) || version <= current) return null;
        string name = $"GPT-Cursor-Setup-{version}.exe";
        foreach (var asset in root.GetProperty("assets").EnumerateArray())
        {
            if (asset.GetProperty("name").GetString() != name) continue;
            string url = asset.GetProperty("browser_download_url").GetString() ?? "";
            string expected = $"{Repository}/releases/download/{tag}/{name}";
            if (!string.Equals(url, expected, StringComparison.Ordinal)) throw new InvalidDataException("Unexpected update download URL.");
            string digest = asset.TryGetProperty("digest", out var property) ? property.GetString() ?? "" : "";
            if (!Regex.IsMatch(digest, @"^sha256:[a-fA-F0-9]{64}$")) throw new InvalidDataException("Release installer has no valid SHA-256 digest.");
            return new(version, new Uri(url), digest[7..]);
        }
        throw new InvalidDataException("The latest release does not contain its setup installer.");
    }
    internal async Task<string> DownloadAsync(AppUpdate update, CancellationToken cancellation)
    {
        string directory = Path.Combine(Path.GetTempPath(), "GPTCursor-Update-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        string path = Path.Combine(directory, $"GPT-Cursor-Setup-{update.Version}.exe");
        try
        {
            using var response = await client.GetAsync(update.Download, HttpCompletionOption.ResponseHeadersRead, cancellation);
            response.EnsureSuccessStatusCode();
            var final = response.RequestMessage?.RequestUri;
            if (final == null || final.Scheme != "https" || !(final.Host == "github.com" || final.Host.EndsWith(".githubusercontent.com", StringComparison.OrdinalIgnoreCase)))
                throw new InvalidDataException("Unexpected update download destination.");
            await using var input = await response.Content.ReadAsStreamAsync(cancellation);
            await using var output = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, true);
            using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            byte[] buffer = new byte[81920]; long total = 0; int count;
            while ((count = await input.ReadAsync(buffer, cancellation)) != 0)
            {
                total += count;
                if (total > 200L * 1024 * 1024) throw new InvalidDataException("Update exceeds size limit.");
                hash.AppendData(buffer, 0, count);
                await output.WriteAsync(buffer.AsMemory(0, count), cancellation);
            }
            if (total == 0 || !CryptographicOperations.FixedTimeEquals(hash.GetHashAndReset(), Convert.FromHexString(update.Sha256)))
                throw new InvalidDataException("Update checksum does not match. Installation cancelled.");
            return path;
        }
        catch { File.Delete(path); Directory.Delete(directory); throw; }
    }
    public void Dispose() => client.Dispose();
}
