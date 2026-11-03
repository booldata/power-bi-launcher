using System.Text.RegularExpressions;

namespace PowerBILauncher;

public sealed class PbiDiscoveryCandidate
{
    public string Path { get; init; } = "";
    public string? ProductName { get; init; }
    public string? FileDescription { get; init; }
    public string? DisplayHint { get; init; }
    public bool IsStorePackage { get; init; }
}

public sealed class ScanAcceptedVersion
{
    public string Path { get; init; } = "";
    public PbiVersionKind Kind { get; init; }
    public string DisplayName { get; init; } = "";
    public bool IsMicrosoftStore { get; init; }
}

public interface IPbiDiscovery
{
    IReadOnlyList<PbiDiscoveryCandidate> FindCandidates();
}

/// <summary>
/// Maps discovery candidates to accepted Versions. Drops junk that fails the executable bar.
/// </summary>
public static class ScanPolicy
{
    private static readonly Regex MonthYear = new(
        @"\b(January|February|March|April|May|June|July|August|September|October|November|December)\s+\d{4}\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    public static IReadOnlyList<ScanAcceptedVersion> Accept(
        IReadOnlyList<PbiDiscoveryCandidate> candidates,
        Func<string, PbiExecutableFacts> inspect)
    {
        ArgumentNullException.ThrowIfNull(candidates);
        ArgumentNullException.ThrowIfNull(inspect);

        var accepted = new List<ScanAcceptedVersion>();
        foreach (PbiDiscoveryCandidate candidate in MergeByPath(candidates))
        {
            string path = candidate.Path;
            PbiExecutableFacts facts = inspect(path);
            if (!PbiExecutablePolicy.IsPowerBiExecutable(facts))
            {
                continue;
            }

            PbiVersionKind kind = PbiVersionKindPolicy.Infer(
                path,
                candidate.ProductName ?? facts.ProductName,
                candidate.FileDescription ?? facts.FileDescription,
                candidate.DisplayHint);
            accepted.Add(new ScanAcceptedVersion
            {
                Path = path,
                Kind = kind,
                DisplayName = DisplayName(
                    kind,
                    path,
                    candidate.ProductName ?? facts.ProductName,
                    candidate.DisplayHint,
                    candidate.IsStorePackage),
                IsMicrosoftStore = PbiVersionKindPolicy.IsMicrosoftStore(path, candidate.IsStorePackage)
            });
        }

        return Disambiguate(accepted);
    }

    public static string DisplayName(
        PbiVersionKind kind,
        string path,
        string? productName,
        string? displayHint,
        bool isStorePackage = false)
    {
        string kindLabel = PbiVersionKindPolicy.ScanNameHead(kind);
        string? monthYear = ExtractMonthYear(displayHint)
            ?? ExtractMonthYear(productName)
            ?? ExtractMonthYear(path);

        switch (kind)
        {
            case PbiVersionKind.ReportServer:
                return monthYear is null ? kindLabel : $"{kindLabel} · {monthYear}";
            case PbiVersionKind.Desktop:
                if (PbiVersionKindPolicy.IsMicrosoftStore(path, isStorePackage))
                {
                    return $"{kindLabel} · Microsoft Store";
                }

                return monthYear is null
                    ? $"{kindLabel} · Downloaded"
                    : $"{kindLabel} · Downloaded · {monthYear}";
            default:
                throw new ArgumentOutOfRangeException(nameof(kind), kind, null);
        }
    }

    public static bool IsPlaceholderDisplayName(string? displayName, PbiVersionKind kind) =>
        string.Equals(
            displayName?.Trim(),
            PbiVersionKindPolicy.ScanNameHead(kind),
            StringComparison.OrdinalIgnoreCase);

    private static List<PbiDiscoveryCandidate> MergeByPath(IReadOnlyList<PbiDiscoveryCandidate> candidates)
    {
        var merged = new List<PbiDiscoveryCandidate>();
        var indexByPath = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (PbiDiscoveryCandidate candidate in candidates)
        {
            string path = candidate.Path?.Trim() ?? "";
            if (path.Length == 0)
            {
                continue;
            }

            if (indexByPath.TryGetValue(path, out int index))
            {
                merged[index] = Combine(merged[index], candidate);
                continue;
            }

            indexByPath[path] = merged.Count;
            merged.Add(new PbiDiscoveryCandidate
            {
                Path = path,
                ProductName = candidate.ProductName,
                FileDescription = candidate.FileDescription,
                DisplayHint = candidate.DisplayHint,
                IsStorePackage = candidate.IsStorePackage
            });
        }

        return merged;
    }

    private static PbiDiscoveryCandidate Combine(
        PbiDiscoveryCandidate current,
        PbiDiscoveryCandidate incoming) =>
        new()
        {
            Path = current.Path,
            ProductName = FirstNonEmpty(current.ProductName, incoming.ProductName),
            FileDescription = FirstNonEmpty(current.FileDescription, incoming.FileDescription),
            DisplayHint = PreferHint(current.DisplayHint, incoming.DisplayHint),
            IsStorePackage = current.IsStorePackage || incoming.IsStorePackage
        };

    private static List<ScanAcceptedVersion> Disambiguate(List<ScanAcceptedVersion> accepted)
    {
        var counts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (ScanAcceptedVersion item in accepted)
        {
            counts[item.DisplayName] = counts.GetValueOrDefault(item.DisplayName) + 1;
        }

        var result = new List<ScanAcceptedVersion>(accepted.Count);
        foreach (ScanAcceptedVersion item in accepted)
        {
            if (counts[item.DisplayName] < 2)
            {
                result.Add(item);
                continue;
            }

            string? folder = InstallFolderName(item.Path);
            string displayName = string.IsNullOrEmpty(folder) ||
                item.DisplayName.Contains(folder, StringComparison.OrdinalIgnoreCase)
                ? item.DisplayName
                : $"{item.DisplayName} · {folder}";
            result.Add(new ScanAcceptedVersion
            {
                Path = item.Path,
                Kind = item.Kind,
                DisplayName = displayName,
                IsMicrosoftStore = item.IsMicrosoftStore
            });
        }

        return result;
    }

    private static string? InstallFolderName(string path)
    {
        string? directory = Path.GetDirectoryName(path);
        if (string.IsNullOrEmpty(directory))
        {
            return null;
        }

        string leaf = Path.GetFileName(directory);
        if (leaf.Equals("bin", StringComparison.OrdinalIgnoreCase))
        {
            directory = Path.GetDirectoryName(directory);
            leaf = string.IsNullOrEmpty(directory) ? "" : Path.GetFileName(directory);
        }

        return string.IsNullOrWhiteSpace(leaf) ? null : leaf;
    }

    private static string? PreferHint(string? current, string? incoming)
    {
        if (ExtractMonthYear(current) is not null)
        {
            return current;
        }

        if (ExtractMonthYear(incoming) is not null)
        {
            return incoming;
        }

        return FirstNonEmpty(current, incoming);
    }

    private static string? FirstNonEmpty(string? current, string? incoming) =>
        string.IsNullOrWhiteSpace(current) ? incoming : current;

    private static string? ExtractMonthYear(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        Match match = MonthYear.Match(value);
        return match.Success ? match.Value : null;
    }
}
