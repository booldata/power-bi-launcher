using System;
using System.Collections.Generic;

namespace PowerBILauncher
{
    public static class ProfileMigration
    {
        public static bool MigrateInPlace(IList<Profile> profiles, Func<string>? pickAvatarColor = null)
        {
            pickAvatarColor ??= LetterAvatar.PickRandomColor;

            bool dirty = false;
            foreach (var profile in profiles)
            {
                if (profile.CardColor == "#FFFFFF")
                {
                    profile.CardColor = "";
                    dirty = true;
                }

                if (string.IsNullOrWhiteSpace(profile.AvatarColor))
                {
                    profile.AvatarColor = pickAvatarColor();
                    dirty = true;
                }

                // Old default pointed at pbi.png beside the exe; treat as letter avatar.
                if (!string.IsNullOrWhiteSpace(profile.IconPath) &&
                    !LetterAvatar.HasCustomIcon(profile.IconPath) &&
                    profile.IconPath.EndsWith("pbi.png", StringComparison.OrdinalIgnoreCase))
                {
                    profile.IconPath = "";
                    dirty = true;
                }

                // Missing ShowOnTray (pre-feature JSON) → on. Explicit false survives.
                if (profile.ShowOnTray is null)
                {
                    profile.ShowOnTray = true;
                    dirty = true;
                }
            }

            return dirty;
        }
    }
}
