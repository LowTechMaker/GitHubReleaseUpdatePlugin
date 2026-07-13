using System.Net;
using System.Reflection;
using System.Text;
using System.Text.Json;
using SceneGallery.PluginSdk;

namespace SceneGallery.Plugin.GitHubReleaseUpdates.Tests;

public sealed class GitHubReleaseUpdatePluginTests
{
    [Fact]
    public void CanCheckUpdate_AcceptsGitHubRepositoryUrl()
    {
        using var plugin = new GitHubReleaseUpdatePlugin();
        var request = new PluginUpdateRequest(
            "Pixiv Authors",
            "1.0.0",
            "https://github.com/LowTechMaker/pixiv-data-plugin");

        Assert.True(plugin.CanCheckUpdate(request));
    }

    [Fact]
    public void AssemblyMetadata_ContainsSelfUpdateUrl()
    {
        var updateUrl = typeof(GitHubReleaseUpdatePlugin).Assembly
            .GetCustomAttributes<AssemblyMetadataAttribute>()
            .Single(attribute => attribute.Key == "PluginUpdateUrl")
            .Value;

        Assert.Equal("https://github.com/LowTechMaker/GitHubReleaseUpdatePlugin", updateUrl);
    }

    [Fact]
    public async Task CheckUpdateAsync_NormalizesTagAndSelectsExactAssemblyAsset()
    {
        const string expectedUrl = "https://example.test/SceneGallery.Plugin.BepisDb.dll";
        using var plugin = CreatePlugin(
            "v1.2.3",
            ("SceneGallery.Plugin.BepisDb.dll", expectedUrl));

        var result = await plugin.CheckUpdateAsync(
            Request("BepisDB"),
            CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal("1.2.3", result.Version);
        Assert.Equal(expectedUrl, result.DownloadUrl);
    }

    [Fact]
    public async Task CheckUpdateAsync_MultipleDlls_SelectsVersionedPluginAssembly()
    {
        const string expectedUrl = "https://example.test/SceneGallery.Plugin.PixivAuthors-2.3.4.dll";
        using var plugin = CreatePlugin(
            "2.3.4",
            ("Dependency.dll", "https://example.test/Dependency.dll"),
            ("SceneGallery.Plugin.Helper.dll", "https://example.test/SceneGallery.Plugin.Helper.dll"),
            ("SceneGallery.Plugin.PixivAuthors-2.3.4.dll", expectedUrl));

        var result = await plugin.CheckUpdateAsync(
            Request("Pixiv Authors"),
            CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(expectedUrl, result.DownloadUrl);
    }

    [Fact]
    public async Task CheckUpdateAsync_NoMatchingAsset_LogsAndSkipsUpdate()
    {
        var host = new RecordingHost();
        using var plugin = CreatePlugin(
            "3.0.0",
            ("Dependency.dll", "https://example.test/Dependency.dll"),
            ("release.zip", "https://example.test/SceneGallery.Plugin.Fanbox.dll"));
        plugin.Initialize(host);

        var result = await plugin.CheckUpdateAsync(
            Request("Fanbox"),
            CancellationToken.None);

        Assert.Null(result);
        Assert.Contains(host.Messages, message => message.Contains("No usable asset", StringComparison.Ordinal));
    }

    private static GitHubReleaseUpdatePlugin CreatePlugin(
        string tag,
        params (string Name, string Url)[] assets)
    {
        var json = JsonSerializer.Serialize(new
        {
            tag_name = tag,
            html_url = "https://example.test/releases/latest",
            draft = false,
            assets = assets.Select(asset => new
            {
                name = asset.Name,
                browser_download_url = asset.Url,
            }),
        });
        return new GitHubReleaseUpdatePlugin(new StubHttpMessageHandler(json));
    }

    private static PluginUpdateRequest Request(string pluginName)
        => new(pluginName, "1.0.0", "https://github.com/example/plugin");

    private sealed class StubHttpMessageHandler(string json) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
            => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json"),
            });
    }

    private sealed class RecordingHost : IPluginHost
    {
        public string StorageDirectory => "";

        public List<string> Messages { get; } = [];

        public void Log(string message) => Messages.Add(message);
    }
}
