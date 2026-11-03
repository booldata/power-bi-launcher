using System;
using System.Collections.Generic;
using System.IO;

namespace PowerBILauncher.Tests;

public class ProfileValidationTests
{
    private static Profile Profile(Guid id, string name) => new()
    {
        Id = id,
        Name = name
    };

    [Fact]
    public void TryValidateProfileName_rejects_empty_name()
    {
        var profiles = new List<Profile>();

        ProfileValidationKind? error = ProfileValidation.TryValidateProfileName(profiles, "   ");

        Assert.Equal(ProfileValidationKind.NameEmpty, error);
    }

    [Fact]
    public void TryValidateProfileName_rejects_duplicate_name_case_insensitive()
    {
        var existingId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var profiles = new List<Profile>
        {
            Profile(existingId, "Work")
        };

        ProfileValidationKind? error = ProfileValidation.TryValidateProfileName(profiles, "work");

        Assert.Equal(ProfileValidationKind.NameDuplicate, error);
    }

    [Fact]
    public void TryValidateProfileName_allows_same_name_when_editing_same_profile()
    {
        var existingId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var profiles = new List<Profile>
        {
            Profile(existingId, "Work")
        };

        ProfileValidationKind? error = ProfileValidation.TryValidateProfileName(profiles, "Work", existingId);

        Assert.Null(error);
    }

    [Fact]
    public void TryValidateProfileName_rejects_duplicate_when_editing_different_profile()
    {
        var profileA = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var profileB = Guid.Parse("22222222-2222-2222-2222-222222222222");
        var profiles = new List<Profile>
        {
            Profile(profileA, "Work"),
            Profile(profileB, "Personal")
        };

        ProfileValidationKind? error = ProfileValidation.TryValidateProfileName(profiles, "WORK", profileB);

        Assert.Equal(ProfileValidationKind.NameDuplicate, error);
    }

    [Fact]
    public void TryValidateProfileName_accepts_unique_trimmed_name()
    {
        var profiles = new List<Profile>
        {
            Profile(Guid.NewGuid(), "Work")
        };

        ProfileValidationKind? error = ProfileValidation.TryValidateProfileName(profiles, "  Personal  ");

        Assert.Null(error);
    }

    [Fact]
    public void TryValidateProfileName_blocks_empty_and_duplicate_for_a_new_draft_id()
    {
        var existingId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var draftId = Guid.Parse("33333333-3333-3333-3333-333333333333");
        var profiles = new List<Profile>
        {
            Profile(existingId, "Work")
        };

        Assert.Equal(
            ProfileValidationKind.NameEmpty,
            ProfileValidation.TryValidateProfileName(profiles, "", draftId));
        Assert.Equal(
            ProfileValidationKind.NameDuplicate,
            ProfileValidation.TryValidateProfileName(profiles, "work", draftId));
        Assert.Null(ProfileValidation.TryValidateProfileName(profiles, "Personal", draftId));
    }
}

public class SettingsValidationTests
{
    private static PbiVersion DesktopVersion() => new()
    {
        Id = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
        DisplayName = "Desktop",
        Path = @"D:\Tools\PBIDesktop\bin\PBIDesktop.exe",
        Kind = PbiVersionKind.Desktop
    };

    private static PbiVersion ReportServerVersion() => new()
    {
        Id = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"),
        DisplayName = "Report Server",
        Path = @"D:\Tools\PBIReportServer\bin\PBIDesktop.exe",
        Kind = PbiVersionKind.ReportServer
    };

    [Fact]
    public void TryValidateSettings_rejects_blank_folder_and_missing_default()
    {
        IReadOnlyList<SettingsIssue> errors = SettingsValidation.TryValidateSettings(
            "   ",
            null,
            Array.Empty<PbiVersion>());

        Assert.Equal(2, errors.Count);
        Assert.Contains(errors, issue => issue.Kind == SettingsIssueKind.FolderEmpty);
        Assert.Contains(errors, issue => issue.Kind == SettingsIssueKind.DefaultVersionRequired);
    }

    [Fact]
    public void TryValidateSettings_allows_a_missing_folder_that_save_can_create()
    {
        string missingFolder = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        PbiVersion version = DesktopVersion();

        IReadOnlyList<SettingsIssue> errors = SettingsValidation.TryValidateSettings(
            missingFolder,
            version.Id,
            new[] { version });

        Assert.Empty(errors);
    }

    [Fact]
    public void TryValidateProfilesFolder_rejects_empty_path()
    {
        SettingsIssue? issue = SettingsValidation.TryValidateProfilesFolder("   ", [DesktopVersion()]);

        Assert.NotNull(issue);
        Assert.Equal(SettingsIssueKind.FolderEmpty, issue.Kind);
    }

    [Theory]
    [InlineData("profiles")]
    [InlineData(@".\profiles")]
    [InlineData(@"..\profiles")]
    [InlineData(@"C:profiles")]
    public void TryValidateProfilesFolder_rejects_relative_path(string path)
    {
        SettingsIssue? issue = SettingsValidation.TryValidateProfilesFolder(path, [DesktopVersion()]);

        Assert.NotNull(issue);
        Assert.Equal(SettingsIssueKind.FolderRelative, issue.Kind);
    }

    [Theory]
    [InlineData(@"C:\bad|path")]
    [InlineData(@"C:\bad*path")]
    [InlineData(@"C:\bad?path")]
    [InlineData(@"C:\bad""path")]
    public void TryValidateProfilesFolder_rejects_invalid_characters(string path)
    {
        SettingsIssue? issue = SettingsValidation.TryValidateProfilesFolder(path, [DesktopVersion()]);

        Assert.NotNull(issue);
        Assert.Equal(SettingsIssueKind.FolderInvalidCharacters, issue.Kind);
    }

