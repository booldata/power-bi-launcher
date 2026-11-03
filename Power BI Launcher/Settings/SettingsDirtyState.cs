using System;
using System.Collections.Generic;

namespace PowerBILauncher;

/// <summary>
/// UI-free dirty check for Settings vs the last saved snapshot.
/// Footer Save enablement is <see cref="CanEnableFooterSave"/>; onboarding stays savable
/// unless the Folder to store Profiles path has an issue.
/// </summary>
public static class SettingsDirtyState
{
    public static bool CanEnableFooterSave(bool dirty, bool hasCompletedSetup, SettingsIssue? pathIssue) =>
        (dirty || !hasCompletedSetup) && pathIssue is null;

    public static bool ShouldShowProfilesRootChangeNote(
        string? savedRoot,
        string? editedRoot,
        int profileCount)
    {
        if (profileCount <= 0)
        {
            return false;
        }

        return !string.Equals(
            (savedRoot ?? "").Trim(),
            (editedRoot ?? "").Trim(),
            StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsDirty(
        Setting? saved,
        IReadOnlyList<PbiVersion> draftVersions,
        Guid? draftDefaultVersionId,
        string currentFolder,
        MinimizeButtonBehavior currentMinimize,
        CloseButtonBehavior currentClose)
    {
        if (saved is null)
        {
            return false;
        }

        ArgumentNullException.ThrowIfNull(draftVersions);

        if (!string.Equals(
                (saved.UserProfilePath ?? "").Trim(),
                (currentFolder ?? "").Trim(),
                StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (saved.MinimizeBehavior != currentMinimize
            || saved.CloseBehavior != currentClose
            || saved.DefaultVersionId != draftDefaultVersionId)
        {
            return true;
        }

        IReadOnlyList<PbiVersion> savedVersions = saved.Versions;
        if (savedVersions.Count != draftVersions.Count)
        {
            return true;
        }

        for (int i = 0; i < draftVersions.Count; i++)
        {
            PbiVersion a = savedVersions[i];
            PbiVersion b = draftVersions[i];
            if (a.Id != b.Id
                || a.Kind != b.Kind
                || !string.Equals(a.DisplayName, b.DisplayName, StringComparison.Ordinal)
                || !string.Equals(a.Path, b.Path, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
}
