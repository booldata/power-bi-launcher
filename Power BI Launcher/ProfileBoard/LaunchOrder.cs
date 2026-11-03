using System;
using System.Collections.Generic;

namespace PowerBILauncher
{
    /// <summary>
    /// UI-free Launch Order apply: reorder Profiles and renumber contiguous Order values.
    /// Board drag-and-drop and tray consumers share this sequence.
    /// </summary>
    public static class LaunchOrder
    {
        /// <summary>
        /// Assigns contiguous Order values 1..n matching the current list sequence.
        /// </summary>
        public static void ApplySequence(IList<Profile> profilesInLaunchOrder)
        {
            ArgumentNullException.ThrowIfNull(profilesInLaunchOrder);

            for (int i = 0; i < profilesInLaunchOrder.Count; i++)
            {
                profilesInLaunchOrder[i].Order = i + 1;
            }
        }

        /// <summary>
        /// Moves the Profile at <paramref name="fromIndex"/> to <paramref name="toIndex"/>,
        /// then assigns contiguous Order values matching the new sequence.
        /// </summary>
        public static void ApplyMove(IList<Profile> profilesInLaunchOrder, int fromIndex, int toIndex)
        {
            ArgumentNullException.ThrowIfNull(profilesInLaunchOrder);

            if (fromIndex < 0 || fromIndex >= profilesInLaunchOrder.Count)
            {
                throw new ArgumentOutOfRangeException(nameof(fromIndex));
            }

            if (toIndex < 0 || toIndex >= profilesInLaunchOrder.Count)
            {
                throw new ArgumentOutOfRangeException(nameof(toIndex));
            }

            if (fromIndex != toIndex)
            {
                Profile moved = profilesInLaunchOrder[fromIndex];
                profilesInLaunchOrder.RemoveAt(fromIndex);
                profilesInLaunchOrder.Insert(toIndex, moved);
            }

            ApplySequence(profilesInLaunchOrder);
        }

        /// <summary>
        /// Maps a drop onto <paramref name="targetIndex"/> (before or after that card)
        /// to the <see cref="ApplyMove"/> destination index after accounting for removal.
        /// </summary>
        public static int ResolveDestinationIndex(int fromIndex, int targetIndex, bool insertAfter)
        {
            int destination = insertAfter ? targetIndex + 1 : targetIndex;
            if (fromIndex < destination)
            {
                destination--;
            }

            return destination;
        }

        /// <summary>
        /// Sorts by Order, resolves drop by Profile id, and applies contiguous Launch Order when the move changes sequence.
        /// Returns false for missing ids or no-op destinations (list left sorted by Order).
        /// </summary>
        public static bool TryApplyDropById(
            IList<Profile> profilesInLaunchOrder,
            Guid draggedId,
            Guid targetId,
            bool insertAfter)
        {
            ArgumentNullException.ThrowIfNull(profilesInLaunchOrder);

            SortByOrder(profilesInLaunchOrder);
            int fromIndex = IndexOfId(profilesInLaunchOrder, draggedId);
            int targetIndex = IndexOfId(profilesInLaunchOrder, targetId);
            if (fromIndex < 0 || targetIndex < 0)
            {
                return false;
            }

            int toIndex = ResolveDestinationIndex(fromIndex, targetIndex, insertAfter);
            if (fromIndex == toIndex)
            {
                return false;
            }

            ApplyMove(profilesInLaunchOrder, fromIndex, toIndex);
            return true;
        }

        /// <summary>
        /// Sorts by Order, moves the dragged Profile to the gap insertion index (0..count),
        /// and applies contiguous Launch Order when the move changes sequence.
        /// Returns false for missing id or no-op destinations (list left sorted by Order).
        /// </summary>
        public static bool TryApplyInsertAt(
            IList<Profile> profilesInLaunchOrder,
            Guid draggedId,
            int insertionIndex)
        {
            ArgumentNullException.ThrowIfNull(profilesInLaunchOrder);

            SortByOrder(profilesInLaunchOrder);
            int fromIndex = IndexOfId(profilesInLaunchOrder, draggedId);
            if (fromIndex < 0)
            {
                return false;
            }

            if (insertionIndex < 0 || insertionIndex > profilesInLaunchOrder.Count)
            {
                return false;
            }

            int? toIndex = LaunchOrderGapTargeting.ResolveMoveToIndex(fromIndex, insertionIndex);
            if (toIndex is null)
            {
                return false;
            }

            ApplyMove(profilesInLaunchOrder, fromIndex, toIndex.Value);
            return true;
        }

        /// <summary>
        /// Sorts by Order and moves the dragged Profile to <paramref name="toIndex"/> in the
        /// post-removal list (used when the drag origin is collapsed and gaps are measured
        /// among remaining cards). Returns false for missing id or no-op.
        /// </summary>
        public static bool TryApplyMoveToIndex(
            IList<Profile> profilesInLaunchOrder,
            Guid draggedId,
            int toIndex)
        {
            ArgumentNullException.ThrowIfNull(profilesInLaunchOrder);

            SortByOrder(profilesInLaunchOrder);
            int fromIndex = IndexOfId(profilesInLaunchOrder, draggedId);
            if (fromIndex < 0 || toIndex < 0 || toIndex >= profilesInLaunchOrder.Count)
            {
                return false;
            }

            if (fromIndex == toIndex)
            {
                return false;
            }

            ApplyMove(profilesInLaunchOrder, fromIndex, toIndex);
            return true;
        }

        /// <summary>
        /// Sorts by Order, nudges a Profile by id by <paramref name="delta"/> indices,
        /// and applies contiguous Launch Order when the move is in bounds.
        /// Returns false for zero delta, missing id, or out-of-bounds nudge (list left sorted by Order).
        /// </summary>
        public static bool TryApplyNudgeById(IList<Profile> profilesInLaunchOrder, Guid profileId, int delta)
        {
            ArgumentNullException.ThrowIfNull(profilesInLaunchOrder);

            if (delta == 0)
            {
                return false;
            }

            SortByOrder(profilesInLaunchOrder);
            int fromIndex = IndexOfId(profilesInLaunchOrder, profileId);
            if (fromIndex < 0)
            {
                return false;
            }

            int toIndex = fromIndex + delta;
            if (toIndex < 0 || toIndex >= profilesInLaunchOrder.Count)
            {
                return false;
            }

            ApplyMove(profilesInLaunchOrder, fromIndex, toIndex);
            return true;
        }

        private static void SortByOrder(IList<Profile> profiles)
        {
            for (int i = 1; i < profiles.Count; i++)
            {
                Profile key = profiles[i];
                int j = i - 1;
                while (j >= 0 && profiles[j].Order > key.Order)
                {
                    profiles[j + 1] = profiles[j];
                    j--;
                }

                profiles[j + 1] = key;
            }
        }

        private static int IndexOfId(IList<Profile> profiles, Guid id)
        {
            for (int i = 0; i < profiles.Count; i++)
            {
                if (profiles[i].Id == id)
                {
                    return i;
                }
            }

            return -1;
        }
    }
}
