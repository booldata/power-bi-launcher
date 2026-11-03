using System;
using System.Collections.Generic;
using System.Linq;

namespace PowerBILauncher
{
    /// <summary>
    /// Card activate outcome from Profile board commanding policy.
    /// </summary>
    public enum ProfileCardActivateKind
    {
        Launch,
        Inert
    }

    public enum ProfileCardAutomationKind
    {
        Launch,
        Profile
    }

    public enum ProfileCardProductIconKind
    {
        Desktop,
        ReportServer,
        Warning
    }

    /// <summary>
    /// Keyboard shortcuts declared on a focused Profile board card.
    /// Enter/Space Launch through Button activation; F2 edits; Delete confirms delete.
    /// </summary>
    public enum ProfileCardShortcutKind
    {
        Launch,
        Edit,
        Delete
    }

    /// <summary>
    /// Caption facts for a Profile board card. Chrome maps these to display text.
    /// </summary>
    public sealed class ProfileCardCaption
    {
        public required string VersionDisplayName { get; init; }

        public required bool FollowsDefault { get; init; }

        public required bool IsUnresolved { get; init; }

        public required bool HasWindowsNetworkIdentity { get; init; }
    }

    /// <summary>
    /// UI-free Profile board commanding policy: card activate, automation name,
    /// header / overflow Reorder availability, shortcut availability, adaptive More
    /// reveal, caption, product-icon kind, and empty-board decisions.
    /// </summary>
    public static class ProfileCardInteraction
    {
        public static ProfileCardActivateKind ActivateKind(bool reorderMode, bool isLaunching = false) =>
            reorderMode || isLaunching ? ProfileCardActivateKind.Inert : ProfileCardActivateKind.Launch;

        public static ProfileCardAutomationKind AutomationKind(bool reorderMode) =>
            reorderMode ? ProfileCardAutomationKind.Profile : ProfileCardAutomationKind.Launch;

        public static bool IsReorderOffered(int profileCount) =>
            profileCount >= 2;

        public static bool IsShortcutAllowed(
            ProfileCardShortcutKind kind,
            bool reorderMode,
            bool isLaunching,
            bool dialogOrFlyoutOpen)
        {
            if (dialogOrFlyoutOpen)
            {
                return false;
            }

            return kind switch
            {
                ProfileCardShortcutKind.Launch =>
                    ActivateKind(reorderMode, isLaunching) == ProfileCardActivateKind.Launch,
                ProfileCardShortcutKind.Edit or ProfileCardShortcutKind.Delete => !reorderMode,
                _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null)
            };
        }

        public static bool AreAppShortcutsAllowed(
            bool setupComplete,
            bool editProfileOpen,
            bool dialogOrFlyoutOpen) =>
            setupComplete && !editProfileOpen && !dialogOrFlyoutOpen;

        public static bool ShouldLeaveReorderOnEscape(bool reorderMode, bool dialogOrFlyoutOpen) =>
            reorderMode && !dialogOrFlyoutOpen;

        public static bool ShowEmptyState(int profileCount) =>
            profileCount == 0;

        /// <summary>
        /// Whether the per-card More control should be revealed.
        /// Active touch/pen interaction: always; mouse: card hover, keyboard focus
        /// within, or flyout-open latch. Hidden in Reorder mode.
        /// </summary>
        public static bool IsMoreRevealed(
            bool reorderMode,
            bool touchOrPenPreferred,
            bool isPointerOver,
            bool hasKeyboardFocusWithin,
            bool isFlyoutOpen)
        {
            if (reorderMode)
            {
                return false;
            }

            if (touchOrPenPreferred)
            {
                return true;
            }

            return isPointerOver || hasKeyboardFocusWithin || isFlyoutOpen;
        }

        public static ProfileCardCaption Caption(
            Guid? profileVersionId,
            string? windowsNetworkIdentity,
            IReadOnlyList<PbiVersion> versions,
            Guid? defaultVersionId)
        {
            ArgumentNullException.ThrowIfNull(versions);

            bool hasIdentity = !string.IsNullOrWhiteSpace(windowsNetworkIdentity);
            VersionSelectionKind kind = VersionSelection.Evaluate(
                profileVersionId,
                versions,
                defaultVersionId);

            return kind switch
            {
                VersionSelectionKind.Explicit => new ProfileCardCaption
                {
                    VersionDisplayName = RequireVersion(versions, profileVersionId!.Value).DisplayName,
                    FollowsDefault = false,
                    IsUnresolved = false,
                    HasWindowsNetworkIdentity = hasIdentity
                },
                VersionSelectionKind.FollowDefault => CaptionForDefault(versions, defaultVersionId, hasIdentity),
                VersionSelectionKind.EmptyRegistry => new ProfileCardCaption
                {
                    VersionDisplayName = "",
                    FollowsDefault = true,
                    IsUnresolved = true,
                    HasWindowsNetworkIdentity = hasIdentity
                },
                VersionSelectionKind.Dangling => new ProfileCardCaption
                {
                    VersionDisplayName = "",
                    FollowsDefault = false,
                    IsUnresolved = true,
                    HasWindowsNetworkIdentity = hasIdentity
                },
                _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null)
            };
        }

        public static ProfileCardProductIconKind ProductIconKind(
            Guid? profileVersionId,
            IReadOnlyList<PbiVersion> versions,
            Guid? defaultVersionId)
        {
            ArgumentNullException.ThrowIfNull(versions);

            PbiVersion? resolved = ResolveVersion(profileVersionId, versions, defaultVersionId);
            if (resolved is null)
            {
                return ProfileCardProductIconKind.Warning;
            }

            return resolved.Kind switch
            {
                PbiVersionKind.Desktop => ProfileCardProductIconKind.Desktop,
                PbiVersionKind.ReportServer => ProfileCardProductIconKind.ReportServer,
                _ => throw new ArgumentOutOfRangeException(nameof(resolved.Kind), resolved.Kind, null)
            };
        }

        private static ProfileCardCaption CaptionForDefault(
            IReadOnlyList<PbiVersion> versions,
            Guid? defaultVersionId,
            bool hasIdentity)
        {
            PbiVersion? defaultVersion = FindVersion(versions, defaultVersionId);
            return new ProfileCardCaption
            {
                VersionDisplayName = defaultVersion?.DisplayName ?? "",
                FollowsDefault = true,
                IsUnresolved = defaultVersion is null,
                HasWindowsNetworkIdentity = hasIdentity
            };
        }

        private static PbiVersion? ResolveVersion(
            Guid? profileVersionId,
            IReadOnlyList<PbiVersion> versions,
            Guid? defaultVersionId)
        {
            VersionSelectionKind kind = VersionSelection.Evaluate(
                profileVersionId,
                versions,
                defaultVersionId);

            return kind switch
            {
                VersionSelectionKind.Explicit => FindVersion(versions, profileVersionId),
                VersionSelectionKind.FollowDefault => FindVersion(versions, defaultVersionId),
                VersionSelectionKind.EmptyRegistry => null,
                VersionSelectionKind.Dangling => null,
                _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null)
            };
        }

        private static PbiVersion RequireVersion(IReadOnlyList<PbiVersion> versions, Guid id) =>
            FindVersion(versions, id)
            ?? throw new InvalidOperationException($"Version {id} was expected after Evaluate.");

        private static PbiVersion? FindVersion(IReadOnlyList<PbiVersion> versions, Guid? id) =>
            id is Guid value ? versions.FirstOrDefault(version => version.Id == value) : null;
    }
}
