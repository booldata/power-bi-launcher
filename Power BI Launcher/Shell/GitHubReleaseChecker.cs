using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace PowerBILauncher;

public readonly record struct LauncherUpdate(string VersionText, Uri ReleaseUrl);

public enum LauncherUpdateCheckStatus
{
    Ineligible,
    UpdateAvailable,
    UpToDate,
    Failed
}

public readonly record struct LauncherUpdateCheckResult(
    LauncherUpdateCheckStatus Status,
    LauncherUpdate? Update = null);

/// <summary>
/// UI-free GitHub Releases latest check. Fail-silent: network, HTTP, JSON, and policy
/// mismatches return no update. Does not download or apply binaries.
/// </summary>
public sealed class GitHubReleaseChecker : IDisposable
{
    public const string LatestReleaseApiUrl =
        "https://api.github.com/repos/booldata/power-bi-launcher/releases/latest";

    public const string UserAgentProduct = "booldata-PowerBILauncher";

    private const string ReleaseHost = "github.com";
    private const string ReleasePathPrefix = "/booldata/power-bi-launcher/releases/tag/";

    private static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(5);

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = false
    };

    private readonly HttpClient _http;
    private bool _disposed;

    public GitHubReleaseChecker()
        : this(new HttpClientHandler(), DefaultTimeout, disposeHandler: true)
    {
    }

    public GitHubReleaseChecker(HttpMessageHandler handler, TimeSpan? timeout = null)
        : this(handler, timeout ?? DefaultTimeout, disposeHandler: false)
    {
    }

    private GitHubReleaseChecker(HttpMessageHandler handler, TimeSpan timeout, bool disposeHandler)
    {
        ArgumentNullException.ThrowIfNull(handler);

        _http = new HttpClient(handler, disposeHandler)
        {
            Timeout = timeout
        };
        _http.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue(UserAgentProduct, null));
        _http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        _http.DefaultRequestHeaders.TryAddWithoutValidation("X-GitHub-Api-Version", "2022-11-28");
    }

    public async Task<LauncherUpdate?> CheckAsync(
        string? installedInformationalVersion,
        CancellationToken cancellationToken = default)
    {
        LauncherUpdateCheckResult result = await CheckDetailedAsync(
            installedInformationalVersion,
            cancellationToken).ConfigureAwait(false);
        return result.Status == LauncherUpdateCheckStatus.UpdateAvailable
            ? result.Update
            : null;
    }

    public async Task<LauncherUpdateCheckResult> CheckDetailedAsync(
        string? installedInformationalVersion,
        CancellationToken cancellationToken = default)
    {
        if (!LauncherVersionPolicy.TryParseInstalled(installedInformationalVersion, out LauncherVersion installed))
        {
            return new LauncherUpdateCheckResult(LauncherUpdateCheckStatus.Ineligible);
        }

        if (cancellationToken.IsCancellationRequested)
        {
            return new LauncherUpdateCheckResult(LauncherUpdateCheckStatus.Failed);
        }

        try
        {
            using HttpResponseMessage response = await _http
                .GetAsync(LatestReleaseApiUrl, cancellationToken)
                .ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                return new LauncherUpdateCheckResult(LauncherUpdateCheckStatus.Failed);
            }

            await using Stream stream = await response.Content
                .ReadAsStreamAsync(cancellationToken)
                .ConfigureAwait(false);
            GitHubLatestReleaseDto? release = await JsonSerializer
                .DeserializeAsync<GitHubLatestReleaseDto>(stream, JsonOptions, cancellationToken)
                .ConfigureAwait(false);
            return ClassifyRelease(release, installed);
        }
        catch (Exception ex) when (IsSilentFailure(ex))
        {
            return new LauncherUpdateCheckResult(LauncherUpdateCheckStatus.Failed);
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _http.Dispose();
    }

    private static LauncherUpdateCheckResult ClassifyRelease(
        GitHubLatestReleaseDto? release,
        LauncherVersion installed)
    {
        if (release is null || release.Draft || release.Prerelease)
        {
            return new LauncherUpdateCheckResult(LauncherUpdateCheckStatus.Failed);
        }

        if (!LauncherVersionPolicy.TryParseReleaseTag(release.TagName, out LauncherVersion remote))
        {
            return new LauncherUpdateCheckResult(LauncherUpdateCheckStatus.Failed);
        }

        if (!remote.IsNewerThan(installed))
        {
            return new LauncherUpdateCheckResult(LauncherUpdateCheckStatus.UpToDate);
        }

        if (!TryValidateReleaseUrl(release.HtmlUrl, release.TagName, out Uri url))
        {
            return new LauncherUpdateCheckResult(LauncherUpdateCheckStatus.Failed);
        }

        return new LauncherUpdateCheckResult(
            LauncherUpdateCheckStatus.UpdateAvailable,
            new LauncherUpdate(remote.ToString(), url));
    }

    private static bool TryValidateReleaseUrl(string? htmlUrl, string? tagName, out Uri url)
    {
        url = null!;
        if (string.IsNullOrWhiteSpace(htmlUrl) || string.IsNullOrWhiteSpace(tagName))
        {
            return false;
        }

        if (!Uri.TryCreate(htmlUrl.Trim(), UriKind.Absolute, out Uri? parsed))
        {
            return false;
        }

        if (!string.Equals(parsed.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(parsed.IdnHost, ReleaseHost, StringComparison.OrdinalIgnoreCase) ||
            !parsed.IsDefaultPort ||
            !string.IsNullOrEmpty(parsed.UserInfo) ||
            !string.IsNullOrEmpty(parsed.Query) ||
            !string.IsNullOrEmpty(parsed.Fragment))
        {
            return false;
        }

        string expectedPath = ReleasePathPrefix + tagName.Trim();
        if (!string.Equals(parsed.AbsolutePath, expectedPath, StringComparison.Ordinal))
        {
            return false;
        }

        url = parsed;
        return true;
    }

    private static bool IsSilentFailure(Exception ex) =>
        ex is HttpRequestException
            or TaskCanceledException
            or OperationCanceledException
            or JsonException
            or IOException
            or ObjectDisposedException
            or InvalidOperationException;

    private sealed class GitHubLatestReleaseDto
    {
        [JsonPropertyName("tag_name")]
        public string? TagName { get; set; }

        [JsonPropertyName("html_url")]
        public string? HtmlUrl { get; set; }

        [JsonPropertyName("draft")]
        public bool Draft { get; set; }

        [JsonPropertyName("prerelease")]
        public bool Prerelease { get; set; }
    }
}
