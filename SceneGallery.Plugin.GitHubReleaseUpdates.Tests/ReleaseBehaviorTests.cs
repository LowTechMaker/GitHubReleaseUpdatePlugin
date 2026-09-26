using System.Net;
using System.Text;
using System.Text.Json;
using SceneGallery.PluginSdk;

namespace SceneGallery.Plugin.GitHubReleaseUpdates.Tests;

public sealed class ReleaseBehaviorTests
{
    private static PluginUpdateRequest Request(string url = "https://github.com/example/plugin")
        => new("Test Plugin", "9.0.0", url);

    [Theory]
    [InlineData("https://github.com/example/plugin/releases/tag/v1", "https://api.github.com/repos/example/plugin/releases/latest")]
    [InlineData("http://api.github.com/repos/example/plugin/releases", "https://api.github.com/repos/example/plugin/releases/latest")]
    public async Task NormalizesRequestAndPreservesHeaders(string url, string expected)
    {
        using var plugin = new GitHubReleaseUpdatePlugin(new Handler((request, _) =>
        {
            Assert.Equal(expected, request.RequestUri!.AbsoluteUri);
            Assert.Contains("application/vnd.github+json", request.Headers.Accept.ToString());
            Assert.Equal("SceneGallery-GitHubReleaseUpdatePlugin/1.0", request.Headers.UserAgent.ToString());
            return Task.FromResult(Response("null"));
        }));
        Assert.True(plugin.CanCheckUpdate(Request(url)));
        Assert.Null(await plugin.CheckUpdateAsync(Request(url), CancellationToken.None));
    }

    [Theory]
    [InlineData("invalid")]
    [InlineData("https://example.test/owner/repo")]
    [InlineData("https://github.com/owner")]
    [InlineData("https://api.github.com/notrepos/owner/repo")]
    public async Task UnsupportedUrlNeverRequestsNetwork(string url)
    {
        using var plugin = new GitHubReleaseUpdatePlugin(new Handler((_, _) => throw new Exception("unexpected HTTP")));
        Assert.False(plugin.CanCheckUpdate(Request(url)));
        Assert.Null(await plugin.CheckUpdateAsync(Request(url), CancellationToken.None));
    }

    [Theory]
    [InlineData("v1.2.3", "1.2.3")]
    [InlineData(" V1.2.3 ", "1.2.3")]
    [InlineData("vv1.2.3", "v1.2.3")]
    public async Task UnversionedAssetWinsAndCurrentVersionIsNotCompared(string tag, string expected)
    {
        var json = JsonSerializer.Serialize(new
        {
            tag_name = tag,
            assets = new[]
            {
                new { name = $"SceneGallery.Plugin.TestPlugin-{expected}.dll", browser_download_url = "https://example.test/versioned" },
                new { name = "scenegallery.plugin.testplugin.DLL", browser_download_url = "https://example.test/unversioned" }
            }
        });
        using var plugin = WithJson(json);
        var result = await plugin.CheckUpdateAsync(Request(), CancellationToken.None);
        Assert.NotNull(result);
        Assert.Equal(expected, result.Version);
        Assert.Equal("https://example.test/unversioned", result.DownloadUrl);
    }

    [Theory]
    [InlineData("null")]
    [InlineData("{")]
    [InlineData("{}")]
    [InlineData("{\"tag_name\":\" \"}")]
    [InlineData("{\"tag_name\":\"1\",\"draft\":true}")]
    [InlineData("{\"tag_name\":\"1\",\"assets\":null}")]
    [InlineData("{\"tag_name\":\"1\",\"assets\":[null]}")]
    [InlineData("{\"tag_name\":\"1\",\"assets\":[{\"name\":null}]}")]
    public async Task UnusableReleaseReturnsNoUpdate(string json)
    {
        using var plugin = WithJson(json);
        Assert.Null(await plugin.CheckUpdateAsync(Request(), CancellationToken.None));
    }

    [Theory]
    [InlineData(404)]
    [InlineData(429)]
    [InlineData(500)]
    public async Task HttpFailureReturnsNullAndLogs(int status)
    {
        var host = new Host();
        using var plugin = new GitHubReleaseUpdatePlugin(new Handler((_, _) =>
            Task.FromResult(new HttpResponseMessage((HttpStatusCode)status))));
        plugin.Initialize(host);
        Assert.Null(await plugin.CheckUpdateAsync(Request(), CancellationToken.None));
        Assert.Single(host.Messages);
        Assert.Contains("GitHub release check failed", host.Messages[0]);
    }

    [Fact]
    public async Task CancellationPropagates()
    {
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var cts = new CancellationTokenSource();
        using var plugin = new GitHubReleaseUpdatePlugin(new Handler(async (_, ct) =>
        {
            started.SetResult();
            await Task.Delay(Timeout.InfiniteTimeSpan, ct);
            return Response("null");
        }));
        var operation = plugin.CheckUpdateAsync(Request(), cts.Token);
        await started.Task;
        cts.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => operation);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(2000)]
    [InlineData(2001)]
    public async Task ChangelogRetainsExistingLimit(int length)
    {
        using var plugin = WithJson(JsonSerializer.Serialize(new
        {
            tag_name = "1",
            body = " " + new string('a', length) + " ",
            assets = new[] { new { name = "SceneGallery.Plugin.TestPlugin.dll", browser_download_url = "https://example.test/a" } }
        }));
        var result = await plugin.CheckUpdateAsync(Request(), CancellationToken.None);
        Assert.NotNull(result);
        Assert.Equal(length == 0 ? null : new string('a', Math.Min(length, 2000)) + (length > 2000 ? "..." : ""), result.Changelog);
    }

    private static GitHubReleaseUpdatePlugin WithJson(string json)
        => new(new Handler((_, _) => Task.FromResult(Response(json))));
    private static HttpResponseMessage Response(string json)
        => new(HttpStatusCode.OK) { Content = new StringContent(json, Encoding.UTF8, "application/json") };
    private sealed class Handler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> respond) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => respond(request, cancellationToken);
    }
    private sealed class Host : IPluginHost
    {
        public string StorageDirectory => "";
        internal List<string> Messages { get; } = [];
        public void Log(string message) => Messages.Add(message);
    }
}