    [Fact]
    public void TryValidateProfilesFolder_accepts_valid_absolute_path()
    {
        SettingsIssue? issue = SettingsValidation.TryValidateProfilesFolder(
            @"C:\Profiles",
            [DesktopVersion()]);

        Assert.Null(issue);
    }

    [Fact]
    public void TryValidateProfilesFolder_rejects_path_over_desktop_budget()
    {
        int maxRoot = PowerBiPathBudget.MaxProfilesRootLength(PbiVersionKind.Desktop);
        string over = @"C:\" + new string('p', maxRoot - 2);

        SettingsIssue? issue = SettingsValidation.TryValidateProfilesFolder(over, [DesktopVersion()]);

        Assert.NotNull(issue);
        Assert.Equal(SettingsIssueKind.PathTooLong, issue.Kind);
        Assert.NotNull(issue.Budget);
        Assert.Equal(PbiVersionKind.Desktop, issue.Budget.Kind);
        Assert.Equal(maxRoot, issue.Budget.MaxLength);
        Assert.Equal(over.Length, issue.Budget.ActualLength);
    }

    [Fact]
    public void TryValidateProfilesFolder_uses_report_server_budget_when_that_kind_is_registered()
    {
        int desktopMax = PowerBiPathBudget.MaxProfilesRootLength(PbiVersionKind.Desktop);
        int reportServerMax = PowerBiPathBudget.MaxProfilesRootLength(PbiVersionKind.ReportServer);
        string overReportServer = @"C:\" + new string('p', reportServerMax - 2);

        Assert.Null(SettingsValidation.TryValidateProfilesFolder(overReportServer, [DesktopVersion()]));

        SettingsIssue? issue = SettingsValidation.TryValidateProfilesFolder(
            overReportServer,
            [ReportServerVersion()]);

        Assert.NotNull(issue);
        Assert.Equal(SettingsIssueKind.PathTooLong, issue.Kind);
        Assert.NotNull(issue.Budget);
        Assert.Equal(PbiVersionKind.ReportServer, issue.Budget.Kind);
        Assert.Equal(reportServerMax, issue.Budget.MaxLength);
        Assert.True(overReportServer.Length > reportServerMax);
        Assert.True(overReportServer.Length <= desktopMax);
    }

    [Fact]
    public void TryValidateSettings_requires_a_default_version()
    {
        IReadOnlyList<SettingsIssue> errors = SettingsValidation.TryValidateSettings(
            Path.GetTempPath(),
            null,
            Array.Empty<PbiVersion>());

        Assert.Contains(errors, issue => issue.Kind == SettingsIssueKind.DefaultVersionRequired);
    }

    [Fact]
    public void TryValidateSettings_accepts_existing_folder_with_a_default_version()
    {
        PbiVersion version = DesktopVersion();

        IReadOnlyList<SettingsIssue> errors = SettingsValidation.TryValidateSettings(
            Path.GetTempPath(),
            version.Id,
            new[] { version });

        Assert.Empty(errors);
    }

    [Theory]
    [InlineData("Minimize", MinimizeButtonBehavior.Minimize, true)]
    [InlineData("SendToTray", MinimizeButtonBehavior.SendToTray, true)]
    [InlineData("invalid", MinimizeButtonBehavior.Minimize, false)]
    [InlineData(null, MinimizeButtonBehavior.Minimize, false)]
    public void TryParseMinimizeBehavior_parses_combo_tags(
        string? tag,
        MinimizeButtonBehavior expected,
        bool expectedSuccess)
    {
        bool success = SettingsValidation.TryParseMinimizeBehavior(tag, out MinimizeButtonBehavior behavior);

        Assert.Equal(expectedSuccess, success);
        Assert.Equal(expected, behavior);
    }

    [Theory]
    [InlineData("Close", CloseButtonBehavior.Close, true)]
    [InlineData("SendToTray", CloseButtonBehavior.SendToTray, true)]
    [InlineData("invalid", CloseButtonBehavior.Close, false)]
    [InlineData(null, CloseButtonBehavior.Close, false)]
    public void TryParseCloseBehavior_parses_combo_tags(
        string? tag,
        CloseButtonBehavior expected,
        bool expectedSuccess)
    {
        bool success = SettingsValidation.TryParseCloseBehavior(tag, out CloseButtonBehavior behavior);

        Assert.Equal(expectedSuccess, success);
        Assert.Equal(expected, behavior);
    }
}

public class LogoValidationTests
{
    [Theory]
    [InlineData("logo.png")]
    [InlineData("logo.PNG")]
    [InlineData("logo.jpg")]
    [InlineData("logo.jpeg")]
    [InlineData("logo.ico")]
    [InlineData("logo.bmp")]
    public void TryValidateLogoExtension_accepts_supported_formats(string fileName)
    {
        LogoValidationKind? error = LogoValidation.TryValidateLogoExtension(fileName, out _);

        Assert.Null(error);
    }

    [Theory]
    [InlineData("logo.gif")]
    [InlineData("logo.webp")]
    [InlineData("logo")]
    public void TryValidateLogoExtension_rejects_unsupported_formats(string fileName)
    {
        LogoValidationKind? error = LogoValidation.TryValidateLogoExtension(fileName, out _);

        Assert.Equal(LogoValidationKind.UnsupportedFormat, error);
    }

    [Fact]
    public void TryValidateLogoExtension_rejects_empty_path()
    {
        LogoValidationKind? error = LogoValidation.TryValidateLogoExtension("   ", out string? extension);

        Assert.Equal(LogoValidationKind.PathEmpty, error);
        Assert.Null(extension);
    }
}
