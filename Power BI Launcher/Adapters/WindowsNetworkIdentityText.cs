namespace PowerBILauncher;

/// <summary>
/// Normalize and parse optional Windows network identity text (DOMAIN\user or UPN).
/// </summary>
public static class WindowsNetworkIdentityText
{
    public static string? Normalize(string? identityText)
    {
        if (string.IsNullOrWhiteSpace(identityText))
        {
            return null;
        }

        return identityText.Trim();
    }

    public static bool TryParse(string? identityText, out string userName, out string? domain)
    {
        string? normalized = Normalize(identityText);
        if (normalized is null)
        {
            userName = "";
            domain = null;
            return false;
        }

        int separator = normalized.IndexOf('\\');
        if (separator > 0 && separator < normalized.Length - 1)
        {
            domain = normalized[..separator];
            userName = normalized[(separator + 1)..];
            return true;
        }

        userName = normalized;
        domain = null;
        return true;
    }
}
