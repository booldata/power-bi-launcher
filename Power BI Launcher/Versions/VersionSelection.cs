using System;
using System.Collections.Generic;
using System.Linq;

namespace PowerBILauncher;

public enum VersionSelectionKind
{
    FollowDefault,
    Explicit,
    EmptyRegistry,
    Dangling
}

public sealed class VersionChoiceOption
{
    public Guid? VersionId { get; init; }
    public string Label { get; init; } = "";
    public bool IsFollowDefault { get; init; }
    public bool IsPlaceholder { get; init; }
    public VersionSelectionKind Kind { get; init; }
}

/// <summary>
/// UI-free Version ComboBox options and empty/dangling evaluation for Edit Profile Launch.
/// </summary>
public static class VersionSelection
{
    public static VersionSelectionKind Evaluate(
        Guid? selectedVersionId,
        IReadOnlyList<PbiVersion> versions,
        Guid? defaultVersionId)
    {
        ArgumentNullException.ThrowIfNull(versions);
        if (versions.Count == 0)
        {
            return VersionSelectionKind.EmptyRegistry;
        }

        if (selectedVersionId is null)
        {
            return VersionSelectionKind.FollowDefault;
        }

        return versions.Any(version => version.Id == selectedVersionId.Value)
            ? VersionSelectionKind.Explicit
            : VersionSelectionKind.Dangling;
    }

    public static IReadOnlyList<VersionChoiceOption> BuildOptions(
        IReadOnlyList<PbiVersion> versions,
        Guid? defaultVersionId,
        Guid? selectedVersionId)
    {
        ArgumentNullException.ThrowIfNull(versions);
        if (versions.Count == 0)
        {
            return
            [
                new VersionChoiceOption
                {
                    IsPlaceholder = true,
                    Kind = VersionSelectionKind.EmptyRegistry
                }
            ];
        }

        var options = new List<VersionChoiceOption>
        {
            new()
            {
                VersionId = null,
                Label = DefaultVersionDisplayName(versions, defaultVersionId),
                IsFollowDefault = true,
                Kind = VersionSelectionKind.FollowDefault
            }
        };

        foreach (PbiVersion version in versions)
        {
            options.Add(new VersionChoiceOption
            {
                VersionId = version.Id,
                Label = version.DisplayName,
                Kind = VersionSelectionKind.Explicit
            });
        }

        if (selectedVersionId is Guid danglingId
            && versions.All(version => version.Id != danglingId))
        {
            options.Add(new VersionChoiceOption
            {
                VersionId = danglingId,
                IsPlaceholder = true,
                Kind = VersionSelectionKind.Dangling
            });
        }

        return options;
    }

    /// <summary>
    /// The Version id Edit Profile Save writes. An empty registry offers nothing to pick (settings.json
    /// may not have loaded this session), so the Profile keeps <paramref name="savedVersionId"/>.
    /// </summary>
    public static Guid? VersionIdToSave(
        Guid? selectedVersionId,
        Guid? savedVersionId,
        IReadOnlyList<PbiVersion> versions)
    {
        ArgumentNullException.ThrowIfNull(versions);
        return versions.Count == 0 ? savedVersionId : selectedVersionId;
    }

    private static string DefaultVersionDisplayName(IReadOnlyList<PbiVersion> versions, Guid? defaultVersionId)
    {
        PbiVersion? defaultVersion = defaultVersionId is Guid id
            ? versions.FirstOrDefault(version => version.Id == id)
            : null;
        return defaultVersion?.DisplayName ?? "";
    }
}
