namespace PowerBILauncher.Tests;

public class LauncherVersionPolicyTests
{
    [Theory]
    [InlineData("1.2.3", 1, 2, 3, null)]
    [InlineData(" 1.2.3 ", 1, 2, 3, null)]
    [InlineData("1.2.3+abc123", 1, 2, 3, null)]
    [InlineData("0.0.1", 0, 0, 1, null)]
    [InlineData("10.20.30", 10, 20, 30, null)]
    [InlineData("1.0.0-beta", 1, 0, 0, "beta")]
    [InlineData("1.0.0-beta.1", 1, 0, 0, "beta.1")]
    [InlineData("1.0.0-rc.1", 1, 0, 0, "rc.1")]
    [InlineData("1.1.0-beta.1+abc123", 1, 1, 0, "beta.1")]
    public void TryParseInstalled_accepts_stable_triples_and_recognised_prereleases(
        string informationalVersion,
        int major,
        int minor,
        int patch,
        string? prerelease)
    {
        Assert.True(LauncherVersionPolicy.TryParseInstalled(informationalVersion, out LauncherVersion version));
        Assert.Equal(new LauncherVersion(major, minor, patch, prerelease), version);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("Unknown")]
    [InlineData("0.0.0-local")]
    [InlineData("1.2.3-local+deadbeef")]
    [InlineData("1.0.0-alpha")]
    [InlineData("1.0.0-rc")]
    [InlineData("1.0.0-beta.01")]
    [InlineData("1.0")]
    [InlineData("1.0.0.0")]
    [InlineData("01.0.0")]
    [InlineData("v1.2.3")]
    public void TryParseInstalled_rejects_local_malformed_and_unknown(string? informationalVersion)
    {
        // -local (and any suffix that is not beta / beta.N / rc.N) must not resolve,
        // or local `dotnet run` builds would start showing update prompts.
        Assert.False(LauncherVersionPolicy.TryParseInstalled(informationalVersion, out _));
    }

    [Fact]
    public void TryParseReleaseTag_accepts_strict_vXYZ()
    {
        Assert.True(LauncherVersionPolicy.TryParseReleaseTag("v1.2.3", out LauncherVersion version));
        Assert.Equal(new LauncherVersion(1, 2, 3), version);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("1.2.3")]
    [InlineData("V1.2.3")]
    [InlineData("v1.2.3-beta")]
    [InlineData("v1.2.3-beta.1")]
    [InlineData("v1.2")]
    [InlineData("vv1.2.3")]
    [InlineData("v01.2.3")]
    public void TryParseReleaseTag_rejects_missing_v_and_prerelease_tags(string? tagName)
    {
        Assert.False(LauncherVersionPolicy.TryParseReleaseTag(tagName, out _));
    }

    [Fact]
    public void IsNewerThan_is_numeric_and_orders_prereleases_below_stable()
    {
        var installed = new LauncherVersion(1, 2, 3);

        Assert.True(new LauncherVersion(1, 2, 4).IsNewerThan(installed));
        Assert.True(new LauncherVersion(1, 10, 0).IsNewerThan(new LauncherVersion(1, 9, 9)));
        Assert.True(new LauncherVersion(2, 0, 0).IsNewerThan(new LauncherVersion(1, 9, 9)));
        Assert.False(installed.IsNewerThan(installed));
        Assert.False(new LauncherVersion(1, 2, 2).IsNewerThan(installed));

        var beta1 = new LauncherVersion(1, 1, 0, "beta.1");
        var beta2 = new LauncherVersion(1, 1, 0, "beta.2");
        var betaBare = new LauncherVersion(1, 1, 0, "beta");
        var stable = new LauncherVersion(1, 1, 0);

        Assert.True(beta1.IsNewerThan(betaBare));
        Assert.True(beta2.IsNewerThan(beta1));
        Assert.True(stable.IsNewerThan(beta1));
        Assert.True(stable.IsNewerThan(beta2));
        Assert.False(beta1.IsNewerThan(stable));
        Assert.False(beta1.IsNewerThan(beta1));
        Assert.True(new LauncherVersion(1, 2, 0).IsNewerThan(new LauncherVersion(1, 1, 0, "beta.9")));
    }
}
