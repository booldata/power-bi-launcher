using System.Globalization;
using System.Reflection;

namespace PowerBILauncher;

/// <summary>
/// Reads launcher build metadata from the running assembly without a custom publish step.
/// </summary>
public static class LauncherBuildInfo
{
    private const string BuildDateKey = "BuildDate";
    private const string BuildDateFormat = "yyyy-MM-dd";

    public static DateTimeOffset? TryGetBuildDate(Assembly assembly)
    {
        ArgumentNullException.ThrowIfNull(assembly);

        string? raw = assembly
            .GetCustomAttributes<AssemblyMetadataAttribute>()
            .FirstOrDefault(attribute =>
                string.Equals(attribute.Key, BuildDateKey, StringComparison.Ordinal))
            ?.Value;

        return TryParseBuildDate(raw);
    }

    public static string FormatBuildDate(DateTimeOffset buildDate) =>
        buildDate.ToString("d", CultureInfo.CurrentCulture);

    private static DateTimeOffset? TryParseBuildDate(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        if (!DateTime.TryParseExact(
                raw.Trim(),
                BuildDateFormat,
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out DateTime date))
        {
            return null;
        }

        return new DateTimeOffset(date, TimeSpan.Zero);
    }
}
