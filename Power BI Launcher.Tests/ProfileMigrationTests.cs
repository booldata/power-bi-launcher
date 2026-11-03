using System.Collections.Generic;

namespace PowerBILauncher.Tests;

public class ProfileMigrationTests
{
    private static List<Profile> Single(Profile profile) => new() { profile };

    [Fact]
    public void MigrateInPlace_returns_false_when_no_legacy_values()
    {
        var profile = new Profile
        {
            CardColor = "",
            AvatarColor = "#4F6BED",
            IconPath = "",
            ShowOnTray = true
        };

        bool dirty = ProfileMigration.MigrateInPlace(Single(profile), () => "#123456");

        Assert.False(dirty);
        Assert.Equal("", profile.CardColor);
        Assert.Equal("#4F6BED", profile.AvatarColor);
        Assert.Equal("", profile.IconPath);
        Assert.True(profile.ShowOnTray);
    }

    [Fact]
    public void MigrateInPlace_sets_missing_show_on_tray_to_true_and_dirties()
    {
        var profile = new Profile
        {
            CardColor = "",
            AvatarColor = "#4F6BED",
            IconPath = "",
            ShowOnTray = null
        };

        bool dirty = ProfileMigration.MigrateInPlace(Single(profile), () => "#123456");

        Assert.True(dirty);
        Assert.True(profile.ShowOnTray);
    }

    [Fact]
    public void MigrateInPlace_leaves_explicit_show_on_tray_true_alone()
    {
        var profile = new Profile
        {
            CardColor = "",
            AvatarColor = "#4F6BED",
            IconPath = "",
            ShowOnTray = true
        };

        bool dirty = ProfileMigration.MigrateInPlace(Single(profile), () => "#123456");

        Assert.False(dirty);
        Assert.True(profile.ShowOnTray);
    }

    [Fact]
    public void MigrateInPlace_leaves_explicit_show_on_tray_false_alone()
    {
        var profile = new Profile
        {
            CardColor = "",
            AvatarColor = "#4F6BED",
            IconPath = "",
            ShowOnTray = false
        };

        bool dirty = ProfileMigration.MigrateInPlace(Single(profile), () => "#123456");

        Assert.False(dirty);
        Assert.False(profile.ShowOnTray);
    }

    [Fact]
    public void MigrateInPlace_clears_legacy_white_card_color()
    {
        var profile = new Profile
        {
            CardColor = "#FFFFFF",
            AvatarColor = "#4F6BED",
            IconPath = ""
        };

        bool dirty = ProfileMigration.MigrateInPlace(Single(profile), () => "#123456");

        Assert.True(dirty);
        Assert.Equal("", profile.CardColor);
    }

    [Fact]
    public void MigrateInPlace_assigns_avatar_color_when_empty()
    {
        var profile = new Profile
        {
            CardColor = "",
            AvatarColor = "",
            IconPath = ""
        };

        bool dirty = ProfileMigration.MigrateInPlace(Single(profile), () => "#ABCDEF");

        Assert.True(dirty);
        Assert.Equal("#ABCDEF", profile.AvatarColor);
    }

    [Fact]
    public void MigrateInPlace_clears_legacy_pbi_png_icon_path()
    {
        var profile = new Profile
        {
            CardColor = "",
            AvatarColor = "#4F6BED",
            IconPath = @"C:\Apps\Power BI Launcher\pbi.png"
        };

        bool dirty = ProfileMigration.MigrateInPlace(Single(profile), () => "#123456");

        Assert.True(dirty);
        Assert.Equal("", profile.IconPath);
    }

    [Fact]
    public void MigrateInPlace_migrates_all_legacy_fields_in_one_pass()
    {
        var profile = new Profile
        {
            CardColor = "#FFFFFF",
            AvatarColor = "",
            IconPath = @"D:\old\pbi.png"
        };

        bool dirty = ProfileMigration.MigrateInPlace(Single(profile), () => "#CAFE00");

        Assert.True(dirty);
        Assert.Equal("", profile.CardColor);
        Assert.Equal("#CAFE00", profile.AvatarColor);
        Assert.Equal("", profile.IconPath);
    }
}
