using System.Text.RegularExpressions;

namespace PowerBILauncher.Tests;

public class PageFrameLayoutTests
{
    [Fact]
    public void Page_padding_leaves_the_right_edge_for_the_scrollbar()
    {
        string xaml = File.ReadAllText(Path.Combine(FindRepoRoot(), "Power BI Launcher", "App.xaml"));
        Assert.Contains("<x:Double x:Key=\"FormPageMaxWidth\">800</x:Double>", xaml, StringComparison.Ordinal);
        Assert.Contains("<Thickness x:Key=\"PagePadding\">32,16,0,20</Thickness>", xaml, StringComparison.Ordinal);
        Assert.Contains("<Thickness x:Key=\"PageRightGutter\">0,0,32,0</Thickness>", xaml, StringComparison.Ordinal);
    }

    [Fact]
    public void Form_page_scrollers_put_the_thumb_on_the_window_edge()
    {
        AssertScrollerPutsThumbOnWindowEdge(
            Path.Combine(FindRepoRoot(), "Power BI Launcher", "EditProfile", "EditProfilePage.xaml"));
        AssertScrollerPutsThumbOnWindowEdge(
            Path.Combine(FindRepoRoot(), "Power BI Launcher", "Settings", "SettingsPanel.xaml"));
    }

    [Fact]
    public void Profiles_frame_stretches_its_page_to_the_pane()
    {
        string xaml = File.ReadAllText(
            Path.Combine(FindRepoRoot(), "Power BI Launcher", "Shell", "MainWindow.xaml"));
        Match frame = Regex.Match(
            xaml,
            @"<Frame\s+x:Name=""ProfilesFrame""[\s\S]*?/>",
            RegexOptions.CultureInvariant);
        Assert.True(frame.Success);
        Assert.Contains("HorizontalContentAlignment=\"Stretch\"", frame.Value, StringComparison.Ordinal);
        Assert.Contains("VerticalContentAlignment=\"Stretch\"", frame.Value, StringComparison.Ordinal);
    }

    [Fact]
    public void Profile_board_scroller_has_no_form_max_width()
    {
        string xaml = File.ReadAllText(
            Path.Combine(FindRepoRoot(), "Power BI Launcher", "ProfileBoard", "ProfileBoardPage.xaml"));
        Assert.DoesNotContain("FormPageMaxWidth", xaml, StringComparison.Ordinal);
        Match scroller = Regex.Match(
            xaml,
            @"<ScrollViewer\b[\s\S]*?</ScrollViewer>",
            RegexOptions.CultureInvariant);
        Assert.True(scroller.Success);
        AssertScrollerHasRightGutter(scroller.Value, path: "ProfileBoardPage.xaml");
    }

    [Fact]
    public void Profiles_chrome_keeps_the_right_gutter_when_the_body_border_does_not()
    {
        string xaml = File.ReadAllText(
            Path.Combine(FindRepoRoot(), "Power BI Launcher", "Shell", "MainWindow.xaml"));
        Assert.Contains("Padding=\"{StaticResource PagePadding}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Margin=\"0,0,32,24\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Margin=\"0,0,32,12\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Margin=\"0,12,32,0\"", xaml, StringComparison.Ordinal);
    }

