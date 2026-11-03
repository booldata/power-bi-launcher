namespace PowerBILauncher.Tests;

public class LaunchOrderGapTargetingTests
{
    private static LaunchOrderRect Card(double x, double y, double w = 100, double h = 120) =>
        new(x, y, w, h);

    [Fact]
    public void ResolveInsertionIndex_pointer_in_mid_row_gap_selects_that_gap()
    {
        // [A][gap][B][gap][C] - one row
        var cards = new[]
        {
            Card(0, 0),
            Card(116, 0),
            Card(232, 0)
        };
        var pointer = new LaunchOrderPoint(108, 60); // center of gap between A and B

        int insertion = LaunchOrderGapTargeting.ResolveInsertionIndex(cards, pointer);

        Assert.Equal(1, insertion);
    }

    [Fact]
    public void ResolveInsertionIndex_pointer_over_card_picks_nearest_edge_gap()
    {
        var cards = new[]
        {
            Card(0, 0),
            Card(116, 0),
            Card(232, 0)
        };

        // Over B, left half → gap before B
        Assert.Equal(1, LaunchOrderGapTargeting.ResolveInsertionIndex(cards, new LaunchOrderPoint(130, 60)));
        // Over B, right half → gap after B
        Assert.Equal(2, LaunchOrderGapTargeting.ResolveInsertionIndex(cards, new LaunchOrderPoint(200, 60)));
    }

    [Fact]
    public void ResolveInsertionIndex_nearest_gap_across_row_wrap()
    {
        // Two columns: A B / C D
        var cards = new[]
        {
            Card(0, 0),
            Card(116, 0),
            Card(0, 136),
            Card(116, 136)
        };
        // Pointer just after B (end of first row) - reading-order gap before C
        var pointer = new LaunchOrderPoint(230, 60);

        int insertion = LaunchOrderGapTargeting.ResolveInsertionIndex(cards, pointer);

        Assert.Equal(2, insertion);
    }

    [Fact]
    public void ResolveInsertionIndex_empty_returns_zero()
    {
        Assert.Equal(0, LaunchOrderGapTargeting.ResolveInsertionIndex([], new LaunchOrderPoint(10, 10)));
    }

    [Fact]
    public void ResolveInsertionIndex_single_card_picks_leading_or_trailing_gap()
    {
        var cards = new[] { Card(0, 0) };

        Assert.Equal(0, LaunchOrderGapTargeting.ResolveInsertionIndex(cards, new LaunchOrderPoint(-4, 60)));
        Assert.Equal(1, LaunchOrderGapTargeting.ResolveInsertionIndex(cards, new LaunchOrderPoint(104, 60)));
    }

    [Fact]
    public void ResolveInsertionIndex_rtl_uses_trailing_edge_as_reading_order_start()
    {
        // Visual RTL: first-in-order card is on the right.
        var cards = new[]
        {
            Card(116, 0),
            Card(0, 0)
        };
        var pointer = new LaunchOrderPoint(108, 60); // gap between them

        int insertion = LaunchOrderGapTargeting.ResolveInsertionIndex(cards, pointer, rightToLeft: true);

        Assert.Equal(1, insertion);
    }

    [Fact]
    public void ResolveMoveToIndex_noop_when_dropping_in_adjacent_self_gaps()
    {
        // Drag index 1; gaps before/after self are no-ops
        Assert.Null(LaunchOrderGapTargeting.ResolveMoveToIndex(fromIndex: 1, insertionIndex: 1));
        Assert.Null(LaunchOrderGapTargeting.ResolveMoveToIndex(fromIndex: 1, insertionIndex: 2));
    }

    [Fact]
    public void ResolveMoveToIndex_maps_insertion_past_dragged_card()
    {
        // [A,B,C] drag A (0) to gap 3 (after C) → toIndex 2
        Assert.Equal(2, LaunchOrderGapTargeting.ResolveMoveToIndex(fromIndex: 0, insertionIndex: 3));
        // drag C (2) to gap 0 → toIndex 0
        Assert.Equal(0, LaunchOrderGapTargeting.ResolveMoveToIndex(fromIndex: 2, insertionIndex: 0));
    }

    [Fact]
    public void TryApplyInsertAt_moves_and_renumbers()
    {
        var alpha = new Profile { Id = Guid.NewGuid(), Name = "Alpha", Order = 1 };
        var beta = new Profile { Id = Guid.NewGuid(), Name = "Beta", Order = 2 };
        var gamma = new Profile { Id = Guid.NewGuid(), Name = "Gamma", Order = 3 };
        var profiles = new List<Profile> { alpha, beta, gamma };

        // Move Alpha to after Gamma (insertion gap 3)
        bool changed = LaunchOrder.TryApplyInsertAt(profiles, alpha.Id, insertionIndex: 3);

        Assert.True(changed);
        Assert.Equal(new[] { beta, gamma, alpha }, profiles);
        Assert.Equal(new[] { 1, 2, 3 }, profiles.Select(p => p.Order));
    }

    [Fact]
    public void TryApplyInsertAt_noop_for_adjacent_self_gap()
    {
        var alpha = new Profile { Id = Guid.NewGuid(), Name = "Alpha", Order = 1 };
        var beta = new Profile { Id = Guid.NewGuid(), Name = "Beta", Order = 2 };
        var gamma = new Profile { Id = Guid.NewGuid(), Name = "Gamma", Order = 3 };
        var profiles = new List<Profile> { alpha, beta, gamma };

        Assert.False(LaunchOrder.TryApplyInsertAt(profiles, beta.Id, insertionIndex: 1));
        Assert.False(LaunchOrder.TryApplyInsertAt(profiles, beta.Id, insertionIndex: 2));
        Assert.Equal(new[] { alpha, beta, gamma }, profiles);
    }

    [Fact]
    public void TryApplyMoveToIndex_moves_when_origin_collapsed_gaps_use_remaining_indices()
    {
        var alpha = new Profile { Id = Guid.NewGuid(), Name = "Alpha", Order = 1 };
        var beta = new Profile { Id = Guid.NewGuid(), Name = "Beta", Order = 2 };
        var gamma = new Profile { Id = Guid.NewGuid(), Name = "Gamma", Order = 3 };
        var profiles = new List<Profile> { alpha, beta, gamma };

        // Drag Beta; among remaining [Alpha, Gamma], insert at gap 2 (after Gamma).
        bool changed = LaunchOrder.TryApplyMoveToIndex(profiles, beta.Id, toIndex: 2);

        Assert.True(changed);
        Assert.Equal(new[] { alpha, gamma, beta }, profiles);
        Assert.Equal(new[] { 1, 2, 3 }, profiles.Select(p => p.Order));
    }
}
