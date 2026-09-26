using System.Text.Json;
using System.Text.Json.Serialization;

namespace SceneGallery.Plugin.GitHubReleaseUpdates;

/// <summary>Owns HTTP transport and wire decoding; release selection belongs to policy.</summary>
internal sealed class GitHubReleaseClient : IDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };
    private readonly HttpClient _http;

    internal GitHubReleaseClient(HttpMessageHandler handler)
    {
        _http = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(10) };
        _http.DefaultRequestHeaders.Add("Accept", "application/vnd.github+json");
        _http.DefaultRequestHeaders.Add("User-Agent", "SceneGallery-GitHubReleaseUpdatePlugin/1.0");
    }

    internal async Task<GitHubRelease?> FetchAsync(string apiUrl, CancellationToken ct)
    {
        var json = await _http.GetStringAsync(apiUrl, ct).ConfigureAwait(false);
        return JsonSerializer.Deserialize<GitHubRelease>(json, JsonOptions);
    }

    public void Dispose() => _http.Dispose();
}

internal sealed class GitHubRelease
{
    [JsonPropertyName("tag_name")]
    public string? TagName { get; set; }
    [JsonPropertyName("html_url")]
    public string? HtmlUrl { get; set; }
    [JsonPropertyName("body")]
    public string? Body { get; set; }
    [JsonPropertyName("draft")]
    public bool Draft { get; set; }
    [JsonPropertyName("assets")]
    public List<GitHubReleaseAsset> Assets { get; set; } = [];
}

internal sealed class GitHubReleaseAsset
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = "";
    [JsonPropertyName("browser_download_url")]
    public string BrowserDownloadUrl { get; set; } = "";
}
