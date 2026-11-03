namespace PowerBILauncher.Tests;

public class WindowLayoutConstraintsTests
{
    [Fact]
    public void PageGutter_is_32_on_the_4_epx_grid()
    {
        Assert.Equal(32, WindowLayoutConstraints.PageGutter);
    }

    [Fact]
    public void FormPageMaxWidth_is_800()
    {
        Assert.Equal(800, WindowLayoutConstraints.FormPageMaxWidth);
    }

    [Fact]
    public void FormFitWidth_is_864()
    {
        Assert.Equal(864, WindowLayoutConstraints.FormFitWidth);
    }

    [Fact]
    public void DefaultWidth_is_815_two_column_board()
    {
        Assert.Equal(815, WindowLayoutConstraints.DefaultWidth);
        Assert.Equal(751, WindowLayoutConstraints.TwoColumnMaxContentWidth);
        Assert.Equal(
            2,
            ProfileBoardLayout.ColumnsThatFit(
                WindowLayoutConstraints.DefaultWidth - WindowLayoutConstraints.BodyHorizontalPadding));
        Assert.Equal(
            3,
            ProfileBoardLayout.ColumnsThatFit(
                WindowLayoutConstraints.DefaultWidth - WindowLayoutConstraints.BodyHorizontalPadding + 1));
    }

    [Fact]
    public void Body_border_right_padding_is_zero_so_the_scrollbar_reaches_the_window_edge()
    {
        Assert.Equal(0, WindowLayoutConstraints.BodyBorderPaddingRight);
        Assert.Equal(
            WindowLayoutConstraints.PageGutter,
            WindowLayoutConstraints.BodyBorderPaddingRight + WindowLayoutConstraints.BodyPaddingRight);
    }

    [Fact]
    public void Board_content_width_subtracts_the_overlay_gutter_from_the_scroller()
    {
        Assert.Equal(
            WindowLayoutConstraints.SingleColumnMaxScrollerWidth,
            WindowLayoutConstraints.BoardContentWidth(
                WindowLayoutConstraints.MinWidth - WindowLayoutConstraints.BodyPaddingLeft));
    }

    [Fact]
    public void CardMinWidth_is_240_on_the_4_epx_grid()
    {
        Assert.Equal(240, ProfileBoardLayout.CardMinWidth);
        Assert.Equal(16, ProfileBoardLayout.CardGap);
        Assert.Equal(5, ProfileBoardLayout.MaxColumns);
    }

    [Fact]
    public void MinWidth_is_559_single_column_board_breakpoint()
    {
        Assert.Equal(559, WindowLayoutConstraints.MinWidth);
    }

    [Fact]
    public void MinWidth_is_two_cards_plus_gutters_minus_one_dip()
    {
        // Two columns need 2*CardMinWidth + CardGap; one less DIP keeps a single column.
        double twoCardsPlusGutters =
            (2 * ProfileBoardLayout.CardMinWidth)
            + ProfileBoardLayout.CardGap
            + (2 * WindowLayoutConstraints.PageGutter);

        Assert.Equal(twoCardsPlusGutters - 1, WindowLayoutConstraints.MinWidth);
        Assert.Equal(560, twoCardsPlusGutters);
        Assert.Equal(64, WindowLayoutConstraints.BodyHorizontalPadding);
        Assert.Equal(
            WindowLayoutConstraints.PageGutter * 2,
            WindowLayoutConstraints.BodyHorizontalPadding);
    }

    [Fact]
    public void MinWidth_derives_from_board_layout_and_page_gutter()
    {
        double singleColumnScrollerWidth =
            (2 * ProfileBoardLayout.CardMinWidth) + ProfileBoardLayout.CardGap - 1;

        Assert.Equal(
            singleColumnScrollerWidth + WindowLayoutConstraints.BodyHorizontalPadding,
            WindowLayoutConstraints.MinWidth);
        Assert.Equal(495, singleColumnScrollerWidth);
    }

    [Fact]
    public void MinHeight_equals_named_profiles_chrome_plus_one_card_row()
    {
        double expected =
            WindowLayoutConstraints.ShellNavRowPaddingTop
            + WindowLayoutConstraints.ShellNavHeight
            + WindowLayoutConstraints.BodyPaddingTop
            + WindowLayoutConstraints.ProfilesTitleRowHeight
            + WindowLayoutConstraints.ProfilesTitleToBoardGap
            + ProfileBoardLayout.CardMinHeight
            + WindowLayoutConstraints.BodyPaddingBottom;

        Assert.Equal(expected, WindowLayoutConstraints.MinHeight);
        Assert.Equal(120, ProfileBoardLayout.CardMinHeight);
    }
}