    [Fact]
    public void Edit_profile_orders_identity_launch_folder_then_full_width_danger()
    {
        string xaml = File.ReadAllText(
            Path.Combine(FindRepoRoot(), "Power BI Launcher", "EditProfile", "EditProfilePage.xaml"));

        Assert.Contains("x:Name=\"IdentityLookCard\"", xaml, StringComparison.Ordinal);
        Assert.Contains("x:Uid=\"LaunchSection\"", xaml, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"ProfileFolderPanel\"", xaml, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"DangerZonePanel\"", xaml, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"OpenFolderButton\"", xaml, StringComparison.Ordinal);
        Assert.Contains("AutomationProperties.AutomationId=\"OpenFolderButton\"", xaml, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"DraftExtrasPanel\"", xaml, StringComparison.Ordinal);

        int identity = xaml.IndexOf("x:Name=\"IdentityLookCard\"", StringComparison.Ordinal);
        int launch = xaml.IndexOf("x:Uid=\"LaunchSection\"", StringComparison.Ordinal);
        int folder = xaml.IndexOf("x:Name=\"ProfileFolderPanel\"", StringComparison.Ordinal);
        int danger = xaml.IndexOf("x:Name=\"DangerZonePanel\"", StringComparison.Ordinal);
        int openFolder = xaml.IndexOf("OpenFolderButton", StringComparison.Ordinal);
        Assert.True(identity >= 0 && launch > identity && folder > launch && danger > folder);
        Assert.True(openFolder > folder && openFolder < danger);

        Assert.DoesNotContain("x:Name=\"AppearanceLayout\"", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("x:Uid=\"AppearanceSection\"", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("x:Name=\"DraftExtrasFooterPanel\"", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("x:Uid=\"StorageSection\"", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("CopyFolderPathButton", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("EditProfileFooterColumnGap", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain($"Width=\"{ProfileBoardLayout.CardMinWidth}\"", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain($"MinHeight=\"{ProfileBoardLayout.CardMinHeight}\"", xaml, StringComparison.Ordinal);
    }

    [Fact]
    public void Edit_profile_form_fills_available_width_up_to_form_max()
    {
        string xaml = File.ReadAllText(
            Path.Combine(FindRepoRoot(), "Power BI Launcher", "EditProfile", "EditProfilePage.xaml"));

        Assert.Contains(
            "Width=\"*\" MaxWidth=\"{StaticResource FormPageMaxWidth}\"",
            xaml,
            StringComparison.Ordinal);
        Assert.DoesNotContain("ViewportWidth", xaml, StringComparison.Ordinal);

        Match scroller = Regex.Match(
            xaml,
            @"<ScrollViewer\b[\s\S]*?</ScrollViewer>",
            RegexOptions.CultureInvariant);
        Assert.True(scroller.Success);
        Match form = Regex.Match(
            scroller.Value,
            @"<StackPanel\s+Spacing=""\{StaticResource SectionSpacing\}""[\s\S]*?>",
            RegexOptions.CultureInvariant);
        Assert.True(form.Success);
        Assert.Contains("HorizontalAlignment=\"Stretch\"", form.Value, StringComparison.Ordinal);
        Assert.DoesNotContain("MaxWidth=", form.Value, StringComparison.Ordinal);
        Assert.DoesNotContain("HorizontalAlignment=\"Left\"", form.Value, StringComparison.Ordinal);
    }

    [Fact]
    public void Edit_profile_launch_lists_version_then_identity_then_tray()
    {
        string xaml = File.ReadAllText(
            Path.Combine(FindRepoRoot(), "Power BI Launcher", "EditProfile", "EditProfilePage.xaml"));

        int launch = xaml.IndexOf("x:Uid=\"LaunchSection\"", StringComparison.Ordinal);
        int version = xaml.IndexOf("AutomationProperties.AutomationId=\"VersionComboBox\"", StringComparison.Ordinal);
        int identity = xaml.IndexOf("AutomationProperties.AutomationId=\"IdentityToggle\"", StringComparison.Ordinal);
        int tray = xaml.IndexOf("AutomationProperties.AutomationId=\"ShowOnTrayToggle\"", StringComparison.Ordinal);
        Assert.True(launch >= 0 && version > launch && identity > version && tray > identity);

        int identityCard = xaml.IndexOf("x:Name=\"IdentityLookCard\"", StringComparison.Ordinal);
        int identityCardEnd = xaml.IndexOf("x:Uid=\"LaunchSection\"", StringComparison.Ordinal);
        Assert.True(identityCard >= 0 && identityCardEnd > identityCard);
        Assert.True(tray > identityCardEnd);
    }

    [Fact]
    public void Edit_profile_uses_circle_on_tile_preview_and_pick_color_without_swatch_grid()
    {
        string xaml = File.ReadAllText(
            Path.Combine(FindRepoRoot(), "Power BI Launcher", "EditProfile", "EditProfilePage.xaml"));

        Assert.Contains("x:Name=\"CardColorPreviewBorder\"", xaml, StringComparison.Ordinal);
        Assert.Contains("AutomationProperties.AutomationId=\"CardColorPreviewBorder\"", xaml, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"AvatarPreviewHost\"", xaml, StringComparison.Ordinal);
        Assert.Contains("AutomationProperties.AutomationId=\"AvatarPreviewHost\"", xaml, StringComparison.Ordinal);
        Assert.Contains("AutomationProperties.AutomationId=\"PickCardColorButton\"", xaml, StringComparison.Ordinal);
        Assert.Contains("AutomationProperties.AutomationId=\"PickAvatarColorButton\"", xaml, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"AvatarColorPanel\"", xaml, StringComparison.Ordinal);

        int tile = xaml.IndexOf("x:Name=\"CardColorPreviewBorder\"", StringComparison.Ordinal);
        int circle = xaml.IndexOf("x:Name=\"AvatarPreviewHost\"", StringComparison.Ordinal);
        Assert.True(tile >= 0 && circle > tile);

        Assert.DoesNotContain("VariableSizedWrapGrid", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("AvatarSwatchesPanel", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("AvatarColorSwatch_", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("NoAccentRadio", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("UseCustomColorRadio", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("AvatarColorCustomButton", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("CardColorPreviewPanel", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("x:Uid=\"CardColorPreview\"", xaml, StringComparison.Ordinal);
    }

    [Fact]
    public void Edit_profile_field_help_uses_hover_i_tooltips()
    {
        string xaml = File.ReadAllText(
            Path.Combine(FindRepoRoot(), "Power BI Launcher", "EditProfile", "EditProfilePage.xaml"));

        Assert.DoesNotContain("<TeachingTip", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("VersionHelpTip", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("IdentityHelpTip", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("TrayHelpTip", xaml, StringComparison.Ordinal);

        foreach (string id in new[] { "VersionHelpButton", "IdentityHelpButton", "TrayHelpButton" })
        {
            string button = FindControl(xaml, "Button", id);
            Assert.Contains("ToolTipService.ToolTip=", button, StringComparison.Ordinal);
            Assert.DoesNotContain("Click=", button, StringComparison.Ordinal);
            Assert.Contains("Text=\"i\"", button, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void Edit_profile_card_color_reset_is_hidden_until_a_custom_color_is_chosen()
    {
        string xaml = File.ReadAllText(
            Path.Combine(FindRepoRoot(), "Power BI Launcher", "EditProfile", "EditProfilePage.xaml"));

        string reset = FindControl(xaml, "Button", "NoAccentButton");
        Assert.Contains("Glyph=\"&#xE72C;\"", reset, StringComparison.Ordinal);
        Assert.Contains("Visibility=\"Collapsed\"", reset, StringComparison.Ordinal);
        Assert.Contains("ToolTipService.ToolTip=\"Reset card color\"", reset, StringComparison.Ordinal);
        Assert.DoesNotContain("Glyph=\"&#xE94D;\"", reset, StringComparison.Ordinal);
    }

    [Fact]
    public void Reorder_mode_banner_matches_the_title_row_height()
    {
        string xaml = File.ReadAllText(
            Path.Combine(FindRepoRoot(), "Power BI Launcher", "Shell", "MainWindow.xaml"));
        Match banner = Regex.Match(
            xaml,
            @"<Border\s+x:Name=""ReorderModeBanner""[\s\S]*?>",
            RegexOptions.CultureInvariant);
        Assert.True(banner.Success);
        Assert.Contains(
            $"Height=\"{WindowLayoutConstraints.ProfilesTitleRowHeight}\"",
            banner.Value,
            StringComparison.Ordinal);
    }

    private static string FindControl(string xaml, string elementName, string automationId)
    {
        string idAttr = $"AutomationProperties.AutomationId=\"{automationId}\"";
        int idIndex = xaml.IndexOf(idAttr, StringComparison.Ordinal);
        Assert.True(idIndex >= 0, automationId);

        int start = xaml.LastIndexOf($"<{elementName}", idIndex, StringComparison.Ordinal);
        Assert.True(start >= 0, automationId);

        int emptyClose = xaml.IndexOf("/>", idIndex, StringComparison.Ordinal);
        int pairClose = xaml.IndexOf($"</{elementName}>", idIndex, StringComparison.Ordinal);
        Assert.True(emptyClose >= 0 || pairClose >= 0, automationId);

        int end = pairClose < 0 || (emptyClose >= 0 && emptyClose < pairClose)
            ? emptyClose + 2
            : pairClose + $"</{elementName}>".Length;
        return xaml[start..end];
    }

    private static void AssertScrollerPutsThumbOnWindowEdge(string path)
    {
        string xaml = File.ReadAllText(path);
        Match scroller = Regex.Match(
            xaml,
            @"<ScrollViewer\b[\s\S]*?</ScrollViewer>",
            RegexOptions.CultureInvariant);
        Assert.True(scroller.Success, path);

        int tagEnd = scroller.Value.IndexOf('>');
        Assert.True(tagEnd > 0, path);
        string openingTag = scroller.Value[..tagEnd];
        Assert.DoesNotContain("FormPageMaxWidth", openingTag, StringComparison.Ordinal);
        Assert.DoesNotContain("ViewportWidth", scroller.Value, StringComparison.Ordinal);
        Assert.DoesNotContain("Padding=", openingTag, StringComparison.Ordinal);
        AssertScrollerHasRightGutter(scroller.Value, path);
        Assert.Contains("FormPageMaxWidth", scroller.Value, StringComparison.Ordinal);
    }

    private static void AssertScrollerHasRightGutter(string xaml, string path)
    {
        Assert.True(
            xaml.Contains("Padding=\"{StaticResource PageRightGutter}\"", StringComparison.Ordinal),
            path);
    }

    private static string FindRepoRoot()
    {
        string? directory = AppContext.BaseDirectory;
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory, "Power BI Launcher.sln")))
            {
                return directory;
            }

            directory = Directory.GetParent(directory)?.FullName;
        }

        throw new DirectoryNotFoundException("Could not find the repository root from the test output directory.");
    }
}
