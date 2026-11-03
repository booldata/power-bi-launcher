namespace PowerBILauncher.Tests;

public class VersionSelectionTests
{
    private static readonly Guid DesktopId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid ReportServerId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid DanglingId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");

    private static PbiVersion Desktop() => new()
    {
        Id = DesktopId,
        DisplayName = "Desktop · May 2026",
        Path = @"D:\PBI\PBIDesktop.exe",
        Kind = PbiVersionKind.Desktop
    };

    private static PbiVersion ReportServer() => new()
    {
        Id = ReportServerId,
        DisplayName = "Report Server · September 2025",
        Path = @"D:\PBIRS\PBIDesktop.exe",
        Kind = PbiVersionKind.ReportServer
    };

    [Fact]
    public void BuildOptions_lists_default_first_with_its_display_name()
    {
        IReadOnlyList<VersionChoiceOption> options = VersionSelection.BuildOptions(
            [Desktop(), ReportServer()],
            defaultVersionId: DesktopId,
            selectedVersionId: null);

        Assert.Equal(3, options.Count);
        Assert.True(options[0].IsFollowDefault);
        Assert.Equal(VersionSelectionKind.FollowDefault, options[0].Kind);
        Assert.Null(options[0].VersionId);
        Assert.Equal("Desktop · May 2026", options[0].Label);
        Assert.Equal(DesktopId, options[1].VersionId);
        Assert.Equal("Desktop · May 2026", options[1].Label);
        Assert.Equal(ReportServerId, options[2].VersionId);
        Assert.Equal("Report Server · September 2025", options[2].Label);
        Assert.All(options, option => Assert.False(option.IsPlaceholder));
    }

    [Fact]
    public void BuildOptions_empty_registry_is_a_disabled_placeholder()
    {
        IReadOnlyList<VersionChoiceOption> options = VersionSelection.BuildOptions(
            [],
            defaultVersionId: null,
            selectedVersionId: null);

        Assert.Single(options);
        Assert.True(options[0].IsPlaceholder);
        Assert.Equal(VersionSelectionKind.EmptyRegistry, options[0].Kind);
        Assert.Equal("", options[0].Label);
        Assert.Equal(VersionSelectionKind.EmptyRegistry, VersionSelection.Evaluate(null, [], null));
    }

    [Fact]
    public void BuildOptions_keeps_a_dangling_selection_visible()
    {
        IReadOnlyList<VersionChoiceOption> options = VersionSelection.BuildOptions(
            [Desktop()],
            defaultVersionId: DesktopId,
            selectedVersionId: DanglingId);

        Assert.Equal(VersionSelectionKind.Dangling, VersionSelection.Evaluate(DanglingId, [Desktop()], DesktopId));
        Assert.Equal(DanglingId, options[^1].VersionId);
        Assert.True(options[^1].IsPlaceholder);
        Assert.Equal(VersionSelectionKind.Dangling, options[^1].Kind);
        Assert.Equal("", options[^1].Label);
    }

    [Fact]
    public void Evaluate_follow_default_and_explicit_are_ready()
    {
        Assert.Equal(
            VersionSelectionKind.FollowDefault,
            VersionSelection.Evaluate(null, [Desktop()], DesktopId));
        Assert.Equal(
            VersionSelectionKind.Explicit,
            VersionSelection.Evaluate(ReportServerId, [Desktop(), ReportServer()], DesktopId));
    }

    [Fact]
    public void VersionIdToSave_keeps_the_pinned_version_when_the_registry_is_empty()
    {
        Assert.Equal(DanglingId, VersionSelection.VersionIdToSave(null, DanglingId, []));
    }

    [Fact]
    public void VersionIdToSave_uses_the_selection_when_the_registry_has_versions()
    {
        Assert.Null(VersionSelection.VersionIdToSave(null, DanglingId, [Desktop()]));
        Assert.Equal(DesktopId, VersionSelection.VersionIdToSave(DesktopId, ReportServerId, [Desktop(), ReportServer()]));
    }

    [Fact]
    public void Pinned_profile_with_an_empty_registry_opens_clean()
    {
        var snapshot = new Profile { Name = "Work", AvatarColor = "#4F6BED", ShowOnTray = true, VersionId = DanglingId };
        Guid? selectedVersionId = VersionSelection.BuildOptions([], null, DanglingId)[0].VersionId;

        bool dirty = EditProfileDirtyState.IsDirty(
            snapshot,
            "Work",
            currentShowOnTray: true,
            currentIconPath: "",
            currentAvatarColor: "#4F6BED",
            currentCardColor: "",
            VersionSelection.VersionIdToSave(selectedVersionId, snapshot.VersionId, []));

        Assert.False(dirty);
    }
}
