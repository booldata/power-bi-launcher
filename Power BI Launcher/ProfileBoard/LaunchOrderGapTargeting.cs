using System;
using System.Collections.Generic;

namespace PowerBILauncher;

/// <summary>
/// Axis-aligned card bounds for UI-free Launch Order gap targeting (DIP).
/// </summary>
public readonly record struct LaunchOrderRect(double X, double Y, double Width, double Height)
{
    public double Left => X;
    public double Top => Y;
    public double Right => X + Width;
    public double Bottom => Y + Height;
    public double CenterX => X + (Width / 2);
    public double CenterY => Y + (Height / 2);
}

/// <summary>
/// Pointer position for UI-free Launch Order gap targeting (DIP).
/// </summary>
public readonly record struct LaunchOrderPoint(double X, double Y);

/// <summary>
/// Resolves nearest reading-order insertion gaps from card geometry (no WinUI types).
/// </summary>
public static class LaunchOrderGapTargeting
{
    /// <summary>
    /// Returns insertion index in 0..count (gap before first … gap after last).
    /// </summary>
    public static int ResolveInsertionIndex(
        IReadOnlyList<LaunchOrderRect> cardsInReadingOrder,
        LaunchOrderPoint pointer,
        bool rightToLeft = false)
    {
        ArgumentNullException.ThrowIfNull(cardsInReadingOrder);

        int count = cardsInReadingOrder.Count;
        if (count == 0)
        {
            return 0;
        }

        // Nearest gap anchor in reading order (including when the pointer is over a card body).
        int bestIndex = 0;
        double bestDist = double.MaxValue;
        foreach ((int insertionIndex, double x, double y) in EnumerateGapAnchors(cardsInReadingOrder, rightToLeft))
        {
            double dx = pointer.X - x;
            double dy = pointer.Y - y;
            double dist = (dx * dx) + (dy * dy);
            if (dist < bestDist)
            {
                bestDist = dist;
                bestIndex = insertionIndex;
            }
        }

        return bestIndex;
    }

    /// <summary>
    /// Maps a gap insertion index to <see cref="LaunchOrder.ApplyMove"/> destination, or null when no-op.
    /// </summary>
    public static int? ResolveMoveToIndex(int fromIndex, int insertionIndex)
    {
        int destination = insertionIndex > fromIndex ? insertionIndex - 1 : insertionIndex;
        if (destination == fromIndex)
        {
            return null;
        }

        return destination;
    }

    /// <summary>
    /// Geometry for painting the insertion slot (center X, top Y, height) at <paramref name="insertionIndex"/>.
    /// </summary>
    public static (double X, double Y, double Height) ResolveGapVisual(
        IReadOnlyList<LaunchOrderRect> cardsInReadingOrder,
        int insertionIndex,
        bool rightToLeft = false)
    {
        ArgumentNullException.ThrowIfNull(cardsInReadingOrder);

        if (cardsInReadingOrder.Count == 0)
        {
            return (0, 0, 0);
        }

        if (insertionIndex <= 0)
        {
            LaunchOrderRect first = cardsInReadingOrder[0];
            double x = rightToLeft ? first.Right : first.Left;
            return (x, first.Top, first.Height);
        }

        if (insertionIndex >= cardsInReadingOrder.Count)
        {
            LaunchOrderRect last = cardsInReadingOrder[^1];
            double x = rightToLeft ? last.Left : last.Right;
            return (x, last.Top, last.Height);
        }

        LaunchOrderRect prev = cardsInReadingOrder[insertionIndex - 1];
        LaunchOrderRect next = cardsInReadingOrder[insertionIndex];
        if (IsSameRow(prev, next))
        {
            (double prevEdge, double nextEdge) = FacingEdges(prev, next);
            return (
                (prevEdge + nextEdge) / 2,
                Math.Min(prev.Top, next.Top),
                Math.Max(prev.Height, next.Height));
        }

        // Row wrap: paint at trailing edge of the previous card.
        double wrapX = rightToLeft ? prev.Left : prev.Right;
        return (wrapX, prev.Top, prev.Height);
    }

    private static IEnumerable<(int InsertionIndex, double X, double Y)> EnumerateGapAnchors(
        IReadOnlyList<LaunchOrderRect> cards,
        bool rightToLeft)
    {
        int count = cards.Count;
        LaunchOrderRect first = cards[0];
        yield return (0, rightToLeft ? first.Right : first.Left, first.CenterY);

        for (int i = 1; i < count; i++)
        {
            LaunchOrderRect prev = cards[i - 1];
            LaunchOrderRect next = cards[i];
            if (IsSameRow(prev, next))
            {
                (double prevEdge, double nextEdge) = FacingEdges(prev, next);
                yield return (i, (prevEdge + nextEdge) / 2, (prev.CenterY + next.CenterY) / 2);
            }
            else
            {
                // Row wrap: same reading-order gap has trailing-of-prev and leading-of-next anchors.
                yield return (i, rightToLeft ? prev.Left : prev.Right, prev.CenterY);
                yield return (i, rightToLeft ? next.Right : next.Left, next.CenterY);
            }
        }

        LaunchOrderRect last = cards[count - 1];
        yield return (count, rightToLeft ? last.Left : last.Right, last.CenterY);
    }

    private static bool IsSameRow(LaunchOrderRect prev, LaunchOrderRect next) =>
        Math.Abs(prev.CenterY - next.CenterY) <= Math.Max(prev.Height, next.Height) * 0.5;

    private static (double PrevEdge, double NextEdge) FacingEdges(LaunchOrderRect prev, LaunchOrderRect next) =>
        prev.CenterX <= next.CenterX
            ? (prev.Right, next.Left)
            : (prev.Left, next.Right);
}
