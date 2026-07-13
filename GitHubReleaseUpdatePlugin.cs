using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using SceneGallery.PluginSdk;

[assembly: AssemblyMetadata("PluginDescription", "Checks plugin updates from GitHub Releases")]
[assembly: AssemblyMetadata("PluginUpdateUrl", "https://github.com/LowTechMaker/GitHubReleaseUpdatePlugin")]
[assembly: InternalsVisibleTo("SceneGallery.Plugin.GitHubReleaseUpdates.Tests")]

namespace SceneGallery.Plugin.GitHubReleaseUpdates;

public sealed class GitHubReleaseUpdatePlugin : IPluginUpdateProvider, IDisposable
{
    private readonly HttpClient _http;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private IPluginHost? _host;

    public GitHubReleaseUpdatePlugin()
        : this(new HttpClientHandler())
    {
    }

    internal GitHubReleaseUpdatePlugin(HttpMessageHandler handler)
    {
        _http = new HttpClient(handler)
        {
            Timeout = TimeSpan.FromSeconds(10),
        };
        _http.DefaultRequestHeaders.Add("Accept", "application/vnd.github+json");
        _http.DefaultRequestHeaders.Add("User-Agent", "SceneGallery-GitHubReleaseUpdatePlugin/1.0");
    }

    public string Name => "GitHub Release Updates";

    public string Version => typeof(GitHubReleaseUpdatePlugin).Assembly.GetName().Version?.ToString(3) ?? "1.0.0";

    public void Initialize(IPluginHost host) => _host = host;

    public bool CanCheckUpdate(PluginUpdateRequest request)
        => TryGetLatestReleaseApiUrl(request.UpdateUrl, out _);

    public async Task<PluginUpdateResult?> CheckUpdateAsync(PluginUpdateRequest request, CancellationToken ct)
    {
        if (!TryGetLatestReleaseApiUrl(request.UpdateUrl, out var apiUrl))
            return null;

        try
        {
            var json = await _http.GetStringAsync(apiUrl, ct).ConfigureAwait(false);
            var release = JsonSerializer.Deserialize<GitHubRelease>(json, JsonOptions);
            if (release is null || release.Draft || string.IsNullOrWhiteSpace(release.TagName))
                return null;

            var version = NormalizeTag(release.TagName);
            var downloadUrl = PickDownloadUrl(release, request.PluginName, version);
            if (downloadUrl is null)
            {
                _host?.Log($"No usable asset for {request.PluginName} in GitHub release {release.TagName}.");
                return null;
            }

            return new PluginUpdateResult(
                version,
                downloadUrl,
                TrimChangelog(release.Body));
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _host?.Log($"GitHub release check failed for {request.PluginName}: {ex.Message}");
            return null;
        }
    }

    private static bool TryGetLatestReleaseApiUrl(string value, out string apiUrl)
    {
        apiUrl = "";
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri))
            return false;

        if (uri.Host.Equals("api.github.com", StringComparison.OrdinalIgnoreCase))
        {
            var segments = uri.AbsolutePath.Trim('/').Split('/', StringSplitOptions.RemoveEmptyEntries);
            if (segments.Length >= 3
                && segments[0].Equals("repos", StringComparison.OrdinalIgnoreCase))
            {
                apiUrl = $"https://api.github.com/repos/{segments[1]}/{segments[2]}/releases/latest";
                return true;
            }
        }

        if (!uri.Host.Equals("github.com", StringComparison.OrdinalIgnoreCase))
            return false;

        var parts = uri.AbsolutePath.Trim('/').Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 2)
            return false;

        apiUrl = $"https://api.github.com/repos/{parts[0]}/{parts[1]}/releases/latest";
        return true;
    }

    private static string NormalizeTag(string tag)
    {
        tag = tag.Trim();
        return tag.StartsWith('v') || tag.StartsWith('V') ? tag[1..] : tag;
    }

    private static string? PickDownloadUrl(GitHubRelease release, string pluginName, string version)
    {
        var assemblySuffix = string.Concat(pluginName.Where(char.IsLetterOrDigit));
        if (assemblySuffix.Length == 0)
            return null;

        var assemblyName = $"SceneGallery.Plugin.{assemblySuffix}";
        var unversionedName = $"{assemblyName}.dll";
        var versionedName = $"{assemblyName}-{version}.dll";

        return release.Assets.FirstOrDefault(a =>
                   a.Name.Equals(unversionedName, StringComparison.OrdinalIgnoreCase)
                   && !string.IsNullOrWhiteSpace(a.BrowserDownloadUrl))?.BrowserDownloadUrl
               ?? release.Assets.FirstOrDefault(a =>
                   a.Name.Equals(versionedName, StringComparison.OrdinalIgnoreCase)
                   && !string.IsNullOrWhiteSpace(a.BrowserDownloadUrl))?.BrowserDownloadUrl;
    }

    private static string? TrimChangelog(string? body)
    {
        if (string.IsNullOrWhiteSpace(body))
            return null;

        body = body.Trim();
        const int maxLength = 2000;
        return body.Length <= maxLength ? body : body[..maxLength] + "...";
    }

    public void Dispose() => _http.Dispose();

    private sealed class GitHubRelease
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

    private sealed class GitHubReleaseAsset
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = "";

        [JsonPropertyName("browser_download_url")]
        public string BrowserDownloadUrl { get; set; } = "";
    }
}
