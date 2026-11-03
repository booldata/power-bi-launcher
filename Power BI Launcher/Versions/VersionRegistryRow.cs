using Microsoft.UI.Xaml;

namespace PowerBILauncher
{
    public sealed class VersionRegistryRow
    {
        /// <summary>
        /// Backstop for absurd paths only. The row already trims to the available width, so a
        /// shorter cap puts an ellipsis on ordinary install paths with free space beside them.
        /// </summary>
        public const int PathDisplayMaxLength = 120;

        public VersionRegistryRow(PbiVersion version, bool isDefault, bool isRenaming, int versionCount)
        {
            ArgumentNullException.ThrowIfNull(version);
            Version = version;
            IsDefault = isDefault;
            IsRenaming = isRenaming;
            CanRemove = !isDefault || versionCount <= 1;
            PathDisplay = FormatPathDisplay(version.Path);
        }

        public PbiVersion Version { get; }
        public Guid Id => Version.Id;
        public string DisplayName => Version.DisplayName;
        public string Path => Version.Path;
        public string PathDisplay { get; }
        public string PathTooltip => Path;
        public PbiVersionKind Kind => Version.Kind;
        public string KindBadgeText => PbiVersionKindPolicy.ScanNameHead(Kind);
        public bool IsDefault { get; }
        public bool CanSetDefault => !IsDefault;
        public bool CanRemove { get; }
        public bool IsRenaming { get; }
        public string AutomationName =>
            IsDefault ? $"{DisplayName}, Default Version" : DisplayName;
        public string RenameMenuAutomationId => $"VersionRename_{Id:N}";
        public string ConfirmRenameAutomationId => $"VersionRenameDone_{Id:N}";
        public string MoreButtonAutomationId => $"VersionMore_{Id:N}";
        public string? RemoveTooltip => CanRemove
            ? null
            : "Set another version as default before removing this one.";
        public Visibility DisplayVisibility => IsRenaming ? Visibility.Collapsed : Visibility.Visible;
        public Visibility EditVisibility => IsRenaming ? Visibility.Visible : Visibility.Collapsed;
        public Visibility DefaultMarkerVisibility =>
            IsDefault && !IsRenaming ? Visibility.Visible : Visibility.Collapsed;
        public Visibility SetDefaultVisibility =>
            CanSetDefault ? Visibility.Visible : Visibility.Collapsed;

        private static string FormatPathDisplay(string? path)
        {
            if (string.IsNullOrEmpty(path) || path.Length <= PathDisplayMaxLength)
            {
                return path ?? "";
            }

            return string.Concat(path.AsSpan(0, PathDisplayMaxLength - 1), "…");
        }

        public override string ToString() => AutomationName;
    }
}
