namespace PowerBILauncher.Tests;

public class TrayMenuCompositionTests
{
    private static Profile Profile(Guid id, string name, int order, bool showOnTray = true) => new()
    {
        Id = id,
        Name = name,
        Order = order,
        ShowOnTray = showOnTray
    };

    [Fact]
    public void Compose_show_hide_and_exit_when_setup_incomplete()
    {
        var profiles = new[]
        {
            Profile(Guid.Parse("11111111-1111-1111-1111-111111111111"), "Work", 1)
        };

        IReadOnlyList<TrayMenuEntry> entries = TrayMenuComposition.Compose(
            profiles,
            hasCompletedSetup: false,
            isWindowVisible: false);

        Assert.Equal(2, entries.Count);
        Assert.Equal(TrayMenuEntryKind.ShowWindow, entries[0].Kind);
        Assert.Equal("", entries[0].Label);
        Assert.Equal(TrayMenuEntryKind.Exit, entries[1].Kind);
        Assert.DoesNotContain(entries, e => e.Kind == TrayMenuEntryKind.LaunchProfile);
        Assert.DoesNotContain(entries, e => e.Kind == TrayMenuEntryKind.Separator);
    }

    [Fact]
    public void Compose_show_hide_and_exit_when_profile_list_empty()
    {
        IReadOnlyList<TrayMenuEntry> entries = TrayMenuComposition.Compose(
            Array.Empty<Profile>(),
            hasCompletedSetup: true,
            isWindowVisible: true);

        Assert.Equal(2, entries.Count);
        Assert.Equal(TrayMenuEntryKind.HideWindow, entries[0].Kind);
        Assert.Equal("", entries[0].Label);
        Assert.Equal(TrayMenuEntryKind.Exit, entries[1].Kind);
        Assert.DoesNotContain(entries, e => e.Kind == TrayMenuEntryKind.Separator);
    }

    [Fact]
    public void Compose_launches_separator_show_hide_then_exit_in_given_order()
    {
        var firstId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var secondId = Guid.Parse("22222222-2222-2222-2222-222222222222");
        // Input order is Launch Order; Profile.Order values are intentionally reversed
        // so accidental Order sorting would fail this test.
        var profiles = new[]
        {
            Profile(firstId, "Alpha", order: 2),
            Profile(secondId, "Beta", order: 1)
        };

        IReadOnlyList<TrayMenuEntry> entries = TrayMenuComposition.Compose(
            profiles,
            hasCompletedSetup: true,
            isWindowVisible: false);

        Assert.Equal(5, entries.Count);
        Assert.Equal(TrayMenuEntryKind.LaunchProfile, entries[0].Kind);
        Assert.Equal(firstId, entries[0].ProfileId);
        Assert.Equal("Alpha", entries[0].Label);
        Assert.Equal(TrayMenuEntryKind.LaunchProfile, entries[1].Kind);
        Assert.Equal(secondId, entries[1].ProfileId);
        Assert.Equal("Beta", entries[1].Label);
        Assert.Equal(TrayMenuEntryKind.Separator, entries[2].Kind);
        Assert.Equal(TrayMenuEntryKind.ShowWindow, entries[3].Kind);
        Assert.Equal("", entries[3].Label);
        Assert.Equal(TrayMenuEntryKind.Exit, entries[4].Kind);
        Assert.Equal("", entries[4].Label);
        Assert.DoesNotContain(entries, e => e.Label.Contains("Settings", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Compose_omits_profiles_with_show_on_tray_off()
    {
        var shownId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var hiddenId = Guid.Parse("22222222-2222-2222-2222-222222222222");
        var profiles = new[]
        {
            Profile(shownId, "Shown", order: 1, showOnTray: true),
            Profile(hiddenId, "Hidden", order: 2, showOnTray: false)
        };

        IReadOnlyList<TrayMenuEntry> entries = TrayMenuComposition.Compose(
            profiles,
            hasCompletedSetup: true,
            isWindowVisible: true);

        Assert.Equal(4, entries.Count);
        Assert.Equal(TrayMenuEntryKind.LaunchProfile, entries[0].Kind);
        Assert.Equal(shownId, entries[0].ProfileId);
        Assert.Equal(TrayMenuEntryKind.Separator, entries[1].Kind);
        Assert.Equal(TrayMenuEntryKind.HideWindow, entries[2].Kind);
        Assert.Equal("", entries[2].Label);
        Assert.Equal(TrayMenuEntryKind.Exit, entries[3].Kind);
        Assert.DoesNotContain(entries, e => e.Kind == TrayMenuEntryKind.LaunchProfile && e.ProfileId == hiddenId);
        Assert.DoesNotContain(entries, e => e.Label.Contains("Settings", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Compose_show_hide_and_exit_when_none_have_show_on_tray()
    {
        var profiles = new[]
        {
            Profile(Guid.Parse("11111111-1111-1111-1111-111111111111"), "Hidden", order: 1, showOnTray: false)
        };

        IReadOnlyList<TrayMenuEntry> entries = TrayMenuComposition.Compose(
            profiles,
            hasCompletedSetup: true,
            isWindowVisible: false);

        Assert.Equal(2, entries.Count);
        Assert.Equal(TrayMenuEntryKind.ShowWindow, entries[0].Kind);
        Assert.Equal(TrayMenuEntryKind.Exit, entries[1].Kind);
        Assert.DoesNotContain(entries, e => e.Kind == TrayMenuEntryKind.LaunchProfile);
        Assert.DoesNotContain(entries, e => e.Kind == TrayMenuEntryKind.Separator);
    }

    [Fact]
    public void Compose_launch_label_is_the_profile_name_even_when_version_is_dangling()
    {
        var id = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var profile = Profile(id, "SIXT", order: 1);
        profile.VersionId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        profile.WindowsNetworkIdentity = @"CONTOSO\JSmith";

        IReadOnlyList<TrayMenuEntry> entries = TrayMenuComposition.Compose(
            [profile],
            hasCompletedSetup: true,
            isWindowVisible: true);

        Assert.Equal(TrayMenuEntryKind.LaunchProfile, entries[0].Kind);
        Assert.Equal("SIXT", entries[0].Label);
        Assert.Equal(id, entries[0].ProfileId);
    }

    [Fact]
    public void Compose_omits_a_draft_that_is_not_in_the_persisted_list()
    {
        var persistedId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var draftId = Guid.Parse("99999999-9999-9999-9999-999999999999");
        var persisted = Profile(persistedId, "Work", order: 1);
        var draft = Profile(draftId, "Draft", order: 2);

        IReadOnlyList<TrayMenuEntry> entries = TrayMenuComposition.Compose(
            [persisted],
            hasCompletedSetup: true,
            isWindowVisible: true);

        Assert.DoesNotContain(entries, e => e.ProfileId == draft.Id);
        Assert.Contains(entries, e => e.Kind == TrayMenuEntryKind.LaunchProfile && e.ProfileId == persistedId);
        Assert.DoesNotContain(entries, e => e.Label == "Draft");
    }
}
