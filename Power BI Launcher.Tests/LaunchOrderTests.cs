namespace PowerBILauncher.Tests;

public class LaunchOrderTests
{
    private static Profile Profile(string name, int order, Guid? id = null) => new()
    {
        Id = id ?? Guid.NewGuid(),
        Name = name,
        Order = order
    };

    [Fact]
    public void ApplySequence_assigns_contiguous_orders_matching_list_order()
    {
        var alpha = Profile("Alpha", order: 10);
        var beta = Profile("Beta", order: 3);
        var gamma = Profile("Gamma", order: 99);
        var profiles = new List<Profile> { alpha, beta, gamma };

        LaunchOrder.ApplySequence(profiles);

        Assert.Equal(1, alpha.Order);
        Assert.Equal(2, beta.Order);
        Assert.Equal(3, gamma.Order);
    }

    [Fact]
    public void ApplyMove_reorders_then_assigns_contiguous_orders()
    {
        var alpha = Profile("Alpha", order: 1);
        var beta = Profile("Beta", order: 2);
        var gamma = Profile("Gamma", order: 3);
        var profiles = new List<Profile> { alpha, beta, gamma };

        // Move Alpha (index 0) to after Gamma (toIndex 2 → ends at last).
        LaunchOrder.ApplyMove(profiles, fromIndex: 0, toIndex: 2);

        Assert.Equal(new[] { beta, gamma, alpha }, profiles);
        Assert.Equal(1, beta.Order);
        Assert.Equal(2, gamma.Order);
        Assert.Equal(3, alpha.Order);
    }

    [Fact]
    public void ApplyMove_moving_later_item_earlier_renumbers()
    {
        var alpha = Profile("Alpha", order: 1);
        var beta = Profile("Beta", order: 2);
        var gamma = Profile("Gamma", order: 3);
        var profiles = new List<Profile> { alpha, beta, gamma };

        LaunchOrder.ApplyMove(profiles, fromIndex: 2, toIndex: 0);

        Assert.Equal(new[] { gamma, alpha, beta }, profiles);
        Assert.Equal(1, gamma.Order);
        Assert.Equal(2, alpha.Order);
        Assert.Equal(3, beta.Order);
    }

    [Fact]
    public void ApplySequence_empty_list_is_noop()
    {
        var profiles = new List<Profile>();

        LaunchOrder.ApplySequence(profiles);

        Assert.Empty(profiles);
    }

    [Fact]
    public void ResolveDestinationIndex_insert_before_moves_earlier_item_in_front_of_target()
    {
        // [A,B,C,D] drag A before C → [B,A,C,D]
        Assert.Equal(1, LaunchOrder.ResolveDestinationIndex(fromIndex: 0, targetIndex: 2, insertAfter: false));
    }

    [Fact]
    public void ResolveDestinationIndex_insert_after_moves_earlier_item_behind_target()
    {
        // [A,B,C,D] drag A after C → [B,C,A,D]
        Assert.Equal(2, LaunchOrder.ResolveDestinationIndex(fromIndex: 0, targetIndex: 2, insertAfter: true));
    }

    [Fact]
    public void ResolveDestinationIndex_insert_before_moves_later_item_in_front_of_target()
    {
        // [A,B,C,D] drag D before B → [A,D,B,C]
        Assert.Equal(1, LaunchOrder.ResolveDestinationIndex(fromIndex: 3, targetIndex: 1, insertAfter: false));
    }

    [Fact]
    public void ResolveDestinationIndex_insert_after_moves_later_item_behind_target()
    {
        // [A,B,C,D] drag D after B → [A,B,D,C]
        Assert.Equal(2, LaunchOrder.ResolveDestinationIndex(fromIndex: 3, targetIndex: 1, insertAfter: true));
    }

    [Fact]
    public void ResolveDestinationIndex_noop_when_drop_does_not_change_order()
    {
        // [A,B,C] drag B before B / after A → stays at 1
        Assert.Equal(1, LaunchOrder.ResolveDestinationIndex(fromIndex: 1, targetIndex: 1, insertAfter: false));
        Assert.Equal(1, LaunchOrder.ResolveDestinationIndex(fromIndex: 1, targetIndex: 0, insertAfter: true));
    }

    [Fact]
    public void TryApplyDropById_moves_dragged_after_target_and_renumbers()
    {
        var alpha = Profile("Alpha", order: 1);
        var beta = Profile("Beta", order: 2);
        var gamma = Profile("Gamma", order: 3);
        var profiles = new List<Profile> { alpha, beta, gamma };

        bool changed = LaunchOrder.TryApplyDropById(profiles, alpha.Id, gamma.Id, insertAfter: true);

        Assert.True(changed);
        Assert.Equal(new[] { beta, gamma, alpha }, profiles);
        Assert.Equal(1, beta.Order);
        Assert.Equal(2, gamma.Order);
        Assert.Equal(3, alpha.Order);
    }

