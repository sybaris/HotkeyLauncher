using System.Net.Http.Headers;
using System.Text.Json;

namespace HotkeyLauncher.Core;

public sealed record UpdateInfo(Version Version, string Tag, string Url);

/// <summary>Asks GitHub for the latest published release. Only the public release information is read.</summary>
public static class UpdateChecker
{
    public const string LatestReleaseApi = "https://api.github.com/repos/sybaris/HotkeyLauncher/releases/latest";
    public const string ReleasesPage = "https://github.com/sybaris/HotkeyLauncher/releases/latest";

    /// <summary>Returns the newer release, or null when up to date (or when no release exists yet).</summary>
    /// <exception cref="HttpRequestException">Network or GitHub error.</exception>
    public static async Task<UpdateInfo?> CheckAsync(Version currentVersion, CancellationToken cancellationToken = default)
    {
        using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
        client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("HotkeyLauncher", currentVersion.ToString(3)));
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));

        using var response = await client.GetAsync(LatestReleaseApi, cancellationToken).ConfigureAwait(false);
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound) return null; // no release published yet
        response.EnsureSuccessStatusCode();

        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false));
        var tag = json.RootElement.GetProperty("tag_name").GetString() ?? string.Empty;
        var url = json.RootElement.TryGetProperty("html_url", out var u) ? u.GetString() ?? ReleasesPage : ReleasesPage;
        return IsNewer(tag, currentVersion, out var version) ? new UpdateInfo(version, tag, url) : null;
    }

    /// <summary>"v1.2.3" (or "1.2.3", "v1.2.3-beta.1") newer than <paramref name="current"/>?</summary>
    public static bool IsNewer(string tag, Version current, out Version version)
    {
        version = new Version(0, 0, 0);
        var text = tag.Trim().TrimStart('v', 'V');
        var dash = text.IndexOfAny(['-', '+']);
        if (dash >= 0) text = text[..dash];
        if (!Version.TryParse(text, out var parsed)) return false;

        version = Normalize(parsed);
        return version > Normalize(current);
    }

    private static Version Normalize(Version v) => new(v.Major, Math.Max(v.Minor, 0), Math.Max(v.Build, 0));
}
