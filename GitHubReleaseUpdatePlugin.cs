using System.Reflection;
using System.Runtime.CompilerServices;
using SceneGallery.PluginSdk;

[assembly: AssemblyMetadata("PluginDescription", "Checks plugin updates from GitHub Releases")]
[assembly: AssemblyMetadata("PluginUpdateUrl", "https://github.com/LowTechMaker/GitHubReleaseUpdatePlugin")]
[assembly: InternalsVisibleTo("SceneGallery.Plugin.GitHubReleaseUpdates.Tests")]

namespace SceneGallery.Plugin.GitHubReleaseUpdates;

public sealed class GitHubReleaseUpdatePlugin : IPluginUpdateProvider, IDisposable
{
    private readonly GitHubReleaseClient _client;

    private IPluginHost? _host;

    public GitHubReleaseUpdatePlugin()
        : this(new HttpClientHandler())
    {
    }

    internal GitHubReleaseUpdatePlugin(HttpMessageHandler handler)
    {
        _client = new GitHubReleaseClient(handler);
    }

    public string Name => "GitHub Release Updates";

    public string Version => typeof(GitHubReleaseUpdatePlugin).Assembly.GetName().Version?.ToString(3) ?? "1.0.0";

    public void Initialize(IPluginHost host) => _host = host;

    public bool CanCheckUpdate(PluginUpdateRequest request)
        => GitHubReleasePolicy.TryGetLatestReleaseApiUrl(request.UpdateUrl, out _);

    public async Task<PluginUpdateResult?> CheckUpdateAsync(PluginUpdateRequest request, CancellationToken ct)
    {
        if (!GitHubReleasePolicy.TryGetLatestReleaseApiUrl(request.UpdateUrl, out var apiUrl))
            return null;

        try
        {
            var release = await _client.FetchAsync(apiUrl, ct).ConfigureAwait(false);
            if (release is null || release.Draft || string.IsNullOrWhiteSpace(release.TagName))
                return null;

            var version = GitHubReleasePolicy.NormalizeTag(release.TagName);
            var downloadUrl = GitHubReleasePolicy.PickDownloadUrl(release, request.PluginName, version);
            if (downloadUrl is null)
            {
                _host?.Log($"No usable asset for {request.PluginName} in GitHub release {release.TagName}.");
                return null;
            }

            return new PluginUpdateResult(
                version,
                downloadUrl,
                GitHubReleasePolicy.TrimChangelog(release.Body));
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

    public void Dispose() => _client.Dispose();
}
