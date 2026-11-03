using System;
using System.Collections.Generic;
using System.Linq;

namespace PowerBILauncher;

/// <summary>
/// UI-free uncommitted Version registry: draft Versions, Default Version, in-progress rename,
/// and Scan staging. Delegates every rule to <see cref="ProfileLifecycle"/>.
/// </summary>
public sealed class VersionRegistryDraft
{
    private readonly ProfileLifecycle _lifecycle;
    private List<PbiVersion> _draftVersions = new();
    private Guid? _draftDefaultVersionId;
    private Guid? _renamingVersionId;
    private IReadOnlyList<ScanAcceptedVersion> _scanFound = Array.Empty<ScanAcceptedVersion>();
    private bool _staging;

    public VersionRegistryDraft(ProfileLifecycle lifecycle)
    {
        _lifecycle = lifecycle ?? throw new ArgumentNullException(nameof(lifecycle));
    }

    public IReadOnlyList<PbiVersion> Versions => _draftVersions;

    public Guid? DefaultVersionId => _draftDefaultVersionId;

    public Guid? RenamingVersionId => _renamingVersionId;

    public bool NeedsDefaultVersion => _draftDefaultVersionId is null;

    public bool IsStaging => _staging;

    public IReadOnlyList<ScanAcceptedVersion> ScanFound => _scanFound;

    public void Reset()
    {
        _draftVersions = CloneVersions(_lifecycle.Settings.Versions);
        _draftDefaultVersionId = _lifecycle.Settings.DefaultVersionId;
        _renamingVersionId = null;
        ClearStaging();
    }

    public bool TryRegister(string path, out VersionRegistryErrorKind? errorKind)
    {
        Guid? defaultVersionId = _draftDefaultVersionId;
        if (!_lifecycle.TryRegisterVersion(_draftVersions, ref defaultVersionId, path, out errorKind))
        {
            return false;
        }

        _draftDefaultVersionId = defaultVersionId;
        _renamingVersionId = null;
        return true;
    }

    public IReadOnlyList<ScanAcceptedVersion> BeginScan()
    {
        _scanFound = _lifecycle.ScanInstalledVersions();
        _staging = true;
        return _scanFound;
    }

    public void ConfirmScan(IReadOnlySet<string> selectedPaths)
    {
        ArgumentNullException.ThrowIfNull(selectedPaths);
        Guid? defaultVersionId = _draftDefaultVersionId;
        _lifecycle.ConfirmScan(_scanFound, selectedPaths, _draftVersions, ref defaultVersionId);
        _draftDefaultVersionId = defaultVersionId;
        _renamingVersionId = null;
        ClearStaging();
    }

    public void CancelScan()
    {
        _renamingVersionId = null;
        ClearStaging();
    }

    public void BeginRename(Guid id) =>
        _renamingVersionId = id;

    public bool TryCommitRename(Guid id, string name, out VersionRegistryErrorKind? errorKind)
    {
        if (!ProfileLifecycle.TryRenameVersion(_draftVersions, id, name, out errorKind))
        {
            return false;
        }

        _renamingVersionId = null;
        return true;
    }

    public void CancelRename() =>
        _renamingVersionId = null;

    public bool TrySetDefault(Guid id, out VersionRegistryErrorKind? errorKind)
    {
        Guid? defaultVersionId = _draftDefaultVersionId;
        if (!ProfileLifecycle.TrySetDefaultVersion(_draftVersions, ref defaultVersionId, id, out errorKind))
        {
            return false;
        }

        _draftDefaultVersionId = defaultVersionId;
        return true;
    }

    public IReadOnlyList<VersionRegistryRow> ProjectRows() =>
        _draftVersions
            .Select(version => new VersionRegistryRow(
                version,
                isDefault: version.Id == _draftDefaultVersionId,
                isRenaming: version.Id == _renamingVersionId,
                versionCount: _draftVersions.Count))
            .ToList();

    public bool TryRemove(Guid id, out VersionRegistryErrorKind? errorKind)
    {
        Guid? defaultVersionId = _draftDefaultVersionId;
        if (!ProfileLifecycle.TryRemoveVersion(_draftVersions, ref defaultVersionId, id, out errorKind))
        {
            return false;
        }

        _draftDefaultVersionId = defaultVersionId;
        if (_renamingVersionId == id)
        {
            _renamingVersionId = null;
        }

        return true;
    }

    public IReadOnlyList<SettingsIssue> Commit(
        string profilesFolder,
        MinimizeButtonBehavior minimizeBehavior,
        CloseButtonBehavior closeBehavior)
    {
        IReadOnlyList<SettingsIssue> errors = _lifecycle.CollectSettingsSaveErrors(
            profilesFolder,
            _draftVersions,
            _draftDefaultVersionId);
        if (errors.Count > 0)
        {
            return errors;
        }

        if (!_lifecycle.TryEnsureProfilesRoot(profilesFolder, out SettingsIssue? folderIssue))
        {
            return new[] { folderIssue! };
        }

        Setting settings = _lifecycle.Settings;
        string previousFolder = settings.UserProfilePath;
        List<PbiVersion> previousVersions = settings.Versions;
        Guid? previousDefaultVersionId = settings.DefaultVersionId;
        MinimizeButtonBehavior previousMinimize = settings.MinimizeBehavior;
        CloseButtonBehavior previousClose = settings.CloseBehavior;

        settings.UserProfilePath = profilesFolder;
        settings.Versions = CloneVersions(_draftVersions);
        settings.DefaultVersionId = _draftDefaultVersionId;
        settings.MinimizeBehavior = minimizeBehavior;
        settings.CloseBehavior = closeBehavior;
        if (!_lifecycle.TrySaveSettings(out string? osDetail))
        {
            settings.UserProfilePath = previousFolder;
            settings.Versions = previousVersions;
            settings.DefaultVersionId = previousDefaultVersionId;
            settings.MinimizeBehavior = previousMinimize;
            settings.CloseBehavior = previousClose;
            return new[] { new SettingsIssue { Kind = SettingsIssueKind.SaveFailed, OsDetail = osDetail } };
        }

        return Array.Empty<SettingsIssue>();
    }

    private void ClearStaging()
    {
        _staging = false;
        _scanFound = Array.Empty<ScanAcceptedVersion>();
    }

    private static List<PbiVersion> CloneVersions(IEnumerable<PbiVersion> versions) =>
        versions.Select(version => new PbiVersion
        {
            Id = version.Id,
            DisplayName = version.DisplayName,
            Path = version.Path,
            Kind = version.Kind
        }).ToList();
}
