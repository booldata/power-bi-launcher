namespace PowerBILauncher.Tests;

public class WindowsNetworkIdentityTests
{
    [Fact]
    public void Normalize_trims_outer_whitespace_and_preserves_spelling()
    {
        Assert.Equal(@"CONTOSO\JSmith", WindowsNetworkIdentityText.Normalize("  CONTOSO\\JSmith  "));
        Assert.Equal("jsmith@contoso.com", WindowsNetworkIdentityText.Normalize(" jsmith@contoso.com "));
        Assert.Null(WindowsNetworkIdentityText.Normalize("   "));
        Assert.Null(WindowsNetworkIdentityText.Normalize(null));
    }

    [Fact]
    public void TryParse_splits_domain_backslash_user()
    {
        Assert.True(WindowsNetworkIdentityText.TryParse(@"CONTOSO\JSmith", out string userName, out string? domain));
        Assert.Equal("JSmith", userName);
        Assert.Equal("CONTOSO", domain);
    }

    [Fact]
    public void TryParse_treats_upn_as_username_without_domain()
    {
        Assert.True(WindowsNetworkIdentityText.TryParse("jsmith@contoso.com", out string userName, out string? domain));
        Assert.Equal("jsmith@contoso.com", userName);
        Assert.Null(domain);
    }
}