    [Fact]
    public void TryApplyDropById_noop_when_drop_does_not_change_order()
    {
        var alpha = Profile("Alpha", order: 1);
        var beta = Profile("Beta", order: 2);
        var gamma = Profile("Gamma", order: 3);
        var profiles = new List<Profile> { alpha, beta, gamma };

        bool changed = LaunchOrder.TryApplyDropById(profiles, beta.Id, beta.Id, insertAfter: false);

        Assert.False(changed);
        Assert.Equal(new[] { alpha, beta, gamma }, profiles);
        Assert.Equal(1, alpha.Order);
        Assert.Equal(2, beta.Order);
        Assert.Equal(3, gamma.Order);
    }

    [Fact]
    public void TryApplyDropById_missing_id_is_noop()
    {
        var alpha = Profile("Alpha", order: 1);
        var beta = Profile("Beta", order: 2);
        var profiles = new List<Profile> { alpha, beta };

        bool changed = LaunchOrder.TryApplyDropById(profiles, alpha.Id, Guid.NewGuid(), insertAfter: true);

        Assert.False(changed);
        Assert.Equal(new[] { alpha, beta }, profiles);
        Assert.Equal(1, alpha.Order);
        Assert.Equal(2, beta.Order);
    }

    [Fact]
    public void TryApplyDropById_sorts_by_order_before_resolving_ids()
    {
        var alpha = Profile("Alpha", order: 1);
        var beta = Profile("Beta", order: 2);
        var gamma = Profile("Gamma", order: 3);
        // List intentionally not in Launch Order sequence.
        var profiles = new List<Profile> { gamma, alpha, beta };

        bool changed = LaunchOrder.TryApplyDropById(profiles, alpha.Id, gamma.Id, insertAfter: true);

        Assert.True(changed);
        Assert.Equal(new[] { beta, gamma, alpha }, profiles);
        Assert.Equal(1, beta.Order);
        Assert.Equal(2, gamma.Order);
        Assert.Equal(3, alpha.Order);
    }

    [Fact]
    public void TryApplyNudgeById_moves_by_delta_and_renumbers()
    {
        var alpha = Profile("Alpha", order: 1);
        var beta = Profile("Beta", order: 2);
        var gamma = Profile("Gamma", order: 3);
        var profiles = new List<Profile> { alpha, beta, gamma };

        bool changed = LaunchOrder.TryApplyNudgeById(profiles, alpha.Id, delta: 1);

        Assert.True(changed);
        Assert.Equal(new[] { beta, alpha, gamma }, profiles);
        Assert.Equal(1, beta.Order);
        Assert.Equal(2, alpha.Order);
        Assert.Equal(3, gamma.Order);
    }

    [Fact]
    public void TryApplyNudgeById_first_up_and_last_down_are_noop()
    {
        var alpha = Profile("Alpha", order: 1);
        var beta = Profile("Beta", order: 2);
        var gamma = Profile("Gamma", order: 3);
        var profiles = new List<Profile> { alpha, beta, gamma };

        Assert.False(LaunchOrder.TryApplyNudgeById(profiles, alpha.Id, delta: -1));
        Assert.False(LaunchOrder.TryApplyNudgeById(profiles, gamma.Id, delta: 1));
        Assert.Equal(new[] { alpha, beta, gamma }, profiles);
        Assert.Equal(1, alpha.Order);
        Assert.Equal(2, beta.Order);
        Assert.Equal(3, gamma.Order);
    }

    [Fact]
    public void TryApplyNudgeById_zero_delta_and_missing_id_are_noop()
    {
        var alpha = Profile("Alpha", order: 1);
        var beta = Profile("Beta", order: 2);
        var profiles = new List<Profile> { alpha, beta };

        Assert.False(LaunchOrder.TryApplyNudgeById(profiles, alpha.Id, delta: 0));
        Assert.False(LaunchOrder.TryApplyNudgeById(profiles, Guid.NewGuid(), delta: 1));
        Assert.Equal(new[] { alpha, beta }, profiles);
        Assert.Equal(1, alpha.Order);
        Assert.Equal(2, beta.Order);
    }

    [Fact]
    public void TryApplyNudgeById_down_then_up_leaves_contiguous_orders()
    {
        var alpha = Profile("Alpha", order: 1);
        var beta = Profile("Beta", order: 2);
        var gamma = Profile("Gamma", order: 3);
        var profiles = new List<Profile> { alpha, beta, gamma };

        // Beta down → [Alpha, Gamma, Beta]; Gamma up → [Gamma, Alpha, Beta]
        Assert.True(LaunchOrder.TryApplyNudgeById(profiles, beta.Id, delta: 1));
        Assert.True(LaunchOrder.TryApplyNudgeById(profiles, gamma.Id, delta: -1));

        Assert.Equal(new[] { gamma, alpha, beta }, profiles);
        Assert.Equal(new[] { 1, 2, 3 }, profiles.Select(p => p.Order));
    }
}
