using System.Text.Json;

namespace PowerBILauncher.Tests;

public class VersionRegistryDraftTests : IDisposable
{
    private static readonly string DesktopPath =
        @"D:\Tools\PBIDesktop\bin\PBIDesktop.exe";
    private static readonly string ReportServerPath =
        @"C:\Program Files\Microsoft Power BI Desktop RS\bin\PBIDesktop.exe";
    private static readonly string BrowsePath =
        @"D:\Archive\PBIDesktop.exe";

    private readonly string _baseDir;
    private readonly string _cwdDir;
    private readonly string _profilesRoot;

    public VersionRegistryDraftTests()
    {
        string run = Guid.NewGuid().ToString("N").Substring(0, 8);
        _baseDir = Path.Combine(Path.GetTempPath(), "pbi-draft-" + run);
        _cwdDir = Path.Combine(Path.GetTempPath(), "pbi-draft-cwd-" + run);
        _profilesRoot = Path.Combine(_baseDir, "Profiles");
        Directory.CreateDirectory(_baseDir);
        Directory.CreateDirectory(_cwdDir);
        Directory.CreateDirectory(_profilesRoot);
    }

    public void Dispose()
    {
        TryDelete(_baseDir);
        TryDelete(_cwdDir);
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, recursive: true);
            }
        }
        catch
        {
        }
    }

    private ProfileLifecycle CreateLifecycle(
        IPbiExecutableInspector inspector,
        IPbiDiscovery? discovery = null) =>
        new(_baseDir, _cwdDir, inspector, discovery: discovery);

    private (ProfileLifecycle Lifecycle, VersionRegistryDraft Draft) OpenDraft(
        StubExecutableInspector inspector,
        StubPbiDiscovery? discovery = null)
    {
        ProfileLifecycle lifecycle = CreateLifecycle(inspector, discovery);
        lifecycle.Load();
        lifecycle.Settings.UserProfilePath = _profilesRoot;
        var draft = new VersionRegistryDraft(lifecycle);
        draft.Reset();
        return (lifecycle, draft);
    }

    [Fact]
    public void Reset_loads_saved_versions_and_default_and_drops_staging_and_rename()
    {
        var inspector = new StubExecutableInspector();
        inspector.Pass(DesktopPath);
        var discovery = new StubPbiDiscovery();
        (ProfileLifecycle lifecycle, VersionRegistryDraft draft) = OpenDraft(inspector, discovery);
        Assert.True(draft.TryRegister(DesktopPath, out _));
        IReadOnlyList<SettingsIssue> saved = draft.Commit(
            _profilesRoot,
            MinimizeButtonBehavior.Minimize,
            CloseButtonBehavior.Close);
        Assert.Empty(saved);

        Guid savedId = lifecycle.Settings.Versions[0].Id;
        draft.BeginRename(savedId);
        draft.BeginScan();
        Assert.True(draft.IsStaging);
        Assert.Equal(savedId, draft.RenamingVersionId);

        draft.Reset();

        Assert.False(draft.IsStaging);
        Assert.Null(draft.RenamingVersionId);
        Assert.Single(draft.Versions);
        Assert.Equal(savedId, draft.Versions[0].Id);
        Assert.Equal(savedId, draft.DefaultVersionId);
    }

    [Fact]
    public void Draft_edits_do_not_reach_settings_or_disk_until_commit()
    {
        var inspector = new StubExecutableInspector();
        inspector.Pass(DesktopPath);
        (ProfileLifecycle lifecycle, VersionRegistryDraft draft) = OpenDraft(inspector);

        Assert.True(draft.TryRegister(DesktopPath, out _));
        Assert.Single(draft.Versions);
        Assert.Empty(lifecycle.Settings.Versions);
        Assert.False(lifecycle.Settings.HasCompletedSetup);
        Assert.False(File.Exists(lifecycle.SettingsFilePath));

        draft.Reset();

        Assert.Empty(draft.Versions);
        Assert.Null(draft.DefaultVersionId);
        Assert.Empty(lifecycle.Settings.Versions);
    }

    [Fact]
    public void Reset_after_edits_restores_saved_values()
    {
        var inspector = new StubExecutableInspector();
        inspector.Pass(DesktopPath);
        inspector.Pass(BrowsePath);
        (ProfileLifecycle lifecycle, VersionRegistryDraft draft) = OpenDraft(inspector);
        Assert.True(draft.TryRegister(DesktopPath, out _));
        Assert.Empty(draft.Commit(
            _profilesRoot,
            MinimizeButtonBehavior.Minimize,
            CloseButtonBehavior.Close));
        Guid savedId = lifecycle.Settings.Versions[0].Id;

        Assert.True(draft.TryRegister(BrowsePath, out _));
        Assert.Equal(2, draft.Versions.Count);

        draft.Reset();

        Assert.Single(draft.Versions);
        Assert.Equal(savedId, draft.Versions[0].Id);
        Assert.Equal(savedId, draft.DefaultVersionId);
        Assert.Single(lifecycle.Settings.Versions);
    }

    [Fact]
    public void Browse_registers_a_passing_executable_and_makes_it_the_default()
    {
        var inspector = new StubExecutableInspector();
        inspector.Pass(DesktopPath);
        (_, VersionRegistryDraft draft) = OpenDraft(inspector);

        Assert.True(draft.TryRegister(DesktopPath, out VersionRegistryErrorKind? error));

        Assert.Null(error);
        Assert.Single(draft.Versions);
        Assert.Equal(DesktopPath, draft.Versions[0].Path);
        Assert.Equal(draft.Versions[0].Id, draft.DefaultVersionId);
        Assert.False(draft.NeedsDefaultVersion);
    }

    [Fact]
    public void Browse_rejects_a_failing_executable_with_the_existing_error_kind()
    {
        var inspector = new StubExecutableInspector();
        inspector.NotPowerBi(DesktopPath, "notepad.exe");
        (_, VersionRegistryDraft draft) = OpenDraft(inspector);

        Assert.False(draft.TryRegister(DesktopPath, out VersionRegistryErrorKind? error));

        Assert.Equal(VersionRegistryErrorKind.NotPowerBiDesktop, error);
        Assert.Empty(draft.Versions);
        Assert.Null(draft.DefaultVersionId);
    }

    [Fact]
    public void Scan_stages_candidates_without_registering()
    {
        var inspector = new StubExecutableInspector();
        inspector.Pass(DesktopPath);
        var discovery = new StubPbiDiscovery();
        discovery.Candidates.Add(new PbiDiscoveryCandidate { Path = DesktopPath, DisplayHint = "May 2026" });
        (_, VersionRegistryDraft draft) = OpenDraft(inspector, discovery);

        IReadOnlyList<ScanAcceptedVersion> found = draft.BeginScan();

        Assert.True(draft.IsStaging);
        Assert.Single(found);
        Assert.Equal(DesktopPath, found[0].Path);
        Assert.Empty(draft.Versions);
        Assert.Null(draft.DefaultVersionId);
    }

    [Fact]
    public void Confirm_scan_registers_selected_paths_and_removes_deselected_discoveries()
    {
        var inspector = new StubExecutableInspector();
        inspector.Pass(DesktopPath);
        inspector.PassReportServer(ReportServerPath);
        inspector.Pass(BrowsePath);
        var discovery = new StubPbiDiscovery();
        discovery.Candidates.Add(new PbiDiscoveryCandidate { Path = DesktopPath, DisplayHint = "May 2026" });
        discovery.Candidates.Add(new PbiDiscoveryCandidate { Path = ReportServerPath, DisplayHint = "September 2025" });
        (_, VersionRegistryDraft draft) = OpenDraft(inspector, discovery);
        Assert.True(draft.TryRegister(DesktopPath, out _));
        Assert.True(draft.TryRegister(BrowsePath, out _));
        Guid browseId = draft.Versions.Single(version =>
            string.Equals(version.Path, BrowsePath, StringComparison.OrdinalIgnoreCase)).Id;

        draft.BeginScan();
        draft.ConfirmScan(new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ReportServerPath });

        Assert.False(draft.IsStaging);
        Assert.Null(draft.RenamingVersionId);
        Assert.DoesNotContain(draft.Versions, version =>
            string.Equals(version.Path, DesktopPath, StringComparison.OrdinalIgnoreCase));
        Assert.Contains(draft.Versions, version =>
            string.Equals(version.Path, ReportServerPath, StringComparison.OrdinalIgnoreCase));
        Assert.Contains(draft.Versions, version => version.Id == browseId);
    }

    [Fact]
    public void Cancel_scan_leaves_the_draft_untouched_and_clears_rename()
    {
        var inspector = new StubExecutableInspector();
        inspector.Pass(DesktopPath);
        var discovery = new StubPbiDiscovery();
        discovery.Candidates.Add(new PbiDiscoveryCandidate { Path = DesktopPath });
        (_, VersionRegistryDraft draft) = OpenDraft(inspector, discovery);
        Assert.True(draft.TryRegister(DesktopPath, out _));
        Guid id = draft.Versions[0].Id;
        draft.BeginRename(id);

        draft.BeginScan();
        draft.CancelScan();

        Assert.False(draft.IsStaging);
        Assert.Null(draft.RenamingVersionId);
        Assert.Single(draft.Versions);
        Assert.Equal(id, draft.Versions[0].Id);
        Assert.Equal(id, draft.DefaultVersionId);
    }

    [Fact]
    public void Confirm_scan_clears_rename_state()
    {
        var inspector = new StubExecutableInspector();
        inspector.Pass(DesktopPath);
        var discovery = new StubPbiDiscovery();
        discovery.Candidates.Add(new PbiDiscoveryCandidate { Path = DesktopPath });
        (_, VersionRegistryDraft draft) = OpenDraft(inspector, discovery);
        Assert.True(draft.TryRegister(DesktopPath, out _));
        draft.BeginRename(draft.Versions[0].Id);

        draft.BeginScan();
        draft.ConfirmScan(new HashSet<string>(StringComparer.OrdinalIgnoreCase) { DesktopPath });

        Assert.Null(draft.RenamingVersionId);
        Assert.False(draft.IsStaging);
    }

    [Fact]
    public void Rename_commits_and_rejects_empty_while_leaving_the_rename_open()
    {
        var inspector = new StubExecutableInspector();
        inspector.Pass(DesktopPath);
        inspector.Pass(BrowsePath);
        (_, VersionRegistryDraft draft) = OpenDraft(inspector);
        Assert.True(draft.TryRegister(DesktopPath, out _));
        Assert.True(draft.TryRegister(BrowsePath, out _));
        Guid first = draft.Versions[0].Id;
        Guid second = draft.Versions[1].Id;

        draft.BeginRename(first);
        Assert.Equal(first, draft.RenamingVersionId);
        draft.BeginRename(second);
        Assert.Equal(second, draft.RenamingVersionId);

        Assert.False(draft.TryCommitRename(second, "   ", out VersionRegistryErrorKind? emptyError));
        Assert.Equal(VersionRegistryErrorKind.NameEmpty, emptyError);
        Assert.Equal(second, draft.RenamingVersionId);

        Assert.True(draft.TryCommitRename(second, "  Client Desktop  ", out VersionRegistryErrorKind? ok));
        Assert.Null(ok);
        Assert.Null(draft.RenamingVersionId);
        Assert.Equal("Client Desktop", draft.Versions[1].DisplayName);
    }

    [Fact]
    public void Project_rows_marks_the_renaming_version_until_commit()
    {
        var inspector = new StubExecutableInspector();
        inspector.Pass(DesktopPath);
        inspector.Pass(BrowsePath);
        (_, VersionRegistryDraft draft) = OpenDraft(inspector);
        Assert.True(draft.TryRegister(DesktopPath, out _));
        Assert.True(draft.TryRegister(BrowsePath, out _));
        Guid first = draft.Versions[0].Id;
        Guid second = draft.Versions[1].Id;

        draft.BeginRename(second);
        IReadOnlyList<VersionRegistryRow> rows = draft.ProjectRows();
        Assert.False(rows.Single(row => row.Id == first).IsRenaming);
        Assert.True(rows.Single(row => row.Id == second).IsRenaming);

        Assert.True(draft.TryCommitRename(second, "Client Desktop", out _));
        rows = draft.ProjectRows();
        Assert.All(rows, row => Assert.False(row.IsRenaming));
        Assert.Equal("Client Desktop", rows.Single(row => row.Id == second).DisplayName);
    }

    [Fact]
    public void Set_default_moves_the_default_version()
    {
        var inspector = new StubExecutableInspector();
        inspector.Pass(DesktopPath);
        inspector.Pass(BrowsePath);
        (_, VersionRegistryDraft draft) = OpenDraft(inspector);
        Assert.True(draft.TryRegister(DesktopPath, out _));
        Assert.True(draft.TryRegister(BrowsePath, out _));
        Guid first = draft.Versions[0].Id;
        Guid second = draft.Versions[1].Id;
        Assert.Equal(first, draft.DefaultVersionId);

        Assert.True(draft.TrySetDefault(second, out VersionRegistryErrorKind? error));

        Assert.Null(error);
        Assert.Equal(second, draft.DefaultVersionId);
        Assert.False(draft.NeedsDefaultVersion);
    }

    [Fact]
    public void Project_rows_marks_exactly_one_default_and_moves_it()
    {
        var inspector = new StubExecutableInspector();
        inspector.Pass(DesktopPath);
        inspector.Pass(BrowsePath);
        (_, VersionRegistryDraft draft) = OpenDraft(inspector);
        Assert.True(draft.TryRegister(DesktopPath, out _));
        Assert.True(draft.TryRegister(BrowsePath, out _));
        Guid first = draft.Versions[0].Id;
        Guid second = draft.Versions[1].Id;

        IReadOnlyList<VersionRegistryRow> rows = draft.ProjectRows();
        Assert.Equal(2, rows.Count);
        Assert.Equal(first, Assert.Single(rows, row => row.IsDefault).Id);
        Assert.False(rows.Single(row => row.Id == second).IsDefault);

        Assert.True(draft.TrySetDefault(second, out _));

        rows = draft.ProjectRows();
        Assert.Equal(second, Assert.Single(rows, row => row.IsDefault).Id);
        Assert.False(rows.Single(row => row.Id == first).IsDefault);
    }

    [Fact]
    public void Project_rows_blocks_remove_for_the_default_when_other_versions_exist()
    {
        var inspector = new StubExecutableInspector();
        inspector.Pass(DesktopPath);
        inspector.Pass(BrowsePath);
        (_, VersionRegistryDraft draft) = OpenDraft(inspector);
        Assert.True(draft.TryRegister(DesktopPath, out _));
        Assert.True(draft.TryRegister(BrowsePath, out _));
        Guid first = draft.Versions[0].Id;
        Guid second = draft.Versions[1].Id;

        IReadOnlyList<VersionRegistryRow> rows = draft.ProjectRows();

        Assert.False(rows.Single(row => row.Id == first).CanRemove);
        Assert.True(rows.Single(row => row.Id == second).CanRemove);
    }

    [Fact]
    public void Project_rows_allows_remove_when_the_default_is_the_only_version()
    {
        var inspector = new StubExecutableInspector();
        inspector.Pass(DesktopPath);
        (_, VersionRegistryDraft draft) = OpenDraft(inspector);
        Assert.True(draft.TryRegister(DesktopPath, out _));

        VersionRegistryRow row = Assert.Single(draft.ProjectRows());

        Assert.True(row.IsDefault);
        Assert.True(row.CanRemove);
    }

    [Fact]
    public void Remove_drops_the_version_and_clears_a_matching_rename()
    {
        var inspector = new StubExecutableInspector();
        inspector.Pass(DesktopPath);
        inspector.Pass(BrowsePath);
        (_, VersionRegistryDraft draft) = OpenDraft(inspector);
        Assert.True(draft.TryRegister(DesktopPath, out _));
        Assert.True(draft.TryRegister(BrowsePath, out _));
        Guid first = draft.Versions[0].Id;
        Guid second = draft.Versions[1].Id;
        draft.BeginRename(second);

        Assert.True(draft.TryRemove(second, out VersionRegistryErrorKind? error));

        Assert.Null(error);
        Assert.Single(draft.Versions);
        Assert.Equal(first, draft.Versions[0].Id);
        Assert.Equal(first, draft.DefaultVersionId);
        Assert.Null(draft.RenamingVersionId);
    }

    [Fact]
    public void Remove_of_the_last_version_clears_the_default()
    {
        var inspector = new StubExecutableInspector();
        inspector.Pass(DesktopPath);
        (_, VersionRegistryDraft draft) = OpenDraft(inspector);
        Assert.True(draft.TryRegister(DesktopPath, out _));

        Assert.True(draft.TryRemove(draft.Versions[0].Id, out _));

        Assert.Empty(draft.Versions);
        Assert.Null(draft.DefaultVersionId);
        Assert.True(draft.NeedsDefaultVersion);
    }

    [Fact]
    public void Commit_returns_folder_empty_without_writing()
    {
        var inspector = new StubExecutableInspector();
        inspector.Pass(DesktopPath);
        (ProfileLifecycle lifecycle, VersionRegistryDraft draft) = OpenDraft(inspector);
        Assert.True(draft.TryRegister(DesktopPath, out _));

        IReadOnlyList<SettingsIssue> issues = draft.Commit(
            "",
            MinimizeButtonBehavior.Minimize,
            CloseButtonBehavior.Close);

        Assert.Equal(SettingsIssueKind.FolderEmpty, Assert.Single(issues).Kind);
        Assert.False(lifecycle.Settings.HasCompletedSetup);
        Assert.Empty(lifecycle.Settings.Versions);
        Assert.False(File.Exists(lifecycle.SettingsFilePath));
    }

    [Fact]
    public void Commit_returns_unusable_folder_without_writing()
    {
        var inspector = new StubExecutableInspector();
        inspector.Pass(DesktopPath);
        (ProfileLifecycle lifecycle, VersionRegistryDraft draft) = OpenDraft(inspector);
        Assert.True(draft.TryRegister(DesktopPath, out _));
        string filePath = Path.Combine(_baseDir, "not-a-folder");
        File.WriteAllText(filePath, "nope");

        IReadOnlyList<SettingsIssue> issues = draft.Commit(
            filePath,
            MinimizeButtonBehavior.Minimize,
            CloseButtonBehavior.Close);

        Assert.Equal(SettingsIssueKind.FolderMissing, Assert.Single(issues).Kind);
        Assert.False(lifecycle.Settings.HasCompletedSetup);
        Assert.Empty(lifecycle.Settings.Versions);
    }

    [Fact]
    public void Commit_returns_path_budget_without_writing()
    {
        var inspector = new StubExecutableInspector();
        inspector.Pass(DesktopPath);
        (ProfileLifecycle lifecycle, VersionRegistryDraft draft) = OpenDraft(inspector);
        Assert.True(draft.TryRegister(DesktopPath, out _));
        int maxDesktopRoot = PowerBiPathBudget.MaxProfilesRootLength(PbiVersionKind.Desktop);
        string longRoot = Path.Combine(_profilesRoot, new string('p', maxDesktopRoot + 1 - _profilesRoot.Length - 1));
        Directory.CreateDirectory(longRoot);

        IReadOnlyList<SettingsIssue> issues = draft.Commit(
            longRoot,
            MinimizeButtonBehavior.Minimize,
            CloseButtonBehavior.Close);

        Assert.Contains(issues, issue => issue.Kind == SettingsIssueKind.PathTooLong);
        Assert.False(lifecycle.Settings.HasCompletedSetup);
        Assert.Empty(lifecycle.Settings.Versions);
    }

    [Fact]
    public void Commit_returns_no_default_without_writing()
    {
        var inspector = new StubExecutableInspector();
        (ProfileLifecycle lifecycle, VersionRegistryDraft draft) = OpenDraft(inspector);

        IReadOnlyList<SettingsIssue> issues = draft.Commit(
            _profilesRoot,
            MinimizeButtonBehavior.Minimize,
            CloseButtonBehavior.Close);

        Assert.Contains(issues, issue => issue.Kind == SettingsIssueKind.DefaultVersionRequired);
        Assert.False(lifecycle.Settings.HasCompletedSetup);
        Assert.False(File.Exists(lifecycle.SettingsFilePath));
    }

    [Fact]
    public void Commit_returns_stale_executable_without_writing()
    {
        var inspector = new StubExecutableInspector();
        inspector.Pass(DesktopPath);
        (ProfileLifecycle lifecycle, VersionRegistryDraft draft) = OpenDraft(inspector);
        Assert.True(draft.TryRegister(DesktopPath, out _));
        inspector.NotPowerBi(DesktopPath, "notepad.exe");

        IReadOnlyList<SettingsIssue> issues = draft.Commit(
            _profilesRoot,
            MinimizeButtonBehavior.Minimize,
            CloseButtonBehavior.Close);

        Assert.Contains(issues, issue => issue.Kind == SettingsIssueKind.VersionNotPowerBiDesktop);
        Assert.False(lifecycle.Settings.HasCompletedSetup);
        Assert.Empty(lifecycle.Settings.Versions);
    }

    [Fact]
    public void Commit_returns_multiple_issues_together()
    {
        var inspector = new StubExecutableInspector();
        inspector.Pass(DesktopPath);
        (_, VersionRegistryDraft draft) = OpenDraft(inspector);
        Assert.True(draft.TryRegister(DesktopPath, out _));
        inspector.NotPowerBi(DesktopPath, "notepad.exe");
        int maxDesktopRoot = PowerBiPathBudget.MaxProfilesRootLength(PbiVersionKind.Desktop);
        string longRoot = Path.Combine(_profilesRoot, new string('p', maxDesktopRoot + 1 - _profilesRoot.Length - 1));
        Directory.CreateDirectory(longRoot);

        IReadOnlyList<SettingsIssue> issues = draft.Commit(
            longRoot,
            MinimizeButtonBehavior.Minimize,
            CloseButtonBehavior.Close);

        Assert.Contains(issues, issue => issue.Kind == SettingsIssueKind.VersionNotPowerBiDesktop);
        Assert.Contains(issues, issue => issue.Kind == SettingsIssueKind.PathTooLong);
    }

    [Fact]
    public void Commit_returns_save_failed_and_keeps_the_saved_settings_when_the_write_fails()
    {
        var inspector = new StubExecutableInspector();
        inspector.Pass(DesktopPath);
        (ProfileLifecycle lifecycle, VersionRegistryDraft draft) = OpenDraft(inspector);
        string savedFolder = lifecycle.Settings.UserProfilePath;
        Assert.True(draft.TryRegister(DesktopPath, out _));
        Directory.CreateDirectory(lifecycle.SettingsFilePath);

        IReadOnlyList<SettingsIssue> issues = draft.Commit(
            Path.Combine(_baseDir, "new-root"),
            MinimizeButtonBehavior.SendToTray,
            CloseButtonBehavior.SendToTray);

        SettingsIssue issue = Assert.Single(issues);
        Assert.Equal(SettingsIssueKind.SaveFailed, issue.Kind);
        Assert.False(string.IsNullOrEmpty(issue.OsDetail));
        Assert.False(lifecycle.Settings.HasCompletedSetup);
        Assert.Empty(lifecycle.Settings.Versions);
        Assert.Null(lifecycle.Settings.DefaultVersionId);
        Assert.Equal(savedFolder, lifecycle.Settings.UserProfilePath);
        Assert.Equal(MinimizeButtonBehavior.Minimize, lifecycle.Settings.MinimizeBehavior);
        Assert.Equal(CloseButtonBehavior.Close, lifecycle.Settings.CloseBehavior);
        Assert.Single(draft.Versions);
    }

    [Fact]
    public void Commit_creates_a_missing_profiles_root_and_persists_settings()
    {
        var inspector = new StubExecutableInspector();
        inspector.Pass(DesktopPath);
        (ProfileLifecycle lifecycle, VersionRegistryDraft draft) = OpenDraft(inspector);
        Assert.True(draft.TryRegister(DesktopPath, out _));
        string missing = Path.Combine(_baseDir, "new-root");
        Assert.False(Directory.Exists(missing));

        IReadOnlyList<SettingsIssue> issues = draft.Commit(
            missing,
            MinimizeButtonBehavior.SendToTray,
            CloseButtonBehavior.SendToTray);

        Assert.Empty(issues);
        Assert.True(Directory.Exists(missing));
        Assert.Equal(missing, lifecycle.Settings.UserProfilePath);
        Assert.Equal(MinimizeButtonBehavior.SendToTray, lifecycle.Settings.MinimizeBehavior);
        Assert.Equal(CloseButtonBehavior.SendToTray, lifecycle.Settings.CloseBehavior);
        Assert.Single(lifecycle.Settings.Versions);
        Assert.Equal(DesktopPath, lifecycle.Settings.Versions[0].Path);
        Assert.Equal(lifecycle.Settings.Versions[0].Id, lifecycle.Settings.DefaultVersionId);
        Assert.True(lifecycle.Settings.HasCompletedSetup);

        string json = File.ReadAllText(lifecycle.SettingsFilePath);
        Setting? reloaded = JsonSerializer.Deserialize<Setting>(json, ProfileLifecycle.SettingsJsonOptions);
        Assert.NotNull(reloaded);
        Assert.Equal(missing, reloaded.UserProfilePath);
        Assert.True(reloaded.HasCompletedSetup);
        Assert.Equal(MinimizeButtonBehavior.SendToTray, reloaded.MinimizeBehavior);
        Assert.Equal(CloseButtonBehavior.SendToTray, reloaded.CloseBehavior);
    }
}
