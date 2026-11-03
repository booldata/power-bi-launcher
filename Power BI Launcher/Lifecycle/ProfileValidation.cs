using System;
using System.Collections.Generic;
using System.Linq;

namespace PowerBILauncher
{
    public enum ProfileValidationKind
    {
        NameEmpty,
        NameDuplicate
    }

    public static class ProfileValidation
    {
        /// <summary>
        /// Profile names are compared case-insensitively (OrdinalIgnoreCase).
        /// </summary>
        public static ProfileValidationKind? TryValidateProfileName(
            IReadOnlyList<Profile> profiles,
            string name,
            Guid? editingProfileId = null)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return ProfileValidationKind.NameEmpty;
            }

            string trimmed = name.Trim();
            bool duplicate = profiles.Any(profile =>
                profile.Id != editingProfileId &&
                string.Equals(profile.Name, trimmed, StringComparison.OrdinalIgnoreCase));

            if (duplicate)
            {
                return ProfileValidationKind.NameDuplicate;
            }

            return null;
        }
    }
}
