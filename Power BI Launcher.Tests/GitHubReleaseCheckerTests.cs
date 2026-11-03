using System.Net;
using System.Net.Http;
using System.Text;

namespace PowerBILauncher.Tests;

public class GitHubReleaseCheckerTests
{
    private const string Installed = "1.0.0";
    private const string NewerTag = "v1.2.3";
    private const string NewerHtmlUrl = "https://github.com/booldata/power-bi-launcher/releases/tag/v1.2.3";

    [Fact]
    public async Task CheckAsync_maps_a_strictly_newer_stable_release()
    {
        var handler = new StubHandler(ReleaseJson(NewerTag, NewerHtmlUrl));
        using var checker = new GitHubReleaseChecker(handler);

        LauncherUpdate? update = await checker.CheckAsync(Installed);

        Assert.NotNull(update);
        Assert.Equal("1.2.3", update.Value.VersionText);
        Assert.Equal(new Uri(NewerHtmlUrl), update.Value.ReleaseUrl);
        Assert.Equal(1, handler.RequestCount);
        Assert.Equal(HttpMethod.Get, handler.LastRequest!.Method);
        Assert.Equal(new Uri(GitHubReleaseChecker.LatestReleaseApiUrl), handler.LastRequest.RequestUri);
        Assert.Contains(
            GitHubReleaseChecker.UserAgentProduct,
            handler.LastRequest.Headers.UserAgent.ToString(),
            StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("0.0.0-local")]
    [InlineData("1.0.0-alpha")]
    [InlineData("1.0")]
    [InlineData("Unknown")]
    [InlineData("")]
    [InlineData(null)]
    public async Task CheckAsync_skips_the_request_when_installed_version_is_ineligible(string? installed)
    {
        var handler = new StubHandler(ReleaseJson(NewerTag, NewerHtmlUrl));
        using var checker = new GitHubReleaseChecker(handler);

        LauncherUpdate? update = await checker.CheckAsync(installed);

        Assert.Null(update);
        Assert.Equal(0, handler.RequestCount);
    }

    [Fact]
    public async Task CheckAsync_returns_nothing_when_the_release_is_not_newer()
    {
        var handler = new StubHandler(ReleaseJson("v1.0.0", "https://github.com/booldata/power-bi-launcher/releases/tag/v1.0.0"));
        using var checker = new GitHubReleaseChecker(handler);

        Assert.Null(await checker.CheckAsync("1.0.0"));
        Assert.Null(await checker.CheckAsync("1.2.0"));
        Assert.Equal(2, handler.RequestCount);
    }

    [Fact]
    public async Task CheckAsync_notifies_a_beta_install_when_stable_is_newer()
    {
        var handler = new StubHandler(ReleaseJson(NewerTag, NewerHtmlUrl));
        using var checker = new GitHubReleaseChecker(handler);

        LauncherUpdate? update = await checker.CheckAsync("1.0.0-beta.1");

        Assert.NotNull(update);
        Assert.Equal("1.2.3", update.Value.VersionText);
        Assert.Equal(new Uri(NewerHtmlUrl), update.Value.ReleaseUrl);
        Assert.Equal(1, handler.RequestCount);
    }

    [Fact]
    public async Task CheckAsync_ignores_drafts_prereleases_and_invalid_tags()
    {
        await AssertIgnored(ReleaseJson(NewerTag, NewerHtmlUrl, draft: true));
        await AssertIgnored(ReleaseJson(NewerTag, NewerHtmlUrl, prerelease: true));
        await AssertIgnored(ReleaseJson("1.2.3", NewerHtmlUrl));
        await AssertIgnored(ReleaseJson("v1.2.3-beta", "https://github.com/booldata/power-bi-launcher/releases/tag/v1.2.3-beta"));
    }

    [Fact]
    public async Task CheckAsync_ignores_non_https_and_nonmatching_release_urls()
    {
        await AssertIgnored(ReleaseJson(NewerTag, "http://github.com/booldata/power-bi-launcher/releases/tag/v1.2.3"));
        await AssertIgnored(ReleaseJson(NewerTag, "https://evil.example/releases/tag/v1.2.3"));
        await AssertIgnored(ReleaseJson(NewerTag, "https://github.com/other/repo/releases/tag/v1.2.3"));
        await AssertIgnored(ReleaseJson(NewerTag, "https://github.com/booldata/power-bi-launcher/releases/tag/v9.9.9"));
        await AssertIgnored(ReleaseJson(NewerTag, "https://github.com/booldata/power-bi-launcher/releases/tag/V1.2.3"));
        await AssertIgnored(ReleaseJson(NewerTag, "https://github.com/booldata/power-bi-launcher/releases/tag/v1.2.3/"));
        await AssertIgnored(ReleaseJson(NewerTag, "https://github.com/booldata/power-bi-launcher/releases/tag/v1.2.3?q=1"));
        await AssertIgnored(ReleaseJson(NewerTag, "https://github.com/booldata/power-bi-launcher/releases/tag/v1.2.3#files"));
    }

    [Fact]
    public async Task CheckAsync_is_silent_for_http_and_json_errors()
    {
        await AssertSilent(new StubHandler(HttpStatusCode.NotFound));
        await AssertSilent(new StubHandler("{not-json", HttpStatusCode.OK));
        await AssertSilent(new StubHandler("[]", HttpStatusCode.OK));
        await AssertSilent(new StubHandler(_ => throw new HttpRequestException("offline")));
    }

    [Fact]
    public async Task CheckAsync_is_silent_when_cancelled_before_the_request()
    {
        var handler = new StubHandler(ReleaseJson(NewerTag, NewerHtmlUrl));
        using var checker = new GitHubReleaseChecker(handler);
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        Assert.Null(await checker.CheckAsync(Installed, cts.Token));
        Assert.Equal(0, handler.RequestCount);
    }

    [Fact]
    public async Task CheckAsync_is_silent_when_the_request_is_cancelled()
    {
        using var cts = new CancellationTokenSource();
        var handler = new StubHandler(_ =>
        {
            cts.Cancel();
            cts.Token.ThrowIfCancellationRequested();
            return new HttpResponseMessage(HttpStatusCode.OK);
        });
        using var checker = new GitHubReleaseChecker(handler);

        Assert.Null(await checker.CheckAsync(Installed, cts.Token));
        Assert.Equal(1, handler.RequestCount);
    }

    [Theory]
    [InlineData("0.0.0-local")]
    [InlineData("Unknown")]
    [InlineData("")]
    [InlineData(null)]
    public async Task CheckDetailedAsync_marks_ineligible_builds(string? installed)
    {
        var handler = new StubHandler(ReleaseJson(NewerTag, NewerHtmlUrl));
        using var checker = new GitHubReleaseChecker(handler);

        LauncherUpdateCheckResult result = await checker.CheckDetailedAsync(installed);

        Assert.Equal(LauncherUpdateCheckStatus.Ineligible, result.Status);
        Assert.Null(result.Update);
        Assert.Equal(0, handler.RequestCount);
    }

    [Fact]
    public async Task CheckDetailedAsync_reports_up_to_date_when_the_release_is_not_newer()
    {
        var handler = new StubHandler(ReleaseJson("v1.0.0", "https://github.com/booldata/power-bi-launcher/releases/tag/v1.0.0"));
        using var checker = new GitHubReleaseChecker(handler);

        LauncherUpdateCheckResult result = await checker.CheckDetailedAsync("1.0.0");

        Assert.Equal(LauncherUpdateCheckStatus.UpToDate, result.Status);
        Assert.Null(result.Update);
        Assert.Equal(1, handler.RequestCount);
    }

    [Fact]
    public async Task CheckDetailedAsync_reports_an_available_update()
    {
        var handler = new StubHandler(ReleaseJson(NewerTag, NewerHtmlUrl));
        using var checker = new GitHubReleaseChecker(handler);

        LauncherUpdateCheckResult result = await checker.CheckDetailedAsync(Installed);

        Assert.Equal(LauncherUpdateCheckStatus.UpdateAvailable, result.Status);
        Assert.NotNull(result.Update);
        Assert.Equal("1.2.3", result.Update!.Value.VersionText);
        Assert.Equal(new Uri(NewerHtmlUrl), result.Update.Value.ReleaseUrl);
    }

    [Fact]
    public async Task CheckDetailedAsync_reports_failure_for_network_errors()
    {
        var handler = new StubHandler(_ => throw new HttpRequestException("offline"));
        using var checker = new GitHubReleaseChecker(handler);

        LauncherUpdateCheckResult result = await checker.CheckDetailedAsync(Installed);

        Assert.Equal(LauncherUpdateCheckStatus.Failed, result.Status);
        Assert.Null(result.Update);
    }

    private static async Task AssertIgnored(string json)
    {
        var handler = new StubHandler(json);
        using var checker = new GitHubReleaseChecker(handler);

        Assert.Null(await checker.CheckAsync(Installed));
        Assert.Equal(1, handler.RequestCount);
    }

    private static async Task AssertSilent(StubHandler handler)
    {
        using var checker = new GitHubReleaseChecker(handler);

        Assert.Null(await checker.CheckAsync(Installed));
    }

    private static string ReleaseJson(
        string tagName,
        string htmlUrl,
        bool draft = false,
        bool prerelease = false) =>
        $$"""
        {
          "tag_name": "{{tagName}}",
          "html_url": "{{htmlUrl}}",
          "draft": {{(draft ? "true" : "false")}},
          "prerelease": {{(prerelease ? "true" : "false")}},
          "name": "ignored"
        }
        """;

    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _respond;

        public StubHandler(string content, HttpStatusCode statusCode = HttpStatusCode.OK)
            : this(_ => new HttpResponseMessage(statusCode)
            {
                Content = new StringContent(content, Encoding.UTF8, "application/json")
            })
        {
        }

        public StubHandler(HttpStatusCode statusCode)
            : this(_ => new HttpResponseMessage(statusCode))
        {
        }

        public StubHandler(Func<HttpRequestMessage, HttpResponseMessage> respond)
        {
            _respond = respond;
        }

        public int RequestCount { get; private set; }

        public HttpRequestMessage? LastRequest { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            RequestCount++;
            LastRequest = request;
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(_respond(request));
        }
    }
}
