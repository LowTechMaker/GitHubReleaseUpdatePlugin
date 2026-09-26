namespace SceneGallery.Plugin.GitHubReleaseUpdates;

/// <summary>Pure compatibility policy. This plugin neither compares versions nor installs assets.</summary>
internal static class GitHubReleasePolicy
{
    internal static bool TryGetLatestReleaseApiUrl(string value, out string apiUrl)
    {
        apiUrl = "";
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri)) return false;
        if (uri.Host.Equals("api.github.com", StringComparison.OrdinalIgnoreCase))
        {
            var segments = uri.AbsolutePath.Trim('/').Split('/', StringSplitOptions.RemoveEmptyEntries);
            if (segments.Length >= 3 && segments[0].Equals("repos", StringComparison.OrdinalIgnoreCase))
            {
                apiUrl = $"https://api.github.com/repos/{segments[1]}/{segments[2]}/releases/latest";
                return true;
            }
        }
        if (!uri.Host.Equals("github.com", StringComparison.OrdinalIgnoreCase)) return false;
        var parts = uri.AbsolutePath.Trim('/').Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 2) return false;
        apiUrl = $"https://api.github.com/repos/{parts[0]}/{parts[1]}/releases/latest";
        return true;
    }

    internal static string NormalizeTag(string tag)
    {
        tag = tag.Trim();
        return tag.StartsWith('v') || tag.StartsWith('V') ? tag[1..] : tag;
    }

    internal static string? PickDownloadUrl(GitHubRelease release, string pluginName, string version)
    {
        var suffix = string.Concat(pluginName.Where(char.IsLetterOrDigit));
        if (suffix.Length == 0) return null;
        var assemblyName = $"SceneGallery.Plugin.{suffix}";
        var unversionedName = $"{assemblyName}.dll";
        var versionedName = $"{assemblyName}-{version}.dll";
        return release.Assets.FirstOrDefault(a =>
                   a.Name.Equals(unversionedName, StringComparison.OrdinalIgnoreCase)
                   && !string.IsNullOrWhiteSpace(a.BrowserDownloadUrl))?.BrowserDownloadUrl
               ?? release.Assets.FirstOrDefault(a =>
                   a.Name.Equals(versionedName, StringComparison.OrdinalIgnoreCase)
                   && !string.IsNullOrWhiteSpace(a.BrowserDownloadUrl))?.BrowserDownloadUrl;
    }

    internal static string? TrimChangelog(string? body)
    {
        if (string.IsNullOrWhiteSpace(body)) return null;
        body = body.Trim();
        const int maxLength = 2000;
        return body.Length <= maxLength ? body : body[..maxLength] + "...";
    }
}
