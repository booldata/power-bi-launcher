using System.Globalization;

namespace PowerBILauncher;

/// <summary>
/// Parsed launcher version. Stable triples have a null <see cref="Prerelease"/>;
/// recognised prereleases store the suffix (<c>beta</c>, <c>beta.N</c>, <c>rc.N</c>).
/// A prerelease sorts below the matching stable triple.
/// </summary>
public readonly record struct LauncherVersion(int Major, int Minor, int Patch, string? Prerelease = null)
    : IComparable<LauncherVersion>
{
    public int CompareTo(LauncherVersion other)
    {
        int major = Major.CompareTo(other.Major);
        if (major != 0)
        {
            return major;
        }

        int minor = Minor.CompareTo(other.Minor);
        if (minor != 0)
        {
            return minor;
        }

        int patch = Patch.CompareTo(other.Patch);
        if (patch != 0)
        {
            return patch;
        }

        bool thisStable = string.IsNullOrEmpty(Prerelease);
        bool otherStable = string.IsNullOrEmpty(other.Prerelease);
        if (thisStable && otherStable)
        {
            return 0;
        }

        if (thisStable)
        {
            return 1;
        }

        if (otherStable)
        {
            return -1;
        }

        return ComparePrereleaseIdentifiers(Prerelease!, other.Prerelease!);
    }

    public bool IsNewerThan(LauncherVersion other) => CompareTo(other) > 0;

    public override string ToString()
    {
        if (string.IsNullOrEmpty(Prerelease))
        {
            return string.Create(CultureInfo.InvariantCulture, $"{Major}.{Minor}.{Patch}");
        }

        return string.Create(CultureInfo.InvariantCulture, $"{Major}.{Minor}.{Patch}-{Prerelease}");
    }

    private static int ComparePrereleaseIdentifiers(string left, string right)
    {
        string[] leftParts = left.Split('.');
        string[] rightParts = right.Split('.');
        int shared = Math.Min(leftParts.Length, rightParts.Length);
        for (int i = 0; i < shared; i++)
        {
            bool leftNumeric = TryParseNumericIdent(leftParts[i], out int leftValue);
            bool rightNumeric = TryParseNumericIdent(rightParts[i], out int rightValue);
            if (leftNumeric && rightNumeric)
            {
                int numeric = leftValue.CompareTo(rightValue);
                if (numeric != 0)
                {
                    return numeric;
                }

                continue;
            }

            if (leftNumeric)
            {
                return -1;
            }

            if (rightNumeric)
            {
                return 1;
            }

            int ordinal = string.CompareOrdinal(leftParts[i], rightParts[i]);
            if (ordinal != 0)
            {
                return ordinal;
            }
        }

        return leftParts.Length.CompareTo(rightParts.Length);
    }

    private static bool TryParseNumericIdent(string text, out int value)
    {
        value = 0;
        if (string.IsNullOrEmpty(text))
        {
            return false;
        }

        if (text.Length > 1 && text[0] == '0')
        {
            return false;
        }

        return int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out value);
    }
}

/// <summary>
/// Installed informational versions are <c>X.Y.Z</c> or a recognised prerelease
/// (<c>X.Y.Z-beta</c>, <c>X.Y.Z-beta.N</c>, <c>X.Y.Z-rc.N</c>).
/// GitHub release tags compared by the updater remain strict <c>vX.Y.Z</c>.
/// Local <c>-local</c> suffixes are rejected so <c>dotnet run</c> does not nag.
/// </summary>
public static class LauncherVersionPolicy
{
    public static bool TryParseInstalled(string? informationalVersion, out LauncherVersion version)
    {
        version = default;
        string? normalized = StripBuildMetadata(informationalVersion);
        if (normalized is null)
        {
            return false;
        }

        int suffixSeparator = normalized.IndexOf('-');
        if (suffixSeparator < 0)
        {
            if (!TryParseNumericTriple(normalized, out int major, out int minor, out int patch))
            {
                return false;
            }

            version = new LauncherVersion(major, minor, patch);
            return true;
        }

        if (!TryParseNumericTriple(normalized[..suffixSeparator], out int coreMajor, out int coreMinor, out int corePatch))
        {
            return false;
        }

        string suffix = normalized[(suffixSeparator + 1)..];
        if (!IsRecognisedPrerelease(suffix))
        {
            return false;
        }

        version = new LauncherVersion(coreMajor, coreMinor, corePatch, suffix);
        return true;
    }

    public static bool TryParseReleaseTag(string? tagName, out LauncherVersion version)
    {
        version = default;
        if (string.IsNullOrWhiteSpace(tagName))
        {
            return false;
        }

        string tag = tagName.Trim();
        if (tag.Length < 2 || tag[0] != 'v')
        {
            return false;
        }

        if (!TryParseNumericTriple(tag[1..], out int major, out int minor, out int patch))
        {
            return false;
        }

        version = new LauncherVersion(major, minor, patch);
        return true;
    }

    private static bool TryParseComponent(string text, out int value)
    {
        value = 0;
        if (string.IsNullOrEmpty(text))
        {
            return false;
        }

        if (text.Length > 1 && text[0] == '0')
        {
            return false;
        }

        return int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out value);
    }

    private static string? StripBuildMetadata(string? informationalVersion)
    {
        if (string.IsNullOrWhiteSpace(informationalVersion))
        {
            return null;
        }

        string trimmed = informationalVersion.Trim();
        int metadataSeparator = trimmed.IndexOf('+');
        return metadataSeparator >= 0 ? trimmed[..metadataSeparator] : trimmed;
    }

    private static bool TryParseNumericTriple(string? text, out int major, out int minor, out int patch)
    {
        major = 0;
        minor = 0;
        patch = 0;
        if (string.IsNullOrEmpty(text))
        {
            return false;
        }

        string[] parts = text.Split('.');
        if (parts.Length != 3)
        {
            return false;
        }

        return TryParseComponent(parts[0], out major) &&
               TryParseComponent(parts[1], out minor) &&
               TryParseComponent(parts[2], out patch);
    }

    private static bool IsRecognisedPrerelease(string suffix)
    {
        if (suffix == "beta")
        {
            return true;
        }

        int dot = suffix.IndexOf('.');
        if (dot <= 0 || dot != suffix.LastIndexOf('.'))
        {
            return false;
        }

        string label = suffix[..dot];
        if (label is not ("beta" or "rc"))
        {
            return false;
        }

        return TryParseComponent(suffix[(dot + 1)..], out _);
    }
}
