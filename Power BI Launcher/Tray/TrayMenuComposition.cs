using System;
using System.Collections.Generic;
using System.Linq;

namespace PowerBILauncher
{
    public enum TrayMenuEntryKind
    {
        LaunchProfile,
        Separator,
        ShowWindow,
        HideWindow,
        Exit
    }

    public sealed class TrayMenuEntry
    {
        public required TrayMenuEntryKind Kind { get; init; }
        public Guid? ProfileId { get; init; }
        public string Label { get; init; } = "";
    }

    /// <summary>
    /// UI-free tray menu composition: ordered entries from Profiles + setup-complete.
    /// Win32 code only renders the result. Chrome labels are filled by the host.
    /// </summary>
    public static class TrayMenuComposition
    {
        public static IReadOnlyList<TrayMenuEntry> Compose(
            IEnumerable<Profile> profilesInLaunchOrder,
            bool hasCompletedSetup,
            bool isWindowVisible)
        {
            ArgumentNullException.ThrowIfNull(profilesInLaunchOrder);

            var entries = new List<TrayMenuEntry>();
            var profiles = profilesInLaunchOrder as IList<Profile> ?? profilesInLaunchOrder.ToList();

            if (hasCompletedSetup)
            {
                foreach (Profile profile in profiles)
                {
                    // Default-on when unset (pre-migration); explicit false omits.
                    if (profile.ShowOnTray == false)
                    {
                        continue;
                    }

                    entries.Add(new TrayMenuEntry
                    {
                        Kind = TrayMenuEntryKind.LaunchProfile,
                        ProfileId = profile.Id,
                        Label = profile.Name
                    });
                }
            }

            if (entries.Count > 0)
            {
                entries.Add(new TrayMenuEntry
                {
                    Kind = TrayMenuEntryKind.Separator
                });
            }

            entries.Add(new TrayMenuEntry
            {
                Kind = isWindowVisible ? TrayMenuEntryKind.HideWindow : TrayMenuEntryKind.ShowWindow
            });
            entries.Add(new TrayMenuEntry
            {
                Kind = TrayMenuEntryKind.Exit
            });

            return entries;
        }
    }
}
